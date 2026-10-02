using System;
using System.Collections.Generic;

namespace AgeOfSakura.Core
{
    public enum SessionStartKind
    {
        NewGame,
        Loaded,
        /// <summary>The save was unreadable; it was preserved and a fresh game started.</summary>
        RecoveredFromCorruptSave,
        /// <summary>The save was written by a newer game version; it was left untouched and a fresh game started in memory only.</summary>
        SaveTooNew
    }

    /// <summary>
    /// Composition root of the engine-independent game state: wallet, grid, buildings, production and persistence.
    /// Owns saving (dirty-flag + explicit flush) so the Unity layer only decides WHEN to flush.
    /// </summary>
    public sealed class GameSession
    {
        private readonly ISaveStorage storage;
        private readonly ITimeProvider time;
        private bool suspendDirty;
        private bool saveWritesDisabled;

        public GameDefinitions Definitions { get; }
        public Wallet Wallet { get; }
        public GridMap Grid { get; }
        public EconomyService Economy { get; }
        public BuildingService Buildings { get; }
        public ProductionService Production { get; }
        public HousingService Housing { get; }
        public ITimeProvider Time => time;
        public bool IsDirty { get; private set; }

        public GameSession(GameDefinitions definitions, ISaveStorage storage, ITimeProvider time, IAnalyticsService analytics = null, float cellSize = 1f)
        {
            Definitions = definitions;
            this.storage = storage;
            this.time = time;
            analytics = analytics ?? new NullAnalyticsService();

            Wallet = new Wallet();
            Grid = GridMap.FromDefinition(definitions.Map, cellSize);
            Economy = new EconomyService(Wallet, definitions.Economy, analytics);
            Buildings = new BuildingService(definitions, Grid, Wallet, analytics);
            Production = new ProductionService(definitions, Buildings, Wallet, Economy, time, analytics);
            Housing = new HousingService(definitions, Buildings, Grid, Wallet, analytics);

            // Every state-changing action marks the session dirty; the host flushes once per frame / on pause.
            Wallet.CurrencyChanged += _ => MarkDirty();
            Buildings.BuildingPlaced += _ => MarkDirty();
            Buildings.BuildingMoved += (_, __, ___) => MarkDirty();
            Production.ProductionStarted += _ => MarkDirty();
            Production.ProductionCompleted += _ => MarkDirty();
            Production.ProductionCollected += (_, __) => MarkDirty();
            Housing.BuildingUpgraded += _ => MarkDirty();
        }

        /// <summary>Loads the save (or recovers) and applies offline production completion.</summary>
        public SessionStartKind Start()
        {
            var kind = SessionStartKind.NewGame;
            suspendDirty = true;
            try
            {
                if (storage.Exists())
                {
                    string raw = null;
                    try
                    {
                        raw = storage.Read();
                        Apply(SaveCodec.Decode(raw));
                        kind = SessionStartKind.Loaded;
                    }
                    catch (SaveTooNewException e)
                    {
                        GameLog.Error(LogCategory.Save, e.Message + " Starting a temporary new game; the newer save file is left untouched.");
                        saveWritesDisabled = true;
                        ResetToNewGameState();
                        kind = SessionStartKind.SaveTooNew;
                    }
                    catch (Exception e)
                    {
                        // Any failure while reading/applying the save is treated as corruption: logged loudly, file preserved, game starts fresh.
                        GameLog.Error(LogCategory.Save, "Save is corrupt, preserving it and starting fresh: " + e.Message);
                        storage.PreserveCorrupt(raw ?? string.Empty);
                        ResetToNewGameState();
                        kind = SessionStartKind.RecoveredFromCorruptSave;
                    }
                }
                else
                {
                    ResetToNewGameState();
                }

                EnsureTownHall();
                Housing.ApplyExpansions(); // land earned by the Town Hall's level
                Production.Refresh(); // offline completion: elapsed timers become ReadyToCollect, rewards stay ungranted
            }
            finally
            {
                suspendDirty = false;
            }

            if (kind != SessionStartKind.Loaded) MarkDirty(); // persist the fresh game immediately
            FlushIfDirty();
            return kind;
        }

        /// <summary>Call when the app resumes: elapsed timers become ReadyToCollect.</summary>
        public void Resume() => Production.Refresh();

        public void MarkDirty()
        {
            if (!suspendDirty) IsDirty = true;
        }

        public void FlushIfDirty()
        {
            if (IsDirty) SaveNow();
        }

        public void SaveNow()
        {
            if (saveWritesDisabled) return;
            storage.Write(SaveCodec.Encode(Capture()));
            IsDirty = false;
            GameLog.Info(LogCategory.Save, "Saved");
        }

        /// <summary>Deletes the stored save. The host must discard this session afterwards and create a new one.</summary>
        public void DeleteSave()
        {
            storage.Delete();
            IsDirty = false;
            saveWritesDisabled = true;
            GameLog.Info(LogCategory.Save, "Save deleted");
        }

        public GameSave Capture()
        {
            var save = new GameSave { Version = SaveMigrator.CurrentVersion, SavedAtUtc = time.UtcNow };
            foreach (CurrencyType c in Enum.GetValues(typeof(CurrencyType))) save.Wallet[c.ToString()] = Wallet.GetBalance(c);
            foreach (var b in Buildings.All)
            {
                save.Buildings.Add(new BuildingSave
                {
                    InstanceId = b.InstanceId,
                    DefinitionId = b.DefinitionId,
                    Level = b.Level,
                    CyclesAtLevel = b.CyclesAtLevel,
                    X = b.Origin.X,
                    Z = b.Origin.Z,
                    Rotation = b.Rotation,
                    ProductionState = b.State.ToString(),
                    ActiveProductionId = b.ActiveProductionId,
                    ProductionStartUtc = b.State == ProductionState.Idle ? (DateTime?)null : b.ProductionStartUtc,
                    ProductionEndUtc = b.State == ProductionState.Idle ? (DateTime?)null : b.ProductionEndUtc
                });
            }
            return save;
        }

        private void Apply(GameSave save)
        {
            var balances = new Dictionary<CurrencyType, long>();
            foreach (var pair in save.Wallet)
            {
                if (!Enum.TryParse(pair.Key, false, out CurrencyType currency) || !Enum.IsDefined(typeof(CurrencyType), currency))
                    throw new SaveCorruptException($"Save wallet has unknown currency '{pair.Key}'.");
                if (pair.Value < 0) throw new SaveCorruptException($"Save wallet has negative balance for {pair.Key}.");
                balances[currency] = pair.Value;
            }
            Wallet.Restore(balances);

            foreach (var b in save.Buildings)
            {
                if (!Enum.TryParse(b.ProductionState, false, out ProductionState state) || !Enum.IsDefined(typeof(ProductionState), state))
                    throw new SaveCorruptException($"Building {b.InstanceId} has unknown production state '{b.ProductionState}'.");

                var instance = new BuildingInstance
                {
                    InstanceId = b.InstanceId,
                    DefinitionId = b.DefinitionId,
                    Level = Math.Max(1, b.Level),
                    CyclesAtLevel = Math.Max(0, b.CyclesAtLevel),
                    Origin = new GridPos(b.X, b.Z),
                    Rotation = Footprint.NormalizeRotation(b.Rotation),
                    State = state,
                    ActiveProductionId = b.ActiveProductionId
                };
                if (state != ProductionState.Idle)
                {
                    if (b.ActiveProductionId == null || !b.ProductionStartUtc.HasValue || !b.ProductionEndUtc.HasValue)
                    {
                        GameLog.Error(LogCategory.Save, $"Building {b.InstanceId} has incomplete production data; reset to Idle.");
                        instance.ClearProduction();
                    }
                    else
                    {
                        instance.ProductionStartUtc = b.ProductionStartUtc.Value;
                        instance.ProductionEndUtc = b.ProductionEndUtc.Value;
                    }
                }
                Buildings.TryRestore(instance);
            }
        }

        private void ResetToNewGameState()
        {
            // Called only on a pristine session (nothing restored yet), or after a failed partial restore.
            ClearBuildings();
            var eco = Definitions.Economy;
            var start = new Dictionary<CurrencyType, long>
            {
                { CurrencyType.Coins, eco.StartingCoins },
                { CurrencyType.Wood, eco.StartingWood },
                { CurrencyType.Diamonds, eco.StartingDiamonds }
            };
            Wallet.Restore(start);
            Buildings.PlaceFree(Definitions.Map.TownHallId, Definitions.Map.TownHallOrigin, 0);
            GameLog.Info(LogCategory.Save, "Started a new game");
        }

        private void ClearBuildings()
        {
            foreach (var b in new List<BuildingInstance>(Buildings.All)) Buildings.RemoveForReset(b.InstanceId);
        }

        private void EnsureTownHall()
        {
            var map = Definitions.Map;
            foreach (var b in Buildings.All)
            {
                if (b.DefinitionId == map.TownHallId) return;
            }
            GameLog.Warn(LogCategory.Building, "No Town Hall in the loaded state; placing a new one.");
            Buildings.PlaceFree(map.TownHallId, map.TownHallOrigin, 0);
        }
    }
}
