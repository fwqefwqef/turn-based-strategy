using System.Collections.Generic;
using TbsFramework.Cells.Highlighters;
using UnityEngine;
using UnityEngine.EventSystems;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid
{
    public abstract partial class CustomSquare : MonoBehaviour, IBattleCell
    {
        [SerializeField] private Vector2 _offsetCoord;

        public Vector2 OffsetCoord
        {
            get => _offsetCoord;
            set
            {
                _offsetCoord = value;
                SyncRegistryAnchorFromCustomSquare();
            }
        }

        public bool IsTaken;
        public float MovementCost = 1f;

        public List<CellHighlighter> MarkAsAttackPreviewFn;
        public List<CellHighlighter> MarkAsTradePreviewFn;
        public List<CellHighlighter> MarkAsAnyPreviewFn;

        protected virtual void Awake()
        {
            EnsureFrameworkSquareAnchor();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureFrameworkSquareAnchor();
            SyncRegistryAnchorFromCustomSquare();
        }
#endif

        public abstract Vector3 GetCellDimensions();

        public virtual void OnMouseDown()
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

            EnsureFrameworkSquareAnchor().RaiseCellClicked();
        }

        public virtual void OnMouseEnter()
        {
            CustomCellGrid grid = FindAnyObjectByType<CustomCellGrid>();
            if (grid != null && grid.ShouldSuppressFrameworkSceneInput)
            {
                return;
            }

            EnsureFrameworkSquareAnchor().RaiseCellHighlighted();
        }

        public virtual void OnMouseExit()
        {
            CustomCellGrid grid = FindAnyObjectByType<CustomCellGrid>();
            if (grid != null && grid.ShouldSuppressFrameworkSceneInput)
            {
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

        public virtual void MarkAsHighlighted()
        {
        }

        public virtual void MarkAsPath()
        {
        }

        public virtual void MarkAsReachable()
        {
        }

        public virtual void UnMark()
        {
        }

        public virtual void MarkAsAttackPreview()
        {
            MarkAsAttackPreviewFn?.ForEach(o => o.Apply(LegacyCell));
        }

        public virtual void MarkAsTradePreview()
        {
            MarkAsTradePreviewFn?.ForEach(o => o.Apply(LegacyCell));
        }

        public virtual void MarkAsAnyPreview()
        {
            MarkAsAnyPreviewFn?.ForEach(o => o.Apply(LegacyCell));
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

        public virtual void ShowPreviewBorder(bool top, bool right, bool bottom, bool left, Color color)
        {
        }

        public virtual void ClearPreviewBorder()
        {
        }
    }
}
