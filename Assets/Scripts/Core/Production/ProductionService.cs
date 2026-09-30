using System;
using System.Collections.Generic;

namespace AgeOfSakura.Core
{
    public enum StartProductionStatus { Ok, UnknownBuilding, NotIdle, UnknownProduction, NotAllowedForBuilding, MissingInputs }
    public enum CollectStatus { Ok, UnknownBuilding, NotReady }
    public enum SkipStatus { Ok, UnknownBuilding, NotProducing, InsufficientDiamonds }

    public readonly struct CollectResult
    {
        public readonly CollectStatus Status;
        public readonly IReadOnlyList<CurrencyAmount> Rewards;

        public CollectResult(CollectStatus status, IReadOnlyList<CurrencyAmount> rewards)
        {
            Status = status;
            Rewards = rewards;
        }

        public bool Success => Status == CollectStatus.Ok;
    }

    public readonly struct SkipResult
    {
        public readonly SkipStatus Status;
        public readonly int Cost;

        public SkipResult(SkipStatus status, int cost)
        {
            Status = status;
            Cost = cost;
        }

        public bool Success => Status == SkipStatus.Ok;
    }

    /// <summary>
    /// Production state machine: Idle -> Producing -> ReadyToCollect -> Idle. Time is compared against stored UTC
    /// timestamps (never counted down and re-saved), so production completes while the app is closed.
    /// Rewards are granted only by <see cref="TryCollect"/>, exactly once.
    /// </summary>
    public sealed class ProductionService
    {
        private readonly GameDefinitions defs;
        private readonly BuildingService buildings;
        private readonly Wallet wallet;
        private readonly EconomyService economy;
        private readonly ITimeProvider time;
        private readonly IAnalyticsService analytics;

        public event Action<BuildingInstance> ProductionStarted;
        /// <summary>Raised when a production becomes ReadyToCollect (naturally or via Diamonds/debug).</summary>
        public event Action<BuildingInstance> ProductionCompleted;
        public event Action<BuildingInstance, IReadOnlyList<CurrencyAmount>> ProductionCollected;

        public ProductionService(GameDefinitions defs, BuildingService buildings, Wallet wallet, EconomyService economy, ITimeProvider time, IAnalyticsService analytics)
        {
            this.defs = defs;
            this.buildings = buildings;
            this.wallet = wallet;
            this.economy = economy;
            this.time = time;
            this.analytics = analytics;
        }

        public StartProductionStatus TryStart(string instanceId, string productionId)
        {
            if (!buildings.TryGet(instanceId, out var instance)) return StartProductionStatus.UnknownBuilding;
            if (!defs.TryGetProduction(productionId, out var production)) return StartProductionStatus.UnknownProduction;
            var buildingDef = buildings.GetDefinition(instance);
            bool allowed = false;
            foreach (var id in buildingDef.GetProductionIds(instance.Level)) allowed |= id == productionId;
            if (!allowed) return StartProductionStatus.NotAllowedForBuilding;

            Refresh(instance);
            if (instance.State != ProductionState.Idle) return StartProductionStatus.NotIdle;
            // Supply chain: goods are taken from the store when work begins. Atomic, so a failed start costs nothing.
            if (!wallet.TrySpend(production.Inputs, CurrencyTransactionReason.ProductionInput)) return StartProductionStatus.MissingInputs;

            var now = time.UtcNow;
            instance.State = ProductionState.Producing;
            instance.ActiveProductionId = productionId;
            instance.ProductionStartUtc = now;
            instance.ProductionEndUtc = now.AddSeconds(production.DurationSeconds);
            GameLog.Info(LogCategory.Production, $"{buildingDef.Id} started {productionId} ({production.DurationSeconds}s)");
            analytics.Track(AnalyticsEvents.ProductionStarted, new Dictionary<string, object> { { "building", buildingDef.Id }, { "production", productionId } });
            ProductionStarted?.Invoke(instance);
            return StartProductionStatus.Ok;
        }

        /// <summary>Promotes all finished productions to ReadyToCollect. Cheap; call a few times per second and on resume/load.</summary>
        public int Refresh()
        {
            int changed = 0;
            var all = buildings.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (Refresh(all[i])) changed++;
            }
            return changed;
        }

        /// <summary>Returns true if this call changed the building's state.</summary>
        public bool Refresh(BuildingInstance instance)
        {
            if (instance.State != ProductionState.Producing) return false;

            if (!defs.TryGetProduction(instance.ActiveProductionId, out _))
            {
                // Recovery: the recipe vanished from the data (e.g. rebalance). Return to Idle rather than getting stuck.
                GameLog.Error(LogCategory.Production, $"Building {instance.InstanceId} runs unknown production '{instance.ActiveProductionId}'; reset to Idle.");
                instance.ClearProduction();
                return true;
            }

            if (instance.ProductionEndUtc > time.UtcNow) return false;
            instance.State = ProductionState.ReadyToCollect;
            GameLog.Info(LogCategory.Production, $"{instance.DefinitionId} {instance.ActiveProductionId} ready to collect");
            ProductionCompleted?.Invoke(instance);
            return true;
        }

        /// <summary>Time left; never larger than the recipe duration (guards against the device clock moving backwards).</summary>
        public TimeSpan GetRemaining(BuildingInstance instance)
        {
            if (instance.State != ProductionState.Producing) return TimeSpan.Zero;
            var remaining = instance.ProductionEndUtc - time.UtcNow;
            var total = instance.ProductionEndUtc - instance.ProductionStartUtc;
            if (remaining > total) remaining = total;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        public float GetProgress01(BuildingInstance instance)
        {
            if (instance.State == ProductionState.ReadyToCollect) return 1f;
            if (instance.State != ProductionState.Producing) return 0f;
            double total = (instance.ProductionEndUtc - instance.ProductionStartUtc).TotalSeconds;
            if (total <= 0d) return 1f;
            double p = 1d - GetRemaining(instance).TotalSeconds / total;
            return (float)Math.Min(1d, Math.Max(0d, p));
        }

        public ProductionDefinition GetActiveProduction(BuildingInstance instance) =>
            instance.ActiveProductionId != null && defs.TryGetProduction(instance.ActiveProductionId, out var p) ? p : null;

        public int GetSkipCost(BuildingInstance instance) => economy.GetProductionSkipCost(GetRemaining(instance));

        public SkipResult TrySkip(string instanceId)
        {
            if (!buildings.TryGet(instanceId, out var instance)) return new SkipResult(SkipStatus.UnknownBuilding, 0);
            Refresh(instance);
            if (instance.State != ProductionState.Producing) return new SkipResult(SkipStatus.NotProducing, 0);

            int cost = GetSkipCost(instance);
            if (!economy.TrySpendPremiumCurrency(cost, CurrencyTransactionReason.ProductionSkip))
                return new SkipResult(SkipStatus.InsufficientDiamonds, cost);

            analytics.Track(AnalyticsEvents.ProductionSkipped, new Dictionary<string, object> { { "building", instance.DefinitionId }, { "production", instance.ActiveProductionId }, { "cost", cost } });
            MarkReady(instance);
            return new SkipResult(SkipStatus.Ok, cost);
        }

        /// <summary>Grants the reward once and returns the building to Idle. A second call fails with NotReady.</summary>
        public CollectResult TryCollect(string instanceId)
        {
            if (!buildings.TryGet(instanceId, out var instance)) return new CollectResult(CollectStatus.UnknownBuilding, null);
            Refresh(instance);
            if (instance.State != ProductionState.ReadyToCollect) return new CollectResult(CollectStatus.NotReady, null);

            if (!defs.TryGetProduction(instance.ActiveProductionId, out var production))
            {
                // Recovery (documented in README): unknown recipe on a finished production -> back to Idle, no reward.
                GameLog.Error(LogCategory.Production, $"Building {instance.InstanceId} finished unknown production '{instance.ActiveProductionId}'; reset to Idle without reward.");
                instance.ClearProduction();
                return new CollectResult(CollectStatus.NotReady, null);
            }
            // State is cleared BEFORE granting so a re-entrant/duplicate tap can never pay twice.
            instance.ClearProduction();
            instance.CyclesAtLevel++;
            foreach (var reward in production.Rewards) wallet.Add(reward.Currency, reward.Amount, CurrencyTransactionReason.ProductionReward);

            GameLog.Info(LogCategory.Production, $"{instance.DefinitionId} collected {production.Id}");
            analytics.Track(AnalyticsEvents.ProductionCollected, new Dictionary<string, object> { { "building", instance.DefinitionId }, { "production", production.Id } });
            ProductionCollected?.Invoke(instance, production.Rewards);
            return new CollectResult(CollectStatus.Ok, production.Rewards);
        }

        /// <summary>Debug: finishes every running production for free.</summary>
        public int FinishAll()
        {
            int count = 0;
            var all = buildings.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].State != ProductionState.Producing) continue;
                MarkReady(all[i]);
                count++;
            }
            return count;
        }

        private void MarkReady(BuildingInstance instance)
        {
            instance.State = ProductionState.ReadyToCollect;
            instance.ProductionEndUtc = time.UtcNow;
            ProductionCompleted?.Invoke(instance);
        }
    }
}
