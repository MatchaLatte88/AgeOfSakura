using System;
using System.Collections.Generic;
using AgeOfSakura.Core;
using NUnit.Framework;

namespace AgeOfSakura.Tests
{
    public class SaveTests
    {
        private sealed class AddFieldMigration : ISaveMigration
        {
            public int FromVersion => 1;
            public void Apply(Dictionary<string, object> root) => root["migrated"] = true;
        }

        [Test]
        public void RoundTrip_PreservesBalancesAndBuildings()
        {
            var s = TestData.NewSession(out var time, out var storage);
            var w = s.Buildings.TryPlace("woodcutter", new GridPos(5, 5), 0).Instance;
            s.Wallet.Add(CurrencyType.Wood, 7, CurrencyTransactionReason.DebugGrant);
            s.SaveNow();

            var loaded = TestData.Reload(storage, time, out var kind);

            Assert.AreEqual(SessionStartKind.Loaded, kind);
            Assert.AreEqual(400, loaded.Wallet.GetBalance(CurrencyType.Coins));
            Assert.AreEqual(7, loaded.Wallet.GetBalance(CurrencyType.Wood));
            Assert.AreEqual(20, loaded.Wallet.GetBalance(CurrencyType.Diamonds));
            Assert.AreEqual(2, loaded.Buildings.All.Count);
            Assert.IsTrue(loaded.Buildings.TryGet(w.InstanceId, out var restored), "stable instance id must survive");
            Assert.AreEqual(new GridPos(5, 5), restored.Origin);
            Assert.AreEqual(w.Level, restored.Level);
            Assert.AreEqual(w.InstanceId, loaded.Grid.GetCell(6, 6).OccupantId, "occupancy is rebuilt from save");
        }

        [Test]
        public void SaveContainsVersion()
        {
            TestData.NewSession(out _, out var storage);
            StringAssert.Contains("\"version\": 1", storage.Contents);
        }

        [Test]
        public void MovedPosition_Persists()
        {
            var s = TestData.NewSession(out var time, out var storage);
            var w = s.Buildings.TryPlace("woodcutter", new GridPos(5, 5), 0).Instance;
            s.Buildings.TryMove(w.InstanceId, new GridPos(7, 8), 0);
            s.FlushIfDirty();

            var loaded = TestData.Reload(storage, time, out _);
            Assert.AreEqual(new GridPos(7, 8), loaded.Buildings.Get(w.InstanceId).Origin);
        }

        [Test]
        public void ActiveProduction_SurvivesReload()
        {
            var s = TestData.NewSession(out var time, out var storage);
            var w = s.Buildings.TryPlace("woodcutter", new GridPos(5, 5), 0).Instance;
            s.Production.TryStart(w.InstanceId, "wood_large");
            var end = w.ProductionEndUtc;
            s.FlushIfDirty();

            time.AdvanceSeconds(100);
            var loaded = TestData.Reload(storage, time, out _);
            var restored = loaded.Buildings.Get(w.InstanceId);

            Assert.AreEqual(ProductionState.Producing, restored.State);
            Assert.AreEqual("wood_large", restored.ActiveProductionId);
            Assert.AreEqual(end, restored.ProductionEndUtc);
            Assert.AreEqual(TimeSpan.FromSeconds(200), loaded.Production.GetRemaining(restored));
        }

        [Test]
        public void ProductionFinishedWhileClosed_LoadsAsReady_AndPaysExactlyOnce()
        {
            var s = TestData.NewSession(out var time, out var storage);
            var w = s.Buildings.TryPlace("woodcutter", new GridPos(5, 5), 0).Instance;
            s.Production.TryStart(w.InstanceId, "wood_large"); // 5 minutes
            s.FlushIfDirty();

            time.Advance(TimeSpan.FromMinutes(6)); // app closed meanwhile
            var loaded = TestData.Reload(storage, time, out _);
            var restored = loaded.Buildings.Get(w.InstanceId);

            Assert.AreEqual(ProductionState.ReadyToCollect, restored.State);
            Assert.AreEqual(0, loaded.Wallet.GetBalance(CurrencyType.Wood), "reward not yet granted");

            Assert.IsTrue(loaded.Production.TryCollect(w.InstanceId).Success);
            Assert.AreEqual(30, loaded.Wallet.GetBalance(CurrencyType.Wood));
            Assert.IsFalse(loaded.Production.TryCollect(w.InstanceId).Success);
            Assert.AreEqual(30, loaded.Wallet.GetBalance(CurrencyType.Wood));

            // and after another restart the collected state persists without paying again
            loaded.FlushIfDirty();
            var again = TestData.Reload(storage, time, out _);
            Assert.AreEqual(30, again.Wallet.GetBalance(CurrencyType.Wood));
            Assert.AreEqual(ProductionState.Idle, again.Buildings.Get(w.InstanceId).State);
        }

        [Test]
        public void ReadyState_PersistsUncollected()
        {
            var s = TestData.NewSession(out var time, out var storage);
            var w = s.Buildings.TryPlace("woodcutter", new GridPos(5, 5), 0).Instance;
            s.Production.TryStart(w.InstanceId, "wood_small");
            time.AdvanceSeconds(31);
            s.Production.Refresh();
            s.FlushIfDirty();

            var loaded = TestData.Reload(storage, time, out _);
            Assert.AreEqual(ProductionState.ReadyToCollect, loaded.Buildings.Get(w.InstanceId).State);
            Assert.AreEqual(0, loaded.Wallet.GetBalance(CurrencyType.Wood));
        }

        [Test]
        public void MissingSave_StartsFreshGame_AndWritesIt()
        {
            var storage = new InMemorySaveStorage();
            var s = new GameSession(TestData.Definitions(), storage, new FakeTimeProvider());
            Assert.AreEqual(SessionStartKind.NewGame, s.Start());
            Assert.IsNotNull(storage.Contents);
        }

        [Test]
        public void CorruptSave_IsPreserved_AndFreshGameStarts()
        {
            var storage = new InMemorySaveStorage { Contents = "{ this is not json" };
            var s = new GameSession(TestData.Definitions(), storage, new FakeTimeProvider());

            Assert.AreEqual(SessionStartKind.RecoveredFromCorruptSave, s.Start());
            Assert.AreEqual(1, storage.PreservedCorrupt.Count);
            Assert.AreEqual("{ this is not json", storage.PreservedCorrupt[0]);
            Assert.AreEqual(500, s.Wallet.GetBalance(CurrencyType.Coins));
            Assert.AreEqual(1, s.Buildings.All.Count);
        }

        [Test]
        public void HalfValidSave_DoesNotLeakPartialState()
        {
            var seed = TestData.NewSession(out var time, out var storage);
            seed.Buildings.TryPlace("woodcutter", new GridPos(5, 5), 0);
            seed.FlushIfDirty();
            // break the second building's production state so Apply fails after the wallet and first building were restored
            storage.Contents = storage.Contents.Replace("\"Idle\"", "\"Exploding\"");

            var loaded = TestData.Reload(storage, time, out var kind);
            Assert.AreEqual(SessionStartKind.RecoveredFromCorruptSave, kind);
            Assert.AreEqual(500, loaded.Wallet.GetBalance(CurrencyType.Coins));
            Assert.AreEqual(1, loaded.Buildings.All.Count, "only the fresh Town Hall");
        }

        [Test]
        public void UnknownBuildingDefinition_IsSkipped_WithoutAbortingLoad()
        {
            var seed = TestData.NewSession(out var time, out var storage);
            seed.Buildings.TryPlace("woodcutter", new GridPos(5, 5), 0);
            seed.FlushIfDirty();
            storage.Contents = storage.Contents.Replace("\"woodcutter\"", "\"pagoda_from_the_future\"");

            var loaded = TestData.Reload(storage, time, out var kind);
            Assert.AreEqual(SessionStartKind.Loaded, kind);
            Assert.AreEqual(1, loaded.Buildings.All.Count);
            Assert.AreEqual("town_hall", loaded.Buildings.All[0].DefinitionId);
        }

        [Test]
        public void UnknownProduction_ResetsBuildingToIdle()
        {
            var seed = TestData.NewSession(out var time, out var storage);
            var w = seed.Buildings.TryPlace("woodcutter", new GridPos(5, 5), 0).Instance;
            seed.Production.TryStart(w.InstanceId, "wood_small");
            seed.FlushIfDirty();
            storage.Contents = storage.Contents.Replace("\"wood_small\"", "\"wood_removed\"");

            var loaded = TestData.Reload(storage, time, out _);
            Assert.AreEqual(ProductionState.Idle, loaded.Buildings.Get(w.InstanceId).State);
        }

        [Test]
        public void NewerSaveVersion_IsNotOverwritten()
        {
            var seed = TestData.NewSession(out var time, out var storage);
            storage.Contents = storage.Contents.Replace("\"version\": 1", "\"version\": 99");
            string original = storage.Contents;

            var loaded = TestData.Reload(storage, time, out var kind);
            loaded.Wallet.Add(CurrencyType.Coins, 1, CurrencyTransactionReason.DebugGrant);
            loaded.FlushIfDirty();

            Assert.AreEqual(SessionStartKind.SaveTooNew, kind);
            Assert.AreEqual(original, storage.Contents);
        }

        [Test]
        public void Migrator_AppliesStepsInOrder()
        {
            var migrator = new SaveMigrator(2, new ISaveMigration[] { new AddFieldMigration() });
            var root = (Dictionary<string, object>)MiniJson.Parse("{\"version\":1}");
            migrator.Migrate(root);
            Assert.AreEqual(2L, root["version"]);
            Assert.AreEqual(true, root["migrated"]);
        }

        [Test]
        public void Migrator_RejectsMissingVersion()
        {
            var root = (Dictionary<string, object>)MiniJson.Parse("{}");
            Assert.Throws<SaveCorruptException>(() => SaveMigrator.Default.Migrate(root));
        }

        [Test]
        public void DateTimes_AreStoredAsUtc()
        {
            var s = TestData.NewSession(out _, out var storage);
            var save = SaveCodec.Decode(storage.Contents);
            Assert.AreEqual(DateTimeKind.Utc, save.SavedAtUtc.Kind);
            StringAssert.Contains("Z\"", storage.Contents);
        }
    }
}
