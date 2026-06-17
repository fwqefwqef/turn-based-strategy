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
        [SerializeField] private List<BattleUnit> units = new List<BattleUnit>();
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
        public event Action<BattleUnit> UnitRegistered;
        public event Action<BattleUnit> UnitUnregistered;

        public readonly struct ShadowTransitionSnapshot
        {
            public ShadowTransitionSnapshot(
                string stateLabel,
                BattleUnit selectedUnit,
                BoardCell pendingDestination,
                BoardCell observedUnitCell,
                float observedUnitMovementPoints,
                bool observedUnitFinished)
            {
                StateLabel = stateLabel;
                SelectedUnit = selectedUnit;
                PendingDestination = pendingDestination;
                ObservedUnitCell = observedUnitCell;
                ObservedUnitMovementPoints = observedUnitMovementPoints;
                ObservedUnitFinished = observedUnitFinished;
            }

            public string StateLabel { get; }
            public BattleUnit SelectedUnit { get; }
            public BoardCell PendingDestination { get; }
            public BoardCell ObservedUnitCell { get; }
            public float ObservedUnitMovementPoints { get; }
            public bool ObservedUnitFinished { get; }
        }

        public readonly struct EndTurnShadowSnapshot
        {
            public EndTurnShadowSnapshot(int nextPlayerId, string postTurnStateLabel)
            {
                NextPlayerId = nextPlayerId;
                PostTurnStateLabel = postTurnStateLabel;
            }

            public int NextPlayerId { get; }
            public string PostTurnStateLabel { get; }
        }

        public IReadOnlyList<BoardCell> Cells => cells;
        public IReadOnlyList<BattleUnit> Units => units;
        public IReadOnlyList<IBattlePlayer> Players => players;
        IReadOnlyList<IBattleUnit> IBattleBoard.Units => units;
        IReadOnlyList<IBattlePlayer> IBattleBoard.Players => players;
        public BoardState CurrentState => currentState;
        public IBattlePlayer CurrentPlayer => players.Count == 0 ? null : players[Mathf.Clamp(currentPlayerIndex, 0, players.Count - 1)];
        public int CurrentPlayerId => CurrentPlayer?.PlayerId ?? -1;
        public bool BattleIsStarted => battleStarted;
        public bool SceneInputEnabled => sceneInputEnabled;

        /// <summary>
        /// When true the board is being driven by the non-authoritative shadow harness:
        /// state transitions still run (so decisions can be read), but visible side effects
        /// (unit Select/Deselect visuals + events, StateChanged subscribers) are suppressed.
        /// </summary>
        public bool ShadowMode { get; private set; }

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

        public virtual void StartBattle()
        {
            RefreshSceneCollections();
            WireCellEvents();
            WireUnitEvents();
            battleStarted = true;
            sceneInputEnabled = true;
            foreach (var player in players.OfType<IBattleTurnPlayer>())
            {
                player.InitializeBoard(this);
            }

            RoundRobinTurnPlan plan = RoundRobinBattleFlow.ResolveStart(this);
            SetCurrentPlayerById(plan.NextPlayer?.PlayerId ?? -1);
            BattleStarted?.Invoke(this);
            BeginCurrentTurn();
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
            if (!ShadowMode)
            {
                StateChanged?.Invoke(currentState);
            }
        }

        /// <summary>
        /// Non-authoritative: evaluates what the runtime's waiting-for-input state would select
        /// given a clicked unit, using the real runtime state classes but suppressing all visible
        /// side effects and leaving the authoritative current state untouched. Returns the unit the
        /// runtime would select, or null if it would not select anything. Used by the shadow harness
        /// to prove decision parity with the framework before authority is flipped.
        /// </summary>
        public BattleUnit ShadowEvaluateUnitClickFromWaiting(BattleUnit clickedUnit)
        {
            BoardState savedState = currentState;
            bool savedShadow = ShadowMode;
            ShadowMode = true;
            try
            {
                currentState = new BoardStateWaitingForInput(this);
                currentState.OnUnitClicked(clickedUnit);
                return currentState.SelectedUnit;
            }
            finally
            {
                currentState = savedState;
                ShadowMode = savedShadow;
            }
        }

        /// <summary>
        /// Non-authoritative: evaluates what the runtime selected-unit state would do on
        /// right-click, suppressing all visible side effects and leaving the authoritative state
        /// untouched. Returns the selected unit after the simulated right-click, or null if the
        /// runtime would deselect.
        /// </summary>
        public BattleUnit ShadowEvaluateRightClickFromSelected(BattleUnit selectedUnit)
        {
            BoardState savedState = currentState;
            bool savedShadow = ShadowMode;
            ShadowMode = true;
            try
            {
                currentState = new BoardStateUnitSelected(this, selectedUnit);
                currentState.OnRightClick();
                return currentState.SelectedUnit;
            }
            finally
            {
                currentState = savedState;
                ShadowMode = savedShadow;
            }
        }

        public BoardState ShadowEvaluateUnitClickFromSelected(BattleUnit selectedUnit, BattleUnit clickedUnit)
        {
            BoardState savedState = currentState;
            bool savedShadow = ShadowMode;
            ShadowMode = true;
            try
            {
                currentState = new BoardStateUnitSelected(this, selectedUnit);
                currentState.OnUnitClicked(clickedUnit);
                return currentState;
            }
            finally
            {
                currentState = savedState;
                ShadowMode = savedShadow;
            }
        }

        public BoardState ShadowEvaluateCellClickFromSelected(BattleUnit selectedUnit, BoardCell clickedCell)
        {
            BoardState savedState = currentState;
            bool savedShadow = ShadowMode;
            ShadowMode = true;
            try
            {
                currentState = new BoardStateUnitSelected(this, selectedUnit);
                currentState.OnCellClicked(clickedCell);
                return currentState;
            }
            finally
            {
                currentState = savedState;
                ShadowMode = savedShadow;
            }
        }

        public ShadowTransitionSnapshot ShadowEvaluatePendingMoveRightClick(BattleUnit selectedUnit, BoardCell pendingDestination)
        {
            BoardState savedState = currentState;
            bool savedShadow = ShadowMode;
            BattleUnit.RuntimeSnapshot savedUnitSnapshot = selectedUnit != null
                ? selectedUnit.CaptureRuntimeSnapshot()
                : default;
            ShadowMode = true;
            try
            {
                currentState = new BoardStateUnitMovePendingConfirm(this, selectedUnit, pendingDestination);
                currentState.OnStateEnter();
                currentState.OnRightClick();
                return CaptureShadowSnapshot(currentState, selectedUnit);
            }
            finally
            {
                if (selectedUnit != null)
                {
                    selectedUnit.RestoreRuntimeSnapshot(savedUnitSnapshot);
                }

                currentState = savedState;
                ShadowMode = savedShadow;
            }
        }

        public ShadowTransitionSnapshot ShadowEvaluatePendingMoveWait(BattleUnit selectedUnit, BoardCell pendingDestination)
        {
            BoardState savedState = currentState;
            bool savedShadow = ShadowMode;
            BattleUnit.RuntimeSnapshot savedUnitSnapshot = selectedUnit != null
                ? selectedUnit.CaptureRuntimeSnapshot()
                : default;
            ShadowMode = true;
            try
            {
                var pendingState = new BoardStateUnitMovePendingConfirm(this, selectedUnit, pendingDestination);
                currentState = pendingState;
                currentState.OnStateEnter();
                pendingState.ConfirmWait();
                return CaptureShadowSnapshot(currentState, selectedUnit);
            }
            finally
            {
                if (selectedUnit != null)
                {
                    selectedUnit.RestoreRuntimeSnapshot(savedUnitSnapshot);
                }

                currentState = savedState;
                ShadowMode = savedShadow;
            }
        }

        public virtual IEnumerable<BattleUnit> GetUnitsOwnedBy(int playerId)
        {
            return BattleBoardQueries.GetUnitsForPlayer(units, playerId);
        }

        public virtual IEnumerable<BattleUnit> GetCurrentPlayerUnits()
        {
            return CurrentPlayer == null
                ? Array.Empty<BattleUnit>()
                : BattleBoardQueries.GetCurrentPlayerUnits(units, CurrentPlayer.PlayerId);
        }

        public virtual IEnumerable<BattleUnit> GetEnemyUnits(IBattlePlayer player)
        {
            if (player == null)
            {
                return Array.Empty<BattleUnit>();
            }

            return BattleBoardQueries.GetEnemyUnits(units, player.PlayerId);
        }

        public virtual IEnumerable<BattleUnit> GetEnemyUnits(int playerId)
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

        public EndTurnShadowSnapshot ShadowEvaluateEndCurrentTurn(bool kickTurnPlayerPlay = true)
        {
            BoardState savedState = currentState;
            int savedPlayerIndex = currentPlayerIndex;
            bool savedShadow = ShadowMode;
            var savedUnitSnapshots = new List<(BattleUnit unit, BattleUnit.RuntimeSnapshot snapshot)>();
            for (int i = 0; i < units.Count; i++)
            {
                BattleUnit unit = units[i];
                if (unit != null)
                {
                    savedUnitSnapshots.Add((unit, unit.CaptureRuntimeSnapshot()));
                }
            }

            ShadowMode = true;
            try
            {
                EndCurrentTurn(kickTurnPlayerPlay);
                return new EndTurnShadowSnapshot(
                    CurrentPlayerId,
                    currentState?.DiagnosticStateLabel ?? "<null>");
            }
            finally
            {
                ShadowMode = savedShadow;
                currentPlayerIndex = savedPlayerIndex;
                currentState = savedState;
                for (int i = 0; i < savedUnitSnapshots.Count; i++)
                {
                    (BattleUnit unit, BattleUnit.RuntimeSnapshot snapshot) = savedUnitSnapshots[i];
                    unit?.RestoreRuntimeSnapshot(snapshot);
                }
            }
        }

        public virtual BattleOutcome EvaluateBattleOutcome()
        {
            return RoundRobinBattleFlow.EvaluateLastSideStanding(this);
        }

        public virtual void RequestAiTurn()
        {
            AiTurnRequested?.Invoke();
        }

        public virtual void ProcessUnitClick(BattleUnit unit)
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

        public virtual void ConfirmPendingMoveWait()
        {
            if (currentState is BoardStateUnitMovePendingConfirm pendingState)
            {
                pendingState.ConfirmWait();
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
            var collectedUnits = GetComponentsInChildren<BattleUnit>(true)
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
            IEnumerable<BattleUnit> mirroredUnits,
            IEnumerable<IBattlePlayer> mirroredPlayers)
        {
            var collectedCells = mirroredCells?
                .Where(cell => cell != null)
                .Distinct()
                .ToList() ?? new List<BoardCell>();
            var collectedUnits = mirroredUnits?
                .Where(unit => unit != null)
                .Distinct()
                .ToList() ?? new List<BattleUnit>();
            var collectedPlayers = mirroredPlayers?
                .Where(player => player != null)
                .Distinct()
                .OrderBy(player => player.PlayerId)
                .ToList() ?? new List<IBattlePlayer>();

            ApplyCollections(collectedCells, collectedUnits, collectedPlayers);
        }

        private void ApplyCollections(
            List<BoardCell> collectedCells,
            List<BattleUnit> collectedUnits,
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

            var previousUnits = new HashSet<BattleUnit>(units.Where(unit => unit != null));
            cells = collectedCells ?? new List<BoardCell>();
            units = collectedUnits ?? new List<BattleUnit>();
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

        public virtual void RegisterUnit(BattleUnit unit)
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

        public virtual void UnregisterUnit(BattleUnit unit)
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

            currentState?.OnCellClicked(cell);
        }

        private void HandleCellHovered(BoardCell cell)
        {
            if (!sceneInputEnabled)
            {
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

            currentState?.OnCellUnhovered(cell);
        }

        private void HandleUnitClicked(BattleUnit unit)
        {
            if (!sceneInputEnabled)
            {
                return;
            }

            currentState?.OnUnitClicked(unit);
        }

        private void HandleUnitHovered(BattleUnit unit)
        {
            if (!sceneInputEnabled)
            {
                return;
            }

            currentState?.OnUnitHovered(unit);
        }

        private void HandleUnitUnhovered(BattleUnit unit)
        {
            if (!sceneInputEnabled)
            {
                return;
            }

            currentState?.OnUnitUnhovered(unit);
        }

        private static ShadowTransitionSnapshot CaptureShadowSnapshot(BoardState state, BattleUnit observedUnit)
        {
            return new ShadowTransitionSnapshot(
                state?.DiagnosticStateLabel ?? "<null>",
                state?.SelectedUnit,
                state?.PendingDestination,
                observedUnit?.CurrentCell,
                observedUnit?.MovementPointsRemaining ?? 0f,
                observedUnit?.IsFinishedForTurn ?? false);
        }
    }
}
