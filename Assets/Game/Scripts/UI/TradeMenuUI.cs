using System;
using System.Collections.Generic;
using TMPro;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Localization;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Game.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class TradeMenuUI : MonoBehaviour, CustomMoveAbility.ITradeMenuUI
{
    private static readonly Color EmptySlotRowColor = new Color(1f, 1f, 1f, 0.45f);

    private enum SwapSelectionMode
    {
        None,
        NeedSelfItem,
        NeedFriendlyItem
    }

    private sealed class TradeItemRowView
    {
        public Item Entry;
        public bool IsSelfSide;
        public Button Button;
        public Image Background;
        public TMP_Text Label;
    }

    public GameObject rootPanel;
    public TMP_Text selfTitleText;
    public TMP_Text friendlyTitleText;
    public RectTransform selfInventoryDropdown;
    public RectTransform friendlyInventoryDropdown;
    public Button itemButtonTemplate;
    public GameObject itemActionPanel;
    [FormerlySerializedAs("primaryActionButton")]
    public Button giveOrTakeButton;
    [FormerlySerializedAs("primaryActionButtonText")]
    public TMP_Text giveOrTakeButtonText;
    public Button swapButton;
    public TMP_Text swapButtonText;
    public Button cancelActionButton;
    public Button closeButton;
    [SerializeField] private RectTransform positionTarget;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Color defaultRowColor = Color.white;
    [SerializeField] private Color selectedRowColor = new Color(0.75f, 0.9f, 1f, 1f);
    private Vector2 screenOffset = new Vector2(240f, -140f);
    private Vector2 screenPadding = new Vector2(24f, 24f);

    private readonly List<TradeItemRowView> selfRows = new List<TradeItemRowView>();
    private readonly List<TradeItemRowView> friendlyRows = new List<TradeItemRowView>();
    private RectTransform panelRectTransform;
    private RectTransform canvasRectTransform;
    private System.Action<bool> onClose;
    private System.Action onFirstTradeCommitted;
    private CustomUnit selfUnit;
    private CustomUnit friendlyUnit;
    private Item selectedSelfEntry;
    private Item selectedFriendlyEntry;
    private SwapSelectionMode swapSelectionMode;
    private bool didTrade;

    private void Awake()
    {
        if (rootPanel != null)
        {
            panelRectTransform = rootPanel.GetComponent<RectTransform>();
            rootPanel.SetActive(false);
        }

        if (positionTarget == null)
        {
            RectTransform ownRectTransform = transform as RectTransform;
            if (ownRectTransform != null && rootPanel != null && rootPanel.transform.IsChildOf(transform))
            {
                positionTarget = ownRectTransform;
            }
            else
            {
                positionTarget = panelRectTransform;
            }
        }

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvas != null)
        {
            canvasRectTransform = canvas.GetComponent<RectTransform>();
        }

        if (itemButtonTemplate != null)
        {
            itemButtonTemplate.gameObject.SetActive(false);
        }

        if (itemActionPanel != null)
        {
            itemActionPanel.SetActive(false);
        }

        if (giveOrTakeButton != null)
        {
            giveOrTakeButton.onClick.AddListener(HandlePrimaryActionClicked);
        }

        if (swapButton != null)
        {
            swapButton.onClick.AddListener(HandleSwapClicked);
        }

        if (cancelActionButton != null)
        {
            cancelActionButton.onClick.AddListener(ClearSelection);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HandleCloseClicked);
        }
    }

    private void Update()
    {
        if (rootPanel == null || !rootPanel.activeSelf || !Input.GetMouseButtonDown(1))
        {
            return;
        }

        if (HasAnySelection())
        {
            ClearSelection();
            return;
        }

        HandleCloseClicked();
    }

    public void Show(Vector3 worldPosition, CustomUnit selfUnit, CustomUnit friendlyUnit, System.Action<bool> onClose, System.Action onFirstTradeCommitted)
    {
        this.selfUnit = selfUnit;
        this.friendlyUnit = friendlyUnit;
        this.onClose = onClose;
        this.onFirstTradeCommitted = onFirstTradeCommitted;
        didTrade = false;
        ClearSelection();
        RebuildEntries();
        UpdateActionPanel();

        if (rootPanel != null)
        {
            rootPanel.SetActive(true);
            PositionPanel(worldPosition);
        }
    }

    public void Hide()
    {
        selfUnit = null;
        friendlyUnit = null;
        onClose = null;
        onFirstTradeCommitted = null;
        didTrade = false;
        ClearSelection();
        ClearSpawnedButtons(selfRows);
        ClearSpawnedButtons(friendlyRows);

        if (itemActionPanel != null)
        {
            itemActionPanel.SetActive(false);
        }

        if (rootPanel != null)
        {
            rootPanel.SetActive(false);
        }
    }

    private void RebuildEntries()
    {
        ClearSpawnedButtons(selfRows);
        ClearSpawnedButtons(friendlyRows);
        ClearDropdownChildren(selfInventoryDropdown);
        ClearDropdownChildren(friendlyInventoryDropdown);

        if (selfTitleText != null)
        {
            selfTitleText.text = selfUnit == null
                ? GameTextCatalog.ResolveSceneText(selfTitleText, "ui.trade.self", "Self")
                : selfUnit.unitName;
        }

        if (friendlyTitleText != null)
        {
            friendlyTitleText.text = friendlyUnit == null
                ? GameTextCatalog.ResolveSceneText(friendlyTitleText, "ui.trade.friendly", "Friendly")
                : friendlyUnit.unitName;
        }

        BuildSideEntries(selfUnit, selfInventoryDropdown, selfRows, true);
        BuildSideEntries(friendlyUnit, friendlyInventoryDropdown, friendlyRows, false);
    }

    private void BuildSideEntries(CustomUnit unit, RectTransform dropdown, List<TradeItemRowView> rows, bool isSelfSide)
    {
        if (unit == null || dropdown == null || itemButtonTemplate == null)
        {
            return;
        }

        itemButtonTemplate.gameObject.SetActive(false);

        foreach (var entry in unit.Inventory?.Entries ?? Array.Empty<Item>())
        {
            var button = Instantiate(itemButtonTemplate, dropdown);
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();

            var row = new TradeItemRowView
            {
                Entry = entry,
                IsSelfSide = isSelfSide,
                Button = button,
                Background = button.GetComponent<Image>(),
                Label = button.GetComponentInChildren<TMP_Text>(true)
            };

            if (row.Label != null)
            {
                row.Label.text = BuildEntryLabel(unit, entry);
            }

            if (row.Background != null)
            {
                row.Background.color = defaultRowColor;
            }

            button.interactable = entry?.Data != null;
            if (button.interactable)
            {
                button.onClick.AddListener(() => SelectEntry(row));
            }

            rows.Add(row);
        }

        while (rows.Count < UnitInventory.MaxSlots)
        {
            var button = Instantiate(itemButtonTemplate, dropdown);
            button.gameObject.SetActive(true);
            var colors = button.colors;
            colors.disabledColor = EmptySlotRowColor;
            button.colors = colors;
            button.interactable = false;

            var row = new TradeItemRowView
            {
                Entry = null,
                IsSelfSide = isSelfSide,
                Button = button,
                Background = button.GetComponent<Image>(),
                Label = button.GetComponentInChildren<TMP_Text>(true)
            };

            if (row.Label != null)
            {
                row.Label.text = string.Empty;
            }

            if (row.Background != null)
            {
                row.Background.color = EmptySlotRowColor;
            }

            rows.Add(row);
        }
    }

    private static string BuildEntryLabel(CustomUnit owner, Item entry)
    {
        if (entry?.Data == null)
        {
            return GameTextCatalog.Get("ui.common.missing_item", "Missing Item");
        }

        string prefix = string.Empty;
        if (entry.Weapon != null && owner?.Inventory?.EquippedWeaponEntry == entry)
        {
            prefix = "[E] ";
        }
        else if (entry.Accessory != null && owner?.Inventory?.EquippedAccessoryEntry == entry)
        {
            prefix = "[E] ";
        }

        if (entry.Consumable != null)
        {
            string charges = entry.HasInfiniteCharges ? GameTextCatalog.Get("ui.common.infinite_short", "inf") : entry.RemainingCharges.ToString();
            return $"{prefix}{entry.Data.Name} ({charges})";
        }

        return $"{prefix}{entry.Data.Name}";
    }

    private void SelectEntry(TradeItemRowView row)
    {
        if (row == null || row.Entry == null)
        {
            return;
        }

        if (row.IsSelfSide)
        {
            selectedSelfEntry = row.Entry;
            if (swapSelectionMode == SwapSelectionMode.None)
            {
                selectedFriendlyEntry = null;
            }
        }
        else
        {
            selectedFriendlyEntry = row.Entry;
            if (swapSelectionMode == SwapSelectionMode.None)
            {
                selectedSelfEntry = null;
            }
        }

        UpdateRowSelectionVisuals();

        if (swapSelectionMode == SwapSelectionMode.NeedFriendlyItem && selectedSelfEntry != null && selectedFriendlyEntry != null)
        {
            PerformSwap();
            return;
        }

        if (swapSelectionMode == SwapSelectionMode.NeedSelfItem && selectedSelfEntry != null && selectedFriendlyEntry != null)
        {
            PerformSwap();
            return;
        }

        UpdateActionPanel();
    }

    private void UpdateRowSelectionVisuals()
    {
        foreach (var row in selfRows)
        {
            if (row?.Background != null)
            {
                row.Background.color =
                    selectedSelfEntry != null && row.Entry == selectedSelfEntry
                        ? selectedRowColor
                        : defaultRowColor;
            }
        }

        foreach (var row in friendlyRows)
        {
            if (row?.Background != null)
            {
                row.Background.color =
                    selectedFriendlyEntry != null && row.Entry == selectedFriendlyEntry
                        ? selectedRowColor
                        : defaultRowColor;
            }
        }
    }

    private void UpdateActionPanel()
    {
        if (itemActionPanel == null)
        {
            return;
        }

        bool hasSelection = HasAnySelection();
        itemActionPanel.SetActive(hasSelection);
        if (!hasSelection)
        {
            return;
        }

        bool hasSelfOnly = selectedSelfEntry != null && selectedFriendlyEntry == null;
        bool hasFriendlyOnly = selectedFriendlyEntry != null && selectedSelfEntry == null;
        bool hasBoth = selectedSelfEntry != null && selectedFriendlyEntry != null;
        bool canGive = hasSelfOnly && friendlyUnit != null && !friendlyUnit.Inventory.IsFull;
        bool canTake = hasFriendlyOnly && selfUnit != null && !selfUnit.Inventory.IsFull;

        if (giveOrTakeButton != null)
        {
            giveOrTakeButton.gameObject.SetActive(!hasBoth && (canGive || canTake));
            giveOrTakeButton.interactable = canGive || canTake;
        }

        if (giveOrTakeButtonText != null)
        {
            if (canGive)
            {
                giveOrTakeButtonText.text = GameTextCatalog.Get("ui.trade.give", "Give");
            }
            else if (canTake)
            {
                giveOrTakeButtonText.text = GameTextCatalog.Get("ui.trade.take", "Take");
            }
            else
            {
                giveOrTakeButtonText.text = string.Empty;
            }
        }

        if (swapButton != null)
        {
            swapButton.interactable = hasSelection;
        }

        if (swapButtonText != null)
        {
            if (swapSelectionMode == SwapSelectionMode.NeedFriendlyItem)
            {
                swapButtonText.text = GameTextCatalog.Get("ui.trade.pick_ally_item", "Pick Ally Item");
            }
            else if (swapSelectionMode == SwapSelectionMode.NeedSelfItem)
            {
                swapButtonText.text = GameTextCatalog.Get("ui.trade.pick_self_item", "Pick Self Item");
            }
            else
            {
                swapButtonText.text = GameTextCatalog.Get("ui.trade.swap", "Swap");
            }
        }
    }

    private void HandlePrimaryActionClicked()
    {
        if (selfUnit == null || friendlyUnit == null)
        {
            return;
        }

        bool didTrade = false;
        if (selectedSelfEntry != null && selectedFriendlyEntry == null)
        {
            didTrade = selfUnit.Inventory.TransferEntryTo(selectedSelfEntry, friendlyUnit.Inventory);
        }
        else if (selectedFriendlyEntry != null && selectedSelfEntry == null)
        {
            didTrade = friendlyUnit.Inventory.TransferEntryTo(selectedFriendlyEntry, selfUnit.Inventory);
        }

        if (!didTrade)
        {
            UpdateActionPanel();
            return;
        }

        HandleTradeCommitted();
    }

    private void HandleSwapClicked()
    {
        if (selectedSelfEntry != null && selectedFriendlyEntry != null)
        {
            PerformSwap();
            return;
        }

        if (selectedSelfEntry != null)
        {
            swapSelectionMode = SwapSelectionMode.NeedFriendlyItem;
            UpdateActionPanel();
            return;
        }

        if (selectedFriendlyEntry != null)
        {
            swapSelectionMode = SwapSelectionMode.NeedSelfItem;
            UpdateActionPanel();
        }
    }

    private void PerformSwap()
    {
        if (selfUnit == null || friendlyUnit == null || selectedSelfEntry == null || selectedFriendlyEntry == null)
        {
            return;
        }

        bool didSwap = selfUnit.Inventory.SwapEntriesWith(selectedSelfEntry, friendlyUnit.Inventory, selectedFriendlyEntry);
        if (!didSwap)
        {
            UpdateActionPanel();
            return;
        }

        HandleTradeCommitted();
    }

    private void HandleTradeCommitted()
    {
        if (!didTrade)
        {
            didTrade = true;
            onFirstTradeCommitted?.Invoke();
        }

        ClearSelection();
        RebuildEntries();
        UpdateRowSelectionVisuals();
        UpdateActionPanel();
    }

    private bool HasAnySelection()
    {
        return selectedSelfEntry != null || selectedFriendlyEntry != null || swapSelectionMode != SwapSelectionMode.None;
    }

    private void ClearSelection()
    {
        selectedSelfEntry = null;
        selectedFriendlyEntry = null;
        swapSelectionMode = SwapSelectionMode.None;
        UpdateRowSelectionVisuals();
        UpdateActionPanel();
    }

    private void HandleCloseClicked()
    {
        var closeAction = onClose;
        bool closeWithTrade = didTrade;
        Hide();
        closeAction?.Invoke(closeWithTrade);
    }

    private static void ClearSpawnedButtons(List<TradeItemRowView> rows)
    {
        foreach (var row in rows)
        {
            if (row?.Button != null)
            {
                Destroy(row.Button.gameObject);
            }
        }

        rows.Clear();
    }

    private void ClearDropdownChildren(RectTransform dropdown)
    {
        if (dropdown == null)
        {
            return;
        }

        Transform templateTransform = itemButtonTemplate != null ? itemButtonTemplate.transform : null;
        for (int i = dropdown.childCount - 1; i >= 0; i--)
        {
            Transform child = dropdown.GetChild(i);
            if (child == templateTransform)
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    private void PositionPanel(Vector3 worldPosition)
    {
        CanvasClampManager.PositionAtWorldPoint(
            canvas,
            worldCamera,
            canvasRectTransform,
            panelRectTransform,
            positionTarget,
            worldPosition,
            screenOffset,
            screenPadding);
    }
}


