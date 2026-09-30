using System;
using System.Collections.Generic;
using AgeOfSakura.Core;
using NUnit.Framework;

namespace AgeOfSakura.Tests
{
    public class WalletTests
    {
        [Test]
        public void Add_IncreasesBalance()
        {
            var w = new Wallet();
            w.Add(CurrencyType.Coins, 50, CurrencyTransactionReason.DebugGrant);
            w.Add(CurrencyType.Coins, 25, CurrencyTransactionReason.DebugGrant);
            Assert.AreEqual(75, w.GetBalance(CurrencyType.Coins));
            Assert.AreEqual(0, w.GetBalance(CurrencyType.Wood));
        }

        [Test]
        public void Spend_DeductsExactAmount()
        {
            var w = new Wallet();
            w.Add(CurrencyType.Wood, 30, CurrencyTransactionReason.DebugGrant);
            Assert.IsTrue(w.TrySpend(CurrencyType.Wood, 20, CurrencyTransactionReason.BuildingPurchase));
            Assert.AreEqual(10, w.GetBalance(CurrencyType.Wood));
        }

        [Test]
        public void Spend_CannotOverspend_AndLeavesBalanceUntouched()
        {
            var w = new Wallet();
            w.Add(CurrencyType.Coins, 99, CurrencyTransactionReason.DebugGrant);
            Assert.IsFalse(w.TrySpend(CurrencyType.Coins, 100, CurrencyTransactionReason.BuildingPurchase));
            Assert.AreEqual(99, w.GetBalance(CurrencyType.Coins));
        }

        [Test]
        public void MultiCostSpend_IsAtomic()
        {
            var w = new Wallet();
            w.Add(CurrencyType.Coins, 100, CurrencyTransactionReason.DebugGrant);
            var costs = new List<CurrencyAmount> { new CurrencyAmount(CurrencyType.Coins, 10), new CurrencyAmount(CurrencyType.Wood, 5) };
            Assert.IsFalse(w.CanAfford(costs));
            Assert.IsFalse(w.TrySpend(costs, CurrencyTransactionReason.BuildingPurchase));
            Assert.AreEqual(100, w.GetBalance(CurrencyType.Coins), "Coins must not be deducted when Wood is missing.");
        }

        [Test]
        public void Add_RejectsZeroAndNegative()
        {
            var w = new Wallet();
            Assert.Throws<ArgumentOutOfRangeException>(() => w.Add(CurrencyType.Coins, 0, CurrencyTransactionReason.DebugGrant));
            Assert.Throws<ArgumentOutOfRangeException>(() => w.Add(CurrencyType.Coins, -5, CurrencyTransactionReason.DebugGrant));
        }

        [Test]
        public void Changes_RaiseEventWithReason()
        {
            var w = new Wallet();
            var seen = new List<CurrencyChange>();
            w.CurrencyChanged += seen.Add;
            w.Add(CurrencyType.Diamonds, 3, CurrencyTransactionReason.DebugGrant);
            w.TrySpend(CurrencyType.Diamonds, 2, CurrencyTransactionReason.ProductionSkip);

            Assert.AreEqual(2, seen.Count);
            Assert.AreEqual(CurrencyTransactionReason.ProductionSkip, seen[1].Reason);
            Assert.AreEqual(3, seen[1].OldValue);
            Assert.AreEqual(1, seen[1].NewValue);
        }
    }
}
