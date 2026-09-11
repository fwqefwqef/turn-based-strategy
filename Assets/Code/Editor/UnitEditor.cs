using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Windy.Srpg.Game.Catalogs;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Editor
{
    [CustomEditor(typeof(Unit), true)]
    [CanEditMultipleObjects]
    public sealed class UnitEditor : UnityEditor.Editor
    {
        private static readonly HashSet<string> InheritedFields = new HashSet<string>
        {
            "unitName", "baseHitPoints", "baseManaPoints", "baseStrength", "baseMagic",
            "baseDefense", "baseSpeed", "baseLuck", "level", "experience", "movementPointsStorage",
            "weaponProficiencies", "actionAiMode", "movementAiMode", "waitGroupId",
            "growthStrength", "growthMagic", "growthDefense", "growthSpeed", "growthLuck",
            "startingInventory", "startingSkills", "startingClassPassives"
        };
        private static readonly HashSet<string> HiddenFields = new HashSet<string>
        {
            "preset",
            "presetOverrides",
            "unitId",
            "visualId",
            "Obstructable",
            "participatesInDeploymentRoster",
            "MovementAnimationSpeed"
        };
        private readonly Dictionary<string, string[]> ids = new Dictionary<string, string[]>();
        private readonly Dictionary<string, string[]> labels = new Dictionary<string, string[]>();
        private readonly Dictionary<string, ItemData> items = new Dictionary<string, ItemData>(StringComparer.OrdinalIgnoreCase);
        private DateTime catalogWriteTime;
        private string catalogError;
        private bool showResolved = true;
        private bool showIdentity;

        private void OnEnable()
        {
            var overrides = serializedObject.FindProperty("presetOverrides");
            if (overrides == null) return;
            foreach (string name in new[] { "StatBonuses", "AdditionalInventory", "AdditionalSkills", "AdditionalPassives" })
                overrides.FindPropertyRelative(name).isExpanded = true;
        }

        public override void OnInspectorGUI()
        {
            RefreshCatalog();
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("preset"), new GUIContent("Inherited Preset"));
                var presetProperty = serializedObject.FindProperty("preset");
                bool hasPreset = presetProperty.objectReferenceValue != null;
                bool mixedPresets = presetProperty.hasMultipleDifferentValues;
                DrawUnitProperties(hasPreset || mixedPresets);
                if (hasPreset || mixedPresets)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Preset Overrides", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("Adds to this unit's inherited preset without changing the asset. Zero keeps a stat unchanged. Inventory appends; skills and passives are unique.", MessageType.Info);
                    var overrides = serializedObject.FindProperty("presetOverrides");
                    var enabled = overrides.FindPropertyRelative("Enabled");
                    EditorGUILayout.PropertyField(enabled, new GUIContent("Enable Overrides"));
                    using (new EditorGUI.DisabledScope(!enabled.boolValue && !enabled.hasMultipleDifferentValues))
                    {
                        var stats = overrides.FindPropertyRelative("StatBonuses");
                        EditorGUILayout.PropertyField(stats, new GUIContent("Stat Bonuses"), true);
                        if (targets.Length == 1)
                        {
                            DrawCatalogList(overrides.FindPropertyRelative("AdditionalInventory"), "Extra Inventory", "ItemId", "items");
                            DrawCatalogList(overrides.FindPropertyRelative("AdditionalSkills"), "Extra Skills", "SkillId", "skills");
                            DrawCatalogList(overrides.FindPropertyRelative("AdditionalPassives"), "Extra Passives", "PassiveId", "passives");
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("Stat bonuses support multi-editing. Select one unit to edit its extra loadout lists or see resolved values. Units without a preset ignore overrides.", MessageType.Info);
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Assign a preset to enable additive overrides. Without a preset, the unit's starting fields above are used directly.", MessageType.Info);
                }
            }
            serializedObject.ApplyModifiedProperties();
            if (Application.isPlaying)
                EditorGUILayout.HelpBox("Starting configuration is read-only during Play Mode. Campaign units load their stats and loadouts from the save, rather than reapplying preset overrides.", MessageType.Info);
            if (!string.IsNullOrEmpty(catalogError)) EditorGUILayout.HelpBox(catalogError, MessageType.Warning);
            if (targets.Length == 1 && target is Unit unit)
            {
                if (unit.AssignedPreset != null && !Application.isPlaying) DrawResolved(unit);
                showIdentity = EditorGUILayout.Foldout(showIdentity, "Save Identity (read-only)", true);
                if (showIdentity)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField("Unit ID", unit.UnitId);
                        EditorGUILayout.TextField("Visual ID", unit.VisualId);
                    }
                }
            }
        }

        private void DrawUnitProperties(bool inheritsPreset)
        {
            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (HiddenFields.Contains(property.propertyPath) || (inheritsPreset && InheritedFields.Contains(property.propertyPath))) continue;
                using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                    EditorGUILayout.PropertyField(property, true);
            }
        }

        private void DrawCatalogList(SerializedProperty list, string title, string idField, string catalog)
        {
            list.isExpanded = EditorGUILayout.Foldout(list.isExpanded, $"{title} ({list.arraySize})", true);
            if (!list.isExpanded) return;
            EditorGUI.indentLevel++;
            for (int i = 0; i < list.arraySize; i++)
            {
                var entry = list.GetArrayElementAtIndex(i);
                var id = entry.FindPropertyRelative(idField);
                EditorGUILayout.BeginHorizontal();
                DrawCatalogId(id, catalog);
                bool remove = GUILayout.Button("−", GUILayout.Width(24));
                EditorGUILayout.EndHorizontal();
                if (remove)
                {
                    list.DeleteArrayElementAtIndex(i);
                    break;
                }
                if (catalog == "items")
                {
                    if (items.TryGetValue(id.stringValue ?? "", out var data) && data is ConsumableData)
                    {
                        var initialized = entry.FindPropertyRelative("ChargesInitialized");
                        var charges = entry.FindPropertyRelative("InitialCharges");
                        if (!initialized.boolValue) { initialized.boolValue = true; charges.intValue = -1; }
                        EditorGUILayout.PropertyField(charges, new GUIContent("Charges", "-1 uses the item's default; 0 omits an empty consumable."));
                        charges.intValue = Mathf.Max(-1, charges.intValue);
                    }
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("IsDroppable"), new GUIContent("Drops on Defeat"));
                }
            }
            if (GUILayout.Button("Add Entry"))
            {
                int index = list.arraySize++;
                var entry = list.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative(idField).stringValue = "";
                if (catalog == "items")
                {
                    entry.FindPropertyRelative("InitialCharges").intValue = -1;
                    entry.FindPropertyRelative("ChargesInitialized").boolValue = true;
                    entry.FindPropertyRelative("IsDroppable").boolValue = false;
                }
            }
            EditorGUI.indentLevel--;
        }

        private void DrawCatalogId(SerializedProperty property, string catalog)
        {
            if (!ids.TryGetValue(catalog, out var options))
            {
                EditorGUILayout.PropertyField(property, GUIContent.none);
                return;
            }
            int selected = Array.FindIndex(options, id => string.Equals(id, property.stringValue, StringComparison.OrdinalIgnoreCase));
            if (selected < 0)
            {
                EditorGUILayout.PropertyField(property, GUIContent.none);
                EditorGUILayout.LabelField("Unknown ID", GUILayout.Width(75));
                int replacement = EditorGUILayout.Popup(0, labels[catalog]);
                if (replacement > 0) property.stringValue = options[replacement];
                return;
            }
            int next = EditorGUILayout.Popup(selected, labels[catalog]);
            if (next != selected) property.stringValue = options[next];
        }

        private void DrawResolved(Unit unit)
        {
            EditorGUILayout.Space();
            showResolved = EditorGUILayout.Foldout(showResolved, "Resolved Starting Values", true);
            if (!showResolved) return;
            var preset = unit.AssignedPreset;
            var overrides = unit.PresetOverrides;
            var stats = overrides.ResolveStats(preset.BaseStats);
            EditorGUILayout.LabelField("Base stats before equipment/passive effects", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("HP / MP / Move", $"{Mathf.Max(1, stats.HitPoints)} / {Mathf.Max(0, stats.ManaPoints)} / {(preset.MovementAiMode == UnitMovementAiMode.NotMove ? 0 : Mathf.Max(0, stats.MovementPoints))}");
            EditorGUILayout.LabelField("Str / Mag / Def / Spd / Lck", $"{stats.Strength} / {stats.Magic} / {stats.Defense} / {stats.Speed} / {stats.Luck}");
            var inventory = overrides.ResolveInventory(preset.StartingInventory);
            EditorGUILayout.LabelField("Inventory", string.Join(", ", inventory.Select(i => i.ItemId)), EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Skills", string.Join(", ", overrides.ResolveSkills(preset.StartingSkills).Select(i => i.SkillId)), EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Passives", string.Join(", ", overrides.ResolvePassives(preset.StartingClassPassives).Select(i => i.PassiveId)), EditorStyles.wordWrappedLabel);
            if (inventory.Count > UnitInventory.MaxSlots)
                EditorGUILayout.HelpBox($"Combined inventory has {inventory.Count} entries, but capacity is {UnitInventory.MaxSlots}. Later entries may not fit; inherited items are loaded first.", MessageType.Warning);
            foreach (var entry in inventory)
            {
                if (items.TryGetValue(entry.ItemId ?? "", out var item) && item is WeaponData weapon
                    && (preset.WeaponProficiencies & WeaponProficiencyUtility.ForWeapon(weapon)) == 0)
                    EditorGUILayout.HelpBox($"{weapon.Name} can be carried but cannot be equipped with this preset's weapon proficiencies.", MessageType.Warning);
            }
            if (preset.MovementAiMode == UnitMovementAiMode.NotMove && overrides.Enabled && overrides.StatBonuses.MovementPoints != 0)
                EditorGUILayout.HelpBox("The preset's NotMove AI setting keeps movement at zero, including movement bonuses.", MessageType.Info);
            EditorGUILayout.HelpBox("Applies to units initialized from a preset. Units loaded from the campaign save retain their saved stats and loadouts.", MessageType.None);
        }

        private void RefreshCatalog()
        {
            string path = Path.Combine(Application.dataPath, "Data/gdata.json");
            DateTime modified = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (ids.Count > 0 && modified == catalogWriteTime) return;
            try
            {
                var data = JsonUtility.FromJson<GameDataCatalogResource>(File.ReadAllText(path));
                items.Clear();
                foreach (var item in data.Items.ToRuntimeDefinitions()) items[item.Id] = item;
                SetOptions("items", items.Values.Select(i => (i.Id, i.Name)));
                SetOptions("skills", data.Skills.ToRuntimeDefinitions().Select(i => (i.Id, i.Name)));
                SetOptions("passives", data.Passives.ToRuntimeDefinitions().Select(i => (i.Id, i.Name)));
                catalogWriteTime = modified;
                catalogError = null;
            }
            catch (Exception exception)
            {
                catalogError = "Could not load catalog choices: " + exception.Message;
            }
        }

        private void SetOptions(string key, IEnumerable<(string id, string name)> definitions)
        {
            var entries = definitions.Where(e => !string.IsNullOrWhiteSpace(e.id)).OrderBy(e => e.name).ToArray();
            ids[key] = new[] { "" }.Concat(entries.Select(e => e.id)).ToArray();
            labels[key] = new[] { "Select..." }.Concat(entries.Select(e => $"{e.name} ({e.id})")).ToArray();
        }
    }
}
