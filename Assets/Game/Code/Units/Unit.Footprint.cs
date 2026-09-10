using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Grid;

namespace Windy.Srpg.Game.Units
{
    public partial class Unit
    {
        public bool IsMultiTile => preset != null && preset.IsMultiTile;
        public int FootprintWidth => IsMultiTile ? Mathf.Max(1, preset.FootprintWidth) : 1;
        public int FootprintHeight => IsMultiTile ? Mathf.Max(1, preset.FootprintHeight) : 1;
        public int FootprintTileCount => FootprintWidth * FootprintHeight;
        public Cell EffectiveAnchorCell => HasPendingMove ? PreviewCell : Cell;

        public static Vector2Int GetPresetFootprintSize(UnitPreset unitPreset)
        {
            return unitPreset != null && unitPreset.IsMultiTile
                ? new Vector2Int(Mathf.Max(1, unitPreset.FootprintWidth), Mathf.Max(1, unitPreset.FootprintHeight))
                : Vector2Int.one;
        }

        public static IReadOnlyList<Cell> GetPresetFootprintCells(UnitPreset unitPreset, Cell anchor, CellGrid grid)
        {
            Vector2Int size = GetPresetFootprintSize(unitPreset);
            if (anchor == null || grid == null)
            {
                return anchor != null && size == Vector2Int.one ? new[] { anchor } : Array.Empty<Cell>();
            }

            List<Cell> footprint = new List<Cell>(size.x * size.y);
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    Cell cellAtCoordinate = grid.FindCellByCoordinates(anchor.Coordinates + new Vector2Int(x, y));
                    if (cellAtCoordinate != null)
                    {
                        footprint.Add(cellAtCoordinate);
                    }
                }
            }

            return footprint;
        }

        public static bool CanPlacePresetFootprint(UnitPreset unitPreset, Cell anchor, CellGrid grid)
        {
            Vector2Int size = GetPresetFootprintSize(unitPreset);
            IReadOnlyList<Cell> footprint = GetPresetFootprintCells(unitPreset, anchor, grid);
            return footprint.Count == size.x * size.y
                && footprint.All(cell => cell != null
                    && cell.IsTraversable
                    && (cell.CurrentUnits == null || cell.CurrentUnits.All(unit => unit == null
                        || unit.ExcludedFromBattle
                        || !unit.Obstructable)));
        }

        /// <summary>
        /// Resolves the rectangular footprint extending right/up from the lower-left anchor.
        /// Missing off-map cells are omitted; callers validating placement must compare Count
        /// with FootprintTileCount.
        /// </summary>
        public IReadOnlyList<Cell> GetFootprintCells(Cell anchorCell = null, CellGrid grid = null)
        {
            Cell anchor = anchorCell ?? Cell;
            grid ??= FindSceneCellGrid();
            if (anchor == null || grid == null)
            {
                return anchor != null && FootprintTileCount == 1
                    ? new[] { anchor }
                    : Array.Empty<Cell>();
            }

            return GetPresetFootprintCells(preset, anchor, grid);
        }

        public IReadOnlyList<Cell> GetEffectiveFootprintCells(CellGrid grid = null)
        {
            return GetFootprintCells(EffectiveAnchorCell, grid);
        }

        public bool OccupiesCell(Cell candidate, Cell anchorCell = null, CellGrid grid = null)
        {
            if (candidate == null)
            {
                return false;
            }

            return GetFootprintCells(anchorCell, grid).Any(cell => cell == candidate
                || (cell != null && cell.Coordinates == candidate.Coordinates));
        }

        public int GetFootprintDistanceTo(Unit other, Cell ownAnchor = null, Cell otherAnchor = null, CellGrid grid = null)
        {
            if (other == null)
            {
                return int.MaxValue;
            }

            grid ??= FindSceneCellGrid();
            IReadOnlyList<Cell> ownCells = GetFootprintCells(ownAnchor ?? EffectiveAnchorCell, grid);
            IReadOnlyList<Cell> otherCells = other.GetFootprintCells(otherAnchor ?? other.EffectiveAnchorCell, grid);
            if (ownCells.Count == 0 || otherCells.Count == 0)
            {
                return int.MaxValue;
            }

            int minimum = int.MaxValue;
            foreach (Cell ownCell in ownCells)
            {
                foreach (Cell otherCell in otherCells)
                {
                    minimum = Mathf.Min(minimum, ownCell.GetDistance(otherCell));
                }
            }

            return minimum;
        }

        public Vector3 GetFootprintWorldCenter(Cell anchorCell = null, CellGrid grid = null)
        {
            IReadOnlyList<Cell> footprint = GetFootprintCells(anchorCell ?? EffectiveAnchorCell, grid);
            if (footprint.Count == 0)
            {
                return transform.position;
            }

            Vector3 sum = Vector3.zero;
            foreach (Cell occupiedCell in footprint)
            {
                sum += occupiedCell.transform.position;
            }

            return sum / footprint.Count;
        }

        public Vector3 GetVisualFootprintWorldCenter()
        {
            return transform.TransformPoint(new Vector3(
                (FootprintWidth - 1) * 0.5f,
                (FootprintHeight - 1) * 0.5f,
                0f));
        }

        internal bool CanPlaceFootprint(Cell anchorCell, bool hostileOnly, CellGrid grid = null)
        {
            IReadOnlyList<Cell> footprint = GetFootprintCells(anchorCell, grid);
            if (footprint.Count != FootprintTileCount || footprint.Any(cell => cell == null || !cell.IsTraversable))
            {
                return false;
            }

            return footprint.All(cell => !HasBlockingOccupant(cell, hostileOnly, grid));
        }

        internal float GetFootprintMovementCost(Cell anchorCell)
        {
            IReadOnlyList<Cell> footprint = GetFootprintCells(anchorCell);
            return footprint.Count == FootprintTileCount
                ? footprint.Max(cell => Mathf.Max(0f, cell.MovementCost))
                : float.PositiveInfinity;
        }
    }
}
