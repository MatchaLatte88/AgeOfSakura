using System;

namespace AgeOfSakura.Core
{
    public enum CurrencyType
    {
        Coins,
        Wood,
        Diamonds,
        /// <summary>Food: produced by the Rice Paddy, consumed by houses when they collect taxes.</summary>
        Rice,
        /// <summary>Crafted by the Blacksmith from Wood; later buildings and upgrades are built with it.</summary>
        Tools,
        /// <summary>Food: caught from the Fisher Dock, eaten by better houses besides Rice.</summary>
        Fish,
        /// <summary>Mined from a mountain; the Blacksmith forges it together with Wood into Tools.</summary>
        Iron
    }

    /// <summary>Why a balance changed. Kept for future analytics / economy balancing.</summary>
    public enum CurrencyTransactionReason
    {
        InitialGrant,
        BuildingPurchase,
        ProductionReward,
        ProductionSkip,
        ProductionInput,
        BuildingUpgrade,
        DebugGrant,
        SaveRestore
    }

    public readonly struct CurrencyAmount
    {
        public readonly CurrencyType Currency;
        public readonly long Amount;

        public CurrencyAmount(CurrencyType currency, long amount)
        {
            Currency = currency;
            Amount = amount;
        }

        public override string ToString() => $"{Amount} {Currency}";
    }

    public readonly struct CurrencyChange
    {
        public readonly CurrencyType Currency;
        public readonly long OldValue;
        public readonly long NewValue;
        public readonly CurrencyTransactionReason Reason;

        public CurrencyChange(CurrencyType currency, long oldValue, long newValue, CurrencyTransactionReason reason)
        {
            Currency = currency;
            OldValue = oldValue;
            NewValue = newValue;
            Reason = reason;
        }
    }

    public static class CurrencyTypeInfo
    {
        public static readonly int Count = Enum.GetValues(typeof(CurrencyType)).Length;
    }
}
