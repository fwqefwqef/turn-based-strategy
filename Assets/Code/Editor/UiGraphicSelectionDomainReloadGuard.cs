using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Windy.Srpg.Game.Editor
{
    /// <summary>
    /// Workaround for a Unity 6000.4 Inspector bug: reloading scripts while a UI
    /// Graphic is selected can fault PropertyEditor.Styles and spam null references.
    /// Remove this guard after upgrading to a Unity release containing the fix.
    /// </summary>
    [InitializeOnLoad]
    internal static class UiGraphicSelectionDomainReloadGuard
    {
        private const string AffectedVersionPrefix = "6000.4.";

        static UiGraphicSelectionDomainReloadGuard()
        {
            if (!IsAffectedEditorVersion())
            {
                return;
            }

            // Covers the reload that first imports this guard when initialization
            // happens before the Inspector rebuilds its preview header.
            DeselectSelectedGraphic();

            AssemblyReloadEvents.beforeAssemblyReload -= DeselectSelectedGraphic;
            AssemblyReloadEvents.beforeAssemblyReload += DeselectSelectedGraphic;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static bool IsAffectedEditorVersion()
        {
            return Application.unityVersion.StartsWith(AffectedVersionPrefix, System.StringComparison.Ordinal);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                DeselectSelectedGraphic();
            }
        }

        private static void DeselectSelectedGraphic()
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject != null && selectedObject.GetComponent<Graphic>() != null)
            {
                Selection.activeObject = null;
            }
        }
    }
}
