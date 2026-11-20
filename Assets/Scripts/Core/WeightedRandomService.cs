using System;
using System.Collections.Generic;

namespace SlotGame.Core
{
    /// <summary>
    /// Weighted RNG backed by System.Random.
    /// Fairness notes:
    ///  - The outcome of a spin is decided here, BEFORE any animation plays. The
    ///    reels are then animated to land on that outcome, so visuals can never
    ///    influence results.
    ///  - System.Random.Next(int) is uniform (no modulo bias), and each reel draws
    ///    independently, so reels cannot "remember" each other.
    ///  - The seed mixes time, a GUID and the tick count, so every launch differs.
    /// </summary>
    public sealed class WeightedRandomService : IRandomService
    {
        private readonly Random random;

        /// <param name="seed">0 = unpredictable seed; non-zero = reproducible sequence.</param>
        public WeightedRandomService(int seed = 0)
        {
            random = new Random(seed != 0 ? seed : CreateUnpredictableSeed());
        }

        public int NextWeightedIndex(IReadOnlyList<int> weights)
        {
            if (weights == null || weights.Count == 0)
                throw new ArgumentException("At least one weight is required.", nameof(weights));

            int total = 0;
            for (int i = 0; i < weights.Count; i++) total += Math.Max(0, weights[i]);
            if (total <= 0)
                throw new ArgumentException("Total weight must be greater than zero.", nameof(weights));

            // Pick a ticket in [0, total) and walk the cumulative ranges.
            int ticket = random.Next(total);
            int cumulative = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                cumulative += Math.Max(0, weights[i]);
                if (ticket < cumulative) return i;
            }
            return weights.Count - 1; // Unreachable; keeps the compiler happy.
        }

        private static int CreateUnpredictableSeed()
        {
            unchecked
            {
                int seed = (int)(DateTime.UtcNow.Ticks ^ Guid.NewGuid().GetHashCode() ^ Environment.TickCount);
                return seed == 0 ? 1 : seed;
            }
        }
    }
}
