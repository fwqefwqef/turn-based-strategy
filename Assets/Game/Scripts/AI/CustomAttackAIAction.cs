using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Game.AI.Evaluators;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.Players;
using UnityEngine;

namespace Windy.Srpg.Game.AI.Actions
{
    public class CustomAttackAIAction : AIAction
    {
        private CustomUnit target;
        private Dictionary<CustomUnit, string> unitDebugInfo;
        private List<(CustomUnit unit, float value)> unitScores;

        private Dictionary<string, Dictionary<string, float>> executionTime;
        private Stopwatch stopWatch = new Stopwatch();

        public override void InitializeAction(CustomPlayer player, CustomUnit unit, CustomCellGrid cellGrid)
        {
            unit.GetComponent<CustomAttackAbility>()?.OnActionSelected(cellGrid);

            executionTime = new Dictionary<string, Dictionary<string, float>>();
            executionTime.Add("precalculate", new Dictionary<string, float>());
            executionTime.Add("evaluate", new Dictionary<string, float>());
        }

        public override bool ShouldExecute(CustomPlayer player, CustomUnit unit, CustomCellGrid cellGrid)
        {
            if (unit == null || unit.GetComponent<CustomAttackAbility>() == null)
            {
                return false;
            }

            var enemyUnits = cellGrid.GetEnemyUnits(player);
            var isEnemyinRange = enemyUnits.Select(u => unit.IsUnitAttackable(u, unit.Cell))
                                           .Aggregate((result, next) => result || next);

            return isEnemyinRange && unit.CanStartActionThisTurn;
        }

        public override void Precalculate(CustomPlayer player, CustomUnit unit, CustomCellGrid cellGrid)
        {
            if (unit == null)
            {
                return;
            }

            var enemyUnits = cellGrid.GetEnemyUnits(player);
            var enemiesInRange = enemyUnits.Where(e => unit.IsUnitAttackable(e, unit.Cell)).ToList();

            unitDebugInfo = new Dictionary<CustomUnit, string>();
            enemyUnits.ForEach(u => unitDebugInfo[u] = "");

            if (enemiesInRange.Count == 0)
            {
                return;
            }

            var evaluators = GetComponents<UnitEvaluator>();
            foreach (var evaluator in evaluators)
            {
                stopWatch.Start();
                evaluator.Precalculate(unit, player, cellGrid);
                stopWatch.Stop();

                executionTime["precalculate"].Add(evaluator.GetType().Name, stopWatch.ElapsedMilliseconds);
                executionTime["evaluate"].Add(evaluator.GetType().Name, 0);
                stopWatch.Reset();
            }

            unitScores = enemiesInRange.Select(enemy =>
            {
                float totalScore = evaluators.Select(evaluator =>
                {
                    stopWatch.Start();
                    var score = evaluator.Evaluate(enemy, unit, player, cellGrid);
                    stopWatch.Stop();
                    executionTime["evaluate"][evaluator.GetType().Name] += stopWatch.ElapsedMilliseconds;
                    stopWatch.Reset();

                    var weightedScore = score * evaluator.Weight;
                    unitDebugInfo[enemy] += string.Format("{0:+0.00;-0.00} * {1:+0.00;-0.00} = {2:+0.00;-0.00} : {3}\n", evaluator.Weight, score, weightedScore, evaluator.GetType());
                    return weightedScore;
                }).DefaultIfEmpty(0f).Aggregate((result, next) => result + next);

                return (enemy, totalScore);
            }).ToList();

            unitScores.ForEach(score => unitDebugInfo[score.unit] += string.Format("Total: {0:0.00}", score.value));
            target = unitScores.OrderByDescending(o => o.value).First().unit;
        }

        public override IEnumerator Execute(CustomPlayer player, CustomUnit unit, CustomCellGrid cellGrid)
        {
            var attackAbility = unit.GetComponent<CustomAttackAbility>();
            if (attackAbility == null || target == null || unit == null)
            {
                yield break;
            }

            attackAbility.UnitToAttack = target;
            attackAbility.UnitToAttackID = target.UnitID;

            unit.AttackHandler(target);
            yield return CustomUnit.WaitForAttackSequenceCompletion(unit);

            if (unit == null)
            {
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
        }

        public override void CleanUp(CustomPlayer player, CustomUnit unit, CustomCellGrid cellGrid)
        {
            foreach (CustomUnit enemy in cellGrid.GetEnemyUnits(player))
            {
                enemy.UnMark();
            }

            target = null;
            unitScores = null;
        }

        public override void ShowDebugInfo(CustomPlayer player, CustomUnit unit, CustomCellGrid cellGrid)
        {
            if (cellGrid.CurrentCustomState is CustomCellGridStateAiTurn aiTurnState)
            {
                aiTurnState.UnitDebugInfo = unitDebugInfo;
            }

            if (unitScores == null)
            {
                return;
            }

            var minScore = unitScores.DefaultIfEmpty().Min(e => e.value);
            var maxScore = unitScores.DefaultIfEmpty().Max(e => e.value);
            foreach (var (evaluatedUnit, value) in unitScores)
            {
                var color = Color.Lerp(Color.red, Color.green, value >= 0 ? value / maxScore : value / minScore * (-1));
                evaluatedUnit.SetColor(color);
            }

            if (target != null)
            {
                target.SetColor(Color.blue);
            }

            var evaluators = GetComponents<UnitEvaluator>();
            var sb = new StringBuilder();
            var sum = 0f;

            sb.AppendFormat("{0} evaluators execution time summary:\n", GetType().Name);
            foreach (var evaluator in evaluators)
            {
                var precalculateTime = executionTime["precalculate"][evaluator.GetType().Name];
                var evaluateTime = executionTime["evaluate"][evaluator.GetType().Name];
                sum += precalculateTime + evaluateTime;

                sb.AppendFormat("total: {0}ms\tprecalculate: {1}ms\tevaluate: {2}ms\t:{3}\n",
                    (precalculateTime + evaluateTime).ToString().PadLeft(4),
                    precalculateTime.ToString().PadLeft(4),
                    evaluateTime.ToString().PadLeft(4),
                    evaluator.GetType().Name);
            }

            sb.AppendFormat("sum: {0}ms", sum.ToString().PadLeft(4));
            UnityEngine.Debug.Log(sb.ToString());
        }
    }
}
