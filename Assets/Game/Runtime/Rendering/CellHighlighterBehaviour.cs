using UnityEngine;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Runtime.Rendering
{
    public abstract class CellHighlighterBehaviour : MonoBehaviour
    {
        public virtual void Apply(BoardCell cell, CellHighlightKind highlightKind)
        {
        }

        public virtual void Clear(BoardCell cell)
        {
        }
    }
}
