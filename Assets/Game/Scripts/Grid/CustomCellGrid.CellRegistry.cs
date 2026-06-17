using System.Collections.Generic;
using TbsFramework.Cells;
using UnityEngine;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid
{
    public partial class CustomCellGrid
    {
        internal static BattleSquareCell ResolveBattleSquareFromRegistryCell(Cell cell)
        {
            return cell != null ? cell.GetComponent<BattleSquareCell>() : null;
        }

        internal static IBattleCell ResolveBattleCellFromRegistryCell(Cell cell)
        {
            return ResolveBattleSquareFromRegistryCell(cell);
        }

        internal static Cell ResolveRegistryCellFromBattleCell(IBattleCell cell)
        {
            if (cell is BattleSquareCell tile)
            {
                return tile.LegacyCell;
            }

            if (cell is BoardCell boardCell)
            {
                FrameworkSquareAnchor anchor = boardCell.GetComponent<FrameworkSquareAnchor>();
                if (anchor != null)
                {
                    return anchor;
                }
            }

            return cell as Cell;
        }

        private void EnsureSceneCellAnchors()
        {
            foreach (BattleSquareCell tile in GetComponentsInChildren<BattleSquareCell>(true))
            {
                if (tile == null)
                {
                    continue;
                }

                tile.EnsureFrameworkSquareAnchor();
            }

            EnsureDeploymentSlotCellBindings();
        }

        private void EnsureDeploymentSlotCellBindings()
        {
            BattleSquareCell[] tiles = GetComponentsInChildren<BattleSquareCell>(true);
            foreach (DeploymentSlot slot in GetDeploymentSlots())
            {
                slot?.EnsureRegistryCellBinding(tiles);
            }
        }
    }
}
