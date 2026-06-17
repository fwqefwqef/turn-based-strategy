using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.AI;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Players.AI
{
    public sealed class MovementFreedomUnitSelection : UnitSelection
    {
        private readonly HashSet<Unit> visitedUnits = new HashSet<Unit>();

        public override IEnumerable<Unit> SelectNext(Func<List<Unit>> getUnits, CellGrid cellGrid)
        {
            List<Unit> units = getUnits?.Invoke() ?? new List<Unit>();
            while (visitedUnits.Count < units.Count)
            {
                List<BoardUnit> candidateRuntimeUnits = units
                    .Where(unit => unit != null && !visitedUnits.Contains(unit))
                    .Select(unit => unit.GetComponent<BoardUnit>())
                    .Where(unit => unit != null)
                    .ToList();

                Unit nextUnit = AiTurnOrdering.OrderByMovementFreedom(candidateRuntimeUnits)
                    .Select(unit => unit != null ? unit.GetComponent<Unit>() : null)
                    .FirstOrDefault(unit => unit != null && !visitedUnits.Contains(unit));

                if (nextUnit == null)
                {
                    nextUnit = units
                        .Where(unit => unit != null && !visitedUnits.Contains(unit))
                        .OrderByDescending(unit => CountTraversableNeighbors(unit, cellGrid))
                        .FirstOrDefault();
                }

                if (nextUnit == null)
                {
                    break;
                }

                visitedUnits.Add(nextUnit);
                yield return nextUnit;
            }

            visitedUnits.Clear();
        }

        private static int CountTraversableNeighbors(Unit unit, CellGrid cellGrid)
        {
            if (unit?.Cell == null)
            {
                return 0;
            }

            return unit.Cell
                .GetNeighbours(cellGrid.GetAllBoardCells())
                .Count(unit.IsCellTraversable);
        }
    }
}

