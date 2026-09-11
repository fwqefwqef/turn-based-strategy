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
            isRegistered = true;
        }

        private sealed class ToxicBuffEffect : BuffEffectBase, IP_TurnStartHealthEffect
        {
            public int GetTurnStartHealthDelta(Unit unit) => -5 * Mathf.Max(1, Entry?.Stacks ?? 1);
        }

        private sealed class BurnBuffEffect : BuffEffectBase, IP_TurnStartHealthEffect
        {
            public int GetTurnStartHealthDelta(Unit unit) => -5;
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

