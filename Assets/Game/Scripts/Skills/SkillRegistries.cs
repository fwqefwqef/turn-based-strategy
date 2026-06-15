using System;
using System.Collections.Generic;
using TbsFramework.Cells;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Skills
{
    public class SkillContext
    {
        public CustomUnit User;
        public CustomUnit PrimaryTargetUnit;
        public Cell TargetCell;
        public CustomCellGrid CellGrid;
        public IReadOnlyList<CustomUnit> AreaTargets;
        public SkillData Skill;
    }

    public interface ISkillEffect
    {
        bool CanUse(CustomUnit user, SkillContext context);
        void Use(CustomUnit user, SkillContext context);
    }

    public interface IHealingSkillEffect : ISkillEffect
    {
        int GetHealingAmount(CustomUnit user, SkillContext context);
    }

    public interface IAttackSkillEffect : ISkillEffect
    {
        void ModifyAttackProfile(CustomUnit user, SkillContext context, ref ResolvedAttackProfile profile);
    }

    public static class SkillRegistry
    {
        private static readonly Dictionary<string, SkillData> Definitions = new Dictionary<string, SkillData>(StringComparer.OrdinalIgnoreCase);

        public static void Register(SkillData definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                return;
            }

            Definitions[definition.Id] = definition;
        }

        public static void RegisterRange(IEnumerable<SkillData> definitions)
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

        public static bool TryGet(string skillId, out SkillData definition)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                definition = null;
                return false;
            }

            return Definitions.TryGetValue(skillId, out definition);
        }

        public static SkillData Get(string skillId)
        {
            TryGet(skillId, out var definition);
            return definition;
        }

        public static void Clear()
        {
            Definitions.Clear();
        }
    }

    public static class SkillEffectRegistry
    {
        private static readonly Dictionary<string, Func<ISkillEffect>> Factories = new Dictionary<string, Func<ISkillEffect>>(StringComparer.OrdinalIgnoreCase);

        public static void Register(string effectId, Func<ISkillEffect> factory)
        {
            if (string.IsNullOrWhiteSpace(effectId) || factory == null)
            {
                return;
            }

            Factories[effectId] = factory;
        }

        public static bool TryCreate(string effectId, out ISkillEffect effect)
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


