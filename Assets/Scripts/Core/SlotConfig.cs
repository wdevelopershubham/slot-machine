using System.Collections.Generic;
using UnityEngine;

namespace SlotGame.Core
{
    /// <summary>
    /// Central, designer-editable configuration: symbols, economy, spin timing
    /// and presentation. A single asset keeps "magic numbers" out of the code.
    /// </summary>
    [CreateAssetMenu(fileName = "SlotConfig", menuName = "SlotGame/Slot Config")]
    public sealed class SlotConfig : ScriptableObject
    {
        /// <summary>The supplied machine artwork has exactly three reel windows.</summary>
        public const int ReelCount = 3;

        [Header("Symbols")]
        [SerializeField] private List<SymbolDefinition> symbols = new List<SymbolDefinition>();

        [Tooltip("Exactly this many lucky symbols (but not a full line) award a free spin.")]
        [SerializeField, Range(1, ReelCount - 1)] private int luckyPairForFreeSpin = 2;

        [Header("Economy")]
        [SerializeField, Min(1)] private int startingCredits = 1000;
        [SerializeField] private int[] betOptions = { 5, 10, 25, 50, 100 };
        [SerializeField, Min(0)] private int defaultBetIndex = 1;

        [Header("Spin Timing (seconds unless noted)")]
        [Tooltip("Reel scroll speed in symbols per second at full speed.")]
        [SerializeField, Min(1f)] private float spinSpeed = 20f;
        [SerializeField, Min(0.01f)] private float spinUpTime = 0.35f;
        [Tooltip("Minimum time every reel spins before the first one may stop.")]
        [SerializeField, Min(0f)] private float minSpinTime = 1.0f;
        [Tooltip("Delay between one reel stopping and the next (left to right).")]
        [SerializeField, Min(0f)] private float reelStopInterval = 0.45f;
        [Tooltip("How many extra symbols scroll past while a reel decelerates.")]
        [SerializeField, Min(2)] private int decelerationRows = 5;
        [Tooltip("How far (in symbols) the reel overshoots before bouncing back.")]
        [SerializeField, Range(0f, 0.5f)] private float overshoot = 0.15f;
        [SerializeField, Min(0.01f)] private float settleTime = 0.22f;

        [Header("Presentation")]
        [SerializeField, Min(0.5f)] private float winPopupSeconds = 2.2f;
        [SerializeField, Min(0.5f)] private float jackpotPopupSeconds = 4f;
        [SerializeField, Min(0.1f)] private float autoPlayDelay = 0.8f;

        [Header("RNG")]
        [Tooltip("0 = unpredictable seed every launch. Any other value gives a reproducible sequence (debugging).")]
        [SerializeField] private int seed = 0;

        public IReadOnlyList<SymbolDefinition> Symbols => symbols;
        public int LuckyPairForFreeSpin => luckyPairForFreeSpin;
        public int StartingCredits => startingCredits;
        public IReadOnlyList<int> BetOptions => betOptions;
        public int DefaultBetIndex => Mathf.Clamp(defaultBetIndex, 0, betOptions.Length - 1);
        public float SpinSpeed => spinSpeed;
        public float SpinUpTime => spinUpTime;
        public float MinSpinTime => minSpinTime;
        public float ReelStopInterval => reelStopInterval;
        public int DecelerationRows => decelerationRows;
        public float Overshoot => overshoot;
        public float SettleTime => settleTime;
        public float WinPopupSeconds => winPopupSeconds;
        public float JackpotPopupSeconds => jackpotPopupSeconds;
        public float AutoPlayDelay => autoPlayDelay;
        public int Seed => seed;

        /// <summary>The weights array in symbol order, used by the RNG.</summary>
        public int[] GetWeights()
        {
            var weights = new int[symbols.Count];
            for (int i = 0; i < weights.Length; i++) weights[i] = symbols[i].Weight;
            return weights;
        }

        /// <summary>Programmatic setup for the scene builder and unit tests.</summary>
        public void Configure(IEnumerable<SymbolDefinition> symbolList, int credits, int[] bets, int defaultBet)
        {
            symbols = new List<SymbolDefinition>(symbolList);
            startingCredits = credits;
            betOptions = bets;
            defaultBetIndex = defaultBet;
        }

        private void OnValidate()
        {
            if (betOptions == null || betOptions.Length == 0) betOptions = new[] { 10 };
            defaultBetIndex = Mathf.Clamp(defaultBetIndex, 0, betOptions.Length - 1);
        }
    }
}
