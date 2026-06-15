namespace Windy.Srpg.Runtime.Units
{
    public sealed class UnitTurnStateReachableEnemy : UnitTurnState
    {
        public UnitTurnStateReachableEnemy(BattleUnit unit) : base(unit)
        {
        }

        public override UnitTurnStateKind Kind => UnitTurnStateKind.ReachableEnemy;
    }
}
