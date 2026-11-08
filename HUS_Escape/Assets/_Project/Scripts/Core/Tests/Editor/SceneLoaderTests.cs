using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HUSEscape.Core
{
    /// <summary>Exercises production SceneLoader using only an instance-scoped engine double.</summary>
    public sealed class SceneLoaderTests
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private GameObject root;
        private SceneLoader loader;
        private GameManager manager;
        private FakeBackend backend;

        private sealed class FakeOperation : SceneLoadOperation
        {
            internal override float Progress => IsDone ? 1f : 0.5f;
            internal override bool IsDone { get; }
            internal override event Action Completed;
            internal FakeOperation(bool done = false) { IsDone = done; }
            internal Action CaptureCallback() => Completed;
            internal void Complete() => Completed?.Invoke();
        }

        private sealed class FakeBackend : SceneLoadBackend
        {
            internal readonly Queue<FakeOperation> Operations = new Queue<FakeOperation>();
            internal FakeOperation LastOperation;
            internal bool Activated = true;
            internal bool ThrowOnStart;
            internal bool NullOperation;
            internal bool ThrowOnValidation;
            internal bool ThrowOnActivation;
            internal int StartCount;
            internal override int FindBuildIndex(string path)
            {
                if (ThrowOnValidation) { throw new InvalidOperationException("validation failed"); }
                return path == ScenePath ? 0 : -1;
            }
            internal override SceneLoadOperation StartLoad(int index)
            {
                StartCount++;
                if (ThrowOnStart) { throw new InvalidOperationException("startup failed"); }
                if (NullOperation) { return null; }
                LastOperation = Operations.Count > 0 ? Operations.Dequeue() : new FakeOperation();
                return LastOperation;
            }
            internal override bool IsSceneActive(string path)
            {
                if (ThrowOnActivation) { throw new InvalidOperationException("activation failed"); }
                return Activated;
            }
        }

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("FO002_EditLoader");
            loader = root.AddComponent<SceneLoader>();
            manager = new GameManager();
            manager.Initialize();
            backend = new FakeBackend();
            loader.Initialize(manager, backend);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("SampleScene")]
        [TestCase("Assets/Scenes/Missing.unity")]
        [TestCase("assets/Scenes/SampleScene.unity")]
        [TestCase("Assets/Scenes/SampleScene.UNITY")]
        public void InvalidPaths_AreRejectedWithoutStartingOrChangingState(string path)
        {
            Assert.That(loader.TryLoadScene(path, GameState.Playing, out var error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            AssertIdle(GameState.Ready);
            Assert.That(backend.StartCount, Is.Zero);
        }

        [TestCase(GameState.Initializing)]
        [TestCase(GameState.Loading)]
        [TestCase(GameState.Paused)]
        [TestCase((GameState)99)]
        public void InvalidDestinations_AreRejected(GameState destination)
        {
            Assert.That(loader.TryLoadScene(ScenePath, destination, out _), Is.False);
            AssertIdle(GameState.Ready);
            Assert.That(backend.StartCount, Is.Zero);
        }

        [Test]
        public void UninitializedOrDisabledLoader_RejectsRequests()
        {
            var uninitialized = new GameObject("FO002_Uninitialized");
            try
            {
                Assert.That(uninitialized.AddComponent<SceneLoader>().TryLoadScene(ScenePath, GameState.Ready, out _), Is.False);
                loader.enabled = false;
                Assert.That(loader.TryLoadScene(ScenePath, GameState.Ready, out _), Is.False);
                AssertIdle(GameState.Ready);
            }
            finally { Object.DestroyImmediate(uninitialized); }
        }

        [Test]
        public void Initialize_RejectsNullAndRepeatedDependencies()
        {
            var uninitialized = new GameObject("FO002_InitializeValidation");
            try
            {
                SceneLoader component = uninitialized.AddComponent<SceneLoader>();
                Assert.Throws<ArgumentNullException>(() => component.Initialize(null, backend));
                Assert.Throws<ArgumentNullException>(() => component.Initialize(manager, null));
                Assert.Throws<InvalidOperationException>(() => loader.Initialize(manager, backend));
                Assert.That(loader.IsInitializedFor(manager), Is.True);
            }
            finally { Object.DestroyImmediate(uninitialized); }
        }

        [Test]
        public void OverlappingAndReentrantRequests_AreRejected()
        {
            bool nestedAccepted = true;
            manager.StateChanged += (previous, state) =>
            {
                if (state == GameState.Loading)
                {
                    nestedAccepted = loader.TryLoadScene(ScenePath, GameState.Ready, out _);
                }
            };
            Assert.That(loader.TryLoadScene(ScenePath, GameState.Playing, out _), Is.True);
            var token = manager.ActiveReservation;
            Assert.That(nestedAccepted, Is.False);
            Assert.That(loader.TryLoadScene(ScenePath, GameState.Ready, out _), Is.False);
            Assert.That(manager.ActiveReservation, Is.SameAs(token));
            Assert.That(backend.StartCount, Is.EqualTo(1));
            Assert.That(loader.Progress, Is.EqualTo(0.5f));
            backend.LastOperation.Complete();
            AssertIdle(GameState.Playing);
        }

        [TestCase(GameState.Ready, false)]
        [TestCase(GameState.Playing, false)]
        [TestCase(GameState.Paused, false)]
        [TestCase(GameState.Ready, true)]
        public void StartupFailure_RestoresStateAndPublishesFailure(GameState source, bool nullOperation)
        {
            SetState(source);
            backend.ThrowOnStart = !nullOperation;
            backend.NullOperation = nullOperation;
            int failures = 0;
            int successes = 0;
            string failedPath = null;
            string failure = null;
            loader.SceneLoaded += _ => successes++;
            loader.SceneLoadFailed += (path, message) => { failures++; failedPath = path; failure = message; };
            LogAssert.Expect(LogType.Error, new Regex("Could not start scene load"));

            Assert.That(loader.TryLoadScene(ScenePath, GameState.Playing, out var error), Is.False);
            AssertIdle(source);
            Assert.That(failures, Is.EqualTo(1));
            Assert.That(successes, Is.Zero);
            Assert.That(failedPath, Is.EqualTo(ScenePath));
            Assert.That(failure, Is.EqualTo(error));
            Assert.That(loader.LastError, Is.EqualTo(error));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ActivationFailure_RestoresPausedState(bool throws)
        {
            SetState(GameState.Paused);
            backend.Activated = false;
            backend.ThrowOnActivation = throws;
            LogAssert.Expect(LogType.Error, new Regex(throws ? "Could not verify scene activation" : "without activating"));
            Assert.That(loader.TryLoadScene(ScenePath, GameState.Playing, out _), Is.True);
            backend.LastOperation.Complete();
            AssertIdle(GameState.Paused);
            Assert.That(loader.LastError, Is.Not.Empty);
        }

        [TestCase(GameState.Ready)]
        [TestCase(GameState.Playing)]
        public void Success_FinalizesBeforeNotifyingAndPublishesOnce(GameState destination)
        {
            int count = 0;
            string loadedPath = null;
            bool idleDuringEvent = false;
            GameState observedState = GameState.Initializing;
            loader.SceneLoaded += path =>
            {
                count++;
                loadedPath = path;
                idleDuringEvent = !loader.IsLoading && manager.ActiveReservation == null;
                observedState = manager.CurrentState;
            };
            Assert.That(loader.TryLoadScene(ScenePath, destination, out _), Is.True);
            Action callback = backend.LastOperation.CaptureCallback();
            backend.LastOperation.Complete();
            callback();
            AssertIdle(destination);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(loadedPath, Is.EqualTo(ScenePath));
            Assert.That(idleDuringEvent, Is.True);
            Assert.That(observedState, Is.EqualTo(destination));
            Assert.That(loader.LastError, Is.Null);
        }

        [Test]
        public void StaleCallback_CannotReleaseNewRequest()
        {
            Assert.That(loader.TryLoadScene(ScenePath, GameState.Playing, out _), Is.True);
            Action oldCallback = backend.LastOperation.CaptureCallback();
            backend.LastOperation.Complete();
            Assert.That(loader.TryLoadScene(ScenePath, GameState.Ready, out _), Is.True);
            var token = manager.ActiveReservation;
            oldCallback();
            Assert.That(loader.IsLoading, Is.True);
            Assert.That(manager.ActiveReservation, Is.SameAs(token));
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Loading));
            backend.LastOperation.Complete();
            AssertIdle(GameState.Ready);
        }

        [Test]
        public void LostReservation_CannotReleaseReplacementAndDoesNotReportIdle()
        {
            Assert.That(loader.TryLoadScene(ScenePath, GameState.Playing, out _), Is.True);
            var oldToken = manager.ActiveReservation;
            Assert.That(manager.TryCompleteSceneLoad(oldToken, GameState.Ready), Is.True);
            Assert.That(manager.TryBeginSceneLoad(out var replacement), Is.True);
            LogAssert.Expect(LogType.Error, "Scene load completion no longer owns its manager reservation.");
            backend.LastOperation.Complete();
            Assert.That(loader.IsLoading, Is.True);
            Assert.That(manager.ActiveReservation, Is.SameAs(replacement));
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Loading));
            Assert.That(manager.TryRecoverSceneLoad(replacement), Is.True);
            AssertIdle(GameState.Ready);
        }

        [Test]
        public void RejectedCompletion_RecoversOwnedReservationBeforeBecomingIdle()
        {
            Assert.That(loader.TryLoadScene(ScenePath, GameState.Playing, out _), Is.True);
            // Deliberate invariant fault injection, without replacing the production finish logic.
            object pending = typeof(SceneLoader).GetField("request", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(loader);
            pending.GetType().GetField("Destination", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pending, (GameState)99);
            LogAssert.Expect(LogType.Error, "The manager rejected the scene load completion state.");
            backend.LastOperation.Complete();
            AssertIdle(GameState.Ready);
            Assert.That(manager.CanTransitionTo(GameState.Loading), Is.True);
        }

        [Test]
        public void CompletionDuringNotification_RemainsBusyUntilProductionUpdateFinalizes()
        {
            Assert.That(loader.TryLoadScene(ScenePath, GameState.Playing, out _), Is.True);
            FieldInfo notifying = typeof(GameManager).GetField("isNotifying", BindingFlags.Instance | BindingFlags.NonPublic);
            notifying.SetValue(manager, true);
            try
            {
                backend.LastOperation.Complete();
                Assert.That(loader.IsLoading, Is.True);
                Assert.That(manager.ActiveReservation, Is.Not.Null);
                Assert.That(manager.CurrentState, Is.EqualTo(GameState.Loading));
            }
            finally { notifying.SetValue(manager, false); }
            typeof(SceneLoader).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(loader, null);
            AssertIdle(GameState.Playing);
        }

        [Test]
        public void AlreadyCompletedOperation_IsFinalized()
        {
            backend.Operations.Enqueue(new FakeOperation(true));
            Assert.That(loader.TryLoadScene(ScenePath, GameState.Playing, out _), Is.True);
            AssertIdle(GameState.Playing);
        }

        [Test]
        public void ValidationException_DoesNotReserveState()
        {
            backend.ThrowOnValidation = true;
            Assert.That(loader.TryLoadScene(ScenePath, GameState.Playing, out var error), Is.False);
            Assert.That(error, Does.Contain("validation failed"));
            AssertIdle(GameState.Ready);
        }

        [Test]
        public void CompletionListener_CanStartAnotherRequest()
        {
            bool accepted = false;
            Action<string> next = null;
            next = _ =>
            {
                loader.SceneLoaded -= next;
                accepted = loader.TryLoadScene(ScenePath, GameState.Ready, out _);
            };
            loader.SceneLoaded += next;
            Assert.That(loader.TryLoadScene(ScenePath, GameState.Playing, out _), Is.True);
            backend.LastOperation.Complete();
            Assert.That(accepted, Is.True);
            Assert.That(loader.IsLoading, Is.True);
            Assert.That(manager.ActiveReservation, Is.Not.Null);
            backend.LastOperation.Complete();
            AssertIdle(GameState.Ready);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ThrowingCompletionSubscriber_DoesNotBlockLaterSubscribers(bool failure)
        {
            int calls = 0;
            backend.Activated = !failure;
            if (failure)
            {
                loader.SceneLoadFailed += (_, __) => throw new InvalidOperationException("expected completion listener failure");
                loader.SceneLoadFailed += (_, __) => calls++;
                LogAssert.Expect(LogType.Error, new Regex("without activating"));
            }
            else
            {
                loader.SceneLoaded += _ => throw new InvalidOperationException("expected completion listener failure");
                loader.SceneLoaded += _ => calls++;
            }
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: expected completion listener failure"));
            Assert.That(loader.TryLoadScene(ScenePath, GameState.Playing, out _), Is.True);
            backend.LastOperation.Complete();
            Assert.That(calls, Is.EqualTo(1));
            AssertIdle(failure ? GameState.Ready : GameState.Playing);
        }

        [Test]
        public void IndependentLoaders_DoNotShareBackendsOrReservations()
        {
            var otherRoot = new GameObject("FO002_OtherLoader");
            try
            {
                var otherManager = new GameManager();
                otherManager.Initialize();
                SceneLoader otherLoader = otherRoot.AddComponent<SceneLoader>();
                var otherBackend = new FakeBackend();
                otherLoader.Initialize(otherManager, otherBackend);
                Assert.That(loader.TryLoadScene(ScenePath, GameState.Playing, out _), Is.True);
                Assert.That(otherLoader.TryLoadScene(ScenePath, GameState.Ready, out _), Is.True);
                backend.LastOperation.Complete();
                Assert.That(otherLoader.IsLoading, Is.True);
                Assert.That(otherManager.CurrentState, Is.EqualTo(GameState.Loading));
                otherBackend.LastOperation.Complete();
                Assert.That(otherLoader.IsLoading, Is.False);
            }
            finally { Object.DestroyImmediate(otherRoot); }
        }

        private void SetState(GameState state)
        {
            if (state == GameState.Ready) { return; }
            Assert.That(manager.TryChangeState(GameState.Playing), Is.True);
            if (state == GameState.Paused) { Assert.That(manager.TryChangeState(GameState.Paused), Is.True); }
        }

        private void AssertIdle(GameState state)
        {
            Assert.That(loader.IsLoading, Is.False);
            Assert.That(manager.ActiveReservation, Is.Null);
            Assert.That(manager.CurrentState, Is.EqualTo(state));
            Assert.That(loader.Progress, Is.Zero);
        }
    }
}
