using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Abilities
{
    public class AttackRangeHighlightAbility : Ability
    {
        private List<Unit> inRange;

        protected override void OnCellSelected(BattleSquareCell cell, CellGrid cellGrid)
        {
            ClearHighlights();
        }

        protected override void OnCellDeselected(BattleSquareCell cell, CellGrid cellGrid)
        {
            ClearHighlights();
        }

        protected override void CleanUp(CellGrid cellGrid)
        {
            ClearHighlights();
        }

        protected override void OnAbilityDeselected(CellGrid cellGrid)
        {
            ClearHighlights();
        }

        protected override void OnTurnEnd(CellGrid cellGrid)
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

