using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.AI;

namespace Windy.Srpg.Game.Players.AI
{
    public sealed class MovementFreedomUnitSelection : UnitSelection
    {
        public override IEnumerable<Unit> SelectNext(Func<List<Unit>> getUnits, CellGrid cellGrid)
        {
            List<Unit> units = getUnits?.Invoke()?
                .Where(unit => unit != null)
                .Distinct()
                .ToList()
                ?? new List<Unit>();

            foreach (Unit unit in AiTurnOrdering.OrderByMovementFreedom(units, cellGrid))
            {
                if (unit != null)
                {
                    yield return unit;
                }
            }
        }
    }
}
