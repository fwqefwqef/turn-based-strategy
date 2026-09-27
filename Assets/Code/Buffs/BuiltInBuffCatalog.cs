using Windy.Srpg.Game.Catalogs;
using Windy.Srpg.Game.Units;
using UnityEngine;

namespace Windy.Srpg.Game.Buffs
{
    public static class BuiltInBuffCatalog
    {
        private static bool isRegistered;

        public static void EnsureRegistered()
        {
            if (isRegistered)
            {
                return;
            }

            BuffCatalogResource catalog = CatalogResourceLoader.LoadBuffCatalog();
            BuffRegistry.RegisterRange(catalog.ToRuntimeDefinitions());

            BuffEffectRegistry.Register("damage_to_one", () => new DamageToOneBuffEffect());
            BuffEffectRegistry.Register("ignore_def_mag", () => new IgnoreDefMag());
            BuffEffectRegistry.Register("toxic", () => new ToxicBuffEffect());
            BuffEffectRegistry.Register("burn", () => new BurnBuffEffect());
            BuffEffectRegistry.Register("stun", () => new StunBuffEffect());
            BuffEffectRegistry.Register("movement_cap_1", () => new MovementCapBuffEffect(1f));
            BuffEffectRegistry.Register("black_fog", () => new BlackFogBuffEffect());
            BuffEffectRegistry.Register("flame_last_stand", () => new InsurmountableEffect());
            BuffEffectRegistry.Register("storm_surge", () => new StormSurgeEffect());
            BuffEffectRegistry.Register("dark_sanctuary", () => new DarkSanctuaryEffect());
            BuffEffectRegistry.Register("slow", () => new SlowEffect());
            BuffEffectRegistry.Register("movement_cap_0", () => new MovementCapZeroEffect());
            BuffEffectRegistry.Register("ice_spikes_slow", () => new MovementPenaltyEffect(2f));
            isRegistered = true;
        }

        private sealed class InsurmountableEffect : BuffEffectBase, IP_AttackSurvivalGuard
        {
            public int LimitAttackDamage(Unit defender, int damage, bool simulateOnly)
            {
                return defender == null || defender.HitPoints <= 0
                    ? damage
                    : Mathf.Min(damage, Mathf.Max(0, defender.HitPoints - 1));
            }
        }

        private sealed class StormSurgeEffect : BuffEffectBase, IP_DamageChange, IP_AfterCombat_Attacker, IP_AfterCombat_Defender
        {
            public void DamageChange(DamageChangeContext context)
            {
                if (context != null && context.Phase == DamageChangePhase.Damage && context.IsHit)
                    context.Damage += 10;
            }

            public void AfterCombatSequenceAsAttacker(CombatSequenceContext context) => SelfRemove();
            public void AfterCombatSequenceAsDefender(CombatSequenceContext context) => SelfRemove();
        }

        private sealed class DarkSanctuaryEffect : BuffEffectBase, IP_TakeDamageMultiplier
        {
            public void TakeDamageMultiplier(DamageChangeContext context)
            {
                if (context != null && context.Phase == DamageChangePhase.Damage && context.IsAreaSpell)
                    context.Damage = Mathf.CeilToInt(context.Damage * 0.5f);
            }
        }

        private sealed class SlowEffect : BuffEffectBase, IP_MovementPointCap
        {
            public override void OnApply(Unit unit, Buff entry)
            {
                base.OnApply(unit, entry);
                if (unit == null) return;

                float normalMovement = GetNormalMovement(unit);
                float spentMovement = Mathf.Max(0f, normalMovement - unit.movementPointsStorage);
                unit.MovementPoints = Mathf.Min(unit.MovementPoints,
                    Mathf.Max(0f, normalMovement - 4f - spentMovement));
            }

            public float GetMovementPointCap(Unit unit, Buff entry, float currentCap)
            {
                if (unit == null) return currentCap;
                return Mathf.Min(currentCap, Mathf.Max(0f, GetNormalMovement(unit) - 4f));
            }

            private static float GetNormalMovement(Unit unit) => unit.customTotalMovementPoints
                + (unit.PassiveList?.GetMovementPointModifier() ?? 0f);
        }

        private sealed class MovementCapZeroEffect : BuffEffectBase, IP_MovementPointCap
        {
            public float GetMovementPointCap(Unit unit, Buff entry, float currentCap) => 0f;
        }

        private sealed class MovementPenaltyEffect : BuffEffectBase, IP_MovementPointCap
        {
            private readonly float penalty;

            public MovementPenaltyEffect(float penalty) => this.penalty = Mathf.Max(0f, penalty);

            public override void OnApply(Unit unit, Buff entry)
            {
                base.OnApply(unit, entry);
                if (unit == null) return;
                float normalMovement = unit.customTotalMovementPoints
                    + (unit.PassiveList?.GetMovementPointModifier() ?? 0f);
                float spentMovement = Mathf.Max(0f, normalMovement - unit.movementPointsStorage);
                unit.MovementPoints = Mathf.Min(unit.MovementPoints,
                    Mathf.Max(0f, normalMovement - penalty - spentMovement));
            }

            public float GetMovementPointCap(Unit unit, Buff entry, float currentCap)
            {
                if (unit == null) return currentCap;
                float normalMovement = unit.customTotalMovementPoints
                    + (unit.PassiveList?.GetMovementPointModifier() ?? 0f);
                return Mathf.Min(currentCap, Mathf.Max(0f, normalMovement - penalty));
            }
        }

        private sealed class ToxicBuffEffect : BuffEffectBase, IP_TurnStartHealthEffect
        {
            public int GetTurnStartHealthDelta(Unit unit) => -5 * Mathf.Max(1, Entry?.Stacks ?? 1);
        }

        private sealed class BurnBuffEffect : BuffEffectBase, IP_TurnStartHealthEffect
        {
            public int GetTurnStartHealthDelta(Unit unit)
            {
                Unit caster = Entry?.SourceUnit;
                int damagePerStack = caster != null
                ? Mathf.Max(0, (caster.Strength + caster.Magic) / 2)
                    : 5;
                return -damagePerStack * Mathf.Max(1, Entry?.Stacks ?? 1);
            }
        }

        private sealed class StunBuffEffect : BuffEffectBase, IP_ActionBlocker { }

        private sealed class BlackFogBuffEffect : BuffEffectBase, IP_TurnStartHealthEffect
        {
            public int GetTurnStartHealthDelta(Unit unit)
            {
                if (unit == null || !unit.TryGetBlackFogDepth(out int depth))
                {
                    return 0;
                }

                int maxHitPoints = Mathf.Max(1, unit.ComputedTotalHitPoints);
                int damage = Mathf.CeilToInt(maxHitPoints * 0.25f * (depth + 1));
                return -damage;
            }
        }

        private sealed class MovementCapBuffEffect : BuffEffectBase, IP_MovementPointCap
        {
            private readonly float cap;

            public MovementCapBuffEffect(float cap)
            {
                this.cap = cap;
            }

            public float GetMovementPointCap(Unit unit, Buff entry, float currentCap)
            {
                return cap;
            }
        }

        private sealed class DamageToOneBuffEffect : BuffEffectBase, IP_TakeDamageChange
        {
            public void TakeDamageChange(DamageChangeContext context)
            {
                if (context.Phase != DamageChangePhase.Damage)
                {
                    return;
                }

                context.Damage = context.Damage <= 0 ? 0 : 1;
            }
        }

        private sealed class IgnoreDefMag : BuffEffectBase, IP_DamageChange, IP_AfterCombat_Attacker
        {
            public void AfterCombatSequenceAsAttacker(CombatSequenceContext context)
            {
                this.SelfRemove();
            }
            
            public void DamageChange(DamageChangeContext context)
            {
                if (context.Phase != DamageChangePhase.Damage || !context.IsHit)
                {
                    return;
                }

                if (context.IsMagicAttack)
                {
                    context.Damage += context.Defender.Magic;
                }
                else
                {
                    context.Damage += context.Defender.Defense;
                }
            }

        }
    }
}

