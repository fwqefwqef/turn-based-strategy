using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Runtime.AI;
using Windy.Srpg.Runtime.Board;
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

        public override void PlayTurn(IBattleBoard board)
        {
            if (board is not BattleBoard battleBoard)
            {
                return;
            }

            StopAllCoroutines();
            StartCoroutine(ExecuteTurn(battleBoard));
        }

        private IEnumerator ExecuteTurn(BattleBoard board)
        {
            IReadOnlyList<BoardUnit> orderedUnits = SelectUnits(board);
            yield return AiTurnRunner.ExecuteTurn(
                this,
                orderedUnits.Cast<IBoardUnit>(),
                board,
                () => board.EndCurrentTurn());
        }

        private IReadOnlyList<BoardUnit> SelectUnits(BattleBoard board)
        {
            return AiTurnOrdering.OrderByMovementFreedom(board.GetCurrentPlayerUnits());
        }
    }
}

