using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Actions;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Players;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CellGrid
    {
        private readonly List<BattleSquareCell> sceneCells = new List<BattleSquareCell>();
        private readonly List<Unit> registeredUnits = new List<Unit>();
        private readonly List<Player> scenePlayers = new List<Player>();
        private readonly List<IBattleTurnPlayer> runtimeTurnPlayers = new List<IBattleTurnPlayer>();
        private readonly HashSet<BattleSquareCell> wiredSceneCells = new HashSet<BattleSquareCell>();

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

        private List<BattleSquareCell> Cells => SceneCells;
        private List<Unit> Units => SceneRegisteredUnits;
        private List<Player> Players => ScenePlayers;
        private List<IBattleTurnPlayer> RuntimePlayers => SceneRuntimeTurnPlayers;
        private List<BattleSquareCell> SceneCells => sceneCells;
        private List<Unit> SceneRegisteredUnits => registeredUnits;
        private List<Player> ScenePlayers => scenePlayers;
        private List<IBattleTurnPlayer> SceneRuntimeTurnPlayers => runtimeTurnPlayers;

        private void EnsureLegacyGridHost()
        {
        }

        private void WireLegacyGridEvents()
        {
            SceneGameStarted += OnGameStarted;
            SceneGameEnded += OnSceneGameEnded;
            SceneLevelLoadingDone += OnLevelLoadingDoneInternal;
            SceneTurnEnded += OnTurnEnded;
            SceneUnitAdded += OnUnitAdded;
        }

        private void UnwireLegacyGridEvents()
        {
            SceneGameStarted -= OnGameStarted;
            SceneGameEnded -= OnSceneGameEnded;
            SceneLevelLoadingDone -= OnLevelLoadingDoneInternal;
            SceneTurnEnded -= OnTurnEnded;
            SceneUnitAdded -= OnUnitAdded;
        }

        internal void PrepareLegacyGridBeforeInitialize()
        {
            EnsureSceneCellAnchors();
        }

        public void InitializeBattleScene()
        {
            EnsureSceneCellAnchors();
            InitializeSceneRegistry();
        }

        public void StartLegacyBattle()
        {
            IBattleTurnResolver turnResolver = GetComponent<IBattleTurnResolver>();
            RoundRobinTurnPlan plan = turnResolver != null
                ? turnResolver.ResolveStart(this)
                : new RoundRobinTurnPlan(null, Array.Empty<IBoardUnit>());
            SyncBattleStartFromPlan(plan, kickPlayerPlay: true);
        }

        public void EndTurn(bool isNetworkInvoked = false) => ExecuteSceneEndTurn(isNetworkInvoked);

        internal void InitializeSceneRegistry()
        {
            SceneLevelLoading?.Invoke(this, EventArgs.Empty);

            gameFinished = false;
            allocatedUnitId = 0;
            scenePlayers.Clear();
            runtimeTurnPlayers.Clear();
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
                        runtimePlayer.InitializeBoard(this);
                        runtimeTurnPlayers.Add(runtimePlayer);
                    }

                    Player customPlayer = playerTransform.GetComponent<Player>();
                    if (customPlayer != null && !scenePlayers.Contains(customPlayer))
                    {
                        scenePlayers.Add(customPlayer);
                    }
                }
            }

            foreach (BattleSquareCell cell in GetComponentsInChildren<BattleSquareCell>(true))
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

        internal void PrepareRuntimeTurnStartForPlan(RoundRobinTurnPlan plan)
        {
            if (plan.PlayableUnits == null)
            {
                return;
            }

            for (int i = 0; i < plan.PlayableUnits.Count; i++)
            {
                ResolveUnitFromBoardUnit(plan.PlayableUnits[i])?.PrepareRuntimeForTurnStart();
            }
        }

        internal void ApplyLegacyTurnEndToCurrentPlayerUnits()
        {
            List<Unit> playableUnits = GetCurrentPlayerUnits();
            for (int i = 0; i < playableUnits.Count; i++)
            {
                Unit unit = playableUnits[i];
                if (unit == null)
                {
                    continue;
                }

                unit.OnTurnEnd();
                NotifyBattleActionsTurnEnded(unit);
            }
        }

        internal void ApplyRuntimeTurnStartToLegacyPlayableUnits()
        {
            List<Unit> playableUnits = GetCurrentPlayerUnits();
            for (int i = 0; i < playableUnits.Count; i++)
            {
                Unit customUnit = playableUnits[i];
                if (customUnit == null)
                {
                    continue;
                }

                NotifyBattleActionsTurnStarted(customUnit);
                customUnit.ApplyLegacyTurnStartFromRuntime();
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
                : new BattleOutcome(false, null, null);
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

            if (TryRouteEndTurnThroughRuntime())
            {
                return;
            }

            EnterBlockedInputState();
            if (CheckGameFinished())
            {
                return;
            }

            EndUnitsForCurrentPlayerTurn();
            IBattleTurnResolver turnResolver = GetComponent<IBattleTurnResolver>();
            RoundRobinTurnPlan plan = turnResolver != null
                ? turnResolver.ResolveTurn(this)
                : new RoundRobinTurnPlan(null, Array.Empty<IBoardUnit>());
            CommitTurnTransition(plan, isNetworkInvoked);
        }

        internal void HandleSceneCellClicked(BattleSquareCell cell)
        {
            if (cell == null)
            {
                return;
            }

            TryDispatchCellClicked(cell);
        }

        internal void HandleSceneCellSelected(BattleSquareCell cell)
        {
            if (cell == null)
            {
                return;
            }

            TryDispatchCellSelected(cell);
        }

        internal void HandleSceneCellDeselected(BattleSquareCell cell)
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

        private void WireSceneCellInput(BattleSquareCell cell)
        {
            if (cell == null || !wiredSceneCells.Add(cell))
            {
                return;
            }

            cell.Clicked += OnSceneCellClicked;
            cell.Hovered += OnSceneCellHovered;
            cell.Unhovered += OnSceneCellUnhovered;
        }

        private void UnwireSceneCellInput(BattleSquareCell cell)
        {
            if (cell == null || !wiredSceneCells.Remove(cell))
            {
                return;
            }

            cell.Clicked -= OnSceneCellClicked;
            cell.Hovered -= OnSceneCellHovered;
            cell.Unhovered -= OnSceneCellUnhovered;
        }

        private void OnSceneCellClicked(BoardCell cell)
        {
            if (cell is BattleSquareCell battleCell)
            {
                HandleSceneCellClicked(battleCell);
            }
        }

        private void OnSceneCellHovered(BoardCell cell)
        {
            if (cell is BattleSquareCell battleCell)
            {
                HandleSceneCellSelected(battleCell);
            }
        }

        private void OnSceneCellUnhovered(BoardCell cell)
        {
            if (cell is BattleSquareCell battleCell)
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
            IBattleTurnPlayer runtimePlayer = SceneRuntimeTurnPlayers.Find(player => player != null && player.PlayerId == currentPlayerNumber);
            if (runtimePlayer != null)
            {
                runtimePlayer.PlayTurn(this);
                return;
            }

            Player legacyStylePlayer = ScenePlayers.Find(player => player != null && player.PlayerNumber == currentPlayerNumber);
            legacyStylePlayer?.PlayTurn(this);
        }

        private void NotifyBattleActions(Unit unit, Action<IBattleAction> notify)
        {
            if (unit == null || notify == null)
            {
                return;
            }

            List<IBattleAction> actions = unit.GetBattleActions();
            for (int i = 0; i < actions.Count; i++)
            {
                notify(actions[i]);
            }
        }

        private static Func<List<Unit>> CreatePlayableUnitsAccessor(RoundRobinTurnPlan plan)
        {
            return () => plan.PlayableUnits?
                .Select(unit => unit as Unit ?? (unit as Component)?.GetComponent<Unit>())
                .Where(unit => unit != null)
                .ToList() ?? new List<Unit>();
        }

        private static Unit ResolveUnitFromBoardUnit(IBoardUnit unit)
        {
            if (unit is Unit customUnit)
            {
                return customUnit;
            }

            return unit is Component component
                ? component.GetComponent<Unit>()
                : null;
        }

        private static string FormatPlayerIdList(IReadOnlyList<int> playerIds)
        {
            return playerIds == null || playerIds.Count == 0
                ? "<none>"
                : string.Join(", ", playerIds);
        }
    }
}
