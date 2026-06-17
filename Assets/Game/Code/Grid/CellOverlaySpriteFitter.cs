using UnityEngine;
using Windy.Srpg.Runtime.Grid;

namespace Windy.Srpg.Game.Grid
{
    /// <summary>
    /// Scales a cell overlay sprite (e.g. wall art on the Sprite child) to fit the parent tile.
    /// Uses the same target-size approach as unit preset sprite layout.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CellOverlaySpriteFitter : MonoBehaviour
    {
        [SerializeField] private Vector2 targetCellSize = Vector2.one;
        [SerializeField] private Vector2 offset;

        private Vector3 baselineLocalScale = Vector3.one;
        private Vector3 baselineLocalPosition;
        private bool baselineCaptured;

        private void Awake()
        {
            FitToCell();
        }

        private void OnEnable()
        {
            FitToCell();
        }

        private void OnValidate()
        {
            FitToCell();
        }

        public void FitToCell()
        {
            if (!TryGetComponent(out SpriteRenderer spriteRenderer) || spriteRenderer.sprite == null)
            {
                return;
            }

            CaptureBaselineIfNeeded();

            Vector2 referenceCellSize = ResolveReferenceCellSize();
            Vector2 targetWorldSize = new Vector2(
                referenceCellSize.x * targetCellSize.x,
                referenceCellSize.y * targetCellSize.y);

            float scaleFactor = ResolveScaleFactor(spriteRenderer.sprite, baselineLocalScale, targetWorldSize);
            Vector3 baseScale = baselineLocalScale == Vector3.zero ? Vector3.one : baselineLocalScale;
            spriteRenderer.transform.localScale = new Vector3(
                baseScale.x * scaleFactor,
                baseScale.y * scaleFactor,
                baseScale.z);
            spriteRenderer.transform.localPosition = baselineLocalPosition + new Vector3(offset.x, offset.y, 0f);
        }

        private void CaptureBaselineIfNeeded()
        {
            if (baselineCaptured)
            {
                return;
            }

            baselineLocalScale = transform.localScale;
            baselineLocalPosition = transform.localPosition;
            baselineCaptured = true;
        }

        private Vector2 ResolveReferenceCellSize()
        {
            Cell cell = GetComponentInParent<Cell>();
            if (cell != null)
            {
                Vector3 rawCellSize = cell.GetCellDimensions();
                Vector2 cellSize = new Vector2(Mathf.Abs(rawCellSize.x), Mathf.Abs(rawCellSize.y));
                if (cellSize.x > 0f && cellSize.y > 0f)
                {
                    return cellSize;
                }
            }

            return Vector2.one;
        }

        private static float ResolveScaleFactor(Sprite sprite, Vector3 baseScale, Vector2 targetWorldSize)
        {
            if (sprite == null || targetWorldSize.x <= 0f || targetWorldSize.y <= 0f)
            {
                return 1f;
            }

            Vector2 spriteSize = sprite.bounds.size;
            Vector2 baseSize = new Vector2(
                Mathf.Abs(baseScale.x) * spriteSize.x,
                Mathf.Abs(baseScale.y) * spriteSize.y);

            if (baseSize.x <= 0f || baseSize.y <= 0f)
            {
                return 1f;
            }

            float widthFactor = targetWorldSize.x / baseSize.x;
            float heightFactor = targetWorldSize.y / baseSize.y;
            return Mathf.Min(widthFactor, heightFactor);
        }
    }

}

