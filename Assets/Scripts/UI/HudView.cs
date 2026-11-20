using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SlotGame.Core;

namespace SlotGame.UI
{
    /// <summary>
    /// Heads-up display: credits, bet, last win, free spins, status line and the
    /// bet / auto-play / mute controls. It only presents data and raises click events;
    /// all decisions live in <see cref="SlotGameManager"/>.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [Header("Readouts")]
        [SerializeField] private Text balanceText;
        [SerializeField] private Text betText;
        [SerializeField] private Text winText;
        [SerializeField] private Text freeSpinText;
        [SerializeField] private Text statusText;

        [Header("Controls")]
        [SerializeField] private Button betDownButton;
        [SerializeField] private Button betUpButton;
        [SerializeField] private Button autoButton;
        [SerializeField] private Text autoLabel;
        [SerializeField] private Button muteButton;
        [SerializeField] private Text muteLabel;

        public event Action BetDecreaseClicked;
        public event Action BetIncreaseClicked;
        public event Action AutoPlayClicked;
        public event Action MuteClicked;

        private WalletModel wallet;
        private int displayedBalance;
        private Coroutine countRoutine;

        private void Awake()
        {
            betDownButton.onClick.AddListener(() => BetDecreaseClicked?.Invoke());
            betUpButton.onClick.AddListener(() => BetIncreaseClicked?.Invoke());
            autoButton.onClick.AddListener(() => AutoPlayClicked?.Invoke());
            muteButton.onClick.AddListener(() => MuteClicked?.Invoke());
        }

        /// <summary>Subscribes to the wallet so the readouts stay in sync automatically.</summary>
        public void Bind(WalletModel model)
        {
            Unbind();
            wallet = model;
            wallet.BalanceChanged += OnBalanceChanged;
            wallet.BetChanged += OnBetChanged;
            wallet.FreeSpinsChanged += OnFreeSpinsChanged;

            displayedBalance = wallet.Balance;
            balanceText.text = displayedBalance.ToString("N0");
            OnBetChanged(wallet.CurrentBet);
            OnFreeSpinsChanged(wallet.FreeSpins);
            SetWin(0);
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (wallet == null) return;
            wallet.BalanceChanged -= OnBalanceChanged;
            wallet.BetChanged -= OnBetChanged;
            wallet.FreeSpinsChanged -= OnFreeSpinsChanged;
            wallet = null;
        }

        public void SetWin(int amount) => winText.text = amount.ToString("N0");

        public void SetStatus(string message) => statusText.text = message;

        public void SetAutoPlayActive(bool active) => autoLabel.text = active ? "AUTO: ON" : "AUTO: OFF";

        public void SetMuteActive(bool muted) => muteLabel.text = muted ? "SOUND: OFF" : "SOUND: ON";

        /// <summary>Locks the bet buttons while reels spin so the stake cannot change mid-spin.</summary>
        public void SetBetControlsInteractable(bool interactable)
        {
            betDownButton.interactable = interactable;
            betUpButton.interactable = interactable;
        }

        private void OnBetChanged(int bet) => betText.text = bet.ToString("N0");

        private void OnFreeSpinsChanged(int count)
        {
            freeSpinText.text = count > 0 ? "FREE SPINS: " + count : string.Empty;
        }

        private void OnBalanceChanged(int newBalance)
        {
            if (countRoutine != null) StopCoroutine(countRoutine);

            // Winnings count up for a satisfying payout feel; stakes deduct instantly.
            if (newBalance > displayedBalance && isActiveAndEnabled)
                countRoutine = StartCoroutine(CountUp(newBalance));
            else
            {
                displayedBalance = newBalance;
                balanceText.text = displayedBalance.ToString("N0");
            }
        }

        private IEnumerator CountUp(int target)
        {
            int start = displayedBalance;
            const float duration = 0.8f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                displayedBalance = Mathf.RoundToInt(Mathf.Lerp(start, target, Mathf.Clamp01(t / duration)));
                balanceText.text = displayedBalance.ToString("N0");
                yield return null;
            }
            displayedBalance = target;
            balanceText.text = target.ToString("N0");
        }
    }
}
