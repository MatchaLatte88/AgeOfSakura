
namespace AgeOfSakura.Core
{
    public enum GoalKind
    {
        /// <summary>Nothing useful to suggest right now (e.g. waiting for a timer, or everything is done).</summary>
        None,
        Build,
        /// <summary>Not enough <see cref="Goal.Currency"/> for <see cref="Goal.BuildingId"/> (or the upgrade); <see cref="Goal.Amount"/> is what is missing.</summary>
        Gather,
        HarvestRice,
        StartTaxes,
        CollectTaxes,
        /// <summary>A house needs more of <see cref="Goal.Need"/>: <see cref="Goal.Value"/> now, <see cref="Goal.Target"/> wanted.</summary>
        ImproveNeed,
        UpgradeHouse,
        /// <summary>Tools are missing for the next step: build the Blacksmith, then forge.</summary>
        ForgeTools,
        /// <summary>The houses want Fish: build the Fisher Dock, then start a catch.</summary>
        CatchFish
    }

    public sealed class Goal
    {
        public GoalKind Kind;
        public string BuildingId;
        public CurrencyType Currency;
        public long Amount;
        public NeedType Need;
        public float Value;
        public float Target;
        public int Level;
    }

    /// <summary>
    /// Suggests the single next useful step of the first playthrough: build the chain (Woodcutter, Rice Paddy, House), get the first
    /// taxes flowing, then whatever the next house upgrade still lacks. Pure function of the game state, so it never drifts from the rules.
    /// </summary>
    public static class GoalAdvisor
    {
        public const string WoodcutterId = "woodcutter";
        public const string RicePaddyId = "rice_paddy";
        public const string HouseId = "house";
        public const string BlacksmithId = "blacksmith";
        public const string FisherDockId = "fisher_dock";
        public const string MineId = "mine";

        public static Goal Next(GameSession session)
        {
            foreach (var id in new[] { WoodcutterId, RicePaddyId, HouseId })
            {
                if (session.Definitions.TryGetBuilding(id, out var def) && !Owns(session, id)) return BuildOrGather(session, def);
            }

            BuildingInstance target = null;
            foreach (var b in session.Buildings.All)
            {
                if (b.DefinitionId != HouseId) continue;
                var info = session.Housing.Evaluate(b);
                if (info.IsMaxLevel) continue;
                if (target == null || b.Level < target.Level || (b.Level == target.Level && b.CyclesAtLevel > target.CyclesAtLevel)) target = b;
            }
            if (target == null) return new Goal { Kind = GoalKind.None };

            var upgrade = session.Housing.Evaluate(target);
            if (upgrade.CanUpgrade) return new Goal { Kind = GoalKind.UpgradeHouse, BuildingId = HouseId, Level = upgrade.NextLevel };

            foreach (var need in upgrade.Needs)
            {
                if (need.Met) continue;
                if (need.Requirement.Type == NeedType.Served) return Serving(session, target, need);
                return new Goal
                {
                    Kind = GoalKind.ImproveNeed, Need = need.Requirement.Type, Value = need.Current,
                    Target = need.Requirement.HasMax ? need.Requirement.Max : need.Requirement.Min, Level = upgrade.NextLevel
                };
            }

            foreach (var cost in upgrade.Cost)
            {
                long have = session.Wallet.GetBalance(cost.Currency);
                if (have >= cost.Amount) continue;
                if (cost.Currency == CurrencyType.Tools)
                {
                    if (session.Definitions.TryGetBuilding(BlacksmithId, out var smith) && !Owns(session, BlacksmithId)) return BuildOrGather(session, smith);
                    // the smith stands but has no iron to forge: the mine comes first
                    if (session.Wallet.GetBalance(CurrencyType.Iron) < 2 && session.Definitions.TryGetBuilding(MineId, out var mine) && !Owns(session, MineId)) return BuildOrGather(session, mine);
                    return new Goal { Kind = GoalKind.ForgeTools, BuildingId = BlacksmithId, Currency = cost.Currency, Amount = cost.Amount - have, Level = upgrade.NextLevel };
                }
                return new Goal { Kind = GoalKind.Gather, BuildingId = HouseId, Currency = cost.Currency, Amount = cost.Amount - have, Level = upgrade.NextLevel };
            }
            return new Goal { Kind = GoalKind.None };
        }

        private static Goal Serving(GameSession session, BuildingInstance house, NeedStatus need)
        {
            switch (house.State)
            {
                case ProductionState.ReadyToCollect:
                    return new Goal { Kind = GoalKind.CollectTaxes, BuildingId = HouseId };
                case ProductionState.Idle:
                    var missing = MissingInput(session, house);
                    if (missing == null) return new Goal { Kind = GoalKind.StartTaxes, BuildingId = HouseId };
                    if (missing.Value == CurrencyType.Fish) return new Goal { Kind = GoalKind.CatchFish, BuildingId = FisherDockId };
                    return new Goal { Kind = GoalKind.HarvestRice, BuildingId = RicePaddyId };
                default:
                    return new Goal { Kind = GoalKind.ImproveNeed, Need = NeedType.Served, Value = need.Current, Target = need.Requirement.Min };
            }
        }

        /// <summary>The first good the house's tax run lacks, or null when everything is on hand.</summary>
        private static CurrencyType? MissingInput(GameSession session, BuildingInstance house)
        {
            var def = session.Buildings.GetDefinition(house);
            var ids = def.GetProductionIds(house.Level);
            if (ids.Count == 0) return null;
            var production = session.Definitions.GetProduction(ids[0]);
            foreach (var input in production.Inputs)
                if (!session.Wallet.CanAfford(input.Currency, input.Amount)) return input.Currency;
            return null;
        }

        private static Goal BuildOrGather(GameSession session, BuildingDefinition def)
        {
            foreach (var cost in def.BuildCost)
            {
                long have = session.Wallet.GetBalance(cost.Currency);
                if (have < cost.Amount) return new Goal { Kind = GoalKind.Gather, BuildingId = def.Id, Currency = cost.Currency, Amount = cost.Amount - have };
            }
            return new Goal { Kind = GoalKind.Build, BuildingId = def.Id };
        }

        private static bool Owns(GameSession session, string definitionId)
        {
            foreach (var b in session.Buildings.All) if (b.DefinitionId == definitionId) return true;
            return false;
        }
    }
}
