using System.Linq;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Grid.States
{
    public class CustomCellGridStateWaitingForInput : CustomCellGridState
    {
        public CustomCellGridStateWaitingForInput(CustomCellGrid cellGrid) : base(cellGrid)
        {
        }

        public override void OnCustomUnitClicked(CustomUnit customUnit)
        {
            if (_cellGrid.ShouldRouteHumanMovementThroughRuntime)
            {
                return;
            }

            bool willSelect = _cellGrid.GetCurrentPlayerCustomUnits().Contains(customUnit)
                && !customUnit.IsFinishedForTurn;

            _cellGrid.ShadowCompareSelection(customUnit, willSelect ? customUnit : null);

            if (willSelect)
            {
                _cellGrid.EnterSelectedState(customUnit);
            }
        }
    }
}
