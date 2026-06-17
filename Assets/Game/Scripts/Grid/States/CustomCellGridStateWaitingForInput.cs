using System.Linq;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Game.Diagnostics;

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
                CustomUnit shadowSelected = _cellGrid.EvaluateRuntimeSelectionFromWaiting(customUnit);
                var shadowDecision = new CustomCellGrid.RuntimeStateTransitionDecision(
                    shadowSelected != null ? "Selected" : "Waiting",
                    shadowSelected,
                    null);
                var runtimeDecision = _cellGrid.ProcessRuntimeWaitingStateUnitClick(customUnit);
                RuntimeParityDiagnostics.CompareRuntimeStateDecision(
                    $"Waiting selection on {customUnit.name}",
                    shadowDecision,
                    runtimeDecision);

                if (runtimeDecision.SelectedUnit != null)
                {
                    _cellGrid.ApplyLegacyStateFromRuntime(() => _cellGrid.EnterSelectedState(runtimeDecision.SelectedUnit));
                }

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
