using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Catalogs;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Passives;
using Windy.Srpg.Game.Skills;
using Windy.Srpg.Game.Units;
using UnityEngine;

namespace Windy.Srpg.Game.Campaign
{
    public static class CampaignSaveFactory
    {
        public const int CurrentSaveVersion = 14;
        public const int StartingGold = 1000;

        private static readonly string[] StartingUnitPresetIds =
        {
            "protagonist",
            "thunder",
            "flame",
            "darkness"
        };

        public static CampaignSaveData CreateNewSave(IEnumerable<UnitPreset> availablePresets = null)
        {
            return EnsureStarterOwnedUnits(new CampaignSaveData(), availablePresets);
        }

        public static CampaignSaveData CreateFromOwnedUnits(
            IEnumerable<Unit> ownedUnits,
            CampaignSaveData existingSave = null,
            IEnumerable<string> deploymentRosterUnitIds = null)
        {
            Unit[] units = ownedUnits?
                .Where(unit => unit != null)
                .ToArray()
                ?? Array.Empty<Unit>();

            OwnedUnitSaveData[] capturedUnits = units
                .Select(unit => unit.CaptureOwnedUnitSaveData())
                .Where(data => data != null)
                .ToArray();

            return MergeOwnedUnits(existingSave, capturedUnits, deploymentRosterUnitIds);
        }

        public static CampaignSaveData EnsureStarterOwnedUnits(CampaignSaveData existingSave, IEnumerable<UnitPreset> starterPresets)
        {
            FriendlyUnitPresetCatalog catalog = Resources.Load<FriendlyUnitPresetCatalog>("FriendlyUnitPresetCatalog");
            Dictionary<string, UnitPreset> availablePresetsById = (starterPresets ?? Enumerable.Empty<UnitPreset>())
                .Concat(catalog?.Presets ?? Array.Empty<UnitPreset>())
                .Where(preset => preset != null && !string.IsNullOrWhiteSpace(preset.PresetId))
                .GroupBy(preset => preset.PresetId.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

            UnitPreset[] presets = StartingUnitPresetIds
                .Select(presetId => availablePresetsById.TryGetValue(presetId, out UnitPreset preset) ? preset : null)
                .Where(preset => preset != null)
                .ToArray();

            HashSet<string> existingUnitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (OwnedUnitSaveData existingUnit in existingSave?.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>())
            {
                if (TryNormalizeIdentity(existingUnit, out string unitId))
                {
                    existingUnitIds.Add(unitId);
                }
            }

            OwnedUnitSaveData[] starterUnits = presets
                .Where(preset => !string.IsNullOrWhiteSpace(preset.PresetId)
                    && !existingUnitIds.Contains(preset.PresetId.Trim()))
                .Select(CreateOwnedUnitFromPreset)
                .Where(data => data != null)
                .ToArray();

            if (starterUnits.Length == 0)
            {
                return EnsureSaveInitialized(existingSave ?? new CampaignSaveData());
            }

            bool isNewParty = (existingSave?.OwnedUnits?.Length ?? 0) == 0;
            IEnumerable<string> deploymentRoster = isNewParty
                ? starterUnits.Select(unit => unit.UnitId)
                : existingSave?.DeploymentRosterUnitIds;
            return MergeOwnedUnits(existingSave, starterUnits, deploymentRoster);
        }

        public static CampaignSaveData MergeOwnedUnits(
            CampaignSaveData existingSave,
            IEnumerable<OwnedUnitSaveData> ownedUnits,
            IEnumerable<string> deploymentRosterUnitIds = null)
        {
            CampaignSaveData baseSave = EnsureSaveInitialized(existingSave ?? new CampaignSaveData());
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
                    continue;
                }

                indexByUnitId[unitId] = savedUnits.Count;
                savedUnits.Add(clonedUnit);
            }

            return new CampaignSaveData
            {
                Version = Mathf.Max(CurrentSaveVersion, baseSave.Version),
                Gold = baseSave.Gold,
                ClearedChapterIds = CampaignProgressUtility.NormalizeClearedChapterIds(baseSave.ClearedChapterIds),
                StorageItems = CloneStorageEntries(baseSave.StorageItems),
                ShopStockInitialized = baseSave.ShopStockInitialized,
                ShopStockItems = ShopStockUtility.CloneAndMerge(baseSave.ShopStockItems),
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
            SkillEffects.EnsureRegistered();
            PassiveEffects.EnsureRegistered();

            string identity = string.IsNullOrWhiteSpace(preset.PresetId)
                ? Guid.NewGuid().ToString("N")
                : preset.PresetId.Trim();

            int movementPoints = Mathf.Max(0, preset.BaseStats.MovementPoints);
            int hitPoints = Mathf.Max(1, preset.BaseStats.HitPoints);
            int manaPoints = Mathf.Max(0, preset.BaseStats.ManaPoints);
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
                    MovementPoints = movementPoints,
                    Strength = preset.BaseStats.Strength,
                    Defense = preset.BaseStats.Defense,
                    Magic = preset.BaseStats.Magic,
                    Speed = preset.BaseStats.Speed,
                    Luck = preset.BaseStats.Luck
                },
                GrowthRates = new UnitGrowthRates
                {
                    Strength = Mathf.Max(0, preset.GrowthRates.Strength),
                    Magic = Mathf.Max(0, preset.GrowthRates.Magic),
                    Defense = Mathf.Max(0, preset.GrowthRates.Defense),
                    Speed = Mathf.Max(0, preset.GrowthRates.Speed),
                    Luck = Mathf.Max(0, preset.GrowthRates.Luck)
                },
                Inventory = CreateSavedInventoryEntries(preset.StartingInventory),
                SkillIds = CreateSkillIds(preset.StartingSkills),
                ClassPassiveIds = CreatePassiveIds(preset.StartingClassPassives)
            };
        }

        private static CampaignSaveData EnsureSaveInitialized(CampaignSaveData save)
        {
            save ??= new CampaignSaveData();
            save.ClearedChapterIds = CampaignProgressUtility.NormalizeClearedChapterIds(save.ClearedChapterIds);
            save.ShopStockItems = ShopStockUtility.CloneAndMerge(save.ShopStockItems);
            NormalizeClassPassives(save);
            if (save.Version < 7)
            {
                AddNewCharacterSkills(save);
            }
            if (save.Version < 8)
            {
                ReplaceProtagonistDistortionWithSlow(save);
            }
            if (save.Version < 9)
            {
                MoveCharacterSkillsToWeapons(save);
            }
            if (save.Version < 10)
            {
                BackfillCandyStockForClearedChapters(save);
            }
            if (save.Version < 11)
            {
                RemoveLegacyStarterUnits(save);
            }
            if (save.Version < 12)
            {
                AddCharacterSkillsToLearnedLists(save);
            }
            if (save.Version < 13)
            {
                ApplyPermanentPassiveAndShopkeepRework(save);
            }
            if (save.Version < 14)
            {
                RestoreShopkeepPunish(save);
            }
            save.Version = Mathf.Max(CurrentSaveVersion, save.Version);
            return save;
        }

        private static void ApplyPermanentPassiveAndShopkeepRework(CampaignSaveData save)
        {
            foreach (OwnedUnitSaveData unit in save.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>())
            {
                if (unit == null)
                {
                    continue;
                }

                string identity = (!string.IsNullOrWhiteSpace(unit.VisualId) ? unit.VisualId : unit.UnitId)
                    ?.Trim().ToLowerInvariant();
                switch (identity)
                {
                    case "protagonist":
                        AddPassive(unit, "accelerated_movement");
                        break;
                    case "thunder":
                        RemovePassive(unit, "wrath");
                        AddPassive(unit, "crisis_might");
                        break;
                    case "flame":
                        AddPassive(unit, "joy_of_burning");
                        break;
                    case "darkness":
                        AddPassive(unit, "soul_stealer");
                        break;
                    case "shopkeep":
                        unit.SkillIds = (unit.SkillIds ?? Array.Empty<string>())
                            .Where(id => !string.Equals(id, "punish", StringComparison.OrdinalIgnoreCase))
                            .Append("return_to_hell")
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                        AddPassive(unit, "sadist");
                        AddPassive(unit, "hegemony");
                        break;
                    case "nurse":
                        RemovePassive(unit, "compassion");
                        AddPassive(unit, "nurse_compassion");
                        AddPassive(unit, "flight");
                        break;
                }
            }
        }

        private static void RestoreShopkeepPunish(CampaignSaveData save)
        {
            foreach (OwnedUnitSaveData unit in save.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>())
            {
                if (unit == null || !string.Equals(
                    !string.IsNullOrWhiteSpace(unit.VisualId) ? unit.VisualId : unit.UnitId,
                    "shopkeep", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                unit.SkillIds = (unit.SkillIds ?? Array.Empty<string>())
                    .Where(id => !string.Equals(id, "return_to_hell", StringComparison.OrdinalIgnoreCase))
                    .Append("punish")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                AddPassive(unit, "sadist");
                AddPassive(unit, "hegemony");
            }
        }

        private static void AddPassive(OwnedUnitSaveData unit, string passiveId)
        {
            unit.ClassPassiveIds = (unit.ClassPassiveIds ?? Array.Empty<string>())
                .Append(passiveId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static void RemovePassive(OwnedUnitSaveData unit, string passiveId)
        {
            unit.ClassPassiveIds = (unit.ClassPassiveIds ?? Array.Empty<string>())
                .Where(id => !string.Equals(id, passiveId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        private static void AddCharacterSkillsToLearnedLists(CampaignSaveData save)
        {
            foreach (OwnedUnitSaveData unit in save.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>())
            {
                if (unit == null)
                {
                    continue;
                }

                string identity = (!string.IsNullOrWhiteSpace(unit.VisualId) ? unit.VisualId : unit.UnitId)
                    ?.Trim().ToLowerInvariant();
                string skillId = identity switch
                {
                    "protagonist" => "slow",
                    "thunder" => "storm_surge",
                    "flame" => "clang",
                    "darkness" => "anathema",
                    "shopkeep" => "punish",
                    "nurse" => "bash",
                    _ => null
                };
                if (skillId == null)
                {
                    continue;
                }

                unit.SkillIds = (unit.SkillIds ?? Array.Empty<string>())
                    .Append(skillId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }

        private static void RemoveLegacyStarterUnits(CampaignSaveData save)
        {
            HashSet<string> removedUnitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "pip",
                "bastion",
                "nova",
                "gooby"
            };

            save.OwnedUnits = (save.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>())
                .Where(unit => unit != null && !removedUnitIds.Contains(unit.UnitId?.Trim() ?? string.Empty))
                .ToArray();
            save.DeploymentRosterUnitIds = (save.DeploymentRosterUnitIds ?? Array.Empty<string>())
                .Where(unitId => !removedUnitIds.Contains(unitId?.Trim() ?? string.Empty))
                .ToArray();
        }

        private static void BackfillCandyStockForClearedChapters(CampaignSaveData save)
        {
            string[] candyItemIds =
            {
                "strength_candy",
                "magic_candy",
                "defense_candy",
                "speed_candy",
                "luck_candy",
                "movement_candy"
            };

            int clearedRestockCount = 0;
            if (CampaignProgressUtility.IsChapterCleared(save, 1f)) clearedRestockCount++;
            if (CampaignProgressUtility.IsChapterCleared(save, 2f)) clearedRestockCount++;
            if (clearedRestockCount == 0)
            {
                return;
            }

            ShopStockUtility.AddStock(save, candyItemIds.Select(itemId => new ShopStockEntryData
            {
                ItemId = itemId,
                Quantity = clearedRestockCount
            }));
        }

        private static void AddNewCharacterSkills(CampaignSaveData save)
        {
            foreach (OwnedUnitSaveData unit in save.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>())
            {
                if (unit == null) continue;
                string identity = !string.IsNullOrWhiteSpace(unit.VisualId) ? unit.VisualId : unit.UnitId;
                string skillId = identity?.Trim().ToLowerInvariant() switch
                {
                    "protagonist" => "slow",
                    "thunder" => "storm_surge",
                    "flame" => "clang",
                    "darkness" => "anathema",
                    _ => null
                };
                if (skillId == null || (unit.SkillIds ?? Array.Empty<string>())
                    .Any(id => string.Equals(id, skillId, StringComparison.OrdinalIgnoreCase))) continue;

                unit.SkillIds = (unit.SkillIds ?? Array.Empty<string>()).Append(skillId).ToArray();
            }
        }

        private static void ReplaceProtagonistDistortionWithSlow(CampaignSaveData save)
        {
            foreach (OwnedUnitSaveData unit in save.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>())
            {
                if (unit == null) continue;
                string identity = !string.IsNullOrWhiteSpace(unit.VisualId) ? unit.VisualId : unit.UnitId;
                if (!string.Equals(identity?.Trim(), "protagonist", StringComparison.OrdinalIgnoreCase)) continue;

                unit.SkillIds = (unit.SkillIds ?? Array.Empty<string>())
                    .Where(id => !string.Equals(id, "distortion", StringComparison.OrdinalIgnoreCase))
                    .Append("slow")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }

        private static void MoveCharacterSkillsToWeapons(CampaignSaveData save)
        {
            foreach (OwnedUnitSaveData unit in save.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>())
            {
                if (unit == null) continue;
                string identity = (!string.IsNullOrWhiteSpace(unit.VisualId) ? unit.VisualId : unit.UnitId)
                    ?.Trim().ToLowerInvariant();
                string weaponSkillId = identity switch
                {
                    "protagonist" => "slow",
                    "thunder" => "storm_surge",
                    "flame" => "clang",
                    "darkness" => "anathema",
                    _ => null
                };
                if (weaponSkillId != null)
                {
                    unit.SkillIds = (unit.SkillIds ?? Array.Empty<string>())
                        .Where(id => !string.Equals(id, weaponSkillId, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                }

                if (identity is "protagonist" or "thunder" or "flame" or "darkness"
                    or "bastion" or "gooby" or "nova" or "pip")
                {
                    unit.WeaponProficiencyIds = new[] { "Melee", "Ranged", "Magic" };
                }
            }
        }

        private static void NormalizeClassPassives(CampaignSaveData save)
        {
            if (save?.OwnedUnits == null)
            {
                return;
            }

            BuiltInItemCatalog.EnsureRegistered();
            PassiveEffects.EnsureRegistered();

            foreach (OwnedUnitSaveData unit in save.OwnedUnits)
            {
                if (unit == null)
                {
                    continue;
                }

                unit.ClassPassiveIds = NormalizePassiveIds(unit.ClassPassiveIds);
            }
        }

        private static string[] NormalizePassiveIds(IEnumerable<string> passiveIds)
        {
            return ClonePassiveIds(passiveIds);
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
                    RemainingCharges = remainingCharges,
                    IsDroppable = entry.IsDroppable
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

        private static IEnumerable<string> GetWeaponProficiencyIds(WeaponProficiency weaponProficiencies)
        {
            WeaponProficiency[] supportedTypes =
            {
                WeaponProficiency.Melee,
                WeaponProficiency.Ranged,
                WeaponProficiency.Magic
            };

            foreach (WeaponProficiency supportedType in supportedTypes)
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
                Inventory = CloneStorageEntries(unit.Inventory),
                SkillIds = unit.SkillIds?.ToArray() ?? Array.Empty<string>(),
                ClassPassiveIds = unit.ClassPassiveIds?.ToArray() ?? Array.Empty<string>()
            };
        }

        private static SavedInventoryEntryData[] CloneStorageEntries(IEnumerable<SavedInventoryEntryData> entries)
        {
            return entries?
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.ItemId))
                .Select(entry => new SavedInventoryEntryData
                {
                    ItemId = entry.ItemId,
                    RemainingCharges = entry.RemainingCharges,
                    IsDroppable = entry.IsDroppable
                })
                .ToArray()
                ?? Array.Empty<SavedInventoryEntryData>();
        }

        private static string[] ClonePassiveIds(IEnumerable<string> passiveIds)
        {
            return passiveIds?
                .Where(passiveId => !string.IsNullOrWhiteSpace(passiveId))
                .Select(passiveId => passiveId.Trim())
                .ToArray()
                ?? Array.Empty<string>();
        }
    }
}

