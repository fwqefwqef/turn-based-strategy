using Windy.Srpg.Game.Units;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid.States
{
    public class CellGridStateMovePendingConfirm : CellGridState
    {
        private readonly MoveAbility customMoveAbility;

        public CellGridStateMovePendingConfirm(CellGrid cellGrid, MoveAbility customMoveAbility) : base(cellGrid)
        {
            this.customMoveAbility = customMoveAbility;
        }

        public MoveAbility MoveAbility => customMoveAbility;

        public override void OnStateEnter()
        {
            base.OnStateEnter();
            customMoveAbility?.OnPendingMoveStateEnter(_cellGrid);
        }

        public override void OnStateExit()
        {
            customMoveAbility?.OnPendingMoveStateExit(_cellGrid);
        }

        public override void OnUnitClicked(Unit customUnit)
        {
            if (_cellGrid.ShouldRouteHumanMovementThroughRuntime)
            {
                return;
            }

            customMoveAbility?.OnPendingMoveUnitClicked(customUnit, _cellGrid);
        }

        public override void OnUnitHighlighted(Unit customUnit)
        {
            customMoveAbility?.OnPendingMoveUnitHighlighted(customUnit, _cellGrid);
        }

        public override void OnUnitDehighlighted(Unit customUnit)
        {
            customMoveAbility?.OnPendingMoveUnitDehighlighted(customUnit, _cellGrid);
        }

        public override void OnCellClicked(IBattleCell cell)
        {
            if (_cellGrid.ShouldRouteHumanMovementThroughRuntime)
            {
                return;
            }

            customMoveAbility?.OnPendingMoveCellClicked(ResolveBoardCell(cell), _cellGrid);
        }

        public override void OnCellSelected(IBattleCell cell)
        {
            customMoveAbility?.OnPendingMoveCellSelected(ResolveBoardCell(cell), _cellGrid);
        }

        public override void OnCellDeselected(IBattleCell cell)
        {
            customMoveAbility?.OnPendingMoveCellDeselected(ResolveBoardCell(cell), _cellGrid);
        }

        public override void OnRightClick()
        {
            if (_cellGrid.ShouldRouteHumanMovementThroughRuntime)
            {
                return;
            }

            customMoveAbility?.OnPendingMoveRightClicked(_cellGrid);
        }
    }
}

