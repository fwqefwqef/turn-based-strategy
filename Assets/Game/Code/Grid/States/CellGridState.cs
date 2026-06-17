using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Rendering;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Grid.States
{
    public interface IRightClickHandler
    {
        void OnRightClick();
    }

    public abstract class CellGridState : IRightClickHandler
    {
        protected readonly CellGrid _cellGrid;

        protected CellGridState(CellGrid cellGrid)
        {
            _cellGrid = cellGrid;
        }

        public virtual bool BlocksEndTurn => false;

        public virtual CellGridState MakeTransition(CellGridState nextState)
        {
            return nextState;
        }

        public virtual void OnUnitClicked(IBoardUnit unit)
        {
            if (unit is Unit customUnit)
            {
                OnUnitClicked(customUnit);
            }
        }

        public virtual void OnUnitHighlighted(IBoardUnit unit)
        {
            if (unit is Unit customUnit)
            {
                OnUnitHighlighted(customUnit);
            }
        }

        public virtual void OnUnitDehighlighted(IBoardUnit unit)
        {
            if (unit is Unit customUnit)
            {
                OnUnitDehighlighted(customUnit);
            }
        }

        public virtual void OnUnitClicked(Unit unit)
        {
        }

        public virtual void OnUnitHighlighted(Unit unit)
        {
        }

        public virtual void OnUnitDehighlighted(Unit unit)
        {
        }

        public virtual void OnCellDeselected(IBattleCell cell)
        {
            ResolveBoardCell(cell)?.ClearHighlight();
        }

        public virtual void OnCellSelected(IBattleCell cell)
        {
            ResolveBoardCell(cell)?.ApplyHighlight(CellHighlightKind.Selected);
        }

        public virtual void OnCellClicked(IBattleCell cell)
        {
        }

        public virtual void OnStateEnter()
        {
            _cellGrid?.ClearAllCellHighlights();
        }

        public virtual void OnStateExit()
        {
        }

        public virtual void OnRightClick()
        {
        }

        protected static BattleSquareCell ResolveBoardCell(IBattleCell cell)
        {
            return CellGrid.ResolveRegistryCellFromBattleCell(cell);
        }
    }
}

