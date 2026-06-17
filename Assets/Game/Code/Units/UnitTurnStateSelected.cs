namespace Windy.Srpg.Runtime.Units
{
    public sealed class UnitTurnStateSelected : UnitTurnState
    {
        public UnitTurnStateSelected(BoardUnit unit) : base(unit)
        {
        }

        public override UnitTurnStateKind Kind => UnitTurnStateKind.Selected;
    }
}

