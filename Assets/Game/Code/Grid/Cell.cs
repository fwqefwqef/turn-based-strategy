using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using Windy.Srpg.Game.UI;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Grid
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
        [SerializeField] private CellTilePreset tilePreset;
        [SerializeField] private List<Cell> explicitNeighbours = new List<Cell>();
        [SerializeField] private List<CellHighlighterBehaviour> highlighters = new List<CellHighlighterBehaviour>();

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
        public CellTilePreset TilePreset => tilePreset;
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

        public void SetTraversable(bool traversable)
        {
            isTraversable = traversable;
        }

        public void SetTilePreset(CellTilePreset preset)
        {
            tilePreset = preset;
            ApplyTilePresetIfAssigned();
        }

        internal void RefreshTilePresetFromAssetInEditor(CellTilePreset preset)
        {
            if (preset == null || tilePreset != preset)
            {
                return;
            }

            ApplyTilePresetIfAssigned();
        }

        protected virtual void Awake()
        {
            CacheHighlightersIfNeeded();
            ApplyTilePresetIfAssigned();
        }

        protected virtual void OnValidate()
        {
            CacheHighlightersIfNeeded();
            ApplyTilePresetIfAssigned();
        }

        protected virtual void OnMouseDown()
        {
            if (GameplayInputController.IsCentralizedSceneInputActive)
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Clicked?.Invoke(this);
        }

        protected virtual void OnMouseEnter()
        {
            if (GameplayInputController.IsCentralizedSceneInputActive)
            {
                return;
            }

            Hovered?.Invoke(this);
        }

        protected virtual void OnMouseExit()
        {
            if (GameplayInputController.IsCentralizedSceneInputActive)
            {
                return;
            }

            Unhovered?.Invoke(this);
        }

        internal void RaiseSceneHighlightEvent()
        {
            Hovered?.Invoke(this);
        }

        internal void RaiseSceneDehighlightEvent()
        {
            Unhovered?.Invoke(this);
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

        internal void ClearCurrentUnits()
        {
            currentUnits.Clear();
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

        public virtual void ShowCursorBorder(Color color)
        {
            CacheHighlightersIfNeeded();
            foreach (var highlighter in highlighters)
            {
                highlighter?.ShowCursorBorder(this, color);
            }
        }

        public virtual void ClearCursorBorder()
        {
            CacheHighlightersIfNeeded();
            foreach (var highlighter in highlighters)
            {
                highlighter?.ClearCursorBorder(this);
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
            Unit.RefreshCellOccupancy(this);
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

        private void ApplyTilePresetIfAssigned()
        {
            if (tilePreset == null)
            {
                return;
            }

            isTraversable = tilePreset.IsTraversable;
            traversalCost = Mathf.Max(0f, tilePreset.TraversalCost);

            if (TryGetComponent(out SpriteRenderer spriteRenderer))
            {
                spriteRenderer.sprite = tilePreset.TileSprite;
                spriteRenderer.color = Color.white;
                FitTileSpriteWithinCell(spriteRenderer);
            }
        }

        private void FitTileSpriteWithinCell(SpriteRenderer spriteRenderer)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            Sprite sprite = spriteRenderer.sprite;
            if (sprite == null)
            {
                transform.localScale = Vector3.one;
                RestoreColliderToSingleCellFootprint();
                return;
            }

            Vector2 spriteSize = sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                transform.localScale = Vector3.one;
                RestoreColliderToSingleCellFootprint();
                return;
            }

            float scaleFactor = Mathf.Min(1f / spriteSize.x, 1f / spriteSize.y);
            transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);
            RestoreColliderToSingleCellFootprint();
        }

        private void RestoreColliderToSingleCellFootprint()
        {
            if (!TryGetComponent(out BoxCollider boxCollider))
            {
                return;
            }

            Vector3 lossyScale = transform.lossyScale;
            float scaleX = Mathf.Abs(lossyScale.x) > 0.0001f ? Mathf.Abs(lossyScale.x) : 1f;
            float scaleY = Mathf.Abs(lossyScale.y) > 0.0001f ? Mathf.Abs(lossyScale.y) : 1f;
            float scaleZ = Mathf.Abs(lossyScale.z) > 0.0001f ? Mathf.Abs(lossyScale.z) : 1f;

            boxCollider.size = new Vector3(1f / scaleX, 1f / scaleY, 0.2f / scaleZ);
            boxCollider.center = Vector3.zero;
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
