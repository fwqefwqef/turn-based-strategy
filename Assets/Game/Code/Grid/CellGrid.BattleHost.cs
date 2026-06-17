using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid
{
    public partial class CellGrid
    {
        public void InitializeBattle() => InitializeBattleScene();

        public void StartLegacyFrameworkBattle() => StartLegacyBattle();

        public void RequestFrameworkInitializeAndStart()
        {
            InitializeBattleScene();
            StartBattleViaRuntimeBoard();
        }

        public void RequestFrameworkBattleStart()
        {
            StartBattleViaRuntimeBoard();
        }

        private void Update()
        {
            NormalizeHumanInputState();
            ApplyRuntimeBoardMirror();
            SyncRuntimeSceneInputGate();
            ProcessDeferredDestroyQueue();
        }

        private void Awake()
        {
            EnsureLegacyGridHost();
            EnsureSceneCellAnchors();
            PrepareFriendlyDeploymentFromSave();
            ResolveRuntimeBoard();
            WireLegacyGridEvents();
            SubscribeToExistingCells();
        }

        private void Start()
        {
            if (enablePreBattleUi && !startBattleImmediatelyWithCurrentRoster)
            {
                RequestFrameworkInitialize();
                SetDeploymentSlotVisibility(true);
                EnterBlockedInputState();
                PreBattleStateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (startBattleImmediatelyWithCurrentRoster || ShouldStartGameImmediately)
            {
                SetDeploymentSlotVisibility(false);
                RequestFrameworkInitializeAndStart();
                return;
            }

            SetDeploymentSlotVisibility(false);
            RequestFrameworkInitialize();
        }

        private void OnDestroy()
        {
            FlushCampaignSaveImmediate();
            UnwireLegacyGridEvents();

            foreach (var unit in subscribedUnits)
            {
                if (unit != null)
                {
                    unit.CombatDestroyed -= OnCombatDestroyed;
                }
            }

            subscribedUnits.Clear();

            foreach (BattleSquareCell cell in subscribedCells)
            {
                if (cell != null)
                {
                    cell.Hovered -= OnCellHighlightedInternal;
                }
            }

            subscribedCells.Clear();
            ClearRuntimeSceneInputCoordinator();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                FlushCampaignSaveImmediate();
            }
        }

        private void OnApplicationQuit()
        {
            FlushCampaignSaveImmediate();
        }

        private void StartBattleViaRuntimeBoard()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                StartLegacyBattle();
                return;
            }

            SyncRuntimeMirrorNow();
            RoundRobinTurnPlan plan = RoundRobinBattleFlow.ResolveStart(this);
            if (plan.NextPlayer == null)
            {
                Debug.LogError("CellGrid: No valid battle turn resolver or next player was found.");
                return;
            }

            ApplyLegacyStateFromRuntime(() => SyncBattleStartFromPlan(plan, kickPlayerPlay: false, syncUnitTurnHooks: false));
            PrepareRuntimeTurnStartForPlan(plan);
            runtimeBoard.BeginBattleFromHost(
                plan,
                kickFirstTurn: false,
                refreshSceneCollections: false);
            ApplyRuntimeTurnStartToLegacyPlayableUnits();
            SyncRuntimeMirrorNow();
            SyncRuntimeSceneInputGate();
            runtimeBoard.KickCurrentTurnPlay();
            Debug.Log("Game started via runtime board");
        }

        private void OnGameStarted(object sender, EventArgs e)
        {
            isPreBattleDeploymentSwapMode = false;
            selectedPreBattleDeploymentSlotIndex = -1;
            selectedPreBattleDeploymentUnit = null;
            battleStarted = true;
            SetDeploymentSlotVisibility(false);
            RebuildSceneCellOccupancy();
            UpdateDeploymentSlotSelectionVisuals();
            TryPersistOwnedUnitSave();
            RoundCount = 1;
            MarkRuntimeBoardDirty();
            PreBattleStateChanged?.Invoke(this, EventArgs.Empty);
            BattleStarted?.Invoke(this, EventArgs.Empty);
            TurnStarted?.Invoke(this, EventArgs.Empty);
        }

        private void OnTurnEnded(object sender, bool isNetworkInvoked)
        {
            RebuildSceneCellOccupancy();

            if (CurrentPlayerNumber == 0)
            {
                RoundCount++;
            }

            MarkRuntimeBoardDirty();
            SyncRuntimeMirrorNow();
            BattleTurnEnded?.Invoke(this, EventArgs.Empty);
            TurnStarted?.Invoke(this, EventArgs.Empty);
        }

        private void OnUnitAdded(object sender, UnitCreatedEventArgs e)
        {
            if (e?.unitTransform == null)
            {
                return;
            }

            Unit customUnit = e.unitTransform.GetComponent<Unit>();
            if (customUnit == null || !subscribedUnits.Add(customUnit))
            {
                return;
            }

            customUnit.CombatDestroyed += OnCombatDestroyed;
            customUnit.DestroyedInCombat += OnUnitDestroyed;
            customUnit.SyncMirroredRuntimeNow();
            UnitAdded?.Invoke(this, new UnitAddedEventArgs(customUnit));
            MarkRuntimeBoardDirty();
        }

        private void OnCombatDestroyed(object sender, AttackEventArgs e)
        {
            Unit defender = e?.Defender;
            if (defender == null)
            {
                return;
            }

            defender.CombatDestroyed -= OnCombatDestroyed;
            subscribedUnits.Remove(defender);
            defender.GetBattleActions().ForEach(action => action.OnOwnerDestroyed(this));
            Units.Remove(defender);
            MarkRuntimeBoardDirty();
            RequestBattleOutcomeEvaluation();
        }

        private void OnUnitDestroyed(object sender, UnitDestroyedEventArgs e)
        {
            if (sender is Unit customUnit)
            {
                customUnit.DestroyedInCombat -= OnUnitDestroyed;
            }
        }

        private void OnLevelLoadingDoneInternal(object sender, EventArgs e)
        {
            EnsureSceneCellAnchors();
            SubscribeToExistingCells();
            LevelInitialized?.Invoke(this, EventArgs.Empty);
        }

        private void SubscribeToExistingCells()
        {
            foreach (BattleSquareCell cell in GetAllBoardCells())
            {
                if (cell == null || !subscribedCells.Add(cell))
                {
                    continue;
                }

                cell.Hovered += OnCellHighlightedInternal;
            }
        }

        private void OnCellHighlightedInternal(BoardCell cell)
        {
            if (cell is not BattleSquareCell battleCell)
            {
                return;
            }

            if (battleCell.Occupants != null && battleCell.Occupants.Count > 0)
            {
                return;
            }

            EmptyCellHighlighted?.Invoke(this, EventArgs.Empty);
        }

        internal bool TryRouteEndTurnThroughRuntime()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null || !ShouldRouteTurnLoopThroughRuntime)
            {
                return false;
            }

            RequestEndTurn();
            return true;
        }

        internal bool TryDispatchCellDeselected(BattleSquareCell cell)
        {
            return cell != null && TryDispatchToCurrentState(state => state.OnCellDeselected(cell));
        }

        internal bool TryDispatchCellSelected(BattleSquareCell cell)
        {
            return cell != null && TryDispatchToCurrentState(state => state.OnCellSelected(cell));
        }

        internal bool TryDispatchCellClicked(BattleSquareCell cell)
        {
            return cell != null && TryDispatchToCurrentState(state => state.OnCellClicked(cell));
        }

        internal bool TryDispatchUnitClicked(Unit unit)
        {
            return unit != null && TryDispatchToCurrentState(state => state.OnUnitClicked(unit));
        }

        internal bool TryDispatchUnitHighlighted(Unit unit)
        {
            return unit != null && TryDispatchToCurrentState(state => state.OnUnitHighlighted(unit));
        }

        internal bool TryDispatchUnitDehighlighted(Unit unit)
        {
            return unit != null && TryDispatchToCurrentState(state => state.OnUnitDehighlighted(unit));
        }

        private bool TryDispatchToCurrentState(Action<CellGridState> dispatch)
        {
            if (CurrentState == null)
            {
                return false;
            }

            dispatch(CurrentState);
            return true;
        }

        private void InstallFrameworkInputRouter()
        {
            SyncRuntimeSceneInputGate();
        }
    }
}
