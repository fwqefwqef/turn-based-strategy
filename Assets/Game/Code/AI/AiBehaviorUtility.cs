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
                    && candidate.HitPoints > 0
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

            int triggeredCount = groupUnits.Count(candidate => AiCombatPlanner.HasAnyOffensivePlanFromReachableCells(candidate, player, grid, out _));
            if (triggeredCount < 2)
            {
                return false;
            }

            foreach (Unit groupUnit in groupUnits)
            {
                groupUnit.ActivateAiWaitState();
            }

            return true;
        }
    }
}
