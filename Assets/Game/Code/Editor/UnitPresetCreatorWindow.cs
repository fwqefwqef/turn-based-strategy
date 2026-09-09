using System.IO;
using UnityEditor;
using UnityEngine;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Editor
{
    public sealed class UnitPresetCreatorWindow : EditorWindow
    {
        private const string DefaultEnemyPresetFolder = "Assets/Game/Data/Preset Data (Unit, Tile)/enemy preset";
        private const string DefaultFriendlyPresetFolder = "Assets/Game/Data/Preset Data (Unit, Tile)/friendly preset";

        [SerializeField] private UnitPreset importPreset;
        [SerializeField] private string outputFolder = DefaultEnemyPresetFolder;
        [SerializeField] private string assetFileName = "NewUnitPreset";

        private UnitPreset draftPreset;
        private Vector2 scrollPosition;

        [MenuItem("Tools/Windy SRPG/Unit Preset Creator")]
        private static void OpenWindow()
        {
            GetWindow<UnitPresetCreatorWindow>("Unit Preset Creator");
        }

        private void OnEnable()
        {
            EnsureDraftPreset();
        }

        private void OnDisable()
        {
            if (draftPreset != null)
            {
                DestroyImmediate(draftPreset);
                draftPreset = null;
            }
        }

        private void OnGUI()
        {
            EnsureDraftPreset();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawImportSection();
            EditorGUILayout.Space(8f);
            DrawOutputSection();
            EditorGUILayout.Space(8f);
            DrawDraftInspector();
            EditorGUILayout.EndScrollView();
        }

        private void DrawImportSection()
        {
            EditorGUILayout.LabelField("Import", EditorStyles.boldLabel);
            importPreset = (UnitPreset)EditorGUILayout.ObjectField("Existing Preset", importPreset, typeof(UnitPreset), false);

            using (new EditorGUI.DisabledScope(importPreset == null))
            {
                if (GUILayout.Button("Import Existing Preset"))
                {
                    ImportExistingPreset();
                }
            }

            if (GUILayout.Button("New Blank Preset"))
            {
                CreateBlankDraftPreset();
            }
        }

        private void DrawOutputSection()
        {
            EditorGUILayout.LabelField("Save", EditorStyles.boldLabel);
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            assetFileName = EditorGUILayout.TextField("Asset File Name", assetFileName);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Enemy Folder"))
            {
                outputFolder = DefaultEnemyPresetFolder;
            }

            if (GUILayout.Button("Use Friendly Folder"))
            {
                outputFolder = DefaultFriendlyPresetFolder;
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Save As New Unit Preset"))
            {
                SaveAsNewPreset();
            }
        }

        private void DrawDraftInspector()
        {
            EditorGUILayout.LabelField("Preset Draft", EditorStyles.boldLabel);
            SerializedObject serializedDraft = new SerializedObject(draftPreset);
            serializedDraft.Update();

            SerializedProperty property = serializedDraft.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(property, includeChildren: true);
                    }

                    continue;
                }

                EditorGUILayout.PropertyField(property, includeChildren: true);
            }

            serializedDraft.ApplyModifiedProperties();
        }

        private void ImportExistingPreset()
        {
            EnsureDraftPreset();
            if (importPreset == null)
            {
                return;
            }

            EditorUtility.CopySerialized(importPreset, draftPreset);
            draftPreset.name = $"{importPreset.name} Draft";
            assetFileName = $"{importPreset.name} Copy";
        }

        private void CreateBlankDraftPreset()
        {
            if (draftPreset != null)
            {
                DestroyImmediate(draftPreset);
            }

            draftPreset = CreateInstance<UnitPreset>();
            draftPreset.name = "Unit Preset Draft";
            draftPreset.PresetId = "unit_preset";
            draftPreset.UnitName = "Unit";
            draftPreset.BaseLevel = 1;
            assetFileName = "NewUnitPreset";
        }

        private void EnsureDraftPreset()
        {
            if (draftPreset != null)
            {
                return;
            }

            CreateBlankDraftPreset();
        }

        private void SaveAsNewPreset()
        {
            EnsureDraftPreset();

            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                Debug.LogError("Unit Preset Creator: Output folder is empty.");
                return;
            }

            if (!EnsureAssetFolder(outputFolder))
            {
                return;
            }

            string sanitizedFileName = SanitizeFileName(assetFileName);
            if (string.IsNullOrWhiteSpace(sanitizedFileName))
            {
                sanitizedFileName = "UnitPreset";
            }

            if (!sanitizedFileName.EndsWith(".asset"))
            {
                sanitizedFileName += ".asset";
            }

            string normalizedOutputFolder = outputFolder.Replace('\\', '/').TrimEnd('/');
            UnitPreset savedPreset = CreateInstance<UnitPreset>();
            EditorUtility.CopySerialized(draftPreset, savedPreset);
            savedPreset.name = Path.GetFileNameWithoutExtension(sanitizedFileName);

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{normalizedOutputFolder}/{sanitizedFileName}");
            AssetDatabase.CreateAsset(savedPreset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = savedPreset;
            EditorGUIUtility.PingObject(savedPreset);
            Debug.Log($"Unit Preset Creator: Saved '{assetPath}'.");
        }

        private static bool EnsureAssetFolder(string folderPath)
        {
            string normalizedPath = folderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalizedPath))
            {
                return true;
            }

            string[] parts = normalizedPath.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                Debug.LogError($"Unit Preset Creator: Output folder must be inside Assets. Current value: '{folderPath}'.");
                return false;
            }

            string currentPath = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string nextPath = $"{currentPath}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, parts[i]);
                }

                currentPath = nextPath;
            }

            return AssetDatabase.IsValidFolder(normalizedPath);
        }

        private static string SanitizeFileName(string fileName)
        {
            string sanitizedFileName = string.IsNullOrWhiteSpace(fileName)
                ? "UnitPreset"
                : fileName.Trim();

            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                sanitizedFileName = sanitizedFileName.Replace(invalidCharacter, '_');
            }

            return sanitizedFileName;
        }
    }
}
