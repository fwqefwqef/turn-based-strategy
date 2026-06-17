using System;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Runtime.Board;

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
        public MovementEventArgs(Unit unit, BattleSquareCell origin, BattleSquareCell destination)
        {
            Unit = unit;
            Origin = origin;
            Destination = destination;
        }

        public Unit Unit { get; }
        public BattleSquareCell Origin { get; }
        public BattleSquareCell Destination { get; }
    }

    public sealed class UnitCreatedEventArgs : EventArgs
    {
        public UnitCreatedEventArgs(Transform unit)
        {
            unitTransform = unit;
        }

        public Transform unitTransform { get; }
    }
}

