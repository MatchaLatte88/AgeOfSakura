using System;

namespace AgeOfSakura.Core
{
    public enum LogCategory
    {
        Save,
        Building,
        Economy,
        Production,
        Game
    }

    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Category-tagged logging for the engine-independent core. The Unity layer replaces
    /// <see cref="Sink"/> with Debug.Log*; tests and tools fall back to the console.
    /// </summary>
    public static class GameLog
    {
        public static Action<LogLevel, LogCategory, string> Sink = ConsoleSink;

        public static void Info(LogCategory category, string message) => Sink(LogLevel.Info, category, message);
        public static void Warn(LogCategory category, string message) => Sink(LogLevel.Warning, category, message);
        public static void Error(LogCategory category, string message) => Sink(LogLevel.Error, category, message);

        private static void ConsoleSink(LogLevel level, LogCategory category, string message)
        {
            Console.WriteLine($"[{category.ToString().ToUpperInvariant()}] {level}: {message}");
        }
    }
}
