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
            CustomUnit selectedUnit = _cellGrid.ProcessRuntimeWaitingStateUnitClick(customUnit).SelectedUnit;

            if (selectedUnit != null)
            {
                _cellGrid.ApplyLegacyStateFromRuntime(() => _cellGrid.EnterSelectedState(selectedUnit));
            }
        }
    }
}
