using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Campaign;
using Windy.Srpg.Game.Grid.States;
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
    public partial class CellGrid : MonoBehaviour, IBattleBoard
    {
        [HideInInspector] public bool Is2D;
        public Transform PlayersParent;
        public bool ShouldStartGameImmediately = true;

        private const float CampaignSaveFlushDelaySeconds = 0.15f;

        public event EventHandler PreBattleStateChanged;
        public event EventHandler DeploymentRosterChanged;
        public event EventHandler<UnitAddedEventArgs> UnitAdded;
        public event EventHandler LevelInitialized;
        public event EventHandler BattleStarted;
        public event EventHandler BattleTurnEnded;
        public event EventHandler<BattleEndedEventArgs> BattleEnded;
        public event EventHandler EmptyCellHighlighted;

        public event EventHandler TurnStarted;
        public int RoundCount { get; private set; }
        public bool IsPreBattlePhase => enablePreBattleUi && !battleStarted;
        internal bool IsBattleStarted => battleStarted;
        IReadOnlyList<IBattlePlayer> IBattleBoard.Players => GetOrderedPlayers().Cast<IBattlePlayer>().ToList();
        IReadOnlyList<IBoardUnit> IBattleBoard.Units => GetAllUnits().Cast<IBoardUnit>().ToList();

        private readonly HashSet<Unit> subscribedUnits = new HashSet<Unit>();
        private readonly HashSet<BattleSquareCell> subscribedCells = new HashSet<BattleSquareCell>();
        [SerializeField] private bool autoCreateOwnedUnitSaveIfMissing;
        [SerializeField] private bool overwriteOwnedUnitSaveOnGameStarted;
        [SerializeField] private bool enablePreBattleUi = true;
        [SerializeField] private bool startBattleImmediatelyWithCurrentRoster;
        [SerializeField] private List<UnitPreset> starterOwnedUnitPresets = new List<UnitPreset>();
        private bool battleStarted;
        private bool isPreBattleDeploymentSwapMode;
        private int selectedPreBattleDeploymentSlotIndex = -1;
        private Unit selectedPreBattleDeploymentUnit;
        private int preBattleDeploymentSelectionFrame = -1;
        private CellGridState currentState;
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
        public CellGridState CurrentState => currentState;
        public Player CurrentPlayer => BattleBoardQueries.GetPlayerById(GetOrderedPlayers(), currentPlayerNumber);
        public int CurrentPlayerId => currentPlayerNumber;
        public int CurrentPlayerNumber => currentPlayerNumber;
        public bool IsHumanTurn => CurrentPlayer is HumanPlayer;
        public bool CanRequestEndTurn => CurrentState?.BlocksEndTurn != true;
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

        public List<Player> GetOrderedPlayers()
        {
            IEnumerable<Player> runtimePlayers = RuntimePlayers?
                .OfType<Player>()
                .Where(player => player != null);

            if (runtimePlayers != null && runtimePlayers.Any())
            {
                return BattleBoardQueries.OrderPlayers(runtimePlayers);
            }

            return BattleBoardQueries.OrderPlayers(
                Players?
                    .OfType<Player>()
                    .Where(player => player != null));
        }

        public List<BattleSquareCell> GetAllBoardCells()
        {
            return Cells?
                .Where(cell => cell != null)
                .ToList()
                ?? new List<BattleSquareCell>();
        }

        [Obsolete("Use GetAllBoardCells().")]
        public List<BattleSquareCell> GetAllCells() => GetAllBoardCells();

        public BattleSquareCell FindCellByOffset(Vector2 offsetCoord)
        {
            return GetAllBoardCells().FirstOrDefault(cell =>
                cell.Coordinates.x == Mathf.RoundToInt(offsetCoord.x)
                && cell.Coordinates.y == Mathf.RoundToInt(offsetCoord.y));
        }

        public List<Unit> GetAllUnits()
        {
            return Units?
                .Select(ResolveUnitFromRegistryUnit)
                .Where(unit => unit != null)
                .ToList()
                ?? new List<Unit>();
        }

        public List<Unit> GetActiveSceneUnits()
        {
            return GetAllSceneUnitsFromHierarchy()
                .Where(unit => unit != null && !unit.ExcludedFromBattle)
                .ToList();
        }

        public List<Unit> GetAllSceneUnitsFromHierarchy(bool includeExcludedFromBattle = false)
        {
            return GetSceneUnitGenerator()?.GetSceneUnits(includeExcludedFromBattle) ?? new List<Unit>();
        }

        public Transform GetSceneUnitsParent()
        {
            return GetSceneUnitGenerator()?.UnitsParent;
        }

        public void RequestFrameworkInitialize()
        {
            InitializeBattleScene();
        }

        public List<Unit> GetUnitsForPlayer(int playerNumber)
        {
            return BattleBoardQueries.GetUnitsForPlayer(GetAllUnits(), playerNumber);
        }

        public List<Unit> GetUnitsForPlayer(Player player)
        {
            return player == null
                ? new List<Unit>()
                : GetUnitsForPlayer(player.PlayerNumber);
        }

        public List<Unit> GetCurrentPlayerUnits()
        {
            return CurrentPlayer == null
                ? new List<Unit>()
                : BattleBoardQueries.GetCurrentPlayerUnits(GetAllUnits(), CurrentPlayer.PlayerNumber);
        }

        public List<Unit> GetEnemyUnits(Player player)
        {
            if (player == null)
            {
                return new List<Unit>();
            }

            return BattleBoardQueries.GetEnemyUnits(GetAllUnits(), player.PlayerNumber);
        }

        public void SetState(CellGridState state)
        {
            if (ReferenceEquals(currentState, state))
            {
                return;
            }

            currentState?.OnStateExit();
            currentState = state;
            if (!suppressLegacyToRuntimeStateMirror)
            {
                MirrorLegacyStateToRuntimeBoard(state);
            }

            currentState?.OnStateEnter();
            InstallFrameworkInputRouter();
        }

        public void EnterWaitingState()
        {
            if (suppressLegacyToRuntimeStateMirror)
            {
                SetState(new CellGridStateWaitingForInput(this));
                return;
            }

            ApplyRuntimeDrivenState(
                new BoardStateWaitingForInput(runtimeBoard),
                () => SetState(new CellGridStateWaitingForInput(this)));
        }

        public void EnterBlockedInputState()
        {
            if (suppressLegacyToRuntimeStateMirror)
            {
                SetState(new CellGridStateBlockInput(this));
                return;
            }

            ApplyRuntimeDrivenState(
                new BoardStateBlockedInput(runtimeBoard),
                () => SetState(new CellGridStateBlockInput(this)));
        }

        /// <summary>
        /// Blocks framework input without replacing the runtime board state. Used during pending-move
        /// combat presentation so <see cref="BattleBoard.ConfirmPendingMoveAfterCombat"/> can still run.
        /// </summary>
        internal void EnterLegacyBlockedInputState()
        {
            ApplyLegacyStateFromRuntime(() => SetState(new CellGridStateBlockInput(this)));
        }

        public void EnterRemotePlayerTurnState()
        {
            if (suppressLegacyToRuntimeStateMirror)
            {
                SetState(new CellGridStateRemotePlayerTurn(this));
                return;
            }

            ApplyRuntimeDrivenState(
                new BoardStateBlockedInput(runtimeBoard),
                () => SetState(new CellGridStateRemotePlayerTurn(this)));
        }

        public void EnterAiTurnState(AiPlayer aiPlayer)
        {
            if (suppressLegacyToRuntimeStateMirror)
            {
                SetState(new CellGridStateAiTurn(this, aiPlayer));
                return;
            }

            ApplyRuntimeDrivenState(
                new BoardStateAiTurn(runtimeBoard),
                () => SetState(new CellGridStateAiTurn(this, aiPlayer)));
        }

        public void EnterSelectedState(Unit unit)
        {
            if (unit == null)
            {
                EnterWaitingState();
                return;
            }

            if (suppressLegacyToRuntimeStateMirror)
            {
                SetState(new UnitSelectedState(this, unit, unit.GetBattleActions()));
                return;
            }

            if (ShouldRouteHumanMovementThroughRuntime)
            {
                ResolveRuntimeBoard();
                ApplyRuntimeDrivenState(
                    BuildRuntimeSelectedState(unit),
                    () => SetState(new UnitSelectedState(this, unit, unit.GetBattleActions())));
                return;
            }

            SetState(new UnitSelectedState(this, unit, unit.GetBattleActions()));
        }

        public void EnterPendingMoveConfirmState(MoveAbility moveAbility)
        {
            if (moveAbility == null)
            {
                EnterWaitingState();
                return;
            }

            if (suppressLegacyToRuntimeStateMirror)
            {
                SetState(new CellGridStateMovePendingConfirm(this, moveAbility));
                return;
            }

            if (ShouldRouteHumanMovementThroughRuntime)
            {
                ResolveRuntimeBoard();
                ApplyRuntimeDrivenState(
                    BuildRuntimePendingMoveState(moveAbility),
                    () => SetState(new CellGridStateMovePendingConfirm(this, moveAbility)));
                return;
            }

            SetState(new CellGridStateMovePendingConfirm(this, moveAbility));
        }

        public void RequestEndTurn()
        {
            if (GameFinished)
            {
                SyncStateToGameOver();
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
                SyncStateToGameOver();
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
        internal void ProcessRuntimeRoutedPendingAttackMoveCommit(Unit unit)
        {
            ProcessRuntimeRoutedPendingMoveCommit(unit, consumeAllRemainingMovement: false);
        }

        /// <summary>
        /// Commits a pending move through runtime board authority, then syncs legacy unit/cell state.
        /// </summary>
        internal void ProcessRuntimeRoutedPendingMoveCommit(Unit unit, bool consumeAllRemainingMovement = false)
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

            BoardUnit runtimeUnit = unit.GetComponent<BoardUnit>();
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
        /// falls back to legacy <see cref="Unit.ConfirmPendingMove"/>.
        /// </summary>
        internal void TryCommitPendingMoveAfterCombatPresentation(Unit unit)
        {
            TryCommitPendingMoveFromPendingAction(unit, consumeAllRemainingMovement: false);
        }

        /// <summary>
        /// Commits a pending move from a pending-action menu (trade close, item use, skill prep, etc.).
        /// </summary>
        internal void TryCommitPendingMoveFromPendingAction(Unit unit, bool consumeAllRemainingMovement = false)
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
                SyncStateToGameOver();
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

        internal void SyncStateToGameOver()
        {
            if (!GameFinished)
            {
                return;
            }

            currentState = new CellGridStateGameOver(this);
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

    public sealed class UnitAddedEventArgs : EventArgs
    {
        public UnitAddedEventArgs(Unit unit)
        {
            Unit = unit;
        }

        public Unit Unit { get; }
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

