using System;
using System.Collections.Generic;

namespace AgeOfSakura.Core
{
    public sealed class BuildingSave
    {
        public string InstanceId;
        public string DefinitionId;
        public int Level;
        public int CyclesAtLevel;
        public int X;
        public int Z;
        public int Rotation;
        public string ProductionState;
        public string ActiveProductionId;
        public DateTime? ProductionStartUtc;
        public DateTime? ProductionEndUtc;
    }

    public sealed class GameSave
    {
        public int Version;
        public DateTime SavedAtUtc;
        public Dictionary<string, long> Wallet = new Dictionary<string, long>();
        public List<BuildingSave> Buildings = new List<BuildingSave>();
    }

    public sealed class SaveCorruptException : Exception
    {
        public SaveCorruptException(string message) : base(message) { }
        public SaveCorruptException(string message, Exception inner) : base(message, inner) { }
    }

    public sealed class SaveTooNewException : Exception
    {
        public SaveTooNewException(int found, int supported)
            : base($"Save version {found} is newer than the supported version {supported}.") { }
    }

    /// <summary>Storage backend (file on device, memory in tests). Uses Application.persistentDataPath in the Unity layer.</summary>
    public interface ISaveStorage
    {
        bool Exists();
        string Read();
        void Write(string contents);
        void Delete();
        /// <summary>Keeps an unreadable save for inspection instead of overwriting it.</summary>
        void PreserveCorrupt(string contents);
    }

    public sealed class InMemorySaveStorage : ISaveStorage
    {
        public string Contents;
        public readonly List<string> PreservedCorrupt = new List<string>();

        public bool Exists() => Contents != null;
        public string Read() => Contents;
        public void Write(string contents) => Contents = contents;
        public void Delete() => Contents = null;
        public void PreserveCorrupt(string contents) => PreservedCorrupt.Add(contents);
    }
}
