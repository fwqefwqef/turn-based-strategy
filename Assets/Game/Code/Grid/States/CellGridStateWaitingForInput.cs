using System.Linq;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Grid.States
{
    public class CellGridStateWaitingForInput : CellGridState
    {
        public CellGridStateWaitingForInput(CellGrid cellGrid) : base(cellGrid)
        {
        }

        public override void OnUnitClicked(Unit customUnit)
        {
            if (_cellGrid.ShouldRouteHumanMovementThroughRuntime)
            {
                return;
            }

            bool willSelect = _cellGrid.GetCurrentPlayerUnits().Contains(customUnit)
                && !customUnit.IsFinishedForTurn;

            if (willSelect)
            {
                _cellGrid.EnterSelectedState(customUnit);
            }
        }
    }
}

