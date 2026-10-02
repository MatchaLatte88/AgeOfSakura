using System;
using System.Collections.Generic;

namespace AgeOfSakura.Core
{
    public sealed class DefinitionException : Exception
    {
        public DefinitionException(string message) : base(message) { }
    }

    public static class DefinitionLoader
    {
        /// <summary>Parses definitions and validates them. Any problem throws so bad data fails loudly at startup.</summary>
        public static GameDefinitions LoadValidated(string json, Func<string, bool> visualExists = null)
        {
            GameDefinitions defs;
            try
            {
                defs = Parse(json);
            }
            catch (MiniJsonException e)
            {
                throw new DefinitionException("Definitions could not be parsed: " + e.Message);
            }

            var errors = DefinitionValidator.Validate(defs, visualExists);
            if (errors.Count > 0)
            {
                throw new DefinitionException("Invalid game definitions:\n - " + string.Join("\n - ", errors));
            }
            return defs;
        }

        public static GameDefinitions Parse(string json)
        {
            var root = new JObj(MiniJson.Parse(json), "$");
            var defs = new GameDefinitions { SchemaVersion = root.Int("schemaVersion") };

            var eco = root.Obj("economy");
            defs.Economy = new EconomyConfig
            {
                StartingCoins = eco.Long("startingCoins"),
                StartingWood = eco.Long("startingWood"),
                StartingDiamonds = eco.Long("startingDiamonds"),
                SecondsPerDiamond = eco.Int("secondsPerDiamond"),
                MinSkipCost = eco.Int("minSkipCost")
            };

            defs.Camera = ParseCamera(root.Has("camera") ? root.Obj("camera") : null);
            defs.Input = ParseInput(root.Has("input") ? root.Obj("input") : null);

            foreach (var p in root.ObjList("productions"))
            {
                var def = new ProductionDefinition
                {
                    Id = p.Str("id"),
                    DisplayName = p.Str("displayName"),
                    DurationSeconds = p.Int("durationSeconds"),
                    Inputs = ParseAmounts(p.ObjListOrEmpty("inputs")),
                    Rewards = ParseAmounts(p.ObjList("rewards"))
                };
                defs.Productions.Add(def);
            }

            foreach (var b in root.ObjList("buildings"))
            {
                var fp = b.Obj("footprint");
                defs.Buildings.Add(new BuildingDefinition
                {
                    Id = b.Str("id"),
                    DisplayName = b.Str("displayName"),
                    Description = b.Str("description"),
                    Category = b.Str("category"),
                    VisualId = b.Str("visualId"),
                    IconId = b.Str("iconId"),
                    FootprintWidth = fp.Int("width"),
                    FootprintHeight = fp.Int("height"),
                    BuildCost = ParseAmounts(b.ObjList("buildCost")),
                    ProductionIds = b.StrList("productions", optional: true),
                    Movable = b.Bool("movable", true),
                    Rotatable = b.Bool("rotatable", true),
                    Buildable = b.Bool("buildable", true),
                    VisualLevel = b.Int("visualLevel", 1),
                    Shore = b.Bool("shore", false),
                    PierCells = b.Int("pierCells", 0),
                    PierTerrain = ParseTerrain(b.StrOrNull("pierTerrain") ?? "Water", b.Path),
                    Chain = b.Bool("chain", false),
                    UpgradeToId = b.StrOrNull("upgradeTo"),
                    Emits = ParseEffects(b.ObjListOrEmpty("emits")),
                    Levels = ParseLevels(b.ObjListOrEmpty("levels"))
                });
            }

            if (root.Has("environment"))
            {
                foreach (var t in root.Obj("environment").ObjListOrEmpty("terrain"))
                {
                    string name = t.Str("terrain");
                    if (!Enum.TryParse(name, false, out TerrainType terrain) || !Enum.IsDefined(typeof(TerrainType), terrain))
                        throw new MiniJsonException($"{t.Path}.terrain: unknown terrain '{name}'");
                    defs.TerrainEffects.Add(new TerrainEffect { Terrain = terrain, Effect = ParseEffect(t) });
                }
            }

            var map = root.Obj("map");
            var unlocked = map.Obj("unlocked");
            var townHall = map.Obj("townHall");
            defs.Map = new MapDefinition
            {
                Width = map.Int("width"),
                Height = map.Int("height"),
                UnlockedX = unlocked.Int("x"),
                UnlockedZ = unlocked.Int("z"),
                UnlockedWidth = unlocked.Int("width"),
                UnlockedHeight = unlocked.Int("height"),
                TownHallId = townHall.Str("buildingId"),
                TownHallOrigin = new GridPos(townHall.Int("x"), townHall.Int("z")),
                Seed = map.Int("seed", 1),
                Rows = map.StrList("rows").ToArray()
            };
            foreach (var e in map.ObjListOrEmpty("expansions"))
                defs.Map.Expansions.Add(new MapExpansion { Level = e.Int("level"), X = e.Int("x"), Z = e.Int("z"), Width = e.Int("width"), Height = e.Int("height") });

            defs.BuildIndex();
            return defs;
        }

        private static TerrainType ParseTerrain(string name, string path)
        {
            if (!Enum.TryParse(name, false, out TerrainType terrain) || !Enum.IsDefined(typeof(TerrainType), terrain))
                throw new MiniJsonException($"{path}.pierTerrain: unknown terrain '{name}'");
            return terrain;
        }

        private static List<CurrencyAmount> ParseAmounts(List<JObj> items)
        {
            var result = new List<CurrencyAmount>(items.Count);
            foreach (var item in items)
            {
                string name = item.Str("currency");
                if (!Enum.TryParse(name, false, out CurrencyType currency) || !Enum.IsDefined(typeof(CurrencyType), currency))
                    throw new MiniJsonException($"{item.Path}.currency: unknown currency '{name}'");
                result.Add(new CurrencyAmount(currency, item.Long("amount")));
            }
            return result;
        }

        private static List<EnvironmentEffect> ParseEffects(List<JObj> items)
        {
            var result = new List<EnvironmentEffect>(items.Count);
            foreach (var item in items) result.Add(ParseEffect(item));
            return result;
        }

        private static EnvironmentEffect ParseEffect(JObj o)
        {
            string name = o.Str("variable");
            if (!Enum.TryParse(name, true, out EnvironmentVariable variable) || !Enum.IsDefined(typeof(EnvironmentVariable), variable))
                throw new MiniJsonException($"{o.Path}.variable: unknown environment variable '{name}'");
            return new EnvironmentEffect { Variable = variable, Value = (float)o.Double("value"), Radius = (float)o.Double("radius") };
        }

        private static List<BuildingLevelDefinition> ParseLevels(List<JObj> items)
        {
            var result = new List<BuildingLevelDefinition>(items.Count);
            foreach (var item in items)
            {
                var level = new BuildingLevelDefinition
                {
                    Level = item.Int("level"),
                    UpgradeCost = ParseAmounts(item.ObjListOrEmpty("upgradeCost")),
                    ProductionIds = item.StrList("productions", optional: true)
                };
                foreach (var n in item.ObjListOrEmpty("needs"))
                {
                    string kind = n.Str("kind");
                    if (!Enum.TryParse(kind, true, out NeedType type) || !Enum.IsDefined(typeof(NeedType), type))
                        throw new MiniJsonException($"{n.Path}.kind: unknown need '{kind}'");
                    level.Needs.Add(new NeedRequirement
                    {
                        Type = type,
                        Min = (float)n.Double("min", 0d),
                        Max = n.Has("max") ? (float)n.Double("max") : float.PositiveInfinity
                    });
                }
                result.Add(level);
            }
            return result;
        }

        private static CameraConfig ParseCamera(JObj o)
        {
            var c = new CameraConfig();
            if (o == null) return c;
            c.YawDegrees = (float)o.Double("yawDegrees", c.YawDegrees);
            c.PitchDegrees = (float)o.Double("pitchDegrees", c.PitchDegrees);
            c.MinOrthoSize = (float)o.Double("minOrthoSize", c.MinOrthoSize);
            c.MaxOrthoSize = (float)o.Double("maxOrthoSize", c.MaxOrthoSize);
            c.DefaultOrthoSize = (float)o.Double("defaultOrthoSize", c.DefaultOrthoSize);
            c.ZoomSmoothTime = (float)o.Double("zoomSmoothTime", c.ZoomSmoothTime);
            c.PinchSensitivity = (float)o.Double("pinchSensitivity", c.PinchSensitivity);
            c.ScrollZoomStep = (float)o.Double("scrollZoomStep", c.ScrollZoomStep);
            c.InertiaDamping = (float)o.Double("inertiaDamping", c.InertiaDamping);
            c.MaxInertiaSpeed = (float)o.Double("maxInertiaSpeed", c.MaxInertiaSpeed);
            c.BoundsPadding = (float)o.Double("boundsPadding", c.BoundsPadding);
            c.DefaultFocusX = (float)o.Double("defaultFocusX", c.DefaultFocusX);
            c.DefaultFocusZ = (float)o.Double("defaultFocusZ", c.DefaultFocusZ);
            return c;
        }

        private static InputConfig ParseInput(JObj o)
        {
            var c = new InputConfig();
            if (o == null) return c;
            c.DragThresholdDp = (float)o.Double("dragThresholdDp", c.DragThresholdDp);
            c.LongPressSeconds = (float)o.Double("longPressSeconds", c.LongPressSeconds);
            c.PlacementGrabRadiusCells = (float)o.Double("placementGrabRadiusCells", c.PlacementGrabRadiusCells);
            return c;
        }
    }

    public static class DefinitionValidator
    {
        /// <summary>Returns every problem found (empty list = valid). <paramref name="visualExists"/> checks model registration.</summary>
        public static List<string> Validate(GameDefinitions defs, Func<string, bool> visualExists = null)
        {
            var errors = new List<string>();

            if (defs.Economy.StartingCoins < 0 || defs.Economy.StartingWood < 0 || defs.Economy.StartingDiamonds < 0)
                errors.Add("economy: starting balances must not be negative");
            if (defs.Economy.SecondsPerDiamond <= 0) errors.Add("economy: secondsPerDiamond must be > 0");
            if (defs.Economy.MinSkipCost < 0) errors.Add("economy: minSkipCost must be >= 0");

            var productionIds = new HashSet<string>();
            foreach (var p in defs.Productions)
            {
                if (string.IsNullOrWhiteSpace(p.Id)) { errors.Add("production with empty id"); continue; }
                if (!productionIds.Add(p.Id)) errors.Add($"production '{p.Id}': duplicate id");
                if (p.DurationSeconds <= 0) errors.Add($"production '{p.Id}': durationSeconds must be > 0");
                if (p.Rewards.Count == 0) errors.Add($"production '{p.Id}': no reward defined");
                ValidateAmounts(p.Rewards, $"production '{p.Id}' reward", errors, requirePositive: true);
                ValidateAmounts(p.Inputs, $"production '{p.Id}' input", errors, requirePositive: true);
            }

            var buildingIds = new HashSet<string>();
            foreach (var b in defs.Buildings)
            {
                if (string.IsNullOrWhiteSpace(b.Id)) { errors.Add("building with empty id"); continue; }
                if (!buildingIds.Add(b.Id)) errors.Add($"building '{b.Id}': duplicate id");
                if (b.FootprintWidth < 1 || b.FootprintHeight < 1) errors.Add($"building '{b.Id}': invalid footprint {b.FootprintWidth}x{b.FootprintHeight}");
                if (string.IsNullOrWhiteSpace(b.VisualId)) errors.Add($"building '{b.Id}': missing visualId");
                else if (visualExists != null && !visualExists(b.VisualId)) errors.Add($"building '{b.Id}': no model registered for visualId '{b.VisualId}'");
                ValidateAmounts(b.BuildCost, $"building '{b.Id}' cost", errors, requirePositive: false);
                foreach (var pid in b.ProductionIds)
                {
                    if (!productionIds.Contains(pid)) errors.Add($"building '{b.Id}': unknown production '{pid}'");
                }
                if (b.UpgradeToId != null && defs.Buildings.TrueForAll(o => o.Id != b.UpgradeToId))
                    errors.Add($"building '{b.Id}': unknown upgradeTo '{b.UpgradeToId}'");
                foreach (var e in b.Emits) ValidateEffect(e, $"building '{b.Id}' emits", errors);
                if (b.Shore && (b.PierCells < 1 || b.PierCells >= Math.Max(b.FootprintWidth, b.FootprintHeight)))
                    errors.Add($"building '{b.Id}': a shore building needs pierCells between 1 and its length - 1");
                ValidateLevels(b, productionIds, errors);
            }
            foreach (var t in defs.TerrainEffects) ValidateEffect(t.Effect, $"environment terrain {t.Terrain}", errors);

            ValidateMap(defs, errors);
            return errors;
        }

        private static void ValidateEffect(EnvironmentEffect e, string label, List<string> errors)
        {
            if (e.Radius <= 0f) errors.Add($"{label}: radius must be > 0");
            if (e.Value <= 0f) errors.Add($"{label}: value must be > 0");
        }

        private static void ValidateLevels(BuildingDefinition b, HashSet<string> productionIds, List<string> errors)
        {
            if (b.Levels.Count == 0) return;
            string label = $"building '{b.Id}'";
            for (int i = 0; i < b.Levels.Count; i++)
            {
                var l = b.Levels[i];
                if (l.Level != i + 1) errors.Add($"{label}: levels must be listed as 1..N in order (found {l.Level} at position {i + 1})");
                if (i == 0 && (l.UpgradeCost.Count > 0 || l.Needs.Count > 0)) errors.Add($"{label}: level 1 must not have an upgradeCost or needs");
                if (i > 0 && l.UpgradeCost.Count == 0) errors.Add($"{label} level {l.Level}: upgradeCost is required");
                ValidateAmounts(l.UpgradeCost, $"{label} level {l.Level} upgradeCost", errors, requirePositive: true);
                foreach (var pid in l.ProductionIds)
                {
                    if (!productionIds.Contains(pid)) errors.Add($"{label} level {l.Level}: unknown production '{pid}'");
                }
                foreach (var n in l.Needs)
                {
                    if (n.Min < 0f || n.Max < n.Min) errors.Add($"{label} level {l.Level}: need {n.Type} has an invalid range");
                    if (n.Min == 0f && !n.HasMax) errors.Add($"{label} level {l.Level}: need {n.Type} has neither min nor max");
                }
            }
        }

        private static void ValidateAmounts(List<CurrencyAmount> amounts, string label, List<string> errors, bool requirePositive)
        {
            var seen = new HashSet<CurrencyType>();
            foreach (var a in amounts)
            {
                if (!Enum.IsDefined(typeof(CurrencyType), a.Currency)) errors.Add($"{label}: invalid currency");
                if (!seen.Add(a.Currency)) errors.Add($"{label}: currency {a.Currency} listed twice");
                if (a.Amount < 0 || (requirePositive && a.Amount == 0)) errors.Add($"{label}: invalid amount {a.Amount} of {a.Currency}");
            }
        }

        private static void ValidateMap(GameDefinitions defs, List<string> errors)
        {
            var m = defs.Map;
            if (m.Width <= 0 || m.Height <= 0) { errors.Add("map: size must be positive"); return; }
            if (m.Rows.Length != m.Height) errors.Add($"map: expected {m.Height} rows, found {m.Rows.Length}");
            for (int z = 0; z < m.Rows.Length; z++)
            {
                if (m.Rows[z].Length != m.Width)
                {
                    errors.Add($"map: row {z} has length {m.Rows[z].Length}, expected {m.Width}");
                    continue;
                }
                foreach (char c in m.Rows[z])
                {
                    if (!MapDefinition.IsValidChar(c)) errors.Add($"map: row {z} contains unknown character '{c}'");
                }
            }
            if (errors.Exists(e => e.StartsWith("map:"))) return;

            if (m.UnlockedX < 0 || m.UnlockedZ < 0 || m.UnlockedWidth <= 0 || m.UnlockedHeight <= 0
                || m.UnlockedX + m.UnlockedWidth > m.Width || m.UnlockedZ + m.UnlockedHeight > m.Height)
                errors.Add("map: unlocked rectangle lies outside the map");

            if (!defs.TryGetBuilding(m.TownHallId, out var hall))
            {
                errors.Add($"map: town hall building '{m.TownHallId}' is not defined");
                return;
            }
            if (errors.Exists(e => e.StartsWith("map:"))) return;

            var grid = GridMap.FromDefinition(m);
            var check = grid.CheckPlacement(m.TownHallOrigin, hall.FootprintWidth, hall.FootprintHeight);
            if (check != PlacementCheck.Ok) errors.Add($"map: town hall cannot be placed at {m.TownHallOrigin} ({check})");

            foreach (var e in m.Expansions)
            {
                if (e.Level < 2 || e.Level > hall.MaxLevel) errors.Add($"map: expansion at level {e.Level} needs a town hall level between 2 and {hall.MaxLevel}");
                if (e.X < 0 || e.Z < 0 || e.Width <= 0 || e.Height <= 0 || e.X + e.Width > m.Width || e.Z + e.Height > m.Height)
                    errors.Add($"map: expansion at level {e.Level} lies outside the map");
            }
        }
    }
}
