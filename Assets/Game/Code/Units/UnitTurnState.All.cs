namespace Windy.Srpg.Runtime.Units
{
    public enum UnitTurnStateKind
    {
        Normal = 0,
        Selected = 1,
        ReachableEnemy = 2,
        Friendly = 3,
        Finished = 4
    }

    public abstract class UnitTurnState
    {
        protected UnitTurnState(GridUnit unit)
        {
            Unit = unit;
        }

        protected GridUnit Unit { get; }
        public virtual UnitTurnStateKind Kind => UnitTurnStateKind.Normal;
        public virtual bool CountsAsFinished => false;

        public virtual void Enter()
        {
        }

        public virtual void Exit()
        {
        }
    }

    public sealed class UnitTurnStateNormal : UnitTurnState
    {
        public UnitTurnStateNormal(GridUnit unit) : base(unit)
        {
        }

        public override UnitTurnStateKind Kind => UnitTurnStateKind.Normal;
    }

    public sealed class UnitTurnStateSelected : UnitTurnState
    {
        public UnitTurnStateSelected(GridUnit unit) : base(unit)
        {
        }

        public override UnitTurnStateKind Kind => UnitTurnStateKind.Selected;
    }

    public sealed class UnitTurnStateReachableEnemy : UnitTurnState
    {
        public UnitTurnStateReachableEnemy(GridUnit unit) : base(unit)
        {
        }

        public override UnitTurnStateKind Kind => UnitTurnStateKind.ReachableEnemy;
    }

    public sealed class UnitTurnStateFriendly : UnitTurnState
    {
        public UnitTurnStateFriendly(GridUnit unit) : base(unit)
        {
        }

        public override UnitTurnStateKind Kind => UnitTurnStateKind.Friendly;
    }

    public sealed class UnitTurnStateFinished : UnitTurnState
    {
        public UnitTurnStateFinished(GridUnit unit) : base(unit)
        {
        }

        public override UnitTurnStateKind Kind => UnitTurnStateKind.Finished;
        public override bool CountsAsFinished => true;
    }
}
