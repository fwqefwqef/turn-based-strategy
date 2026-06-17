using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Board.States;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CellGrid
    {
        private BattleBoard runtimeBoard;
        private bool runtimeBoardCollectionsDirty = true;

        private void NormalizeHumanInputState()
        {
            if (!IsHumanTurn)
            {
                return;
            }

            if (CurrentState is CellGridStateWaitingForInput)
            {
                EnterWaitingState();
            }
        }

        private void ResolveRuntimeBoard()
        {
            if (runtimeBoard == null)
            {
                runtimeBoard = GetComponent<BattleBoard>();
            }
        }

        private void ApplyRuntimeBoardMirror()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            RefreshRuntimeBoardCollectionsIfNeeded();
            UpdateRuntimeBoardMetadata();
        }

        private void ApplyRuntimeDrivenState(BoardState runtimeState, System.Action applyLegacyState)
        {
            if (runtimeState == null)
            {
                applyLegacyState?.Invoke();
                return;
            }

            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                applyLegacyState?.Invoke();
                return;
            }

            RefreshRuntimeBoardCollectionsIfNeeded();
            UpdateRuntimeBoardMetadata();
            runtimeBoard.SetState(runtimeState);
            ApplyLegacyStateFromRuntime(applyLegacyState);
        }

        private void MirrorLegacyStateToRuntimeBoard(CellGridState legacyState)
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null || legacyState == null)
            {
                return;
            }

            RefreshRuntimeBoardCollectionsIfNeeded();
            UpdateRuntimeBoardMetadata();

            BoardState runtimeState = BuildRuntimeStateFromLegacyState(legacyState);
            if (runtimeState != null)
            {
                runtimeBoard.SetState(runtimeState);
            }
        }

        private BoardState BuildRuntimeStateFromLegacyState(CellGridState legacyState)
        {
            return legacyState switch
            {
                CellGridStateWaitingForInput => new BoardStateWaitingForInput(runtimeBoard),
                CellGridStateBlockInput => new BoardStateBlockedInput(runtimeBoard),
                CellGridStateRemotePlayerTurn => new BoardStateBlockedInput(runtimeBoard),
                PreBattleDeploymentSwapState => new BoardStateBlockedInput(runtimeBoard),
                CellGridStateAiTurn => new BoardStateAiTurn(runtimeBoard),
                UnitSelectedState selectedState => BuildRuntimeSelectedState(selectedState),
                CellGridStateMovePendingConfirm pendingState => BuildRuntimePendingMoveState(pendingState),
                _ => new BoardStateBlockedInput(runtimeBoard)
            };
        }

        private BoardState BuildRuntimeSelectedState(UnitSelectedState selectedState)
        {
            return BuildRuntimeSelectedState(selectedState?.SelectedUnit);
        }

        private BoardState BuildRuntimeSelectedState(Unit unit)
        {
            BoardUnit runtimeSelected = GetRuntimeUnit(unit);
            return runtimeSelected != null
                ? new BoardStateUnitSelected(runtimeBoard, runtimeSelected)
                : new BoardStateWaitingForInput(runtimeBoard);
        }

        private BoardState BuildRuntimePendingMoveState(CellGridStateMovePendingConfirm pendingState)
        {
            return BuildRuntimePendingMoveState(pendingState?.MoveAbility);
        }

        private BoardState BuildRuntimePendingMoveState(MoveAbility moveAbility)
        {
            Unit legacyUnit = moveAbility != null ? moveAbility.GetComponent<Unit>() : null;
            BoardUnit runtimeUnit = GetRuntimeUnit(legacyUnit);
            BattleSquareCell legacyDestination = legacyUnit != null && legacyUnit.HasPendingMove
                ? legacyUnit.PreviewCell
                : moveAbility?.Destination ?? legacyUnit?.PreviewCell ?? legacyUnit?.Cell;
            BoardCell runtimeDestination = GetRuntimeCell(legacyDestination);

            return runtimeUnit != null
                ? new BoardStateUnitMovePendingConfirm(runtimeBoard, runtimeUnit, runtimeDestination)
                : new BoardStateWaitingForInput(runtimeBoard);
        }

        private RuntimeStateTransitionDecision CaptureRuntimeDecision()
        {
            return new RuntimeStateTransitionDecision(
                runtimeBoard?.CurrentState?.DiagnosticStateLabel ?? "Waiting",
                ResolveSceneUnit(runtimeBoard?.CurrentState?.SelectedUnit),
                ResolveSceneCell(runtimeBoard?.CurrentState?.PendingDestination));
        }

        private void RefreshRuntimeBoardCollectionsIfNeeded()
        {
            if (!runtimeBoardCollectionsDirty)
            {
                return;
            }

            runtimeBoard.SetMirroredCollections(
                GetAllBoardCells().Select(GetRuntimeCell),
                GetAllUnits().Select(GetRuntimeUnit),
                GetOrderedPlayers().Cast<Runtime.Players.IBattlePlayer>());
            runtimeBoardCollectionsDirty = false;
        }

        private void UpdateRuntimeBoardMetadata()
        {
            runtimeBoard.SetBattleStarted(!IsPreBattlePhase);

            if (CurrentPlayer != null)
            {
                runtimeBoard.SetCurrentPlayerById(CurrentPlayer.PlayerNumber);
            }
        }

        [ContextMenu("Sync Runtime Mirror Now")]
        public void SyncRuntimeMirrorNow()
        {
            MarkRuntimeBoardDirty();

            foreach (Unit unit in GetAllUnits())
            {
                unit?.SyncMirroredRuntimeNow();
            }

            ApplyRuntimeBoardMirror();
        }

        public void ClearAllCellHighlights()
        {
            foreach (BattleSquareCell cell in GetAllBoardCells())
            {
                if (cell == null)
                {
                    continue;
                }

                cell.UnMark();
                GetRuntimeCell(cell)?.ClearHighlight();
            }
        }

        private void MarkRuntimeBoardDirty()
        {
            runtimeBoardCollectionsDirty = true;
        }

        private static BoardUnit GetRuntimeUnit(Unit unit)
        {
            return unit != null ? unit.GetComponent<BoardUnit>() : null;
        }

        private static BoardCell GetRuntimeCell(BattleSquareCell cell)
        {
            return cell;
        }

        private static Unit ResolveSceneUnit(BoardUnit unit)
        {
            return unit != null ? unit.GetComponent<Unit>() : null;
        }

        private static BattleSquareCell ResolveSceneCell(BoardCell cell)
        {
            return cell as BattleSquareCell;
        }
    }
}
