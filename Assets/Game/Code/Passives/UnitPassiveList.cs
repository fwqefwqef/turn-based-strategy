using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Units;
using UnityEngine;

namespace Windy.Srpg.Game.Passives
{
    [Serializable]
    public class Passive
    {
        [SerializeField]
        private string passiveId;

        [NonSerialized]
        private IP_PassiveEffect effectInstance;

        public string PassiveId => passiveId;
        public PassiveData Data => PassiveRegistry.Get(passiveId);
        public IP_PassiveEffect EffectInstance => effectInstance;

        public Passive()
        {
        }

        public Passive(string passiveId)
        {
            this.passiveId = passiveId;
            TryCreateEffectInstance();
        }

        public Passive(PassiveData data)
        {
            if (data == null)
            {
                return;
            }

            passiveId = data.Id;
            TryCreateEffectInstance();
        }

        private void TryCreateEffectInstance()
        {
            if (Data == null)
            {
                effectInstance = null;
                return;
            }

            PassiveEffectRegistry.TryCreate(Data.EffectId, out effectInstance);
        }
    }

    public sealed class UnitPassiveList
    {
        public const int DefaultEquipPassiveSlotLimit = 4;
        public const int DefaultEquipPassiveCostLimit = 12;

        private readonly Unit owner;
        private readonly List<Passive> uniqueEntries = new List<Passive>();
        private readonly List<Passive> equippedEntries = new List<Passive>();
        private readonly List<Passive> combinedEntries = new List<Passive>();
        private bool combinedEntriesDirty = true;

        public IReadOnlyList<Passive> UniqueEntries => uniqueEntries;
        public IReadOnlyList<Passive> EquippedEntries => equippedEntries;
        public IReadOnlyList<Passive> Entries
        {
            get
            {
                RebuildCombinedEntriesIfNeeded();
                return combinedEntries;
            }
        }

        public int EquipPassiveSlotLimit => DefaultEquipPassiveSlotLimit;
        public int EquipPassiveCostLimit => DefaultEquipPassiveCostLimit;
        public int EquippedPassiveCount => equippedEntries.Count;
        public int EquippedPassiveCost => equippedEntries.Sum(entry => entry?.Data?.Cost ?? 0);

        public UnitPassiveList(Unit owner)
        {
            this.owner = owner;
        }

        public void LoadStartingPassives(IEnumerable<StartingPassiveEntry> uniquePassives, IEnumerable<StartingPassiveEntry> equipPassives)
        {
            ClearInternal();

            if (uniquePassives != null)
            {
                foreach (StartingPassiveEntry entry in uniquePassives)
                {
                    AddPassiveById(entry.PassiveId, PassiveListKind.Unique, notifyOwner: false);
                }
            }

            if (equipPassives != null)
            {
                foreach (StartingPassiveEntry entry in equipPassives)
                {
                    AddPassiveById(entry.PassiveId, PassiveListKind.Equip, notifyOwner: false);
                }
            }

            NotifyOwnerChanged();
        }

        public Passive AddPassive(PassiveData data, bool notifyOwner = true)
        {
            return AddPassive(data, PassiveListKind.Unique, notifyOwner);
        }

        public Passive AddEquipPassive(PassiveData data, bool notifyOwner = true)
        {
            return AddPassive(data, PassiveListKind.Equip, notifyOwner);
        }

        public Passive AddPassive(PassiveData data, PassiveListKind listKind, bool notifyOwner = true)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.Id))
            {
                return null;
            }

            PassiveRegistry.Register(data);

            if (ContainsPassiveId(data.Id))
            {
                return Entries.FirstOrDefault(entry => string.Equals(entry.PassiveId, data.Id, StringComparison.OrdinalIgnoreCase));
            }

            if (listKind == PassiveListKind.Equip && !CanEquipPassive(data))
            {
                return null;
            }

            Passive entry = new Passive(data);
            GetTargetList(listKind).Add(entry);
            entry.EffectInstance?.OnApply(owner, entry);
            MarkEntriesDirty();

            if (notifyOwner)
            {
                NotifyOwnerChanged();
            }

            return entry;
        }

        public Passive AddPassiveById(string passiveId, bool notifyOwner = true)
        {
            return AddPassiveById(passiveId, PassiveListKind.Unique, notifyOwner);
        }

        public Passive AddEquipPassiveById(string passiveId, bool notifyOwner = true)
        {
            return AddPassiveById(passiveId, PassiveListKind.Equip, notifyOwner);
        }

        public Passive AddPassiveById(string passiveId, PassiveListKind listKind, bool notifyOwner = true)
        {
            if (string.IsNullOrWhiteSpace(passiveId))
            {
                return null;
            }

            if (!PassiveRegistry.TryGet(passiveId, out PassiveData data))
            {
                Debug.LogWarning($"UnitPassiveList: Passive id '{passiveId}' is not registered.");
                return null;
            }

            return AddPassive(data, listKind, notifyOwner);
        }

        public bool RemovePassive(Passive entry, bool notifyOwner = true)
        {
            if (entry == null)
            {
                return false;
            }

            bool removed = uniqueEntries.Remove(entry) || equippedEntries.Remove(entry);
            if (!removed)
            {
                return false;
            }

            entry.EffectInstance?.OnRemove(owner, entry);
            MarkEntriesDirty();
            if (notifyOwner)
            {
                NotifyOwnerChanged();
            }

            return true;
        }

        public void OnTurnStart()
        {
            foreach (Passive entry in Entries)
            {
                entry?.EffectInstance?.OnTurnStart(owner, entry);
            }
        }

        public void OnTurnEnd()
        {
            foreach (Passive entry in Entries)
            {
                entry?.EffectInstance?.OnTurnEnd(owner, entry);
            }
        }

        public IEnumerable<IP_PassiveEffect> GetActiveEffects()
        {
            return Entries
                .Select(entry => entry?.EffectInstance)
                .Where(effect => effect != null)
                .ToList();
        }

        public PrimaryStatModifiers GetPrimaryStatModifiers()
        {
            PrimaryStatModifiers modifiers = default;
            foreach (Passive entry in Entries)
            {
                if (entry?.Data == null)
                {
                    continue;
                }

                modifiers += entry.Data.PrimaryStatModifiers;
            }

            return modifiers;
        }

        public SecondaryStatModifiers GetSecondaryStatModifiers()
        {
            SecondaryStatModifiers modifiers = default;
            foreach (Passive entry in Entries)
            {
                if (entry?.Data == null)
                {
                    continue;
                }

                modifiers += entry.Data.SecondaryStatModifiers;
            }

            return modifiers;
        }

        public void Clear()
        {
            ClearInternal();
            NotifyOwnerChanged();
        }

        private bool CanEquipPassive(PassiveData data)
        {
            if (data == null)
            {
                return true;
            }

            if (equippedEntries.Count >= EquipPassiveSlotLimit)
            {
                return false;
            }

            return EquippedPassiveCost + data.Cost <= EquipPassiveCostLimit;
        }

        private bool ContainsPassiveId(string passiveId)
        {
            return Entries.Any(entry => string.Equals(entry?.PassiveId, passiveId, StringComparison.OrdinalIgnoreCase));
        }

        private List<Passive> GetTargetList(PassiveListKind listKind)
        {
            return listKind == PassiveListKind.Equip ? equippedEntries : uniqueEntries;
        }

        private void ClearInternal()
        {
            foreach (Passive entry in Entries.ToList())
            {
                entry?.EffectInstance?.OnRemove(owner, entry);
            }

            uniqueEntries.Clear();
            equippedEntries.Clear();
            MarkEntriesDirty();
        }

        private void RebuildCombinedEntriesIfNeeded()
        {
            if (!combinedEntriesDirty)
            {
                return;
            }

            combinedEntries.Clear();
            combinedEntries.AddRange(uniqueEntries);
            combinedEntries.AddRange(equippedEntries);
            combinedEntriesDirty = false;
        }

        private void MarkEntriesDirty()
        {
            combinedEntriesDirty = true;
        }

        private void NotifyOwnerChanged()
        {
            owner?.OnPassivesChanged();
        }
    }
}

