using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.UI;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Units
{
    public partial class Unit
    {
        internal void SyncMirroredRuntimeCell(BattleSquareCell battleCell)
        {
            BoardUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            if (battleCell == null)
            {
                runtimeUnit.ClearCurrentCell();
                return;
            }

            runtimeUnit.AssignCellImmediate(battleCell, syncTransform: false);
        }

        internal void ClearMirroredRuntimeCell()
        {
            BoardUnit runtimeUnit = ResolveRuntimeUnit();
            runtimeUnit?.ClearCurrentCell();
        }

        internal void PrepareRuntimeForTurnStart()
        {
            BoardUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            runtimeUnit.SetBaseMovementPoints(ComputedTotalMovementPoints);
        }

        internal void ApplyLegacyTurnStartFromRuntime()
        {
            cachedPaths = null;

            PassiveList?.OnTurnStart();
            BuffList?.OnTurnStart();
            RefreshHealthState();
            SkillList?.ResetTurnUsage();
            RaiseBuffsChanged();

            BoardUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                MovementPoints = ComputedTotalMovementPoints;
                SetTurnStateKind(UnitTurnStateKind.Friendly, syncRuntime: false);
                return;
            }

            MovementPoints = runtimeUnit.MovementPointsRemaining;
            UnitTurnStateKind turnState = ResolveLegacyTurnStateFromRuntime(runtimeUnit);
            if (turnState == UnitTurnStateKind.Normal)
            {
                turnState = UnitTurnStateKind.Friendly;
            }

            SetTurnStateKind(turnState, syncRuntime: false);
        }

        internal void SyncMirroredRuntimeNow()
        {
            if (HasPendingMove)
            {
                PushLegacyStateToRuntimeMirror();
                return;
            }

            if (ShouldPullLegacyFromRuntimeMirror())
            {
                PullRuntimeStateToLegacy(refreshOccupancy: true);
                return;
            }

            PushLegacyStateToRuntimeMirror();
        }

        internal void PullRuntimeStateToLegacy(bool refreshOccupancy = true)
        {
            BoardUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            MovementPoints = runtimeUnit.MovementPointsRemaining;
            SetTurnStateKind(ResolveLegacyTurnStateFromRuntime(runtimeUnit), syncRuntime: false);

            BattleSquareCell resolvedCell = ResolveLinkedLegacyCell(runtimeUnit.CurrentCell);
            if (resolvedCell != Cell)
            {
                UnregisterCellOccupancyList(Cell);
                RefreshCellOccupancy(Cell);
                Cell = resolvedCell;
                RegisterCellOccupancyList(resolvedCell);
            }

            if (refreshOccupancy)
            {
                RefreshCellOccupancy(resolvedCell);
                RefreshSceneOccupancyFromLiveUnits();
            }

            cachedPaths = null;
        }

        internal void ApplyLegacySyncFromRuntimeMoveCommit(CellGrid cellGrid)
        {
            PullRuntimeStateToLegacy();
            OnMoveFinished();
            cellGrid?.RequestBattleOutcomeEvaluation();
        }

        internal void RegisterCellOccupancyList(BattleSquareCell targetCell = null)
        {
            BattleSquareCell resolvedCell = targetCell ?? Cell;
            if (resolvedCell == null)
            {
                return;
            }

            if (!resolvedCell.CurrentUnits.Contains(this))
            {
                resolvedCell.CurrentUnits.Add(this);
            }

            resolvedCell.RefreshOccupancyFromCurrentUnits();
            RegisterCellOccupancy(resolvedCell);
        }

        internal void UnregisterCellOccupancyList(BattleSquareCell targetCell = null)
        {
            BattleSquareCell resolvedCell = targetCell ?? Cell;
            if (resolvedCell == null)
            {
                return;
            }

            resolvedCell.CurrentUnits.Remove(this);
            RefreshCellOccupancy(resolvedCell);
            UnregisterCellOccupancy(resolvedCell);
        }

        internal static void RefreshCellOccupancy(BattleSquareCell cell)
        {
            if (cell == null)
            {
                return;
            }

            bool hasLegacyOccupant = cell.CurrentUnits != null
                && cell.CurrentUnits.Any(occupant =>
                    occupant != null && occupant.Obstructable && !occupant.ExcludedFromBattle);
            bool hasSceneOccupant = HasBlockingSceneOccupant(cell, null);
            bool hasRuntimeOccupant = cell.Occupants.Any(unit => unit != null && unit.BlocksOtherUnits);

            cell.IsTaken = (!cell.IsTraversable)
                || hasLegacyOccupant
                || hasSceneOccupant
                || hasRuntimeOccupant;

            InvalidateAllCachedPaths();
        }

        internal void InvalidateCachedPaths()
        {
            cachedPaths = null;
        }

        internal static void InvalidateAllCachedPaths()
        {
            foreach (Unit unit in FindObjectsByType<Unit>())
            {
                if (unit != null)
                {
                    unit.cachedPaths = null;
                }
            }
        }

        private UnitTurnStateKind ResolveLegacyTurnStateFromRuntime(BoardUnit runtimeUnit)
        {
            if (runtimeUnit == null)
            {
                return UnitTurnStateKind.Normal;
            }

            UnitTurnStateKind runtimeKind = runtimeUnit.CurrentTurnStateKind;
            CellGrid cellGrid = FindSceneCellGrid();
            if (cellGrid == null || !cellGrid.IsBattleStarted)
            {
                return runtimeKind;
            }

            if (PlayerNumber != cellGrid.CurrentPlayerNumber)
            {
                return runtimeKind == UnitTurnStateKind.ReachableEnemy
                    ? UnitTurnStateKind.ReachableEnemy
                    : UnitTurnStateKind.Normal;
            }

            return runtimeKind;
        }

        private void PushLegacyStateToRuntimeMirror()
        {
            SyncMirroredRuntimeMovementPoints();
            SyncMirroredRuntimeTurnState();
            SyncMirroredRuntimeCell(Cell);
            SyncMirroredRuntimePendingMove();
        }

        private bool ShouldPullLegacyFromRuntimeMirror()
        {
            CellGrid cellGrid = FindSceneCellGrid();
            return cellGrid != null
                && cellGrid.IsBattleStarted
                && ResolveRuntimeUnit() != null;
        }

        private void SyncMirroredRuntimePendingMove()
        {
            BoardUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            if (!HasPendingMove)
            {
                if (runtimeUnit.HasPendingMove)
                {
                    runtimeUnit.CancelPendingMove();
                }

                return;
            }

            PendingMove pending = _pendingMove.Value;
            BoardCell runtimeDestination = ResolveLinkedRuntimeCell(pending.ToCell);
            if (runtimeDestination == null)
            {
                return;
            }

            if (runtimeUnit.HasPendingMove && runtimeUnit.PreviewCell == runtimeDestination)
            {
                return;
            }

            if (pending.ToCell == pending.FromCell)
            {
                runtimeUnit.BeginPendingMoveInPlace();
                return;
            }

            if (TryBuildRuntimeMovementPath(pending.Path, out List<BoardCell> runtimePath))
            {
                runtimeUnit.BeginPendingMove(runtimeDestination, runtimePath);
            }
        }

        private void SyncMirroredRuntimeTurnState()
        {
            BoardUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            UnitTurnState runtimeState = CurrentTurnStateKind switch
            {
                UnitTurnStateKind.Selected => new UnitTurnStateSelected(runtimeUnit),
                UnitTurnStateKind.ReachableEnemy => new UnitTurnStateReachableEnemy(runtimeUnit),
                UnitTurnStateKind.Friendly => new UnitTurnStateFriendly(runtimeUnit),
                UnitTurnStateKind.Finished => new UnitTurnStateFinished(runtimeUnit),
                _ => new UnitTurnStateNormal(runtimeUnit)
            };

            runtimeUnit.SetState(runtimeState);
        }

        private void SyncMirroredRuntimeMovementPoints()
        {
            BoardUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            runtimeUnit.SetMovementPointsRemaining(MovementPoints);
        }

        private bool HasBlockingRuntimeOccupant(BattleSquareCell cell)
        {
            if (cell == null)
            {
                return false;
            }

            BoardUnit runtimeUnit = ResolveRuntimeUnit();
            foreach (BoardUnit occupant in cell.Occupants)
            {
                if (occupant == null || occupant == runtimeUnit || !occupant.BlocksOtherUnits)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool IsLinkedBoardCellTraversable(BattleSquareCell cell)
        {
            return cell == null || cell.IsTraversable;
        }

        private bool TryBuildRuntimeMovementPath(IList<BattleSquareCell> movementPath, out List<BoardCell> orderedRuntimePath)
        {
            orderedRuntimePath = null;
            if (movementPath == null || movementPath.Count == 0)
            {
                return false;
            }

            var path = new List<BoardCell>(movementPath.Count);
            for (int i = movementPath.Count - 1; i >= 0; i--)
            {
                BoardCell runtimeCell = ResolveLinkedRuntimeCell(movementPath[i]);
                if (runtimeCell == null)
                {
                    return false;
                }

                path.Add(runtimeCell);
            }

            orderedRuntimePath = path;
            return true;
        }

        private static bool TryBuildLegacyMovementPath(
            IList<BoardCell> runtimePath,
            BoardCell originRuntimeCell,
            out List<BattleSquareCell> orderedLegacyPath)
        {
            orderedLegacyPath = null;
            if (runtimePath == null || runtimePath.Count == 0)
            {
                return false;
            }

            var path = new List<BattleSquareCell>(runtimePath.Count);
            for (int i = runtimePath.Count - 1; i >= 0; i--)
            {
                BoardCell runtimeCell = runtimePath[i];
                if (originRuntimeCell != null && runtimeCell == originRuntimeCell)
                {
                    continue;
                }

                BattleSquareCell legacyCell = ResolveLinkedLegacyCell(runtimeCell);
                if (legacyCell == null)
                {
                    return false;
                }

                path.Add(legacyCell);
            }

            orderedLegacyPath = path;
            return true;
        }

        private bool TryUseRuntimeMovementAuthority(out CellGrid cellGrid, out BoardUnit runtimeUnit)
        {
            cellGrid = FindSceneCellGrid();
            runtimeUnit = ResolveRuntimeUnit();
            return Application.isPlaying
                && cellGrid != null
                && runtimeUnit != null;
        }

        private bool TryUseRuntimePathAuthority(out CellGrid cellGrid, out BoardUnit runtimeUnit)
        {
            cellGrid = FindSceneCellGrid();
            runtimeUnit = ResolveRuntimeUnit();
            return TryUseRuntimeMovementAuthority(out cellGrid, out runtimeUnit);
        }

        private static void RefreshSceneOccupancyFromLiveUnits()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            FindSceneCellGrid()?.RefreshSceneCellOccupancyNow();
        }

        private static bool HasBlockingSceneOccupant(BattleSquareCell cell, Unit self)
        {
            if (cell == null)
            {
                return false;
            }

            CellGrid cellGrid = FindSceneCellGrid();
            if (cellGrid == null)
            {
                return false;
            }

            foreach (Unit unit in cellGrid.GetAllSceneUnitsFromHierarchy())
            {
                if (unit == null || unit == self || unit.Cell != cell || !unit.Obstructable || unit.ExcludedFromBattle)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private BoardUnit ResolveRuntimeUnit()
        {
            return GetComponent<BoardUnit>();
        }

        private static BoardCell ResolveLinkedRuntimeCell(BattleSquareCell cell)
        {
            return cell;
        }

        private static BattleSquareCell ResolveLinkedLegacyCell(BoardCell cell)
        {
            return cell as BattleSquareCell;
        }

        private static CellGrid FindSceneCellGrid()
        {
            return FindAnyObjectByType<CellGrid>();
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
            return FindSceneCellGrid()?.Is2D ?? true;
        }
    }
}
