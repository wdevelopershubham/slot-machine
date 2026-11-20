using System.Collections;
using UnityEngine;

namespace SlotGame.UI
{
    /// <summary>Short decaying shake applied to a UI element (the machine) on a jackpot.</summary>
    public sealed class ScreenShake : MonoBehaviour
    {
        [SerializeField] private RectTransform target;

        public void Play(float magnitude = 14f, float duration = 0.6f)
        {
            StopAllCoroutines();
            StartCoroutine(Shake(magnitude, duration));
        }

        private IEnumerator Shake(float magnitude, float duration)
        {
            Vector2 origin = target.anchoredPosition;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float falloff = 1f - t / duration;
                target.anchoredPosition = origin + Random.insideUnitCircle * (magnitude * falloff);
                yield return null;
            }
            target.anchoredPosition = origin;
        }
    }
}
