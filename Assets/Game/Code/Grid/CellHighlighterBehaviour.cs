using UnityEngine;
using Windy.Srpg.Runtime.Grid;

namespace Windy.Srpg.Runtime.Rendering
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

