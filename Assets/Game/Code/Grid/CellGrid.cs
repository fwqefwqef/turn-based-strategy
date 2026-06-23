using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Campaign;
using Windy.Srpg.Game.Diagnostics;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Abilities;
using UnityEngine;
using Windy.Srpg.Runtime.Grid;
using Windy.Srpg.Runtime.Players;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Grid
{
    /// <summary>
    /// Scene-facing battle host. Owns scene objects, deployment/save integration, Unity event
    /// wiring, and the scene state machine. Pushes collection/metadata into RuntimeGrid mirror only.
    /// </summary>
    public partial class CellGrid : MonoBehaviour, IGridContext
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
        IReadOnlyList<IBattlePlayer> IGridContext.Players => GetOrderedPlayers().Cast<IBattlePlayer>().ToList();
        IReadOnlyList<IGridUnit> IGridContext.Units => GetAllUnits().Cast<IGridUnit>().ToList();

        private readonly HashSet<Unit> subscribedUnits = new HashSet<Unit>();
        private readonly HashSet<Cell> subscribedCells = new HashSet<Cell>();
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
        public bool IsPreBattleDeploymentSwapModeActive => IsPreBattlePhase && isPreBattleDeploymentSwapMode;
        public bool HasUnsavedDeploymentRosterChanges => hasUnsavedDeploymentRosterChanges;
        public int OccupancyRevision => occupancyRevision;

        public int SelectedPreBattleDeploymentSlotIndex => selectedPreBattleDeploymentSlotIndex;
        public CellGridState CurrentState => currentState;
        public Player CurrentPlayer => GridQueries.GetPlayerById(GetOrderedPlayers(), currentPlayerNumber);
        public int CurrentPlayerId => currentPlayerNumber;
        public int CurrentPlayerNumber => currentPlayerNumber;
        public bool IsHumanTurn => CurrentPlayer is HumanPlayer;
        public bool CanRequestEndTurn => CurrentState?.BlocksEndTurn != true;

        public List<Player> GetOrderedPlayers()
        {
            IEnumerable<Player> runtimePlayers = RuntimePlayers?
                .OfType<Player>()
                .Where(player => player != null);

            if (runtimePlayers != null && runtimePlayers.Any())
            {
                return GridQueries.OrderPlayers(runtimePlayers);
            }

            return GridQueries.OrderPlayers(
                Players?
                    .OfType<Player>()
                    .Where(player => player != null));
        }

        public List<Cell> GetAllCells()
        {
            return Cells?
                .Where(cell => cell != null)
                .ToList()
                ?? new List<Cell>();
        }

        public Cell FindCellByOffset(Vector2 offsetCoord)
        {
            return GetAllCells().FirstOrDefault(cell =>
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
            return GridQueries.GetUnitsForPlayer(GetAllUnits(), playerNumber);
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
                : GridQueries.GetCurrentPlayerUnits(GetAllUnits(), CurrentPlayer.PlayerNumber);
        }

        public List<Unit> GetEnemyUnits(Player player)
        {
            if (player == null)
            {
                return new List<Unit>();
            }

            return GridQueries.GetEnemyUnits(GetAllUnits(), player.PlayerNumber);
        }

        public void SetState(CellGridState state)
        {
            if (ReferenceEquals(currentState, state))
            {
                return;
            }

            currentState?.OnStateExit();
            currentState = state;
            currentState?.OnStateEnter();
        }

        public void EnterWaitingState()
        {
            SetState(new CellGridStateWaitingForInput(this));
        }

        public void EnterBlockedInputState()
        {
            SetState(new CellGridStateBlockInput(this));
        }

        internal void EnterSceneOnlyBlockedInputState()
        {
            SetState(new CellGridStateBlockInput(this));
        }

        public void EnterRemotePlayerTurnState()
        {
            SetState(new CellGridStateRemotePlayerTurn(this));
        }

        public void EnterAiTurnState(AiPlayer aiPlayer)
        {
            SetState(new CellGridStateAiTurn(this, aiPlayer));
        }

        public void EnterSelectedState(Unit unit)
        {
            if (unit == null)
            {
                EnterWaitingState();
                return;
            }

            SetState(new UnitSelectedState(this, unit, unit.GetBattleActions()));

            RuntimeParityDiagnostics.CompareMovementReach(unit, this, "human-select");
        }

        public void EnterPendingMoveConfirmState(MoveAbility moveAbility)
        {
            if (moveAbility == null)
            {
                EnterWaitingState();
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

            ExecuteSceneEndTurn();
        }

        public bool RequestBattleOutcomeEvaluation()
        {
            bool finished = CheckGameFinished();
            if (finished)
            {
                SyncStateToGameOver();
            }

            return finished;
        }

        internal void PreparePendingCombatPresentation()
        {
            NotifyCombatPresentationBegan();
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
            CommitPendingMoveOnSceneUnit(unit, consumeAllRemainingMovement);
        }

        public void EnterPostCombatGridState()
        {
            if (GameFinished)
            {
                SyncStateToGameOver();
                return;
            }

            EnterWaitingState();
        }

        internal void NotifyCombatPresentationBegan()
        {
            if (GameFinished)
            {
                return;
            }

            SyncRuntimeMirrorNow();
            if (IsHumanTurn && CurrentState is not CellGridStateBlockInput)
            {
                EnterSceneOnlyBlockedInputState();
            }
        }

        internal void NotifyCombatPresentationEnded()
        {
            if (GameFinished)
            {
                return;
            }

            SyncRuntimeMirrorNow();
            RefreshSceneCellOccupancyNow();
            TryFlushDeferredDestroyQueue();
            RequestBattleOutcomeEvaluation();
        }

        internal void SyncStateToGameOver()
        {
            if (!GameFinished)
            {
                return;
            }

            currentState = new CellGridStateGameOver(this);
        }

        private SceneUnitGenerator GetSceneUnitGenerator()
        {
            if (sceneUnitGenerator == null)
            {
                sceneUnitGenerator = GetComponent<SceneUnitGenerator>();
            }

            return sceneUnitGenerator;
        }

        // --- Deferred unit destroy queue ---
        private readonly List<Unit> deferredDestroyQueue = new List<Unit>();

        public void EnqueueDeferredDestroy(Unit unit)
        {
            if (unit == null || deferredDestroyQueue.Contains(unit))
            {
                return;
            }

            deferredDestroyQueue.Add(unit);
        }

        public void TryFlushDeferredDestroyQueue()
        {
            if (Unit.IsAnyCombatPresentationActive)
            {
                return;
            }

            FlushDeferredDestroyQueue();
        }

        private void FlushDeferredDestroyQueue()
        {
            for (int i = deferredDestroyQueue.Count - 1; i >= 0; i--)
            {
                Unit unit = deferredDestroyQueue[i];
                deferredDestroyQueue.RemoveAt(i);
                unit?.CompleteDeferredDestroy();
            }
        }

        private void ProcessDeferredDestroyQueue()
        {
            TryFlushDeferredDestroyQueue();
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
