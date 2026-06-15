namespace Windy.Srpg.Runtime.Units
{
    public sealed class UnitTurnStateNormal : UnitTurnState
    {
        public UnitTurnStateNormal(BattleUnit unit) : base(unit)
        {
        }

        public override UnitTurnStateKind Kind => UnitTurnStateKind.Normal;
    }
}
