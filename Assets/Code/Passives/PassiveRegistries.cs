using System;
using System.Collections.Generic;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Skills;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Passives
{
    public interface IP_PassiveEffect
    {
        void OnApply(Unit unit, Passive entry);
        void OnRemove(Unit unit, Passive entry);
        void OnTurnStart(Unit unit, Passive entry);
        void OnTurnEnd(Unit unit, Passive entry);
    }

    public interface IP_DynamicSecondaryStatModifier
    {
        SecondaryStatModifiers GetSecondaryStatModifiers(Unit unit);
    }

    public interface IP_DynamicPrimaryStatModifier
    {
        PrimaryStatModifiers GetPrimaryStatModifiers(Unit unit);
    }

    public interface IP_MovementPointModifier
    {
        float GetMovementPointModifier(Unit unit);
    }

    public interface IP_PostActionMovement
    {
        float GetPostActionMovementPoints(Unit unit);
    }

    public interface IP_IgnoreTerrainMovementCost
    {
    }

    public interface IP_TraverseUntraversableTerrain
    {
    }

    public interface IP_CrowdControlImmunity
    {
    }

    public interface IP_SpellMaxRangeModifier
    {
        int GetSpellMaxRangeModifier(Unit unit, SkillData skill);
    }

    public interface IP_HealingPerformed
    {
        void OnHealingPerformed(Unit healer, Unit target, int actualAmount);
    }

    public abstract class PassiveEffectBase : IP_PassiveEffect
    {
        protected Unit Owner { get; private set; }
        protected Passive Entry { get; private set; }

        void IP_PassiveEffect.OnApply(Unit unit, Passive entry)
        {
            if (unit == null || entry == null)
            {
                return;
            }

            Owner = unit;
            Entry = entry;
            OnApply();
        }

        void IP_PassiveEffect.OnRemove(Unit unit, Passive entry)
        {
            if (!ReferenceEquals(Owner, unit) || !ReferenceEquals(Entry, entry))
            {
                return;
            }

            OnRemove();
            Owner = null;
            Entry = null;
        }

        void IP_PassiveEffect.OnTurnStart(Unit unit, Passive entry)
        {
            if (ReferenceEquals(Owner, unit) && ReferenceEquals(Entry, entry)) OnTurnStart();
        }

        void IP_PassiveEffect.OnTurnEnd(Unit unit, Passive entry)
        {
            if (ReferenceEquals(Owner, unit) && ReferenceEquals(Entry, entry)) OnTurnEnd();
        }

        protected virtual void OnApply() { }
        protected virtual void OnRemove() { }
        protected virtual void OnTurnStart() { }
        protected virtual void OnTurnEnd() { }
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

