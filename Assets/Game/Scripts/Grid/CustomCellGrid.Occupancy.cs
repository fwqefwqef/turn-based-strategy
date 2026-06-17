using System.Collections.Generic;
using System.Linq;
using TbsFramework.Cells;
using UnityEngine;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid
{
    public partial class CustomCellGrid
    {
        public void RefreshSceneCellOccupancyNow()
        {
            RebuildSceneCellOccupancy();
        }

        private void RebuildSceneCellOccupancy()
        {
            List<Cell> allCells = GetAllCells();
            if (allCells.Count == 0)
            {
                return;
            }

            foreach (Cell cell in allCells)
            {
                if (cell == null)
                {
                    continue;
                }

                cell.CurrentUnits.Clear();
                cell.IsTaken = IsCellBlockedByTerrain(cell);
            }

            foreach (CustomUnit unit in GetAllSceneCustomUnitsFromHierarchy())
            {
                if (unit == null || unit.ExcludedFromBattle || unit.Cell == null)
                {
                    continue;
                }

                if (!unit.Cell.CurrentUnits.Contains(unit))
                {
                    unit.Cell.CurrentUnits.Add(unit);
                }

                if (unit.Obstructable)
                {
                    unit.Cell.IsTaken = true;
                }
            }

            occupancyRevision++;
            CustomUnit.InvalidateAllCachedPaths();
        }

        private static bool ShouldSyncRuntimeCellOccupancy(CustomUnit unit)
        {
            return Application.isPlaying && unit != null && unit.HasInitializedTurnState;
        }

        private static void RefreshCellOccupancy(Cell cell)
        {
            if (cell == null)
            {
                return;
            }

            cell.IsTaken = IsCellBlockedByTerrain(cell)
                || cell.CurrentUnits.Any(unit => unit != null && unit.Obstructable && !unit.ExcludedFromBattle);
            CustomUnit.InvalidateAllCachedPaths();
        }

        private static bool IsCellBlockedByTerrain(Cell cell)
        {
            BoardCell runtimeCell = GetRuntimeCell(cell);
            return runtimeCell != null && !runtimeCell.IsTraversable;
        }
    }
}
