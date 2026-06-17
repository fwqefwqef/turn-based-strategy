using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Windy.Srpg.Game.Inventory
{
    public enum ItemType
    {
        Weapon,
        Accessory,
        Consumable
    }

    public enum DamageType
    {
        Physical,
        Magic
    }

    public enum ConsumableTargetType
    {
        Self
    }

    [Flags]
    public enum WeaponType
    {
        None = 0,
        Sword = 1 << 0,
        Lance = 1 << 1,
        Ranged = 1 << 2,
        Blunt = 1 << 3,
        // Magic is intended for magical implements such as tomes and staves.
        Magic = 1 << 4
    }

    [Serializable]
    public struct PrimaryStatModifiers
    {
        public int MaxHitPoints;
        public int MaxManaPoints;
        public int Attack;
        public int Defense;
        public int Magic;
        public int Resistance;
        public int Speed;
        public int Luck;

        public static PrimaryStatModifiers operator +(PrimaryStatModifiers left, PrimaryStatModifiers right)
        {
            return new PrimaryStatModifiers
            {
                MaxHitPoints = left.MaxHitPoints + right.MaxHitPoints,
                MaxManaPoints = left.MaxManaPoints + right.MaxManaPoints,
                Attack = left.Attack + right.Attack,
                Defense = left.Defense + right.Defense,
                Magic = left.Magic + right.Magic,
                Resistance = left.Resistance + right.Resistance,
                Speed = left.Speed + right.Speed,
                Luck = left.Luck + right.Luck
            };
        }
    }

    [Serializable]
    public struct SecondaryStatModifiers
    {
        public int Accuracy;
        public int Crit;
        public int CritAvoid;
        public int AttackRange;

        public static SecondaryStatModifiers operator +(SecondaryStatModifiers left, SecondaryStatModifiers right)
        {
            return new SecondaryStatModifiers
            {
                Accuracy = left.Accuracy + right.Accuracy,
                Crit = left.Crit + right.Crit,
                CritAvoid = left.CritAvoid + right.CritAvoid,
                AttackRange = left.AttackRange + right.AttackRange
            };
        }
    }

    [Serializable]
    public abstract class ItemData
    {
        public string Id;
        public string Name = "item_name";
        [TextArea]
        public string Description = "item_desc";
        public int Value = 100;

        public abstract ItemType ItemType { get; }
    }

    [Serializable]
    public class WeaponData : ItemData
    {
        public WeaponType WeaponType = WeaponType.Sword;
        public DamageType DamageType = DamageType.Physical;
        public int Might = 0;
        public int MinRange = 1;
        public int MaxRange = 1;
        public int Accuracy = 100;
        public int Crit = 0;
        public int NumHits = 1;
        public bool CanPursuitAttack = true;
        public bool CanCounterAttack = true;
        public bool PreventsCounterattack = false;
        public PrimaryStatModifiers StatModifiers;
        [FormerlySerializedAs("PassiveId")]
        public string EffectId;
        public string[] GrantedSkillIds = Array.Empty<string>();

        public override ItemType ItemType => ItemType.Weapon;
    }

    [Serializable]
    public class AccessoryData : ItemData
    {
        public PrimaryStatModifiers StatModifiers;
        public SecondaryStatModifiers SecondaryStatModifiers;
        [FormerlySerializedAs("PassiveId")]
        public string EffectId;
        public string[] GrantedSkillIds = Array.Empty<string>();

        public override ItemType ItemType => ItemType.Accessory;
    }

    [Serializable]
    public class ConsumableData : ItemData
    {
        public int Charges = 3;
        public string EffectId;
        public ConsumableTargetType TargetType = ConsumableTargetType.Self;

        public override ItemType ItemType => ItemType.Consumable;
    }

    [Serializable]
    public struct StartingInventoryItem
    {
        public string ItemId;
        public int InitialCharges;

        // Legacy toggle retained only so older assets deserialize cleanly.
        [FormerlySerializedAs("OverrideInitialCharges")]
        [HideInInspector]
        public bool LegacyOverrideInitialCharges;

        [HideInInspector]
        public bool ChargesInitialized;

        // -1 means "use the item's default charges". 0 means "empty" and the item will not be kept.
        public bool HasInitialChargesOverride => InitialCharges >= 0;
    }
}



