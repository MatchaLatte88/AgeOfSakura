using System;
using System.Collections.Generic;

namespace AgeOfSakura.Core
{
    /// <summary>
    /// Logical grid: terrain, unlock state and building occupancy. Contains no rendering.
    /// Footprints are rectangles anchored at their minimum (x, z) cell; the footprint size is
    /// always data-driven, never inferred from meshes.
    /// </summary>
    public sealed class GridMap
    {
        private readonly struct Placement
        {
            public readonly GridPos Origin;
            public readonly int Width;
            public readonly int Height;

            public Placement(GridPos origin, int width, int height)
            {
                Origin = origin;
                Width = width;
                Height = height;
            }
        }

        private readonly GridCell[,] cells;
        private readonly Dictionary<string, Placement> placements = new Dictionary<string, Placement>();

        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }

        /// <summary>Raised whenever occupancy or unlock state changed (views refresh overlays / cosmetics).</summary>
        public event Action OccupancyChanged;

        /// <summary>Raised when land was unlocked (views rebuild boundary posts, the locked-area veil and what villagers may roam).</summary>
        public event Action LandUnlocked;

        public GridMap(int width, int height, float cellSize = 1f)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Grid size must be positive.");
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            Width = width;
            Height = height;
            CellSize = cellSize;
            cells = new GridCell[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++) cells[x, z] = new GridCell(new GridPos(x, z));
            }
        }

        public static GridMap FromDefinition(MapDefinition map, float cellSize = 1f)
        {
            var grid = new GridMap(map.Width, map.Height, cellSize);
            for (int z = 0; z < map.Height; z++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    MapDefinition.ParseCell(map.Rows[z][x], out var terrain, out var cosmetic);
                    var cell = grid.cells[x, z];
                    cell.Terrain = terrain;
                    cell.Cosmetic = cosmetic;
                    cell.Unlocked = x >= map.UnlockedX && x < map.UnlockedX + map.UnlockedWidth
                                    && z >= map.UnlockedZ && z < map.UnlockedZ + map.UnlockedHeight;
                }
            }
            return grid;
        }

        public bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Height;
        public bool InBounds(GridPos pos) => InBounds(pos.X, pos.Z);

        public GridCell GetCell(int x, int z)
        {
            if (!InBounds(x, z)) throw new ArgumentOutOfRangeException($"Cell ({x},{z}) is outside the {Width}x{Height} grid.");
            return cells[x, z];
        }

        public GridCell GetCell(GridPos pos) => GetCell(pos.X, pos.Z);

        public bool TryGetCell(int x, int z, out GridCell cell)
        {
            if (InBounds(x, z))
            {
                cell = cells[x, z];
                return true;
            }
            cell = null;
            return false;
        }

        public void SetTerrain(int x, int z, TerrainType terrain) => GetCell(x, z).Terrain = terrain;

        public void SetUnlocked(int x, int z, bool unlocked)
        {
            GetCell(x, z).Unlocked = unlocked;
            OccupancyChanged?.Invoke();
        }

        /// <summary>Unlocks a rectangle of land (cells outside the map are ignored) and raises <see cref="LandUnlocked"/> once if anything changed.</summary>
        public int UnlockRect(int x, int z, int width, int height)
        {
            int changed = 0;
            for (int cx = x; cx < x + width; cx++)
            {
                for (int cz = z; cz < z + height; cz++)
                {
                    if (!InBounds(cx, cz) || cells[cx, cz].Unlocked) continue;
                    cells[cx, cz].Unlocked = true;
                    changed++;
                }
            }
            if (changed > 0)
            {
                OccupancyChanged?.Invoke();
                LandUnlocked?.Invoke();
            }
            return changed;
        }

        // ---- world conversion (XZ plane; the Unity layer maps to Vector3) ----

        public (float x, float z) CellCenterToWorld(GridPos pos) => ((pos.X + 0.5f) * CellSize, (pos.Z + 0.5f) * CellSize);

        public (float x, float z) FootprintCenterToWorld(GridPos origin, int width, int height) =>
            ((origin.X + width * 0.5f) * CellSize, (origin.Z + height * 0.5f) * CellSize);

        public GridPos WorldToCell(float worldX, float worldZ) =>
            new GridPos((int)Math.Floor(worldX / CellSize), (int)Math.Floor(worldZ / CellSize));

        // ---- placement ----

        /// <summary>
        /// Checks a rectangular footprint. If several problems exist the most fundamental one is
        /// reported (OutOfBounds &gt; Locked &gt; BlockedTerrain &gt; Occupied).
        /// <paramref name="ignoreOccupantId"/> lets a building being moved ignore its own cells.
        /// </summary>
        public PlacementCheck CheckPlacement(GridPos origin, int width, int height, string ignoreOccupantId = null, PlacementRule rule = null)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Footprint must be at least 1x1.");
            var worst = PlacementCheck.Ok;
            for (int x = origin.X; x < origin.X + width; x++)
            {
                for (int z = origin.Z; z < origin.Z + height; z++)
                {
                    PlacementCheck result;
                    if (!InBounds(x, z)) result = PlacementCheck.OutOfBounds;
                    else
                    {
                        var cell = cells[x, z];
                        if (!cell.Unlocked) result = PlacementCheck.Locked;
                        else if (rule != null && rule.PierCells > 0 && !ShoreTerrainOk(cell, x, z, origin, width, height, rule)) result = PlacementCheck.NeedsShore;
                        else if ((rule == null || rule.PierCells == 0) && !cell.TerrainBuildable) result = PlacementCheck.BlockedTerrain;
                        else if (cell.OccupantId != null && cell.OccupantId != ignoreOccupantId) result = PlacementCheck.Occupied;
                        else result = PlacementCheck.Ok;
                    }
                    if (Severity(result) > Severity(worst)) worst = result;
                }
            }
            return worst;
        }

        /// <summary>Shore rule: distance from the landward end along the building's long axis decides land (buildable) or pier (water).</summary>
        private static bool ShoreTerrainOk(GridCell cell, int x, int z, GridPos origin, int width, int height, PlacementRule rule)
        {
            int length = (rule.Rotation & 1) == 0 ? height : width;
            int t;
            switch (Footprint.NormalizeRotation(rule.Rotation))
            {
                case 0: t = z - origin.Z; break;
                case 1: t = x - origin.X; break;
                case 2: t = origin.Z + height - 1 - z; break;
                default: t = origin.X + width - 1 - x; break;
            }
            return t >= length - rule.PierCells ? cell.Terrain == rule.PierTerrain : cell.TerrainBuildable;
        }

        private static int Severity(PlacementCheck check)
        {
            switch (check)
            {
                case PlacementCheck.OutOfBounds: return 4;
                case PlacementCheck.Locked: return 3;
                case PlacementCheck.NeedsShore: return 2;
                case PlacementCheck.BlockedTerrain: return 2;
                case PlacementCheck.Occupied: return 1;
                default: return 0;
            }
        }

        public void Occupy(string occupantId, GridPos origin, int width, int height, PlacementRule rule = null)
        {
            if (string.IsNullOrEmpty(occupantId)) throw new ArgumentException("Occupant id is required.", nameof(occupantId));
            if (placements.ContainsKey(occupantId)) throw new InvalidOperationException($"'{occupantId}' already occupies cells; use Move.");
            var check = CheckPlacement(origin, width, height, null, rule);
            if (check != PlacementCheck.Ok) throw new InvalidOperationException($"Cannot occupy {origin} {width}x{height}: {check}.");
            Mark(occupantId, origin, width, height, rule != null && rule.WalkableSurface);
            placements[occupantId] = new Placement(origin, width, height);
            OccupancyChanged?.Invoke();
        }

        public void Release(string occupantId)
        {
            if (!placements.TryGetValue(occupantId, out var p)) return;
            Mark(null, p.Origin, p.Width, p.Height, false);
            placements.Remove(occupantId);
            OccupancyChanged?.Invoke();
        }

        /// <summary>Atomically re-anchors an existing occupant. Leaves everything untouched unless the result is Ok.</summary>
        public PlacementCheck Move(string occupantId, GridPos newOrigin, int width, int height, PlacementRule rule = null)
        {
            if (!placements.TryGetValue(occupantId, out var old)) throw new InvalidOperationException($"'{occupantId}' does not occupy any cells.");
            var check = CheckPlacement(newOrigin, width, height, occupantId, rule);
            if (check != PlacementCheck.Ok) return check;
            Mark(null, old.Origin, old.Width, old.Height, false);
            Mark(occupantId, newOrigin, width, height, rule != null && rule.WalkableSurface);
            placements[occupantId] = new Placement(newOrigin, width, height);
            OccupancyChanged?.Invoke();
            return PlacementCheck.Ok;
        }

        public bool TryGetFootprint(string occupantId, out GridPos origin, out int width, out int height)
        {
            if (placements.TryGetValue(occupantId, out var p))
            {
                origin = p.Origin;
                width = p.Width;
                height = p.Height;
                return true;
            }
            origin = default;
            width = 0;
            height = 0;
            return false;
        }

        private void Mark(string occupantId, GridPos origin, int width, int height, bool walkable)
        {
            for (int x = origin.X; x < origin.X + width; x++)
            {
                for (int z = origin.Z; z < origin.Z + height; z++)
                {
                    cells[x, z].OccupantId = occupantId;
                    cells[x, z].WalkableOccupant = occupantId != null && walkable;
                }
            }
        }
    }
}
