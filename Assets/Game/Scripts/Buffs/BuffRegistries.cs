using System;
using System.Collections.Generic;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Buffs
{
    public interface IP_BuffEffect
    {
        void OnApply(CustomUnit unit, Buff entry);
        void OnRemove(CustomUnit unit, Buff entry);
        void OnTurnStart(CustomUnit unit, Buff entry);
        void OnTurnEnd(CustomUnit unit, Buff entry);
    }

    public abstract class BuffEffectBase : IP_BuffEffect
    {
        protected CustomUnit Owner { get; private set; }
        protected Buff Entry { get; private set; }

        public virtual void OnApply(CustomUnit unit, Buff entry)
        {
            Owner = unit;
            Entry = entry;
        }

        public virtual void OnRemove(CustomUnit unit, Buff entry)
        {
            if (ReferenceEquals(Owner, unit) && ReferenceEquals(Entry, entry))
            {
                Owner = null;
                Entry = null;
            }
        }

        public virtual void OnTurnStart(CustomUnit unit, Buff entry) { }
        public virtual void OnTurnEnd(CustomUnit unit, Buff entry) { }

        protected bool SelfRemove()
        {
            return Owner != null && Entry != null && Owner.RemoveBuff(Entry);
        }
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


