using System;

namespace Windy.Srpg.Game.Units
{
    // Shared combat support types used by Unit, Unit.Behavior, abilities, and UI.
    public struct ResolvedAttackProfile
    {
        public int Damage;
        public int Accuracy;
        public int Crit;
        public int NumHits;
        public int PursuitSpeed;
        public bool IsMagic;
        public bool CanPursuitAttack;
        public bool PreventsCounterattack;
        public bool EndsTurn;
        public bool UsesWeaponEffects;
    }

    public enum CombatStrikePhaseKind
    {
        InitialAttack,
        InitialCounter,
        PursuitAttack,
        PursuitCounter
    }

    public readonly struct CombatStrikePhase
    {
        public CombatStrikePhaseKind Kind { get; }
        public Unit Attacker { get; }
        public Unit Defender { get; }
        public int HitCount { get; }
        public bool IsCounter => Kind == CombatStrikePhaseKind.InitialCounter
            || Kind == CombatStrikePhaseKind.PursuitCounter;
        public bool IsPursuit => Kind == CombatStrikePhaseKind.PursuitAttack
            || Kind == CombatStrikePhaseKind.PursuitCounter;
        public bool HasVantagePriority { get; }

        public CombatStrikePhase(
            CombatStrikePhaseKind kind,
            Unit attacker,
            Unit defender,
            int hitCount,
            bool hasVantagePriority = false)
        {
            Kind = kind;
            Attacker = attacker;
            Defender = defender;
            HitCount = Math.Max(1, hitCount);
            HasVantagePriority = hasVantagePriority;
        }
    }

    public sealed class CombatSequencePlan
    {
        public System.Collections.Generic.IReadOnlyList<CombatStrikePhase> Phases { get; }

        public CombatSequencePlan(System.Collections.Generic.IReadOnlyList<CombatStrikePhase> phases)
        {
            Phases = phases ?? Array.Empty<CombatStrikePhase>();
        }

        public int GetTotalHitCount(Unit attacker)
        {
            int total = 0;
            foreach (CombatStrikePhase phase in Phases)
            {
                if (phase.Attacker == attacker)
                {
                    total += phase.HitCount;
                }
            }

            return total;
        }

        public bool HasCounterPhase
        {
            get
            {
                foreach (CombatStrikePhase phase in Phases)
                {
                    if (phase.IsCounter) return true;
                }

                return false;
            }
        }
    }

    public static class CombatSequenceBuilder
    {
        public static CombatSequencePlan Build(
            Unit attacker,
            Unit defender,
            ResolvedAttackProfile attackProfile,
            bool? canCounterOverride = null,
            bool? attackerPursuesOverride = null,
            bool? defenderPursuesOverride = null)
        {
            var phases = new System.Collections.Generic.List<CombatStrikePhase>();
            if (attacker == null || defender == null)
            {
                return new CombatSequencePlan(phases);
            }

            bool canCounter = canCounterOverride
                ?? defender.CanCounterAttackAgainst(attacker, attackProfile.PreventsCounterattack);
            bool vantage = canCounter && defender.HasVantage;
            int pursuitSpeed = attackProfile.PursuitSpeed > 0
                ? attackProfile.PursuitSpeed
                : attacker.Speed;
            bool attackerPursues = attackerPursuesOverride
                ?? (attackProfile.CanPursuitAttack
                    && pursuitSpeed >= defender.Speed + attacker.PursuitAttackSpeedThreshold);
            bool defenderPursues = canCounter && (defenderPursuesOverride
                ?? defender.CanPursuitAttackAgainst(attacker));

            var initialAttack = new CombatStrikePhase(
                CombatStrikePhaseKind.InitialAttack, attacker, defender, attackProfile.NumHits);
            var initialCounter = new CombatStrikePhase(
                CombatStrikePhaseKind.InitialCounter, defender, attacker, defender.NumHits, vantage);
            var pursuitAttack = new CombatStrikePhase(
                CombatStrikePhaseKind.PursuitAttack, attacker, defender, attackProfile.NumHits);
            var pursuitCounter = new CombatStrikePhase(
                CombatStrikePhaseKind.PursuitCounter, defender, attacker, defender.NumHits, vantage);

            if (vantage)
            {
                phases.Add(initialCounter);
                phases.Add(initialAttack);
                if (defenderPursues) phases.Add(pursuitCounter);
                if (attackerPursues) phases.Add(pursuitAttack);
            }
            else
            {
                phases.Add(initialAttack);
                if (canCounter) phases.Add(initialCounter);
                if (attackerPursues) phases.Add(pursuitAttack);
                if (defenderPursues) phases.Add(pursuitCounter);
            }

            return new CombatSequencePlan(phases);
        }
    }

    public enum DamageChangePhase
    {
        Outcome,
        Damage
    }

    public sealed class DamageChangeContext
    {
        public Unit Attacker;
        public Unit Defender;
        public int Damage;
        public bool IsHit;
        public bool IsMagicAttack;
        public bool IsCrit;
        public bool IsCounterAttack;
        public bool IsAreaSpell;
        public bool IsSimulated;
        public DamageChangePhase Phase;
    }

    public interface IP_DamageChange
    {
        void DamageChange(DamageChangeContext context);
    }

    public interface IP_AttackHitEffect
    {
        void OnAttackHit(Unit attacker, Unit defender, int damageDealt, bool isBasicAttack);
    }

    public interface IP_CancelCounterattackOnHit : IP_AttackHitEffect
    {
        bool HitLanded { get; }
    }

    public interface IP_AttackNeverMisses { }

    public interface IP_TakeDamageChange
    {
        void TakeDamageChange(DamageChangeContext context);
    }

    // Runs after every ordinary damage modifier, so survival effects cannot be
    // bypassed by a later multiplier. Simulations must not consume limited uses.
    public interface IP_AttackSurvivalGuard
    {
        int LimitAttackDamage(int damage, bool simulateOnly);
    }

    public interface IP_PainDamageChange
    {
        int ModifyPainDamage(Unit unit, int damage);
    }

    /// <summary>
    /// Participates in the dedicated health phase before ordinary OnTurnStart hooks.
    /// Positive values heal and negative values deal Pain damage.
    /// </summary>
    public interface IP_TurnStartHealthEffect
    {
        int GetTurnStartHealthDelta();
    }

    public interface IP_DamageMultiplier
    {
        void DamageMultiplier(DamageChangeContext context);
    }

    public interface IP_TakeDamageMultiplier
    {
        void TakeDamageMultiplier(DamageChangeContext context);
    }

    /// <summary>
    /// Causes a legal defender counterattack to resolve before the attacker's strikes.
    /// That preemptive counter replaces the ordinary post-attack counterattack.
    /// </summary>
    public interface IP_Vantage
    {
    }

    public sealed class CombatSequenceContext
    {
        public Unit Attacker { get; }
        public Unit Defender { get; }
        public bool CounterPrevented { get; }

        public CombatSequenceContext(Unit attacker, Unit defender, bool counterPrevented)
        {
            Attacker = attacker;
            Defender = defender;
            CounterPrevented = counterPrevented;
        }
    }

    public interface IP_AfterCombat_Attacker
    {
        void AfterCombatSequenceAsAttacker(CombatSequenceContext context);
    }

    public interface IP_BeforeCombat_Attacker
    {
        void BeforeCombatSequenceAsAttacker(CombatSequenceContext context);
    }

    public interface IP_AfterCombat_Defender
    {
        void AfterCombatSequenceAsDefender(CombatSequenceContext context);
    }

    public interface IP_BeforeCombat_Defender
    {
        void BeforeCombatSequenceAsDefender(CombatSequenceContext context);
    }
}

