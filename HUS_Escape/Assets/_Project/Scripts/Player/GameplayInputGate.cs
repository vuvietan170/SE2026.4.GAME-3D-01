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
        public sealed class Snapshot
        {
            internal Snapshot() { }
            internal GameplayInputGate Owner;
            internal bool Blocked, CameraEnabled, CameraPending, CameraWasEnabled, Visible, PreviousVisible;
            internal float Scale, PreviousScale;
            internal CursorLockMode Cursor, PreviousCursor;
            internal InputAction[] Actions, Suspended;
            internal bool[] Enabled;
            internal int ReleaseDelay, ResumeDelay;
        }
        private Coroutine cameraResume;
        private readonly List<InputAction> suspended = new List<InputAction>();
        private float previousTimeScale;
        private int releaseFrame = -1;
        private int resumeFrame = -1;
        private bool cameraWasEnabled;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        /// <summary>Whether gameplay input is blocked, including the closing-frame grace period.</summary>
        public bool IsBlocked { get; private set; }
        /// <summary>Captures this gate's lease and actual action enablement before a travel request.</summary>
        public Snapshot CaptureSnapshot()
        {
            var actions = new List<InputAction>();
            var map = inputActions != null ? inputActions.FindActionMap("Player", false) : null;
            if (map != null) foreach (var action in map.actions) actions.Add(action);
            var enabled = new bool[actions.Count];
            for (int i = 0; i < actions.Count; i++) enabled[i] = actions[i].enabled;
            return new Snapshot {
                Owner = this, Blocked = IsBlocked, Scale = Time.timeScale, PreviousScale = previousTimeScale,
                CameraEnabled = cameraFollow != null && cameraFollow.enabled, CameraPending = cameraResume != null, CameraWasEnabled = cameraWasEnabled,
                Cursor = Cursor.lockState, Visible = Cursor.visible, PreviousCursor = previousCursorLock, PreviousVisible = previousCursorVisible,
                Actions = actions.ToArray(), Enabled = enabled, Suspended = suspended.ToArray(),
                ReleaseDelay = releaseFrame < 0 ? -1 : Mathf.Max(0, releaseFrame - Time.frameCount),
                ResumeDelay = resumeFrame < 0 ? -1 : Mathf.Max(0, resumeFrame - Time.frameCount)
            };
        }
        /// <summary>Restores an existing scene after a failed destination disabled shared input actions.</summary>
        public void RestoreSnapshot(Snapshot snapshot)
        {
            if (snapshot == null || snapshot.Owner != this) throw new System.ArgumentException("Input snapshot belongs to another gate.");
            if (cameraResume != null && cameraFollow != null) cameraFollow.StopCoroutine(cameraResume);
            cameraResume = null;
            IsBlocked = snapshot.Blocked; Time.timeScale = snapshot.Scale; previousTimeScale = snapshot.PreviousScale;
            cameraWasEnabled = snapshot.CameraWasEnabled; previousCursorLock = snapshot.PreviousCursor; previousCursorVisible = snapshot.PreviousVisible;
            releaseFrame = snapshot.ReleaseDelay < 0 ? -1 : Time.frameCount + snapshot.ReleaseDelay;
            resumeFrame = snapshot.ResumeDelay < 0 ? -1 : Time.frameCount + snapshot.ResumeDelay;
            suspended.Clear(); suspended.AddRange(snapshot.Suspended);
            Cursor.lockState = snapshot.Cursor; Cursor.visible = snapshot.Visible;
            // Set the camera first: its OnDisable changes Player/Look too.
            if (cameraFollow != null)
            {
                cameraFollow.enabled = snapshot.CameraEnabled && Time.deltaTime > 0;
                if ((snapshot.CameraEnabled || snapshot.CameraPending) && !cameraFollow.enabled) cameraResume = cameraFollow.StartCoroutine(ResumeCamera(cameraFollow));
            }
            for (int i = 0; i < snapshot.Actions.Length; i++)
                if (snapshot.Enabled[i]) snapshot.Actions[i].Enable(); else snapshot.Actions[i].Disable();
        }
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
        /// <summary>Ends the source scene's pause lease after loading, before destination actors activate.</summary>
        public void ReleaseImmediately() => Restore();
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
                else cameraResume = cameraFollow.StartCoroutine(ResumeCamera(cameraFollow));
            }
        }
        private IEnumerator ResumeCamera(ThirdPersonCameraFollow follow)
        {
            // Never run the unchanged SmoothDamp camera with deltaTime=0 after a pause.
            yield return null;
            while (IsBlocked || Time.deltaTime <= 0) yield return null;
            if (follow != null) follow.enabled = true;
            cameraResume = null;
        }
        private void OnDisable() => Restore();
        private void OnDestroy() => Restore();
    }
}
