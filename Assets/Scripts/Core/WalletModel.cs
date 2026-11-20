using System;
using System.Collections.Generic;

namespace SlotGame.Core
{
    /// <summary>
    /// Owns the player's credits, current bet and free spins. UI listens to the
    /// events instead of polling, which keeps the model and views decoupled.
    /// </summary>
    public sealed class WalletModel
    {
        private readonly IReadOnlyList<int> betOptions;
        private readonly int startingCredits;
        private readonly int defaultBetIndex;
        private int betIndex;

        public event Action<int> BalanceChanged;
        public event Action<int> BetChanged;
        public event Action<int> FreeSpinsChanged;

        public WalletModel(int startingCredits, IReadOnlyList<int> betOptions, int defaultBetIndex)
        {
            if (betOptions == null || betOptions.Count == 0)
                throw new ArgumentException("At least one bet option is required.", nameof(betOptions));

            this.startingCredits = startingCredits;
            this.betOptions = betOptions;
            this.defaultBetIndex = defaultBetIndex;
            Reset();
        }

        public int Balance { get; private set; }
        public int FreeSpins { get; private set; }
        public int CurrentBet => betOptions[betIndex];
        public int MinBet => betOptions[0];

        /// <summary>True when the player can start a spin (free spin available or enough credits).</summary>
        public bool CanSpin => FreeSpins > 0 || Balance >= CurrentBet;

        /// <summary>True when not even the cheapest bet is affordable and no free spins remain: game over.</summary>
        public bool IsBroke => FreeSpins == 0 && Balance < MinBet;

        /// <summary>
        /// Pays for a spin. A free spin is consumed first; otherwise the bet is deducted.
        /// Returns false (and changes nothing) when the player cannot afford it.
        /// </summary>
        public bool TryPlaceBet(out bool usedFreeSpin)
        {
            usedFreeSpin = false;
            if (FreeSpins > 0)
            {
                FreeSpins--;
                usedFreeSpin = true;
                FreeSpinsChanged?.Invoke(FreeSpins);
                return true;
            }

            if (Balance < CurrentBet) return false;
            Balance -= CurrentBet;
            BalanceChanged?.Invoke(Balance);
            return true;
        }

        public void Credit(int amount)
        {
            if (amount <= 0) return;
            Balance += amount;
            BalanceChanged?.Invoke(Balance);
        }

        public void AddFreeSpins(int count)
        {
            if (count <= 0) return;
            FreeSpins += count;
            FreeSpinsChanged?.Invoke(FreeSpins);
        }

        /// <summary>Moves the bet selector up (+1) or down (-1), clamped to the available options.</summary>
        public void ChangeBet(int direction)
        {
            int next = Math.Max(0, Math.Min(betOptions.Count - 1, betIndex + Math.Sign(direction)));
            if (next == betIndex) return;
            betIndex = next;
            BetChanged?.Invoke(CurrentBet);
        }

        /// <summary>Lowers the bet to the highest option the player can still afford.</summary>
        public void ClampBetToBalance()
        {
            int target = betIndex;
            while (target > 0 && betOptions[target] > Balance) target--;
            if (target == betIndex) return;
            betIndex = target;
            BetChanged?.Invoke(CurrentBet);
        }

        public void Reset()
        {
            Balance = startingCredits;
            FreeSpins = 0;
            betIndex = Math.Max(0, Math.Min(betOptions.Count - 1, defaultBetIndex));
            BalanceChanged?.Invoke(Balance);
            FreeSpinsChanged?.Invoke(FreeSpins);
            BetChanged?.Invoke(CurrentBet);
        }
    }
}
