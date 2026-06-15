using System;
using System.Collections.Generic;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Passives;
using Windy.Srpg.Game.Skills;
using UnityEngine;
using UnityEngine.Serialization;

namespace Windy.Srpg.Game.Units
{
    [Serializable]
    public struct UnitStatBlock
    {
        public int HitPoints;
        public int ManaPoints;
        public float MovementPoints;
        public int Strength;
        public int Defense;
        public int Magic;
        public int Resistance;
        public int Speed;
        public int Luck;
    }

    [Serializable]
    public struct UnitGrowthRates
    {
        public int Strength;
        public int Magic;
        public int Defense;
        public int Resistance;
        public int Speed;
        public int Luck;
    }

    [Serializable]
    public struct UnitSpriteLayoutSettings
    {
        public Vector2 TargetSize;
        public float OffsetX;
        public float OffsetY;

        [HideInInspector, FormerlySerializedAs("SizeReference")]
        public int LegacySizeReference;
        [HideInInspector, FormerlySerializedAs("ScaleMode")]
        public int LegacyScaleMode;
        [HideInInspector, FormerlySerializedAs("FallbackWorldSize")]
        public Vector2 LegacyFallbackWorldSize;
        [HideInInspector, FormerlySerializedAs("ScaleMultiplier")]
        public float LegacyScaleMultiplier;
        [HideInInspector, FormerlySerializedAs("MinScaleFactor")]
        public float LegacyMinScaleFactor;
        [HideInInspector, FormerlySerializedAs("MaxScaleFactor")]
        public float LegacyMaxScaleFactor;
        [HideInInspector, FormerlySerializedAs("VerticalAnchor")]
        public int LegacyVerticalAnchor;
        [HideInInspector, FormerlySerializedAs("LocalOffset")]
        public Vector2 LegacyLocalOffset;

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

    [Serializable]
    public class UnitPresetOverride
    {
        public bool OverrideUnitName;
        public string UnitNameOverride;
        public bool OverrideLevel;
        public int FinalLevel = 1;
        public bool OverrideMovementPoints;
        public float FinalMovementPoints = 5f;
        public UnitStatBlock StatOffsets;
        public SecondaryStatModifiers SecondaryStatOffsets;
        public List<StartingInventoryItem> ExtraInventory = new List<StartingInventoryItem>();
        public List<StartingSkillEntry> ExtraSkills = new List<StartingSkillEntry>();
        public List<StartingPassiveEntry> ExtraUniquePassives = new List<StartingPassiveEntry>();
        public List<StartingPassiveEntry> ExtraEquipPassives = new List<StartingPassiveEntry>();
    }

    [CreateAssetMenu(fileName = "UnitPreset", menuName = "TBS/Units/Unit Preset")]
    public class UnitPreset : ScriptableObject
    {
        public string PresetId = "unit_preset";
        public string UnitName = "Enemy";
        public Sprite UnitSprite;
        public UnitSpriteLayoutSettings SpriteLayout;
        public int BaseLevel = 1;
        public WeaponType WeaponProficiencies = WeaponType.Sword | WeaponType.Lance | WeaponType.Blunt | WeaponType.Ranged | WeaponType.Magic;
        public UnitStatBlock BaseStats;
        public UnitGrowthRates GrowthRates;
        public List<StartingInventoryItem> StartingInventory = new List<StartingInventoryItem>();
        public List<StartingSkillEntry> StartingSkills = new List<StartingSkillEntry>();
        public List<StartingPassiveEntry> StartingUniquePassives = new List<StartingPassiveEntry>();
        public List<StartingPassiveEntry> StartingEquipPassives = new List<StartingPassiveEntry>();

        // Preserves movement values from older presets that stored this outside BaseStats.
        [FormerlySerializedAs("BaseMovementPoints")]
        [HideInInspector]
        public float LegacyBaseMovementPoints = 5f;

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

            if (!Mathf.Approximately(SpriteLayout.LegacyLocalOffset.x, 0f) ||
                !Mathf.Approximately(SpriteLayout.LegacyLocalOffset.y, 0f))
            {
                if (Mathf.Approximately(SpriteLayout.OffsetX, 0f))
                {
                    SpriteLayout.OffsetX = SpriteLayout.LegacyLocalOffset.x;
                    changed = true;
                }

                if (Mathf.Approximately(SpriteLayout.OffsetY, 0f))
                {
                    SpriteLayout.OffsetY = SpriteLayout.LegacyLocalOffset.y;
                    changed = true;
                }

                SpriteLayout.LegacyLocalOffset = Vector2.zero;
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

            CustomUnit[] units = Resources.FindObjectsOfTypeAll<CustomUnit>();
            foreach (CustomUnit unit in units)
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
