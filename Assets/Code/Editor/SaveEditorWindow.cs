using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Windy.Srpg.Game.Campaign;
using Windy.Srpg.Game.Catalogs;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Editor
{
    public sealed class SaveEditorWindow : EditorWindow
    {
        private CampaignSaveSlot slot = CampaignSaveSlot.Campaign;
        private CampaignSaveData save;
        private Vector2 scroll;
        private string status;
        private string[] itemIds = Array.Empty<string>();
        private string[] skillIds = Array.Empty<string>();
        private string[] passiveIds = Array.Empty<string>();
        private UnitPreset unitPresetToImport;
        private readonly HashSet<string> expandedUnits = new HashSet<string>();

        [MenuItem("Tools/Windy SRPG/Save Editor")]
        private static void Open() => GetWindow<SaveEditorWindow>("Save Editor");

        private void OnEnable()
        {
            RefreshCatalogs();
            LoadSelectedSlot(createIfMissing: true);
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (save == null)
            {
                EditorGUILayout.HelpBox("No save is loaded.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawGlobalValues();
            DrawFloatArray("Cleared Chapters", ref save.ClearedChapterIds);
            DrawStringArray("Deployment Roster Unit IDs", ref save.DeploymentRosterUnitIds, null);
            DrawInventory("Storage Items", ref save.StorageItems);
            DrawOwnedUnits();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            CampaignSaveSlot nextSlot = (CampaignSaveSlot)EditorGUILayout.EnumPopup("Save Slot", slot);
            if (nextSlot != slot)
            {
                slot = nextSlot;
                LoadSelectedSlot(createIfMissing: true);
            }

            EditorGUILayout.SelectableLabel(CampaignSaveManager.GetSavePath(slot), EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload")) LoadSelectedSlot(createIfMissing: true);
            if (GUILayout.Button("Save")) SaveSelectedSlot();
            if (GUILayout.Button("New Blank")) NewBlankSave();
            if (GUILayout.Button("Reveal File")) EditorUtility.RevealInFinder(CampaignSaveManager.GetSavePath(slot));
            if (GUILayout.Button("Delete")) DeleteSelectedSlot();
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrWhiteSpace(status)) EditorGUILayout.HelpBox(status, MessageType.None);
            EditorGUILayout.EndVertical();
        }

        private void DrawGlobalValues()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Global Save Values", EditorStyles.boldLabel);
            save.Version = EditorGUILayout.IntField("Version", save.Version);
            save.Gold = EditorGUILayout.IntField("Gold", save.Gold);
        }

        private void DrawOwnedUnits()
        {
            save.OwnedUnits ??= Array.Empty<OwnedUnitSaveData>();
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField($"Owned Units ({save.OwnedUnits.Length})", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            unitPresetToImport = (UnitPreset)EditorGUILayout.ObjectField(
                "Unit Preset",
                unitPresetToImport,
                typeof(UnitPreset),
                false);
            using (new EditorGUI.DisabledScope(unitPresetToImport == null))
            {
                if (GUILayout.Button("Import Unit From Preset"))
                {
                    ImportUnitFromPreset(unitPresetToImport);
                }
            }
            EditorGUILayout.HelpBox(
                "Imports the preset's identity, level, stats, growths, proficiencies, inventory, skills, and class passives. Press Save to write the change to disk.",
                MessageType.Info);
            EditorGUILayout.EndVertical();

            for (int i = 0; i < save.OwnedUnits.Length; i++)
            {
                save.OwnedUnits[i] ??= new OwnedUnitSaveData();
                OwnedUnitSaveData unit = save.OwnedUnits[i];
                string foldoutKey = i.ToString();
                bool expanded = expandedUnits.Contains(foldoutKey);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                bool nextExpanded = EditorGUILayout.Foldout(expanded, $"[{i}] {DisplayUnitName(unit)}", true);
                if (nextExpanded != expanded)
                {
                    if (nextExpanded) expandedUnits.Add(foldoutKey); else expandedUnits.Remove(foldoutKey);
                }
                if (GUILayout.Button("Up", GUILayout.Width(38))) Move(ref save.OwnedUnits, i, i - 1);
                if (GUILayout.Button("Down", GUILayout.Width(48))) Move(ref save.OwnedUnits, i, i + 1);
                if (GUILayout.Button("Remove", GUILayout.Width(62)))
                {
                    RemoveAt(ref save.OwnedUnits, i--);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }
                EditorGUILayout.EndHorizontal();

                if (nextExpanded)
                {
                    unit.UnitId = EditorGUILayout.TextField("Unit ID", unit.UnitId ?? string.Empty);
                    unit.VisualId = EditorGUILayout.TextField("Visual ID", unit.VisualId ?? string.Empty);
                    unit.UnitName = EditorGUILayout.TextField("Name", unit.UnitName ?? string.Empty);
                    unit.Level = EditorGUILayout.IntField("Level", unit.Level);
                    unit.Experience = EditorGUILayout.IntField("Experience", unit.Experience);
                    DrawStringArray("Weapon Proficiencies", ref unit.WeaponProficiencyIds,
                        Enum.GetNames(typeof(WeaponProficiency)).Where(id => id != nameof(WeaponProficiency.None)).ToArray());
                    unit.BaseStats = DrawStats("Base Stats", unit.BaseStats);
                    unit.GrowthRates = DrawGrowths("Growth Rates", unit.GrowthRates);
                    DrawInventory("Inventory", ref unit.Inventory);
                    DrawStringArray("Skills", ref unit.SkillIds, skillIds);
                    DrawStringArray("Class Passives", ref unit.ClassPassiveIds, passiveIds);
                }
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Add Owned Unit"))
            {
                Append(ref save.OwnedUnits, new OwnedUnitSaveData());
            }
        }

        private void ImportUnitFromPreset(UnitPreset preset)
        {
            OwnedUnitSaveData importedUnit = CampaignSaveFactory.CreateOwnedUnitFromPreset(preset);
            if (importedUnit == null)
            {
                status = "Could not import the selected unit preset.";
                return;
            }

            importedUnit.UnitId = CreateUniqueUnitId(importedUnit.UnitId);
            Append(ref save.OwnedUnits, importedUnit);
            expandedUnits.Add((save.OwnedUnits.Length - 1).ToString());
            status = $"Imported {DisplayUnitName(importedUnit)} from {preset.name}. Press Save to write the change.";
        }

        private string CreateUniqueUnitId(string requestedId)
        {
            string baseId = string.IsNullOrWhiteSpace(requestedId)
                ? "unit"
                : requestedId.Trim();
            HashSet<string> existingIds = new HashSet<string>(
                (save.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>())
                    .Where(unit => unit != null && !string.IsNullOrWhiteSpace(unit.UnitId))
                    .Select(unit => unit.UnitId.Trim()),
                StringComparer.OrdinalIgnoreCase);
            if (!existingIds.Contains(baseId))
            {
                return baseId;
            }

            int suffix = 2;
            string candidate;
            do
            {
                candidate = $"{baseId}_{suffix++}";
            }
            while (existingIds.Contains(candidate));

            return candidate;
        }

        private static UnitStatBlock DrawStats(string label, UnitStatBlock value)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            value.HitPoints = EditorGUILayout.IntField("HP", value.HitPoints);
            value.ManaPoints = EditorGUILayout.IntField("MP", value.ManaPoints);
            value.MovementPoints = EditorGUILayout.IntField("Movement", value.MovementPoints);
            value.Strength = EditorGUILayout.IntField("Strength", value.Strength);
            value.Defense = EditorGUILayout.IntField("Defense", value.Defense);
            value.Magic = EditorGUILayout.IntField("Magic", value.Magic);
            value.Speed = EditorGUILayout.IntField("Speed", value.Speed);
            value.Luck = EditorGUILayout.IntField("Luck", value.Luck);
            EditorGUI.indentLevel--;
            return value;
        }

        private static UnitGrowthRates DrawGrowths(string label, UnitGrowthRates value)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            value.Strength = EditorGUILayout.IntField("Strength %", value.Strength);
            value.Defense = EditorGUILayout.IntField("Defense %", value.Defense);
            value.Magic = EditorGUILayout.IntField("Magic %", value.Magic);
            value.Speed = EditorGUILayout.IntField("Speed %", value.Speed);
            value.Luck = EditorGUILayout.IntField("Luck %", value.Luck);
            EditorGUI.indentLevel--;
            return value;
        }

        private void DrawInventory(string label, ref SavedInventoryEntryData[] entries)
        {
            entries ??= Array.Empty<SavedInventoryEntryData>();
            EditorGUILayout.LabelField($"{label} ({entries.Length})", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i] ??= new SavedInventoryEntryData();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                entries[i].ItemId = DrawCatalogId("Item", entries[i].ItemId, itemIds);
                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    RemoveAt(ref entries, i--);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }
                EditorGUILayout.EndHorizontal();
                entries[i].RemainingCharges = EditorGUILayout.IntField("Remaining Charges", entries[i].RemainingCharges);
                entries[i].IsDroppable = EditorGUILayout.Toggle("Droppable", entries[i].IsDroppable);
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button($"Add {label} Entry")) Append(ref entries, new SavedInventoryEntryData());
            EditorGUI.indentLevel--;
        }

        private static void DrawStringArray(string label, ref string[] values, string[] catalog)
        {
            values ??= Array.Empty<string>();
            EditorGUILayout.LabelField($"{label} ({values.Length})", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < values.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                values[i] = DrawCatalogId($"[{i}]", values[i], catalog);
                if (GUILayout.Button("X", GUILayout.Width(24))) RemoveAt(ref values, i--);
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button($"Add {label} Entry")) Append(ref values, catalog?.FirstOrDefault() ?? string.Empty);
            EditorGUI.indentLevel--;
        }

        private static void DrawFloatArray(string label, ref float[] values)
        {
            values ??= Array.Empty<float>();
            EditorGUILayout.LabelField($"{label} ({values.Length})", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < values.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                values[i] = EditorGUILayout.FloatField($"[{i}]", values[i]);
                if (GUILayout.Button("X", GUILayout.Width(24))) RemoveAt(ref values, i--);
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button($"Add {label} Entry")) Append(ref values, 1f);
            EditorGUI.indentLevel--;
        }

        private static string DrawCatalogId(string label, string value, string[] catalog)
        {
            value ??= string.Empty;
            if (catalog == null || catalog.Length == 0) return EditorGUILayout.TextField(label, value);
            string[] choices = catalog.Contains(value, StringComparer.OrdinalIgnoreCase)
                ? catalog
                : new[] { value }.Concat(catalog).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            int index = Math.Max(0, Array.FindIndex(choices, id => string.Equals(id, value, StringComparison.OrdinalIgnoreCase)));
            EditorGUILayout.BeginVertical();
            int selected = EditorGUILayout.Popup(label, index, choices);
            string result = EditorGUILayout.TextField("Exact ID", choices.Length > 0 ? choices[selected] : value);
            EditorGUILayout.EndVertical();
            return result;
        }

        private void RefreshCatalogs()
        {
            itemIds = CatalogResourceLoader.LoadItemCatalog().ToRuntimeDefinitions().Select(data => data?.Id).Where(Valid).Distinct().OrderBy(id => id).ToArray();
            skillIds = CatalogResourceLoader.LoadSkillCatalog().ToRuntimeDefinitions().Select(data => data?.Id).Where(Valid).Distinct().OrderBy(id => id).ToArray();
            passiveIds = CatalogResourceLoader.LoadPassiveCatalog().ToRuntimeDefinitions().Select(data => data?.Id).Where(Valid).Distinct().OrderBy(id => id).ToArray();
        }

        private void LoadSelectedSlot(bool createIfMissing)
        {
            save = CampaignSaveManager.Load(slot);
            if (save == null && createIfMissing) save = new CampaignSaveData();
            NormalizeArrays();
            expandedUnits.Clear();
            status = save == null ? "Save does not exist." : $"Loaded {slot} save.";
        }

        private void SaveSelectedSlot()
        {
            NormalizeArrays();
            CampaignSaveManager.Save(save, slot);
            status = $"Saved {slot} save at {DateTime.Now:T}.";
        }

        private void NewBlankSave()
        {
            if (!EditorUtility.DisplayDialog("New Blank Save", $"Replace the editor buffer for the {slot} slot? The file is not changed until Save is pressed.", "Create", "Cancel")) return;
            save = new CampaignSaveData();
            expandedUnits.Clear();
            status = "Created a blank in-memory save. Press Save to write it.";
        }

        private void DeleteSelectedSlot()
        {
            if (!EditorUtility.DisplayDialog("Delete Save", $"Permanently delete the {slot} save file?", "Delete", "Cancel")) return;
            bool deleted = CampaignSaveManager.Delete(slot);
            save = new CampaignSaveData();
            status = deleted ? $"Deleted {slot} save." : $"Could not delete {slot} save; see Console.";
        }

        private void NormalizeArrays()
        {
            if (save == null) return;
            save.ClearedChapterIds ??= Array.Empty<float>();
            save.OwnedUnits ??= Array.Empty<OwnedUnitSaveData>();
            save.DeploymentRosterUnitIds ??= Array.Empty<string>();
            save.StorageItems ??= Array.Empty<SavedInventoryEntryData>();
            foreach (OwnedUnitSaveData unit in save.OwnedUnits.Where(unit => unit != null))
            {
                unit.WeaponProficiencyIds ??= Array.Empty<string>();
                unit.Inventory ??= Array.Empty<SavedInventoryEntryData>();
                unit.SkillIds ??= Array.Empty<string>();
                unit.ClassPassiveIds ??= Array.Empty<string>();
            }
        }

        private static bool Valid(string value) => !string.IsNullOrWhiteSpace(value);
        private static string DisplayUnitName(OwnedUnitSaveData unit) => !string.IsNullOrWhiteSpace(unit?.UnitName) ? unit.UnitName : (!string.IsNullOrWhiteSpace(unit?.UnitId) ? unit.UnitId : "New Unit");
        private static void Append<T>(ref T[] array, T value) { var list = (array ?? Array.Empty<T>()).ToList(); list.Add(value); array = list.ToArray(); }
        private static void RemoveAt<T>(ref T[] array, int index) { var list = (array ?? Array.Empty<T>()).ToList(); if (index >= 0 && index < list.Count) list.RemoveAt(index); array = list.ToArray(); }
        private static void Move<T>(ref T[] array, int from, int to) { if (array == null || from < 0 || from >= array.Length || to < 0 || to >= array.Length) return; (array[from], array[to]) = (array[to], array[from]); }
    }
}
