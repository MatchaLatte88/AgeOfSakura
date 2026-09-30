using AgeOfSakura.Core;
using NUnit.Framework;

namespace AgeOfSakura.Tests
{
    public class BuildingTests
    {
        private GameSession session;

        [SetUp]
        public void SetUp()
        {
            session = TestData.NewSession(out _, out _);
        }

        [Test]
        public void FreshGame_HasStartingResourcesAndTownHall()
        {
            Assert.AreEqual(500, session.Wallet.GetBalance(CurrencyType.Coins));
            Assert.AreEqual(0, session.Wallet.GetBalance(CurrencyType.Wood));
            Assert.AreEqual(20, session.Wallet.GetBalance(CurrencyType.Diamonds));
            Assert.AreEqual(1, session.Buildings.All.Count);
            Assert.AreEqual("town_hall", session.Buildings.All[0].DefinitionId);
        }

        [Test]
        public void PlaceWoodcutter_DeductsCoins_AndOccupiesCells()
        {
            var r = session.Buildings.TryPlace("woodcutter", new GridPos(5, 5), 0);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(400, session.Wallet.GetBalance(CurrencyType.Coins));
            Assert.AreEqual(r.Instance.InstanceId, session.Grid.GetCell(6, 6).OccupantId);
        }

        [Test]
        public void OverlappingPlacement_IsRejected_AndCostsNothing()
        {
            Assert.IsTrue(session.Buildings.TryPlace("woodcutter", new GridPos(5, 5), 0).Success);
            var r = session.Buildings.TryPlace("woodcutter", new GridPos(6, 6), 0);
            Assert.AreEqual(PlaceStatus.InvalidPlacement, r.Status);
            Assert.AreEqual(PlacementCheck.Occupied, r.Check);
            Assert.AreEqual(400, session.Wallet.GetBalance(CurrencyType.Coins));
            Assert.AreEqual(2, session.Buildings.All.Count);
        }

        [Test]
        public void OverlapWithTownHall_IsRejected()
        {
            // Town hall covers x 9..11, z 10..12
            var r = session.Buildings.TryPlace("woodcutter", new GridPos(10, 11), 0);
            Assert.AreEqual(PlacementCheck.Occupied, r.Check);
        }

        [Test]
        public void InsufficientFunds_AreReported_AndNothingIsPlaced()
        {
            var r = session.Buildings.TryPlace("house", new GridPos(5, 5), 0); // needs 20 Wood, has 0
            Assert.AreEqual(PlaceStatus.CannotAfford, r.Status);
            Assert.AreEqual(500, session.Wallet.GetBalance(CurrencyType.Coins));
            Assert.AreEqual(0, session.Wallet.GetBalance(CurrencyType.Wood));
            Assert.AreEqual(1, session.Buildings.All.Count);
        }

        [Test]
        public void InvalidTerrain_DoesNotCostResources()
        {
            var r = session.Buildings.TryPlace("woodcutter", new GridPos(14, 4), 0); // water
            Assert.AreEqual(PlaceStatus.InvalidPlacement, r.Status);
            Assert.AreEqual(PlacementCheck.BlockedTerrain, r.Check);
            Assert.AreEqual(500, session.Wallet.GetBalance(CurrencyType.Coins));
        }

        [Test]
        public void TownHall_CannotBeBuilt_OrMoved()
        {
            Assert.AreEqual(PlaceStatus.NotBuildable, session.Buildings.TryPlace("town_hall", new GridPos(5, 5), 0).Status);
            var hall = TestData.First(session, "town_hall");
            Assert.Throws<System.InvalidOperationException>(() => session.Buildings.TryMove(hall.InstanceId, new GridPos(5, 5), 0));
        }

        [Test]
        public void MoveBuilding_UpdatesPositionAndCells()
        {
            var w = session.Buildings.TryPlace("woodcutter", new GridPos(5, 5), 0).Instance;
            var check = session.Buildings.TryMove(w.InstanceId, new GridPos(7, 7), 0);
            Assert.AreEqual(PlacementCheck.Ok, check);
            Assert.AreEqual(new GridPos(7, 7), w.Origin);
            Assert.IsNull(session.Grid.GetCell(5, 5).OccupantId);
            Assert.AreEqual(w.InstanceId, session.Grid.GetCell(8, 8).OccupantId);
            Assert.AreEqual(400, session.Wallet.GetBalance(CurrencyType.Coins), "moving is free in 0.01");
        }

        [Test]
        public void FailedMove_RestoresNothingChanged()
        {
            var w = session.Buildings.TryPlace("woodcutter", new GridPos(5, 5), 0).Instance;
            var check = session.Buildings.TryMove(w.InstanceId, new GridPos(10, 10), 0); // town hall
            Assert.AreEqual(PlacementCheck.Occupied, check);
            Assert.AreEqual(new GridPos(5, 5), w.Origin);
            Assert.AreEqual(w.InstanceId, session.Grid.GetCell(5, 5).OccupantId);
        }
    }
}
