using TbsFramework.Cells;
using UnityEngine;

namespace Windy.Srpg.Game.Grid
{
    [ExecuteAlways]
    public sealed class DeploymentSlot : MonoBehaviour
    {
        private const int LegacyHiddenSortingOrder = -20;
        private const int DefaultSortingOrder = 5;

        [SerializeField] private int slotIndex;
        [SerializeField] private Cell cell;
        [SerializeField] private SpriteRenderer highlightRenderer;
        [SerializeField] private Color highlightColor = new Color(0.55f, 0.85f, 1f, 0.72f);
        [SerializeField] private Color selectedHighlightColor = new Color(1f, 0.9f, 0.2f, 0.88f);
        [SerializeField] private int sortingOrder = DefaultSortingOrder;
        [SerializeField] private float zOffset = 0.05f;

        private bool isSelected;

        public int SlotIndex => slotIndex;
        public Cell Cell => cell;

        private void Awake()
        {
            SyncToCell();
        }

        private void OnEnable()
        {
            SyncToCell();
        }

        private void OnValidate()
        {
            SyncToCell();
        }

        public void SyncToCell()
        {
            UpgradeLegacyVisualDefaults();

            if (highlightRenderer == null)
            {
                highlightRenderer = GetComponent<SpriteRenderer>();
            }

            RefreshVisual();

            if (cell == null)
            {
                return;
            }

            Vector3 targetPosition = cell.transform.position;
            targetPosition.z += zOffset;
            transform.position = targetPosition;
        }

        private void UpgradeLegacyVisualDefaults()
        {
            if (sortingOrder == LegacyHiddenSortingOrder)
            {
                sortingOrder = DefaultSortingOrder;
            }
        }

        public void SetSelected(bool selected)
        {
            if (isSelected == selected)
            {
                RefreshVisual();
                return;
            }

            isSelected = selected;
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (highlightRenderer == null)
            {
                highlightRenderer = GetComponent<SpriteRenderer>();
            }

            if (highlightRenderer == null)
            {
                return;
            }

            highlightRenderer.color = isSelected ? selectedHighlightColor : highlightColor;
            highlightRenderer.sortingOrder = sortingOrder;
        }
    }
}
