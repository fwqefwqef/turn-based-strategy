using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Editor
{
    [CustomEditor(typeof(Unit), true)]
    [CanEditMultipleObjects]
    public sealed class UnitEditor : UnityEditor.Editor
    {
        private static readonly HashSet<string> HiddenUnitFields = new HashSet<string>
        {
            "preset",
            "startingInventory",
            "startingSkills",
            "startingClassPassives"
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawUnitProperties();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8f);
            if (targets.Length == 1 && target is Unit unit)
            {
                DrawAssignedPresetReference(unit);
            }
            else
            {
                EditorGUILayout.HelpBox("Assigned preset reference is shown when inspecting a single Unit.", MessageType.Info);
            }
        }

        private void DrawUnitProperties()
        {
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (HiddenUnitFields.Contains(property.propertyPath))
                {
                    continue;
                }

                using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                {
                    EditorGUILayout.PropertyField(property, includeChildren: true);
                }
            }
        }

        private void DrawAssignedPresetReference(Unit unit)
        {
            UnitPreset preset = serializedObject.FindProperty("preset")?.objectReferenceValue as UnitPreset;
            EditorGUILayout.LabelField("Assigned Preset", EditorStyles.boldLabel);
            if (preset == null)
            {
                EditorGUILayout.HelpBox("This unit has no assigned UnitPreset.", MessageType.Info);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Preset", preset, typeof(UnitPreset), false);
            }
        }
    }
}
