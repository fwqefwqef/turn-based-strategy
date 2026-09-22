using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Windy.Srpg.Game.Overworld;

namespace Windy.Srpg.Game.Editor
{
    // Author scene objects once; Shop only populates the wired list at runtime.
    internal static class ShopSellSceneSetup
    {
        [MenuItem("CONTEXT/Shop/Set Up Buy and Sell UI")]
        private static void Setup(MenuCommand command)
        {
            Shop shop = (Shop)command.context;
            if (Application.isPlaying || shop.ShopPanel == null || shop.CatalogContent == null
                || shop.CatalogButtonTemplate == null || shop.MainMenuButton == null
                || shop.PurchasePanel == null || shop.ItemNameText == null) return;

            Undo.RegisterFullObjectHierarchyUndo(shop.ShopPanel, "Set up shop selling");
            Undo.RecordObject(shop, "Wire shop selling");
            Transform root = shop.ShopPanel.transform;
            Transform background = root.Find("Background");
            if (background != null) Place(background, 100, 0, 1400, 1000);
            if (shop.BuyModeButton == null) shop.BuyModeButton = MakeButton(shop.MainMenuButton, root, "Buy Mode Button", "Buy");
            if (shop.SellModeButton == null) shop.SellModeButton = MakeButton(shop.MainMenuButton, root, "Sell Mode Button", "Sell");
            Place(shop.BuyModeButton.transform, -220, 290, 190, 48);
            Place(shop.SellModeButton.transform, -10, 290, 190, 48);

            // Preserve the existing look, with room for owner names and the shared details panel.
            ScrollRect scroll = shop.CatalogContent.GetComponentInParent<ScrollRect>(true);
            Place(scroll.transform, -120, -25, 700, 540);
            scroll.scrollSensitivity = 30;
            Place(shop.PurchasePanel.transform, 480, -25, 440, 540);
            Place(shop.ItemNameText.transform, 0, 220, 400, 56);
            Place(shop.ItemDescriptionText.transform, 0, 85, 400, 180);
            shop.ItemDescriptionText.fontSize = 24;
            shop.ItemDescriptionText.margin = Vector4.zero;
            shop.ItemDescriptionText.textWrappingMode = TextWrappingModes.Normal;
            shop.ItemDescriptionText.overflowMode = TextOverflowModes.Ellipsis;
            if (shop.ItemValueText == null) shop.ItemValueText = MakeText(shop.ItemNameText, shop.PurchasePanel.transform, "Item Value Text");
            if (shop.ItemQuantityText == null) shop.ItemQuantityText = MakeText(shop.ItemNameText, shop.PurchasePanel.transform, "Item Owner Or Stock Text");
            Place(shop.ItemValueText.transform, 0, -65, 400, 40);
            Place(shop.ItemQuantityText.transform, 0, -115, 400, 50);
            shop.ItemValueText.text = "Value";
            shop.ItemQuantityText.text = "Stock / Owner";
            Place(shop.BuyButton.transform, -105, -215, 180, 50);
            Place(shop.CancelButton.transform, 105, -215, 180, 50);

            LayoutElement row = shop.CatalogButtonTemplate.GetComponent<LayoutElement>();
            if (row == null) row = Undo.AddComponent<LayoutElement>(shop.CatalogButtonTemplate.gameObject);
            row.preferredHeight = 48;
            row.minHeight = 48;
            row.flexibleHeight = 0;
            TMP_Text rowText = shop.CatalogButtonTemplate.GetComponentInChildren<TMP_Text>(true);
            if (rowText != null)
            {
                rowText.fontSize = 24;
                rowText.enableAutoSizing = true;
                rowText.fontSizeMin = 18;
                rowText.fontSizeMax = 24;
                Stretch(rowText.rectTransform);
            }
            Text legacyRowText = shop.CatalogButtonTemplate.GetComponentInChildren<Text>(true);
            if (legacyRowText != null)
            {
                legacyRowText.fontSize = 26;
                legacyRowText.resizeTextForBestFit = true;
                legacyRowText.resizeTextMinSize = 20;
                legacyRowText.resizeTextMaxSize = 26;
                Stretch(legacyRowText.rectTransform);
            }
            VerticalLayoutGroup layout = shop.CatalogContent.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.childControlHeight = true;
                layout.childForceExpandHeight = false;
            }
            ContentSizeFitter fitter = shop.CatalogContent.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            shop.CatalogButtonTemplate.gameObject.SetActive(false);
            shop.PurchasePanel.SetActive(false);
            EditorUtility.SetDirty(shop);
            EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);
            Selection.activeGameObject = shop.gameObject;
            Debug.Log("Shop buy/sell scene objects created and wired. Save the scene to keep them.", shop);
        }

        private static Button MakeButton(Button template, Transform parent, string name, string label)
        {
            Button button = Object.Instantiate(template, parent);
            button.name = name;
            Undo.RegisterCreatedObjectUndo(button.gameObject, "Create shop mode button");
            button.onClick = new Button.ButtonClickedEvent();
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = label;
            Text legacy = button.GetComponentInChildren<Text>(true);
            if (legacy != null) legacy.text = label;
            button.interactable = true;
            button.gameObject.SetActive(true);
            return button;
        }

        private static TMP_Text MakeText(TMP_Text template, Transform parent, string name)
        {
            TMP_Text text = Object.Instantiate(template, parent);
            text.name = name;
            Undo.RegisterCreatedObjectUndo(text.gameObject, "Create shop detail text");
            text.fontSize = 24;
            text.raycastTarget = false;
            return text;
        }

        private static void Place(Transform transform, float x, float y, float width, float height)
        {
            RectTransform rect = (RectTransform)transform;
            rect.localScale = Vector3.one;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10, 2);
            rect.offsetMax = new Vector2(-10, -2);
        }
    }
}
