using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Windy.Srpg.Game.Campaign;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Windy.Srpg.Game.UI
{
    public sealed class PreBattleUIController : MonoBehaviour
    {
        private const float ButtonHeight = 34f;
        private const float ButtonSpacing = 8f;
        private const float ContainerPadding = 12f;
        private const float MinimumButtonWidth = 96f;

        [Header("References")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CustomCellGrid cellGrid;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private bool autoGenerateUiIfMissing = true;
        [SerializeField] private bool autoResizeGeneratedLists;

        [Header("Root UI")]
        [SerializeField] private RectTransform rootPanel;
        [SerializeField] private Button battleStartButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button selectUnitsButton;
        [SerializeField] private Button switchDeploymentButton;
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
        [SerializeField] private RectTransform switchSlotContainer;

        private TMP_FontAsset fontAsset;
        private int pendingSwapSlotIndex = -1;
        private bool initialized;
        private bool generatedFallbackUi;

        public void Initialize(CustomCellGrid grid, Button linkedEndTurnButton)
        {
            if (initialized)
            {
                return;
            }

            if (grid != null)
            {
                cellGrid = grid;
            }

            if (linkedEndTurnButton != null)
            {
                endTurnButton = linkedEndTurnButton;
            }

            if (cellGrid == null)
            {
                cellGrid = FindAnyObjectByType<CustomCellGrid>();
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
            initialized = true;
        }

        private void OnDestroy()
        {
            UnhookButtonEvents();
            UnhookGridEvents();
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

            if (switchDeploymentPanel != null && switchSlotContainer == null)
            {
                switchSlotContainer = CreateSceneAuthoredSwitchSlotContainer();
            }
        }

        private void HookButtonEvents()
        {
            battleStartButton?.onClick.AddListener(BeginBattle);
            saveButton?.onClick.AddListener(SaveRosterChanges);
            selectUnitsButton?.onClick.AddListener(OpenSelectUnitsPanelFromButton);
            switchDeploymentButton?.onClick.AddListener(OpenSwitchDeploymentPanelFromButton);
            selectUnitsBackButton?.onClick.AddListener(ReturnToMainPanel);
            switchDeploymentBackButton?.onClick.AddListener(ReturnToMainPanel);
        }

        private void UnhookButtonEvents()
        {
            battleStartButton?.onClick.RemoveListener(BeginBattle);
            saveButton?.onClick.RemoveListener(SaveRosterChanges);
            selectUnitsButton?.onClick.RemoveListener(OpenSelectUnitsPanelFromButton);
            switchDeploymentButton?.onClick.RemoveListener(OpenSwitchDeploymentPanelFromButton);
            selectUnitsBackButton?.onClick.RemoveListener(ReturnToMainPanel);
            switchDeploymentBackButton?.onClick.RemoveListener(ReturnToMainPanel);
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

            if (rootPanel != null)
            {
                rootPanel.gameObject.SetActive(showPreBattle && !showSelectUnitsPanel && !showSwitchDeploymentPanel);
            }

            if (endTurnButton != null)
            {
                endTurnButton.gameObject.SetActive(!showPreBattle);
            }

            if (!showPreBattle)
            {
                CloseSubPanels();
                return;
            }

            IReadOnlyList<string> roster = cellGrid.GetDeploymentRosterForPreBattle();
            int deploymentSlotLimit = GetDeploymentSlotLimit();
            if (pendingSwapSlotIndex >= roster.Count)
            {
                pendingSwapSlotIndex = -1;
            }

            if (statusText != null)
            {
                int filledSlotCount = CountFilledRosterSlots(roster);
                string baseStatus = deploymentSlotLimit > 0
                    ? GameTextCatalog.Format("ui.pre_battle.status_roster", "Roster: {0}/{1}", filledSlotCount, deploymentSlotLimit)
                    : GameTextCatalog.Get("ui.pre_battle.status_no_slots", "No deployment slots.");
                statusText.text = cellGrid.HasUnsavedDeploymentRosterChanges
                    ? GameTextCatalog.Format("ui.pre_battle.status_unsaved", "{0} (Unsaved)", baseStatus)
                    : baseStatus;
            }

            if (saveButton != null)
            {
                saveButton.interactable = cellGrid.HasUnsavedDeploymentRosterChanges;
            }

            RefreshSelectUnitsPanel();
            RefreshSwitchDeploymentPanel();
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

            switchDeploymentPanel.gameObject.SetActive(true);
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
            if (selectUnitsPanel != null)
            {
                selectUnitsPanel.gameObject.SetActive(false);
            }

            if (switchDeploymentPanel != null)
            {
                switchDeploymentPanel.gameObject.SetActive(false);
            }
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
            pendingSwapSlotIndex = cellGrid.SelectedPreBattleDeploymentSlotIndex;
            if (switchDeploymentInstructionText != null)
            {
                switchDeploymentInstructionText.text = pendingSwapSlotIndex < 0
                    ? GameTextCatalog.Get("ui.pre_battle.switch_instruction", "Pick 2 units to swap.")
                    : GameTextCatalog.Format("ui.pre_battle.switch_selected_instruction", "Selected: {0}", BuildRosterDisplayName(roster, pendingSwapSlotIndex));
            }

            if (autoResizeGeneratedLists || generatedFallbackUi)
            {
                ResizeSwitchDeploymentPanel(roster.Length);
            }

            if (switchSlotContainer != null)
            {
                ClearDynamicChildren(switchSlotContainer);
                switchSlotContainer.gameObject.SetActive(true);
                RebuildSwitchDeploymentButtons(switchSlotContainer, roster);
            }
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
                        ToggleUnitSelection(clickedUnitId);
                        RefreshAll();
                    });
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
                        ToggleUnitSelection(clickedUnitId);
                        RefreshAll();
                    });
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

        private void RebuildSwitchDeploymentButtons(RectTransform container, IReadOnlyList<string> roster)
        {
            if (container == null)
            {
                return;
            }

            if (roster == null || roster.Count == 0)
            {
                CreateRuntimeText(container, GameTextCatalog.Get("ui.pre_battle.no_roster_units", "No deployed units."), new Vector2(12f, -12f), GetTextSize(container), 16, FontStyles.Italic, TextAlignmentOptions.Left);
                return;
            }

            int buttonIndex = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(roster[i]))
                {
                    continue;
                }

                string label = BuildRosterDisplayName(roster, i);
                int slotIndex = i;
                Button button = CreateRuntimeButton(
                    container,
                    label,
                    GetButtonPosition(buttonIndex),
                    GetButtonSize(container),
                    () =>
                    {
                        cellGrid?.HandlePreBattleDeploymentSlotClicked(slotIndex);
                        RefreshAll();
                    });

                Image image = button.GetComponent<Image>();
                if (image != null)
                {
                    image.color = pendingSwapSlotIndex == i
                        ? new Color(1f, 0.9f, 0.2f, 0.95f)
                        : new Color(0.88f, 0.88f, 0.9f, 0.95f);
                }

                buttonIndex++;
            }

            if (buttonIndex == 0)
            {
                CreateRuntimeText(container, GameTextCatalog.Get("ui.pre_battle.no_roster_units", "No deployed units."), new Vector2(12f, -12f), GetTextSize(container), 16, FontStyles.Italic, TextAlignmentOptions.Left);
            }
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
            if (switchDeploymentPanel == null || switchSlotContainer == null)
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
            switchSlotContainer = CreateRuntimePanel("Switch Slot Container", switchDeploymentPanel, new Vector2(16f, -100f), new Vector2(388f, 154f), new Color(0f, 0f, 0f, 0.16f));
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

        private RectTransform CreateSceneAuthoredSwitchSlotContainer()
        {
            if (switchDeploymentPanel == null)
            {
                return null;
            }

            Vector2 position = new Vector2(0f, -100f);
            Vector2 size = new Vector2(260f, 150f);
            if (selectSlotContainer != null)
            {
                position = selectSlotContainer.anchoredPosition;
                size = selectSlotContainer.sizeDelta;
            }

            RectTransform container = CreateRuntimePanel(
                "Switch Slot Container",
                switchDeploymentPanel,
                position,
                size,
                new Color(0f, 0f, 0f, 0.16f));
            return container;
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
