using System.Collections.Generic;
using UnityEngine;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Board.States;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CellGrid : IBattleBoardSceneInputCoordinator
    {
        public readonly struct RuntimeStateTransitionDecision
        {
            public RuntimeStateTransitionDecision(string stateLabel, Unit selectedUnit, BattleSquareCell pendingDestination)
            {
                StateLabel = stateLabel;
                SelectedUnit = selectedUnit;
                PendingDestination = pendingDestination;
            }

            public string StateLabel { get; }
            public Unit SelectedUnit { get; }
            public BattleSquareCell PendingDestination { get; }
        }

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
            if (CurrentState is not States.IRightClickHandler)
            {
                return;
            }

            ResolveRuntimeBoard();
            if (runtimeBoard != null && UsesRuntimeDirectSceneInput)
            {
                runtimeBoard.ProcessSceneRightClick();
                return;
            }

            ((States.IRightClickHandler)CurrentState).OnRightClick();
        }

        void IBattleBoardSceneInputCoordinator.OnSceneRightClick(BattleBoard board)
        {
            ProcessRuntimeRoutedSceneRightClick();
        }

        void IBattleBoardSceneInputCoordinator.OnSceneUnitClicked(BattleBoard board, BoardUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            Unit customUnit = ResolveSceneUnit(unit);
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

        void IBattleBoardSceneInputCoordinator.OnSceneUnitHovered(BattleBoard board, BoardUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            Unit customUnit = ResolveSceneUnit(unit);
            if (customUnit == null)
            {
                return;
            }

            customUnit.RaiseSceneHighlightEvent();
        }

        void IBattleBoardSceneInputCoordinator.OnSceneUnitUnhovered(BattleBoard board, BoardUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            Unit customUnit = ResolveSceneUnit(unit);
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

            BattleSquareCell legacyCell = ResolveSceneCell(cell);
            BattleSquareCell tile = ResolveBattleSquareFromRegistryCell(legacyCell);
            if (tile != null)
            {
                tile.RaiseSceneHighlightEvent();
            }
        }

        void IBattleBoardSceneInputCoordinator.OnSceneCellUnhovered(BattleBoard board, BoardCell cell)
        {
            if (cell == null)
            {
                return;
            }

            BattleSquareCell legacyCell = ResolveSceneCell(cell);
            BattleSquareCell tile = ResolveBattleSquareFromRegistryCell(legacyCell);
            if (tile != null)
            {
                tile.RaiseSceneDehighlightEvent();
            }
        }

        private bool IsHumanSceneInputStateActive()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard?.CurrentState != null && IsRuntimeHumanSceneInputState(runtimeBoard.CurrentState))
            {
                return true;
            }

            return CurrentState is CellGridStateWaitingForInput
                or UnitSelectedState
                or CellGridStateMovePendingConfirm;
        }

        private static bool IsRuntimeHumanSceneInputState(BoardState runtimeState)
        {
            return runtimeState is BoardStateWaitingForInput
                or BoardStateUnitSelected
                or BoardStateUnitMovePendingConfirm;
        }

        private static bool TryGetRuntimeSelectedSceneUnit(
            BoardState runtimeState,
            System.Func<BoardUnit, Unit> resolveSceneUnit,
            out Unit sceneUnit)
        {
            sceneUnit = runtimeState is BoardStateUnitSelected selectedState
                ? resolveSceneUnit(selectedState.SelectedUnit)
                : null;
            return sceneUnit != null;
        }

        private bool TryGetPendingMoveSceneContext(
            BoardState runtimeState,
            out MoveAbility moveAbility,
            out Unit actingUnit)
        {
            moveAbility = null;
            actingUnit = null;

            if (runtimeState is not BoardStateUnitMovePendingConfirm pendingState)
            {
                return false;
            }

            actingUnit = ResolveSceneUnit(pendingState.SelectedUnit);
            if (actingUnit == null)
            {
                return false;
            }

            moveAbility = actingUnit.GetComponent<MoveAbility>();
            return moveAbility != null;
        }

        internal void ProcessRuntimeRoutedSceneUnitClick(Unit clickedUnit)
        {
            if (clickedUnit == null)
            {
                return;
            }

            if (!ShouldRouteHumanMovementThroughRuntime)
            {
                CurrentState?.OnUnitClicked(clickedUnit);
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
                    RuntimeStateTransitionDecision waitingRuntimeDecision = ProcessRuntimeWaitingStateUnitClick(clickedUnit);
                    ApplyLegacyEffectsAfterRuntimeUnitClick(waitingRuntimeDecision, clickedUnit, previouslySelectedUnit: null);
                    break;

                case BoardStateUnitSelected selectedRuntimeState:
                    if (!TryGetRuntimeSelectedSceneUnit(selectedRuntimeState, ResolveSceneUnit, out Unit previouslySelectedUnit))
                    {
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
                    if (TryGetPendingMoveSceneContext(pendingRuntimeState, out MoveAbility moveAbility, out _))
                    {
                        moveAbility.OnPendingMoveUnitClicked(clickedUnit, this);
                    }

                    break;
            }
        }

        internal void ProcessRuntimeRoutedSceneCellClick(BoardCell clickedCell)
        {
            BattleSquareCell legacyCell = clickedCell != null ? ResolveSceneCell(clickedCell) : null;

            if (clickedCell == null || !ShouldRouteHumanMovementThroughRuntime)
            {
                IBattleCell battleCell = ResolveBattleCellFromRegistryCell(legacyCell);
                if (battleCell != null && CurrentState != null)
                {
                    CurrentState.OnCellClicked(battleCell);
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
                    if (!TryGetRuntimeSelectedSceneUnit(selectedRuntimeState, ResolveSceneUnit, out Unit previouslySelectedUnit))
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
                    if (TryGetPendingMoveSceneContext(pendingRuntimeState, out MoveAbility moveAbility, out _)
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
                if (CurrentState is States.IRightClickHandler handler)
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
                    if (!TryGetRuntimeSelectedSceneUnit(selectedRuntimeState, ResolveSceneUnit, out _))
                    {
                        break;
                    }

                    RuntimeStateTransitionDecision selectedRuntimeDecision = ProcessRuntimeRightClick();
                    ApplyLegacyEffectsAfterRuntimeSelectedRightClick(selectedRuntimeDecision);
                    break;

                case BoardStateUnitMovePendingConfirm pendingRuntimeState:
                    if (!TryGetPendingMoveSceneContext(pendingRuntimeState, out MoveAbility moveAbility, out _))
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
            Unit clickedUnit,
            Unit previouslySelectedUnit)
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
                runtimeDecision.SelectedUnit.GetComponent<MoveAbility>()?.OnSelectedUnitClicked(this);
                return;
            }

            ApplyLegacyStateFromRuntime(EnterWaitingState);
        }

        private void ApplyLegacyEffectsAfterRuntimeCellClick(
            RuntimeStateTransitionDecision runtimeDecision,
            Unit previouslySelectedUnit,
            BattleSquareCell legacyCell)
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
                IBattleCell battleCell = ResolveBattleCellFromRegistryCell(legacyCell);
                if (battleCell == null)
                {
                    return;
                }

                MoveAbility moveAbility = previouslySelectedUnit.GetComponent<MoveAbility>();
                if (moveAbility != null)
                {
                    moveAbility.OnCellClicked(battleCell, this);
                }
                else
                {
                    ForwardCellClickToAbilities(previouslySelectedUnit, battleCell);
                }
            }
        }

        private void ForwardCellClickToAbilities(Unit actingUnit, IBattleCell cell)
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

        internal RuntimeStateTransitionDecision ProcessRuntimeWaitingStateUnitClick(Unit clickedUnit)
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

        internal RuntimeStateTransitionDecision ProcessRuntimeSelectedStateUnitClick(Unit clickedUnit)
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

        internal RuntimeStateTransitionDecision ProcessRuntimeSelectedStateCellClick(BattleSquareCell clickedCell)
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
    }
}
