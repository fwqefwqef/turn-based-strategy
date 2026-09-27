using System;
using System.Collections.Generic;
using System.Linq;
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
        [Header("Navigation")]
        public GameObject ShopPanel;
        public GameObject MainMenuPanel;
        public Button ShopButton;
        public Button MainMenuButton;
        public Button BuyModeButton;
        public Button SellModeButton;

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
            new ShopCatalogEntry { itemId = "potion", quantity = -1 },
            new ShopCatalogEntry { itemId = "knife", quantity = 1 },
        };

        private static readonly HashSet<string> RetiredCatalogItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "iron_sword",
            "magic_sword"
        };

        private enum ShopState
        {
            Catalog,
            ConfirmPurchase
        }

        private CampaignSaveData workingSave;
        private ShopStockEntryData selectedCatalogEntry;
        private ShopState state = ShopState.Catalog;
        private bool hasUnsavedChanges;
        private bool selling;
        private SavedInventoryEntryData selectedSaleItem;
        private OwnedUnitSaveData selectedSaleOwner;
        private int selectedSaleIndex = -1;

        private void Awake()
        {
            BuiltInItemCatalog.EnsureRegistered();
            LoadWorkingSave();

            ShopButton?.onClick.AddListener(OpenShop);
            MainMenuButton?.onClick.AddListener(OpenMainMenu);
            SaveButton?.onClick.AddListener(ApplyChanges);
            BuyButton?.onClick.AddListener(ConfirmTransaction);
            BuyModeButton?.onClick.AddListener(ShowBuyCatalog);
            SellModeButton?.onClick.AddListener(ShowSellCatalog);
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
            BuyButton?.onClick.RemoveListener(ConfirmTransaction);
            BuyModeButton?.onClick.RemoveListener(ShowBuyCatalog);
            SellModeButton?.onClick.RemoveListener(ShowSellCatalog);
            CancelButton?.onClick.RemoveListener(CancelPurchase);
        }

        private void LoadWorkingSave()
        {
            workingSave = CampaignSaveManager.Load(CampaignSaveSlot.Campaign);
            if (workingSave == null)
            {
                workingSave = CampaignSaveFactory.CreateNewSave();
                hasUnsavedChanges = true;
            }

            if (!workingSave.ShopStockInitialized)
            {
                ShopStockUtility.AddStock(workingSave, (builtInCatalog ?? Array.Empty<ShopCatalogEntry>())
                    .Where(entry => entry != null)
                    .Select(entry => new ShopStockEntryData { ItemId = entry.itemId, Quantity = entry.quantity }));
                workingSave.ShopStockInitialized = true;
                hasUnsavedChanges = true;
            }

            workingSave.ShopStockItems = ShopStockUtility.CloneAndMerge(workingSave.ShopStockItems);
            if (ReconcileBuiltInCatalog())
            {
                hasUnsavedChanges = true;
            }
        }

        private bool ReconcileBuiltInCatalog()
        {
            ShopStockEntryData[] previousStock = ShopStockUtility.CloneAndMerge(workingSave.ShopStockItems);
            workingSave.ShopStockItems = previousStock
                .Where(entry => entry != null && !RetiredCatalogItemIds.Contains(entry.ItemId))
                .ToArray();

            foreach (ShopCatalogEntry catalogEntry in builtInCatalog ?? Array.Empty<ShopCatalogEntry>())
            {
                if (catalogEntry == null || string.IsNullOrWhiteSpace(catalogEntry.itemId))
                {
                    continue;
                }

                ShopStockEntryData existingEntry = workingSave.ShopStockItems.FirstOrDefault(entry =>
                    entry != null && string.Equals(entry.ItemId, catalogEntry.itemId, StringComparison.OrdinalIgnoreCase));
                if (existingEntry == null)
                {
                    ShopStockUtility.AddStock(workingSave, new[]
                    {
                        new ShopStockEntryData { ItemId = catalogEntry.itemId, Quantity = catalogEntry.quantity }
                    });
                }
                else if (catalogEntry.quantity < 0 && existingEntry.Quantity >= 0)
                {
                    existingEntry.Quantity = -1;
                }
            }

            ShopStockEntryData[] reconciledStock = ShopStockUtility.CloneAndMerge(workingSave.ShopStockItems);
            workingSave.ShopStockItems = reconciledStock;
            return previousStock.Length != reconciledStock.Length
                || previousStock.Where((entry, index) => index >= reconciledStock.Length
                    || !string.Equals(entry.ItemId, reconciledStock[index].ItemId, StringComparison.OrdinalIgnoreCase)
                    || entry.Quantity != reconciledStock[index].Quantity).Any();
        }

        public void ReloadCampaignSave()
        {
            ClearSaleSelection();
            selectedCatalogEntry = null;
            state = ShopState.Catalog;
            hasUnsavedChanges = false;
            LoadWorkingSave();
            SetPurchasePanelVisible(false);
            RefreshShopView();
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
            SetButtonText(BuyButton, selling ? "Sell" : "Buy");
            if (BuyModeButton != null) BuyModeButton.interactable = selling;
            if (SellModeButton != null) SellModeButton.interactable = !selling;
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
                child.gameObject.SetActive(false);
            }

            if (selling)
            {
                AddSaleButtons(workingSave?.StorageItems, null);
                foreach (OwnedUnitSaveData owner in workingSave?.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>())
                {
                    if (owner != null) AddSaleButtons(owner.Inventory, owner);
                }
                return;
            }

            foreach (ShopStockEntryData entry in workingSave?.ShopStockItems ?? Array.Empty<ShopStockEntryData>())
            {
                ItemData item = ResolveItem(entry);
                if (item == null)
                {
                    continue;
                }

                Button button = Instantiate(CatalogButtonTemplate, CatalogContent);
                button.gameObject.SetActive(true);
                button.interactable = entry.Quantity != 0;

                SetButtonText(button, BuildCatalogButtonText(entry, item));

                ShopStockEntryData capturedEntry = entry;
                button.onClick.AddListener(() => SelectCatalogEntry(capturedEntry));
            }
        }

        private void SelectCatalogEntry(ShopStockEntryData entry)
        {
            selectedCatalogEntry = entry;
            state = ShopState.ConfirmPurchase;
            SetPurchasePanelVisible(true);
            RefreshPurchasePanel();
        }

        private void RefreshPurchasePanel()
        {
            if (selling)
            {
                RefreshSalePanel();
                return;
            }
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
            SafeSetText(ItemQuantityText, $"Stock: {FormatQuantity(selectedCatalogEntry.Quantity)}");

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

            if (selectedCatalogEntry.Quantity > 0)
            {
                selectedCatalogEntry.Quantity--;
            }

            hasUnsavedChanges = true;
            selectedCatalogEntry = null;
            state = ShopState.Catalog;
            SetPurchasePanelVisible(false);
            RefreshShopView();
        }

        private void CancelPurchase()
        {
            ClearSaleSelection();
            selectedCatalogEntry = null;
            state = ShopState.Catalog;
            SetPurchasePanelVisible(false);
            RefreshShopView();
        }

        private void ShowBuyCatalog()
        {
            selling = false;
            CancelPurchase();
        }

        private void ShowSellCatalog()
        {
            selling = true;
            CancelPurchase();
        }

        private void ConfirmTransaction()
        {
            if (selling) SellSelectedItem();
            else BuySelectedItemToStorage();
        }

        private static int SellValue(ItemData item) => Math.Max(0, item.Value / 2);

        private void AddSaleButtons(SavedInventoryEntryData[] inventory, OwnedUnitSaveData owner)
        {
            if (inventory == null) return;
            for (int i = 0; i < inventory.Length; i++)
            {
                SavedInventoryEntryData entry = inventory[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId)) continue;
                ItemData item = ItemRegistry.Get(entry.ItemId);
                if (item == null) continue;

                Button button = Instantiate(CatalogButtonTemplate, CatalogContent);
                button.gameObject.SetActive(true);
                button.interactable = true;
                string ownerName = owner == null ? "Storage" : owner.UnitName;
                SetButtonText(button, $"{item.Name} x1 ({SellValue(item)}G) -- {ownerName}");
                int index = i;
                button.onClick.AddListener(() =>
                {
                    selectedSaleItem = entry;
                    selectedSaleOwner = owner;
                    selectedSaleIndex = index;
                    state = ShopState.ConfirmPurchase;
                    RefreshSalePanel();
                });
            }
        }

        private void RefreshSalePanel()
        {
            bool hasSelection = selectedSaleItem != null && state == ShopState.ConfirmPurchase;
            SetPurchasePanelVisible(hasSelection);
            if (!hasSelection) return;
            ItemData item = ItemRegistry.Get(selectedSaleItem.ItemId);
            SafeSetText(ItemNameText, item?.Name ?? "Unknown Item");
            SafeSetText(ItemDescriptionText, item?.Description ?? string.Empty);
            SafeSetText(ItemValueText, item == null ? string.Empty : $"Sell: {SellValue(item)}G");
            string ownerName = selectedSaleOwner == null ? "Storage" : selectedSaleOwner.UnitName;
            string charges = item is ConsumableData
                ? $" | Charges: {selectedSaleItem.RemainingCharges}" : string.Empty;
            SafeSetText(ItemQuantityText, $"From: {ownerName}{charges}");
            if (BuyButton != null) BuyButton.interactable = item != null;
        }

        private void SellSelectedItem()
        {
            if (workingSave == null || selectedSaleItem == null) return;
            SavedInventoryEntryData[] inventory = selectedSaleOwner == null
                ? workingSave.StorageItems : selectedSaleOwner.Inventory;
            // Validate the exact copy: duplicate items and different remaining charges are distinct.
            if (inventory == null || selectedSaleIndex < 0 || selectedSaleIndex >= inventory.Length
                || !ReferenceEquals(inventory[selectedSaleIndex], selectedSaleItem))
            {
                CancelPurchase();
                return;
            }
            ItemData item = ItemRegistry.Get(selectedSaleItem.ItemId);
            if (item == null || workingSave.Gold > int.MaxValue - SellValue(item)) return;
            var remaining = inventory.ToList();
            remaining.RemoveAt(selectedSaleIndex);
            if (selectedSaleOwner == null) workingSave.StorageItems = remaining.ToArray();
            else selectedSaleOwner.Inventory = remaining.ToArray();
            workingSave.Gold += SellValue(item);
            hasUnsavedChanges = true;
            CancelPurchase();
        }

        private void ClearSaleSelection()
        {
            selectedSaleItem = null;
            selectedSaleOwner = null;
            selectedSaleIndex = -1;
        }

        private void ApplyChanges()
        {
            CampaignSaveManager.Save(workingSave, CampaignSaveSlot.Campaign);
            hasUnsavedChanges = false;
            RefreshStatusText();
        }

        private bool CanBuy(ShopStockEntryData entry, ItemData item)
        {
            return entry != null
                && item != null
                && entry.Quantity != 0
                && workingSave != null
                && workingSave.Gold >= item.Value;
        }

        private static SavedInventoryEntryData CreateSavedInventoryEntry(ItemData item)
        {
            int remainingCharges = item is ConsumableData consumable ? consumable.Charges : -1;
            return new SavedInventoryEntryData
            {
                ItemId = item.Id,
                RemainingCharges = remainingCharges,
                IsDroppable = false
            };
        }

        private static ItemData ResolveItem(ShopStockEntryData entry)
        {
            return entry == null || string.IsNullOrWhiteSpace(entry.ItemId)
                ? null
                : ItemRegistry.Get(entry.ItemId);
        }

        private static string BuildCatalogButtonText(ShopStockEntryData entry, ItemData item)
        {
            return $"{item.Name} {FormatButtonQuantity(entry.Quantity)} ({item.Value}G)";
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
