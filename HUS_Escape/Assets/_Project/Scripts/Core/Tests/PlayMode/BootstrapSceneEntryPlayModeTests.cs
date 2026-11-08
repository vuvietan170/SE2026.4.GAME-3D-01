using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HUSEscape.Core
{
    /// <summary>Runs real startup lifecycle and production loader coordination without unloading runner scenes.</summary>
    public sealed class BootstrapSceneEntryPlayModeTests
    {
        private const string SamplePath = "Assets/Scenes/SampleScene.unity";
        private readonly List<GameObject> roots = new List<GameObject>();
        private GameBootstrap bootstrap;
        private BootstrapSceneEntry entry;
        private GameManager manager;
        private SceneLoader loader;
        private DeferredBackend backend;

        private sealed class DeferredOperation : SceneLoadOperation
        {
            internal override float Progress => 0.5f;
            internal override bool IsDone => false;
            internal override event Action Completed;
            internal void Complete() => Completed?.Invoke();
        }

        private sealed class DeferredBackend : SceneLoadBackend
        {
            internal readonly DeferredOperation Operation = new DeferredOperation();
            internal bool Activated = true;
            internal bool ThrowOnStart = false;
            internal int StartCount;
            internal override int FindBuildIndex(string path) => path == SamplePath ? 0 : -1;
            internal override bool IsSceneActive(string path) => Activated;
            internal override SceneLoadOperation StartLoad(int index)
            {
                StartCount++;
                if (ThrowOnStart) { throw new InvalidOperationException("FO003A controlled startup failure"); }
                return Operation;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject root in roots) { if (root != null) { Object.Destroy(root); } }
            roots.Clear();
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Start_WaitsForCoreAndFinalizesSuccessfulStartupOnce()
        {
            CreateEntry();
            Assert.That(bootstrap.TryGetServices(out _, out _), Is.True);
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Ready));
            Assert.That(entry.HasStarted, Is.False);
            yield return null;
            Assert.That(entry.HasStarted, Is.True);
            Assert.That(entry.HasFinished, Is.False);
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Loading));
            Assert.That(HasSubscription("SceneLoaded"), Is.True);
            backend.Operation.Complete();
            AssertFinished(GameState.Playing, null);
            Assert.That(HasSubscription("SceneLoaded") || HasSubscription("SceneLoadFailed"), Is.False);
            entry.enabled = true;
            yield return null;
            Assert.That(entry.TryBeginStartup(out _), Is.False);
            Assert.That(backend.StartCount, Is.EqualTo(1));
            Assert.That(entry.LastError, Is.Null);
        }

        [UnityTest]
        public IEnumerator ReadyDestination_RemainsReadyAfterLoading()
        {
            CreateEntry(state: GameState.Ready);
            yield return null;
            backend.Operation.Complete();
            AssertFinished(GameState.Ready, null);
        }

        [UnityTest]
        public IEnumerator MissingReference_FailsWithoutLoading()
        {
            CreateEntry();
            SetField(entry, "bootstrap", null);
            LogAssert.Expect(LogType.Error, "Bootstrap startup failed: Assign the GameBootstrap on this same root GameObject.");
            yield return null;
            AssertFinished(GameState.Ready, "Assign the GameBootstrap on this same root GameObject.");
            Assert.That(backend.StartCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator UnlistedDestination_FailsOnceAndUnsubscribes()
        {
            CreateEntry("Assets/Scenes/Missing.unity");
            LogAssert.Expect(LogType.Error, new Regex("Bootstrap startup failed: Scene path must exactly match"));
            yield return null;
            Assert.That(entry.LastError, Does.Contain("enabled build scene"));
            AssertFinished(GameState.Ready, entry.LastError);
            Assert.That(backend.StartCount, Is.Zero);
            Assert.That(HasSubscription("SceneLoaded") || HasSubscription("SceneLoadFailed"), Is.False);
        }

        [UnityTest]
        public IEnumerator DisabledOwner_FailsSafelyWithoutRetryingWhenOwnerEnables()
        {
            GameObject root = NewRoot();
            root.SetActive(false);
            bootstrap = root.AddComponent<GameBootstrap>();
            bootstrap.enabled = false;
            entry = root.AddComponent<BootstrapSceneEntry>();
            Configure(entry, bootstrap, SamplePath, GameState.Playing);
            LogAssert.Expect(LogType.Error, "Bootstrap startup failed: The referenced bootstrap is not the active initialized Core owner.");
            root.SetActive(true);
            yield return null;
            Assert.That(entry.HasFinished && entry.HasStarted, Is.True);
            Assert.That(entry.enabled, Is.False);
            bootstrap.enabled = true;
            yield return null;
            Assert.That(bootstrap.TryGetServices(out var initialized, out var initializedLoader), Is.True);
            Assert.That(initialized.CurrentState, Is.EqualTo(GameState.Ready));
            Assert.That(initializedLoader.IsLoading, Is.False);
        }

        [UnityTest]
        public IEnumerator DuplicateOwner_CannotStartAnotherStartupLoad()
        {
            GameBootstrap owner = NewRoot().AddComponent<GameBootstrap>();
            GameObject duplicate = NewRoot();
            duplicate.SetActive(false);
            GameBootstrap rejected = duplicate.AddComponent<GameBootstrap>();
            BootstrapSceneEntry rejectedEntry = duplicate.AddComponent<BootstrapSceneEntry>();
            Configure(rejectedEntry, rejected, SamplePath, GameState.Playing);
            LogAssert.Expect(LogType.Error, "A GameBootstrap already owns Core initialization; this duplicate is disabled.");
            LogAssert.Expect(LogType.Error, "Bootstrap startup failed: The referenced bootstrap is not the active initialized Core owner.");
            duplicate.SetActive(true);
            yield return null;
            Assert.That(rejectedEntry.HasFinished && rejectedEntry.HasStarted, Is.True);
            Assert.That(rejectedEntry.enabled, Is.False);
            Assert.That(owner.Manager.CurrentState, Is.EqualTo(GameState.Ready));
            Assert.That(owner.Loader.IsLoading, Is.False);
        }

        [UnityTest]
        public IEnumerator StartupException_FinishesOnceAfterCoreRecovers()
        {
            CreateEntry();
            backend.ThrowOnStart = true;
            LogAssert.Expect(LogType.Error, new Regex("Could not start scene load.*FO003A controlled startup failure"));
            LogAssert.Expect(LogType.Error, new Regex("Bootstrap startup failed: Could not start scene load"));
            yield return null;
            Assert.That(entry.LastError, Does.Contain("FO003A controlled startup failure"));
            AssertFinished(GameState.Ready, entry.LastError);
            Assert.That(HasSubscription("SceneLoaded") || HasSubscription("SceneLoadFailed"), Is.False);
            Assert.That(backend.StartCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ActivationFailure_PreservesCoreRecoveryAndCleansHandlers()
        {
            CreateEntry();
            backend.Activated = false;
            yield return null;
            LogAssert.Expect(LogType.Error, new Regex("Unity completed loading without activating"));
            LogAssert.Expect(LogType.Error, new Regex("Bootstrap startup failed: Unity completed loading without activating"));
            backend.Operation.Complete();
            Assert.That(entry.LastError, Does.Contain("without activating"));
            AssertFinished(GameState.Ready, entry.LastError);
            Assert.That(HasSubscription("SceneLoaded") || HasSubscription("SceneLoadFailed"), Is.False);
        }

        [UnityTest]
        public IEnumerator DisabledPendingEntry_StillObservesCompletion()
        {
            CreateEntry();
            yield return null;
            entry.enabled = false;
            backend.Operation.Complete();
            AssertFinished(GameState.Playing, null);
        }

        [UnityTest]
        public IEnumerator DestroyedEntry_UnsubscribesWithoutCancellingCoreLoad()
        {
            CreateEntry();
            yield return null;
            Object.Destroy(entry);
            yield return null;
            Assert.That(HasSubscription("SceneLoaded") || HasSubscription("SceneLoadFailed"), Is.False);
            Assert.That(loader.IsLoading, Is.True);
            backend.Operation.Complete();
            Assert.That(loader.IsLoading, Is.False);
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Playing));
        }

        [UnityTest]
        public IEnumerator NonReadyCore_IsNotRedirectedByStartup()
        {
            CreateEntry();
            Assert.That(manager.TryChangeState(GameState.Playing), Is.True);
            LogAssert.Expect(LogType.Error, "Bootstrap startup failed: Bootstrap startup requires the Ready state.");
            yield return null;
            AssertFinished(GameState.Playing, "Bootstrap startup requires the Ready state.");
            Assert.That(backend.StartCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CompletionUsesCapturedPathIfInspectorConfigurationChanges()
        {
            CreateEntry();
            yield return null;
            SetField(entry, "destinationScenePath", "Assets/Scenes/Changed.unity");
            backend.Operation.Complete();
            AssertFinished(GameState.Playing, null);
        }

        private GameObject NewRoot()
        {
            var root = new GameObject("FO003A_PlayEntry");
            roots.Add(root);
            return root;
        }

        private void CreateEntry(string path = SamplePath, GameState state = GameState.Playing)
        {
            GameObject root = NewRoot();
            root.SetActive(false);
            bootstrap = root.AddComponent<GameBootstrap>();
            entry = root.AddComponent<BootstrapSceneEntry>();
            Configure(entry, bootstrap, path, state);
            root.SetActive(true);
            if (!bootstrap.TryGetServices(out manager, out loader)) { throw new InvalidOperationException("Test owner failed to initialize."); }
            backend = new DeferredBackend();
            // Test-only instance injection between real Awake and Start; never modifies production Core.
            SetField(loader, "backend", backend);
        }

        private static void Configure(BootstrapSceneEntry startup, GameBootstrap owner, string path, GameState state)
        {
            SetField(startup, "bootstrap", owner);
            SetField(startup, "destinationScenePath", path);
            SetField(startup, "destinationState", state);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        }

        private bool HasSubscription(string name)
        {
            var listeners = (Delegate)typeof(SceneLoader).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(loader);
            if (listeners == null) { return false; }
            foreach (Delegate listener in listeners.GetInvocationList())
            {
                if (ReferenceEquals(listener.Target, entry)) { return true; }
            }
            return false;
        }

        private void AssertFinished(GameState state, string error)
        {
            Assert.That(entry.HasStarted && entry.HasFinished, Is.True);
            Assert.That(entry.enabled, Is.False);
            Assert.That(entry.LastError, Is.EqualTo(error));
            Assert.That(manager.CurrentState, Is.EqualTo(state));
            Assert.That(manager.ActiveReservation, Is.Null);
            Assert.That(loader.IsLoading, Is.False);
        }
    }
}
