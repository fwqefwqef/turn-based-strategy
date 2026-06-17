using TbsFramework.Cells;
using UnityEngine;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Rendering;

namespace Windy.Srpg.Game.Grid
{
    internal static class CellTilePreviewUtility
    {
        internal static void ApplySkillPreviewHighlight(Cell cell, CellHighlightKind highlightKind, bool faint = false)
        {
            BattleSquareCellHighlighter highlighter = GetHighlighter(cell);
            BoardCell boardCell = GetBoardCell(cell);
            if (highlighter != null && boardCell != null)
            {
                if (faint)
                {
                    highlighter.ApplyFaint(boardCell, highlightKind);
                }
                else
                {
                    boardCell.ApplyHighlight(highlightKind);
                }

                return;
            }

            boardCell?.ApplyHighlight(highlightKind);
        }

        internal static void ClearSkillPreviewHighlight(Cell cell)
        {
            GetBoardCell(cell)?.ClearHighlight();
        }

        internal static void ShowPreviewBorder(Cell cell, bool top, bool right, bool bottom, bool left, Color color)
        {
            GetHighlighter(cell)?.ShowPreviewBorder(top, right, bottom, left, color);
        }

        internal static void ClearPreviewBorder(Cell cell)
        {
            GetHighlighter(cell)?.ClearPreviewBorder();
        }

        private static BoardCell GetBoardCell(Cell cell)
        {
            if (cell == null)
            {
                return null;
            }

            BattleSquareCell tile = cell.GetComponent<BattleSquareCell>();
            if (tile != null)
            {
                return tile;
            }

            return cell.GetComponent<BoardCell>();
        }

        private static BattleSquareCellHighlighter GetHighlighter(Cell cell)
        {
            return cell != null ? cell.GetComponent<BattleSquareCellHighlighter>() : null;
        }
    }
}
