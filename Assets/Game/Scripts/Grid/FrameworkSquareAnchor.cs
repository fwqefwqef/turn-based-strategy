using System;
using TbsFramework.Cells;
using TbsFramework.Grid;
using UnityEngine;

namespace Windy.Srpg.Game.Grid
{
    /// <summary>
    /// Thin framework <see cref="Square"/> token on the same GameObject as <see cref="CustomSquare"/>
    /// so legacy <see cref="CellGrid"/> registries and pathfinding still work.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CustomSquare))]
    [ExecuteInEditMode]
    public sealed class FrameworkSquareAnchor : Square
    {
        private CustomSquare Owner => GetComponent<CustomSquare>();

        public override Vector3 GetCellDimensions()
        {
            return Owner != null ? Owner.GetCellDimensions() : Vector3.one;
        }

        public override void MarkAsHighlighted() => Owner?.MarkAsHighlighted();

        public override void MarkAsPath() => Owner?.MarkAsPath();

        public override void MarkAsReachable() => Owner?.MarkAsReachable();

        public override void UnMark() => Owner?.UnMark();

        public override void Initialize(CellGrid cellGrid)
        {
            Owner?.SyncRegistryAnchorFromCustomSquare(this);
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
    }
}
