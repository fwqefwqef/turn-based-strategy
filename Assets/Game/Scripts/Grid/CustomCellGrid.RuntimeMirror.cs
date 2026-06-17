using System;
using System.Collections.Generic;
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
    public partial class CustomCellGrid : IBattleBoardSceneInputCoordinator
    {
        public readonly struct RuntimeStateTransitionDecision
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

        private void SyncRuntimeSceneInputGate()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            bool directInputActive = UsesRuntimeDirectSceneInput;
            runtimeBoard.SetSceneInputEnabled(directInputActive);
            runtimeBoard.SceneInputCoordinator = directInputActive ? this : null;
        }

        private void ClearRuntimeSceneInputCoordinator()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            runtimeBoard.SceneInputCoordinator = null;
            runtimeBoard.SetSceneInputEnabled(false);
        }

        public void ProcessSceneRightClick()
        {
            if (CurrentCustomState is not States.ICustomRightClickHandler)
            {
                return;
            }

            ResolveRuntimeBoard();
            if (runtimeBoard != null && UsesRuntimeDirectSceneInput)
            {
                runtimeBoard.ProcessSceneRightClick();
                return;
            }

            ((States.ICustomRightClickHandler)CurrentCustomState).OnRightClick();
        }

        void IBattleBoardSceneInputCoordinator.OnSceneRightClick(BattleBoard board)
        {
            ProcessRuntimeRoutedSceneRightClick();
        }

        void IBattleBoardSceneInputCoordinator.OnSceneUnitClicked(BattleBoard board, BattleUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            CustomUnit customUnit = GetLegacyUnit(unit);
            if (customUnit == null)
            {
                return;
            }

            ProcessRuntimeRoutedSceneUnitClick(customUnit);
        }

        void IBattleBoardSceneInputCoordinator.OnSceneCellClicked(BattleBoard board, BoardCell cell)
        {
            if (cell == null)
            {
                return;
            }

            ProcessRuntimeRoutedSceneCellClick(cell);
        }

        void IBattleBoardSceneInputCoordinator.OnSceneUnitHovered(BattleBoard board, BattleUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            CustomUnit customUnit = GetLegacyUnit(unit);
            if (customUnit == null)
            {
                return;
            }

            customUnit.RaiseSceneHighlightEvent();
        }

        void IBattleBoardSceneInputCoordinator.OnSceneUnitUnhovered(BattleBoard board, BattleUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            CustomUnit customUnit = GetLegacyUnit(unit);
            if (customUnit == null)
            {
                return;
            }

            customUnit.RaiseSceneDehighlightEvent();
        }

        void IBattleBoardSceneInputCoordinator.OnSceneCellHovered(BattleBoard board, BoardCell cell)
        {
            if (cell == null)
            {
                return;
            }

            Cell legacyCell = GetLegacyCell(cell);
            if (legacyCell is CustomSquare customSquare)
            {
                customSquare.RaiseSceneHighlightEvent();
                return;
            }

            if (legacyCell != null)
            {
                legacyCell.OnMouseEnter();
            }
        }

        void IBattleBoardSceneInputCoordinator.OnSceneCellUnhovered(BattleBoard board, BoardCell cell)
        {
            if (cell == null)
            {
                return;
            }

            Cell legacyCell = GetLegacyCell(cell);
            if (legacyCell is CustomSquare customSquare)
            {
                customSquare.RaiseSceneDehighlightEvent();
                return;
            }

            if (legacyCell != null)
            {
                legacyCell.OnMouseExit();
            }
        }

        private bool IsHumanSceneInputStateActive()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard?.CurrentState != null && IsRuntimeHumanSceneInputState(runtimeBoard.CurrentState))
            {
                return true;
            }

            return CurrentCustomState is CustomCellGridStateWaitingForInput
                or CustomUnitSelectedState
                or CustomCellGridStateMovePendingConfirm;
        }

        private static bool IsRuntimeHumanSceneInputState(BoardState runtimeState)
        {
            return runtimeState is BoardStateWaitingForInput
                or BoardStateUnitSelected
                or BoardStateUnitMovePendingConfirm;
        }

        private static bool TryGetRuntimeSelectedLegacyUnit(
            BoardState runtimeState,
            System.Func<BattleUnit, CustomUnit> getLegacyUnit,
            out CustomUnit legacyUnit)
        {
            legacyUnit = runtimeState is BoardStateUnitSelected selectedState
                ? getLegacyUnit(selectedState.SelectedUnit)
                : null;
            return legacyUnit != null;
        }

        private bool TryGetPendingMoveLegacyContext(
            BoardState runtimeState,
            out CustomMoveAbility moveAbility,
            out CustomUnit actingUnit)
        {
            moveAbility = null;
            actingUnit = null;

            if (runtimeState is not BoardStateUnitMovePendingConfirm pendingState)
            {
                return false;
            }

            actingUnit = GetLegacyUnit(pendingState.SelectedUnit);
            if (actingUnit == null)
            {
                return false;
            }

            moveAbility = actingUnit.GetComponent<CustomMoveAbility>();
            return moveAbility != null;
        }

        internal void ProcessRuntimeRoutedSceneUnitClick(CustomUnit clickedUnit)
        {
            if (clickedUnit == null)
            {
                return;
            }

            if (!ShouldRouteHumanMovementThroughRuntime)
            {
                CurrentCustomState?.OnCustomUnitClicked(clickedUnit);
                return;
            }

            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            switch (runtimeBoard.CurrentState)
            {
                case BoardStateWaitingForInput:
                {
                    RuntimeStateTransitionDecision waitingRuntimeDecision = ProcessRuntimeWaitingStateUnitClick(clickedUnit);
                    ApplyLegacyEffectsAfterRuntimeUnitClick(waitingRuntimeDecision, clickedUnit, previouslySelectedUnit: null);
                    break;
                }

                case BoardStateUnitSelected selectedRuntimeState:
                    if (!TryGetRuntimeSelectedLegacyUnit(selectedRuntimeState, GetLegacyUnit, out CustomUnit previouslySelectedUnit))
                    {
                        break;
                    }

                    if (!GetCurrentPlayerCustomUnits().Contains(clickedUnit))
                    {
                        ForwardUnitClickToAbilities(previouslySelectedUnit, clickedUnit);
                        break;
                    }

                    RuntimeStateTransitionDecision selectedRuntimeDecision =
                        ProcessRuntimeSelectedStateUnitClick(clickedUnit);
                    ApplyLegacyEffectsAfterRuntimeUnitClick(
                        selectedRuntimeDecision,
                        clickedUnit,
                        previouslySelectedUnit);
                    break;

                case BoardStateUnitMovePendingConfirm pendingRuntimeState:
                    if (TryGetPendingMoveLegacyContext(pendingRuntimeState, out CustomMoveAbility moveAbility, out _))
                    {
                        moveAbility.OnPendingMoveUnitClicked(clickedUnit, this);
                    }

                    break;
            }
        }

        internal void ProcessRuntimeRoutedSceneCellClick(BoardCell clickedCell)
        {
            Cell legacyCell = clickedCell != null ? GetLegacyCell(clickedCell) : null;

            if (clickedCell == null || !ShouldRouteHumanMovementThroughRuntime)
            {
                if (legacyCell is IBattleCell battleCell && CurrentCustomState != null)
                {
                    CurrentCustomState.OnCellClicked(battleCell);
                }

                return;
            }

            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            switch (runtimeBoard.CurrentState)
            {
                case BoardStateUnitSelected selectedRuntimeState:
                    if (!TryGetRuntimeSelectedLegacyUnit(selectedRuntimeState, GetLegacyUnit, out CustomUnit previouslySelectedUnit))
                    {
                        break;
                    }

                    RuntimeStateTransitionDecision selectedRuntimeDecision =
                        ProcessRuntimeSelectedStateCellClick(legacyCell);
                    ApplyLegacyEffectsAfterRuntimeCellClick(
                        selectedRuntimeDecision,
                        previouslySelectedUnit,
                        legacyCell);
                    break;

                case BoardStateUnitMovePendingConfirm pendingRuntimeState:
                    if (TryGetPendingMoveLegacyContext(pendingRuntimeState, out CustomMoveAbility moveAbility, out _)
                        && legacyCell != null)
                    {
                        moveAbility.OnPendingMoveCellClicked(legacyCell, this);
                    }

                    break;
            }
        }

        internal void ProcessRuntimeRoutedSceneRightClick()
        {
            if (!ShouldRouteHumanMovementThroughRuntime)
            {
                if (CurrentCustomState is States.ICustomRightClickHandler handler)
                {
                    handler.OnRightClick();
                }

                return;
            }

            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            switch (runtimeBoard.CurrentState)
            {
                case BoardStateUnitSelected selectedRuntimeState:
                    if (!TryGetRuntimeSelectedLegacyUnit(selectedRuntimeState, GetLegacyUnit, out CustomUnit selectedUnit))
                    {
                        break;
                    }

                    RuntimeStateTransitionDecision selectedRuntimeDecision = ProcessRuntimeRightClick();
                    ApplyLegacyEffectsAfterRuntimeSelectedRightClick(selectedRuntimeDecision);
                    break;

                case BoardStateUnitMovePendingConfirm pendingRuntimeState:
                    if (!TryGetPendingMoveLegacyContext(pendingRuntimeState, out CustomMoveAbility moveAbility, out CustomUnit pendingUnit))
                    {
                        break;
                    }

                    if (moveAbility.TryHandlePendingMoveRightClickUiModes(this))
                    {
                        return;
                    }

                    RuntimeStateTransitionDecision pendingRuntimeDecision = ProcessRuntimePendingMoveRightClick();
                    moveAbility.ApplyLegacyEffectsAfterRuntimePendingMoveRightClick(this, pendingRuntimeDecision);
                    break;
            }
        }

        private void ApplyLegacyEffectsAfterRuntimeSelectedRightClick(RuntimeStateTransitionDecision runtimeDecision)
        {
            if (runtimeDecision.StateLabel == "Waiting")
            {
                ApplyLegacyStateFromRuntime(EnterWaitingState);
                return;
            }

            if (runtimeDecision.StateLabel == "Selected" && runtimeDecision.SelectedUnit != null)
            {
                ApplyLegacyStateFromRuntime(() => EnterSelectedState(runtimeDecision.SelectedUnit));
            }
        }

        private void ApplyLegacyEffectsAfterRuntimeUnitClick(
            RuntimeStateTransitionDecision runtimeDecision,
            CustomUnit clickedUnit,
            CustomUnit previouslySelectedUnit)
        {
            if (runtimeDecision.StateLabel == "Waiting")
            {
                ApplyLegacyStateFromRuntime(EnterWaitingState);
                return;
            }

            if (runtimeDecision.StateLabel == "Selected" && runtimeDecision.SelectedUnit != null)
            {
                ApplyLegacyStateFromRuntime(() => EnterSelectedState(runtimeDecision.SelectedUnit));
                return;
            }

            if (runtimeDecision.StateLabel == "PendingMoveConfirm"
                && runtimeDecision.SelectedUnit != null
                && runtimeDecision.SelectedUnit == previouslySelectedUnit)
            {
                runtimeDecision.SelectedUnit.GetComponent<CustomMoveAbility>()?.OnSelectedUnitClicked(this);
                return;
            }

            if (previouslySelectedUnit != null)
            {
                ForwardUnitClickToAbilities(previouslySelectedUnit, clickedUnit);
            }
        }

        private void ApplyLegacyEffectsAfterRuntimeCellClick(
            RuntimeStateTransitionDecision runtimeDecision,
            CustomUnit previouslySelectedUnit,
            Cell legacyCell)
        {
            if (runtimeDecision.StateLabel == "Waiting")
            {
                ApplyLegacyStateFromRuntime(EnterWaitingState);
                return;
            }

            if (runtimeDecision.StateLabel == "PendingMoveConfirm"
                && runtimeDecision.SelectedUnit == previouslySelectedUnit
                && legacyCell != null)
            {
                CustomMoveAbility moveAbility = previouslySelectedUnit.GetComponent<CustomMoveAbility>();
                if (moveAbility != null)
                {
                    moveAbility.OnCellClicked((IBattleCell)legacyCell, this);
                }
                else
                {
                    ForwardCellClickToAbilities(previouslySelectedUnit, (IBattleCell)legacyCell);
                }
            }
        }

        private void ForwardUnitClickToAbilities(CustomUnit actingUnit, CustomUnit clickedUnit)
        {
            if (actingUnit == null || clickedUnit == null)
            {
                return;
            }

            IBattleUnit battleUnit = clickedUnit;
            foreach (Windy.Srpg.Runtime.Actions.IBattleAction action in actingUnit.GetBattleActions())
            {
                action?.OnUnitClicked(battleUnit, this);
            }
        }

        private void ForwardCellClickToAbilities(CustomUnit actingUnit, IBattleCell cell)
        {
            if (actingUnit == null || cell == null)
            {
                return;
            }

            foreach (Windy.Srpg.Runtime.Actions.IBattleAction action in actingUnit.GetBattleActions())
            {
                action?.OnCellClicked(cell, this);
            }
        }

        private void NormalizeHumanInputState()
        {
            if (!IsHumanTurn)
            {
                return;
            }

            if (cellGridState != null && cellGridState.GetType().Name == "CellGridStateWaitingForInput")
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
            return BuildRuntimeSelectedState(selectedState?.SelectedUnit);
        }

        private BoardState BuildRuntimeSelectedState(CustomUnit unit)
        {
            BattleUnit runtimeSelected = GetRuntimeUnit(unit);
            return runtimeSelected != null
                ? new BoardStateUnitSelected(runtimeBoard, runtimeSelected)
                : new BoardStateWaitingForInput(runtimeBoard);
        }

        private BoardState BuildRuntimePendingMoveState(CustomCellGridStateMovePendingConfirm pendingState)
        {
            return BuildRuntimePendingMoveState(pendingState?.MoveAbility);
        }

        private BoardState BuildRuntimePendingMoveState(CustomMoveAbility moveAbility)
        {
            CustomUnit legacyUnit = moveAbility != null ? moveAbility.GetComponent<CustomUnit>() : null;
            BattleUnit runtimeUnit = GetRuntimeUnit(legacyUnit);
            Cell legacyDestination = legacyUnit != null && legacyUnit.HasPendingMove
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

        public void ClearAllCellHighlights()
        {
            foreach (Cell cell in GetAllCells())
            {
                if (cell == null)
                {
                    continue;
                }

                cell.UnMark();
                GetRuntimeCell(cell)?.ClearHighlight();
            }
        }

        internal bool ProcessRuntimeRoutedBattleOutcomeEvaluation()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                bool legacyFinished = CheckGameFinished();
                if (legacyFinished)
                {
                    SyncCustomStateToGameOver();
                }

                return legacyFinished;
            }

            if (GameFinished)
            {
                SyncCustomStateToGameOver();
                return true;
            }

            SyncRuntimeMirrorNow();
            BattleOutcome runtimeOutcomeAuthority = runtimeBoard.EvaluateBattleOutcome();
            bool finished = TryApplyBattleOutcome(runtimeOutcomeAuthority);
            if (finished)
            {
                SyncCustomStateToGameOver();
            }

            return finished;
        }

        internal void ProcessRuntimeRoutedCombatPresentationBegan()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            SyncRuntimeMirrorNow();

            if (IsHumanTurn && CurrentCustomState is not CustomCellGridStateBlockInput)
            {
                EnterLegacyBlockedInputState();
            }
        }

        internal void ProcessRuntimeRoutedCombatPresentationEnded()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            SyncRuntimeMirrorNow();
            RefreshSceneCellOccupancyNow();
            TryFlushDeferredDestroyQueue();

            if (ShouldRouteBattleOutcomeThroughRuntime)
            {
                RequestBattleOutcomeEvaluation();
            }
        }

        internal void ProcessRuntimeRoutedPostCombatRecovery()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                EnterWaitingState();
                return;
            }

            SyncRuntimeMirrorNow();
            TryFlushDeferredDestroyQueue();

            if (RequestBattleOutcomeEvaluation())
            {
                return;
            }

            ApplyRuntimeDrivenState(
                new BoardStateWaitingForInput(runtimeBoard),
                EnterWaitingState);
        }

        internal void PrepareRuntimeRoutedAiTurn()
        {
            SyncRuntimeMirrorNow();
        }

        internal void ProcessRuntimeRoutedEndTurn()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                EndTurn();
                return;
            }

            EnterBlockedInputState();
            if (RequestBattleOutcomeEvaluation())
            {
                return;
            }

            EndUnitsForCurrentPlayerTurn();

            RoundRobinTurnPlan plan = RoundRobinBattleFlow.ResolveTurn(this);
            if (plan.NextPlayer == null)
            {
                Debug.LogError("CustomCellGrid: No valid battle turn resolver or next player was found.");
                return;
            }

            SyncRuntimeMirrorNow();
            runtimeBoard.EndCurrentTurn(kickTurnPlayerPlay: false);
            ApplyLegacyStateFromRuntime(() => CommitTurnTransition(plan, kickPlayerPlay: false));
            SyncRuntimeMirrorNow();
            runtimeBoard.KickCurrentTurnPlay();
        }

        internal Cell ResolveRuntimeActingCell(CustomUnit unit)
        {
            BattleUnit runtimeUnit = GetRuntimeUnit(unit);
            if (runtimeUnit == null)
            {
                return unit?.Cell;
            }

            BoardCell runtimeActingCell = runtimeUnit.PreviewCell;
            if (runtimeActingCell != null)
            {
                return GetLegacyCell(runtimeActingCell) ?? unit?.PreviewCell ?? unit?.Cell;
            }

            return unit?.Cell;
        }

        internal List<CustomUnit> GetAttackableEnemiesFromActingCell(CustomUnit actor, Cell actingCell)
        {
            if (actor == null || actingCell == null)
            {
                return new List<CustomUnit>();
            }

            return GetEnemyUnits(CurrentCustomPlayer)
                .Where(enemy => enemy != null && actor.CanAttackTargetWithAnyWeapon(enemy, actingCell))
                .ToList();
        }

        private void MarkRuntimeBoardDirty()
        {
            runtimeBoardCollectionsDirty = true;
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
    }
}
