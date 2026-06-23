using System.Linq;
using System;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.UI;
using Windy.Srpg.Game.Grid;

namespace Windy.Srpg.Game.Units
{
    public partial class Unit
    {
        // --- Scene binding layer ---
        public event EventHandler UnitClicked;
        public event EventHandler UnitHighlighted;
        public event EventHandler UnitDehighlighted;
        public event EventHandler UnitSelected;
        public event EventHandler UnitDeselected;
        public event EventHandler<AttackEventArgs> UnitDestroyed;

        public int UnitID { get; set; }
        public bool Obstructable = true;

        [SerializeField, HideInInspector]
        private bool excludedFromBattle;

        public bool ExcludedFromBattle
        {
            get => excludedFromBattle;
            set => excludedFromBattle = value;
        }

        [SerializeField, HideInInspector]
        private Cell cell;

        public Cell Cell
        {
            get => cell;
            set => cell = value;
        }

        [SerializeField]
        private float movementPointsStorage;

        public int PlayerNumber;
        public float MovementAnimationSpeed;

        public virtual float MovementPoints
        {
            get => movementPointsStorage;
            set => movementPointsStorage = value;
        }

        internal void RaiseUnitClicked() => UnitClicked?.Invoke(this, EventArgs.Empty);

        internal void RaiseUnitHighlighted() => UnitHighlighted?.Invoke(this, EventArgs.Empty);

        internal void RaiseUnitDehighlighted() => UnitDehighlighted?.Invoke(this, EventArgs.Empty);

        internal void RaiseUnitDestroyed(AttackEventArgs args) => UnitDestroyed?.Invoke(this, args);

        internal Cell ResolveOccupancyCell(Cell targetCell = null)
        {
            Cell sourceCell = targetCell ?? Cell;
            if (sourceCell == null)
            {
                return null;
            }

            CellGrid cellGrid = FindSceneCellGrid();
            Cell canonicalCell = cellGrid?.ResolveCanonicalCell(sourceCell) ?? sourceCell;
            if (targetCell == null || ReferenceEquals(targetCell, Cell))
            {
                cell = canonicalCell;
            }

            return canonicalCell;
        }

        internal void EnsureSceneCellBinding(bool notifyGrid = true)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (Cell == null)
            {
                CellGrid cellGrid = FindSceneCellGrid();
                Cell resolved = ResolveTransformStartCell(cellGrid, null);
                if (resolved == null)
                {
                    return;
                }

                Cell = resolved;
            }

            RegisterCellOccupancyList(Cell, notifyGrid);
        }

        internal void RegisterCellOccupancyList(Cell targetCell = null, bool notifyGrid = true)
        {
            Cell resolvedCell = ResolveOccupancyCell(targetCell);
            if (resolvedCell == null)
            {
                return;
            }

            if (!resolvedCell.CurrentUnits.Contains(this))
            {
                resolvedCell.CurrentUnits.Add(this);
            }

            RefreshCellOccupancy(resolvedCell);
            if (notifyGrid)
            {
                FindSceneCellGrid()?.NotifyOccupancyChanged();
            }
        }

        internal void UnregisterCellOccupancyList(Cell targetCell = null, bool notifyGrid = true)
        {
            Cell sourceCell = targetCell ?? Cell;
            Cell resolvedCell = FindSceneCellGrid()?.ResolveCanonicalCell(sourceCell) ?? sourceCell;
            if (resolvedCell == null)
            {
                return;
            }

            resolvedCell.CurrentUnits.Remove(this);
            RefreshCellOccupancy(resolvedCell);
            if (notifyGrid)
            {
                FindSceneCellGrid()?.NotifyOccupancyChanged();
            }
        }

        internal static void RefreshCellOccupancy(Cell cell)
        {
            if (cell == null)
            {
                return;
            }

            CellGrid cellGrid = FindAnyObjectByType<CellGrid>();
            Cell canonicalCell = cellGrid?.ResolveCanonicalCell(cell) ?? cell;

            bool hasBlockingUnit = canonicalCell.CurrentUnits != null
                && canonicalCell.CurrentUnits.Any(occupant =>
                    occupant != null && occupant.Obstructable && !occupant.ExcludedFromBattle);

            canonicalCell.IsTaken = !canonicalCell.IsTraversable || hasBlockingUnit;
        }

        internal void InvalidateCachedPaths()
        {
            cachedPaths = null;
        }

        private static CellGrid FindSceneCellGrid()
        {
            return FindAnyObjectByType<CellGrid>();
        }

        private static ExperienceGainHUD FindSceneExperienceGainHud()
        {
            return FindAnyObjectByType<ExperienceGainHUD>();
        }

        private static LevelUpUI FindSceneLevelUpUi()
        {
            return FindAnyObjectByType<LevelUpUI>();
        }

        private static CombatSequenceUI FindSceneCombatSequenceUi()
        {
            return FindAnyObjectByType<CombatSequenceUI>();
        }

        private static bool IsSceneGrid2D()
        {
            return FindSceneCellGrid()?.Is2D ?? true;
        }
    }
}
