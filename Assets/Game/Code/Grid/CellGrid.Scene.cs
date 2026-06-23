using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Players;

namespace Windy.Srpg.Game.Grid
{
    public partial class CellGrid
    {
        // --- Unity lifecycle and scene input dispatch ---
        public void InitializeBattle() => InitializeBattleScene();

        public void RequestFrameworkInitializeAndStart()
        {
            InitializeBattleScene();
            StartBattle();
        }

        public void RequestFrameworkBattleStart()
        {
            StartBattle();
        }

        private void Update()
        {
            ProcessDeferredDestroyQueue();
        }

        private void Awake()
        {
            EnsureSceneCellAnchors();
            PrepareFriendlyDeploymentFromSave();
            WireSceneGridEvents();
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
            UnwireSceneGridEvents();

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
                    cell.Hovered -= OnCellHighlightedInternal;
                }
            }

            subscribedCells.Clear();
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

        private void StartBattle()
        {
            RoundRobinTurnPlan plan = RoundRobinBattleFlow.ResolveStart(this);
            if (plan.NextPlayer == null)
            {
                Debug.LogError("CellGrid: No valid battle turn resolver or next player was found.");
                return;
            }

            SyncBattleStartFromPlan(plan, kickPlayerPlay: true, syncUnitTurnHooks: true);
            Debug.Log("Game started via scene grid");
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
            UnitAdded?.Invoke(this, new UnitAddedEventArgs(customUnit));
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
            foreach (Cell cell in GetAllCells())
            {
                if (cell == null || !subscribedCells.Add(cell))
                {
                    continue;
                }

                cell.Hovered += OnCellHighlightedInternal;
            }
        }

        private void OnCellHighlightedInternal(Cell cell)
        {
            if (cell is not Cell battleCell)
            {
                return;
            }

            if (battleCell.CurrentUnits != null
                && battleCell.CurrentUnits.Any(unit => unit != null && !unit.ExcludedFromBattle))
            {
                return;
            }

            EmptyCellHighlighted?.Invoke(this, EventArgs.Empty);
        }

        internal bool TryDispatchCellDeselected(Cell cell)
        {
            return cell != null && TryDispatchToCurrentState(state => state.OnCellDeselected(cell));
        }

        internal bool TryDispatchCellSelected(Cell cell)
        {
            return cell != null && TryDispatchToCurrentState(state => state.OnCellSelected(cell));
        }

        internal bool TryDispatchCellClicked(Cell cell)
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

        // --- Scene registry and occupancy ---
        internal static Unit ResolveUnitFromRegistryUnit(object unit)
        {
            return unit as Unit;
        }

        public void RefreshSceneCellOccupancyNow()
        {
            RebuildSceneCellOccupancy();
        }

        internal void RegisterSceneUnitTransform(
            Transform unitTransform,
            Cell targetCell = null,
            Player ownerPlayer = null)
        {
            Unit customUnit = unitTransform != null ? unitTransform.GetComponent<Unit>() : null;
            if (customUnit == null)
            {
                Debug.LogError("CellGrid: RegisterSceneUnitTransform requires a Unit component.");
                return;
            }

            if (IsUnitRegistered(customUnit))
            {
                customUnit.EnsureSceneCellBinding();
                return;
            }

            int assignedUnitId = AllocateNextUnitId();
            customUnit.UnitID = assignedUnitId;
            registeredUnits.Add(customUnit);

            if (targetCell != null)
            {
                customUnit.Cell = targetCell;
                customUnit.transform.localPosition = targetCell.transform.localPosition;
            }

            if (ownerPlayer != null)
            {
                customUnit.PlayerNumber = ownerPlayer.PlayerNumber;
            }

            customUnit.RegisterCellOccupancyList(targetCell ?? customUnit.Cell);
            customUnit.transform.localRotation = Quaternion.Euler(0, 0, 0);
            customUnit.Initialize();
            customUnit.EnsureSceneCellBinding();

            customUnit.UnitClicked += OnSceneUnitClicked;
            customUnit.UnitHighlighted += OnSceneUnitHighlighted;
            customUnit.UnitDehighlighted += OnSceneUnitDehighlighted;
            customUnit.UnitDestroyed += OnSceneUnitDestroyed;

            NotifySceneUnitAdded(unitTransform);
        }

        protected void RegisterSceneUnit(Unit unit, Cell targetCell = null, Player ownerPlayer = null)
        {
            if (unit == null || IsUnitRegistered(unit))
            {
                return;
            }

            RegisterSceneUnitTransform(unit.transform, targetCell, ownerPlayer);
        }

        protected bool IsUnitRegistered(Unit unit)
        {
            return unit != null && registeredUnits.Contains(unit);
        }

        protected void UnregisterSceneUnit(Unit unit)
        {
            if (unit == null || !registeredUnits.Remove(unit))
            {
                return;
            }

            unit.UnitClicked -= OnSceneUnitClicked;
            unit.UnitHighlighted -= OnSceneUnitHighlighted;
            unit.UnitDehighlighted -= OnSceneUnitDehighlighted;
            unit.UnitDestroyed -= OnSceneUnitDestroyed;
        }

        private void EnsureSceneCellAnchors()
        {
            foreach (Cell tile in GetComponentsInChildren<Cell>(true))
            {
                if (tile == null)
                {
                    continue;
                }
            }

            EnsureDeploymentSlotCellBindings();
        }

        private void EnsureDeploymentSlotCellBindings()
        {
            Cell[] tiles = GetComponentsInChildren<Cell>(true);
            foreach (DeploymentSlot slot in GetDeploymentSlots())
            {
                slot?.EnsureRegistryCellBinding(tiles);
            }
        }

        internal Cell ResolveCanonicalCell(Cell cell)
        {
            if (cell == null)
            {
                return null;
            }

            Cell match = FindCellByOffset(cell.OffsetCoord);
            return match ?? cell;
        }

        internal void NotifyOccupancyChanged()
        {
            occupancyRevision++;
        }

        internal IEnumerable<Unit> GetOccupancyTrackedUnits()
        {
            HashSet<Unit> trackedUnits = new HashSet<Unit>();
            foreach (Unit unit in GetAllUnits())
            {
                if (unit != null && !unit.ExcludedFromBattle)
                {
                    trackedUnits.Add(unit);
                }
            }

            foreach (Unit unit in GetAllSceneUnitsFromHierarchy())
            {
                if (unit != null && !unit.ExcludedFromBattle)
                {
                    trackedUnits.Add(unit);
                }
            }

            return trackedUnits;
        }

        private void RebuildSceneCellOccupancy()
        {
            List<Cell> allCells = GetAllCells();
            if (allCells.Count == 0)
            {
                return;
            }

            foreach (Cell cell in allCells)
            {
                if (cell == null)
                {
                    continue;
                }

                cell.ClearCurrentUnits();
            }

            foreach (Unit unit in GetOccupancyTrackedUnits())
            {
                unit.EnsureSceneCellBinding(notifyGrid: false);
            }

            NotifyOccupancyChanged();
        }

        private static bool IsCellBlockedByTerrain(Cell cell)
        {
            return cell != null && !cell.IsTraversable;
        }
        // --- Scene battle loop and turn sync ---
        private readonly List<Cell> sceneCells = new List<Cell>();
        private readonly List<Unit> registeredUnits = new List<Unit>();
        private readonly List<Player> scenePlayers = new List<Player>();
        private readonly List<IBattleTurnPlayer> sceneTurnPlayers = new List<IBattleTurnPlayer>();
        private readonly HashSet<Cell> wiredSceneCells = new HashSet<Cell>();

        private int allocatedUnitId;
        private int currentPlayerNumber;
        private bool gameFinished;
        private Func<List<Unit>> playableUnitsAccessor = () => new List<Unit>();

        public event EventHandler LevelLoading
        {
            add => SceneLevelLoading += value;
            remove => SceneLevelLoading -= value;
        }

        public event EventHandler SceneLevelLoading;
        public event EventHandler SceneLevelLoadingDone;
        public event EventHandler SceneGameStarted;
        public event EventHandler<BattleEndedEventArgs> SceneGameEnded;
        public event EventHandler<bool> SceneTurnEnded;
        public event EventHandler<UnitCreatedEventArgs> SceneUnitAdded;

        public int SceneCurrentPlayerNumber => currentPlayerNumber;
        public bool SceneGameFinished => gameFinished;
        public bool GameFinished => SceneGameFinished;

        private List<Cell> Cells => SceneCells;
        private List<Unit> Units => SceneRegisteredUnits;
        private List<Player> Players => ScenePlayers;
        private List<Cell> SceneCells => sceneCells;
        private List<Unit> SceneRegisteredUnits => registeredUnits;
        private List<Player> ScenePlayers => scenePlayers;
        private List<IBattleTurnPlayer> SceneTurnPlayers => sceneTurnPlayers;

        private void WireSceneGridEvents()
        {
            SceneGameStarted += OnGameStarted;
            SceneGameEnded += OnSceneGameEnded;
            SceneLevelLoadingDone += OnLevelLoadingDoneInternal;
            SceneTurnEnded += OnTurnEnded;
            SceneUnitAdded += OnUnitAdded;
        }

        private void UnwireSceneGridEvents()
        {
            SceneGameStarted -= OnGameStarted;
            SceneGameEnded -= OnSceneGameEnded;
            SceneLevelLoadingDone -= OnLevelLoadingDoneInternal;
            SceneTurnEnded -= OnTurnEnded;
            SceneUnitAdded -= OnUnitAdded;
        }


        public void InitializeBattleScene()
        {
            EnsureSceneCellAnchors();
            InitializeSceneRegistry();
        }

        public void EndTurn(bool isNetworkInvoked = false) => ExecuteSceneEndTurn(isNetworkInvoked);

        internal void InitializeSceneRegistry()
        {
            SceneLevelLoading?.Invoke(this, EventArgs.Empty);

            gameFinished = false;
            allocatedUnitId = 0;
            scenePlayers.Clear();
            sceneTurnPlayers.Clear();
            sceneCells.Clear();
            registeredUnits.Clear();

            if (PlayersParent != null)
            {
                for (int i = 0; i < PlayersParent.childCount; i++)
                {
                    Transform playerTransform = PlayersParent.GetChild(i);
                    if (playerTransform == null || !playerTransform.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    IBattleTurnPlayer runtimePlayer = playerTransform
                        .GetComponents<MonoBehaviour>()
                        .OfType<IBattleTurnPlayer>()
                        .FirstOrDefault();
                    if (runtimePlayer != null)
                    {
                        runtimePlayer.BindToGrid(this);
                        sceneTurnPlayers.Add(runtimePlayer);
                    }

                    Player customPlayer = playerTransform.GetComponent<Player>();
                    if (customPlayer != null && !scenePlayers.Contains(customPlayer))
                    {
                        scenePlayers.Add(customPlayer);
                    }
                }
            }

            foreach (Cell cell in GetComponentsInChildren<Cell>(true))
            {
                if (cell == null || !cell.gameObject.activeInHierarchy)
                {
                    continue;
                }

                sceneCells.Add(cell);
                WireSceneCellInput(cell);
            }

            IBattleSceneUnitSource unitSource = this as IBattleSceneUnitSource ?? GetComponent<IBattleSceneUnitSource>();
            if (unitSource != null)
            {
                IReadOnlyList<Transform> unitTransforms = unitSource.GetInitialUnitTransforms(this) ?? Array.Empty<Transform>();
                foreach (Transform unitTransform in unitTransforms)
                {
                    if (unitTransform != null)
                    {
                        RegisterSceneUnitTransform(unitTransform);
                    }
                }
            }
            else
            {
                Debug.LogError("CellGrid: No battle scene unit source script attached to cell grid.");
            }

            SceneLevelLoadingDone?.Invoke(this, EventArgs.Empty);
        }

        internal int AllocateNextUnitId() => allocatedUnitId++;

        internal void NotifySceneUnitAdded(Transform unitTransform)
        {
            SceneUnitAdded?.Invoke(this, new UnitCreatedEventArgs(unitTransform));
        }

        internal void SyncBattleStartFromPlan(
            RoundRobinTurnPlan plan,
            bool kickPlayerPlay = true,
            bool syncUnitTurnHooks = true)
        {
            playableUnitsAccessor = CreatePlayableUnitsAccessor(plan);
            if (plan.NextPlayer == null)
            {
                Debug.LogError("CellGrid: No valid battle turn resolver or next player was found.");
                return;
            }

            currentPlayerNumber = plan.NextPlayer.PlayerId;
            SceneGameStarted?.Invoke(this, EventArgs.Empty);
            Debug.Log($"[BattleFlow] Battle started. First player: {currentPlayerNumber}.");

            if (syncUnitTurnHooks)
            {
                foreach (Unit unit in playableUnitsAccessor())
                {
                    if (unit == null)
                    {
                        continue;
                    }

                    NotifyBattleActionsTurnStarted(unit);
                    unit.OnTurnStart();
                }
            }

            if (!kickPlayerPlay)
            {
                return;
            }

            KickCurrentScenePlayer();
        }

        internal void CommitTurnTransition(
            RoundRobinTurnPlan plan,
            bool isNetworkInvoked = false,
            bool kickPlayerPlay = true,
            bool syncUnitTurnHooks = true)
        {
            playableUnitsAccessor = CreatePlayableUnitsAccessor(plan);
            if (plan.NextPlayer == null)
            {
                Debug.LogError("CellGrid: No valid battle turn resolver or next player was found.");
                return;
            }

            currentPlayerNumber = plan.NextPlayer.PlayerId;
            SceneTurnEnded?.Invoke(this, isNetworkInvoked);
            Debug.Log($"[BattleFlow] Turn advanced. Current player: {currentPlayerNumber}.");

            if (syncUnitTurnHooks)
            {
                foreach (Unit unit in playableUnitsAccessor())
                {
                    if (unit == null)
                    {
                        continue;
                    }

                    NotifyBattleActionsTurnStarted(unit);
                    unit.OnTurnStart();
                }
            }

            if (!kickPlayerPlay)
            {
                return;
            }

            KickCurrentScenePlayer();
        }

        internal void EndUnitsForCurrentPlayerTurn()
        {
            foreach (Unit unit in playableUnitsAccessor())
            {
                if (unit == null)
                {
                    continue;
                }

                unit.OnTurnEnd();
                NotifyBattleActionsTurnEnded(unit);
            }
        }

        internal void NotifyBattleActionsTurnStarted(Unit unit)
        {
            NotifyBattleActions(unit, action => action.OnTurnStarted(this));
        }

        internal void NotifyBattleActionsTurnEnded(Unit unit)
        {
            NotifyBattleActions(unit, action => action.OnTurnEnded(this));
        }

        internal void NotifyBattleActionsOwnerDestroyed(Unit unit)
        {
            NotifyBattleActions(unit, action => action.OnOwnerDestroyed(this));
        }

        internal bool CheckGameFinished()
        {
            IBattleEndCondition endCondition = GetComponent<IBattleEndCondition>();
            BattleOutcome outcome = endCondition != null
                ? endCondition.Evaluate(this)
                : RoundRobinBattleFlow.EvaluateLastSideStanding(this);
            return TryApplyBattleOutcome(outcome);
        }

        internal bool TryApplyBattleOutcome(BattleOutcome outcome)
        {
            if (gameFinished)
            {
                return true;
            }

            if (!outcome.IsFinished)
            {
                return false;
            }

            gameFinished = true;
            IReadOnlyList<int> winningPlayers = outcome.WinningPlayerIds ?? Array.Empty<int>();
            IReadOnlyList<int> losingPlayers = outcome.DefeatedPlayerIds ?? Array.Empty<int>();
            Debug.Log(
                $"[BattleFlow] Victory condition triggered. Winners: {FormatPlayerIdList(winningPlayers)}. Defeated: {FormatPlayerIdList(losingPlayers)}.");
            SceneGameEnded?.Invoke(this, new BattleEndedEventArgs(winningPlayers, losingPlayers));
            return true;
        }

        public void ExecuteSceneEndTurn(bool isNetworkInvoked = false)
        {
            if (CurrentState?.BlocksEndTurn == true)
            {
                return;
            }

            EnterBlockedInputState();
            if (CheckGameFinished())
            {
                return;
            }

            EndUnitsForCurrentPlayerTurn();
            RoundRobinTurnPlan plan = RoundRobinBattleFlow.ResolveTurn(this);
            if (plan.NextPlayer == null)
            {
                Debug.LogError("CellGrid: No valid battle turn resolver or next player was found.");
                return;
            }

            CommitTurnTransition(plan, isNetworkInvoked);
        }

        internal void HandleSceneCellClicked(Cell cell)
        {
            if (cell == null)
            {
                return;
            }

            TryDispatchCellClicked(cell);
        }

        internal void HandleSceneCellSelected(Cell cell)
        {
            if (cell == null)
            {
                return;
            }

            TryDispatchCellSelected(cell);
        }

        internal void HandleSceneCellDeselected(Cell cell)
        {
            if (cell == null)
            {
                return;
            }

            TryDispatchCellDeselected(cell);
        }

        internal void HandleSceneUnitClicked(Unit unit)
        {
            if (unit == null)
            {
                return;
            }

            TryDispatchUnitClicked(unit);
        }

        internal void HandleSceneUnitHighlighted(Unit unit)
        {
            if (unit == null)
            {
                return;
            }

            TryDispatchUnitHighlighted(unit);
        }

        internal void HandleSceneUnitDehighlighted(Unit unit)
        {
            if (unit == null)
            {
                return;
            }

            TryDispatchUnitDehighlighted(unit);
        }

        internal void HandleSceneUnitDestroyed(Unit unit, AttackEventArgs args)
        {
            if (unit == null)
            {
                return;
            }

            UnregisterSceneUnit(unit);
            unit.UnitClicked -= OnSceneUnitClicked;
            unit.UnitHighlighted -= OnSceneUnitHighlighted;
            unit.UnitDehighlighted -= OnSceneUnitDehighlighted;
            unit.UnitDestroyed -= OnSceneUnitDestroyed;
            NotifyBattleActionsOwnerDestroyed(unit);
            CheckGameFinished();
        }

        private void WireSceneCellInput(Cell cell)
        {
            if (cell == null || !wiredSceneCells.Add(cell))
            {
                return;
            }

            cell.Clicked += OnSceneCellClicked;
            cell.Hovered += OnSceneCellHovered;
            cell.Unhovered += OnSceneCellUnhovered;
        }

        private void UnwireSceneCellInput(Cell cell)
        {
            if (cell == null || !wiredSceneCells.Remove(cell))
            {
                return;
            }

            cell.Clicked -= OnSceneCellClicked;
            cell.Hovered -= OnSceneCellHovered;
            cell.Unhovered -= OnSceneCellUnhovered;
        }

        private void OnSceneCellClicked(Cell cell)
        {
            if (cell is Cell battleCell)
            {
                HandleSceneCellClicked(battleCell);
            }
        }

        private void OnSceneCellHovered(Cell cell)
        {
            if (cell is Cell battleCell)
            {
                HandleSceneCellSelected(battleCell);
            }
        }

        private void OnSceneCellUnhovered(Cell cell)
        {
            if (cell is Cell battleCell)
            {
                HandleSceneCellDeselected(battleCell);
            }
        }

        private void OnSceneUnitClicked(object sender, EventArgs e)
        {
            if (sender is Unit unit)
            {
                HandleSceneUnitClicked(unit);
            }
        }

        private void OnSceneUnitHighlighted(object sender, EventArgs e)
        {
            if (sender is Unit unit)
            {
                HandleSceneUnitHighlighted(unit);
            }
        }

        private void OnSceneUnitDehighlighted(object sender, EventArgs e)
        {
            if (sender is Unit unit)
            {
                HandleSceneUnitDehighlighted(unit);
            }
        }

        private void OnSceneUnitDestroyed(object sender, AttackEventArgs e)
        {
            if (sender is Unit unit)
            {
                HandleSceneUnitDestroyed(unit, e);
            }
        }

        private void OnSceneGameEnded(object sender, BattleEndedEventArgs e)
        {
            BattleEnded?.Invoke(this, e);
        }

        private void KickCurrentScenePlayer()
        {
            IBattleTurnPlayer turnPlayer = SceneTurnPlayers.Find(player => player != null && player.PlayerId == currentPlayerNumber);
            if (turnPlayer != null)
            {
                turnPlayer.PlayTurn(this);
                return;
            }

            Player scenePlayer = ScenePlayers.Find(player => player != null && player.PlayerNumber == currentPlayerNumber);
            scenePlayer?.PlayTurn(this);
        }

        private void NotifyBattleActions(Unit unit, Action<BattleAction> notify)
        {
            if (unit == null || notify == null)
            {
                return;
            }

            List<BattleAction> actions = unit.GetBattleActions();
            for (int i = 0; i < actions.Count; i++)
            {
                notify(actions[i]);
            }
        }

        private static Func<List<Unit>> CreatePlayableUnitsAccessor(RoundRobinTurnPlan plan)
        {
            return () => plan.PlayableUnits?
                .Where(unit => unit != null)
                .ToList() ?? new List<Unit>();
        }

        private static string FormatPlayerIdList(IReadOnlyList<int> playerIds)
        {
            return playerIds == null || playerIds.Count == 0
                ? "<none>"
                : string.Join(", ", playerIds);
        }
    }
}
