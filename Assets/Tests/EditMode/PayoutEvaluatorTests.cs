using NUnit.Framework;
using SlotGame.Core;

namespace SlotGame.Tests
{
    public class PayoutEvaluatorTests
    {
        private PayoutEvaluator evaluator;

        [SetUp]
        public void SetUp() => evaluator = new PayoutEvaluator(TestConfigFactory.Create());

        [Test]
        public void ThreeMatchingSymbols_Wins_BetTimesMultiplier()
        {
            SpinResult result = evaluator.Evaluate(new[] { 3, 3, 3 }, 10); // cherries x5
            Assert.IsTrue(result.IsWin);
            Assert.AreEqual(50, result.Payout);
            Assert.IsFalse(result.IsJackpot);
        }

        [Test]
        public void ThreeSevens_IsJackpot()
        {
            SpinResult result = evaluator.Evaluate(new[] { 0, 0, 0 }, 25);
            Assert.IsTrue(result.IsWin);
            Assert.IsTrue(result.IsJackpot);
            Assert.AreEqual(2500, result.Payout);
        }

        [TestCase(0, 1, 2)]
        [TestCase(3, 3, 2)]
        [TestCase(1, 2, 1)]
        public void MixedSymbols_Lose(int a, int b, int c)
        {
            SpinResult result = evaluator.Evaluate(new[] { a, b, c }, 10);
            Assert.IsFalse(result.IsWin);
            Assert.AreEqual(0, result.Payout);
        }

        [TestCase(0, 0, 1)]
        [TestCase(0, 2, 0)]
        [TestCase(3, 0, 0)]
        public void ExactlyTwoSevens_GrantsFreeSpin(int a, int b, int c)
        {
            SpinResult result = evaluator.Evaluate(new[] { a, b, c }, 10);
            Assert.IsFalse(result.IsWin);
            Assert.IsTrue(result.GrantsFreeSpin);
        }

        [Test]
        public void OneSeven_DoesNotGrantFreeSpin()
        {
            Assert.IsFalse(evaluator.Evaluate(new[] { 0, 1, 2 }, 10).GrantsFreeSpin);
        }

        [Test]
        public void TheoreticalMaths_MatchesDesign()
        {
            Assert.AreEqual(0.10, evaluator.CalculateHitFrequency(), 1e-9);
            Assert.AreEqual(0.93, evaluator.CalculateBaseRtp(), 1e-9);
            Assert.AreEqual(0.027, evaluator.CalculateFreeSpinProbability(), 1e-9);
            Assert.AreEqual(0.9558, evaluator.CalculateTheoreticalRtp(), 1e-3);
        }
    }
}
