using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Windy.Srpg.Game.Buffs;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Localization;
using Windy.Srpg.Game.Passives;
using Windy.Srpg.Game.Skills;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Game.Abilities;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RuntimeBuff = Windy.Srpg.Game.Buffs.Buff;
using Windy.Srpg.Runtime.Grid;

namespace Windy.Srpg.Game.UI
{
    public class UnitInspectPanelUI : MonoBehaviour
    {
        private static readonly Color InspectFocusColor = new Color(0.63f, 0.84f, 1f, 1f);

        public static event Action<Unit> SelectionTargetChanged;
        public static event Action<Unit> InspectTargetChanged;
        private static UnitInspectPanelUI activeInstance;
        public static bool HasOpenInspect => activeInstance != null && activeInstance.inspectedUnit != null;
        public static Unit CurrentInspectedUnit => activeInstance != null ? activeInstance.inspectedUnit : null;

        [Header("References")]
        [SerializeField] private CellGrid cellGrid;
        [SerializeField] private GameObject root;

        [Header("Summary")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text experienceText;
        [SerializeField] private TMP_Text hitPointsText;
        [SerializeField] private TMP_Text manaPointsText;
        [SerializeField] private TMP_Text attackText;
        [SerializeField] private TMP_Text equipText;

        [Header("Stats")]
        [SerializeField] private TMP_Text strengthText;
        [SerializeField] private TMP_Text magicText;
        [SerializeField] private TMP_Text defenseText;
        [SerializeField] private TMP_Text resistanceText;
        [SerializeField] private TMP_Text speedText;
        [SerializeField] private TMP_Text luckText;
        [SerializeField] private TMP_Text movementText;
        [SerializeField] private TMP_Text rangeText;
        [SerializeField] private Button hideButton;

        [Header("Detail Panel")]
        [SerializeField] private GameObject detailPanelRoot;
        [SerializeField] private TMP_Text detailTitleText;
        [SerializeField] private TMP_Text detailBodyText;

        [Header("Stat Detail Text")]
        [SerializeField, TextArea] private string nameDescription = string.Empty;
        [SerializeField, TextArea] private string levelDescription = string.Empty;
        [SerializeField, TextArea] private string experienceDescription = string.Empty;
        [SerializeField, TextArea] private string hitPointsDescription = string.Empty;
        [SerializeField, TextArea] private string manaPointsDescription = string.Empty;
        [SerializeField, TextArea] private string attackDescription = string.Empty;
        [SerializeField, TextArea] private string equipDescription = string.Empty;
        [SerializeField, TextArea] private string strengthDescription = string.Empty;
        [SerializeField, TextArea] private string magicDescription = string.Empty;
        [SerializeField, TextArea] private string defenseDescription = string.Empty;
        [SerializeField, TextArea] private string resistanceDescription = string.Empty;
        [SerializeField, TextArea] private string speedDescription = string.Empty;
        [SerializeField, TextArea] private string luckDescription = string.Empty;
        [SerializeField, TextArea] private string movementDescription = string.Empty;
        [SerializeField, TextArea] private string rangeDescription = string.Empty;

        [Header("Lists")]
        [SerializeField] private UnitInspectEntryListUI inventoryList;
        [SerializeField] private UnitInspectEntryListUI skillsList;
        [SerializeField] private UnitInspectEntryListUI passivesList;
        [SerializeField] private UnitInspectEntryListUI buffsList;

        [Header("Buff Panel")]
        [SerializeField] private GameObject buffsPanelRoot;
        [SerializeField] private Vector2 buffsPanelOffset = new Vector2(0f, -24f);

        [Header("Behavior")]
        [SerializeField] private bool emptyCellClickClearsSelection = true;

        private readonly HashSet<Unit> subscribedUnits = new HashSet<Unit>();
        private readonly Dictionary<TMP_Text, Color> clickableFieldBaseColors = new Dictionary<TMP_Text, Color>();
        private Unit selectedUnit;
        private Unit inspectedUnit;
        private RectTransform rootRectTransform;
        private RectTransform buffsPanelRectTransform;
        private RectTransform detailPanelRectTransform;
        private bool actionMenuVisible;
        private bool attackPreviewVisible;
        private bool areaConfirmVisible;
        private bool lastSelectedUnitHadPendingMove;
        private string selectedDetailId;
        private TMP_Text hoveredClickableField;

        private void OnValidate()
        {
            AutoAssignListsFromChildren();
        }

        private void OnEnable()
        {
            activeInstance = this;
            AutoAssignListsFromChildren();

            if (cellGrid == null)
            {
                cellGrid = FindAnyObjectByType<CellGrid>();
            }

            if (root != null)
            {
                rootRectTransform = root.GetComponent<RectTransform>();
            }

            if (buffsPanelRoot != null)
            {
                buffsPanelRectTransform = buffsPanelRoot.GetComponent<RectTransform>();
                buffsPanelRoot.SetActive(false);
            }

            if (detailPanelRoot != null)
            {
                detailPanelRectTransform = detailPanelRoot.GetComponent<RectTransform>();
            }

            if (hideButton != null)
            {
                hideButton.onClick.RemoveListener(OnHideButtonClicked);
                hideButton.onClick.AddListener(OnHideButtonClicked);
            }

            RegisterClickableFields();

            if (cellGrid == null)
            {
                Hide();
                return;
            }

            ActionMenuUI.VisibilityChanged += OnActionMenuVisibilityChanged;
            AttackPreviewUI.VisibilityChanged += OnAttackPreviewVisibilityChanged;
            AreaConfirmUI.VisibilityChanged += OnAreaConfirmVisibilityChanged;
            cellGrid.LevelInitialized += OnLevelLoadingDone;
            cellGrid.UnitAdded += OnUnitAdded;
            cellGrid.EmptyCellHighlighted += OnEmptyCellHighlighted;
            cellGrid.BattleTurnEnded += OnTurnEnded;
            SubscribeToExistingGridObjects();
            Refresh();
        }

        private void OnDisable()
        {
            if (activeInstance == this)
            {
                activeInstance = null;
            }

            if (cellGrid != null)
            {
                cellGrid.LevelInitialized -= OnLevelLoadingDone;
                cellGrid.UnitAdded -= OnUnitAdded;
                cellGrid.EmptyCellHighlighted -= OnEmptyCellHighlighted;
                cellGrid.BattleTurnEnded -= OnTurnEnded;
            }

            ActionMenuUI.VisibilityChanged -= OnActionMenuVisibilityChanged;
            AttackPreviewUI.VisibilityChanged -= OnAttackPreviewVisibilityChanged;
            AreaConfirmUI.VisibilityChanged -= OnAreaConfirmVisibilityChanged;
            UnsubscribeAllUnits();
            SelectionTargetChanged?.Invoke(null);
            InspectTargetChanged?.Invoke(null);
        }

        public static void RequestGameplayHide()
        {
            activeInstance?.ApplyAutomaticHideAndRefresh();
        }

        private void Update()
        {
            UpdatePendingMoveAutoHideState();
            RefreshClickableFieldHighlights();
        }

        private void OnLevelLoadingDone(object sender, EventArgs e)
        {
            SubscribeToExistingGridObjects();
            selectedUnit = null;
            inspectedUnit = null;
            lastSelectedUnitHadPendingMove = false;
            ClearDetailSelection();
            SelectionTargetChanged?.Invoke(null);
            InspectTargetChanged?.Invoke(null);
            Hide();
        }

        private void OnTurnEnded(object sender, EventArgs e)
        {
            Refresh();
        }

        private void OnActionMenuVisibilityChanged(bool isVisible)
        {
            actionMenuVisible = isVisible;
            if (isVisible)
            {
                ApplyAutomaticHide();
            }

            Refresh();
        }

        private void OnAttackPreviewVisibilityChanged(bool isVisible)
        {
            attackPreviewVisible = isVisible;
            if (isVisible)
            {
                ApplyAutomaticHide();
            }

            Refresh();
        }

        private void OnAreaConfirmVisibilityChanged(bool isVisible)
        {
            areaConfirmVisible = isVisible;
            if (isVisible)
            {
                ApplyAutomaticHide();
            }

            Refresh();
        }

        private void OnHideButtonClicked()
        {
            if (inspectedUnit == null)
            {
                return;
            }

            CloseInspect(clearRememberedTarget: true);
            Refresh();
        }

        private void OnUnitAdded(object sender, UnitAddedEventArgs e)
        {
            if (e?.Unit != null)
            {
                SubscribeUnit(e.Unit);
            }
        }

        private void SubscribeToExistingGridObjects()
        {
            if (cellGrid != null)
            {
                foreach (var unit in cellGrid.GetAllUnits())
                {
                    if (unit != null)
                    {
                        SubscribeUnit(unit);
                    }
                }
            }

        }

        private void SubscribeUnit(Unit unit)
        {
            if (!subscribedUnits.Add(unit))
            {
                return;
            }

            unit.UnitClicked += OnUnitClicked;
            unit.GameplaySelected += OnUnitSelected;
            unit.GameplayDeselected += OnUnitDeselected;
            unit.UnitHealthChanged += OnUnitHealthChanged;
            unit.UnitStatsChanged += OnUnitStatsChanged;
            unit.UnitProgressionChanged += OnUnitProgressionChanged;
            unit.UnitBuffsChanged += OnUnitBuffsChanged;
            unit.DestroyedInCombat += OnUnitDestroyed;
        }

        private void UnsubscribeAllUnits()
        {
            foreach (var unit in subscribedUnits)
            {
                if (unit == null)
                {
                    continue;
                }

                unit.UnitClicked -= OnUnitClicked;
                unit.GameplaySelected -= OnUnitSelected;
                unit.GameplayDeselected -= OnUnitDeselected;
                unit.UnitHealthChanged -= OnUnitHealthChanged;
                unit.UnitStatsChanged -= OnUnitStatsChanged;
                unit.UnitProgressionChanged -= OnUnitProgressionChanged;
                unit.UnitBuffsChanged -= OnUnitBuffsChanged;
                unit.DestroyedInCombat -= OnUnitDestroyed;
            }

            subscribedUnits.Clear();
        }

        private void OnUnitClicked(object sender, EventArgs e)
        {
            if (MoveAbility.SuppressInspectClicks)
            {
                return;
            }

            if (sender is not Unit clickedUnit)
            {
                return;
            }

            // When a unit is already gameplay-selected, clicking it again is used for
            // same-tile pending move / action flow. Do not let that click reopen inspect.
            if (IsGameplaySelectedUnit(clickedUnit))
            {
                return;
            }

            if (!CanAdoptClickedUnit(clickedUnit))
            {
                return;
            }

            SetSelectedUnit(clickedUnit, true);
        }

        private void OnUnitSelected(object sender, EventArgs e)
        {
            if (sender is not Unit unit)
            {
                return;
            }

            bool changed = selectedUnit != unit;
            SetSelectedUnit(unit, changed);
            UpdatePendingMoveAutoHideState();
            Refresh();
        }

        private void OnUnitDeselected(object sender, EventArgs e)
        {
            if (sender is not Unit unit || selectedUnit != unit)
            {
                return;
            }

            SetSelectedUnit(null, true);
        }

        private void OnEmptyCellHighlighted(object sender, EventArgs e)
        {
            if (!emptyCellClickClearsSelection || selectedUnit == null || IsGameplaySelectedUnit(selectedUnit))
            {
                return;
            }

            SetSelectedUnit(null, true);
        }

        private void OnUnitHealthChanged(object sender, UnitHealthChangedEventArgs e)
        {
            if (inspectedUnit == sender as Unit)
            {
                UpdatePendingMoveAutoHideState();
                Refresh();
            }
        }

        private void OnUnitStatsChanged(object sender, EventArgs e)
        {
            if (inspectedUnit == sender as Unit)
            {
                UpdatePendingMoveAutoHideState();
                Refresh();
            }
        }

        private void OnUnitProgressionChanged(object sender, EventArgs e)
        {
            if (inspectedUnit == sender as Unit)
            {
                Refresh();
            }
        }

        private void OnUnitBuffsChanged(object sender, EventArgs e)
        {
            if (inspectedUnit == sender as Unit)
            {
                Refresh();
            }
        }

        private void OnUnitDestroyed(object sender, UnitDestroyedEventArgs e)
        {
            if (selectedUnit == e?.Defender)
            {
                SetSelectedUnit(null, true);
            }
        }

        private void Refresh()
        {
            UpdatePendingMoveAutoHideState();

            Unit displayUnit = inspectedUnit;
            if (displayUnit == null)
            {
                Hide();
                return;
            }

            if (root != null)
            {
                root.SetActive(true);
            }

            IReadOnlyList<UnitInspectEntryListUI.EntryData> activeBuffEntries = BuildBuffEntries(displayUnit).ToList();

            if (nameText != null)
            {
                nameText.text = displayUnit.unitName;
            }

            if (levelText != null)
            {
                levelText.text = GameTextCatalog.Format("ui.common.level_short", "Lv: {0}", displayUnit.Level);
            }

            if (experienceText != null)
            {
                experienceText.text = BuildExperienceText(displayUnit);
            }

            if (hitPointsText != null)
            {
                hitPointsText.text = GameTextCatalog.Format("ui.common.hp_pair", "HP: {0}/{1}", displayUnit.HitPoints, displayUnit.ComputedTotalHitPoints);
            }

            if (manaPointsText != null)
            {
                manaPointsText.text = GameTextCatalog.Format("ui.common.mp_pair", "MP: {0}/{1}", displayUnit.CurrentManaPoints, displayUnit.ComputedTotalManaPoints);
            }

            if (attackText != null)
            {
                attackText.text = GameTextCatalog.Format("ui.common.atk_short", "Atk: {0}", displayUnit.Attack);
            }

            if (equipText != null)
            {
                equipText.text = GameTextCatalog.Format("ui.common.equip_short", "[E] {0}", displayUnit.EquippedWeapon?.Name ?? GameTextCatalog.Get("ui.common.none", "None"));
            }

            if (strengthText != null)
            {
                strengthText.text = GameTextCatalog.Format("ui.common.str_short", "Str: {0}", displayUnit.Strength);
            }

            if (magicText != null)
            {
                magicText.text = GameTextCatalog.Format("ui.common.mag_short", "Mag: {0}", displayUnit.Magic);
            }

            if (defenseText != null)
            {
                defenseText.text = GameTextCatalog.Format("ui.common.def_short", "Def: {0}", displayUnit.Defense);
            }

            if (resistanceText != null)
            {
                resistanceText.text = GameTextCatalog.Format("ui.common.res_short", "Res: {0}", displayUnit.Resistance);
            }

            if (speedText != null)
            {
                speedText.text = GameTextCatalog.Format("ui.common.spd_short", "Spd: {0}", displayUnit.Speed);
            }

            if (luckText != null)
            {
                luckText.text = GameTextCatalog.Format("ui.common.lck_short", "Lck: {0}", displayUnit.Luck);
            }

            if (movementText != null)
            {
                movementText.text = GameTextCatalog.Format("ui.common.mov_short", "Mov: {0}", FormatFloat(displayUnit.ComputedTotalMovementPoints));
            }

            if (rangeText != null)
            {
                rangeText.text = GameTextCatalog.Format("ui.common.rng_short", "Rng: {0}", FormatRange(displayUnit.MinAttackRange, displayUnit.MaxAttackRange));
            }

            inventoryList?.SetEntries(BuildInventoryEntries(displayUnit), OnEntryClicked);
            skillsList?.SetEntries(BuildSkillEntries(displayUnit), OnEntryClicked);
            passivesList?.SetEntries(BuildPassiveEntries(displayUnit), OnEntryClicked);
            buffsList?.SetEntries(activeBuffEntries, OnEntryClicked);
            RefreshBuffPanel(activeBuffEntries.Count > 0);
            Canvas.ForceUpdateCanvases();
            inventoryList?.RefreshLayoutNow();
            skillsList?.RefreshLayoutNow();
            passivesList?.RefreshLayoutNow();
            buffsList?.RefreshLayoutNow();
            RefreshDetailPanelVisibility();
        }

        private void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }

            inventoryList?.ClearEntries();
            skillsList?.ClearEntries();
            passivesList?.ClearEntries();
            buffsList?.ClearEntries();
            if (buffsPanelRoot != null)
            {
                buffsPanelRoot.SetActive(false);
            }

            if (detailPanelRoot != null)
            {
                detailPanelRoot.SetActive(false);
            }
        }

        private void UpdatePendingMoveAutoHideState()
        {
            bool selectedUnitHasPendingMove = selectedUnit != null && selectedUnit.HasPendingMove;
            if (selectedUnitHasPendingMove && !lastSelectedUnitHadPendingMove)
            {
                ApplyAutomaticHide();
            }

            lastSelectedUnitHadPendingMove = selectedUnitHasPendingMove;
        }

        private void SetSelectedUnit(Unit unit, bool resetManualHideState)
        {
            bool changed = selectedUnit != unit;
            selectedUnit = unit;

            lastSelectedUnitHadPendingMove = unit != null && unit.HasPendingMove;

            if (changed)
            {
                SelectionTargetChanged?.Invoke(selectedUnit);
                if (inspectedUnit != null && inspectedUnit != selectedUnit)
                {
                    CloseInspect(clearRememberedTarget: true);
                }
            }

            Refresh();
        }

        private void OpenInspect(Unit unit)
        {
            if (unit == null)
            {
                CloseInspect(clearRememberedTarget: true);
                return;
            }

            bool changed = inspectedUnit != unit;
            inspectedUnit = unit;
            if (changed)
            {
                ClearDetailSelection();
                InspectTargetChanged?.Invoke(inspectedUnit);
            }
        }

        private bool TryClearInspect()
        {
            if (inspectedUnit == null)
            {
                return false;
            }

            CloseInspect(clearRememberedTarget: true);
            return true;
        }

        private void CloseInspect(bool clearRememberedTarget)
        {
            bool hadInspect = inspectedUnit != null;
            if (clearRememberedTarget)
            {
                inspectedUnit = null;
            }

            ClearDetailSelection();
            if (hadInspect)
            {
                InspectTargetChanged?.Invoke(null);
            }
        }

        private void ClearSelectionAndInspect()
        {
            bool hadSelection = selectedUnit != null;
            selectedUnit = null;
            lastSelectedUnitHadPendingMove = false;
            if (hadSelection)
            {
                SelectionTargetChanged?.Invoke(null);
            }

            CloseInspect(clearRememberedTarget: true);
            Refresh();
        }

        public static bool TryOpenInspectForUnit(Unit unit)
        {
            if (activeInstance == null || unit == null)
            {
                return false;
            }

            if (activeInstance.actionMenuVisible || activeInstance.attackPreviewVisible || activeInstance.areaConfirmVisible)
            {
                return false;
            }

            activeInstance.OpenInspect(unit);
            activeInstance.Refresh();
            return true;
        }

        public static bool TryClearInspectFromInput()
        {
            if (activeInstance == null)
            {
                return false;
            }

            bool changed = activeInstance.TryClearInspect();
            if (changed)
            {
                activeInstance.Refresh();
            }

            return changed;
        }

        public static bool IsPointerInsideActiveInspectUi()
        {
            return activeInstance != null && activeInstance.IsPointerInsideInspectUi();
        }

        private bool IsPointerInsideInspectUi()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            PointerEventData pointerEventData = new PointerEventData(eventSystem)
            {
                position = Input.mousePosition
            };

            List<RaycastResult> raycastResults = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerEventData, raycastResults);
            foreach (RaycastResult result in raycastResults)
            {
                GameObject hitObject = result.gameObject;
                if (hitObject == null)
                {
                    continue;
                }

                Transform transform = hitObject.transform;
                if (IsWithinInspectHierarchy(transform))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsWithinInspectHierarchy(Transform transform)
        {
            if (transform == null)
            {
                return false;
            }

            if (rootRectTransform != null && transform.IsChildOf(rootRectTransform))
            {
                return true;
            }

            if (buffsPanelRectTransform != null && transform.IsChildOf(buffsPanelRectTransform))
            {
                return true;
            }

            if (detailPanelRectTransform != null && transform.IsChildOf(detailPanelRectTransform))
            {
                return true;
            }

            return false;
        }

        private bool CanAdoptClickedUnit(Unit clickedUnit)
        {
            Unit gameplaySelectedFriendly = GetGameplaySelectedFriendlyUnit();
            if (gameplaySelectedFriendly == null || gameplaySelectedFriendly == clickedUnit)
            {
                return true;
            }

            return cellGrid?.CurrentState is Windy.Srpg.Game.Grid.States.UnitSelectedState
                && clickedUnit != null
                && !IsCurrentFriendlyUnit(clickedUnit);
        }

        private Unit GetGameplaySelectedFriendlyUnit()
        {
            return subscribedUnits.FirstOrDefault(unit => IsGameplaySelectedUnit(unit) && IsCurrentFriendlyUnit(unit));
        }

        private bool IsGameplaySelectedUnit(Unit unit)
        {
            return unit != null && unit.IsSelectedForTurn;
        }

        private bool IsCurrentFriendlyUnit(Unit unit)
        {
            return unit != null && cellGrid != null && unit.PlayerNumber == cellGrid.CurrentPlayerNumber;
        }

        private void ApplyAutomaticHide()
        {
            if (inspectedUnit != null)
            {
                CloseInspect(clearRememberedTarget: true);
            }
        }

        private void ApplyAutomaticHideAndRefresh()
        {
            ApplyAutomaticHide();
            Refresh();
        }

        private static string FormatRange(int minRange, int maxRange)
        {
            return minRange == maxRange ? minRange.ToString() : $"{minRange}-{maxRange}";
        }

        private static string FormatFloat(float value)
        {
            float rounded = Mathf.Round(value);
            if (Mathf.Approximately(value, rounded))
            {
                return ((int)rounded).ToString();
            }

            return value.ToString("0.##");
        }

        private IEnumerable<UnitInspectEntryListUI.EntryData> BuildInventoryEntries(Unit unit)
        {
            if (unit?.Inventory?.Entries == null)
            {
                yield break;
            }

            IReadOnlyList<Item> inventoryEntries = unit.Inventory.Entries;
            Item equippedWeaponEntry = unit.Inventory.EquippedWeaponEntry;
            Item equippedAccessoryEntry = unit.Inventory.EquippedAccessoryEntry;

            IEnumerable<Item> orderedEntries = inventoryEntries
                .Where(entry => entry != null)
                .OrderBy(entry => entry == equippedWeaponEntry ? 0 : entry == equippedAccessoryEntry ? 1 : 2);

            foreach (Item entry in orderedEntries)
            {
                if (entry?.Data == null)
                {
                    continue;
                }

                bool isEquipped = unit.Inventory.EquippedWeaponEntry == entry || unit.Inventory.EquippedAccessoryEntry == entry;
                string prefix = isEquipped ? "[E] " : string.Empty;
                string suffix = entry.Consumable != null && !entry.HasInfiniteCharges
                    ? $" x{entry.RemainingCharges}"
                    : string.Empty;

                yield return new UnitInspectEntryListUI.EntryData(
                    $"item:{entry.Data.Id}:{prefix}",
                    $"{prefix}{entry.Data.Name}{suffix}",
                    entry.Data.Name,
                    BuildItemDetailBody(entry));
            }
        }

        private IEnumerable<UnitInspectEntryListUI.EntryData> BuildSkillEntries(Unit unit)
        {
            if (unit?.SkillList?.Entries == null)
            {
                yield break;
            }

            foreach (var entry in unit.SkillList.Entries)
            {
                SkillData skillData = entry?.Data;
                if (skillData == null || string.IsNullOrWhiteSpace(skillData.Name))
                {
                    continue;
                }

                yield return new UnitInspectEntryListUI.EntryData(
                    $"skill:{skillData.Id}",
                    skillData.Name,
                    skillData.Name,
                    BuildSkillDetailBody(skillData));
            }
        }

        private IEnumerable<UnitInspectEntryListUI.EntryData> BuildPassiveEntries(Unit unit)
        {
            if (unit?.PassiveList?.Entries == null)
            {
                yield break;
            }

            foreach (Passive passive in unit.PassiveList.Entries)
            {
                PassiveData passiveData = passive?.Data;
                if (passiveData == null)
                {
                    continue;
                }

                bool isEquippedPassive = unit.PassiveList.EquippedEntries.Contains(passive);
                string displayName = isEquippedPassive
                    ? $"{passiveData.Name} ({passiveData.Cost})"
                    : passiveData.Name;

                yield return new UnitInspectEntryListUI.EntryData(
                    $"passive:{passiveData.Id}",
                    displayName,
                    passiveData.Name,
                    passiveData.Description);
            }
        }

        private IEnumerable<UnitInspectEntryListUI.EntryData> BuildBuffEntries(Unit unit)
        {
            if (unit?.BuffList?.Entries == null)
            {
                yield break;
            }

            foreach (RuntimeBuff buff in unit.BuffList.Entries)
            {
                if (buff?.Data == null)
                {
                    continue;
                }

                string displayName = BuildBuffDisplayName(buff);
                yield return new UnitInspectEntryListUI.EntryData(
                    $"buff:{buff.BuffId}:{displayName}",
                    displayName,
                    buff.Data.Name,
                    buff.Data.Description);
            }
        }

        private void RefreshBuffPanel(bool hasActiveEntries)
        {
            if (buffsPanelRoot == null)
            {
                return;
            }

            buffsPanelRoot.SetActive(hasActiveEntries);
            if (!hasActiveEntries)
            {
                return;
            }

            PositionBuffPanel();
        }

        private void PositionBuffPanel()
        {
            if (rootRectTransform == null || buffsPanelRectTransform == null)
            {
                return;
            }

            if (buffsPanelRectTransform.parent != rootRectTransform.parent)
            {
                return;
            }

            buffsPanelRectTransform.anchoredPosition = rootRectTransform.anchoredPosition + buffsPanelOffset;
        }

        private void AutoAssignListsFromChildren()
        {
            if (inventoryList != null && skillsList != null && passivesList != null && buffsList != null)
            {
                return;
            }

            UnitInspectEntryListUI[] lists = GetComponentsInChildren<UnitInspectEntryListUI>(true);
            foreach (UnitInspectEntryListUI list in lists)
            {
                if (list == null)
                {
                    continue;
                }

                string listName = list.gameObject.name.ToLowerInvariant();
                if (inventoryList == null && listName.Contains("inventory"))
                {
                    inventoryList = list;
                }
                else if (skillsList == null && (listName.Contains("skill") || listName.Contains("skills")))
                {
                    skillsList = list;
                }
                else if (passivesList == null && listName.Contains("passive"))
                {
                    passivesList = list;
                }
                else if (buffsList == null && (listName.Contains("buff") || listName.Contains("debuff")))
                {
                    buffsList = list;
                }
            }
        }

        private void RegisterClickableFields()
        {
            RegisterDetailClick(nameText, "summary:name", GameTextCatalog.Get("ui.inspect.detail.name", "Name"), () => ResolveDetailDescription(nameDescription, "ui.inspect.description.name", "The unit's displayed name."));
            RegisterDetailClick(levelText, "summary:level", GameTextCatalog.Get("ui.inspect.detail.level", "Level"), () => ResolveDetailDescription(levelDescription, "ui.inspect.description.level", "A unit's overall power level. Levels increase when enough EXP is gained."));
            RegisterDetailClick(experienceText, "summary:exp", GameTextCatalog.Get("ui.inspect.detail.exp", "EXP"), () => ResolveDetailDescription(experienceDescription, "ui.inspect.description.exp", "Progress toward the next level. Enemy units always display 0 EXP."));
            RegisterDetailClick(hitPointsText, "summary:hp", GameTextCatalog.Get("ui.inspect.detail.hp", "HP"), () => ResolveDetailDescription(hitPointsDescription, "ui.inspect.description.hp", "Current HP out of max HP. Units are defeated when HP reaches 0."));
            RegisterDetailClick(manaPointsText, "summary:mp", GameTextCatalog.Get("ui.inspect.detail.mp", "MP"), () => ResolveDetailDescription(manaPointsDescription, "ui.inspect.description.mp", "Current MP out of max MP. Skills require enough MP to cast."));
            RegisterDetailClick(attackText, "summary:atk", GameTextCatalog.Get("ui.inspect.detail.atk", "Atk"), () => ResolveDetailDescription(attackDescription, "ui.inspect.description.atk", "Base attack power before target mitigation. Specific combat previews show the exact resulting damage."));
            RegisterDetailClick(
                equipText,
                "summary:equip",
                () => inspectedUnit?.EquippedWeapon?.Name ?? GameTextCatalog.Get("ui.common.none", "None"),
                BuildEquippedItemDetailBody);
            RegisterDetailClick(strengthText, "stat:str", GameTextCatalog.Get("ui.inspect.detail.strength", "Strength"), () => ResolveDetailDescription(strengthDescription, "ui.inspect.description.strength", "Strength supports physical damage output."));
            RegisterDetailClick(magicText, "stat:mag", GameTextCatalog.Get("ui.inspect.detail.magic", "Magic"), () => ResolveDetailDescription(magicDescription, "ui.inspect.description.magic", "Magic supports magical damage and contributes to max MP."));
            RegisterDetailClick(defenseText, "stat:def", GameTextCatalog.Get("ui.inspect.detail.defense", "Defense"), () => ResolveDetailDescription(defenseDescription, "ui.inspect.description.defense", "Defense reduces incoming physical damage."));
            RegisterDetailClick(resistanceText, "stat:res", GameTextCatalog.Get("ui.inspect.detail.resistance", "Resistance"), () => ResolveDetailDescription(resistanceDescription, "ui.inspect.description.resistance", "Resistance reduces incoming magical damage, improves healing, and contributes to max MP."));
            RegisterDetailClick(speedText, "stat:spd", GameTextCatalog.Get("ui.inspect.detail.speed", "Speed"), () => ResolveDetailDescription(speedDescription, "ui.inspect.description.speed", "Speed affects combat tempo and follow-up potential."));
            RegisterDetailClick(luckText, "stat:lck", GameTextCatalog.Get("ui.inspect.detail.luck", "Luck"), () => ResolveDetailDescription(luckDescription, "ui.inspect.description.luck", "Luck supports accuracy, consistency, and resistance to enemy crits depending on the formula in use."));
            RegisterDetailClick(movementText, "stat:mov", GameTextCatalog.Get("ui.inspect.detail.movement", "Movement"), () => ResolveDetailDescription(movementDescription, "ui.inspect.description.movement", "Movement determines how many tiles the unit can travel in a turn."));
            RegisterDetailClick(rangeText, "stat:rng", GameTextCatalog.Get("ui.inspect.detail.range", "Range"), () => ResolveDetailDescription(rangeDescription, "ui.inspect.description.range", "The unit's currently available basic attack range with equipped gear."));
        }

        private void RegisterDetailClick(TMP_Text text, string detailId, string title, Func<string> bodyProvider)
        {
            RegisterDetailClick(text, detailId, () => title, bodyProvider);
        }

        private void RegisterDetailClick(TMP_Text text, string detailId, Func<string> titleProvider, Func<string> bodyProvider)
        {
            if (text == null)
            {
                return;
            }

            clickableFieldBaseColors[text] = text.color;

            Button button = text.GetComponent<Button>();
            if (button == null)
            {
                button = text.gameObject.AddComponent<Button>();
            }

            button.transition = Selectable.Transition.None;
            button.targetGraphic = text;
            ColorBlock colors = button.colors;
            colors.normalColor = text.color;
            colors.highlightedColor = InspectFocusColor;
            colors.selectedColor = InspectFocusColor;
            colors.pressedColor = InspectFocusColor;
            button.colors = colors;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ToggleDetail(detailId, titleProvider?.Invoke() ?? string.Empty, bodyProvider?.Invoke() ?? string.Empty));

            RegisterClickableFieldHover(text);
        }

        private void RegisterClickableFieldHover(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            EventTrigger trigger = text.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = text.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers ??= new List<EventTrigger.Entry>();
            trigger.triggers.RemoveAll(entry =>
                entry != null &&
                (entry.eventID == EventTriggerType.PointerEnter || entry.eventID == EventTriggerType.PointerExit));

            EventTrigger.Entry pointerEnter = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };
            pointerEnter.callback.AddListener(_ =>
            {
                hoveredClickableField = text;
                RefreshClickableFieldHighlights();
            });
            trigger.triggers.Add(pointerEnter);

            EventTrigger.Entry pointerExit = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerExit
            };
            pointerExit.callback.AddListener(_ =>
            {
                if (hoveredClickableField == text)
                {
                    hoveredClickableField = null;
                }

                RefreshClickableFieldHighlights();
            });
            trigger.triggers.Add(pointerExit);
        }

        private void RefreshClickableFieldHighlights()
        {
            if (clickableFieldBaseColors.Count == 0)
            {
                return;
            }

            GameObject selectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            foreach (var pair in clickableFieldBaseColors)
            {
                TMP_Text text = pair.Key;
                if (text == null)
                {
                    continue;
                }

                bool isHighlighted = false;
                if (selectedObject != null)
                {
                    isHighlighted = selectedObject == text.gameObject || selectedObject.transform.IsChildOf(text.transform);
                }

                if (!isHighlighted && hoveredClickableField == text)
                {
                    isHighlighted = true;
                }

                text.color = isHighlighted ? InspectFocusColor : pair.Value;
                text.fontStyle = isHighlighted ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        private void OnEntryClicked(UnitInspectEntryListUI.EntryData entry)
        {
            if (string.IsNullOrWhiteSpace(entry.DisplayName))
            {
                return;
            }

            ToggleDetail(entry.Id, entry.DetailTitle, entry.DetailBody);
        }

        private void ToggleDetail(string detailId, string title, string body)
        {
            if (inspectedUnit == null || string.IsNullOrWhiteSpace(detailId))
            {
                return;
            }

            if (selectedDetailId == detailId && detailPanelRoot != null && detailPanelRoot.activeSelf)
            {
                ClearDetailSelection();
                RefreshDetailPanelVisibility();
                return;
            }

            selectedDetailId = detailId;

            if (detailTitleText != null)
            {
                detailTitleText.text = title ?? string.Empty;
            }

            if (detailBodyText != null)
            {
                detailBodyText.text = body ?? string.Empty;
            }

            RefreshDetailPanelVisibility();
        }

        private void RefreshDetailPanelVisibility()
        {
            if (detailPanelRoot == null)
            {
                return;
            }

            bool shouldShow = inspectedUnit != null
                && !string.IsNullOrWhiteSpace(selectedDetailId);

            detailPanelRoot.SetActive(shouldShow);
        }

        private bool TryGetUnitUnderPointer(out Unit unit)
        {
            unit = null;
            if (UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return false;
            }

            Camera worldCamera = Camera.main;
            if (worldCamera == null)
            {
                worldCamera = FindAnyObjectByType<Camera>();
            }

            if (worldCamera == null)
            {
                return false;
            }

            Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, float.MaxValue);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                Unit hitUnit = hit.collider.GetComponentInParent<Unit>();
                if (hitUnit != null)
                {
                    unit = hitUnit;
                    return true;
                }

                Cell hitCell = hit.collider.GetComponentInParent<Cell>();
                if (hitCell?.CurrentUnits == null)
                {
                    continue;
                }

                unit = hitCell.CurrentUnits
                    .FirstOrDefault(candidate => candidate != null);
                if (unit != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearDetailSelection()
        {
            selectedDetailId = null;

            if (detailTitleText != null)
            {
                detailTitleText.text = string.Empty;
            }

            if (detailBodyText != null)
            {
                detailBodyText.text = string.Empty;
            }
        }

        private static string BuildBuffDisplayName(RuntimeBuff buff)
        {
            if (buff?.Data == null)
            {
                return string.Empty;
            }

            if (buff.IsInfinite)
            {
                return buff.Data.Name;
            }

            return $"{buff.Data.Name} ({Mathf.Max(0, buff.RemainingDuration)})";
        }

        private string BuildEquippedItemDetailBody()
        {
            Item equippedItem = inspectedUnit?.Inventory?.EquippedWeaponEntry;
            if (equippedItem?.Data == null)
            {
                return GameTextCatalog.Get("ui.inspect.no_equipped_item", "No weapon or implement equipped.");
            }

            return BuildItemDetailBody(equippedItem);
        }

        private static string BuildItemDetailBody(Item item)
        {
            ItemData itemData = item?.Data;
            if (itemData == null)
            {
                return string.Empty;
            }

            var lines = new List<string>();

            if (itemData is WeaponData weaponData)
            {
                lines.Add(GameTextCatalog.Format("ui.inspect.skill_line.attack_with_range", "Mt: {0} | Hit: {1} | Crit: {2} | Range: {3}", weaponData.Might, weaponData.Accuracy, weaponData.Crit, FormatPreviewRange(weaponData.MinRange, weaponData.MaxRange)));
            }

            if (!string.IsNullOrWhiteSpace(itemData.Description))
            {
                lines.Add(itemData.Description);
            }

            return string.Join("\n", lines);
        }

        private static string BuildSkillDetailBody(SkillData skillData)
        {
            if (skillData == null)
            {
                return string.Empty;
            }

            var lines = new List<string>
            {
                GameTextCatalog.Format("ui.inspect.skill_category_mp", "{0} | {1} MP", GetSkillCategoryDisplayName(skillData.Category), skillData.MpCost)
            };

            string combatLine = BuildSkillCombatSummary(skillData);
            if (!string.IsNullOrWhiteSpace(combatLine))
            {
                lines.Add(combatLine);
            }

            if (!string.IsNullOrWhiteSpace(skillData.Description))
            {
                lines.Add(skillData.Description);
            }

            return string.Join("\n", lines);
        }

        private static string BuildSkillCombatSummary(SkillData skillData)
        {
            if (skillData == null)
            {
                return string.Empty;
            }

            if (skillData.AreaProfile.Enabled)
            {
                return GameTextCatalog.Format("ui.inspect.skill_line.area", "Mt: {0} | Range: {1} | Radius: {2}", skillData.AreaProfile.Might, FormatPreviewRange(skillData.AreaProfile.MinRange, skillData.AreaProfile.MaxRange), skillData.AreaProfile.Radius);
            }

            if (skillData.AttackProfile.Enabled)
            {
                if (skillData.Category != SkillCategory.CombatArt)
                {
                    return GameTextCatalog.Format("ui.inspect.skill_line.attack_with_range", "Mt: {0} | Hit: {1} | Crit: {2} | Range: {3}", skillData.AttackProfile.Might, skillData.AttackProfile.Accuracy, skillData.AttackProfile.Crit, FormatPreviewRange(skillData.AttackProfile.MinRange, skillData.AttackProfile.MaxRange));
                }

                return GameTextCatalog.Format("ui.inspect.skill_line.attack", "Mt: {0} | Hit: {1} | Crit: {2}", skillData.AttackProfile.Might, skillData.AttackProfile.Accuracy, skillData.AttackProfile.Crit);
            }

            return string.Empty;
        }

        private static string FormatPreviewRange(int minRange, int maxRange)
        {
            string maxRangeText = SkillRangeUtility.IsInfiniteRange(maxRange) ? GameTextCatalog.Get("ui.common.inf", "Inf") : maxRange.ToString();
            if (minRange == maxRange && !SkillRangeUtility.IsInfiniteRange(maxRange))
            {
                return minRange.ToString();
            }

            return $"{minRange}-{maxRangeText}";
        }

        private static string GetSkillCategoryDisplayName(SkillCategory category)
        {
            return category switch
            {
                SkillCategory.CombatArt => GameTextCatalog.Get("ui.inspect.skill_category.combat_art", "Combat Art"),
                SkillCategory.Spell => GameTextCatalog.Get("ui.inspect.skill_category.spell", "Spell"),
                SkillCategory.AreaSpell => GameTextCatalog.Get("ui.inspect.skill_category.area_spell", "Area Spell"),
                SkillCategory.Misc => GameTextCatalog.Get("ui.inspect.skill_category.misc", "Misc"),
                _ => category.ToString()
            };
        }

        private string BuildExperienceText(Unit unit)
        {
            if (unit == null)
            {
                return GameTextCatalog.Get("ui.common.exp_zero", "EXP: 0");
            }

            if (!IsCurrentFriendlyUnit(unit))
            {
                return GameTextCatalog.Get("ui.common.exp_zero", "EXP: 0");
            }

            if (unit.Level >= ExperienceCalculator.MaxLevel)
            {
                return GameTextCatalog.Get("ui.common.exp_zero", "EXP: 0");
            }

            return GameTextCatalog.Format("ui.common.exp_value", "EXP: {0}", unit.Experience);
        }

        private static string ResolveDetailDescription(string overrideText, string key, string fallback)
        {
            return GameTextCatalog.ResolveOverride(overrideText, key, fallback);
        }
    }
}

