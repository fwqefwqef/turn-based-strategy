using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Rendering;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Grid
{
    /// <summary>
    /// Square grid tile: coordinates, pathfinding, occupancy, highlights, and scene input.
    /// </summary>
    [DisallowMultipleComponent]
    public class Cell : MonoBehaviour
    {
        [SerializeField] private Vector2Int coordinates;
        [SerializeField] private bool deriveCoordinatesFromTransform = true;
        [SerializeField] private float traversalCost = 1f;
        [SerializeField] private bool isTraversable = true;
        [SerializeField] private List<Cell> explicitNeighbours = new List<Cell>();
        [SerializeField] private List<CellHighlighterBehaviour> highlighters = new List<CellHighlighterBehaviour>();

        private readonly List<GridUnit> occupants = new List<GridUnit>();
        private readonly List<Unit> currentUnits = new List<Unit>();
        private bool isTaken;
        private SpriteRenderer debugTintRenderer;
        private Color? debugTintColor;

        public event Action<Cell> Clicked;
        public event Action<Cell> Hovered;
        public event Action<Cell> Unhovered;

        public Vector2Int Coordinates => GetResolvedCoordinates();
        public float TraversalCost => Mathf.Max(0f, traversalCost);
        public bool IsTraversable => isTraversable;
        public IReadOnlyList<GridUnit> Occupants => occupants;
        public bool IsOccupied => occupants.Count > 0;
        public CellHighlightKind ActiveHighlight { get; private set; }

        public bool IsTaken
        {
            get => isTaken;
            set => isTaken = value;
        }

        public float MovementCost
        {
            get => TraversalCost;
            set => SetTraversalCost(value);
        }

        public List<Unit> CurrentUnits => currentUnits;
        public Vector2 OffsetCoord => new Vector2(Coordinates.x, Coordinates.y);

        public void SetTraversalCost(float cost)
        {
            traversalCost = Mathf.Max(0f, cost);
        }

        protected virtual void Awake()
        {
            CacheHighlightersIfNeeded();
        }

        protected virtual void OnValidate()
        {
            CacheHighlightersIfNeeded();
        }

        protected virtual void OnMouseDown()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            CellGrid grid = FindAnyObjectByType<CellGrid>();
            if (grid != null && grid.ShouldSuppressFrameworkSceneInput)
            {
                Clicked?.Invoke(this);
                return;
            }

            grid?.HandleSceneCellClicked(this);
        }

        protected virtual void OnMouseEnter()
        {
            CellGrid grid = FindAnyObjectByType<CellGrid>();
            if (grid != null && grid.ShouldSuppressFrameworkSceneInput)
            {
                Hovered?.Invoke(this);
                return;
            }

            grid?.HandleSceneCellSelected(this);
        }

        protected virtual void OnMouseExit()
        {
            CellGrid grid = FindAnyObjectByType<CellGrid>();
            if (grid != null && grid.ShouldSuppressFrameworkSceneInput)
            {
                Unhovered?.Invoke(this);
                return;
            }

            grid?.HandleSceneCellDeselected(this);
        }

        internal void RaiseSceneHighlightEvent()
        {
            FindAnyObjectByType<CellGrid>()?.HandleSceneCellSelected(this);
        }

        internal void RaiseSceneDehighlightEvent()
        {
            FindAnyObjectByType<CellGrid>()?.HandleSceneCellDeselected(this);
        }

        public virtual IEnumerable<Cell> GetNeighbours(IReadOnlyCollection<Cell> candidateCells)
        {
            if (explicitNeighbours.Count > 0)
            {
                return explicitNeighbours.Where(neighbour => neighbour != null);
            }

            if (candidateCells == null || candidateCells.Count == 0)
            {
                return Array.Empty<Cell>();
            }

            return candidateCells.Where(cell =>
                cell != null &&
                cell != this &&
                Mathf.Abs(cell.Coordinates.x - Coordinates.x) + Mathf.Abs(cell.Coordinates.y - Coordinates.y) == 1);
        }

        public List<Cell> GetNeighbours(List<Cell> cells)
        {
            IReadOnlyCollection<Cell> candidates = cells ?? (IReadOnlyCollection<Cell>)Array.Empty<Cell>();
            return GetNeighbours(candidates).ToList();
        }

        public virtual int GetDistance(Cell other)
        {
            if (other == null)
            {
                return int.MaxValue;
            }

            return Mathf.Abs(other.Coordinates.x - Coordinates.x) + Mathf.Abs(other.Coordinates.y - Coordinates.y);
        }

        public virtual Vector3 GetCellDimensions()
        {
            if (TryGetComponent<Renderer>(out var renderer))
            {
                return renderer.bounds.size;
            }

            if (TryGetComponent<Collider2D>(out var collider2D))
            {
                return collider2D.bounds.size;
            }

            return Vector3.one;
        }

        public virtual bool CanOccupy(GridUnit unit)
        {
            if (!isTraversable)
            {
                return false;
            }

            return !IsOccupied || occupants.Contains(unit);
        }

        public virtual bool TryAddOccupant(GridUnit unit)
        {
            if (unit == null || !CanOccupy(unit))
            {
                return false;
            }

            if (!occupants.Contains(unit))
            {
                occupants.Add(unit);
            }

            return true;
        }

        public virtual void RemoveOccupant(GridUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            occupants.Remove(unit);
        }

        public virtual void ClearOccupants()
        {
            occupants.Clear();
        }

        public virtual void ApplyHighlight(CellHighlightKind highlightKind)
        {
            ActiveHighlight = highlightKind;
            CacheHighlightersIfNeeded();
            foreach (var highlighter in highlighters)
            {
                highlighter?.Apply(this, highlightKind);
            }
        }

        public virtual void ClearHighlight()
        {
            ActiveHighlight = CellHighlightKind.None;
            CacheHighlightersIfNeeded();
            foreach (var highlighter in highlighters)
            {
                highlighter?.Clear(this);
            }
        }

        public void MarkAsReachable()
        {
            ApplyHighlight(CellHighlightKind.Reachable);
        }

        public void MarkAsPath()
        {
            ApplyHighlight(CellHighlightKind.Path);
        }

        public void MarkAsHighlighted()
        {
            ApplyHighlight(CellHighlightKind.Selected);
        }

        public void UnMark()
        {
            if (debugTintColor.HasValue)
            {
                RestoreDebugTint();
            }

            ClearHighlight();
        }

        public void SetColor(Color color)
        {
            CacheDebugTintRenderer();
            if (debugTintRenderer == null)
            {
                return;
            }

            if (!debugTintColor.HasValue)
            {
                debugTintColor = debugTintRenderer.color;
            }

            debugTintRenderer.color = color;
        }

        internal void RefreshOccupancyFromCurrentUnits()
        {
            bool hasBlockingUnit = currentUnits.Any(unit =>
                unit != null && unit.Obstructable && !unit.ExcludedFromBattle);
            bool hasRuntimeOccupant = Occupants.Any(unit => unit != null && unit.BlocksOtherUnits);
            isTaken = !IsTraversable || hasBlockingUnit || hasRuntimeOccupant;
        }

        private void CacheHighlightersIfNeeded()
        {
            highlighters.RemoveAll(entry => entry == null);
            if (highlighters.Count == 0)
            {
                highlighters.AddRange(GetComponents<CellHighlighterBehaviour>());
            }
        }

        private void CacheDebugTintRenderer()
        {
            if (debugTintRenderer == null)
            {
                debugTintRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void RestoreDebugTint()
        {
            if (debugTintRenderer != null && debugTintColor.HasValue)
            {
                debugTintRenderer.color = debugTintColor.Value;
            }

            debugTintColor = null;
        }

        private Vector2Int GetResolvedCoordinates()
        {
            if (!deriveCoordinatesFromTransform)
            {
                return coordinates;
            }

            Vector3 position = transform.localPosition;
            return new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));
        }
    }
}
