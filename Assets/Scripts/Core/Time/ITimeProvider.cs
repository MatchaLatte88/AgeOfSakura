using System;

namespace AgeOfSakura.Core
{
    public interface ITimeProvider
    {
        DateTime UtcNow { get; }
    }

    /// <summary>
    /// Device UTC clock.
    /// Device time is not secure against clock manipulation. Replace/validate with trusted server
    /// time before production monetization goes live.
    /// </summary>
    public sealed class SystemTimeProvider : ITimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
