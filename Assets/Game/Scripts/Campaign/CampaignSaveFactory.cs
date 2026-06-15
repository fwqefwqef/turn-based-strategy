using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Passives;
using Windy.Srpg.Game.Skills;
using Windy.Srpg.Game.Units;
using UnityEngine;

namespace Windy.Srpg.Game.Campaign
{
    public static class CampaignSaveFactory
    {
        public static CampaignSaveData CreateFromOwnedUnits(
            IEnumerable<CustomUnit> ownedUnits,
            CampaignSaveData existingSave = null,
            IEnumerable<string> deploymentRosterUnitIds = null)
        {
            CustomUnit[] units = ownedUnits?
                .Where(unit => unit != null)
                .ToArray()
                ?? Array.Empty<CustomUnit>();

            OwnedUnitSaveData[] capturedUnits = units
                .Select(unit => unit.CaptureOwnedUnitSaveData())
                .Where(data => data != null)
                .ToArray();

            return MergeOwnedUnits(existingSave, capturedUnits, deploymentRosterUnitIds);
        }

        public static CampaignSaveData EnsureStarterOwnedUnits(CampaignSaveData existingSave, IEnumerable<UnitPreset> starterPresets)
        {
            UnitPreset[] presets = starterPresets?
                .Where(preset => preset != null)
                .ToArray()
                ?? Array.Empty<UnitPreset>();

            OwnedUnitSaveData[] starterUnits = presets
                .Select(CreateOwnedUnitFromPreset)
                .Where(data => data != null)
                .ToArray();

            return MergeOwnedUnits(existingSave, starterUnits, existingSave?.DeploymentRosterUnitIds);
        }

        public static CampaignSaveData MergeOwnedUnits(
            CampaignSaveData existingSave,
            IEnumerable<OwnedUnitSaveData> ownedUnits,
            IEnumerable<string> deploymentRosterUnitIds = null)
        {
            CampaignSaveData baseSave = existingSave ?? new CampaignSaveData();
            List<OwnedUnitSaveData> savedUnits = new List<OwnedUnitSaveData>();
            Dictionary<string, int> indexByUnitId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (OwnedUnitSaveData existingUnit in baseSave.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>())
            {
                OwnedUnitSaveData clonedUnit = CloneOwnedUnit(existingUnit);
                if (!TryNormalizeIdentity(clonedUnit, out string unitId))
                {
                    continue;
                }

                indexByUnitId[unitId] = savedUnits.Count;
                savedUnits.Add(clonedUnit);
            }

            foreach (OwnedUnitSaveData unit in ownedUnits ?? Array.Empty<OwnedUnitSaveData>())
            {
                OwnedUnitSaveData clonedUnit = CloneOwnedUnit(unit);
                if (!TryNormalizeIdentity(clonedUnit, out string unitId))
                {
                    clonedUnit.UnitId = Guid.NewGuid().ToString("N");
                    unitId = clonedUnit.UnitId;
                }

                if (indexByUnitId.TryGetValue(unitId, out int existingIndex))
                {
                    savedUnits[existingIndex] = clonedUnit;
                    Debug.LogWarning($"CampaignSaveFactory: Duplicate owned unit id '{unitId}' was captured. The later unit entry overwrote the earlier one.");
                    continue;
                }

                indexByUnitId[unitId] = savedUnits.Count;
                savedUnits.Add(clonedUnit);
            }

            return new CampaignSaveData
            {
                Version = baseSave.Version,
                Gold = baseSave.Gold,
                StorageItems = CloneStorageEntries(baseSave.StorageItems),
                DeploymentRosterUnitIds = NormalizeRoster(deploymentRosterUnitIds ?? baseSave.DeploymentRosterUnitIds),
                OwnedUnits = savedUnits.ToArray()
            };
        }

        public static string[] ResolveDeploymentRoster(CampaignSaveData save, int deploymentSlotCount)
        {
            return ResolveDeploymentRosterForChapter(save?.DeploymentRosterUnitIds, save, deploymentSlotCount);
        }

        public static string[] CompactDeploymentRoster(IEnumerable<string> deploymentRosterUnitIds)
        {
            HashSet<string> seenUnitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> compactedRoster = new List<string>();

            foreach (string rosterUnitId in deploymentRosterUnitIds ?? Array.Empty<string>())
            {
                string normalizedUnitId = rosterUnitId?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedUnitId) || !seenUnitIds.Add(normalizedUnitId))
                {
                    continue;
                }

                compactedRoster.Add(normalizedUnitId);
            }

            return compactedRoster.ToArray();
        }

        public static string[] ResolveDeploymentRosterForChapter(IEnumerable<string> deploymentRosterUnitIds, CampaignSaveData save, int deploymentSlotCount)
        {
            if (deploymentSlotCount <= 0)
            {
                return Array.Empty<string>();
            }

            List<string> ownedUnitIds = new List<string>();
            HashSet<string> ownedUnitIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (OwnedUnitSaveData ownedUnit in save?.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>())
            {
                if (!TryNormalizeIdentity(ownedUnit, out string unitId))
                {
                    continue;
                }

                if (ownedUnitIdSet.Add(unitId))
                {
                    ownedUnitIds.Add(unitId);
                }
            }

            List<string> roster = new List<string>(deploymentSlotCount);
            HashSet<string> rosterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rosterUnitId in CompactDeploymentRoster(deploymentRosterUnitIds ?? save?.DeploymentRosterUnitIds))
            {
                if (string.IsNullOrWhiteSpace(rosterUnitId) || !ownedUnitIdSet.Contains(rosterUnitId) || !rosterIds.Add(rosterUnitId))
                {
                    continue;
                }

                roster.Add(rosterUnitId);
                if (roster.Count >= deploymentSlotCount)
                {
                    return roster.ToArray();
                }
            }

            foreach (string ownedUnitId in ownedUnitIds)
            {
                if (!rosterIds.Add(ownedUnitId))
                {
                    continue;
                }

                roster.Add(ownedUnitId);
                if (roster.Count >= deploymentSlotCount)
                {
                    break;
                }
            }

            return roster.ToArray();
        }

        public static OwnedUnitSaveData CreateOwnedUnitFromPreset(UnitPreset preset)
        {
            if (preset == null)
            {
                return null;
            }

            BuiltInItemCatalog.EnsureRegistered();
            BuiltInSkillCatalog.EnsureRegistered();
            BuiltInPassiveCatalog.EnsureRegistered();

            string identity = string.IsNullOrWhiteSpace(preset.PresetId)
                ? Guid.NewGuid().ToString("N")
                : preset.PresetId.Trim();

            float movementPoints = preset.BaseStats.MovementPoints > 0f
                ? preset.BaseStats.MovementPoints
                : preset.LegacyBaseMovementPoints;
            int hitPoints = Mathf.Max(1, preset.BaseStats.HitPoints);
            int manaPoints = Mathf.Max(0, preset.BaseStats.ManaPoints);
            int maxHitPoints = ResolveInitialMaxHitPoints(preset, hitPoints);
            int maxManaPoints = ResolveInitialMaxManaPoints(preset, manaPoints);

            return new OwnedUnitSaveData
            {
                UnitId = identity,
                VisualId = identity,
                UnitName = preset.UnitName ?? string.Empty,
                Level = Mathf.Max(1, preset.BaseLevel),
                Experience = 0,
                WeaponProficiencyIds = GetWeaponProficiencyIds(preset.WeaponProficiencies).ToArray(),
                BaseStats = new UnitStatBlock
                {
                    HitPoints = hitPoints,
                    ManaPoints = manaPoints,
                    MovementPoints = Mathf.Max(0f, movementPoints),
                    Strength = preset.BaseStats.Strength,
                    Defense = preset.BaseStats.Defense,
                    Magic = preset.BaseStats.Magic,
                    Resistance = preset.BaseStats.Resistance,
                    Speed = preset.BaseStats.Speed,
                    Luck = preset.BaseStats.Luck
                },
                GrowthRates = new UnitGrowthRates
                {
                    Strength = Mathf.Max(0, preset.GrowthRates.Strength),
                    Magic = Mathf.Max(0, preset.GrowthRates.Magic),
                    Defense = Mathf.Max(0, preset.GrowthRates.Defense),
                    Resistance = Mathf.Max(0, preset.GrowthRates.Resistance),
                    Speed = Mathf.Max(0, preset.GrowthRates.Speed),
                    Luck = Mathf.Max(0, preset.GrowthRates.Luck)
                },
                CurrentHitPoints = maxHitPoints,
                CurrentManaPoints = maxManaPoints,
                Inventory = CreateSavedInventoryEntries(preset.StartingInventory),
                SkillIds = CreateSkillIds(preset.StartingSkills),
                UniquePassiveIds = CreatePassiveIds(preset.StartingUniquePassives),
                EquipPassiveIds = CreatePassiveIds(preset.StartingEquipPassives)
            };
        }

        private static int ResolveInitialMaxHitPoints(UnitPreset preset, int baseHitPoints)
        {
            PrimaryStatModifiers modifiers = ResolveInitialPrimaryStatModifiers(preset);
            return Mathf.Max(1, baseHitPoints + modifiers.MaxHitPoints + preset.BaseStats.Strength);
        }

        private static int ResolveInitialMaxManaPoints(UnitPreset preset, int baseManaPoints)
        {
            PrimaryStatModifiers modifiers = ResolveInitialPrimaryStatModifiers(preset);
            int magic = preset.BaseStats.Magic + modifiers.Magic;
            int resistance = preset.BaseStats.Resistance + modifiers.Resistance;
            return Mathf.Max(0, baseManaPoints + modifiers.MaxManaPoints + ((magic + resistance) * 3));
        }

        private static PrimaryStatModifiers ResolveInitialPrimaryStatModifiers(UnitPreset preset)
        {
            PrimaryStatModifiers modifiers = default;
            if (preset == null)
            {
                return modifiers;
            }

            WeaponData equippedWeapon = ResolveInitialEquippedWeapon(preset);
            if (equippedWeapon != null)
            {
                modifiers += equippedWeapon.StatModifiers;
            }

            AccessoryData equippedAccessory = ResolveInitialEquippedAccessory(preset);
            if (equippedAccessory != null)
            {
                modifiers += equippedAccessory.StatModifiers;
            }

            foreach (PassiveData passive in ResolveInitialPassiveDefinitions(preset))
            {
                if (passive != null)
                {
                    modifiers += passive.PrimaryStatModifiers;
                }
            }

            return modifiers;
        }

        private static WeaponData ResolveInitialEquippedWeapon(UnitPreset preset)
        {
            foreach (StartingInventoryItem entry in preset.StartingInventory ?? Enumerable.Empty<StartingInventoryItem>())
            {
                if (string.IsNullOrWhiteSpace(entry.ItemId) || !ItemRegistry.TryGet(entry.ItemId, out ItemData data))
                {
                    continue;
                }

                if (data is WeaponData weapon && (preset.WeaponProficiencies & weapon.WeaponType) != 0)
                {
                    return weapon;
                }
            }

            return null;
        }

        private static AccessoryData ResolveInitialEquippedAccessory(UnitPreset preset)
        {
            foreach (StartingInventoryItem entry in preset.StartingInventory ?? Enumerable.Empty<StartingInventoryItem>())
            {
                if (string.IsNullOrWhiteSpace(entry.ItemId) || !ItemRegistry.TryGet(entry.ItemId, out ItemData data))
                {
                    continue;
                }

                if (data is AccessoryData accessory)
                {
                    return accessory;
                }
            }

            return null;
        }

        private static IEnumerable<PassiveData> ResolveInitialPassiveDefinitions(UnitPreset preset)
        {
            foreach (StartingPassiveEntry entry in preset.StartingUniquePassives ?? new List<StartingPassiveEntry>())
            {
                if (!string.IsNullOrWhiteSpace(entry.PassiveId) && PassiveRegistry.TryGet(entry.PassiveId, out PassiveData passive))
                {
                    yield return passive;
                }
            }

            foreach (StartingPassiveEntry entry in preset.StartingEquipPassives ?? new List<StartingPassiveEntry>())
            {
                if (!string.IsNullOrWhiteSpace(entry.PassiveId) && PassiveRegistry.TryGet(entry.PassiveId, out PassiveData passive))
                {
                    yield return passive;
                }
            }
        }

        private static bool TryNormalizeIdentity(OwnedUnitSaveData unit, out string unitId)
        {
            unitId = string.Empty;
            if (unit == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(unit.VisualId))
            {
                unit.VisualId = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(unit.UnitId))
            {
                unit.UnitId = !string.IsNullOrWhiteSpace(unit.VisualId)
                    ? unit.VisualId
                    : string.Empty;
            }

            unitId = unit.UnitId?.Trim() ?? string.Empty;
            unit.UnitId = unitId;
            unit.VisualId = unit.VisualId?.Trim() ?? string.Empty;
            unit.UnitName = unit.UnitName ?? string.Empty;
            return !string.IsNullOrWhiteSpace(unitId);
        }

        private static SavedInventoryEntryData[] CreateSavedInventoryEntries(IEnumerable<StartingInventoryItem> startingInventory)
        {
            List<SavedInventoryEntryData> entries = new List<SavedInventoryEntryData>();
            foreach (StartingInventoryItem entry in startingInventory ?? Array.Empty<StartingInventoryItem>())
            {
                if (string.IsNullOrWhiteSpace(entry.ItemId) || !ItemRegistry.TryGet(entry.ItemId, out ItemData data))
                {
                    continue;
                }

                int remainingCharges = -1;
                if (data is ConsumableData consumable)
                {
                    remainingCharges = entry.HasInitialChargesOverride ? entry.InitialCharges : consumable.Charges;
                    if (remainingCharges == 0)
                    {
                        continue;
                    }
                }

                entries.Add(new SavedInventoryEntryData
                {
                    ItemId = entry.ItemId,
                    RemainingCharges = remainingCharges
                });
            }

            return entries.ToArray();
        }

        private static string[] CreateSkillIds(IEnumerable<StartingSkillEntry> startingSkills)
        {
            return startingSkills?
                .Where(entry => !string.IsNullOrWhiteSpace(entry.SkillId))
                .Select(entry => entry.SkillId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? Array.Empty<string>();
        }

        private static string[] CreatePassiveIds(IEnumerable<StartingPassiveEntry> startingPassives)
        {
            return startingPassives?
                .Where(entry => !string.IsNullOrWhiteSpace(entry.PassiveId))
                .Select(entry => entry.PassiveId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? Array.Empty<string>();
        }

        private static IEnumerable<string> GetWeaponProficiencyIds(WeaponType weaponProficiencies)
        {
            WeaponType[] supportedTypes =
            {
                WeaponType.Sword,
                WeaponType.Lance,
                WeaponType.Ranged,
                WeaponType.Blunt,
                WeaponType.Magic
            };

            foreach (WeaponType supportedType in supportedTypes)
            {
                if ((weaponProficiencies & supportedType) != 0)
                {
                    yield return supportedType.ToString();
                }
            }
        }

        private static string[] NormalizeRoster(IEnumerable<string> deploymentRosterUnitIds)
        {
            HashSet<string> seenUnitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> normalizedRoster = new List<string>();

            foreach (string rosterUnitId in deploymentRosterUnitIds ?? Array.Empty<string>())
            {
                string normalizedUnitId = rosterUnitId?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedUnitId))
                {
                    normalizedRoster.Add(string.Empty);
                    continue;
                }

                if (!seenUnitIds.Add(normalizedUnitId))
                {
                    normalizedRoster.Add(string.Empty);
                    continue;
                }

                normalizedRoster.Add(normalizedUnitId);
            }

            return normalizedRoster.ToArray();
        }

        private static OwnedUnitSaveData CloneOwnedUnit(OwnedUnitSaveData unit)
        {
            if (unit == null)
            {
                return null;
            }

            return new OwnedUnitSaveData
            {
                UnitId = unit.UnitId,
                VisualId = unit.VisualId,
                UnitName = unit.UnitName,
                Level = unit.Level,
                Experience = unit.Experience,
                WeaponProficiencyIds = unit.WeaponProficiencyIds?.ToArray() ?? Array.Empty<string>(),
                BaseStats = unit.BaseStats,
                GrowthRates = unit.GrowthRates,
                CurrentHitPoints = unit.CurrentHitPoints,
                CurrentManaPoints = unit.CurrentManaPoints,
                Inventory = CloneStorageEntries(unit.Inventory),
                SkillIds = unit.SkillIds?.ToArray() ?? Array.Empty<string>(),
                UniquePassiveIds = unit.UniquePassiveIds?.ToArray() ?? Array.Empty<string>(),
                EquipPassiveIds = unit.EquipPassiveIds?.ToArray() ?? Array.Empty<string>()
            };
        }

        private static SavedInventoryEntryData[] CloneStorageEntries(IEnumerable<SavedInventoryEntryData> entries)
        {
            return entries?
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.ItemId))
                .Select(entry => new SavedInventoryEntryData
                {
                    ItemId = entry.ItemId,
                    RemainingCharges = entry.RemainingCharges
                })
                .ToArray()
                ?? Array.Empty<SavedInventoryEntryData>();
        }
    }
}
