using System;
using System.Collections.Generic;

namespace AgeOfSakura.Core
{
    /// <summary>Event names emitted at the points where a real analytics backend can hook in later.</summary>
    public static class AnalyticsEvents
    {
        public const string BuildingPlaced = "building_placed";
        public const string BuildingUpgraded = "building_upgraded";
        public const string ProductionStarted = "production_started";
        public const string ProductionSkipped = "production_skipped";
        public const string ProductionCollected = "production_collected";
        public const string CurrencySpent = "currency_spent";
    }

    public interface IAnalyticsService
    {
        void Track(string eventName, IReadOnlyDictionary<string, object> parameters = null);
    }

    public sealed class NullAnalyticsService : IAnalyticsService
    {
        public void Track(string eventName, IReadOnlyDictionary<string, object> parameters = null) { }
    }

    public readonly struct PurchaseResult
    {
        public readonly bool Success;
        public readonly string Error;

        public PurchaseResult(bool success, string error)
        {
            Success = success;
            Error = error;
        }
    }

    /// <summary>
    /// Store abstraction (Google Play / App Store). Gameplay never talks to a store; a future
    /// implementation grants Diamonds through <see cref="Wallet"/> like any other source.
    /// </summary>
    public interface IPurchaseService
    {
        bool IsAvailable { get; }
        void RequestPurchase(string productId, Action<PurchaseResult> onCompleted);
    }

    public sealed class NullPurchaseService : IPurchaseService
    {
        public bool IsAvailable => false;

        public void RequestPurchase(string productId, Action<PurchaseResult> onCompleted)
        {
            onCompleted?.Invoke(new PurchaseResult(false, "Purchases are not available in Prototype 0.01."));
        }
    }
}
