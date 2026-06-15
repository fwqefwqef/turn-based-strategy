using System;
using System.Collections.Generic;
using Windy.Srpg.Game.Localization;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Windy.Srpg.Game.UI
{
    /// <summary>
    /// Shows unit stats while hovering units, with persistence for the currently selected unit.
    /// </summary>
    public class UnitStatsHoverDisplay : MonoBehaviour
    {
        [Header("References")]
        public CustomCellGrid CellGrid;
        public GameObject Root;

        [Header("Labels")]
        [FormerlySerializedAs("AllyEnemyText")]
        public TMP_Text NameText;
        public TMP_Text HitPointsText;
        public TMP_Text EquippedWeaponText;
        public TMP_Text AttackText;
        public TMP_Text StrengthText;
        public TMP_Text RangeText;
        public TMP_Text DefenseText;
        public TMP_Text SpeedText;
        public TMP_Text LuckText;
        public TMP_Text MagicText;
        public TMP_Text ResistanceText;
        public TMP_Text MaxMovementText;
        public TMP_Text CurrentBuffsText;

        private readonly HashSet<CustomUnit> _subscribedUnits = new HashSet<CustomUnit>();
        private CustomUnit _selectedUnit;
        private CustomUnit _hoveredUnit;

        private void OnEnable()
        {
            if (CellGrid == null)
            {
                CellGrid = FindAnyObjectByType<CustomCellGrid>();
            }

            if (CellGrid == null)
            {
                Debug.LogWarning("UnitStatsHoverDisplay: CellGrid reference is missing.");
                Hide();
                return;
            }

            CellGrid.LevelInitialized += OnLevelLoadingDone;
            CellGrid.CustomUnitAdded += OnUnitAdded;
            CellGrid.EmptyCellHighlighted += OnEmptyCellHighlighted;
            CellGrid.BattleTurnEnded += OnTurnEnded;

            SubscribeToExistingGridObjects();
            RefreshFromContext();
        }

        private void OnDisable()
        {
            if (CellGrid != null)
            {
                CellGrid.LevelInitialized -= OnLevelLoadingDone;
                CellGrid.CustomUnitAdded -= OnUnitAdded;
                CellGrid.EmptyCellHighlighted -= OnEmptyCellHighlighted;
                CellGrid.BattleTurnEnded -= OnTurnEnded;
            }

            UnsubscribeAllUnits();
        }

        private void OnLevelLoadingDone(object sender, EventArgs e)
        {
            SubscribeToExistingGridObjects();
            _selectedUnit = null;
            _hoveredUnit = null;
            Hide();
        }

        private void OnTurnEnded(object sender, EventArgs e)
        {
            _selectedUnit = null;
            _hoveredUnit = null;
            Hide();
        }

        private void OnUnitAdded(object sender, CustomUnitAddedEventArgs e)
        {
            if (e?.Unit == null)
            {
                return;
            }

            SubscribeUnit(e.Unit);
        }

        private void SubscribeToExistingGridObjects()
        {
            if (CellGrid != null)
            {
                foreach (var unit in CellGrid.GetAllCustomUnits())
                {
                    if (unit != null)
                    {
                        SubscribeUnit(unit);
                    }
                }
            }

        }

        private void SubscribeUnit(CustomUnit unit)
        {
            if (!_subscribedUnits.Add(unit))
            {
                return;
            }

            unit.UnitHighlighted += OnUnitHighlighted;
            unit.UnitDehighlighted += OnUnitDehighlighted;
            unit.GameplaySelected += OnUnitSelected;
            unit.GameplayDeselected += OnUnitDeselected;
            unit.DestroyedInCombat += OnUnitDestroyed;
            unit.UnitHealthChanged += OnUnitHealthChanged;
            unit.UnitStatsChanged += OnUnitStatsChanged;
            unit.UnitBuffsChanged += OnUnitBuffsChanged;
        }

        private void UnsubscribeAllUnits()
        {
            foreach (var unit in _subscribedUnits)
            {
                if (unit == null)
                {
                    continue;
                }

                unit.UnitHighlighted -= OnUnitHighlighted;
                unit.UnitDehighlighted -= OnUnitDehighlighted;
                unit.GameplaySelected -= OnUnitSelected;
                unit.GameplayDeselected -= OnUnitDeselected;
                unit.DestroyedInCombat -= OnUnitDestroyed;
                unit.UnitHealthChanged -= OnUnitHealthChanged;
                unit.UnitStatsChanged -= OnUnitStatsChanged;
                unit.UnitBuffsChanged -= OnUnitBuffsChanged;
            }

            _subscribedUnits.Clear();
        }

        private void OnUnitHighlighted(object sender, EventArgs e)
        {
            var unit = sender as CustomUnit;
            if (unit == null)
            {
                return;
            }

            _hoveredUnit = unit;
            Show(unit);
        }

        private void OnUnitDehighlighted(object sender, EventArgs e)
        {
            var unit = sender as CustomUnit;
            if (unit == null)
            {
                return;
            }

            if (_hoveredUnit == unit)
            {
                _hoveredUnit = null;
            }

            RefreshFromContext();
        }

        private void OnUnitSelected(object sender, EventArgs e)
        {
            var unit = sender as CustomUnit;
            if (unit == null)
            {
                return;
            }

            _selectedUnit = unit;
            RefreshFromContext();
        }

        private void OnUnitDeselected(object sender, EventArgs e)
        {
            var unit = sender as CustomUnit;
            if (unit == null)
            {
                return;
            }

            if (_selectedUnit == unit)
            {
                _selectedUnit = null;
            }

            RefreshFromContext();
        }

        private void OnUnitDestroyed(object sender, CustomUnitDestroyedEventArgs e)
        {
            var destroyedUnit = e?.Defender;
            if (destroyedUnit == null)
            {
                return;
            }

            if (_hoveredUnit == destroyedUnit)
            {
                _hoveredUnit = null;
            }

            if (_selectedUnit == destroyedUnit)
            {
                _selectedUnit = null;
            }

            RefreshFromContext();
        }

        private void OnUnitBuffsChanged(object sender, EventArgs e)
        {
            var unit = sender as CustomUnit;
            if (unit == null)
            {
                return;
            }

            if (_hoveredUnit == unit || _selectedUnit == unit)
            {
                RefreshFromContext();
            }
        }

        private void OnUnitHealthChanged(object sender, UnitHealthChangedEventArgs e)
        {
            var unit = sender as CustomUnit;
            if (unit == null)
            {
                return;
            }

            if (_hoveredUnit == unit || _selectedUnit == unit)
            {
                RefreshFromContext();
            }
        }

        private void OnUnitStatsChanged(object sender, EventArgs e)
        {
            var unit = sender as CustomUnit;
            if (unit == null)
            {
                return;
            }

            if (_hoveredUnit == unit || _selectedUnit == unit)
            {
                RefreshFromContext();
            }
        }

        private void OnEmptyCellHighlighted(object sender, EventArgs e)
        {
            _hoveredUnit = null;
            RefreshFromContext();
        }

        private void RefreshFromContext()
        {
            if (_hoveredUnit != null)
            {
                Show(_hoveredUnit);
                return;
            }

            if (_selectedUnit != null)
            {
                Show(_selectedUnit);
                return;
            }

            Hide();
        }

        private void Show(CustomUnit unit)
        {
            if (unit == null)
            {
                Hide();
                return;
            }

            if (Root != null)
            {
                Root.SetActive(true);
            }

            if (NameText != null)
            {
                NameText.text = GameTextCatalog.Format("ui.common.name_label", "Name: {0}", unit.unitName);
            }

            if (HitPointsText != null)
            {
                HitPointsText.text = GameTextCatalog.Format("ui.common.hp_pair", "HP: {0}/{1}", unit.HitPoints, unit.ComputedTotalHitPoints);
            }

            if (EquippedWeaponText != null)
            {
                EquippedWeaponText.text = GameTextCatalog.Format("ui.common.equip_label", "Equip: {0}", unit.EquippedWeapon?.Name ?? GameTextCatalog.Get("ui.common.none", "None"));
            }

            if (AttackText != null)
            {
                AttackText.text = GameTextCatalog.Format("ui.common.attack_label", "Atk: {0}", unit.Attack);
            }

            if (StrengthText != null)
            {
                var strengthText = GameTextCatalog.Format("ui.common.strength_label", "Strength: {0}", unit.Strength);
                StrengthText.text = unit.IsMagic ? strengthText : $"<u>{strengthText}</u>";
            }

            if (RangeText != null)
            {
                RangeText.text = GameTextCatalog.Format("ui.common.range_label", "Range: {0}", GameTextCatalog.Format("ui.common.range_value", "{0}-{1}", unit.MinAttackRange, unit.MaxAttackRange));
            }

            if (DefenseText != null)
            {
                DefenseText.text = GameTextCatalog.Format("ui.common.defense_label", "Defense: {0}", unit.Defense);
            }

            if (SpeedText != null)
            {
                SpeedText.text = GameTextCatalog.Format("ui.common.speed_label", "Speed: {0}", unit.Speed);
            }

            if (LuckText != null)
            {
                LuckText.text = GameTextCatalog.Format("ui.common.luck_label", "Luck: {0}", unit.Luck);
            }

            if (MagicText != null)
            {
                var magicText = GameTextCatalog.Format("ui.common.magic_label", "Magic: {0}", unit.Magic);
                MagicText.text = unit.IsMagic ? $"<u>{magicText}</u>" : magicText;
            }

            if (ResistanceText != null)
            {
                ResistanceText.text = GameTextCatalog.Format("ui.common.resistance_label", "Resistance: {0}", unit.Resistance);
            }

            if (MaxMovementText != null)
            {
                MaxMovementText.text = GameTextCatalog.Format("ui.common.movement_label", "Movement: {0}", FormatFloat(unit.ComputedTotalMovementPoints));
            }

            if (CurrentBuffsText != null)
            {
                CurrentBuffsText.text = unit.GetActiveBuffDisplayText();
            }
        }

        private void Hide()
        {
            if (Root != null)
            {
                Root.SetActive(false);
            }
        }

        private static string FormatFloat(float value)
        {
            var rounded = Mathf.Round(value);
            if (Mathf.Approximately(value, rounded))
            {
                return ((int)rounded).ToString();
            }

            return value.ToString("0.##");
        }

    }
}


