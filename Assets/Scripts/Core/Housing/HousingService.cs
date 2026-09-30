using System;
using System.Collections.Generic;

namespace AgeOfSakura.Core
{
    public enum UpgradeStatus { Ok, UnknownBuilding, NotUpgradable, MaxLevel, NeedsNotMet, CannotAfford }

    /// <summary>One need of the next level with the value it currently has.</summary>
    public sealed class NeedStatus
    {
        public NeedRequirement Requirement;
        public float Current;
        public bool Met;
    }

    /// <summary>Everything the UI needs to explain an upgrade: what it costs, what the house needs, and what is still missing.</summary>
    public sealed class UpgradeInfo
    {
        public BuildingInstance Instance;
        public int CurrentLevel;
        public int NextLevel;
        public bool IsMaxLevel;
        public IReadOnlyList<NeedStatus> Needs = new List<NeedStatus>();
        public IReadOnlyList<CurrencyAmount> Cost = new List<CurrencyAmount>();
        public bool NeedsMet;
        public bool CanAfford;

        public bool CanUpgrade => !IsMaxLevel && NeedsMet && CanAfford;
    }

    /// <summary>
    /// Upgrade rules for buildings with a level path (houses). A house asks for things: its supply chain must have worked
    /// (collected cycles), and its surroundings must suit it (Beauty, Faith, low Noise). The surroundings are computed live from
    /// buildings and terrain, so moving a Woodcutter or planting a Garden changes what a house can become.
    /// </summary>
    public sealed class HousingService
    {
        private readonly GameDefinitions defs;
        private readonly BuildingService buildings;
        private readonly GridMap grid;
        private readonly Wallet wallet;
        private readonly IAnalyticsService analytics;

        public event Action<BuildingInstance> BuildingUpgraded;

        public HousingService(GameDefinitions defs, BuildingService buildings, GridMap grid, Wallet wallet, IAnalyticsService analytics)
        {
            this.defs = defs;
            this.buildings = buildings;
            this.grid = grid;
            this.wallet = wallet;
            this.analytics = analytics;
        }

        public bool IsUpgradable(BuildingInstance instance) => buildings.GetDefinition(instance).Levels.Count > 0;

        /// <summary>
        /// Sum of all sources of <paramref name="variable"/> that reach the building: other buildings' emissions and terrain cells,
        /// each counted in full when its centre lies within the source's radius of the building's footprint centre.
        /// </summary>
        public float GetEnvironment(BuildingInstance instance, EnvironmentVariable variable)
        {
            if (!grid.TryGetFootprint(instance.InstanceId, out var origin, out int w, out int h)) return 0f;
            float cx = origin.X + w * 0.5f;
            float cz = origin.Z + h * 0.5f;
            float total = 0f;

            foreach (var other in buildings.All)
            {
                if (ReferenceEquals(other, instance)) continue;
                var def = buildings.GetDefinition(other);
                if (def.Emits.Count == 0 || !grid.TryGetFootprint(other.InstanceId, out var o, out int ow, out int oh)) continue;
                float dx = o.X + ow * 0.5f - cx;
                float dz = o.Z + oh * 0.5f - cz;
                foreach (var effect in def.Emits)
                {
                    if (effect.Variable == variable && dx * dx + dz * dz <= effect.Radius * effect.Radius) total += effect.Value;
                }
            }

            foreach (var t in defs.TerrainEffects)
            {
                if (t.Effect.Variable != variable) continue;
                float r = t.Effect.Radius;
                int x0 = (int)Math.Floor(cx - r), x1 = (int)Math.Ceiling(cx + r);
                int z0 = (int)Math.Floor(cz - r), z1 = (int)Math.Ceiling(cz + r);
                for (int x = x0; x <= x1; x++)
                {
                    for (int z = z0; z <= z1; z++)
                    {
                        if (!grid.TryGetCell(x, z, out var cell) || cell.Terrain != t.Terrain) continue;
                        float dx = x + 0.5f - cx;
                        float dz = z + 0.5f - cz;
                        if (dx * dx + dz * dz <= r * r) total += t.Effect.Value;
                    }
                }
            }
            return total;
        }

        public float GetNeedValue(BuildingInstance instance, NeedRequirement need)
        {
            if (need.Type == NeedType.Served) return instance.CyclesAtLevel;
            need.TryGetVariable(out var variable);
            return GetEnvironment(instance, variable);
        }

        public UpgradeInfo Evaluate(BuildingInstance instance)
        {
            var def = buildings.GetDefinition(instance);
            var info = new UpgradeInfo { Instance = instance, CurrentLevel = instance.Level, NextLevel = instance.Level + 1 };
            if (def.Levels.Count == 0 || instance.Level >= def.MaxLevel || !def.TryGetLevel(instance.Level + 1, out var next))
            {
                info.IsMaxLevel = true;
                return info;
            }

            var needs = new List<NeedStatus>();
            bool allMet = true;
            foreach (var need in next.Needs)
            {
                float value = GetNeedValue(instance, need);
                bool met = need.IsMet(value);
                allMet &= met;
                needs.Add(new NeedStatus { Requirement = need, Current = value, Met = met });
            }
            info.Needs = needs;
            info.Cost = next.UpgradeCost;
            info.NeedsMet = allMet;
            info.CanAfford = wallet.CanAfford(next.UpgradeCost);
            return info;
        }

        /// <summary>Validates needs and cost, pays, raises the level and restarts the "served" counter. Nothing is paid on failure.</summary>
        public UpgradeStatus TryUpgrade(string instanceId)
        {
            if (!buildings.TryGet(instanceId, out var instance)) return UpgradeStatus.UnknownBuilding;
            if (!IsUpgradable(instance)) return UpgradeStatus.NotUpgradable;
            var info = Evaluate(instance);
            if (info.IsMaxLevel) return UpgradeStatus.MaxLevel;
            if (!info.NeedsMet) return UpgradeStatus.NeedsNotMet;
            if (!info.CanAfford) return UpgradeStatus.CannotAfford;

            if (!wallet.TrySpend(info.Cost, CurrencyTransactionReason.BuildingUpgrade))
                throw new InvalidOperationException("Wallet rejected a cost that CanAfford accepted.");
            instance.Level = info.NextLevel;
            instance.CyclesAtLevel = 0;
            GameLog.Info(LogCategory.Building, $"Upgraded {instance.DefinitionId} {instance.InstanceId} to level {instance.Level}");
            analytics.Track(AnalyticsEvents.BuildingUpgraded, new Dictionary<string, object> { { "building", instance.DefinitionId }, { "level", instance.Level } });
            BuildingUpgraded?.Invoke(instance);
            return UpgradeStatus.Ok;
        }
    }
}
