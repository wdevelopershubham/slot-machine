using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.UI
{
    /// <summary>
    /// Modal popup with two modes:
    ///  - Win banner: title + amount, closes by timer, by the X button or by clicking the backdrop.
    ///  - Confirm dialog: title + message with YES / NO buttons (used for "out of credits, play again?").
    /// Pop-in / fade-out are code tweens so the popup has no dependency on Animator setup.
    /// </summary>
    public sealed class ResultPopup : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;
        [SerializeField] private Button backdropButton;

        private Action onYes;
        private Action onNo;
        private bool confirmMode;
        private Coroutine tween;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            closeButton.onClick.AddListener(Hide);
            backdropButton.onClick.AddListener(() => { if (!confirmMode) Hide(); });
            yesButton.onClick.AddListener(() => Answer(onYes));
            noButton.onClick.AddListener(() => Answer(onNo));
            SetVisibleImmediate(false);
        }

        /// <summary>Shows the win banner and waits until it closes (timer or player click).</summary>
        public IEnumerator ShowWin(string title, string body, float seconds)
        {
            confirmMode = false;
            Open(title, body);
            closeButton.gameObject.SetActive(true);
            yesButton.gameObject.SetActive(false);
            noButton.gameObject.SetActive(false);

            float elapsed = 0f;
            while (IsOpen && elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (IsOpen) Hide();
            while (canvasGroup.alpha > 0f) yield return null; // let the fade-out finish
        }

        /// <summary>Shows a YES / NO dialog. The callbacks fire after the popup has been dismissed.</summary>
        public void ShowConfirm(string title, string body, Action yes, Action no)
        {
            confirmMode = true;
            onYes = yes;
            onNo = no;
            Open(title, body);
            closeButton.gameObject.SetActive(false);
            yesButton.gameObject.SetActive(true);
            noButton.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (!IsOpen) return;
            IsOpen = false;
            StartTween(FadeOut());
        }

        private void Answer(Action callback)
        {
            Hide();
            callback?.Invoke();
        }

        private void Open(string title, string body)
        {
            titleText.text = title;
            bodyText.text = body;
            IsOpen = true;
            StartTween(PopIn());
        }

        private void StartTween(IEnumerator routine)
        {
            if (tween != null) StopCoroutine(tween);
            tween = StartCoroutine(routine);
        }

        private IEnumerator PopIn()
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            const float duration = 0.28f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                canvasGroup.alpha = Mathf.Clamp01(k * 3f);
                panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(k));
                yield return null;
            }
            canvasGroup.alpha = 1f;
            panel.localScale = Vector3.one;
        }

        private IEnumerator FadeOut()
        {
            canvasGroup.interactable = false;
            const float duration = 0.18f;
            float start = canvasGroup.alpha;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                canvasGroup.alpha = Mathf.Lerp(start, 0f, t / duration);
                yield return null;
            }
            SetVisibleImmediate(false);
        }

        private void SetVisibleImmediate(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }

        // Standard "back" easing: overshoots slightly, then settles.
        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float p = x - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }
    }
}
