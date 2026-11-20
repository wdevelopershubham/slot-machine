using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.UI
{
    /// <summary>
    /// Glowing line across the reels' centre row. Uses an Animator (clips live in Assets/Animations)
    /// so designers can retune the pulse; falls back to a code pulse if no Animator is present.
    /// </summary>
    public sealed class PaylineHighlight : MonoBehaviour
    {
        private static readonly int ActiveParam = Animator.StringToHash("Active");

        [SerializeField] private Animator animator;
        [SerializeField] private Image line;

        private Coroutine fallbackPulse;

        public void Show()
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetBool(ActiveParam, true);
                return;
            }
            Hide();
            fallbackPulse = StartCoroutine(Pulse());
        }

        public void Hide()
        {
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetBool(ActiveParam, false);

            if (fallbackPulse != null) StopCoroutine(fallbackPulse);
            fallbackPulse = null;
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                var c = line.color; c.a = 0f; line.color = c;
            }
        }

        private IEnumerator Pulse()
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime * 6f;
                var c = line.color; c.a = 0.55f + 0.45f * Mathf.Sin(t); line.color = c;
                yield return null;
            }
        }
    }
}
