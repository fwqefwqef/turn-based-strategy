using UnityEngine;

namespace Windy.Srpg.Game.Grid
{
    public sealed class CellHighlighter : CellHighlighterBehaviour
    {
        private const string OverlayObjectName = "RuntimeOverlay";
        private const string BorderTopName = "BorderTop";
        private const string BorderRightName = "BorderRight";
        private const string BorderBottomName = "BorderBottom";
        private const string BorderLeftName = "BorderLeft";

        private static readonly Color HiddenOverlayColor = new Color(1f, 1f, 1f, 0f);
        private static readonly Color ReachableColor = new Color(0.08f, 0.2f, 0.7f, 0.45f);
        private static readonly Color PathColor = new Color(0.35f, 0.68f, 1f, 0.4f);
        private static readonly Color AttackColor = new Color(0.92f, 0.2f, 0.2f, 0.4f);
        private static readonly Color SupportColor = new Color(0.35f, 0.9f, 0.35f, 0.4f);
        private static readonly Color DeploymentColor = new Color(0.5f, 0.82f, 1f, 0.4f);
        private static readonly Color SelectedColor = new Color(1f, 0.92f, 0.25f, 0.4f);
        private static Sprite borderSprite;

        [SerializeField] private Renderer baseRenderer;
        [SerializeField] private Renderer overlayRenderer;
        [SerializeField] private SpriteRenderer topBorderRenderer;
        [SerializeField] private SpriteRenderer rightBorderRenderer;
        [SerializeField] private SpriteRenderer bottomBorderRenderer;
        [SerializeField] private SpriteRenderer leftBorderRenderer;

        private SpriteRenderer baseSpriteRenderer;
        private SpriteRenderer overlaySpriteRenderer;

        private void Awake()
        {
            CacheRenderers();
            EnsureOverlayRenderer();
            SetOverlayColor(HiddenOverlayColor);
        }

        private void OnValidate()
        {
            CacheRenderers();
            BindExistingOverlayRenderer();
            SetOverlayColor(HiddenOverlayColor);
        }

        public override void Apply(Cell cell, CellHighlightKind highlightKind)
        {
            EnsureOverlayRenderer();
            SetOverlayColor(GetHighlightColor(highlightKind));
        }

        public void ApplyFaint(Cell cell, CellHighlightKind highlightKind)
        {
            EnsureOverlayRenderer();
            Color color = GetHighlightColor(highlightKind);
            color.a *= 0.4f;
            SetOverlayColor(color);
        }

        public override void Clear(Cell cell)
        {
            EnsureOverlayRenderer();
            SetOverlayColor(HiddenOverlayColor);
        }

        public override void ShowCursorBorder(Cell cell, Color color)
        {
            ShowPreviewBorder(true, true, true, true, color);
        }

        public override void ClearCursorBorder(Cell cell)
        {
            ClearPreviewBorder();
        }

        public void ShowPreviewBorder(bool top, bool right, bool bottom, bool left, Color color)
        {
            EnsureBorderRenderers();
            ClearPreviewBorder();
            SetBorderRendererState(topBorderRenderer, top, color);
            SetBorderRendererState(rightBorderRenderer, right, color);
            SetBorderRendererState(bottomBorderRenderer, bottom, color);
            SetBorderRendererState(leftBorderRenderer, left, color);
        }

        public void ClearPreviewBorder()
        {
            SetBorderRendererState(topBorderRenderer, false, Color.clear);
            SetBorderRendererState(rightBorderRenderer, false, Color.clear);
            SetBorderRendererState(bottomBorderRenderer, false, Color.clear);
            SetBorderRendererState(leftBorderRenderer, false, Color.clear);
        }

        private void CacheRenderers()
        {
            if (baseRenderer == null)
            {
                baseRenderer = GetComponent<Renderer>();
            }

            baseSpriteRenderer = baseRenderer as SpriteRenderer;
            overlaySpriteRenderer = overlayRenderer as SpriteRenderer;
        }

        private void EnsureOverlayRenderer()
        {
            if (overlayRenderer != null || baseSpriteRenderer == null)
            {
                overlaySpriteRenderer = overlayRenderer as SpriteRenderer;
                ConfigureOverlayTransform();
                return;
            }

            if (BindExistingOverlayRenderer())
            {
                return;
            }

            GameObject overlayObject = new GameObject(OverlayObjectName);
            overlayObject.transform.SetParent(transform, false);
            overlayObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);

            overlaySpriteRenderer = overlayObject.AddComponent<SpriteRenderer>();
            overlaySpriteRenderer.sprite = GetBorderSprite();
            overlaySpriteRenderer.color = HiddenOverlayColor;
            overlaySpriteRenderer.flipX = baseSpriteRenderer.flipX;
            overlaySpriteRenderer.flipY = baseSpriteRenderer.flipY;
            overlaySpriteRenderer.drawMode = SpriteDrawMode.Simple;
            overlaySpriteRenderer.size = Vector2.one;
            overlaySpriteRenderer.maskInteraction = baseSpriteRenderer.maskInteraction;
            overlaySpriteRenderer.sortingLayerID = baseSpriteRenderer.sortingLayerID;
            overlaySpriteRenderer.sortingOrder = baseSpriteRenderer.sortingOrder + 1;
            overlaySpriteRenderer.spriteSortPoint = baseSpriteRenderer.spriteSortPoint;
            overlayRenderer = overlaySpriteRenderer;
            ConfigureOverlayTransform();
        }

        private bool BindExistingOverlayRenderer()
        {
            Transform existingOverlay = transform.Find(OverlayObjectName);
            if (existingOverlay == null)
            {
                overlayRenderer = null;
                overlaySpriteRenderer = null;
                return false;
            }

            overlayRenderer = existingOverlay.GetComponent<Renderer>();
            overlaySpriteRenderer = overlayRenderer as SpriteRenderer;
            ConfigureOverlayTransform();
            return overlayRenderer != null;
        }

        private void EnsureBorderRenderers()
        {
            if (baseSpriteRenderer == null)
            {
                CacheRenderers();
            }

            if (baseSpriteRenderer == null)
            {
                return;
            }

            topBorderRenderer = EnsureBorderRenderer(BorderTopName, topBorderRenderer, baseSpriteRenderer);
            rightBorderRenderer = EnsureBorderRenderer(BorderRightName, rightBorderRenderer, baseSpriteRenderer);
            bottomBorderRenderer = EnsureBorderRenderer(BorderBottomName, bottomBorderRenderer, baseSpriteRenderer);
            leftBorderRenderer = EnsureBorderRenderer(BorderLeftName, leftBorderRenderer, baseSpriteRenderer);
            ConfigureBorderLayout();
        }

        private SpriteRenderer EnsureBorderRenderer(string objectName, SpriteRenderer existingRenderer, SpriteRenderer spriteRenderer)
        {
            if (existingRenderer != null)
            {
                return existingRenderer;
            }

            Transform existingTransform = transform.Find(objectName);
            if (existingTransform != null)
            {
                SpriteRenderer existing = existingTransform.GetComponent<SpriteRenderer>();
                if (existing != null)
                {
                    return existing;
                }
            }

            GameObject borderObject = new GameObject(objectName);
            borderObject.transform.SetParent(transform, false);
            SpriteRenderer borderRenderer = borderObject.AddComponent<SpriteRenderer>();
            borderRenderer.sprite = GetBorderSprite();
            borderRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            borderRenderer.sortingOrder = spriteRenderer.sortingOrder + 2;
            borderRenderer.maskInteraction = spriteRenderer.maskInteraction;
            return borderRenderer;
        }

        private void ConfigureBorderLayout()
        {
            if (baseSpriteRenderer == null)
            {
                return;
            }

            Vector2 size = baseSpriteRenderer.size;
            float width = Mathf.Abs(size.x);
            float height = Mathf.Abs(size.y);
            if (width <= 0f || height <= 0f)
            {
                width = 1f;
                height = 1f;
            }

            float thickness = Mathf.Max(0.06f, Mathf.Min(width, height) * 0.12f);
            ConfigureBorderRenderer(topBorderRenderer, new Vector3(0f, height * 0.5f - thickness * 0.5f, -0.02f), new Vector3(width, thickness, 1f));
            ConfigureBorderRenderer(bottomBorderRenderer, new Vector3(0f, -height * 0.5f + thickness * 0.5f, -0.02f), new Vector3(width, thickness, 1f));
            ConfigureBorderRenderer(rightBorderRenderer, new Vector3(width * 0.5f - thickness * 0.5f, 0f, -0.02f), new Vector3(thickness, height, 1f));
            ConfigureBorderRenderer(leftBorderRenderer, new Vector3(-width * 0.5f + thickness * 0.5f, 0f, -0.02f), new Vector3(thickness, height, 1f));
        }

        private static void ConfigureBorderRenderer(SpriteRenderer renderer, Vector3 localPosition, Vector3 localScale)
        {
            if (renderer == null)
            {
                return;
            }

            Vector3 inverseParentScale = ResolveInverseParentScale(renderer.transform.parent);
            renderer.transform.localPosition = Vector3.Scale(localPosition, inverseParentScale);
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.Scale(localScale, inverseParentScale);
        }

        private void ConfigureOverlayTransform()
        {
            if (overlaySpriteRenderer == null)
            {
                return;
            }

            Vector3 inverseParentScale = ResolveInverseParentScale(overlaySpriteRenderer.transform.parent);
            overlaySpriteRenderer.transform.localPosition = Vector3.Scale(new Vector3(0f, 0f, -0.01f), inverseParentScale);
            overlaySpriteRenderer.transform.localRotation = Quaternion.identity;
            overlaySpriteRenderer.transform.localScale = inverseParentScale;
        }

        private static Sprite GetBorderSprite()
        {
            if (borderSprite != null)
            {
                return borderSprite;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            borderSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return borderSprite;
        }

        private static void SetBorderRendererState(SpriteRenderer renderer, bool visible, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.gameObject.SetActive(visible);
            renderer.color = color;
        }

        private static Vector3 ResolveInverseParentScale(Transform parent)
        {
            if (parent == null)
            {
                return Vector3.one;
            }

            Vector3 scale = parent.localScale;
            return new Vector3(
                Mathf.Abs(scale.x) > 0.0001f ? 1f / scale.x : 1f,
                Mathf.Abs(scale.y) > 0.0001f ? 1f / scale.y : 1f,
                Mathf.Abs(scale.z) > 0.0001f ? 1f / scale.z : 1f);
        }

        private void SetOverlayColor(Color color)
        {
            if (overlaySpriteRenderer != null)
            {
                overlaySpriteRenderer.color = color;
            }
            else if (overlayRenderer != null)
            {
                overlayRenderer.material.color = color;
            }
        }

        private static Color GetHighlightColor(CellHighlightKind highlightKind)
        {
            return highlightKind switch
            {
                CellHighlightKind.Reachable => ReachableColor,
                CellHighlightKind.Path => PathColor,
                CellHighlightKind.Attack => AttackColor,
                CellHighlightKind.Support => SupportColor,
                CellHighlightKind.Deployment => DeploymentColor,
                CellHighlightKind.Selected => SelectedColor,
                _ => HiddenOverlayColor
            };
        }
    }
}

