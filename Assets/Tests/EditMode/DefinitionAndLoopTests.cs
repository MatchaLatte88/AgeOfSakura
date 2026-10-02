using System.Collections.Generic;
using AgeOfSakura.Core;
using NUnit.Framework;

namespace AgeOfSakura.Tests
{
    public class DefinitionTests
    {
        [Test]
        public void ShippedDefinitions_AreValid()
        {
            var defs = TestData.Definitions();
            Assert.AreEqual(10, defs.Buildings.Count);
            Assert.AreEqual(3, defs.GetBuilding("woodcutter").ProductionIds.Count);
            Assert.AreEqual(3, defs.GetBuilding("house").MaxLevel);
        }

        [Test]
        public void Validator_ReportsDuplicateAndInvalidData()
        {
            var defs = TestData.Definitions();
            defs.Buildings.Add(defs.Buildings[1]); // duplicate id
            defs.Productions[0].DurationSeconds = 0;
            defs.Productions[1].Rewards.Clear();
            defs.Buildings[1].BuildCost[0] = new CurrencyAmount(CurrencyType.Coins, -5);
            defs.Buildings[2].FootprintWidth = 0;
            defs.Buildings[2].ProductionIds.Add("does_not_exist");

            var errors = DefinitionValidator.Validate(defs);

            Assert.That(errors, Has.Some.Contain("duplicate id"));
            Assert.That(errors, Has.Some.Contain("durationSeconds"));
            Assert.That(errors, Has.Some.Contain("no reward"));
            Assert.That(errors, Has.Some.Contain("invalid amount"));
            Assert.That(errors, Has.Some.Contain("invalid footprint"));
            Assert.That(errors, Has.Some.Contain("unknown production"));
        }

        [Test]
        public void Validator_ReportsMissingModel()
        {
            var errors = DefinitionValidator.Validate(TestData.Definitions(), visualId => visualId != "woodcutter");
            Assert.That(errors, Has.Some.Contain("no model registered"));
        }

        [Test]
        public void Loader_ThrowsOnInvalidMap()
        {
            string json = TestData.DefinitionsJson();
            string firstRow = TestData.Definitions().Map.Rows[0];
            json = json.Replace("\"" + firstRow + "\"", "\"short\"");
            Assert.AreNotEqual(TestData.DefinitionsJson(), json, "the test must actually damage the map");
            Assert.Throws<DefinitionException>(() => DefinitionLoader.LoadValidated(json));
        }

        [Test]
        public void Loader_ThrowsOnBrokenJson()
        {
            Assert.Throws<DefinitionException>(() => DefinitionLoader.LoadValidated("{ nope"));
        }
    }

    /// <summary>Plays the loop of PROTOTYPE.md section 53 / 83 against the core with a fake clock.</summary>
    public class EconomyLoopTests
    {
        [Test]
        public void FreshSave_ReachesSelfSustainingEconomy()
        {
            var s = TestData.NewSession(out var time, out var storage);

            // Woodcutter for 100 coins
            var wood = s.Buildings.TryPlace("woodcutter", new GridPos(5, 5), 0);
            Assert.IsTrue(wood.Success);
            Assert.AreEqual(400, s.Wallet.GetBalance(CurrencyType.Coins));

            // overlapping second placement is rejected
            Assert.IsFalse(s.Buildings.TryPlace("woodcutter", new GridPos(5, 6), 0).Success);

            // 30s production, normal completion, collect
            Assert.AreEqual(StartProductionStatus.Ok, s.Production.TryStart(wood.Instance.InstanceId, "wood_small"));
            time.AdvanceSeconds(30);
            s.Production.Refresh();
            Assert.IsTrue(s.Production.TryCollect(wood.Instance.InstanceId).Success);
            Assert.AreEqual(5, s.Wallet.GetBalance(CurrencyType.Wood));

            // second production finished via diamonds
            s.Production.TryStart(wood.Instance.InstanceId, "wood_large");
            var skip = s.Production.TrySkip(wood.Instance.InstanceId);
            Assert.IsTrue(skip.Success);
            Assert.AreEqual(20 - skip.Cost, s.Wallet.GetBalance(CurrencyType.Diamonds));
            Assert.IsTrue(s.Production.TryCollect(wood.Instance.InstanceId).Success);
            Assert.AreEqual(35, s.Wallet.GetBalance(CurrencyType.Wood));

            // house for 20 wood, produce coins, close app, come back later
            var house = s.Buildings.TryPlace("house", new GridPos(7, 5), 0);
            Assert.IsTrue(house.Success);
            Assert.AreEqual(15, s.Wallet.GetBalance(CurrencyType.Wood));
            // taxes need rice from the store: without it the house cannot start
            Assert.AreEqual(StartProductionStatus.MissingInputs, s.Production.TryStart(house.Instance.InstanceId, "coins_house_basic"));
            s.Wallet.Add(CurrencyType.Rice, 2, CurrencyTransactionReason.DebugGrant);
            Assert.AreEqual(StartProductionStatus.Ok, s.Production.TryStart(house.Instance.InstanceId, "coins_house_basic"));
            Assert.AreEqual(0, s.Wallet.GetBalance(CurrencyType.Rice), "the house consumed its rice");
            s.FlushIfDirty();

            time.AdvanceSeconds(90);
            var reopened = TestData.Reload(storage, time, out _);
            var reopenedHouse = reopened.Buildings.Get(house.Instance.InstanceId);
            Assert.AreEqual(ProductionState.ReadyToCollect, reopenedHouse.State);

            long coins = reopened.Wallet.GetBalance(CurrencyType.Coins);
            Assert.IsTrue(reopened.Production.TryCollect(house.Instance.InstanceId).Success);
            Assert.AreEqual(coins + 25, reopened.Wallet.GetBalance(CurrencyType.Coins));

            // move the house, restart, position and balances persist
            Assert.AreEqual(PlacementCheck.Ok, reopened.Buildings.TryMove(house.Instance.InstanceId, new GridPos(7, 8), 0));
            reopened.FlushIfDirty();
            long finalCoins = reopened.Wallet.GetBalance(CurrencyType.Coins);
            long finalWood = reopened.Wallet.GetBalance(CurrencyType.Wood);
            long finalDiamonds = reopened.Wallet.GetBalance(CurrencyType.Diamonds);

            var last = TestData.Reload(storage, time, out _);
            Assert.AreEqual(new GridPos(7, 8), last.Buildings.Get(house.Instance.InstanceId).Origin);
            Assert.AreEqual(finalCoins, last.Wallet.GetBalance(CurrencyType.Coins));
            Assert.AreEqual(finalWood, last.Wallet.GetBalance(CurrencyType.Wood));
            Assert.AreEqual(finalDiamonds, last.Wallet.GetBalance(CurrencyType.Diamonds));
        }

        [Test]
        public void AnalyticsHooks_ReceiveEvents()
        {
            var events = new List<string>();
            var analytics = new RecordingAnalytics(events);
            var time = new FakeTimeProvider();
            var s = new GameSession(TestData.Definitions(), new InMemorySaveStorage(), time, analytics);
            s.Start();

            var w = s.Buildings.TryPlace("woodcutter", new GridPos(5, 5), 0).Instance;
            s.Production.TryStart(w.InstanceId, "wood_small");
            s.Production.TrySkip(w.InstanceId);
            s.Production.TryCollect(w.InstanceId);

            Assert.That(events, Does.Contain(AnalyticsEvents.BuildingPlaced));
            Assert.That(events, Does.Contain(AnalyticsEvents.ProductionStarted));
            Assert.That(events, Does.Contain(AnalyticsEvents.ProductionSkipped));
            Assert.That(events, Does.Contain(AnalyticsEvents.ProductionCollected));
            Assert.That(events, Does.Contain(AnalyticsEvents.CurrencySpent));
        }

        private sealed class RecordingAnalytics : IAnalyticsService
        {
            private readonly List<string> events;
            public RecordingAnalytics(List<string> events) { this.events = events; }
            public void Track(string eventName, IReadOnlyDictionary<string, object> parameters = null) => events.Add(eventName);
        }
    }
}
