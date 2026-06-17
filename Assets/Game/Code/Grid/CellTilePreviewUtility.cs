using UnityEngine;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Rendering;

namespace Windy.Srpg.Game.Grid
{
    internal static class CellTilePreviewUtility
    {
        internal static void ApplySkillPreviewHighlight(BattleSquareCell cell, CellHighlightKind highlightKind, bool faint = false)
        {
            BattleSquareCellHighlighter highlighter = GetHighlighter(cell);
            if (highlighter != null)
            {
                if (faint)
                {
                    highlighter.ApplyFaint(cell, highlightKind);
                }
                else
                {
                    cell.ApplyHighlight(highlightKind);
                }

                return;
            }

            cell?.ApplyHighlight(highlightKind);
        }

        internal static void ClearSkillPreviewHighlight(BattleSquareCell cell)
        {
            cell?.ClearHighlight();
        }

        internal static void ShowPreviewBorder(BattleSquareCell cell, bool top, bool right, bool bottom, bool left, Color color)
        {
            GetHighlighter(cell)?.ShowPreviewBorder(top, right, bottom, left, color);
        }

        internal static void ClearPreviewBorder(BattleSquareCell cell)
        {
            GetHighlighter(cell)?.ClearPreviewBorder();
        }

        private static BattleSquareCellHighlighter GetHighlighter(BattleSquareCell cell)
        {
            return cell != null ? cell.GetComponent<BattleSquareCellHighlighter>() : null;
        }
    }
}

