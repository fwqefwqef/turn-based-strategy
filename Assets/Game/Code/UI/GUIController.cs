using System;
using UnityEngine;
using UnityEngine.UI;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.Players;

namespace Windy.Srpg.Game.UI
{
    public class GUIController : MonoBehaviour
    {
        public CellGrid CellGrid;
        public Button EndTurnButton;
        [SerializeField] private PreBattleUIController preBattleUiController;

        private void Awake()
        {
            if (CellGrid == null)
            {
                CellGrid = FindAnyObjectByType<CellGrid>();
            }

            if (CellGrid == null)
            {
                enabled = false;
                return;
            }

            if (preBattleUiController == null)
            {
                preBattleUiController = GetComponent<PreBattleUIController>();
            }

            if (preBattleUiController == null)
            {
                preBattleUiController = FindAnyObjectByType<PreBattleUIController>();
            }

            if (preBattleUiController == null)
            {
                preBattleUiController = gameObject.AddComponent<PreBattleUIController>();
            }

            preBattleUiController.Initialize(CellGrid, EndTurnButton);

            CellGrid.LevelLoading += OnLevelLoading;
            CellGrid.LevelInitialized += OnLevelLoadingDone;
            CellGrid.BattleEnded += OnGameEnded;
            CellGrid.BattleTurnEnded += OnTurnEnded;
            CellGrid.BattleStarted += OnGameStarted;
        }

        private void OnDestroy()
        {
            if (CellGrid == null)
            {
                return;
            }

            CellGrid.LevelLoading -= OnLevelLoading;
            CellGrid.LevelInitialized -= OnLevelLoadingDone;
            CellGrid.BattleEnded -= OnGameEnded;
            CellGrid.BattleTurnEnded -= OnTurnEnded;
            CellGrid.BattleStarted -= OnGameStarted;
        }

        private void OnGameStarted(object sender, EventArgs e)
        {
            if (EndTurnButton != null)
            {
                EndTurnButton.interactable = CellGrid.IsHumanTurn;
            }
        }

        private void OnTurnEnded(object sender, EventArgs e)
        {
            if (EndTurnButton != null)
            {
                EndTurnButton.interactable = CellGrid.IsHumanTurn;
            }
        }

        private void OnGameEnded(object sender, BattleEndedEventArgs e)
        {
            if (EndTurnButton != null)
            {
                EndTurnButton.interactable = false;
            }
        }

        private void OnLevelLoading(object sender, EventArgs e)
        {
        }

        private void OnLevelLoadingDone(object sender, EventArgs e)
        {
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M) && CellGrid.CurrentState is not CellGridStateAiTurn)
            {
                EndTurn();
            }

            if (Input.GetMouseButtonDown(1) && CellGrid.CurrentState is IRightClickHandler)
            {
                CellGrid.ProcessSceneRightClick();
            }
        }

        public void EndTurn()
        {
            CellGrid.RequestEndTurn();
        }
    }
}

