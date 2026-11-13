using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HUSEscape.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: InternalsVisibleTo("HUSEscape.UI.EditModeTests")]
[assembly: InternalsVisibleTo("HUSEscape.UI.PlayModeTests")]

namespace HUSEscape.UI
{
    /// <summary>Binds scene-owned menu views to services from an explicitly assigned persistent owner.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuFlow : MonoBehaviour
    {
        internal delegate bool SceneRequest(string path, GameState destination, out string error);
        [SerializeField] private GameBootstrap bootstrap = null;
        [SerializeField] private string playScenePath = "Assets/_Project/Scenes/Floor01_Prototype.unity";
        private GameManager manager;
        private SceneLoader loader;
        private SceneRequest requestScene;
        private SceneHandle boundScene;
        private bool hasStarted;
        private bool pending;
        private Coroutine rebind;

        /// <summary>Gets the currently bound scene view, or null when no menu is owned.</summary>
        public MainMenuView BoundView { get; private set; }

        /// <summary>Gets the latest technical failure for diagnostics; UI shows a concise retry message.</summary>
        public string LastError { get; private set; }

        /// <summary>Gets whether a Start request or an owned Core load is pending.</summary>
        public bool IsLoading => pending || (loader != null && loader.IsLoading);

        private void Start()
        {
            hasStarted = true;
            Initialize();
        }

        private void OnEnable()
        {
            if (hasStarted) { rebind = StartCoroutine(RebindAfterEnable()); }
        }

        private IEnumerator RebindAfterEnable()
        {
            // Allow every owner's OnEnable to finish; no script execution-order override is needed.
            yield return null;
            rebind = null;
            Initialize();
        }

        private void Initialize()
        {
            if (manager != null) { return; }
            if (bootstrap == null || bootstrap.gameObject != gameObject
                || !bootstrap.TryGetServices(out manager, out loader))
            {
                manager = null;
                loader = null;
                LastError = "MainMenuFlow requires its active initialized BootstrapCore owner.";
                Debug.LogError(LastError, this);
                enabled = false;
                return;
            }

            requestScene = loader.TryLoadScene;
            loader.SceneLoaded += OnSceneLoaded;
            loader.SceneLoadFailed += OnSceneLoadFailed;
            manager.StateChanged += OnStateChanged;
            BindLoadedScene(SceneManager.GetActiveScene());
        }

        internal void BindLoadedScene(Scene scene)
        {
            if (manager == null || !scene.IsValid() || !scene.isLoaded
                || !scene.handle.Equals(SceneManager.GetActiveScene().handle)) { return; }
            var candidates = new List<MainMenuView>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                candidates.AddRange(root.GetComponentsInChildren<MainMenuView>(true));
            }

            if (candidates.Count != 1)
            {
                UnbindView();
                if (candidates.Count > 1)
                {
                    LastError = "A menu scene must contain exactly one MainMenuView.";
                    Debug.LogError(LastError, this);
                }
                return;
            }

            MainMenuView candidate = candidates[0];
            if (ReferenceEquals(BoundView, candidate) && boundScene.Equals(scene.handle)) { RefreshView(); return; }
            UnbindView();
            BoundView = candidate;
            boundScene = scene.handle;
            LastError = null;
            candidate.StartRequested += OnStartRequested;
            candidate.Destroying += OnViewDestroying;
            candidate.Bind();
            RefreshView();
        }

        private void OnSceneLoaded(string path)
        {
            pending = false;
            LastError = null;
            BindLoadedScene(SceneManager.GetSceneByPath(path));
        }

        private void OnSceneLoadFailed(string path, string error)
        {
            pending = false;
            LastError = error;
            RefreshView();
        }

        private void OnStateChanged(GameState previous, GameState current) => RefreshView();

        private void Update()
        {
            // Owner availability can change without a state event (for example disabling its component).
            if (BoundView != null) { RefreshView(); }
        }

        private void RefreshView()
        {
            if (BoundView == null) { return; }
            bool ownerReady = bootstrap != null && bootstrap.TryGetServices(out var currentManager, out var currentLoader)
                && ReferenceEquals(manager, currentManager) && ReferenceEquals(loader, currentLoader);
            bool busy = IsLoading || manager.CurrentState == GameState.Loading;
            bool canStart = ownerReady && manager.CurrentState == GameState.Ready && !busy;
            string message = busy ? "Loading..." : LastError != null ? "Unable to start. Please try again."
                : ownerReady ? string.Empty : "Menu unavailable.";
            BoundView.SetState(canStart, message);
        }

        private void OnStartRequested(MainMenuView source)
        {
            if (!ReferenceEquals(BoundView, source)) { return; }
            if (IsLoading) { RefreshView(); return; }
            if (!bootstrap.TryGetServices(out var currentManager, out var currentLoader)
                || !ReferenceEquals(currentManager, manager) || !ReferenceEquals(currentLoader, loader)
                || manager.CurrentState != GameState.Ready) { RefreshView(); return; }
            if (string.IsNullOrWhiteSpace(playScenePath) || !playScenePath.StartsWith("Assets/", StringComparison.Ordinal)
                || !playScenePath.EndsWith(".unity", StringComparison.Ordinal)
                || playScenePath == "Assets/_Project/Scenes/Bootstrap/Bootstrap.unity"
                || playScenePath == SceneManager.GetActiveScene().path)
            {
                LastError = "Start requires a different full enabled content-scene path.";
                Debug.LogError(LastError, this);
                RefreshView();
                return;
            }

            LastError = null;
            pending = true;
            RefreshView();
            if (!requestScene(playScenePath, GameState.Playing, out string error))
            {
                pending = false;
                LastError = error;
                Debug.LogError("Main menu Start rejected: " + error, this);
                RefreshView();
            }
        }

        private void OnViewDestroying(MainMenuView source)
        {
            if (ReferenceEquals(source, BoundView)) { UnbindView(); }
        }

        private void UnbindView()
        {
            MainMenuView previous = BoundView;
            BoundView = null;
            boundScene = default;
            if (previous == null) { return; }
            previous.StartRequested -= OnStartRequested;
            previous.Destroying -= OnViewDestroying;
            previous.Unbind();
        }

        private void OnDisable()
        {
            if (rebind != null) { StopCoroutine(rebind); rebind = null; }
            UnbindView();
            if (loader != null) { loader.SceneLoaded -= OnSceneLoaded; loader.SceneLoadFailed -= OnSceneLoadFailed; }
            if (manager != null) { manager.StateChanged -= OnStateChanged; }
            manager = null;
            loader = null;
            requestScene = null;
            pending = false;
        }
    }
}
