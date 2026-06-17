using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Runtime.Actions;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Pathfinding;

namespace Windy.Srpg.Runtime.Units
{
    public class BattleUnit : MonoBehaviour, IBattleUnit
    {
        [SerializeField] private string unitId = string.Empty;
        [SerializeField] private string displayName = "Unit";
        [SerializeField] private int playerId;
        [SerializeField] private float baseMovementPoints = 5f;
        [SerializeField] private BoardCell startingCell;
        [SerializeField] private bool blocksOtherUnits = true;

        private readonly List<BattleAction> actions = new List<BattleAction>();
        private readonly IPathfinder pathfinder = new DijkstraPathfinder();
        private UnitTurnState turnState;
        private Dictionary<BoardCell, IList<BoardCell>> cachedPaths;
        private PendingMove? pendingMove;

        public event Action<BattleUnit> UnitSelected;
        public event Action<BattleUnit> UnitDeselected;
        public event Action<BattleUnit> Clicked;
        public event Action<BattleUnit> Hovered;
        public event Action<BattleUnit> Unhovered;

        public string UnitId => unitId;
        public string DisplayName => displayName;
        public int PlayerId => playerId;
        public float BaseMovementPoints => Mathf.Max(0f, baseMovementPoints);
        public float MovementPointsRemaining { get; private set; }
        public IReadOnlyList<BattleAction> Actions => actions;
        public BoardCell CurrentCell { get; private set; }
        public BoardCell PreviewCell => pendingMove?.ToCell ?? CurrentCell;
        public bool HasPendingMove => pendingMove.HasValue;
        public bool IsFinishedForTurn => turnState is { CountsAsFinished: true };
        public UnitTurnStateKind CurrentTurnStateKind => turnState?.Kind ?? UnitTurnStateKind.Normal;
        public UnitTurnState TurnState => turnState;
        public BattleBoard Board { get; private set; }
        public bool BlocksOtherUnits => blocksOtherUnits;

        internal struct PendingMove
        {
            public BoardCell FromCell;
            public BoardCell ToCell;
            public IList<BoardCell> Path;
            public float MovementPointsBefore;
            public float MovementCost;
        }

        protected virtual void Awake()
        {
            CacheActions();
            Initialize();
        }

        protected virtual void OnValidate()
        {
            CacheActions();
        }

        public virtual void Initialize()
        {
            CacheActions();
            foreach (var action in actions)
            {
                action.InitializeAction(this);
            }

            MovementPointsRemaining = BaseMovementPoints;
            SetState(new UnitTurnStateNormal(this));

            if (startingCell != null && CurrentCell == null)
            {
                AssignCellImmediate(startingCell);
            }
        }

        public virtual void BeginTurn()
        {
            MovementPointsRemaining = BaseMovementPoints;
            SetState(new UnitTurnStateNormal(this));
            foreach (var action in actions)
            {
                action.OnTurnStarted(Board);
            }
        }

        public virtual void EndTurn()
        {
            SetState(new UnitTurnStateFinished(this));
            foreach (var action in actions)
            {
                action.OnTurnEnded(Board);
            }
        }

        public virtual void SetState(UnitTurnState newState)
        {
            if (newState == null)
            {
                return;
            }

            turnState?.Exit();
            turnState = newState;
            turnState.Enter();
        }

        public virtual bool AssignCellImmediate(BoardCell targetCell, bool syncTransform = true)
        {
            if (targetCell == null)
            {
                return false;
            }

            if (CurrentCell == targetCell)
            {
                if (!targetCell.TryAddOccupant(this))
                {
                    return false;
                }

                if (syncTransform)
                {
                    transform.position = targetCell.transform.position;
                }

                return true;
            }

            if (!targetCell.TryAddOccupant(this))
            {
                return false;
            }

            CurrentCell?.RemoveOccupant(this);
            CurrentCell = targetCell;
            if (syncTransform)
            {
                transform.position = targetCell.transform.position;
            }

            return true;
        }

        public virtual void AttachToBoard(BattleBoard board)
        {
            Board = board;
            if (CurrentCell == null)
            {
                TryResolveCurrentCellFromBoard();
            }
        }

        public virtual bool CanTraverse(BoardCell cell)
        {
            return cell != null && cell.IsTraversable && (!cell.IsOccupied || cell.Occupants.Contains(this) || !blocksOtherUnits);
        }

        public virtual bool CanStopOn(BoardCell cell)
        {
            return cell != null && cell.CanOccupy(this);
        }

        public virtual void SpendMovement(float amount)
        {
            MovementPointsRemaining = Mathf.Max(0f, MovementPointsRemaining - Mathf.Max(0f, amount));
        }

        public virtual void SetBaseMovementPoints(float amount)
        {
            baseMovementPoints = Mathf.Max(0f, amount);
        }

        public virtual void SetMovementPointsRemaining(float amount)
        {
            MovementPointsRemaining = Mathf.Max(0f, amount);
        }

        public virtual IEnumerator MoveAlongPath(IReadOnlyList<BoardCell> path, float secondsPerStep = 0.05f)
        {
            if (path == null || path.Count == 0)
            {
                yield break;
            }

            for (var i = 1; i < path.Count; i++)
            {
                var step = path[i];
                if (step == null)
                {
                    continue;
                }

                SpendMovement(step.TraversalCost);
                AssignCellImmediate(step);
                yield return new WaitForSeconds(secondsPerStep);
            }
        }

        /// <summary>
        /// Drives a purely visual local-space walk of this unit along an origin-to-destination
        /// ordered path. It mutates no occupancy, cell, or movement-point state &mdash; callers remain
        /// responsible for committing end-state. Used by the runtime-movement migration toggle so the
        /// runtime owns the visible movement while the framework still owns bookkeeping.
        /// <paramref name="isCancelled"/> is polled each frame to support interruptible previews, and
        /// <paramref name="onFrame"/> receives the world position each frame (e.g. for camera follow).
        /// </summary>
        public virtual IEnumerator AnimateAlongPathVisual(
            IReadOnlyList<BoardCell> orderedPath,
            float speed,
            bool preserveZ = true,
            System.Func<bool> isCancelled = null,
            System.Action<Vector3> onFrame = null)
        {
            if (orderedPath == null || orderedPath.Count == 0)
            {
                yield break;
            }

            foreach (var step in orderedPath)
            {
                if (step == null)
                {
                    continue;
                }

                Vector3 target = step.transform.localPosition;
                if (preserveZ)
                {
                    target.z = transform.localPosition.z;
                }

                if (speed > 0f)
                {
                    while ((transform.localPosition - target).sqrMagnitude > 0.0000001f)
                    {
                        if (isCancelled != null && isCancelled())
                        {
                            yield break;
                        }

                        transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, Time.deltaTime * speed);
                        onFrame?.Invoke(transform.position);
                        yield return null;
                    }
                }

                if (isCancelled != null && isCancelled())
                {
                    yield break;
                }

                transform.localPosition = target;
                onFrame?.Invoke(transform.position);
            }
        }

        public virtual void Select()
        {
            SetState(new UnitTurnStateSelected(this));
            UnitSelected?.Invoke(this);
        }

        public virtual void Deselect()
        {
            if (IsFinishedForTurn)
            {
                SetState(new UnitTurnStateFinished(this));
            }
            else
            {
                SetState(new UnitTurnStateNormal(this));
            }

            UnitDeselected?.Invoke(this);
        }

        public virtual void ClearCurrentCell()
        {
            CurrentCell?.RemoveOccupant(this);
            CurrentCell = null;
        }

        public virtual bool BeginPendingMove(BoardCell destinationCell, IList<BoardCell> path)
        {
            CancelPendingMove();

            if (destinationCell == null || path == null || path.Count == 0)
            {
                return false;
            }

            pendingMove = new PendingMove
            {
                FromCell = CurrentCell,
                ToCell = destinationCell,
                Path = path,
                MovementPointsBefore = MovementPointsRemaining,
                MovementCost = GetPathCost(path)
            };

            return true;
        }

        public virtual bool BeginPendingMoveInPlace()
        {
            CancelPendingMove();

            if (CurrentCell == null)
            {
                return false;
            }

            pendingMove = new PendingMove
            {
                FromCell = CurrentCell,
                ToCell = CurrentCell,
                Path = new List<BoardCell> { CurrentCell },
                MovementPointsBefore = MovementPointsRemaining,
                MovementCost = 0f
            };

            return true;
        }

        public virtual bool ConfirmPendingMove(bool consumeAllRemainingMovement = true, bool syncTransform = false)
        {
            if (!pendingMove.HasValue)
            {
                return false;
            }

            PendingMove move = pendingMove.Value;
            bool isStayingInPlace = move.ToCell == move.FromCell;

            if (!isStayingInPlace && !CanStopOn(move.ToCell))
            {
                CancelPendingMove();
                return false;
            }

            if (move.ToCell != null)
            {
                if (!AssignCellImmediate(move.ToCell, syncTransform))
                {
                    CancelPendingMove();
                    return false;
                }
            }

            SetMovementPointsRemaining(
                consumeAllRemainingMovement
                    ? 0f
                    : Mathf.Max(0f, move.MovementPointsBefore - move.MovementCost));

            pendingMove = null;
            cachedPaths = null;
            return true;
        }

        public virtual bool CancelPendingMove()
        {
            if (!pendingMove.HasValue)
            {
                return false;
            }

            pendingMove = null;
            return true;
        }

        public virtual bool IsCellTraversable(BoardCell cell)
        {
            return CanTraverse(cell);
        }

        public virtual bool IsCellMovableTo(BoardCell cell)
        {
            return CanStopOn(cell);
        }

        public virtual void CachePaths(IReadOnlyList<BoardCell> cells)
        {
            cachedPaths = pathfinder.FindAllPaths(GetGraphEdges(cells), CurrentCell);
        }

        public virtual IList<BoardCell> FindPath(IReadOnlyList<BoardCell> cells, BoardCell destination)
        {
            if (CurrentCell == null || destination == null)
            {
                return Array.Empty<BoardCell>();
            }

            if (cachedPaths == null || !cachedPaths.ContainsKey(destination))
            {
                CachePaths(cells);
            }

            return cachedPaths != null && cachedPaths.TryGetValue(destination, out var path)
                ? path
                : Array.Empty<BoardCell>();
        }

        public virtual HashSet<BoardCell> GetAvailableDestinations(IReadOnlyList<BoardCell> cells)
        {
            if (CurrentCell == null)
            {
                return new HashSet<BoardCell>();
            }

            if (cachedPaths == null)
            {
                CachePaths(cells);
            }

            var result = new HashSet<BoardCell>();
            if (cachedPaths == null)
            {
                return result;
            }

            foreach (var entry in cachedPaths)
            {
                var cell = entry.Key;
                if (cell == null || cell == CurrentCell || !IsCellMovableTo(cell))
                {
                    continue;
                }

                var pathCost = GetPathCost(entry.Value);
                if (pathCost <= MovementPointsRemaining)
                {
                    result.Add(cell);
                }
            }

            return result;
        }

        protected virtual Dictionary<BoardCell, Dictionary<BoardCell, float>> GetGraphEdges(IReadOnlyList<BoardCell> cells)
        {
            var edges = new Dictionary<BoardCell, Dictionary<BoardCell, float>>();
            if (cells == null)
            {
                return edges;
            }

            foreach (var cell in cells)
            {
                if (cell == null)
                {
                    continue;
                }

                if (!IsCellTraversable(cell) && cell != CurrentCell)
                {
                    continue;
                }

                var neighbours = new Dictionary<BoardCell, float>();
                foreach (var neighbour in cell.GetNeighbours(cells))
                {
                    if (neighbour == null)
                    {
                        continue;
                    }

                    if (IsCellTraversable(neighbour) || IsCellMovableTo(neighbour))
                    {
                        neighbours[neighbour] = Mathf.Max(0f, neighbour.TraversalCost);
                    }
                }

                edges[cell] = neighbours;
            }

            return edges;
        }

        protected virtual void OnMouseDown()
        {
            Clicked?.Invoke(this);
        }

        protected virtual void OnMouseEnter()
        {
            Hovered?.Invoke(this);
        }

        protected virtual void OnMouseExit()
        {
            Unhovered?.Invoke(this);
        }

        private void CacheActions()
        {
            actions.Clear();
            actions.AddRange(GetComponentsInChildren<BattleAction>(true).Where(action => action != null));
        }

        private float GetPathCost(IList<BoardCell> path)
        {
            if (path == null || path.Count == 0)
            {
                return 0f;
            }

            var total = 0f;
            for (var i = 1; i < path.Count; i++)
            {
                var step = path[i];
                if (step != null)
                {
                    total += Mathf.Max(0f, step.TraversalCost);
                }
            }

            return total;
        }

        private void RestoreTurnState(UnitTurnStateKind turnStateKind)
        {
            UnitTurnState restoredState = turnStateKind switch
            {
                UnitTurnStateKind.Selected => new UnitTurnStateSelected(this),
                UnitTurnStateKind.ReachableEnemy => new UnitTurnStateReachableEnemy(this),
                UnitTurnStateKind.Friendly => new UnitTurnStateFriendly(this),
                UnitTurnStateKind.Finished => new UnitTurnStateFinished(this),
                _ => new UnitTurnStateNormal(this)
            };

            SetState(restoredState);
        }

        private void TryResolveCurrentCellFromBoard()
        {
            if (Board == null)
            {
                return;
            }

            BoardCell bestCell = null;
            float bestDistanceSqr = float.MaxValue;

            foreach (var cell in Board.Cells)
            {
                if (cell == null)
                {
                    continue;
                }

                float distanceSqr = (cell.transform.position - transform.position).sqrMagnitude;
                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    bestCell = cell;
                }
            }

            if (bestCell != null && bestDistanceSqr <= 0.36f)
            {
                AssignCellImmediate(bestCell);
            }
        }
    }
}
