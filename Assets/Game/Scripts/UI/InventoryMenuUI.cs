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

public class InventoryMenuUI : MonoBehaviour, CustomMoveAbility.IInventoryMenuUI
{
    private static readonly Color EmptySlotRowColor = new Color(1f, 1f, 1f, 0.45f);

    private sealed class ItemRowView
    {
        public Item Entry;
        public Button Button;
        public Image Background;
        public TMP_Text Label;
    }

    [FormerlySerializedAs("panel")]
    public GameObject rootPanel;
    public TMP_Text titleText;
    [FormerlySerializedAs("itemListRoot")]
    public RectTransform inventoryDropdown;
    public Button itemButtonTemplate;
    public Button closeButton;
    public GameObject itemActionPanel;
    [FormerlySerializedAs("primaryActionButton")]
    public Button equipOrUseButton;
    [FormerlySerializedAs("primaryActionButtonText")]
    public TMP_Text equipOrUseButtonText;
    [FormerlySerializedAs("cancelActionButton")]
    public Button itemActionCancelButton;
    public GameObject itemDisplayPanel;
    public TMP_Text itemDisplayNameText;
    public TMP_Text itemDisplayBodyText;
    [SerializeField] private RectTransform positionTarget;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Color defaultRowColor = Color.white;
    [SerializeField] private Color selectedRowColor = new Color(0.75f, 0.9f, 1f, 1f);
    private Vector2 screenOffset = new Vector2(240f, -140f);
    private Vector2 screenPadding = new Vector2(24f, 24f);

    private readonly List<ItemRowView> itemRows = new List<ItemRowView>();
    private RectTransform panelRectTransform;
    private RectTransform canvasRectTransform;
    private System.Action onClose;
    private System.Action onConsumableUsed;
    private CustomUnit currentUnit;
    private Item selectedEntry;

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

        if (itemDisplayPanel != null)
        {
            itemDisplayPanel.SetActive(false);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HandleCloseClicked);
        }

        if (equipOrUseButton != null)
        {
            equipOrUseButton.onClick.AddListener(HandlePrimaryActionClicked);
        }

        if (itemActionCancelButton != null)
        {
            itemActionCancelButton.onClick.AddListener(HandleCancelSelectionClicked);
        }
    }

    private void Update()
    {
        if (rootPanel == null || !rootPanel.activeSelf)
        {
            return;
        }

        if (!Input.GetMouseButtonDown(1))
        {
            return;
        }

        if (selectedEntry != null)
        {
            HandleCancelSelectionClicked();
            return;
        }

        HandleCloseClicked();
    }

    public void Show(Vector3 worldPosition, CustomUnit unit, System.Action onClose, System.Action onConsumableUsed)
    {
        currentUnit = unit;
        this.onClose = onClose;
        this.onConsumableUsed = onConsumableUsed;
        selectedEntry = null;

        RebuildEntries();
        UpdateActionPanel();
        UpdateItemDisplayPanel();

        if (rootPanel != null)
        {
            rootPanel.SetActive(true);
            PositionPanel(worldPosition);
        }
    }

    public void Hide()
    {
        currentUnit = null;
        onClose = null;
        onConsumableUsed = null;
        selectedEntry = null;
        ClearSpawnedButtons();

        if (itemActionPanel != null)
        {
            itemActionPanel.SetActive(false);
        }

        if (itemDisplayPanel != null)
        {
            itemDisplayPanel.SetActive(false);
        }

        if (rootPanel != null)
        {
            rootPanel.SetActive(false);
        }
    }

    private void RebuildEntries()
    {
        ClearSpawnedButtons();

        if (itemButtonTemplate != null)
        {
            itemButtonTemplate.gameObject.SetActive(false);
        }

        if (titleText != null)
        {
            titleText.text = currentUnit == null
                ? GameTextCatalog.ResolveSceneText(titleText, "ui.inventory.title", "Inventory")
                : GameTextCatalog.Format("ui.inventory.title_with_name", "{0} Inventory", currentUnit.unitName);
        }

        if (currentUnit == null || inventoryDropdown == null || itemButtonTemplate == null)
        {
            return;
        }

        foreach (var entry in currentUnit.Inventory?.Entries ?? Array.Empty<Item>())
        {
            var button = Instantiate(itemButtonTemplate, inventoryDropdown);
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();

            var row = new ItemRowView
            {
                Entry = entry,
                Button = button,
                Background = button.GetComponent<Image>(),
                Label = button.GetComponentInChildren<TMP_Text>(true)
            };

            if (row.Label != null)
            {
                row.Label.text = BuildEntryLabel(entry);
            }

            if (row.Background != null)
            {
                row.Background.color = defaultRowColor;
            }

            bool isSelectable = entry?.Data != null;
            button.interactable = isSelectable;
            if (isSelectable)
            {
                button.onClick.AddListener(() => SelectEntry(row));
            }

            itemRows.Add(row);
        }

        while (itemRows.Count < UnitInventory.MaxSlots)
        {
            var button = Instantiate(itemButtonTemplate, inventoryDropdown);
            button.gameObject.SetActive(true);
            var colors = button.colors;
            colors.disabledColor = EmptySlotRowColor;
            button.colors = colors;
            button.interactable = false;

            var row = new ItemRowView
            {
                Entry = null,
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

            itemRows.Add(row);
        }
    }

    private string BuildEntryLabel(Item entry)
    {
        if (entry == null)
        {
            return GameTextCatalog.Get("ui.common.missing_item", "Missing Item");
        }

        if (entry.Data == null)
        {
            return GameTextCatalog.Format("ui.common.missing_item_id", "Missing: {0}", entry.ItemId);
        }

        string prefix = string.Empty;
        if (entry.Weapon != null && currentUnit?.Inventory?.EquippedWeaponEntry == entry)
        {
            prefix = "[E] ";
        }
        else if (entry.Accessory != null && currentUnit?.Inventory?.EquippedAccessoryEntry == entry)
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

    private void SelectEntry(ItemRowView row)
    {
        selectedEntry = row?.Entry;
        UpdateRowSelectionVisuals();
        UpdateActionPanel();
        UpdateItemDisplayPanel();
    }

    private void UpdateRowSelectionVisuals()
    {
        foreach (var row in itemRows)
        {
            if (row?.Background == null)
            {
                continue;
            }

            row.Background.color =
                selectedEntry != null && row.Entry == selectedEntry
                    ? selectedRowColor
                    : defaultRowColor;
        }
    }

    private void UpdateActionPanel()
    {
        if (itemActionPanel == null)
        {
            return;
        }

        bool hasSelection = selectedEntry != null;
        itemActionPanel.SetActive(hasSelection);
        if (!hasSelection)
        {
            return;
        }

        if (equipOrUseButton == null)
        {
            return;
        }

        bool isEquipAction = selectedEntry.Weapon != null || selectedEntry.Accessory != null;
        bool isUseAction = selectedEntry.Consumable != null;

        if (equipOrUseButtonText != null)
        {
            if (isEquipAction)
            {
                equipOrUseButtonText.text = GameTextCatalog.Get("ui.inventory.action_equip", "Equip");
            }
            else if (isUseAction)
            {
                equipOrUseButtonText.text = GameTextCatalog.Get("ui.inventory.action_use", "Use");
            }
            else
            {
                equipOrUseButtonText.text = GameTextCatalog.Get("ui.inventory.action_generic", "Action");
            }
        }

        if (isEquipAction)
        {
            equipOrUseButton.interactable = true;
            return;
        }

        if (isUseAction)
        {
            equipOrUseButton.interactable = currentUnit != null && currentUnit.CanUseConsumable(selectedEntry);
            return;
        }

        equipOrUseButton.interactable = false;
    }

    private void UpdateItemDisplayPanel()
    {
        if (itemDisplayPanel == null)
        {
            return;
        }

        bool hasSelection = selectedEntry?.Data != null;
        itemDisplayPanel.SetActive(hasSelection);
        if (!hasSelection)
        {
            return;
        }

        var data = selectedEntry.Data;

        if (itemDisplayNameText != null)
        {
            itemDisplayNameText.text = data.Name;
        }

        if (itemDisplayBodyText != null)
        {
            itemDisplayBodyText.text = BuildItemBodyText(selectedEntry);
        }
    }

    private static string BuildItemBodyText(Item entry)
    {
        if (entry?.Data == null)
        {
            return string.Empty;
        }

        var lines = new List<string>();

        if (entry.Weapon != null)
        {
            var weapon = entry.Weapon;
            lines.Add($"{weapon.Might} Mt, {weapon.Accuracy} Hit, {weapon.Crit} Crit");
        }

        if (!string.IsNullOrWhiteSpace(entry.Data.Description))
        {
            lines.Add(entry.Data.Description);
        }

        lines.Add($"Value: {entry.Data.Value} Gold");

        return string.Join("\n", lines);
    }

    private void HandlePrimaryActionClicked()
    {
        if (currentUnit == null || selectedEntry == null)
        {
            return;
        }

        bool didAct = false;
        bool usedConsumable = false;
        if (selectedEntry.Weapon != null)
        {
            didAct = currentUnit.EquipWeapon(selectedEntry);
        }
        else if (selectedEntry.Accessory != null)
        {
            didAct = currentUnit.EquipAccessory(selectedEntry);
        }
        else if (selectedEntry.Consumable != null)
        {
            didAct = currentUnit.UseConsumable(selectedEntry);
            usedConsumable = didAct;
        }

        if (!didAct)
        {
            UpdateActionPanel();
            return;
        }

        if (usedConsumable)
        {
            var consumableUsedAction = onConsumableUsed;
            Hide();
            consumableUsedAction?.Invoke();
            return;
        }

        HandleCloseClicked();
    }

    private void HandleCancelSelectionClicked()
    {
        selectedEntry = null;
        UpdateRowSelectionVisuals();
        UpdateActionPanel();
        UpdateItemDisplayPanel();
    }

    private void HandleCloseClicked()
    {
        var closeAction = onClose;
        Hide();
        closeAction?.Invoke();
    }

    private void ClearSpawnedButtons()
    {
        foreach (var row in itemRows)
        {
            if (row?.Button != null)
            {
                Destroy(row.Button.gameObject);
            }
        }

        itemRows.Clear();
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


