using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Grid;

namespace Windy.Srpg.Game.Units
{
    public readonly struct DisplacementResult
    {
        public readonly BattleSquareCell UserStartCell;
        public readonly BattleSquareCell UserEndCell;
        public readonly BattleSquareCell TargetStartCell;
        public readonly BattleSquareCell TargetEndCell;
        public readonly int UserStepsMoved;
        public readonly int TargetStepsMoved;

        public bool Success => TargetStepsMoved > 0 || UserStepsMoved > 0;

        public DisplacementResult(
            BattleSquareCell userStartCell,
            BattleSquareCell userEndCell,
            BattleSquareCell targetStartCell,
            BattleSquareCell targetEndCell,
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
            BattleSquareCell userCell,
            Unit target,
            BattleSquareCell targetCell,
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
            BattleSquareCell userCell = user?.Cell;
            BattleSquareCell targetCell = target?.Cell;
            if (!PlanDisplacement(user, userCell, target, targetCell, cellGrid, distance, push, moveUserWithTarget, out var plan))
            {
                return new DisplacementResult(userCell, userCell, targetCell, targetCell, 0, 0);
            }

            ApplyDisplacementPlan(user, target, cellGrid, plan);
            return new DisplacementResult(plan.UserStartCell, plan.UserCell, plan.TargetStartCell, plan.TargetCell, plan.UserStepsMoved, plan.TargetStepsMoved);
        }

        private sealed class DisplacementPlan
        {
            public BattleSquareCell UserStartCell;
            public BattleSquareCell UserCell;
            public BattleSquareCell TargetStartCell;
            public BattleSquareCell TargetCell;
            public int UserStepsMoved;
            public int TargetStepsMoved;
        }

        private static bool PlanDisplacement(
            Unit user,
            BattleSquareCell userCell,
            Unit target,
            BattleSquareCell targetCell,
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
                if (!TryPlanStep(user, target, cellGrid, workingPlan.UserCell, workingPlan.TargetCell, push, moveUserWithTarget, out BattleSquareCell nextUserCell, out BattleSquareCell nextTargetCell))
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
            BattleSquareCell userCell,
            BattleSquareCell targetCell,
            bool push,
            bool moveUserWithTarget,
            out BattleSquareCell nextUserCell,
            out BattleSquareCell nextTargetCell)
        {
            nextUserCell = userCell;
            nextTargetCell = targetCell;

            List<BattleSquareCell> allCells = cellGrid?.GetAllBoardCells() ?? new List<BattleSquareCell>();
            List<BattleSquareCell> neighbours = targetCell.GetNeighbours(allCells);
            if (neighbours == null || neighbours.Count == 0)
            {
                return false;
            }

            double currentDistance = GetWeightedDistance(userCell, targetCell);
            BattleSquareCell bestTargetCell = null;
            BattleSquareCell bestUserCell = userCell;
            double bestDistance = push ? double.NegativeInfinity : double.PositiveInfinity;
            int bestManhattan = push ? int.MinValue : int.MaxValue;
            float bestEuclidean = push ? float.MinValue : float.MaxValue;

            foreach (BattleSquareCell candidateTargetCell in neighbours)
            {
                if (candidateTargetCell == null)
                {
                    continue;
                }

                Vector2 stepVector = candidateTargetCell.OffsetCoord - targetCell.OffsetCoord;
                BattleSquareCell candidateUserCell = userCell;
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

                if (!CanOccupyCandidate(candidateTargetCell, user, target, moveUserWithTarget ? userCell : null))
                {
                    continue;
                }

                if (moveUserWithTarget && !CanOccupyCandidate(candidateUserCell, user, target, targetCell))
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
            BattleSquareCell originalUserCell = user.Cell;
            BattleSquareCell originalTargetCell = target.Cell;

            if (originalUserCell != null)
            {
                originalUserCell.CurrentUnits.Remove(user);
                RefreshCellOccupancy(originalUserCell);
            }

            if (originalTargetCell != null)
            {
                originalTargetCell.CurrentUnits.Remove(target);
                RefreshCellOccupancy(originalTargetCell);
            }

            user.Cell = plan.UserCell;
            target.Cell = plan.TargetCell;

            if (plan.UserCell != null)
            {
                plan.UserCell.CurrentUnits.Add(user);
                RefreshCellOccupancy(plan.UserCell);
            }

            if (plan.TargetCell != null)
            {
                plan.TargetCell.CurrentUnits.Add(target);
                RefreshCellOccupancy(plan.TargetCell);
            }

            user.SyncMirroredRuntimeCell(plan.UserCell);
            target.SyncMirroredRuntimeCell(plan.TargetCell);

            SnapUnitToCell(user, plan.UserCell, cellGrid);
            SnapUnitToCell(target, plan.TargetCell, cellGrid);
            cellGrid?.RequestBattleOutcomeEvaluation();
        }

        private static bool CanOccupyCandidate(BattleSquareCell candidateCell, Unit user, Unit target, BattleSquareCell simultaneouslyVacatedCell)
        {
            if (candidateCell == null)
            {
                return false;
            }

            if (candidateCell == simultaneouslyVacatedCell)
            {
                return !HasExternalBlockingOccupant(candidateCell, user, target);
            }

            if (candidateCell.IsTaken)
            {
                return !HasExternalBlockingOccupant(candidateCell, user, target)
                    && candidateCell.CurrentUnits.All(unit => unit == null || unit == user || unit == target || !unit.Obstructable);
            }

            return !HasExternalBlockingOccupant(candidateCell, user, target);
        }

        private static bool HasExternalBlockingOccupant(BattleSquareCell cell, Unit user, Unit target)
        {
            return cell.CurrentUnits
                .Any(unit => unit != null && unit != user && unit != target && unit.Obstructable);
        }

        private static void RefreshCellOccupancy(BattleSquareCell cell)
        {
            Unit.RefreshCellOccupancy(cell);
        }

        private static void SnapUnitToCell(Unit unit, BattleSquareCell destinationCell, CellGrid cellGrid)
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

        private static BattleSquareCell FindCell(CellGrid cellGrid, Vector2 offsetCoord)
        {
            return cellGrid?.FindCellByOffset(offsetCoord);
        }

        private static double GetWeightedDistance(BattleSquareCell first, BattleSquareCell second)
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

