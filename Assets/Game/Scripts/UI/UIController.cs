using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Localization;

public class UIController : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI turnCountText;     // For displaying the turn number
    public Button endTurnButton;         // For the "End Turn" button
    public CustomCellGrid cellGrid;            // Reference to your grid controller

    private void Start()
    {
        if (cellGrid == null)
            cellGrid = FindAnyObjectByType<CustomCellGrid>();

        if (cellGrid != null)
            cellGrid.TurnStarted += OnTurnStarted;

        // Hook the button click
        if (endTurnButton != null)
            endTurnButton.onClick.AddListener(OnEndTurnClicked);
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

        if (endTurnButton != null)
            endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
    }

    private void OnEndTurnClicked()
    {
        if (cellGrid != null)
            cellGrid.RequestEndTurn();
    }
}


