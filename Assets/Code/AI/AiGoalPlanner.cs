using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.AI
{
    /// <summary>
    /// Compares the shortest terrain-only routes to the nearest goal with routes
    /// available while hostile units occupy the board. Allies remain pass-through.
    /// </summary>
    internal static class AiGoalPlanner
    {
        private const float Epsilon = 0.001f;

        internal static bool TryPlan(Unit unit, CellGrid grid, out Cell destination, out bool attack)
        {
            destination = unit?.Cell;
            attack = false;
            if (unit?.Cell == null || grid == null || unit.AiGoalTiles == null || unit.AiGoalTiles.Count == 0)
            {
                return false;
            }

            List<Cell> cells = grid.GetAllCells();
            var goalCoordinates = new HashSet<Vector2Int>(unit.AiGoalTiles);
            var passable = cells.Where(cell => CanUseAnchor(unit, cell, grid)).ToList();
            if (!passable.Contains(unit.Cell))
            {
                passable.Add(unit.Cell);
            }

            var passableSet = new HashSet<Cell>(passable);
            List<Cell> goalAnchors = passable.Where(cell =>
                unit.GetFootprintCells(cell, grid).Any(footprintCell => goalCoordinates.Contains(footprintCell.Coordinates)))
                .ToList();
            if (goalAnchors.Count == 0)
            {
                return false;
            }

            Dictionary<Cell, float> ideal = FindDistances(unit, grid, cells, passableSet, false, out _);
            float nearestCost = goalAnchors.Where(ideal.ContainsKey)
                .Select(cell => ideal[cell]).DefaultIfEmpty(float.PositiveInfinity).Min();
            if (float.IsPositiveInfinity(nearestCost))
            {
                return false;
            }

            List<Cell> nearestGoals = goalAnchors
                .Where(cell => ideal.TryGetValue(cell, out float cost) && cost <= nearestCost + Epsilon)
                .ToList();
            if (nearestCost <= Epsilon)
            {
                return true;
            }

            Dictionary<Cell, float> open = FindDistances(unit, grid, cells, passableSet, true, out Dictionary<Cell, Cell> previous);
            Cell openGoal = nearestGoals.Where(open.ContainsKey)
                .OrderBy(cell => open[cell]).FirstOrDefault();
            attack = openGoal == null || open[openGoal] > nearestCost + Epsilon;

            // Follow an unobstructed shortest route if one exists. Otherwise approach
            // the obstruction along the best currently reachable route.
            if (!attack)
            {
                Cell step = openGoal;
                Cell best = unit.Cell;
                while (step != null && step != unit.Cell)
                {
                    if (open[step] <= unit.MovementPoints + Epsilon && unit.IsCellMovableTo(step))
                    {
                        best = step;
                        break;
                    }

                    previous.TryGetValue(step, out step);
                }

                destination = best;
                return true;
            }

            Dictionary<Cell, float> remaining = FindReverseDistances(unit, grid, cells, passableSet, nearestGoals);
            float currentRemaining = remaining.TryGetValue(unit.Cell, out float originRemaining)
                ? originRemaining : float.PositiveInfinity;
            Cell approach = open.Where(entry => entry.Key != unit.Cell
                    && entry.Value <= unit.MovementPoints + Epsilon
                    && unit.IsCellMovableTo(entry.Key)
                    && remaining.ContainsKey(entry.Key))
                .OrderBy(entry => remaining[entry.Key])
                .ThenByDescending(entry => entry.Value)
                .Select(entry => entry.Key)
                .FirstOrDefault();
            if (approach != null && remaining[approach] < currentRemaining - Epsilon)
            {
                destination = approach;
            }

            return true;
        }

        private static bool CanUseAnchor(Unit unit, Cell anchor, CellGrid grid)
        {
            if (anchor == null) return false;
            IReadOnlyList<Cell> footprint = unit.GetFootprintCells(anchor, grid);
            bool canFly = unit.PassiveList?.CanTraverseUntraversableTerrain == true;
            return footprint.Count == unit.FootprintTileCount
                && footprint.All(cell => cell != null && (cell.IsTraversable || canFly));
        }

        private static Dictionary<Cell, float> FindDistances(
            Unit unit, CellGrid grid, List<Cell> cells, HashSet<Cell> passable,
            bool respectHostileUnits, out Dictionary<Cell, Cell> previous)
        {
            var distances = new Dictionary<Cell, float> { [unit.Cell] = 0f };
            previous = new Dictionary<Cell, Cell>();
            var settled = new HashSet<Cell>();
            var frontier = new SortedSet<(float cost, int order, Cell cell)> { (0f, 0, unit.Cell) };
            int nextOrder = 1;
            while (frontier.Count > 0)
            {
                (float _, int _, Cell current) = frontier.Min;
                frontier.Remove(frontier.Min);
                if (!settled.Add(current)) continue;
                foreach (Cell next in current.GetNeighbours(grid, cells))
                {
                    if (!passable.Contains(next) || (respectHostileUnits && !unit.IsCellTraversable(next)))
                    {
                        continue;
                    }

                    float distance = distances[current] + unit.GetFootprintMovementCost(next, grid);
                    if (distances.TryGetValue(next, out float existing) && distance >= existing - Epsilon) continue;
                    distances[next] = distance;
                    previous[next] = current;
                    frontier.Add((distance, nextOrder++, next));
                }
            }

            return distances;
        }

        private static Dictionary<Cell, float> FindReverseDistances(
            Unit unit, CellGrid grid, List<Cell> cells, HashSet<Cell> passable, List<Cell> goals)
        {
            var distances = goals.ToDictionary(goal => goal, _ => 0f);
            var settled = new HashSet<Cell>();
            var frontier = new SortedSet<(float cost, int order, Cell cell)>();
            int nextOrder = 0;
            foreach (Cell goal in goals) frontier.Add((0f, nextOrder++, goal));
            while (frontier.Count > 0)
            {
                (float _, int _, Cell current) = frontier.Min;
                frontier.Remove(frontier.Min);
                if (!settled.Add(current)) continue;
                foreach (Cell prior in current.GetNeighbours(grid, cells))
                {
                    if (!passable.Contains(prior)) continue;
                    float distance = distances[current] + unit.GetFootprintMovementCost(current, grid);
                    if (distances.TryGetValue(prior, out float existing) && distance >= existing - Epsilon) continue;
                    distances[prior] = distance;
                    frontier.Add((distance, nextOrder++, prior));
                }
            }

            return distances;
        }
    }
}
