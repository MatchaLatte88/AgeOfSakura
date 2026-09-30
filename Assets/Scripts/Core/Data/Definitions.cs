using System;
using System.Collections.Generic;

namespace AgeOfSakura.Core
{
    /// <summary>A production recipe (data only). Inputs are paid when it starts; rewards are granted on manual collection.</summary>
    public sealed class ProductionDefinition
    {
        public string Id;
        public string DisplayName;
        public int DurationSeconds;
        /// <summary>Goods taken from the store when the production starts (the supply chain). Empty for raw producers.</summary>
        public List<CurrencyAmount> Inputs = new List<CurrencyAmount>();
        public List<CurrencyAmount> Rewards = new List<CurrencyAmount>();
    }

    /// <summary>Local environment values a house cares about. Sources (buildings, terrain) add to them within a radius.</summary>
    public enum EnvironmentVariable
    {
        Beauty,
        Noise,
        Faith
    }

    /// <summary>A source of one environment variable: full <see cref="Value"/> within <see cref="Radius"/> cells (centre to centre).</summary>
    public sealed class EnvironmentEffect
    {
        public EnvironmentVariable Variable;
        public float Value;
        public float Radius;
    }

    /// <summary>Every terrain cell of this type acts as an environment source (e.g. cherry trees add Beauty).</summary>
    public sealed class TerrainEffect
    {
        public TerrainType Terrain;
        public EnvironmentEffect Effect;
    }

    /// <summary>What a house asks for before it may be upgraded.</summary>
    public enum NeedType
    {
        /// <summary>Production cycles collected (each one consumed Rice) since the last upgrade: the supply chain works.</summary>
        Served,
        Beauty,
        Noise,
        Faith
    }

    public sealed class NeedRequirement
    {
        public NeedType Type;
        /// <summary>Lower bound (inclusive); 0 = none.</summary>
        public float Min;
        /// <summary>Upper bound (inclusive); <see cref="float.PositiveInfinity"/> = none.</summary>
        public float Max = float.PositiveInfinity;

        public bool HasMax => !float.IsPositiveInfinity(Max);

        public bool IsMet(float value) => value >= Min && value <= Max;

        /// <summary>The environment variable behind this need, or false for <see cref="NeedType.Served"/>.</summary>
        public bool TryGetVariable(out EnvironmentVariable variable)
        {
            switch (Type)
            {
                case NeedType.Beauty: variable = EnvironmentVariable.Beauty; return true;
                case NeedType.Noise: variable = EnvironmentVariable.Noise; return true;
                case NeedType.Faith: variable = EnvironmentVariable.Faith; return true;
                default: variable = default; return false;
            }
        }
    }

    /// <summary>One level of an upgradeable building. Level 1 carries no cost or needs; higher levels list what reaching them takes.</summary>
    public sealed class BuildingLevelDefinition
    {
        public int Level;
        public List<CurrencyAmount> UpgradeCost = new List<CurrencyAmount>();
        public List<NeedRequirement> Needs = new List<NeedRequirement>();
        /// <summary>Recipes available at this level; empty = use the building's own list.</summary>
        public List<string> ProductionIds = new List<string>();
    }

    /// <summary>
    /// Static building data. Contains no runtime state. The footprint comes from here, never from mesh bounds,
    /// and gameplay refers to buildings by <see cref="Id"/>, never by prefab/model name.
    /// </summary>
    public sealed class BuildingDefinition
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public string Category;
        /// <summary>Key the Unity layer uses to pick a model/prefab. Replaceable without touching gameplay.</summary>
        public string VisualId;
        public string IconId;
        public int FootprintWidth;
        public int FootprintHeight;
        public List<CurrencyAmount> BuildCost = new List<CurrencyAmount>();
        public List<string> ProductionIds = new List<string>();
        public bool Movable;
        public bool Rotatable;
        /// <summary>Whether the building appears in the build menu (Town Hall does not).</summary>
        public bool Buildable;
        public int VisualLevel = 1;
        /// <summary>Unused; the level path below is the upgrade mechanic.</summary>
        public string UpgradeToId;
        /// <summary>Environment values this building adds to its surroundings (Garden: Beauty, Woodcutter: Noise, ...).</summary>
        public List<EnvironmentEffect> Emits = new List<EnvironmentEffect>();
        /// <summary>Optional level path, ordered 1..N. Empty = the building cannot be upgraded.</summary>
        public List<BuildingLevelDefinition> Levels = new List<BuildingLevelDefinition>();

        public int MaxLevel => Levels.Count > 0 ? Levels[Levels.Count - 1].Level : 1;

        public bool TryGetLevel(int level, out BuildingLevelDefinition definition)
        {
            foreach (var l in Levels)
            {
                if (l.Level == level) { definition = l; return true; }
            }
            definition = null;
            return false;
        }

        /// <summary>Recipes offered at a level (falls back to the base list when the level does not override it).</summary>
        public IReadOnlyList<string> GetProductionIds(int level)
        {
            if (TryGetLevel(level, out var l) && l.ProductionIds.Count > 0) return l.ProductionIds;
            return ProductionIds;
        }
    }

    public sealed class EconomyConfig
    {
        public long StartingCoins;
        public long StartingWood;
        public long StartingDiamonds;
        /// <summary>Skip price = max(MinSkipCost, ceil(remainingSeconds / SecondsPerDiamond)).</summary>
        public int SecondsPerDiamond;
        public int MinSkipCost;
    }

    public sealed class CameraConfig
    {
        public float YawDegrees = 45f;
        public float PitchDegrees = 30f;
        public float MinOrthoSize = 3.2f;
        public float MaxOrthoSize = 10f;
        public float DefaultOrthoSize = 6f;
        public float ZoomSmoothTime = 0.08f;
        public float PinchSensitivity = 1f;
        public float ScrollZoomStep = 0.12f;
        public float InertiaDamping = 7f;
        public float MaxInertiaSpeed = 30f;
        public float BoundsPadding = 1.5f;
        public float DefaultFocusX = 10f;
        public float DefaultFocusZ = 10f;
    }

    public sealed class InputConfig
    {
        /// <summary>Movement in density-independent pixels (1 dp = dpi/160 px) before a press becomes a drag.</summary>
        public float DragThresholdDp = 10f;
        public float LongPressSeconds = 0.45f;
        /// <summary>How far (in cells) from a placement ghost a drag may start and still grab it.</summary>
        public float PlacementGrabRadiusCells = 1.5f;
    }

    /// <summary>ASCII terrain map. rows[z][x]; see <see cref="ParseCell"/> for the legend.</summary>
    public sealed class MapDefinition
    {
        public int Width;
        public int Height;
        public int UnlockedX;
        public int UnlockedZ;
        public int UnlockedWidth;
        public int UnlockedHeight;
        public string TownHallId;
        public GridPos TownHallOrigin;
        public int Seed;
        public string[] Rows;

        public static bool IsValidChar(char c)
        {
            switch (c)
            {
                case '.': case ',': case '~': case '=': case 'T': case 'C': case 'B': case 'R':
                case 'f': case 's': case 'g': case 'r':
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Legend: . grass, ',' dirt, ~ water, = bridge, T tree, C cherry tree, B bamboo, R big rock,
        /// f/s/g/r = buildable grass carrying removable flowers / shrub / tuft / small rock.
        /// </summary>
        public static void ParseCell(char c, out TerrainType terrain, out CosmeticKind cosmetic)
        {
            cosmetic = CosmeticKind.None;
            switch (c)
            {
                case '.': terrain = TerrainType.Grass; break;
                case ',': terrain = TerrainType.Dirt; break;
                case '~': terrain = TerrainType.Water; break;
                case '=': terrain = TerrainType.Bridge; break;
                case 'T': terrain = TerrainType.Tree; break;
                case 'C': terrain = TerrainType.Cherry; break;
                case 'B': terrain = TerrainType.Bamboo; break;
                case 'R': terrain = TerrainType.Rock; break;
                case 'f': terrain = TerrainType.Grass; cosmetic = CosmeticKind.Flowers; break;
                case 's': terrain = TerrainType.Grass; cosmetic = CosmeticKind.Shrub; break;
                case 'g': terrain = TerrainType.Grass; cosmetic = CosmeticKind.Tuft; break;
                case 'r': terrain = TerrainType.Grass; cosmetic = CosmeticKind.SmallRock; break;
                default: throw new ArgumentException($"Unknown map character '{c}'.", nameof(c));
            }
        }
    }

    public sealed class GameDefinitions
    {
        public int SchemaVersion;
        public EconomyConfig Economy;
        public CameraConfig Camera;
        public InputConfig Input;
        public MapDefinition Map;
        public List<ProductionDefinition> Productions = new List<ProductionDefinition>();
        public List<BuildingDefinition> Buildings = new List<BuildingDefinition>();
        public List<TerrainEffect> TerrainEffects = new List<TerrainEffect>();

        private Dictionary<string, BuildingDefinition> buildingIndex;
        private Dictionary<string, ProductionDefinition> productionIndex;

        /// <summary>Builds the ID lookup tables. Call after loading; validation reports duplicate IDs separately.</summary>
        public void BuildIndex()
        {
            buildingIndex = new Dictionary<string, BuildingDefinition>();
            foreach (var b in Buildings) if (!buildingIndex.ContainsKey(b.Id)) buildingIndex.Add(b.Id, b);
            productionIndex = new Dictionary<string, ProductionDefinition>();
            foreach (var p in Productions) if (!productionIndex.ContainsKey(p.Id)) productionIndex.Add(p.Id, p);
        }

        public bool TryGetBuilding(string id, out BuildingDefinition definition)
        {
            if (buildingIndex == null) BuildIndex();
            return buildingIndex.TryGetValue(id ?? string.Empty, out definition);
        }

        public BuildingDefinition GetBuilding(string id)
        {
            if (!TryGetBuilding(id, out var d)) throw new KeyNotFoundException($"Unknown building definition '{id}'.");
            return d;
        }

        public bool TryGetProduction(string id, out ProductionDefinition definition)
        {
            if (productionIndex == null) BuildIndex();
            return productionIndex.TryGetValue(id ?? string.Empty, out definition);
        }

        public ProductionDefinition GetProduction(string id)
        {
            if (!TryGetProduction(id, out var d)) throw new KeyNotFoundException($"Unknown production definition '{id}'.");
            return d;
        }
    }
}
