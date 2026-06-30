using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Windy.Srpg.Game.Grid
{
    [CreateAssetMenu(fileName = "CellTilePreset", menuName = "TBS/Grid/Cell Tile Preset")]
    public sealed class CellTilePreset : ScriptableObject
    {
        public string PresetId = "cell_tile";
        public Sprite TileSprite;
        public bool IsTraversable = true;
        public float TraversalCost = 1f;

        private void OnValidate()
        {
            TraversalCost = Mathf.Max(0f, TraversalCost);
            QueueEditorRefresh();
        }

        private void RefreshLinkedCellsInEditor()
        {
            if (Application.isPlaying)
            {
                return;
            }

            Cell[] cells = Resources.FindObjectsOfTypeAll<Cell>();
            foreach (Cell cell in cells)
            {
                if (cell == null)
                {
                    continue;
                }

                GameObject cellObject = cell.gameObject;
                if (cellObject == null || !cellObject.scene.IsValid())
                {
                    continue;
                }

                try
                {
                    cell.RefreshTilePresetFromAssetInEditor(this);
                }
                catch
                {
                    // Editor restore can momentarily surface partially initialized scene objects.
                    // Skip those and let the next validation/editor refresh pick them up.
                }
            }
        }

#if UNITY_EDITOR
        private void QueueEditorRefresh()
        {
            if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            EditorApplication.delayCall -= RefreshLinkedCellsInEditor;
            EditorApplication.delayCall += RefreshLinkedCellsInEditor;
        }
#else
        private void QueueEditorRefresh()
        {
            RefreshLinkedCellsInEditor();
        }
#endif
    }
}
