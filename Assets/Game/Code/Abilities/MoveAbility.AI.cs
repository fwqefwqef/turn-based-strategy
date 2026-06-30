using System.Collections;
using System.Collections.Generic;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Skills;
using Windy.Srpg.Game.Units;

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

            yield return ExecuteAreaSkillThenConfirmPendingMove(skill, centerCell, affectedTargets, cellGrid);
        }
    }
}
