namespace Windy.Srpg.Runtime.Units
{
    public sealed class UnitTurnStateReachableEnemy : UnitTurnState
    {
        public UnitTurnStateReachableEnemy(BoardUnit unit) : base(unit)
        {
        }

        public override UnitTurnStateKind Kind => UnitTurnStateKind.ReachableEnemy;
    }
}

