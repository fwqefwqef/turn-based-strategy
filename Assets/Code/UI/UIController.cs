using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Localization;

public class UIController : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI turnCountText;     // For displaying the turn number
    public CellGrid cellGrid;            // Reference to your grid controller

    private void Start()
    {
        if (cellGrid == null)
            cellGrid = FindAnyObjectByType<CellGrid>();

        if (cellGrid != null)
            cellGrid.TurnStarted += OnTurnStarted;
    }

    private void OnTurnStarted(object sender, System.EventArgs e)
    {
        if (turnCountText != null)
            turnCountText.text = GameTextCatalog.Format("ui.common.turn_format", "Turn {0}", cellGrid.RoundCount);
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (cellGrid != null)
            cellGrid.TurnStarted -= OnTurnStarted;
    }
}



