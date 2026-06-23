using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Actions;
using Windy.Srpg.Runtime.Grid;
using Windy.Srpg.Runtime.Grid.States;
using Windy.Srpg.Runtime.Units;
using RuntimeGridState = Windy.Srpg.Runtime.Grid.States.RuntimeGridState;

namespace Windy.Srpg.Game.Grid
{
    public partial class CellGrid
    {
        // --- Runtime grid mirror and state bridge ---
        private RuntimeGrid runtimeGrid;
        private bool runtimeGridCollectionsDirty = true;

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

        private void ResolveRuntimeGrid()
        {
            if (runtimeGrid == null)
            {
                runtimeGrid = GetComponent<RuntimeGrid>();
            }
        }

        private void SyncRuntimeGrid()
        {
            ResolveRuntimeGrid();
            if (runtimeGrid == null)
            {
                return;
            }

            RefreshRuntimeGridCollections();
            UpdateRuntimeGridMetadata();
        }

        private void ApplyRuntimeDrivenState(RuntimeGridState runtimeState, System.Action applySceneState)
        {
            if (runtimeState == null)
            {
                applySceneState?.Invoke();
                return;
            }

            ResolveRuntimeGrid();
            if (runtimeGrid == null)
            {
                applySceneState?.Invoke();
                return;
            }

            RefreshRuntimeGridCollections();
            UpdateRuntimeGridMetadata();
            runtimeGrid.SetState(runtimeState);
            ApplySceneStateFromRuntime(applySceneState);
        }

        private void MirrorSceneStateToRuntimeGrid(CellGridState legacyState)
        {
            ResolveRuntimeGrid();
            if (runtimeGrid == null || legacyState == null)
            {
                return;
            }

            RefreshRuntimeGridCollections();
            UpdateRuntimeGridMetadata();

            RuntimeGridState runtimeState = BuildRuntimeStateFromLegacyState(legacyState);
            if (runtimeState != null)
            {
                runtimeGrid.SetState(runtimeState);
            }
        }

        private RuntimeGridState BuildRuntimeStateFromLegacyState(CellGridState legacyState)
        {
            return legacyState switch
            {
                CellGridStateWaitingForInput => new RuntimeGridStateWaitingForInput(runtimeGrid),
                CellGridStateBlockInput => new RuntimeGridStateBlockedInput(runtimeGrid),
                CellGridStateRemotePlayerTurn => new RuntimeGridStateBlockedInput(runtimeGrid),
                PreBattleDeploymentSwapState => new RuntimeGridStateBlockedInput(runtimeGrid),
                CellGridStateAiTurn => new RuntimeGridStateAiTurn(runtimeGrid),
                UnitSelectedState selectedState => BuildRuntimeSelectedState(selectedState),
                CellGridStateMovePendingConfirm pendingState => BuildRuntimePendingMoveState(pendingState),
                _ => new RuntimeGridStateBlockedInput(runtimeGrid)
            };
        }

        private RuntimeGridState BuildRuntimeSelectedState(UnitSelectedState selectedState)
        {
            return BuildRuntimeSelectedState(selectedState?.SelectedUnit);
        }

        private RuntimeGridState BuildRuntimeSelectedState(Unit unit)
        {
            GridUnit runtimeSelected = GetRuntimeUnit(unit);
            return runtimeSelected != null
                ? new RuntimeGridStateUnitSelected(runtimeGrid, runtimeSelected)
                : new RuntimeGridStateWaitingForInput(runtimeGrid);
        }

        private RuntimeGridState BuildRuntimePendingMoveState(CellGridStateMovePendingConfirm pendingState)
        {
            return BuildRuntimePendingMoveState(pendingState?.MoveAbility);
        }

        private RuntimeGridState BuildRuntimePendingMoveState(MoveAbility moveAbility)
        {
            Unit legacyUnit = moveAbility != null ? moveAbility.GetComponent<Unit>() : null;
            GridUnit runtimeUnit = GetRuntimeUnit(legacyUnit);
            Cell legacyDestination = legacyUnit != null && legacyUnit.HasPendingMove
                ? legacyUnit.PreviewCell
                : moveAbility?.Destination ?? legacyUnit?.PreviewCell ?? legacyUnit?.Cell;
            Cell runtimeDestination = legacyDestination;

            return runtimeUnit != null
                ? new RuntimeGridStateUnitMovePendingConfirm(runtimeGrid, runtimeUnit, runtimeDestination)
                : new RuntimeGridStateWaitingForInput(runtimeGrid);
        }

        private RuntimeStateTransitionDecision CaptureRuntimeDecision()
        {
            return new RuntimeStateTransitionDecision(
                runtimeGrid?.CurrentState?.DiagnosticStateLabel ?? "Waiting",
                ResolveSceneUnit(runtimeGrid?.CurrentState?.SelectedUnit),
                runtimeGrid?.CurrentState?.PendingDestination);
        }

        private void RefreshRuntimeGridCollections()
        {
            if (!runtimeGridCollectionsDirty)
            {
                return;
            }

            runtimeGrid.SetMirroredCollections(
                GetAllCells(),
                GetAllUnits().Select(GetRuntimeUnit),
                GetOrderedPlayers().Cast<Runtime.Players.IBattlePlayer>());
            runtimeGridCollectionsDirty = false;
        }

        private void UpdateRuntimeGridMetadata()
        {
            runtimeGrid.SetBattleStarted(!IsPreBattlePhase);

            if (CurrentPlayer != null)
            {
                runtimeGrid.SetCurrentPlayerById(CurrentPlayer.PlayerNumber);
            }
        }

        [ContextMenu("Sync Runtime Mirror Now")]
        public void SyncRuntimeMirrorNow()
        {
            MarkRuntimeGridDirty();

            foreach (Unit unit in GetAllUnits())
            {
                unit?.SyncMirroredRuntimeNow();
            }

            SyncRuntimeGrid();
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
                cell?.ClearHighlight();
            }
        }

        private void MarkRuntimeGridDirty()
        {
            runtimeGridCollectionsDirty = true;
        }

        private static GridUnit GetRuntimeUnit(Unit unit)
        {
            return unit != null ? unit.GetComponent<GridUnit>() : null;
        }

        private static Unit ResolveSceneUnit(GridUnit unit)
        {
            return unit != null ? unit.GetComponent<Unit>() : null;
        }

        public readonly struct RuntimeStateTransitionDecision
        {
            public RuntimeStateTransitionDecision(string stateLabel, Unit selectedUnit, Cell pendingDestination)
            {
                StateLabel = stateLabel;
                SelectedUnit = selectedUnit;
                PendingDestination = pendingDestination;
            }

            public string StateLabel { get; }
            public Unit SelectedUnit { get; }
            public Cell PendingDestination { get; }
        }

        private void SyncRuntimeSceneInputGate()
        {
            ResolveRuntimeGrid();
            if (runtimeGrid == null)
            {
                return;
            }

            bool directInputActive = UsesRuntimeDirectSceneInput;
            runtimeGrid.SetSceneInputEnabled(directInputActive);
            runtimeGrid.SceneInputCoordinator = directInputActive ? this : null;
        }

        private void ClearRuntimeSceneInputCoordinator()
        {
            ResolveRuntimeGrid();
            if (runtimeGrid == null)
            {
                return;
            }

            runtimeGrid.SceneInputCoordinator = null;
            runtimeGrid.SetSceneInputEnabled(false);
        }

        public void ProcessSceneRightClick()
        {
            if (CurrentState is not States.IRightClickHandler)
            {
                return;
            }

            ResolveRuntimeGrid();
            if (runtimeGrid != null && UsesRuntimeDirectSceneInput)
            {
                runtimeGrid.ProcessSceneRightClick();
                return;
            }

            ((States.IRightClickHandler)CurrentState).OnRightClick();
        }

        void IRuntimeGridSceneInputCoordinator.OnSceneRightClick(RuntimeGrid grid)
        {
            ProcessRuntimeRoutedSceneRightClick();
        }

        void IRuntimeGridSceneInputCoordinator.OnSceneUnitClicked(RuntimeGrid grid, GridUnit unit)
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

        void IRuntimeGridSceneInputCoordinator.OnSceneCellClicked(RuntimeGrid grid, Cell cell)
        {
            if (cell == null)
            {
                return;
            }

            ProcessRuntimeRoutedSceneCellClick(cell);
        }

        void IRuntimeGridSceneInputCoordinator.OnSceneUnitHovered(RuntimeGrid grid, GridUnit unit)
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

        void IRuntimeGridSceneInputCoordinator.OnSceneUnitUnhovered(RuntimeGrid grid, GridUnit unit)
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

        void IRuntimeGridSceneInputCoordinator.OnSceneCellHovered(RuntimeGrid grid, Cell cell)
        {
            if (cell == null)
            {
                return;
            }

            cell.RaiseSceneHighlightEvent();
        }

        void IRuntimeGridSceneInputCoordinator.OnSceneCellUnhovered(RuntimeGrid grid, Cell cell)
        {
            if (cell == null)
            {
                return;
            }

            cell.RaiseSceneDehighlightEvent();
        }

        private bool IsHumanSceneInputStateActive()
        {
            ResolveRuntimeGrid();
            if (runtimeGrid?.CurrentState != null && IsRuntimeHumanSceneInputState(runtimeGrid.CurrentState))
            {
                return true;
            }

            return CurrentState is CellGridStateWaitingForInput
                or UnitSelectedState
                or CellGridStateMovePendingConfirm;
        }

        private static bool IsRuntimeHumanSceneInputState(RuntimeGridState runtimeState)
        {
            return runtimeState is RuntimeGridStateWaitingForInput
                or RuntimeGridStateUnitSelected
                or RuntimeGridStateUnitMovePendingConfirm;
        }

        private static bool TryGetRuntimeSelectedSceneUnit(
            RuntimeGridState runtimeState,
            System.Func<GridUnit, Unit> resolveSceneUnit,
            out Unit sceneUnit)
        {
            sceneUnit = runtimeState is RuntimeGridStateUnitSelected selectedState
                ? resolveSceneUnit(selectedState.SelectedUnit)
                : null;
            return sceneUnit != null;
        }

        private bool TryGetPendingMoveSceneContext(
            RuntimeGridState runtimeState,
            out MoveAbility moveAbility,
            out Unit actingUnit)
        {
            moveAbility = null;
            actingUnit = null;

            if (runtimeState is not RuntimeGridStateUnitMovePendingConfirm pendingState)
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

            ResolveRuntimeGrid();
            if (runtimeGrid == null)
            {
                return;
            }

            switch (runtimeGrid.CurrentState)
            {
                case RuntimeGridStateWaitingForInput:
                    RuntimeStateTransitionDecision waitingRuntimeDecision = ProcessRuntimeWaitingStateUnitClick(clickedUnit);
                    ApplySceneEffectsAfterRuntimeUnitClick(waitingRuntimeDecision, clickedUnit, previouslySelectedUnit: null);
                    break;

                case RuntimeGridStateUnitSelected selectedRuntimeState:
                    if (!TryGetRuntimeSelectedSceneUnit(selectedRuntimeState, ResolveSceneUnit, out Unit previouslySelectedUnit))
                    {
                        break;
                    }

                    RuntimeStateTransitionDecision selectedRuntimeDecision =
                        ProcessRuntimeSelectedStateUnitClick(clickedUnit);
                    ApplySceneEffectsAfterRuntimeUnitClick(
                        selectedRuntimeDecision,
                        clickedUnit,
                        previouslySelectedUnit);
                    break;

                case RuntimeGridStateUnitMovePendingConfirm pendingRuntimeState:
                    if (TryGetPendingMoveSceneContext(pendingRuntimeState, out MoveAbility moveAbility, out _))
                    {
                        moveAbility.OnPendingMoveUnitClicked(clickedUnit, this);
                    }

                    break;
            }
        }

        internal void ProcessRuntimeRoutedSceneCellClick(Cell clickedCell)
        {
            Cell sceneCell = clickedCell;

            if (clickedCell == null || !ShouldRouteHumanMovementThroughRuntime)
            {
                if (clickedCell != null && CurrentState != null)
                {
                    CurrentState.OnCellClicked(clickedCell);
                }

                return;
            }

            ResolveRuntimeGrid();
            if (runtimeGrid == null)
            {
                return;
            }

            switch (runtimeGrid.CurrentState)
            {
                case RuntimeGridStateUnitSelected selectedRuntimeState:
                    if (!TryGetRuntimeSelectedSceneUnit(selectedRuntimeState, ResolveSceneUnit, out Unit previouslySelectedUnit))
                    {
                        break;
                    }

                    RuntimeStateTransitionDecision selectedRuntimeDecision =
                        ProcessRuntimeSelectedStateCellClick(sceneCell);
                    ApplySceneEffectsAfterRuntimeCellClick(
                        selectedRuntimeDecision,
                        previouslySelectedUnit,
                        sceneCell);
                    break;

                case RuntimeGridStateUnitMovePendingConfirm pendingRuntimeState:
                    if (TryGetPendingMoveSceneContext(pendingRuntimeState, out MoveAbility moveAbility, out _)
                        && sceneCell != null)
                    {
                        moveAbility.OnPendingMoveCellClicked(sceneCell, this);
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

            ResolveRuntimeGrid();
            if (runtimeGrid == null)
            {
                return;
            }

            switch (runtimeGrid.CurrentState)
            {
                case RuntimeGridStateUnitSelected selectedRuntimeState:
                    if (!TryGetRuntimeSelectedSceneUnit(selectedRuntimeState, ResolveSceneUnit, out _))
                    {
                        break;
                    }

                    RuntimeStateTransitionDecision selectedRuntimeDecision = ProcessRuntimeRightClick();
                    ApplySceneEffectsAfterRuntimeSelectedRightClick(selectedRuntimeDecision);
                    break;

                case RuntimeGridStateUnitMovePendingConfirm pendingRuntimeState:
                    if (!TryGetPendingMoveSceneContext(pendingRuntimeState, out MoveAbility moveAbility, out _))
                    {
                        break;
                    }

                    if (moveAbility.TryHandlePendingMoveRightClickUiModes(this))
                    {
                        return;
                    }

                    RuntimeStateTransitionDecision pendingRuntimeDecision = ProcessRuntimePendingMoveRightClick();
                    moveAbility.ApplySceneEffectsAfterRuntimePendingMoveRightClick(this, pendingRuntimeDecision);
                    break;
            }
        }

        private void ApplySceneEffectsAfterRuntimeSelectedRightClick(RuntimeStateTransitionDecision runtimeDecision)
        {
            if (runtimeDecision.StateLabel == "Waiting")
            {
                ApplySceneStateFromRuntime(EnterWaitingState);
                return;
            }

            if (runtimeDecision.StateLabel == "Selected" && runtimeDecision.SelectedUnit != null)
            {
                ApplySceneStateFromRuntime(() => EnterSelectedState(runtimeDecision.SelectedUnit));
            }
        }

        private void ApplySceneEffectsAfterRuntimeUnitClick(
            RuntimeStateTransitionDecision runtimeDecision,
            Unit clickedUnit,
            Unit previouslySelectedUnit)
        {
            if (runtimeDecision.StateLabel == "Waiting")
            {
                ApplySceneStateFromRuntime(EnterWaitingState);
                return;
            }

            if (runtimeDecision.StateLabel == "Selected" && runtimeDecision.SelectedUnit != null)
            {
                ApplySceneStateFromRuntime(() => EnterSelectedState(runtimeDecision.SelectedUnit));
                return;
            }

            if (runtimeDecision.StateLabel == "PendingMoveConfirm"
                && runtimeDecision.SelectedUnit != null
                && runtimeDecision.SelectedUnit == previouslySelectedUnit)
            {
                runtimeDecision.SelectedUnit.GetComponent<MoveAbility>()?.OnSelectedUnitClicked(this);
                return;
            }

            ApplySceneStateFromRuntime(EnterWaitingState);
        }

        private void ApplySceneEffectsAfterRuntimeCellClick(
            RuntimeStateTransitionDecision runtimeDecision,
            Unit previouslySelectedUnit,
            Cell sceneCell)
        {
            if (runtimeDecision.StateLabel == "Waiting")
            {
                ApplySceneStateFromRuntime(EnterWaitingState);
                return;
            }

            if (runtimeDecision.StateLabel == "PendingMoveConfirm"
                && runtimeDecision.SelectedUnit == previouslySelectedUnit
                && sceneCell != null)
            {
                MoveAbility moveAbility = previouslySelectedUnit.GetComponent<MoveAbility>();
                if (moveAbility != null)
                {
                    moveAbility.OnCellClicked(sceneCell, this);
                }
                else
                {
                    ForwardCellClickToAbilities(previouslySelectedUnit, sceneCell);
                }
            }
        }

        private void ForwardCellClickToAbilities(Unit actingUnit, Cell cell)
        {
            if (actingUnit == null || cell == null)
            {
                return;
            }

            foreach (BattleAction action in actingUnit.GetBattleActions())
            {
                action?.OnCellClicked(cell, this);
            }
        }

        internal RuntimeStateTransitionDecision ProcessRuntimeWaitingStateUnitClick(Unit clickedUnit)
        {
            ResolveRuntimeGrid();
            if (runtimeGrid == null)
            {
                return new RuntimeStateTransitionDecision("Waiting", null, null);
            }

            SyncRuntimeMirrorNow();
            runtimeGrid.ProcessUnitClick(GetRuntimeUnit(clickedUnit));
            return CaptureRuntimeDecision();
        }

        internal RuntimeStateTransitionDecision ProcessRuntimeRightClick()
        {
            ResolveRuntimeGrid();
            if (runtimeGrid == null)
            {
                return new RuntimeStateTransitionDecision("Waiting", null, null);
            }

            SyncRuntimeMirrorNow();
            runtimeGrid.ProcessRightClick();
            return CaptureRuntimeDecision();
        }

        internal RuntimeStateTransitionDecision ProcessRuntimeSelectedStateUnitClick(Unit clickedUnit)
        {
            ResolveRuntimeGrid();
            if (runtimeGrid == null)
            {
                return new RuntimeStateTransitionDecision("Waiting", null, null);
            }

            SyncRuntimeMirrorNow();
            runtimeGrid.ProcessUnitClick(GetRuntimeUnit(clickedUnit));
            return CaptureRuntimeDecision();
        }

        internal RuntimeStateTransitionDecision ProcessRuntimeSelectedStateCellClick(Cell clickedCell)
        {
            ResolveRuntimeGrid();
            if (runtimeGrid == null)
            {
                return new RuntimeStateTransitionDecision("Waiting", null, null);
            }

            SyncRuntimeMirrorNow();
            runtimeGrid.ProcessCellClick(clickedCell);
            return CaptureRuntimeDecision();
        }

        internal RuntimeStateTransitionDecision ProcessRuntimePendingMoveRightClick()
        {
            return ProcessRuntimeRightClick();
        }

        internal RuntimeStateTransitionDecision ProcessRuntimePendingMoveWait()
        {
            ResolveRuntimeGrid();
            if (runtimeGrid == null)
            {
                return new RuntimeStateTransitionDecision("Waiting", null, null);
            }

            SyncRuntimeMirrorNow();
            runtimeGrid.ConfirmPendingMoveWait();
            return CaptureRuntimeDecision();
        }
        // --- Runtime battle flow routing ---
        internal bool ProcessRuntimeRoutedBattleOutcomeEvaluation()
        {
            ResolveRuntimeGrid();
            if (runtimeGrid == null)
            {
                bool legacyFinished = CheckGameFinished();
                if (legacyFinished)
                {
                    SyncStateToGameOver();
                }

                return legacyFinished;
            }

            if (GameFinished)
            {
                SyncStateToGameOver();
                return true;
            }

            SyncRuntimeMirrorNow();
            BattleOutcome runtimeOutcomeAuthority = runtimeGrid.EvaluateBattleOutcome();
            bool finished = TryApplyBattleOutcome(runtimeOutcomeAuthority);
            if (finished)
            {
                SyncStateToGameOver();
            }

            return finished;
        }

        internal void ProcessRuntimeRoutedCombatPresentationBegan()
        {
            ResolveRuntimeGrid();
            if (runtimeGrid == null)
            {
                return;
            }

            SyncRuntimeMirrorNow();

            if (IsHumanTurn && CurrentState is not States.CellGridStateBlockInput)
            {
                EnterSceneOnlyBlockedInputState();
            }
        }

        internal void ProcessRuntimeRoutedCombatPresentationEnded()
        {
            ResolveRuntimeGrid();
            if (runtimeGrid == null)
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
            ResolveRuntimeGrid();
            if (runtimeGrid == null)
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
                new Runtime.Grid.States.RuntimeGridStateWaitingForInput(runtimeGrid),
                EnterWaitingState);
        }

        internal void PrepareRuntimeRoutedAiTurn()
        {
            SyncRuntimeMirrorNow();
        }

        internal void EndUnitsForCurrentPlayerTurnViaUnits()
        {
            if (ShouldRouteTurnLoopThroughRuntime)
            {
                ApplySceneTurnEndToCurrentPlayerUnits();
                return;
            }

            EndUnitsForCurrentPlayerTurn();
        }

        internal void ProcessRuntimeRoutedEndTurn()
        {
            ResolveRuntimeGrid();
            if (runtimeGrid == null)
            {
                EndTurn();
                return;
            }

            EnterBlockedInputState();
            if (RequestBattleOutcomeEvaluation())
            {
                return;
            }

            EndUnitsForCurrentPlayerTurnViaUnits();

            RoundRobinTurnPlan plan = RoundRobinBattleFlow.ResolveTurn(this);
            if (plan.NextPlayer == null)
            {
                Debug.LogError("CellGrid: No valid battle turn resolver or next player was found.");
                return;
            }

            PrepareRuntimeTurnStartForPlan(plan);
            runtimeGrid.EndCurrentTurn(kickTurnPlayerPlay: false);
            ApplySceneStateFromRuntime(() => CommitTurnTransition(plan, kickPlayerPlay: false, syncUnitTurnHooks: false));
            ApplyRuntimeTurnStartToScenePlayableUnits();
            SyncRuntimeMirrorNow();
            runtimeGrid.KickCurrentTurnPlay();
        }

        internal Cell ResolveRuntimeActingCell(Unit unit)
        {
            GridUnit runtimeUnit = GetRuntimeUnit(unit);
            if (runtimeUnit == null)
            {
                return unit?.Cell;
            }

            Cell runtimeActingCell = runtimeUnit.PreviewCell;
            if (runtimeActingCell != null)
            {
                return runtimeActingCell ?? unit?.PreviewCell ?? unit?.Cell;
            }

            return unit?.Cell;
        }

        internal List<Unit> GetAttackableEnemiesFromActingCell(Unit actor, Cell actingCell)
        {
            if (actor == null || actingCell == null)
            {
                return new List<Unit>();
            }

            return GetEnemyUnits(CurrentPlayer)
                .Where(enemy => enemy != null && actor.CanAttackTargetWithAnyWeapon(enemy, actingCell))
                .ToList();
        }
    }
}
