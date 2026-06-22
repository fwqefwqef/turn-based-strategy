using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Runtime.Grid.States;
using Windy.Srpg.Runtime.Players;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Grid
{
    /// <summary>
    /// Pure battle runtime host.
    /// Tracks the mirrored cells/units/players collection, owns the runtime turn/state machine,
    /// and exposes the authoritative runtime-side input hooks used by the scene host.
    /// </summary>
    public class RuntimeGrid : MonoBehaviour, IGridContext
    {
        [SerializeField] private List<Cell> cells = new List<Cell>();
        [SerializeField] private List<GridUnit> units = new List<GridUnit>();
        [SerializeField] private bool autoCollectSceneChildren = true;
        [SerializeField] private bool startBattleOnStart;
        [SerializeField] private bool sceneInputEnabled;

        private readonly List<IBattlePlayer> players = new List<IBattlePlayer>();
        private RuntimeGridState currentState;
        private int currentPlayerIndex;
        private bool battleStarted;

        public event Action<RuntimeGridState> StateChanged;
        public event Action<IBattlePlayer> TurnStarted;
        public event Action<IBattlePlayer> TurnEnded;
        public event Action AiTurnRequested;
        public event Action<RuntimeGrid> BattleStarted;
        public event Action<GridUnit> UnitRegistered;
        public event Action<GridUnit> UnitUnregistered;

        public IReadOnlyList<Cell> Cells => cells;
        public IReadOnlyList<GridUnit> Units => units;
        public IReadOnlyList<IBattlePlayer> Players => players;
        IReadOnlyList<IGridUnit> IGridContext.Units => units;
        IReadOnlyList<IBattlePlayer> IGridContext.Players => players;
        public RuntimeGridState CurrentState => currentState;
        public IBattlePlayer CurrentPlayer => players.Count == 0 ? null : players[Mathf.Clamp(currentPlayerIndex, 0, players.Count - 1)];
        public int CurrentPlayerId => CurrentPlayer?.PlayerId ?? -1;
        public bool BattleIsStarted => battleStarted;
        public bool SceneInputEnabled => sceneInputEnabled;

        /// <summary>
        /// Optional scene-input coordinator. When present, scene clicks/hover are routed here
        /// first so the scene host can translate Unity objects into runtime-state transitions.
        /// </summary>
        public IRuntimeGridSceneInputCoordinator SceneInputCoordinator { get; set; }

        protected virtual void Awake()
        {
            RefreshSceneCollections();
            WireCellEvents();
            WireUnitEvents();
        }

        protected virtual void Start()
        {
            if (startBattleOnStart && sceneInputEnabled)
            {
                StartBattle();
            }
        }

        protected virtual void OnValidate()
        {
            if (autoCollectSceneChildren)
            {
                RefreshSceneCollections();
            }
        }

        public virtual void SetSceneInputEnabled(bool enabled)
        {
            sceneInputEnabled = enabled;
        }

        public virtual void StartBattle()
        {
            RoundRobinTurnPlan plan = RoundRobinBattleFlow.ResolveStart(this);
            BeginBattleFromHost(plan, kickFirstTurn: true, enableSceneInput: true, refreshSceneCollections: true);
        }

        /// <summary>
        /// Host-driven battle start. <see cref="CellGrid"/> typically prepares the mirrored
        /// collections first, then calls here to let runtime own round-robin flow.
        /// </summary>
        public virtual void BeginBattleFromHost(
            RoundRobinTurnPlan startPlan,
            bool kickFirstTurn = true,
            bool enableSceneInput = false,
            bool refreshSceneCollections = false)
        {
            if (refreshSceneCollections)
            {
                RefreshSceneCollections();
            }
            else
            {
                WireCellEvents();
                WireUnitEvents();
            }

            battleStarted = true;
            if (enableSceneInput)
            {
                sceneInputEnabled = true;
            }

            foreach (var player in players.OfType<IBattleTurnPlayer>())
            {
                player.BindToGrid(this);
            }

            SetCurrentPlayerById(startPlan.NextPlayer?.PlayerId ?? -1);
            BattleStarted?.Invoke(this);
            BeginCurrentTurn(kickFirstTurn);
        }

        public virtual void SetState(RuntimeGridState nextState)
        {
            if (nextState == null)
            {
                return;
            }

            currentState?.OnStateExit();
            currentState = nextState;
            currentState.OnStateEnter();
            StateChanged?.Invoke(currentState);
        }

        public virtual IEnumerable<GridUnit> GetUnitsOwnedBy(int playerId)
        {
            return GridQueries.GetUnitsForPlayer(units, playerId);
        }

        public virtual IEnumerable<GridUnit> GetCurrentPlayerUnits()
        {
            return CurrentPlayer == null
                ? Array.Empty<GridUnit>()
                : GridQueries.GetCurrentPlayerUnits(units, CurrentPlayer.PlayerId);
        }

        public virtual IEnumerable<GridUnit> GetEnemyUnits(IBattlePlayer player)
        {
            if (player == null)
            {
                return Array.Empty<GridUnit>();
            }

            return GridQueries.GetEnemyUnits(units, player.PlayerId);
        }

        public virtual IEnumerable<GridUnit> GetEnemyUnits(int playerId)
        {
            return GridQueries.GetEnemyUnits(units, playerId);
        }

        public virtual IBattlePlayer GetPlayerById(int playerId)
        {
            return GridQueries.GetPlayerById(players, playerId);
        }

        public virtual void SetCurrentPlayerById(int playerId)
        {
            int index = players.FindIndex(player => player != null && player.PlayerId == playerId);
            if (index >= 0)
            {
                currentPlayerIndex = index;
            }
        }

        public virtual void SetBattleStarted(bool started)
        {
            battleStarted = started;
        }

        public virtual void EndCurrentTurn(bool kickTurnPlayerPlay = true)
        {
            Debug.Log($"[BattleFlow] Ending player {CurrentPlayerId} turn.");

            foreach (var unit in GetCurrentPlayerUnits())
            {
                unit?.EndTurn();
            }

            var player = CurrentPlayer;
            if (player != null)
            {
                TurnEnded?.Invoke(player);
            }

            if (players.Count == 0)
            {
                return;
            }

            RoundRobinTurnPlan plan = RoundRobinBattleFlow.ResolveTurn(this);
            SetCurrentPlayerById(plan.NextPlayer?.PlayerId ?? -1);
            BeginCurrentTurn(kickTurnPlayerPlay);
        }

        public virtual BattleOutcome EvaluateBattleOutcome()
        {
            return RoundRobinBattleFlow.EvaluateLastSideStanding(this);
        }

        public virtual void RequestAiTurn()
        {
            AiTurnRequested?.Invoke();
        }

        /// <summary>
        /// Starts the active player's turn execution after legacy grid sync has caught up.
        /// </summary>
        public virtual void KickCurrentTurnPlay()
        {
            var player = CurrentPlayer;
            if (player == null)
            {
                return;
            }

            if (!player.IsHumanControlled)
            {
                RequestAiTurn();
            }

            if (player is IBattleTurnPlayer turnPlayer)
            {
                turnPlayer.PlayTurn(this);
            }
        }

        public virtual void ProcessUnitClick(GridUnit unit)
        {
            currentState?.OnUnitClicked(unit);
        }

        public virtual void ProcessCellClick(Cell cell)
        {
            currentState?.OnCellClicked(cell);
        }

        public virtual void ProcessRightClick()
        {
            currentState?.OnRightClick();
        }

        /// <summary>
        /// Entry point for human right-click input. Delegates to the scene input coordinator when
        /// active, otherwise applies the authoritative runtime grid state handler.
        /// </summary>
        public virtual void ProcessSceneRightClick()
        {
            if (SceneInputCoordinator != null)
            {
                SceneInputCoordinator.OnSceneRightClick(this);
                return;
            }

            ProcessRightClick();
        }

        public virtual void ConfirmPendingMoveWait()
        {
            if (currentState is RuntimeGridStateUnitMovePendingConfirm pendingState)
            {
                pendingState.ConfirmWait();
            }
        }

        public virtual void ConfirmPendingMoveAfterCombat(bool consumeAllRemainingMovement = false)
        {
            if (currentState is RuntimeGridStateUnitMovePendingConfirm pendingState)
            {
                pendingState.ConfirmPendingMoveAfterCombat(consumeAllRemainingMovement);
            }
        }

        public virtual void RefreshSceneCollections()
        {
            if (!autoCollectSceneChildren)
            {
                return;
            }

            var collectedCells = GetComponentsInChildren<Cell>(true)
                .Where(cell => cell != null)
                .Distinct()
                .ToList();
            var collectedUnits = GetComponentsInChildren<GridUnit>(true)
                .Where(unit => unit != null)
                .Distinct()
                .ToList();
            var collectedPlayers = GetComponentsInChildren<MonoBehaviour>(true)
                .OfType<IBattlePlayer>()
                .Where(player => player != null)
                .Distinct()
                .OrderBy(player => player.PlayerId)
                .ToList();

            ApplyCollections(collectedCells, collectedUnits, collectedPlayers);
        }

        public virtual void SetMirroredCollections(
            IEnumerable<Cell> mirroredCells,
            IEnumerable<GridUnit> mirroredUnits,
            IEnumerable<IBattlePlayer> mirroredPlayers)
        {
            var collectedCells = mirroredCells?
                .Where(cell => cell != null)
                .Distinct()
                .ToList() ?? new List<Cell>();
            var collectedUnits = mirroredUnits?
                .Where(unit => unit != null)
                .Distinct()
                .ToList() ?? new List<GridUnit>();
            var collectedPlayers = mirroredPlayers?
                .Where(player => player != null)
                .Distinct()
                .OrderBy(player => player.PlayerId)
                .ToList() ?? new List<IBattlePlayer>();

            ApplyCollections(collectedCells, collectedUnits, collectedPlayers);
        }

        private void ApplyCollections(
            List<Cell> collectedCells,
            List<GridUnit> collectedUnits,
            List<IBattlePlayer> collectedPlayers)
        {
            foreach (var cell in cells)
            {
                if (cell == null)
                {
                    continue;
                }

                cell.Clicked -= HandleCellClicked;
                cell.Hovered -= HandleCellHovered;
                cell.Unhovered -= HandleCellUnhovered;
            }

            foreach (var unit in units)
            {
                if (unit == null)
                {
                    continue;
                }

                unit.Clicked -= HandleUnitClicked;
                unit.Hovered -= HandleUnitHovered;
                unit.Unhovered -= HandleUnitUnhovered;
            }

            var previousUnits = new HashSet<GridUnit>(units.Where(unit => unit != null));
            cells = collectedCells ?? new List<Cell>();
            units = collectedUnits ?? new List<GridUnit>();
            players.Clear();
            players.AddRange(collectedPlayers ?? Enumerable.Empty<IBattlePlayer>());

            WireCellEvents();
            WireUnitEvents();

            foreach (var unit in units)
            {
                if (unit != null && !previousUnits.Contains(unit))
                {
                    UnitRegistered?.Invoke(unit);
                }
            }

            foreach (var removed in previousUnits.Where(unit => unit != null && !units.Contains(unit)))
            {
                UnitUnregistered?.Invoke(removed);
            }
        }

        public virtual void RegisterUnit(GridUnit unit)
        {
            if (unit == null || units.Contains(unit))
            {
                return;
            }

            units.Add(unit);
            unit.AttachToGrid(this);
            WireUnitEvents();
            UnitRegistered?.Invoke(unit);
        }

        public virtual void UnregisterUnit(GridUnit unit)
        {
            if (unit == null || !units.Remove(unit))
            {
                return;
            }

            unit.Clicked -= HandleUnitClicked;
            unit.Hovered -= HandleUnitHovered;
            unit.Unhovered -= HandleUnitUnhovered;
            UnitUnregistered?.Invoke(unit);
        }

        private void BeginCurrentTurn(bool kickTurnPlayerPlay = true)
        {
            foreach (var unit in GetCurrentPlayerUnits())
            {
                unit?.BeginTurn();
            }

            var player = CurrentPlayer;
            Debug.Log($"[BattleFlow] Starting player {player?.PlayerId ?? -1} turn.");
            TurnStarted?.Invoke(player);

            if (player != null && player.IsHumanControlled)
            {
                SetState(new RuntimeGridStateWaitingForInput(this));
            }
            else
            {
                SetState(new RuntimeGridStateAiTurn(this));
                if (kickTurnPlayerPlay)
                {
                    RequestAiTurn();
                }
            }

            if (kickTurnPlayerPlay && player is IBattleTurnPlayer turnPlayer)
            {
                turnPlayer.PlayTurn(this);
            }
        }

        private void WireCellEvents()
        {
            foreach (var cell in cells)
            {
                if (cell == null)
                {
                    continue;
                }

                cell.Clicked -= HandleCellClicked;
                cell.Hovered -= HandleCellHovered;
                cell.Unhovered -= HandleCellUnhovered;
                cell.Clicked += HandleCellClicked;
                cell.Hovered += HandleCellHovered;
                cell.Unhovered += HandleCellUnhovered;
            }
        }

        private void WireUnitEvents()
        {
            foreach (var unit in units)
            {
                if (unit == null)
                {
                    continue;
                }

                unit.AttachToGrid(this);
                unit.Clicked -= HandleUnitClicked;
                unit.Hovered -= HandleUnitHovered;
                unit.Unhovered -= HandleUnitUnhovered;
                unit.Clicked += HandleUnitClicked;
                unit.Hovered += HandleUnitHovered;
                unit.Unhovered += HandleUnitUnhovered;
            }
        }

        private void HandleCellClicked(Cell cell)
        {
            if (!sceneInputEnabled)
            {
                return;
            }

            if (SceneInputCoordinator != null)
            {
                SceneInputCoordinator.OnSceneCellClicked(this, cell);
                return;
            }

            currentState?.OnCellClicked(cell);
        }

        private void HandleCellHovered(Cell cell)
        {
            if (!sceneInputEnabled)
            {
                return;
            }

            if (SceneInputCoordinator != null)
            {
                SceneInputCoordinator.OnSceneCellHovered(this, cell);
                return;
            }

            currentState?.OnCellHovered(cell);
        }

        private void HandleCellUnhovered(Cell cell)
        {
            if (!sceneInputEnabled)
            {
                return;
            }

            if (SceneInputCoordinator != null)
            {
                SceneInputCoordinator.OnSceneCellUnhovered(this, cell);
                return;
            }

            currentState?.OnCellUnhovered(cell);
        }

        private void HandleUnitClicked(GridUnit unit)
        {
            if (!sceneInputEnabled)
            {
                return;
            }

            if (SceneInputCoordinator != null)
            {
                SceneInputCoordinator.OnSceneUnitClicked(this, unit);
                return;
            }

            currentState?.OnUnitClicked(unit);
        }

        private void HandleUnitHovered(GridUnit unit)
        {
            if (!sceneInputEnabled)
            {
                return;
            }

            if (SceneInputCoordinator != null)
            {
                SceneInputCoordinator.OnSceneUnitHovered(this, unit);
                return;
            }

            currentState?.OnUnitHovered(unit);
        }

        private void HandleUnitUnhovered(GridUnit unit)
        {
            if (!sceneInputEnabled)
            {
                return;
            }

            if (SceneInputCoordinator != null)
            {
                SceneInputCoordinator.OnSceneUnitUnhovered(this, unit);
                return;
            }

            currentState?.OnUnitUnhovered(unit);
        }
    }

    /// <summary>
    /// Handles Runtime grid scene input directly (no Func-bridge indirection). The coordinator
    /// owns human input when migration toggle is on; RuntimeGrid forwards clicks/hover here
    /// instead of through legacy bridge callbacks.
    /// </summary>
    public interface IRuntimeGridSceneInputCoordinator
    {
        void OnSceneUnitClicked(RuntimeGrid grid, GridUnit unit);
        void OnSceneCellClicked(RuntimeGrid grid, Cell cell);
        void OnSceneRightClick(RuntimeGrid grid);
        void OnSceneUnitHovered(RuntimeGrid grid, GridUnit unit);
        void OnSceneUnitUnhovered(RuntimeGrid grid, GridUnit unit);
        void OnSceneCellHovered(RuntimeGrid grid, Cell cell);
        void OnSceneCellUnhovered(RuntimeGrid grid, Cell cell);
    }
}

