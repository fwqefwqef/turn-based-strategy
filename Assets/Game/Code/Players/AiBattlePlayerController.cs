using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Game.AI;

namespace Windy.Srpg.Game.Players
{
    public sealed class AiBattlePlayerController : BattlePlayerController
    {
        public override bool IsHumanControlled => false;

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        public override void PlayTurn(CellGrid grid)
        {
            if (grid == null)
            {
                return;
            }

            AiPlayer aiPlayer = GetComponent<AiPlayer>();
            StopAllCoroutines();
            grid.EnterAiTurnState(aiPlayer);
            StartCoroutine(ExecuteTurn(grid, aiPlayer));
        }

        private IEnumerator ExecuteTurn(CellGrid grid, AiPlayer aiPlayer)
        {
            if (aiPlayer == null)
            {
                yield break;
            }

            IReadOnlyList<Unit> orderedUnits = SelectUnits(grid);

            yield return AiTurnRunner.ExecuteTurn(
                aiPlayer,
                orderedUnits,
                grid,
                () => grid.RequestEndTurn());
        }

        private IReadOnlyList<Unit> SelectUnits(CellGrid grid)
        {
            return AiTurnOrdering.OrderByMovementFreedom(
                grid.GetCurrentPlayerUnits().Where(unit => unit != null),
                grid);
        }
    }
}

