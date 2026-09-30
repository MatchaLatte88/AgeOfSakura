using System.Collections.Generic;
using AgeOfSakura.Core;
using NUnit.Framework;

namespace AgeOfSakura.Tests
{
    public class GridTests
    {
        private GridMap grid;

        [SetUp]
        public void SetUp()
        {
            grid = GridMap.FromDefinition(TestData.Definitions().Map);
        }

        [Test]
        public void Map_HasExpectedSizeAndUnlockedArea()
        {
            Assert.AreEqual(20, grid.Width);
            Assert.AreEqual(20, grid.Height);
            int unlocked = 0;
            for (int x = 0; x < grid.Width; x++)
                for (int z = 0; z < grid.Height; z++)
                    if (grid.GetCell(x, z).Unlocked) unlocked++;
            Assert.AreEqual(144, unlocked);
        }

        [Test]
        public void ValidPlacement_IsOk()
        {
            Assert.AreEqual(PlacementCheck.Ok, grid.CheckPlacement(new GridPos(6, 5), 2, 2));
        }

        [Test]
        public void Overlap_IsRejected()
        {
            grid.Occupy("a", new GridPos(6, 5), 2, 2);
            Assert.AreEqual(PlacementCheck.Occupied, grid.CheckPlacement(new GridPos(7, 6), 2, 2));
            Assert.AreEqual(PlacementCheck.Ok, grid.CheckPlacement(new GridPos(8, 5), 2, 2));
        }

        [Test]
        public void LockedCells_AreRejected()
        {
            // (2,2) is outside the unlocked 12x12 area; (3,5)-(4,6) straddles the border
            Assert.AreEqual(PlacementCheck.Locked, grid.CheckPlacement(new GridPos(2, 2), 2, 2));
            Assert.AreEqual(PlacementCheck.Locked, grid.CheckPlacement(new GridPos(3, 5), 2, 2));
        }

        [Test]
        public void BlockedTerrain_IsRejected()
        {
            // (15,4) is stream water inside the unlocked area; (4,13) is a boulder
            Assert.AreEqual(PlacementCheck.BlockedTerrain, grid.CheckPlacement(new GridPos(14, 4), 2, 2));
            Assert.AreEqual(PlacementCheck.BlockedTerrain, grid.CheckPlacement(new GridPos(4, 12), 2, 2));
        }

        [Test]
        public void OutOfBounds_IsRejected()
        {
            Assert.AreEqual(PlacementCheck.OutOfBounds, grid.CheckPlacement(new GridPos(19, 19), 2, 2));
            Assert.AreEqual(PlacementCheck.OutOfBounds, grid.CheckPlacement(new GridPos(-1, 5), 2, 2));
        }

        [Test]
        public void Move_ReleasesOldCellsAndReclaimsNew()
        {
            grid.Occupy("a", new GridPos(6, 5), 2, 2);
            Assert.AreEqual(PlacementCheck.Ok, grid.Move("a", new GridPos(8, 5), 2, 2));

            Assert.IsNull(grid.GetCell(6, 5).OccupantId, "old cells must be released");
            Assert.AreEqual("a", grid.GetCell(8, 5).OccupantId);
            Assert.AreEqual(PlacementCheck.Ok, grid.CheckPlacement(new GridPos(6, 5), 2, 2));
        }

        [Test]
        public void Move_ToOverlappingOwnCells_IsAllowed()
        {
            grid.Occupy("a", new GridPos(6, 5), 2, 2);
            Assert.AreEqual(PlacementCheck.Ok, grid.Move("a", new GridPos(7, 5), 2, 2));
            Assert.IsNull(grid.GetCell(6, 5).OccupantId);
            Assert.AreEqual("a", grid.GetCell(8, 6).OccupantId);
        }

        [Test]
        public void Move_ToInvalidTarget_LeavesEverythingUntouched()
        {
            grid.Occupy("a", new GridPos(6, 5), 2, 2);
            grid.Occupy("b", new GridPos(8, 5), 2, 2);
            Assert.AreEqual(PlacementCheck.Occupied, grid.Move("a", new GridPos(8, 5), 2, 2));
            Assert.AreEqual("a", grid.GetCell(6, 5).OccupantId);
            Assert.AreEqual("b", grid.GetCell(8, 5).OccupantId);
        }

        [Test]
        public void Footprint_SupportsNonSquareAndRotation()
        {
            Footprint.Size(2, 3, 1, out int w, out int h);
            Assert.AreEqual(3, w);
            Assert.AreEqual(2, h);
            Footprint.Size(2, 3, 2, out w, out h);
            Assert.AreEqual(2, w);
            Assert.AreEqual(3, h);
            Assert.AreEqual(PlacementCheck.Ok, grid.CheckPlacement(new GridPos(5, 5), 1, 1));
            Assert.AreEqual(PlacementCheck.Ok, grid.CheckPlacement(new GridPos(5, 5), 4, 4));
        }

        [Test]
        public void CosmeticCells_AreStillBuildable()
        {
            bool found = false;
            for (int x = 4; x < 16 && !found; x++)
                for (int z = 4; z < 16 && !found; z++)
                {
                    var c = grid.GetCell(x, z);
                    if (c.Cosmetic != CosmeticKind.None)
                    {
                        Assert.IsTrue(c.IsBuildable);
                        found = true;
                    }
                }
            Assert.IsTrue(found, "expected removable vegetation inside the unlocked area");
        }

        [Test]
        public void Pathfinder_FindsPathAroundWater_AndRejectsUnreachable()
        {
            var path = new List<GridPos>();
            Assert.IsTrue(GridPathfinder.FindPath(grid, new GridPos(5, 5), new GridPos(12, 8), c => c.IsWalkable, path));
            Assert.AreEqual(new GridPos(12, 8), path[path.Count - 1]);
            foreach (var p in path) Assert.IsTrue(grid.GetCell(p).IsWalkable);

            // (16,10) is water: not a valid goal
            Assert.IsFalse(GridPathfinder.FindPath(grid, new GridPos(5, 5), new GridPos(16, 10), c => c.IsWalkable, path));
        }

        [Test]
        public void Pathfinder_AvoidsOccupiedCells()
        {
            for (int z = 4; z <= 15; z++) grid.Occupy("wall" + z, new GridPos(9, z), 1, 1);
            var path = new List<GridPos>();
            // the wall splits the unlocked area, but the outer ring still connects both sides or it is unreachable - either way no path may cross a wall cell
            bool found = GridPathfinder.FindPath(grid, new GridPos(6, 8), new GridPos(12, 8), c => c.IsWalkable, path);
            if (found) foreach (var p in path) Assert.IsNull(grid.GetCell(p).OccupantId);
        }
    }
}
