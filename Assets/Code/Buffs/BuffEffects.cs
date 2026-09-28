using Windy.Srpg.Game.Catalogs;
using Windy.Srpg.Game.Units;
using UnityEngine;

namespace Windy.Srpg.Game.Buffs
{
    public static class BuffEffects
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
            public int LimitAttackDamage(Unit defender, int damage, bool simulateOnly) =>
                ResolveForOwner(defender, damage, () => Owner.HitPoints <= 0
                    ? damage
                    : Mathf.Min(damage, Mathf.Max(0, Owner.HitPoints - 1)));
        }

        private sealed class StormSurgeEffect : BuffEffectBase, IP_DamageChange, IP_AfterCombat_Attacker, IP_AfterCombat_Defender
        {
            public void DamageChange(DamageChangeContext context) => UseDamageContext(context, ApplyDamageChange);
            public void AfterCombatSequenceAsAttacker(CombatSequenceContext context) => RemoveSelfAfterCombat(context);
            public void AfterCombatSequenceAsDefender(CombatSequenceContext context) => RemoveSelfAfterCombat(context);

            private void ApplyDamageChange()
            {
                if (DamageContext.Phase == DamageChangePhase.Damage && DamageContext.IsHit)
                    DamageContext.Damage += 10;
            }
        }

        private sealed class DarkSanctuaryEffect : BuffEffectBase, IP_TakeDamageMultiplier
        {
            public void TakeDamageMultiplier(DamageChangeContext context) => UseDamageContext(context, ApplyDamageMultiplier);

            private void ApplyDamageMultiplier()
            {
                if (DamageContext.Phase == DamageChangePhase.Damage && DamageContext.IsAreaSpell)
                    DamageContext.Damage = Mathf.CeilToInt(DamageContext.Damage * 0.5f);
            }
        }

        private sealed class SlowEffect : BuffEffectBase, IP_MovementPointCap
        {
            protected override void OnApply()
            {
                float normalMovement = GetNormalMovement();
                float spentMovement = Mathf.Max(0f, normalMovement - Owner.movementPointsStorage);
                Owner.MovementPoints = Mathf.Min(Owner.MovementPoints,
                    Mathf.Max(0f, normalMovement - 4f - spentMovement));
            }

            public float GetMovementPointCap(Unit unit, Buff entry, float currentCap) =>
                ResolveForOwner(unit, entry, currentCap,
                    () => Mathf.Min(currentCap, Mathf.Max(0f, GetNormalMovement() - 4f)));

            private float GetNormalMovement() =>
                Owner.customTotalMovementPoints + Owner.PassiveList.GetMovementPointModifier();
        }

        private sealed class MovementCapZeroEffect : BuffEffectBase, IP_MovementPointCap
        {
            public float GetMovementPointCap(Unit unit, Buff entry, float currentCap) =>
                ResolveForOwner(unit, entry, currentCap, () => 0f);
        }

        private sealed class MovementPenaltyEffect : BuffEffectBase, IP_MovementPointCap
        {
            private readonly float penalty;

            public MovementPenaltyEffect(float penalty) => this.penalty = Mathf.Max(0f, penalty);

            protected override void OnApply()
            {
                float normalMovement = GetNormalMovement();
                float spentMovement = Mathf.Max(0f, normalMovement - Owner.movementPointsStorage);
                Owner.MovementPoints = Mathf.Min(Owner.MovementPoints,
                    Mathf.Max(0f, normalMovement - penalty - spentMovement));
            }

            public float GetMovementPointCap(Unit unit, Buff entry, float currentCap) =>
                ResolveForOwner(unit, entry, currentCap,
                    () => Mathf.Min(currentCap, Mathf.Max(0f, GetNormalMovement() - penalty)));

            private float GetNormalMovement() =>
                Owner.customTotalMovementPoints + Owner.PassiveList.GetMovementPointModifier();
        }

        private sealed class ToxicBuffEffect : BuffEffectBase, IP_TurnStartHealthEffect
        {
            public int GetTurnStartHealthDelta(Unit unit) =>
                ResolveForOwner(unit, 0, () => -5 * Stacks);
        }

        private sealed class BurnBuffEffect : BuffEffectBase, IP_TurnStartHealthEffect
        {
            public int GetTurnStartHealthDelta(Unit unit) =>
                ResolveForOwner(unit, 0, () => -GetSourceStrengthMagicAverageOr(5) * Stacks);
        }

        private sealed class StunBuffEffect : BuffEffectBase, IP_ActionBlocker { }

        private sealed class BlackFogBuffEffect : BuffEffectBase, IP_TurnStartHealthEffect
        {
            public int GetTurnStartHealthDelta(Unit unit) =>
                ResolveForOwner(unit, 0, GetBlackFogDamage);

            private int GetBlackFogDamage()
            {
                if (!Owner.TryGetBlackFogDepth(out int depth))
                {
                    return 0;
                }

                int maxHitPoints = Mathf.Max(1, Owner.ComputedTotalHitPoints);
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

            public float GetMovementPointCap(Unit unit, Buff entry, float currentCap) =>
                ResolveForOwner(unit, entry, currentCap, () => cap);
        }

        private sealed class DamageToOneBuffEffect : BuffEffectBase, IP_TakeDamageChange
        {
            public void TakeDamageChange(DamageChangeContext context) => UseDamageContext(context, ApplyDamageChange);

            private void ApplyDamageChange()
            {
                if (DamageContext.Phase != DamageChangePhase.Damage)
                {
                    return;
                }

                DamageContext.Damage = DamageContext.Damage <= 0 ? 0 : 1;
            }
        }

        private sealed class IgnoreDefMag : BuffEffectBase, IP_DamageChange, IP_AfterCombat_Attacker
        {
            public void DamageChange(DamageChangeContext context) => UseDamageContext(context, ApplyDamageChange);
            public void AfterCombatSequenceAsAttacker(CombatSequenceContext context) => RemoveSelfAfterCombat(context);

            private void ApplyDamageChange()
            {
                if (DamageContext.Phase != DamageChangePhase.Damage || !DamageContext.IsHit)
                {
                    return;
                }

                if (DamageContext.IsMagicAttack)
                {
                    DamageContext.Damage += DamageContext.Defender.Magic;
                }
                else
                {
                    DamageContext.Damage += DamageContext.Defender.Defense;
                }
            }

        }
    }
}

