using System;
using System.Collections.Generic;
using HUSEscape.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HUSEscape.Player
{
    public interface IGameplayPauseOwner { bool IsPauseOpen { get; } }

    /// <summary>Actor-owned actions and modal leases; never changes the serialized shared action asset.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-10000)]
    public sealed class GameplayInputGate : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private ThirdPersonCameraFollow cameraFollow;
        private InputActionAsset runtimeActions;
        private InputActionMap playerMap, menuMap;
        private readonly Dictionary<object, bool> owners = new Dictionary<object, bool>();
        private readonly List<object> ownerOrder = new List<object>();
        private readonly HashSet<string> consumedUi = new HashSet<string>();
        private int consumedFrame = -1;
        private readonly List<InputAction> suspended = new List<InputAction>();
        private readonly object legacyOwner = new object();
        private GameplayPauseCoordinator coordinator;
        private bool inputSuspended, localEnvironment;
        private float previousScale;
        private CursorLockMode previousCursor;
        private bool previousVisible;
        private int releaseFrame = -1;
        public event Action Deactivated;
        public sealed class Snapshot
        {
            internal GameplayInputGate Owner;
            internal Dictionary<object, bool> Owners;
            internal object[] Order;
            internal InputAction[] ResumeActions;
            internal bool CameraEnabled, LocalEnvironment;
            internal float PreviousScale;
            internal CursorLockMode PreviousCursor;
            internal bool PreviousVisible;
        }
        public bool IsBlocked => !isActiveAndEnabled || runtimeActions == null || owners.Count > 0 || Time.frameCount <= releaseFrame ||
            (coordinator != null && (!coordinator.Available || coordinator.Manager.CurrentState != GameState.Playing));
        public bool CanOpenMenus => isActiveAndEnabled && runtimeActions != null && Time.frameCount > releaseFrame &&
            (coordinator == null || (coordinator.Available && (coordinator.Manager.CurrentState == GameState.Playing || coordinator.Manager.CurrentState == GameState.Paused)));
        internal void Configure(InputActionAsset asset, ThirdPersonCameraFollow camera)
        {
            if (inputActions == null) inputActions = asset;
            if (cameraFollow == null) cameraFollow = camera;
            EnsureActions(); Sync();
        }
        private void Awake() => EnsureActions();
        private void EnsureActions()
        {
            if (runtimeActions != null || inputActions == null) return;
            runtimeActions = Instantiate(inputActions); runtimeActions.hideFlags = HideFlags.HideAndDontSave;
            runtimeActions.Disable();
            playerMap = runtimeActions.FindActionMap("Player", true);
            menuMap = runtimeActions.FindActionMap("GameplayUI", false);
            playerMap.Enable(); if (isActiveAndEnabled) menuMap?.Enable();
        }
        public InputAction PlayerAction(string name) { EnsureActions(); return playerMap?.FindAction(name, false); }
        public bool PlayerPressed(string name) => !IsBlocked && (PlayerAction(name)?.WasPressedThisFrame() ?? false);
        public bool UiPressed(string name) => isActiveAndEnabled &&
            (consumedFrame != Time.frameCount || !consumedUi.Contains(name)) &&
            (menuMap?.FindAction(name, false)?.WasPressedThisFrame() ?? false);
        public bool IsTopModal(object owner)
        {
            for (int i = ownerOrder.Count - 1; i >= 0; i--)
                if (owners.TryGetValue(ownerOrder[i], out bool pause) && pause) return ReferenceEquals(ownerOrder[i], owner);
            return false;
        }
        public bool ConsumeUi(string name, object owner)
        {
            if (!IsTopModal(owner) || !UiPressed(name)) return false;
            if (consumedFrame != Time.frameCount) { consumedUi.Clear(); consumedFrame = Time.frameCount; }
            consumedUi.Add(name); return true;
        }
        public void Bind(GameBootstrap core)
        {
            if (coordinator != null && coordinator.IsFor(core)) { Sync(); return; }
            if (coordinator != null) coordinator.SetPaused(this, false);
            RestoreLocalEnvironment(); coordinator = GameplayPauseCoordinator.Bind(core); Sync();
        }
        private void Start()
        {
            if (coordinator != null) return;
            GameBootstrap owner = null;
            foreach (var candidate in FindObjectsByType<GameBootstrap>(FindObjectsInactive.Include))
            {
                if (!candidate.IsInitialized || !candidate.isActiveAndEnabled) continue;
                if (owner != null) { Debug.LogError("Input gate requires one Core owner.", this); return; }
                owner = candidate;
            }
            // Legacy SampleScene can run without Core. Journey injects Core explicitly when it creates one later.
            if (owner != null) Bind(owner);
        }
        public bool Acquire(object owner, bool pauseGame = true)
        {
            if (owner == null || !isActiveAndEnabled || runtimeActions == null) return false;
            if (pauseGame && coordinator != null && !coordinator.Available) return false;
            releaseFrame = -1;
            if (!owners.ContainsKey(owner)) ownerOrder.Add(owner);
            owners[owner] = pauseGame || (owners.TryGetValue(owner, out bool previous) && previous);
            Sync(); return true;
        }
        public void Release(object owner)
        {
            if (owner == null || !owners.Remove(owner)) return;
            ownerOrder.Remove(owner);
            if (owners.Count == 0) releaseFrame = Time.frameCount + 1;
            Sync();
        }
        public void Block() => Acquire(legacyOwner);
        public void HoldForTravel() => Acquire(legacyOwner, false);
        public void ReleaseAfterFrame()
        {
            owners.Remove(legacyOwner); ownerOrder.Remove(legacyOwner);
            if (owners.Count == 0) releaseFrame = Time.frameCount + 1;
            Sync();
        }
        public void ReleaseImmediately() { owners.Clear(); ownerOrder.Clear(); releaseFrame = -1; Sync(); }
        public Snapshot CaptureSnapshot()
        {
            var resume = new List<InputAction>();
            if (inputSuspended) resume.AddRange(suspended);
            else if (playerMap != null) foreach (var action in playerMap.actions) if (action.enabled) resume.Add(action);
            return new Snapshot { Owner = this, Owners = new Dictionary<object, bool>(owners), Order = ownerOrder.ToArray(), ResumeActions = resume.ToArray(),
                CameraEnabled = cameraFollow != null && cameraFollow.enabled, LocalEnvironment = localEnvironment,
                PreviousScale = previousScale, PreviousCursor = previousCursor, PreviousVisible = previousVisible };
        }
        public void RestoreSnapshot(Snapshot snapshot)
        {
            if (snapshot == null || snapshot.Owner != this) throw new ArgumentException("Input snapshot belongs to another gate.");
            owners.Clear(); ownerOrder.Clear();
            foreach (var pair in snapshot.Owners)
            {
                if (pair.Key is UnityEngine.Object obj && obj == null) continue;
                if (pair.Key is IGameplayPauseOwner modal && !modal.IsPauseOpen) continue;
                owners.Add(pair.Key, pair.Value);
            }
            foreach (var owner in snapshot.Order) if (owners.ContainsKey(owner)) ownerOrder.Add(owner);
            releaseFrame = -1; playerMap?.Disable(); inputSuspended = true;
            suspended.Clear(); suspended.AddRange(snapshot.ResumeActions);
            localEnvironment = snapshot.LocalEnvironment; previousScale = snapshot.PreviousScale;
            previousCursor = snapshot.PreviousCursor; previousVisible = snapshot.PreviousVisible;
            if (cameraFollow != null) cameraFollow.enabled = snapshot.CameraEnabled;
            Sync();
        }
        private void Update()
        {
            var dead = new List<object>();
            foreach (var owner in owners.Keys) if (owner is UnityEngine.Object obj && obj == null) dead.Add(owner);
            foreach (var owner in dead) { owners.Remove(owner); ownerOrder.Remove(owner); }
            Sync();
        }
        private void Sync()
        {
            bool pause = false;
            foreach (bool request in owners.Values) pause |= request;
            if (coordinator != null) coordinator.SetPaused(this, isActiveAndEnabled && pause);
            else if (isActiveAndEnabled && pause)
            {
                if (!localEnvironment) { previousScale = Time.timeScale; previousCursor = Cursor.lockState; previousVisible = Cursor.visible; localEnvironment = true; }
                Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
            else RestoreLocalEnvironment();
            if (playerMap == null) return;
            if (IsBlocked)
            {
                if (!inputSuspended)
                {
                    suspended.Clear(); foreach (var action in playerMap.actions) if (action.enabled) suspended.Add(action);
                    inputSuspended = true;
                }
                playerMap.Disable();
            }
            else if (inputSuspended)
            {
                inputSuspended = false; foreach (var action in suspended) action.Enable(); suspended.Clear();
            }
        }
        private void RestoreLocalEnvironment()
        {
            if (!localEnvironment) return;
            localEnvironment = false; Time.timeScale = previousScale; Cursor.lockState = previousCursor; Cursor.visible = previousVisible;
        }
        private void OnEnable() { releaseFrame = Time.frameCount + 1; EnsureActions(); menuMap?.Enable(); Sync(); }
        private void OnDisable()
        {
            if (Deactivated != null) foreach (Action handler in Deactivated.GetInvocationList())
                try { handler(); } catch (Exception error) { Debug.LogWarning(error.Message, this); }
            owners.Clear(); ownerOrder.Clear(); releaseFrame = -1;
            if (coordinator != null) coordinator.SetPaused(this, false);
            RestoreLocalEnvironment(); Sync(); menuMap?.Disable();
        }
        private void OnDestroy() { if (runtimeActions != null) Destroy(runtimeActions); }
    }
}
