using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Units;
using UnityEngine;

namespace Windy.Srpg.Game.Buffs
{
    [Serializable]
    public class Buff
    {
        [SerializeField]
        private string buffId;

        [SerializeField]
        private int remainingDuration;

        [NonSerialized]
        private IP_BuffEffect effectInstance;

        public string BuffId => buffId;
        public BuffData Data => BuffRegistry.Get(buffId);
        public int RemainingDuration => remainingDuration;
        public bool IsInfinite => Data != null && Data.Duration == 0;
        public IP_BuffEffect EffectInstance => effectInstance;

        public Buff()
        {
        }

        public Buff(string buffId)
        {
            this.buffId = buffId;
            remainingDuration = Mathf.Max(0, Data?.Duration ?? 0);
            TryCreateEffectInstance();
        }

        public Buff(BuffData data)
        {
            if (data == null)
            {
                return;
            }

            buffId = data.Id;
            remainingDuration = Mathf.Max(0, data.Duration);
            TryCreateEffectInstance();
        }

        public bool HasExpired()
        {
            return !IsInfinite && remainingDuration <= 0;
        }

        public void DecrementDuration()
        {
            if (IsInfinite || remainingDuration <= 0)
            {
                return;
            }

            remainingDuration--;
        }

        private void TryCreateEffectInstance()
        {
            if (Data == null)
            {
                effectInstance = null;
                return;
            }

            BuffEffectRegistry.TryCreate(Data.EffectId, out effectInstance);
        }
    }

    public sealed class UnitBuffList
    {
        private readonly Unit owner;
        private readonly List<Buff> entries = new List<Buff>();

        public IReadOnlyList<Buff> Entries => entries;

        public UnitBuffList(Unit owner)
        {
            this.owner = owner;
        }

        public Buff AddBuff(BuffData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.Id))
            {
                return null;
            }

            BuffRegistry.Register(data);

            var entry = new Buff(data);
            entries.Add(entry);
            entry.EffectInstance?.OnApply(owner, entry);
            return entry;
        }

        public Buff AddBuffById(string buffId)
        {
            if (!BuffRegistry.TryGet(buffId, out var data))
            {
                Debug.LogWarning($"UnitBuffList: Buff id '{buffId}' is not registered.");
                return null;
            }

            return AddBuff(data);
        }

        public bool RemoveBuff(Buff entry)
        {
            if (entry == null || !entries.Remove(entry))
            {
                return false;
            }

            entry.EffectInstance?.OnRemove(owner, entry);
            return true;
        }

        public void OnTurnStart()
        {
            RemoveExpiredEntries();

            foreach (var entry in entries)
            {
                entry.EffectInstance?.OnTurnStart(owner, entry);
            }
        }

        public void OnTurnEnd()
        {
            foreach (var entry in entries)
            {
                entry.EffectInstance?.OnTurnEnd(owner, entry);
                entry.DecrementDuration();
            }
        }

        public void Clear()
        {
            foreach (var entry in entries.ToList())
            {
                RemoveBuff(entry);
            }
        }

        public IEnumerable<IP_BuffEffect> GetActiveEffects()
        {
            return entries
                .Select(entry => entry?.EffectInstance)
                .Where(effect => effect != null)
                .ToList();
        }

        public PrimaryStatModifiers GetPrimaryStatModifiers()
        {
            PrimaryStatModifiers modifiers = default;

            foreach (var entry in entries)
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

            foreach (var entry in entries)
            {
                if (entry?.Data == null)
                {
                    continue;
                }

                modifiers += entry.Data.SecondaryStatModifiers;
            }

            return modifiers;
        }

        private void RemoveExpiredEntries()
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (entry == null || !entry.HasExpired())
                {
                    continue;
                }

                RemoveBuff(entry);
            }
        }
    }
}



