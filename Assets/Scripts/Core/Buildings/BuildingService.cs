using System;
using System.Collections.Generic;

namespace AgeOfSakura.Core
{
    public enum PlaceStatus
    {
        Ok,
        UnknownDefinition,
        NotBuildable,
        CannotAfford,
        InvalidPlacement
    }

    public readonly struct PlaceResult
    {
        public readonly PlaceStatus Status;
        public readonly PlacementCheck Check;
        public readonly BuildingInstance Instance;

        public PlaceResult(PlaceStatus status, PlacementCheck check, BuildingInstance instance)
        {
            Status = status;
            Check = check;
            Instance = instance;
        }

        public bool Success => Status == PlaceStatus.Ok;
    }

    /// <summary>Owns all placed buildings: purchase, placement, moving. Occupancy lives in the <see cref="GridMap"/>.</summary>
    public sealed class BuildingService
    {
        private readonly GameDefinitions defs;
        private readonly GridMap grid;
        private readonly Wallet wallet;
        private readonly IAnalyticsService analytics;
        private readonly List<BuildingInstance> buildings = new List<BuildingInstance>();
        private readonly Dictionary<string, BuildingInstance> byId = new Dictionary<string, BuildingInstance>();

        public event Action<BuildingInstance> BuildingPlaced;
        public event Action<BuildingInstance, GridPos, int> BuildingMoved;

        public IReadOnlyList<BuildingInstance> All => buildings;

        public BuildingService(GameDefinitions defs, GridMap grid, Wallet wallet, IAnalyticsService analytics)
        {
            this.defs = defs;
            this.grid = grid;
            this.wallet = wallet;
            this.analytics = analytics;
        }

        public bool TryGet(string instanceId, out BuildingInstance instance) => byId.TryGetValue(instanceId ?? string.Empty, out instance);

        public BuildingInstance Get(string instanceId)
        {
            if (!TryGet(instanceId, out var b)) throw new KeyNotFoundException($"Unknown building instance '{instanceId}'.");
            return b;
        }

        public BuildingDefinition GetDefinition(BuildingInstance instance) => defs.GetBuilding(instance.DefinitionId);

        public void GetFootprintSize(BuildingInstance instance, out int width, out int height)
        {
            var def = GetDefinition(instance);
            Footprint.Size(def.FootprintWidth, def.FootprintHeight, instance.Rotation, out width, out height);
        }

        public PlacementCheck CheckPlacement(string definitionId, GridPos origin, int rotation, string ignoreInstanceId = null)
        {
            var def = defs.GetBuilding(definitionId);
            rotation = def.Rotatable ? Footprint.NormalizeRotation(rotation) : 0;
            Footprint.Size(def.FootprintWidth, def.FootprintHeight, rotation, out int w, out int h);
            return grid.CheckPlacement(origin, w, h, ignoreInstanceId, def.RuleFor(rotation));
        }

        public bool CanAfford(string definitionId) => wallet.CanAfford(defs.GetBuilding(definitionId).BuildCost);

        /// <summary>
        /// Validates, then pays and places. Resources are deducted only after validation passed, so a
        /// rejected placement never costs anything.
        /// </summary>
        public PlaceResult TryPlace(string definitionId, GridPos origin, int rotation)
        {
            if (!defs.TryGetBuilding(definitionId, out var def)) return new PlaceResult(PlaceStatus.UnknownDefinition, PlacementCheck.Ok, null);
            if (!def.Buildable) return new PlaceResult(PlaceStatus.NotBuildable, PlacementCheck.Ok, null);

            rotation = def.Rotatable ? Footprint.NormalizeRotation(rotation) : 0;
            Footprint.Size(def.FootprintWidth, def.FootprintHeight, rotation, out int w, out int h);
            var check = grid.CheckPlacement(origin, w, h, null, def.RuleFor(rotation));
            if (check != PlacementCheck.Ok) return new PlaceResult(PlaceStatus.InvalidPlacement, check, null);
            if (!wallet.CanAfford(def.BuildCost)) return new PlaceResult(PlaceStatus.CannotAfford, check, null);

            if (!wallet.TrySpend(def.BuildCost, CurrencyTransactionReason.BuildingPurchase))
                throw new InvalidOperationException("Wallet rejected a cost that CanAfford accepted.");
            foreach (var cost in def.BuildCost)
            {
                if (cost.Amount > 0)
                    analytics.Track(AnalyticsEvents.CurrencySpent, new Dictionary<string, object> { { "currency", cost.Currency.ToString() }, { "amount", cost.Amount }, { "reason", "building_purchase" } });
            }

            var instance = Create(def, origin, rotation, Guid.NewGuid().ToString("N"));
            GameLog.Info(LogCategory.Building, $"Placed {def.Id} at {origin} (rot {rotation}) id={instance.InstanceId}");
            analytics.Track(AnalyticsEvents.BuildingPlaced, new Dictionary<string, object> { { "building", def.Id } });
            BuildingPlaced?.Invoke(instance);
            return new PlaceResult(PlaceStatus.Ok, PlacementCheck.Ok, instance);
        }

        /// <summary>Places a building without cost (new-game Town Hall).</summary>
        public BuildingInstance PlaceFree(string definitionId, GridPos origin, int rotation)
        {
            var def = defs.GetBuilding(definitionId);
            rotation = def.Rotatable ? Footprint.NormalizeRotation(rotation) : 0;
            var instance = Create(def, origin, rotation, Guid.NewGuid().ToString("N"));
            GameLog.Info(LogCategory.Building, $"Placed {def.Id} at {origin} (free) id={instance.InstanceId}");
            BuildingPlaced?.Invoke(instance);
            return instance;
        }

        /// <summary>
        /// Re-adds a building from save data. Returns false (and logs) instead of throwing when the
        /// record cannot be restored, so one bad entry does not abort the whole load.
        /// </summary>
        public bool TryRestore(BuildingInstance restored)
        {
            if (!defs.TryGetBuilding(restored.DefinitionId, out var def))
            {
                GameLog.Error(LogCategory.Building, $"Save references unknown building definition '{restored.DefinitionId}' (instance {restored.InstanceId}); skipped.");
                return false;
            }
            if (byId.ContainsKey(restored.InstanceId))
            {
                GameLog.Error(LogCategory.Building, $"Save contains duplicate instance id {restored.InstanceId}; skipped.");
                return false;
            }
            if (restored.Level > def.MaxLevel)
            {
                GameLog.Warn(LogCategory.Building, $"Saved {def.Id} has level {restored.Level}, above the maximum {def.MaxLevel}; clamped.");
                restored.Level = def.MaxLevel;
            }
            Footprint.Size(def.FootprintWidth, def.FootprintHeight, restored.Rotation, out int w, out int h);
            var check = grid.CheckPlacement(restored.Origin, w, h, null, def.RuleFor(restored.Rotation));
            if (check != PlacementCheck.Ok)
            {
                GameLog.Error(LogCategory.Building, $"Saved {def.Id} at {restored.Origin} is not placeable ({check}); skipped.");
                return false;
            }
            grid.Occupy(restored.InstanceId, restored.Origin, w, h, def.RuleFor(restored.Rotation));
            buildings.Add(restored);
            byId[restored.InstanceId] = restored;
            BuildingPlaced?.Invoke(restored);
            return true;
        }

        /// <summary>Removes a building without events. Only for discarding a half-restored save before any view exists.</summary>
        public void RemoveForReset(string instanceId)
        {
            if (!byId.TryGetValue(instanceId, out var instance)) return;
            grid.Release(instanceId);
            buildings.Remove(instance);
            byId.Remove(instanceId);
        }

        public PlacementCheck TryMove(string instanceId, GridPos newOrigin, int newRotation)
        {
            var instance = Get(instanceId);
            var def = GetDefinition(instance);
            if (!def.Movable) throw new InvalidOperationException($"{def.Id} cannot be moved.");
            newRotation = def.Rotatable ? Footprint.NormalizeRotation(newRotation) : instance.Rotation;
            Footprint.Size(def.FootprintWidth, def.FootprintHeight, newRotation, out int w, out int h);

            var oldOrigin = instance.Origin;
            int oldRotation = instance.Rotation;
            var check = grid.Move(instanceId, newOrigin, w, h, def.RuleFor(newRotation));
            if (check != PlacementCheck.Ok) return check;

            instance.Origin = newOrigin;
            instance.Rotation = newRotation;
            GameLog.Info(LogCategory.Building, $"Moved {def.Id} {oldOrigin} -> {newOrigin}");
            BuildingMoved?.Invoke(instance, oldOrigin, oldRotation);
            return PlacementCheck.Ok;
        }

        private BuildingInstance Create(BuildingDefinition def, GridPos origin, int rotation, string instanceId)
        {
            Footprint.Size(def.FootprintWidth, def.FootprintHeight, rotation, out int w, out int h);
            grid.Occupy(instanceId, origin, w, h, def.RuleFor(rotation));
            var instance = new BuildingInstance
            {
                InstanceId = instanceId,
                DefinitionId = def.Id,
                Level = 1,
                Origin = origin,
                Rotation = rotation
            };
            buildings.Add(instance);
            byId[instanceId] = instance;
            return instance;
        }
    }
}
