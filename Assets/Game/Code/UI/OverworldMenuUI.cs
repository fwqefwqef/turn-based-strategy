using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Windy.Srpg.Game.Campaign;
using Windy.Srpg.Game.Localization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Windy.Srpg.Game.UI
{
    [AddComponentMenu("UI/Overworld Menu UI")]
    public sealed class OverworldMenuUI : MonoBehaviour
    {
        private const string LevelSceneFolderPath = "Assets/Scenes/Level";
        private const string ChapterDataTypeIdentifier = "Windy.Srpg.Game.Chapters.ChapterData";
        private static readonly Color DisabledLevelButtonColor = new Color(0.58f, 0.58f, 0.58f, 0.85f);
        private static readonly Color DisabledLevelTextColor = new Color(0.28f, 0.28f, 0.28f, 0.9f);

        [Serializable]
        public sealed class LevelSceneEntry
        {
            public string DisplayName;
            public string ScenePath;
            public string ChapterName;
            public float ChapterId;
            public bool Replayable = true;
            public float UnlockRequiredChapterId;

            public LevelSceneEntry(string displayName, string scenePath)
            {
                DisplayName = displayName;
                ScenePath = scenePath;
                ChapterName = displayName;
            }

            public LevelSceneEntry(string chapterName, float chapterId, string scenePath, bool replayable, float unlockRequiredChapterId)
            {
                DisplayName = chapterName;
                ChapterName = chapterName;
                ChapterId = chapterId;
                ScenePath = scenePath;
                Replayable = replayable;
                UnlockRequiredChapterId = unlockRequiredChapterId;
            }
        }

        [Header("Scene List")]
        [SerializeField] private List<LevelSceneEntry> levelScenes = new List<LevelSceneEntry>
        {
            new LevelSceneEntry("Chapter 1", 1f, "Assets/Scenes/Level/Chapter 1.unity", replayable: false, unlockRequiredChapterId: 0f),
            new LevelSceneEntry("Free Battle 1", 1.5f, "Assets/Scenes/Level/Free Battle 1.unity", replayable: true, unlockRequiredChapterId: 1f),
            new LevelSceneEntry("Chapter 2", 2f, "Assets/Scenes/Level/Chapter 2.unity", replayable: false, unlockRequiredChapterId: 1f)
        };

        [Header("References")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform rootPanel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text emptyText;
        [SerializeField] private RectTransform levelListContent;
        [SerializeField] private Button levelButtonTemplate;
        [SerializeField] private Button quitButton;
        [SerializeField] private bool autoGenerateUiIfMissing = true;

        private readonly List<Button> generatedLevelButtons = new List<Button>();
        private CampaignSaveData campaignSave;

        private void Awake()
        {
            EnsureUiExists();
            HookButtons();
            RefreshLevelScenesFromProject();
            campaignSave = CampaignSaveManager.Load() ?? new CampaignSaveData();
            RebuildLevelList();
        }

        private void OnDestroy()
        {
            UnhookButtons();
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Canvas sceneCanvas,
            RectTransform sceneRootPanel,
            RectTransform sceneLevelListContent,
            Button sceneLevelButtonTemplate,
            Button sceneQuitButton)
        {
            canvas = sceneCanvas;
            rootPanel = sceneRootPanel;
            levelListContent = sceneLevelListContent;
            levelButtonTemplate = sceneLevelButtonTemplate;
            quitButton = sceneQuitButton;
        }
#endif

        private void EnsureUiExists()
        {
            if (HasAuthoredUi())
            {
                levelButtonTemplate.gameObject.SetActive(false);
                return;
            }

            if (!autoGenerateUiIfMissing)
            {
                enabled = false;
                return;
            }

            BuildFallbackUi();
        }

        private bool HasAuthoredUi()
        {
            return rootPanel != null
                && levelListContent != null
                && levelButtonTemplate != null;
        }

        private void HookButtons()
        {
            if (quitButton != null)
            {
                quitButton.onClick.AddListener(QuitGame);
            }
        }

        private void UnhookButtons()
        {
            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(QuitGame);
            }

            foreach (Button button in generatedLevelButtons)
            {
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                }
            }
        }

        private void RebuildLevelList()
        {
            if (titleText != null)
            {
                titleText.text = GameTextCatalog.Get("ui.overworld.title", "Overworld Menu");
            }

            ClearGeneratedButtons();
            if (levelListContent == null || levelButtonTemplate == null)
            {
                return;
            }

            levelButtonTemplate.gameObject.SetActive(false);

            List<LevelSceneEntry> validEntries = levelScenes
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.ScenePath))
                .OrderBy(entry => entry.ChapterId)
                .ThenBy(entry => BuildLevelLabel(entry), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(validEntries.Count == 0);
                emptyText.text = GameTextCatalog.Get("ui.overworld.no_levels", "No levels available.");
            }

            foreach (LevelSceneEntry entry in validEntries)
            {
                LevelSceneEntry capturedEntry = entry;
                bool isCleared = CampaignProgressUtility.IsChapterCleared(campaignSave, capturedEntry.ChapterId);
                bool canEnter = CampaignProgressUtility.CanEnterChapter(
                    campaignSave,
                    capturedEntry.ChapterId,
                    capturedEntry.Replayable,
                    capturedEntry.UnlockRequiredChapterId);

                Button button = Instantiate(levelButtonTemplate, levelListContent, false);
                button.name = $"Level Button - {BuildLevelLabel(capturedEntry)}";
                button.gameObject.SetActive(true);
                button.onClick.RemoveAllListeners();
                if (canEnter)
                {
                    button.onClick.AddListener(() => LoadLevel(capturedEntry));
                }

                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = BuildLevelLabel(capturedEntry, isCleared);
                }
                else
                {
                    Text legacyLabel = button.GetComponentInChildren<Text>(true);
                    if (legacyLabel != null)
                    {
                        legacyLabel.text = BuildLevelLabel(capturedEntry, isCleared);
                    }
                }

                ApplyLevelButtonAvailability(button, canEnter);
                generatedLevelButtons.Add(button);
            }
        }

        private static void ApplyLevelButtonAvailability(Button button, bool canEnter)
        {
            if (button == null)
            {
                return;
            }

            ColorBlock colors = button.colors;
            colors.disabledColor = DisabledLevelButtonColor;
            button.colors = colors;
            button.interactable = canEnter;

            if (!canEnter && button.targetGraphic != null)
            {
                button.targetGraphic.color = DisabledLevelButtonColor;
            }

            TMP_Text tmpLabel = button.GetComponentInChildren<TMP_Text>(true);
            if (tmpLabel != null)
            {
                if (!canEnter)
                {
                    tmpLabel.color = DisabledLevelTextColor;
                }

                return;
            }

            Text legacyLabel = button.GetComponentInChildren<Text>(true);
            if (legacyLabel != null)
            {
                if (!canEnter)
                {
                    legacyLabel.color = DisabledLevelTextColor;
                }
            }
        }

        private void LoadLevel(LevelSceneEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.ScenePath))
            {
                return;
            }

            try
            {
                SceneManager.LoadScene(entry.ScenePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"OverworldMenuUI: Failed to load scene '{entry.ScenePath}'. Make sure it is in Build Settings. {ex.Message}", this);
            }
        }

        private static string BuildLevelLabel(LevelSceneEntry entry, bool isCleared = false)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            string label = !string.IsNullOrWhiteSpace(entry.ChapterName)
                ? entry.ChapterName
                : entry.DisplayName;

            if (string.IsNullOrWhiteSpace(label))
            {
                label = Path.GetFileNameWithoutExtension(entry.ScenePath) ?? entry.ScenePath;
            }

            return isCleared ? $"\u2713 {label}" : label;
        }

        private void RefreshLevelScenesFromProject()
        {
#if UNITY_EDITOR
            List<LevelSceneEntry> discoveredEntries = DiscoverLevelSceneEntriesInEditor();
            if (discoveredEntries.Count > 0)
            {
                levelScenes = discoveredEntries;
                EnsureDiscoveredScenesAreInBuildSettings(discoveredEntries.Select(entry => entry.ScenePath));
            }
#endif
        }

#if UNITY_EDITOR
        private static List<LevelSceneEntry> DiscoverLevelSceneEntriesInEditor()
        {
            if (!AssetDatabase.IsValidFolder(LevelSceneFolderPath))
            {
                return new List<LevelSceneEntry>();
            }

            return AssetDatabase.FindAssets("t:Scene", new[] { LevelSceneFolderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(BuildLevelSceneEntryFromSceneAsset)
                .Where(entry => entry != null)
                .OrderBy(entry => entry.ChapterId)
                .ThenBy(entry => BuildLevelLabel(entry), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static LevelSceneEntry BuildLevelSceneEntryFromSceneAsset(string scenePath)
        {
            string fallbackName = Path.GetFileNameWithoutExtension(scenePath) ?? scenePath;
            if (!File.Exists(scenePath))
            {
                return new LevelSceneEntry(fallbackName, scenePath);
            }

            string sceneText = File.ReadAllText(scenePath);
            int chapterDataIndex = sceneText.IndexOf(ChapterDataTypeIdentifier, StringComparison.Ordinal);
            if (chapterDataIndex < 0)
            {
                return new LevelSceneEntry(fallbackName, scenePath);
            }

            string chapterDataBlock = sceneText.Substring(chapterDataIndex);
            string chapterName = ReadYamlString(chapterDataBlock, "chapterName");
            float chapterId = ReadYamlFloat(chapterDataBlock, "chapterId", 0f);
            bool replayable = ReadYamlBool(chapterDataBlock, "replayable", true);
            float unlockRequiredChapterId = ReadYamlFloat(chapterDataBlock, "unlockRequiredChapterId", 0f);

            if (string.IsNullOrWhiteSpace(chapterName))
            {
                chapterName = fallbackName;
            }

            return new LevelSceneEntry(chapterName, chapterId, scenePath, replayable, unlockRequiredChapterId);
        }

        private static string ReadYamlString(string yamlText, string fieldName)
        {
            Match match = Regex.Match(yamlText, @"^\s*" + Regex.Escape(fieldName) + @":\s*(.*)$", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private static float ReadYamlFloat(string yamlText, string fieldName, float fallbackValue)
        {
            string rawValue = ReadYamlString(yamlText, fieldName);
            return float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                ? Mathf.Max(0f, value)
                : fallbackValue;
        }

        private static bool ReadYamlBool(string yamlText, string fieldName, bool fallbackValue)
        {
            string rawValue = ReadYamlString(yamlText, fieldName);
            return rawValue switch
            {
                "0" => false,
                "1" => true,
                _ => fallbackValue
            };
        }

        private static void EnsureDiscoveredScenesAreInBuildSettings(IEnumerable<string> scenePaths)
        {
            List<EditorBuildSettingsScene> buildScenes = EditorBuildSettings.scenes.ToList();
            HashSet<string> existingPaths = new HashSet<string>(
                buildScenes.Select(scene => scene.path),
                StringComparer.OrdinalIgnoreCase);

            bool changed = false;
            foreach (string scenePath in scenePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                if (existingPaths.Contains(scenePath))
                {
                    continue;
                }

                buildScenes.Add(new EditorBuildSettingsScene(scenePath, enabled: true));
                existingPaths.Add(scenePath);
                changed = true;
            }

            if (changed)
            {
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }
        }
#endif

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ClearGeneratedButtons()
        {
            foreach (Button button in generatedLevelButtons)
            {
                if (button != null)
                {
                    Destroy(button.gameObject);
                }
            }

            generatedLevelButtons.Clear();
        }

        private void BuildFallbackUi()
        {
            EnsureCanvas();
            EnsureEventSystem();

            rootPanel = CreatePanel("Root Panel", canvas.transform, new Vector2(0f, 0f), new Vector2(520f, 520f), new Color(0.09f, 0.12f, 0.16f, 0.94f));
            rootPanel.anchorMin = new Vector2(0.5f, 0.5f);
            rootPanel.anchorMax = new Vector2(0.5f, 0.5f);
            rootPanel.pivot = new Vector2(0.5f, 0.5f);

            titleText = CreateText(rootPanel, GameTextCatalog.Get("ui.overworld.title", "Overworld Menu"), new Vector2(0f, -28f), new Vector2(460f, 42f), 30f, FontStyles.Bold, TextAlignmentOptions.Center);

            RectTransform scrollRoot = CreatePanel("Level Scroll View", rootPanel, new Vector2(40f, -92f), new Vector2(440f, 320f), new Color(0f, 0f, 0f, 0.16f));
            ScrollRect scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            RectTransform viewport = CreatePanel("Viewport", scrollRoot, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.04f));
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            levelListContent = CreatePanel("Content", viewport, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
            levelListContent.anchorMin = new Vector2(0f, 1f);
            levelListContent.anchorMax = new Vector2(1f, 1f);
            levelListContent.pivot = new Vector2(0.5f, 1f);
            levelListContent.offsetMin = new Vector2(12f, 0f);
            levelListContent.offsetMax = new Vector2(-12f, 0f);

            VerticalLayoutGroup layoutGroup = levelListContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(8, 8, 8, 8);
            layoutGroup.spacing = 8f;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            ContentSizeFitter fitter = levelListContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = levelListContent;

            levelButtonTemplate = CreateTemplateButton(levelListContent, "Level Button Template");
            levelButtonTemplate.gameObject.SetActive(false);

            emptyText = CreateText(rootPanel, string.Empty, new Vector2(40f, -430f), new Vector2(440f, 28f), 18f, FontStyles.Normal, TextAlignmentOptions.Center);
            quitButton = CreateButton(rootPanel, GameTextCatalog.Get("ui.overworld.button_quit", "Quit"), new Vector2(180f, -462f), new Vector2(160f, 34f));
        }

        private void EnsureCanvas()
        {
            if (canvas != null)
            {
                return;
            }

            canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private Button CreateTemplateButton(Transform parent, string name)
        {
            Button button = CreateButton(parent, name, Vector2.zero, new Vector2(0f, 42f));
            LayoutElement layoutElement = button.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 42f;
            layoutElement.minHeight = 36f;
            return button;
        }

        private Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 size)
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
            image.color = new Color(0.88f, 0.9f, 0.92f, 0.96f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            TMP_Text labelText = CreateText(rectTransform, label, Vector2.zero, size, 18f, FontStyles.Normal, TextAlignmentOptions.Center);
            StretchButtonLabel(labelText.transform as RectTransform);
            return button;
        }

        private static void StretchButtonLabel(RectTransform labelRectTransform)
        {
            if (labelRectTransform == null)
            {
                return;
            }

            labelRectTransform.anchorMin = Vector2.zero;
            labelRectTransform.anchorMax = Vector2.one;
            labelRectTransform.pivot = new Vector2(0.5f, 0.5f);
            labelRectTransform.anchoredPosition = Vector2.zero;
            labelRectTransform.offsetMin = new Vector2(8f, 0f);
            labelRectTransform.offsetMax = new Vector2(-8f, 0f);
        }

        private RectTransform CreatePanel(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);

            RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Image image = panelObject.GetComponent<Image>();
            image.color = color;
            return rectTransform;
        }

        private TMP_Text CreateText(Transform parent, string content, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
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
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }
    }
}
