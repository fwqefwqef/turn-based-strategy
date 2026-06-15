using Windy.Srpg.Game.Units;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid.States
{
    public class CustomCellGridStateMovePendingConfirm : CustomCellGridState
    {
        private readonly CustomMoveAbility customMoveAbility;

        public CustomCellGridStateMovePendingConfirm(CustomCellGrid cellGrid, CustomMoveAbility customMoveAbility) : base(cellGrid)
        {
            this.customMoveAbility = customMoveAbility;
        }

        public override void OnStateEnter()
        {
            base.OnStateEnter();
            customMoveAbility?.OnPendingMoveStateEnter(_cellGrid);
        }

        public override void OnStateExit()
        {
            customMoveAbility?.OnPendingMoveStateExit(_cellGrid);
        }

        public override void OnCustomUnitClicked(CustomUnit customUnit)
        {
            customMoveAbility?.OnPendingMoveUnitClicked(customUnit, _cellGrid);
        }

        public override void OnCustomUnitHighlighted(CustomUnit customUnit)
        {
            customMoveAbility?.OnPendingMoveUnitHighlighted(customUnit, _cellGrid);
        }

        public override void OnCustomUnitDehighlighted(CustomUnit customUnit)
        {
            customMoveAbility?.OnPendingMoveUnitDehighlighted(customUnit, _cellGrid);
        }

        public override void OnCellClicked(IBattleCell cell)
        {
            customMoveAbility?.OnPendingMoveCellClicked(ResolveLegacyCell(cell), _cellGrid);
        }

        public override void OnCellSelected(IBattleCell cell)
        {
            customMoveAbility?.OnPendingMoveCellSelected(ResolveLegacyCell(cell), _cellGrid);
        }

        public override void OnCellDeselected(IBattleCell cell)
        {
            customMoveAbility?.OnPendingMoveCellDeselected(ResolveLegacyCell(cell), _cellGrid);
        }

        public override void OnRightClick()
        {
            customMoveAbility?.OnPendingMoveRightClicked(_cellGrid);
        }
    }
}
