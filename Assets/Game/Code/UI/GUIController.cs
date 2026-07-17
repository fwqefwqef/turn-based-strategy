using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        [SerializeField] private GameplayInputController gameplayInputController;
        [SerializeField] private SceneButtonTextOverflowFitter buttonTextOverflowFitter;
        [SerializeField] private BattleResultUI battleResultUi;
        [SerializeField] private string overworldMenuSceneName = "OverworldMenu";

        private void Awake()
        {
            CellGrid = ResolveSceneReference(CellGrid);
            if (CellGrid == null)
            {
                enabled = false;
                return;
            }

            preBattleUiController = ResolveSceneReference(preBattleUiController);
            preBattleUiController?.Initialize(CellGrid);

            gameplayInputController = EnsureLocalComponent(gameplayInputController);
            gameplayInputController.Initialize(CellGrid);

            buttonTextOverflowFitter = EnsureLocalComponent(buttonTextOverflowFitter);
            buttonTextOverflowFitter.FitAllButtons();

            battleResultUi = ResolveSceneReference(battleResultUi);

            CellGrid.LevelLoading += OnLevelLoading;
            CellGrid.LevelInitialized += OnLevelLoadingDone;
            CellGrid.BattleEnded += OnGameEnded;
            CellGrid.BattleTurnEnded += OnTurnEnded;
            CellGrid.BattleStarted += OnGameStarted;
        }

        private T EnsureLocalComponent<T>(T component) where T : Component
        {
            return component != null
                ? component
                : GetComponent<T>() ?? gameObject.AddComponent<T>();
        }

        private static T ResolveSceneReference<T>(T component, bool includeInactive = false) where T : UnityEngine.Object
        {
            if (component != null)
            {
                return component;
            }

            if (!includeInactive)
            {
                return FindAnyObjectByType<T>();
            }

            return FindObjectsByType<T>(FindObjectsInactive.Include).FirstOrDefault(found => found != null);
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

            if (e?.WinningPlayerNumbers?.Contains(0) == true)
            {
                CellGrid.SaveVictoryProgress();
            }

            battleResultUi?.Show(e, ExitScene);
        }

        private void ExitScene()
        {
            if (string.IsNullOrWhiteSpace(overworldMenuSceneName))
            {
                Debug.LogWarning("GUIController: Overworld menu scene name is empty.");
                return;
            }

            SceneManager.LoadScene(overworldMenuSceneName);
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
        }

        public void EndTurn()
        {
            CellGrid.RequestEndTurn();
        }
    }
}
