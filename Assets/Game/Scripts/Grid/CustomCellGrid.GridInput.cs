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
            endTurnRouter ??= new CustomCellGridEndTurnRouter(LegacyGrid, this);
            LegacyGrid.InstallEndTurnRouter(endTurnRouter);
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

        internal bool TryDispatchCellDeselected(Cell cell)
        {
            IBattleCell battleCell = ResolveBattleCellFromRegistryCell(cell);
            if (battleCell != null
                && TryDispatchToCustomState(state => state.OnCellDeselected(battleCell)))
            {
                return true;
            }

            return false;
        }

        internal bool TryDispatchCellSelected(Cell cell)
        {
            IBattleCell battleCell = ResolveBattleCellFromRegistryCell(cell);
            if (battleCell != null
                && TryDispatchToCustomState(state => state.OnCellSelected(battleCell)))
            {
                return true;
            }

            return false;
        }

        internal bool TryDispatchCellClicked(Cell cell)
        {
            IBattleCell battleCell = ResolveBattleCellFromRegistryCell(cell);
            if (battleCell != null
                && TryDispatchToCustomState(state => state.OnCellClicked(battleCell)))
            {
                return true;
            }

            return false;
        }

        internal bool TryDispatchUnitClicked(Unit unit)
        {
            IBattleUnit battleUnit = ResolveBattleUnitFromRegistryUnit(unit);
            if (battleUnit != null
                && TryDispatchToCustomState(state => state.OnUnitClicked(battleUnit)))
            {
                return true;
            }

            return false;
        }

        internal bool TryDispatchUnitHighlighted(Unit unit)
        {
            IBattleUnit battleUnit = ResolveBattleUnitFromRegistryUnit(unit);
            if (battleUnit != null
                && TryDispatchToCustomState(state => state.OnUnitHighlighted(battleUnit)))
            {
                return true;
            }

            return false;
        }

        internal bool TryDispatchUnitDehighlighted(Unit unit)
        {
            IBattleUnit battleUnit = ResolveBattleUnitFromRegistryUnit(unit);
            if (battleUnit != null
                && TryDispatchToCustomState(state => state.OnUnitDehighlighted(battleUnit)))
            {
                return true;
            }

            return false;
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
