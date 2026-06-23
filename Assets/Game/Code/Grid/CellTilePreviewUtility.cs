using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Grid;

namespace Windy.Srpg.Game.Grid
{
    internal static class CellTilePreviewUtility
    {
        internal static void ApplySkillPreviewHighlight(Cell cell, CellHighlightKind highlightKind, bool faint = false)
        {
            CellHighlighter highlighter = GetHighlighter(cell);
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

        internal static void ClearSkillPreviewHighlight(Cell cell)
        {
            cell?.ClearHighlight();
        }

        internal static void ShowPreviewBorder(Cell cell, bool top, bool right, bool bottom, bool left, Color color)
        {
            GetHighlighter(cell)?.ShowPreviewBorder(top, right, bottom, left, color);
        }

        internal static void ClearPreviewBorder(Cell cell)
        {
            GetHighlighter(cell)?.ClearPreviewBorder();
        }

        private static CellHighlighter GetHighlighter(Cell cell)
        {
            return cell != null ? cell.GetComponent<CellHighlighter>() : null;
        }
    }
}

