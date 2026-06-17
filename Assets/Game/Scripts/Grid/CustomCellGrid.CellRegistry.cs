using System.Collections.Generic;
using TbsFramework.Cells;
using UnityEngine;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid
{
    public partial class CustomCellGrid
    {
        internal static CustomSquare ResolveCustomSquareFromRegistryCell(Cell cell)
        {
            return cell != null ? cell.GetComponent<CustomSquare>() : null;
        }

        internal static IBattleCell ResolveBattleCellFromRegistryCell(Cell cell)
        {
            return ResolveCustomSquareFromRegistryCell(cell);
        }

        internal static Cell ResolveRegistryCellFromBattleCell(IBattleCell cell)
        {
            if (cell is CustomSquare square)
            {
                return square.LegacyCell;
            }

            return cell as Cell;
        }

        private void EnsureSceneCellAnchors()
        {
            foreach (CustomSquare square in GetComponentsInChildren<CustomSquare>(true))
            {
                if (square == null)
                {
                    continue;
                }

                square.EnsureFrameworkSquareAnchor();
            }

            EnsureDeploymentSlotCellBindings();
        }

        private void EnsureDeploymentSlotCellBindings()
        {
            CustomSquare[] squares = GetComponentsInChildren<CustomSquare>(true);
            foreach (DeploymentSlot slot in GetDeploymentSlots())
            {
                slot?.EnsureRegistryCellBinding(squares);
            }
        }
    }
}
