using System.Collections.Generic;
using TbsFramework.Cells;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;
using FrameworkBuff = TbsFramework.Units.Buff;

namespace Windy.Srpg.Game.Units
{
    public partial class CustomUnit
    {
        internal void SyncMirroredRuntimeCell(Cell legacyCell)
        {
            BattleUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            if (legacyCell == null)
            {
                runtimeUnit.ClearCurrentCell();
                return;
            }

            BoardCell runtimeCell = ResolveLinkedRuntimeCell(legacyCell);
            if (runtimeCell == null)
            {
                runtimeUnit.ClearCurrentCell();
                return;
            }

            runtimeUnit.AssignCellImmediate(runtimeCell, syncTransform: false);
        }

        internal void ClearMirroredRuntimeCell()
        {
            BattleUnit runtimeUnit = ResolveRuntimeUnit();
            runtimeUnit?.ClearCurrentCell();
        }

        internal void PrepareRuntimeForTurnStart()
        {
            BattleUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            runtimeUnit.SetBaseMovementPoints(ComputedTotalMovementPoints);
        }

        /// <summary>
        /// Applies game-owned turn-start hooks, then pulls MP/turn visuals from the runtime unit.
        /// </summary>
        internal void ApplyLegacyTurnStartFromRuntime()
        {
            cachedPaths = null;

            legacyBuffs ??= new List<(FrameworkBuff, int)>();
            legacyBuffs.FindAll(b => b.timeLeft == 0).ForEach(b => { b.buff.Undo(LegacyUnit); });
            legacyBuffs.RemoveAll(b => b.timeLeft == 0);
            PassiveList?.OnTurnStart();
            BuffList?.OnTurnStart();
            RefreshHealthState();
            SkillList?.ResetTurnUsage();
            RaiseBuffsChanged();

            BattleUnit runtimeUnit = ResolveRuntimeUnit();
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

        private UnitTurnStateKind ResolveLegacyTurnStateFromRuntime(BattleUnit runtimeUnit)
        {
            if (runtimeUnit == null)
            {
                return UnitTurnStateKind.Normal;
            }

            UnitTurnStateKind runtimeKind = runtimeUnit.CurrentTurnStateKind;
            CustomCellGrid cellGrid = FindSceneCellGrid();
            if (cellGrid == null || !cellGrid.IsBattleStarted)
            {
                return runtimeKind;
            }

            if (PlayerNumber != cellGrid.CurrentCustomPlayerNumber)
            {
                // Finished/selected/friendly only apply while that side is acting; keep attack-range tint.
                return runtimeKind == UnitTurnStateKind.ReachableEnemy
                    ? UnitTurnStateKind.ReachableEnemy
                    : UnitTurnStateKind.Normal;
            }

            return runtimeKind;
        }

        /// <summary>
        /// Keeps legacy and runtime mirrors aligned. During battle, runtime is authoritative unless
        /// a pending move still lives on the legacy side (preview / confirm path).
        /// </summary>
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

        /// <summary>
        /// Pulls cell, MP, and turn state from the runtime unit into legacy scene/framework fields.
        /// </summary>
        internal void PullRuntimeStateToLegacy(bool refreshOccupancy = true)
        {
            BattleUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            MovementPoints = runtimeUnit.MovementPointsRemaining;
            SetTurnStateKind(ResolveLegacyTurnStateFromRuntime(runtimeUnit), syncRuntime: false);

            Cell resolvedCell = ResolveLinkedLegacyCell(runtimeUnit.CurrentCell);
            if (resolvedCell != this.Cell)
            {
                UnregisterLegacyCellOccupancy(this.Cell);
                RefreshLegacyCellOccupancy(this.Cell);
                this.Cell = resolvedCell;
                RegisterLegacyCellOccupancy(resolvedCell);
            }

            if (refreshOccupancy)
            {
                RefreshLegacyCellOccupancy(resolvedCell);
                RefreshSceneOccupancyFromLiveUnits();
            }

            cachedPaths = null;
        }

        /// <summary>
        /// Applies legacy occupancy and MP after a runtime-led move commit.
        /// </summary>
        internal void ApplyLegacySyncFromRuntimeMoveCommit(CustomCellGrid cellGrid)
        {
            PullRuntimeStateToLegacy();
            OnMoveFinished();
            cellGrid?.RequestBattleOutcomeEvaluation();
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
            CustomCellGrid cellGrid = FindSceneCellGrid();
            return cellGrid != null
                && cellGrid.IsBattleStarted
                && ResolveRuntimeUnit() != null;
        }

        private void SyncMirroredRuntimePendingMove()
        {
            BattleUnit runtimeUnit = ResolveRuntimeUnit();
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
            BattleUnit runtimeUnit = ResolveRuntimeUnit();
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
            BattleUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            runtimeUnit.SetMovementPointsRemaining(MovementPoints);
        }

        private BattleUnit ResolveRuntimeUnit()
        {
            return GetComponent<BattleUnit>();
        }

        private static BoardCell ResolveLinkedRuntimeCell(Cell cell)
        {
            return cell != null ? cell.GetComponent<BoardCell>() : null;
        }

        private static Cell ResolveLinkedLegacyCell(BoardCell cell)
        {
            return cell != null ? cell.GetComponent<Cell>() : null;
        }
    }
}
