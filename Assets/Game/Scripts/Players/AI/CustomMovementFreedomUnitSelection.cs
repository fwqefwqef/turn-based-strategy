using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;

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
                CustomUnit nextUnit = units
                    .Where(unit => unit != null && !visitedUnits.Contains(unit))
                    .OrderByDescending(unit => CountTraversableNeighbors(unit, cellGrid))
                    .FirstOrDefault();

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
