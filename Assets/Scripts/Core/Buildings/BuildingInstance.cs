using System;

namespace AgeOfSakura.Core
{
    public enum ProductionState
    {
        Idle,
        Producing,
        ReadyToCollect
    }

    /// <summary>
    /// Runtime state of one placed building. Identified by a stable string id (never a Unity instance id)
    /// so it survives saves, model swaps and scene reloads.
    /// </summary>
    public sealed class BuildingInstance
    {
        public string InstanceId;
        public string DefinitionId;
        /// <summary>Upgrade level (1..MaxLevel of the definition). Raised by <see cref="HousingService.TryUpgrade"/>.</summary>
        public int Level = 1;
        /// <summary>Production cycles collected since the last upgrade. Drives the "served" need of houses.</summary>
        public int CyclesAtLevel;
        public GridPos Origin;
        /// <summary>Quarter turns, 0..3.</summary>
        public int Rotation;

        public ProductionState State = ProductionState.Idle;
        public string ActiveProductionId;
        public DateTime ProductionStartUtc;
        public DateTime ProductionEndUtc;

        public void ClearProduction()
        {
            State = ProductionState.Idle;
            ActiveProductionId = null;
            ProductionStartUtc = default;
            ProductionEndUtc = default;
        }
    }
}
