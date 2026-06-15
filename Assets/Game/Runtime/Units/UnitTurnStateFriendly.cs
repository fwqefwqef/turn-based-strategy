namespace Windy.Srpg.Runtime.Units
{
    public sealed class UnitTurnStateFriendly : UnitTurnState
    {
        public UnitTurnStateFriendly(BattleUnit unit) : base(unit)
        {
        }

        public override UnitTurnStateKind Kind => UnitTurnStateKind.Friendly;
    }
}
