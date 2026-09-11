using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Windy.Srpg.Game.Grid;
using Object = UnityEngine.Object;

namespace Windy.Srpg.Game.Editor
{
    public sealed class SceneSystemSyncWindow : EditorWindow
    {
        private const string DefaultSourceScenePath = "Assets/Scenes/PaintedMap.unity";
        private const string DefaultLevelSceneFolder = "Assets/Scenes/Level";
        private const string DefaultPreservedRootNames =
            "CellGrid\n" +
            "Units\n" +
            "Friendly Deployment Slots\n" +
            "ReinforcementTiles";

        [SerializeField] private SceneAsset sourceScene;
        [SerializeField] private string preservedRootNames = DefaultPreservedRootNames;
        [SerializeField] private bool syncComponentsOnPreservedRoots = true;
        [SerializeField] private bool removeExtraNonMapRootObjects;

        private Vector2 scrollPosition;

        private static void OpenWindow()
        {
            GetWindow<SceneSystemSyncWindow>("Scene System Sync");
        }

        private static void SyncFromPaintedMap()
        {
            SyncSceneSystems(
                DefaultSourceScenePath,
                ParseRootNames(DefaultPreservedRootNames),
                syncComponentsOnPreservedRoots: true,
                removeExtraNonMapRootObjects: false);
        }

        [MenuItem("Tools/Windy SRPG/Sync All Level Scenes From PaintedMap")]
        private static void SyncAllLevelScenesFromPaintedMap()
        {
            SyncLevelScenesFromSource(
                DefaultSourceScenePath,
                DefaultLevelSceneFolder,
                ParseRootNames(DefaultPreservedRootNames),
                syncComponentsOnPreservedRoots: true,
                removeExtraNonMapRootObjects: false);
        }

        private static void RepairCurrentSceneMissingMapPrefabLinks()
        {
            Scene targetScene = SceneManager.GetActiveScene();
            if (!targetScene.IsValid() || string.IsNullOrWhiteSpace(targetScene.path))
            {
                Debug.LogWarning("Scene System Sync: Save the active scene before repairing missing map prefab links.");
                return;
            }

            int repairedCount = UnpackMissingPrefabInstances(targetScene, ParseRootNames(DefaultPreservedRootNames));
            if (repairedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(targetScene);
            }

            Debug.Log($"Scene System Sync: Repaired {repairedCount} missing prefab instance link(s) in '{targetScene.path}'.");
        }

        private void OnEnable()
        {
            sourceScene ??= AssetDatabase.LoadAssetAtPath<SceneAsset>(DefaultSourceScenePath);
            if (string.IsNullOrWhiteSpace(preservedRootNames))
            {
                preservedRootNames = DefaultPreservedRootNames;
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("Scene System Sync", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Copies reusable scene systems from a source scene into the active scene while preserving map roots. " +
                "Default source is PaintedMap.unity. Default preserved roots are CellGrid, Units, Friendly Deployment Slots, and ReinforcementTiles. " +
                "Chapter Data should stay on its own root object so it copies from the source scene.",
                MessageType.Info);

            sourceScene = (SceneAsset)EditorGUILayout.ObjectField("Source Scene", sourceScene, typeof(SceneAsset), false);

            EditorGUILayout.LabelField("Preserved Map Root Names", EditorStyles.boldLabel);
            preservedRootNames = EditorGUILayout.TextArea(preservedRootNames, GUILayout.MinHeight(72f));

            syncComponentsOnPreservedRoots = EditorGUILayout.Toggle(
                "Sync Root Components",
                syncComponentsOnPreservedRoots);

            EditorGUILayout.HelpBox(
                "Root component sync updates components on preserved roots, such as CellGrid settings, without replacing their children. " +
                "MapPainterSceneContext is skipped so map bounds and map metadata stay local to the current scene.",
                MessageType.None);

            removeExtraNonMapRootObjects = EditorGUILayout.Toggle(
                "Remove Extra Non-Map Roots",
                removeExtraNonMapRootObjects);

            EditorGUILayout.HelpBox(
                "When enabled, non-preserved root objects that do not exist in the source scene are removed from the active scene.",
                MessageType.Warning);

            using (new EditorGUI.DisabledScope(sourceScene == null))
            {
                if (GUILayout.Button("Sync Active Scene From Source"))
                {
                    string sourcePath = AssetDatabase.GetAssetPath(sourceScene);
                    SyncSceneSystems(
                        sourcePath,
                        ParseRootNames(preservedRootNames),
                        syncComponentsOnPreservedRoots,
                        removeExtraNonMapRootObjects);
                }

                if (GUILayout.Button("Sync All Level Scenes From Source"))
                {
                    string sourcePath = AssetDatabase.GetAssetPath(sourceScene);
                    SyncLevelScenesFromSource(
                        sourcePath,
                        DefaultLevelSceneFolder,
                        ParseRootNames(preservedRootNames),
                        syncComponentsOnPreservedRoots,
                        removeExtraNonMapRootObjects);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static void SyncLevelScenesFromSource(
            string sourceScenePath,
            string levelSceneFolder,
            HashSet<string> preservedRootNameSet,
            bool syncComponentsOnPreservedRoots,
            bool removeExtraNonMapRootObjects)
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Scene System Sync: Stop Play Mode before syncing level scenes.");
                return;
            }

            if (string.IsNullOrWhiteSpace(sourceScenePath))
            {
                Debug.LogWarning("Scene System Sync: No source scene selected.");
                return;
            }

            string[] levelScenePaths = FindLevelScenePaths(levelSceneFolder, sourceScenePath);
            if (levelScenePaths.Length == 0)
            {
                Debug.LogWarning($"Scene System Sync: No level scenes were found under '{levelSceneFolder}'.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene sourceScene = default;
            bool sourceSceneOpenedByTool = false;
            Scene originalActiveScene = SceneManager.GetActiveScene();
            int syncedCount = 0;

            try
            {
                sourceScene = FindLoadedScene(sourceScenePath);
                if (!sourceScene.IsValid() || !sourceScene.isLoaded)
                {
                    sourceScene = EditorSceneManager.OpenScene(sourceScenePath, OpenSceneMode.Additive);
                    sourceSceneOpenedByTool = true;
                }

                for (int i = 0; i < levelScenePaths.Length; i++)
                {
                    string targetScenePath = levelScenePaths[i];
                    EditorUtility.DisplayProgressBar(
                        "Scene System Sync",
                        $"Syncing {targetScenePath}",
                        levelScenePaths.Length == 0 ? 1f : i / (float)levelScenePaths.Length);

                    Scene targetScene = FindLoadedScene(targetScenePath);
                    bool targetSceneOpenedByTool = false;
                    if (!targetScene.IsValid() || !targetScene.isLoaded)
                    {
                        targetScene = EditorSceneManager.OpenScene(targetScenePath, OpenSceneMode.Additive);
                        targetSceneOpenedByTool = true;
                    }

                    try
                    {
                        SyncLoadedTargetScene(
                            sourceScene,
                            targetScene,
                            sourceScenePath,
                            preservedRootNameSet,
                            syncComponentsOnPreservedRoots,
                            removeExtraNonMapRootObjects);

                        EditorSceneManager.SaveScene(targetScene);
                        syncedCount++;
                    }
                    finally
                    {
                        if (targetSceneOpenedByTool && targetScene.IsValid() && targetScene.isLoaded)
                        {
                            EditorSceneManager.CloseScene(targetScene, removeScene: true);
                        }
                    }
                }

                Debug.Log($"Scene System Sync: Synced {syncedCount} level scene(s) from '{sourceScenePath}'.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                if (sourceSceneOpenedByTool && sourceScene.IsValid() && sourceScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(sourceScene, removeScene: true);
                }

                if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
                {
                    EditorSceneManager.SetActiveScene(originalActiveScene);
                }
            }
        }

        private static void SyncSceneSystems(
            string sourceScenePath,
            HashSet<string> preservedRootNameSet,
            bool syncComponentsOnPreservedRoots,
            bool removeExtraNonMapRootObjects)
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Scene System Sync: Stop Play Mode before syncing scene systems.");
                return;
            }

            if (string.IsNullOrWhiteSpace(sourceScenePath))
            {
                Debug.LogWarning("Scene System Sync: No source scene selected.");
                return;
            }

            Scene targetScene = SceneManager.GetActiveScene();
            if (!targetScene.IsValid() || string.IsNullOrWhiteSpace(targetScene.path))
            {
                Debug.LogWarning("Scene System Sync: Save the active scene before syncing.");
                return;
            }

            if (string.Equals(targetScene.path, sourceScenePath, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("Scene System Sync: The active scene is already the source scene.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene sourceScene = default;
            bool sourceSceneOpened = false;

            try
            {
                sourceScene = EditorSceneManager.OpenScene(sourceScenePath, OpenSceneMode.Additive);
                sourceSceneOpened = true;
                EditorSceneManager.SetActiveScene(targetScene);

                SyncLoadedTargetScene(
                    sourceScene,
                    targetScene,
                    sourceScenePath,
                    preservedRootNameSet,
                    syncComponentsOnPreservedRoots,
                    removeExtraNonMapRootObjects);
            }
            finally
            {
                if (sourceSceneOpened && sourceScene.IsValid() && sourceScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(sourceScene, removeScene: true);
                }

                EditorSceneManager.SetActiveScene(targetScene);
            }
        }

        private static void SyncLoadedTargetScene(
            Scene sourceScene,
            Scene targetScene,
            string sourceScenePath,
            HashSet<string> preservedRootNameSet,
            bool syncComponentsOnPreservedRoots,
            bool removeExtraNonMapRootObjects)
        {
            if (!sourceScene.IsValid() || !sourceScene.isLoaded || !targetScene.IsValid() || !targetScene.isLoaded)
            {
                return;
            }

            SyncOpenedScenes(
                sourceScene,
                targetScene,
                preservedRootNameSet,
                syncComponentsOnPreservedRoots,
                removeExtraNonMapRootObjects);

            int repairedMissingPrefabCount = UnpackMissingPrefabInstances(targetScene, preservedRootNameSet);
            EditorSceneManager.MarkSceneDirty(targetScene);
            Debug.Log(
                $"Scene System Sync: Synced '{targetScene.path}' from '{sourceScenePath}'." +
                (repairedMissingPrefabCount > 0
                    ? $" Repaired {repairedMissingPrefabCount} missing prefab instance link(s) on preserved map roots."
                    : string.Empty));
        }

        private static string[] FindLevelScenePaths(string levelSceneFolder, string sourceScenePath)
        {
            if (string.IsNullOrWhiteSpace(levelSceneFolder) || !AssetDatabase.IsValidFolder(levelSceneFolder))
            {
                return Array.Empty<string>();
            }

            return AssetDatabase.FindAssets("t:Scene", new[] { levelSceneFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Where(path => !string.Equals(path, sourceScenePath, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static Scene FindLoadedScene(string scenePath)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    return scene;
                }
            }

            return default;
        }

        private static int UnpackMissingPrefabInstances(Scene targetScene, HashSet<string> preservedRootNameSet)
        {
            int repairedCount = 0;
            foreach (GameObject root in targetScene.GetRootGameObjects())
            {
                if (root == null || !preservedRootNameSet.Contains(root.name))
                {
                    continue;
                }

                GameObject[] missingPrefabRoots = root
                    .GetComponentsInChildren<Transform>(true)
                    .Select(transform => transform.gameObject)
                    .Where(IsMissingPrefabOutermostRoot)
                    .Distinct()
                    .ToArray();

                foreach (GameObject missingPrefabRoot in missingPrefabRoots)
                {
                    PrefabUtility.UnpackPrefabInstance(
                        missingPrefabRoot,
                        PrefabUnpackMode.OutermostRoot,
                        InteractionMode.AutomatedAction);
                    repairedCount++;
                }
            }

            return repairedCount;
        }

        private static bool IsMissingPrefabOutermostRoot(GameObject gameObject)
        {
            return gameObject != null
                && PrefabUtility.GetPrefabAssetType(gameObject) == PrefabAssetType.MissingAsset
                && PrefabUtility.IsOutermostPrefabInstanceRoot(gameObject);
        }

        private static void SyncOpenedScenes(
            Scene sourceScene,
            Scene targetScene,
            HashSet<string> preservedRootNameSet,
            bool syncComponentsOnPreservedRoots,
            bool removeExtraNonMapRootObjects)
        {
            GameObject[] sourceRoots = sourceScene.GetRootGameObjects();
            GameObject[] targetRoots = targetScene.GetRootGameObjects();

            Dictionary<string, GameObject> sourceRootByName = BuildRootLookup(sourceRoots);
            Dictionary<string, GameObject> targetRootByName = BuildRootLookup(targetRoots);
            Dictionary<Object, Object> objectMap = new Dictionary<Object, Object>();
            List<GameObject> clonedRoots = new List<GameObject>();
            List<Component> remapComponents = new List<Component>();

            foreach (string preservedRootName in preservedRootNameSet)
            {
                if (!sourceRootByName.TryGetValue(preservedRootName, out GameObject sourceRoot) ||
                    !targetRootByName.TryGetValue(preservedRootName, out GameObject targetRoot))
                {
                    continue;
                }

                MapHierarchyByPath(sourceRoot.transform, targetRoot.transform, objectMap);
            }

            HashSet<string> sourceRootNames = new HashSet<string>(
                sourceRoots.Select(root => root.name),
                StringComparer.OrdinalIgnoreCase);

            foreach (GameObject targetRoot in targetRoots)
            {
                if (targetRoot == null || preservedRootNameSet.Contains(targetRoot.name))
                {
                    continue;
                }

                bool existsInSource = sourceRootNames.Contains(targetRoot.name);
                if (existsInSource || removeExtraNonMapRootObjects)
                {
                    Undo.DestroyObjectImmediate(targetRoot);
                }
            }

            foreach (GameObject sourceRoot in sourceRoots)
            {
                if (sourceRoot == null || preservedRootNameSet.Contains(sourceRoot.name))
                {
                    continue;
                }

                GameObject clonedRoot = Instantiate(sourceRoot);
                clonedRoot.name = sourceRoot.name;
                SceneManager.MoveGameObjectToScene(clonedRoot, targetScene);
                Undo.RegisterCreatedObjectUndo(clonedRoot, $"Sync {sourceRoot.name}");
                clonedRoot.transform.SetSiblingIndex(sourceRoot.transform.GetSiblingIndex());

                clonedRoots.Add(clonedRoot);
                MapHierarchyByPath(sourceRoot.transform, clonedRoot.transform, objectMap);
            }

            if (syncComponentsOnPreservedRoots)
            {
                foreach (string preservedRootName in preservedRootNameSet)
                {
                    if (!sourceRootByName.TryGetValue(preservedRootName, out GameObject sourceRoot) ||
                        !targetRootByName.TryGetValue(preservedRootName, out GameObject targetRoot))
                    {
                        continue;
                    }

                    SyncRootComponents(sourceRoot, targetRoot, objectMap, remapComponents);
                }
            }

            foreach (GameObject clonedRoot in clonedRoots)
            {
                remapComponents.AddRange(
                    clonedRoot
                        .GetComponentsInChildren<Component>(true)
                        .Where(component => component != null));
            }

            RemapObjectReferences(remapComponents, objectMap);
        }

        private static Dictionary<string, GameObject> BuildRootLookup(IEnumerable<GameObject> roots)
        {
            Dictionary<string, GameObject> lookup = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            foreach (GameObject root in roots)
            {
                if (root != null && !lookup.ContainsKey(root.name))
                {
                    lookup.Add(root.name, root);
                }
            }

            return lookup;
        }

        private static HashSet<string> ParseRootNames(string rawRootNames)
        {
            return new HashSet<string>(
                (rawRootNames ?? string.Empty)
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(name => name.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.OrdinalIgnoreCase);
        }

        private static void MapHierarchyByPath(Transform sourceRoot, Transform targetRoot, IDictionary<Object, Object> objectMap)
        {
            if (sourceRoot == null || targetRoot == null)
            {
                return;
            }

            MapTransformObjects(sourceRoot, targetRoot, objectMap);

            foreach (Transform sourceChild in sourceRoot)
            {
                Transform targetChild = targetRoot.Find(sourceChild.name);
                if (targetChild != null)
                {
                    MapHierarchyByPath(sourceChild, targetChild, objectMap);
                }
            }
        }

        private static void MapTransformObjects(Transform sourceTransform, Transform targetTransform, IDictionary<Object, Object> objectMap)
        {
            objectMap[sourceTransform.gameObject] = targetTransform.gameObject;
            objectMap[sourceTransform] = targetTransform;

            Component[] sourceComponents = sourceTransform.GetComponents<Component>();
            Component[] targetComponents = targetTransform.GetComponents<Component>();
            Dictionary<Type, int> occurrenceByType = new Dictionary<Type, int>();

            foreach (Component sourceComponent in sourceComponents)
            {
                if (sourceComponent == null)
                {
                    continue;
                }

                Type componentType = sourceComponent.GetType();
                occurrenceByType.TryGetValue(componentType, out int occurrence);
                occurrenceByType[componentType] = occurrence + 1;

                Component targetComponent = targetComponents
                    .Where(component => component != null && component.GetType() == componentType)
                    .Skip(occurrence)
                    .FirstOrDefault();

                if (targetComponent != null)
                {
                    objectMap[sourceComponent] = targetComponent;
                }
            }
        }

        private static void SyncRootComponents(
            GameObject sourceRoot,
            GameObject targetRoot,
            IDictionary<Object, Object> objectMap,
            ICollection<Component> remapComponents)
        {
            Component[] sourceComponents = sourceRoot.GetComponents<Component>();
            Component[] targetComponents = targetRoot.GetComponents<Component>();
            Dictionary<Type, int> sourceTypeCounts = CountSyncableComponentTypes(sourceComponents);
            Dictionary<Type, int> seenSourceTypeCounts = new Dictionary<Type, int>();
            HashSet<Component> syncedTargetComponents = new HashSet<Component>();

            foreach (Component sourceComponent in sourceComponents)
            {
                if (!CanSyncRootComponent(sourceComponent))
                {
                    continue;
                }

                Type componentType = sourceComponent.GetType();
                seenSourceTypeCounts.TryGetValue(componentType, out int occurrence);
                seenSourceTypeCounts[componentType] = occurrence + 1;

                Component targetComponent = targetComponents
                    .Where(component => component != null && component.GetType() == componentType)
                    .Skip(occurrence)
                    .FirstOrDefault();

                if (targetComponent == null)
                {
                    targetComponent = Undo.AddComponent(targetRoot, componentType);
                }

                Undo.RecordObject(targetComponent, $"Sync {componentType.Name}");
                EditorUtility.CopySerialized(sourceComponent, targetComponent);
                EditorUtility.SetDirty(targetComponent);

                objectMap[sourceComponent] = targetComponent;
                syncedTargetComponents.Add(targetComponent);
                remapComponents.Add(targetComponent);
            }

            Dictionary<Type, int> seenTargetTypeCounts = new Dictionary<Type, int>();
            foreach (Component targetComponent in targetComponents)
            {
                if (!CanSyncRootComponent(targetComponent))
                {
                    continue;
                }

                Type componentType = targetComponent.GetType();
                seenTargetTypeCounts.TryGetValue(componentType, out int occurrence);
                seenTargetTypeCounts[componentType] = occurrence + 1;

                sourceTypeCounts.TryGetValue(componentType, out int sourceCount);
                if (occurrence >= sourceCount && !syncedTargetComponents.Contains(targetComponent))
                {
                    Undo.DestroyObjectImmediate(targetComponent);
                }
            }
        }

        private static Dictionary<Type, int> CountSyncableComponentTypes(IEnumerable<Component> components)
        {
            Dictionary<Type, int> counts = new Dictionary<Type, int>();
            foreach (Component component in components)
            {
                if (!CanSyncRootComponent(component))
                {
                    continue;
                }

                Type componentType = component.GetType();
                counts.TryGetValue(componentType, out int count);
                counts[componentType] = count + 1;
            }

            return counts;
        }

        private static bool CanSyncRootComponent(Component component)
        {
            return component != null
                && component is not Transform
                && component is not MapPainterSceneContext;
        }

        private static void RemapObjectReferences(IEnumerable<Component> components, IReadOnlyDictionary<Object, Object> objectMap)
        {
            foreach (Component component in components.Where(component => component != null).Distinct())
            {
                SerializedObject serializedObject = new SerializedObject(component);
                SerializedProperty property = serializedObject.GetIterator();
                bool changed = false;

                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    Object sourceReference = property.objectReferenceValue;
                    if (sourceReference == null)
                    {
                        continue;
                    }

                    if (objectMap.TryGetValue(sourceReference, out Object mappedReference))
                    {
                        property.objectReferenceValue = mappedReference;
                        changed = true;
                    }
                }

                if (changed)
                {
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(component);
                }
            }
        }
    }
}
