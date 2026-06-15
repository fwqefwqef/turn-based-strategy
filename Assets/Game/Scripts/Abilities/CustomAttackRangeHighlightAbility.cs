using System.Collections.Generic;
using System.Linq;
using TbsFramework.Cells;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Abilities
{
    public class CustomAttackRangeHighlightAbility : CustomAbility
    {
        private List<CustomUnit> inRange;

        protected override void OnCellSelected(Cell cell, CustomCellGrid cellGrid)
        {
            ClearHighlights();
        }

        protected override void OnCellDeselected(Cell cell, CustomCellGrid cellGrid)
        {
            ClearHighlights();
        }

        protected override void CleanUp(CustomCellGrid cellGrid)
        {
            ClearHighlights();
        }

        protected override void OnAbilityDeselected(CustomCellGrid cellGrid)
        {
            ClearHighlights();
        }

        protected override void OnTurnEnd(CustomCellGrid cellGrid)
        {
            ClearHighlights();
        }

        private void ClearHighlights()
        {
            inRange?.ForEach(u => u?.UnMark());
            inRange = null;
        }
    }
}
