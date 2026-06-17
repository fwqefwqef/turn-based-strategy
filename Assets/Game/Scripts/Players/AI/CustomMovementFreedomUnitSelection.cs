using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.AI;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Players.AI
{
    public sealed class CustomMovementFreedomUnitSelection : CustomUnitSelection
    {
        private readonly HashSet<CustomUnit> visitedUnits = new HashSet<CustomUnit>();

        public override IEnumerable<CustomUnit> SelectNext(Func<List<CustomUnit>> getUnits, CustomCellGrid cellGrid)
        {
            List<CustomUnit> units = getUnits?.Invoke() ?? new List<CustomUnit>();
            while (visitedUnits.Count < units.Count)
            {
                List<BattleUnit> candidateRuntimeUnits = units
                    .Where(unit => unit != null && !visitedUnits.Contains(unit))
                    .Select(unit => unit.GetComponent<BattleUnit>())
                    .Where(unit => unit != null)
                    .ToList();

                CustomUnit nextUnit = AiTurnOrdering.OrderByMovementFreedom(candidateRuntimeUnits)
                    .Select(unit => unit != null ? unit.GetComponent<CustomUnit>() : null)
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

        private static int CountTraversableNeighbors(CustomUnit unit, CustomCellGrid cellGrid)
        {
            if (unit?.Cell == null)
            {
                return 0;
            }

            return unit.Cell
                .GetNeighbours(cellGrid.GetAllCells())
                .Count(unit.IsCellTraversable);
        }
    }
}
