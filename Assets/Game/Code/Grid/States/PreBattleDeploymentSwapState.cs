using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid.States
{
    public sealed class PreBattleDeploymentSwapState : CellGridState
    {
        private readonly CellGrid customCellGrid;

        public PreBattleDeploymentSwapState(CellGrid cellGrid) : base(cellGrid)
        {
            customCellGrid = cellGrid;
        }

        public override void OnUnitClicked(Unit customUnit)
        {
            customCellGrid.HandlePreBattleDeploymentUnitClicked(customUnit);
        }

        public override void OnCellClicked(IBattleCell cell)
        {
            customCellGrid.HandlePreBattleDeploymentCellClicked(ResolveBoardCell(cell));
        }

        public override void OnRightClick()
        {
            customCellGrid.CancelPreBattleDeploymentSelection();
        }
    }
}

