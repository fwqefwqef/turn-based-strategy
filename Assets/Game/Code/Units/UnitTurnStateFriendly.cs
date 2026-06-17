namespace Windy.Srpg.Runtime.Units
{
    public sealed class UnitTurnStateFriendly : UnitTurnState
    {
        public UnitTurnStateFriendly(BoardUnit unit) : base(unit)
        {
        }

        public override UnitTurnStateKind Kind => UnitTurnStateKind.Friendly;
    }
}

