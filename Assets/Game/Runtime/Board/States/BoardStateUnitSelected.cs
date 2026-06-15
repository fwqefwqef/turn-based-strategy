using System.Linq;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Board.States
{
    public sealed class BoardStateUnitSelected : BoardState
    {
        private readonly BattleUnit selectedUnit;

        public BoardStateUnitSelected(BattleBoard board, BattleUnit selectedUnit) : base(board)
        {
            this.selectedUnit = selectedUnit;
        }

        public override BattleUnit SelectedUnit => selectedUnit;
        public override string DiagnosticStateLabel => "Selected";

        public override void OnStateEnter()
        {
            selectedUnit?.Select();
        }

        public override void OnStateExit()
        {
            selectedUnit?.Deselect();
        }

        public override void OnUnitClicked(BattleUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            if (unit == selectedUnit)
            {
                Board.SetState(new BoardStateUnitMovePendingConfirm(Board, selectedUnit, selectedUnit?.CurrentCell));
                return;
            }

            if (!unit.IsFinishedForTurn && Board.GetCurrentPlayerUnits().Contains(unit))
            {
                Board.SetState(new BoardStateUnitSelected(Board, unit));
                return;
            }

            Board.SetState(new BoardStateWaitingForInput(Board));
        }

        public override void OnCellClicked(BoardCell cell)
        {
            if (selectedUnit == null || cell == null)
            {
                return;
            }

            var reachableCells = selectedUnit.GetAvailableDestinations(Board.Cells.ToList());
            if (reachableCells.Contains(cell))
            {
                Board.SetState(new BoardStateUnitMovePendingConfirm(Board, selectedUnit, cell));
                return;
            }

            Board.SetState(new BoardStateWaitingForInput(Board));
        }

        public override void OnRightClick()
        {
            Board.SetState(new BoardStateWaitingForInput(Board));
        }
    }

    public sealed class BoardStateUnitMovePendingConfirm : BoardState
    {
        private readonly BattleUnit selectedUnit;
        private readonly BoardCell pendingDestination;

        public BoardStateUnitMovePendingConfirm(BattleBoard board, BattleUnit selectedUnit, BoardCell pendingDestination) : base(board)
        {
            this.selectedUnit = selectedUnit;
            this.pendingDestination = pendingDestination;
        }

        public override BattleUnit SelectedUnit => selectedUnit;
        public override BoardCell PendingDestination => pendingDestination;
        public override string DiagnosticStateLabel => "PendingMoveConfirm";

        public override void OnStateEnter()
        {
            selectedUnit?.Select();
        }

        public override void OnStateExit()
        {
            selectedUnit?.Deselect();
        }

        public override void OnRightClick()
        {
            if (selectedUnit == null)
            {
                Board.SetState(new BoardStateWaitingForInput(Board));
                return;
            }

            Board.SetState(new BoardStateUnitSelected(Board, selectedUnit));
        }
    }
}
