using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.UI;
using Windy.Srpg.Runtime.Grid;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Units
{
    public partial class Unit
    {
        // --- Scene-facing surface (events, cell binding) ---
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
            set => SetMovementPoints(value, syncRuntimeMirror: true);
        }

        internal void SetMovementPoints(float value, bool syncRuntimeMirror)
        {
            movementPointsStorage = value;
            if (syncRuntimeMirror)
            {
                SyncMirroredRuntimeMovementPoints();
            }
        }

        internal void RaiseUnitClicked() => UnitClicked?.Invoke(this, EventArgs.Empty);

        internal void RaiseUnitHighlighted() => UnitHighlighted?.Invoke(this, EventArgs.Empty);

        internal void RaiseUnitDehighlighted() => UnitDehighlighted?.Invoke(this, EventArgs.Empty);

        internal void RaiseUnitDestroyed(AttackEventArgs args) => UnitDestroyed?.Invoke(this, args);

        internal void RegisterCellOccupancy(Cell targetCell = null)
        {
            Cell resolvedCell = targetCell ?? Cell;
            if (resolvedCell == null)
            {
                return;
            }

            GridUnit runtimeUnit = GetComponent<GridUnit>();
            runtimeUnit?.AssignCellImmediate(resolvedCell, syncTransform: false);
        }

        internal void UnregisterCellOccupancy(Cell targetCell = null)
        {
            Cell resolvedCell = targetCell ?? Cell;
            GridUnit runtimeUnit = GetComponent<GridUnit>();
            if (runtimeUnit != null && runtimeUnit.CurrentCell == resolvedCell)
            {
                runtimeUnit.ClearCurrentCell();
            }
        }
        // --- Runtime grid sync ---
        internal void SyncMirroredRuntimeCell(Cell battleCell)
        {
            GridUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            if (battleCell == null)
            {
                runtimeUnit.ClearCurrentCell();
                return;
            }

            runtimeUnit.AssignCellImmediate(battleCell, syncTransform: false);
        }

        internal void ClearMirroredRuntimeCell()
        {
            GridUnit runtimeUnit = ResolveRuntimeUnit();
            runtimeUnit?.ClearCurrentCell();
        }

        internal void PrepareRuntimeForTurnStart()
        {
            GridUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            runtimeUnit.SetBaseMovementPoints(ComputedTotalMovementPoints);
        }

        internal void ApplySceneTurnStartFromRuntime()
        {
            cachedPaths = null;

            PassiveList?.OnTurnStart();
            BuffList?.OnTurnStart();
            RefreshHealthState();
            SkillList?.ResetTurnUsage();
            RaiseBuffsChanged();

            GridUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                MovementPoints = ComputedTotalMovementPoints;
                SetTurnStateKind(UnitTurnStateKind.Friendly, syncRuntime: false);
                return;
            }

            MovementPoints = runtimeUnit.MovementPointsRemaining;
            UnitTurnStateKind turnState = ResolveSceneTurnStateFromRuntime(runtimeUnit);
            if (turnState == UnitTurnStateKind.Normal)
            {
                turnState = UnitTurnStateKind.Friendly;
            }

            SetTurnStateKind(turnState, syncRuntime: false);
        }

        internal void SyncMirroredRuntimeNow()
        {
            if (HasPendingMove)
            {
                PushSceneStateToRuntimeMirror();
                return;
            }

            if (ShouldPullSceneFromRuntimeMirror())
            {
                PullRuntimeStateToScene(refreshOccupancy: true);
                return;
            }

            PushSceneStateToRuntimeMirror();
        }

        internal void PullRuntimeStateToScene(bool refreshOccupancy = true)
        {
            GridUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            MovementPoints = runtimeUnit.MovementPointsRemaining;
            SetTurnStateKind(ResolveSceneTurnStateFromRuntime(runtimeUnit), syncRuntime: false);

            Cell resolvedCell = runtimeUnit.CurrentCell;
            if (resolvedCell != Cell)
            {
                UnregisterCellOccupancyList(Cell);
                RefreshCellOccupancy(Cell);
                Cell = resolvedCell;
                RegisterCellOccupancyList(resolvedCell);
            }

            if (refreshOccupancy)
            {
                RefreshCellOccupancy(resolvedCell);
                RefreshSceneOccupancyFromLiveUnits();
            }

            cachedPaths = null;
        }

        internal void ApplySceneSyncFromRuntimeMoveCommit(CellGrid cellGrid)
        {
            PullRuntimeStateToScene();
            OnMoveFinished();
            cellGrid?.RequestBattleOutcomeEvaluation();
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

            resolvedCell.RefreshOccupancyFromCurrentUnits();
            RegisterCellOccupancy(resolvedCell);
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
            UnregisterCellOccupancy(resolvedCell);
        }

        internal static void RefreshCellOccupancy(Cell cell)
        {
            if (cell == null)
            {
                return;
            }

            bool hasLegacyOccupant = cell.CurrentUnits != null
                && cell.CurrentUnits.Any(occupant =>
                    occupant != null && occupant.Obstructable && !occupant.ExcludedFromBattle);
            bool hasSceneOccupant = HasBlockingSceneOccupant(cell, null);
            bool hasRuntimeOccupant = cell.Occupants.Any(unit => unit != null && unit.BlocksOtherUnits);

            cell.IsTaken = (!cell.IsTraversable)
                || hasLegacyOccupant
                || hasSceneOccupant
                || hasRuntimeOccupant;

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

        private UnitTurnStateKind ResolveSceneTurnStateFromRuntime(GridUnit runtimeUnit)
        {
            if (runtimeUnit == null)
            {
                return UnitTurnStateKind.Normal;
            }

            UnitTurnStateKind runtimeKind = runtimeUnit.CurrentTurnStateKind;
            CellGrid cellGrid = FindSceneCellGrid();
            if (cellGrid == null || !cellGrid.IsBattleStarted)
            {
                return runtimeKind;
            }

            if (PlayerNumber != cellGrid.CurrentPlayerNumber)
            {
                return runtimeKind == UnitTurnStateKind.ReachableEnemy
                    ? UnitTurnStateKind.ReachableEnemy
                    : UnitTurnStateKind.Normal;
            }

            return runtimeKind;
        }

        private void PushSceneStateToRuntimeMirror()
        {
            SyncMirroredRuntimeMovementPoints();
            SyncMirroredRuntimeTurnState();
            SyncMirroredRuntimeCell(Cell);
            SyncMirroredRuntimePendingMove();
        }

        private bool ShouldPullSceneFromRuntimeMirror()
        {
            CellGrid cellGrid = FindSceneCellGrid();
            return cellGrid != null
                && cellGrid.IsBattleStarted
                && ResolveRuntimeUnit() != null;
        }

        private void SyncMirroredRuntimePendingMove()
        {
            GridUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            if (!HasPendingMove)
            {
                if (runtimeUnit.HasPendingMove)
                {
                    runtimeUnit.CancelPendingMove();
                }

                return;
            }

            PendingMove pending = _pendingMove.Value;
            Cell runtimeDestination = pending.ToCell;
            if (runtimeDestination == null)
            {
                return;
            }

            if (runtimeUnit.HasPendingMove && runtimeUnit.PreviewCell == runtimeDestination)
            {
                return;
            }

            if (pending.ToCell == pending.FromCell)
            {
                runtimeUnit.BeginPendingMoveInPlace();
                return;
            }

            if (TryBuildRuntimeMovementPath(pending.Path, out List<Cell> runtimePath))
            {
                runtimeUnit.BeginPendingMove(runtimeDestination, runtimePath);
            }
        }

        private void SyncMirroredRuntimeTurnState()
        {
            GridUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            UnitTurnState runtimeState = CurrentTurnStateKind switch
            {
                UnitTurnStateKind.Selected => new UnitTurnStateSelected(runtimeUnit),
                UnitTurnStateKind.ReachableEnemy => new UnitTurnStateReachableEnemy(runtimeUnit),
                UnitTurnStateKind.Friendly => new UnitTurnStateFriendly(runtimeUnit),
                UnitTurnStateKind.Finished => new UnitTurnStateFinished(runtimeUnit),
                _ => new UnitTurnStateNormal(runtimeUnit)
            };

            runtimeUnit.SetState(runtimeState);
        }

        private void SyncMirroredRuntimeMovementPoints()
        {
            GridUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            runtimeUnit.SetMovementPointsRemaining(MovementPoints);
        }

        private bool HasBlockingRuntimeOccupant(Cell cell)
        {
            if (cell == null)
            {
                return false;
            }

            GridUnit runtimeUnit = ResolveRuntimeUnit();
            foreach (GridUnit occupant in cell.Occupants)
            {
                if (occupant == null || occupant == runtimeUnit || !occupant.BlocksOtherUnits)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool IsLinkedCellTraversable(Cell cell)
        {
            return cell == null || cell.IsTraversable;
        }

        private bool TryBuildRuntimeMovementPath(IList<Cell> movementPath, out List<Cell> orderedRuntimePath)
        {
            orderedRuntimePath = null;
            if (movementPath == null || movementPath.Count == 0)
            {
                return false;
            }

            var path = new List<Cell>(movementPath.Count);
            for (int i = movementPath.Count - 1; i >= 0; i--)
            {
                Cell runtimeCell = movementPath[i];
                if (runtimeCell == null)
                {
                    return false;
                }

                path.Add(runtimeCell);
            }

            orderedRuntimePath = path;
            return true;
        }

        private static bool TryBuildSceneMovementPath(
            IList<Cell> runtimePath,
            Cell originCell,
            out List<Cell> orderedPath)
        {
            orderedPath = null;
            if (runtimePath == null || runtimePath.Count == 0)
            {
                return false;
            }

            var path = new List<Cell>(runtimePath.Count);
            for (int i = runtimePath.Count - 1; i >= 0; i--)
            {
                Cell cell = runtimePath[i];
                if (originCell != null && cell == originCell)
                {
                    continue;
                }

                if (cell == null)
                {
                    return false;
                }

                path.Add(cell);
            }

            orderedPath = path;
            return true;
        }

        private bool TryUseRuntimeMovementAuthority(out CellGrid cellGrid, out GridUnit runtimeUnit)
        {
            cellGrid = FindSceneCellGrid();
            runtimeUnit = ResolveRuntimeUnit();
            return Application.isPlaying
                && cellGrid != null
                && runtimeUnit != null;
        }

        private bool TryUseRuntimePathAuthority(out CellGrid cellGrid, out GridUnit runtimeUnit)
        {
            cellGrid = FindSceneCellGrid();
            runtimeUnit = ResolveRuntimeUnit();
            return TryUseRuntimeMovementAuthority(out cellGrid, out runtimeUnit);
        }

        private static void RefreshSceneOccupancyFromLiveUnits()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            FindSceneCellGrid()?.RefreshSceneCellOccupancyNow();
        }

        private static bool HasBlockingSceneOccupant(Cell cell, Unit self)
        {
            if (cell == null)
            {
                return false;
            }

            CellGrid cellGrid = FindSceneCellGrid();
            if (cellGrid == null)
            {
                return false;
            }

            foreach (Unit unit in cellGrid.GetAllSceneUnitsFromHierarchy())
            {
                if (unit == null || unit == self || unit.Cell != cell || !unit.Obstructable || unit.ExcludedFromBattle)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private GridUnit ResolveRuntimeUnit()
        {
            return GetComponent<GridUnit>();
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
