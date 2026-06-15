using System;
using System.Collections.Generic;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Passives
{
    public interface IP_PassiveEffect
    {
        void OnApply(CustomUnit unit, Passive entry);
        void OnRemove(CustomUnit unit, Passive entry);
        void OnTurnStart(CustomUnit unit, Passive entry);
        void OnTurnEnd(CustomUnit unit, Passive entry);
    }

    public abstract class PassiveEffectBase : IP_PassiveEffect
    {
        protected CustomUnit Owner { get; private set; }
        protected Passive Entry { get; private set; }

        public virtual void OnApply(CustomUnit unit, Passive entry)
        {
            Owner = unit;
            Entry = entry;
        }

        public virtual void OnRemove(CustomUnit unit, Passive entry)
        {
            if (ReferenceEquals(Owner, unit) && ReferenceEquals(Entry, entry))
            {
                Owner = null;
                Entry = null;
            }
        }

        public virtual void OnTurnStart(CustomUnit unit, Passive entry) { }
        public virtual void OnTurnEnd(CustomUnit unit, Passive entry) { }
    }

    public static class PassiveRegistry
    {
        private static readonly Dictionary<string, PassiveData> Definitions = new Dictionary<string, PassiveData>(StringComparer.OrdinalIgnoreCase);

        public static void Register(PassiveData definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                return;
            }

            Definitions[definition.Id] = definition;
        }

        public static void RegisterRange(IEnumerable<PassiveData> definitions)
        {
            if (definitions == null)
            {
                return;
            }

            foreach (PassiveData definition in definitions)
            {
                Register(definition);
            }
        }

        public static bool TryGet(string passiveId, out PassiveData definition)
        {
            if (string.IsNullOrWhiteSpace(passiveId))
            {
                definition = null;
                return false;
            }

            return Definitions.TryGetValue(passiveId, out definition);
        }

        public static PassiveData Get(string passiveId)
        {
            TryGet(passiveId, out PassiveData definition);
            return definition;
        }

        public static void Clear()
        {
            Definitions.Clear();
        }
    }

    public static class PassiveEffectRegistry
    {
        private static readonly Dictionary<string, Func<IP_PassiveEffect>> Factories = new Dictionary<string, Func<IP_PassiveEffect>>(StringComparer.OrdinalIgnoreCase);

        public static void Register(string effectId, Func<IP_PassiveEffect> factory)
        {
            if (string.IsNullOrWhiteSpace(effectId) || factory == null)
            {
                return;
            }

            Factories[effectId] = factory;
        }

        public static bool TryCreate(string effectId, out IP_PassiveEffect effect)
        {
            effect = null;
            if (string.IsNullOrWhiteSpace(effectId))
            {
                return false;
            }

            if (!Factories.TryGetValue(effectId, out Func<IP_PassiveEffect> factory))
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
