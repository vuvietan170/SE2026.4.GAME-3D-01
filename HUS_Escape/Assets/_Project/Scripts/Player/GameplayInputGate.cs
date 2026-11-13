using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HUSEscape.Player
{
    /// <summary>Scene-local modal input/pause lease; preserves controller state and existing action enablement.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-10000)]
    public sealed class GameplayInputGate : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private ThirdPersonCameraFollow cameraFollow;
        private readonly List<InputAction> suspended = new List<InputAction>();
        private float previousTimeScale;
        private int releaseFrame = -1;
        private int resumeFrame = -1;
        private bool cameraWasEnabled;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        /// <summary>Whether gameplay input is blocked, including the closing-frame grace period.</summary>
        public bool IsBlocked { get; private set; }
        /// <summary>Pauses gameplay without disabling or resetting the player/camera components.</summary>
        public void Block()
        {
            releaseFrame = -1;
            resumeFrame = -1;
            if (IsBlocked) return;
            IsBlocked = true; previousTimeScale = Time.timeScale; Time.timeScale = 0;
            cameraWasEnabled = cameraFollow != null && cameraFollow.enabled;
            if (cameraWasEnabled) cameraFollow.enabled = false;
            previousCursorLock = Cursor.lockState; previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var map = inputActions != null ? inputActions.FindActionMap("Player", false) : null;
            if (map == null) return;
            foreach (var action in map.actions) if (action.enabled) { suspended.Add(action); action.Disable(); }
        }
        /// <summary>Closes safely without leaking the same key press into gameplay.</summary>
        public void ReleaseAfterFrame() { if (IsBlocked) releaseFrame = Time.frameCount + 1; }
        private void Update()
        {
            if (releaseFrame >= 0 && Time.frameCount > releaseFrame && resumeFrame < 0)
            { Time.timeScale = previousTimeScale; resumeFrame = Time.frameCount + 1; }
            if (resumeFrame >= 0 && Time.frameCount >= resumeFrame) Restore();
        }
        private void Restore()
        {
            if (!IsBlocked) return;
            IsBlocked = false; releaseFrame = resumeFrame = -1; Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible;
            foreach (var action in suspended) action.Enable();
            suspended.Clear();
            if (cameraWasEnabled && cameraFollow != null)
            {
                if (Time.deltaTime > 0 || !cameraFollow.gameObject.activeInHierarchy) cameraFollow.enabled = true;
                else cameraFollow.StartCoroutine(ResumeCamera(cameraFollow));
            }
        }
        private IEnumerator ResumeCamera(ThirdPersonCameraFollow follow)
        {
            // Never run the unchanged SmoothDamp camera with deltaTime=0 after a pause.
            yield return null;
            while (IsBlocked || Time.deltaTime <= 0) yield return null;
            if (follow != null) follow.enabled = true;
        }
        private void OnDisable() => Restore();
        private void OnDestroy() => Restore();
    }
}
