using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HUSEscape.UI
{
    /// <summary>Presents menu availability and emits instance-based user intent.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField] private Button startButton = null;
        [SerializeField] private Text statusText = null;
        [SerializeField] private EventSystem eventSystem = null;
        private bool available;

        /// <summary>Raised once per enabled Start activation, with this view as its source.</summary>
        public event Action<MainMenuView> StartRequested;

        /// <summary>Raised during destruction so the persistent flow can release scene references.</summary>
        public event Action<MainMenuView> Destroying;

        /// <summary>Gets whether a flow has explicitly bound this view.</summary>
        public bool IsBound { get; private set; }

        /// <summary>Gets whether Start can currently emit a request.</summary>
        public bool CanStart => IsBound && available && isActiveAndEnabled && startButton != null && startButton.interactable;

        /// <summary>Gets the current user-facing status text.</summary>
        public string Status => statusText != null ? statusText.text : string.Empty;

        private void Awake()
        {
            IsBound = false;
            SetState(false, string.Empty);
        }

        private void OnEnable()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartClicked);
                startButton.onClick.AddListener(OnStartClicked);
                startButton.interactable = IsBound && available;
            }
        }

        /// <summary>Marks this view as owned by a flow, initially with Start disabled.</summary>
        public void Bind()
        {
            IsBound = true;
            SetState(false, string.Empty);
        }

        /// <summary>Releases ownership and disables all Start requests.</summary>
        public void Unbind()
        {
            IsBound = false;
            SetState(false, string.Empty);
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null
                && eventSystem.currentSelectedGameObject.transform.IsChildOf(transform))
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }

        /// <summary>Updates interaction and status without exposing Core or scene-loading APIs.</summary>
        /// <param name="canStart">Whether the bound Core is ready to accept Start.</param>
        /// <param name="message">A user-facing status, or an empty string.</param>
        public void SetState(bool canStart, string message)
        {
            bool select = !available && canStart && IsBound;
            available = canStart;
            if (startButton != null) { startButton.interactable = IsBound && available && isActiveAndEnabled; }
            if (statusText != null && statusText.text != (message ?? string.Empty)) { statusText.text = message ?? string.Empty; }
            if (select && CanStart && eventSystem != null && eventSystem.isActiveAndEnabled)
            {
                eventSystem.SetSelectedGameObject(startButton.gameObject);
            }
        }

        private void OnStartClicked()
        {
            if (!CanStart) { return; }
            // Latch before notification, including repeated pointer/submit events in the same frame.
            SetState(false, Status);
            Raise(StartRequested);
        }

        private void OnDisable()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartClicked);
                startButton.interactable = false;
            }
        }

        private void OnDestroy()
        {
            Raise(Destroying);
            StartRequested = null;
            Destroying = null;
        }

        private void Raise(Action<MainMenuView> listeners)
        {
            if (listeners == null) { return; }
            foreach (Action<MainMenuView> listener in listeners.GetInvocationList())
            {
                try { listener(this); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }
    }
}
