using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Skills
{
    public class SkillContext
    {
        public Unit User;
        public Unit PrimaryTargetUnit;
        public Cell TargetCell;
        public CellGrid CellGrid;
        public IReadOnlyList<Unit> AreaTargets;
        public IReadOnlyList<Cell> AreaCells;
        public SkillData Skill;
    }

    public interface ISkillEffect
    {
        bool CanUse(Unit user, SkillContext context);
        void Use(Unit user, SkillContext context);
    }

    public abstract class SkillEffectBase : ISkillEffect
    {
        protected Unit User { get; private set; }
        protected SkillContext Context { get; private set; }
        protected Unit Target => Context.PrimaryTargetUnit;
        protected CellGrid Grid => Context.CellGrid;

        protected IEnumerable<Unit> AreaTargets => Context.AreaTargets ?? Array.Empty<Unit>();
        protected IEnumerable<Unit> EnemyAreaTargets => AreaTargets
            .Where(target => target != null && target.IsAliveForBattle && target.PlayerNumber != User.PlayerNumber)
            .Distinct();
        protected virtual bool RequiresGrid => false;
        protected virtual bool RequiresTarget => false;

        public bool CanUse(Unit user, SkillContext context)
        {
            if (!SkillTargetValidator.CanUse(user, context))
            {
                return false;
            }

            Bind(user, context);
            return (!RequiresGrid || Grid != null)
                && (!RequiresTarget || Target != null)
                && MeetsAdditionalUseConditions();
        }

        protected virtual bool MeetsAdditionalUseConditions() => true;

        void ISkillEffect.Use(Unit user, SkillContext context)
        {
            if (CanUse(user, context))
            {
                Use();
            }
        }

        protected abstract void Use();

        protected void ApplyStatusToTarget(string statusId, int stacks = 1)
        {
            for (int i = 0; i < stacks; i++)
            {
                Target.AddBuffById(statusId, User);
            }
        }

        protected void ApplyStatusToSelf(string statusId, int stacks = 1)
        {
            for (int i = 0; i < stacks; i++)
            {
                User.AddBuffById(statusId, User);
            }
        }

        protected void ApplyStatusToEnemies(string statusId, int stacks = 1)
        {
            foreach (Unit target in EnemyAreaTargets)
            {
                for (int i = 0; i < stacks; i++)
                {
                    target.AddBuffById(statusId, User);
                }
            }
        }

        private void Bind(Unit user, SkillContext context)
        {
            User = user;
            Context = context;
        }

    }

    public abstract class AttackHitSkillEffectBase : SkillEffectBase, IP_AttackHitEffect
    {
        protected Unit Attacker { get; private set; }
        protected Unit Defender { get; private set; }
        protected int DamageDealt { get; private set; }
        protected bool IsBasicAttack { get; private set; }

        void IP_AttackHitEffect.OnAttackHit(Unit attacker, Unit defender, int damageDealt, bool isBasicAttack)
        {
            Attacker = attacker;
            Defender = defender;
            DamageDealt = damageDealt;
            IsBasicAttack = isBasicAttack;
            OnAttackHit();
        }

        protected abstract void OnAttackHit();

        protected void ApplyStatusToDefender(string statusId, int stacks = 1)
        {
            for (int i = 0; i < stacks; i++)
            {
                Defender.AddBuffById(statusId, Attacker);
            }
        }
    }

    public static class SkillTargetValidator
    {
        public static bool CanUse(Unit user, SkillContext context)
        {
            SkillData data = context?.Skill;
            if (user == null || !user.IsAliveForBattle || data == null)
            {
                return false;
            }

            Unit target = context.PrimaryTargetUnit;
            switch (data.TargetingType)
            {
                case SkillTargetingType.None:
                    return true;
                case SkillTargetingType.Self:
                    return target == user;
                case SkillTargetingType.EnemyUnit:
                    return IsLivingTarget(target) && target.PlayerNumber != user.PlayerNumber;
                case SkillTargetingType.AllyUnit:
                    return IsLivingTarget(target) && target != user && target.PlayerNumber == user.PlayerNumber;
                case SkillTargetingType.AnyUnit:
                    return IsLivingTarget(target) && target != user;
                case SkillTargetingType.Cell:
                    return context.TargetCell != null;
                case SkillTargetingType.AreaCell:
                    return CanTargetArea(user, target, context);
                default:
                    return false;
            }
        }

        private static bool CanTargetArea(Unit user, Unit target, SkillContext context)
        {
            if (context.TargetCell == null)
            {
                return false;
            }

            if (target == null)
            {
                return true;
            }

            if (!IsLivingTarget(target) || (context.Skill.SelfImmune && target == user))
            {
                return false;
            }

            bool isAlly = target.PlayerNumber == user.PlayerNumber;
            return isAlly
                ? context.Skill.AreaProfile.AffectsAllies
                : context.Skill.AreaProfile.AffectsEnemies;
        }

        private static bool IsLivingTarget(Unit target) => target != null && target.IsAliveForBattle;
    }

    public interface IHealingSkillEffect : ISkillEffect
    {
        int GetHealingAmount(Unit user, SkillContext context);
    }

    public interface IAttackSkillEffect : ISkillEffect
    {
        void ModifyAttackProfile(Unit user, SkillContext context, ref ResolvedAttackProfile profile);
    }

    public interface IAreaHitPointChangeSkillEffect : ISkillEffect
    {
        int GetProjectedHitPointDelta(Unit user, SkillContext context);
    }

    public interface IAreaAttackTargetModifier : ISkillEffect
    {
        void ModifyAttackProfileForTarget(Unit user, SkillContext context, ref ResolvedAttackProfile profile);
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

