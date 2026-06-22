using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Windy.Srpg.Game.Localization;
using Windy.Srpg.Game.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Windy.Srpg.Game.UI
{
    [AddComponentMenu("UI/Level Up UI")]
    public class LevelUpUI : MonoBehaviour
    {
        public static event Action<bool> VisibilityChanged;
        private static LevelUpUI activeInstance;

        [Serializable]
        public class StatRowBindings
        {
            public LevelableStatKind Stat;
            public TMP_Text NameText;
            public TMP_Text ValueText;
            public RectTransform BarBounds;
            public Image BaseBarImage;
            public RectTransform IncrementSliceTransform;
            public Image HighlightImage;
            public Button SelectButton;
        }

        [Header("References")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private GameObject confirmRoot;
        [SerializeField] private TMP_Text confirmText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private List<StatRowBindings> statRows = new List<StatRowBindings>();

        [Header("Visuals")]
        [SerializeField] private Color baseBarColor = new Color32(190, 190, 190, 255);
        [SerializeField] private Color incrementBarColor = new Color32(80, 220, 120, 255);
        [SerializeField] private Color highlightColor = new Color32(255, 240, 120, 255);
        [SerializeField] private int maxChartStatValue = 40;
        [SerializeField] private float postConfirmDelaySeconds = 0.1f;

        private LevelUpPresentation currentPresentation;
        private LevelableStatKind? pendingSelection;
        private LevelableStatKind? resolvedSelection;
        private Action<LevelableStatKind> onResolved;
        private LevelableStatKind? hoveredStat;

        private void Awake()
        {
            activeInstance = this;
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnConfirmClicked);
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(OnCancelClicked);
                cancelButton.onClick.AddListener(OnCancelClicked);
            }

            HideImmediate();
        }

        private void Update()
        {
            RefreshRowHighlightStates();
        }

        private void OnDisable()
        {
            if (activeInstance == this && root != null && !root.activeSelf)
            {
                pendingSelection = null;
            }

            VisibilityChanged?.Invoke(false);
        }

        private void OnDestroy()
        {
            if (activeInstance == this)
            {
                activeInstance = null;
            }
        }

        public static Button GetPreferredFocusButton(IReadOnlyList<Button> activeButtons)
        {
            if (activeInstance == null || activeButtons == null || activeButtons.Count == 0)
            {
                return null;
            }

            return activeInstance.ResolvePreferredFocusButton(activeButtons);
        }

        public IEnumerator ShowAndWait(Unit unit, LevelUpPresentation presentation, Action<LevelableStatKind> resolvedCallback)
        {
            if (unit == null || presentation == null || root == null)
            {
                resolvedCallback?.Invoke(LevelableStatKind.Strength);
                yield break;
            }

            currentPresentation = presentation;
            onResolved = resolvedCallback;
            pendingSelection = null;
            resolvedSelection = null;
            hoveredStat = null;

            if (levelText != null)
            {
                levelText.text = GameTextCatalog.Format("ui.common.level_transition", "Lv {0} -> {1}", presentation.OldLevel, presentation.NewLevel);
            }

            root.SetActive(true);
            VisibilityChanged?.Invoke(true);
            if (confirmRoot != null)
            {
                confirmRoot.SetActive(false);
            }

            RefreshRows();

            while (!resolvedSelection.HasValue)
            {
                yield return null;
            }

            onResolved?.Invoke(resolvedSelection.Value);

            if (postConfirmDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(postConfirmDelaySeconds);
            }

            HideImmediate();
        }

        private void RefreshRows()
        {
            foreach (StatRowBindings row in statRows)
            {
                if (row == null)
                {
                    continue;
                }

                int baseValue = currentPresentation?.GetBaseStat(row.Stat) ?? 0;
                int totalGain = currentPresentation?.GetDisplayedGain(row.Stat, pendingSelection) ?? 0;
                int finalValue = baseValue + totalGain;

                if (row.NameText != null)
                {
                    row.NameText.text = GetStatDisplayName(row.Stat);
                }

                if (row.ValueText != null)
                {
                    row.ValueText.text = totalGain > 0 ? $"{baseValue} -> {finalValue}" : baseValue.ToString();
                }

                if (row.BaseBarImage != null)
                {
                    row.BaseBarImage.color = baseBarColor;
                }

                if (row.HighlightImage != null)
                {
                    row.HighlightImage.color = highlightColor;
                }

                RefreshBarSlice(row, baseValue, totalGain);
                ConfigureRowButton(row);
            }

            if (confirmButton != null)
            {
                confirmButton.interactable = pendingSelection.HasValue;
            }

            if (summaryText != null)
            {
                summaryText.text = BuildSummaryText();
            }

            RefreshRowHighlightStates();
        }

        private void RefreshBarSlice(StatRowBindings row, int baseValue, int totalGain)
        {
            if (row.BarBounds == null)
            {
                return;
            }

            float barWidth = row.BarBounds.rect.width;
            float cappedBase = Mathf.Clamp(baseValue, 0, maxChartStatValue);
            float cappedFinal = Mathf.Clamp(baseValue + totalGain, 0, maxChartStatValue);
            float baseWidth = barWidth * (cappedBase / Mathf.Max(1f, maxChartStatValue));
            float incrementWidth = barWidth * ((cappedFinal - cappedBase) / Mathf.Max(1f, maxChartStatValue));

            if (row.BaseBarImage != null)
            {
                RectTransform rect = row.BaseBarImage.rectTransform;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, baseWidth);
            }

            if (row.IncrementSliceTransform != null)
            {
                row.IncrementSliceTransform.gameObject.SetActive(incrementWidth > 0.01f);
                row.IncrementSliceTransform.anchoredPosition = new Vector2(baseWidth, row.IncrementSliceTransform.anchoredPosition.y);
                row.IncrementSliceTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, incrementWidth);

                Image sliceImage = row.IncrementSliceTransform.GetComponent<Image>();
                if (sliceImage != null)
                {
                    sliceImage.color = incrementBarColor;
                }
            }
        }

        private void ConfigureRowButton(StatRowBindings row)
        {
            if (row.SelectButton == null)
            {
                return;
            }

            row.SelectButton.onClick.RemoveAllListeners();
            row.SelectButton.onClick.AddListener(() => OnRowClicked(row.Stat));

            LevelUpRowHoverForwarder hoverForwarder = row.SelectButton.GetComponent<LevelUpRowHoverForwarder>();
            if (hoverForwarder == null)
            {
                hoverForwarder = row.SelectButton.gameObject.AddComponent<LevelUpRowHoverForwarder>();
            }

            hoverForwarder.Configure(
                () =>
                {
                    hoveredStat = row.Stat;
                    RefreshRowHighlightStates();
                },
                () =>
                {
                    if (hoveredStat.HasValue && hoveredStat.Value == row.Stat)
                    {
                        hoveredStat = null;
                    }

                    RefreshRowHighlightStates();
                });
        }

        private void OnRowClicked(LevelableStatKind stat)
        {
            pendingSelection = stat;

            if (confirmRoot != null)
            {
                confirmRoot.SetActive(true);
            }

            if (confirmText != null)
            {
                confirmText.text = GameTextCatalog.Format("ui.level_up.increase_stat", "Increase {0}?", GetStatDisplayName(stat));
            }

            RefreshRows();
        }

        private void OnConfirmClicked()
        {
            if (!pendingSelection.HasValue)
            {
                return;
            }

            resolvedSelection = pendingSelection.Value;
            if (confirmRoot != null)
            {
                confirmRoot.SetActive(false);
            }
        }

        private void OnCancelClicked()
        {
            pendingSelection = null;
            if (confirmRoot != null)
            {
                confirmRoot.SetActive(false);
            }

            RefreshRows();
        }

        private string BuildSummaryText()
        {
            if (currentPresentation == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            foreach (LevelableStatKind stat in Enum.GetValues(typeof(LevelableStatKind)))
            {
                int gain = currentPresentation.GetDisplayedGain(stat, pendingSelection);
                if (gain > 0)
                {
                    parts.Add($"{GetStatDisplayName(stat)} +{gain}");
                }
            }

            return parts.Count > 0
                ? string.Join(", ", parts)
                : GameTextCatalog.Get("ui.level_up.choose_stat", "Choose 1 stat to increase.");
        }

        private void HideImmediate()
        {
            if (root != null)
            {
                root.SetActive(false);
            }

            hoveredStat = null;

            VisibilityChanged?.Invoke(false);

            if (confirmRoot != null)
            {
                confirmRoot.SetActive(false);
            }
        }

        private static string GetStatDisplayName(LevelableStatKind stat)
        {
            return stat switch
            {
                LevelableStatKind.Strength => GameTextCatalog.Get("ui.level_up.stat.strength", "Strength"),
                LevelableStatKind.Magic => GameTextCatalog.Get("ui.level_up.stat.magic", "Magic"),
                LevelableStatKind.Defense => GameTextCatalog.Get("ui.level_up.stat.defense", "Defense"),
                LevelableStatKind.Resistance => GameTextCatalog.Get("ui.level_up.stat.resistance", "Resistance"),
                LevelableStatKind.Speed => GameTextCatalog.Get("ui.level_up.stat.speed", "Speed"),
                LevelableStatKind.Luck => GameTextCatalog.Get("ui.level_up.stat.luck", "Luck"),
                _ => stat.ToString()
            };
        }

        private Button ResolvePreferredFocusButton(IReadOnlyList<Button> activeButtons)
        {
            if (root == null || !root.activeInHierarchy || activeButtons == null || activeButtons.Count == 0)
            {
                return null;
            }

            if (confirmRoot != null && confirmRoot.activeInHierarchy)
            {
                if (confirmButton != null && confirmButton.interactable && activeButtons.Contains(confirmButton))
                {
                    return confirmButton;
                }

                if (cancelButton != null && activeButtons.Contains(cancelButton))
                {
                    return cancelButton;
                }
            }

            foreach (StatRowBindings row in statRows)
            {
                if (row?.SelectButton == null)
                {
                    continue;
                }

                if (activeButtons.Contains(row.SelectButton) && row.SelectButton.isActiveAndEnabled && row.SelectButton.interactable)
                {
                    return row.SelectButton;
                }
            }

            if (cancelButton != null && activeButtons.Contains(cancelButton))
            {
                return cancelButton;
            }

            return null;
        }

        private void RefreshRowHighlightStates()
        {
            if (statRows == null || statRows.Count == 0)
            {
                return;
            }

            GameObject selectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            foreach (StatRowBindings row in statRows)
            {
                if (row?.HighlightImage == null)
                {
                    continue;
                }

                bool shouldHighlight = pendingSelection.HasValue && pendingSelection.Value == row.Stat;
                if (!shouldHighlight && hoveredStat.HasValue && hoveredStat.Value == row.Stat)
                {
                    shouldHighlight = true;
                }

                if (!shouldHighlight && selectedObject != null && row.SelectButton != null)
                {
                    shouldHighlight = selectedObject == row.SelectButton.gameObject
                        || selectedObject.transform.IsChildOf(row.SelectButton.transform);
                }

                row.HighlightImage.color = highlightColor;
                row.HighlightImage.enabled = shouldHighlight;
            }
        }
    }

    public class LevelUpRowHoverForwarder : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Action onEnter;
        private Action onExit;

        public void Configure(Action handleEnter, Action handleExit)
        {
            onEnter = handleEnter;
            onExit = handleExit;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            onEnter?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            onExit?.Invoke();
        }
    }
}

