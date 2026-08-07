using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Windy.Srpg.Game.Campaign;
using Windy.Srpg.Game.Inventory;

namespace Windy.Srpg.Game.Overworld
{
    [Serializable]
    public class ShopCatalogEntry
    {
        public string itemId;
        public int quantity; // -1 = unlimited
    }

    public class Shop : MonoBehaviour
    {
        private const int StartingGold = 5000;

        [Header("Navigation")]
        public GameObject ShopPanel;
        public GameObject MainMenuPanel;
        public Button ShopButton;
        public Button MainMenuButton;

        [Header("Catalog")]
        public RectTransform CatalogContent;
        public Button CatalogButtonTemplate;
        public TMP_Text GoldText;
        public TMP_Text StatusText;

        [Header("Purchase Confirmation")]
        public GameObject PurchasePanel;
        public TMP_Text ItemNameText;
        public TMP_Text ItemDescriptionText;
        public TMP_Text ItemValueText;
        public TMP_Text ItemQuantityText;
        public Button BuyButton;
        public Button CancelButton;

        [Header("Save")]
        public Button SaveButton;

        [SerializeField]
        private ShopCatalogEntry[] builtInCatalog =
        {
            new ShopCatalogEntry { itemId = "iron_sword", quantity = -1 },
            new ShopCatalogEntry { itemId = "magic_sword", quantity = 1 },
        };

        private enum ShopState
        {
            Catalog,
            ConfirmPurchase
        }

        private CampaignSaveData workingSave;
        private ShopCatalogEntry selectedCatalogEntry;
        private ShopState state = ShopState.Catalog;
        private bool hasUnsavedChanges;

        private void Awake()
        {
            BuiltInItemCatalog.EnsureRegistered();
            LoadWorkingSave();

            ShopButton?.onClick.AddListener(OpenShop);
            MainMenuButton?.onClick.AddListener(OpenMainMenu);
            SaveButton?.onClick.AddListener(ApplyChanges);
            BuyButton?.onClick.AddListener(BuySelectedItemToStorage);
            CancelButton?.onClick.AddListener(CancelPurchase);

            if (CatalogButtonTemplate != null)
            {
                CatalogButtonTemplate.gameObject.SetActive(false);
            }

            SetPurchasePanelVisible(false);
            RefreshShopView();
        }

        private void OnDestroy()
        {
            ShopButton?.onClick.RemoveListener(OpenShop);
            MainMenuButton?.onClick.RemoveListener(OpenMainMenu);
            SaveButton?.onClick.RemoveListener(ApplyChanges);
            BuyButton?.onClick.RemoveListener(BuySelectedItemToStorage);
            CancelButton?.onClick.RemoveListener(CancelPurchase);
        }

        private void LoadWorkingSave()
        {
            workingSave = CampaignSaveManager.Load();
            if (workingSave == null)
            {
                workingSave = new CampaignSaveData();
            }

            if (workingSave.Gold <= 0)
            {
                workingSave.Gold = StartingGold;
                hasUnsavedChanges = true;
            }
        }

        private void OpenShop()
        {
            ShopPanel?.SetActive(true);
            MainMenuPanel?.SetActive(false);
            RefreshShopView();
        }

        private void OpenMainMenu()
        {
            ShopPanel?.SetActive(false);
            MainMenuPanel?.SetActive(true);
            CancelPurchase();
        }

        private void RefreshShopView()
        {
            RefreshGoldText();
            RefreshStatusText();
            RebuildCatalogButtons();
            RefreshPurchasePanel();
        }

        private void RebuildCatalogButtons()
        {
            if (CatalogContent == null || CatalogButtonTemplate == null)
            {
                return;
            }

            for (int i = CatalogContent.childCount - 1; i >= 0; i--)
            {
                Transform child = CatalogContent.GetChild(i);
                if (child == CatalogButtonTemplate.transform)
                {
                    continue;
                }

                Destroy(child.gameObject);
            }

            foreach (ShopCatalogEntry entry in builtInCatalog ?? Array.Empty<ShopCatalogEntry>())
            {
                ItemData item = ResolveItem(entry);
                if (item == null)
                {
                    continue;
                }

                Button button = Instantiate(CatalogButtonTemplate, CatalogContent);
                button.gameObject.SetActive(true);
                button.interactable = entry.quantity != 0;

                SetButtonText(button, BuildCatalogButtonText(entry, item));

                ShopCatalogEntry capturedEntry = entry;
                button.onClick.AddListener(() => SelectCatalogEntry(capturedEntry));
            }
        }

        private void SelectCatalogEntry(ShopCatalogEntry entry)
        {
            selectedCatalogEntry = entry;
            state = ShopState.ConfirmPurchase;
            SetPurchasePanelVisible(true);
            RefreshPurchasePanel();
        }

        private void RefreshPurchasePanel()
        {
            bool hasSelection = selectedCatalogEntry != null;
            SetPurchasePanelVisible(hasSelection && state == ShopState.ConfirmPurchase);
            if (!hasSelection)
            {
                return;
            }

            ItemData item = ResolveItem(selectedCatalogEntry);
            if (item == null)
            {
                SafeSetText(ItemNameText, "Unknown Item");
                SafeSetText(ItemDescriptionText, "This item is missing from the item registry.");
                SafeSetText(ItemValueText, string.Empty);
                SafeSetText(ItemQuantityText, string.Empty);
                if (BuyButton != null)
                {
                    BuyButton.interactable = false;
                }

                return;
            }

            SafeSetText(ItemNameText, item.Name);
            SafeSetText(ItemDescriptionText, item.Description);
            SafeSetText(ItemValueText, $"Value: {item.Value}G");
            SafeSetText(ItemQuantityText, $"Stock: {FormatQuantity(selectedCatalogEntry.quantity)}");

            if (BuyButton != null)
            {
                BuyButton.interactable = CanBuy(selectedCatalogEntry, item);
            }
        }

        private void BuySelectedItemToStorage()
        {
            if (selectedCatalogEntry == null)
            {
                return;
            }

            ItemData item = ResolveItem(selectedCatalogEntry);
            if (!CanBuy(selectedCatalogEntry, item))
            {
                RefreshPurchasePanel();
                return;
            }

            List<SavedInventoryEntryData> storageItems = new List<SavedInventoryEntryData>(
                workingSave.StorageItems ?? Array.Empty<SavedInventoryEntryData>());

            storageItems.Add(CreateSavedInventoryEntry(item));
            workingSave.StorageItems = storageItems.ToArray();
            workingSave.Gold -= item.Value;

            if (selectedCatalogEntry.quantity > 0)
            {
                selectedCatalogEntry.quantity--;
            }

            hasUnsavedChanges = true;
            selectedCatalogEntry = null;
            state = ShopState.Catalog;
            SetPurchasePanelVisible(false);
            RefreshShopView();
        }

        private void CancelPurchase()
        {
            selectedCatalogEntry = null;
            state = ShopState.Catalog;
            SetPurchasePanelVisible(false);
            RefreshShopView();
        }

        private void ApplyChanges()
        {
            CampaignSaveManager.Save(workingSave);
            hasUnsavedChanges = false;
            RefreshStatusText();
        }

        private bool CanBuy(ShopCatalogEntry entry, ItemData item)
        {
            return entry != null
                && item != null
                && entry.quantity != 0
                && workingSave != null
                && workingSave.Gold >= item.Value;
        }

        private static SavedInventoryEntryData CreateSavedInventoryEntry(ItemData item)
        {
            int remainingCharges = item is ConsumableData consumable ? consumable.Charges : -1;
            return new SavedInventoryEntryData
            {
                ItemId = item.Id,
                RemainingCharges = remainingCharges
            };
        }

        private static ItemData ResolveItem(ShopCatalogEntry entry)
        {
            return entry == null || string.IsNullOrWhiteSpace(entry.itemId)
                ? null
                : ItemRegistry.Get(entry.itemId);
        }

        private static string BuildCatalogButtonText(ShopCatalogEntry entry, ItemData item)
        {
            return $"{item.Name} {FormatButtonQuantity(entry.quantity)} ({item.Value}G)";
        }

        private static string FormatQuantity(int quantity)
        {
            return quantity < 0 ? "Unlimited" : quantity.ToString();
        }

        private static string FormatButtonQuantity(int quantity)
        {
            int displayQuantity = quantity < 0 ? 99 : Mathf.Clamp(quantity, 0, 99);
            return $"x{displayQuantity}";
        }

        private void RefreshGoldText()
        {
            SafeSetText(GoldText, $"Gold: {workingSave?.Gold ?? 0}G");
        }

        private void RefreshStatusText()
        {
            SafeSetText(StatusText, hasUnsavedChanges ? "Unsaved shop changes" : "Shop changes saved");
        }

        private void SetPurchasePanelVisible(bool visible)
        {
            PurchasePanel?.SetActive(visible);
        }

        private static void SetButtonText(Button button, string text)
        {
            if (button == null)
            {
                return;
            }

            TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(includeInactive: true);
            if (tmpText != null)
            {
                tmpText.text = text;
                return;
            }

            Text legacyText = button.GetComponentInChildren<Text>(includeInactive: true);
            if (legacyText != null)
            {
                legacyText.text = text;
            }
        }

        private static void SafeSetText(TMP_Text textField, string value)
        {
            if (textField != null)
            {
                textField.text = value ?? string.Empty;
            }
        }
    }
}
