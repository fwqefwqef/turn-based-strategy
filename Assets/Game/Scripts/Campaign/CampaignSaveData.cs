using System;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Campaign
{
    [Serializable]
    public sealed class CampaignSaveData
    {
        public int Version = 1;
        public int Gold = 0;
        public OwnedUnitSaveData[] OwnedUnits = Array.Empty<OwnedUnitSaveData>();
        public string[] DeploymentRosterUnitIds = Array.Empty<string>();
        public SavedInventoryEntryData[] StorageItems = Array.Empty<SavedInventoryEntryData>();
    }

    [Serializable]
    public sealed class OwnedUnitSaveData
    {
        public string UnitId;
        public string VisualId;
        public string UnitName;
        public int Level = 1;
        public int Experience = 0;
        public string[] WeaponProficiencyIds = Array.Empty<string>();
        public UnitStatBlock BaseStats;
        public UnitGrowthRates GrowthRates;
        public int CurrentHitPoints = 1;
        public int CurrentManaPoints = 0;
        public SavedInventoryEntryData[] Inventory = Array.Empty<SavedInventoryEntryData>();
        public string[] SkillIds = Array.Empty<string>();
        public string[] UniquePassiveIds = Array.Empty<string>();
        public string[] EquipPassiveIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class SavedInventoryEntryData
    {
        public string ItemId;
        public int RemainingCharges = -1;
    }
}
