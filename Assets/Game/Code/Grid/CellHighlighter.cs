using UnityEngine;

namespace Windy.Srpg.Game.Grid
{
    public sealed class CellHighlighter : CellHighlighterBehaviour
    {
        private const string OverlayObjectName = "RuntimeOverlay";
        private const string EnemyRangeOverlayObjectName = "EnemyRangeOverlay";
        private const string BlackFogOverlayObjectName = "BlackFogOverlay";
        private const string BurningTerrainOverlayObjectName = "BurningTerrainOverlay";
        private const string EnemyRangeBorderTopName = "EnemyRangeBorderTop";
        private const string EnemyRangeBorderRightName = "EnemyRangeBorderRight";
        private const string EnemyRangeBorderBottomName = "EnemyRangeBorderBottom";
        private const string EnemyRangeBorderLeftName = "EnemyRangeBorderLeft";
        private const string BorderTopName = "BorderTop";
        private const string BorderRightName = "BorderRight";
        private const string BorderBottomName = "BorderBottom";
        private const string BorderLeftName = "BorderLeft";

        private static readonly Color HiddenOverlayColor = new Color(1f, 1f, 1f, 0f);
        private static readonly Color BlackFogColor = new Color(0.015f, 0.02f, 0.025f, 0.62f);
        private static readonly Color BurningTerrainColor = new Color(1f, 0.22f, 0.02f, 0.48f);
        private static readonly Color EnemyThreatCollectiveColor = new Color(1f, 0.56f, 0.78f, 0.28f);
        private static readonly Color EnemyThreatCollectiveBorderColor = new Color(1f, 0.56f, 0.78f, 0.95f);
        private static readonly Color EnemyThreatIndividualColor = new Color(1f, 0.46f, 0.46f, 0.32f);
        private static readonly Color EnemyThreatIndividualBorderColor = new Color(1f, 0.52f, 0.52f, 0.98f);
        private static readonly Color ReachableColor = new Color(0.08f, 0.2f, 0.7f, 0.45f);
        private static readonly Color PathColor = new Color(0.35f, 0.68f, 1f, 0.4f);
        private static readonly Color AttackColor = new Color(0.92f, 0.2f, 0.2f, 0.4f);
        private static readonly Color SupportColor = new Color(0.66f, 0.88f, 1f, 0.4f);
        private static readonly Color DeploymentColor = new Color(0.5f, 0.82f, 1f, 0.4f);
        private static readonly Color SelectedColor = new Color(1f, 0.92f, 0.25f, 0.4f);
        private static Sprite borderSprite;

        [SerializeField] private Renderer baseRenderer;
        [SerializeField] private Renderer overlayRenderer;
        [SerializeField] private Renderer enemyRangeOverlayRenderer;
        [SerializeField] private Renderer blackFogOverlayRenderer;
        [SerializeField] private Renderer burningTerrainOverlayRenderer;
        [SerializeField] private SpriteRenderer topBorderRenderer;
        [SerializeField] private SpriteRenderer rightBorderRenderer;
        [SerializeField] private SpriteRenderer bottomBorderRenderer;
        [SerializeField] private SpriteRenderer leftBorderRenderer;
        [SerializeField] private SpriteRenderer enemyRangeTopBorderRenderer;
        [SerializeField] private SpriteRenderer enemyRangeRightBorderRenderer;
        [SerializeField] private SpriteRenderer enemyRangeBottomBorderRenderer;
        [SerializeField] private SpriteRenderer enemyRangeLeftBorderRenderer;

        private SpriteRenderer baseSpriteRenderer;
        private SpriteRenderer overlaySpriteRenderer;
        private SpriteRenderer enemyRangeOverlaySpriteRenderer;
        private SpriteRenderer blackFogOverlaySpriteRenderer;
        private SpriteRenderer burningTerrainOverlaySpriteRenderer;

        private void Awake()
        {
            CacheRenderers();
            EnsureOverlayRenderer();
            EnsureEnemyRangeOverlayRenderer();
            EnsureBlackFogOverlayRenderer();
            EnsureBurningTerrainOverlayRenderer();
            SetOverlayColor(HiddenOverlayColor);
            SetEnemyRangeOverlayColor(HiddenOverlayColor);
            SetBlackFogOverlayColor(HiddenOverlayColor);
            SetBurningTerrainOverlayColor(HiddenOverlayColor);
        }

        private void OnValidate()
        {
            CacheRenderers();
            BindExistingOverlayRenderer();
            BindExistingEnemyRangeOverlayRenderer();
            BindExistingBlackFogOverlayRenderer();
            BindExistingBurningTerrainOverlayRenderer();
            SetOverlayColor(HiddenOverlayColor);
            SetEnemyRangeOverlayColor(HiddenOverlayColor);
            SetBlackFogOverlayColor(HiddenOverlayColor);
            SetBurningTerrainOverlayColor(HiddenOverlayColor);
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

        public override void ApplyEnemyRangeOverlay(Cell cell, EnemyRangeOverlayKind overlayKind)
        {
            ApplyEnemyRangeOverlay(cell, overlayKind, top: true, right: true, bottom: true, left: true);
        }

        public override void ApplyEnemyRangeOverlay(Cell cell, EnemyRangeOverlayKind overlayKind, bool top, bool right, bool bottom, bool left)
        {
            EnsureEnemyRangeOverlayRenderer();
            EnsureEnemyRangeBorderRenderers();
            SetEnemyRangeOverlayColor(GetEnemyRangeFillColor(overlayKind));
            Color borderColor = overlayKind == EnemyRangeOverlayKind.None ? Color.clear : GetEnemyRangeBorderColor(overlayKind);
            SetEnemyRangeBorderState(top, right, bottom, left, borderColor, overlayKind != EnemyRangeOverlayKind.None);
        }

        public override void ClearEnemyRangeOverlay(Cell cell)
        {
            EnsureEnemyRangeOverlayRenderer();
            SetEnemyRangeOverlayColor(HiddenOverlayColor);
            SetEnemyRangeBorderState(false, false, false, false, Color.clear, false);
        }

        public override void ApplyBlackFogOverlay(Cell cell)
        {
            EnsureBlackFogOverlayRenderer();
            SetBlackFogOverlayColor(BlackFogColor);
        }

        public override void ClearBlackFogOverlay(Cell cell)
        {
            EnsureBlackFogOverlayRenderer();
            SetBlackFogOverlayColor(HiddenOverlayColor);
        }

        public override void ApplyBurningTerrainOverlay(Cell cell)
        {
            EnsureBurningTerrainOverlayRenderer();
            SetBurningTerrainOverlayColor(BurningTerrainColor);
        }

        public override void ClearBurningTerrainOverlay(Cell cell)
        {
            EnsureBurningTerrainOverlayRenderer();
            SetBurningTerrainOverlayColor(HiddenOverlayColor);
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
            enemyRangeOverlaySpriteRenderer = enemyRangeOverlayRenderer as SpriteRenderer;
            blackFogOverlaySpriteRenderer = blackFogOverlayRenderer as SpriteRenderer;
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
            overlaySpriteRenderer.sortingOrder = baseSpriteRenderer.sortingOrder + 3;
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

        private void EnsureEnemyRangeOverlayRenderer()
        {
            if (enemyRangeOverlayRenderer != null || baseSpriteRenderer == null)
            {
                enemyRangeOverlaySpriteRenderer = enemyRangeOverlayRenderer as SpriteRenderer;
                ConfigureEnemyRangeOverlayTransform();
                return;
            }

            if (BindExistingEnemyRangeOverlayRenderer())
            {
                return;
            }

            GameObject overlayObject = new GameObject(EnemyRangeOverlayObjectName);
            overlayObject.transform.SetParent(transform, false);
            overlayObject.transform.localPosition = new Vector3(0f, 0f, -0.015f);

            enemyRangeOverlaySpriteRenderer = overlayObject.AddComponent<SpriteRenderer>();
            enemyRangeOverlaySpriteRenderer.sprite = GetBorderSprite();
            enemyRangeOverlaySpriteRenderer.color = HiddenOverlayColor;
            enemyRangeOverlaySpriteRenderer.flipX = baseSpriteRenderer.flipX;
            enemyRangeOverlaySpriteRenderer.flipY = baseSpriteRenderer.flipY;
            enemyRangeOverlaySpriteRenderer.drawMode = SpriteDrawMode.Simple;
            enemyRangeOverlaySpriteRenderer.size = Vector2.one;
            enemyRangeOverlaySpriteRenderer.maskInteraction = baseSpriteRenderer.maskInteraction;
            enemyRangeOverlaySpriteRenderer.sortingLayerID = baseSpriteRenderer.sortingLayerID;
            enemyRangeOverlaySpriteRenderer.sortingOrder = baseSpriteRenderer.sortingOrder + 1;
            enemyRangeOverlaySpriteRenderer.spriteSortPoint = baseSpriteRenderer.spriteSortPoint;
            enemyRangeOverlayRenderer = enemyRangeOverlaySpriteRenderer;
            ConfigureEnemyRangeOverlayTransform();
        }

        private bool BindExistingEnemyRangeOverlayRenderer()
        {
            Transform existingOverlay = transform.Find(EnemyRangeOverlayObjectName);
            if (existingOverlay == null)
            {
                enemyRangeOverlayRenderer = null;
                enemyRangeOverlaySpriteRenderer = null;
                return false;
            }

            enemyRangeOverlayRenderer = existingOverlay.GetComponent<Renderer>();
            enemyRangeOverlaySpriteRenderer = enemyRangeOverlayRenderer as SpriteRenderer;
            ConfigureEnemyRangeOverlayTransform();
            return enemyRangeOverlayRenderer != null;
        }

        private void EnsureBlackFogOverlayRenderer()
        {
            if (blackFogOverlayRenderer != null || baseSpriteRenderer == null)
            {
                blackFogOverlaySpriteRenderer = blackFogOverlayRenderer as SpriteRenderer;
                ConfigureBlackFogOverlayTransform();
                return;
            }

            if (BindExistingBlackFogOverlayRenderer())
            {
                return;
            }

            GameObject overlayObject = new GameObject(BlackFogOverlayObjectName);
            overlayObject.transform.SetParent(transform, false);

            blackFogOverlaySpriteRenderer = overlayObject.AddComponent<SpriteRenderer>();
            blackFogOverlaySpriteRenderer.sprite = GetBorderSprite();
            blackFogOverlaySpriteRenderer.color = HiddenOverlayColor;
            blackFogOverlaySpriteRenderer.flipX = baseSpriteRenderer.flipX;
            blackFogOverlaySpriteRenderer.flipY = baseSpriteRenderer.flipY;
            blackFogOverlaySpriteRenderer.drawMode = SpriteDrawMode.Simple;
            blackFogOverlaySpriteRenderer.size = Vector2.one;
            blackFogOverlaySpriteRenderer.maskInteraction = baseSpriteRenderer.maskInteraction;
            blackFogOverlaySpriteRenderer.sortingLayerID = baseSpriteRenderer.sortingLayerID;
            blackFogOverlaySpriteRenderer.sortingOrder = baseSpriteRenderer.sortingOrder + 1;
            blackFogOverlaySpriteRenderer.spriteSortPoint = baseSpriteRenderer.spriteSortPoint;
            blackFogOverlayRenderer = blackFogOverlaySpriteRenderer;
            ConfigureBlackFogOverlayTransform();
        }

        private bool BindExistingBlackFogOverlayRenderer()
        {
            Transform existingOverlay = transform.Find(BlackFogOverlayObjectName);
            if (existingOverlay == null)
            {
                blackFogOverlayRenderer = null;
                blackFogOverlaySpriteRenderer = null;
                return false;
            }

            blackFogOverlayRenderer = existingOverlay.GetComponent<Renderer>();
            blackFogOverlaySpriteRenderer = blackFogOverlayRenderer as SpriteRenderer;
            ConfigureBlackFogOverlayTransform();
            return blackFogOverlayRenderer != null;
        }

        private void EnsureBurningTerrainOverlayRenderer()
        {
            if (burningTerrainOverlayRenderer != null || baseSpriteRenderer == null)
            {
                burningTerrainOverlaySpriteRenderer = burningTerrainOverlayRenderer as SpriteRenderer;
                ConfigureBurningTerrainOverlayTransform();
                return;
            }

            if (BindExistingBurningTerrainOverlayRenderer())
            {
                return;
            }

            GameObject overlayObject = new GameObject(BurningTerrainOverlayObjectName);
            overlayObject.transform.SetParent(transform, false);
            burningTerrainOverlaySpriteRenderer = overlayObject.AddComponent<SpriteRenderer>();
            burningTerrainOverlaySpriteRenderer.sprite = GetBorderSprite();
            burningTerrainOverlaySpriteRenderer.color = HiddenOverlayColor;
            burningTerrainOverlaySpriteRenderer.flipX = baseSpriteRenderer.flipX;
            burningTerrainOverlaySpriteRenderer.flipY = baseSpriteRenderer.flipY;
            burningTerrainOverlaySpriteRenderer.drawMode = SpriteDrawMode.Simple;
            burningTerrainOverlaySpriteRenderer.size = Vector2.one;
            burningTerrainOverlaySpriteRenderer.maskInteraction = baseSpriteRenderer.maskInteraction;
            burningTerrainOverlaySpriteRenderer.sortingLayerID = baseSpriteRenderer.sortingLayerID;
            burningTerrainOverlaySpriteRenderer.sortingOrder = baseSpriteRenderer.sortingOrder + 2;
            burningTerrainOverlaySpriteRenderer.spriteSortPoint = baseSpriteRenderer.spriteSortPoint;
            burningTerrainOverlayRenderer = burningTerrainOverlaySpriteRenderer;
            ConfigureBurningTerrainOverlayTransform();
        }

        private bool BindExistingBurningTerrainOverlayRenderer()
        {
            Transform existingOverlay = transform.Find(BurningTerrainOverlayObjectName);
            if (existingOverlay == null)
            {
                burningTerrainOverlayRenderer = null;
                burningTerrainOverlaySpriteRenderer = null;
                return false;
            }

            burningTerrainOverlayRenderer = existingOverlay.GetComponent<Renderer>();
            burningTerrainOverlaySpriteRenderer = burningTerrainOverlayRenderer as SpriteRenderer;
            ConfigureBurningTerrainOverlayTransform();
            return burningTerrainOverlayRenderer != null;
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

        private void EnsureEnemyRangeBorderRenderers()
        {
            if (baseSpriteRenderer == null)
            {
                CacheRenderers();
            }

            if (baseSpriteRenderer == null)
            {
                return;
            }

            enemyRangeTopBorderRenderer = EnsureBorderRenderer(EnemyRangeBorderTopName, enemyRangeTopBorderRenderer, baseSpriteRenderer, baseSpriteRenderer.sortingOrder + 2);
            enemyRangeRightBorderRenderer = EnsureBorderRenderer(EnemyRangeBorderRightName, enemyRangeRightBorderRenderer, baseSpriteRenderer, baseSpriteRenderer.sortingOrder + 2);
            enemyRangeBottomBorderRenderer = EnsureBorderRenderer(EnemyRangeBorderBottomName, enemyRangeBottomBorderRenderer, baseSpriteRenderer, baseSpriteRenderer.sortingOrder + 2);
            enemyRangeLeftBorderRenderer = EnsureBorderRenderer(EnemyRangeBorderLeftName, enemyRangeLeftBorderRenderer, baseSpriteRenderer, baseSpriteRenderer.sortingOrder + 2);
            ConfigureEnemyRangeBorderLayout();
        }

        private SpriteRenderer EnsureBorderRenderer(string objectName, SpriteRenderer existingRenderer, SpriteRenderer spriteRenderer)
        {
            return EnsureBorderRenderer(objectName, existingRenderer, spriteRenderer, spriteRenderer.sortingOrder + 4);
        }

        private SpriteRenderer EnsureBorderRenderer(string objectName, SpriteRenderer existingRenderer, SpriteRenderer spriteRenderer, int sortingOrder)
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
            borderRenderer.sortingOrder = sortingOrder;
            borderRenderer.maskInteraction = spriteRenderer.maskInteraction;
            return borderRenderer;
        }

        private void ConfigureBorderLayout()
        {
            if (baseSpriteRenderer == null)
            {
                return;
            }

            Vector2 size = ResolveCellWorldSize();
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

        private void ConfigureEnemyRangeBorderLayout()
        {
            if (baseSpriteRenderer == null)
            {
                return;
            }

            Vector2 size = ResolveCellWorldSize();
            float width = Mathf.Abs(size.x);
            float height = Mathf.Abs(size.y);
            if (width <= 0f || height <= 0f)
            {
                width = 1f;
                height = 1f;
            }

            float thickness = Mathf.Max(0.045f, Mathf.Min(width, height) * 0.09f);
            ConfigureBorderRenderer(enemyRangeTopBorderRenderer, new Vector3(0f, height * 0.5f - thickness * 0.5f, -0.025f), new Vector3(width, thickness, 1f));
            ConfigureBorderRenderer(enemyRangeBottomBorderRenderer, new Vector3(0f, -height * 0.5f + thickness * 0.5f, -0.025f), new Vector3(width, thickness, 1f));
            ConfigureBorderRenderer(enemyRangeRightBorderRenderer, new Vector3(width * 0.5f - thickness * 0.5f, 0f, -0.025f), new Vector3(thickness, height, 1f));
            ConfigureBorderRenderer(enemyRangeLeftBorderRenderer, new Vector3(-width * 0.5f + thickness * 0.5f, 0f, -0.025f), new Vector3(thickness, height, 1f));
        }

        private Vector2 ResolveCellWorldSize()
        {
            // Cell restores its collider to a one-cell world footprint even when a
            // source sprite needs a compensating transform scale. Using the collider
            // therefore keeps borders independent of sprite rect and pixels-per-unit.
            if (TryGetComponent(out BoxCollider boxCollider))
            {
                Vector3 colliderSize = boxCollider.bounds.size;
                if (colliderSize.x > 0.0001f && colliderSize.y > 0.0001f)
                {
                    return new Vector2(colliderSize.x, colliderSize.y);
                }
            }

            if (baseSpriteRenderer != null)
            {
                Vector3 renderedSize = baseSpriteRenderer.bounds.size;
                if (renderedSize.x > 0.0001f && renderedSize.y > 0.0001f)
                {
                    return new Vector2(renderedSize.x, renderedSize.y);
                }
            }

            return Vector2.one;
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

        private void ConfigureEnemyRangeOverlayTransform()
        {
            if (enemyRangeOverlaySpriteRenderer == null)
            {
                return;
            }

            Vector3 inverseParentScale = ResolveInverseParentScale(enemyRangeOverlaySpriteRenderer.transform.parent);
            enemyRangeOverlaySpriteRenderer.transform.localPosition = Vector3.Scale(new Vector3(0f, 0f, -0.015f), inverseParentScale);
            enemyRangeOverlaySpriteRenderer.transform.localRotation = Quaternion.identity;
            enemyRangeOverlaySpriteRenderer.transform.localScale = inverseParentScale;
        }

        private void ConfigureBlackFogOverlayTransform()
        {
            if (blackFogOverlaySpriteRenderer == null)
            {
                return;
            }

            Vector3 inverseParentScale = ResolveInverseParentScale(blackFogOverlaySpriteRenderer.transform.parent);
            blackFogOverlaySpriteRenderer.transform.localPosition = Vector3.Scale(new Vector3(0f, 0f, -0.005f), inverseParentScale);
            blackFogOverlaySpriteRenderer.transform.localRotation = Quaternion.identity;
            blackFogOverlaySpriteRenderer.transform.localScale = inverseParentScale;
        }

        private void ConfigureBurningTerrainOverlayTransform()
        {
            if (burningTerrainOverlaySpriteRenderer == null)
            {
                return;
            }

            Vector3 inverseParentScale = ResolveInverseParentScale(burningTerrainOverlaySpriteRenderer.transform.parent);
            burningTerrainOverlaySpriteRenderer.transform.localPosition = Vector3.Scale(new Vector3(0f, 0f, -0.004f), inverseParentScale);
            burningTerrainOverlaySpriteRenderer.transform.localRotation = Quaternion.identity;
            burningTerrainOverlaySpriteRenderer.transform.localScale = inverseParentScale;
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

            Vector3 scale = parent.lossyScale;
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

        private void SetEnemyRangeOverlayColor(Color color)
        {
            if (enemyRangeOverlaySpriteRenderer != null)
            {
                enemyRangeOverlaySpriteRenderer.color = color;
            }
            else if (enemyRangeOverlayRenderer != null)
            {
                enemyRangeOverlayRenderer.material.color = color;
            }
        }

        private void SetBlackFogOverlayColor(Color color)
        {
            if (blackFogOverlaySpriteRenderer != null)
            {
                blackFogOverlaySpriteRenderer.color = color;
            }
            else if (blackFogOverlayRenderer != null)
            {
                blackFogOverlayRenderer.material.color = color;
            }
        }

        private void SetBurningTerrainOverlayColor(Color color)
        {
            if (burningTerrainOverlaySpriteRenderer != null)
            {
                burningTerrainOverlaySpriteRenderer.color = color;
            }
            else if (burningTerrainOverlayRenderer != null)
            {
                burningTerrainOverlayRenderer.material.color = color;
            }
        }

        private void SetEnemyRangeBorderState(bool top, bool right, bool bottom, bool left, Color color, bool visible)
        {
            SetBorderRendererState(enemyRangeTopBorderRenderer, visible && top, color);
            SetBorderRendererState(enemyRangeRightBorderRenderer, visible && right, color);
            SetBorderRendererState(enemyRangeBottomBorderRenderer, visible && bottom, color);
            SetBorderRendererState(enemyRangeLeftBorderRenderer, visible && left, color);
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

        private static Color GetEnemyRangeFillColor(EnemyRangeOverlayKind overlayKind)
        {
            return overlayKind switch
            {
                EnemyRangeOverlayKind.Collective => EnemyThreatCollectiveColor,
                EnemyRangeOverlayKind.Individual => EnemyThreatIndividualColor,
                _ => HiddenOverlayColor
            };
        }

        private static Color GetEnemyRangeBorderColor(EnemyRangeOverlayKind overlayKind)
        {
            return overlayKind switch
            {
                EnemyRangeOverlayKind.Collective => EnemyThreatCollectiveBorderColor,
                EnemyRangeOverlayKind.Individual => EnemyThreatIndividualBorderColor,
                _ => Color.clear
            };
        }
    }
}

