using System.Collections;
using System.Collections.Generic;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Skills;
using Windy.Srpg.Game.Units;
using UnityEngine;

namespace Windy.Srpg.Game.Abilities
{
    public partial class MoveAbility
    {
        // AI uses the same skill-resolution path as the player so combat rules stay in one place.
        public IEnumerator ExecuteAiSkill(Skill skill, Unit target, CellGrid cellGrid, Inventory.Item preferredWeaponEntry = null)
        {
            if (skill?.Data == null || target == null || cellGrid == null)
            {
                yield break;
            }

            Inventory.Item previousWeaponEntry = selectedSkillPreviewWeaponEntry;
            selectedSkillPreviewWeaponEntry = preferredWeaponEntry;
            try
            {
                yield return ExecuteSkillThenConfirmPendingMove(skill, target, cellGrid);
            }
            finally
            {
                selectedSkillPreviewWeaponEntry = previousWeaponEntry;
            }
        }

        public IEnumerator ExecuteAiAreaSkill(Skill skill, Cell centerCell, IReadOnlyList<Unit> affectedTargets, CellGrid cellGrid)
        {
            if (skill?.Data == null || centerCell == null || affectedTargets == null || affectedTargets.Count == 0 || cellGrid == null)
            {
                yield break;
            }

            yield return ShowAiAreaSkillTelegraph(skill, centerCell, cellGrid);
            yield return ExecuteAreaSkillThenConfirmPendingMove(skill, centerCell, affectedTargets, cellGrid);
        }

        private IEnumerator ShowAiAreaSkillTelegraph(Skill skill, Cell centerCell, CellGrid cellGrid)
        {
            if (skill?.Data == null || centerCell == null || cellGrid == null || aiAreaSkillTelegraphSeconds <= 0f)
            {
                yield break;
            }

            HashSet<Cell> affectedCells = GetAreaSkillAffectedCells(skill, centerCell, cellGrid);
            if (affectedCells == null || affectedCells.Count == 0)
            {
                yield break;
            }

            List<Cell> highlightedCells = new List<Cell>();
            foreach (Cell cell in affectedCells)
            {
                if (cell == null)
                {
                    continue;
                }

                CellTilePreviewUtility.ApplySkillPreviewHighlight(cell, CellHighlightKind.Attack);
                highlightedCells.Add(cell);
            }

            yield return new WaitForSeconds(aiAreaSkillTelegraphSeconds);

            foreach (Cell cell in highlightedCells)
            {
                CellTilePreviewUtility.ClearSkillPreviewHighlight(cell);
            }
        }
    }
}
