using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.UI
{
    /// <summary>Jackpot celebration: symbol sprites burst upward and rain down with simple physics.</summary>
    public sealed class ConfettiBurst : MonoBehaviour
    {
        [SerializeField] private RectTransform container;
        [SerializeField] private Sprite[] sprites;
        [SerializeField, Min(1)] private int count = 70;
        [SerializeField] private float gravity = 1800f;
        [SerializeField] private float lifetime = 3f;

        public void Play(Vector2 origin)
        {
            if (sprites == null || sprites.Length == 0) return;
            StartCoroutine(Run(origin));
        }

        private IEnumerator Run(Vector2 origin)
        {
            var pieces = new List<Piece>(count);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(container, false);
                var image = go.GetComponent<Image>();
                image.sprite = sprites[Random.Range(0, sprites.Length)];
                image.raycastTarget = false;
                var rect = go.GetComponent<RectTransform>();
                rect.sizeDelta = Vector2.one * Random.Range(40f, 80f);
                rect.anchoredPosition = origin;
                pieces.Add(new Piece
                {
                    rect = rect,
                    image = image,
                    velocity = new Vector2(Random.Range(-600f, 600f), Random.Range(500f, 1400f)),
                    spin = Random.Range(-360f, 360f)
                });
            }

            for (float t = 0f; t < lifetime; t += Time.deltaTime)
            {
                foreach (var p in pieces)
                {
                    p.velocity.y -= gravity * Time.deltaTime;
                    p.rect.anchoredPosition += p.velocity * Time.deltaTime;
                    p.rect.Rotate(0f, 0f, p.spin * Time.deltaTime);
                    var c = p.image.color; c.a = Mathf.Clamp01((lifetime - t) / 0.7f); p.image.color = c;
                }
                yield return null;
            }

            foreach (var p in pieces) Destroy(p.rect.gameObject);
        }

        private sealed class Piece
        {
            public RectTransform rect;
            public Image image;
            public Vector2 velocity;
            public float spin;
        }
    }
}
