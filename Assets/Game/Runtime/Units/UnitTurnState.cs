namespace Windy.Srpg.Runtime.Units
{
    public abstract class UnitTurnState
    {
        protected UnitTurnState(BattleUnit unit)
        {
            Unit = unit;
        }

        protected BattleUnit Unit { get; }
        public virtual UnitTurnStateKind Kind => UnitTurnStateKind.Normal;
        public virtual bool CountsAsFinished => false;
        public virtual void Enter()
        {
        }

        public virtual void Exit()
        {
        }
    }
}
