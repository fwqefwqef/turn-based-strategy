using System;
using System.Collections.Generic;
using System.Linq;
using TbsFramework.Cells;
using TbsFramework.Grid.GameResolvers;
using TbsFramework.Grid.GridStates;
using TbsFramework.Players;
using TbsFramework.Units;
using TbsFramework.Units.Abilities;
using UnityEngine;
using Windy.Srpg.Runtime.Actions;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Players;
using Windy.Srpg.Runtime.Units;

namespace TbsFramework.Grid
{
    /// <summary>
    /// CellGrid class keeps track of the game, stores cells, units and players objects. It starts the game and makes turn transitions. 
    /// It reacts to user interacting with units or cells, and raises events related to game progress. 
    /// </summary>
    public partial class CellGrid : MonoBehaviour
    {
        /// <summary>
        /// LevelLoading event is invoked before Initialize method is run.
        /// </summary>
        public event EventHandler LevelLoading;
        /// <summary>
        /// LevelLoadingDone event is invoked after Initialize method has finished running.
        /// </summary>
        public event EventHandler LevelLoadingDone;
        /// <summary>
        /// GameStarted event is invoked at the beggining of StartGame method.
        /// </summary>
        public event EventHandler GameStarted;
        /// <summary>
        /// GameEnded event is invoked when there is a single player left in the game.
        /// </summary>
        public event EventHandler<GameEndedArgs> GameEnded;
        /// <summary>
        /// Turn ended event is invoked at the end of each turn.
        /// </summary>
        public event EventHandler<bool> TurnEnded;

        /// <summary>
        /// UnitAdded event is invoked each time AddUnit method is called.
        /// </summary>
        public event EventHandler<UnitCreatedEventArgs> UnitAdded;

        private CellGridState _cellGridState;
        public CellGridState cellGridState
        {
            get
            {
                return _cellGridState;
            }
            set
            {
                CellGridState nextState;
                if (_cellGridState != null)
                {
                    _cellGridState.OnStateExit();
                    nextState = _cellGridState.MakeTransition(value);
                }
                else
                {
                    nextState = value;
                }

                _cellGridState = nextState;
                _cellGridState.OnStateEnter();
            }
        }

        public int NumberOfPlayers { get { return RuntimePlayers.Count > 0 ? RuntimePlayers.Count : Players.Count; } }

        public Player CurrentPlayer
        {
            get { return Players.Find(p => p.PlayerNumber.Equals(CurrentPlayerNumber)); }
        }
        public IBattleTurnPlayer CurrentRuntimePlayer
        {
            get { return RuntimePlayers.Find(p => p != null && p.PlayerId.Equals(CurrentPlayerNumber)); }
        }
        public int CurrentPlayerNumber { get; private set; }

        [HideInInspector]
        public bool Is2D;

        /// <summary>
        /// GameObject that holds player objects.
        /// </summary>
        public Transform PlayersParent;
        public bool ShouldStartGameImmediately = true;

        private int UnitId = 0;

        public bool GameFinished { get; private set; }
        public List<Player> Players { get; private set; }
        public List<IBattleTurnPlayer> RuntimePlayers { get; private set; }
        public List<Cell> Cells { get; private set; }
        public List<Unit> Units { get; private set; }
        private Func<List<Unit>> PlayableUnits = () => new List<Unit>();

        private void Start()
        {
            if (ShouldStartGameImmediately)
            {
                InitializeAndStart();
            }
        }

        public void InitializeAndStart()
        {
            Initialize();
            StartGame();
        }

        public void Initialize()
        {
            if (LevelLoading != null)
                LevelLoading.Invoke(this, EventArgs.Empty);

            GameFinished = false;
            Players = new List<Player>();
            RuntimePlayers = new List<IBattleTurnPlayer>();
            IBattleBoard battleBoard = ResolveBattleBoard();
            for (int i = 0; i < PlayersParent.childCount; i++)
            {
                Transform playerTransform = PlayersParent.GetChild(i);
                if (playerTransform == null || !playerTransform.gameObject.activeInHierarchy)
                {
                    continue;
                }

                MonoBehaviour runtimePlayerComponent = playerTransform
                    .GetComponents<MonoBehaviour>()
                    .FirstOrDefault(component => component is IBattleTurnPlayer);
                if (runtimePlayerComponent is IBattleTurnPlayer runtimePlayer)
                {
                    if (battleBoard != null)
                    {
                        runtimePlayer.InitializeBoard(battleBoard);
                    }

                    RuntimePlayers.Add(runtimePlayer);
                }

                var player = playerTransform.GetComponent<Player>();
                if (player != null)
                {
                    player.Initialize(this);
                    Players.Add(player);
                }
            }

            Cells = new List<Cell>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var cell = transform.GetChild(i).gameObject.GetComponent<Cell>();
                if (cell != null)
                {
                    if (cell.gameObject.activeInHierarchy)
                    {
                        Cells.Add(cell);
                        cell.Initialize(this);
                    }
                }
                else
                {
                    Debug.LogError("Invalid object in cells parent game object");
                }
            }

            foreach (var cell in Cells)
            {
                cell.CellClicked += OnCellClicked;
                cell.CellHighlighted += OnCellHighlighted;
                cell.CellDehighlighted += OnCellDehighlighted;
                cell.GetComponent<Cell>().GetNeighbours(Cells);
            }

            Units = new List<Unit>();
            var runtimeUnitSource = GetComponent<IBattleSceneUnitSource>();
            if (runtimeUnitSource != null && battleBoard != null)
            {
                var unitTransforms = runtimeUnitSource.GetInitialUnitTransforms(battleBoard) ?? Array.Empty<Transform>();
                foreach (var unitTransform in unitTransforms)
                {
                    if (unitTransform != null)
                    {
                        AddUnit(unitTransform);
                    }
                }
            }
            else
            {
                Debug.LogError("No battle scene unit source script attached to cell grid");
            }

            if (LevelLoadingDone != null)
                LevelLoadingDone.Invoke(this, EventArgs.Empty);
        }

        private IBattleBoard ResolveBattleBoard()
        {
            return this as IBattleBoard ?? GetComponent<IBattleBoard>();
        }

        private void OnCellDehighlighted(object sender, EventArgs e)
        {
            DispatchCellDeselected(sender as Cell);
        }
        private void OnCellHighlighted(object sender, EventArgs e)
        {
            DispatchCellSelected(sender as Cell);
        }
        private void OnCellClicked(object sender, EventArgs e)
        {
            DispatchCellClicked(sender as Cell);
        }

        protected void OnUnitClicked(object sender, EventArgs e)
        {
            DispatchUnitClicked(sender as Unit);
        }

        protected void OnUnitHighlighted(object sender, EventArgs e)
        {
            DispatchUnitHighlighted(sender as Unit);
        }

        protected void OnUnitDehighlighted(object sender, EventArgs e)
        {
            DispatchUnitDehighlighted(sender as Unit);
        }

        protected virtual void DispatchCellDeselected(Cell cell)
        {
            if (cellGridState == null || cell == null)
            {
                return;
            }

            cellGridState.OnCellDeselected(cell);
        }

        protected virtual void DispatchCellSelected(Cell cell)
        {
            if (cellGridState == null || cell == null)
            {
                return;
            }

            cellGridState.OnCellSelected(cell);
        }

        protected virtual void DispatchCellClicked(Cell cell)
        {
            if (cellGridState == null || cell == null)
            {
                return;
            }

            cellGridState.OnCellClicked(cell);
        }

        protected virtual void DispatchUnitClicked(Unit unit)
        {
            if (cellGridState == null || unit == null)
            {
                return;
            }

            cellGridState.OnUnitClicked(unit);
        }

        protected virtual void DispatchUnitHighlighted(Unit unit)
        {
            if (cellGridState == null || unit == null)
            {
                return;
            }

            cellGridState.OnUnitHighlighted(unit);
        }

        protected virtual void DispatchUnitDehighlighted(Unit unit)
        {
            if (cellGridState == null || unit == null)
            {
                return;
            }

            cellGridState.OnUnitDehighlighted(unit);
        }

        protected void OnUnitDestroyed(object sender, AttackEventArgs e)
        {
            Units.Remove(e.Defender);
            NotifyOwnerDestroyed(e.Defender);
            e.Defender.UnitClicked -= OnUnitClicked;
            e.Defender.UnitHighlighted -= OnUnitHighlighted;
            e.Defender.UnitDehighlighted -= OnUnitDehighlighted;
            e.Defender.UnitDestroyed -= OnUnitDestroyed;
            e.Defender.UnitMoved -= OnUnitMoved;
            CheckGameFinished();
        }

        protected void NotifyUnitAdded(Transform unitTransform)
        {
            UnitAdded?.Invoke(this, new UnitCreatedEventArgs(unitTransform));
        }

        /// <summary>
        /// Adds unit to the game
        /// </summary>
        /// <param name="unit">Unit to add</param>
        public virtual void AddUnit(Transform unit, Cell targetCell = null, Player ownerPlayer = null)
        {
            unit.GetComponent<Unit>().UnitID = AllocateNextUnitId();
            Units.Add(unit.GetComponent<Unit>());

            if (targetCell != null)
            {
                targetCell.IsTaken = unit.GetComponent<Unit>().Obstructable;

                unit.GetComponent<Unit>().Cell = targetCell;
                unit.GetComponent<Unit>().transform.localPosition = targetCell.transform.localPosition;
            }

            if (ownerPlayer != null)
            {
                unit.GetComponent<Unit>().PlayerNumber = ownerPlayer.PlayerNumber;
            }

            if(unit.GetComponent<Unit>().Cell != null)
            {
                unit.GetComponent<Unit>().Cell.CurrentUnits.Add(unit.GetComponent<Unit>());
            }

            unit.GetComponent<Unit>().transform.localRotation = Quaternion.Euler(0, 0, 0);
            unit.GetComponent<Unit>().Initialize();

            unit.GetComponent<Unit>().UnitClicked += OnUnitClicked;
            unit.GetComponent<Unit>().UnitHighlighted += OnUnitHighlighted;
            unit.GetComponent<Unit>().UnitDehighlighted += OnUnitDehighlighted;
            unit.GetComponent<Unit>().UnitDestroyed += OnUnitDestroyed;
            unit.GetComponent<Unit>().UnitMoved += OnUnitMoved;

            if (UnitAdded != null)
            {
                UnitAdded.Invoke(this, new UnitCreatedEventArgs(unit));
            }
        }

        protected bool IsUnitRegistered(Unit unit)
        {
            return unit != null && Units != null && Units.Contains(unit);
        }

        protected void RegisterSceneUnit(Unit unit, Cell targetCell = null, Player ownerPlayer = null)
        {
            if (unit == null)
            {
                return;
            }

            Units ??= new List<Unit>();
            if (Units.Contains(unit))
            {
                return;
            }

            AddUnit(unit.transform, targetCell, ownerPlayer);
        }

        protected void UnregisterSceneUnit(Unit unit)
        {
            if (unit == null || Units == null || !Units.Remove(unit))
            {
                return;
            }

            unit.UnitClicked -= OnUnitClicked;
            unit.UnitHighlighted -= OnUnitHighlighted;
            unit.UnitDehighlighted -= OnUnitDehighlighted;
            unit.UnitDestroyed -= OnUnitDestroyed;
            unit.UnitMoved -= OnUnitMoved;
        }

        protected void OnUnitMoved(object sender, MovementEventArgs e)
        {
            CheckGameFinished();
        }

        /// <summary>
        /// Method is called once, at the beggining of the game.
        /// </summary>
        public void StartGame()
        {
            SyncBattleStartFromPlan(ResolveStartPlan(), kickPlayerPlay: true);
            Debug.Log("Game started");
        }

        /// <summary>
        /// Applies legacy battle-start sync (player index, GameStarted, unit turn hooks).
        /// When runtime already kicked the first turn, pass kickPlayerPlay: false.
        /// </summary>
        protected void SyncBattleStartFromPlan(RoundRobinTurnPlan plan, bool kickPlayerPlay = true, bool syncUnitTurnHooks = true)
        {
            PlayableUnits = CreatePlayableUnitsAccessor(plan);

            if (plan.NextPlayer == null)
            {
                Debug.LogError("CellGrid: No valid battle turn resolver or next player was found.");
                return;
            }

            CurrentPlayerNumber = plan.NextPlayer.PlayerId;

            GameStarted?.Invoke(this, EventArgs.Empty);

            if (syncUnitTurnHooks)
            {
                PlayableUnits().ForEach(u =>
                {
                    NotifyTurnStarted(u);
                    u.OnTurnStart();
                });
            }

            if (!kickPlayerPlay)
            {
                return;
            }

            if (CurrentRuntimePlayer != null && this is IBattleBoard battleBoard)
            {
                CurrentRuntimePlayer.PlayTurn(battleBoard);
            }
            else
            {
                CurrentPlayer?.Play(this);
            }
        }

        public void EndTurn(bool isNetworkInvoked=false)
        {
            _cellGridState.EndTurn(isNetworkInvoked);
        }

        /// <summary>
        /// Method makes the actual turn transitions.
        /// </summary>
        private void EndTurnExecute(bool isNetworkInvoked=false)
        {
            cellGridState = new CellGridStateBlockInput(this);
            if (CheckGameFinished())
            {
                return;
            }

            EndUnitsForCurrentPlayerTurn();
            CommitTurnTransition(ResolveNextTurnPlan(), isNetworkInvoked);
        }

        protected void EndUnitsForCurrentPlayerTurn()
        {
            var playableUnits = PlayableUnits();
            for (int i = 0; i < playableUnits.Count; i++)
            {
                var unit = playableUnits[i];
                if (unit == null)
                {
                    continue;
                }

                unit.OnTurnEnd();
                NotifyTurnEnded(unit);
            }
        }

        protected void CommitTurnTransition(RoundRobinTurnPlan plan, bool isNetworkInvoked = false, bool kickPlayerPlay = true, bool syncUnitTurnHooks = true)
        {
            PlayableUnits = CreatePlayableUnitsAccessor(plan);

            if (plan.NextPlayer == null)
            {
                Debug.LogError("CellGrid: No valid battle turn resolver or next player was found.");
                return;
            }

            CurrentPlayerNumber = plan.NextPlayer.PlayerId;

            if (TurnEnded != null)
            {
                TurnEnded.Invoke(this, isNetworkInvoked);
            }

            Debug.Log(string.Format("Player {0} turn", CurrentPlayerNumber));

            if (syncUnitTurnHooks)
            {
                var playableUnits = PlayableUnits();
                for (int i = 0; i < playableUnits.Count; i++)
                {
                    var unit = playableUnits[i];
                    if (unit == null)
                    {
                        continue;
                    }

                    NotifyTurnStarted(unit);
                    unit.OnTurnStart();
                }
            }

            if (!kickPlayerPlay)
            {
                return;
            }

            if (CurrentRuntimePlayer != null && this is IBattleBoard battleBoard)
            {
                CurrentRuntimePlayer.PlayTurn(battleBoard);
            }
            else
            {
                CurrentPlayer?.Play(this);
            }
        }

        public List<Unit> GetCurrentPlayerUnits()
        {
            return PlayableUnits();
        }
        public List<Unit> GetEnemyUnits(Player player)
        {
            return Units.FindAll(u => u.PlayerNumber != player.PlayerNumber);
        }
        public List<Unit> GetPlayerUnits(Player player)
        {
            return Units.FindAll(u => u.PlayerNumber == player.PlayerNumber);
        }

        public bool CheckGameFinished()
        {
            TryApplyBattleOutcome(ResolveBattleOutcome());
            return GameFinished;
        }

        protected bool TryApplyBattleOutcome(BattleOutcome outcome)
        {
            if (GameFinished)
            {
                return true;
            }

            if (!outcome.IsFinished)
            {
                return false;
            }

            cellGridState = new CellGridStateGameOver(this);
            GameFinished = true;
            if (GameEnded != null)
            {
                GameEnded.Invoke(this, new GameEndedArgs(CreateGameResult(outcome)));
            }

            return true;
        }

        private RoundRobinTurnPlan ResolveStartPlan()
        {
            var runtimeTurnResolver = GetComponent<IBattleTurnResolver>();
            if (runtimeTurnResolver != null && this is IBattleBoard battleBoard)
            {
                return runtimeTurnResolver.ResolveStart(battleBoard);
            }

            Debug.LogError("CellGrid: No battle turn resolver script attached to cell grid.");
            return new RoundRobinTurnPlan(null, Array.Empty<IBattleUnit>());
        }

        private RoundRobinTurnPlan ResolveNextTurnPlan()
        {
            var runtimeTurnResolver = GetComponent<IBattleTurnResolver>();
            if (runtimeTurnResolver != null && this is IBattleBoard battleBoard)
            {
                return runtimeTurnResolver.ResolveTurn(battleBoard);
            }

            Debug.LogError("CellGrid: No battle turn resolver script attached to cell grid.");
            return new RoundRobinTurnPlan(null, Array.Empty<IBattleUnit>());
        }

        private BattleOutcome ResolveBattleOutcome()
        {
            var runtimeEndCondition = GetComponent<IBattleEndCondition>();
            if (runtimeEndCondition != null && this is IBattleBoard battleBoard)
            {
                return runtimeEndCondition.Evaluate(battleBoard);
            }

            Debug.LogError("CellGrid: No battle end condition script attached to cell grid.");
            return new BattleOutcome(false, null, null);
        }

        private static Func<List<Unit>> CreatePlayableUnitsAccessor(RoundRobinTurnPlan plan)
        {
            return () => plan.PlayableUnits?.OfType<Unit>().ToList() ?? new List<Unit>();
        }

        private static GameResult CreateGameResult(BattleOutcome outcome)
        {
            return new GameResult(
                outcome.IsFinished,
                outcome.WinningPlayerIds?.ToList() ?? new List<int>(),
                outcome.DefeatedPlayerIds?.ToList() ?? new List<int>());
        }

        protected int AllocateNextUnitId()
        {
            return UnitId++;
        }

        private IEnumerable<IBattleAction> GetRuntimeActions(Unit unit)
        {
            return unit == null
                ? Enumerable.Empty<IBattleAction>()
                : unit.GetComponents<MonoBehaviour>().OfType<IBattleAction>();
        }

        protected virtual void NotifyTurnStarted(Unit unit)
        {
            if (unit == null)
            {
                return;
            }

            List<IBattleAction> runtimeActions = GetRuntimeActions(unit).ToList();
            if (runtimeActions.Count > 0 && this is IBattleBoard battleBoard)
            {
                for (int i = 0; i < runtimeActions.Count; i++)
                {
                    runtimeActions[i]?.OnTurnStarted(battleBoard);
                }

                return;
            }

            Ability[] abilities = unit.GetComponents<Ability>();
            for (int i = 0; i < abilities.Length; i++)
            {
                abilities[i]?.OnTurnStart(this);
            }
        }

        protected virtual void NotifyTurnEnded(Unit unit)
        {
            if (unit == null)
            {
                return;
            }

            List<IBattleAction> runtimeActions = GetRuntimeActions(unit).ToList();
            if (runtimeActions.Count > 0 && this is IBattleBoard battleBoard)
            {
                for (int i = 0; i < runtimeActions.Count; i++)
                {
                    runtimeActions[i]?.OnTurnEnded(battleBoard);
                }

                return;
            }

            Ability[] abilities = unit.GetComponents<Ability>();
            for (int i = 0; i < abilities.Length; i++)
            {
                abilities[i]?.OnTurnEnd(this);
            }
        }

        protected virtual void NotifyOwnerDestroyed(Unit unit)
        {
            if (unit == null)
            {
                return;
            }

            List<IBattleAction> runtimeActions = GetRuntimeActions(unit).ToList();
            if (runtimeActions.Count > 0 && this is IBattleBoard battleBoard)
            {
                for (int i = 0; i < runtimeActions.Count; i++)
                {
                    runtimeActions[i]?.OnOwnerDestroyed(battleBoard);
                }

                return;
            }

            Ability[] abilities = unit.GetComponents<Ability>();
            for (int i = 0; i < abilities.Length; i++)
            {
                abilities[i]?.OnUnitDestroyed(this);
            }
        }
    }
}

