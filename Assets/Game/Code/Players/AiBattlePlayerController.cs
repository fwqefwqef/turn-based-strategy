using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Runtime.AI;
using Windy.Srpg.Runtime.Grid;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Players
{
    public sealed class AiBattlePlayerController : BattlePlayerController
    {
        public override bool IsHumanControlled => false;

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        public override void PlayTurn(IGridContext grid)
        {
            if (grid is not RuntimeGrid runtimeGrid)
            {
                return;
            }

            StopAllCoroutines();
            StartCoroutine(ExecuteTurn(runtimeGrid));
        }

        private IEnumerator ExecuteTurn(RuntimeGrid grid)
        {
            IReadOnlyList<GridUnit> orderedUnits = SelectUnits(grid);
            yield return AiTurnRunner.ExecuteTurn(
                this,
                orderedUnits.Cast<IGridUnit>(),
                grid,
                () => grid.EndCurrentTurn());
        }

        private IReadOnlyList<GridUnit> SelectUnits(RuntimeGrid grid)
        {
            return AiTurnOrdering.OrderByMovementFreedom(grid.GetCurrentPlayerUnits());
        }
    }
}
