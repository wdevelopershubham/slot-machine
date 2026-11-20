using System.Collections;
using UnityEngine;
using SlotGame.Audio;
using SlotGame.Reels;
using SlotGame.UI;

namespace SlotGame.Core
{
    /// <summary>
    /// Orchestrates the game loop (Idle -> Spinning -> Resolving -> Idle / GameOver).
    /// Responsibilities are deliberately thin: it asks the RNG for an outcome, tells the reels to
    /// animate to it, asks the evaluator what it is worth, and updates wallet / UI / audio.
    /// The maths lives in <see cref="PayoutEvaluator"/>, money in <see cref="WalletModel"/>.
    /// </summary>
    public sealed class SlotGameManager : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private SlotConfig config;

        [Header("Scene references")]
        [SerializeField] private ReelController[] reels;
        [SerializeField] private HudView hud;
        [SerializeField] private ResultPopup popup;
        [SerializeField] private LeverController lever;
        [SerializeField] private PaylineHighlight payline;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private ConfettiBurst confetti;
        [SerializeField] private ScreenShake screenShake;

        private IRandomService rng;
        private PayoutEvaluator evaluator;
        private WalletModel wallet;
        private GameState state = GameState.Idle;
        private bool autoPlay;
        private int reelsStillSpinning;

        private void Awake()
        {
            if (config == null || config.Symbols.Count < 2)
                Debug.LogError("SlotGameManager needs a SlotConfig with at least two symbols.", this);
            if (reels == null || reels.Length != SlotConfig.ReelCount)
                Debug.LogError($"SlotGameManager needs exactly {SlotConfig.ReelCount} reels assigned.", this);

            rng = new WeightedRandomService(config.Seed);
            evaluator = new PayoutEvaluator(config);
            wallet = new WalletModel(config.StartingCredits, config.BetOptions, config.DefaultBetIndex);
        }

        private void Start()
        {
            hud.Bind(wallet);
            hud.SetAutoPlayActive(false);
            hud.SetMuteActive(false);
            hud.SetStatus("PULL THE LEVER OR PRESS SPACE");

            // Show three different symbols at start so the machine doesn't open on a fake "win".
            for (int i = 0; i < reels.Length; i++)
            {
                reels[i].Initialize(config, i % config.Symbols.Count);
                reels[i].Stopped += OnReelStopped;
            }

            lever.Clicked += RequestSpin;
            hud.BetDecreaseClicked += OnBetDecrease;
            hud.BetIncreaseClicked += OnBetIncrease;
            hud.AutoPlayClicked += OnAutoPlayClicked;
            hud.MuteClicked += OnMuteClicked;

            Debug.Log($"[Slots] Hit frequency {evaluator.CalculateHitFrequency():P1}, " +
                      $"theoretical RTP {evaluator.CalculateTheoreticalRtp():P2} " +
                      $"(base {evaluator.CalculateBaseRtp():P2} + free-spin bonus)");
        }

        private void OnDestroy()
        {
            if (reels != null)
                foreach (var reel in reels) if (reel != null) reel.Stopped -= OnReelStopped;
            if (lever != null) lever.Clicked -= RequestSpin;
            if (hud != null)
            {
                hud.BetDecreaseClicked -= OnBetDecrease;
                hud.BetIncreaseClicked -= OnBetIncrease;
                hud.AutoPlayClicked -= OnAutoPlayClicked;
                hud.MuteClicked -= OnMuteClicked;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) RequestSpin();
        }

        // ---- Input handlers ----

        /// <summary>Starts a spin if the game is idle and the player can pay for it.</summary>
        public void RequestSpin()
        {
            if (state == GameState.GameOver) { PromptRestart(); return; }
            if (state != GameState.Idle || popup.IsOpen) return;

            if (!wallet.CanSpin)
            {
                wallet.ClampBetToBalance(); // maybe a cheaper bet is affordable
                if (!wallet.CanSpin) { EnterGameOver(); return; }
            }

            StartCoroutine(SpinRoutine());
        }

        private void OnBetDecrease()
        {
            if (state != GameState.Idle) return;
            wallet.ChangeBet(-1);
            audioManager.PlayClick();
        }

        private void OnBetIncrease()
        {
            if (state != GameState.Idle) return;
            wallet.ChangeBet(+1);
            audioManager.PlayClick();
        }

        private void OnAutoPlayClicked()
        {
            autoPlay = !autoPlay;
            hud.SetAutoPlayActive(autoPlay);
            audioManager.PlayClick();
            if (autoPlay) RequestSpin();
        }

        private void OnMuteClicked()
        {
            audioManager.SetMuted(!audioManager.IsMuted);
            hud.SetMuteActive(audioManager.IsMuted);
            audioManager.PlayClick();
        }

        private void OnReelStopped(ReelController reel)
        {
            reelsStillSpinning--;
            audioManager.PlayReelStop();
        }

        // ---- Core loop ----

        private IEnumerator SpinRoutine()
        {
            state = GameState.Spinning;
            SetControlsInteractable(false);

            wallet.TryPlaceBet(out bool wasFreeSpin);
            int bet = wallet.CurrentBet;

            // Reset feedback from the previous round.
            hud.SetWin(0);
            hud.SetStatus(wasFreeSpin ? "FREE SPIN!" : "GOOD LUCK!");
            payline.Hide();
            foreach (var reel in reels) reel.StopWinPulse();

            lever.PlayPull();
            audioManager.PlayLever();

            // 1) Decide the outcome FIRST, independently per reel. Animation is cosmetic only.
            int[] weights = config.GetWeights();
            int[] outcome = new int[reels.Length];
            for (int i = 0; i < outcome.Length; i++) outcome[i] = rng.NextWeightedIndex(weights);

            // 2) Spin everything.
            reelsStillSpinning = reels.Length;
            foreach (var reel in reels) reel.StartSpin();
            audioManager.StartSpinLoop();
            yield return new WaitForSeconds(config.MinSpinTime);

            // 3) Stop reels left to right with a stagger for suspense.
            for (int i = 0; i < reels.Length; i++)
            {
                reels[i].StopAt(outcome[i]);
                yield return new WaitForSeconds(config.ReelStopInterval);
            }
            while (reelsStillSpinning > 0) yield return null;
            audioManager.StopSpinLoop();

            // 4) Evaluate and present the result.
            state = GameState.Resolving;
            SpinResult result = evaluator.Evaluate(outcome, bet);
            yield return ResolveResult(result);

            // 5) Housekeeping: keep the stake affordable, detect bankruptcy, continue auto-play.
            wallet.ClampBetToBalance();
            if (wallet.IsBroke) { EnterGameOver(); yield break; }

            state = GameState.Idle;
            SetControlsInteractable(true);
            if (!autoPlay) hud.SetStatus("PULL THE LEVER OR PRESS SPACE");

            if (autoPlay)
            {
                yield return new WaitForSeconds(config.AutoPlayDelay);
                if (autoPlay && state == GameState.Idle) RequestSpin();
            }
        }

        private IEnumerator ResolveResult(SpinResult result)
        {
            if (result.IsWin)
            {
                wallet.Credit(result.Payout);
                hud.SetWin(result.Payout);
                payline.Show();
                foreach (var reel in reels) reel.PlayWinPulse(result.IsJackpot);

                string title = result.IsJackpot ? "JACKPOT!" : "YOU WIN!";
                hud.SetStatus(title);

                if (result.IsJackpot)
                {
                    audioManager.PlayJackpot();
                    confetti.Play(Vector2.zero);
                    screenShake.Play();
                    // Pause auto-play so the player actually sees the big win.
                    autoPlay = false;
                    hud.SetAutoPlayActive(false);
                }
                else
                {
                    audioManager.PlayWin();
                }

                string body = $"{result.WinningSymbol.DisplayName} x{result.WinningSymbol.TripleMultiplier}\n+{result.Payout:N0}";
                float seconds = result.IsJackpot ? config.JackpotPopupSeconds : config.WinPopupSeconds;
                yield return popup.ShowWin(title, body, seconds);
            }
            else if (result.GrantsFreeSpin)
            {
                wallet.AddFreeSpins(1);
                audioManager.PlayBonus();
                hud.SetStatus("BONUS! FREE SPIN EARNED");
                yield return new WaitForSeconds(1.2f);
            }
            else
            {
                hud.SetStatus("NO WIN - TRY AGAIN");
                yield return new WaitForSeconds(0.4f);
            }
        }

        // ---- Game over / restart ----

        private void EnterGameOver()
        {
            state = GameState.GameOver;
            autoPlay = false;
            hud.SetAutoPlayActive(false);
            hud.SetBetControlsInteractable(false);
            lever.SetInteractable(true); // clicking the lever re-opens the restart prompt
            audioManager.PlayGameOver();
            hud.SetStatus("OUT OF CREDITS");
            PromptRestart();
        }

        private void PromptRestart()
        {
            if (popup.IsOpen) return;
            popup.ShowConfirm(
                "OUT OF CREDITS",
                $"Start a new game with {config.StartingCredits:N0} credits?",
                yes: () =>
                {
                    wallet.Reset();
                    hud.SetWin(0);
                    state = GameState.Idle;
                    SetControlsInteractable(true);
                    hud.SetStatus("PULL THE LEVER OR PRESS SPACE");
                },
                no: () => hud.SetStatus("GAME OVER - PULL THE LEVER TO PLAY AGAIN"));
        }

        private void SetControlsInteractable(bool interactable)
        {
            lever.SetInteractable(interactable);
            hud.SetBetControlsInteractable(interactable);
        }
    }
}
