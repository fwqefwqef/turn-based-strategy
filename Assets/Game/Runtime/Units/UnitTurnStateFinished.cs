namespace Windy.Srpg.Runtime.Units
{
    public sealed class UnitTurnStateFinished : UnitTurnState
    {
        public UnitTurnStateFinished(BattleUnit unit) : base(unit)
        {
        }

        public override UnitTurnStateKind Kind => UnitTurnStateKind.Finished;
        public override bool CountsAsFinished => true;
    }
}
