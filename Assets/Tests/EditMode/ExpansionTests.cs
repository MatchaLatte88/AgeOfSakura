using System.Linq;
using AgeOfSakura.Core;
using NUnit.Framework;

namespace AgeOfSakura.Tests
{
    /// <summary>Tools and the Blacksmith, fish and the Fisher Dock (shore placement), roads and the "connected" need, land opened by the Town Hall.</summary>
    public class ExpansionTests
    {
        private GameSession s;
        private FakeTimeProvider time;

        // the Town Hall stands at (9,10) 3x3; the river's first water columns inside the unlocked area are x 16 and 17
        private static readonly GridPos DockPos = new GridPos(14, 6);

        [SetUp]
        public void SetUp()
        {
            s = TestData.NewSession(out time, out _);
            s.Wallet.Add(CurrencyType.Coins, 5000, CurrencyTransactionReason.DebugGrant);
            s.Wallet.Add(CurrencyType.Wood, 2000, CurrencyTransactionReason.DebugGrant);
            s.Wallet.Add(CurrencyType.Tools, 200, CurrencyTransactionReason.DebugGrant);
            s.Wallet.Add(CurrencyType.Iron, 200, CurrencyTransactionReason.DebugGrant);
        }

        private BuildingInstance Place(string id, GridPos pos, int rotation = 0)
        {
            var r = s.Buildings.TryPlace(id, pos, rotation);
            Assert.IsTrue(r.Success, $"placing {id} at {pos} rot {rotation}: {r.Status} {r.Check}");
            return r.Instance;
        }

        // ---------------------------------------------------------------- Blacksmith / tools

        [Test]
        public void Blacksmith_TurnsWoodIntoTools()
        {
            var smith = Place("blacksmith", new GridPos(5, 10));
            long wood = s.Wallet.GetBalance(CurrencyType.Wood);
            long tools = s.Wallet.GetBalance(CurrencyType.Tools);
            Assert.AreEqual(StartProductionStatus.Ok, s.Production.TryStart(smith.InstanceId, "tools_small"));
            Assert.AreEqual(wood - 3, s.Wallet.GetBalance(CurrencyType.Wood), "the wood is taken when the work starts");
            Assert.AreEqual(198, s.Wallet.GetBalance(CurrencyType.Iron), "and so is the iron");
            time.AdvanceSeconds(45);
            s.Production.Refresh();
            Assert.IsTrue(s.Production.TryCollect(smith.InstanceId).Success);
            Assert.AreEqual(tools + 2, s.Wallet.GetBalance(CurrencyType.Tools));
        }

        [Test]
        public void Blacksmith_NeedsWood()
        {
            var smith = Place("blacksmith", new GridPos(5, 10));
            s.Wallet.TrySpend(CurrencyType.Wood, s.Wallet.GetBalance(CurrencyType.Wood), CurrencyTransactionReason.DebugGrant);
            Assert.AreEqual(StartProductionStatus.MissingInputs, s.Production.TryStart(smith.InstanceId, "tools_small"));
        }

        [Test]
        public void Blacksmith_NeedsIron()
        {
            var smith = Place("blacksmith", new GridPos(5, 10));
            s.Wallet.TrySpend(CurrencyType.Iron, s.Wallet.GetBalance(CurrencyType.Iron), CurrencyTransactionReason.DebugGrant);
            Assert.AreEqual(StartProductionStatus.MissingInputs, s.Production.TryStart(smith.InstanceId, "tools_small"));
        }

        // ---------------------------------------------------------------- Mine / iron

        [Test]
        public void Mine_StandsAgainstTheMountain_WithItsEntranceInTheRock()
        {
            // mountain cells are row 3, x 7..12; the mine reaches 2 wide x 3 long and turned so that its far end is the rock
            var mine = Place("mine", new GridPos(8, 3), 2);
            Assert.AreEqual(TerrainType.Mountain, s.Grid.GetCell(8, 3).Terrain);
            Assert.AreEqual(mine.InstanceId, s.Grid.GetCell(9, 3).OccupantId);
            Assert.AreEqual(mine.InstanceId, s.Grid.GetCell(8, 5).OccupantId);
        }

        [Test]
        public void Mine_RefusedOnOpenGround_AndWhenFacingTheWrongWay()
        {
            Assert.AreEqual(PlacementCheck.NeedsShore, s.Buildings.CheckPlacement("mine", new GridPos(8, 8), 2));
            Assert.AreEqual(PlacementCheck.NeedsShore, s.Buildings.CheckPlacement("mine", new GridPos(8, 3), 0), "the rock end must be the far end");
            Assert.AreEqual(PlacementCheck.Ok, s.Buildings.CheckPlacement("mine", new GridPos(8, 3), 2));
        }

        [Test]
        public void Mountain_IsNeitherBuildableNorWalkable()
        {
            var cell = s.Grid.GetCell(9, 2);
            Assert.AreEqual(TerrainType.Mountain, cell.Terrain);
            Assert.IsFalse(cell.TerrainBuildable);
            Assert.IsFalse(cell.IsWalkable);
            Assert.AreEqual(PlacementCheck.Locked, s.Buildings.CheckPlacement("house", new GridPos(9, 1), 0), "and the rock above is still closed land");
        }

        [Test]
        public void Mine_DigsIron_ThatTheSmithForges()
        {
            var mine = Place("mine", new GridPos(8, 3), 2);
            long iron = s.Wallet.GetBalance(CurrencyType.Iron);
            Assert.AreEqual(StartProductionStatus.Ok, s.Production.TryStart(mine.InstanceId, "iron_small"));
            time.AdvanceSeconds(45);
            s.Production.Refresh();
            Assert.IsTrue(s.Production.TryCollect(mine.InstanceId).Success);
            Assert.AreEqual(iron + 4, s.Wallet.GetBalance(CurrencyType.Iron));
            Assert.That(s.Definitions.GetBuilding("mine").BuildCost.All(c => c.Currency != CurrencyType.Tools), "the mine comes before tools");
        }

        [Test]
        public void LaterBuildings_CostTools()
        {
            var defs = s.Definitions;
            Assert.That(defs.GetBuilding("fisher_dock").BuildCost.Any(c => c.Currency == CurrencyType.Tools));
            Assert.That(defs.GetBuilding("shrine").BuildCost.Any(c => c.Currency == CurrencyType.Tools));
            Assert.That(defs.GetBuilding("house").Levels[1].UpgradeCost.Any(c => c.Currency == CurrencyType.Tools));
            Assert.That(defs.GetBuilding("woodcutter").BuildCost.All(c => c.Currency != CurrencyType.Tools), "the first chain stays free of tools");
            Assert.That(defs.GetBuilding("rice_paddy").BuildCost.All(c => c.Currency != CurrencyType.Tools));
            Assert.That(defs.GetBuilding("house").BuildCost.All(c => c.Currency != CurrencyType.Tools));
        }

        // ---------------------------------------------------------------- Fisher Dock / fish

        [Test]
        public void Dock_StandsWithItsPierInTheWater()
        {
            var dock = Place("fisher_dock", DockPos, 1);
            s.Buildings.GetFootprintSize(dock, out int w, out int h);
            Assert.AreEqual((4, 2), (w, h));
            Assert.AreEqual(TerrainType.Water, s.Grid.GetCell(16, 6).Terrain);
            Assert.AreEqual(TerrainType.Water, s.Grid.GetCell(17, 7).Terrain);
            Assert.AreEqual(dock.InstanceId, s.Grid.GetCell(17, 7).OccupantId);
        }

        [Test]
        public void Dock_NeedsLandAndWater_InTheRightOrder()
        {
            // rotated the other way round, the pier cells would stand on grass and the landward cells in the river
            Assert.AreEqual(PlacementCheck.NeedsShore, s.Buildings.CheckPlacement("fisher_dock", DockPos, 3));
            // entirely on land
            Assert.AreEqual(PlacementCheck.NeedsShore, s.Buildings.CheckPlacement("fisher_dock", new GridPos(6, 5), 1));
            Assert.AreEqual(PlacementCheck.Ok, s.Buildings.CheckPlacement("fisher_dock", DockPos, 1));
            var refused = s.Buildings.TryPlace("fisher_dock", new GridPos(6, 5), 1);
            Assert.AreEqual(PlaceStatus.InvalidPlacement, refused.Status);
            Assert.AreEqual(PlacementCheck.NeedsShore, refused.Check);
        }

        [Test]
        public void Dock_CanBeMovedAlongTheBank_ButNotOntoLand()
        {
            var dock = Place("fisher_dock", DockPos, 1);
            Assert.AreEqual(PlacementCheck.Ok, s.Buildings.TryMove(dock.InstanceId, new GridPos(14, 10), 1));
            Assert.AreEqual(PlacementCheck.NeedsShore, s.Buildings.TryMove(dock.InstanceId, new GridPos(8, 5), 1));
            Assert.AreEqual(new GridPos(14, 10), dock.Origin);
        }

        [Test]
        public void Dock_SurvivesSaveAndReload()
        {
            var t = new FakeTimeProvider();
            var storage = new InMemorySaveStorage();
            var first = new GameSession(TestData.Definitions(), storage, t);
            first.Start();
            first.Wallet.Add(CurrencyType.Coins, 1000, CurrencyTransactionReason.DebugGrant);
            first.Wallet.Add(CurrencyType.Wood, 500, CurrencyTransactionReason.DebugGrant);
            first.Wallet.Add(CurrencyType.Tools, 20, CurrencyTransactionReason.DebugGrant);
            var dock = first.Buildings.TryPlace("fisher_dock", DockPos, 1).Instance;
            first.Production.TryStart(dock.InstanceId, "fish_small");
            first.FlushIfDirty();

            var second = TestData.Reload(storage, t, out var kind);
            Assert.AreEqual(SessionStartKind.Loaded, kind);
            var loaded = TestData.First(second, "fisher_dock");
            Assert.AreEqual(DockPos, loaded.Origin);
            Assert.AreEqual(1, loaded.Rotation);
            Assert.AreEqual(ProductionState.Producing, loaded.State);
        }

        [Test]
        public void Fish_FeedsTheLaterTaxRuns()
        {
            var dock = Place("fisher_dock", DockPos, 1);
            Assert.AreEqual(StartProductionStatus.Ok, s.Production.TryStart(dock.InstanceId, "fish_small"));
            time.AdvanceSeconds(60);
            s.Production.Refresh();
            Assert.IsTrue(s.Production.TryCollect(dock.InstanceId).Success);
            Assert.AreEqual(5, s.Wallet.GetBalance(CurrencyType.Fish));

            var l2 = s.Definitions.GetProduction("coins_house_l2");
            Assert.That(l2.Inputs.Any(i => i.Currency == CurrencyType.Fish), "level 2 taxes need fish besides rice");
        }

        // ---------------------------------------------------------------- roads

        [Test]
        public void Road_IsOneCellAndCheap()
        {
            var def = s.Definitions.GetBuilding("road");
            Assert.AreEqual((1, 1), (def.FootprintWidth, def.FootprintHeight));
            Assert.IsTrue(def.Chain);
            Assert.IsFalse(def.Rotatable);
            Assert.LessOrEqual(def.BuildCost.Sum(c => c.Amount), 10);
        }

        [Test]
        public void House_IsConnected_OnlyThroughAChainFromTheTownHall()
        {
            var house = Place("house", new GridPos(5, 12));            // cells 5..6 x 12..13, the hall starts at x 9
            Assert.IsFalse(s.Housing.Roads.IsConnected(house));

            Place("road", new GridPos(7, 13));                         // touches the house, but not the hall
            Assert.IsFalse(s.Housing.Roads.IsConnected(house), "a road that leads nowhere does not connect");

            Place("road", new GridPos(8, 13));                         // still not touching the hall (its z range is 10..12)
            Assert.IsFalse(s.Housing.Roads.IsConnected(house));
            Place("road", new GridPos(8, 12));                         // next to the hall cell (9,12)
            Assert.IsTrue(s.Housing.Roads.IsConnected(house));
        }

        [Test]
        public void House_NextToTheTownHall_IsConnectedWithoutARoad()
        {
            var house = Place("house", new GridPos(7, 10));            // cells 7..8, touching the hall at x 9
            Assert.IsTrue(s.Housing.Roads.IsConnected(house));
        }

        [Test]
        public void MovingARoad_BreaksAndRestoresTheConnection()
        {
            var house = Place("house", new GridPos(5, 12));
            Place("road", new GridPos(7, 13));
            Place("road", new GridPos(8, 13));
            var last = Place("road", new GridPos(8, 12));
            Assert.IsTrue(s.Housing.Roads.IsConnected(house));
            Assert.AreEqual(PlacementCheck.Ok, s.Buildings.TryMove(last.InstanceId, new GridPos(13, 8), 0));
            Assert.IsFalse(s.Housing.Roads.IsConnected(house), "the cache must notice the move");
            Assert.AreEqual(PlacementCheck.Ok, s.Buildings.TryMove(last.InstanceId, new GridPos(8, 12), 0));
            Assert.IsTrue(s.Housing.Roads.IsConnected(house));
        }

        [Test]
        public void Roads_AreOccupiedButWalkable_HousesAreNot()
        {
            var road = Place("road", new GridPos(8, 10));
            var house = Place("house", new GridPos(5, 5));
            Assert.IsTrue(s.Grid.GetCell(8, 10).IsOccupied);
            Assert.IsTrue(s.Grid.GetCell(8, 10).IsWalkable, "villagers walk along roads");
            Assert.IsFalse(s.Grid.GetCell(5, 5).IsWalkable);
            Assert.AreEqual(PlacementCheck.Ok, s.Buildings.TryMove(road.InstanceId, new GridPos(8, 14), 0));
            Assert.IsTrue(s.Grid.GetCell(8, 14).IsWalkable);
            Assert.IsTrue(s.Grid.GetCell(8, 10).IsWalkable && !s.Grid.GetCell(8, 10).IsOccupied, "the old cell is plain ground again");
            Assert.IsNotNull(house);
        }

        [Test]
        public void BridgesCountAsRoad()
        {
            Assert.AreEqual(TerrainType.Bridge, s.Grid.GetCell(16, 9).Terrain);
            Assert.IsTrue(s.Housing.Roads.IsRoad(s.Grid.GetCell(18, 9)));
        }

        [Test]
        public void HouseLevelTwo_AsksForARoad()
        {
            var level2 = s.Definitions.GetBuilding("house").Levels[1];
            Assert.That(level2.Needs.Any(n => n.Type == NeedType.Connected && n.Min >= 1f));
        }

        // ---------------------------------------------------------------- Town Hall opens land

        [Test]
        public void TownHall_StartsWithTheSmallValley()
        {
            Assert.IsFalse(s.Grid.GetCell(10, 20).Unlocked, "the southern meadow is still closed");
            Assert.IsFalse(s.Grid.GetCell(24, 8).Unlocked, "and so is the east bank");
        }

        [Test]
        public void TownHall_Level2_OpensTheSouthernMeadow()
        {
            RaiseHall(2);
            Assert.AreEqual(2, TestData.First(s, "town_hall").Level);
            Assert.IsTrue(s.Grid.GetCell(10, 20).Unlocked);
            Assert.IsFalse(s.Grid.GetCell(24, 8).Unlocked, "the east bank waits for level 3");
        }

        [Test]
        public void TownHall_Level3_OpensTheEastBank()
        {
            RaiseHall(2);
            RaiseHall(3);
            Assert.IsTrue(s.Grid.GetCell(24, 8).Unlocked);
        }

        [Test]
        public void TownHall_Upgrade_NeedsBeautyAndFaithAround()
        {
            var hall = TestData.First(s, "town_hall");
            long coins = s.Wallet.GetBalance(CurrencyType.Coins);
            Assert.AreEqual(UpgradeStatus.NeedsNotMet, s.Housing.TryUpgrade(hall.InstanceId));
            Assert.AreEqual(1, hall.Level);
            Assert.AreEqual(coins, s.Wallet.GetBalance(CurrencyType.Coins), "a refused upgrade costs nothing");
        }

        [Test]
        public void LandOpensAgain_WhenALevelTwoHallIsLoaded()
        {
            var storage = new InMemorySaveStorage();
            var t = new FakeTimeProvider();
            var a = new GameSession(TestData.Definitions(), storage, t);
            a.Start();
            Assert.IsFalse(a.Grid.GetCell(10, 20).Unlocked);
            var save = a.Capture();
            save.Buildings.First(b => b.DefinitionId == "town_hall").Level = 2;
            storage.Write(SaveCodec.Encode(save));
            var reloaded = TestData.Reload(storage, t, out _);
            Assert.IsTrue(reloaded.Grid.GetCell(10, 20).Unlocked);
        }

        [Test]
        public void BuildingsInNewLand_AreRefusedUntilItOpens()
        {
            Assert.AreEqual(PlacementCheck.Locked, s.Buildings.CheckPlacement("house", new GridPos(10, 22), 0));
            RaiseHall(2);
            Assert.AreEqual(PlacementCheck.Ok, s.Buildings.CheckPlacement("house", new GridPos(10, 22), 0), "the meadow cell must be free grass");
        }

        // ---------------------------------------------------------------- goals

        [Test]
        public void Advisor_PointsToTheBlacksmith_WhenToolsAreMissing()
        {
            var fresh = TestData.NewSession(out var t, out _);
            fresh.Wallet.Add(CurrencyType.Coins, 3000, CurrencyTransactionReason.DebugGrant);
            fresh.Wallet.Add(CurrencyType.Wood, 500, CurrencyTransactionReason.DebugGrant);
            fresh.Wallet.Add(CurrencyType.Rice, 100, CurrencyTransactionReason.DebugGrant);
            fresh.Buildings.TryPlace("woodcutter", new GridPos(5, 5), 0);
            fresh.Buildings.TryPlace("rice_paddy", new GridPos(13, 13), 0);
            var house = fresh.Buildings.TryPlace("house", new GridPos(7, 7), 0).Instance;
            fresh.Wallet.Add(CurrencyType.Tools, 2, CurrencyTransactionReason.DebugGrant);
            Assert.IsTrue(fresh.Buildings.TryPlace("garden", new GridPos(9, 7), 0).Success);
            fresh.Buildings.TryPlace("road", new GridPos(9, 9), 0);
            fresh.Buildings.TryPlace("road", new GridPos(8, 9), 0);
            for (int i = 0; i < 2; i++)
            {
                fresh.Production.TryStart(house.InstanceId, "coins_house_basic");
                t.AdvanceSeconds(70);
                fresh.Production.Refresh();
                fresh.Production.TryCollect(house.InstanceId);
            }
            var goal = GoalAdvisor.Next(fresh);
            Assert.AreEqual(GoalKind.Build, goal.Kind);
            Assert.AreEqual("blacksmith", goal.BuildingId);
        }

        /// <summary>Satisfies the hall's beauty and faith needs with real gardens and shrines, then upgrades it one level.</summary>
        private void RaiseHall(int level)
        {
            var hall = TestData.First(s, "town_hall");
            if (hall.Level >= level) return;
            if (level == 2)
            {
                Place("garden", new GridPos(12, 8));
                Place("garden", new GridPos(6, 13));
                Place("shrine", new GridPos(12, 13));
            }
            else
            {
                Place("shrine", new GridPos(6, 8));
            }
            var status = s.Housing.TryUpgrade(hall.InstanceId);
            Assert.AreEqual(UpgradeStatus.Ok, status, "needs: " + string.Join(", ", s.Housing.Evaluate(hall).Needs.Select(n => $"{n.Requirement.Type} {n.Current}/{n.Requirement.Min}")));
        }
    }
}
