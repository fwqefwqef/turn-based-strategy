using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Windy.Srpg.Game.Localization;
using Windy.Srpg.Game.Skills;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Game.Abilities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Windy.Srpg.Game.UI
{
    public class SkillMenuUI : MonoBehaviour, MoveAbility.ISkillMenuUI
    {
        private sealed class SkillRowView
        {
            public Skill Entry;
            public Button Button;
            public Image Background;
            public TMP_Text Label;
        }

        [SerializeField] private GameObject rootPanel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private RectTransform skillDropdown;
        [SerializeField] private RectTransform skillViewport;
        [SerializeField] private ScrollRect skillScrollRect;
        [SerializeField] private Scrollbar verticalScrollbar;
        [SerializeField] private Button skillButtonTemplate;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject skillDisplayPanel;
        [SerializeField] private TMP_Text skillDisplayNameText;
        [SerializeField] private TMP_Text skillDisplayBodyText;
        [SerializeField] private RectTransform positionTarget;
        [SerializeField] private Canvas canvas;
        [SerializeField] private Camera worldCamera;
        [SerializeField, Min(1)] private int visibleSkillRows = 8;
        [SerializeField] private Color defaultRowColor = Color.white;
        [SerializeField] private Color disabledRowColor = new Color(1f, 1f, 1f, 0.45f);
        private readonly List<SkillRowView> skillRows = new List<SkillRowView>();
        private RectTransform panelRectTransform;
        private RectTransform skillDisplayPanelRectTransform;
        private RectTransform canvasRectTransform;
        private VerticalLayoutGroup skillLayoutGroup;
        private Action<Skill> onSelect;
        private Action onCancel;
        private Func<Skill, bool> canUse;
        private Unit currentUnit;
        private Vector2 screenOffset = new Vector2(240f, -140f);
        private Vector2 screenPadding = new Vector2(24f, 24f);
        private Vector2 skillDisplayPanelOffset;

        private void OnValidate()
        {
            if (skillDropdown != null && skillLayoutGroup == null)
            {
                skillLayoutGroup = skillDropdown.GetComponent<VerticalLayoutGroup>();
            }

            if (skillScrollRect == null && skillDropdown != null)
            {
                skillScrollRect = skillDropdown.GetComponentInParent<ScrollRect>();
            }

            if (skillScrollRect != null)
            {
                if (skillViewport == null)
                {
                    skillViewport = skillScrollRect.viewport;
                }

                if (skillDropdown != null && skillScrollRect.content == null)
                {
                    skillScrollRect.content = skillDropdown;
                }

                if (skillViewport != null && skillScrollRect.viewport == null)
                {
                    skillScrollRect.viewport = skillViewport;
                }

                skillScrollRect.horizontal = false;

                if (verticalScrollbar == null)
                {
                    verticalScrollbar = skillScrollRect.verticalScrollbar;
                }
            }

            NormalizeScrollContentRect();
            NormalizeViewportRect();
            ConfigureSkillLayoutGroup();

            if (positionTarget == null)
            {
                RectTransform ownRectTransform = transform as RectTransform;
                if (ownRectTransform != null && rootPanel != null && rootPanel.transform.IsChildOf(transform))
                {
                    positionTarget = ownRectTransform;
                }
            }
        }

        private void Awake()
        {
            if (rootPanel != null)
            {
                panelRectTransform = rootPanel.GetComponent<RectTransform>();
                rootPanel.SetActive(false);
            }

            if (positionTarget == null)
            {
                RectTransform ownRectTransform = transform as RectTransform;
                if (ownRectTransform != null && rootPanel != null && rootPanel.transform.IsChildOf(transform))
                {
                    positionTarget = ownRectTransform;
                }
                else
                {
                    positionTarget = panelRectTransform;
                }
            }

            if (skillDisplayPanel != null)
            {
                skillDisplayPanelRectTransform = skillDisplayPanel.GetComponent<RectTransform>();
            }

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            if (canvas != null)
            {
                canvasRectTransform = canvas.GetComponent<RectTransform>();
            }

            if (skillDropdown != null)
            {
                skillLayoutGroup = skillDropdown.GetComponent<VerticalLayoutGroup>();
                NormalizeScrollContentRect();
                ConfigureSkillLayoutGroup();
            }

            if (skillScrollRect == null && skillDropdown != null)
            {
                skillScrollRect = skillDropdown.GetComponentInParent<ScrollRect>();
            }

            if (skillScrollRect != null)
            {
                if (skillViewport == null)
                {
                    skillViewport = skillScrollRect.viewport;
                }

                if (skillScrollRect.content == null)
                {
                    skillScrollRect.content = skillDropdown;
                }

                skillScrollRect.horizontal = false;
                NormalizeViewportRect();
            }

            if (verticalScrollbar == null && skillScrollRect != null)
            {
                verticalScrollbar = skillScrollRect.verticalScrollbar;
            }

            if (skillButtonTemplate != null)
            {
                skillButtonTemplate.gameObject.SetActive(false);
            }

            if (skillDisplayPanel != null)
            {
                skillDisplayPanel.SetActive(false);
            }

            CacheSkillDisplayPanelOffset();

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => onCancel?.Invoke());
            }
        }

        private void Update()
        {
            if (rootPanel == null || !rootPanel.activeSelf || !Input.GetMouseButtonDown(1))
            {
                return;
            }

            onCancel?.Invoke();
        }

        public void Show(
            Vector3 worldPosition,
            Unit unit,
            IReadOnlyList<Skill> skills,
            Func<Skill, bool> canUse,
            Action<Skill> onSelect,
            Action onCancel)
        {
            currentUnit = unit;
            this.canUse = canUse;
            this.onSelect = onSelect;
            this.onCancel = onCancel;

            if (titleText != null)
            {
                titleText.text = currentUnit == null
                    ? GameTextCatalog.ResolveSceneText(titleText, "ui.skill_menu.title", "Skills")
                    : GameTextCatalog.Format("ui.skill_menu.title_with_name", "{0} Skills", currentUnit.unitName);
            }

            if (rootPanel != null)
            {
                rootPanel.SetActive(true);
                PositionPanel(worldPosition);
            }

            Canvas.ForceUpdateCanvases();
            RebuildEntries(skills);
            ConfigureScrollableList();
            ResetScrollPosition();
            Canvas.ForceUpdateCanvases();
            ConfigureScrollableList();
            ResetScrollPosition();
        }

        public void Hide()
        {
            currentUnit = null;
            canUse = null;
            onSelect = null;
            onCancel = null;
            ClearSpawnedButtons();

            if (skillDisplayPanel != null)
            {
                skillDisplayPanel.SetActive(false);
            }

            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }
        }

        private void RebuildEntries(IReadOnlyList<Skill> skills)
        {
            ClearSpawnedButtons();

            if (skillDropdown == null || skillButtonTemplate == null || skills == null)
            {
                return;
            }

            var orderedSkills = skills
                .Select((skill, index) => new
                {
                    Skill = skill,
                    Index = index,
                    IsUsable = skill != null && canUse != null && canUse(skill)
                })
                .OrderByDescending(entry => entry.IsUsable)
                .ThenBy(entry => entry.Index)
                .ToList();

            foreach (var entry in orderedSkills)
            {
                var skill = entry.Skill;
                var button = Instantiate(skillButtonTemplate, skillDropdown, false);
                button.gameObject.SetActive(true);
                button.onClick.RemoveAllListeners();

                var row = new SkillRowView
                {
                    Entry = skill,
                    Button = button,
                    Background = button.GetComponent<Image>(),
                    Label = button.GetComponentInChildren<TMP_Text>(true)
                };

                bool isUsable = entry.IsUsable;
                string skillName = skill?.Data?.Name ?? skill?.SkillId ?? "Missing Skill";
                if (row.Label != null)
                {
                    row.Label.text = skillName;
                }

                if (row.Background != null)
                {
                    row.Background.color = isUsable ? defaultRowColor : disabledRowColor;
                }

                AddHoverPreview(button, skill);
                button.interactable = isUsable;
                if (isUsable)
                {
                    button.onClick.AddListener(() =>
                    {
                        ShowSkillDetails(skill);
                        onSelect?.Invoke(skill);
                    });
                }
                else
                {
                    button.onClick.AddListener(() => ShowSkillDetails(skill));
                }

                skillRows.Add(row);
            }

            ApplyVisibleRowSizing();
            ShowSkillDetails(orderedSkills.Select(entry => entry.Skill).FirstOrDefault(skill => skill?.Data != null));
        }

        private void AddHoverPreview(Button button, Skill skill)
        {
            if (button == null)
            {
                return;
            }

            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers ??= new List<EventTrigger.Entry>();
            trigger.triggers.RemoveAll(entry =>
                entry != null &&
                (entry.eventID == EventTriggerType.PointerEnter || entry.eventID == EventTriggerType.Select));

            AddEventTriggerEntry(trigger, EventTriggerType.PointerEnter, () => ShowSkillDetails(skill));
            AddEventTriggerEntry(trigger, EventTriggerType.Select, () => ShowSkillDetails(skill));
        }

        private static void AddEventTriggerEntry(EventTrigger trigger, EventTriggerType eventType, Action action)
        {
            if (trigger == null || action == null)
            {
                return;
            }

            EventTrigger.Entry entry = new EventTrigger.Entry
            {
                eventID = eventType
            };
            entry.callback.AddListener(_ => action.Invoke());
            trigger.triggers.Add(entry);
        }

        private void ConfigureScrollableList()
        {
            if (skillDropdown == null || skillButtonTemplate == null)
            {
                return;
            }

            NormalizeScrollContentRect();
            NormalizeViewportRect();

            float rowHeight = GetRowHeight();
            float spacing = skillLayoutGroup != null ? skillLayoutGroup.spacing : 0f;
            RectOffset padding = skillLayoutGroup != null ? skillLayoutGroup.padding : new RectOffset();
            int rowCount = skillRows.Count;

            float contentHeight = padding.top + padding.bottom;
            if (rowCount > 0)
            {
                contentHeight += rowCount * rowHeight + Mathf.Max(0, rowCount - 1) * spacing;
            }

            skillDropdown.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);

            bool shouldScroll = rowCount > visibleSkillRows;
            if (skillScrollRect != null)
            {
                skillScrollRect.content = skillDropdown;
                if (skillViewport != null)
                {
                    skillScrollRect.viewport = skillViewport;
                }

                skillScrollRect.vertical = shouldScroll;
                skillScrollRect.movementType = ScrollRect.MovementType.Clamped;
                skillScrollRect.scrollSensitivity = Mathf.Max(skillScrollRect.scrollSensitivity, rowHeight * 0.6f);
            }

            if (verticalScrollbar != null)
            {
                verticalScrollbar.gameObject.SetActive(shouldScroll);
            }

            ApplyVisibleRowSizing();
            LayoutRebuilder.ForceRebuildLayoutImmediate(skillDropdown);
            if (skillViewport != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(skillViewport);
            }
        }

        private void ResetScrollPosition()
        {
            if (skillScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                skillDropdown.anchoredPosition = Vector2.zero;
                skillScrollRect.verticalNormalizedPosition = 1f;
                Canvas.ForceUpdateCanvases();
                skillDropdown.anchoredPosition = Vector2.zero;
            }
        }

        private void NormalizeScrollContentRect()
        {
            if (skillDropdown == null)
            {
                return;
            }

            skillDropdown.anchorMin = new Vector2(0f, 1f);
            skillDropdown.anchorMax = new Vector2(1f, 1f);
            skillDropdown.pivot = new Vector2(0.5f, 1f);
            skillDropdown.anchoredPosition = Vector2.zero;
        }

        private void NormalizeViewportRect()
        {
            if (skillViewport == null)
            {
                return;
            }

            skillViewport.pivot = new Vector2(0.5f, 1f);
            skillViewport.anchoredPosition = Vector2.zero;
        }

        private void ConfigureSkillLayoutGroup()
        {
            if (skillLayoutGroup == null)
            {
                return;
            }

            // Skill rows should size to their content instead of stretching to fill the viewport.
            skillLayoutGroup.childForceExpandHeight = false;
        }

        private float GetRowHeight()
        {
            float fittedRowHeight = GetViewportFittedRowHeight();
            if (fittedRowHeight > 0f)
            {
                return fittedRowHeight;
            }

            if (skillButtonTemplate == null)
            {
                return 40f;
            }

            LayoutElement layoutElement = skillButtonTemplate.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                if (layoutElement.preferredHeight > 0f)
                {
                    return layoutElement.preferredHeight;
                }

                if (layoutElement.minHeight > 0f)
                {
                    return layoutElement.minHeight;
                }
            }

            RectTransform templateRect = skillButtonTemplate.GetComponent<RectTransform>();
            if (templateRect == null)
            {
                return 40f;
            }

            float height = templateRect.rect.height;
            return height > 0f ? height : 40f;
        }

        private void ApplyVisibleRowSizing()
        {
            float fittedRowHeight = GetViewportFittedRowHeight();
            if (fittedRowHeight <= 0f)
            {
                return;
            }

            foreach (SkillRowView row in skillRows)
            {
                if (row?.Button == null)
                {
                    continue;
                }

                LayoutElement layoutElement = row.Button.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = row.Button.gameObject.AddComponent<LayoutElement>();
                }

                layoutElement.minHeight = fittedRowHeight;
                layoutElement.preferredHeight = fittedRowHeight;
                layoutElement.flexibleHeight = 0f;
            }
        }

        private float GetViewportFittedRowHeight()
        {
            if (skillViewport == null || visibleSkillRows <= 0)
            {
                return -1f;
            }

            float viewportHeight = skillViewport.rect.height;
            if (viewportHeight <= 0f)
            {
                return -1f;
            }

            float spacing = skillLayoutGroup != null ? skillLayoutGroup.spacing : 0f;
            RectOffset padding = skillLayoutGroup != null ? skillLayoutGroup.padding : new RectOffset();
            float availableHeight = viewportHeight - padding.top - padding.bottom - Mathf.Max(0, visibleSkillRows - 1) * spacing;
            if (availableHeight <= 0f)
            {
                return -1f;
            }

            return availableHeight / visibleSkillRows;
        }

        private void ShowSkillDetails(Skill skill)
        {
            if (skillDisplayPanel != null)
            {
                skillDisplayPanel.SetActive(skill?.Data != null);
            }

            if (skill?.Data == null)
            {
                if (skillDisplayNameText != null)
                {
                    skillDisplayNameText.text = string.Empty;
                }

                if (skillDisplayBodyText != null)
                {
                    skillDisplayBodyText.text = string.Empty;
                }

                return;
            }

            if (skillDisplayNameText != null)
            {
                skillDisplayNameText.text = skill.Data.Name;
            }

            if (skillDisplayBodyText != null)
            {
                skillDisplayBodyText.text = skill.Data.Description;
            }
        }

        private void ClearSpawnedButtons()
        {
            foreach (var row in skillRows)
            {
                if (row?.Button != null)
                {
                    Destroy(row.Button.gameObject);
                }
            }

            skillRows.Clear();
        }

        private void PositionPanel(Vector3 worldPosition)
        {
            bool positioned = CanvasClampManager.PositionAtWorldPoint(
                canvas,
                worldCamera,
                canvasRectTransform,
                panelRectTransform,
                positionTarget,
                worldPosition,
                screenOffset,
                screenPadding);
            if (!positioned)
            {
                return;
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                PositionSkillDisplayPanelOverlay();
            }
            else
            {
                PositionSkillDisplayPanelLocal();
            }
        }

        private void CacheSkillDisplayPanelOffset()
        {
            if (panelRectTransform == null || skillDisplayPanelRectTransform == null)
            {
                return;
            }

            if (positionTarget != null && skillDisplayPanelRectTransform.IsChildOf(positionTarget))
            {
                return;
            }

            if (skillDisplayPanelRectTransform.parent != panelRectTransform.parent)
            {
                return;
            }

            skillDisplayPanelOffset = skillDisplayPanelRectTransform.anchoredPosition - panelRectTransform.anchoredPosition;
        }

        private void PositionSkillDisplayPanelOverlay()
        {
            if (positionTarget == null || panelRectTransform == null || skillDisplayPanelRectTransform == null)
            {
                return;
            }

            if (skillDisplayPanelRectTransform.IsChildOf(positionTarget))
            {
                return;
            }

            if (skillDisplayPanelRectTransform.parent != panelRectTransform.parent)
            {
                return;
            }

            skillDisplayPanelRectTransform.position = positionTarget.position + (Vector3)skillDisplayPanelOffset;
        }

        private void PositionSkillDisplayPanelLocal()
        {
            if (positionTarget == null || panelRectTransform == null || skillDisplayPanelRectTransform == null)
            {
                return;
            }

            if (skillDisplayPanelRectTransform.IsChildOf(positionTarget))
            {
                return;
            }

            if (skillDisplayPanelRectTransform.parent != panelRectTransform.parent)
            {
                return;
            }

            skillDisplayPanelRectTransform.anchoredPosition = positionTarget.anchoredPosition + skillDisplayPanelOffset;
        }

    }
}

