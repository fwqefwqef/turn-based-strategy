using System;
using TMPro;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Localization;

public class TurnCounterUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private CellGrid cellGrid;

    private void Awake()
    {
        if (turnText == null)
            turnText = GetComponent<TextMeshProUGUI>();
    }

    private void Start()
    {
        if (cellGrid == null)
            cellGrid = FindAnyObjectByType<CellGrid>();

        if (cellGrid == null || turnText == null)
            return;

        // Update immediately
        UpdateTurnText();

        // Subscribe to events that exist in YOUR CellGrid
        cellGrid.BattleStarted += OnGameStarted;
        cellGrid.TurnStarted += OnTurnStarted;
        cellGrid.BattleTurnEnded += OnTurnEnded;
    }

    private void OnDestroy()
    {
        if (cellGrid == null) return;

        cellGrid.BattleStarted -= OnGameStarted;
        cellGrid.TurnStarted -= OnTurnStarted;
        cellGrid.BattleTurnEnded -= OnTurnEnded;
    }

    private void OnGameStarted(object sender, EventArgs e) => UpdateTurnText();
    private void OnTurnStarted(object sender, EventArgs e) => UpdateTurnText();

    private void OnTurnEnded(object sender, EventArgs e) => UpdateTurnText();

    private void UpdateTurnText()
    {
        // "RoundCount" is your turn counter
        // You can show player too if you want.
        turnText.text = GameTextCatalog.Format("ui.common.turn_format", "Turn {0}", cellGrid.RoundCount);
        // Or: turnText.text = $"Turn {cellGrid.RoundCount}  (P{cellGrid.CurrentPlayerNumber + 1})";
    }
}


