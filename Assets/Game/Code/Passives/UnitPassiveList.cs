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
        private readonly Unit owner;
        private readonly List<Passive> entries = new List<Passive>();

        public IReadOnlyList<Passive> ClassEntries => entries;
        public IReadOnlyList<Passive> Entries => entries;

        public UnitPassiveList(Unit owner)
        {
            this.owner = owner;
        }

        public void LoadStartingPassives(IEnumerable<StartingPassiveEntry> classPassives)
        {
            ClearInternal();

            foreach (StartingPassiveEntry entry in classPassives ?? Array.Empty<StartingPassiveEntry>())
            {
                AddPassiveById(entry.PassiveId, notifyOwner: false);
            }

            NotifyOwnerChanged();
        }

        public Passive AddPassive(PassiveData data, bool notifyOwner = true)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.Id))
            {
                return null;
            }

            PassiveRegistry.Register(data);

            if (ContainsPassiveId(data.Id))
            {
                return entries.FirstOrDefault(entry => string.Equals(entry.PassiveId, data.Id, StringComparison.OrdinalIgnoreCase));
            }

            Passive passive = new Passive(data);
            entries.Add(passive);
            passive.EffectInstance?.OnApply(owner, passive);

            if (notifyOwner)
            {
                NotifyOwnerChanged();
            }

            return passive;
        }

        public Passive AddPassiveById(string passiveId, bool notifyOwner = true)
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

            return AddPassive(data, notifyOwner);
        }

        public bool RemovePassive(Passive entry, bool notifyOwner = true)
        {
            if (entry == null || !entries.Remove(entry))
            {
                return false;
            }

            entry.EffectInstance?.OnRemove(owner, entry);
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

        private bool ContainsPassiveId(string passiveId)
        {
            return entries.Any(entry => string.Equals(entry?.PassiveId, passiveId, StringComparison.OrdinalIgnoreCase));
        }

        private void ClearInternal()
        {
            foreach (Passive entry in entries.ToList())
            {
                entry?.EffectInstance?.OnRemove(owner, entry);
            }

            entries.Clear();
        }

        private void NotifyOwnerChanged()
        {
            owner?.OnPassivesChanged();
        }
    }
}
