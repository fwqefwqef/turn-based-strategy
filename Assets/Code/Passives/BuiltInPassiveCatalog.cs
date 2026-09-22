using System;
using System.Collections.Generic;
using Windy.Srpg.Game.Catalogs;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Skills;
using Windy.Srpg.Game.Units;
using UnityEngine;

namespace Windy.Srpg.Game.Passives
{
    public static class BuiltInPassiveCatalog
    {
        private static bool isRegistered;

        public static void EnsureRegistered()
        {
            if (isRegistered)
            {
                return;
            }

            PassiveCatalogResource catalog = CatalogResourceLoader.LoadPassiveCatalog();
            PassiveRegistry.RegisterRange(catalog.ToRuntimeDefinitions());

            PassiveEffectRegistry.Register("restore_hp_3_turn_start", () => new RestoreHitPointsOnTurnStartEffect(3));
            PassiveEffectRegistry.Register("exp_x2", () => new MultiplyExperienceGainEffect(2f));
            PassiveEffectRegistry.Register("exp_set_100", () => new SetExperienceGainEffect(100));
            PassiveEffectRegistry.Register("prevent_exp_to_attackers", () => new PreventExperienceToAttackersEffect());
            PassiveEffectRegistry.Register("fortress", () => new FortressEffect());
            PassiveEffectRegistry.Register("vantage", () => new VantageEffect());
            PassiveEffectRegistry.Register("wrath_missing_hp_crit", () => new WrathEffect());
            PassiveEffectRegistry.Register("accelerated_movement", () => new AcceleratedMovementEffect());
            PassiveEffectRegistry.Register("indomitable", () => new IndomitableEffect());
            PassiveEffectRegistry.Register("joy_of_burning", () => new JoyOfBurningEffect());
            PassiveEffectRegistry.Register("cursed_existence", () => new CursedExistenceEffect());
            PassiveEffectRegistry.Register("soul_stealer", () => new SoulStealerEffect());
            PassiveEffectRegistry.Register("sadist", () => new SadistEffect());
            PassiveEffectRegistry.Register("penetrate", () => new PenetrateEffect());
            PassiveEffectRegistry.Register("nurse_compassion", () => new NurseCompassionEffect());
            PassiveEffectRegistry.Register("apotheosis", () => new ApotheosisEffect());
            isRegistered = true;
        }

        private sealed class IndomitableEffect : PassiveEffectBase, IP_AttackSurvivalGuard
        {
            private bool usedThisTurn;

            public override void OnTurnStart(Unit unit, Passive entry)
            {
                usedThisTurn = false;
            }

            public int LimitAttackDamage(Unit defender, int damage, bool simulateOnly)
            {
                if (usedThisTurn || defender == null || defender.HitPoints <= 0
                    || damage < defender.HitPoints || damage <= 0)
                    return damage;

                if (!simulateOnly) usedThisTurn = true;
                return Mathf.Max(0, defender.HitPoints - 1);
            }
        }

        private sealed class CursedExistenceEffect : PassiveEffectBase, IP_AttackHitEffect
        {
            public void OnAttackHit(Unit attacker, Unit defender, int damageDealt, bool isBasicAttack)
            {
                if (isBasicAttack && attacker != null && defender != null
                    && attacker.PlayerNumber != defender.PlayerNumber && defender.IsAliveForBattle)
                    defender.AddBuffById("curse");
            }
        }

        private sealed class SoulStealerEffect : PassiveEffectBase, IP_AttackNeverMisses, IP_AttackHitEffect
        {
            public void OnAttackHit(Unit attacker, Unit defender, int damageDealt, bool isBasicAttack)
            {
                if (attacker == null || defender == null || attacker.PlayerNumber == defender.PlayerNumber
                    || damageDealt <= 0) return;

                int drained = Mathf.Min(defender.CurrentManaPoints, damageDealt / 2);
                if (drained <= 0) return;
                defender.SetCurrentManaPoints(defender.CurrentManaPoints - drained);
                attacker.RestoreManaPoints(drained);
            }
        }

        private sealed class JoyOfBurningEffect : PassiveEffectBase, IP_DynamicPrimaryStatModifier
        {
            public PrimaryStatModifiers GetPrimaryStatModifiers(Unit unit)
            {
                int bonus = 2 * (unit?.CountBurningEnemies() ?? 0);
                return new PrimaryStatModifiers { Strength = bonus, Magic = bonus };
            }
        }

        private sealed class SadistEffect : PassiveEffectBase, IP_DynamicPrimaryStatModifier
        {
            public PrimaryStatModifiers GetPrimaryStatModifiers(Unit unit)
            {
                int bonus = unit?.CountDamagedEnemies() ?? 0;
                return new PrimaryStatModifiers { Attack = bonus };
            }
        }

        private sealed class PenetrateEffect : PassiveEffectBase, IP_DamageChange
        {
            public void DamageChange(DamageChangeContext context)
            {
                if (context != null && context.Phase == DamageChangePhase.Damage && context.IsHit
                    && context.IsMagicAttack && context.Defender != null)
                    context.Damage += Mathf.FloorToInt(context.Defender.Magic * 0.5f);
            }
        }

        private sealed class NurseCompassionEffect : PassiveEffectBase, IP_HealingPerformed
        {
            public void OnHealingPerformed(Unit healer, Unit target, int actualAmount)
            {
                if (healer == null || target == null || target == healer
                    || target.PlayerNumber != healer.PlayerNumber || actualAmount <= 0) return;
                healer.RestoreHitPoints(actualAmount / 2, healer);
            }
        }

        private sealed class ApotheosisEffect : PassiveEffectBase, IP_SpellMaxRangeModifier
        {
            public int GetSpellMaxRangeModifier(Unit unit, SkillData skill)
            {
                return skill != null
                    && (skill.Category == SkillCategory.Spell || skill.Category == SkillCategory.AreaSpell)
                    ? 2
                    : 0;
            }
        }

        private sealed class AcceleratedMovementEffect : PassiveEffectBase
        {
        }

        private sealed class VantageEffect : PassiveEffectBase, IP_Vantage
        {
        }

        private sealed class WrathEffect : PassiveEffectBase, IP_DynamicSecondaryStatModifier
        {
            public SecondaryStatModifiers GetSecondaryStatModifiers(Unit unit)
            {
                if (unit == null)
                {
                    return default;
                }

                int currentHitPoints = Mathf.Min(unit.HitPoints, unit.MaxHitPoints);
                int missingHitPoints = Mathf.Max(0, unit.MaxHitPoints - currentHitPoints);
                return new SecondaryStatModifiers { Crit = missingHitPoints * 2 };
            }
        }

        private sealed class FortressEffect : PassiveEffectBase, IP_TakeDamageChange, IP_PainDamageChange, IP_TurnStartHealthEffect
        {
            public void TakeDamageChange(DamageChangeContext context)
            {
                if (context != null && context.Phase == DamageChangePhase.Damage && context.Damage > 0)
                {
                    context.Damage = Mathf.Max(0, context.Damage - 5);
                }
            }

            public int ModifyPainDamage(Unit unit, int damage)
            {
                return damage <= 0 ? 0 : Mathf.Max(0, damage - 5);
            }

            public int GetTurnStartHealthDelta(Unit unit)
            {
                return unit == null ? 0 : Mathf.CeilToInt(unit.MaxHitPoints * 0.2f);
            }
        }

        private sealed class RestoreHitPointsOnTurnStartEffect : PassiveEffectBase
        {
            private readonly int amount;

            public RestoreHitPointsOnTurnStartEffect(int amount)
            {
                this.amount = amount;
            }

            public override void OnTurnStart(Unit unit, Passive entry)
            {
                unit?.RestoreHitPoints(amount, unit);
            }
        }

        private sealed class MultiplyExperienceGainEffect : PassiveEffectBase, IP_ModifyExperienceGain
        {
            private readonly float multiplier;

            public MultiplyExperienceGainEffect(float multiplier)
            {
                this.multiplier = multiplier;
            }

            public void ModifyExperienceGain(ExperienceGainContext context)
            {
                if (context == null || context.Recipient != Owner || context.Amount <= 0)
                {
                    return;
                }

                context.Amount = Mathf.FloorToInt(context.Amount * multiplier);
            }
        }

        private sealed class PreventExperienceToAttackersEffect : PassiveEffectBase, IP_PreventExperienceGain
        {
            public void PreventExperienceGain(ExperienceGainContext context)
            {
                if (context == null)
                {
                    return;
                }

                context.Prevented = true;
                context.Amount = 0;
            }
        }

        private sealed class SetExperienceGainEffect : PassiveEffectBase, IP_ModifyExperienceGain
        {
            private readonly int amount;

            public SetExperienceGainEffect(int amount)
            {
                this.amount = amount;
            }

            public void ModifyExperienceGain(ExperienceGainContext context)
            {
                if (context == null || context.Recipient != Owner || context.Amount <= 0)
                {
                    return;
                }

                context.Amount = amount;
            }
        }
    }
}

