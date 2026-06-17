using System.Linq;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.AI.Evaluators
{
    public class DamageUnitEvaluator : UnitEvaluator
    {
        private float topDamage;

        public override void Precalculate(Unit evaluatingUnit, Player currentPlayer, CellGrid cellGrid)
        {
            var enemyUnits = cellGrid.GetEnemyUnits(currentPlayer);
            var enemiesInRange = enemyUnits.Where(unit => evaluatingUnit.Cell.GetDistance(unit.Cell) <= evaluatingUnit.AttackRange);
            topDamage = enemiesInRange.Select(unit => evaluatingUnit.DryAttack(unit)).DefaultIfEmpty().Max();
        }

        public override float Evaluate(Unit unitToEvaluate, Unit evaluatingUnit, Player currentPlayer, CellGrid cellGrid)
        {
            return evaluatingUnit.DryAttack(unitToEvaluate) / topDamage;
        }
    }
}

