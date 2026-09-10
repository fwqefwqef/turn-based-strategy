using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using Windy.Srpg.Game.Grid;

namespace Windy.Srpg.Game.UI
{
    /// <summary>Bottom strip describing the tile under the mouse or keyboard cursor.</summary>
    public sealed class TileHoverStripUI : MonoBehaviour
    {
        [Header("Runtime References (normally supplied by GUIController)")]
        [SerializeField] private CellGrid cellGrid;
        [SerializeField] private GameplayInputController inputController;

        [Header("Scene UI")]
        [Tooltip("The panel that is shown while a tile is hovered. Keep this controller on a separate, always-active object.")]
        [SerializeField] private GameObject root;
        [Tooltip("Optional TextMeshPro label used for the tile name.")]
        [SerializeField] private TMP_Text titleLabel;
        [Tooltip("Optional TextMeshPro label used for movement cost and terrain effects.")]
        [SerializeField] private TMP_Text detailsLabel;
        [Tooltip("Optional single-label layout. Leave blank when Title Label and Details Label are assigned.")]
        [FormerlySerializedAs("label")]
        [SerializeField] private TMP_Text combinedLabel;

        private Cell hoveredCell;

        public void Initialize(CellGrid grid, GameplayInputController input)
        {
            UnsubscribeInput();
            cellGrid = grid;
            inputController = input;
            SubscribeInput();
            SetHoveredCell(null);
        }

        private void OnEnable()
        {
            cellGrid ??= FindAnyObjectByType<CellGrid>();
            inputController ??= FindAnyObjectByType<GameplayInputController>();
            SubscribeInput();
            Refresh();
        }

        private void OnDisable()
        {
            UnsubscribeInput();
            SubscribeCell(null);
            if (root != null) root.SetActive(false);
        }

        private void SubscribeInput()
        {
            if (inputController == null)
            {
                return;
            }

            inputController.HoveredCellChanged -= SetHoveredCell;
            inputController.HoveredCellChanged += SetHoveredCell;
        }

        private void UnsubscribeInput()
        {
            if (inputController != null)
            {
                inputController.HoveredCellChanged -= SetHoveredCell;
            }
        }

        private void SetHoveredCell(Cell cell)
        {
            if (hoveredCell == cell)
            {
                Refresh();
                return;
            }

            SubscribeCell(cell);
            Refresh();
        }

        private void SubscribeCell(Cell cell)
        {
            if (hoveredCell != null)
            {
                hoveredCell.TerrainEffectsChanged -= OnTerrainEffectsChanged;
            }

            hoveredCell = cell;
            if (hoveredCell != null)
            {
                hoveredCell.TerrainEffectsChanged += OnTerrainEffectsChanged;
            }
        }

        private void OnTerrainEffectsChanged(Cell cell)
        {
            if (cell == hoveredCell)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            bool hasSplitLabels = titleLabel != null && detailsLabel != null;
            if (root == null || (!hasSplitLabels && combinedLabel == null) || hoveredCell == null)
            {
                if (root != null) root.SetActive(false);
                return;
            }

            root.SetActive(true);
            string tileName = !string.IsNullOrWhiteSpace(hoveredCell.TilePreset?.name)
                ? hoveredCell.TilePreset.name
                : "Tile";
            StringBuilder details = new StringBuilder();
            details.Append("Move Cost ").Append(hoveredCell.MovementCost.ToString("0.##"));

            var effects = (cellGrid?.GetTerrainEffects(hoveredCell) ?? hoveredCell.TerrainEffects)
                .Where(effect => effect?.Data != null)
                .OrderBy(effect => effect.Data.Name)
                .ToList();
            if (effects.Count == 0)
            {
                details.Append("\nNo terrain effects");
            }
            else
            {
                details.Append('\n');
                for (int i = 0; i < effects.Count; i++)
                {
                    TerrainEffectInstance effect = effects[i];
                    if (i > 0) details.Append("   |   ");
                    details.Append("<b>").Append(effect.Data.Name).Append("</b>");
                    if (!effect.IsPermanent) details.Append(" (").Append(effect.RemainingRounds).Append(" rounds)");
                    if (effect.Data.OverlayStyle == TerrainEffectOverlayStyle.BlackFog)
                    {
                        details.Append(" (Depth ").Append(effect.Intensity + 1).Append(')');
                    }
                    if (!string.IsNullOrWhiteSpace(effect.Data.Description))
                    {
                        details.Append(": ").Append(effect.Data.Description);
                    }
                }
            }

            if (hasSplitLabels)
            {
                titleLabel.text = tileName;
                detailsLabel.text = details.ToString();
            }

            if (combinedLabel != null)
            {
                combinedLabel.text = $"<b>{tileName}</b>   {details}";
            }
        }
    }
}
