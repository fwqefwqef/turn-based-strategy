using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Catalogs;
using Windy.Srpg.Game.Units;
using UnityEngine;

namespace Windy.Srpg.Game.Skills
{
    public static class BuiltInSkillCatalog
    {
        private static bool isRegistered;

        public static void EnsureRegistered()
        {
            if (isRegistered)
            {
                return;
            }

            SkillCatalogResource catalog = CatalogResourceLoader.LoadSkillCatalog();
            SkillRegistry.RegisterRange(catalog.ToRuntimeDefinitions());

            SkillEffectRegistry.Register("sacrifice_heal", () => new SacrificeHealSkillEffect());
            SkillEffectRegistry.Register("immolate", () => new ImmolateSkillEffect());
            SkillEffectRegistry.Register("ignore_def_mag", () => new IgnoreDefMagSkillEffect());
            SkillEffectRegistry.Register("shove", () => new ShoveSkillEffect());
            SkillEffectRegistry.Register("cleanse", () => new CleanseSkillEffect());
            SkillEffectRegistry.Register("break_current_hp", () => new BreakCurrentHitPointsEffect());
            SkillEffectRegistry.Register("refresh_action", () => new RefreshActionSkillEffect());
            SkillEffectRegistry.Register("burn_area_targets", () => new BurnAreaTargetsSkillEffect());
            SkillEffectRegistry.Register("flame_last_stand", () => new InsurmountableSkillEffect());
            SkillEffectRegistry.Register("profile_heal", () => new ProfileHealSkillEffect());
            SkillEffectRegistry.Register("anathema", () => new AnathemaSkillEffect());
            SkillEffectRegistry.Register("slow", () => new SlowSkillEffect());
            SkillEffectRegistry.Register("storm_surge", () => new StormSurgeSkillEffect());
            SkillEffectRegistry.Register("clang", () => new ClangSkillEffect());
            SkillEffectRegistry.Register("dark_sanctuary", () => new DarkSanctuarySkillEffect());
            SkillEffectRegistry.Register("punish", () => new PunishSkillEffect());
            SkillEffectRegistry.Register("ice_spikes", () => new IceSpikesSkillEffect());
            SkillEffectRegistry.Register("return_to_hell", () => new ReturnToHellSkillEffect());
            SkillEffectRegistry.Register("bash_lifesteal", () => new BashLifeStealEffect());
            SkillEffectRegistry.Register("shining_pillar", () => new ShiningPillarEffect());

            isRegistered = true;
        }

        private static int GetProfileHealingAmount(Unit user, SkillContext context)
        {
            if (user == null || context?.PrimaryTargetUnit == null || context.Skill == null)
            {
                return 0;
            }

            SkillHealProfile profile = context.Skill.HealProfile;
            if (!profile.Enabled)
            {
                return 0;
            }

            int amount = profile.Might + (profile.ScalesWithMagic ? user.Magic : 0);
            amount = Mathf.Max(0, amount);
            return profile.DoubleAtDeathsDoor && context.PrimaryTargetUnit.HitPoints <= 0
                ? amount * 2
                : amount;
        }

        private sealed class PunishSkillEffect : SkillEffectBase, IP_AttackHitEffect
        {
            protected override void Apply(Unit user, SkillContext context) { }

            public void OnAttackHit(Unit attacker, Unit defender, int damageDealt, bool isBasicAttack)
            {
                if (attacker != null && defender != null && attacker.PlayerNumber != defender.PlayerNumber)
                    defender.AddBuffById("punished", attacker);
            }
        }

        private sealed class IceSpikesSkillEffect : SkillEffectBase
        {
            protected override bool MeetsAdditionalUseConditions(Unit user, SkillContext context) =>
                context?.AreaTargets != null;

            protected override void Apply(Unit user, SkillContext context)
            {
                foreach (Unit target in context.AreaTargets.Distinct())
                {
                    if (target != null && target.IsAliveForBattle && target.PlayerNumber != user.PlayerNumber)
                        target.AddBuffById("ice_spikes_slow", user);
                }
            }
        }

        private sealed class ReturnToHellSkillEffect : SkillEffectBase, IAreaAttackTargetModifier
        {
            protected override void Apply(Unit user, SkillContext context) { }

            public void ModifyAttackProfileForTarget(Unit user, SkillContext context, ref ResolvedAttackProfile profile)
            {
                Unit target = context?.PrimaryTargetUnit;
                if (target != null && target.HitPoints * 2 <= target.ComputedTotalHitPoints)
                {
                    int defense = profile.IsMagic ? target.Magic : target.Defense;
                    int normalDamage = Mathf.Max(1, profile.Damage - defense);
                    profile.Damage = defense + normalDamage * 2;
                }
            }
        }

        private sealed class BashLifeStealEffect : SkillEffectBase, IP_AttackHitEffect
        {
            protected override void Apply(Unit user, SkillContext context) { }

            public void OnAttackHit(Unit attacker, Unit defender, int damageDealt, bool isBasicAttack)
            {
                if (attacker != null && defender != null && damageDealt > 0)
                    attacker.RestoreHitPoints(damageDealt / 2, attacker);
            }
        }

        private sealed class ShiningPillarEffect : SkillEffectBase, IAreaHitPointChangeSkillEffect
        {
            public int GetProjectedHitPointDelta(Unit user, SkillContext context)
            {
                if (!CanUse(user, context)) return 0;
                Unit target = context.PrimaryTargetUnit;
                int power = GetProfileHealingAmount(user, context);
                return target.PlayerNumber == user.PlayerNumber
                    ? Mathf.Min(power, Mathf.Max(0, target.ComputedTotalHitPoints - target.HitPoints))
                    : -Mathf.Min(target.HitPoints, Mathf.Max(1, power - target.Magic));
            }

            protected override void Apply(Unit user, SkillContext context)
            {
                Unit target = context.PrimaryTargetUnit;
                int power = GetProfileHealingAmount(user, context);
                if (target.PlayerNumber == user.PlayerNumber)
                {
                    target.RestoreHitPoints(power, user);
                    return;
                }

                target.DefendHandler(
                    user,
                    power,
                    user.Speed * Unit.AccuracyPerSpeedPoint + 100,
                    user.Luck * 5,
                    isMagicAttack: true,
                    isCounterAttack: false,
                    simulateOnly: false,
                    applyWeaponEffects: false,
                    isAreaSpell: true);
            }

        }

        private sealed class RefreshActionSkillEffect : SkillEffectBase
        {
            protected override bool MeetsAdditionalUseConditions(Unit user, SkillContext context)
            {
                Unit target = context?.PrimaryTargetUnit;
                return target != null && target.IsFinishedForTurn
                    && !target.IsActionBlocked;
            }

            protected override void Apply(Unit user, SkillContext context)
            {
                context.PrimaryTargetUnit.RefreshAction();
            }
        }

        private sealed class BurnAreaTargetsSkillEffect : SkillEffectBase
        {
            protected override bool MeetsAdditionalUseConditions(Unit user, SkillContext context) =>
                context?.AreaTargets != null;

            protected override void Apply(Unit user, SkillContext context)
            {
                var seen = new System.Collections.Generic.HashSet<Unit>();
                foreach (Unit target in context.AreaTargets)
                {
                    if (target != null && target.PlayerNumber != user.PlayerNumber
                        && target.IsAliveForBattle && seen.Add(target))
                        target.AddBuffById("burn", user);
                }
            }
        }

        private sealed class InsurmountableSkillEffect : SkillEffectBase
        {
            protected override void Apply(Unit user, SkillContext context)
            {
                if (user.HitPoints <= 0)
                    user.RestoreHitPoints(1 - user.HitPoints, user);

                user.AddBuffById("flame_last_stand");
            }
        }

        private sealed class ProfileHealSkillEffect : SkillEffectBase, IHealingSkillEffect
        {
            protected override bool MeetsAdditionalUseConditions(Unit user, SkillContext context)
            {
                Unit target = context?.PrimaryTargetUnit;
                return context?.Skill?.HealProfile.Enabled == true
                    && target != null && target.HitPoints < target.ComputedTotalHitPoints;
            }

            public int GetHealingAmount(Unit user, SkillContext context)
            {
                if (!CanUse(user, context)) return 0;
                if (context.Skill == null) return 0;
                return GetProfileHealingAmount(user, context);
            }

            protected override void Apply(Unit user, SkillContext context)
            {
                int amount = GetHealingAmount(user, context);
                if (amount > 0) context.PrimaryTargetUnit.RestoreHitPoints(amount, user);
            }
        }

        private sealed class AnathemaSkillEffect : SkillEffectBase
        {
            protected override void Apply(Unit user, SkillContext context)
            {
                for (int i = 0; i < 2; i++) context.PrimaryTargetUnit.AddBuffById("curse", user);
            }
        }

        private sealed class SlowSkillEffect : SkillEffectBase
        {
            protected override void Apply(Unit user, SkillContext context)
            {
                context.PrimaryTargetUnit.AddBuffById("slow", user);
            }
        }

        private sealed class StormSurgeSkillEffect : SkillEffectBase
        {
            protected override void Apply(Unit user, SkillContext context)
            {
                user.AddBuffById("storm_surge");
            }
        }

        private sealed class ClangSkillEffect : SkillEffectBase, IP_CancelCounterattackOnHit
        {
            private Grid.CellGrid grid;
            public bool HitLanded { get; private set; }

            protected override bool MeetsAdditionalUseConditions(Unit user, SkillContext context) =>
                context?.CellGrid != null;

            protected override void Apply(Unit user, SkillContext context)
            {
                grid = context.CellGrid;
            }

            public void OnAttackHit(Unit attacker, Unit defender, int damageDealt, bool isBasicAttack)
            {
                HitLanded = true;
                if (grid != null && attacker != null && defender != null && defender.IsAliveForBattle)
                    attacker.DisplaceTarget(defender, grid, distance: 2, push: true);
            }
        }

        private sealed class DarkSanctuarySkillEffect : SkillEffectBase
        {
            protected override void Apply(Unit user, SkillContext context)
            {
                context.PrimaryTargetUnit.AddBuffById("dark_sanctuary");
            }
        }

        private sealed class CleanseSkillEffect : SkillEffectBase
        {
            protected override bool MeetsAdditionalUseConditions(Unit user, SkillContext context)
            {
                Unit target = context?.PrimaryTargetUnit;
                return target != null && target.HasRemovableDebuffs;
            }

            protected override void Apply(Unit user, SkillContext context)
            {
                context.PrimaryTargetUnit.RemoveRemovableDebuffs();
            }
        }

        private sealed class BreakCurrentHitPointsEffect : SkillEffectBase, IAttackSkillEffect
        {
            public void ModifyAttackProfile(Unit user, SkillContext context, ref ResolvedAttackProfile profile)
            {
                if (user != null) profile.Damage += Mathf.Max(0, user.HitPoints);
            }

            protected override void Apply(Unit user, SkillContext context) { }
        }

        private sealed class SacrificeHealSkillEffect : SkillEffectBase, IHealingSkillEffect
        {
            private bool appliedSelfCost;

            protected override bool MeetsAdditionalUseConditions(Unit user, SkillContext context)
            {
                var target = context?.PrimaryTargetUnit;
                return user.HitPoints > 1
                    && target != null && target.HitPoints < target.ComputedTotalHitPoints;
            }

            public int GetHealingAmount(Unit user, SkillContext context)
            {
                return GetProfileHealingAmount(user, context);
            }

            protected override void Apply(Unit user, SkillContext context)
            {
                if (!appliedSelfCost)
                {
                    appliedSelfCost = true;
                    user.SetCurrentHitPoints(Mathf.Max(1, user.HitPoints > 0 ? 1 : 0), user);
                }

                int healingAmount = GetHealingAmount(user, context);
                if (healingAmount <= 0)
                {
                    return;
                }

                context.PrimaryTargetUnit.RestoreHitPoints(healingAmount, user);
            }
        }

        private sealed class IgnoreDefMagSkillEffect : SkillEffectBase
        {
            protected override void Apply(Unit user, SkillContext context)
            {
                user.BuffAdd("luna");
            }
        }

        private sealed class ImmolateSkillEffect : SkillEffectBase, IAttackSkillEffect
        {
            protected override bool MeetsAdditionalUseConditions(Unit user, SkillContext context)
            {
                return user.HitPoints > GetHealthCost(user);
            }

            public void ModifyAttackProfile(Unit user, SkillContext context, ref ResolvedAttackProfile profile)
            {
                if (user == null)
                {
                    return;
                }

                profile.Damage += GetHealthCost(user) * 2;
            }

            protected override void Apply(Unit user, SkillContext context)
            {
                int healthCost = GetHealthCost(user);
                if (healthCost <= 0)
                {
                    return;
                }

                user.SetCurrentHitPoints(user.HitPoints - healthCost, user);
            }

            private static int GetHealthCost(Unit user)
            {
                return user == null ? 0 : Mathf.Max(0, user.ComputedTotalHitPoints / 2);
            }
        }

        private sealed class ShoveSkillEffect : SkillEffectBase
        {
            protected override bool MeetsAdditionalUseConditions(Unit user, SkillContext context)
            {
                if (user == null || context?.PrimaryTargetUnit == null || context.CellGrid == null)
                {
                    return false;
                }

                var actingCell = user.HasPendingMove ? user.PreviewCell : user.Cell;
                var targetCell = context.PrimaryTargetUnit.HasPendingMove ? context.PrimaryTargetUnit.PreviewCell : context.PrimaryTargetUnit.Cell;
                return UnitDisplacementUtility.CanDisplaceRelative(
                    user,
                    actingCell,
                    context.PrimaryTargetUnit,
                    targetCell,
                    context.CellGrid,
                    distance: 1,
                    push: true,
                    moveUserWithTarget: false);
            }

            protected override void Apply(Unit user, SkillContext context)
            {
                user.DisplaceTarget(
                    context.PrimaryTargetUnit,
                    context.CellGrid,
                    distance: 1,
                    push: true,
                    moveUserWithTarget: false);
            }
        }
    }
}
