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

            SkillEffectRegistry.Register("regen_self_10", () => new RestoreHitPointsSkillEffect(10));
            SkillEffectRegistry.Register("heal_mag_10", () => new MagicScalingHealSkillEffect(10));
            SkillEffectRegistry.Register("heal_mag_25", () => new MagicScalingHealSkillEffect(25));
            SkillEffectRegistry.Register("sacrifice_heal_mag_25", () => new SacrificeHealSkillEffect(25));
            SkillEffectRegistry.Register("immolate", () => new ImmolateSkillEffect());
            SkillEffectRegistry.Register("ignore_def_mag", () => new IgnoreDefMagSkillEffect());
            SkillEffectRegistry.Register("shove", () => new ShoveSkillEffect());
            SkillEffectRegistry.Register("cleanse", () => new CleanseSkillEffect());
            SkillEffectRegistry.Register("break_current_hp", () => new BreakCurrentHitPointsEffect());
            SkillEffectRegistry.Register("refresh_action", () => new RefreshActionSkillEffect());
            SkillEffectRegistry.Register("burn_area_targets", () => new BurnAreaTargetsSkillEffect());
            SkillEffectRegistry.Register("flame_last_stand", () => new InsurmountableSkillEffect());
            SkillEffectRegistry.Register("dark_heal", () => new DarkHealSkillEffect());
            SkillEffectRegistry.Register("anathema", () => new AnathemaSkillEffect());
            SkillEffectRegistry.Register("slow", () => new SlowSkillEffect());
            SkillEffectRegistry.Register("storm_surge", () => new StormSurgeSkillEffect());
            SkillEffectRegistry.Register("clang", () => new ClangSkillEffect());
            SkillEffectRegistry.Register("dark_sanctuary", () => new DarkSanctuarySkillEffect());
            SkillEffectRegistry.Register("punish", () => new PunishSkillEffect());
            SkillEffectRegistry.Register("ice_spikes", () => new IceSpikesSkillEffect());
            SkillEffectRegistry.Register("bash_lifesteal", () => new BashLifeStealEffect());
            SkillEffectRegistry.Register("shining_pillar", () => new ShiningPillarEffect());

            isRegistered = true;
        }

        private sealed class PunishSkillEffect : ISkillEffect, IP_AttackHitEffect
        {
            public bool CanUse(Unit user, SkillContext context) => user != null
                && context?.PrimaryTargetUnit != null
                && context.PrimaryTargetUnit.PlayerNumber != user.PlayerNumber;

            public void Use(Unit user, SkillContext context) { }

            public void OnAttackHit(Unit attacker, Unit defender, int damageDealt, bool isBasicAttack)
            {
                if (attacker != null && defender != null && attacker.PlayerNumber != defender.PlayerNumber)
                    defender.AddBuffById("punished");
            }
        }

        private sealed class IceSpikesSkillEffect : ISkillEffect
        {
            public bool CanUse(Unit user, SkillContext context) => user != null && context?.AreaTargets != null;

            public void Use(Unit user, SkillContext context)
            {
                if (!CanUse(user, context)) return;
                foreach (Unit target in context.AreaTargets.Distinct())
                {
                    if (target != null && target.IsAliveForBattle && target.PlayerNumber != user.PlayerNumber)
                        target.AddBuffById("ice_spikes_slow");
                }
            }
        }

        private sealed class BashLifeStealEffect : ISkillEffect, IP_AttackHitEffect
        {
            public bool CanUse(Unit user, SkillContext context) => user != null
                && context?.PrimaryTargetUnit != null
                && context.PrimaryTargetUnit.PlayerNumber != user.PlayerNumber;

            public void Use(Unit user, SkillContext context) { }

            public void OnAttackHit(Unit attacker, Unit defender, int damageDealt, bool isBasicAttack)
            {
                if (attacker != null && defender != null && damageDealt > 0)
                    attacker.RestoreHitPoints(damageDealt / 2, attacker);
            }
        }

        private sealed class ShiningPillarEffect : IAreaHitPointChangeSkillEffect
        {
            public bool CanUse(Unit user, SkillContext context)
            {
                Unit target = context?.PrimaryTargetUnit;
                return user != null && target != null && target.IsAliveForBattle;
            }

            public int GetProjectedHitPointDelta(Unit user, SkillContext context)
            {
                if (!CanUse(user, context)) return 0;
                Unit target = context.PrimaryTargetUnit;
                int power = GetPower(user);
                return target.PlayerNumber == user.PlayerNumber
                    ? Mathf.Min(power, Mathf.Max(0, target.ComputedTotalHitPoints - target.HitPoints))
                    : -Mathf.Min(target.HitPoints, Mathf.Max(1, power - target.Magic));
            }

            public void Use(Unit user, SkillContext context)
            {
                if (!CanUse(user, context)) return;
                Unit target = context.PrimaryTargetUnit;
                int power = GetPower(user);
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

            private static int GetPower(Unit user) => user == null ? 0 : Mathf.Max(0, user.Magic + 10);
        }

        private sealed class RefreshActionSkillEffect : ISkillEffect
        {
            public bool CanUse(Unit user, SkillContext context)
            {
                Unit target = context?.PrimaryTargetUnit;
                return user != null
                    && target != null
                    && target != user
                    && target.PlayerNumber == user.PlayerNumber
                    && target.IsAliveForBattle
                    && target.IsFinishedForTurn
                    && !target.IsActionBlocked;
            }

            public void Use(Unit user, SkillContext context)
            {
                if (CanUse(user, context))
                {
                    context.PrimaryTargetUnit.RefreshAction();
                }
            }
        }

        private sealed class BurnAreaTargetsSkillEffect : ISkillEffect
        {
            public bool CanUse(Unit user, SkillContext context)
            {
                return user != null && context?.AreaTargets != null;
            }

            public void Use(Unit user, SkillContext context)
            {
                if (!CanUse(user, context)) return;
                var seen = new System.Collections.Generic.HashSet<Unit>();
                foreach (Unit target in context.AreaTargets)
                {
                    if (target != null && target.PlayerNumber != user.PlayerNumber
                        && target.IsAliveForBattle && seen.Add(target))
                        target.AddBuffById("burn");
                }
            }
        }

        private sealed class InsurmountableSkillEffect : ISkillEffect
        {
            public bool CanUse(Unit user, SkillContext context)
            {
                return user != null && user.IsAliveForBattle
                    && context?.PrimaryTargetUnit == user;
            }

            public void Use(Unit user, SkillContext context)
            {
                if (!CanUse(user, context)) return;

                if (user.HitPoints <= 0)
                    user.RestoreHitPoints(1 - user.HitPoints, user);

                user.AddBuffById("flame_last_stand");
            }
        }

        private sealed class DarkHealSkillEffect : IHealingSkillEffect
        {
            public bool CanUse(Unit user, SkillContext context)
            {
                Unit target = context?.PrimaryTargetUnit;
                return user != null && target != null && target.IsAliveForBattle
                    && target.PlayerNumber == user.PlayerNumber
                    && target.HitPoints < target.ComputedTotalHitPoints;
            }

            public int GetHealingAmount(Unit user, SkillContext context)
            {
                if (!CanUse(user, context)) return 0;
                int amount = Mathf.Max(0, user.Magic + 10);
                return context.PrimaryTargetUnit.HitPoints <= 0 ? amount * 2 : amount;
            }

            public void Use(Unit user, SkillContext context)
            {
                int amount = GetHealingAmount(user, context);
                if (amount > 0) context.PrimaryTargetUnit.RestoreHitPoints(amount, user);
            }
        }

        private sealed class AnathemaSkillEffect : ISkillEffect
        {
            public bool CanUse(Unit user, SkillContext context)
            {
                Unit target = context?.PrimaryTargetUnit;
                return user != null && target != null && target.IsAliveForBattle
                    && target.PlayerNumber != user.PlayerNumber;
            }

            public void Use(Unit user, SkillContext context)
            {
                if (!CanUse(user, context)) return;
                for (int i = 0; i < 2; i++) context.PrimaryTargetUnit.AddBuffById("curse");
            }
        }

        private sealed class SlowSkillEffect : ISkillEffect
        {
            public bool CanUse(Unit user, SkillContext context)
            {
                Unit target = context?.PrimaryTargetUnit;
                return user != null && target != null && target.IsAliveForBattle
                    && target.PlayerNumber != user.PlayerNumber;
            }

            public void Use(Unit user, SkillContext context)
            {
                if (CanUse(user, context)) context.PrimaryTargetUnit.AddBuffById("slow");
            }
        }

        private sealed class StormSurgeSkillEffect : ISkillEffect
        {
            public bool CanUse(Unit user, SkillContext context) =>
                user != null && user.IsAliveForBattle && context?.PrimaryTargetUnit == user;

            public void Use(Unit user, SkillContext context)
            {
                if (CanUse(user, context)) user.AddBuffById("storm_surge");
            }
        }

        private sealed class ClangSkillEffect : ISkillEffect, IP_CancelCounterattackOnHit
        {
            private Grid.CellGrid grid;
            public bool HitLanded { get; private set; }

            public bool CanUse(Unit user, SkillContext context)
            {
                Unit target = context?.PrimaryTargetUnit;
                return user != null && target != null && target.IsAliveForBattle
                    && target.PlayerNumber != user.PlayerNumber && context.CellGrid != null;
            }

            public void Use(Unit user, SkillContext context)
            {
                if (CanUse(user, context)) grid = context.CellGrid;
            }

            public void OnAttackHit(Unit attacker, Unit defender, int damageDealt, bool isBasicAttack)
            {
                HitLanded = true;
                if (grid != null && attacker != null && defender != null && defender.IsAliveForBattle)
                    attacker.DisplaceTarget(defender, grid, distance: 2, push: true);
            }
        }

        private sealed class DarkSanctuarySkillEffect : ISkillEffect
        {
            public bool CanUse(Unit user, SkillContext context)
            {
                Unit target = context?.PrimaryTargetUnit;
                return user != null && target != null && target.IsAliveForBattle
                    && target.PlayerNumber == user.PlayerNumber;
            }

            public void Use(Unit user, SkillContext context)
            {
                if (CanUse(user, context)) context.PrimaryTargetUnit.AddBuffById("dark_sanctuary");
            }
        }

        private sealed class CleanseSkillEffect : ISkillEffect
        {
            public bool CanUse(Unit user, SkillContext context)
            {
                Unit target = context?.PrimaryTargetUnit;
                return user != null && target != null && target.IsAliveForBattle
                    && target.PlayerNumber == user.PlayerNumber && target.HasRemovableDebuffs;
            }

            public void Use(Unit user, SkillContext context)
            {
                if (CanUse(user, context)) context.PrimaryTargetUnit.RemoveRemovableDebuffs();
            }
        }

        private sealed class BreakCurrentHitPointsEffect : IAttackSkillEffect
        {
            public bool CanUse(Unit user, SkillContext context)
            {
                return user != null && context?.PrimaryTargetUnit != null
                    && context.PrimaryTargetUnit.PlayerNumber != user.PlayerNumber;
            }

            public void ModifyAttackProfile(Unit user, SkillContext context, ref ResolvedAttackProfile profile)
            {
                if (user != null) profile.Damage += Mathf.Max(0, user.HitPoints);
            }

            public void Use(Unit user, SkillContext context) { }
        }

        private sealed class RestoreHitPointsSkillEffect : ISkillEffect
        {
            private readonly int amount;

            public RestoreHitPointsSkillEffect(int amount)
            {
                this.amount = amount;
            }

            public bool CanUse(Unit user, SkillContext context)
            {
                var target = context?.PrimaryTargetUnit;
                return user != null
                    && target != null
                    && target.IsAliveForBattle
                    && target.HitPoints < target.ComputedTotalHitPoints;
            }

            public void Use(Unit user, SkillContext context)
            {
                context?.PrimaryTargetUnit?.RestoreHitPoints(amount, user);
            }
        }

        private sealed class MagicScalingHealSkillEffect : IHealingSkillEffect
        {
            private readonly int baseAmount;

            public MagicScalingHealSkillEffect(int baseAmount)
            {
                this.baseAmount = baseAmount;
            }

            public bool CanUse(Unit user, SkillContext context)
            {
                var target = context?.PrimaryTargetUnit;
                return user != null
                    && target != null
                    && target.PlayerNumber == user.PlayerNumber
                    && target.IsAliveForBattle
                    && target.HitPoints < target.ComputedTotalHitPoints;
            }

            public int GetHealingAmount(Unit user, SkillContext context)
            {
                if (user == null || context?.PrimaryTargetUnit == null)
                {
                    return 0;
                }

                return Mathf.Max(0, user.Magic + baseAmount);
            }

            public void Use(Unit user, SkillContext context)
            {
                int healingAmount = GetHealingAmount(user, context);
                if (healingAmount <= 0)
                {
                    return;
                }

                context?.PrimaryTargetUnit?.RestoreHitPoints(healingAmount, user);
            }
        }

        private sealed class SacrificeHealSkillEffect : IHealingSkillEffect
        {
            private readonly int baseAmount;
            private bool appliedSelfCost;

            public SacrificeHealSkillEffect(int baseAmount)
            {
                this.baseAmount = baseAmount;
            }

            public bool CanUse(Unit user, SkillContext context)
            {
                var target = context?.PrimaryTargetUnit;
                return user != null
                    && user.HitPoints > 1
                    && target != null
                    && target != user
                    && target.PlayerNumber == user.PlayerNumber
                    && target.IsAliveForBattle
                    && target.HitPoints < target.ComputedTotalHitPoints;
            }

            public int GetHealingAmount(Unit user, SkillContext context)
            {
                if (user == null || context?.PrimaryTargetUnit == null)
                {
                    return 0;
                }

                return Mathf.Max(0, user.Magic + baseAmount);
            }

            public void Use(Unit user, SkillContext context)
            {
                if (user == null || context?.PrimaryTargetUnit == null)
                {
                    return;
                }

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

        private sealed class IgnoreDefMagSkillEffect : ISkillEffect
        {
            public bool CanUse(Unit user, SkillContext context)
            {
                return user != null
                    && context?.PrimaryTargetUnit != null
                    && context.PrimaryTargetUnit.PlayerNumber != user.PlayerNumber
                    && context.PrimaryTargetUnit.IsAliveForBattle;
            }

            public void Use(Unit user, SkillContext context)
            {
                user?.BuffAdd("luna");
            }
        }

        private sealed class ImmolateSkillEffect : IAttackSkillEffect
        {
            public bool CanUse(Unit user, SkillContext context)
            {
                return user != null && user.HitPoints > GetHealthCost(user);
            }

            public void ModifyAttackProfile(Unit user, SkillContext context, ref ResolvedAttackProfile profile)
            {
                if (user == null)
                {
                    return;
                }

                profile.Damage += GetHealthCost(user) * 2;
            }

            public void Use(Unit user, SkillContext context)
            {
                if (user == null)
                {
                    return;
                }

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

        private sealed class ShoveSkillEffect : ISkillEffect
        {
            public bool CanUse(Unit user, SkillContext context)
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

            public void Use(Unit user, SkillContext context)
            {
                if (user == null || context?.PrimaryTargetUnit == null || context.CellGrid == null)
                {
                    return;
                }

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

