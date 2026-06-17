using UnityEngine;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Runtime.Rendering
{
    public abstract class CellHighlighterBehaviour : MonoBehaviour
    {
        public abstract void Apply(BoardCell cell, CellHighlightKind highlightKind);
        public abstract void Clear(BoardCell cell);
    }
}

