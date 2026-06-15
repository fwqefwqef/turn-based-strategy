using System;
using System.Collections.Generic;
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
            SkillEffectRegistry.Register("heal_res_10", () => new ResistanceScalingHealSkillEffect(10));
            SkillEffectRegistry.Register("heal_res_25", () => new ResistanceScalingHealSkillEffect(25));
            SkillEffectRegistry.Register("sacrifice_heal_res_25", () => new SacrificeHealSkillEffect(25));
            SkillEffectRegistry.Register("immolate", () => new ImmolateSkillEffect());
            SkillEffectRegistry.Register("ignore_def_res", () => new IgnoreDefResSkillEffect());
            SkillEffectRegistry.Register("shove", () => new ShoveSkillEffect());

            isRegistered = true;
        }

        private sealed class RestoreHitPointsSkillEffect : ISkillEffect
        {
            private readonly int amount;

            public RestoreHitPointsSkillEffect(int amount)
            {
                this.amount = amount;
            }

            public bool CanUse(CustomUnit user, SkillContext context)
            {
                var target = context?.PrimaryTargetUnit;
                return user != null
                    && target != null
                    && target.HitPoints > 0
                    && target.HitPoints < target.ComputedTotalHitPoints;
            }

            public void Use(CustomUnit user, SkillContext context)
            {
                context?.PrimaryTargetUnit?.RestoreHitPoints(amount, user);
            }
        }

        private sealed class ResistanceScalingHealSkillEffect : IHealingSkillEffect
        {
            private readonly int baseAmount;

            public ResistanceScalingHealSkillEffect(int baseAmount)
            {
                this.baseAmount = baseAmount;
            }

            public bool CanUse(CustomUnit user, SkillContext context)
            {
                var target = context?.PrimaryTargetUnit;
                return user != null
                    && target != null
                    && target.PlayerNumber == user.PlayerNumber
                    && target.HitPoints > 0
                    && target.HitPoints < target.ComputedTotalHitPoints;
            }

            public int GetHealingAmount(CustomUnit user, SkillContext context)
            {
                if (user == null || context?.PrimaryTargetUnit == null)
                {
                    return 0;
                }

                return Mathf.Max(0, user.Resistance + baseAmount);
            }

            public void Use(CustomUnit user, SkillContext context)
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

            public bool CanUse(CustomUnit user, SkillContext context)
            {
                var target = context?.PrimaryTargetUnit;
                return user != null
                    && user.HitPoints > 1
                    && target != null
                    && target != user
                    && target.PlayerNumber == user.PlayerNumber
                    && target.HitPoints > 0
                    && target.HitPoints < target.ComputedTotalHitPoints;
            }

            public int GetHealingAmount(CustomUnit user, SkillContext context)
            {
                if (user == null || context?.PrimaryTargetUnit == null)
                {
                    return 0;
                }

                return Mathf.Max(0, user.Resistance + baseAmount);
            }

            public void Use(CustomUnit user, SkillContext context)
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

        private sealed class IgnoreDefResSkillEffect : ISkillEffect
        {
            public bool CanUse(CustomUnit user, SkillContext context)
            {
                return user != null
                    && context?.PrimaryTargetUnit != null
                    && context.PrimaryTargetUnit.PlayerNumber != user.PlayerNumber
                    && context.PrimaryTargetUnit.HitPoints > 0;
            }

            public void Use(CustomUnit user, SkillContext context)
            {
                user?.BuffAdd("luna");
            }
        }

        private sealed class ImmolateSkillEffect : IAttackSkillEffect
        {
            public bool CanUse(CustomUnit user, SkillContext context)
            {
                return user != null && user.HitPoints > GetHealthCost(user);
            }

            public void ModifyAttackProfile(CustomUnit user, SkillContext context, ref ResolvedAttackProfile profile)
            {
                if (user == null)
                {
                    return;
                }

                profile.Damage += GetHealthCost(user) * 2;
            }

            public void Use(CustomUnit user, SkillContext context)
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

            private static int GetHealthCost(CustomUnit user)
            {
                return user == null ? 0 : Mathf.Max(0, user.ComputedTotalHitPoints / 2);
            }
        }

        private sealed class ShoveSkillEffect : ISkillEffect
        {
            public bool CanUse(CustomUnit user, SkillContext context)
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

            public void Use(CustomUnit user, SkillContext context)
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
