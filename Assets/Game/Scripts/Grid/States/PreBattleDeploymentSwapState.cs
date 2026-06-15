using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid.States
{
    public sealed class PreBattleDeploymentSwapState : CustomCellGridState
    {
        private readonly CustomCellGrid customCellGrid;

        public PreBattleDeploymentSwapState(CustomCellGrid cellGrid) : base(cellGrid)
        {
            customCellGrid = cellGrid;
        }

        public override void OnCustomUnitClicked(CustomUnit customUnit)
        {
            customCellGrid.HandlePreBattleDeploymentUnitClicked(customUnit);
        }

        public override void OnCellClicked(IBattleCell cell)
        {
            customCellGrid.HandlePreBattleDeploymentCellClicked(ResolveLegacyCell(cell));
        }

        public override void OnRightClick()
        {
            customCellGrid.CancelPreBattleDeploymentSelection();
        }
    }
}
