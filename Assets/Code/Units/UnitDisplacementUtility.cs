using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Grid;

namespace Windy.Srpg.Game.Units
{
    public readonly struct DisplacementResult
    {
        public readonly Cell UserStartCell;
        public readonly Cell UserEndCell;
        public readonly Cell TargetStartCell;
        public readonly Cell TargetEndCell;
        public readonly int UserStepsMoved;
        public readonly int TargetStepsMoved;

        public bool Success => TargetStepsMoved > 0 || UserStepsMoved > 0;

        public DisplacementResult(
            Cell userStartCell,
            Cell userEndCell,
            Cell targetStartCell,
            Cell targetEndCell,
            int userStepsMoved,
            int targetStepsMoved)
        {
            UserStartCell = userStartCell;
            UserEndCell = userEndCell;
            TargetStartCell = targetStartCell;
            TargetEndCell = targetEndCell;
            UserStepsMoved = userStepsMoved;
            TargetStepsMoved = targetStepsMoved;
        }
    }

    public static class UnitDisplacementUtility
    {
        public static bool CanDisplaceRelative(
            Unit user,
            Cell userCell,
            Unit target,
            Cell targetCell,
            CellGrid cellGrid,
            int distance = 1,
            bool push = true,
            bool moveUserWithTarget = false)
        {
            return PlanDisplacement(user, userCell, target, targetCell, cellGrid, distance, push, moveUserWithTarget, out _);
        }

        public static DisplacementResult TryDisplaceRelative(
            Unit user,
            Unit target,
            CellGrid cellGrid,
            int distance = 1,
            bool push = true,
            bool moveUserWithTarget = false)
        {
            Cell userCell = user?.Cell;
            Cell targetCell = target?.Cell;
            if (!PlanDisplacement(user, userCell, target, targetCell, cellGrid, distance, push, moveUserWithTarget, out var plan))
            {
                return new DisplacementResult(userCell, userCell, targetCell, targetCell, 0, 0);
            }

            ApplyDisplacementPlan(user, target, cellGrid, plan);
            return new DisplacementResult(plan.UserStartCell, plan.UserCell, plan.TargetStartCell, plan.TargetCell, plan.UserStepsMoved, plan.TargetStepsMoved);
        }

        private sealed class DisplacementPlan
        {
            public Cell UserStartCell;
            public Cell UserCell;
            public Cell TargetStartCell;
            public Cell TargetCell;
            public int UserStepsMoved;
            public int TargetStepsMoved;
        }

        private static bool PlanDisplacement(
            Unit user,
            Cell userCell,
            Unit target,
            Cell targetCell,
            CellGrid cellGrid,
            int distance,
            bool push,
            bool moveUserWithTarget,
            out DisplacementPlan plan)
        {
            plan = null;
            if (user == null || target == null || userCell == null || targetCell == null || cellGrid == null || distance <= 0)
            {
                return false;
            }

            var workingPlan = new DisplacementPlan
            {
                UserStartCell = userCell,
                UserCell = userCell,
                TargetStartCell = targetCell,
                TargetCell = targetCell,
                UserStepsMoved = 0,
                TargetStepsMoved = 0
            };

            for (int i = 0; i < distance; i++)
            {
                if (!TryPlanStep(user, target, cellGrid, workingPlan.UserCell, workingPlan.TargetCell, push, moveUserWithTarget, out Cell nextUserCell, out Cell nextTargetCell))
                {
                    break;
                }

                if (nextTargetCell == workingPlan.TargetCell && nextUserCell == workingPlan.UserCell)
                {
                    break;
                }

                if (nextTargetCell != workingPlan.TargetCell)
                {
                    workingPlan.TargetCell = nextTargetCell;
                    workingPlan.TargetStepsMoved++;
                }

                if (nextUserCell != workingPlan.UserCell)
                {
                    workingPlan.UserCell = nextUserCell;
                    workingPlan.UserStepsMoved++;
                }
            }

            if (workingPlan.TargetStepsMoved <= 0)
            {
                return false;
            }

            plan = workingPlan;
            return true;
        }

        private static bool TryPlanStep(
            Unit user,
            Unit target,
            CellGrid cellGrid,
            Cell userCell,
            Cell targetCell,
            bool push,
            bool moveUserWithTarget,
            out Cell nextUserCell,
            out Cell nextTargetCell)
        {
            nextUserCell = userCell;
            nextTargetCell = targetCell;

            List<Cell> allCells = cellGrid?.GetAllCells() ?? new List<Cell>();
            List<Cell> neighbours = targetCell.GetNeighbours(allCells);
            if (neighbours == null || neighbours.Count == 0)
            {
                return false;
            }

            double currentDistance = GetWeightedDistance(userCell, targetCell);
            Cell bestTargetCell = null;
            Cell bestUserCell = userCell;
            double bestDistance = push ? double.NegativeInfinity : double.PositiveInfinity;
            int bestManhattan = push ? int.MinValue : int.MaxValue;
            float bestEuclidean = push ? float.MinValue : float.MaxValue;

            foreach (Cell candidateTargetCell in neighbours)
            {
                if (candidateTargetCell == null)
                {
                    continue;
                }

                Vector2 stepVector = candidateTargetCell.OffsetCoord - targetCell.OffsetCoord;
                Cell candidateUserCell = userCell;
                if (moveUserWithTarget)
                {
                    candidateUserCell = FindCell(cellGrid, userCell.OffsetCoord + stepVector);
                    if (candidateUserCell == null || candidateUserCell == candidateTargetCell)
                    {
                        continue;
                    }
                }
                else if (candidateTargetCell == userCell)
                {
                    continue;
                }

                HashSet<Unit> vacatingUnits = moveUserWithTarget
                    ? new HashSet<Unit> { user, target }
                    : new HashSet<Unit> { target };
                if (!CanOccupyCandidate(target, candidateTargetCell, cellGrid, vacatingUnits))
                {
                    continue;
                }

                if (moveUserWithTarget && !CanOccupyCandidate(user, candidateUserCell, cellGrid, vacatingUnits))
                {
                    continue;
                }

                if (moveUserWithTarget && target.GetFootprintCells(candidateTargetCell, cellGrid)
                    .Intersect(user.GetFootprintCells(candidateUserCell, cellGrid)).Any())
                {
                    continue;
                }

                double candidateDistance = GetWeightedDistance(userCell, candidateTargetCell);
                if (push)
                {
                    if (candidateDistance <= currentDistance)
                    {
                        continue;
                    }
                }
                else
                {
                    if (candidateDistance >= currentDistance)
                    {
                        continue;
                    }
                }

                int candidateManhattan = userCell.GetDistance(candidateTargetCell);
                float candidateEuclidean = (candidateTargetCell.transform.position - userCell.transform.position).sqrMagnitude;

                bool isBetter = push
                    ? candidateDistance > bestDistance
                        || (Math.Abs(candidateDistance - bestDistance) < 0.0001d && candidateManhattan > bestManhattan)
                        || (Math.Abs(candidateDistance - bestDistance) < 0.0001d && candidateManhattan == bestManhattan && candidateEuclidean > bestEuclidean)
                    : candidateDistance < bestDistance
                        || (Math.Abs(candidateDistance - bestDistance) < 0.0001d && candidateManhattan < bestManhattan)
                        || (Math.Abs(candidateDistance - bestDistance) < 0.0001d && candidateManhattan == bestManhattan && candidateEuclidean < bestEuclidean);

                if (!isBetter)
                {
                    continue;
                }

                bestDistance = candidateDistance;
                bestManhattan = candidateManhattan;
                bestEuclidean = candidateEuclidean;
                bestTargetCell = candidateTargetCell;
                bestUserCell = candidateUserCell;
            }

            if (bestTargetCell == null)
            {
                return false;
            }

            nextUserCell = bestUserCell;
            nextTargetCell = bestTargetCell;
            return true;
        }

        private static void ApplyDisplacementPlan(Unit user, Unit target, CellGrid cellGrid, DisplacementPlan plan)
        {
            Cell originalUserCell = user.Cell;
            Cell originalTargetCell = target.Cell;

            user.UnregisterCellOccupancyList(originalUserCell, notifyGrid: false);
            target.UnregisterCellOccupancyList(originalTargetCell, notifyGrid: false);

            user.Cell = plan.UserCell;
            target.Cell = plan.TargetCell;

            user.RegisterCellOccupancyList(plan.UserCell, notifyGrid: false);
            target.RegisterCellOccupancyList(plan.TargetCell, notifyGrid: false);

            SnapUnitToCell(user, plan.UserCell, cellGrid);
            SnapUnitToCell(target, plan.TargetCell, cellGrid);
            cellGrid?.NotifyOccupancyChanged();
            cellGrid?.RequestBattleOutcomeEvaluation();
        }

        private static bool CanOccupyCandidate(Unit movingUnit, Cell candidateCell, CellGrid cellGrid, ISet<Unit> vacatingUnits)
        {
            if (movingUnit == null || candidateCell == null || cellGrid == null)
            {
                return false;
            }

            IReadOnlyList<Cell> footprint = movingUnit.GetFootprintCells(candidateCell, cellGrid);
            if (footprint.Count != movingUnit.FootprintTileCount || footprint.Any(cell => cell == null || !cell.IsTraversable))
            {
                return false;
            }

            return footprint.All(cell => cell.CurrentUnits == null || cell.CurrentUnits.All(unit =>
                unit == null
                || unit == movingUnit
                || (vacatingUnits != null && vacatingUnits.Contains(unit))
                || unit.ExcludedFromBattle
                || !unit.Obstructable));
        }

        private static void SnapUnitToCell(Unit unit, Cell destinationCell, CellGrid cellGrid)
        {
            if (unit == null || destinationCell == null)
            {
                return;
            }

            Vector3 destinationLocal = destinationCell.transform.localPosition;
            if (cellGrid != null && cellGrid.Is2D)
            {
                destinationLocal = new Vector3(destinationLocal.x, destinationLocal.y, unit.transform.localPosition.z);
            }

            unit.transform.localPosition = destinationLocal;
        }

        private static Cell FindCell(CellGrid cellGrid, Vector2 offsetCoord)
        {
            return cellGrid?.FindCellByOffset(offsetCoord);
        }

        private static double GetWeightedDistance(Cell first, Cell second)
        {
            if (first == null || second == null)
            {
                return 0d;
            }

            float dx = Mathf.Abs(first.OffsetCoord.x - second.OffsetCoord.x);
            float dy = Mathf.Abs(first.OffsetCoord.y - second.OffsetCoord.y);
            float diagonalSteps = Mathf.Min(dx, dy);
            float straightSteps = Mathf.Max(dx, dy) - diagonalSteps;
            return diagonalSteps * 1.5d + straightSteps;
        }
    }
}

