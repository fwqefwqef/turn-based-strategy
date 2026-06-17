using System;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Runtime.Grid;

namespace Windy.Srpg.Game.Units
{
    public sealed class AttackEventArgs : EventArgs
    {
        public AttackEventArgs(Unit aggressor, Unit defender, int damage)
        {
            Aggressor = aggressor;
            Defender = defender;
            Damage = damage;
        }

        public Unit Aggressor { get; }
        public Unit Defender { get; }
        public int Damage { get; }

        public Unit Attacker => Aggressor;
    }

    public readonly struct AttackAction
    {
        public AttackAction(int damage, float actionCost)
        {
            Damage = damage;
            ActionCost = actionCost;
        }

        public int Damage { get; }
        public float ActionCost { get; }
    }

    public sealed class MovementEventArgs : EventArgs
    {
        public MovementEventArgs(Unit unit, Cell origin, Cell destination)
        {
            Unit = unit;
            Origin = origin;
            Destination = destination;
        }

        public Unit Unit { get; }
        public Cell Origin { get; }
        public Cell Destination { get; }
    }

    public sealed class UnitCreatedEventArgs : EventArgs
    {
        public UnitCreatedEventArgs(Transform unit)
        {
            unitTransform = unit;
        }

        public Transform unitTransform { get; }
    }


    public class UnitHealthChangedEventArgs : EventArgs
    {
        public Unit Source;
        public Unit Unit;
        public int PreviousHitPoints;
        public int CurrentHitPoints;
        public int Delta;

        public UnitHealthChangedEventArgs(Unit source, Unit unit, int previousHitPoints, int currentHitPoints)
        {
            Source = source;
            Unit = unit;
            PreviousHitPoints = previousHitPoints;
            CurrentHitPoints = currentHitPoints;
            Delta = currentHitPoints - previousHitPoints;
        }
    }

    public class CombatSequenceEventArgs : EventArgs
    {
        public Unit Attacker;
        public Unit Defender;

        public CombatSequenceEventArgs(Unit attacker, Unit defender)
        {
            Attacker = attacker;
            Defender = defender;
        }
    }

    public class UnitDestroyedEventArgs : EventArgs
    {
        public Unit Attacker;
        public Unit Defender;
        public int Damage;

        public UnitDestroyedEventArgs(Unit attacker, Unit defender, int damage)
        {
            Attacker = attacker;
            Defender = defender;
            Damage = damage;
        }
    }

}
