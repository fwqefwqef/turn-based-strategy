using Windy.Srpg.Game.Units;
using UnityEngine;

public class SampleUnit : CustomUnit
{
    private static readonly Color SelectedTint = new Color(0.7f, 1f, 0.7f, 1f);
    private static readonly Color ReachableEnemyTint = new Color(1f, 0.65f, 0.65f, 1f);
    private static readonly Color FinishedTint = new Color(0.65f, 0.65f, 0.65f, 1f);

    public override void Initialize()
    {
        base.Initialize();
        ApplyDefaultStateColor();
    }

    public override void MarkAsFriendly()
    {
        ApplyDefaultStateColor();
    }
    public override void MarkAsReachableEnemy()
    {
        SetRendererColor(ReachableEnemyTint);
    }
    public override void MarkAsSelected()
    {
        SetRendererColor(SelectedTint);
    }
    public override void MarkAsFinished()
    {
        SetRendererColor(FinishedTint);
    }
    public override void UnMark()
    {
        ApplyDefaultStateColor();
    }

    public override void SetColor(Color color)
    {
        SetRendererColor(color);
    }

    private void SetRendererColor(Color color)
    {
        if (this == null)
        {
            return;
        }

        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
            return;
        }

        Renderer renderer = GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
    }

    private void ApplyDefaultStateColor()
    {
        SetRendererColor(IsFinishedForTurn ? FinishedTint : Color.white);
    }
}

