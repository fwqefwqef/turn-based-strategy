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

        public void EnsureRegistryCellBinding(CustomSquare[] candidateSquares = null)
        {
            if (cell != null)
            {
                CustomSquare host = cell.GetComponent<CustomSquare>();
                if (host != null && cell is not FrameworkSquareAnchor)
                {
                    cell = host.LegacyCell;
                }

                return;
            }

            CustomSquare closestSquare = FindClosestSquare(candidateSquares);
            if (closestSquare == null)
            {
                return;
            }

            cell = closestSquare.LegacyCell;
        }

        private CustomSquare FindClosestSquare(CustomSquare[] candidateSquares)
        {
            if (candidateSquares == null || candidateSquares.Length == 0)
            {
                candidateSquares = FindObjectsByType<CustomSquare>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }

            CustomSquare closestSquare = null;
            float closestDistance = float.MaxValue;
            const float maxBindingDistance = 1f;
            float maxBindingDistanceSqr = maxBindingDistance * maxBindingDistance;
            Vector3 slotPosition = transform.position;
            foreach (CustomSquare square in candidateSquares)
            {
                if (square == null)
                {
                    continue;
                }

                float distance = (square.transform.position - slotPosition).sqrMagnitude;
                if (distance > maxBindingDistanceSqr || distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                closestSquare = square;
            }

            return closestSquare;
        }

        private void Awake()
        {
            EnsureRegistryCellBinding();
            SyncToCell();
        }

        private void OnEnable()
        {
            EnsureRegistryCellBinding();
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
