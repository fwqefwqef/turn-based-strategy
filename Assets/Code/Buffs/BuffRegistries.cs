using System;
using System.Collections.Generic;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Buffs
{
    public interface IP_BuffEffect
    {
        void OnApply(Unit unit, Buff entry);
        void OnRemove(Unit unit, Buff entry);
        void OnTurnStart(Unit unit, Buff entry);
        void OnTurnEnd(Unit unit, Buff entry);
    }

    public interface IP_BuffStackChanged
    {
        void OnStackChanged(Unit unit, Buff entry, int previousStacks, int currentStacks);
    }

    // Implemented by effects that block voluntary actions and counterattacks.
    public interface IP_ActionBlocker { }

    // Implemented by effects that cap the owner's available movement points.
    public interface IP_MovementPointCap
    {
        float GetMovementPointCap(float currentCap);
    }

    public abstract class BuffEffectBase : IP_BuffEffect
    {
        protected Unit Owner { get; private set; }
        protected Buff Entry { get; private set; }
        protected Unit Source => Entry.SourceUnit;
        protected int Stacks => Entry.Stacks;

        void IP_BuffEffect.OnApply(Unit unit, Buff entry)
        {
            if (unit == null || entry == null)
            {
                return;
            }

            Owner = unit;
            Entry = entry;
            OnApply();
        }

        void IP_BuffEffect.OnRemove(Unit unit, Buff entry)
        {
            if (!ReferenceEquals(Owner, unit) || !ReferenceEquals(Entry, entry))
            {
                return;
            }

            OnRemove();
            Owner = null;
            Entry = null;
        }

        void IP_BuffEffect.OnTurnStart(Unit unit, Buff entry)
        {
            if (ReferenceEquals(Owner, unit) && ReferenceEquals(Entry, entry)) OnTurnStart();
        }

        void IP_BuffEffect.OnTurnEnd(Unit unit, Buff entry)
        {
            if (ReferenceEquals(Owner, unit) && ReferenceEquals(Entry, entry)) OnTurnEnd();
        }

        protected virtual void OnApply() { }
        protected virtual void OnRemove() { }
        protected virtual void OnTurnStart() { }
        protected virtual void OnTurnEnd() { }

        protected bool SelfRemove() => Owner != null && Entry != null && Owner.RemoveBuff(Entry);
    }

    public static class BuffRegistry
    {
        private static readonly Dictionary<string, BuffData> Definitions = new Dictionary<string, BuffData>(StringComparer.OrdinalIgnoreCase);

        public static BuffData CreateRuntimeInstance(BuffData template, int? durationOverride = null, string idSuffix = null)
        {
            if (template == null || string.IsNullOrWhiteSpace(template.Id))
            {
                return null;
            }

            return new BuffData
            {
                Id = $"{template.Id}__runtime__{(string.IsNullOrWhiteSpace(idSuffix) ? Guid.NewGuid().ToString("N") : idSuffix)}",
                Name = template.Name,
                Description = template.Description,
                Duration = durationOverride ?? template.Duration,
                Category = template.Category,
                MaxStacks = template.MaxStacks,
                Removable = template.Removable,
                StackKey = template.StackingId,
                PrimaryStatModifiers = template.PrimaryStatModifiers,
                SecondaryStatModifiers = template.SecondaryStatModifiers,
                EffectId = template.EffectId
            };
        }

        public static void Register(BuffData definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                return;
            }

            Definitions[definition.Id] = definition;
        }

        public static void RegisterRange(IEnumerable<BuffData> definitions)
        {
            if (definitions == null)
            {
                return;
            }

            foreach (var definition in definitions)
            {
                Register(definition);
            }
        }

        public static bool TryGet(string buffId, out BuffData definition)
        {
            if (string.IsNullOrWhiteSpace(buffId))
            {
                definition = null;
                return false;
            }

            return Definitions.TryGetValue(buffId, out definition);
        }

        public static BuffData Get(string buffId)
        {
            TryGet(buffId, out var definition);
            return definition;
        }

        public static void Clear()
        {
            Definitions.Clear();
        }
    }

    public static class BuffEffectRegistry
    {
        private static readonly Dictionary<string, Func<IP_BuffEffect>> Factories = new Dictionary<string, Func<IP_BuffEffect>>(StringComparer.OrdinalIgnoreCase);

        public static void Register(string effectId, Func<IP_BuffEffect> factory)
        {
            if (string.IsNullOrWhiteSpace(effectId) || factory == null)
            {
                return;
            }

            Factories[effectId] = factory;
        }

        public static bool TryCreate(string effectId, out IP_BuffEffect effect)
        {
            effect = null;
            if (string.IsNullOrWhiteSpace(effectId))
            {
                return false;
            }

            if (!Factories.TryGetValue(effectId, out var factory))
            {
                return false;
            }

            effect = factory.Invoke();
            return effect != null;
        }

        public static void Clear()
        {
            Factories.Clear();
        }
    }
}



