using System;
using System.Collections.Generic;

namespace SlotGame.Core
{
    /// <summary>
    /// Pure game rules, free of any UnityEngine scene dependency so it is easy to unit-test.
    /// Rules:
    ///  1. All reels show the same symbol -> WIN, pays bet x symbol multiplier.
    ///  2. A full line of the lucky symbol is the JACKPOT (presentation only).
    ///  3. Bonus: exactly <see cref="SlotConfig.LuckyPairForFreeSpin"/> lucky symbols awards a free spin.
    /// </summary>
    public sealed class PayoutEvaluator
    {
        private readonly SlotConfig config;

        public PayoutEvaluator(SlotConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public SpinResult Evaluate(IReadOnlyList<int> symbolIndices, int bet)
        {
            if (symbolIndices == null || symbolIndices.Count == 0)
                throw new ArgumentException("A spin needs at least one reel result.", nameof(symbolIndices));

            var symbols = config.Symbols;
            int first = symbolIndices[0];
            bool allMatch = true;
            int luckyCount = 0;

            for (int i = 0; i < symbolIndices.Count; i++)
            {
                if (symbolIndices[i] != first) allMatch = false;
                if (symbols[symbolIndices[i]].IsLuckySymbol) luckyCount++;
            }

            if (allMatch)
            {
                SymbolDefinition symbol = symbols[first];
                int payout = bet * symbol.TripleMultiplier;
                return new SpinResult(symbolIndices, symbol, bet, payout, symbol.IsLuckySymbol, false);
            }

            bool freeSpin = luckyCount == config.LuckyPairForFreeSpin;
            return new SpinResult(symbolIndices, null, bet, 0, false, freeSpin);
        }

        // ---- Maths helpers (used by the HUD log and the unit tests) ----

        /// <summary>Probability that every reel lands on the same symbol.</summary>
        public double CalculateHitFrequency()
        {
            double total = TotalWeight();
            double hit = 0;
            foreach (var s in config.Symbols) hit += Math.Pow(s.Weight / total, SlotConfig.ReelCount);
            return hit;
        }

        /// <summary>Expected return per credit staked, ignoring the free-spin bonus.</summary>
        public double CalculateBaseRtp()
        {
            double total = TotalWeight();
            double rtp = 0;
            foreach (var s in config.Symbols)
                rtp += Math.Pow(s.Weight / total, SlotConfig.ReelCount) * s.TripleMultiplier;
            return rtp;
        }

        /// <summary>Probability that a spin lands exactly the lucky pair (free spin bonus).</summary>
        public double CalculateFreeSpinProbability()
        {
            double total = TotalWeight();
            double luckyWeight = 0;
            foreach (var s in config.Symbols) if (s.IsLuckySymbol) luckyWeight += s.Weight;
            double p = luckyWeight / total;
            int n = SlotConfig.ReelCount, k = config.LuckyPairForFreeSpin;
            return Binomial(n, k) * Math.Pow(p, k) * Math.Pow(1 - p, n - k);
        }

        /// <summary>
        /// Total expected return including free spins. A free spin is a spin that costs nothing,
        /// so total = base + pFreeSpin * total  =>  total = base / (1 - pFreeSpin).
        /// </summary>
        public double CalculateTheoreticalRtp()
        {
            return CalculateBaseRtp() / (1.0 - CalculateFreeSpinProbability());
        }

        private double TotalWeight()
        {
            double total = 0;
            foreach (var s in config.Symbols) total += s.Weight;
            return total;
        }

        private static double Binomial(int n, int k)
        {
            double result = 1;
            for (int i = 1; i <= k; i++) result = result * (n - k + i) / i;
            return result;
        }
    }
}
