using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.UI
{
    /// <summary>
    /// The machine's pull-lever. The artwork has two full-size layers (lever up / lever down);
    /// a transparent button over the knob captures clicks and taps.
    /// </summary>
    public sealed class LeverController : MonoBehaviour
    {
        [SerializeField] private Image leverUp;
        [SerializeField] private Image leverDown;
        [SerializeField] private Button hitArea;
        [SerializeField, Min(0.05f)] private float pullDuration = 0.3f;

        public event Action Clicked;

        private void Awake()
        {
            hitArea.onClick.AddListener(() => Clicked?.Invoke());
            ShowUp();
        }

        public void SetInteractable(bool interactable) => hitArea.interactable = interactable;

        /// <summary>Plays the down-then-up pull animation.</summary>
        public void PlayPull()
        {
            StopAllCoroutines();
            StartCoroutine(PullRoutine());
        }

        private IEnumerator PullRoutine()
        {
            leverUp.enabled = false;
            leverDown.enabled = true;
            yield return new WaitForSeconds(pullDuration);
            ShowUp();
        }

        private void ShowUp()
        {
            leverUp.enabled = true;
            leverDown.enabled = false;
        }
    }
}
