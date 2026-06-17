using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Runtime.Board.States;
using Windy.Srpg.Runtime.Players;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Board
{
    public class BattleBoard : MonoBehaviour, IBattleBoard
    {
        [SerializeField] private List<BoardCell> cells = new List<BoardCell>();
        [SerializeField] private List<BoardUnit> units = new List<BoardUnit>();
        [SerializeField] private bool autoCollectSceneChildren = true;
        [SerializeField] private bool startBattleOnStart;
        [SerializeField] private bool sceneInputEnabled;

        private readonly List<IBattlePlayer> players = new List<IBattlePlayer>();
        private BoardState currentState;
        private int currentPlayerIndex;
        private bool battleStarted;

        public event Action<BoardState> StateChanged;
        public event Action<IBattlePlayer> TurnStarted;
        public event Action<IBattlePlayer> TurnEnded;
        public event Action AiTurnRequested;
        public event Action<BattleBoard> BattleStarted;
        public event Action<BoardUnit> UnitRegistered;
        public event Action<BoardUnit> UnitUnregistered;

        public IReadOnlyList<BoardCell> Cells => cells;
        public IReadOnlyList<BoardUnit> Units => units;
        public IReadOnlyList<IBattlePlayer> Players => players;
        IReadOnlyList<IBoardUnit> IBattleBoard.Units => units;
        IReadOnlyList<IBattlePlayer> IBattleBoard.Players => players;
        public BoardState CurrentState => currentState;
        public IBattlePlayer CurrentPlayer => players.Count == 0 ? null : players[Mathf.Clamp(currentPlayerIndex, 0, players.Count - 1)];
        public int CurrentPlayerId => CurrentPlayer?.PlayerId ?? -1;
        public bool BattleIsStarted => battleStarted;
        public bool SceneInputEnabled => sceneInputEnabled;

        /// <summary>
        /// When set, scene input is handled by the coordinator (direct runtime authority path)
        /// instead of the board state's native handlers or legacy Func bridges.
        /// </summary>
        public IBattleBoardSceneInputCoordinator SceneInputCoordinator { get; set; }

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
        /// Host-driven battle start: init players, set first player, kick first turn.
        /// When refreshSceneCollections is false the host must already have mirrored units/players
        /// (e.g. via SetMirroredCollections) because scene units may live outside this transform.
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
                player.InitializeBoard(this);
            }

            SetCurrentPlayerById(startPlan.NextPlayer?.PlayerId ?? -1);
            BattleStarted?.Invoke(this);
            BeginCurrentTurn(kickFirstTurn);
        }

        public virtual void SetState(BoardState nextState)
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

        public virtual IEnumerable<BoardUnit> GetUnitsOwnedBy(int playerId)
        {
            return BattleBoardQueries.GetUnitsForPlayer(units, playerId);
        }

        public virtual IEnumerable<BoardUnit> GetCurrentPlayerUnits()
        {
            return CurrentPlayer == null
                ? Array.Empty<BoardUnit>()
                : BattleBoardQueries.GetCurrentPlayerUnits(units, CurrentPlayer.PlayerId);
        }

        public virtual IEnumerable<BoardUnit> GetEnemyUnits(IBattlePlayer player)
        {
            if (player == null)
            {
                return Array.Empty<BoardUnit>();
            }

            return BattleBoardQueries.GetEnemyUnits(units, player.PlayerId);
        }

        public virtual IEnumerable<BoardUnit> GetEnemyUnits(int playerId)
        {
            return BattleBoardQueries.GetEnemyUnits(units, playerId);
        }

        public virtual IBattlePlayer GetPlayerById(int playerId)
        {
            return BattleBoardQueries.GetPlayerById(players, playerId);
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
        /// Starts the active player's turn execution after legacy board sync has caught up.
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

        public virtual void ProcessUnitClick(BoardUnit unit)
        {
            currentState?.OnUnitClicked(unit);
        }

        public virtual void ProcessCellClick(BoardCell cell)
        {
            currentState?.OnCellClicked(cell);
        }

        public virtual void ProcessRightClick()
        {
            currentState?.OnRightClick();
        }

        /// <summary>
        /// Entry point for human right-click input. Delegates to the scene input coordinator when
        /// active, otherwise applies the authoritative runtime board state handler.
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
            if (currentState is BoardStateUnitMovePendingConfirm pendingState)
            {
                pendingState.ConfirmWait();
            }
        }

        public virtual void ConfirmPendingMoveAfterCombat(bool consumeAllRemainingMovement = false)
        {
            if (currentState is BoardStateUnitMovePendingConfirm pendingState)
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

            var collectedCells = GetComponentsInChildren<BoardCell>(true)
                .Where(cell => cell != null)
                .Distinct()
                .ToList();
            var collectedUnits = GetComponentsInChildren<BoardUnit>(true)
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
            IEnumerable<BoardCell> mirroredCells,
            IEnumerable<BoardUnit> mirroredUnits,
            IEnumerable<IBattlePlayer> mirroredPlayers)
        {
            var collectedCells = mirroredCells?
                .Where(cell => cell != null)
                .Distinct()
                .ToList() ?? new List<BoardCell>();
            var collectedUnits = mirroredUnits?
                .Where(unit => unit != null)
                .Distinct()
                .ToList() ?? new List<BoardUnit>();
            var collectedPlayers = mirroredPlayers?
                .Where(player => player != null)
                .Distinct()
                .OrderBy(player => player.PlayerId)
                .ToList() ?? new List<IBattlePlayer>();

            ApplyCollections(collectedCells, collectedUnits, collectedPlayers);
        }

        private void ApplyCollections(
            List<BoardCell> collectedCells,
            List<BoardUnit> collectedUnits,
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

            var previousUnits = new HashSet<BoardUnit>(units.Where(unit => unit != null));
            cells = collectedCells ?? new List<BoardCell>();
            units = collectedUnits ?? new List<BoardUnit>();
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

        public virtual void RegisterUnit(BoardUnit unit)
        {
            if (unit == null || units.Contains(unit))
            {
                return;
            }

            units.Add(unit);
            unit.AttachToBoard(this);
            WireUnitEvents();
            UnitRegistered?.Invoke(unit);
        }

        public virtual void UnregisterUnit(BoardUnit unit)
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
                SetState(new BoardStateWaitingForInput(this));
            }
            else
            {
                SetState(new BoardStateAiTurn(this));
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

                unit.AttachToBoard(this);
                unit.Clicked -= HandleUnitClicked;
                unit.Hovered -= HandleUnitHovered;
                unit.Unhovered -= HandleUnitUnhovered;
                unit.Clicked += HandleUnitClicked;
                unit.Hovered += HandleUnitHovered;
                unit.Unhovered += HandleUnitUnhovered;
            }
        }

        private void HandleCellClicked(BoardCell cell)
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

        private void HandleCellHovered(BoardCell cell)
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

        private void HandleCellUnhovered(BoardCell cell)
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

        private void HandleUnitClicked(BoardUnit unit)
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

        private void HandleUnitHovered(BoardUnit unit)
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

        private void HandleUnitUnhovered(BoardUnit unit)
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
    /// Handles runtime board scene input directly (no Func-bridge indirection). The coordinator
    /// owns human input when migration toggle is on; BattleBoard forwards clicks/hover here
    /// instead of through legacy bridge callbacks.
    /// </summary>
    public interface IBattleBoardSceneInputCoordinator
    {
        void OnSceneUnitClicked(BattleBoard board, BoardUnit unit);
        void OnSceneCellClicked(BattleBoard board, BoardCell cell);
        void OnSceneRightClick(BattleBoard board);
        void OnSceneUnitHovered(BattleBoard board, BoardUnit unit);
        void OnSceneUnitUnhovered(BattleBoard board, BoardUnit unit);
        void OnSceneCellHovered(BattleBoard board, BoardCell cell);
        void OnSceneCellUnhovered(BattleBoard board, BoardCell cell);
    }
}

