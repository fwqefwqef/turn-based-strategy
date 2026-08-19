using System;
using System.Collections.Generic;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Passives;
using Windy.Srpg.Game.Skills;
using UnityEngine;

namespace Windy.Srpg.Game.Units
{
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

