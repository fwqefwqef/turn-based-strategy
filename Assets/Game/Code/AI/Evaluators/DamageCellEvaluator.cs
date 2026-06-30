using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.AI.Evaluators
{
    public class DamageCellEvaluator : CellEvaluator
    {
        public override void Precalculate(Unit evaluatingUnit, Player currentPlayer, CellGrid cellGrid)
        {
        }

        public override float Evaluate(Cell cellToEvaluate, Unit evaluatingUnit, Player currentPlayer, CellGrid cellGrid)
        {
            if (cellToEvaluate == null || evaluatingUnit == null || currentPlayer == null || cellGrid == null)
            {
                return 0f;
            }

            return AiCombatPlanner.EvaluateBestPlanScore(evaluatingUnit, currentPlayer, cellGrid, cellToEvaluate);
        }
    }
}
