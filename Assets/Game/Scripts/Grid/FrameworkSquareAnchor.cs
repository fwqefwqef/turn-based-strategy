using System;
using TbsFramework.Cells;
using TbsFramework.Grid;
using UnityEngine;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Rendering;

namespace Windy.Srpg.Game.Grid
{
    /// <summary>
    /// Thin framework <see cref="Square"/> token on the same GameObject as <see cref="BattleSquareCell"/>.
    /// Keeps legacy cell registries and pathfinding while the runtime cell owns gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BattleSquareCell))]
    [ExecuteInEditMode]
    public sealed class FrameworkSquareAnchor : Square
    {
        private BattleSquareCell Owner => GetComponent<BattleSquareCell>();

        private BoardCell BoardCell => Owner;

        public override Vector3 GetCellDimensions()
        {
            return BoardCell != null ? BoardCell.GetCellDimensions() : Vector3.one;
        }

        public override void MarkAsHighlighted() => ApplyRuntimeHighlight(CellHighlightKind.Selected);

        public override void MarkAsPath() => ApplyRuntimeHighlight(CellHighlightKind.Path);

        public override void MarkAsReachable() => ApplyRuntimeHighlight(CellHighlightKind.Reachable);

        public override void UnMark() => BoardCell?.ClearHighlight();

        public override void Initialize(CellGrid cellGrid)
        {
            Owner?.SyncRegistryAnchorFromBoardCell(this);
        }

        internal void RaiseCellClicked() => base.RaiseCellClicked();

        internal void RaiseCellHighlighted() => base.RaiseCellHighlighted();

        internal void RaiseCellDehighlighted() => base.RaiseCellDehighlighted();

        public override void OnMouseDown()
        {
        }

        public override void OnMouseEnter()
        {
        }

        public override void OnMouseExit()
        {
        }

        private new void Reset()
        {
        }

        private void ApplyRuntimeHighlight(CellHighlightKind highlightKind)
        {
            BoardCell?.ApplyHighlight(highlightKind);
        }
    }
}
