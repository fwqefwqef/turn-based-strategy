using UnityEngine;
using UnityEngine.EventSystems;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid
{
    /// <summary>
    /// Single scene tile host: runtime <see cref="BoardCell"/> plus a baked
    /// <see cref="FrameworkSquareAnchor"/> registry token on the same GameObject.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FrameworkSquareAnchor))]
    public sealed partial class BattleSquareCell : SquareBoardCell
    {
        protected override void Awake()
        {
            base.Awake();
            EnsureFrameworkSquareAnchor();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureFrameworkSquareAnchor();
            SyncRegistryAnchorFromBoardCell();
        }
#endif

        protected override void OnMouseDown()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            CustomCellGrid grid = FindAnyObjectByType<CustomCellGrid>();
            if (grid != null && grid.ShouldSuppressFrameworkSceneInput)
            {
                base.OnMouseDown();
                return;
            }

            EnsureFrameworkSquareAnchor().RaiseCellClicked();
        }

        protected override void OnMouseEnter()
        {
            CustomCellGrid grid = FindAnyObjectByType<CustomCellGrid>();
            if (grid != null && grid.ShouldSuppressFrameworkSceneInput)
            {
                base.OnMouseEnter();
                return;
            }

            EnsureFrameworkSquareAnchor().RaiseCellHighlighted();
        }

        protected override void OnMouseExit()
        {
            CustomCellGrid grid = FindAnyObjectByType<CustomCellGrid>();
            if (grid != null && grid.ShouldSuppressFrameworkSceneInput)
            {
                base.OnMouseExit();
                return;
            }

            EnsureFrameworkSquareAnchor().RaiseCellDehighlighted();
        }

        internal void RaiseSceneHighlightEvent()
        {
            EnsureFrameworkSquareAnchor().RaiseCellHighlighted();
        }

        internal void RaiseSceneDehighlightEvent()
        {
            EnsureFrameworkSquareAnchor().RaiseCellDehighlighted();
        }
    }
}
