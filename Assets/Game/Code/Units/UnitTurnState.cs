namespace Windy.Srpg.Runtime.Units
{
    public abstract class UnitTurnState
    {
        protected UnitTurnState(BoardUnit unit)
        {
            Unit = unit;
        }

        protected BoardUnit Unit { get; }
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

