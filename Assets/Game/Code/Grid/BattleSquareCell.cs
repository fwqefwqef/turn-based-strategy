using UnityEngine;
using UnityEngine.EventSystems;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid
{
    /// <summary>
    /// Single scene tile host: runtime <see cref="BoardCell"/> for gameplay and input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class BattleSquareCell : SquareBoardCell
    {
        protected override void OnMouseDown()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            CellGrid grid = FindAnyObjectByType<CellGrid>();
            if (grid != null && grid.ShouldSuppressFrameworkSceneInput)
            {
                base.OnMouseDown();
                return;
            }

            grid?.HandleSceneCellClicked(this);
        }

        protected override void OnMouseEnter()
        {
            CellGrid grid = FindAnyObjectByType<CellGrid>();
            if (grid != null && grid.ShouldSuppressFrameworkSceneInput)
            {
                base.OnMouseEnter();
                return;
            }

            grid?.HandleSceneCellSelected(this);
        }

        protected override void OnMouseExit()
        {
            CellGrid grid = FindAnyObjectByType<CellGrid>();
            if (grid != null && grid.ShouldSuppressFrameworkSceneInput)
            {
                base.OnMouseExit();
                return;
            }

            grid?.HandleSceneCellDeselected(this);
        }

        internal void RaiseSceneHighlightEvent()
        {
            HandleSceneCellSelected();
        }

        internal void RaiseSceneDehighlightEvent()
        {
            HandleSceneCellDeselected();
        }

        private void HandleSceneCellSelected()
        {
            FindAnyObjectByType<CellGrid>()?.HandleSceneCellSelected(this);
        }

        private void HandleSceneCellDeselected()
        {
            FindAnyObjectByType<CellGrid>()?.HandleSceneCellDeselected(this);
        }
    }
}

