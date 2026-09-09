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

        [SerializeField]
        private int stacks = 1;

        [NonSerialized]
        private IP_BuffEffect effectInstance;

        [NonSerialized] private bool controlTurnStarted;

        public string BuffId => buffId;
        public BuffData Data => BuffRegistry.Get(buffId);
        public int RemainingDuration => remainingDuration;
        public int Stacks => Mathf.Clamp(stacks <= 0 ? 1 : stacks, 1, MaxStacks);
        public int MaxStacks => Mathf.Max(1, Data?.MaxStacks ?? 1);
        public BuffCategory Category => Data?.Category ?? BuffCategory.Buff;
        public bool Removable => Data?.Removable ?? true;
        public string StackingId => Data?.StackingId ?? buffId;
        public bool IsInfinite => Data != null && Data.Duration == 0;
        public IP_BuffEffect EffectInstance => effectInstance;

        public Buff()
        {
        }

        public Buff(string buffId)
        {
            this.buffId = buffId;
            remainingDuration = Mathf.Max(0, Data?.Duration ?? 0);
            stacks = 1;
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
            stacks = 1;
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

        public void BeginOwnerTurn() { controlTurnStarted = true; }

        public void EndOwnerTurn()
        {
            // Pain ages with actual ticks; CC must survive a complete upcoming turn.
            if (Category != BuffCategory.Pain && (Category != BuffCategory.CC || controlTurnStarted))
                DecrementDuration();
            controlTurnStarted = false;
        }

        public bool ApplyAdditionalStack(BuffData appliedData, out int previousStacks, out int currentStacks)
        {
            previousStacks = Stacks;
            BuffData data = appliedData ?? Data;
            int maxStacks = Mathf.Max(1, data?.MaxStacks ?? MaxStacks);
            stacks = Mathf.Clamp(previousStacks + 1, 1, maxStacks);
            currentStacks = Stacks;
            remainingDuration = Mathf.Max(0, data?.Duration ?? Data?.Duration ?? remainingDuration);
            controlTurnStarted = false;

            return previousStacks != currentStacks;
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

            data.MaxStacks = Mathf.Max(1, data.MaxStacks);
            BuffRegistry.Register(data);

            Buff existingEntry = entries.FirstOrDefault(entry => HasSameStackingId(entry, data));
            if (existingEntry != null)
            {
                bool stackChanged = existingEntry.ApplyAdditionalStack(data, out int previousStacks, out int currentStacks);
                if (stackChanged && existingEntry.EffectInstance is IP_BuffStackChanged stackChangedEffect)
                {
                    stackChangedEffect.OnStackChanged(owner, existingEntry, previousStacks, currentStacks);
                }

                return existingEntry;
            }

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

            foreach (var entry in entries.ToList())
            {
                if (entry == null || !entries.Contains(entry))
                {
                    continue;
                }

                entry.BeginOwnerTurn();
                entry.EffectInstance?.OnTurnStart(owner, entry);
            }
        }

        public void OnTurnEnd()
        {
            foreach (var entry in entries.ToList())
            {
                if (entry == null || !entries.Contains(entry))
                {
                    continue;
                }

                entry.EffectInstance?.OnTurnEnd(owner, entry);
                entry.EndOwnerTurn();
                if (entry.Category == BuffCategory.CC && entry.HasExpired()) RemoveBuff(entry);
            }
        }

        public void OnDotTick(BuffCategory category = BuffCategory.Pain)
        {
            foreach (var entry in entries.ToList())
            {
                if (owner == null || !owner.IsAliveForBattle) break;
                if (entry == null || !entries.Contains(entry) || entry.HasExpired() || entry.Category != category)
                {
                    continue;
                }

                if (entry.EffectInstance is IP_DotTick dotTickEffect)
                {
                    dotTickEffect.OnDotTick(owner, entry);
                    entry.DecrementDuration();
                    if (entry.HasExpired()) RemoveBuff(entry);
                }
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

        public float ApplyMovementPointCaps(float movementPoints)
        {
            float cappedMovementPoints = movementPoints;
            foreach (Buff entry in entries)
            {
                if (entry?.EffectInstance is IP_MovementPointCap movementCap)
                {
                    cappedMovementPoints = Math.Min(
                        cappedMovementPoints,
                        movementCap.GetMovementPointCap(owner, entry, cappedMovementPoints));
                }
            }

            return cappedMovementPoints;
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

                modifiers += entry.Data.PrimaryStatModifiers * entry.Stacks;
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

                modifiers += entry.Data.SecondaryStatModifiers * entry.Stacks;
            }

            return modifiers;
        }

        public bool HasBuff(string buffId)
        {
            return !string.IsNullOrWhiteSpace(buffId)
                && entries.Any(entry => entry != null && string.Equals(entry.BuffId, buffId, StringComparison.OrdinalIgnoreCase));
        }

        public Buff GetBuff(string buffId)
        {
            return string.IsNullOrWhiteSpace(buffId)
                ? null
                : entries.FirstOrDefault(entry => entry != null && string.Equals(entry.BuffId, buffId, StringComparison.OrdinalIgnoreCase));
        }

        public int RemoveRemovableBuffs(params BuffCategory[] categories)
        {
            HashSet<BuffCategory> categorySet = categories != null && categories.Length > 0
                ? new HashSet<BuffCategory>(categories)
                : null;

            return RemoveWhere(entry => entry != null
                && entry.Removable
                && (categorySet == null || categorySet.Contains(entry.Category)));
        }

        public int RemoveRemovableDebuffs()
        {
            return RemoveRemovableBuffs(BuffCategory.Weakening, BuffCategory.CC, BuffCategory.Pain, BuffCategory.Misc);
        }

        private int RemoveWhere(Func<Buff, bool> predicate)
        {
            if (predicate == null)
            {
                return 0;
            }

            int removedCount = 0;
            foreach (Buff entry in entries.ToList())
            {
                if (!predicate(entry))
                {
                    continue;
                }

                if (RemoveBuff(entry))
                {
                    removedCount++;
                }
            }

            return removedCount;
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

        private static bool HasSameStackingId(Buff entry, BuffData data)
        {
            string existingStackingId = entry?.StackingId;
            string incomingStackingId = data?.StackingId;
            return !string.IsNullOrWhiteSpace(existingStackingId)
                && !string.IsNullOrWhiteSpace(incomingStackingId)
                && string.Equals(existingStackingId, incomingStackingId, StringComparison.OrdinalIgnoreCase);
        }
    }
}



