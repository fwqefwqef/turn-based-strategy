using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Windy.Srpg.Game.Campaign;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Localization;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.UI
{
    public sealed class DroppedItemStorageChoiceUI : GameplayModalUI
    {
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text instructionsText;
        [SerializeField] private TMP_Text inventoryTitleText;
        [SerializeField] private TMP_Text droppedItemTitleText;
        [SerializeField] private RectTransform inventoryOptionContainer;
        [SerializeField] private RectTransform droppedItemOptionContainer;
        [SerializeField] private Button inventoryOptionButtonTemplate;
        [SerializeField] private Button droppedItemOptionButtonTemplate;

        private readonly List<Button> spawnedButtons = new List<Button>();
        private Action<Item> onChoiceSelected;

        protected override void Awake()
        {
            base.Awake();

            if (inventoryOptionButtonTemplate != null)
            {
                inventoryOptionButtonTemplate.gameObject.SetActive(false);
            }

            if (droppedItemOptionButtonTemplate != null)
            {
                droppedItemOptionButtonTemplate.gameObject.SetActive(false);
            }

            ConfigureModal(rootPanel != null ? rootPanel : gameObject);
            SetModalVisible(false);
        }

        public void Show(Unit recipient, SavedInventoryEntryData droppedItem, string sourceName, Action<Item> onChoiceSelected)
        {
            this.onChoiceSelected = onChoiceSelected;
            RebuildChoices(recipient, droppedItem, sourceName);
            SetDefaultFocusButton(ResolvePreferredFocusButton());
            SetModalVisible(true);
        }

        public void Hide()
        {
            ClearSpawnedButtons();
            onChoiceSelected = null;
            SetModalVisible(false);
        }

        protected override bool HandleCancelFromInput()
        {
            return false;
        }

        private void RebuildChoices(Unit recipient, SavedInventoryEntryData droppedItem, string sourceName)
        {
            ClearSpawnedButtons();

            string recipientName = recipient != null && !string.IsNullOrWhiteSpace(recipient.unitName)
                ? recipient.unitName
                : recipient != null ? recipient.name : GameTextCatalog.Get("ui.common.unit", "Unit");

            if (titleText != null)
            {
                titleText.text = GameTextCatalog.Get("ui.loot_storage.title", "Inventory Full");
            }

            if (instructionsText != null)
            {
                instructionsText.text = GameTextCatalog.Format(
                    "ui.loot_storage.instructions",
                    "{0}'s inventory is full. Choose one item to send to Storage.",
                    recipientName);
            }

            if (inventoryTitleText != null)
            {
                inventoryTitleText.text = GameTextCatalog.Get("ui.loot_storage.inventory_title", "Inventory");
            }

            if (droppedItemTitleText != null)
            {
                droppedItemTitleText.text = GameTextCatalog.Get("ui.loot_storage.dropped_title", "New Item");
            }

            if (inventoryOptionContainer == null
                || droppedItemOptionContainer == null
                || inventoryOptionButtonTemplate == null
                || droppedItemOptionButtonTemplate == null)
            {
                return;
            }

            Button droppedButton = CreateChoiceButton(
                droppedItemOptionButtonTemplate,
                droppedItemOptionContainer,
                GameTextCatalog.Format(
                    "ui.loot_storage.dropped_item",
                    "{0}",
                    BuildDroppedItemLabel(droppedItem)),
                itemToStore: null);
            SetDefaultFocusButton(droppedButton);

            foreach (Item item in recipient?.Inventory?.Entries ?? Array.Empty<Item>())
            {
                Item capturedItem = item;
                CreateChoiceButton(
                    inventoryOptionButtonTemplate,
                    inventoryOptionContainer,
                    GameTextCatalog.Format(
                        "ui.loot_storage.inventory_item",
                        "{0}",
                        BuildRuntimeItemLabel(capturedItem, recipient)),
                    capturedItem);
            }
        }

        private Button CreateChoiceButton(Button template, RectTransform container, string label, Item itemToStore)
        {
            Button button = Instantiate(template, container, false);
            button.gameObject.SetActive(true);
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectChoice(itemToStore));

            TMP_Text labelText = button.GetComponentInChildren<TMP_Text>(true);
            if (labelText != null)
            {
                labelText.text = label;
            }

            spawnedButtons.Add(button);
            return button;
        }

        private void SelectChoice(Item itemToStore)
        {
            Action<Item> callback = onChoiceSelected;
            Hide();
            callback?.Invoke(itemToStore);
        }

        private void ClearSpawnedButtons()
        {
            foreach (Button button in spawnedButtons)
            {
                if (button != null)
                {
                    Destroy(button.gameObject);
                }
            }

            spawnedButtons.Clear();
        }

        private Button ResolvePreferredFocusButton()
        {
            foreach (Button button in spawnedButtons)
            {
                if (button != null && button.interactable && button.gameObject.activeInHierarchy)
                {
                    return button;
                }
            }

            return null;
        }

        private static string BuildDroppedItemLabel(SavedInventoryEntryData entry)
        {
            if (entry == null)
            {
                return GameTextCatalog.Get("ui.common.missing_item", "Missing Item");
            }

            ItemData data = ItemRegistry.Get(entry.ItemId);
            if (data == null)
            {
                return GameTextCatalog.Format("ui.common.missing_item_id", "Missing: {0}", entry.ItemId);
            }

            return BuildItemLabel(data, entry.RemainingCharges, isEquipped: false);
        }

        private static string BuildRuntimeItemLabel(Item item, Unit owner)
        {
            if (item == null)
            {
                return GameTextCatalog.Get("ui.common.missing_item", "Missing Item");
            }

            if (item.Data == null)
            {
                return GameTextCatalog.Format("ui.common.missing_item_id", "Missing: {0}", item.ItemId);
            }

            bool isEquipped = owner?.Inventory != null
                && (owner.Inventory.EquippedWeaponEntry == item || owner.Inventory.EquippedAccessoryEntry == item);
            return BuildItemLabel(item.Data, item.RemainingCharges, isEquipped);
        }

        private static string BuildItemLabel(ItemData data, int remainingCharges, bool isEquipped)
        {
            string prefix = isEquipped ? "[E] " : string.Empty;
            if (data is ConsumableData)
            {
                string charges = remainingCharges < 0
                    ? GameTextCatalog.Get("ui.common.infinite_short", "inf")
                    : remainingCharges.ToString();
                return $"{prefix}{data.Name} ({charges})";
            }

            return $"{prefix}{data.Name}";
        }
    }
}
