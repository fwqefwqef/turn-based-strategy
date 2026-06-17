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
    public partial class CustomCellGrid : CellGrid, IBattleBoard
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
        IReadOnlyList<IBattlePlayer> IBattleBoard.Players => GetOrderedCustomPlayers().Cast<IBattlePlayer>().ToList();
        IReadOnlyList<IBattleUnit> IBattleBoard.Units => GetAllCustomUnits().Cast<IBattleUnit>().ToList();

        private readonly HashSet<CustomUnit> subscribedUnits = new HashSet<CustomUnit>();
        private readonly HashSet<Cell> subscribedCells = new HashSet<Cell>();
        [SerializeField] private bool autoCreateOwnedUnitSaveIfMissing;
        [SerializeField] private bool overwriteOwnedUnitSaveOnGameStarted;
        [SerializeField] private bool enablePreBattleUi = true;
        [SerializeField] private bool startBattleImmediatelyWithCurrentRoster;
        [Tooltip("Migration toggle: when enabled on a human turn, selection/move routing, pending-move commit, end-turn routing, and movement animation use the runtime BattleUnit/BattleBoard instead of the framework. Default off.")]
        [SerializeField] private bool useRuntimeMovementExecution;
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
        public bool UseRuntimeMovementExecution => useRuntimeMovementExecution;
        public bool ShouldRouteHumanMovementThroughRuntime => useRuntimeMovementExecution && IsHumanTurn;
        public bool ShouldRouteBattleOutcomeThroughRuntime => useRuntimeMovementExecution;
        public bool ShouldSuppressFrameworkSceneInput => CanBridgeHumanRuntimeSceneInput;
        public bool CanBridgeHumanRuntimeSceneInput =>
            ShouldRouteHumanMovementThroughRuntime
            && !GameFinished
            && !IsPreBattlePhase
            && (CurrentCustomState is CustomCellGridStateWaitingForInput
                or CustomUnitSelectedState
                or CustomCellGridStateMovePendingConfirm);

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
                .OfType<CustomUnit>()
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
            Initialize();
        }

        public void RequestFrameworkInitializeAndStart()
        {
            InitializeAndStart();
        }

        public void RequestFrameworkBattleStart()
        {
            StartGame();
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
            currentCustomState = state;
            cellGridState = new LegacyCustomCellGridStateAdapter(this, state);
            if (!suppressLegacyToRuntimeStateMirror)
            {
                MirrorLegacyStateToRuntimeBoard(state);
            }
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

            SetState(new CustomUnitSelectedState(this, unit, unit.GetBattleActions()));
        }

        public void EnterPendingMoveConfirmState(CustomMoveAbility moveAbility)
        {
            if (moveAbility == null)
            {
                EnterWaitingState();
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

            if (ShouldRouteHumanMovementThroughRuntime)
            {
                ProcessRuntimeRoutedEndTurn();
                return;
            }

            ShadowCompareEndTurn();
            EndTurn();
        }

        public bool RequestBattleOutcomeEvaluation()
        {
            if (ShouldRouteBattleOutcomeThroughRuntime)
            {
                return ProcessRuntimeRoutedBattleOutcomeEvaluation();
            }

            bool finished = CheckGameFinished();
            ShadowCompareBattleOutcome(finished ? "Battle ended" : "Battle outcome");
            if (finished)
            {
                SyncCustomStateToGameOver();
            }

            return finished;
        }

        /// <summary>
        /// Restores human input after a pending-move combat coroutine. Leaves the framework
        /// game-over state untouched when the battle has already ended.
        /// </summary>
        public void EnterPostCombatGridState()
        {
            if (GameFinished)
            {
                SyncCustomStateToGameOver();
                return;
            }

            EnterWaitingState();
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
