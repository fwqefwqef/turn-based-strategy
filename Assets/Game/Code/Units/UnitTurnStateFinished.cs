namespace Windy.Srpg.Runtime.Units
{
    public sealed class UnitTurnStateFinished : UnitTurnState
    {
        public UnitTurnStateFinished(BoardUnit unit) : base(unit)
        {
        }

        public override UnitTurnStateKind Kind => UnitTurnStateKind.Finished;
        public override bool CountsAsFinished => true;
    }
}

