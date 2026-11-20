using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SlotGame.Core;

namespace SlotGame.UI
{
    /// <summary>
    /// Builds a paytable (three symbol icons + multiplier per row) from the SlotConfig, so the
    /// displayed payouts can never drift from the real ones.
    /// </summary>
    public sealed class PaytableView : MonoBehaviour
    {
        [SerializeField] private SlotConfig config;
        [SerializeField] private RectTransform rowContainer;

        private void Start() => Build();

        private void Build()
        {
            // Highest paying symbols first.
            var ordered = new List<SymbolDefinition>(config.Symbols);
            ordered.Sort((a, b) => b.TripleMultiplier.CompareTo(a.TripleMultiplier));

            foreach (var symbol in ordered)
                CreateRow(symbol);

            CreateNote("2 x lucky symbol = FREE SPIN");
        }

        private void CreateRow(SymbolDefinition symbol)
        {
            var row = new GameObject(symbol.DisplayName + "_Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(rowContainer, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 48f);

            for (int i = 0; i < SlotConfig.ReelCount; i++)
            {
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(row.transform, false);
                var image = icon.GetComponent<Image>();
                image.sprite = symbol.Sprite;
                image.raycastTarget = false;
                icon.GetComponent<RectTransform>().sizeDelta = new Vector2(42f, 42f);
            }

            var label = CreateText(row.transform, "x" + symbol.TripleMultiplier, 30, new Vector2(110f, 44f));
            label.alignment = TextAnchor.MiddleRight;
            label.color = symbol.IsLuckySymbol ? new Color(1f, 0.85f, 0.2f) : Color.white;
        }

        private void CreateNote(string message)
        {
            var text = CreateText(rowContainer, message, 20, new Vector2(0f, 40f));
            text.color = new Color(0.7f, 0.9f, 1f);
        }

        private static Text CreateText(Transform parent, string content, int size, Vector2 sizeDelta)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = UiFont.Default;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = content;
            text.raycastTarget = false;
            go.GetComponent<RectTransform>().sizeDelta = sizeDelta;
            return text;
        }
    }
}
