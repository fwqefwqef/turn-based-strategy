using System.Collections.Generic;
using System.Linq;
using TbsFramework.Cells;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.UI;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Units
{
    public partial class CustomUnit
    {
        private bool HasBlockingRuntimeOccupant(Cell cell)
        {
            BoardCell runtimeCell = ResolveLinkedRuntimeCell(cell);
            if (runtimeCell == null)
            {
                return false;
            }

            BattleUnit runtimeUnit = ResolveRuntimeUnit();
            foreach (BattleUnit occupant in runtimeCell.Occupants)
            {
                if (occupant == null || occupant == runtimeUnit || !occupant.BlocksOtherUnits)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool IsLinkedBoardCellTraversable(Cell cell)
        {
            BoardCell runtimeCell = ResolveLinkedRuntimeCell(cell);
            return runtimeCell == null || runtimeCell.IsTraversable;
        }

        // Maps a framework movement path (ordered destination-first, as produced by the pathfinder)
        // to its runtime BoardCell equivalents in origin-to-destination order. Returns false if any
        // cell lacks a runtime mirror, so the caller can fall back to framework animation.
        private bool TryBuildRuntimeMovementPath(IList<Cell> frameworkPath, out System.Collections.Generic.List<BoardCell> orderedRuntimePath)
        {
            orderedRuntimePath = null;
            if (frameworkPath == null || frameworkPath.Count == 0)
            {
                return false;
            }

            var path = new System.Collections.Generic.List<BoardCell>(frameworkPath.Count);
            for (int i = frameworkPath.Count - 1; i >= 0; i--)
            {
                BoardCell runtimeCell = ResolveLinkedRuntimeCell(frameworkPath[i]);
                if (runtimeCell == null)
                {
                    return false;
                }

                path.Add(runtimeCell);
            }

            orderedRuntimePath = path;
            return true;
        }

        private static bool TryBuildLegacyMovementPath(IList<BoardCell> runtimePath, out List<Cell> orderedLegacyPath)
        {
            orderedLegacyPath = null;
            if (runtimePath == null || runtimePath.Count == 0)
            {
                return false;
            }

            var path = new List<Cell>(runtimePath.Count);
            for (int i = runtimePath.Count - 1; i >= 0; i--)
            {
                Cell legacyCell = ResolveLinkedLegacyCell(runtimePath[i]);
                if (legacyCell == null)
                {
                    return false;
                }

                path.Add(legacyCell);
            }

            orderedLegacyPath = path;
            return true;
        }

        private bool TryUseRuntimeMovementAuthority(out CustomCellGrid cellGrid, out BattleUnit runtimeUnit)
        {
            cellGrid = FindSceneCellGrid();
            runtimeUnit = ResolveRuntimeUnit();
            return Application.isPlaying
                && cellGrid != null
                && cellGrid.UseRuntimeMovementExecution
                && cellGrid.IsHumanTurn
                && runtimeUnit != null;
        }

        private bool TryUseRuntimePathAuthority(out CustomCellGrid cellGrid, out BattleUnit runtimeUnit)
        {
            cellGrid = FindSceneCellGrid();
            runtimeUnit = ResolveRuntimeUnit();
            return Application.isPlaying
                && cellGrid != null
                && runtimeUnit != null;
        }

        private static void RefreshLegacyCellOccupancy(Cell cell)
        {
            if (cell == null)
            {
                return;
            }

            BoardCell runtimeCell = ResolveLinkedRuntimeCell(cell);
            bool hasLegacyOccupant = cell.CurrentUnits != null
                && cell.CurrentUnits.Any(unit => unit != null && unit.Obstructable && !unit.ExcludedFromBattle);
            bool hasSceneOccupant = HasBlockingSceneOccupant(cell, null);
            bool hasRuntimeOccupant = runtimeCell != null
                && runtimeCell.Occupants.Any(unit => unit != null && unit.BlocksOtherUnits);

            cell.IsTaken = (runtimeCell != null && !runtimeCell.IsTraversable)
                || hasLegacyOccupant
                || hasSceneOccupant
                || hasRuntimeOccupant;

            InvalidateAllCachedPaths();
        }

        private static void RefreshSceneOccupancyFromLiveUnits()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            FindSceneCustomCellGrid()?.RefreshSceneCellOccupancyNow();
        }

        private static bool HasBlockingSceneOccupant(Cell cell, CustomUnit self)
        {
            if (cell == null)
            {
                return false;
            }

            CustomCellGrid cellGrid = FindSceneCustomCellGrid();
            if (cellGrid == null)
            {
                return false;
            }

            foreach (CustomUnit unit in cellGrid.GetAllSceneCustomUnitsFromHierarchy())
            {
                if (unit == null || unit == self || unit.Cell != cell || !unit.Obstructable || unit.ExcludedFromBattle)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        internal void InvalidateCachedPaths()
        {
            cachedPaths = null;
        }

        internal static void InvalidateAllCachedPaths()
        {
            foreach (CustomUnit unit in FindObjectsByType<CustomUnit>())
            {
                if (unit != null)
                {
                    unit.cachedPaths = null;
                }
            }
        }

        private static CustomCellGrid FindSceneCellGrid()
        {
            return FindAnyObjectByType<CustomCellGrid>();
        }

        private static CustomCellGrid FindSceneCustomCellGrid()
        {
            return FindSceneCellGrid();
        }

        private static ExperienceGainHUD FindSceneExperienceGainHud()
        {
            return FindAnyObjectByType<ExperienceGainHUD>();
        }

        private static LevelUpUI FindSceneLevelUpUi()
        {
            return FindAnyObjectByType<LevelUpUI>();
        }

        private static CombatSequenceUI FindSceneCombatSequenceUi()
        {
            return FindAnyObjectByType<CombatSequenceUI>();
        }

        private static bool IsSceneGrid2D()
        {
            return FindSceneCustomCellGrid()?.Is2D ?? true;
        }
    }
}
