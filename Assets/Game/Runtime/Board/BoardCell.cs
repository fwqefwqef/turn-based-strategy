using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Runtime.Rendering;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Board
{
    public class BoardCell : MonoBehaviour, IBattleCell
    {
        [SerializeField] private Vector2Int coordinates;
        [SerializeField] private bool deriveCoordinatesFromTransform = true;
        [SerializeField] private float traversalCost = 1f;
        [SerializeField] private bool isTraversable = true;
        [SerializeField] private List<BoardCell> explicitNeighbours = new List<BoardCell>();
        [SerializeField] private List<CellHighlighterBehaviour> highlighters = new List<CellHighlighterBehaviour>();

        private readonly List<BattleUnit> occupants = new List<BattleUnit>();

        public event Action<BoardCell> Clicked;
        public event Action<BoardCell> Hovered;
        public event Action<BoardCell> Unhovered;

        public Vector2Int Coordinates => GetResolvedCoordinates();
        public float TraversalCost => Mathf.Max(0f, traversalCost);
        public bool IsTraversable => isTraversable;
        public IReadOnlyList<BattleUnit> Occupants => occupants;
        public bool IsOccupied => occupants.Count > 0;
        public CellHighlightKind ActiveHighlight { get; private set; }

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

        public virtual IEnumerable<BoardCell> GetNeighbours(IReadOnlyCollection<BoardCell> candidateCells)
        {
            if (explicitNeighbours.Count > 0)
            {
                return explicitNeighbours.Where(cell => cell != null);
            }

            if (candidateCells == null || candidateCells.Count == 0)
            {
                return Array.Empty<BoardCell>();
            }

            return candidateCells.Where(cell =>
                cell != null &&
                cell != this &&
                Mathf.Abs(cell.Coordinates.x - Coordinates.x) + Mathf.Abs(cell.Coordinates.y - Coordinates.y) == 1);
        }

        public virtual int GetDistance(BoardCell other)
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

        public virtual bool CanOccupy(BattleUnit unit)
        {
            if (!isTraversable)
            {
                return false;
            }

            return !IsOccupied || occupants.Contains(unit);
        }

        public virtual bool TryAddOccupant(BattleUnit unit)
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

        public virtual void RemoveOccupant(BattleUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            occupants.Remove(unit);
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

        private void CacheHighlightersIfNeeded()
        {
            highlighters.RemoveAll(entry => entry == null);
            if (highlighters.Count == 0)
            {
                highlighters.AddRange(GetComponents<CellHighlighterBehaviour>());
            }
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
