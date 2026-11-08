using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HUSEscape.Core
{
    /// <summary>Checks native activation additively, preserving every preexisting Test Runner scene.</summary>
    public sealed class SceneLoaderPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private readonly HashSet<SceneHandle> originalHandles = new HashSet<SceneHandle>();
        private GameObject root;
        private Scene originalActive;
        private Scene loadedScene;
        private UnityAction<Scene, LoadSceneMode> activationHandler;
        private SceneLoader pendingLoader;

        [SetUp]
        public void SetUp()
        {
            originalActive = SceneManager.GetActiveScene();
            originalHandles.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                originalHandles.Add(SceneManager.GetSceneAt(i).handle);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // A started native load cannot be cancelled. Keep observing it before removing handlers.
            while (pendingLoader != null && pendingLoader.IsLoading) { yield return null; }
            if (activationHandler != null) { SceneManager.sceneLoaded -= activationHandler; }
            if (originalActive.IsValid() && originalActive.isLoaded) { SceneManager.SetActiveScene(originalActive); }
            if (root != null) { Object.Destroy(root); }
            yield return null;
            if (loadedScene.IsValid() && loadedScene.isLoaded && !originalHandles.Contains(loadedScene.handle))
            {
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            }
            AssertOriginalScenesRemain();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NativeSampleScene_ActivatesWithoutUnloadingRunnerScenes()
        {
            var backend = new UnitySceneLoadBackend(LoadSceneMode.Additive);
            Assert.That(backend.FindBuildIndex(ScenePath), Is.GreaterThanOrEqualTo(0),
                "SampleScene must remain enabled in the unchanged build settings.");
            root = new GameObject("FO002_NativeLoader");
            SceneLoader loader = root.AddComponent<SceneLoader>();
            pendingLoader = loader;
            var manager = new GameManager();
            manager.Initialize();
            loader.Initialize(manager, backend);
            int successes = 0;
            int failures = 0;
            string completedPath = null;
            loader.SceneLoaded += path => { successes++; completedPath = path; };
            loader.SceneLoadFailed += (_, __) => failures++;
            bool activated = false;
            activationHandler = (scene, mode) =>
            {
                if (scene.path == ScenePath && !originalHandles.Contains(scene.handle))
                {
                    loadedScene = scene;
                    activated = SceneManager.SetActiveScene(scene);
                }
            };
            SceneManager.sceneLoaded += activationHandler;

            Assert.That(loader.TryLoadScene(ScenePath, GameState.Playing, out var error), Is.True, error);
            Assert.That(loader.IsLoading, Is.True);
            Assert.That(loader.TryLoadScene(ScenePath, GameState.Ready, out _), Is.False);
            loader.enabled = false;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (loader.IsLoading && Time.realtimeSinceStartup < deadline) { yield return null; }

            Assert.That(loader.IsLoading, Is.False, "Native load did not finish within 30 seconds; no cancellation was attempted.");
            Assert.That(activated, Is.True);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(loadedScene));
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Playing));
            Assert.That(manager.ActiveReservation, Is.Null);
            Assert.That(successes, Is.EqualTo(1));
            Assert.That(failures, Is.Zero);
            Assert.That(completedPath, Is.EqualTo(ScenePath));
            Assert.That(loader.LastError, Is.Null);
            Assert.That(loader.enabled, Is.False, "Disabling the component must not lose native completion.");
            AssertOriginalScenesRemain();
        }

        private void AssertOriginalScenesRemain()
        {
            var remaining = new HashSet<SceneHandle>();
            for (int i = 0; i < SceneManager.sceneCount; i++) { remaining.Add(SceneManager.GetSceneAt(i).handle); }
            foreach (SceneHandle handle in originalHandles)
            {
                Assert.That(remaining.Contains(handle), Is.True, "A preexisting runner scene was unloaded.");
            }
        }
    }
}
