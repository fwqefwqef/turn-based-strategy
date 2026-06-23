using UnityEngine;

namespace Windy.Srpg.Game.Grid
{
    public abstract class CellHighlighterBehaviour : MonoBehaviour
    {
        public abstract void Apply(Cell cell, CellHighlightKind highlightKind);
        public abstract void Clear(Cell cell);

        public virtual void ShowCursorBorder(Cell cell, Color color)
        {
        }

        public virtual void ClearCursorBorder(Cell cell)
        {
        }
    }
}

