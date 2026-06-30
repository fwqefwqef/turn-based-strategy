using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;
using UnityEngine;

namespace Windy.Srpg.Game.Inventory
{
    [Serializable]
    public class Item
    {
        [SerializeField]
        private string itemId;
        [SerializeField]
        private int remainingCharges;

        public string ItemId => itemId;
        public int RemainingCharges => remainingCharges;
        public ItemData Data => ItemRegistry.Get(itemId);
        public WeaponData Weapon => Data as WeaponData;
        public AccessoryData Accessory => Data as AccessoryData;
        public ConsumableData Consumable => Data as ConsumableData;
        public bool HasInfiniteCharges => remainingCharges < 0;
        public bool HasChargesRemaining => HasInfiniteCharges || remainingCharges > 0;

        public Item()
        {
        }

        public Item(string itemId, int remainingCharges)
        {
            this.itemId = itemId;
            this.remainingCharges = remainingCharges;
        }

        public Item(ItemData data, int? remainingChargesOverride = null)
        {
            if (data == null)
            {
                return;
            }

            itemId = data.Id;
            if (data is ConsumableData consumable)
            {
                remainingCharges = remainingChargesOverride ?? consumable.Charges;
            }
            else
            {
                remainingCharges = -1;
            }
        }

        public void SetRemainingCharges(int value)
        {
            remainingCharges = value;
        }

        public bool ConsumeCharge()
        {
            if (HasInfiniteCharges)
            {
                return true;
            }

            if (remainingCharges <= 0)
            {
                return false;
            }

            remainingCharges--;
            return true;
        }
    }

    public sealed class UnitInventory
    {
        public const int MaxSlots = 8;

        private readonly Unit owner;
        private readonly List<Item> entries = new List<Item>();

        public IReadOnlyList<Item> Entries => entries;
        public int Count => entries.Count;
        public bool IsFull => entries.Count >= MaxSlots;
        public Item EquippedWeaponEntry { get; private set; }
        public Item EquippedAccessoryEntry { get; private set; }
        public WeaponData EquippedWeapon => EquippedWeaponEntry?.Weapon;
        public AccessoryData EquippedAccessory => EquippedAccessoryEntry?.Accessory;

        public UnitInventory(Unit owner)
        {
            this.owner = owner;
        }

        public void LoadStartingItems(IEnumerable<StartingInventoryItem> startingItems)
        {
            entries.Clear();
            EquippedWeaponEntry = null;
            EquippedAccessoryEntry = null;

            if (startingItems == null)
            {
                NotifyInventoryChanged();
                return;
            }

            foreach (var item in startingItems)
            {
                AddItemById(item.ItemId, item.HasInitialChargesOverride ? (int?)item.InitialCharges : null, notifyOwner: false);
            }

            AutoEquipIfNeeded();
            NotifyInventoryChanged();
        }

        public void LoadExactItems(IEnumerable<Item> items)
        {
            entries.Clear();
            EquippedWeaponEntry = null;
            EquippedAccessoryEntry = null;

            if (items == null)
            {
                NotifyInventoryChanged();
                return;
            }

            foreach (Item item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
                {
                    continue;
                }

                if (!ItemRegistry.TryGet(item.ItemId, out ItemData data))
                {
                    Debug.LogWarning($"UnitInventory: Item id '{item.ItemId}' is not registered.");
                    continue;
                }

                if (data is ConsumableData && item.RemainingCharges == 0)
                {
                    continue;
                }

                if (IsFull)
                {
                    break;
                }

                entries.Add(new Item(item.ItemId, item.RemainingCharges));
            }

            AutoEquipIfNeeded();
            NotifyInventoryChanged();
        }

        public Item AddItem(ItemData data, int? remainingChargesOverride = null, bool notifyOwner = true)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.Id) || IsFull)
            {
                return null;
            }

            if (data is ConsumableData && remainingChargesOverride.HasValue && remainingChargesOverride.Value == 0)
            {
                return null;
            }

            ItemRegistry.Register(data);
            var entry = new Item(data, remainingChargesOverride);
            entries.Add(entry);
            AutoEquipIfNeeded();

            if (notifyOwner)
            {
                NotifyInventoryChanged();
            }

            return entry;
        }

        public Item AddItemById(string itemId, int? remainingChargesOverride = null, bool notifyOwner = true)
        {
            if (!ItemRegistry.TryGet(itemId, out var data))
            {
                Debug.LogWarning($"UnitInventory: Item id '{itemId}' is not registered.");
                return null;
            }

            return AddItem(data, remainingChargesOverride, notifyOwner);
        }

        public bool RemoveItem(Item entry)
        {
            if (entry == null || !entries.Remove(entry))
            {
                return false;
            }

            if (EquippedWeaponEntry == entry)
            {
                EquippedWeaponEntry = null;
            }

            if (EquippedAccessoryEntry == entry)
            {
                EquippedAccessoryEntry = null;
            }

            AutoEquipIfNeeded();
            NotifyInventoryChanged();
            return true;
        }

        public bool EquipWeapon(Item entry)
        {
            if (entry == null || !entries.Contains(entry) || entry.Weapon == null || owner == null || !owner.CanEquipWeapon(entry.Weapon))
            {
                return false;
            }

            EquippedWeaponEntry = entry;
            NotifyInventoryChanged();
            return true;
        }

        public bool EquipAccessory(Item entry)
        {
            if (entry == null || !entries.Contains(entry) || entry.Accessory == null)
            {
                return false;
            }

            EquippedAccessoryEntry = entry;
            NotifyInventoryChanged();
            return true;
        }

        public bool UseConsumable(Item entry, Unit target = null)
        {
            if (!CanUseConsumable(entry, target))
            {
                return false;
            }

            var consumable = entry.Consumable;
            target ??= owner;

            if (!ConsumableEffectRegistry.TryCreate(consumable.EffectId, out var effect))
            {
                Debug.LogWarning($"UnitInventory: Consumable effect '{consumable.EffectId}' is not registered.");
                return false;
            }

            if (!effect.CanUse(owner, target))
            {
                return false;
            }

            string itemName = !string.IsNullOrWhiteSpace(entry?.Data?.Name) ? entry.Data.Name : entry?.ItemId ?? "Item";
            BattleLog.Log("Action", $"{DescribeUnit(owner)} uses {itemName} on {DescribeUnit(target)}.");
            effect.Use(owner, target);
            if (!entry.ConsumeCharge())
            {
                return false;
            }

            if (!entry.HasInfiniteCharges && entry.RemainingCharges <= 0)
            {
                RemoveItem(entry);
                return true;
            }

            NotifyInventoryChanged();
            return true;
        }

        public bool CanUseConsumable(Item entry, Unit target = null)
        {
            if (entry == null || !entries.Contains(entry))
            {
                return false;
            }

            var consumable = entry.Consumable;
            if (consumable == null)
            {
                return false;
            }

            if (!entry.HasChargesRemaining || owner == null || !owner.CanStartActionThisTurn)
            {
                return false;
            }

            target ??= owner;
            if (consumable.TargetType == ConsumableTargetType.Self && target != owner)
            {
                return false;
            }

            if (!ConsumableEffectRegistry.TryCreate(consumable.EffectId, out var effect))
            {
                return false;
            }

            return effect.CanUse(owner, target);
        }

        private static string DescribeUnit(Unit unit)
        {
            if (unit == null)
            {
                return "None";
            }

            string side = unit.PlayerNumber switch
            {
                0 => "Friendly",
                1 => "Enemy",
                _ => $"Player {unit.PlayerNumber}"
            };

            string nameLabel = string.IsNullOrWhiteSpace(unit.unitName) ? unit.name : unit.unitName;
            return $"{side} {nameLabel}";
        }

        public void Clear()
        {
            entries.Clear();
            EquippedWeaponEntry = null;
            EquippedAccessoryEntry = null;
            NotifyInventoryChanged();
        }

        public bool HasAnyWeaponEntries()
        {
            return entries.Any(entry => entry?.Weapon != null && owner != null && owner.CanEquipWeapon(entry.Weapon));
        }

        public bool TransferEntryTo(Item entry, UnitInventory other)
        {
            if (entry == null || other == null || other == this || !entries.Contains(entry) || other.IsFull)
            {
                return false;
            }

            entries.Remove(entry);
            if (EquippedWeaponEntry == entry)
            {
                EquippedWeaponEntry = null;
            }

            if (EquippedAccessoryEntry == entry)
            {
                EquippedAccessoryEntry = null;
            }

            other.entries.Add(entry);

            AutoEquipIfNeeded();
            other.AutoEquipIfNeeded();
            NotifyInventoryChanged();
            other.NotifyInventoryChanged();
            return true;
        }

        public bool SwapEntriesWith(Item ownEntry, UnitInventory other, Item otherEntry)
        {
            if (ownEntry == null || otherEntry == null || other == null || other == this)
            {
                return false;
            }

            int ownIndex = entries.IndexOf(ownEntry);
            int otherIndex = other.entries.IndexOf(otherEntry);
            if (ownIndex < 0 || otherIndex < 0)
            {
                return false;
            }

            entries[ownIndex] = otherEntry;
            other.entries[otherIndex] = ownEntry;

            if (EquippedWeaponEntry == ownEntry)
            {
                EquippedWeaponEntry = null;
            }

            if (EquippedAccessoryEntry == ownEntry)
            {
                EquippedAccessoryEntry = null;
            }

            if (other.EquippedWeaponEntry == otherEntry)
            {
                other.EquippedWeaponEntry = null;
            }

            if (other.EquippedAccessoryEntry == otherEntry)
            {
                other.EquippedAccessoryEntry = null;
            }

            AutoEquipIfNeeded();
            other.AutoEquipIfNeeded();
            NotifyInventoryChanged();
            other.NotifyInventoryChanged();
            return true;
        }

        public IEnumerable<IUnitPassive> CreateActiveEquipmentEffects()
        {
            foreach (var effectId in GetActiveEquipmentEffectIds())
            {
                if (UnitPassiveRegistry.TryCreate(effectId, out var passive))
                {
                    yield return passive;
                }
            }
        }

        private IEnumerable<string> GetActiveEquipmentEffectIds()
        {
            if (!string.IsNullOrWhiteSpace(EquippedWeapon?.EffectId))
            {
                yield return EquippedWeapon.EffectId;
            }

            if (!string.IsNullOrWhiteSpace(EquippedAccessory?.EffectId))
            {
                yield return EquippedAccessory.EffectId;
            }
        }

        private void AutoEquipIfNeeded()
        {
            if (EquippedWeaponEntry == null
                || !entries.Contains(EquippedWeaponEntry)
                || EquippedWeaponEntry.Weapon == null
                || owner == null
                || !owner.CanEquipWeapon(EquippedWeaponEntry.Weapon))
            {
                EquippedWeaponEntry = entries.FirstOrDefault(entry => entry?.Weapon != null && owner != null && owner.CanEquipWeapon(entry.Weapon));
            }

            if (EquippedAccessoryEntry == null || !entries.Contains(EquippedAccessoryEntry) || EquippedAccessoryEntry.Accessory == null)
            {
                EquippedAccessoryEntry = entries.FirstOrDefault(entry => entry?.Accessory != null);
            }
        }

        private void NotifyInventoryChanged()
        {
            owner?.OnInventoryChanged();
        }
    }
}



