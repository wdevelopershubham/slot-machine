using NUnit.Framework;
using SlotGame.Core;

namespace SlotGame.Tests
{
    public class WeightedRandomServiceTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new WeightedRandomService(1234);
            var b = new WeightedRandomService(1234);
            var weights = new[] { 1, 2, 3, 4 };
            for (int i = 0; i < 50; i++)
                Assert.AreEqual(a.NextWeightedIndex(weights), b.NextWeightedIndex(weights));
        }

        [Test]
        public void Distribution_FollowsWeights()
        {
            var rng = new WeightedRandomService(98765);
            var weights = new[] { 1, 2, 3, 4 };
            var counts = new int[4];
            const int draws = 200000;
            for (int i = 0; i < draws; i++) counts[rng.NextWeightedIndex(weights)]++;

            for (int i = 0; i < weights.Length; i++)
            {
                double expected = weights[i] / 10.0;
                Assert.AreEqual(expected, counts[i] / (double)draws, 0.01, "symbol " + i);
            }
        }

        [Test]
        public void ZeroWeightSymbol_NeverChosen()
        {
            var rng = new WeightedRandomService(5);
            var weights = new[] { 0, 1, 1 };
            for (int i = 0; i < 5000; i++) Assert.AreNotEqual(0, rng.NextWeightedIndex(weights));
        }

        [Test]
        public void EmptyOrZeroTotal_Throws()
        {
            var rng = new WeightedRandomService(1);
            Assert.Throws<System.ArgumentException>(() => rng.NextWeightedIndex(new int[0]));
            Assert.Throws<System.ArgumentException>(() => rng.NextWeightedIndex(new[] { 0, 0 }));
        }

        [Test]
        public void SimulatedPlay_RtpIsNearTheory()
        {
            // 300k paid spins with free spins and payouts; should land within a few points of ~95.6%.
            var config = TestConfigFactory.Create();
            var evaluator = new PayoutEvaluator(config);
            var rng = new WeightedRandomService(2024);
            int[] weights = config.GetWeights();

            long staked = 0, won = 0;
            int freeSpins = 0;
            var outcome = new int[3];
            for (int spin = 0; spin < 300000; spin++)
            {
                if (freeSpins > 0) freeSpins--; else staked += 1;
                for (int r = 0; r < 3; r++) outcome[r] = rng.NextWeightedIndex(weights);
                SpinResult result = evaluator.Evaluate(outcome, 1);
                won += result.Payout;
                if (result.GrantsFreeSpin) freeSpins++;
            }
            double rtp = (double)won / staked;
            Assert.That(rtp, Is.InRange(0.85, 1.07));
        }
    }
}
