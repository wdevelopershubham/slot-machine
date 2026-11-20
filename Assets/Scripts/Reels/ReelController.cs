using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SlotGame.Core;

namespace SlotGame.Reels
{
    /// <summary>
    /// One scrolling reel. It shows a few pooled symbol cells inside a masked window and
    /// animates a virtual scroll position (measured in symbols) through four phases:
    ///   Spin-up (ease-in) -> Full-speed loop -> Deceleration (ease-out cubic)
    ///   -> Settle (small overshoot, then ease back onto the final symbol).
    ///
    /// The reel never decides the result: the game manager picks the winning symbol with the RNG
    /// and tells the reel where to land via <see cref="StopAt"/>. The reel only makes that
    /// outcome look natural.
    ///
    /// Coordinate model: "row" r is an integer slot on an endless vertical strip. Row r is drawn at
    /// y = (r - position) * cellHeight, so a growing position scrolls symbols DOWN, and the row
    /// nearest position sits on the payline (centre of the window).
    /// </summary>
    public sealed class ReelController : MonoBehaviour
    {
        private enum Phase { Idle, SpinningUp, Spinning, Decelerating, Settling }

        [Header("References")]
        [Tooltip("Child of the masked window; its centre is the payline.")]
        [SerializeField] private RectTransform stripRoot;
        [Tooltip("Prefab with an Image used for each pooled symbol cell.")]
        [SerializeField] private Image symbolCellPrefab;

        [Header("Layout")]
        [Tooltip("Vertical distance between symbols in UI pixels.")]
        [SerializeField] private float cellHeight = 100f;

        [Header("Motion blur (visual only)")]
        [SerializeField, Range(0f, 0.6f)] private float maxBlurStretch = 0.3f;

        // 5 cells cover rows [floor(p)-2 .. floor(p)+2], enough for a ~2.1-symbol-tall window.
        private const int PoolSize = 5;
        private const int PoolHalf = 2;
        // Length of the decorative loop shown while spinning. Positions wrap modulo this.
        private const int StripLength = 24;

        private SlotConfig config;
        private Image[] cells;
        private int[] cellRows;               // which row each pooled cell currently shows
        private int[] strip;                  // symbol index per row (periodic)

        private Phase phase = Phase.Idle;
        private float position;               // scroll position in rows
        private float speed;                  // rows per second (for blur only once decelerating)
        private float phaseTimer;

        // Deceleration / settle bookkeeping
        private float decelStart, decelDistance, decelDuration;
        private int targetRow, forcedSymbol;
        private int forcedRow = int.MinValue;
        private float overshootPosition;

        private Coroutine pulseRoutine;
        private Image pulsingCell;

        /// <summary>Raised once the reel is perfectly still on its target symbol.</summary>
        public event Action<ReelController> Stopped;

        public bool IsSpinning => phase != Phase.Idle;

        /// <summary>Builds the cell pool and the decorative symbol strip.</summary>
        public void Initialize(SlotConfig slotConfig, int startingSymbol)
        {
            config = slotConfig;
            cells = new Image[PoolSize];
            cellRows = new int[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                cells[i] = Instantiate(symbolCellPrefab, stripRoot);
                cells[i].gameObject.name = "Cell_" + i;
                cellRows[i] = int.MinValue; // forces the first refresh
            }

            // The strip only affects what blurs past while spinning, so Unity's Random is fine here.
            // Real outcomes come from IRandomService.
            strip = new int[StripLength];
            int count = config.Symbols.Count;
            for (int i = 0; i < StripLength; i++)
            {
                int candidate = UnityEngine.Random.Range(0, count);
                // Avoid identical neighbours so the blur looks varied.
                if (i > 0 && count > 1 && candidate == strip[i - 1]) candidate = (candidate + 1) % count;
                strip[i] = candidate;
            }

            strip[0] = Mathf.Clamp(startingSymbol, 0, count - 1);
            position = 0f;
            Layout();
        }

        public void StartSpin()
        {
            if (IsSpinning) return;
            StopWinPulse();
            phase = Phase.SpinningUp;
            phaseTimer = 0f;
        }

        /// <summary>Begins decelerating so the reel comes to rest on <paramref name="symbolIndex"/>.</summary>
        public void StopAt(int symbolIndex)
        {
            if (phase != Phase.SpinningUp && phase != Phase.Spinning) return;

            // Make sure the symbol we land on is the one chosen by the RNG.
            targetRow = Mathf.CeilToInt(position) + config.DecelerationRows;
            forcedRow = targetRow;
            forcedSymbol = symbolIndex;
            InvalidateCells();

            overshootPosition = targetRow + config.Overshoot;
            decelStart = position;
            decelDistance = overshootPosition - decelStart;

            // For easeOutCubic the initial slope is 3 * distance / duration. Choosing
            // duration = 3 * distance / currentSpeed makes the slow-down start with no visible jerk.
            float entrySpeed = Mathf.Max(speed, config.SpinSpeed * 0.6f);
            decelDuration = 3f * decelDistance / entrySpeed;
            phaseTimer = 0f;
            phase = Phase.Decelerating;
        }

        private void Update()
        {
            if (phase == Phase.Idle) return;
            float dt = Time.deltaTime;

            switch (phase)
            {
                case Phase.SpinningUp:
                {
                    phaseTimer += dt;
                    float k = Mathf.Clamp01(phaseTimer / config.SpinUpTime);
                    speed = config.SpinSpeed * k * k; // ease-in
                    position += speed * dt;
                    if (k >= 1f) phase = Phase.Spinning;
                    break;
                }
                case Phase.Spinning:
                    speed = config.SpinSpeed;
                    position += speed * dt;
                    break;

                case Phase.Decelerating:
                {
                    phaseTimer += dt;
                    float k = Mathf.Clamp01(phaseTimer / decelDuration);
                    float eased = 1f - Mathf.Pow(1f - k, 3f);          // easeOutCubic
                    float previous = position;
                    position = decelStart + decelDistance * eased;
                    speed = dt > 0f ? (position - previous) / dt : 0f; // for motion blur
                    if (k >= 1f)
                    {
                        phase = Phase.Settling;
                        phaseTimer = 0f;
                    }
                    break;
                }
                case Phase.Settling:
                {
                    phaseTimer += dt;
                    float k = Mathf.Clamp01(phaseTimer / config.SettleTime);
                    position = Mathf.Lerp(overshootPosition, targetRow, Mathf.SmoothStep(0f, 1f, k));
                    speed = 0f;
                    if (k >= 1f) FinishStop();
                    break;
                }
            }

            Layout();
        }

        private void FinishStop()
        {
            // Bake the landing symbol into the periodic strip, then wrap the position by whole
            // strip lengths. The strip is periodic, so wrapping changes nothing visually but keeps
            // the float small (no precision loss after thousands of spins).
            strip[Mod(targetRow, StripLength)] = forcedSymbol;
            position = targetRow;
            position -= StripLength * Mathf.Floor(position / StripLength);
            forcedRow = int.MinValue;
            speed = 0f;
            phase = Phase.Idle;
            InvalidateCells();
            Layout();
            Stopped?.Invoke(this);
        }

        /// <summary>Positions every pooled cell for the current scroll position.</summary>
        private void Layout()
        {
            int baseRow = Mathf.FloorToInt(position);
            float blur = config != null && config.SpinSpeed > 0f
                ? Mathf.Clamp01(Mathf.Abs(speed) / config.SpinSpeed)
                : 0f;
            float stretch = 1f + maxBlurStretch * blur;
            float alpha = 1f - 0.25f * blur;

            for (int i = 0; i < PoolSize; i++)
            {
                int row = baseRow + i - PoolHalf;
                Image cell = cells[i];

                if (cellRows[i] != row)
                {
                    cellRows[i] = row;
                    cell.sprite = config.Symbols[SymbolAtRow(row)].Sprite;
                }

                var rect = cell.rectTransform;
                rect.anchoredPosition = new Vector2(0f, (row - position) * cellHeight);

                // Don't touch the scale of a cell that is currently pulsing.
                if (cell != pulsingCell) rect.localScale = new Vector3(1f, stretch, 1f);
                var color = cell.color; color.a = alpha; cell.color = color;
            }
        }

        private int SymbolAtRow(int row)
        {
            return row == forcedRow ? forcedSymbol : strip[Mod(row, StripLength)];
        }

        private void InvalidateCells()
        {
            if (cellRows == null) return;
            for (int i = 0; i < cellRows.Length; i++) cellRows[i] = int.MinValue;
        }

        // ---- Win feedback ----

        /// <summary>Pulses the symbol sitting on the payline (called when the spin wins).</summary>
        public void PlayWinPulse(bool strong)
        {
            StopWinPulse();
            int centreRow = Mathf.RoundToInt(position);
            for (int i = 0; i < PoolSize; i++)
            {
                if (cellRows[i] == centreRow) { pulsingCell = cells[i]; break; }
            }
            if (pulsingCell != null) pulseRoutine = StartCoroutine(PulseRoutine(pulsingCell, strong ? 1.4f : 1.2f));
        }

        public void StopWinPulse()
        {
            if (pulseRoutine != null) StopCoroutine(pulseRoutine);
            pulseRoutine = null;
            if (pulsingCell != null) pulsingCell.rectTransform.localScale = Vector3.one;
            pulsingCell = null;
        }

        private IEnumerator PulseRoutine(Image cell, float maxScale)
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime * 5f;
                float s = Mathf.Lerp(1f, maxScale, 0.5f + 0.5f * Mathf.Sin(t));
                cell.rectTransform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
        }

        private static int Mod(int value, int length) => ((value % length) + length) % length;
    }
}
