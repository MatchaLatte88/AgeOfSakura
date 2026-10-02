using System.Linq;
using AgeOfSakura.Core;
using NUnit.Framework;

namespace AgeOfSakura.Tests
{
    /// <summary>Supply chain (production inputs), environment values, the house upgrade rules and the first-playthrough advisor.</summary>
    public class HousingTests
    {
        private GameSession session;
        private FakeTimeProvider time;
        private InMemorySaveStorage storage;

        private static readonly GridPos HousePos = new GridPos(7, 7);
        private static readonly GridPos GardenPos = new GridPos(9, 7);
        private static readonly GridPos ShrinePos = new GridPos(5, 7);
        private static readonly GridPos FarPos = new GridPos(13, 13);

        [SetUp]
        public void SetUp()
        {
            session = TestData.NewSession(out time, out storage);
            session.Wallet.Add(CurrencyType.Coins, 1000, CurrencyTransactionReason.DebugGrant);
            session.Wallet.Add(CurrencyType.Wood, 400, CurrencyTransactionReason.DebugGrant);
            session.Wallet.Add(CurrencyType.Rice, 100, CurrencyTransactionReason.DebugGrant);
            session.Wallet.Add(CurrencyType.Tools, 100, CurrencyTransactionReason.DebugGrant);
            session.Wallet.Add(CurrencyType.Fish, 100, CurrencyTransactionReason.DebugGrant);
            session.Wallet.Add(CurrencyType.Iron, 100, CurrencyTransactionReason.DebugGrant);
            // two road cells lead from the Town Hall (9..11 x 10..12) past the house at HousePos, so level 2 can ask for a road connection
            Assert.IsTrue(session.Buildings.TryPlace("road", new GridPos(9, 9), 0).Success);
            Assert.IsTrue(session.Buildings.TryPlace("road", new GridPos(8, 9), 0).Success);
        }

        private BuildingInstance Place(string id, GridPos pos)
        {
            var r = session.Buildings.TryPlace(id, pos, 0);
            Assert.IsTrue(r.Success, $"placing {id} at {pos}: {r.Status} {r.Check}");
            return r.Instance;
        }

        /// <summary>Runs one complete tax cycle of the house (rice is consumed at the start, coins arrive on collect).</summary>
        private void RunCycle(BuildingInstance house)
        {
            string production = session.Buildings.GetDefinition(house).GetProductionIds(house.Level)[0];
            Assert.AreEqual(StartProductionStatus.Ok, session.Production.TryStart(house.InstanceId, production));
            time.AdvanceSeconds(120);
            session.Production.Refresh();
            Assert.IsTrue(session.Production.TryCollect(house.InstanceId).Success);
        }

        // ---------------------------------------------------------------- supply chain

        [Test]
        public void ProductionInputs_AreSpentAtStart()
        {
            var house = Place("house", HousePos);
            long rice = session.Wallet.GetBalance(CurrencyType.Rice);
            Assert.AreEqual(StartProductionStatus.Ok, session.Production.TryStart(house.InstanceId, "coins_house_basic"));
            Assert.AreEqual(rice - 2, session.Wallet.GetBalance(CurrencyType.Rice));
        }

        [Test]
        public void ProductionWithoutInputs_IsRefused_AndChangesNothing()
        {
            var house = Place("house", HousePos);
            session.Wallet.TrySpend(CurrencyType.Rice, 99, CurrencyTransactionReason.DebugGrant);
            Assert.AreEqual(StartProductionStatus.MissingInputs, session.Production.TryStart(house.InstanceId, "coins_house_basic"));
            Assert.AreEqual(ProductionState.Idle, house.State);
            Assert.AreEqual(1, session.Wallet.GetBalance(CurrencyType.Rice));
        }

        [Test]
        public void RicePaddy_ProducesRice_ThatFeedsHouses()
        {
            session.Wallet.TrySpend(CurrencyType.Rice, 100, CurrencyTransactionReason.DebugGrant);
            var paddy = Place("rice_paddy", FarPos);
            var house = Place("house", HousePos);
            Assert.AreEqual(StartProductionStatus.MissingInputs, session.Production.TryStart(house.InstanceId, "coins_house_basic"));

            Assert.AreEqual(StartProductionStatus.Ok, session.Production.TryStart(paddy.InstanceId, "rice_small"));
            time.AdvanceSeconds(45);
            session.Production.Refresh();
            Assert.IsTrue(session.Production.TryCollect(paddy.InstanceId).Success);
            Assert.AreEqual(6, session.Wallet.GetBalance(CurrencyType.Rice));
            Assert.AreEqual(StartProductionStatus.Ok, session.Production.TryStart(house.InstanceId, "coins_house_basic"));
        }

        [Test]
        public void SkippingRunningProduction_DoesNotRefundInputs()
        {
            var house = Place("house", HousePos);
            session.Production.TryStart(house.InstanceId, "coins_house_basic");
            long rice = session.Wallet.GetBalance(CurrencyType.Rice);
            Assert.IsTrue(session.Production.TrySkip(house.InstanceId).Success);
            Assert.AreEqual(rice, session.Wallet.GetBalance(CurrencyType.Rice));
        }

        // ---------------------------------------------------------------- environment

        [Test]
        public void Garden_AddsBeauty_WithinRadiusOnly()
        {
            var house = Place("house", HousePos);
            float before = session.Housing.GetEnvironment(house, EnvironmentVariable.Beauty);
            var garden = Place("garden", GardenPos);
            Assert.AreEqual(before + 3f, session.Housing.GetEnvironment(house, EnvironmentVariable.Beauty), 0.001f);

            Assert.AreEqual(PlacementCheck.Ok, session.Buildings.TryMove(garden.InstanceId, FarPos, 0));
            Assert.AreEqual(before, session.Housing.GetEnvironment(house, EnvironmentVariable.Beauty), 0.001f, "a distant garden does not count");
        }

        [Test]
        public void Woodcutter_AddsNoise_AndMovingItAwayRemovesIt()
        {
            var house = Place("house", HousePos);
            var cutter = Place("woodcutter", new GridPos(5, 5));
            Assert.AreEqual(3f, session.Housing.GetEnvironment(house, EnvironmentVariable.Noise), 0.001f);
            session.Buildings.TryMove(cutter.InstanceId, FarPos, 0);
            Assert.AreEqual(0f, session.Housing.GetEnvironment(house, EnvironmentVariable.Noise), 0.001f);
        }

        [Test]
        public void BuildingDoesNotAffectItself()
        {
            var garden = Place("garden", GardenPos);
            Assert.AreEqual(session.Housing.GetEnvironment(garden, EnvironmentVariable.Beauty),
                session.Housing.GetEnvironment(garden, EnvironmentVariable.Beauty));
            var lonely = Place("shrine", FarPos);
            Assert.AreEqual(0f, session.Housing.GetEnvironment(lonely, EnvironmentVariable.Faith), 0.001f);
        }

        [Test]
        public void Terrain_CherryTreeAddsBeauty()
        {
            // the map has a cherry tree at (5,14); a house two cells away sees it, a house far away does not
            var near = Place("house", new GridPos(5, 12));
            var far = Place("house", HousePos);
            Assert.Greater(session.Housing.GetEnvironment(near, EnvironmentVariable.Beauty), session.Housing.GetEnvironment(far, EnvironmentVariable.Beauty));
        }

        // ---------------------------------------------------------------- upgrade

        [Test]
        public void Upgrade_IsRefusedWhileNeedsAreUnmet_AndCostsNothing()
        {
            var house = Place("house", HousePos);
            long coins = session.Wallet.GetBalance(CurrencyType.Coins), wood = session.Wallet.GetBalance(CurrencyType.Wood);
            var info = session.Housing.Evaluate(house);
            Assert.IsFalse(info.NeedsMet);
            Assert.IsFalse(info.CanUpgrade);
            Assert.AreEqual(UpgradeStatus.NeedsNotMet, session.Housing.TryUpgrade(house.InstanceId));
            Assert.AreEqual(1, house.Level);
            Assert.AreEqual(coins, session.Wallet.GetBalance(CurrencyType.Coins));
            Assert.AreEqual(wood, session.Wallet.GetBalance(CurrencyType.Wood));
        }

        [Test]
        public void Upgrade_NeedsBothServedCyclesAndBeauty()
        {
            var house = Place("house", HousePos);
            RunCycle(house);
            RunCycle(house);
            Assert.AreEqual(2, house.CyclesAtLevel);
            var info = session.Housing.Evaluate(house);
            Assert.IsTrue(info.Needs.Count > 0 && !info.NeedsMet, "served is met, beauty is not");
            Assert.IsTrue(info.Needs[0].Met);
            Assert.IsFalse(info.Needs[1].Met);

            Place("garden", GardenPos);
            Assert.IsTrue(session.Housing.Evaluate(house).NeedsMet);
        }

        [Test]
        public void Upgrade_CannotAfford_KeepsEverything()
        {
            var house = Place("house", HousePos);
            RunCycle(house);
            RunCycle(house);
            Place("garden", GardenPos);
            session.Wallet.TrySpend(CurrencyType.Coins, session.Wallet.GetBalance(CurrencyType.Coins), CurrencyTransactionReason.DebugGrant);
            long wood = session.Wallet.GetBalance(CurrencyType.Wood);
            Assert.AreEqual(UpgradeStatus.CannotAfford, session.Housing.TryUpgrade(house.InstanceId));
            Assert.AreEqual(1, house.Level);
            Assert.AreEqual(wood, session.Wallet.GetBalance(CurrencyType.Wood));
        }

        [Test]
        public void Upgrade_PaysRaisesLevelResetsCyclesAndSwitchesRecipe()
        {
            var house = Place("house", HousePos);
            RunCycle(house);
            RunCycle(house);
            Place("garden", GardenPos);
            long coins = session.Wallet.GetBalance(CurrencyType.Coins), wood = session.Wallet.GetBalance(CurrencyType.Wood);
            BuildingInstance upgraded = null;
            session.Housing.BuildingUpgraded += b => upgraded = b;

            Assert.AreEqual(UpgradeStatus.Ok, session.Housing.TryUpgrade(house.InstanceId));

            Assert.AreEqual(2, house.Level);
            Assert.AreEqual(0, house.CyclesAtLevel);
            Assert.AreSame(house, upgraded);
            Assert.AreEqual(coins - 100, session.Wallet.GetBalance(CurrencyType.Coins));
            Assert.AreEqual(wood - 40, session.Wallet.GetBalance(CurrencyType.Wood));
            Assert.AreEqual(StartProductionStatus.NotAllowedForBuilding, session.Production.TryStart(house.InstanceId, "coins_house_basic"));

            long coinsBefore = session.Wallet.GetBalance(CurrencyType.Coins);
            RunCycle(house); // uses coins_house_l2: 3 rice -> 60 coins
            Assert.AreEqual(coinsBefore + 60, session.Wallet.GetBalance(CurrencyType.Coins));
        }

        [Test]
        public void LevelThree_NeedsFaithAndQuiet()
        {
            var house = Place("house", HousePos);
            var garden = Place("garden", GardenPos);
            RunCycle(house);
            RunCycle(house);
            Assert.AreEqual(UpgradeStatus.Ok, session.Housing.TryUpgrade(house.InstanceId));
            for (int i = 0; i < 4; i++) RunCycle(house);

            var cutter = Place("woodcutter", new GridPos(5, 5));
            var shrine = Place("shrine", ShrinePos);
            var second = Place("garden", new GridPos(11, 5));
            Assert.AreNotEqual(garden.InstanceId, second.InstanceId);

            var info = session.Housing.Evaluate(house);
            Assert.IsTrue(info.Needs.Any(n => n.Requirement.Type == NeedType.Noise && !n.Met), "the woodcutter is too loud");
            Assert.AreEqual(UpgradeStatus.NeedsNotMet, session.Housing.TryUpgrade(house.InstanceId));

            session.Buildings.TryMove(cutter.InstanceId, FarPos, 0);
            Assert.IsTrue(session.Housing.Evaluate(house).NeedsMet, "beauty from gardens and shrine, faith from the shrine, no noise");
            Assert.AreEqual(UpgradeStatus.Ok, session.Housing.TryUpgrade(house.InstanceId));
            Assert.AreEqual(3, house.Level);
            Assert.IsTrue(session.Housing.Evaluate(house).IsMaxLevel);
            Assert.AreEqual(UpgradeStatus.MaxLevel, session.Housing.TryUpgrade(house.InstanceId));
            Assert.IsNotNull(shrine);
        }

        [Test]
        public void NonUpgradableBuilding_IsRefused()
        {
            var cutter = Place("woodcutter", new GridPos(5, 5));
            Assert.AreEqual(UpgradeStatus.NotUpgradable, session.Housing.TryUpgrade(cutter.InstanceId));
            Assert.AreEqual(UpgradeStatus.UnknownBuilding, session.Housing.TryUpgrade("nope"));
            Assert.IsTrue(session.Housing.Evaluate(cutter).IsMaxLevel);
        }

        // ---------------------------------------------------------------- save

        [Test]
        public void LevelAndCycles_SurviveSaveAndReload()
        {
            var house = Place("house", HousePos);
            RunCycle(house);
            RunCycle(house);
            Place("garden", GardenPos);
            session.Housing.TryUpgrade(house.InstanceId);
            RunCycle(house);
            session.FlushIfDirty();

            var loaded = TestData.Reload(storage, time, out var kind);
            Assert.AreEqual(SessionStartKind.Loaded, kind);
            var loadedHouse = loaded.Buildings.Get(house.InstanceId);
            Assert.AreEqual(2, loadedHouse.Level);
            Assert.AreEqual(1, loadedHouse.CyclesAtLevel);
            Assert.AreEqual(session.Wallet.GetBalance(CurrencyType.Rice), loaded.Wallet.GetBalance(CurrencyType.Rice));
        }

        [Test]
        public void SaveWithoutCycleField_LoadsWithZero()
        {
            var house = Place("house", HousePos);
            session.FlushIfDirty();
            storage.Contents = storage.Contents.Replace("\"cyclesAtLevel\"", "\"ignored\"");
            var loaded = TestData.Reload(storage, time, out var kind);
            Assert.AreEqual(SessionStartKind.Loaded, kind);
            Assert.AreEqual(0, loaded.Buildings.Get(house.InstanceId).CyclesAtLevel);
        }

        [Test]
        public void SavedLevelAboveMaximum_IsClamped()
        {
            var house = Place("house", HousePos);
            house.Level = 9;
            session.SaveNow();
            var loaded = TestData.Reload(storage, time, out _);
            Assert.AreEqual(3, loaded.Buildings.Get(house.InstanceId).Level);
        }

        // ---------------------------------------------------------------- definitions

        [Test]
        public void Validator_ReportsBrokenLevelsAndEffects()
        {
            var defs = TestData.Definitions();
            var house = defs.GetBuilding("house");
            house.Levels[1].UpgradeCost.Clear();
            house.Levels[2].Level = 5;
            house.Levels[1].Needs.Add(new NeedRequirement());
            defs.GetBuilding("garden").Emits[0].Radius = 0f;
            defs.Productions[0].Inputs.Add(new CurrencyAmount(CurrencyType.Rice, 0));

            var errors = DefinitionValidator.Validate(defs);

            Assert.That(errors, Has.Some.Contain("upgradeCost is required"));
            Assert.That(errors, Has.Some.Contain("levels must be listed"));
            Assert.That(errors, Has.Some.Contain("neither min nor max"));
            Assert.That(errors, Has.Some.Contain("radius must be > 0"));
            Assert.That(errors, Has.Some.Contain("input: invalid amount"));
        }

        // ---------------------------------------------------------------- advisor

        [Test]
        public void Advisor_LeadsThroughTheFirstLoop()
        {
            var fresh = TestData.NewSession(out var t, out _);
            Assert.AreEqual(GoalKind.Build, GoalAdvisor.Next(fresh).Kind);
            Assert.AreEqual("woodcutter", GoalAdvisor.Next(fresh).BuildingId);

            fresh.Buildings.TryPlace("woodcutter", new GridPos(5, 5), 0);
            var goal = GoalAdvisor.Next(fresh);
            Assert.AreEqual(GoalKind.Build, goal.Kind);
            Assert.AreEqual("rice_paddy", goal.BuildingId);

            fresh.Buildings.TryPlace("rice_paddy", new GridPos(13, 13), 0);
            goal = GoalAdvisor.Next(fresh);
            Assert.AreEqual(GoalKind.Gather, goal.Kind, "the house needs 20 wood");
            Assert.AreEqual(CurrencyType.Wood, goal.Currency);
            Assert.AreEqual(20, goal.Amount);

            fresh.Wallet.Add(CurrencyType.Wood, 20, CurrencyTransactionReason.DebugGrant);
            fresh.Buildings.TryPlace("house", HousePos, 0);
            Assert.AreEqual(GoalKind.HarvestRice, GoalAdvisor.Next(fresh).Kind, "no rice yet");

            fresh.Wallet.Add(CurrencyType.Rice, 2, CurrencyTransactionReason.DebugGrant);
            Assert.AreEqual(GoalKind.StartTaxes, GoalAdvisor.Next(fresh).Kind);

            var house = TestData.First(fresh, "house");
            fresh.Production.TryStart(house.InstanceId, "coins_house_basic");
            t.AdvanceSeconds(61);
            fresh.Production.Refresh();
            Assert.AreEqual(GoalKind.CollectTaxes, GoalAdvisor.Next(fresh).Kind);
        }

        [Test]
        public void Advisor_NamesTheMissingNeed_ThenTheUpgrade()
        {
            var house = Place("house", HousePos);
            Place("woodcutter", FarPos);
            Place("rice_paddy", new GridPos(11, 7));
            RunCycle(house);
            RunCycle(house);
            var goal = GoalAdvisor.Next(session);
            Assert.AreEqual(GoalKind.ImproveNeed, goal.Kind);
            Assert.AreEqual(NeedType.Beauty, goal.Need);

            Place("garden", GardenPos);
            goal = GoalAdvisor.Next(session);
            Assert.AreEqual(GoalKind.UpgradeHouse, goal.Kind);
            Assert.AreEqual(2, goal.Level);
        }
            // ---------------------------------------------------------------- pacing

        /// <summary>
        /// A simple bot that only uses the public rules (no debug grants) must get from a fresh save to a level 3 house.
        /// Guards against a broken chain (a need that can never be met, a cost that can never be paid) and reports the pacing.
        /// </summary>
        [Test]
        public void Bot_CanPlayFromFreshSaveToLevelThree_WithoutDebugGrants()
        {
            var s = TestData.NewSession(out var t, out _);
            var wallet = s.Wallet;
            BuildingInstance cutter = s.Buildings.TryPlace("woodcutter", new GridPos(5, 5), 0).Instance;
            BuildingInstance paddy = s.Buildings.TryPlace("rice_paddy", new GridPos(13, 13), 0).Instance;
            BuildingInstance house = null, smith = null, dock = null, mine = null;
            bool garden = false, shrine = false, roads = false;
            int levelTwoAt = -1, levelThreeAt = -1;

            void Work(BuildingInstance b, string production)
            {
                s.Production.Refresh(b);
                if (b.State == ProductionState.ReadyToCollect) s.Production.TryCollect(b.InstanceId);
                if (b.State == ProductionState.Idle) s.Production.TryStart(b.InstanceId, production);
            }

            for (int second = 0; second < 3 * 3600 && levelThreeAt < 0; second += 5)
            {
                t.AdvanceSeconds(5);
                s.Production.Refresh();
                Work(cutter, "wood_medium");
                Work(paddy, "rice_medium");
                // one road cell touches the Town Hall (9..11 x 10..12) and the house at (7,8)
                if (!roads && wallet.CanAfford(CurrencyType.Coins, 5)) roads = s.Buildings.TryPlace("road", new GridPos(8, 10), 0).Success;
                // tools open the way (garden, shrine and every upgrade cost them), fish feeds the level 2 and 3 tax runs
                if (house != null && mine == null) { var p = s.Buildings.TryPlace("mine", new GridPos(8, 3), 2); if (p.Success) mine = p.Instance; }
                if (mine != null && wallet.GetBalance(CurrencyType.Iron) < 30) Work(mine, "iron_medium");
                if (house != null && smith == null) { var p = s.Buildings.TryPlace("blacksmith", new GridPos(5, 10), 0); if (p.Success) smith = p.Instance; }
                if (house != null && house.Level >= 2 && dock == null) { var p = s.Buildings.TryPlace("fisher_dock", new GridPos(14, 6), 1); if (p.Success) dock = p.Instance; }
                if (smith != null && wallet.GetBalance(CurrencyType.Tools) < 20 && wallet.GetBalance(CurrencyType.Wood) >= 12 && wallet.GetBalance(CurrencyType.Iron) >= 7) Work(smith, "tools_medium");
                if (dock != null) Work(dock, "fish_medium");
                if (house == null)
                {
                    var placed = s.Buildings.TryPlace("house", new GridPos(7, 8), 0);
                    if (placed.Success) house = placed.Instance;
                    continue;
                }
                var next = house.Level == 1 ? "coins_house_basic" : house.Level == 2 ? "coins_house_l2" : "coins_house_l3";
                Work(house, next);
                if (house.Level >= 1 && !garden && wallet.CanAfford(s.Definitions.GetBuilding("garden").BuildCost) && s.Buildings.TryPlace("garden", new GridPos(9, 7), 0).Success) garden = true;
                if (house.Level >= 2 && garden && !shrine && s.Buildings.TryPlace("shrine", new GridPos(5, 8), 0).Success) shrine = true;
                if (house.Level == 2 && !garden) continue;
                if (s.Housing.TryUpgrade(house.InstanceId) == UpgradeStatus.Ok)
                {
                    if (house.Level == 2) levelTwoAt = second;
                    if (house.Level == 3) levelThreeAt = second;
                }
                // level 3 asks for quiet: once the shrine stands, move the woodcutter out of earshot
                if (shrine && house.Level == 2 && s.Housing.GetEnvironment(house, EnvironmentVariable.Noise) > 2f)
                    s.Buildings.TryMove(cutter.InstanceId, new GridPos(12, 4), 0);
            }

            TestContext.WriteLine($"level 2 after {levelTwoAt / 60} min, level 3 after {levelThreeAt / 60} min");
            string state = $"coins {wallet.GetBalance(CurrencyType.Coins)}, wood {wallet.GetBalance(CurrencyType.Wood)}, rice {wallet.GetBalance(CurrencyType.Rice)}, tools {wallet.GetBalance(CurrencyType.Tools)}, "
                + $"fish {wallet.GetBalance(CurrencyType.Fish)}; dock {dock != null}, shrine {shrine}; house {(house == null ? "none" : "L" + house.Level + " cycles " + house.CyclesAtLevel)}, smith {smith != null}, garden {garden}, roads {roads}";
            Assert.Greater(levelTwoAt, 0, "the bot never reached level 2: " + state);
            Assert.Greater(levelThreeAt, 0, "the bot never reached level 3: " + state);
            Assert.Less(levelTwoAt, 30 * 60, "level 2 should be reachable within half an hour");
        }
    }
}
