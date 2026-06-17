using System;
using System.Collections.Generic;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Inventory
{
    public interface IConsumableEffect
    {
        bool CanUse(Unit user, Unit target);
        void Use(Unit user, Unit target);
    }

    public interface IUnitPassive
    {
    }

    public static class ItemRegistry
    {
        private static readonly Dictionary<string, ItemData> Definitions = new Dictionary<string, ItemData>(StringComparer.OrdinalIgnoreCase);

        public static void Register(ItemData definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                return;
            }

            Definitions[definition.Id] = definition;
        }

        public static void RegisterRange(IEnumerable<ItemData> definitions)
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

        public static bool TryGet(string itemId, out ItemData definition)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                definition = null;
                return false;
            }

            return Definitions.TryGetValue(itemId, out definition);
        }

        public static ItemData Get(string itemId)
        {
            TryGet(itemId, out var definition);
            return definition;
        }

        public static void Clear()
        {
            Definitions.Clear();
        }
    }

    public static class ConsumableEffectRegistry
    {
        private static readonly Dictionary<string, Func<IConsumableEffect>> Factories = new Dictionary<string, Func<IConsumableEffect>>(StringComparer.OrdinalIgnoreCase);

        public static void Register(string effectId, Func<IConsumableEffect> factory)
        {
            if (string.IsNullOrWhiteSpace(effectId) || factory == null)
            {
                return;
            }

            Factories[effectId] = factory;
        }

        public static bool TryCreate(string effectId, out IConsumableEffect effect)
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

    public static class UnitPassiveRegistry
    {
        private static readonly Dictionary<string, Func<IUnitPassive>> Factories = new Dictionary<string, Func<IUnitPassive>>(StringComparer.OrdinalIgnoreCase);

        public static void Register(string passiveId, Func<IUnitPassive> factory)
        {
            if (string.IsNullOrWhiteSpace(passiveId) || factory == null)
            {
                return;
            }

            Factories[passiveId] = factory;
        }

        public static bool TryCreate(string passiveId, out IUnitPassive passive)
        {
            passive = null;
            if (string.IsNullOrWhiteSpace(passiveId))
            {
                return false;
            }

            if (!Factories.TryGetValue(passiveId, out var factory))
            {
                return false;
            }

            passive = factory.Invoke();
            return passive != null;
        }

        public static void Clear()
        {
            Factories.Clear();
        }
    }
}



