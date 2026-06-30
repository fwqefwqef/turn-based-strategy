using System;
using System.Collections.Generic;
using TMPro;
using Windy.Srpg.Game.Localization;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;
using UnityEngine;
using UnityEngine.UI;

namespace Windy.Srpg.Game.UI
{
    public class UnitHoverStripUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CellGrid cellGrid;
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform positionTarget;
        [SerializeField] private Canvas canvas;
        [SerializeField] private Camera worldCamera;

        [Header("Labels")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text hitPointsText;
        [SerializeField] private TMP_Text manaPointsText;
        [SerializeField] private TMP_Text attackText;
        [SerializeField] private TMP_Text equipText;

        [Header("Positioning")]
        [SerializeField] private Vector2 screenOffset = new Vector2(170f, -180f);
        [SerializeField] private Vector2 screenPadding = new Vector2(20f, 20f);

        private readonly HashSet<Unit> subscribedUnits = new HashSet<Unit>();
        private Unit hoveredUnit;
        private bool suppressWhileInspectOpen;
        private bool suppressWhileActionMenuOpen;
        private RectTransform rootRectTransform;
        private RectTransform canvasRectTransform;

        private void OnEnable()
        {
            if (cellGrid == null)
            {
                cellGrid = FindAnyObjectByType<CellGrid>();
            }

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            if (root != null)
            {
                rootRectTransform = root.GetComponent<RectTransform>();
                DisableRaycastTargets(root);
            }

            if (positionTarget == null)
            {
                RectTransform ownRectTransform = transform as RectTransform;
                if (ownRectTransform != null && root != null && root.transform.IsChildOf(transform))
                {
                    positionTarget = ownRectTransform;
                }
                else
                {
                    positionTarget = rootRectTransform;
                }
            }

            if (canvas != null)
            {
                canvasRectTransform = canvas.GetComponent<RectTransform>();
            }

            if (cellGrid == null)
            {
                Hide();
                return;
            }

            cellGrid.LevelInitialized += OnLevelLoadingDone;
            cellGrid.UnitAdded += OnUnitAdded;
            cellGrid.EmptyCellHighlighted += OnEmptyCellHighlighted;
            UnitInspectPanelUI.InspectTargetChanged += OnInspectTargetChanged;
            ActionMenuUI.VisibilityChanged += OnActionMenuVisibilityChanged;
            SubscribeToExistingGridObjects();
            Refresh();
        }

        private void OnDisable()
        {
            if (cellGrid != null)
            {
                cellGrid.LevelInitialized -= OnLevelLoadingDone;
                cellGrid.UnitAdded -= OnUnitAdded;
                cellGrid.EmptyCellHighlighted -= OnEmptyCellHighlighted;
            }

            UnitInspectPanelUI.InspectTargetChanged -= OnInspectTargetChanged;
            ActionMenuUI.VisibilityChanged -= OnActionMenuVisibilityChanged;

            UnsubscribeAllUnits();
        }

        private void OnLevelLoadingDone(object sender, EventArgs e)
        {
            SubscribeToExistingGridObjects();
            hoveredUnit = null;
            suppressWhileInspectOpen = false;
            suppressWhileActionMenuOpen = false;
            Hide();
        }

        private void OnInspectTargetChanged(Unit unit)
        {
            suppressWhileInspectOpen = unit != null;
            if (suppressWhileInspectOpen)
            {
                Hide();
                return;
            }

            Refresh();
        }

        private void OnActionMenuVisibilityChanged(bool isVisible)
        {
            suppressWhileActionMenuOpen = isVisible;
            if (suppressWhileActionMenuOpen)
            {
                Hide();
                return;
            }

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

            unit.UnitHighlighted += OnUnitHighlighted;
            unit.UnitDehighlighted += OnUnitDehighlighted;
            unit.UnitHealthChanged += OnUnitHealthChanged;
            unit.UnitStatsChanged += OnUnitStatsChanged;
            unit.UnitProgressionChanged += OnUnitProgressionChanged;
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

                unit.UnitHighlighted -= OnUnitHighlighted;
                unit.UnitDehighlighted -= OnUnitDehighlighted;
                unit.UnitHealthChanged -= OnUnitHealthChanged;
                unit.UnitStatsChanged -= OnUnitStatsChanged;
                unit.UnitProgressionChanged -= OnUnitProgressionChanged;
                unit.DestroyedInCombat -= OnUnitDestroyed;
            }

            subscribedUnits.Clear();
        }

        private void OnUnitHighlighted(object sender, EventArgs e)
        {
            hoveredUnit = sender as Unit;
            Refresh();
        }

        private void OnUnitDehighlighted(object sender, EventArgs e)
        {
            if (hoveredUnit == sender as Unit)
            {
                hoveredUnit = null;
            }

            Refresh();
        }

        private void OnEmptyCellHighlighted(object sender, EventArgs e)
        {
            hoveredUnit = null;
            Refresh();
        }

        private void OnUnitHealthChanged(object sender, UnitHealthChangedEventArgs e)
        {
            if (hoveredUnit == sender as Unit)
            {
                Refresh();
            }
        }

        private void OnUnitStatsChanged(object sender, EventArgs e)
        {
            if (hoveredUnit == sender as Unit)
            {
                Refresh();
            }
        }

        private void OnUnitProgressionChanged(object sender, EventArgs e)
        {
            if (hoveredUnit == sender as Unit)
            {
                Refresh();
            }
        }

        private void OnUnitDestroyed(object sender, UnitDestroyedEventArgs e)
        {
            if (hoveredUnit == e?.Defender)
            {
                hoveredUnit = null;
                Refresh();
            }
        }

        private void Refresh()
        {
            if (suppressWhileInspectOpen || suppressWhileActionMenuOpen || hoveredUnit == null)
            {
                Hide();
                return;
            }

            if (root != null)
            {
                root.SetActive(true);
            }

            if (nameText != null)
            {
                nameText.text = hoveredUnit.unitName;
            }

            if (levelText != null)
            {
                levelText.text = GameTextCatalog.Format("ui.common.level_short", "Lv: {0}", hoveredUnit.Level);
            }

            if (hitPointsText != null)
            {
                hitPointsText.text = GameTextCatalog.Format("ui.common.hp_pair", "HP: {0}/{1}", hoveredUnit.HitPoints, hoveredUnit.ComputedTotalHitPoints);
            }

            if (manaPointsText != null)
            {
                manaPointsText.text = GameTextCatalog.Format("ui.common.mp_pair", "MP: {0}/{1}", hoveredUnit.CurrentManaPoints, hoveredUnit.ComputedTotalManaPoints);
            }

            if (attackText != null)
            {
                attackText.text = GameTextCatalog.Format("ui.common.atk_short", "Atk: {0}", hoveredUnit.Attack);
            }

            if (equipText != null)
            {
                equipText.text = GameTextCatalog.Format("ui.common.equip_short", "[E] {0}", hoveredUnit.EquippedWeapon?.Name ?? GameTextCatalog.Get("ui.common.none", "None"));
            }

            PositionUnderUnit(hoveredUnit.transform.position);
        }

        private void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private static void DisableRaycastTargets(GameObject targetRoot)
        {
            if (targetRoot == null)
            {
                return;
            }

            CanvasGroup canvasGroup = targetRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = targetRoot.AddComponent<CanvasGroup>();
            }

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            foreach (Graphic graphic in targetRoot.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }

        private void PositionUnderUnit(Vector3 worldPosition)
        {
            Camera activeWorldCamera = worldCamera != null ? worldCamera : Camera.main;
            Vector3 screenPoint = RectTransformUtility.WorldToScreenPoint(activeWorldCamera, worldPosition);
            screenPoint = ApplyPanelCornerOffset(screenPoint);
            CanvasClampManager.PositionAtScreenPoint(
                canvas,
                canvasRectTransform,
                rootRectTransform,
                positionTarget,
                screenPoint,
                screenPadding);
        }

        private Vector3 ApplyPanelCornerOffset(Vector3 screenPoint)
        {
            if (rootRectTransform == null)
            {
                screenPoint.x += screenOffset.x;
                screenPoint.y += screenOffset.y;
                return screenPoint;
            }

            Vector2 panelSize = rootRectTransform.rect.size;
            Vector2 pivot = rootRectTransform.pivot;

            screenPoint.x += screenOffset.x + panelSize.x * pivot.x;
            screenPoint.y += screenOffset.y - panelSize.y * (1f - pivot.y);
            return screenPoint;
        }

    }
}

