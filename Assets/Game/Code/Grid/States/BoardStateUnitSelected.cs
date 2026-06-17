using System.Linq;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Board.States
{
    public sealed class BoardStateUnitSelected : BoardState
    {
        private readonly BoardUnit selectedUnit;

        public BoardStateUnitSelected(BattleBoard board, BoardUnit selectedUnit) : base(board)
        {
            this.selectedUnit = selectedUnit;
        }

        public override BoardUnit SelectedUnit => selectedUnit;
        public override string DiagnosticStateLabel => "Selected";

        public override void OnStateEnter()
        {
            selectedUnit?.Select();
        }

        public override void OnStateExit()
        {
            selectedUnit?.Deselect();
        }

        public override void OnUnitClicked(BoardUnit unit)
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
        private readonly BoardUnit selectedUnit;
        private readonly BoardCell pendingDestination;

        public BoardStateUnitMovePendingConfirm(BattleBoard board, BoardUnit selectedUnit, BoardCell pendingDestination) : base(board)
        {
            this.selectedUnit = selectedUnit;
            this.pendingDestination = pendingDestination;
        }

        public override BoardUnit SelectedUnit => selectedUnit;
        public override BoardCell PendingDestination => pendingDestination;
        public override string DiagnosticStateLabel => "PendingMoveConfirm";

        public override void OnStateEnter()
        {
            selectedUnit?.Select();

            if (selectedUnit == null)
            {
                return;
            }

            if (pendingDestination == null)
            {
                selectedUnit.BeginPendingMoveInPlace();
                return;
            }

            if (pendingDestination == selectedUnit.CurrentCell)
            {
                selectedUnit.BeginPendingMoveInPlace();
                return;
            }

            var path = selectedUnit.FindPath(Board.Cells, pendingDestination);
            if (path != null && path.Count > 0)
            {
                selectedUnit.BeginPendingMove(pendingDestination, path);
            }
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

            selectedUnit.CancelPendingMove();
            Board.SetState(new BoardStateUnitSelected(Board, selectedUnit));
        }

        public void ConfirmWait()
        {
            if (selectedUnit == null)
            {
                Board.SetState(new BoardStateWaitingForInput(Board));
                return;
            }

            if (selectedUnit.HasPendingMove)
            {
                selectedUnit.ConfirmPendingMove();
            }

            selectedUnit.EndTurn();
            Board.SetState(new BoardStateWaitingForInput(Board));
        }

        public void ConfirmPendingMoveAfterCombat(bool consumeAllRemainingMovement = false)
        {
            if (selectedUnit == null)
            {
                return;
            }

            if (selectedUnit.HasPendingMove)
            {
                selectedUnit.ConfirmPendingMove(consumeAllRemainingMovement, syncTransform: false);
            }
        }
    }
}

