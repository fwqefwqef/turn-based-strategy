using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.AI
{
    internal static class AiBehaviorUtility
    {
        public static bool ShouldAllowMovement(Unit unit, Player player, CellGrid grid)
        {
            if (unit == null)
            {
                return false;
            }

            return unit.MovementAiMode switch
            {
                UnitMovementAiMode.Move => true,
                UnitMovementAiMode.NotMove => false,
                UnitMovementAiMode.Wait => ShouldAllowTriggeredMovement(unit, player, grid, EnsureWaitTriggered),
                UnitMovementAiMode.WaitGroup => ShouldAllowTriggeredMovement(unit, player, grid, EnsureWaitGroupTriggered),
                UnitMovementAiMode.Goal => true,
                _ => true
            };
        }

        public static bool ShouldAllowAction(Unit unit, Player player, CellGrid grid)
        {
            if (unit == null)
            {
                return false;
            }

            return unit.MovementAiMode switch
            {
                UnitMovementAiMode.Wait => EnsureWaitTriggered(unit, player, grid),
                UnitMovementAiMode.WaitGroup => EnsureWaitGroupTriggered(unit, player, grid),
                UnitMovementAiMode.Goal => AiGoalPlanner.HasReachedGoal(unit, grid)
                    || (AiGoalPlanner.TryPlan(unit, grid, out _, out bool attack) && attack),
                _ => true
            };
        }

        private static bool EnsureWaitTriggered(Unit unit, Player player, CellGrid grid)
        {
            if (unit.IsAiWaitTriggered)
            {
                return true;
            }

            bool hasThreatInRange = AiCombatPlanner.HasAnyOffensivePlanFromReachableCells(unit, player, grid, out Cell triggerCell);
            if (!hasThreatInRange)
            {
                return false;
            }

            unit.ActivateAiWaitState();
            return true;
        }

        private static bool ShouldAllowTriggeredMovement(Unit unit, Player player, CellGrid grid, System.Func<Unit, Player, CellGrid, bool> triggerResolver)
        {
            if (unit == null)
            {
                return false;
            }

            if (unit.IsAiWaitTriggered)
            {
                return true;
            }

            bool triggered = triggerResolver(unit, player, grid);
            bool hasOffensivePlanHere = AiCombatPlanner.HasAnyOffensivePlan(unit, player, grid, unit.Cell);
            return triggered && !hasOffensivePlanHere;
        }

        private static bool EnsureWaitGroupTriggered(Unit unit, Player player, CellGrid grid)
        {
            if (unit.IsAiWaitTriggered)
            {
                return true;
            }

            if (unit.WaitGroupId < 0 || player == null || grid == null)
            {
                return false;
            }

            List<Unit> groupUnits = grid.GetUnitsForPlayer(player)
                .Where(candidate =>
                    candidate != null
                    && candidate.IsAliveForBattle
                    && !candidate.ExcludedFromBattle
                    && candidate.WaitGroupId == unit.WaitGroupId
                    && candidate.MovementAiMode == UnitMovementAiMode.WaitGroup)
                .ToList();

            if (groupUnits.Count == 0)
            {
                return false;
            }

            if (groupUnits.Count == 1)
            {
                return EnsureWaitTriggered(unit, player, grid);
            }

            if (!CanAtLeastTwoGroupUnitsAttackSimultaneously(groupUnits, player, grid))
            {
                return false;
            }

            foreach (Unit groupUnit in groupUnits)
            {
                groupUnit.ActivateAiWaitState();
            }

            return true;
        }

        private static bool CanAtLeastTwoGroupUnitsAttackSimultaneously(
            IReadOnlyList<Unit> groupUnits,
            Player player,
            CellGrid grid)
        {
            var threatCellsByUnit = new List<(Unit Unit, IReadOnlyList<Cell> ThreatCells)>();
            foreach (Unit groupUnit in groupUnits)
            {
                if (AiCombatPlanner.TryGetPreferredOffensiveThreatCell(
                        groupUnit,
                        player,
                        grid,
                        out Cell preferredCell))
                {
                    threatCellsByUnit.Add((groupUnit, new[] { preferredCell }));
                }
            }

            if (threatCellsByUnit.Count < 2)
            {
                return false;
            }

            for (int firstIndex = 0; firstIndex < threatCellsByUnit.Count - 1; firstIndex++)
            {
                (Unit firstUnit, IReadOnlyList<Cell> firstThreatCells) = threatCellsByUnit[firstIndex];
                for (int secondIndex = firstIndex + 1; secondIndex < threatCellsByUnit.Count; secondIndex++)
                {
                    (Unit secondUnit, IReadOnlyList<Cell> secondThreatCells) = threatCellsByUnit[secondIndex];
                    foreach (Cell firstCell in firstThreatCells)
                    {
                        foreach (Cell secondCell in secondThreatCells)
                        {
                            if (CanOccupyTogether(firstUnit, firstCell, secondUnit, secondCell, grid))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool CanOccupyTogether(
            Unit firstUnit,
            Cell firstAnchor,
            Unit secondUnit,
            Cell secondAnchor,
            CellGrid grid)
        {
            if (firstUnit == null || secondUnit == null || firstAnchor == null || secondAnchor == null)
            {
                return false;
            }

            IReadOnlyList<Cell> firstFootprint = firstUnit.GetFootprintCells(firstAnchor, grid);
            IReadOnlyList<Cell> secondFootprint = secondUnit.GetFootprintCells(secondAnchor, grid);
            if (firstFootprint.Count != firstUnit.FootprintTileCount
                || secondFootprint.Count != secondUnit.FootprintTileCount)
            {
                return false;
            }

            var occupiedCoordinates = new HashSet<UnityEngine.Vector2Int>(
                firstFootprint.Where(cell => cell != null).Select(cell => cell.Coordinates));
            return secondFootprint.All(cell => cell != null && !occupiedCoordinates.Contains(cell.Coordinates));
        }
    }
}
