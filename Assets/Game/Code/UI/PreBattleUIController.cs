using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Windy.Srpg.Game.Campaign;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Localization;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Windy.Srpg.Game.UI
{
    /// <summary>
    /// Pre-battle roster/deployment UI host.
    /// The select-units panel is button-list driven, while switch-deployment now delegates slot
    /// interaction to the board deployment tiles themselves.
    /// </summary>
    public sealed class PreBattleUIController : MonoBehaviour
    {
        private static PreBattleUIController activeInstance;
        private const float ButtonHeight = 34f;
        private const float ButtonSpacing = 8f;
        private const float ContainerPadding = 12f;
        private const float MinimumButtonWidth = 96f;

        [Header("References")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CellGrid cellGrid;
        [SerializeField] private bool autoGenerateUiIfMissing = true;
        [SerializeField] private bool autoResizeGeneratedLists;

        [Header("Root UI")]
        [SerializeField] private RectTransform rootPanel;
        [SerializeField] private Button battleStartButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button selectUnitsButton;
        [SerializeField] private Button switchDeploymentButton;
        [FormerlySerializedAs("inventoryButton")]
        [SerializeField] private Button inventoryManagementButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Select Units UI")]
        [SerializeField] private RectTransform selectUnitsPanel;
        [SerializeField] private Button selectUnitsBackButton;
        [SerializeField] private TMP_Text selectUnitsInstructionText;
        [SerializeField] private RectTransform selectSlotContainer;
        [SerializeField] private RectTransform reserveListContainer;

        [Header("Switch Deployment UI")]
        [SerializeField] private RectTransform switchDeploymentPanel;
        [SerializeField] private Button switchDeploymentBackButton;
        [SerializeField] private TMP_Text switchDeploymentInstructionText;
        // Retained as a scene-compatibility anchor for older authored UI setups.
        // Runtime switch deployment now uses the board slots directly instead of generating a button list here.
        [SerializeField] private RectTransform switchSlotContainer;

        [Header("Inventory Management UI")]
        [FormerlySerializedAs("inventoryPanel")]
        [SerializeField] private RectTransform inventoryManagementPanel;
        [FormerlySerializedAs("inventoryBackButton")]
        [SerializeField] private Button inventoryManagementBackButton;
        [FormerlySerializedAs("inventoryInstructionText")]
        [SerializeField] private TMP_Text inventoryManagementInstructionText;
        [FormerlySerializedAs("inventoryUnitContainer")]
        [SerializeField] private RectTransform inventoryManagementUnitContainer;
        [FormerlySerializedAs("inventoryUnitButtonTemplate")]
        [SerializeField] private Button inventoryManagementUnitButtonTemplate;
        [FormerlySerializedAs("inventoryOwnItemsContainer")]
        [SerializeField] private RectTransform inventoryManagementOwnItemsContainer;
        [FormerlySerializedAs("inventoryOwnItemButtonTemplate")]
        [SerializeField] private Button inventoryManagementOwnItemButtonTemplate;
        [FormerlySerializedAs("inventoryOtherItemsContainer")]
        [SerializeField] private RectTransform inventoryManagementOtherItemsContainer;
        [FormerlySerializedAs("inventoryOtherItemButtonTemplate")]
        [SerializeField] private Button inventoryManagementOtherItemButtonTemplate;
        [FormerlySerializedAs("inventoryWeaponFilterButton")]
        [SerializeField] private Button inventoryManagementWeaponFilterButton;
        [FormerlySerializedAs("inventoryAccessoryFilterButton")]
        [SerializeField] private Button inventoryManagementAccessoryFilterButton;
        [FormerlySerializedAs("inventoryConsumableFilterButton")]
        [SerializeField] private Button inventoryManagementConsumableFilterButton;
        [FormerlySerializedAs("inventoryAllFilterButton")]
        [SerializeField] private Button inventoryManagementAllFilterButton;
        [FormerlySerializedAs("inventoryActionPanel")]
        [SerializeField] private RectTransform inventoryManagementActionPanel;
        [FormerlySerializedAs("inventoryActionText")]
        [SerializeField] private TMP_Text inventoryManagementActionText;
        [FormerlySerializedAs("inventoryConfirmActionButton")]
        [SerializeField] private Button inventoryManagementConfirmActionButton;
        [FormerlySerializedAs("inventoryCancelActionButton")]
        [SerializeField] private Button inventoryManagementCancelActionButton;

        private TMP_FontAsset fontAsset;
        private string preferredSelectUnitId;
        private string selectedInventoryManagementUnitId;
        private InventoryManagementFilterKind inventoryManagementFilter = InventoryManagementFilterKind.Weapon;
        private PendingInventoryManagementAction pendingInventoryManagementAction;
        private bool initialized;
        private bool generatedFallbackUi;

        private enum InventoryManagementFilterKind
        {
            Weapon,
            Accessory,
            Consumable,
            All
        }

        private sealed class PendingInventoryManagementAction
        {
            public string TargetUnitId;
            public string SourceUnitId;
            public int SourceItemIndex;
            public bool SourceIsStorage;
            public bool GiveToStorage;
            public string ItemLabel;
        }

        private readonly struct IndexedInventoryEntry
        {
            public IndexedInventoryEntry(int index, SavedInventoryEntryData entry)
            {
                Index = index;
                Entry = entry;
            }

            public int Index { get; }
            public SavedInventoryEntryData Entry { get; }
        }

        public void Initialize(CellGrid grid)
        {
            if (initialized)
            {
                return;
            }

            if (grid != null)
            {
                cellGrid = grid;
            }

            if (cellGrid == null)
            {
                cellGrid = FindAnyObjectByType<CellGrid>();
            }

            if (cellGrid == null)
            {
                enabled = false;
                return;
            }

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            if (canvas == null)
            {
                canvas = FindAnyObjectByType<Canvas>();
            }

            if (canvas == null)
            {
                enabled = false;
                return;
            }

            fontAsset = ResolveFontAsset();
            EnsureUiExists();
            HookButtonEvents();
            HookGridEvents();
            CloseSubPanels();
            RefreshAll();
            activeInstance = this;
            initialized = true;
        }

        private void OnDestroy()
        {
            if (activeInstance == this)
            {
                activeInstance = null;
            }

            UnhookButtonEvents();
            UnhookGridEvents();
        }

        public static bool RequestBackFromInput()
        {
            if (activeInstance == null || !activeInstance.initialized)
            {
                return false;
            }

            return activeInstance.TryReturnToMainPanelFromInput();
        }

        public static Button GetPreferredFocusButton(IReadOnlyList<Button> activeButtons)
        {
            if (activeInstance == null || !activeInstance.initialized || activeButtons == null)
            {
                return null;
            }

            return activeInstance.ResolvePreferredFocusButton(activeButtons);
        }

        public static bool IsSwitchDeploymentBoardInteractionActive =>
            activeInstance != null
            && activeInstance.initialized
            && activeInstance.cellGrid != null
            && activeInstance.cellGrid.IsPreBattlePhase
            && activeInstance.switchDeploymentPanel != null
            && activeInstance.switchDeploymentPanel.gameObject.activeSelf;

        public static bool TryGetPreferredSwitchDeploymentCell(out Cell cell)
        {
            cell = null;
            if (!IsSwitchDeploymentBoardInteractionActive || activeInstance.cellGrid == null)
            {
                return false;
            }

            cell = activeInstance.cellGrid.GetPreferredPreBattleDeploymentCell();
            return cell != null;
        }

        private void EnsureUiExists()
        {
            if (HasSceneAuthoredUi())
            {
                EnsureSceneAuthoredRuntimeElements();
                return;
            }

            if (!autoGenerateUiIfMissing)
            {
                Debug.LogWarning("PreBattleUIController: Scene-authored pre-battle UI references are incomplete, and auto-generation is disabled.");
                enabled = false;
                return;
            }

            BuildFallbackUi();
            generatedFallbackUi = true;
        }

        private bool HasSceneAuthoredUi()
        {
            return rootPanel != null
                && selectUnitsPanel != null
                && switchDeploymentPanel != null
                && battleStartButton != null
                && selectUnitsButton != null
                && switchDeploymentButton != null
                && selectUnitsBackButton != null
                && switchDeploymentBackButton != null
                && selectSlotContainer != null;
        }

        private void EnsureSceneAuthoredRuntimeElements()
        {
            if (rootPanel != null && saveButton == null)
            {
                saveButton = CreateSceneAuthoredSaveButton();
            }

            PrepareInventoryButtonTemplate(inventoryManagementUnitButtonTemplate);
            PrepareInventoryButtonTemplate(inventoryManagementOwnItemButtonTemplate);
            PrepareInventoryButtonTemplate(inventoryManagementOtherItemButtonTemplate);
        }

        private void HookButtonEvents()
        {
            battleStartButton?.onClick.AddListener(BeginBattle);
            saveButton?.onClick.AddListener(SaveRosterChanges);
            selectUnitsButton?.onClick.AddListener(OpenSelectUnitsPanelFromButton);
            switchDeploymentButton?.onClick.AddListener(OpenSwitchDeploymentPanelFromButton);
            inventoryManagementButton?.onClick.AddListener(OpenInventoryPanelFromButton);
            selectUnitsBackButton?.onClick.AddListener(ReturnToMainPanel);
            switchDeploymentBackButton?.onClick.AddListener(ReturnToMainPanel);
            inventoryManagementBackButton?.onClick.AddListener(ReturnToMainPanel);
            inventoryManagementWeaponFilterButton?.onClick.AddListener(SetInventoryFilterWeapon);
            inventoryManagementAccessoryFilterButton?.onClick.AddListener(SetInventoryFilterAccessory);
            inventoryManagementConsumableFilterButton?.onClick.AddListener(SetInventoryFilterConsumable);
            inventoryManagementAllFilterButton?.onClick.AddListener(SetInventoryFilterAll);
            inventoryManagementConfirmActionButton?.onClick.AddListener(ConfirmPendingInventoryAction);
            inventoryManagementCancelActionButton?.onClick.AddListener(ClearPendingInventoryAction);
        }

        private void UnhookButtonEvents()
        {
            battleStartButton?.onClick.RemoveListener(BeginBattle);
            saveButton?.onClick.RemoveListener(SaveRosterChanges);
            selectUnitsButton?.onClick.RemoveListener(OpenSelectUnitsPanelFromButton);
            switchDeploymentButton?.onClick.RemoveListener(OpenSwitchDeploymentPanelFromButton);
            inventoryManagementButton?.onClick.RemoveListener(OpenInventoryPanelFromButton);
            selectUnitsBackButton?.onClick.RemoveListener(ReturnToMainPanel);
            switchDeploymentBackButton?.onClick.RemoveListener(ReturnToMainPanel);
            inventoryManagementBackButton?.onClick.RemoveListener(ReturnToMainPanel);
            inventoryManagementWeaponFilterButton?.onClick.RemoveListener(SetInventoryFilterWeapon);
            inventoryManagementAccessoryFilterButton?.onClick.RemoveListener(SetInventoryFilterAccessory);
            inventoryManagementConsumableFilterButton?.onClick.RemoveListener(SetInventoryFilterConsumable);
            inventoryManagementAllFilterButton?.onClick.RemoveListener(SetInventoryFilterAll);
            inventoryManagementConfirmActionButton?.onClick.RemoveListener(ConfirmPendingInventoryAction);
            inventoryManagementCancelActionButton?.onClick.RemoveListener(ClearPendingInventoryAction);
        }

        private void HookGridEvents()
        {
            if (cellGrid == null)
            {
                return;
            }

            cellGrid.PreBattleStateChanged += OnPreBattleStateChanged;
            cellGrid.DeploymentRosterChanged += OnDeploymentRosterChanged;
            cellGrid.BattleStarted += OnGameStarted;
        }

        private void UnhookGridEvents()
        {
            if (cellGrid == null)
            {
                return;
            }

            cellGrid.PreBattleStateChanged -= OnPreBattleStateChanged;
            cellGrid.DeploymentRosterChanged -= OnDeploymentRosterChanged;
            cellGrid.BattleStarted -= OnGameStarted;
        }

        private void OnPreBattleStateChanged(object sender, EventArgs e)
        {
            RefreshAll();
        }

        private void OnDeploymentRosterChanged(object sender, EventArgs e)
        {
            RefreshAll();
        }

        private void OnGameStarted(object sender, EventArgs e)
        {
            RefreshAll();
        }

        private void RefreshAll()
        {
            bool showPreBattle = cellGrid != null && cellGrid.IsPreBattlePhase;
            bool showSelectUnitsPanel = selectUnitsPanel != null && selectUnitsPanel.gameObject.activeSelf;
            bool showSwitchDeploymentPanel = switchDeploymentPanel != null && switchDeploymentPanel.gameObject.activeSelf;
            bool showInventoryPanel = inventoryManagementPanel != null && inventoryManagementPanel.gameObject.activeSelf;

            if (rootPanel != null)
            {
                rootPanel.gameObject.SetActive(showPreBattle && !showSelectUnitsPanel && !showSwitchDeploymentPanel && !showInventoryPanel);
            }

            if (!showPreBattle)
            {
                CloseSubPanels();
                return;
            }

            IReadOnlyList<string> roster = cellGrid.GetDeploymentRosterForPreBattle();
            int deploymentSlotLimit = GetDeploymentSlotLimit();

            if (statusText != null)
            {
                int filledSlotCount = CountFilledRosterSlots(roster);
                string baseStatus = deploymentSlotLimit > 0
                    ? GameTextCatalog.Format("ui.pre_battle.status_roster", "Roster: {0}/{1}", filledSlotCount, deploymentSlotLimit)
                    : GameTextCatalog.Get("ui.pre_battle.status_no_slots", "No deployment slots.");
                statusText.text = cellGrid.HasUnsavedPreBattleChanges
                    ? GameTextCatalog.Format("ui.pre_battle.status_unsaved", "{0} (Unsaved)", baseStatus)
                    : baseStatus;
            }

            if (saveButton != null)
            {
                saveButton.interactable = cellGrid.HasUnsavedPreBattleChanges;
            }

            RefreshSelectUnitsPanel();
            RefreshSwitchDeploymentPanel();
            RefreshInventoryPanel();
        }

        private void OpenSelectUnitsPanelFromButton()
        {
            cellGrid?.ExitPreBattleDeploymentSwapMode();
            OpenSelectUnitsPanel();
            RefreshSelectUnitsPanel();
        }

        private void OpenSwitchDeploymentPanelFromButton()
        {
            OpenSwitchDeploymentPanel();
            cellGrid?.EnterPreBattleDeploymentSwapMode();
            RefreshSwitchDeploymentPanel();
        }

        private void OpenInventoryPanelFromButton()
        {
            cellGrid?.ExitPreBattleDeploymentSwapMode();
            OpenInventoryPanel();
            RefreshInventoryPanel();
        }

        private void OpenSelectUnitsPanel()
        {
            if (selectUnitsPanel == null)
            {
                return;
            }

            rootPanel?.gameObject.SetActive(false);
            if (switchDeploymentPanel != null)
            {
                switchDeploymentPanel.gameObject.SetActive(false);
            }

            if (inventoryManagementPanel != null)
            {
                inventoryManagementPanel.gameObject.SetActive(false);
            }

            selectUnitsPanel.gameObject.SetActive(true);
        }

        private void OpenSwitchDeploymentPanel()
        {
            if (switchDeploymentPanel == null)
            {
                return;
            }

            rootPanel?.gameObject.SetActive(false);
            if (selectUnitsPanel != null)
            {
                selectUnitsPanel.gameObject.SetActive(false);
            }

            if (inventoryManagementPanel != null)
            {
                inventoryManagementPanel.gameObject.SetActive(false);
            }

            switchDeploymentPanel.gameObject.SetActive(true);
            preferredSelectUnitId = null;
        }

        private void OpenInventoryPanel()
        {
            if (inventoryManagementPanel == null)
            {
                return;
            }

            rootPanel?.gameObject.SetActive(false);
            if (selectUnitsPanel != null)
            {
                selectUnitsPanel.gameObject.SetActive(false);
            }

            if (switchDeploymentPanel != null)
            {
                switchDeploymentPanel.gameObject.SetActive(false);
            }

            inventoryManagementPanel.gameObject.SetActive(true);
            preferredSelectUnitId = null;
        }

        private void ReturnToMainPanel()
        {
            cellGrid?.ExitPreBattleDeploymentSwapMode();
            CloseSubPanels();
            if (cellGrid != null && cellGrid.IsPreBattlePhase)
            {
                rootPanel?.gameObject.SetActive(true);
            }
        }

        private bool TryReturnToMainPanelFromInput()
        {
            if (cellGrid == null || !cellGrid.IsPreBattlePhase)
            {
                return false;
            }

            bool hasOpenSubPanel =
                (selectUnitsPanel != null && selectUnitsPanel.gameObject.activeSelf)
                || (switchDeploymentPanel != null && switchDeploymentPanel.gameObject.activeSelf)
                || (inventoryManagementPanel != null && inventoryManagementPanel.gameObject.activeSelf);
            if (!hasOpenSubPanel)
            {
                return false;
            }

            ReturnToMainPanel();
            return true;
        }

        private void BeginBattle()
        {
            cellGrid?.ExitPreBattleDeploymentSwapMode();
            CloseSubPanels();
            cellGrid?.BeginBattleFromPreBattle();
        }

        private void SaveRosterChanges()
        {
            cellGrid?.SaveDeploymentRosterChanges();
            RefreshAll();
        }

        private void CloseSubPanels()
        {
            preferredSelectUnitId = null;
            if (selectUnitsPanel != null)
            {
                selectUnitsPanel.gameObject.SetActive(false);
            }

            if (switchDeploymentPanel != null)
            {
                switchDeploymentPanel.gameObject.SetActive(false);
            }

            if (inventoryManagementPanel != null)
            {
                inventoryManagementPanel.gameObject.SetActive(false);
            }

            ClearPendingInventoryAction();
        }

        private void RefreshSelectUnitsPanel()
        {
            if (selectUnitsPanel == null || !selectUnitsPanel.gameObject.activeSelf || cellGrid == null)
            {
                return;
            }

            IReadOnlyList<OwnedUnitSaveData> ownedUnits = cellGrid.GetOwnedUnitsForPreBattle();
            string[] roster = cellGrid.GetDeploymentRosterForPreBattle().ToArray();
            int deploymentSlotLimit = GetDeploymentSlotLimit();
            int ownedUnitCount = CountOwnedUnits(ownedUnits);

            if (selectUnitsInstructionText != null)
            {
                selectUnitsInstructionText.text = deploymentSlotLimit <= 0
                    ? GameTextCatalog.Get("ui.pre_battle.status_no_slots", "No deployment slots.")
                    : GameTextCatalog.Format("ui.pre_battle.select_instruction", "Click units to deploy. Max {0}.", deploymentSlotLimit);
            }

            if (autoResizeGeneratedLists || generatedFallbackUi)
            {
                ResizeSelectUnitsPanel(ownedUnitCount);
            }

            if (reserveListContainer != null)
            {
                reserveListContainer.gameObject.SetActive(false);
            }

            RebuildOwnedUnitButtons(selectSlotContainer, ownedUnits, roster);
        }

        private void RefreshSwitchDeploymentPanel()
        {
            if (switchDeploymentPanel == null || !switchDeploymentPanel.gameObject.activeSelf || cellGrid == null)
            {
                return;
            }

            string[] roster = cellGrid.GetDeploymentRosterForPreBattle().ToArray();
            int selectedSlotIndex = cellGrid.SelectedPreBattleDeploymentSlotIndex;
            if (switchDeploymentInstructionText != null)
            {
                switchDeploymentInstructionText.text = selectedSlotIndex < 0
                    ? GameTextCatalog.Get("ui.pre_battle.switch_instruction", "Pick 2 units to swap.")
                    : GameTextCatalog.Format("ui.pre_battle.switch_selected_instruction", "Selected: {0}", BuildRosterDisplayName(roster, selectedSlotIndex));
            }

            if (autoResizeGeneratedLists || generatedFallbackUi)
            {
                ResizeSwitchDeploymentPanel(roster.Length);
            }

            if (switchSlotContainer != null)
            {
                switchSlotContainer.gameObject.SetActive(false);
            }
        }

        private void RefreshInventoryPanel()
        {
            if (inventoryManagementPanel == null || !inventoryManagementPanel.gameObject.activeSelf || cellGrid == null)
            {
                return;
            }

            IReadOnlyList<OwnedUnitSaveData> ownedUnits = cellGrid.GetOwnedUnitsForPreBattle();
            EnsureSelectedInventoryUnit(ownedUnits);
            OwnedUnitSaveData selectedUnit = FindOwnedUnit(ownedUnits, selectedInventoryManagementUnitId);
            string selectedName = GetUnitDisplayName(selectedUnit);

            if (inventoryManagementInstructionText != null)
            {
                string slotText = selectedUnit != null
                    ? $"{CountInventoryEntries(selectedUnit.Inventory)}/{UnitInventory.MaxSlots}"
                    : $"0/{UnitInventory.MaxSlots}";
                inventoryManagementInstructionText.text = selectedUnit == null
                    ? "Select a unit."
                    : $"{selectedName} inventory {slotText}";
            }

            SetFilterButtonVisuals();
            RebuildInventoryUnitButtons(ownedUnits);
            RebuildInventoryOwnItems(selectedUnit);
            RebuildInventoryOtherItems(ownedUnits, selectedUnit);
            RefreshInventoryActionPanel();
        }

        private void EnsureSelectedInventoryUnit(IReadOnlyList<OwnedUnitSaveData> ownedUnits)
        {
            if (FindOwnedUnit(ownedUnits, selectedInventoryManagementUnitId) != null)
            {
                return;
            }

            selectedInventoryManagementUnitId = ownedUnits?
                .FirstOrDefault(unit => unit != null && !string.IsNullOrWhiteSpace(unit.UnitId))
                ?.UnitId;
            ClearPendingInventoryAction();
        }

        private void RebuildInventoryUnitButtons(IReadOnlyList<OwnedUnitSaveData> ownedUnits)
        {
            RectTransform unitContainer = ResolveInventoryListContainer(inventoryManagementUnitContainer, inventoryManagementUnitButtonTemplate);
            if (unitContainer == null || inventoryManagementUnitButtonTemplate == null)
            {
                return;
            }

            ClearDynamicChildrenExcept(unitContainer, inventoryManagementUnitButtonTemplate);
            int buttonIndex = 0;
            foreach (OwnedUnitSaveData unit in ownedUnits ?? Array.Empty<OwnedUnitSaveData>())
            {
                if (unit == null || string.IsNullOrWhiteSpace(unit.UnitId))
                {
                    continue;
                }

                string unitId = unit.UnitId;
                bool isSelected = string.Equals(unitId, selectedInventoryManagementUnitId, StringComparison.OrdinalIgnoreCase);
                Button button = CreateInventoryTemplateButton(
                    inventoryManagementUnitButtonTemplate,
                    unitContainer,
                    GetUnitDisplayName(unit),
                    () =>
                    {
                        selectedInventoryManagementUnitId = unitId;
                        ClearPendingInventoryAction();
                        RefreshAll();
                    });
                button.name = $"PreBattleInventoryUnit:{unitId}";
                SetButtonColor(button, isSelected ? new Color(0.34f, 0.57f, 0.9f, 0.95f) : new Color(0.88f, 0.88f, 0.9f, 0.95f));
                buttonIndex++;
            }

            if (buttonIndex == 0)
            {
                CreateInventoryTemplateButton(inventoryManagementUnitButtonTemplate, unitContainer, "No owned units found.", null, false);
            }
        }

        private void RebuildInventoryOwnItems(OwnedUnitSaveData selectedUnit)
        {
            RectTransform ownItemsContainer = ResolveInventoryListContainer(inventoryManagementOwnItemsContainer, inventoryManagementOwnItemButtonTemplate);
            if (ownItemsContainer == null || inventoryManagementOwnItemButtonTemplate == null)
            {
                return;
            }

            ClearDynamicChildrenExcept(ownItemsContainer, inventoryManagementOwnItemButtonTemplate);
            int buttonIndex = 0;
            foreach (IndexedInventoryEntry indexedEntry in GetFilteredIndexedInventoryEntries(selectedUnit?.Inventory))
            {
                int sourceIndex = indexedEntry.Index;
                string itemLabel = BuildItemDisplayLabel(indexedEntry.Entry);
                Button button = CreateInventoryTemplateButton(
                    inventoryManagementOwnItemButtonTemplate,
                    ownItemsContainer,
                    itemLabel,
                    () => BeginGiveInventoryAction(selectedUnit?.UnitId, sourceIndex, itemLabel));
                button.name = $"PreBattleInventoryOwn:{sourceIndex}";
                buttonIndex++;
            }

            if (buttonIndex == 0)
            {
                CreateInventoryTemplateButton(inventoryManagementOwnItemButtonTemplate, ownItemsContainer, "No matching items.", null, false);
            }
        }

        private void RebuildInventoryOtherItems(IReadOnlyList<OwnedUnitSaveData> ownedUnits, OwnedUnitSaveData selectedUnit)
        {
            RectTransform otherItemsContainer = ResolveInventoryListContainer(inventoryManagementOtherItemsContainer, inventoryManagementOtherItemButtonTemplate);
            if (otherItemsContainer == null || inventoryManagementOtherItemButtonTemplate == null)
            {
                return;
            }

            ClearDynamicChildrenExcept(otherItemsContainer, inventoryManagementOtherItemButtonTemplate);
            bool targetInventoryFull = CountInventoryEntries(selectedUnit?.Inventory) >= UnitInventory.MaxSlots;
            int buttonIndex = 0;
            foreach (OwnedUnitSaveData unit in ownedUnits ?? Array.Empty<OwnedUnitSaveData>())
            {
                if (unit == null || string.IsNullOrWhiteSpace(unit.UnitId))
                {
                    continue;
                }

                bool isOwnUnit = selectedUnit != null && string.Equals(unit.UnitId, selectedUnit.UnitId, StringComparison.OrdinalIgnoreCase);
                foreach (IndexedInventoryEntry indexedEntry in GetFilteredIndexedInventoryEntries(unit.Inventory))
                {
                    int sourceIndex = indexedEntry.Index;
                    string itemLabel = $"{GetUnitDisplayName(unit)}: {BuildItemDisplayLabel(indexedEntry.Entry)}";
                    bool canTake = !isOwnUnit && !targetInventoryFull;
                    Button button = CreateInventoryTemplateButton(
                        inventoryManagementOtherItemButtonTemplate,
                        otherItemsContainer,
                        itemLabel,
                        () => BeginTakeInventoryAction(selectedUnit?.UnitId, unit.UnitId, sourceIndex, sourceIsStorage: false, itemLabel),
                        canTake);
                    button.name = $"PreBattleInventoryOther:{unit.UnitId}:{sourceIndex}";
                    buttonIndex++;
                }
            }

            IReadOnlyList<SavedInventoryEntryData> storageItems = cellGrid.GetStorageItemsForPreBattle();
            foreach (IndexedInventoryEntry indexedEntry in GetFilteredIndexedInventoryEntries(storageItems))
            {
                int sourceIndex = indexedEntry.Index;
                string itemLabel = $"Storage: {BuildItemDisplayLabel(indexedEntry.Entry)}";
                Button button = CreateInventoryTemplateButton(
                    inventoryManagementOtherItemButtonTemplate,
                    otherItemsContainer,
                    itemLabel,
                    () => BeginTakeInventoryAction(selectedUnit?.UnitId, null, sourceIndex, sourceIsStorage: true, itemLabel),
                    !targetInventoryFull);
                button.name = $"PreBattleInventoryStorage:{sourceIndex}";
                buttonIndex++;
            }

            if (buttonIndex == 0)
            {
                CreateInventoryTemplateButton(inventoryManagementOtherItemButtonTemplate, otherItemsContainer, "No matching items.", null, false);
            }
        }

        private void SetInventoryFilter(InventoryManagementFilterKind filter)
        {
            inventoryManagementFilter = filter;
            ClearPendingInventoryAction();
            RefreshAll();
        }

        private void SetInventoryFilterWeapon()
        {
            SetInventoryFilter(InventoryManagementFilterKind.Weapon);
        }

        private void SetInventoryFilterAccessory()
        {
            SetInventoryFilter(InventoryManagementFilterKind.Accessory);
        }

        private void SetInventoryFilterConsumable()
        {
            SetInventoryFilter(InventoryManagementFilterKind.Consumable);
        }

        private void SetInventoryFilterAll()
        {
            SetInventoryFilter(InventoryManagementFilterKind.All);
        }

        private void BeginTakeInventoryAction(string targetUnitId, string sourceUnitId, int sourceItemIndex, bool sourceIsStorage, string itemLabel)
        {
            if (string.IsNullOrWhiteSpace(targetUnitId) || sourceItemIndex < 0)
            {
                return;
            }

            pendingInventoryManagementAction = new PendingInventoryManagementAction
            {
                TargetUnitId = targetUnitId,
                SourceUnitId = sourceUnitId,
                SourceItemIndex = sourceItemIndex,
                SourceIsStorage = sourceIsStorage,
                GiveToStorage = false,
                ItemLabel = itemLabel
            };
            RefreshInventoryActionPanel();
        }

        private void BeginGiveInventoryAction(string sourceUnitId, int sourceItemIndex, string itemLabel)
        {
            if (string.IsNullOrWhiteSpace(sourceUnitId) || sourceItemIndex < 0)
            {
                return;
            }

            pendingInventoryManagementAction = new PendingInventoryManagementAction
            {
                SourceUnitId = sourceUnitId,
                SourceItemIndex = sourceItemIndex,
                GiveToStorage = true,
                ItemLabel = itemLabel
            };
            RefreshInventoryActionPanel();
        }

        private void ConfirmPendingInventoryAction()
        {
            if (pendingInventoryManagementAction == null || cellGrid == null)
            {
                return;
            }

            bool changed = pendingInventoryManagementAction.GiveToStorage
                ? cellGrid.GivePreBattleInventoryItemToStorage(pendingInventoryManagementAction.SourceUnitId, pendingInventoryManagementAction.SourceItemIndex)
                : cellGrid.TakePreBattleInventoryItem(
                    pendingInventoryManagementAction.TargetUnitId,
                    pendingInventoryManagementAction.SourceUnitId,
                    pendingInventoryManagementAction.SourceItemIndex,
                    pendingInventoryManagementAction.SourceIsStorage);

            ClearPendingInventoryAction();
            if (changed)
            {
                RefreshAll();
            }
        }

        private void ClearPendingInventoryAction()
        {
            pendingInventoryManagementAction = null;
            RefreshInventoryActionPanel();
        }

        private void RefreshInventoryActionPanel()
        {
            if (inventoryManagementActionPanel == null)
            {
                return;
            }

            bool hasAction = pendingInventoryManagementAction != null;
            inventoryManagementActionPanel.gameObject.SetActive(hasAction);
            if (!hasAction)
            {
                return;
            }

            if (inventoryManagementActionText != null)
            {
                inventoryManagementActionText.text = pendingInventoryManagementAction.GiveToStorage
                    ? $"Give {pendingInventoryManagementAction.ItemLabel} to Storage?"
                    : $"Take {pendingInventoryManagementAction.ItemLabel}?";
            }

            TMP_Text confirmText = inventoryManagementConfirmActionButton?.GetComponentInChildren<TMP_Text>();
            if (confirmText != null)
            {
                confirmText.text = pendingInventoryManagementAction.GiveToStorage ? "Give to Storage" : "Take";
            }
        }

        private void SetFilterButtonVisuals()
        {
            SetFilterButtonVisual(inventoryManagementWeaponFilterButton, InventoryManagementFilterKind.Weapon);
            SetFilterButtonVisual(inventoryManagementAccessoryFilterButton, InventoryManagementFilterKind.Accessory);
            SetFilterButtonVisual(inventoryManagementConsumableFilterButton, InventoryManagementFilterKind.Consumable);
            SetFilterButtonVisual(inventoryManagementAllFilterButton, InventoryManagementFilterKind.All);
        }

        private void SetFilterButtonVisual(Button button, InventoryManagementFilterKind filter)
        {
            if (button == null)
            {
                return;
            }

            SetButtonColor(button, inventoryManagementFilter == filter ? new Color(0.34f, 0.57f, 0.9f, 0.95f) : new Color(0.88f, 0.88f, 0.9f, 0.95f));
        }

        private void RebuildOwnedUnitButtons(RectTransform container, IReadOnlyList<OwnedUnitSaveData> ownedUnits, IReadOnlyList<string> roster)
        {
            ClearDynamicChildren(container);
            if (container == null)
            {
                return;
            }

            if (ownedUnits == null || ownedUnits.Count == 0)
            {
                CreateRuntimeText(container, GameTextCatalog.Get("ui.pre_battle.no_owned_units", "No owned units found."), new Vector2(12f, -12f), GetTextSize(container), 16, FontStyles.Italic, TextAlignmentOptions.Left);
                return;
            }

            int buttonIndex = 0;
            int deploymentSlotLimit = GetDeploymentSlotLimit();
            int filledSlotCount = CountFilledRosterSlots(roster);
            bool canRemoveSelectedUnit = filledSlotCount > 1;
            bool hasEmptySlot = FindFirstEmptyRosterSlotIndex(roster) >= 0 && deploymentSlotLimit > 0;
            for (int i = 0; i < roster.Count; i++)
            {
                string rosterUnitId = roster[i];
                if (string.IsNullOrWhiteSpace(rosterUnitId))
                {
                Button emptyButton = CreateRuntimeButton(
                        container,
                        $"{i + 1}. {GameTextCatalog.Get("ui.common.empty", "Empty")}",
                        GetButtonPosition(buttonIndex),
                        GetButtonSize(container),
                        null);
                    emptyButton.name = $"PreBattleSelectEmpty:{i}";
                    emptyButton.interactable = false;
                    Image emptyImage = emptyButton.GetComponent<Image>();
                    if (emptyImage != null)
                    {
                        emptyImage.color = new Color(0.6f, 0.6f, 0.65f, 0.8f);
                    }

                    buttonIndex++;
                    continue;
                }

                OwnedUnitSaveData slottedUnit = ownedUnits.FirstOrDefault(unit =>
                    unit != null && string.Equals(unit.UnitId, rosterUnitId, StringComparison.OrdinalIgnoreCase));
                string displayName = string.IsNullOrWhiteSpace(slottedUnit?.UnitName)
                    ? rosterUnitId
                    : slottedUnit.UnitName;
                string clickedUnitId = rosterUnitId;
                Button selectedButton = CreateRuntimeButton(
                    container,
                    $"{i + 1}. {displayName}",
                    GetButtonPosition(buttonIndex),
                    GetButtonSize(container),
                    () =>
                    {
                        preferredSelectUnitId = clickedUnitId;
                        ToggleUnitSelection(clickedUnitId);
                        RefreshAll();
                    });
                selectedButton.name = $"PreBattleSelectUnit:{clickedUnitId}";
                selectedButton.interactable = canRemoveSelectedUnit;
                Image selectedImage = selectedButton.GetComponent<Image>();
                if (selectedImage != null)
                {
                    selectedImage.color = selectedButton.interactable
                        ? new Color(0.34f, 0.57f, 0.9f, 0.95f)
                        : new Color(0.45f, 0.58f, 0.78f, 0.9f);
                }

                buttonIndex++;
            }

            IEnumerable<OwnedUnitSaveData> orderedOwnedUnits = GetOwnedUnitsInDisplayOrder(ownedUnits, roster);
            foreach (OwnedUnitSaveData ownedUnit in orderedOwnedUnits)
            {
                if (ownedUnit == null || string.IsNullOrWhiteSpace(ownedUnit.UnitId))
                {
                    continue;
                }

                int rosterIndex = FindRosterIndex(roster, ownedUnit.UnitId);
                if (rosterIndex >= 0)
                {
                    continue;
                }

                bool canAdd = hasEmptySlot;
                string displayName = string.IsNullOrWhiteSpace(ownedUnit.UnitName)
                    ? ownedUnit.UnitId
                    : ownedUnit.UnitName;
                string label = displayName;
                string clickedUnitId = ownedUnit.UnitId;
                Button button = CreateRuntimeButton(
                    container,
                    label,
                    GetButtonPosition(buttonIndex),
                    GetButtonSize(container),
                    () =>
                    {
                        preferredSelectUnitId = clickedUnitId;
                        ToggleUnitSelection(clickedUnitId);
                        RefreshAll();
                    });
                button.name = $"PreBattleSelectUnit:{clickedUnitId}";
                button.interactable = canAdd;
                buttonIndex++;

                Image image = button.GetComponent<Image>();
                if (image != null)
                {
                    image.color = canAdd
                        ? new Color(0.88f, 0.88f, 0.9f, 0.95f)
                        : new Color(0.6f, 0.6f, 0.65f, 0.8f);
                }
            }
            if (buttonIndex == 0)
            {
                CreateRuntimeText(container, GameTextCatalog.Get("ui.pre_battle.no_owned_units", "No owned units found."), new Vector2(12f, -12f), GetTextSize(container), 16, FontStyles.Italic, TextAlignmentOptions.Left);
            }
        }

        private void ToggleUnitSelection(string unitId)
        {
            if (cellGrid == null || string.IsNullOrWhiteSpace(unitId))
            {
                return;
            }

            List<string> roster = cellGrid.GetDeploymentRosterForPreBattle().ToList();
            int existingIndex = roster.FindIndex(rosterUnitId => string.Equals(rosterUnitId, unitId, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                if (CountFilledRosterSlots(roster) <= 1)
                {
                    return;
                }

                cellGrid.ClearDeploymentSlotUnit(existingIndex);
                return;
            }

            int emptySlotIndex = FindFirstEmptyRosterSlotIndex(roster);
            if (emptySlotIndex < 0)
            {
                return;
            }

            cellGrid.ReplaceDeploymentSlotUnit(emptySlotIndex, unitId);
        }

        private int GetDeploymentSlotLimit()
        {
            return cellGrid != null ? Mathf.Max(0, cellGrid.GetDeploymentSlotCount()) : 0;
        }

        private static int CountOwnedUnits(IReadOnlyList<OwnedUnitSaveData> ownedUnits)
        {
            if (ownedUnits == null || ownedUnits.Count == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < ownedUnits.Count; i++)
            {
                OwnedUnitSaveData ownedUnit = ownedUnits[i];
                if (ownedUnit == null || string.IsNullOrWhiteSpace(ownedUnit.UnitId))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private static OwnedUnitSaveData FindOwnedUnit(IReadOnlyList<OwnedUnitSaveData> ownedUnits, string unitId)
        {
            if (ownedUnits == null || string.IsNullOrWhiteSpace(unitId))
            {
                return null;
            }

            return ownedUnits.FirstOrDefault(unit =>
                unit != null && string.Equals(unit.UnitId, unitId, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetUnitDisplayName(OwnedUnitSaveData unit)
        {
            if (unit == null)
            {
                return "None";
            }

            return string.IsNullOrWhiteSpace(unit.UnitName) ? unit.UnitId : unit.UnitName;
        }

        private IEnumerable<IndexedInventoryEntry> GetFilteredIndexedInventoryEntries(IEnumerable<SavedInventoryEntryData> entries)
        {
            int index = 0;
            foreach (SavedInventoryEntryData entry in entries ?? Array.Empty<SavedInventoryEntryData>())
            {
                if (entry != null && !string.IsNullOrWhiteSpace(entry.ItemId) && MatchesInventoryFilter(entry))
                {
                    yield return new IndexedInventoryEntry(index, entry);
                }

                index++;
            }
        }

        private bool MatchesInventoryFilter(SavedInventoryEntryData entry)
        {
            if (inventoryManagementFilter == InventoryManagementFilterKind.All)
            {
                return true;
            }

            ItemData data = ItemRegistry.Get(entry?.ItemId);
            return inventoryManagementFilter switch
            {
                InventoryManagementFilterKind.Weapon => data is WeaponData,
                InventoryManagementFilterKind.Accessory => data is AccessoryData,
                InventoryManagementFilterKind.Consumable => data is ConsumableData,
                _ => true
            };
        }

        private static int CountInventoryEntries(IEnumerable<SavedInventoryEntryData> entries)
        {
            return entries?.Count(entry => entry != null && !string.IsNullOrWhiteSpace(entry.ItemId)) ?? 0;
        }

        private static string BuildItemDisplayLabel(SavedInventoryEntryData entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId))
            {
                return "Unknown";
            }

            ItemData data = ItemRegistry.Get(entry.ItemId);
            string name = string.IsNullOrWhiteSpace(data?.Name) ? entry.ItemId : data.Name;
            if (data is ConsumableData && entry.RemainingCharges >= 0)
            {
                return $"{name} x{entry.RemainingCharges}";
            }

            return name;
        }

        private void ResizeSelectUnitsPanel(int ownedUnitCount)
        {
            if (selectUnitsPanel == null || selectSlotContainer == null)
            {
                return;
            }

            float listHeight = GetContainerHeight(Mathf.Max(1, ownedUnitCount));
            selectSlotContainer.sizeDelta = new Vector2(selectSlotContainer.sizeDelta.x, listHeight);
            if (reserveListContainer != null)
            {
                reserveListContainer.gameObject.SetActive(false);
            }

            if (generatedFallbackUi)
            {
                selectUnitsPanel.sizeDelta = new Vector2(selectUnitsPanel.sizeDelta.x, Mathf.Max(250f, 156f + listHeight));
            }
        }

        private void ResizeSwitchDeploymentPanel(int rosterCount)
        {
            if (switchDeploymentPanel == null)
            {
                return;
            }

            if (generatedFallbackUi)
            {
                switchDeploymentPanel.sizeDelta = new Vector2(switchDeploymentPanel.sizeDelta.x, 164f);
            }
        }

        private string BuildRosterDisplayName(IReadOnlyList<string> roster, int rosterIndex)
        {
            if (roster == null || rosterIndex < 0 || rosterIndex >= roster.Count)
            {
                return GameTextCatalog.Get("ui.common.none", "None");
            }

            string unitId = roster[rosterIndex];
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return GameTextCatalog.Get("ui.common.empty", "Empty");
            }

            OwnedUnitSaveData ownedUnit = cellGrid?.GetOwnedUnitsForPreBattle()
                .FirstOrDefault(unit => unit != null && string.Equals(unit.UnitId, unitId, StringComparison.OrdinalIgnoreCase));
            if (ownedUnit == null)
            {
                return unitId;
            }

            return string.IsNullOrWhiteSpace(ownedUnit.UnitName) ? ownedUnit.UnitId : ownedUnit.UnitName;
        }

        private Button ResolvePreferredFocusButton(IReadOnlyList<Button> activeButtons)
        {
            if (activeButtons == null || activeButtons.Count == 0)
            {
                return null;
            }

            if (selectUnitsPanel != null && selectUnitsPanel.gameObject.activeSelf && !string.IsNullOrWhiteSpace(preferredSelectUnitId))
            {
                string expectedName = $"PreBattleSelectUnit:{preferredSelectUnitId}";
                Button preferred = activeButtons.FirstOrDefault(button =>
                    button != null
                    && string.Equals(button.name, expectedName, StringComparison.OrdinalIgnoreCase));
                if (preferred != null)
                {
                    return preferred;
                }
            }

            return null;
        }

        private static int FindRosterIndex(IReadOnlyList<string> roster, string unitId)
        {
            if (roster == null || string.IsNullOrWhiteSpace(unitId))
            {
                return -1;
            }

            for (int i = 0; i < roster.Count; i++)
            {
                if (string.Equals(roster[i], unitId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindFirstEmptyRosterSlotIndex(IReadOnlyList<string> roster)
        {
            if (roster == null)
            {
                return -1;
            }

            for (int i = 0; i < roster.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(roster[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int CountFilledRosterSlots(IReadOnlyList<string> roster)
        {
            if (roster == null)
            {
                return 0;
            }

            int filledCount = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(roster[i]))
                {
                    filledCount++;
                }
            }

            return filledCount;
        }

        private static IEnumerable<OwnedUnitSaveData> GetOwnedUnitsInDisplayOrder(IReadOnlyList<OwnedUnitSaveData> ownedUnits, IReadOnlyList<string> roster)
        {
            List<OwnedUnitSaveData> orderedUnits = new List<OwnedUnitSaveData>();
            HashSet<string> addedUnitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < (roster?.Count ?? 0); i++)
            {
                string rosterUnitId = roster[i];
                if (string.IsNullOrWhiteSpace(rosterUnitId))
                {
                    continue;
                }

                OwnedUnitSaveData ownedUnit = ownedUnits?.FirstOrDefault(unit =>
                    unit != null && string.Equals(unit.UnitId, rosterUnitId, StringComparison.OrdinalIgnoreCase));
                if (ownedUnit == null || !addedUnitIds.Add(ownedUnit.UnitId))
                {
                    continue;
                }

                orderedUnits.Add(ownedUnit);
            }

            foreach (OwnedUnitSaveData ownedUnit in ownedUnits ?? Array.Empty<OwnedUnitSaveData>())
            {
                if (ownedUnit == null || string.IsNullOrWhiteSpace(ownedUnit.UnitId) || !addedUnitIds.Add(ownedUnit.UnitId))
                {
                    continue;
                }

                orderedUnits.Add(ownedUnit);
            }

            return orderedUnits;
        }

        private void BuildFallbackUi()
        {
            rootPanel = CreateRuntimePanel("Pre Battle Panel", canvas.transform, new Vector2(16f, -16f), new Vector2(220f, 232f), new Color(0.08f, 0.18f, 0.26f, 0.86f));
            CreateRuntimeText(rootPanel, GameTextCatalog.Get("ui.pre_battle.title", "Pre Battle"), new Vector2(16f, -12f), new Vector2(188f, 28f), 24, FontStyles.Bold, TextAlignmentOptions.Center);

            battleStartButton = CreateRuntimeButton(rootPanel, GameTextCatalog.Get("ui.pre_battle.button_battle_start", "Battle Start"), new Vector2(16f, -52f), new Vector2(188f, 34f), null);
            selectUnitsButton = CreateRuntimeButton(rootPanel, GameTextCatalog.Get("ui.pre_battle.button_select_units", "Select Units"), new Vector2(16f, -94f), new Vector2(188f, 34f), null);
            switchDeploymentButton = CreateRuntimeButton(rootPanel, GameTextCatalog.Get("ui.pre_battle.button_switch_deployment", "Switch Deployment"), new Vector2(16f, -136f), new Vector2(188f, 34f), null);
            saveButton = CreateRuntimeButton(rootPanel, GameTextCatalog.Get("ui.pre_battle.button_save", "Save"), new Vector2(16f, -178f), new Vector2(188f, 34f), null);
            statusText = CreateRuntimeText(rootPanel, string.Empty, new Vector2(16f, -218f), new Vector2(188f, 44f), 16, FontStyles.Normal, TextAlignmentOptions.Left);

            selectUnitsPanel = CreateRuntimePanel("Select Units Panel", canvas.transform, new Vector2(252f, -16f), new Vector2(420f, 330f), new Color(0.12f, 0.12f, 0.18f, 0.92f));
            CreateRuntimeText(selectUnitsPanel, GameTextCatalog.Get("ui.pre_battle.button_select_units", "Select Units"), new Vector2(16f, -12f), new Vector2(280f, 28f), 24, FontStyles.Bold, TextAlignmentOptions.Left);
            selectUnitsBackButton = CreateRuntimeButton(selectUnitsPanel, GameTextCatalog.Get("ui.pre_battle.button_back", "Back"), new Vector2(320f, -12f), new Vector2(84f, 30f), null);
            selectUnitsInstructionText = CreateRuntimeText(selectUnitsPanel, string.Empty, new Vector2(16f, -48f), new Vector2(388f, 36f), 16, FontStyles.Normal, TextAlignmentOptions.Left);
            CreateRuntimeText(selectUnitsPanel, "Units", new Vector2(16f, -84f), new Vector2(220f, 24f), 16, FontStyles.Bold, TextAlignmentOptions.Left);
            selectSlotContainer = CreateRuntimePanel("Select Slot Container", selectUnitsPanel, new Vector2(16f, -112f), new Vector2(388f, 176f), new Color(0f, 0f, 0f, 0.16f));
            reserveListContainer = CreateRuntimePanel("Reserve List Container", selectUnitsPanel, new Vector2(16f, -296f), new Vector2(388f, 32f), new Color(0f, 0f, 0f, 0f));
            reserveListContainer.gameObject.SetActive(false);
            selectUnitsPanel.gameObject.SetActive(false);

            switchDeploymentPanel = CreateRuntimePanel("Switch Deployment Panel", canvas.transform, new Vector2(252f, -16f), new Vector2(420f, 280f), new Color(0.12f, 0.12f, 0.18f, 0.92f));
            CreateRuntimeText(switchDeploymentPanel, GameTextCatalog.Get("ui.pre_battle.button_switch_deployment", "Switch Deployment"), new Vector2(16f, -12f), new Vector2(280f, 28f), 24, FontStyles.Bold, TextAlignmentOptions.Left);
            switchDeploymentBackButton = CreateRuntimeButton(switchDeploymentPanel, GameTextCatalog.Get("ui.pre_battle.button_back", "Back"), new Vector2(320f, -12f), new Vector2(84f, 30f), null);
            switchDeploymentInstructionText = CreateRuntimeText(switchDeploymentPanel, string.Empty, new Vector2(16f, -48f), new Vector2(388f, 40f), 16, FontStyles.Normal, TextAlignmentOptions.Left);
            switchDeploymentPanel.gameObject.SetActive(false);
        }

        private Button CreateSceneAuthoredSaveButton()
        {
            if (rootPanel == null)
            {
                return null;
            }

            Vector2 position = new Vector2(0f, -50f);
            Vector2 size = new Vector2(160f, 30f);
            if (selectUnitsButton != null)
            {
                RectTransform selectRect = selectUnitsButton.transform as RectTransform;
                if (selectRect != null)
                {
                    position = selectRect.anchoredPosition;
                    size = selectRect.sizeDelta;
                }
            }

            if (switchDeploymentButton != null)
            {
                RectTransform switchRect = switchDeploymentButton.transform as RectTransform;
                if (switchRect != null)
                {
                    position.y = switchRect.anchoredPosition.y;
                    if (size == default)
                    {
                        size = switchRect.sizeDelta;
                    }
                }
            }

            Button button = CreateRuntimeButton(
                rootPanel,
                GameTextCatalog.Get("ui.pre_battle.button_save", "Save"),
                position,
                size,
                null);
            button.name = "Save Button";
            return button;
        }

        private TMP_FontAsset ResolveFontAsset()
        {
            if (TMP_Settings.defaultFontAsset != null)
            {
                return TMP_Settings.defaultFontAsset;
            }

            return Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()
                .Select(text => text != null ? text.font : null)
                .FirstOrDefault(font => font != null);
        }

        private static Vector2 GetButtonPosition(int rowIndex)
        {
            return new Vector2(ContainerPadding, -(ContainerPadding + (rowIndex * (ButtonHeight + ButtonSpacing))));
        }

        private static Vector2 GetButtonSize(RectTransform container)
        {
            float width = Mathf.Max(MinimumButtonWidth, GetContainerWidth(container) - (ContainerPadding * 2f));
            return new Vector2(width, ButtonHeight);
        }

        private static Vector2 GetTextSize(RectTransform container)
        {
            float width = Mathf.Max(MinimumButtonWidth, GetContainerWidth(container) - (ContainerPadding * 2f));
            return new Vector2(width, 24f);
        }

        private static float GetContainerHeight(int rowCount)
        {
            return (ContainerPadding * 2f) + (rowCount * ButtonHeight) + (Mathf.Max(0, rowCount - 1) * ButtonSpacing);
        }

        private static float GetContainerWidth(RectTransform container)
        {
            if (container == null)
            {
                return 0f;
            }

            float width = container.rect.width;
            if (width <= 0f)
            {
                width = container.sizeDelta.x;
            }

            return width;
        }

        private static void ClearDynamicChildren(RectTransform container)
        {
            if (container == null)
            {
                return;
            }

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Destroy(container.GetChild(i).gameObject);
            }
        }

        private static void ClearDynamicChildrenExcept(RectTransform container, Button template)
        {
            if (container == null)
            {
                return;
            }

            Transform templateTransform = template != null ? template.transform : null;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                if (child == templateTransform)
                {
                    continue;
                }

                Destroy(child.gameObject);
            }
        }

        private static void PrepareInventoryButtonTemplate(Button template)
        {
            if (template == null)
            {
                return;
            }

            template.gameObject.SetActive(false);
        }

        private static RectTransform ResolveInventoryListContainer(RectTransform assignedContainer, Button template)
        {
            if (assignedContainer == null)
            {
                return null;
            }

            ScrollRect scrollRect = assignedContainer.GetComponent<ScrollRect>();
            if (scrollRect == null)
            {
                return assignedContainer;
            }

            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            RectTransform content = scrollRect.content;
            if (content == null)
            {
                content = FindChildRectTransformByName(assignedContainer, "Content");
            }

            if (content == null)
            {
                Transform contentParent = scrollRect.viewport != null ? scrollRect.viewport : assignedContainer;
                GameObject contentObject = new GameObject("Content", typeof(RectTransform));
                contentObject.transform.SetParent(contentParent, false);
                content = contentObject.GetComponent<RectTransform>();
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.anchoredPosition = Vector2.zero;
                content.sizeDelta = Vector2.zero;
            }

            scrollRect.content = content;
            if (template != null && template.transform.parent != content)
            {
                template.transform.SetParent(content, false);
            }

            return content;
        }

        private static RectTransform FindChildRectTransformByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase)
                    && child is RectTransform rectTransform)
                {
                    return rectTransform;
                }

                RectTransform nested = FindChildRectTransformByName(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private Button CreateInventoryTemplateButton(Button template, RectTransform container, string label, Action onClick, bool interactable = true)
        {
            if (template == null || container == null)
            {
                return null;
            }

            Button button = Instantiate(template, container);
            button.name = $"{template.name}:{label}";
            button.gameObject.SetActive(true);
            button.interactable = interactable;
            button.onClick.RemoveAllListeners();
            if (onClick != null && interactable)
            {
                button.onClick.AddListener(() => onClick.Invoke());
            }

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
            }

            return button;
        }

        private RectTransform CreateRuntimePanel(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Color backgroundColor)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            RectTransform rectTransform = panel.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Image image = panel.GetComponent<Image>();
            image.color = backgroundColor;
            return rectTransform;
        }

        private Button CreateRuntimeButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 size, Action onClick)
        {
            GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.88f, 0.88f, 0.9f, 0.95f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.95f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.8f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.targetGraphic = image;
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick.Invoke());
            }

            CreateRuntimeText(rectTransform, label, new Vector2(0f, 0f), size, 18, FontStyles.Normal, TextAlignmentOptions.Center);
            return button;
        }

        private static void SetButtonColor(Button button, Color color)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = new Color(
                Mathf.Min(1f, color.r + 0.08f),
                Mathf.Min(1f, color.g + 0.08f),
                Mathf.Min(1f, color.b + 0.08f),
                Mathf.Min(1f, color.a + 0.08f));
            colors.pressedColor = new Color(
                Mathf.Max(0f, color.r - 0.12f),
                Mathf.Max(0f, color.g - 0.12f),
                Mathf.Max(0f, color.b - 0.12f),
                color.a);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
        }

        private TMP_Text CreateRuntimeText(Transform parent, string content, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = fontAsset;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }
    }
}
