using System;
using UnityEngine;

namespace HUSEscape.Core
{
    /// <summary>Requests the application's first scene after the owning Core root initializes.</summary>
    /// <remarks>Keep this component on the persistent GameBootstrap root. It attempts startup once.</remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameBootstrap))]
    public sealed class BootstrapSceneEntry : MonoBehaviour
    {
        internal const string BootstrapScenePath = "Assets/_Project/Scenes/Bootstrap/Bootstrap.unity";

        [SerializeField] private GameBootstrap bootstrap = null;
        [SerializeField] private string destinationScenePath = "Assets/Scenes/SampleScene.unity";
        [SerializeField] private GameState destinationState = GameState.Playing;

        private SceneLoader observedLoader;
        private string requestedPath;

        /// <summary>Gets whether this component has attempted startup, including a rejected attempt.</summary>
        public bool HasStarted { get; private set; }

        /// <summary>Gets whether startup has completed successfully or failed.</summary>
        public bool HasFinished { get; private set; }

        /// <summary>Gets the startup failure, or null when no failure has occurred.</summary>
        public string LastError { get; private set; }

        private void Start()
        {
            TryBeginStartup(out _);
        }

        internal bool TryValidateConfiguration(out string error)
        {
            error = null;
            if (bootstrap == null || bootstrap.gameObject != gameObject)
            {
                error = "Assign the GameBootstrap on this same root GameObject.";
            }
            else if (string.IsNullOrWhiteSpace(destinationScenePath)
                || !destinationScenePath.StartsWith("Assets/", StringComparison.Ordinal)
                || !destinationScenePath.EndsWith(".unity", StringComparison.Ordinal))
            {
                error = "The startup destination must be a full Assets/ path ending in .unity.";
            }
            else if (string.Equals(destinationScenePath, BootstrapScenePath, StringComparison.Ordinal))
            {
                error = "Bootstrap cannot load itself as its startup destination.";
            }
            else if (destinationState != GameState.Ready && destinationState != GameState.Playing)
            {
                error = "The startup destination state must be Ready or Playing.";
            }

            return error == null;
        }

        internal bool TryBeginStartup(out string error)
        {
            if (HasStarted)
            {
                error = "Bootstrap startup has already been attempted.";
                return false;
            }

            HasStarted = true;
            if (!TryValidateConfiguration(out error))
            {
                Finish(error);
                return false;
            }

            if (!bootstrap.TryGetServices(out GameManager manager, out SceneLoader loader))
            {
                error = "The referenced bootstrap is not the active initialized Core owner.";
                Finish(error);
                return false;
            }

            if (manager.CurrentState != GameState.Ready)
            {
                error = "Bootstrap startup requires the Ready state.";
                Finish(error);
                return false;
            }

            requestedPath = destinationScenePath;
            observedLoader = loader;
            observedLoader.SceneLoaded += OnSceneLoaded;
            observedLoader.SceneLoadFailed += OnSceneLoadFailed;
            if (!loader.TryLoadScene(requestedPath, destinationState, out error))
            {
                // A synchronous startup failure may already have invoked OnSceneLoadFailed.
                if (!HasFinished)
                {
                    Finish(error);
                }

                return false;
            }

            return true;
        }

        private void OnSceneLoaded(string scenePath)
        {
            if (string.Equals(scenePath, requestedPath, StringComparison.Ordinal))
            {
                Finish(null);
            }
        }

        private void OnSceneLoadFailed(string scenePath, string error)
        {
            if (string.Equals(scenePath, requestedPath, StringComparison.Ordinal))
            {
                Finish(error);
            }
        }

        private void Finish(string error)
        {
            LastError = error;
            HasFinished = true;
            Unsubscribe();
            enabled = false;
            if (error != null)
            {
                Debug.LogError("Bootstrap startup failed: " + error, this);
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (observedLoader != null)
            {
                observedLoader.SceneLoaded -= OnSceneLoaded;
                observedLoader.SceneLoadFailed -= OnSceneLoadFailed;
                observedLoader = null;
            }
        }
    }
}
