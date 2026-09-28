using Windy.Srpg.Game.Catalogs;
using Windy.Srpg.Game.Units;
using UnityEngine;

namespace Windy.Srpg.Game.Skills
{
    public static class SkillEffects
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
            SkillHealProfile profile = context.Skill.HealProfile;
            int amount = profile.Might + (profile.ScalesWithMagic ? user.Magic : 0);
            amount = Mathf.Max(0, amount);
            return profile.DoubleAtDeathsDoor && context.PrimaryTargetUnit.HitPoints <= 0
                ? amount * 2
                : amount;
        }

        private sealed class PunishSkillEffect : SkillEffectBase, IP_AttackHitEffect
        {
            protected override void Use() { }

            public void OnAttackHit(Unit attacker, Unit defender, int damageDealt, bool isBasicAttack)
            {
                defender.AddBuffById("punished", attacker);
            }
        }

        private sealed class IceSpikesSkillEffect : SkillEffectBase
        {
            protected override void Use() => ApplyStatusToEnemies("ice_spikes_slow");
        }

        private sealed class ReturnToHellSkillEffect : SkillEffectBase, IAreaAttackTargetModifier
        {
            protected override void Use() { }

            public void ModifyAttackProfileForTarget(Unit user, SkillContext context, ref ResolvedAttackProfile profile)
            {
                Unit target = context.PrimaryTargetUnit;
                if (target.HitPoints * 2 <= target.ComputedTotalHitPoints)
                {
                    int defense = profile.IsMagic ? target.Magic : target.Defense;
                    int normalDamage = Mathf.Max(1, profile.Damage - defense);
                    profile.Damage = defense + normalDamage * 2;
                }
            }
        }

        private sealed class BashLifeStealEffect : SkillEffectBase, IP_AttackHitEffect
        {
            protected override void Use() { }

            public void OnAttackHit(Unit attacker, Unit defender, int damageDealt, bool isBasicAttack)
            {
                if (damageDealt > 0)
                    attacker.RestoreHitPoints(damageDealt / 2, attacker);
            }
        }

        private sealed class ShiningPillarEffect : SkillEffectBase, IAreaHitPointChangeSkillEffect
        {
            protected override bool RequiresTarget => true;

            public int GetProjectedHitPointDelta(Unit user, SkillContext context)
            {
                Unit target = context.PrimaryTargetUnit;
                int power = GetProfileHealingAmount(user, context);
                return target.PlayerNumber == user.PlayerNumber
                    ? Mathf.Min(power, Mathf.Max(0, target.ComputedTotalHitPoints - target.HitPoints))
                    : -Mathf.Min(target.HitPoints, Mathf.Max(1, power - target.Magic));
            }

            protected override void Use()
            {
                int power = GetProfileHealingAmount(User, Context);
                if (Target.PlayerNumber == User.PlayerNumber)
                {
                    Target.RestoreHitPoints(power, User);
                    return;
                }

                Target.DefendHandler(
                    User,
                    power,
                    User.Speed * Unit.AccuracyPerSpeedPoint + 100,
                    User.Luck * 5,
                    isMagicAttack: true,
                    isCounterAttack: false,
                    simulateOnly: false,
                    applyWeaponEffects: false,
                    isAreaSpell: true);
            }

        }

        private sealed class RefreshActionSkillEffect : SkillEffectBase
        {
            protected override bool MeetsAdditionalUseConditions() =>
                Target.IsFinishedForTurn && !Target.IsActionBlocked;

            protected override void Use() => Target.RefreshAction();
        }

        private sealed class BurnAreaTargetsSkillEffect : SkillEffectBase
        {
            protected override void Use() => ApplyStatusToEnemies("burn");
        }

        private sealed class InsurmountableSkillEffect : SkillEffectBase
        {
            protected override void Use()
            {
                if (User.HitPoints <= 0)
                    User.RestoreHitPoints(1 - User.HitPoints, User);

                ApplyStatusToSelf("flame_last_stand");
            }
        }

        private sealed class ProfileHealSkillEffect : SkillEffectBase, IHealingSkillEffect
        {
            protected override bool MeetsAdditionalUseConditions() =>
                Context.Skill.HealProfile.Enabled && Target.HitPoints < Target.ComputedTotalHitPoints;

            public int GetHealingAmount(Unit user, SkillContext context)
            {
                return GetProfileHealingAmount(user, context);
            }

            protected override void Use()
            {
                int amount = GetProfileHealingAmount(User, Context);
                if (amount > 0) Target.RestoreHitPoints(amount, User);
            }
        }

        private sealed class AnathemaSkillEffect : SkillEffectBase
        {
            protected override void Use() => ApplyStatusToTarget("curse", stacks: 2);
        }

        private sealed class SlowSkillEffect : SkillEffectBase
        {
            protected override void Use() => ApplyStatusToTarget("slow");
        }

        private sealed class StormSurgeSkillEffect : SkillEffectBase
        {
            protected override void Use() => ApplyStatusToSelf("storm_surge");
        }

        private sealed class ClangSkillEffect : SkillEffectBase, IP_CancelCounterattackOnHit
        {
            private Grid.CellGrid grid;
            public bool HitLanded { get; private set; }
            protected override bool RequiresGrid => true;

            protected override void Use() => grid = Grid;

            public void OnAttackHit(Unit attacker, Unit defender, int damageDealt, bool isBasicAttack)
            {
                HitLanded = true;
                if (defender.IsAliveForBattle)
                    attacker.DisplaceTarget(defender, grid, distance: 2, push: true);
            }
        }

        private sealed class DarkSanctuarySkillEffect : SkillEffectBase
        {
            protected override bool RequiresTarget => true;

            protected override void Use() => ApplyStatusToTarget("dark_sanctuary");
        }

        private sealed class CleanseSkillEffect : SkillEffectBase
        {
            protected override bool MeetsAdditionalUseConditions() => Target.HasRemovableDebuffs;

            protected override void Use() => Target.RemoveRemovableDebuffs();
        }

        private sealed class BreakCurrentHitPointsEffect : SkillEffectBase, IAttackSkillEffect
        {
            public void ModifyAttackProfile(Unit user, SkillContext context, ref ResolvedAttackProfile profile)
            {
                profile.Damage += Mathf.Max(0, user.HitPoints);
            }

            protected override void Use() { }
        }

        private sealed class SacrificeHealSkillEffect : SkillEffectBase, IHealingSkillEffect
        {
            private bool appliedSelfCost;

            protected override bool MeetsAdditionalUseConditions() =>
                User.HitPoints > 1 && Target.HitPoints < Target.ComputedTotalHitPoints;

            public int GetHealingAmount(Unit user, SkillContext context)
            {
                return GetProfileHealingAmount(user, context);
            }

            protected override void Use()
            {
                if (!appliedSelfCost)
                {
                    appliedSelfCost = true;
                    User.SetCurrentHitPoints(Mathf.Max(1, User.HitPoints > 0 ? 1 : 0), User);
                }

                int healingAmount = GetProfileHealingAmount(User, Context);
                if (healingAmount <= 0)
                {
                    return;
                }

                Target.RestoreHitPoints(healingAmount, User);
            }
        }

        private sealed class IgnoreDefMagSkillEffect : SkillEffectBase
        {
            protected override void Use() => User.BuffAdd("luna");
        }

        private sealed class ImmolateSkillEffect : SkillEffectBase, IAttackSkillEffect
        {
            protected override bool MeetsAdditionalUseConditions() => User.HitPoints > GetHealthCost(User);

            public void ModifyAttackProfile(Unit user, SkillContext context, ref ResolvedAttackProfile profile)
            {
                profile.Damage += GetHealthCost(user) * 2;
            }

            protected override void Use()
            {
                int healthCost = GetHealthCost(User);
                if (healthCost <= 0)
                {
                    return;
                }

                User.SetCurrentHitPoints(User.HitPoints - healthCost, User);
            }

            private static int GetHealthCost(Unit user) => Mathf.Max(0, user.ComputedTotalHitPoints / 2);
        }

        private sealed class ShoveSkillEffect : SkillEffectBase
        {
            protected override bool RequiresGrid => true;

            protected override bool MeetsAdditionalUseConditions()
            {
                var actingCell = User.HasPendingMove ? User.PreviewCell : User.Cell;
                var targetCell = Target.HasPendingMove ? Target.PreviewCell : Target.Cell;
                return UnitDisplacementUtility.CanDisplaceRelative(
                    User,
                    actingCell,
                    Target,
                    targetCell,
                    Grid,
                    distance: 1,
                    push: true,
                    moveUserWithTarget: false);
            }

            protected override void Use()
            {
                User.DisplaceTarget(
                    Target,
                    Grid,
                    distance: 1,
                    push: true,
                    moveUserWithTarget: false);
            }
        }
    }
}
