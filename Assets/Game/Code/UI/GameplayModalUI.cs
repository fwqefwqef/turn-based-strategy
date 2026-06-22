using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Windy.Srpg.Game.UI
{
    public abstract class GameplayModalUI : MonoBehaviour
    {
        private static readonly List<GameplayModalUI> activeModalStack = new List<GameplayModalUI>();

        [SerializeField] private bool blocksGameplayInput = true;
        [SerializeField] private bool participatesInKeyboardNavigation = true;

        private GameObject modalRoot;
        private Button defaultFocusButton;
        private Button cancelInputButton;
        private bool isVisible;

        public static IReadOnlyList<GameplayModalUI> ActiveModalStack => activeModalStack;

        public bool IsVisible => isVisible;
        public bool BlocksGameplayInput => isVisible && blocksGameplayInput;
        public bool ParticipatesInKeyboardNavigation => isVisible && participatesInKeyboardNavigation;
        public GameObject ModalRoot => modalRoot;

        protected virtual void Awake()
        {
        }

        protected virtual void OnDisable()
        {
            ForceHiddenWithoutMutatingScene();
        }

        protected virtual void OnDestroy()
        {
            ForceHiddenWithoutMutatingScene();
        }

        protected void ConfigureModal(GameObject root, Button initialFocusButton = null, Button cancelInputButton = null)
        {
            modalRoot = root;
            defaultFocusButton = initialFocusButton;
            this.cancelInputButton = cancelInputButton;
        }

        protected void SetDefaultFocusButton(Button button)
        {
            defaultFocusButton = button;
        }

        protected void SetCancelButton(Button button)
        {
            cancelInputButton = button;
        }

        protected void SetModalVisible(bool visible)
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(visible);
            }

            SetTrackedVisibility(visible);
        }

        public bool ContainsButton(Button button)
        {
            return isVisible
                && modalRoot != null
                && button != null
                && button.transform.IsChildOf(modalRoot.transform);
        }

        public Button GetPreferredFocusButton()
        {
            if (!ParticipatesInKeyboardNavigation || modalRoot == null)
            {
                return null;
            }

            if (IsButtonUsable(defaultFocusButton))
            {
                return defaultFocusButton;
            }

            Button[] buttons = modalRoot.GetComponentsInChildren<Button>(true);
            return buttons.FirstOrDefault(IsButtonUsable);
        }

        public bool TryCancelFromInput()
        {
            if (!isVisible)
            {
                return false;
            }

            return HandleCancelFromInput();
        }

        public static GameplayModalUI GetTopmostActiveModal(bool requireKeyboardNavigation = false)
        {
            for (int i = activeModalStack.Count - 1; i >= 0; i--)
            {
                GameplayModalUI modal = activeModalStack[i];
                if (modal == null || !modal.IsVisible)
                {
                    continue;
                }

                if (requireKeyboardNavigation && !modal.ParticipatesInKeyboardNavigation)
                {
                    continue;
                }

                return modal;
            }

            return null;
        }

        public static bool TryCancelTopmostActiveModal()
        {
            GameplayModalUI modal = GetTopmostActiveModal();
            return modal != null && modal.TryCancelFromInput();
        }

        public static bool HasAnyBlockingModal()
        {
            return activeModalStack.Any(modal => modal != null && modal.BlocksGameplayInput);
        }

        public static bool HasAnyKeyboardNavigationModal()
        {
            return activeModalStack.Any(modal => modal != null && modal.ParticipatesInKeyboardNavigation);
        }

        private void SetTrackedVisibility(bool visible)
        {
            if (isVisible == visible)
            {
                return;
            }

            isVisible = visible;
            if (visible)
            {
                activeModalStack.Remove(this);
                activeModalStack.Add(this);
            }
            else
            {
                activeModalStack.Remove(this);
            }

            OnModalVisibilityChanged(visible);
        }

        private void ForceHiddenWithoutMutatingScene()
        {
            if (!isVisible)
            {
                return;
            }

            isVisible = false;
            activeModalStack.Remove(this);
            OnModalVisibilityChanged(false);
        }

        protected virtual void OnModalVisibilityChanged(bool isVisible)
        {
        }

        protected virtual bool HandleCancelFromInput()
        {
            if (IsButtonUsable(cancelInputButton))
            {
                cancelInputButton.onClick.Invoke();
                return true;
            }

            SetModalVisible(false);
            return true;
        }

        private static bool IsButtonUsable(Button button)
        {
            return button != null
                && button.isActiveAndEnabled
                && button.interactable
                && button.gameObject.activeInHierarchy;
        }
    }
}
