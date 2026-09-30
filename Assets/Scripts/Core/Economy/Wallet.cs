using System;
using System.Collections.Generic;

namespace AgeOfSakura.Core
{
    /// <summary>
    /// The single place where currency balances change. Nothing else may hold or mutate balances,
    /// so every change carries a reason and raises <see cref="CurrencyChanged"/> for the HUD/analytics.
    /// </summary>
    public sealed class Wallet
    {
        private readonly long[] balances = new long[CurrencyTypeInfo.Count];

        public event Action<CurrencyChange> CurrencyChanged;

        public long GetBalance(CurrencyType currency) => balances[(int)currency];

        public bool CanAfford(CurrencyType currency, long amount) => amount >= 0 && balances[(int)currency] >= amount;

        public bool CanAfford(IReadOnlyList<CurrencyAmount> costs)
        {
            var needed = SumByCurrency(costs);
            for (int i = 0; i < needed.Length; i++)
            {
                if (balances[i] < needed[i]) return false;
            }
            return true;
        }

        /// <summary>Atomically spends all costs, or nothing if any cost cannot be paid.</summary>
        public bool TrySpend(IReadOnlyList<CurrencyAmount> costs, CurrencyTransactionReason reason)
        {
            var needed = SumByCurrency(costs);
            for (int i = 0; i < needed.Length; i++)
            {
                if (balances[i] < needed[i]) return false;
            }

            for (int i = 0; i < needed.Length; i++)
            {
                if (needed[i] > 0) Apply((CurrencyType)i, -needed[i], reason);
            }
            return true;
        }

        public bool TrySpend(CurrencyType currency, long amount, CurrencyTransactionReason reason)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Cannot spend a negative amount.");
            if (balances[(int)currency] < amount) return false;
            if (amount > 0) Apply(currency, -amount, reason);
            return true;
        }

        public void Add(CurrencyType currency, long amount, CurrencyTransactionReason reason)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount to add must be positive.");
            Apply(currency, amount, reason);
        }

        /// <summary>Overwrites all balances (save restore). Raises change events so views refresh.</summary>
        public void Restore(IReadOnlyDictionary<CurrencyType, long> values)
        {
            for (int i = 0; i < balances.Length; i++)
            {
                var currency = (CurrencyType)i;
                long target = values.TryGetValue(currency, out var v) ? v : 0;
                if (target < 0) throw new ArgumentOutOfRangeException(nameof(values), $"Negative balance for {currency}.");
                long old = balances[i];
                balances[i] = target;
                if (old != target) CurrencyChanged?.Invoke(new CurrencyChange(currency, old, target, CurrencyTransactionReason.SaveRestore));
            }
        }

        private void Apply(CurrencyType currency, long delta, CurrencyTransactionReason reason)
        {
            long old = balances[(int)currency];
            long updated = old + delta;
            balances[(int)currency] = updated;
            GameLog.Info(LogCategory.Economy, $"{currency} {old} -> {updated} ({reason})");
            CurrencyChanged?.Invoke(new CurrencyChange(currency, old, updated, reason));
        }

        private static long[] SumByCurrency(IReadOnlyList<CurrencyAmount> costs)
        {
            var sums = new long[CurrencyTypeInfo.Count];
            for (int i = 0; i < costs.Count; i++)
            {
                if (costs[i].Amount < 0) throw new ArgumentOutOfRangeException(nameof(costs), "Cost amounts must not be negative.");
                sums[(int)costs[i].Currency] += costs[i].Amount;
            }
            return sums;
        }
    }
}
