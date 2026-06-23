using System.Linq;
using System;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.UI;
using Windy.Srpg.Runtime.Grid;

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

        internal void EnsureSceneCellBinding()
        {
            if (!Application.isPlaying || Cell != null)
            {
                return;
            }

            CellGrid cellGrid = FindSceneCellGrid();
            Cell resolved = ResolveTransformStartCell(cellGrid, null);
            if (resolved == null)
            {
                return;
            }

            Cell = resolved;
            RegisterCellOccupancyList(resolved);
        }

        internal void RegisterCellOccupancyList(Cell targetCell = null)
        {
            Cell resolvedCell = targetCell ?? Cell;
            if (resolvedCell == null)
            {
                return;
            }

            if (!resolvedCell.CurrentUnits.Contains(this))
            {
                resolvedCell.CurrentUnits.Add(this);
            }

            RefreshCellOccupancy(resolvedCell);
        }

        internal void UnregisterCellOccupancyList(Cell targetCell = null)
        {
            Cell resolvedCell = targetCell ?? Cell;
            if (resolvedCell == null)
            {
                return;
            }

            resolvedCell.CurrentUnits.Remove(this);
            RefreshCellOccupancy(resolvedCell);
        }

        internal static void RefreshCellOccupancy(Cell cell)
        {
            if (cell == null)
            {
                return;
            }

            bool hasBlockingUnit = cell.CurrentUnits != null
                && cell.CurrentUnits.Any(occupant =>
                    occupant != null && occupant.Obstructable && !occupant.ExcludedFromBattle);

            cell.IsTaken = !cell.IsTraversable || hasBlockingUnit;

            InvalidateAllCachedPaths();
        }

        internal void InvalidateCachedPaths()
        {
            cachedPaths = null;
        }

        internal static void InvalidateAllCachedPaths()
        {
            foreach (Unit unit in FindObjectsByType<Unit>())
            {
                if (unit != null)
                {
                    unit.cachedPaths = null;
                }
            }
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
