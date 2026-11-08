using System;
using UnityEngine;

namespace HUSEscape.Core
{
    /// <summary>Serializes asynchronous scene loads and coordinates token-owned state recovery.</summary>
    /// <remarks>Use on Unity's main thread. The production backend loads in Single mode.</remarks>
    [DisallowMultipleComponent]
    public sealed class SceneLoader : MonoBehaviour
    {
        private sealed class LoadRequest
        {
            internal string Path;
            internal GameState Destination;
            internal GameManager.SceneLoadReservation Reservation;
            internal SceneLoadOperation Operation;
            internal Action Completion;
            internal bool CompletionObserved;
            internal bool Finishing;
            internal bool ResultCaptured;
            internal bool Succeeded;
            internal string Error;
        }

        private GameManager manager;
        private SceneLoadBackend backend;
        private LoadRequest request;

        /// <summary>Raised after scene activation and destination-state commitment.</summary>
        public event Action<string> SceneLoaded;

        /// <summary>Raised after an accepted request fails and its owned reservation is released.</summary>
        public event Action<string, string> SceneLoadFailed;

        /// <summary>Gets whether a request or a reservation on this manager is still pending.</summary>
        public bool IsLoading => request != null || (manager != null && manager.ActiveReservation != null);

        /// <summary>Gets raw Unity progress, or zero when no operation is pending.</summary>
        public float Progress => request?.Operation != null ? request.Operation.Progress : 0f;

        /// <summary>Gets the latest rejection or failure; cleared on acceptance and successful completion.</summary>
        public string LastError { get; private set; }

        internal void Initialize(GameManager gameManager)
        {
            Initialize(gameManager, new UnitySceneLoadBackend());
        }

        internal bool IsInitializedFor(GameManager gameManager)
        {
            return gameManager != null && ReferenceEquals(manager, gameManager) && backend != null;
        }

        internal void Initialize(GameManager gameManager, SceneLoadBackend sceneBackend)
        {
            if (gameManager == null)
            {
                throw new ArgumentNullException(nameof(gameManager));
            }

            if (sceneBackend == null)
            {
                throw new ArgumentNullException(nameof(sceneBackend));
            }

            if (manager != null)
            {
                throw new InvalidOperationException("SceneLoader has already been initialized.");
            }

            manager = gameManager;
            backend = sceneBackend;
        }

        /// <summary>Attempts to start loading an exact enabled build-scene path.</summary>
        /// <param name="scenePath">A full project-relative scene path, including Assets/ and .unity.</param>
        /// <param name="loadedState">Ready or Playing after successful activation.</param>
        /// <param name="error">A rejection/startup error, or null when accepted.</param>
        /// <returns>True when loading starts, not a guarantee of successful completion.</returns>
        /// <remarks>Requests are not queued or cancellable. Completion uses instance events.</remarks>
        public bool TryLoadScene(string scenePath, GameState loadedState, out string error)
        {
            if (!isActiveAndEnabled || manager == null || !manager.IsInitialized)
            {
                return Reject("SceneLoader must be active and initialized by GameBootstrap.", out error);
            }

            if (IsLoading)
            {
                return Reject("A scene load is already in progress.", out error);
            }

            if (loadedState != GameState.Ready && loadedState != GameState.Playing)
            {
                return Reject("The destination state must be Ready or Playing.", out error);
            }

            if (string.IsNullOrWhiteSpace(scenePath) || !scenePath.StartsWith("Assets/", StringComparison.Ordinal)
                || !scenePath.EndsWith(".unity", StringComparison.Ordinal))
            {
                return Reject("Scene path must be a full Assets/ path ending in .unity.", out error);
            }

            int buildIndex;
            try
            {
                buildIndex = backend.FindBuildIndex(scenePath);
            }
            catch (Exception exception)
            {
                return Reject("Could not validate scene path: " + exception.Message, out error);
            }

            if (buildIndex < 0)
            {
                return Reject("Scene path must exactly match an enabled build scene: " + scenePath, out error);
            }

            var accepted = new LoadRequest { Path = scenePath, Destination = loadedState };
            request = accepted;
            if (!manager.TryBeginSceneLoad(out accepted.Reservation))
            {
                request = null;
                return Reject("Cannot start a scene load from state " + manager.CurrentState + ".", out error);
            }

            LastError = null;
            try
            {
                accepted.Operation = backend.StartLoad(buildIndex);
                if (accepted.Operation == null)
                {
                    throw new InvalidOperationException("Unity returned no scene loading operation.");
                }

                accepted.Completion = () => ObserveCompletion(accepted);
                accepted.Operation.Completed += accepted.Completion;
                // Also handles an operation that completed before its observer was attached.
                if (accepted.Operation.IsDone)
                {
                    ObserveCompletion(accepted);
                }
            }
            catch (Exception exception)
            {
                error = "Could not start scene load for " + scenePath + ": " + exception.Message;
                accepted.ResultCaptured = true;
                accepted.CompletionObserved = true;
                accepted.Succeeded = false;
                accepted.Error = error;
                Finish(accepted);
                return false;
            }

            error = null;
            return true;
        }

        private void ObserveCompletion(LoadRequest completed)
        {
            if (!ReferenceEquals(request, completed) || completed.CompletionObserved)
            {
                return;
            }

            completed.CompletionObserved = true;
            Finish(completed);
        }

        private void Update()
        {
            // Never unlock a request by forcing a transition inside another state notification.
            if (request != null && request.CompletionObserved)
            {
                Finish(request);
            }
        }

        private void Finish(LoadRequest completed)
        {
            if (!ReferenceEquals(request, completed) || completed.Finishing || manager.IsNotifying)
            {
                return;
            }

            completed.Finishing = true;
            try
            {
                if (!completed.ResultCaptured)
                {
                    completed.ResultCaptured = true;
                    try
                    {
                        completed.Succeeded = backend.IsSceneActive(completed.Path);
                        if (!completed.Succeeded)
                        {
                            completed.Error = "Unity completed loading without activating the requested scene: " + completed.Path;
                        }
                    }
                    catch (Exception exception)
                    {
                        completed.Error = "Could not verify scene activation: " + exception.Message;
                    }
                }

                if (!manager.OwnsReservation(completed.Reservation))
                {
                    // This result cannot commit or release someone else's newer reservation.
                    completed.Succeeded = false;
                    completed.Error = "Scene load completion no longer owns its manager reservation.";
                }
                else if (!completed.Succeeded
                    || !manager.TryCompleteSceneLoad(completed.Reservation, completed.Destination))
                {
                    completed.Succeeded = false;
                    completed.Error = completed.Error ?? "The manager rejected the scene load completion state.";
                    if (!manager.TryRecoverSceneLoad(completed.Reservation))
                    {
                        // Retain the request and busy status until its owned reservation can be recovered.
                        return;
                    }
                }

                if (completed.Operation != null && completed.Completion != null)
                {
                    completed.Operation.Completed -= completed.Completion;
                }

                LastError = completed.Error;
                request = null;
                PublishCompletion(completed);
            }
            finally
            {
                completed.Finishing = false;
            }
        }

        private void PublishCompletion(LoadRequest completed)
        {
            if (!completed.Succeeded)
            {
                Debug.LogError(completed.Error);
            }

            Delegate listeners = completed.Succeeded ? (Delegate)SceneLoaded : SceneLoadFailed;
            if (listeners == null)
            {
                return;
            }

            foreach (Delegate listener in listeners.GetInvocationList())
            {
                try
                {
                    if (completed.Succeeded)
                    {
                        ((Action<string>)listener)(completed.Path);
                    }
                    else
                    {
                        ((Action<string, string>)listener)(completed.Path, completed.Error);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private bool Reject(string message, out string error)
        {
            error = message;
            LastError = message;
            return false;
        }
    }
}
