using System.Collections.Generic;

namespace SlotGame.Core
{
    /// <summary>
    /// Abstraction over the random source so game logic can be unit-tested with
    /// deterministic values and the RNG implementation can be swapped.
    /// </summary>
    public interface IRandomService
    {
        /// <summary>Returns an index in [0, weights.Count) chosen with probability weight[i] / sum(weights).</summary>
        int NextWeightedIndex(IReadOnlyList<int> weights);
    }
}
