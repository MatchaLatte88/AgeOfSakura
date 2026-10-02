using System.Collections.Generic;

namespace AgeOfSakura.Core
{
    /// <summary>
    /// Which buildings are connected to the Town Hall by road. Road cells are buildings of the category "road" (and the bridges of the
    /// map); a chain of them that touches the Town Hall is connected, and so is every building that touches the Town Hall or such a chain.
    /// Recomputed lazily after the grid changed, so asking is cheap.
    /// </summary>
    public sealed class RoadNetwork
    {
        public const string RoadCategory = "road";

        private static readonly int[] Dx = { 1, -1, 0, 0 };
        private static readonly int[] Dz = { 0, 0, 1, -1 };

        private readonly GameDefinitions defs;
        private readonly BuildingService buildings;
        private readonly GridMap grid;
        private readonly HashSet<GridPos> reached = new HashSet<GridPos>();
        private bool dirty = true;

        public RoadNetwork(GameDefinitions defs, BuildingService buildings, GridMap grid)
        {
            this.defs = defs;
            this.buildings = buildings;
            this.grid = grid;
            grid.OccupancyChanged += () => dirty = true;
        }

        public bool IsRoad(GridCell cell)
        {
            if (cell.Terrain == TerrainType.Bridge) return true;
            return cell.OccupantId != null && buildings.TryGet(cell.OccupantId, out var b) && buildings.GetDefinition(b).Category == RoadCategory;
        }

        private bool IsHall(GridCell cell) =>
            cell.OccupantId != null && buildings.TryGet(cell.OccupantId, out var b) && b.DefinitionId == defs.Map.TownHallId;

        /// <summary>Whether the cell is a road cell that belongs to a chain leading to the Town Hall.</summary>
        public bool IsConnectedRoad(GridPos pos)
        {
            Refresh();
            return reached.Contains(pos);
        }

        public bool IsConnected(BuildingInstance instance)
        {
            if (instance.DefinitionId == defs.Map.TownHallId) return true;
            Refresh();
            if (!grid.TryGetFootprint(instance.InstanceId, out var origin, out int w, out int h)) return false;
            for (int x = origin.X - 1; x <= origin.X + w; x++)
            {
                for (int z = origin.Z - 1; z <= origin.Z + h; z++)
                {
                    bool insideX = x >= origin.X && x < origin.X + w;
                    bool insideZ = z >= origin.Z && z < origin.Z + h;
                    if (insideX == insideZ) continue; // only the four edge neighbours: skip the footprint itself and the corners
                    if (!grid.TryGetCell(x, z, out var cell)) continue;
                    if (IsHall(cell) || reached.Contains(cell.Pos)) return true;
                }
            }
            return false;
        }

        private void Refresh()
        {
            if (!dirty) return;
            dirty = false;
            reached.Clear();
            var queue = new Queue<GridPos>();
            for (int x = 0; x < grid.Width; x++)
            {
                for (int z = 0; z < grid.Height; z++)
                {
                    var cell = grid.GetCell(x, z);
                    if (!IsRoad(cell) || !TouchesHall(x, z)) continue;
                    if (reached.Add(cell.Pos)) queue.Enqueue(cell.Pos);
                }
            }
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    if (!grid.TryGetCell(p.X + Dx[d], p.Z + Dz[d], out var next) || !IsRoad(next)) continue;
                    if (reached.Add(next.Pos)) queue.Enqueue(next.Pos);
                }
            }
        }

        private bool TouchesHall(int x, int z)
        {
            for (int d = 0; d < 4; d++)
                if (grid.TryGetCell(x + Dx[d], z + Dz[d], out var n) && IsHall(n)) return true;
            return false;
        }
    }
}
