using SlotGame.Core;
using UnityEngine;

namespace SlotGame.Tests
{
    /// <summary>Builds the same symbol table as the shipped game, without needing any asset files.</summary>
    internal static class TestConfigFactory
    {
        // Indices: 0 = Seven (lucky), 1 = Bar, 2 = Bell, 3 = Cherry
        public static SlotConfig Create()
        {
            var seven = Make("Seven", 1, 100, true);
            var bar = Make("Bar", 2, 30, false);
            var bell = Make("Bell", 3, 10, false);
            var cherry = Make("Cherry", 4, 5, false);

            var config = ScriptableObject.CreateInstance<SlotConfig>();
            config.Configure(new[] { seven, bar, bell, cherry }, 1000, new[] { 5, 10, 25 }, 1);
            return config;
        }

        private static SymbolDefinition Make(string name, int weight, int multiplier, bool lucky)
        {
            var symbol = ScriptableObject.CreateInstance<SymbolDefinition>();
            symbol.Configure(name, null, weight, multiplier, lucky);
            return symbol;
        }
    }
}
