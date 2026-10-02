using System;

namespace AgeOfSakura.Core
{
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Z;

        public GridPos(int x, int z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(GridPos other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is GridPos other && Equals(other);
        public override int GetHashCode() => X * 397 ^ Z;
        public override string ToString() => $"({X},{Z})";
        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);
    }

    public enum TerrainType
    {
        Grass,
        Dirt,
        Water,
        Bridge,
        Tree,
        Cherry,
        Bamboo,
        Rock,
        /// <summary>A mountain: never buildable, but a Mine stands against it.</summary>
        Mountain
    }

    /// <summary>Small removable vegetation on otherwise buildable cells; hidden when a building covers it.</summary>
    public enum CosmeticKind
    {
        None,
        Flowers,
        Shrub,
        Tuft,
        SmallRock
    }

    public enum PlacementCheck
    {
        Ok,
        OutOfBounds,
        Locked,
        BlockedTerrain,
        Occupied,
        /// <summary>A shore building needs land under its landward part and water under its pier.</summary>
        NeedsShore
    }

    /// <summary>
    /// Extra placement rule for buildings that are not plain land buildings. A shore building (the Fisher Dock) is laid out along its
    /// local +Z axis: the first cells are land, the last <see cref="PierCells"/> cells (the pier) must stand in water.
    /// <see cref="Rotation"/> is the quarter turns of the footprint, so +Z maps to +X, -Z, -X for 1, 2, 3.
    /// </summary>
    public sealed class PlacementRule
    {
        /// <summary>Cells at the far end that must stand on <see cref="PierTerrain"/> (water for a dock, mountain for a mine); 0 = an ordinary land building.</summary>
        public int PierCells;
        public TerrainType PierTerrain = TerrainType.Water;
        public int Rotation;
        /// <summary>People may walk over the cells this building covers (roads).</summary>
        public bool WalkableSurface;
    }

    public sealed class GridCell
    {
        public GridPos Pos { get; }
        public TerrainType Terrain { get; internal set; }
        public CosmeticKind Cosmetic { get; internal set; }
        public bool Unlocked { get; internal set; }

        /// <summary>Stable instance ID of the building covering this cell, or null.</summary>
        public string OccupantId { get; internal set; }

        /// <summary>The building on this cell is a road: it still counts as occupied, but villagers and animals walk over it.</summary>
        public bool WalkableOccupant { get; internal set; }

        public GridCell(GridPos pos)
        {
            Pos = pos;
        }

        /// <summary>Terrain rule only (ignores lock state and occupancy). Kept in one place so terrain rules can grow later.</summary>
        public bool TerrainBuildable => Terrain == TerrainType.Grass || Terrain == TerrainType.Dirt;

        public bool IsOccupied => OccupantId != null;

        /// <summary>Buildable right now: unlocked, suitable terrain, free.</summary>
        public bool IsBuildable => Unlocked && TerrainBuildable && !IsOccupied;

        /// <summary>Villagers and animals may walk here (bridges yes, water/trees/rocks no, buildings no).</summary>
        public bool IsWalkable
        {
            get
            {
                if (IsOccupied && !WalkableOccupant) return false;
                return Terrain == TerrainType.Grass || Terrain == TerrainType.Dirt || Terrain == TerrainType.Bridge;
            }
        }
    }

    public static class Footprint
    {
        public static int NormalizeRotation(int rotation) => ((rotation % 4) + 4) % 4;

        /// <summary>Footprint size after applying a quarter-turn rotation (odd rotations swap width and height).</summary>
        public static void Size(int width, int height, int rotation, out int rotatedWidth, out int rotatedHeight)
        {
            bool swap = (NormalizeRotation(rotation) & 1) == 1;
            rotatedWidth = swap ? height : width;
            rotatedHeight = swap ? width : height;
        }
    }
}
