using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Windy.Srpg.Game.UI;

namespace Windy.Srpg.Game.Editor
{
    internal static class OverworldMenuSceneBuilder
    {
        private const string OverworldScenePath = "Assets/Scenes/OverworldMenu.unity";
        private const string Level0ScenePath = "Assets/Scenes/Level/Level0.unity";
        private const string Level1ScenePath = "Assets/Scenes/Level/Level1.unity";

        [MenuItem("Tools/Windy SRPG/Create Overworld Menu Scene")]
        private static void CreateOverworldMenuScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera camera = CreateCamera();
            Canvas canvas = CreateCanvas();
            CreateEventSystem();

            GameObject controllerObject = new GameObject("Overworld Menu UI");
            OverworldMenuUI overworldMenu = controllerObject.AddComponent<OverworldMenuUI>();

            RectTransform rootPanel = CreatePanel("Root Panel", canvas.transform, new Vector2(0f, 0f), new Vector2(520f, 520f), new Color(0.09f, 0.12f, 0.16f, 0.94f));
            rootPanel.anchorMin = new Vector2(0.5f, 0.5f);
            rootPanel.anchorMax = new Vector2(0.5f, 0.5f);
            rootPanel.pivot = new Vector2(0.5f, 0.5f);

            CreateText(rootPanel, "Overworld Menu", new Vector2(0f, -28f), new Vector2(460f, 42f), 30f, FontStyle.Bold, TextAnchor.MiddleCenter);

            RectTransform scrollRoot = CreatePanel("Level Scroll View", rootPanel, new Vector2(40f, -92f), new Vector2(440f, 320f), new Color(0f, 0f, 0f, 0.16f));
            ScrollRect scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            RectTransform viewport = CreatePanel("Viewport", scrollRoot, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.04f));
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            RectTransform content = CreatePanel("Content", viewport, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(12f, 0f);
            content.offsetMax = new Vector2(-12f, 0f);

            VerticalLayoutGroup layoutGroup = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(8, 8, 8, 8);
            layoutGroup.spacing = 8f;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = content;

            Button levelButtonTemplate = CreateButton(content, "Level Button Template", Vector2.zero, new Vector2(0f, 42f));
            LayoutElement layoutElement = levelButtonTemplate.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 42f;
            layoutElement.minHeight = 36f;
            levelButtonTemplate.gameObject.SetActive(false);

            CreateText(rootPanel, string.Empty, new Vector2(40f, -430f), new Vector2(440f, 28f), 18f, FontStyle.Normal, TextAnchor.MiddleCenter);
            Button quitButton = CreateButton(rootPanel, "Quit", new Vector2(180f, -462f), new Vector2(160f, 34f));

            overworldMenu.EditorConfigure(canvas, rootPanel, content, levelButtonTemplate, quitButton);
            EditorUtility.SetDirty(overworldMenu);
            _ = camera;

            EditorSceneManager.SaveScene(scene, OverworldScenePath);
            AddScenesToBuildSettings(OverworldScenePath, Level0ScenePath, Level1ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Overworld Menu Scene Builder: Created {OverworldScenePath} and added it with Level0/Level1 to Build Settings.");
        }

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);
            camera.orthographic = true;
            return camera;
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void AddScenesToBuildSettings(params string[] scenePaths)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            foreach (string scenePath in scenePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                EditorBuildSettingsScene existingScene = scenes.FirstOrDefault(scene => string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase));
                if (existingScene != null)
                {
                    existingScene.enabled = true;
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static RectTransform CreatePanel(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Color color)
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

        private static Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 size)
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
            CreateText(rectTransform, label, Vector2.zero, size, 18f, FontStyle.Normal, TextAnchor.MiddleCenter);
            return button;
        }

        private static Text CreateText(Transform parent, string content, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyle fontStyle, TextAnchor alignment)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Text text = textObject.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.RoundToInt(fontSize);
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }
    }
}
