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
        public bool IsMagic;
        public bool CanPursuitAttack;
        public bool PreventsCounterattack;
        public bool EndsTurn;
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
        public bool IsSimulated;
        public DamageChangePhase Phase;
    }

    public interface IP_DamageChange
    {
        void DamageChange(DamageChangeContext context);
    }

    public interface IP_TakeDamageChange
    {
        void TakeDamageChange(DamageChangeContext context);
    }

    public interface IP_DamageMultiplier
    {
        void DamageMultiplier(DamageChangeContext context);
    }

    public interface IP_TakeDamageMultiplier
    {
        void TakeDamageMultiplier(DamageChangeContext context);
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

