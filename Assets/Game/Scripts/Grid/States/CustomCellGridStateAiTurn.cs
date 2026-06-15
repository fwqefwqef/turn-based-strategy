using System.Collections.Generic;
using TbsFramework.Cells;
using UnityEngine;
using Windy.Srpg.Game.AI;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid.States
{
    public sealed class CustomCellGridStateAiTurn : CustomCellGridState
    {
        private readonly CustomAiPlayer aiPlayer;
        private Dictionary<Cell, AiDebugInfo> cellDebugInfo;

        public CustomCellGridStateAiTurn(CustomCellGrid cellGrid, CustomAiPlayer aiPlayer) : base(cellGrid)
        {
            this.aiPlayer = aiPlayer;
        }

        public Dictionary<Cell, AiDebugInfo> CellDebugInfo
        {
            get => cellDebugInfo;
            set
            {
                cellDebugInfo = value;
                if (!IsDebugEnabled || cellDebugInfo == null)
                {
                    return;
                }

                foreach ((Cell cell, AiDebugInfo debugInfo) in cellDebugInfo)
                {
                    if (cell != null && debugInfo != null)
                    {
                        cell.SetColor(debugInfo.Color);
                    }
                }
            }
        }

        public Dictionary<CustomUnit, string> UnitDebugInfo { get; set; }

        public override void OnCellDeselected(IBattleCell cell)
        {
            base.OnCellDeselected(cell);
            Cell legacyCell = ResolveLegacyCell(cell);
            if (IsDebugEnabled
                && legacyCell != null
                && CellDebugInfo != null
                && CellDebugInfo.TryGetValue(legacyCell, out AiDebugInfo debugInfo)
                && debugInfo != null)
            {
                legacyCell.SetColor(debugInfo.Color);
            }
        }

        public override void OnCellClicked(IBattleCell cell)
        {
            Cell legacyCell = ResolveLegacyCell(cell);
            if (IsDebugEnabled
                && legacyCell != null
                && CellDebugInfo != null
                && CellDebugInfo.TryGetValue(legacyCell, out AiDebugInfo debugInfo)
                && debugInfo != null
                && !string.IsNullOrWhiteSpace(debugInfo.Metadata))
            {
                Debug.Log(debugInfo.Metadata);
            }
        }

        public override void OnCustomUnitClicked(CustomUnit customUnit)
        {
            if (IsDebugEnabled
                && customUnit != null
                && UnitDebugInfo != null
                && UnitDebugInfo.TryGetValue(customUnit, out string debugText)
                && !string.IsNullOrWhiteSpace(debugText))
            {
                Debug.Log(debugText);
            }
        }

        private bool IsDebugEnabled => aiPlayer != null && aiPlayer.DebugMode;
    }
}
