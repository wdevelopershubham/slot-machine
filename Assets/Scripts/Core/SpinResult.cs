using System.Collections.Generic;

namespace SlotGame.Core
{
    /// <summary>Immutable outcome of one spin, produced by <see cref="PayoutEvaluator"/>.</summary>
    public sealed class SpinResult
    {
        public SpinResult(IReadOnlyList<int> symbolIndices, SymbolDefinition winningSymbol,
                          int bet, int payout, bool isJackpot, bool grantsFreeSpin)
        {
            SymbolIndices = symbolIndices;
            WinningSymbol = winningSymbol;
            Bet = bet;
            Payout = payout;
            IsJackpot = isJackpot;
            GrantsFreeSpin = grantsFreeSpin;
        }

        /// <summary>Index (into SlotConfig.Symbols) shown on each reel, left to right.</summary>
        public IReadOnlyList<int> SymbolIndices { get; }

        /// <summary>The symbol that filled every reel, or null when the spin lost.</summary>
        public SymbolDefinition WinningSymbol { get; }

        public int Bet { get; }
        public int Payout { get; }

        /// <summary>A full line of the lucky symbol.</summary>
        public bool IsJackpot { get; }

        /// <summary>Bonus: exactly a pair of lucky symbols awards one free spin.</summary>
        public bool GrantsFreeSpin { get; }

        public bool IsWin => WinningSymbol != null;
    }
}
