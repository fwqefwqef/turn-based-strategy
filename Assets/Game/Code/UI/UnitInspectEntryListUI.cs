using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Windy.Srpg.Game.UI
{
    public class UnitInspectEntryListUI : MonoBehaviour
    {
        private static readonly Color TextNormalColor = Color.black;
        private static readonly Color TextHighlightColor = new Color(0.08f, 0.33f, 0.62f, 1f);
        private static readonly Color BackgroundHighlightColor = new Color(0.78f, 0.87f, 1f, 1f);

        private sealed class RowVisualState
        {
            public Button Button;
            public Image Background;
            public TMP_Text Label;
            public string BaseLabel;
        }

        public readonly struct EntryData
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly string DetailTitle;
            public readonly string DetailBody;

            public EntryData(string id, string displayName, string detailTitle, string detailBody)
            {
                Id = id ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                DetailTitle = string.IsNullOrWhiteSpace(detailTitle) ? DisplayName : detailTitle;
                DetailBody = detailBody ?? string.Empty;
            }
        }

        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Scrollbar verticalScrollbar;
        [SerializeField] private Button rowTemplate;
        [SerializeField] private TMP_Text emptyText;
        [SerializeField, Min(1)] private int visibleRows = 3;
        [SerializeField] private Color rowColor = Color.white;
        [SerializeField] private Color disabledRowColor = Color.white;

        private readonly List<Button> spawnedRows = new List<Button>();
        private readonly List<RowVisualState> rowStates = new List<RowVisualState>();
        private VerticalLayoutGroup layoutGroup;
        private bool isSynchronizingScroll;
        private int lastRowCount;
        private System.Action<EntryData> onEntryClicked;
        private Button hoveredButton;

        private void OnValidate()
        {
            if (content != null && layoutGroup == null)
            {
                layoutGroup = content.GetComponent<VerticalLayoutGroup>();
            }

            if (scrollRect == null && content != null)
            {
                scrollRect = content.GetComponentInParent<ScrollRect>();
            }

            if (scrollRect != null)
            {
                if (viewport == null)
                {
                    viewport = scrollRect.viewport;
                }

                if (content != null && scrollRect.content == null)
                {
                    scrollRect.content = content;
                }

                if (viewport != null && scrollRect.viewport == null)
                {
                    scrollRect.viewport = viewport;
                }

                scrollRect.horizontal = false;

                if (verticalScrollbar == null)
                {
                    verticalScrollbar = scrollRect.verticalScrollbar;
                }
                else if (scrollRect.verticalScrollbar == null)
                {
                    scrollRect.verticalScrollbar = verticalScrollbar;
                }
            }

            NormalizeContentRect();
            NormalizeViewportRect();
        }

        private void Awake()
        {
            if (content != null)
            {
                layoutGroup = content.GetComponent<VerticalLayoutGroup>();
                NormalizeContentRect();
            }

            if (scrollRect == null && content != null)
            {
                scrollRect = content.GetComponentInParent<ScrollRect>();
            }

            if (scrollRect != null)
            {
                if (viewport == null)
                {
                    viewport = scrollRect.viewport;
                }

                if (scrollRect.content == null)
                {
                    scrollRect.content = content;
                }

                scrollRect.horizontal = false;
                if (verticalScrollbar != null && scrollRect.verticalScrollbar == null)
                {
                    scrollRect.verticalScrollbar = verticalScrollbar;
                }
                NormalizeViewportRect();
            }

            if (verticalScrollbar == null && scrollRect != null)
            {
                verticalScrollbar = scrollRect.verticalScrollbar;
            }
            else if (verticalScrollbar != null && scrollRect != null && scrollRect.verticalScrollbar == null)
            {
                scrollRect.verticalScrollbar = verticalScrollbar;
            }

            if (rowTemplate != null)
            {
                rowTemplate.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            RegisterScrollCallbacks();
        }

        private void OnDisable()
        {
            UnregisterScrollCallbacks();
            hoveredButton = null;
        }

        private void Update()
        {
            RefreshRowVisualStates();
        }

        public void SetEntries(IEnumerable<EntryData> entries, System.Action<EntryData> onEntryClicked = null)
        {
            ClearSpawnedRows();
            this.onEntryClicked = onEntryClicked;

            List<EntryData> resolvedEntries = entries?
                .Where(entry => !string.IsNullOrWhiteSpace(entry.DisplayName))
                .ToList() ?? new List<EntryData>();
            lastRowCount = resolvedEntries.Count;

            if (content != null && rowTemplate != null)
            {
                foreach (EntryData entry in resolvedEntries)
                {
                    Button rowButton = Instantiate(rowTemplate, content, false);
                    rowButton.gameObject.SetActive(true);
                    rowButton.interactable = true;
                    rowButton.onClick.RemoveAllListeners();
                    if (this.onEntryClicked != null)
                    {
                        EntryData capturedEntry = entry;
                        rowButton.onClick.AddListener(() => this.onEntryClicked?.Invoke(capturedEntry));
                        RegisterPointerClickCallbacks(rowButton, capturedEntry);
                    }

                    Image background = rowButton.GetComponent<Image>();
                    if (background != null)
                    {
                        background.color = rowColor;
                    }

                    ColorBlock colors = rowButton.colors;
                    colors.normalColor = rowColor;
                    colors.highlightedColor = rowColor;
                    colors.selectedColor = rowColor;
                    colors.pressedColor = rowColor;
                    colors.disabledColor = disabledRowColor;
                    rowButton.colors = colors;

                    TMP_Text label = rowButton.GetComponentInChildren<TMP_Text>(true);
                    if (label != null)
                    {
                        label.text = entry.DisplayName;
                        label.color = TextNormalColor;
                        label.fontStyle = FontStyles.Normal;
                    }

                    RegisterRowVisualCallbacks(rowButton, label);

                    spawnedRows.Add(rowButton);
                    rowStates.Add(new RowVisualState
                    {
                        Button = rowButton,
                        Background = background,
                        Label = label,
                        BaseLabel = entry.DisplayName
                    });
                }
            }

            ApplyVisibleRowSizing();

            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(resolvedEntries.Count == 0);
            }

            ConfigureScrollableList(resolvedEntries.Count);
            ResetScrollPosition();
        }

        public bool ContainsButton(Button button)
        {
            return button != null && spawnedRows.Contains(button);
        }

        public bool TryGetAdjacentButton(Button currentButton, int direction, out Button nextButton)
        {
            nextButton = null;
            if (currentButton == null || direction == 0)
            {
                return false;
            }

            int currentIndex = spawnedRows.IndexOf(currentButton);
            if (currentIndex < 0)
            {
                return false;
            }

            int targetIndex = Mathf.Clamp(currentIndex + direction, 0, spawnedRows.Count - 1);
            if (targetIndex == currentIndex)
            {
                return false;
            }

            nextButton = spawnedRows[targetIndex];
            return nextButton != null;
        }

        public void ScrollButtonIntoView(Button button)
        {
            if (button == null || scrollRect == null || content == null || viewport == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            int rowIndex = spawnedRows.IndexOf(button);
            if (rowIndex < 0)
            {
                return;
            }

            float rowHeight = GetRowHeight();
            float spacing = layoutGroup != null ? layoutGroup.spacing : 0f;
            float step = rowHeight + spacing;
            if (step <= 0f)
            {
                return;
            }

            RectOffset padding = layoutGroup != null ? layoutGroup.padding : new RectOffset();
            int maxTopIndex = Mathf.Max(0, spawnedRows.Count - visibleRows);
            int currentTopIndex = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(0f, content.anchoredPosition.y - padding.top) / step), 0, maxTopIndex);
            int newTopIndex = currentTopIndex;

            if (rowIndex < currentTopIndex)
            {
                newTopIndex = rowIndex;
            }
            else if (rowIndex > currentTopIndex + visibleRows - 1)
            {
                newTopIndex = rowIndex - visibleRows + 1;
            }

            if (newTopIndex == currentTopIndex)
            {
                return;
            }

            Vector2 anchoredPosition = content.anchoredPosition;
            anchoredPosition.y = padding.top + (newTopIndex * step);
            float maxScroll = Mathf.Max(0f, content.rect.height - viewport.rect.height);
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, 0f, maxScroll);
            content.anchoredPosition = anchoredPosition;
            Canvas.ForceUpdateCanvases();

            if (maxScroll > 0.001f)
            {
                float normalized = 1f - Mathf.Clamp01(anchoredPosition.y / maxScroll);
                scrollRect.verticalNormalizedPosition = normalized;
                if (verticalScrollbar != null)
                {
                    verticalScrollbar.SetValueWithoutNotify(normalized);
                }
            }
        }

        public void ClearEntries()
        {
            ClearSpawnedRows();
            lastRowCount = 0;
            onEntryClicked = null;
            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(true);
            }

            ConfigureScrollableList(0);
            ResetScrollPosition();
        }

        private void ClearSpawnedRows()
        {
            foreach (Button row in spawnedRows)
            {
                if (row != null)
                {
                    Destroy(row.gameObject);
                }
            }

            spawnedRows.Clear();
            rowStates.Clear();
            hoveredButton = null;
        }

        private void RegisterRowVisualCallbacks(Button rowButton, TMP_Text label)
        {
            if (rowButton == null)
            {
                return;
            }

            RegisterHoverCallbacks(rowButton.gameObject, rowButton);
            if (label != null)
            {
                RegisterHoverCallbacks(label.gameObject, rowButton);
            }

            EventTrigger trigger = GetOrCreateEventTrigger(rowButton.gameObject);
            RemoveEventTriggerEntries(trigger, EventTriggerType.Select, EventTriggerType.Deselect);

            AddEventTriggerEntry(trigger, EventTriggerType.Select, () =>
            {
                RefreshRowVisualStates();
            });
            AddEventTriggerEntry(trigger, EventTriggerType.Deselect, RefreshRowVisualStates);
        }

        private void RegisterPointerClickCallbacks(Button rowButton, EntryData entry)
        {
            if (rowButton == null)
            {
                return;
            }

            RegisterPointerClickCallback(rowButton.gameObject, entry);

            Image background = rowButton.GetComponent<Image>();
            if (background != null)
            {
                RegisterPointerClickCallback(background.gameObject, entry);
            }

            TMP_Text label = rowButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                RegisterPointerClickCallback(label.gameObject, entry);
            }
        }

        private void RegisterPointerClickCallback(GameObject target, EntryData entry)
        {
            if (target == null)
            {
                return;
            }

            EventTrigger trigger = GetOrCreateEventTrigger(target);
            RemoveEventTriggerEntries(trigger, EventTriggerType.PointerClick);
            AddEventTriggerEntry(trigger, EventTriggerType.PointerClick, () => onEntryClicked?.Invoke(entry));
        }

        private void RegisterHoverCallbacks(GameObject target, Button owningButton)
        {
            if (target == null || owningButton == null)
            {
                return;
            }

            EventTrigger trigger = GetOrCreateEventTrigger(target);
            RemoveEventTriggerEntries(trigger, EventTriggerType.PointerEnter, EventTriggerType.PointerExit);

            AddEventTriggerEntry(trigger, EventTriggerType.PointerEnter, () =>
            {
                hoveredButton = owningButton;
                RefreshRowVisualStates();
            });
            AddEventTriggerEntry(trigger, EventTriggerType.PointerExit, () =>
            {
                if (hoveredButton == owningButton)
                {
                    hoveredButton = null;
                }

                RefreshRowVisualStates();
            });
        }

        private static EventTrigger GetOrCreateEventTrigger(GameObject target)
        {
            EventTrigger trigger = target.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = target.AddComponent<EventTrigger>();
            }

            trigger.triggers ??= new List<EventTrigger.Entry>();
            return trigger;
        }

        private static void RemoveEventTriggerEntries(EventTrigger trigger, params EventTriggerType[] eventTypes)
        {
            if (trigger?.triggers == null || eventTypes == null || eventTypes.Length == 0)
            {
                return;
            }

            trigger.triggers.RemoveAll(entry => entry != null && eventTypes.Contains(entry.eventID));
        }

        private static void AddEventTriggerEntry(EventTrigger trigger, EventTriggerType eventType, System.Action action)
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

        private void ApplyRowVisualState(RowVisualState rowState, bool highlighted)
        {
            if (rowState == null || rowState.Button == null)
            {
                return;
            }

            if (rowState.Background != null)
            {
                rowState.Background.color = highlighted ? BackgroundHighlightColor : rowColor;
            }

            if (rowState.Label == null)
            {
                return;
            }

            rowState.Label.text = rowState.BaseLabel;
            rowState.Label.color = highlighted ? TextHighlightColor : TextNormalColor;
            rowState.Label.fontStyle = highlighted ? FontStyles.Bold : FontStyles.Normal;
        }

        private static bool IsRowSelected(Button rowButton)
        {
            if (rowButton == null || EventSystem.current == null)
            {
                return false;
            }

            GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
            return currentSelected != null
                && (currentSelected == rowButton.gameObject
                    || currentSelected.transform.IsChildOf(rowButton.transform));
        }

        private void RefreshRowVisualStates()
        {
            foreach (RowVisualState rowState in rowStates)
            {
                if (rowState?.Button == null)
                {
                    continue;
                }

                bool highlighted = rowState.Button == hoveredButton || IsRowSelected(rowState.Button);
                ApplyRowVisualState(rowState, highlighted);
            }
        }

        private void ConfigureScrollableList(int rowCount)
        {
            if (content == null || rowTemplate == null)
            {
                return;
            }

            NormalizeContentRect();
            NormalizeViewportRect();

            float rowHeight = GetRowHeight();
            float spacing = layoutGroup != null ? layoutGroup.spacing : 0f;
            RectOffset padding = layoutGroup != null ? layoutGroup.padding : new RectOffset();

            float contentHeight = padding.top + padding.bottom;
            if (rowCount > 0)
            {
                contentHeight += rowCount * rowHeight + Mathf.Max(0, rowCount - 1) * spacing;
            }

            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);

            bool shouldScroll = rowCount > visibleRows;
            if (scrollRect != null)
            {
                scrollRect.content = content;
                if (viewport != null)
                {
                    scrollRect.viewport = viewport;
                }

                if (verticalScrollbar != null)
                {
                    scrollRect.verticalScrollbar = verticalScrollbar;
                }

                scrollRect.vertical = shouldScroll;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.scrollSensitivity = Mathf.Max(scrollRect.scrollSensitivity, rowHeight * 0.6f);
            }

            if (verticalScrollbar != null)
            {
                verticalScrollbar.interactable = shouldScroll;
                verticalScrollbar.gameObject.SetActive(shouldScroll);
            }

            ApplyVisibleRowSizing();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            if (viewport != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
            }
        }

        public void RefreshLayoutNow()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            ConfigureScrollableList(lastRowCount);
            ResetScrollPosition();
        }

        private void ResetScrollPosition()
        {
            if (scrollRect == null || content == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            content.anchoredPosition = Vector2.zero;
            scrollRect.verticalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();
            content.anchoredPosition = Vector2.zero;
        }

        private void NormalizeContentRect()
        {
            if (content == null)
            {
                return;
            }

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
        }

        private void NormalizeViewportRect()
        {
            if (viewport == null)
            {
                return;
            }

            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.anchoredPosition = Vector2.zero;
        }

        private float GetRowHeight()
        {
            float fittedRowHeight = GetViewportFittedRowHeight();
            if (fittedRowHeight > 0f)
            {
                return fittedRowHeight;
            }

            if (rowTemplate == null)
            {
                return 40f;
            }

            LayoutElement layoutElement = rowTemplate.GetComponent<LayoutElement>();
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

            RectTransform rowTemplateRect = rowTemplate.GetComponent<RectTransform>();
            if (rowTemplateRect == null)
            {
                return 40f;
            }

            float height = rowTemplateRect.rect.height;
            return height > 0f ? height : 40f;
        }

        private void ApplyVisibleRowSizing()
        {
            float fittedRowHeight = GetViewportFittedRowHeight();
            if (fittedRowHeight <= 0f)
            {
                return;
            }

            foreach (Button row in spawnedRows)
            {
                if (row == null)
                {
                    continue;
                }

                LayoutElement layoutElement = row.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = row.gameObject.AddComponent<LayoutElement>();
                }

                layoutElement.minHeight = fittedRowHeight;
                layoutElement.preferredHeight = fittedRowHeight;
                layoutElement.flexibleHeight = 0f;
            }
        }

        private float GetViewportFittedRowHeight()
        {
            if (viewport == null || visibleRows <= 0)
            {
                return -1f;
            }

            float viewportHeight = viewport.rect.height;
            if (viewportHeight <= 0f)
            {
                return -1f;
            }

            float spacing = layoutGroup != null ? layoutGroup.spacing : 0f;
            RectOffset padding = layoutGroup != null ? layoutGroup.padding : new RectOffset();
            float availableHeight = viewportHeight - padding.top - padding.bottom - Mathf.Max(0, visibleRows - 1) * spacing;
            if (availableHeight <= 0f)
            {
                return -1f;
            }

            return availableHeight / visibleRows;
        }

        private void RegisterScrollCallbacks()
        {
            if (scrollRect != null)
            {
                scrollRect.onValueChanged.RemoveListener(OnScrollRectValueChanged);
                scrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
            }

            if (verticalScrollbar != null)
            {
                verticalScrollbar.onValueChanged.RemoveListener(OnScrollbarValueChanged);
                verticalScrollbar.onValueChanged.AddListener(OnScrollbarValueChanged);
            }
        }

        private void UnregisterScrollCallbacks()
        {
            if (scrollRect != null)
            {
                scrollRect.onValueChanged.RemoveListener(OnScrollRectValueChanged);
            }

            if (verticalScrollbar != null)
            {
                verticalScrollbar.onValueChanged.RemoveListener(OnScrollbarValueChanged);
            }
        }

        private void OnScrollRectValueChanged(Vector2 normalizedPosition)
        {
            if (verticalScrollbar == null || isSynchronizingScroll)
            {
                return;
            }

            isSynchronizingScroll = true;
            verticalScrollbar.SetValueWithoutNotify(normalizedPosition.y);
            isSynchronizingScroll = false;
        }

        private void OnScrollbarValueChanged(float value)
        {
            if (scrollRect == null || isSynchronizingScroll)
            {
                return;
            }

            isSynchronizingScroll = true;
            scrollRect.verticalNormalizedPosition = value;
            isSynchronizingScroll = false;
        }
    }
}

