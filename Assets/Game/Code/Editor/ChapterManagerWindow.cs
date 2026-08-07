using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Windy.Srpg.Game.Chapters;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Editor
{
    public sealed class ChapterManagerWindow : EditorWindow
    {
        private const float ObjectListWidth = 330f;
        private const float OutlinePadding = 0.14f;

        private Vector2 tileScrollPosition;
        private Vector2 unitScrollPosition;
        private Vector2 detailsScrollPosition;
        private UnityEngine.Object selectedObject;
        private bool includeInactiveObjects = true;

        [MenuItem("Tools/Windy SRPG/Chapter Manager")]
        private static void OpenWindow()
        {
            GetWindow<ChapterManagerWindow>("Chapter Manager");
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            SceneView.duringSceneGui -= OnSceneGui;
        }

        private void OnGUI()
        {
            DrawToolbar();

            CellGrid cellGrid = FindSceneComponent<CellGrid>();
            ChapterData chapterData = ChapterData.FindForGrid(cellGrid) ?? FindSceneComponent<ChapterData>();

            DrawChapterDataSection(cellGrid, chapterData);

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            DrawTileList();
            EditorGUILayout.Space(6f);
            DrawUnitList(cellGrid);
            EditorGUILayout.Space(6f);
            DrawSelectedObjectInspector();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            includeInactiveObjects = GUILayout.Toggle(includeInactiveObjects, "Include Inactive", EditorStyles.toolbarButton, GUILayout.Width(110f));

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                Repaint();
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Focus Selected", EditorStyles.toolbarButton, GUILayout.Width(105f)))
            {
                FocusSelectedObject();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(SceneManager.GetActiveScene().path, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawChapterDataSection(CellGrid cellGrid, ChapterData chapterData)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Chapter Data", EditorStyles.boldLabel);
                if (chapterData == null)
                {
                    EditorGUILayout.HelpBox("No Chapter Data component was found in the active scene.", MessageType.Info);
                    if (GUILayout.Button("Add Chapter Data"))
                    {
                        chapterData = CreateSceneChapterData();
                    }

                    return;
                }

                if (cellGrid != null && chapterData.gameObject == cellGrid.gameObject)
                {
                    EditorGUILayout.HelpBox(
                        "This Chapter Data component is attached to CellGrid. Move it to its own scene object so Scene System Sync can copy chapter configuration cleanly.",
                        MessageType.Warning);

                    if (GUILayout.Button("Move To Separate Chapter Data Object"))
                    {
                        chapterData = MoveChapterDataToSeparateObject(chapterData);
                    }

                    EditorGUILayout.Space(4f);
                }

                DrawSerializedObject(chapterData, drawScriptField: false);
            }
        }

        private ChapterData CreateSceneChapterData()
        {
            GameObject chapterObject = new GameObject("Chapter Data");
            Undo.RegisterCreatedObjectUndo(chapterObject, "Create Chapter Data");
            ChapterData chapterData = Undo.AddComponent<ChapterData>(chapterObject);
            selectedObject = chapterData;
            Selection.activeObject = chapterObject;
            MarkSceneDirty(chapterData);
            return chapterData;
        }

        private ChapterData MoveChapterDataToSeparateObject(ChapterData oldChapterData)
        {
            if (oldChapterData == null)
            {
                return null;
            }

            ChapterData newChapterData = CreateSceneChapterData();
            EditorUtility.CopySerialized(oldChapterData, newChapterData);
            ReplaceSceneObjectReferences(oldChapterData, newChapterData);
            Undo.DestroyObjectImmediate(oldChapterData);
            EditorUtility.SetDirty(newChapterData);
            MarkSceneDirty(newChapterData);
            return newChapterData;
        }

        private void ReplaceSceneObjectReferences(UnityEngine.Object oldReference, UnityEngine.Object newReference)
        {
            if (oldReference == null || newReference == null)
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            IEnumerable<Component> sceneComponents = Resources.FindObjectsOfTypeAll<Component>()
                .Where(component => component != null
                    && !EditorUtility.IsPersistent(component)
                    && component.gameObject.scene == activeScene);

            foreach (Component component in sceneComponents)
            {
                SerializedObject serializedObject = new SerializedObject(component);
                SerializedProperty property = serializedObject.GetIterator();
                bool changed = false;

                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference
                        || property.objectReferenceValue != oldReference)
                    {
                        continue;
                    }

                    property.objectReferenceValue = newReference;
                    changed = true;
                }

                if (changed)
                {
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(component);
                }
            }
        }

        private void DrawTileList()
        {
            List<TileEntry> tileEntries = BuildTileEntries();

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(ObjectListWidth)))
            {
                EditorGUILayout.LabelField($"Tiles ({tileEntries.Count})", EditorStyles.boldLabel);
                tileScrollPosition = EditorGUILayout.BeginScrollView(tileScrollPosition, EditorStyles.helpBox);

                foreach (TileEntry entry in tileEntries)
                {
                    DrawObjectRow(entry.Target, entry.Label, entry.IconColor);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawUnitList(CellGrid cellGrid)
        {
            List<Unit> units = GetSceneUnits(cellGrid);

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(ObjectListWidth)))
            {
                EditorGUILayout.LabelField($"Units ({units.Count})", EditorStyles.boldLabel);
                unitScrollPosition = EditorGUILayout.BeginScrollView(unitScrollPosition, EditorStyles.helpBox);

                foreach (Unit unit in units)
                {
                    DrawObjectRow(unit, BuildUnitLabel(unit), GetUnitIconColor(unit));
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSelectedObjectInspector()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Selected Data", EditorStyles.boldLabel);
                detailsScrollPosition = EditorGUILayout.BeginScrollView(detailsScrollPosition, EditorStyles.helpBox);

                Component selectedComponent = GetSelectedComponent();
                if (selectedComponent == null)
                {
                    EditorGUILayout.HelpBox("Select a tile, deployment tile, reinforcement tile, unit, or Chapter Data entry to edit it here.", MessageType.Info);
                    EditorGUILayout.EndScrollView();
                    return;
                }

                EditorGUILayout.ObjectField("Object", selectedComponent.gameObject, typeof(GameObject), true);
                EditorGUILayout.ObjectField("Component", selectedComponent, selectedComponent.GetType(), true);
                EditorGUILayout.Space(4f);
                DrawSerializedObject(selectedComponent, drawScriptField: true);

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawObjectRow(Component target, string label, Color iconColor)
        {
            if (target == null)
            {
                return;
            }

            Rect rowRect = EditorGUILayout.BeginHorizontal();
            bool isSelected = selectedObject == target || Selection.activeGameObject == target.gameObject;
            GUIStyle rowStyle = isSelected ? EditorStyles.helpBox : GUIStyle.none;
            if (GUI.Button(rowRect, GUIContent.none, rowStyle))
            {
                SelectObject(target);
            }

            Rect iconRect = GUILayoutUtility.GetRect(14f, 18f, GUILayout.Width(14f));
            EditorGUI.DrawRect(new Rect(iconRect.x + 2f, iconRect.y + 4f, 10f, 10f), iconColor);

            EditorGUILayout.LabelField(label);
            if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(45f)))
            {
                SelectObject(target);
                EditorGUIUtility.PingObject(target.gameObject);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSerializedObject(UnityEngine.Object target, bool drawScriptField)
        {
            if (target == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(target);
            serializedObject.Update();

            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                bool isScriptField = property.propertyPath == "m_Script";
                if (isScriptField && !drawScriptField)
                {
                    continue;
                }

                using (new EditorGUI.DisabledScope(isScriptField))
                {
                    EditorGUILayout.PropertyField(property, includeChildren: true);
                }
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(target);
                MarkSceneDirty(target);
            }
        }

        private List<TileEntry> BuildTileEntries()
        {
            List<TileEntry> entries = new List<TileEntry>();

            foreach (Cell cell in FindSceneComponents<Cell>())
            {
                entries.Add(new TileEntry(cell, BuildCellLabel(cell), cell.Coordinates, new Color(0.2f, 0.8f, 0.35f)));
            }

            foreach (DeploymentSlot deploymentSlot in FindSceneComponents<DeploymentSlot>())
            {
                Cell cell = deploymentSlot.Cell;
                Vector2Int coordinate = cell != null ? cell.Coordinates : Vector2Int.RoundToInt(deploymentSlot.transform.position);
                entries.Add(new TileEntry(deploymentSlot, BuildDeploymentLabel(deploymentSlot), coordinate, new Color(0.2f, 0.65f, 1f)));
            }

            foreach (ReinforcementTile reinforcementTile in FindSceneComponents<ReinforcementTile>())
            {
                Cell cell = reinforcementTile.Cell;
                Vector2Int coordinate = cell != null ? cell.Coordinates : Vector2Int.RoundToInt(reinforcementTile.transform.position);
                entries.Add(new TileEntry(reinforcementTile, BuildReinforcementLabel(reinforcementTile), coordinate, new Color(1f, 0.65f, 0.15f)));
            }

            return entries
                .OrderBy(entry => entry.Coordinate.y)
                .ThenBy(entry => entry.Coordinate.x)
                .ThenBy(entry => entry.KindSort)
                .ThenBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<Unit> GetSceneUnits(CellGrid cellGrid)
        {
            List<Unit> units = cellGrid != null
                ? cellGrid.GetAllSceneUnitsFromHierarchy(includeExcludedFromBattle: true)
                : new List<Unit>();

            if (units.Count == 0)
            {
                units = FindSceneComponents<Unit>();
            }

            return units
                .Where(unit => unit != null && (includeInactiveObjects || unit.gameObject.activeInHierarchy))
                .Distinct()
                .OrderBy(unit => unit.PlayerNumber)
                .ThenBy(unit => unit.unitName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(unit => unit.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string BuildCellLabel(Cell cell)
        {
            string presetName = cell.TilePreset != null ? $" - {cell.TilePreset.name}" : string.Empty;
            return $"Cell {FormatCoordinate(cell.Coordinates)}{presetName}";
        }

        private static string BuildDeploymentLabel(DeploymentSlot deploymentSlot)
        {
            Cell cell = deploymentSlot.Cell;
            string coordinate = cell != null ? FormatCoordinate(cell.Coordinates) : FormatPosition(deploymentSlot.transform.position);
            return $"Deployment {coordinate} - Slot {deploymentSlot.SlotIndex}";
        }

        private static string BuildReinforcementLabel(ReinforcementTile reinforcementTile)
        {
            Cell cell = reinforcementTile.Cell;
            string coordinate = cell != null ? FormatCoordinate(cell.Coordinates) : FormatPosition(reinforcementTile.transform.position);
            int unitCount = reinforcementTile.Units?.Count ?? 0;
            int turnCount = reinforcementTile.SpawnTurns?.Count ?? 0;
            return $"Reinforcement {coordinate} - {unitCount} units, {turnCount} turns";
        }

        private static string BuildUnitLabel(Unit unit)
        {
            string unitName = string.IsNullOrWhiteSpace(unit.unitName) ? unit.name : unit.unitName;
            string cellLabel = unit.Cell != null ? FormatCoordinate(unit.Cell.Coordinates) : FormatPosition(unit.transform.position);
            string excludedLabel = unit.ExcludedFromBattle ? " - Excluded" : string.Empty;
            return $"{unitName} - P{unit.PlayerNumber} - Lv {unit.Level} - {cellLabel}{excludedLabel}";
        }

        private static string FormatCoordinate(Vector2Int coordinate)
        {
            return $"({coordinate.x}, {coordinate.y})";
        }

        private static string FormatPosition(Vector3 position)
        {
            return $"({Mathf.RoundToInt(position.x)}, {Mathf.RoundToInt(position.y)})";
        }

        private static Color GetUnitIconColor(Unit unit)
        {
            return unit.PlayerNumber == 0
                ? new Color(0.3f, 0.7f, 1f)
                : new Color(1f, 0.35f, 0.25f);
        }

        private void SelectObject(Component target)
        {
            selectedObject = target;
            Selection.activeGameObject = target.gameObject;
            SceneView.RepaintAll();
            Repaint();
        }

        private Component GetSelectedComponent()
        {
            if (selectedObject is Component selectedComponent && selectedComponent != null)
            {
                return selectedComponent;
            }

            GameObject activeGameObject = Selection.activeGameObject;
            if (activeGameObject == null)
            {
                return null;
            }

            Component component = activeGameObject.GetComponent<ChapterData>();
            if (component != null)
            {
                return component;
            }

            component = activeGameObject.GetComponent<Unit>();
            if (component != null)
            {
                return component;
            }

            component = activeGameObject.GetComponent<DeploymentSlot>();
            if (component != null)
            {
                return component;
            }

            component = activeGameObject.GetComponent<ReinforcementTile>();
            if (component != null)
            {
                return component;
            }

            return activeGameObject.GetComponent<Cell>();
        }

        private void OnSelectionChanged()
        {
            Component selectedComponent = GetSelectedComponent();
            if (selectedComponent != null)
            {
                selectedObject = selectedComponent;
            }

            Repaint();
            SceneView.RepaintAll();
        }

        private void FocusSelectedObject()
        {
            Component selectedComponent = GetSelectedComponent();
            if (selectedComponent == null)
            {
                return;
            }

            Selection.activeGameObject = selectedComponent.gameObject;
            EditorGUIUtility.PingObject(selectedComponent.gameObject);
            SceneView.FrameLastActiveSceneView();
        }

        private void OnSceneGui(SceneView sceneView)
        {
            Component selectedComponent = GetSelectedComponent();
            if (selectedComponent == null)
            {
                return;
            }

            if (selectedComponent is not Unit
                && selectedComponent is not Cell
                && selectedComponent is not DeploymentSlot
                && selectedComponent is not ReinforcementTile)
            {
                return;
            }

            Color outlineColor = selectedComponent is Unit
                ? Color.yellow
                : new Color(0.1f, 0.95f, 1f);

            DrawSelectionOutline(selectedComponent.transform, outlineColor);
        }

        private static void DrawSelectionOutline(Transform targetTransform, Color color)
        {
            if (targetTransform == null)
            {
                return;
            }

            Bounds bounds = ResolveBounds(targetTransform);
            Vector3 size = bounds.size;
            size.x = Mathf.Max(size.x + OutlinePadding, 0.7f);
            size.y = Mathf.Max(size.y + OutlinePadding, 0.7f);
            size.z = Mathf.Max(size.z + OutlinePadding, 0.08f);

            Handles.zTest = CompareFunction.Always;
            Handles.color = color;
            Handles.DrawWireCube(bounds.center, size);
        }

        private static Bounds ResolveBounds(Transform targetTransform)
        {
            Renderer[] renderers = targetTransform.GetComponentsInChildren<Renderer>(includeInactive: true);
            Bounds bounds = new Bounds(targetTransform.position, Vector3.one);
            bool hasBounds = false;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            if (hasBounds)
            {
                return bounds;
            }

            Collider2D collider2D = targetTransform.GetComponentInChildren<Collider2D>(includeInactive: true);
            if (collider2D != null)
            {
                return collider2D.bounds;
            }

            Collider collider = targetTransform.GetComponentInChildren<Collider>(includeInactive: true);
            return collider != null ? collider.bounds : bounds;
        }

        private T FindSceneComponent<T>() where T : Component
        {
            return FindSceneComponents<T>().FirstOrDefault();
        }

        private List<T> FindSceneComponents<T>() where T : Component
        {
            Scene activeScene = SceneManager.GetActiveScene();
            return Resources.FindObjectsOfTypeAll<T>()
                .Where(component => component != null
                    && !EditorUtility.IsPersistent(component)
                    && component.gameObject.scene == activeScene
                    && (includeInactiveObjects || component.gameObject.activeInHierarchy))
                .OrderBy(component => GetHierarchyPath(component.transform), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            for (Transform current = transform; current != null; current = current.parent)
            {
                parts.Add(current.name);
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        private static void MarkSceneDirty(UnityEngine.Object target)
        {
            if (target is Component component && component.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            }
        }

        private sealed class TileEntry
        {
            public readonly Component Target;
            public readonly string Label;
            public readonly Vector2Int Coordinate;
            public readonly int KindSort;
            public readonly Color IconColor;

            public TileEntry(Component target, string label, Vector2Int coordinate, Color iconColor)
            {
                Target = target;
                Label = label;
                Coordinate = coordinate;
                IconColor = iconColor;
                KindSort = target switch
                {
                    Cell => 0,
                    DeploymentSlot => 1,
                    ReinforcementTile => 2,
                    _ => 99
                };
            }
        }
    }
}
