using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Passives;
using Windy.Srpg.Game.Skills;
using UnityEngine;

namespace Windy.Srpg.Game.Units
{
    [Serializable]
    public sealed class UnitPresetOverride
    {
        public bool Enabled = true;
        public UnitStatBlock StatBonuses;
        public List<StartingInventoryItem> AdditionalInventory = new List<StartingInventoryItem>();
        public List<StartingSkillEntry> AdditionalSkills = new List<StartingSkillEntry>();
        public List<StartingPassiveEntry> AdditionalPassives = new List<StartingPassiveEntry>();

        public UnitStatBlock ResolveStats(UnitStatBlock basis)
        {
            if (!Enabled) return basis;
            basis.HitPoints += StatBonuses.HitPoints;
            basis.ManaPoints += StatBonuses.ManaPoints;
            basis.MovementPoints += StatBonuses.MovementPoints;
            basis.Strength += StatBonuses.Strength;
            basis.Magic += StatBonuses.Magic;
            basis.Defense += StatBonuses.Defense;
            basis.Speed += StatBonuses.Speed;
            basis.Luck += StatBonuses.Luck;
            return basis;
        }

        public List<StartingInventoryItem> ResolveInventory(IEnumerable<StartingInventoryItem> inherited)
        {
            var result = new List<StartingInventoryItem>(inherited ?? Enumerable.Empty<StartingInventoryItem>());
            if (!Enabled) return result;
            foreach (var item in AdditionalInventory ?? Enumerable.Empty<StartingInventoryItem>())
            {
                if (string.IsNullOrWhiteSpace(item.ItemId)) continue;
                var resolved = item;
                resolved.ItemId = resolved.ItemId.Trim();
                if (!resolved.ChargesInitialized)
                {
                    resolved.InitialCharges = -1;
                    resolved.ChargesInitialized = true;
                }
                result.Add(resolved);
            }
            return result;
        }

        public List<StartingSkillEntry> ResolveSkills(IEnumerable<StartingSkillEntry> inherited)
        {
            var entries = (inherited ?? Enumerable.Empty<StartingSkillEntry>())
                .Concat(Enabled ? AdditionalSkills ?? Enumerable.Empty<StartingSkillEntry>() : Enumerable.Empty<StartingSkillEntry>());
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return entries.Where(e => !string.IsNullOrWhiteSpace(e.SkillId) && ids.Add(e.SkillId.Trim()))
                .Select(e => new StartingSkillEntry { SkillId = e.SkillId.Trim() }).ToList();
        }

        public List<StartingPassiveEntry> ResolvePassives(IEnumerable<StartingPassiveEntry> inherited)
        {
            var entries = (inherited ?? Enumerable.Empty<StartingPassiveEntry>())
                .Concat(Enabled ? AdditionalPassives ?? Enumerable.Empty<StartingPassiveEntry>() : Enumerable.Empty<StartingPassiveEntry>());
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return entries.Where(e => !string.IsNullOrWhiteSpace(e.PassiveId) && ids.Add(e.PassiveId.Trim()))
                .Select(e => new StartingPassiveEntry { PassiveId = e.PassiveId.Trim() }).ToList();
        }
    }

    public enum UnitActionAiMode
    {
        Attack,
        Heal
    }

    public enum UnitMovementAiMode
    {
        Move,
        Wait,
        WaitGroup,
        NotMove
    }

    [Serializable]
    public struct UnitStatBlock
    {
        public int HitPoints;
        public int ManaPoints;
        public int MovementPoints;
        public int Strength;
        public int Defense;
        public int Magic;
        public int Speed;
        public int Luck;
    }

    [Serializable]
    public struct UnitGrowthRates
    {
        public int Strength;
        public int Magic;
        public int Defense;
        public int Speed;
        public int Luck;
    }

    [Serializable]
    public struct UnitSpriteLayoutSettings
    {
        public Vector2 TargetSize;
        public float OffsetX;
        public float OffsetY;

        public static UnitSpriteLayoutSettings CreateDefault()
        {
            return new UnitSpriteLayoutSettings
            {
                TargetSize = new Vector2(1.2f, 1.2f),
                OffsetX = 0f,
                OffsetY = 0f
            };
        }

        public Vector2 ResolvedTargetSize =>
            TargetSize.x > 0f && TargetSize.y > 0f ? TargetSize : new Vector2(1.2f, 1.2f);
    }

    [CreateAssetMenu(fileName = "UnitPreset", menuName = "TBS/Units/Unit Preset")]
    public class UnitPreset : ScriptableObject
    {
        public string PresetId = "unit_preset";
        public string UnitName = "Enemy";
        public Sprite UnitSprite;
        public Sprite FaceSprite;
        public UnitSpriteLayoutSettings SpriteLayout;
        public UnitActionAiMode ActionAiMode = UnitActionAiMode.Attack;
        public UnitMovementAiMode MovementAiMode = UnitMovementAiMode.Move;
        public int WaitGroupId = 0;
        public int BaseLevel = 1;
        public WeaponType WeaponProficiencies = WeaponType.Sword | WeaponType.Lance | WeaponType.Blunt | WeaponType.Ranged | WeaponType.Magic;
        public UnitStatBlock BaseStats;
        public UnitGrowthRates GrowthRates;
        public List<StartingInventoryItem> StartingInventory = new List<StartingInventoryItem>();
        public List<StartingSkillEntry> StartingSkills = new List<StartingSkillEntry>();
        public List<StartingPassiveEntry> StartingClassPassives = new List<StartingPassiveEntry>();

        private void OnValidate()
        {
            bool inventoryWasInitialized = InitializeStartingInventoryChargeDefaults();
            bool layoutWasInitialized = InitializeSpriteLayoutDefaultsIfUnset();
            RefreshLinkedUnitsInEditor();

#if UNITY_EDITOR
            if (layoutWasInitialized || inventoryWasInitialized)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
        }

        private bool InitializeStartingInventoryChargeDefaults()
        {
            if (StartingInventory == null || StartingInventory.Count == 0)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < StartingInventory.Count; i++)
            {
                StartingInventoryItem entry = StartingInventory[i];
                if (entry.ChargesInitialized)
                {
                    continue;
                }

                entry.InitialCharges = -1;
                entry.ChargesInitialized = true;
                StartingInventory[i] = entry;
                changed = true;
            }

            return changed;
        }

        private bool InitializeSpriteLayoutDefaultsIfUnset()
        {
            bool changed = false;
            if (SpriteLayout.TargetSize.x <= 0f || SpriteLayout.TargetSize.y <= 0f)
            {
                SpriteLayout.TargetSize = new Vector2(1.2f, 1.2f);
                changed = true;
            }

            return changed;
        }

        private void RefreshLinkedUnitsInEditor()
        {
            if (Application.isPlaying)
            {
                return;
            }

            Unit[] units = Resources.FindObjectsOfTypeAll<Unit>();
            foreach (Unit unit in units)
            {
                if (unit == null || !unit.gameObject.scene.IsValid())
                {
                    continue;
                }

                unit.RefreshPresetFromAssetInEditor(this);
            }
        }
    }
}

