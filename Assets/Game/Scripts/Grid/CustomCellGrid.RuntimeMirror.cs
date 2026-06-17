using System.Linq;
using TbsFramework.Cells;
using UnityEngine;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Board.States;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CustomCellGrid
    {
        internal readonly struct RuntimeStateTransitionDecision
        {
            public RuntimeStateTransitionDecision(string stateLabel, CustomUnit selectedUnit, Cell pendingDestination)
            {
                StateLabel = stateLabel;
                SelectedUnit = selectedUnit;
                PendingDestination = pendingDestination;
            }

            public string StateLabel { get; }
            public CustomUnit SelectedUnit { get; }
            public Cell PendingDestination { get; }
        }

        private BattleBoard runtimeBoard;
        private bool runtimeBoardCollectionsDirty = true;

        private void NormalizeHumanInputState()
        {
            if (!IsHumanTurn)
            {
                return;
            }

            if (IsLegacyWaitingForInputState(cellGridState))
            {
                EnterWaitingState();
            }
        }

        private static bool IsLegacyWaitingForInputState(object state)
        {
            return state != null
                && state.GetType() != typeof(LegacyCustomCellGridStateAdapter)
                && state.GetType().Name == "CellGridStateWaitingForInput";
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

        private void MirrorLegacyStateToRuntimeBoard(CustomCellGridState legacyState)
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

        private BoardState BuildRuntimeStateFromLegacyState(CustomCellGridState legacyState)
        {
            return legacyState switch
            {
                CustomCellGridStateWaitingForInput => new BoardStateWaitingForInput(runtimeBoard),
                CustomCellGridStateBlockInput => new BoardStateBlockedInput(runtimeBoard),
                CustomCellGridStateRemotePlayerTurn => new BoardStateBlockedInput(runtimeBoard),
                PreBattleDeploymentSwapState => new BoardStateBlockedInput(runtimeBoard),
                CustomCellGridStateAiTurn => new BoardStateAiTurn(runtimeBoard),
                CustomUnitSelectedState selectedState => BuildRuntimeSelectedState(selectedState),
                CustomCellGridStateMovePendingConfirm pendingState => BuildRuntimePendingMoveState(pendingState),
                _ => new BoardStateBlockedInput(runtimeBoard)
            };
        }

        private BoardState BuildRuntimeSelectedState(CustomUnitSelectedState selectedState)
        {
            BattleUnit runtimeSelected = GetRuntimeUnit(selectedState?.SelectedUnit);
            return runtimeSelected != null
                ? new BoardStateUnitSelected(runtimeBoard, runtimeSelected)
                : new BoardStateWaitingForInput(runtimeBoard);
        }

        private BoardState BuildRuntimePendingMoveState(CustomCellGridStateMovePendingConfirm pendingState)
        {
            CustomMoveAbility moveAbility = pendingState?.MoveAbility;
            CustomUnit legacyUnit = moveAbility != null ? moveAbility.GetComponent<CustomUnit>() : null;
            BattleUnit runtimeUnit = GetRuntimeUnit(legacyUnit);
            BoardCell runtimeDestination = GetRuntimeCell(legacyUnit?.PreviewCell);

            return runtimeUnit != null
                ? new BoardStateUnitMovePendingConfirm(runtimeBoard, runtimeUnit, runtimeDestination)
                : new BoardStateWaitingForInput(runtimeBoard);
        }

        private RuntimeStateTransitionDecision CaptureRuntimeDecision()
        {
            return new RuntimeStateTransitionDecision(
                runtimeBoard?.CurrentState?.DiagnosticStateLabel ?? "Waiting",
                GetLegacyUnit(runtimeBoard?.CurrentState?.SelectedUnit),
                GetLegacyCell(runtimeBoard?.CurrentState?.PendingDestination));
        }

        private void RefreshRuntimeBoardCollectionsIfNeeded()
        {
            if (!runtimeBoardCollectionsDirty)
            {
                return;
            }

            runtimeBoard.SetMirroredCollections(
                GetAllCells().Select(GetRuntimeCell),
                GetAllCustomUnits().Select(GetRuntimeUnit),
                GetOrderedCustomPlayers().Cast<Runtime.Players.IBattlePlayer>());
            runtimeBoardCollectionsDirty = false;
        }

        private void UpdateRuntimeBoardMetadata()
        {
            runtimeBoard.SetBattleStarted(!IsPreBattlePhase);

            if (CurrentCustomPlayer != null)
            {
                runtimeBoard.SetCurrentPlayerById(CurrentCustomPlayer.PlayerNumber);
            }
        }

        [ContextMenu("Sync Runtime Mirror Now")]
        public void SyncRuntimeMirrorNow()
        {
            MarkRuntimeBoardDirty();

            foreach (CustomUnit unit in GetAllCustomUnits())
            {
                unit?.SyncMirroredRuntimeNow();
            }

            ApplyRuntimeBoardMirror();
        }

        private void MarkRuntimeBoardDirty()
        {
            runtimeBoardCollectionsDirty = true;
        }

        /// <summary>
        /// Shadow-harness hook: drives the runtime board's real waiting-for-input logic with the
        /// same clicked unit the framework just processed, then logs whether the runtime would
        /// select the same unit the framework selected. Non-authoritative — the runtime board is
        /// only evaluated, never committed.
        /// </summary>
        internal void ShadowCompareSelection(CustomUnit clickedUnit, CustomUnit frameworkSelectedUnit)
        {
            if (!Diagnostics.RuntimeParityDiagnostics.LogSelectionParity)
            {
                return;
            }

            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            SyncRuntimeMirrorNow();

            BattleUnit runtimeClicked = GetRuntimeUnit(clickedUnit);
            BattleUnit runtimeDecided = runtimeBoard.ShadowEvaluateUnitClickFromWaiting(runtimeClicked);
            BattleUnit expectedRuntime = GetRuntimeUnit(frameworkSelectedUnit);

            Diagnostics.RuntimeParityDiagnostics.CompareSelection(
                clickedUnit, frameworkSelectedUnit, runtimeDecided, expectedRuntime);
        }

        internal RuntimeStateTransitionDecision ProcessRuntimeWaitingStateUnitClick(CustomUnit clickedUnit)
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return new RuntimeStateTransitionDecision("Waiting", null, null);
            }

            SyncRuntimeMirrorNow();
            runtimeBoard.ProcessUnitClick(GetRuntimeUnit(clickedUnit));
            return CaptureRuntimeDecision();
        }

        internal CustomUnit EvaluateRuntimeSelectionFromWaiting(CustomUnit clickedUnit)
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return null;
            }

            SyncRuntimeMirrorNow();
            BattleUnit runtimeClicked = GetRuntimeUnit(clickedUnit);
            BattleUnit runtimeDecided = runtimeBoard.ShadowEvaluateUnitClickFromWaiting(runtimeClicked);
            return GetLegacyUnit(runtimeDecided);
        }

        internal void ShadowCompareRightClick(CustomUnit previouslySelectedUnit, CustomUnit frameworkSelectedUnitAfterRightClick)
        {
            if (!Diagnostics.RuntimeParityDiagnostics.LogRightClickParity)
            {
                return;
            }

            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            SyncRuntimeMirrorNow();

            BattleUnit runtimePreviouslySelected = GetRuntimeUnit(previouslySelectedUnit);
            BattleUnit runtimeSelectedAfterRightClick = runtimeBoard.ShadowEvaluateRightClickFromSelected(runtimePreviouslySelected);
            BattleUnit expectedRuntimeAfterRightClick = GetRuntimeUnit(frameworkSelectedUnitAfterRightClick);

            Diagnostics.RuntimeParityDiagnostics.CompareRightClick(
                previouslySelectedUnit,
                frameworkSelectedUnitAfterRightClick,
                runtimeSelectedAfterRightClick,
                expectedRuntimeAfterRightClick);
        }

        internal RuntimeStateTransitionDecision ProcessRuntimeRightClick()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return new RuntimeStateTransitionDecision("Waiting", null, null);
            }

            SyncRuntimeMirrorNow();
            runtimeBoard.ProcessRightClick();
            return CaptureRuntimeDecision();
        }

        internal RuntimeStateTransitionDecision EvaluateRuntimeSelectedStateRightClick(CustomUnit previouslySelectedUnit)
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return new RuntimeStateTransitionDecision("Waiting", null, null);
            }

            SyncRuntimeMirrorNow();

            BattleUnit runtimePreviouslySelected = GetRuntimeUnit(previouslySelectedUnit);
            BattleUnit runtimeSelectedAfterRightClick = runtimeBoard.ShadowEvaluateRightClickFromSelected(runtimePreviouslySelected);

            return new RuntimeStateTransitionDecision(
                runtimeSelectedAfterRightClick != null ? "Selected" : "Waiting",
                GetLegacyUnit(runtimeSelectedAfterRightClick),
                null);
        }

        internal RuntimeStateTransitionDecision EvaluateRuntimeSelectedStateUnitClick(CustomUnit previouslySelectedUnit, CustomUnit clickedUnit)
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return new RuntimeStateTransitionDecision("Waiting", null, null);
            }

            SyncRuntimeMirrorNow();

            BattleUnit runtimePreviouslySelected = GetRuntimeUnit(previouslySelectedUnit);
            BattleUnit runtimeClicked = GetRuntimeUnit(clickedUnit);
            BoardState runtimeState = runtimeBoard.ShadowEvaluateUnitClickFromSelected(runtimePreviouslySelected, runtimeClicked);

            return new RuntimeStateTransitionDecision(
                runtimeState?.DiagnosticStateLabel ?? "Waiting",
                GetLegacyUnit(runtimeState?.SelectedUnit),
                GetLegacyCell(runtimeState?.PendingDestination));
        }

        internal RuntimeStateTransitionDecision ProcessRuntimeSelectedStateUnitClick(CustomUnit clickedUnit)
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return new RuntimeStateTransitionDecision("Waiting", null, null);
            }

            SyncRuntimeMirrorNow();
            runtimeBoard.ProcessUnitClick(GetRuntimeUnit(clickedUnit));
            return CaptureRuntimeDecision();
        }

        internal RuntimeStateTransitionDecision EvaluateRuntimeSelectedStateCellClick(CustomUnit previouslySelectedUnit, Cell clickedCell)
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return new RuntimeStateTransitionDecision("Waiting", null, null);
            }

            SyncRuntimeMirrorNow();

            BattleUnit runtimePreviouslySelected = GetRuntimeUnit(previouslySelectedUnit);
            BoardCell runtimeClicked = GetRuntimeCell(clickedCell);
            BoardState runtimeState = runtimeBoard.ShadowEvaluateCellClickFromSelected(runtimePreviouslySelected, runtimeClicked);

            return new RuntimeStateTransitionDecision(
                runtimeState?.DiagnosticStateLabel ?? "Waiting",
                GetLegacyUnit(runtimeState?.SelectedUnit),
                GetLegacyCell(runtimeState?.PendingDestination));
        }

        internal RuntimeStateTransitionDecision ProcessRuntimeSelectedStateCellClick(Cell clickedCell)
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return new RuntimeStateTransitionDecision("Waiting", null, null);
            }

            SyncRuntimeMirrorNow();
            runtimeBoard.ProcessCellClick(GetRuntimeCell(clickedCell));
            return CaptureRuntimeDecision();
        }

        internal RuntimeStateTransitionDecision EvaluateRuntimePendingMoveRightClick(CustomUnit frameworkUnit, Cell simulatedFrameworkPendingDestination)
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return new RuntimeStateTransitionDecision("Selected", frameworkUnit, null);
            }

            SyncRuntimeMirrorNow();

            BattleUnit runtimeUnit = GetRuntimeUnit(frameworkUnit);
            BoardCell runtimePendingDestination = GetRuntimeCell(simulatedFrameworkPendingDestination);
            BattleBoard.ShadowTransitionSnapshot runtimeSnapshot =
                runtimeBoard.ShadowEvaluatePendingMoveRightClick(runtimeUnit, runtimePendingDestination);

            return new RuntimeStateTransitionDecision(
                runtimeSnapshot.StateLabel,
                GetLegacyUnit(runtimeSnapshot.SelectedUnit),
                GetLegacyCell(runtimeSnapshot.PendingDestination));
        }

        internal RuntimeStateTransitionDecision EvaluateRuntimePendingMoveWait(CustomUnit frameworkUnit, Cell simulatedFrameworkPendingDestination)
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return new RuntimeStateTransitionDecision("Waiting", null, null);
            }

            SyncRuntimeMirrorNow();

            BattleUnit runtimeUnit = GetRuntimeUnit(frameworkUnit);
            BoardCell runtimePendingDestination = GetRuntimeCell(simulatedFrameworkPendingDestination);
            BattleBoard.ShadowTransitionSnapshot runtimeSnapshot =
                runtimeBoard.ShadowEvaluatePendingMoveWait(runtimeUnit, runtimePendingDestination);

            return new RuntimeStateTransitionDecision(
                runtimeSnapshot.StateLabel,
                GetLegacyUnit(runtimeSnapshot.SelectedUnit),
                GetLegacyCell(runtimeSnapshot.PendingDestination));
        }

        internal RuntimeStateTransitionDecision ProcessRuntimePendingMoveRightClick()
        {
            return ProcessRuntimeRightClick();
        }

        internal RuntimeStateTransitionDecision ProcessRuntimePendingMoveWait()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return new RuntimeStateTransitionDecision("Waiting", null, null);
            }

            SyncRuntimeMirrorNow();
            runtimeBoard.ConfirmPendingMoveWait();
            return CaptureRuntimeDecision();
        }

        internal void ShadowCompareSelectedStateUnitClick(
            CustomUnit previouslySelectedUnit,
            CustomUnit clickedUnit,
            string frameworkStateLabel,
            CustomUnit frameworkSelectedUnitAfterClick,
            Cell frameworkPendingDestination)
        {
            if (!Diagnostics.RuntimeParityDiagnostics.LogSelectedMoveParity)
            {
                return;
            }

            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            SyncRuntimeMirrorNow();

            BattleUnit runtimePreviouslySelected = GetRuntimeUnit(previouslySelectedUnit);
            BattleUnit runtimeClicked = GetRuntimeUnit(clickedUnit);
            BoardState runtimeState = runtimeBoard.ShadowEvaluateUnitClickFromSelected(runtimePreviouslySelected, runtimeClicked);
            BattleUnit expectedRuntimeSelected = GetRuntimeUnit(frameworkSelectedUnitAfterClick);
            BoardCell expectedRuntimePendingDestination = GetRuntimeCell(frameworkPendingDestination);

            Diagnostics.RuntimeParityDiagnostics.CompareSelectedStateTransition(
                $"Unit click on {Describe(clickedUnit)}",
                previouslySelectedUnit,
                frameworkStateLabel,
                frameworkSelectedUnitAfterClick,
                frameworkPendingDestination,
                runtimeState,
                expectedRuntimeSelected,
                expectedRuntimePendingDestination);
        }

        internal void ShadowCompareSelectedStateCellClick(
            CustomUnit previouslySelectedUnit,
            Cell clickedCell,
            string frameworkStateLabel,
            CustomUnit frameworkSelectedUnitAfterClick,
            Cell frameworkPendingDestination)
        {
            if (!Diagnostics.RuntimeParityDiagnostics.LogSelectedMoveParity)
            {
                return;
            }

            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            SyncRuntimeMirrorNow();

            BattleUnit runtimePreviouslySelected = GetRuntimeUnit(previouslySelectedUnit);
            BoardCell runtimeClicked = GetRuntimeCell(clickedCell);
            BoardState runtimeState = runtimeBoard.ShadowEvaluateCellClickFromSelected(runtimePreviouslySelected, runtimeClicked);
            BattleUnit expectedRuntimeSelected = GetRuntimeUnit(frameworkSelectedUnitAfterClick);
            BoardCell expectedRuntimePendingDestination = GetRuntimeCell(frameworkPendingDestination);

            Diagnostics.RuntimeParityDiagnostics.CompareSelectedStateTransition(
                $"Cell click on {Describe(clickedCell)}",
                previouslySelectedUnit,
                frameworkStateLabel,
                frameworkSelectedUnitAfterClick,
                frameworkPendingDestination,
                runtimeState,
                expectedRuntimeSelected,
                expectedRuntimePendingDestination);
        }

        internal void ShadowComparePendingMoveRightClick(
            CustomUnit frameworkUnit,
            Cell simulatedFrameworkPendingDestination,
            Cell frameworkPendingDestinationAfterTransition,
            string frameworkStateLabel,
            CustomUnit frameworkSelectedUnitAfterTransition,
            Cell frameworkUnitCellAfterTransition,
            float frameworkMovementPointsAfterTransition,
            bool frameworkFinishedAfterTransition)
        {
            if (!Diagnostics.RuntimeParityDiagnostics.LogPendingMoveParity)
            {
                return;
            }

            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            SyncRuntimeMirrorNow();

            BattleUnit runtimeUnit = GetRuntimeUnit(frameworkUnit);
            BoardCell runtimePendingDestination = GetRuntimeCell(simulatedFrameworkPendingDestination);
            BattleBoard.ShadowTransitionSnapshot runtimeSnapshot =
                runtimeBoard.ShadowEvaluatePendingMoveRightClick(runtimeUnit, runtimePendingDestination);

            Diagnostics.RuntimeParityDiagnostics.ComparePendingMoveTransition(
                "Pending move right-click",
                frameworkUnit,
                frameworkStateLabel,
                frameworkSelectedUnitAfterTransition,
                frameworkPendingDestinationAfterTransition,
                frameworkUnitCellAfterTransition,
                frameworkMovementPointsAfterTransition,
                frameworkFinishedAfterTransition,
                runtimeSnapshot);
        }

        internal void ShadowComparePendingMoveWait(
            CustomUnit frameworkUnit,
            Cell simulatedFrameworkPendingDestination,
            Cell frameworkPendingDestinationAfterTransition,
            string frameworkStateLabel,
            CustomUnit frameworkSelectedUnitAfterTransition,
            Cell frameworkUnitCellAfterTransition,
            float frameworkMovementPointsAfterTransition,
            bool frameworkFinishedAfterTransition)
        {
            if (!Diagnostics.RuntimeParityDiagnostics.LogPendingMoveParity)
            {
                return;
            }

            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            SyncRuntimeMirrorNow();

            BattleUnit runtimeUnit = GetRuntimeUnit(frameworkUnit);
            BoardCell runtimePendingDestination = GetRuntimeCell(simulatedFrameworkPendingDestination);
            BattleBoard.ShadowTransitionSnapshot runtimeSnapshot =
                runtimeBoard.ShadowEvaluatePendingMoveWait(runtimeUnit, runtimePendingDestination);

            Diagnostics.RuntimeParityDiagnostics.ComparePendingMoveTransition(
                "Pending move wait",
                frameworkUnit,
                frameworkStateLabel,
                frameworkSelectedUnitAfterTransition,
                frameworkPendingDestinationAfterTransition,
                frameworkUnitCellAfterTransition,
                frameworkMovementPointsAfterTransition,
                frameworkFinishedAfterTransition,
                runtimeSnapshot);
        }

        private static BattleUnit GetRuntimeUnit(CustomUnit unit)
        {
            return unit != null ? unit.GetComponent<BattleUnit>() : null;
        }

        private static BoardCell GetRuntimeCell(Cell cell)
        {
            return cell != null ? cell.GetComponent<BoardCell>() : null;
        }

        private static CustomUnit GetLegacyUnit(BattleUnit unit)
        {
            return unit != null ? unit.GetComponent<CustomUnit>() : null;
        }

        private static Cell GetLegacyCell(BoardCell cell)
        {
            return cell != null ? cell.GetComponent<Cell>() : null;
        }

        private static string Describe(CustomUnit unit)
        {
            return unit == null ? "<none>" : unit.name;
        }

        private static string Describe(Cell cell)
        {
            return cell == null ? "<none>" : cell.OffsetCoord.ToString();
        }
    }
}
