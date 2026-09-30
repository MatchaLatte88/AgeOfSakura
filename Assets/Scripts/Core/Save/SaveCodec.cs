using System;
using System.Collections.Generic;
using System.Globalization;

namespace AgeOfSakura.Core
{
    public interface ISaveMigration
    {
        /// <summary>Version this migration upgrades FROM (result is FromVersion + 1).</summary>
        int FromVersion { get; }
        void Apply(Dictionary<string, object> root);
    }

    /// <summary>
    /// Upgrades older save documents step by step before they are read into <see cref="GameSave"/>.
    /// No migrations exist yet (schema version 1); the layer is here so version 2 does not need a redesign.
    /// </summary>
    public sealed class SaveMigrator
    {
        public const int CurrentVersion = 1;
        public static readonly SaveMigrator Default = new SaveMigrator(CurrentVersion, new List<ISaveMigration>());

        private readonly int targetVersion;
        private readonly Dictionary<int, ISaveMigration> steps = new Dictionary<int, ISaveMigration>();

        public SaveMigrator(int targetVersion, IEnumerable<ISaveMigration> migrations)
        {
            this.targetVersion = targetVersion;
            foreach (var m in migrations) steps[m.FromVersion] = m;
        }

        public void Migrate(Dictionary<string, object> root)
        {
            if (!root.TryGetValue("version", out var raw) || !(raw is long version) || version < 1)
                throw new SaveCorruptException("Save has no valid 'version' field.");
            if (version > targetVersion) throw new SaveTooNewException((int)version, targetVersion);

            while (version < targetVersion)
            {
                if (!steps.TryGetValue((int)version, out var step))
                    throw new SaveCorruptException($"No migration from save version {version}.");
                step.Apply(root);
                version++;
                root["version"] = version;
            }
        }
    }

    /// <summary>Converts <see cref="GameSave"/> to/from JSON text.</summary>
    public static class SaveCodec
    {
        public static string Encode(GameSave save)
        {
            var wallet = new Dictionary<string, object>();
            foreach (var pair in save.Wallet) wallet[pair.Key] = pair.Value;

            var buildings = new List<object>();
            foreach (var b in save.Buildings)
            {
                buildings.Add(new Dictionary<string, object>
                {
                    { "instanceId", b.InstanceId },
                    { "definitionId", b.DefinitionId },
                    { "level", b.Level },
                    { "cyclesAtLevel", b.CyclesAtLevel },
                    { "x", b.X },
                    { "z", b.Z },
                    { "rotation", b.Rotation },
                    { "productionState", b.ProductionState },
                    { "activeProductionId", b.ActiveProductionId },
                    { "productionStartUtc", FormatDate(b.ProductionStartUtc) },
                    { "productionEndUtc", FormatDate(b.ProductionEndUtc) }
                });
            }

            var root = new Dictionary<string, object>
            {
                { "version", save.Version },
                { "savedAtUtc", FormatDate(save.SavedAtUtc) },
                { "wallet", wallet },
                { "buildings", buildings }
            };
            return MiniJson.Serialize(root, pretty: true);
        }

        /// <summary>Parses, migrates and reads a save. Throws <see cref="SaveCorruptException"/> / <see cref="SaveTooNewException"/>.</summary>
        public static GameSave Decode(string json, SaveMigrator migrator = null)
        {
            migrator = migrator ?? SaveMigrator.Default;
            try
            {
                if (!(MiniJson.Parse(json) is Dictionary<string, object> root))
                    throw new SaveCorruptException("Save root is not a JSON object.");
                migrator.Migrate(root);

                var doc = new JObj(root, "save");
                var save = new GameSave
                {
                    Version = doc.Int("version"),
                    SavedAtUtc = ParseDate(doc.Str("savedAtUtc"), "savedAtUtc")
                };

                var wallet = doc.Obj("wallet");
                foreach (CurrencyType currency in Enum.GetValues(typeof(CurrencyType)))
                {
                    string key = currency.ToString();
                    if (wallet.Has(key)) save.Wallet[key] = wallet.Long(key);
                }

                foreach (var b in doc.ObjList("buildings"))
                {
                    save.Buildings.Add(new BuildingSave
                    {
                        InstanceId = b.Str("instanceId"),
                        DefinitionId = b.Str("definitionId"),
                        Level = b.Int("level", 1),
                        CyclesAtLevel = b.Int("cyclesAtLevel", 0),
                        X = b.Int("x"),
                        Z = b.Int("z"),
                        Rotation = b.Int("rotation", 0),
                        ProductionState = b.Str("productionState"),
                        ActiveProductionId = b.StrOrNull("activeProductionId"),
                        ProductionStartUtc = b.Has("productionStartUtc") ? ParseDate(b.Str("productionStartUtc"), "productionStartUtc") : (DateTime?)null,
                        ProductionEndUtc = b.Has("productionEndUtc") ? ParseDate(b.Str("productionEndUtc"), "productionEndUtc") : (DateTime?)null
                    });
                }
                return save;
            }
            catch (MiniJsonException e)
            {
                throw new SaveCorruptException("Save is malformed: " + e.Message, e);
            }
            catch (OverflowException e)
            {
                throw new SaveCorruptException("Save contains an out-of-range number.", e);
            }
        }

        private static string FormatDate(DateTime? value) =>
            value.HasValue ? value.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) : null;

        private static DateTime ParseDate(string text, string field)
        {
            if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date))
                throw new SaveCorruptException($"Save field '{field}' is not a valid date: '{text}'.");
            // Values are always written as UTC ("...Z"); anything else is normalised rather than trusted.
            return date.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : date.ToUniversalTime();
        }
    }
}
