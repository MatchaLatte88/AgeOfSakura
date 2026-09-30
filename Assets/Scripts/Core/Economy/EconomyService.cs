using System;
using System.Collections.Generic;

namespace AgeOfSakura.Core
{
    /// <summary>
    /// Premium-currency rules on top of the <see cref="Wallet"/>. UI never touches Diamonds directly;
    /// pricing formula and spending live here so IAP/analytics can plug in without redesigning production.
    /// </summary>
    public sealed class EconomyService
    {
        private readonly Wallet wallet;
        private readonly EconomyConfig config;
        private readonly IAnalyticsService analytics;

        public EconomyService(Wallet wallet, EconomyConfig config, IAnalyticsService analytics)
        {
            this.wallet = wallet;
            this.config = config;
            this.analytics = analytics;
        }

        /// <summary>Diamonds needed to finish a production instantly: max(minSkipCost, ceil(remaining / secondsPerDiamond)).</summary>
        public int GetProductionSkipCost(TimeSpan remaining)
        {
            double seconds = Math.Max(0d, remaining.TotalSeconds);
            int byTime = (int)Math.Ceiling(seconds / config.SecondsPerDiamond);
            return Math.Max(config.MinSkipCost, byTime);
        }

        public bool TrySpendPremiumCurrency(int amount, CurrencyTransactionReason reason)
        {
            if (!wallet.TrySpend(CurrencyType.Diamonds, amount, reason)) return false;
            if (amount > 0)
                analytics.Track(AnalyticsEvents.CurrencySpent, new Dictionary<string, object> { { "currency", "Diamonds" }, { "amount", amount }, { "reason", reason.ToString() } });
            return true;
        }
    }
}
