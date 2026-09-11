using System.Collections;
using System.Collections.Generic;
using System.Text;
using Windy.Srpg.Game.AI;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.Players;
using UnityEngine;

namespace Windy.Srpg.Game.AI.Actions
{
    public class AttackAIAction : AIAction
    {
        private AiCombatPlan selectedPlan;
        private Dictionary<Unit, string> unitDebugInfo;

        public override void InitializeAction(Player player, Unit unit, CellGrid cellGrid)
        {
            unit.GetComponent<AttackAbility>()?.OnActionSelected(cellGrid);
        }

        public override bool ShouldExecute(Player player, Unit unit, CellGrid cellGrid)
        {
            if (unit == null || cellGrid == null || !unit.CanStartActionThisTurn)
            {
                return false;
            }

            if (!AiBehaviorUtility.ShouldAllowAction(unit, player, cellGrid))
            {
                return false;
            }

            return AiCombatPlanner.HasAnyPlan(unit, player, cellGrid, unit.Cell);
        }

        public override void Precalculate(Player player, Unit unit, CellGrid cellGrid)
        {
            selectedPlan = null;
            unitDebugInfo = null;

            if (unit == null || player == null || cellGrid == null)
            {
                return;
            }

            unitDebugInfo = new Dictionary<Unit, string>();
            foreach (Unit enemy in cellGrid.GetEnemyUnits(player))
            {
                if (enemy != null)
                {
                    unitDebugInfo[enemy] = string.Empty;
                }
            }

            if (!AiCombatPlanner.TryFindBestPlan(unit, player, cellGrid, unit.Cell, out selectedPlan))
            {
                return;
            }

            if (selectedPlan.PrimaryTarget != null)
            {
                string valueLabel = selectedPlan.IsHealingPlan ? "Healing" : "Expected";
                string killLabel = selectedPlan.IsHealingPlan ? "Heal" : "Kill";
                unitDebugInfo[selectedPlan.PrimaryTarget] =
                    $"{selectedPlan.DebugLabel}\n" +
                    $"{valueLabel}: {selectedPlan.ExpectedDamage:0.00}\n" +
                    $"Score: {selectedPlan.Score:0.00}\n" +
                    $"{killLabel}: {(selectedPlan.ProjectsKill ? "Yes" : "No")}\n" +
                    $"Counter: {(selectedPlan.AvoidsCounterattack ? "No" : "Yes")}\n" +
                    $"MP: {(selectedPlan.CostsNoMp ? "0" : selectedPlan.Skill?.Data?.MpCost.ToString() ?? "0")}";
            }

            if (selectedPlan.AreaTargets != null)
            {
                foreach (Unit target in selectedPlan.AreaTargets)
                {
                    if (target == null || !unitDebugInfo.ContainsKey(target))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(unitDebugInfo[target]))
                    {
                        unitDebugInfo[target] = selectedPlan.DebugLabel;
                    }
                }
            }
        }

        public override IEnumerator Execute(Player player, Unit unit, CellGrid cellGrid)
        {
            if (unit == null || cellGrid == null || selectedPlan == null)
            {
                yield break;
            }

            MoveAbility moveAbility = unit.GetComponent<MoveAbility>();
            switch (selectedPlan.Kind)
            {
                case AiCombatActionKind.WeaponAttack:
                    if (selectedPlan.PrimaryTarget == null)
                    {
                        yield break;
                    }

                    if (selectedPlan.WeaponEntry?.Weapon != null)
                    {
                        unit.EquipWeapon(selectedPlan.WeaponEntry);
                    }

                    unit.AttackHandler(selectedPlan.PrimaryTarget);
                    yield return Unit.WaitForAttackSequenceCompletion(unit);
                    break;

                case AiCombatActionKind.Skill:
                    if (moveAbility == null || selectedPlan.Skill == null || selectedPlan.PrimaryTarget == null)
                    {
                        yield break;
                    }

                    yield return moveAbility.ExecuteAiSkill(selectedPlan.Skill, selectedPlan.PrimaryTarget, cellGrid, selectedPlan.WeaponEntry);
                    break;

                case AiCombatActionKind.AreaSkill:
                    if (moveAbility == null || selectedPlan.Skill == null || selectedPlan.AreaCenterCell == null)
                    {
                        yield break;
                    }

                    yield return moveAbility.ExecuteAiAreaSkill(selectedPlan.Skill, selectedPlan.AreaCenterCell, selectedPlan.AreaTargets, cellGrid);
                    break;
            }

            if (unit != null)
            {
                yield return new WaitForSeconds(0.15f);
            }
        }

        public override void CleanUp(Player player, Unit unit, CellGrid cellGrid)
        {
            foreach (Unit enemy in cellGrid.GetEnemyUnits(player))
            {
                enemy.UnMark();
            }

            selectedPlan = null;
            unitDebugInfo = null;
        }

        public override void ShowDebugInfo(Player player, Unit unit, CellGrid cellGrid)
        {
            if (cellGrid.CurrentState is CellGridStateAiTurn aiTurnState)
            {
                aiTurnState.UnitDebugInfo = unitDebugInfo;
            }

            if (selectedPlan == null)
            {
                return;
            }

            if (selectedPlan.AreaTargets != null)
            {
                foreach (Unit target in selectedPlan.AreaTargets)
                {
                    target?.SetColor(Color.magenta);
                }
            }

            if (selectedPlan.PrimaryTarget != null)
            {
                selectedPlan.PrimaryTarget.SetColor(Color.blue);
            }

            StringBuilder logBuilder = new StringBuilder();
            logBuilder.AppendLine($"{GetType().Name} selected combat plan");
            logBuilder.Append(" - Action: ").AppendLine(selectedPlan.DebugLabel);
            logBuilder.Append(selectedPlan.IsHealingPlan ? " - Expected healing: " : " - Expected damage: ")
                .AppendLine(selectedPlan.ExpectedDamage.ToString("0.00"));
            logBuilder.Append(" - Score: ").AppendLine(selectedPlan.Score.ToString("0.00"));
            logBuilder.Append(selectedPlan.IsHealingPlan ? " - Heal bonus: " : " - Kill bonus: ")
                .AppendLine(selectedPlan.ProjectsKill ? "Yes" : "No");
            logBuilder.Append(" - Avoids counter: ").AppendLine(selectedPlan.AvoidsCounterattack ? "Yes" : "No");
            logBuilder.Append(" - MP free: ").AppendLine(selectedPlan.CostsNoMp ? "Yes" : "No");
            UnityEngine.Debug.Log(logBuilder.ToString());
        }
    }
}

