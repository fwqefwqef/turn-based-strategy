using System;
using System.Collections.Generic;
using System.Linq;
using TbsFramework.Cells;
using TbsFramework.Grid;
using TbsFramework.Units;
using UnityEngine;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CustomCellGrid
    {
        private void Update()
        {
            NormalizeHumanInputState();
            ApplyRuntimeBoardMirror();
            SyncRuntimeSceneInputGate();
            ProcessDeferredDestroyQueue();
        }

        private void Awake()
        {
            PrepareFriendlyDeploymentFromSave();
            ResolveRuntimeBoard();
            GameStarted += OnGameStarted;
            GameEnded += OnLegacyGameEnded;
            LevelLoadingDone += OnLevelLoadingDoneInternal;
            TurnEnded += OnTurnEnded;
            UnitAdded += OnUnitAdded;
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
            GameStarted -= OnGameStarted;
            GameEnded -= OnLegacyGameEnded;
            LevelLoadingDone -= OnLevelLoadingDoneInternal;
            TurnEnded -= OnTurnEnded;
            UnitAdded -= OnUnitAdded;

            foreach (var unit in subscribedUnits)
            {
                if (unit != null)
                {
                    unit.CombatDestroyed -= OnCombatDestroyed;
                }
            }

            subscribedUnits.Clear();

            foreach (Cell cell in subscribedCells)
            {
                if (cell != null)
                {
                    cell.CellHighlighted -= OnCellHighlightedInternal;
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

            if (CurrentCustomPlayerNumber == 0)
            {
                RoundCount++;
            }

            MarkRuntimeBoardDirty();
            SyncRuntimeMirrorNow();
            ShadowCompareCurrentPlayerSync();
            BattleTurnEnded?.Invoke(this, EventArgs.Empty);
            TurnStarted?.Invoke(this, EventArgs.Empty);
        }

        private void OnUnitAdded(object sender, UnitCreatedEventArgs e)
        {
            if (e?.unit == null)
            {
                return;
            }

            var customUnit = e.unit.GetComponent<CustomUnit>();
            if (customUnit == null || !subscribedUnits.Add(customUnit))
            {
                return;
            }

            customUnit.CombatDestroyed += OnCombatDestroyed;
            customUnit.DestroyedInCombat += OnCustomUnitDestroyed;
            customUnit.SyncMirroredRuntimeNow();
            CustomUnitAdded?.Invoke(this, new CustomUnitAddedEventArgs(customUnit));
            MarkRuntimeBoardDirty();
        }

        private void OnCombatDestroyed(object sender, AttackEventArgs e)
        {
            var defender = e?.Defender;
            if (defender == null)
            {
                return;
            }

            if (defender is CustomUnit customUnit)
            {
                customUnit.CombatDestroyed -= OnCombatDestroyed;
                subscribedUnits.Remove(customUnit);
                customUnit.GetBattleActions().ForEach(action => action.OnOwnerDestroyed(this));
            }
            Units.Remove(defender);
            MarkRuntimeBoardDirty();
            RequestBattleOutcomeEvaluation();
        }

        private void OnCustomUnitDestroyed(object sender, CustomUnitDestroyedEventArgs e)
        {
            if (sender is CustomUnit customUnit)
            {
                customUnit.DestroyedInCombat -= OnCustomUnitDestroyed;
            }
        }

        private void OnLegacyGameEnded(object sender, GameEndedArgs e)
        {
            IReadOnlyList<int> winningPlayers = e?.gameResult?.WinningPlayers != null
                ? e.gameResult.WinningPlayers.ToList()
                : new List<int>();
            IReadOnlyList<int> losingPlayers = e?.gameResult?.LoosingPlayers != null
                ? e.gameResult.LoosingPlayers.ToList()
                : new List<int>();
            BattleEnded?.Invoke(this, new BattleEndedEventArgs(winningPlayers, losingPlayers));
        }

        private void OnLevelLoadingDoneInternal(object sender, EventArgs e)
        {
            SubscribeToExistingCells();
            LevelInitialized?.Invoke(this, EventArgs.Empty);
        }

        private void SubscribeToExistingCells()
        {
            foreach (Cell cell in GetAllCells())
            {
                if (cell == null || !subscribedCells.Add(cell))
                {
                    continue;
                }

                cell.CellHighlighted += OnCellHighlightedInternal;
            }
        }

        private void OnCellHighlightedInternal(object sender, EventArgs e)
        {
            if (sender is not Cell cell)
            {
                return;
            }

            if (cell.CurrentUnits != null && cell.CurrentUnits.Count > 0)
            {
                return;
            }

            EmptyCellHighlighted?.Invoke(this, EventArgs.Empty);
        }
    }
}
