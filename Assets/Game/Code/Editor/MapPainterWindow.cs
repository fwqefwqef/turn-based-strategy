using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Editor
{
    public sealed class MapPainterWindow : EditorWindow
    {
        private const string TemplateScenePath = "Assets/Scenes/test.unity";
        private const string FriendlyUnitPrefabPath = "Assets/Game/Prefabs/FriendlyUnit.prefab";
        private const string EnemyUnitPrefabPath = "Assets/Game/Prefabs/EnemyUnit.prefab";
        private const string DeploymentSlotPrefabPath = "Assets/Game/Prefabs/DeploymentSlot.prefab";
        private const string ReinforcementTilesParentName = "ReinforcementTiles";

        private static readonly FieldInfo UnitPresetField = typeof(Unit).GetField("preset", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo UnitPresetOverrideField = typeof(Unit).GetField("presetOverride", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private enum PaintMode
        {
            Tile,
            DeploymentSlot,
            Enemy,
            Friendly,
            Reinforcement,
            Erase
        }

        [SerializeField] private SceneAsset templateScene;
        [SerializeField] private GameObject baseCellPrefab;
        [SerializeField] private CellTilePreset defaultTraversableTilePreset;
        [SerializeField] private CellTilePreset selectedTilePreset;
        [SerializeField] private GameObject friendlyUnitPrefab;
        [SerializeField] private GameObject enemyUnitPrefab;
        [SerializeField] private UnitPreset friendlyUnitPreset;
        [SerializeField] private UnitPreset enemyUnitPreset;
        [SerializeField] private UnitPresetOverride enemyUnitPresetOverride = new UnitPresetOverride();
        [SerializeField] private List<ReinforcementUnitEntry> reinforcementUnits = new List<ReinforcementUnitEntry>();
        [SerializeField] private List<int> reinforcementSpawnTurns = new List<int>();
        [SerializeField] private bool placeReinforcementSpawner;
        [SerializeField] private int reinforcementSpawnerPlayerNumber = 1;
        [SerializeField] private UnitPreset reinforcementSpawnerPreset;
        [SerializeField] private UnitPresetOverride reinforcementSpawnerPresetOverride = new UnitPresetOverride();
        [SerializeField] private int mapWidth = 20;
        [SerializeField] private int mapHeight = 20;
        [SerializeField] private bool enableScenePainting = true;
        [SerializeField] private PaintMode paintMode = PaintMode.Tile;

        private Vector2 scrollPosition;
        private Vector2Int hoveredCoordinate = new Vector2Int(-1, -1);
        private List<CellTilePreset> availableTilePresets = new List<CellTilePreset>();

        [MenuItem("Tools/Windy SRPG/Map Painter")]
        private static void OpenWindow()
        {
            GetWindow<MapPainterWindow>("Map Painter");
        }

        private void OnEnable()
        {
            templateScene ??= AssetDatabase.LoadAssetAtPath<SceneAsset>(TemplateScenePath);
            CellTilePresetBootstrap.EnsureDefaults();
            defaultTraversableTilePreset ??= CellTilePresetBootstrap.LoadSquarePreset();
            selectedTilePreset ??= defaultTraversableTilePreset;
            RefreshAvailableTilePresets();
            friendlyUnitPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(FriendlyUnitPrefabPath);
            enemyUnitPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(EnemyUnitPrefabPath);
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawSceneCreationSection();
            EditorGUILayout.Space(10f);
            DrawSceneStatusSection();
            EditorGUILayout.Space(10f);
            DrawPaletteSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSceneCreationSection()
        {
            EditorGUILayout.LabelField("Scene Setup", EditorStyles.boldLabel);
            templateScene = (SceneAsset)EditorGUILayout.ObjectField("Template Scene", templateScene, typeof(SceneAsset), false);
            baseCellPrefab = (GameObject)EditorGUILayout.ObjectField("Base Cell Prefab", baseCellPrefab, typeof(GameObject), false);
            defaultTraversableTilePreset = (CellTilePreset)EditorGUILayout.ObjectField("Default Traversable Tile", defaultTraversableTilePreset, typeof(CellTilePreset), false);
            friendlyUnitPrefab = (GameObject)EditorGUILayout.ObjectField("Friendly Unit Prefab", friendlyUnitPrefab, typeof(GameObject), false);
            enemyUnitPrefab = (GameObject)EditorGUILayout.ObjectField("Enemy Unit Prefab", enemyUnitPrefab, typeof(GameObject), false);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("20x20"))
            {
                mapWidth = 20;
                mapHeight = 20;
            }

            if (GUILayout.Button("50x50"))
            {
                mapWidth = 50;
                mapHeight = 50;
            }

            if (GUILayout.Button("100x100"))
            {
                mapWidth = 100;
                mapHeight = 100;
            }

            EditorGUILayout.EndHorizontal();

            mapWidth = Mathf.Max(1, EditorGUILayout.IntField("Map Width", mapWidth));
            mapHeight = Mathf.Max(1, EditorGUILayout.IntField("Map Height", mapHeight));

            if (GUILayout.Button("Create New Painted Scene"))
            {
                CreateNewPaintScene();
            }

            if (GUILayout.Button("Apply Size To Current Scene"))
            {
                MapPainterSceneContext context = GetCurrentContext();
                if (context == null)
                {
                    Debug.LogWarning("Map Painter: No active painter scene context found.");
                }
                else
                {
                    Undo.RecordObject(context, "Resize Map Painter Bounds");
                    context.MapWidth = mapWidth;
                    context.MapHeight = mapHeight;
                    EditorUtility.SetDirty(context);
                    EditorSceneManager.MarkSceneDirty(context.gameObject.scene);
                    FocusSceneCamera(context);
                }
            }
        }

        private void DrawSceneStatusSection()
        {
            EditorGUILayout.LabelField("Active Scene", EditorStyles.boldLabel);
            MapPainterSceneContext context = GetCurrentContext();
            if (context == null)
            {
                EditorGUILayout.HelpBox("Open a painter scene or create one first. The tool looks for a MapPainterSceneContext in the active scene.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Scene", context.gameObject.scene.path);
            EditorGUILayout.LabelField("Bounds", $"{context.MapWidth} x {context.MapHeight}");
            EditorGUILayout.LabelField("Hovered Cell", IsCoordinateInBounds(context, hoveredCoordinate) ? hoveredCoordinate.ToString() : "Out of bounds");
            enableScenePainting = EditorGUILayout.Toggle("Enable Scene Painting", enableScenePainting);

            if (GUILayout.Button("Clear Painted Map"))
            {
                if (EditorUtility.DisplayDialog("Clear Painted Map", "Delete all painted cells, deployment slots, and painted units in this scene?", "Clear", "Cancel"))
                {
                    ClearPaintedMap(context);
                }
            }
        }

        private void DrawPaletteSection()
        {
            EditorGUILayout.LabelField("Paint Palette", EditorStyles.boldLabel);
            paintMode = (PaintMode)GUILayout.Toolbar((int)paintMode, new[] { "Tile", "Deploy", "Enemy", "Friendly", "Reinforce", "Erase" });

            switch (paintMode)
            {
                case PaintMode.Tile:
                    DrawTilePresetPalette();
                    break;

                case PaintMode.Enemy:
                    enemyUnitPreset = (UnitPreset)EditorGUILayout.ObjectField("Enemy Preset", enemyUnitPreset, typeof(UnitPreset), false);
                    DrawEnemyOverrideEditor();
                    break;

                case PaintMode.Friendly:
                    friendlyUnitPreset = (UnitPreset)EditorGUILayout.ObjectField("Friendly Preset", friendlyUnitPreset, typeof(UnitPreset), false);
                    EditorGUILayout.HelpBox("Direct friendlies are player 0 controllable units that bypass deployment roster/save ownership.", MessageType.None);
                    break;

                case PaintMode.Reinforcement:
                    DrawReinforcementPalette();
                    break;

                case PaintMode.DeploymentSlot:
                    EditorGUILayout.HelpBox("Painting a deployment slot forces a traversable floor cell and removes any unit already on that coordinate.", MessageType.None);
                    break;

                case PaintMode.Erase:
                    EditorGUILayout.HelpBox("Erase removes any painted cell, deployment slot, and unit on the targeted coordinate.", MessageType.None);
                    break;
            }
        }

        private void DrawTilePresetPalette()
        {
            if (GUILayout.Button("Refresh Tile Presets"))
            {
                RefreshAvailableTilePresets();
            }

            selectedTilePreset = (CellTilePreset)EditorGUILayout.ObjectField("Selected Tile", selectedTilePreset, typeof(CellTilePreset), false);
            if (availableTilePresets.Count == 0)
            {
                EditorGUILayout.HelpBox("No CellTilePreset assets were found.", MessageType.Warning);
                return;
            }

            foreach (CellTilePreset tilePreset in availableTilePresets)
            {
                if (tilePreset == null)
                {
                    continue;
                }

                bool isSelected = selectedTilePreset == tilePreset;
                GUIStyle style = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fixedHeight = 28f
                };

                Rect rowRect = EditorGUILayout.GetControlRect(false, 28f);
                if (isSelected)
                {
                    EditorGUI.DrawRect(rowRect, new Color(0.55f, 0.75f, 1f, 0.45f));
                }

                string label = $"{tilePreset.name}  |  {(tilePreset.IsTraversable ? "Traversable" : "Blocked")}  |  Cost {tilePreset.TraversalCost:0.##}";
                if (GUI.Button(rowRect, label, style))
                {
                    selectedTilePreset = tilePreset;
                }
            }
        }

        private void DrawEnemyOverrideEditor()
        {
            if (enemyUnitPresetOverride == null)
            {
                enemyUnitPresetOverride = new UnitPresetOverride();
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Enemy Preset Override", EditorStyles.miniBoldLabel);
            enemyUnitPresetOverride.OverrideWaitGroupId = EditorGUILayout.Toggle("Override Wait Group", enemyUnitPresetOverride.OverrideWaitGroupId);
            if (enemyUnitPresetOverride.OverrideWaitGroupId)
            {
                enemyUnitPresetOverride.WaitGroupId = Mathf.Max(0, EditorGUILayout.IntField("Wait Group Id", enemyUnitPresetOverride.WaitGroupId));
            }
        }

        private void DrawReinforcementPalette()
        {
            reinforcementUnits ??= new List<ReinforcementUnitEntry>();
            reinforcementSpawnTurns ??= new List<int>();
            reinforcementSpawnerPresetOverride ??= new UnitPresetOverride();
            int reinforcementUnitCountBeforeDrawing = reinforcementUnits.Count;

            EditorGUILayout.HelpBox(
                "The turn list is the schedule. Preset entries repeat from the beginning when the schedule has more entries than the unit list.",
                MessageType.None);

            SerializedObject serializedWindow = new SerializedObject(this);
            serializedWindow.Update();
            EditorGUILayout.PropertyField(
                serializedWindow.FindProperty("reinforcementUnits"),
                new GUIContent("Reinforcement Units"),
                includeChildren: true);
            EditorGUILayout.PropertyField(
                serializedWindow.FindProperty("reinforcementSpawnTurns"),
                new GUIContent("Spawn Turns"),
                includeChildren: true);
            EditorGUILayout.PropertyField(
                serializedWindow.FindProperty("placeReinforcementSpawner"),
                new GUIContent("Place Linked Spawner"));

            SerializedProperty placeSpawnerProperty = serializedWindow.FindProperty("placeReinforcementSpawner");
            if (placeSpawnerProperty.boolValue)
            {
                EditorGUILayout.PropertyField(
                    serializedWindow.FindProperty("reinforcementSpawnerPlayerNumber"),
                    new GUIContent("Spawner Player Number"));
                EditorGUILayout.PropertyField(
                    serializedWindow.FindProperty("reinforcementSpawnerPreset"),
                    new GUIContent("Spawner Preset"));
                EditorGUILayout.PropertyField(
                    serializedWindow.FindProperty("reinforcementSpawnerPresetOverride"),
                    new GUIContent("Spawner Override"),
                    includeChildren: true);
                EditorGUILayout.HelpBox(
                    "The spawner is created from this preset and override. Configure zero movement in either of those assets when the spawner should be immovable.",
                    MessageType.Info);
            }

            serializedWindow.ApplyModifiedProperties();

            for (int i = reinforcementUnitCountBeforeDrawing; i < reinforcementUnits.Count; i++)
            {
                ReinforcementUnitEntry newEntry = reinforcementUnits[i] ?? new ReinforcementUnitEntry();
                newEntry.PlayerNumber = 1;
                newEntry.PresetOverride ??= new UnitPresetOverride();
                reinforcementUnits[i] = newEntry;
            }
        }

        private void OnSceneGui(SceneView sceneView)
        {
            MapPainterSceneContext context = GetCurrentContext();
            if (context == null)
            {
                return;
            }

            DrawSceneGrid(context);
            hoveredCoordinate = GetHoveredCoordinate(context, Event.current.mousePosition, sceneView.camera);
            DrawHoveredCoordinate(context);

            if (!enableScenePainting || Application.isPlaying)
            {
                return;
            }

            Event current = Event.current;
            if (current == null || current.alt || current.button != 0)
            {
                return;
            }

            if (current.type == EventType.MouseDown || current.type == EventType.MouseDrag)
            {
                if (!IsCoordinateInBounds(context, hoveredCoordinate))
                {
                    return;
                }

                if (TryPaintAtCoordinate(context, hoveredCoordinate))
                {
                    current.Use();
                }
            }
        }

        private void DrawSceneGrid(MapPainterSceneContext context)
        {
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            Handles.color = new Color(0f, 0f, 0f, 0.18f);

            float minX = -0.5f;
            float maxX = context.MapWidth - 0.5f;
            float minY = -0.5f;
            float maxY = context.MapHeight - 0.5f;

            for (int x = 0; x <= context.MapWidth; x++)
            {
                float drawX = x - 0.5f;
                Handles.DrawLine(new Vector3(drawX, minY, 0f), new Vector3(drawX, maxY, 0f));
            }

            for (int y = 0; y <= context.MapHeight; y++)
            {
                float drawY = y - 0.5f;
                Handles.DrawLine(new Vector3(minX, drawY, 0f), new Vector3(maxX, drawY, 0f));
            }
        }

        private void DrawHoveredCoordinate(MapPainterSceneContext context)
        {
            if (!IsCoordinateInBounds(context, hoveredCoordinate))
            {
                return;
            }

            Handles.color = new Color(0.15f, 0.7f, 1f, 0.95f);
            Vector3 center = new Vector3(hoveredCoordinate.x, hoveredCoordinate.y, 0f);
            Vector3 size = Vector3.one;
            Handles.DrawWireCube(center, size);
        }

        private Vector2Int GetHoveredCoordinate(MapPainterSceneContext context, Vector2 guiPosition, Camera sceneCamera)
        {
            if (sceneCamera == null)
            {
                return new Vector2Int(-1, -1);
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
            Plane plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out float enter))
            {
                return new Vector2Int(-1, -1);
            }

            Vector3 hit = ray.GetPoint(enter);
            return new Vector2Int(Mathf.RoundToInt(hit.x), Mathf.RoundToInt(hit.y));
        }

        private bool TryPaintAtCoordinate(MapPainterSceneContext context, Vector2Int coordinate)
        {
            switch (paintMode)
            {
                case PaintMode.Tile:
                    return PaintTilePresetAt(context, selectedTilePreset, coordinate);

                case PaintMode.DeploymentSlot:
                    return PaintDeploymentSlotAt(context, coordinate);

                case PaintMode.Enemy:
                    return PaintSceneUnitAt(context, coordinate, enemyUnitPrefab, enemyUnitPreset, enemyUnitPresetOverride, playerNumber: 1, participatesInDeploymentRoster: false, includeInOwnedUnitSave: false);

                case PaintMode.Friendly:
                    return PaintSceneUnitAt(context, coordinate, friendlyUnitPrefab, friendlyUnitPreset, null, playerNumber: 0, participatesInDeploymentRoster: false, includeInOwnedUnitSave: false);

                case PaintMode.Reinforcement:
                    return PaintReinforcementTileAt(context, coordinate);

                case PaintMode.Erase:
                    return EraseAtCoordinate(context, coordinate);
            }

            return false;
        }

        private void CreateNewPaintScene()
        {
            if (templateScene == null)
            {
                Debug.LogError("Map Painter: Template scene is not assigned.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string templatePath = AssetDatabase.GetAssetPath(templateScene);
            string savePath = EditorUtility.SaveFilePanelInProject("Create Painted Scene", "PaintedMap", "unity", "Choose where to save the new painted scene.");
            if (string.IsNullOrWhiteSpace(savePath))
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(savePath) != null)
            {
                if (!AssetDatabase.DeleteAsset(savePath))
                {
                    Debug.LogError($"Map Painter: Failed to replace existing scene at '{savePath}'.");
                    return;
                }
            }

            FileUtil.CopyFileOrDirectory(templatePath, savePath);
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.OpenScene(savePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("Map Painter: Failed to open the copied scene.");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return;
            }

            if (!TryGetCoreSceneReferences(out CellGrid cellGrid, out SceneUnitGenerator sceneUnitGenerator, out Transform cellsParent, out Transform unitsParent, out Transform deploymentSlotsParent))
            {
                Debug.LogError("Map Painter: The template scene is missing CellGrid, SceneUnitGenerator, or required parents.");
                return;
            }

            ClearChildren(cellsParent);
            ClearChildren(unitsParent);
            ClearChildren(deploymentSlotsParent);
            ClearChildren(GameObject.Find("WorldHealthBars")?.transform);

            MapPainterSceneContext context = cellGrid.GetComponent<MapPainterSceneContext>();
            if (context == null)
            {
                context = Undo.AddComponent<MapPainterSceneContext>(cellGrid.gameObject);
            }

            context.CellGrid = cellGrid;
            context.SceneUnitGenerator = sceneUnitGenerator;
            context.DeploymentSlotsParent = deploymentSlotsParent;
            context.MapWidth = mapWidth;
            context.MapHeight = mapHeight;
            ClearChildren(GetOrCreateReinforcementTilesParent(context));
            cellGrid.SetDeploymentSlotsParent(deploymentSlotsParent);
            sceneUnitGenerator.SetDeploymentRosterUnitPrefab(friendlyUnitPrefab != null ? friendlyUnitPrefab.GetComponent<Unit>() : null);
            EditorUtility.SetDirty(cellGrid);
            EditorUtility.SetDirty(sceneUnitGenerator);
            EditorUtility.SetDirty(context);

            FocusSceneCamera(context);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }

        private void ClearPaintedMap(MapPainterSceneContext context)
        {
            if (context == null)
            {
                return;
            }

            if (!TryGetCoreSceneReferences(context, out _, out _, out Transform cellsParent, out Transform unitsParent, out Transform deploymentSlotsParent))
            {
                return;
            }

            ClearChildren(cellsParent);
            ClearChildren(deploymentSlotsParent);
            ClearChildren(GetOrCreateReinforcementTilesParent(context));

            foreach (Unit unit in unitsParent.GetComponentsInChildren<Unit>(true).ToList())
            {
                if (unit == null)
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(unit.gameObject);
            }
            if (context.SceneUnitGenerator != null)
            {
                context.SceneUnitGenerator.SetDeploymentRosterUnitPrefab(friendlyUnitPrefab != null ? friendlyUnitPrefab.GetComponent<Unit>() : null);
                EditorUtility.SetDirty(context.SceneUnitGenerator);
            }

            if (context.CellGrid != null)
            {
                context.CellGrid.SetDeploymentSlotsParent(deploymentSlotsParent);
                EditorUtility.SetDirty(context.CellGrid);
            }
            EditorSceneManager.MarkSceneDirty(context.gameObject.scene);
        }

        private bool PaintTilePresetAt(MapPainterSceneContext context, CellTilePreset tilePreset, Vector2Int coordinate)
        {
            if (context == null || tilePreset == null)
            {
                return false;
            }

            if (!TryGetCoreSceneReferences(context, out _, out _, out Transform cellsParent, out _, out _))
            {
                return false;
            }

            RemoveUnitAtCoordinate(context, coordinate);
            RemoveDeploymentSlotAtCoordinate(context, coordinate);
            RemoveReinforcementTileAtCoordinate(context, coordinate);
            RemoveCellAtCoordinate(context, coordinate);

            GameObject cellObject = baseCellPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(baseCellPrefab, context.gameObject.scene)
                : CreateCellFallbackObject(tilePreset);
            if (cellObject == null)
            {
                return false;
            }

            Undo.RegisterCreatedObjectUndo(cellObject, "Paint Cell");
            cellObject.transform.SetParent(cellsParent, false);
            cellObject.transform.position = new Vector3(coordinate.x, coordinate.y, 0f);
            cellObject.name = $"{tilePreset.name}_{coordinate.x}_{coordinate.y}";

            if (cellObject.TryGetComponent(out Cell cell))
            {
                cell.SetTilePreset(tilePreset);
                EditorUtility.SetDirty(cell);
            }

            EditorSceneManager.MarkSceneDirty(context.gameObject.scene);
            return true;
        }

        private bool PaintDeploymentSlotAt(MapPainterSceneContext context, Vector2Int coordinate)
        {
            Cell cell = EnsureTraversableCellAt(context, coordinate);
            if (cell == null || !TryGetCoreSceneReferences(context, out _, out _, out _, out _, out Transform deploymentSlotsParent))
            {
                return false;
            }

            RemoveUnitAtCoordinate(context, coordinate);
            RemoveReinforcementTileAtCoordinate(context, coordinate);

            DeploymentSlot slot = GetDeploymentSlotAtCoordinate(context, coordinate);
            if (slot == null)
            {
                GameObject slotPrefab = EnsureDeploymentSlotPrefab();
                GameObject slotObject = slotPrefab != null
                    ? (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, context.gameObject.scene)
                    : CreateDeploymentSlotFallbackObject();

                if (slotObject == null)
                {
                    return false;
                }

                Undo.RegisterCreatedObjectUndo(slotObject, "Paint Deployment Slot");
                slotObject.transform.SetParent(deploymentSlotsParent, false);
                slot = slotObject.GetComponent<DeploymentSlot>();
            }

            Undo.RecordObject(slot, "Bind Deployment Slot");
            slot.BindToCell(cell);
            RenumberDeploymentSlots(context);
            EditorSceneManager.MarkSceneDirty(context.gameObject.scene);
            return true;
        }

        private bool PaintReinforcementTileAt(MapPainterSceneContext context, Vector2Int coordinate)
        {
            if (reinforcementUnits == null
                || reinforcementUnits.Count == 0
                || reinforcementUnits.Any(entry => entry == null || entry.Preset == null))
            {
                Debug.LogWarning("Map Painter: Every reinforcement unit entry requires a UnitPreset.");
                return false;
            }

            List<ReinforcementUnitEntry> configuredUnits = reinforcementUnits
                .Select(entry => new ReinforcementUnitEntry
                {
                    PlayerNumber = Mathf.Max(0, entry.PlayerNumber),
                    Preset = entry.Preset,
                    PresetOverride = ClonePresetOverride(entry.PresetOverride)
                })
                .ToList();
            List<int> configuredTurns = (reinforcementSpawnTurns ?? new List<int>())
                .Select(turn => Mathf.Max(1, turn))
                .ToList();
            if (configuredTurns.Count == 0)
            {
                Debug.LogWarning("Map Painter: A reinforcement tile requires at least one spawn turn.");
                return false;
            }

            Cell cell = EnsureTraversableCellAt(context, coordinate);
            Transform reinforcementTilesParent = GetOrCreateReinforcementTilesParent(context);
            if (cell == null || reinforcementTilesParent == null)
            {
                return false;
            }

            RemoveDeploymentSlotAtCoordinate(context, coordinate);
            RemoveUnitAtCoordinate(context, coordinate);

            Unit linkedSpawner = null;
            if (placeReinforcementSpawner)
            {
                if (reinforcementSpawnerPreset == null)
                {
                    Debug.LogWarning("Map Painter: Assign a UnitPreset for the linked reinforcement spawner.");
                    return false;
                }

                int spawnerPlayerNumber = Mathf.Max(0, reinforcementSpawnerPlayerNumber);
                GameObject spawnerUnitTemplate = spawnerPlayerNumber == 0
                    ? friendlyUnitPrefab
                    : enemyUnitPrefab;
                spawnerUnitTemplate ??= enemyUnitPrefab != null ? enemyUnitPrefab : friendlyUnitPrefab;
                if (spawnerUnitTemplate == null || spawnerUnitTemplate.GetComponent<Unit>() == null)
                {
                    Debug.LogWarning("Map Painter: Configure a valid base unit prefab in Scene Setup before painting a spawner.");
                    return false;
                }

                if (!PaintSceneUnitAt(
                    context,
                    coordinate,
                    spawnerUnitTemplate,
                    reinforcementSpawnerPreset,
                    reinforcementSpawnerPresetOverride,
                    playerNumber: spawnerPlayerNumber,
                    participatesInDeploymentRoster: false,
                    includeInOwnedUnitSave: false))
                {
                    return false;
                }

                linkedSpawner = GetUnitAtCoordinate(context, coordinate);
                if (linkedSpawner == null)
                {
                    Debug.LogError("Map Painter: The reinforcement spawner was placed but could not be resolved.");
                    return false;
                }
            }

            ReinforcementTile reinforcementTile = GetReinforcementTileAtCoordinate(context, coordinate);
            if (reinforcementTile != null && reinforcementTile.gameObject == cell.gameObject)
            {
                Undo.DestroyObjectImmediate(reinforcementTile);
                reinforcementTile = null;
            }

            if (reinforcementTile == null)
            {
                GameObject reinforcementObject = new GameObject($"ReinforcementTile_{coordinate.x}_{coordinate.y}");
                Undo.RegisterCreatedObjectUndo(reinforcementObject, "Paint Reinforcement Tile");
                reinforcementObject.transform.SetParent(reinforcementTilesParent, false);
                reinforcementTile = Undo.AddComponent<ReinforcementTile>(reinforcementObject);
            }
            else
            {
                Undo.RecordObject(reinforcementTile, "Update Reinforcement Tile");
                reinforcementTile.transform.SetParent(reinforcementTilesParent, false);
            }

            reinforcementTile.Configure(cell, configuredUnits, configuredTurns, linkedSpawner);
            reinforcementTile.name = $"ReinforcementTile_{coordinate.x}_{coordinate.y}";
            EditorUtility.SetDirty(reinforcementTile);
            EditorSceneManager.MarkSceneDirty(context.gameObject.scene);
            return true;
        }

        private bool PaintSceneUnitAt(
            MapPainterSceneContext context,
            Vector2Int coordinate,
            GameObject unitPrefab,
            UnitPreset preset,
            UnitPresetOverride presetOverride,
            int playerNumber,
            bool participatesInDeploymentRoster,
            bool includeInOwnedUnitSave)
        {
            if (context == null || unitPrefab == null)
            {
                return false;
            }

            Cell cell = EnsureTraversableCellAt(context, coordinate);
            if (cell == null || !TryGetCoreSceneReferences(context, out _, out _, out _, out Transform unitsParent, out _))
            {
                return false;
            }

            RemoveUnitAtCoordinate(context, coordinate);
            RemoveDeploymentSlotAtCoordinate(context, coordinate);

            GameObject unitObject = (GameObject)PrefabUtility.InstantiatePrefab(unitPrefab, context.gameObject.scene);
            if (unitObject == null)
            {
                return false;
            }

            Undo.RegisterCreatedObjectUndo(unitObject, "Paint Unit");
            unitObject.transform.SetParent(unitsParent, false);
            unitObject.transform.position = cell.transform.position;

            if (!unitObject.TryGetComponent(out Unit unit))
            {
                Undo.DestroyObjectImmediate(unitObject);
                Debug.LogError("Map Painter: The painted unit prefab is missing a Unit component.");
                return false;
            }

            ApplyUnitStamp(unit, cell, preset, presetOverride, playerNumber, participatesInDeploymentRoster, includeInOwnedUnitSave);
            unitObject.name = !string.IsNullOrWhiteSpace(preset?.UnitName)
                ? $"{preset.UnitName}_{coordinate.x}_{coordinate.y}"
                : $"{unitPrefab.name}_{coordinate.x}_{coordinate.y}";

            EditorSceneManager.MarkSceneDirty(context.gameObject.scene);
            return true;
        }

        private void ApplyUnitStamp(
            Unit unit,
            Cell cell,
            UnitPreset preset,
            UnitPresetOverride presetOverride,
            int playerNumber,
            bool participatesInDeploymentRoster,
            bool includeInOwnedUnitSave)
        {
            if (unit == null)
            {
                return;
            }

            unit.PlayerNumber = playerNumber;
            unit.Cell = cell;
            unit.ExcludedFromBattle = false;
            unit.ParticipatesInDeploymentRoster = participatesInDeploymentRoster;
            unit.IncludeInOwnedUnitSave = includeInOwnedUnitSave;

            UnitPresetField?.SetValue(unit, preset);
            UnitPresetOverrideField?.SetValue(unit, ClonePresetOverride(presetOverride));

            if (preset != null)
            {
                InvokeEditorPresetRefresh(unit, preset);
            }

            EditorUtility.SetDirty(unit);
        }

        private static UnitPresetOverride ClonePresetOverride(UnitPresetOverride source)
        {
            if (source == null)
            {
                return new UnitPresetOverride();
            }

            string json = JsonUtility.ToJson(source);
            return JsonUtility.FromJson<UnitPresetOverride>(json);
        }

        private static void InvokeEditorPresetRefresh(Unit unit, UnitPreset preset)
        {
            MethodInfo refreshMethod = typeof(Unit).GetMethod(
                "RefreshPresetFromAssetInEditor",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            refreshMethod?.Invoke(unit, new object[] { preset });
        }

        private Cell EnsureTraversableCellAt(MapPainterSceneContext context, Vector2Int coordinate)
        {
            Cell existing = GetCellAtCoordinate(context, coordinate);
            if (existing != null && existing.IsTraversable)
            {
                return existing;
            }

            if (!PaintTilePresetAt(context, defaultTraversableTilePreset, coordinate))
            {
                return null;
            }

            return GetCellAtCoordinate(context, coordinate);
        }

        private bool EraseAtCoordinate(MapPainterSceneContext context, Vector2Int coordinate)
        {
            bool changed = false;

            if (GetUnitAtCoordinate(context, coordinate) != null)
            {
                RemoveUnitAtCoordinate(context, coordinate);
                changed = true;
            }

            if (GetDeploymentSlotAtCoordinate(context, coordinate) != null)
            {
                RemoveDeploymentSlotAtCoordinate(context, coordinate);
                changed = true;
            }

            if (GetReinforcementTileAtCoordinate(context, coordinate) != null)
            {
                RemoveReinforcementTileAtCoordinate(context, coordinate);
                changed = true;
            }

            if (GetCellAtCoordinate(context, coordinate) != null)
            {
                RemoveCellAtCoordinate(context, coordinate);
                changed = true;
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(context.gameObject.scene);
            }

            return changed;
        }

        private void RemoveCellAtCoordinate(MapPainterSceneContext context, Vector2Int coordinate)
        {
            Cell cell = GetCellAtCoordinate(context, coordinate);
            if (cell != null)
            {
                Undo.DestroyObjectImmediate(cell.gameObject);
            }
        }

        private void RemoveUnitAtCoordinate(MapPainterSceneContext context, Vector2Int coordinate)
        {
            Unit unit = GetUnitAtCoordinate(context, coordinate);
            if (unit != null)
            {
                Undo.DestroyObjectImmediate(unit.gameObject);
            }
        }

        private void RemoveDeploymentSlotAtCoordinate(MapPainterSceneContext context, Vector2Int coordinate)
        {
            DeploymentSlot slot = GetDeploymentSlotAtCoordinate(context, coordinate);
            if (slot != null)
            {
                Undo.DestroyObjectImmediate(slot.gameObject);
            }
        }

        private void RemoveReinforcementTileAtCoordinate(MapPainterSceneContext context, Vector2Int coordinate)
        {
            ReinforcementTile reinforcementTile = GetReinforcementTileAtCoordinate(context, coordinate);
            if (reinforcementTile != null)
            {
                if (reinforcementTile.GetComponent<Cell>() != null)
                {
                    Undo.DestroyObjectImmediate(reinforcementTile);
                }
                else
                {
                    Undo.DestroyObjectImmediate(reinforcementTile.gameObject);
                }
            }
        }

        private Cell GetCellAtCoordinate(MapPainterSceneContext context, Vector2Int coordinate)
        {
            if (!TryGetCoreSceneReferences(context, out _, out _, out Transform cellsParent, out _, out _))
            {
                return null;
            }

            foreach (Cell cell in cellsParent.GetComponentsInChildren<Cell>(true))
            {
                if (cell != null && cell.Coordinates == coordinate)
                {
                    return cell;
                }
            }

            return null;
        }

        private Unit GetUnitAtCoordinate(MapPainterSceneContext context, Vector2Int coordinate)
        {
            if (!TryGetCoreSceneReferences(context, out _, out _, out _, out Transform unitsParent, out _))
            {
                return null;
            }

            foreach (Unit unit in unitsParent.GetComponentsInChildren<Unit>(true))
            {
                if (unit == null)
                {
                    continue;
                }

                Cell cell = unit.Cell;
                if (cell != null && cell.Coordinates == coordinate)
                {
                    return unit;
                }

                if (!unit.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 position = unit.transform.position;
                if (Mathf.RoundToInt(position.x) == coordinate.x && Mathf.RoundToInt(position.y) == coordinate.y)
                {
                    return unit;
                }
            }

            return null;
        }

        private DeploymentSlot GetDeploymentSlotAtCoordinate(MapPainterSceneContext context, Vector2Int coordinate)
        {
            if (!TryGetCoreSceneReferences(context, out _, out _, out _, out _, out Transform deploymentSlotsParent))
            {
                return null;
            }

            foreach (DeploymentSlot slot in deploymentSlotsParent.GetComponentsInChildren<DeploymentSlot>(true))
            {
                if (slot == null)
                {
                    continue;
                }

                Cell cell = slot.Cell;
                if (cell != null && cell.Coordinates == coordinate)
                {
                    return slot;
                }

                Vector3 position = slot.transform.position;
                if (Mathf.RoundToInt(position.x) == coordinate.x && Mathf.RoundToInt(position.y) == coordinate.y)
                {
                    return slot;
                }
            }

            return null;
        }

        private ReinforcementTile GetReinforcementTileAtCoordinate(MapPainterSceneContext context, Vector2Int coordinate)
        {
            Transform reinforcementTilesParent = context != null ? context.ReinforcementTilesParent : null;
            if (reinforcementTilesParent != null)
            {
                foreach (ReinforcementTile reinforcementTile in reinforcementTilesParent.GetComponentsInChildren<ReinforcementTile>(true))
                {
                    if (reinforcementTile == null)
                    {
                        continue;
                    }

                    Cell linkedCell = reinforcementTile.Cell;
                    if (linkedCell != null && linkedCell.Coordinates == coordinate)
                    {
                        return reinforcementTile;
                    }

                    Vector3 position = reinforcementTile.transform.position;
                    if (Mathf.RoundToInt(position.x) == coordinate.x && Mathf.RoundToInt(position.y) == coordinate.y)
                    {
                        return reinforcementTile;
                    }
                }
            }

            Cell cell = GetCellAtCoordinate(context, coordinate);
            return cell != null ? cell.GetComponent<ReinforcementTile>() : null;
        }

        private Transform GetOrCreateReinforcementTilesParent(MapPainterSceneContext context)
        {
            if (context == null)
            {
                return null;
            }

            if (context.ReinforcementTilesParent != null)
            {
                return context.ReinforcementTilesParent;
            }

            Transform hierarchyParent = context.DeploymentSlotsParent != null
                ? context.DeploymentSlotsParent.parent
                : context.transform;
            Transform reinforcementTilesParent = hierarchyParent != null
                ? hierarchyParent.Find(ReinforcementTilesParentName)
                : null;
            if (reinforcementTilesParent == null)
            {
                GameObject parentObject = new GameObject(ReinforcementTilesParentName);
                Undo.RegisterCreatedObjectUndo(parentObject, "Create Reinforcement Tiles Parent");
                if (hierarchyParent != null)
                {
                    parentObject.transform.SetParent(hierarchyParent, false);
                }
                else
                {
                    SceneManager.MoveGameObjectToScene(parentObject, context.gameObject.scene);
                }

                reinforcementTilesParent = parentObject.transform;
            }

            context.ReinforcementTilesParent = reinforcementTilesParent;
            EditorUtility.SetDirty(context);
            return reinforcementTilesParent;
        }

        private void RenumberDeploymentSlots(MapPainterSceneContext context)
        {
            if (!TryGetCoreSceneReferences(context, out _, out _, out _, out _, out Transform deploymentSlotsParent))
            {
                return;
            }

            List<DeploymentSlot> slots = deploymentSlotsParent
                .GetComponentsInChildren<DeploymentSlot>(true)
                .Where(slot => slot != null)
                .OrderByDescending(slot => slot.Cell != null ? slot.Cell.Coordinates.y : Mathf.RoundToInt(slot.transform.position.y))
                .ThenBy(slot => slot.Cell != null ? slot.Cell.Coordinates.x : Mathf.RoundToInt(slot.transform.position.x))
                .ToList();

            for (int i = 0; i < slots.Count; i++)
            {
                DeploymentSlot slot = slots[i];
                SerializedObject serializedSlot = new SerializedObject(slot);
                serializedSlot.FindProperty("slotIndex").intValue = i;
                serializedSlot.ApplyModifiedPropertiesWithoutUndo();
                slot.name = $"DeploymentSlot_{i}";
                slot.SyncToCell();
                EditorUtility.SetDirty(slot);
            }
        }

        private GameObject EnsureDeploymentSlotPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DeploymentSlotPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            GameObject slotObject = CreateDeploymentSlotFallbackObject();
            if (slotObject == null)
            {
                return null;
            }

            string directory = Path.GetDirectoryName(DeploymentSlotPrefabPath)?.Replace("\\", "/");
            if (!string.IsNullOrWhiteSpace(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(slotObject, DeploymentSlotPrefabPath);
            DestroyImmediate(slotObject);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        private GameObject CreateDeploymentSlotFallbackObject()
        {
            GameObject slotObject = new GameObject("DeploymentSlot");
            SpriteRenderer renderer = slotObject.AddComponent<SpriteRenderer>();
            DeploymentSlot slot = slotObject.AddComponent<DeploymentSlot>();

            if (defaultTraversableTilePreset != null && defaultTraversableTilePreset.TileSprite != null)
            {
                renderer.sprite = defaultTraversableTilePreset.TileSprite;
            }

            renderer.color = new Color(0.55f, 0.85f, 1f, 0.72f);
            renderer.sortingOrder = 5;

            SerializedObject serializedSlot = new SerializedObject(slot);
            serializedSlot.FindProperty("highlightRenderer").objectReferenceValue = renderer;
            serializedSlot.ApplyModifiedPropertiesWithoutUndo();
            return slotObject;
        }

        private GameObject CreateCellFallbackObject(CellTilePreset tilePreset)
        {
            GameObject cellObject = new GameObject(tilePreset != null ? tilePreset.name : "Cell");
            SpriteRenderer renderer = cellObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 0;
            cellObject.AddComponent<BoxCollider>();
            Cell cell = cellObject.AddComponent<Cell>();
            cellObject.AddComponent<CellHighlighter>();

            if (tilePreset != null)
            {
                renderer.sprite = tilePreset.TileSprite;
                renderer.color = Color.white;
                cell.SetTilePreset(tilePreset);
            }
            else
            {
                renderer.color = Color.white;
            }

            return cellObject;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            List<GameObject> children = new List<GameObject>();
            for (int i = 0; i < parent.childCount; i++)
            {
                children.Add(parent.GetChild(i).gameObject);
            }

            foreach (GameObject child in children)
            {
                Undo.DestroyObjectImmediate(child);
            }
        }

        private void FocusSceneCamera(MapPainterSceneContext context)
        {
            if (context == null)
            {
                return;
            }

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return;
            }

            Vector3 center = new Vector3((context.MapWidth - 1) * 0.5f, (context.MapHeight - 1) * 0.5f, 0f);
            sceneView.pivot = center;
            sceneView.size = Mathf.Max(context.MapWidth, context.MapHeight) * 0.75f;
            sceneView.Repaint();
        }

        private MapPainterSceneContext GetCurrentContext()
        {
            return UnityEngine.Object.FindFirstObjectByType<MapPainterSceneContext>();
        }

        private bool TryGetCoreSceneReferences(
            out CellGrid cellGrid,
            out SceneUnitGenerator sceneUnitGenerator,
            out Transform cellsParent,
            out Transform unitsParent,
            out Transform deploymentSlotsParent)
        {
            return TryGetCoreSceneReferences(GetCurrentContext(), out cellGrid, out sceneUnitGenerator, out cellsParent, out unitsParent, out deploymentSlotsParent);
        }

        private bool TryGetCoreSceneReferences(
            MapPainterSceneContext context,
            out CellGrid cellGrid,
            out SceneUnitGenerator sceneUnitGenerator,
            out Transform cellsParent,
            out Transform unitsParent,
            out Transform deploymentSlotsParent)
        {
            cellGrid = context != null ? context.CellGrid : UnityEngine.Object.FindFirstObjectByType<CellGrid>();
            sceneUnitGenerator = context != null ? context.SceneUnitGenerator : null;
            cellsParent = null;
            unitsParent = null;
            deploymentSlotsParent = null;

            if (cellGrid == null)
            {
                return false;
            }

            sceneUnitGenerator ??= cellGrid.GetComponent<SceneUnitGenerator>();
            if (sceneUnitGenerator == null)
            {
                return false;
            }

            cellsParent = sceneUnitGenerator.CellsParent;
            unitsParent = sceneUnitGenerator.UnitsParent;
            deploymentSlotsParent = context != null ? context.DeploymentSlotsParent : cellGrid.GetDeploymentSlotsParent();

            if (cellsParent == null || unitsParent == null || deploymentSlotsParent == null)
            {
                return false;
            }

            if (context != null)
            {
                context.CellGrid = cellGrid;
                context.SceneUnitGenerator = sceneUnitGenerator;
                context.DeploymentSlotsParent = deploymentSlotsParent;
            }

            cellGrid.SetDeploymentSlotsParent(deploymentSlotsParent);

            return true;
        }

        private static bool IsCoordinateInBounds(MapPainterSceneContext context, Vector2Int coordinate)
        {
            return context != null
                && coordinate.x >= 0
                && coordinate.y >= 0
                && coordinate.x < context.MapWidth
                && coordinate.y < context.MapHeight;
        }

        private void RefreshAvailableTilePresets()
        {
            string[] guids = AssetDatabase.FindAssets("t:CellTilePreset");
            availableTilePresets = guids
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<CellTilePreset>(path))
                .Where(preset => preset != null)
                .OrderBy(preset => preset.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (selectedTilePreset == null || !availableTilePresets.Contains(selectedTilePreset))
            {
                selectedTilePreset = availableTilePresets.FirstOrDefault();
            }

            if (defaultTraversableTilePreset == null)
            {
                defaultTraversableTilePreset = availableTilePresets.FirstOrDefault(preset => preset != null && preset.IsTraversable);
            }
        }
    }
}
