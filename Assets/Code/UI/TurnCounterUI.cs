using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Localization;

public class TurnCounterUI : Windy.Srpg.Game.UI.GameplayModalUI
{
    public static event Action<bool> VisibilityChanged;
    private static TurnCounterUI activeInstance;

    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private CellGrid cellGrid;
    [SerializeField] private GameObject root;
    [SerializeField] private Button endTurnButton;
    [SerializeField] private Button cancelButton;
    private bool runtimeHooksRegistered;

    public new static bool IsVisible => activeInstance != null && ((Windy.Srpg.Game.UI.GameplayModalUI)activeInstance).IsVisible;

    protected override void Awake()
    {
        base.Awake();
        activeInstance = this;
        EnsureReady();
    }

    private void Start()
    {
        EnsureReady();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (activeInstance == this)
        {
            activeInstance = null;
        }

        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(OnCancelClicked);
        }

        UnregisterRuntimeHooks();
    }

    private void OnGameStarted(object sender, EventArgs e)
    {
        HideImmediate();
        UpdateTurnText();
    }

    private void OnTurnStarted(object sender, EventArgs e) => UpdateTurnText();

    private void OnTurnEnded(object sender, EventArgs e)
    {
        HideImmediate();
        UpdateTurnText();
    }

    private void UpdateTurnText()
    {
        if (cellGrid == null || turnText == null)
        {
            return;
        }

        // "RoundCount" is your turn counter
        // You can show player too if you want.
        turnText.text = GameTextCatalog.Format("ui.common.turn_format", "Turn {0}", cellGrid.RoundCount);
        // Or: turnText.text = $"Turn {cellGrid.RoundCount}  (P{cellGrid.CurrentPlayerNumber + 1})";
    }

    public static void RequestShow()
    {
        ResolveInstance()?.Show();
    }

    public static void RequestHide()
    {
        ResolveInstance()?.HideImmediate();
    }

    public static bool RequestCancelFromInput()
    {
        TurnCounterUI instance = ResolveInstance();
        if (instance == null)
        {
            return false;
        }

        return instance.TryCancelFromInput();
    }

    public static bool IsActiveTurnInfoButton(Button button)
    {
        return activeInstance != null
            && activeInstance.root != null
            && button != null
            && button.transform.IsChildOf(activeInstance.root.transform);
    }

    private void Show()
    {
        EnsureReady();

        if (root == null)
        {
            return;
        }

        UpdateTurnText();

        if (endTurnButton != null)
        {
            endTurnButton.interactable = cellGrid != null && cellGrid.IsHumanTurn;
        }

        SetDefaultFocusButton(endTurnButton != null && endTurnButton.interactable ? endTurnButton : cancelButton);
        SetModalVisible(true);
    }

    private void HideImmediate()
    {
        SetModalVisible(false);
    }

    private void OnEndTurnClicked()
    {
        if (cellGrid == null || !cellGrid.IsHumanTurn)
        {
            return;
        }

        HideImmediate();
        cellGrid.RequestEndTurn();
    }

    private void OnCancelClicked()
    {
        HideImmediate();
    }

    private void AutoAssignButtons()
    {
        if (root == null)
        {
            return;
        }

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button == null)
            {
                continue;
            }

            string buttonName = button.gameObject.name.ToLowerInvariant();
            if (endTurnButton == null && buttonName.Contains("end turn"))
            {
                endTurnButton = button;
                continue;
            }

            if (cancelButton == null && (buttonName.Contains("cancel") || buttonName.Contains("close")))
            {
                cancelButton = button;
            }
        }
    }

    protected override void OnModalVisibilityChanged(bool isVisible)
    {
        VisibilityChanged?.Invoke(isVisible);
    }

    private void EnsureReady()
    {
        if (turnText == null)
        {
            turnText = GetComponent<TextMeshProUGUI>();
        }

        if (root == null)
        {
            root = transform.parent != null ? transform.parent.gameObject : gameObject;
        }

        AutoAssignButtons();

        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
            endTurnButton.onClick.AddListener(OnEndTurnClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(OnCancelClicked);
            cancelButton.onClick.AddListener(OnCancelClicked);
        }

        ConfigureModal(root, endTurnButton, cancelButton);
        RegisterRuntimeHooks();

        if (turnText != null)
        {
            UpdateTurnText();
        }
    }

    private void RegisterRuntimeHooks()
    {
        if (runtimeHooksRegistered)
        {
            return;
        }

        if (cellGrid == null)
        {
            cellGrid = FindAnyObjectByType<CellGrid>();
        }

        if (cellGrid == null)
        {
            return;
        }

        cellGrid.BattleStarted += OnGameStarted;
        cellGrid.TurnStarted += OnTurnStarted;
        cellGrid.BattleTurnEnded += OnTurnEnded;
        runtimeHooksRegistered = true;
    }

    private void UnregisterRuntimeHooks()
    {
        if (!runtimeHooksRegistered || cellGrid == null)
        {
            return;
        }

        cellGrid.BattleStarted -= OnGameStarted;
        cellGrid.TurnStarted -= OnTurnStarted;
        cellGrid.BattleTurnEnded -= OnTurnEnded;
        runtimeHooksRegistered = false;
    }

    private static TurnCounterUI ResolveInstance()
    {
        if (activeInstance != null)
        {
            activeInstance.EnsureReady();
            return activeInstance;
        }

        activeInstance = Resources.FindObjectsOfTypeAll<TurnCounterUI>()
            .FirstOrDefault(instance =>
                instance != null
                && instance.gameObject.scene.IsValid()
                && !string.IsNullOrEmpty(instance.gameObject.scene.name));

        activeInstance?.EnsureReady();
        return activeInstance;
    }
}


