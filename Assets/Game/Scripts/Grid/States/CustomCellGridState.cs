using TbsFramework.Cells;
using TbsFramework.Grid;
using TbsFramework.Units;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Grid.States
{
    public interface ICustomRightClickHandler
    {
        void OnRightClick();
    }

    public abstract class CustomCellGridState : ICustomRightClickHandler
    {
        protected readonly CustomCellGrid _cellGrid;

        protected CustomCellGridState(CustomCellGrid cellGrid)
        {
            _cellGrid = cellGrid;
        }

        public virtual bool BlocksEndTurn => false;

        public virtual CustomCellGridState MakeTransition(CustomCellGridState nextState)
        {
            return nextState;
        }

        public virtual void OnUnitClicked(IBattleUnit unit)
        {
            if (unit is CustomUnit customUnit)
            {
                OnCustomUnitClicked(customUnit);
            }
        }

        public virtual void OnUnitHighlighted(IBattleUnit unit)
        {
            if (unit is CustomUnit customUnit)
            {
                OnCustomUnitHighlighted(customUnit);
            }
        }

        public virtual void OnUnitDehighlighted(IBattleUnit unit)
        {
            if (unit is CustomUnit customUnit)
            {
                OnCustomUnitDehighlighted(customUnit);
            }
        }

        public virtual void OnCustomUnitClicked(CustomUnit unit)
        {
        }

        public virtual void OnCustomUnitHighlighted(CustomUnit unit)
        {
        }

        public virtual void OnCustomUnitDehighlighted(CustomUnit unit)
        {
        }

        public virtual void OnCellDeselected(IBattleCell cell)
        {
            ResolveLegacyCell(cell)?.UnMark();
        }

        public virtual void OnCellSelected(IBattleCell cell)
        {
            ResolveLegacyCell(cell)?.MarkAsHighlighted();
        }

        public virtual void OnCellClicked(IBattleCell cell)
        {
        }

        public virtual void OnStateEnter()
        {
            if (_cellGrid == null)
            {
                return;
            }

            foreach (Cell cell in _cellGrid.GetAllCells())
            {
                cell?.UnMark();
            }
        }

        public virtual void OnStateExit()
        {
        }

        public virtual void OnRightClick()
        {
        }

        protected static Cell ResolveLegacyCell(IBattleCell cell)
        {
            return cell as Cell;
        }
    }

    internal sealed class LegacyCustomCellGridStateAdapter : CellGrid.CellGridState, ICustomRightClickHandler
    {
        private readonly CustomCellGridState state;

        public LegacyCustomCellGridStateAdapter(CustomCellGrid cellGrid, CustomCellGridState state) : base(cellGrid)
        {
            this.state = state;
        }

        public override CellGrid.CellGridState MakeTransition(CellGrid.CellGridState nextState)
        {
            return nextState;
        }

        public override void OnUnitClicked(Unit unit)
        {
            state?.OnUnitClicked(unit as IBattleUnit);
        }

        public override void OnUnitHighlighted(Unit unit)
        {
            state?.OnUnitHighlighted(unit as IBattleUnit);
        }

        public override void OnUnitDehighlighted(Unit unit)
        {
            state?.OnUnitDehighlighted(unit as IBattleUnit);
        }

        public override void OnCellDeselected(Cell cell)
        {
            state?.OnCellDeselected(cell as IBattleCell);
        }

        public override void OnCellSelected(Cell cell)
        {
            state?.OnCellSelected(cell as IBattleCell);
        }

        public override void OnCellClicked(Cell cell)
        {
            state?.OnCellClicked(cell as IBattleCell);
        }

        public override void EndTurn(bool isNetworkInvoked)
        {
            if (state?.BlocksEndTurn == true)
            {
                return;
            }

            base.EndTurn(isNetworkInvoked);
        }

        public override void OnStateEnter()
        {
            state?.OnStateEnter();
        }

        public override void OnStateExit()
        {
            state?.OnStateExit();
        }

        public void OnRightClick()
        {
            state?.OnRightClick();
        }
    }
}
