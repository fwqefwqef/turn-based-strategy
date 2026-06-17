using System.Collections.Generic;
using System.Linq;
using TbsFramework.Cells;
using UnityEngine;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;

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

                if (!unit.Cell.CurrentUnits.Contains(unit.LegacyUnit))
                {
                    unit.Cell.CurrentUnits.Add(unit.LegacyUnit);
                }

                if (unit.Obstructable)
                {
                    unit.Cell.IsTaken = true;
                }
            }

            RebuildRuntimeBoardCellOccupancy();
            occupancyRevision++;
            CustomUnit.InvalidateAllCachedPaths();
        }

        private void RebuildRuntimeBoardCellOccupancy()
        {
            foreach (Cell cell in GetAllCells())
            {
                BoardCell runtimeCell = GetRuntimeCell(cell);
                runtimeCell?.ClearOccupants();
            }

            foreach (CustomUnit unit in GetAllSceneCustomUnitsFromHierarchy())
            {
                if (unit == null || unit.ExcludedFromBattle)
                {
                    continue;
                }

                BattleUnit runtimeUnit = unit.GetComponent<BattleUnit>();
                if (runtimeUnit == null)
                {
                    continue;
                }

                if (unit.Cell == null)
                {
                    runtimeUnit.ClearCurrentCell();
                    continue;
                }

                BoardCell runtimeCell = GetRuntimeCell(unit.Cell);
                if (runtimeCell == null)
                {
                    runtimeUnit.ClearCurrentCell();
                    continue;
                }

                runtimeUnit.AssignCellImmediate(runtimeCell, syncTransform: false);
            }
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
