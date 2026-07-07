using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Localization;

namespace Windy.Srpg.Game.UI
{
    [AddComponentMenu("UI/Battle Result UI")]
    public sealed class BattleResultUI : GameplayModalUI
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button exitButton;

        private Action onExit;
        private bool isConfigured;
        private bool exitListenerRegistered;

        protected override void Awake()
        {
            base.Awake();
            EnsureConfigured();
            SetModalVisible(false);
        }

        public void Show(BattleEndedEventArgs args, Action exitAction)
        {
            EnsureConfigured();
            onExit = exitAction;

            bool victory = args?.WinningPlayerNumbers?.Contains(0) == true;
            bool defeat = args?.LosingPlayerNumbers?.Contains(0) == true;
            string text = victory
                ? GameTextCatalog.Get("ui.battle_result.victory", "Victory")
                : defeat
                    ? GameTextCatalog.Get("ui.battle_result.defeat", "Defeat")
                    : GameTextCatalog.Get("ui.battle_result.finished", "Battle Finished");

            if (resultText != null)
            {
                resultText.text = text;
            }

            SetModalVisible(true);
        }

        private void Exit()
        {
            onExit?.Invoke();
        }

        private void EnsureConfigured()
        {
            if (!isConfigured)
            {
                ConfigureModal(panelRoot != null ? panelRoot : gameObject, exitButton, exitButton);
                isConfigured = true;
            }

            if (!exitListenerRegistered && exitButton != null)
            {
                exitButton.onClick.AddListener(Exit);
                exitListenerRegistered = true;
            }
        }
    }
}
