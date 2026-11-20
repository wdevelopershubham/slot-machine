using UnityEngine;

namespace SlotGame.Core
{
    /// <summary>
    /// Data asset describing a single reel symbol: its artwork, how often it
    /// appears (weight) and what it pays when it fills every reel.
    /// Keeping this as a ScriptableObject lets designers tune the game
    /// without touching code.
    /// </summary>
    [CreateAssetMenu(fileName = "Symbol", menuName = "SlotGame/Symbol Definition")]
    public sealed class SymbolDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Symbol";
        [SerializeField] private Sprite sprite;

        [Tooltip("Relative chance of landing on this symbol. Higher = more common.")]
        [SerializeField, Min(1)] private int weight = 1;

        [Tooltip("Payout multiplier (x bet) when ALL reels show this symbol.")]
        [SerializeField, Min(0)] private int tripleMultiplier = 5;

        [Tooltip("Lucky symbol: a full line is the JACKPOT, and exactly a pair awards a free spin.")]
        [SerializeField] private bool isLuckySymbol;

        public string DisplayName => displayName;
        public Sprite Sprite => sprite;
        public int Weight => weight;
        public int TripleMultiplier => tripleMultiplier;
        public bool IsLuckySymbol => isLuckySymbol;

        /// <summary>Programmatic setup, used by the editor scene builder and unit tests.</summary>
        public void Configure(string name, Sprite symbolSprite, int symbolWeight, int multiplier, bool lucky)
        {
            displayName = name;
            sprite = symbolSprite;
            weight = Mathf.Max(1, symbolWeight);
            tripleMultiplier = Mathf.Max(0, multiplier);
            isLuckySymbol = lucky;
        }
    }
}
