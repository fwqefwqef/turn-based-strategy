using UnityEngine;
using Windy.Srpg.Runtime.Units;

public sealed class RuntimeSampleUnit : GridUnit
{
    [SerializeField] private bool applyVisualState;

    public override void Initialize()
    {
        base.Initialize();
        ApplyStateColor(TurnState);
    }

    public override void SetState(UnitTurnState newState)
    {
        base.SetState(newState);
        ApplyStateColor(newState);
    }

    private void ApplyStateColor(UnitTurnState state)
    {
        if (!applyVisualState)
        {
            return;
        }

        Color color = state switch
        {
            UnitTurnStateSelected => Color.green,
            UnitTurnStateReachableEnemy => Color.red,
            UnitTurnStateFinished => Color.gray,
            _ => Color.white
        };

        Renderer renderer = GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
    }
}
