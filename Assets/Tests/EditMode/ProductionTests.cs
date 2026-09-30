using System;
using AgeOfSakura.Core;
using NUnit.Framework;

namespace AgeOfSakura.Tests
{
    public class ProductionTests
    {
        private GameSession session;
        private FakeTimeProvider time;
        private BuildingInstance woodcutter;

        [SetUp]
        public void SetUp()
        {
            session = TestData.NewSession(out time, out _);
            woodcutter = session.Buildings.TryPlace("woodcutter", new GridPos(5, 5), 0).Instance;
        }

        [Test]
        public void Start_SetsStartAndEndFromDuration()
        {
            var start = time.UtcNow;
            Assert.AreEqual(StartProductionStatus.Ok, session.Production.TryStart(woodcutter.InstanceId, "wood_medium"));
            Assert.AreEqual(ProductionState.Producing, woodcutter.State);
            Assert.AreEqual(start, woodcutter.ProductionStartUtc);
            Assert.AreEqual(start.AddSeconds(120), woodcutter.ProductionEndUtc);
        }

        [Test]
        public void IncompleteTimer_StaysProducing()
        {
            session.Production.TryStart(woodcutter.InstanceId, "wood_small");
            time.AdvanceSeconds(29);
            session.Production.Refresh();
            Assert.AreEqual(ProductionState.Producing, woodcutter.State);
            Assert.AreEqual(TimeSpan.FromSeconds(1), session.Production.GetRemaining(woodcutter));
        }

        [Test]
        public void ElapsedTimer_BecomesReady_WithoutGrantingReward()
        {
            session.Production.TryStart(woodcutter.InstanceId, "wood_small");
            time.AdvanceSeconds(30);
            session.Production.Refresh();
            Assert.AreEqual(ProductionState.ReadyToCollect, woodcutter.State);
            Assert.AreEqual(0, session.Wallet.GetBalance(CurrencyType.Wood), "reward must wait for manual collection");
        }

        [Test]
        public void Collect_GrantsRewardOnce_AndCannotCollectTwice()
        {
            session.Production.TryStart(woodcutter.InstanceId, "wood_small");
            time.AdvanceSeconds(31);

            var first = session.Production.TryCollect(woodcutter.InstanceId);
            Assert.IsTrue(first.Success);
            Assert.AreEqual(5, session.Wallet.GetBalance(CurrencyType.Wood));
            Assert.AreEqual(ProductionState.Idle, woodcutter.State);

            var second = session.Production.TryCollect(woodcutter.InstanceId);
            Assert.AreEqual(CollectStatus.NotReady, second.Status);
            Assert.AreEqual(5, session.Wallet.GetBalance(CurrencyType.Wood));
        }

        [Test]
        public void Collect_BeforeFinished_IsRejected()
        {
            session.Production.TryStart(woodcutter.InstanceId, "wood_small");
            time.AdvanceSeconds(10);
            Assert.AreEqual(CollectStatus.NotReady, session.Production.TryCollect(woodcutter.InstanceId).Status);
            Assert.AreEqual(0, session.Wallet.GetBalance(CurrencyType.Wood));
        }

        [Test]
        public void CannotStart_WhileBusy_OrWithForeignRecipe()
        {
            Assert.AreEqual(StartProductionStatus.Ok, session.Production.TryStart(woodcutter.InstanceId, "wood_small"));
            Assert.AreEqual(StartProductionStatus.NotIdle, session.Production.TryStart(woodcutter.InstanceId, "wood_large"));

            var other = session.Buildings.TryPlace("woodcutter", new GridPos(7, 5), 0).Instance;
            Assert.AreEqual(StartProductionStatus.NotAllowedForBuilding, session.Production.TryStart(other.InstanceId, "coins_house_basic"));
            Assert.AreEqual(StartProductionStatus.UnknownProduction, session.Production.TryStart(other.InstanceId, "nope"));
        }

        [TestCase(30, 1)]
        [TestCase(120, 2)]
        [TestCase(300, 5)]
        public void DiamondSkip_DeductsCostFromRemainingTime(int recipeSeconds, int expectedCost)
        {
            string recipe = recipeSeconds == 30 ? "wood_small" : recipeSeconds == 120 ? "wood_medium" : "wood_large";
            session.Production.TryStart(woodcutter.InstanceId, recipe);

            var result = session.Production.TrySkip(woodcutter.InstanceId);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(expectedCost, result.Cost);
            Assert.AreEqual(20 - expectedCost, session.Wallet.GetBalance(CurrencyType.Diamonds));
            Assert.AreEqual(ProductionState.ReadyToCollect, woodcutter.State);
            Assert.AreEqual(0, session.Wallet.GetBalance(CurrencyType.Wood), "skipping must not grant the reward");
        }

        [Test]
        public void DiamondSkip_PriceDropsAsTimePasses()
        {
            session.Production.TryStart(woodcutter.InstanceId, "wood_large"); // 300s
            time.AdvanceSeconds(200); // 100s left -> ceil(100/60) = 2
            Assert.AreEqual(2, session.Production.GetSkipCost(woodcutter));
        }

        [Test]
        public void DiamondSkip_WithoutEnoughDiamonds_ChargesNothing()
        {
            session.Wallet.TrySpend(CurrencyType.Diamonds, 20, CurrencyTransactionReason.DebugGrant);
            session.Production.TryStart(woodcutter.InstanceId, "wood_small");
            var r = session.Production.TrySkip(woodcutter.InstanceId);
            Assert.AreEqual(SkipStatus.InsufficientDiamonds, r.Status);
            Assert.AreEqual(ProductionState.Producing, woodcutter.State);
        }

        [Test]
        public void DiamondSkip_OnAlreadyFinishedTimer_IsFree()
        {
            session.Production.TryStart(woodcutter.InstanceId, "wood_small");
            time.AdvanceSeconds(45);
            var r = session.Production.TrySkip(woodcutter.InstanceId);
            Assert.AreEqual(SkipStatus.NotProducing, r.Status);
            Assert.AreEqual(20, session.Wallet.GetBalance(CurrencyType.Diamonds));
        }

        [Test]
        public void ClockMovedBackwards_DoesNotExtendRemainingBeyondDuration()
        {
            session.Production.TryStart(woodcutter.InstanceId, "wood_small");
            time.Advance(TimeSpan.FromHours(-5));
            Assert.AreEqual(TimeSpan.FromSeconds(30), session.Production.GetRemaining(woodcutter));
        }

        [Test]
        public void House_ProducesCoins()
        {
            session.Wallet.Add(CurrencyType.Wood, 20, CurrencyTransactionReason.DebugGrant);
            var house = session.Buildings.TryPlace("house", new GridPos(7, 5), 0).Instance;
            Assert.AreEqual(0, session.Wallet.GetBalance(CurrencyType.Wood));

            long coinsBefore = session.Wallet.GetBalance(CurrencyType.Coins);
            session.Wallet.Add(CurrencyType.Rice, 2, CurrencyTransactionReason.DebugGrant);
            session.Production.TryStart(house.InstanceId, "coins_house_basic");
            time.AdvanceSeconds(60);
            session.Production.Refresh();
            Assert.IsTrue(session.Production.TryCollect(house.InstanceId).Success);
            Assert.AreEqual(coinsBefore + 25, session.Wallet.GetBalance(CurrencyType.Coins));
        }

        [Test]
        public void Events_FireForStartCompleteCollect()
        {
            int started = 0, completed = 0, collected = 0;
            session.Production.ProductionStarted += _ => started++;
            session.Production.ProductionCompleted += _ => completed++;
            session.Production.ProductionCollected += (_, __) => collected++;

            session.Production.TryStart(woodcutter.InstanceId, "wood_small");
            time.AdvanceSeconds(30);
            session.Production.Refresh();
            session.Production.Refresh(); // second refresh must not re-fire
            session.Production.TryCollect(woodcutter.InstanceId);

            Assert.AreEqual(1, started);
            Assert.AreEqual(1, completed);
            Assert.AreEqual(1, collected);
        }
    }
}
