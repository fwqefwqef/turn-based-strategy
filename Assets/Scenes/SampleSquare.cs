using Windy.Srpg.Game.Grid;
using UnityEngine;

public class SampleSquare : CustomSquare
{
    private const string HighlightOverlayName = "HighlightOverlay";
    private const string BorderTopName = "BorderTop";
    private const string BorderRightName = "BorderRight";
    private const string BorderBottomName = "BorderBottom";
    private const string BorderLeftName = "BorderLeft";
    private static readonly Color HighlightedColor = new Color(1f, 0.92f, 0.25f, 0.4f);
    private static readonly Color ReachableColor = new Color(0.08f, 0.2f, 0.7f, 0.45f);
    private static readonly Color PathColor = new Color(0.35f, 0.68f, 1f, 0.4f);
    private static readonly Color AttackPreviewColor = new Color(0.92f, 0.2f, 0.2f, 0.4f);
    private static readonly Color TradePreviewColor = new Color(0.5f, 0.82f, 1f, 0.4f);
    private static readonly Color AnyPreviewColor = new Color(0.35f, 0.9f, 0.35f, 0.4f);
    private static readonly Color AttackPreviewFaintColor = new Color(0.92f, 0.2f, 0.2f, 0.16f);
    private static readonly Color TradePreviewFaintColor = new Color(0.5f, 0.82f, 1f, 0.16f);
    private static readonly Color AnyPreviewFaintColor = new Color(0.35f, 0.9f, 0.35f, 0.16f);
    private static readonly Color HiddenOverlayColor = new Color(1f, 1f, 1f, 0f);
    private static Sprite _borderSprite;

    [SerializeField] private Renderer baseRenderer;
    [SerializeField] private Renderer highlightOverlayRenderer;
    [SerializeField] private SpriteRenderer topBorderRenderer;
    [SerializeField] private SpriteRenderer rightBorderRenderer;
    [SerializeField] private SpriteRenderer bottomBorderRenderer;
    [SerializeField] private SpriteRenderer leftBorderRenderer;

    private Color _baseColor = Color.white;
    private SpriteRenderer _baseSpriteRenderer;
    private SpriteRenderer _highlightOverlaySpriteRenderer;

    private void Awake()
    {
        if (baseRenderer == null)
        {
            baseRenderer = GetComponent<Renderer>();
        }

        _baseSpriteRenderer = baseRenderer as SpriteRenderer;

        EnsureHighlightOverlayRenderer();
        EnsureBorderRenderers();

        _highlightOverlaySpriteRenderer = highlightOverlayRenderer as SpriteRenderer;

        if (_baseSpriteRenderer != null)
        {
            _baseColor = _baseSpriteRenderer.color;
        }
        else if (baseRenderer != null)
        {
            _baseColor = baseRenderer.material.color;
        }

        if (_highlightOverlaySpriteRenderer != null)
        {
            _highlightOverlaySpriteRenderer.color = HiddenOverlayColor;
        }
        else if (highlightOverlayRenderer != null)
        {
            highlightOverlayRenderer.material.color = HiddenOverlayColor;
        }
    }

    private void EnsureHighlightOverlayRenderer()
    {
        if (highlightOverlayRenderer != null || baseRenderer == null)
        {
            return;
        }

        if (baseRenderer is not SpriteRenderer baseSpriteRenderer)
        {
            return;
        }

        Transform existingOverlay = transform.Find(HighlightOverlayName);
        if (existingOverlay != null)
        {
            highlightOverlayRenderer = existingOverlay.GetComponent<Renderer>();
            if (highlightOverlayRenderer != null)
            {
                _highlightOverlaySpriteRenderer = highlightOverlayRenderer as SpriteRenderer;
                return;
            }
        }

        GameObject overlayObject = new GameObject(HighlightOverlayName);
        overlayObject.transform.SetParent(transform, false);
        overlayObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);

        SpriteRenderer overlaySpriteRenderer = overlayObject.AddComponent<SpriteRenderer>();
        overlaySpriteRenderer.sprite = baseSpriteRenderer.sprite;
        overlaySpriteRenderer.color = HiddenOverlayColor;
        overlaySpriteRenderer.flipX = baseSpriteRenderer.flipX;
        overlaySpriteRenderer.flipY = baseSpriteRenderer.flipY;
        overlaySpriteRenderer.drawMode = baseSpriteRenderer.drawMode;
        overlaySpriteRenderer.size = baseSpriteRenderer.size;
        overlaySpriteRenderer.maskInteraction = baseSpriteRenderer.maskInteraction;
        overlaySpriteRenderer.sortingLayerID = baseSpriteRenderer.sortingLayerID;
        overlaySpriteRenderer.sortingOrder = baseSpriteRenderer.sortingOrder + 1;
        overlaySpriteRenderer.spriteSortPoint = baseSpriteRenderer.spriteSortPoint;
        highlightOverlayRenderer = overlaySpriteRenderer;
        _highlightOverlaySpriteRenderer = overlaySpriteRenderer;
    }

    private void EnsureBorderRenderers()
    {
        if (baseRenderer is not SpriteRenderer baseSpriteRenderer)
        {
            return;
        }

        topBorderRenderer = EnsureBorderRenderer(BorderTopName, topBorderRenderer, baseSpriteRenderer);
        rightBorderRenderer = EnsureBorderRenderer(BorderRightName, rightBorderRenderer, baseSpriteRenderer);
        bottomBorderRenderer = EnsureBorderRenderer(BorderBottomName, bottomBorderRenderer, baseSpriteRenderer);
        leftBorderRenderer = EnsureBorderRenderer(BorderLeftName, leftBorderRenderer, baseSpriteRenderer);

        ConfigureBorderLayout();
        SetBorderRendererState(topBorderRenderer, false, Color.clear);
        SetBorderRendererState(rightBorderRenderer, false, Color.clear);
        SetBorderRendererState(bottomBorderRenderer, false, Color.clear);
        SetBorderRendererState(leftBorderRenderer, false, Color.clear);
    }

    private SpriteRenderer EnsureBorderRenderer(string objectName, SpriteRenderer existingRenderer, SpriteRenderer baseSpriteRenderer)
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
        borderRenderer.sortingLayerID = baseSpriteRenderer.sortingLayerID;
        borderRenderer.sortingOrder = baseSpriteRenderer.sortingOrder + 2;
        borderRenderer.maskInteraction = baseSpriteRenderer.maskInteraction;
        return borderRenderer;
    }

    private void ConfigureBorderLayout()
    {
        if (_baseSpriteRenderer == null)
        {
            return;
        }

        Vector2 size = _baseSpriteRenderer.size;
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

        renderer.transform.localPosition = localPosition;
        renderer.transform.localRotation = Quaternion.identity;
        renderer.transform.localScale = localScale;
    }

    private static Sprite GetBorderSprite()
    {
        if (_borderSprite != null)
        {
            return _borderSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Point;
        _borderSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return _borderSprite;
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

    private void ClearBorders()
    {
        SetBorderRendererState(topBorderRenderer, false, Color.clear);
        SetBorderRendererState(rightBorderRenderer, false, Color.clear);
        SetBorderRendererState(bottomBorderRenderer, false, Color.clear);
        SetBorderRendererState(leftBorderRenderer, false, Color.clear);
    }

    public override Vector3 GetCellDimensions()
    {
        return (baseRenderer != null ? baseRenderer : GetComponent<Renderer>()).bounds.size;
    }
    public override void MarkAsHighlighted()
    {
        ApplyHighlight(HighlightedColor);
    }

    public override void MarkAsPath()
    {
        ApplyHighlight(PathColor);
    }

    public override void MarkAsReachable()
    {
        ApplyHighlight(ReachableColor);
    }

    public override void MarkAsAttackPreview()
    {
        ApplyHighlight(AttackPreviewColor);
    }

    public override void MarkAsTradePreview()
    {
        ApplyHighlight(TradePreviewColor);
    }

    public override void MarkAsAnyPreview()
    {
        ApplyHighlight(AnyPreviewColor);
    }

    public override void MarkAsAttackPreviewFaint()
    {
        ApplyHighlight(AttackPreviewFaintColor);
    }

    public override void MarkAsTradePreviewFaint()
    {
        ApplyHighlight(TradePreviewFaintColor);
    }

    public override void MarkAsAnyPreviewFaint()
    {
        ApplyHighlight(AnyPreviewFaintColor);
    }

    public override void UnMark()
    {
        if (baseRenderer != null)
        {
            SetRendererColor(baseRenderer, _baseColor);
        }

        if (highlightOverlayRenderer != null)
        {
            SetRendererColor(highlightOverlayRenderer, HiddenOverlayColor);
        }
    }

    public override void ShowPreviewBorder(bool top, bool right, bool bottom, bool left, Color color)
    {
        EnsureBorderRenderers();
        ClearBorders();
        SetBorderRendererState(topBorderRenderer, top, color);
        SetBorderRendererState(rightBorderRenderer, right, color);
        SetBorderRendererState(bottomBorderRenderer, bottom, color);
        SetBorderRendererState(leftBorderRenderer, left, color);
    }

    public override void ClearPreviewBorder()
    {
        ClearBorders();
    }

    private void ApplyHighlight(Color color)
    {
        if (highlightOverlayRenderer != null)
        {
            if (baseRenderer != null)
            {
                SetRendererColor(baseRenderer, _baseColor);
            }

            SetRendererColor(highlightOverlayRenderer, color);
            return;
        }

        if (baseRenderer != null)
        {
            SetRendererColor(baseRenderer, color);
        }
    }

    private static void SetRendererColor(Renderer renderer, Color color)
    {
        if (renderer == null)
        {
            return;
        }

        if (renderer is SpriteRenderer spriteRenderer)
        {
            spriteRenderer.color = color;
            return;
        }

        renderer.material.color = color;
    }
}
