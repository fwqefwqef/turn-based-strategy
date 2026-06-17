using System.Collections;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.AI;
using Windy.Srpg.Runtime.Grid;
using Windy.Srpg.Runtime.Players;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.AI.Actions
{
    public abstract class AIAction : AiDecisionAction
    {
        private static Player ResolvePlayer(IBattlePlayer player)
        {
            return player as Player;
        }

        private static Unit ResolveUnit(IGridUnit unit)
        {
            if (unit is Unit customUnit)
            {
                return customUnit;
            }

            return (unit as GridUnit)?.GetComponent<Unit>();
        }

        private static CellGrid ResolveCellGrid(IGridContext grid)
        {
            if (grid is CellGrid customCellGrid)
            {
                return customCellGrid;
            }

            return (grid as RuntimeGrid)?.GetComponent<CellGrid>();
        }

        public sealed override void InitializeDecision(IBattlePlayer player, IGridUnit unit, IGridContext grid)
        {
            Player customPlayer = ResolvePlayer(player);
            Unit customUnit = ResolveUnit(unit);
            CellGrid customCellGrid = ResolveCellGrid(grid);
            if (customPlayer != null && customUnit != null && customCellGrid != null)
            {
                InitializeAction(customPlayer, customUnit, customCellGrid);
            }
        }

        public sealed override bool ShouldExecute(IBattlePlayer player, IGridUnit unit, IGridContext grid)
        {
            Player customPlayer = ResolvePlayer(player);
            Unit customUnit = ResolveUnit(unit);
            CellGrid customCellGrid = ResolveCellGrid(grid);
            return customPlayer != null
                && customUnit != null
                && customCellGrid != null
                && ShouldExecute(customPlayer, customUnit, customCellGrid);
        }

        public sealed override void Precalculate(IBattlePlayer player, IGridUnit unit, IGridContext grid)
        {
            Player customPlayer = ResolvePlayer(player);
            Unit customUnit = ResolveUnit(unit);
            CellGrid customCellGrid = ResolveCellGrid(grid);
            if (customPlayer != null && customUnit != null && customCellGrid != null)
            {
                Precalculate(customPlayer, customUnit, customCellGrid);
            }
        }

        public sealed override IEnumerator ExecuteDecision(IBattlePlayer player, IGridUnit unit, IGridContext grid)
        {
            Player customPlayer = ResolvePlayer(player);
            Unit customUnit = ResolveUnit(unit);
            CellGrid customCellGrid = ResolveCellGrid(grid);
            if (customPlayer != null && customUnit != null && customCellGrid != null)
            {
                return Execute(customPlayer, customUnit, customCellGrid);
            }

            return null;
        }

        public sealed override void CleanUpDecision(IBattlePlayer player, IGridUnit unit, IGridContext grid)
        {
            Player customPlayer = ResolvePlayer(player);
            Unit customUnit = ResolveUnit(unit);
            CellGrid customCellGrid = ResolveCellGrid(grid);
            if (customPlayer != null && customUnit != null && customCellGrid != null)
            {
                CleanUp(customPlayer, customUnit, customCellGrid);
            }
        }

        public sealed override void ShowDebugDecisionInfo(IBattlePlayer player, IGridUnit unit, IGridContext grid)
        {
            Player customPlayer = ResolvePlayer(player);
            Unit customUnit = ResolveUnit(unit);
            CellGrid customCellGrid = ResolveCellGrid(grid);
            if (customPlayer != null && customUnit != null && customCellGrid != null)
            {
                ShowDebugInfo(customPlayer, customUnit, customCellGrid);
            }
        }

        public abstract void InitializeAction(Player player, Unit unit, CellGrid cellGrid);
        public abstract bool ShouldExecute(Player player, Unit unit, CellGrid cellGrid);
        public abstract void Precalculate(Player player, Unit unit, CellGrid cellGrid);
        public abstract IEnumerator Execute(Player player, Unit unit, CellGrid cellGrid);
        public abstract void CleanUp(Player player, Unit unit, CellGrid cellGrid);
        public abstract void ShowDebugInfo(Player player, Unit unit, CellGrid cellGrid);
    }
}

