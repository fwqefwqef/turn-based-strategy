using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid
{
    public sealed partial class BattleSquareCell
    {
        private readonly List<Unit> currentUnits = new List<Unit>();
        private bool isTaken;
        private SpriteRenderer debugTintRenderer;
        private Color? debugTintColor;

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

        public List<BattleSquareCell> GetNeighbours(List<BattleSquareCell> cells)
        {
            IReadOnlyCollection<BoardCell> candidates = cells ?? (IReadOnlyCollection<BoardCell>)System.Array.Empty<BoardCell>();
            return GetNeighbours(candidates)
                .OfType<BattleSquareCell>()
                .ToList();
        }

        public int GetDistance(BattleSquareCell other)
        {
            return GetDistance((BoardCell)other);
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
    }
}

