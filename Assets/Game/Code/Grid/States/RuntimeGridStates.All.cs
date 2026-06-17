using System.Linq;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Grid.States
{
    // --- Base runtime grid state ---
    public abstract class RuntimeGridState
    {
        protected RuntimeGridState(RuntimeGrid grid)
        {
            Grid = grid;
        }

        protected RuntimeGrid Grid { get; }

        /// <summary>
        /// The unit this state considers "selected", or null. Used by the non-authoritative
        /// shadow harness to read the runtime's decision without inspecting private fields.
        /// </summary>
        public virtual GridUnit SelectedUnit => null;
        public virtual Cell PendingDestination => null;
        public virtual string DiagnosticStateLabel => "Waiting";

        public virtual void OnStateEnter()
        {
        }

        public virtual void OnStateExit()
        {
        }

        public virtual void OnCellClicked(Cell cell)
        {
        }

        public virtual void OnCellHovered(Cell cell)
        {
        }

        public virtual void OnCellUnhovered(Cell cell)
        {
        }

        public virtual void OnUnitClicked(GridUnit unit)
        {
        }

        public virtual void OnUnitHovered(GridUnit unit)
        {
        }

        public virtual void OnUnitUnhovered(GridUnit unit)
        {
        }

        public virtual void OnRightClick()
        {
        }
    }

    // --- Waiting for input ---
    public sealed class RuntimeGridStateWaitingForInput : RuntimeGridState
    {
        public RuntimeGridStateWaitingForInput(RuntimeGrid grid) : base(grid)
        {
        }

        public override string DiagnosticStateLabel => "Waiting";

        public override void OnUnitClicked(GridUnit unit)
        {
            if (unit == null || unit.IsFinishedForTurn)
            {
                return;
            }

            if (!Grid.GetCurrentPlayerUnits().Contains(unit))
            {
                return;
            }

            Grid.SetState(new RuntimeGridStateUnitSelected(Grid, unit));
        }
    }

    // --- Blocked input ---
    public sealed class RuntimeGridStateBlockedInput : RuntimeGridState
    {
        public RuntimeGridStateBlockedInput(RuntimeGrid grid) : base(grid)
        {
        }
    }

    // --- AI turn ---
    public sealed class RuntimeGridStateAiTurn : RuntimeGridState
    {
        public RuntimeGridStateAiTurn(RuntimeGrid grid) : base(grid)
        {
        }

        public override string DiagnosticStateLabel => "AiTurn";
    }

    // --- Unit selected + pending move confirm ---
    public sealed class RuntimeGridStateUnitSelected : RuntimeGridState
    {
        private readonly GridUnit selectedUnit;

        public RuntimeGridStateUnitSelected(RuntimeGrid grid, GridUnit selectedUnit) : base(grid)
        {
            this.selectedUnit = selectedUnit;
        }

        public override GridUnit SelectedUnit => selectedUnit;
        public override string DiagnosticStateLabel => "Selected";

        public override void OnStateEnter()
        {
            selectedUnit?.Select();
        }

        public override void OnStateExit()
        {
            selectedUnit?.Deselect();
        }

        public override void OnUnitClicked(GridUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            if (unit == selectedUnit)
            {
                Grid.SetState(new RuntimeGridStateUnitMovePendingConfirm(Grid, selectedUnit, selectedUnit?.CurrentCell));
                return;
            }

            if (!unit.IsFinishedForTurn && Grid.GetCurrentPlayerUnits().Contains(unit))
            {
                Grid.SetState(new RuntimeGridStateUnitSelected(Grid, unit));
                return;
            }

            Grid.SetState(new RuntimeGridStateWaitingForInput(Grid));
        }

        public override void OnCellClicked(Cell cell)
        {
            if (selectedUnit == null || cell == null)
            {
                return;
            }

            var reachableCells = selectedUnit.GetAvailableDestinations(Grid.Cells.ToList());
            if (reachableCells.Contains(cell))
            {
                Grid.SetState(new RuntimeGridStateUnitMovePendingConfirm(Grid, selectedUnit, cell));
                return;
            }

            Grid.SetState(new RuntimeGridStateWaitingForInput(Grid));
        }

        public override void OnRightClick()
        {
            Grid.SetState(new RuntimeGridStateWaitingForInput(Grid));
        }
    }

    public sealed class RuntimeGridStateUnitMovePendingConfirm : RuntimeGridState
    {
        private readonly GridUnit selectedUnit;
        private readonly Cell pendingDestination;

        public RuntimeGridStateUnitMovePendingConfirm(RuntimeGrid grid, GridUnit selectedUnit, Cell pendingDestination) : base(grid)
        {
            this.selectedUnit = selectedUnit;
            this.pendingDestination = pendingDestination;
        }

        public override GridUnit SelectedUnit => selectedUnit;
        public override Cell PendingDestination => pendingDestination;
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

            var path = selectedUnit.FindPath(Grid.Cells, pendingDestination);
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
                Grid.SetState(new RuntimeGridStateWaitingForInput(Grid));
                return;
            }

            selectedUnit.CancelPendingMove();
            Grid.SetState(new RuntimeGridStateUnitSelected(Grid, selectedUnit));
        }

        public void ConfirmWait()
        {
            if (selectedUnit == null)
            {
                Grid.SetState(new RuntimeGridStateWaitingForInput(Grid));
                return;
            }

            if (selectedUnit.HasPendingMove)
            {
                selectedUnit.ConfirmPendingMove();
            }

            selectedUnit.EndTurn();
            Grid.SetState(new RuntimeGridStateWaitingForInput(Grid));
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

    // --- Game over ---
    public sealed class RuntimeGridStateGameOver : RuntimeGridState
    {
        public RuntimeGridStateGameOver(RuntimeGrid grid) : base(grid)
        {
        }
    }

}
