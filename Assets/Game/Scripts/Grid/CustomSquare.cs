using System.Collections.Generic;
using TbsFramework.Cells;
using TbsFramework.Cells.Highlighters;
using UnityEngine;
using UnityEngine.EventSystems;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid
{
    public abstract class CustomSquare : Square, IBattleCell
    {
        public List<CellHighlighter> MarkAsAttackPreviewFn;
        public List<CellHighlighter> MarkAsTradePreviewFn;
        public List<CellHighlighter> MarkAsAnyPreviewFn;

        public override void OnMouseDown()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            CustomCellGrid grid = FindAnyObjectByType<CustomCellGrid>();
            if (grid != null && grid.ShouldSuppressFrameworkSceneInput)
            {
                return;
            }

            base.OnMouseDown();
        }

        public override void OnMouseEnter()
        {
            CustomCellGrid grid = FindAnyObjectByType<CustomCellGrid>();
            if (grid != null && grid.ShouldSuppressFrameworkSceneInput)
            {
                return;
            }

            base.OnMouseEnter();
        }

        public override void OnMouseExit()
        {
            CustomCellGrid grid = FindAnyObjectByType<CustomCellGrid>();
            if (grid != null && grid.ShouldSuppressFrameworkSceneInput)
            {
                return;
            }

            base.OnMouseExit();
        }

        internal void RaiseSceneHighlightEvent()
        {
            base.OnMouseEnter();
        }

        internal void RaiseSceneDehighlightEvent()
        {
            base.OnMouseExit();
        }

        public virtual void MarkAsAttackPreview()
        {
            MarkAsAttackPreviewFn?.ForEach(o => o.Apply(this));
        }

        public virtual void MarkAsTradePreview()
        {
            MarkAsTradePreviewFn?.ForEach(o => o.Apply(this));
        }

        public virtual void MarkAsAnyPreview()
        {
            MarkAsAnyPreviewFn?.ForEach(o => o.Apply(this));
        }

        public virtual void MarkAsAttackPreviewFaint()
        {
            MarkAsAttackPreview();
        }

        public virtual void MarkAsTradePreviewFaint()
        {
            MarkAsTradePreview();
        }

        public virtual void MarkAsAnyPreviewFaint()
        {
            MarkAsAnyPreview();
        }

        public virtual void ShowPreviewBorder(bool top, bool right, bool bottom, bool left, UnityEngine.Color color)
        {
        }

        public virtual void ClearPreviewBorder()
        {
        }
    }
}


