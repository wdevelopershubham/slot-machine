using NUnit.Framework;
using SlotGame.Core;

namespace SlotGame.Tests
{
    public class WalletModelTests
    {
        private static WalletModel Create() => new WalletModel(100, new[] { 5, 10, 25 }, 1);

        [Test]
        public void StartsWithDefaultBetAndBalance()
        {
            var wallet = Create();
            Assert.AreEqual(100, wallet.Balance);
            Assert.AreEqual(10, wallet.CurrentBet);
        }

        [Test]
        public void PlacingBet_DeductsStake()
        {
            var wallet = Create();
            Assert.IsTrue(wallet.TryPlaceBet(out bool free));
            Assert.IsFalse(free);
            Assert.AreEqual(90, wallet.Balance);
        }

        [Test]
        public void FreeSpin_IsConsumedBeforeCredits()
        {
            var wallet = Create();
            wallet.AddFreeSpins(1);
            Assert.IsTrue(wallet.TryPlaceBet(out bool free));
            Assert.IsTrue(free);
            Assert.AreEqual(100, wallet.Balance);
            Assert.AreEqual(0, wallet.FreeSpins);
        }

        [Test]
        public void CannotBet_WhenBroke()
        {
            var wallet = new WalletModel(3, new[] { 5, 10 }, 0);
            Assert.IsFalse(wallet.TryPlaceBet(out _));
            Assert.IsTrue(wallet.IsBroke);
            Assert.AreEqual(3, wallet.Balance);
        }

        [Test]
        public void BetChange_IsClampedToOptions()
        {
            var wallet = Create();
            wallet.ChangeBet(-1); wallet.ChangeBet(-1);
            Assert.AreEqual(5, wallet.CurrentBet);
            wallet.ChangeBet(+1); wallet.ChangeBet(+1); wallet.ChangeBet(+1);
            Assert.AreEqual(25, wallet.CurrentBet);
        }

        [Test]
        public void BetCannotBeRaisedAboveBalance()
        {
            var wallet = new WalletModel(12, new[] { 5, 10, 25 }, 1);
            wallet.ChangeBet(+1);
            Assert.AreEqual(10, wallet.CurrentBet);
        }

        [Test]
        public void ClampBetToBalance_LowersStake()
        {
            var wallet = new WalletModel(100, new[] { 5, 10, 25, 50 }, 3);
            wallet.TryPlaceBet(out _);              // 100 - 50 = 50
            wallet.TryPlaceBet(out _);              // 50 - 50 = 0
            wallet.Credit(12);
            wallet.ClampBetToBalance();
            Assert.AreEqual(10, wallet.CurrentBet);
        }

        [Test]
        public void Reset_RestoresStartingState()
        {
            var wallet = Create();
            wallet.TryPlaceBet(out _);
            wallet.AddFreeSpins(2);
            wallet.Reset();
            Assert.AreEqual(100, wallet.Balance);
            Assert.AreEqual(0, wallet.FreeSpins);
        }
    }
}
