using System.Collections.Generic;
using System.Linq;
using TbsFramework.Cells;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.AI.Evaluators
{
    public class DamageCellEvaluator : CellEvaluator
    {
        private float maxPossibleDamage;
        private List<CustomUnit> enemyUnits;
        private Dictionary<CustomUnit, float> damage;

        public override void Precalculate(CustomUnit evaluatingUnit, CustomPlayer currentPlayer, CustomCellGrid cellGrid)
        {
            damage = new Dictionary<CustomUnit, float>();
            maxPossibleDamage = 0f;

            enemyUnits = cellGrid.GetEnemyUnits(currentPlayer);
            foreach (CustomUnit enemy in enemyUnits)
            {
                float realDamage = evaluatingUnit.DryAttack(enemy);
                damage.Add(enemy, realDamage);
                if (realDamage > maxPossibleDamage)
                {
                    maxPossibleDamage = realDamage;
                }
            }
        }

        public override float Evaluate(Cell cellToEvaluate, CustomUnit evaluatingUnit, CustomPlayer currentPlayer, CustomCellGrid cellGrid)
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
