using UnityEngine;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Rendering;

public sealed class RuntimeSampleSquareHighlighter : CellHighlighterBehaviour
{
    private const string OverlayObjectName = "RuntimeOverlay";
    private static readonly Color HiddenOverlayColor = new Color(1f, 1f, 1f, 0f);
    private static readonly Color ReachableColor = new Color(0.08f, 0.2f, 0.7f, 0.45f);
    private static readonly Color PathColor = new Color(0.35f, 0.68f, 1f, 0.4f);
    private static readonly Color AttackColor = new Color(0.92f, 0.2f, 0.2f, 0.4f);
    private static readonly Color SupportColor = new Color(0.35f, 0.9f, 0.35f, 0.4f);
    private static readonly Color DeploymentColor = new Color(0.5f, 0.82f, 1f, 0.4f);
    private static readonly Color SelectedColor = new Color(1f, 0.92f, 0.25f, 0.4f);

    [SerializeField] private Renderer baseRenderer;
    [SerializeField] private Renderer overlayRenderer;

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
        EnsureOverlayRenderer();
        SetOverlayColor(HiddenOverlayColor);
    }

    public override void Apply(BoardCell cell, CellHighlightKind highlightKind)
    {
        EnsureOverlayRenderer();
        SetOverlayColor(GetHighlightColor(highlightKind));
    }

    public override void Clear(BoardCell cell)
    {
        EnsureOverlayRenderer();
        SetOverlayColor(HiddenOverlayColor);
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
            return;
        }

        Transform existingOverlay = transform.Find(OverlayObjectName);
        if (existingOverlay != null)
        {
            overlayRenderer = existingOverlay.GetComponent<Renderer>();
            overlaySpriteRenderer = overlayRenderer as SpriteRenderer;
            return;
        }

        GameObject overlayObject = new GameObject(OverlayObjectName);
        overlayObject.transform.SetParent(transform, false);
        overlayObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);

        overlaySpriteRenderer = overlayObject.AddComponent<SpriteRenderer>();
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
        overlayRenderer = overlaySpriteRenderer;
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
