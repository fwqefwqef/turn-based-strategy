using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Game.Grid;

namespace Windy.Srpg.Game.AI.Evaluators
{
    public class DamageCellEvaluator : CellEvaluator
    {
        private float maxPossibleDamage;
        private List<Unit> enemyUnits;
        private Dictionary<Unit, float> damage;

        public override void Precalculate(Unit evaluatingUnit, Player currentPlayer, CellGrid cellGrid)
        {
            damage = new Dictionary<Unit, float>();
            maxPossibleDamage = 0f;

            enemyUnits = cellGrid.GetEnemyUnits(currentPlayer);
            foreach (Unit enemy in enemyUnits)
            {
                float realDamage = evaluatingUnit.DryAttack(enemy);
                damage.Add(enemy, realDamage);
                if (realDamage > maxPossibleDamage)
                {
                    maxPossibleDamage = realDamage;
                }
            }
        }

        public override float Evaluate(Cell cellToEvaluate, Unit evaluatingUnit, Player currentPlayer, CellGrid cellGrid)
        {
            if (maxPossibleDamage.Equals(0f))
            {
                return 0f;
            }

            IEnumerable<float> scores = enemyUnits.Select(unit =>
            {
                float isAttackableValue = evaluatingUnit.IsUnitAttackable(unit, cellToEvaluate) ? 1f : 0f;
                return (isAttackableValue * damage[unit]) / maxPossibleDamage;
            });

            return scores.DefaultIfEmpty().Max();
        }
    }
}
