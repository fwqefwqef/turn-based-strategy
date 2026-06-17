using System.Collections.Generic;
using UnityEngine;
using Windy.Srpg.Game.AI;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid.States
{
    public sealed class CellGridStateAiTurn : CellGridState
    {
        private readonly AiPlayer aiPlayer;
        private Dictionary<BattleSquareCell, AiDebugInfo> cellDebugInfo;

        public CellGridStateAiTurn(CellGrid cellGrid, AiPlayer aiPlayer) : base(cellGrid)
        {
            this.aiPlayer = aiPlayer;
        }

        public Dictionary<BattleSquareCell, AiDebugInfo> CellDebugInfo
        {
            get => cellDebugInfo;
            set
            {
                cellDebugInfo = value;
                if (!IsDebugEnabled || cellDebugInfo == null)
                {
                    return;
                }

                foreach ((BattleSquareCell cell, AiDebugInfo debugInfo) in cellDebugInfo)
                {
                    if (cell != null && debugInfo != null)
                    {
                        cell.SetColor(debugInfo.Color);
                    }
                }
            }
        }

        public Dictionary<Unit, string> UnitDebugInfo { get; set; }

        public override void OnCellDeselected(IBattleCell cell)
        {
            base.OnCellDeselected(cell);
            BattleSquareCell boardCell = ResolveBoardCell(cell);
            if (IsDebugEnabled
                && boardCell != null
                && CellDebugInfo != null
                && CellDebugInfo.TryGetValue(boardCell, out AiDebugInfo debugInfo)
                && debugInfo != null)
            {
                boardCell.SetColor(debugInfo.Color);
            }
        }

        public override void OnCellClicked(IBattleCell cell)
        {
            BattleSquareCell boardCell = ResolveBoardCell(cell);
            if (IsDebugEnabled
                && boardCell != null
                && CellDebugInfo != null
                && CellDebugInfo.TryGetValue(boardCell, out AiDebugInfo debugInfo)
                && debugInfo != null
                && !string.IsNullOrWhiteSpace(debugInfo.Metadata))
            {
                Debug.Log(debugInfo.Metadata);
            }
        }

        public override void OnUnitClicked(Unit customUnit)
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

