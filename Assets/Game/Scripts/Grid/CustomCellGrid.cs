using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Campaign;
using Windy.Srpg.Game.Grid.States;
using TbsFramework.Cells;
using TbsFramework.Grid;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Abilities;
using UnityEngine;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Players;
using Windy.Srpg.Runtime.Board.States;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CustomCellGrid : MonoBehaviour, IBattleBoard
    {
        private const float CampaignSaveFlushDelaySeconds = 0.15f;

        public event EventHandler PreBattleStateChanged;
        public event EventHandler DeploymentRosterChanged;
        public event EventHandler<CustomUnitAddedEventArgs> CustomUnitAdded;
        public event EventHandler LevelInitialized;
        public event EventHandler BattleStarted;
        public event EventHandler BattleTurnEnded;
        public event EventHandler<BattleEndedEventArgs> BattleEnded;
        public event EventHandler EmptyCellHighlighted;

        public event EventHandler TurnStarted;
        public int RoundCount { get; private set; }
        public bool IsPreBattlePhase => enablePreBattleUi && !battleStarted;
        internal bool IsBattleStarted => battleStarted;
        IReadOnlyList<IBattlePlayer> IBattleBoard.Players => GetOrderedCustomPlayers().Cast<IBattlePlayer>().ToList();
        IReadOnlyList<IBattleUnit> IBattleBoard.Units => GetAllCustomUnits().Cast<IBattleUnit>().ToList();

        private readonly HashSet<CustomUnit> subscribedUnits = new HashSet<CustomUnit>();
        private readonly HashSet<Cell> subscribedCells = new HashSet<Cell>();
        [SerializeField] private bool autoCreateOwnedUnitSaveIfMissing;
        [SerializeField] private bool overwriteOwnedUnitSaveOnGameStarted;
        [SerializeField] private bool enablePreBattleUi = true;
        [SerializeField] private bool startBattleImmediatelyWithCurrentRoster;
        [SerializeField] private List<UnitPreset> starterOwnedUnitPresets = new List<UnitPreset>();
        private bool battleStarted;
        private bool isPreBattleDeploymentSwapMode;
        private int selectedPreBattleDeploymentSlotIndex = -1;
        private CustomUnit selectedPreBattleDeploymentUnit;
        private int preBattleDeploymentSelectionFrame = -1;
        private CustomCellGridState currentCustomState;
        private SceneUnitGenerator sceneUnitGenerator;
        private CampaignSaveData cachedCampaignSave;
        private bool campaignSaveDirty;
        private Coroutine pendingCampaignSaveFlushCoroutine;
        private string[] stagedDeploymentRosterUnitIds = Array.Empty<string>();
        private bool hasUnsavedDeploymentRosterChanges;
        private int occupancyRevision;
        private bool suppressLegacyToRuntimeStateMirror;
        public bool IsPreBattleDeploymentSwapModeActive => IsPreBattlePhase && isPreBattleDeploymentSwapMode;
        public bool HasUnsavedDeploymentRosterChanges => hasUnsavedDeploymentRosterChanges;
        public int OccupancyRevision => occupancyRevision;

        public int SelectedPreBattleDeploymentSlotIndex => selectedPreBattleDeploymentSlotIndex;
        public CustomCellGridState CurrentCustomState => currentCustomState;
        public CustomPlayer CurrentCustomPlayer => BattleBoardQueries.GetPlayerById(GetOrderedCustomPlayers(), CurrentPlayerNumber);
        public int CurrentPlayerId => CurrentCustomPlayerNumber;
        public int CurrentCustomPlayerNumber => CurrentCustomPlayer?.PlayerNumber ?? -1;
        public bool IsHumanTurn => CurrentCustomPlayer is CustomHumanPlayer;
        public bool CanRequestEndTurn => CurrentCustomState?.BlocksEndTurn != true;
        public bool ShouldRouteHumanMovementThroughRuntime => IsHumanTurn;
        public bool ShouldRouteAiMovementThroughRuntime => !IsHumanTurn;
        public bool ShouldRouteTurnLoopThroughRuntime => true;
        public bool ShouldRouteBattleOutcomeThroughRuntime => true;
        public bool ShouldSuppressFrameworkSceneInput => UsesRuntimeDirectSceneInput;
        public bool UsesRuntimeDirectSceneInput =>
            ShouldRouteHumanMovementThroughRuntime
            && !GameFinished
            && !IsPreBattlePhase
            && IsHumanSceneInputStateActive();

        public List<CustomPlayer> GetOrderedCustomPlayers()
        {
            IEnumerable<CustomPlayer> runtimePlayers = RuntimePlayers?
                .OfType<CustomPlayer>()
                .Where(player => player != null);

            if (runtimePlayers != null && runtimePlayers.Any())
            {
                return BattleBoardQueries.OrderPlayers(runtimePlayers);
            }

            return BattleBoardQueries.OrderPlayers(
                Players?
                    .OfType<CustomPlayer>()
                    .Where(player => player != null));
        }

        public List<Cell> GetAllCells()
        {
            return Cells?
                .Where(cell => cell != null)
                .ToList()
                ?? new List<Cell>();
        }

        public List<CustomUnit> GetAllCustomUnits()
        {
            return Units?
                .Select(ResolveCustomUnitFromRegistryUnit)
                .Where(unit => unit != null)
                .ToList()
                ?? new List<CustomUnit>();
        }

        public List<CustomUnit> GetActiveSceneCustomUnits()
        {
            return GetAllSceneCustomUnitsFromHierarchy()
                .Where(unit => unit != null && !unit.ExcludedFromBattle)
                .ToList();
        }

        public List<CustomUnit> GetAllSceneCustomUnitsFromHierarchy(bool includeExcludedFromBattle = false)
        {
            return GetSceneUnitGenerator()?.GetSceneCustomUnits(includeExcludedFromBattle) ?? new List<CustomUnit>();
        }

        public Transform GetSceneUnitsParent()
        {
            return GetSceneUnitGenerator()?.UnitsParent;
        }

        public void RequestFrameworkInitialize()
        {
            InitializeBattleScene();
        }

        public Cell FindCellByOffset(Vector2 offsetCoord)
        {
            return GetAllCells().FirstOrDefault(cell => cell.OffsetCoord == offsetCoord);
        }

        public List<CustomUnit> GetUnitsForPlayer(int playerNumber)
        {
            return BattleBoardQueries.GetUnitsForPlayer(GetAllCustomUnits(), playerNumber);
        }

        public List<CustomUnit> GetUnitsForPlayer(CustomPlayer player)
        {
            return player == null
                ? new List<CustomUnit>()
                : GetUnitsForPlayer(player.PlayerNumber);
        }

        public List<CustomUnit> GetCurrentPlayerCustomUnits()
        {
            return CurrentCustomPlayer == null
                ? new List<CustomUnit>()
                : BattleBoardQueries.GetCurrentPlayerUnits(GetAllCustomUnits(), CurrentCustomPlayer.PlayerNumber);
        }

        public List<CustomUnit> GetEnemyUnits(CustomPlayer player)
        {
            if (player == null)
            {
                return new List<CustomUnit>();
            }

            return BattleBoardQueries.GetEnemyUnits(GetAllCustomUnits(), player.PlayerNumber);
        }

        public void SetState(CustomCellGridState state)
        {
            if (ReferenceEquals(currentCustomState, state))
            {
                return;
            }

            currentCustomState?.OnStateExit();
            currentCustomState = state;
            if (!suppressLegacyToRuntimeStateMirror)
            {
                MirrorLegacyStateToRuntimeBoard(state);
            }

            currentCustomState?.OnStateEnter();
            InstallFrameworkInputRouter();
        }

        public void EnterWaitingState()
        {
            if (suppressLegacyToRuntimeStateMirror)
            {
                SetState(new CustomCellGridStateWaitingForInput(this));
                return;
            }

            ApplyRuntimeDrivenState(
                new BoardStateWaitingForInput(runtimeBoard),
                () => SetState(new CustomCellGridStateWaitingForInput(this)));
        }

        public void EnterBlockedInputState()
        {
            if (suppressLegacyToRuntimeStateMirror)
            {
                SetState(new CustomCellGridStateBlockInput(this));
                return;
            }

            ApplyRuntimeDrivenState(
                new BoardStateBlockedInput(runtimeBoard),
                () => SetState(new CustomCellGridStateBlockInput(this)));
        }

        /// <summary>
        /// Blocks framework input without replacing the runtime board state. Used during pending-move
        /// combat presentation so <see cref="BattleBoard.ConfirmPendingMoveAfterCombat"/> can still run.
        /// </summary>
        internal void EnterLegacyBlockedInputState()
        {
            ApplyLegacyStateFromRuntime(() => SetState(new CustomCellGridStateBlockInput(this)));
        }

        public void EnterRemotePlayerTurnState()
        {
            if (suppressLegacyToRuntimeStateMirror)
            {
                SetState(new CustomCellGridStateRemotePlayerTurn(this));
                return;
            }

            ApplyRuntimeDrivenState(
                new BoardStateBlockedInput(runtimeBoard),
                () => SetState(new CustomCellGridStateRemotePlayerTurn(this)));
        }

        public void EnterAiTurnState(CustomAiPlayer aiPlayer)
        {
            if (suppressLegacyToRuntimeStateMirror)
            {
                SetState(new CustomCellGridStateAiTurn(this, aiPlayer));
                return;
            }

            ApplyRuntimeDrivenState(
                new BoardStateAiTurn(runtimeBoard),
                () => SetState(new CustomCellGridStateAiTurn(this, aiPlayer)));
        }

        public void EnterSelectedState(CustomUnit unit)
        {
            if (unit == null)
            {
                EnterWaitingState();
                return;
            }

            if (suppressLegacyToRuntimeStateMirror)
            {
                SetState(new CustomUnitSelectedState(this, unit, unit.GetBattleActions()));
                return;
            }

            if (ShouldRouteHumanMovementThroughRuntime)
            {
                ResolveRuntimeBoard();
                ApplyRuntimeDrivenState(
                    BuildRuntimeSelectedState(unit),
                    () => SetState(new CustomUnitSelectedState(this, unit, unit.GetBattleActions())));
                return;
            }

            SetState(new CustomUnitSelectedState(this, unit, unit.GetBattleActions()));
        }

        public void EnterPendingMoveConfirmState(CustomMoveAbility moveAbility)
        {
            if (moveAbility == null)
            {
                EnterWaitingState();
                return;
            }

            if (suppressLegacyToRuntimeStateMirror)
            {
                SetState(new CustomCellGridStateMovePendingConfirm(this, moveAbility));
                return;
            }

            if (ShouldRouteHumanMovementThroughRuntime)
            {
                ResolveRuntimeBoard();
                ApplyRuntimeDrivenState(
                    BuildRuntimePendingMoveState(moveAbility),
                    () => SetState(new CustomCellGridStateMovePendingConfirm(this, moveAbility)));
                return;
            }

            SetState(new CustomCellGridStateMovePendingConfirm(this, moveAbility));
        }

        public void RequestEndTurn()
        {
            if (GameFinished)
            {
                SyncCustomStateToGameOver();
                return;
            }

            if (!CanRequestEndTurn)
            {
                return;
            }

            ProcessRuntimeRoutedEndTurn();
        }

        public bool RequestBattleOutcomeEvaluation()
        {
            if (ShouldRouteBattleOutcomeThroughRuntime)
            {
                return ProcessRuntimeRoutedBattleOutcomeEvaluation();
            }

            bool finished = CheckGameFinished();
            if (finished)
            {
                SyncCustomStateToGameOver();
            }

            return finished;
        }

        /// <summary>
        /// Syncs runtime mirror and blocked input before pending-move combat presentation (attack/skill/heal).
        /// </summary>
        internal void PrepareRuntimeRoutedPendingAttackCommit()
        {
            if (!ShouldRouteHumanMovementThroughRuntime || GameFinished)
            {
                return;
            }

            ProcessRuntimeRoutedCombatPresentationBegan();
        }

        /// <summary>
        /// Commits the pending move on the runtime board after combat presentation, then syncs legacy unit state.
        /// </summary>
        internal void ProcessRuntimeRoutedPendingAttackMoveCommit(CustomUnit unit)
        {
            ProcessRuntimeRoutedPendingMoveCommit(unit, consumeAllRemainingMovement: false);
        }

        /// <summary>
        /// Commits a pending move through runtime board authority, then syncs legacy unit/cell state.
        /// </summary>
        internal void ProcessRuntimeRoutedPendingMoveCommit(CustomUnit unit, bool consumeAllRemainingMovement = false)
        {
            if (!ShouldRouteHumanMovementThroughRuntime || unit == null || !unit.HasPendingMove)
            {
                return;
            }

            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            SyncRuntimeMirrorNow();
            runtimeBoard.ConfirmPendingMoveAfterCombat(consumeAllRemainingMovement);

            BattleUnit runtimeUnit = unit.GetComponent<BattleUnit>();
            if (unit.HasPendingMove
                && runtimeUnit != null
                && runtimeUnit.HasPendingMove)
            {
                runtimeUnit.ConfirmPendingMove(consumeAllRemainingMovement, syncTransform: false);
            }

            unit.ApplyLegacySyncAfterRuntimePendingMoveCommit(this);
        }

        /// <summary>
        /// Commits a pending move after combat presentation when runtime routing is active; otherwise
        /// falls back to legacy <see cref="CustomUnit.ConfirmPendingMove"/>.
        /// </summary>
        internal void TryCommitPendingMoveAfterCombatPresentation(CustomUnit unit)
        {
            TryCommitPendingMoveFromPendingAction(unit, consumeAllRemainingMovement: false);
        }

        /// <summary>
        /// Commits a pending move from a pending-action menu (trade close, item use, skill prep, etc.).
        /// </summary>
        internal void TryCommitPendingMoveFromPendingAction(CustomUnit unit, bool consumeAllRemainingMovement = false)
        {
            if (unit == null || !unit.HasPendingMove)
            {
                return;
            }

            if (ShouldRouteHumanMovementThroughRuntime)
            {
                ProcessRuntimeRoutedPendingMoveCommit(unit, consumeAllRemainingMovement);
                return;
            }

            unit.ConfirmPendingMove(consumeAllRemainingMovement);
        }

        /// <summary>
        /// Restores human input after combat. When runtime turn routing is active, the runtime
        /// board owns the post-combat return to waiting-for-input.
        /// </summary>
        public void EnterPostCombatGridState()
        {
            if (GameFinished)
            {
                SyncCustomStateToGameOver();
                return;
            }

            if (ShouldRouteTurnLoopThroughRuntime)
            {
                ProcessRuntimeRoutedPostCombatRecovery();
                return;
            }

            EnterWaitingState();
        }

        /// <summary>
        /// Called when combat presentation begins (attack/skill sequences). Syncs runtime blocked
        /// input for human turns and refreshes the runtime mirror before combat resolves.
        /// </summary>
        internal void NotifyCombatPresentationBegan()
        {
            if (!ShouldRouteTurnLoopThroughRuntime || GameFinished)
            {
                return;
            }

            ProcessRuntimeRoutedCombatPresentationBegan();
        }

        /// <summary>
        /// Called when the outermost combat presentation ends. Refreshes occupancy/outcome on the
        /// runtime board after combat mutations settle.
        /// </summary>
        internal void NotifyCombatPresentationEnded()
        {
            if (!ShouldRouteTurnLoopThroughRuntime || GameFinished)
            {
                return;
            }

            ProcessRuntimeRoutedCombatPresentationEnded();
        }

        internal void SyncCustomStateToGameOver()
        {
            if (!GameFinished)
            {
                return;
            }

            currentCustomState = new CustomCellGridStateGameOver(this);
        }

        internal void ApplyLegacyStateFromRuntime(Action applyState)
        {
            suppressLegacyToRuntimeStateMirror = true;
            try
            {
                applyState?.Invoke();
            }
            finally
            {
                suppressLegacyToRuntimeStateMirror = false;
            }
        }

        private SceneUnitGenerator GetSceneUnitGenerator()
        {
            if (sceneUnitGenerator == null)
            {
                sceneUnitGenerator = GetComponent<SceneUnitGenerator>();
            }

            return sceneUnitGenerator;
        }
    }

    public sealed class CustomUnitAddedEventArgs : EventArgs
    {
        public CustomUnitAddedEventArgs(CustomUnit unit)
        {
            Unit = unit;
        }

        public CustomUnit Unit { get; }
    }

    public sealed class BattleEndedEventArgs : EventArgs
    {
        public BattleEndedEventArgs(IReadOnlyList<int> winningPlayerNumbers, IReadOnlyList<int> losingPlayerNumbers)
        {
            WinningPlayerNumbers = winningPlayerNumbers ?? Array.Empty<int>();
            LosingPlayerNumbers = losingPlayerNumbers ?? Array.Empty<int>();
        }

        public IReadOnlyList<int> WinningPlayerNumbers { get; }
        public IReadOnlyList<int> LosingPlayerNumbers { get; }
    }
}
