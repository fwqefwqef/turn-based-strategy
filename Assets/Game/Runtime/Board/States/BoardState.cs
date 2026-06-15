using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Board.States
{
    public abstract class BoardState
    {
        protected BoardState(BattleBoard board)
        {
            Board = board;
        }

        protected BattleBoard Board { get; }

        /// <summary>
        /// The unit this state considers "selected", or null. Used by the non-authoritative
        /// shadow harness to read the runtime's decision without inspecting private fields.
        /// </summary>
        public virtual BattleUnit SelectedUnit => null;
        public virtual BoardCell PendingDestination => null;
        public virtual string DiagnosticStateLabel => "Waiting";

        public virtual void OnStateEnter()
        {
        }

        public virtual void OnStateExit()
        {
        }

        public virtual void OnCellClicked(BoardCell cell)
        {
        }

        public virtual void OnCellHovered(BoardCell cell)
        {
        }

        public virtual void OnCellUnhovered(BoardCell cell)
        {
        }

        public virtual void OnUnitClicked(BattleUnit unit)
        {
        }

        public virtual void OnUnitHovered(BattleUnit unit)
        {
        }

        public virtual void OnUnitUnhovered(BattleUnit unit)
        {
        }

        public virtual void OnRightClick()
        {
        }
    }
}
