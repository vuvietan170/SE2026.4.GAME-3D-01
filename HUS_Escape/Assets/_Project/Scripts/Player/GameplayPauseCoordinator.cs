using System.Collections.Generic;
using HUSEscape.Core;
using UnityEngine;

namespace HUSEscape.Player
{
    /// <summary>One pause owner per persistent Core; survives scene-local UI teardown during Loading.</summary>
    [DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class GameplayPauseCoordinator : MonoBehaviour
    {
        private readonly HashSet<GameplayInputGate> gates = new HashSet<GameplayInputGate>();
        private GameBootstrap bootstrap;
        private GameManager manager;
        private bool ownsPause, environmentHeld;
        private float previousScale;
        private CursorLockMode previousCursor;
        private bool previousVisible;
        internal GameManager Manager => manager;
        internal bool IsFor(GameBootstrap core) => bootstrap == core;
        internal bool Available => bootstrap != null && bootstrap.TryGetServices(out var current, out _) && ReferenceEquals(current, manager);
        internal static GameplayPauseCoordinator Bind(GameBootstrap core)
        {
            if (!core.TryGetServices(out var manager, out _)) return null;
            var coordinator = core.GetComponent<GameplayPauseCoordinator>();
            if (coordinator == null) coordinator = core.gameObject.AddComponent<GameplayPauseCoordinator>();
            coordinator.bootstrap = core; coordinator.manager = manager;
            coordinator.Reconcile(); return coordinator;
        }
        internal void SetPaused(GameplayInputGate gate, bool paused)
        {
            if (paused) gates.Add(gate); else gates.Remove(gate);
            Reconcile();
        }
        private void Update() => Reconcile();
        private void Reconcile()
        {
            gates.RemoveWhere(g => g == null || !g.isActiveAndEnabled);
            if (manager == null) return;
            if (Available)
            {
                // CanTransitionTo also guards notification reentrancy and load reservations.
                if (gates.Count > 0 && manager.CurrentState == GameState.Playing && manager.CanTransitionTo(GameState.Paused))
                    ownsPause = manager.TryChangeState(GameState.Paused) || ownsPause;
                else if (gates.Count == 0 && ownsPause)
                {
                    if (manager.CurrentState == GameState.Paused && manager.CanTransitionTo(GameState.Playing))
                        ownsPause = !manager.TryChangeState(GameState.Playing);
                    else if (manager.CurrentState == GameState.Playing || manager.CurrentState == GameState.Ready) ownsPause = false;
                }
            }
            bool pauseEnvironment = gates.Count > 0 || manager.CurrentState == GameState.Paused;
            if (pauseEnvironment)
            {
                if (!environmentHeld)
                {
                    previousScale = Time.timeScale; previousCursor = Cursor.lockState; previousVisible = Cursor.visible;
                    environmentHeld = true;
                }
                Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
            else RestoreEnvironment();
        }
        private void RestoreEnvironment()
        {
            if (!environmentHeld) return;
            environmentHeld = false; Time.timeScale = previousScale;
            Cursor.lockState = previousCursor; Cursor.visible = previousVisible;
        }
        private void OnDestroy() => RestoreEnvironment();
    }
}
