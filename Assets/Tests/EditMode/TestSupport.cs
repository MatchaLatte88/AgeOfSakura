using System;
using System.IO;
using AgeOfSakura.Core;
#if UNITY_EDITOR
using UnityEngine;
#endif

namespace AgeOfSakura.Tests
{
    /// <summary>Controllable clock so tests never wait in real time.</summary>
    public sealed class FakeTimeProvider : ITimeProvider
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        public void Advance(TimeSpan span) => UtcNow += span;
        public void AdvanceSeconds(double seconds) => UtcNow = UtcNow.AddSeconds(seconds);
    }

    public static class TestData
    {
        public static string DefinitionsJson()
        {
#if UNITY_EDITOR
            var asset = Resources.Load<TextAsset>("Definitions/game_definitions");
            if (asset == null) throw new FileNotFoundException("Resources/Definitions/game_definitions.json not found.");
            return asset.text;
#else
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "game_definitions.json"));
#endif
        }

        public static GameDefinitions Definitions() => DefinitionLoader.LoadValidated(DefinitionsJson());

        public static GameSession NewSession(out FakeTimeProvider time, out InMemorySaveStorage storage)
        {
            time = new FakeTimeProvider();
            storage = new InMemorySaveStorage();
            var session = new GameSession(Definitions(), storage, time);
            session.Start();
            return session;
        }

        /// <summary>Simulates closing and reopening the app: a brand-new session reading the same storage and clock.</summary>
        public static GameSession Reload(InMemorySaveStorage storage, FakeTimeProvider time, out SessionStartKind kind)
        {
            var session = new GameSession(Definitions(), storage, time);
            kind = session.Start();
            return session;
        }

        public static BuildingInstance First(GameSession s, string definitionId)
        {
            foreach (var b in s.Buildings.All) if (b.DefinitionId == definitionId) return b;
            throw new InvalidOperationException($"No building '{definitionId}' in session.");
        }
    }
}
