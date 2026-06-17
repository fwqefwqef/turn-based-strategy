using TbsFramework.Cells;
using TbsFramework.Units;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CustomCellGrid
    {
        private CustomCellGridEndTurnRouter endTurnRouter;

        private void InstallFrameworkInputRouter()
        {
            endTurnRouter ??= new CustomCellGridEndTurnRouter(this);
            cellGridState = endTurnRouter;
        }

        internal bool TryRouteEndTurnThroughRuntime()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null || !ShouldRouteTurnLoopThroughRuntime)
            {
                return false;
            }

            RequestEndTurn();
            return true;
        }

        protected override void DispatchCellDeselected(Cell cell)
        {
            if (cell is IBattleCell battleCell
                && TryDispatchToCustomState(state => state.OnCellDeselected(battleCell)))
            {
                return;
            }

            base.DispatchCellDeselected(cell);
        }

        protected override void DispatchCellSelected(Cell cell)
        {
            if (cell is IBattleCell battleCell
                && TryDispatchToCustomState(state => state.OnCellSelected(battleCell)))
            {
                return;
            }

            base.DispatchCellSelected(cell);
        }

        protected override void DispatchCellClicked(Cell cell)
        {
            if (cell is IBattleCell battleCell
                && TryDispatchToCustomState(state => state.OnCellClicked(battleCell)))
            {
                return;
            }

            base.DispatchCellClicked(cell);
        }

        protected override void DispatchUnitClicked(Unit unit)
        {
            if (unit is IBattleUnit battleUnit
                && TryDispatchToCustomState(state => state.OnUnitClicked(battleUnit)))
            {
                return;
            }

            base.DispatchUnitClicked(unit);
        }

        protected override void DispatchUnitHighlighted(Unit unit)
        {
            if (unit is IBattleUnit battleUnit
                && TryDispatchToCustomState(state => state.OnUnitHighlighted(battleUnit)))
            {
                return;
            }

            base.DispatchUnitHighlighted(unit);
        }

        protected override void DispatchUnitDehighlighted(Unit unit)
        {
            if (unit is IBattleUnit battleUnit
                && TryDispatchToCustomState(state => state.OnUnitDehighlighted(battleUnit)))
            {
                return;
            }

            base.DispatchUnitDehighlighted(unit);
        }

        private bool TryDispatchToCustomState(System.Action<CustomCellGridState> dispatch)
        {
            if (currentCustomState == null)
            {
                return false;
            }

            dispatch(currentCustomState);
            return true;
        }
    }
}
