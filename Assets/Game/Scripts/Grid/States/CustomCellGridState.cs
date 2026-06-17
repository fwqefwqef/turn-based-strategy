using TbsFramework.Cells;
using TbsFramework.Grid;
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

            _cellGrid.ClearAllCellHighlights();
        }

        public virtual void OnStateExit()
        {
        }

        public virtual void OnRightClick()
        {
        }

        protected static Cell ResolveLegacyCell(IBattleCell cell)
        {
            return CustomCellGrid.ResolveRegistryCellFromBattleCell(cell);
        }
    }

    internal sealed class CustomCellGridEndTurnRouter : CellGrid.CellGridState
    {
        private readonly CustomCellGrid grid;

        public CustomCellGridEndTurnRouter(CellGrid legacyGrid, CustomCellGrid grid) : base(legacyGrid)
        {
            this.grid = grid;
        }

        public override void OnStateEnter()
        {
        }

        public override void OnStateExit()
        {
        }

        public override void EndTurn(bool isNetworkInvoked)
        {
            if (grid.CurrentCustomState?.BlocksEndTurn == true)
            {
                return;
            }

            if (grid.TryRouteEndTurnThroughRuntime())
            {
                return;
            }

            base.EndTurn(isNetworkInvoked);
        }
    }
}
