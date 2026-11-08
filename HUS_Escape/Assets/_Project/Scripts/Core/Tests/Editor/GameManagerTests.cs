using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HUSEscape.Core
{
    /// <summary>Verifies state rules independently of scenes and gameplay components.</summary>
    public sealed class GameManagerTests
    {
        private static readonly GameState[] InitializedStates =
        {
            GameState.Ready, GameState.Loading, GameState.Playing, GameState.Paused
        };

        /// <summary>Enumerates every destination for every initialized source state.</summary>
        public static IEnumerable<TestCaseData> TransitionCases()
        {
            var allowed = new Dictionary<GameState, GameState[]>
            {
                { GameState.Ready, new[] { GameState.Loading, GameState.Playing } },
                { GameState.Loading, new[] { GameState.Ready, GameState.Playing, GameState.Paused } },
                { GameState.Playing, new[] { GameState.Loading, GameState.Paused } },
                { GameState.Paused, new[] { GameState.Loading, GameState.Playing } }
            };

            foreach (GameState source in InitializedStates)
            {
                foreach (GameState destination in Enum.GetValues(typeof(GameState)))
                {
                    yield return new TestCaseData(source, destination,
                        Array.IndexOf(allowed[source], destination) >= 0);
                }
            }
        }

        [Test]
        public void NewManager_StartsUninitialized()
        {
            var manager = new GameManager();

            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Initializing));
            Assert.That(manager.IsInitialized, Is.False);
        }

        [Test]
        public void Initialize_CommitsReadyAndNotifiesOnce()
        {
            var manager = new GameManager();
            var changes = new List<(GameState, GameState)>();
            bool initializedDuringEvent = false;
            GameState stateDuringEvent = GameState.Initializing;
            manager.StateChanged += (previous, current) =>
            {
                changes.Add((previous, current));
                initializedDuringEvent = manager.IsInitialized;
                stateDuringEvent = manager.CurrentState;
            };

            Assert.That(manager.Initialize(), Is.True);
            Assert.That(manager.Initialize(), Is.False);
            Assert.That(manager.IsInitialized, Is.True);
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Ready));
            Assert.That(initializedDuringEvent, Is.True);
            Assert.That(stateDuringEvent, Is.EqualTo(GameState.Ready));
            Assert.That(changes, Is.EqualTo(new[] { (GameState.Initializing, GameState.Ready) }));
        }

        [TestCase(GameState.Initializing)]
        [TestCase(GameState.Ready)]
        [TestCase(GameState.Loading)]
        [TestCase(GameState.Playing)]
        [TestCase(GameState.Paused)]
        public void StateChanges_BeforeInitializationAreRejected(GameState destination)
        {
            var manager = new GameManager();
            int notifications = 0;
            manager.StateChanged += (_, __) => notifications++;

            Assert.That(manager.CanTransitionTo(destination), Is.False);
            Assert.That(manager.TryChangeState(destination), Is.False);
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Initializing));
            Assert.That(notifications, Is.Zero);
        }

        [TestCaseSource(nameof(TransitionCases))]
        public void TransitionMatrix_IsEnforced(GameState source, GameState destination, bool expected)
        {
            GameManager manager = CreateAt(source);
            var changes = new List<(GameState, GameState)>();
            manager.StateChanged += (previous, current) => changes.Add((previous, current));

            Assert.That(manager.CanTransitionTo(destination), Is.EqualTo(expected));
            Assert.That(manager.TryChangeState(destination), Is.EqualTo(expected));
            Assert.That(manager.CurrentState, Is.EqualTo(expected ? destination : source));
            Assert.That(changes.Count, Is.EqualTo(expected ? 1 : 0));
            if (expected)
            {
                Assert.That(changes[0], Is.EqualTo((source, destination)));
            }
        }

        [TestCase(-1)]
        [TestCase(99)]
        public void UnknownEnumValues_AreRejected(int value)
        {
            foreach (GameState source in InitializedStates)
            {
                GameManager manager = CreateAt(source);
                int notifications = 0;
                manager.StateChanged += (_, __) => notifications++;

                Assert.That(manager.CanTransitionTo((GameState)value), Is.False);
                Assert.That(manager.TryChangeState((GameState)value), Is.False);
                Assert.That(manager.CurrentState, Is.EqualTo(source));
                Assert.That(notifications, Is.Zero);
            }
        }

        [Test]
        public void StateChanged_ObservesCommittedStateAndSupportsUnsubscription()
        {
            GameManager manager = CreateAt(GameState.Ready);
            int notifications = 0;
            GameState observedState = GameState.Initializing;
            Action<GameState, GameState> listener = (_, __) =>
            {
                notifications++;
                observedState = manager.CurrentState;
            };
            manager.StateChanged += listener;

            Assert.That(manager.TryChangeState(GameState.Playing), Is.True);
            manager.StateChanged -= listener;
            Assert.That(manager.TryChangeState(GameState.Paused), Is.True);

            Assert.That(observedState, Is.EqualTo(GameState.Playing));
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void StateChanged_ReentrantTransitionsAndInitializationAreRejected()
        {
            GameManager manager = CreateAt(GameState.Ready);
            bool nestedTransition = true;
            bool nestedInitialization = true;
            bool canTransition = true;
            manager.StateChanged += (_, __) =>
            {
                canTransition = manager.CanTransitionTo(GameState.Paused);
                nestedTransition = manager.TryChangeState(GameState.Paused);
                nestedInitialization = manager.Initialize();
            };

            Assert.That(manager.TryChangeState(GameState.Playing), Is.True);
            Assert.That(canTransition, Is.False);
            Assert.That(nestedTransition, Is.False);
            Assert.That(nestedInitialization, Is.False);
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Playing));
            Assert.That(manager.CanTransitionTo(GameState.Paused), Is.True);
        }

        [Test]
        public void StateChanged_ThrowingSubscriberDoesNotBlockOtherSubscribers()
        {
            GameManager manager = CreateAt(GameState.Ready);
            int notifications = 0;
            manager.StateChanged += (_, __) => throw new InvalidOperationException("Expected listener failure");
            manager.StateChanged += (_, __) => notifications++;
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Expected listener failure"));

            Assert.That(manager.TryChangeState(GameState.Playing), Is.True);
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Playing));
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(manager.CanTransitionTo(GameState.Paused), Is.True);
        }

        [TestCase(GameState.Ready)]
        [TestCase(GameState.Playing)]
        [TestCase(GameState.Paused)]
        public void SceneLoad_ReservesTransitionsUntilCompletion(GameState source)
        {
            GameManager manager = CreateAt(source);

            Assert.That(manager.TryBeginSceneLoad(out var reservation), Is.True);
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Loading));
            foreach (GameState destination in Enum.GetValues(typeof(GameState)))
            {
                Assert.That(manager.CanTransitionTo(destination), Is.False);
                Assert.That(manager.TryChangeState(destination), Is.False);
            }

            Assert.That(manager.TryBeginSceneLoad(out _), Is.False);
            Assert.That(manager.TryCompleteSceneLoad(reservation, source), Is.True);
            Assert.That(manager.CurrentState, Is.EqualTo(source));
            Assert.That(manager.CanTransitionTo(GameState.Loading), Is.True);
        }

        [TestCase(GameState.Ready)]
        [TestCase(GameState.Playing)]
        [TestCase(GameState.Paused)]
        public void SceneLoad_CompletionCommitsOnceAndReleasesReservation(GameState destination)
        {
            GameManager manager = CreateAt(GameState.Ready);
            Assert.That(manager.TryBeginSceneLoad(out var reservation), Is.True);
            var changes = new List<(GameState, GameState)>();
            manager.StateChanged += (previous, current) => changes.Add((previous, current));

            Assert.That(manager.TryCompleteSceneLoad(reservation, destination), Is.True);
            Assert.That(manager.TryCompleteSceneLoad(reservation, destination), Is.False);
            Assert.That(changes, Is.EqualTo(new[] { (GameState.Loading, destination) }));
            Assert.That(manager.CanTransitionTo(GameState.Loading), Is.True);
        }

        [Test]
        public void SceneLoad_InvalidCompletionPreservesReservation()
        {
            GameManager manager = CreateAt(GameState.Ready);
            Assert.That(manager.TryCompleteSceneLoad(null, GameState.Ready), Is.False);
            Assert.That(manager.TryBeginSceneLoad(out var reservation), Is.True);

            Assert.That(manager.TryCompleteSceneLoad(reservation, GameState.Initializing), Is.False);
            Assert.That(manager.TryCompleteSceneLoad(reservation, (GameState)99), Is.False);
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Loading));
            Assert.That(manager.TryChangeState(GameState.Ready), Is.False);
            Assert.That(manager.TryCompleteSceneLoad(reservation, GameState.Playing), Is.True);
        }

        [Test]
        public void Managers_HaveIndependentStateAndEvents()
        {
            GameManager first = CreateAt(GameState.Ready);
            GameManager second = CreateAt(GameState.Ready);
            int secondNotifications = 0;
            second.StateChanged += (_, __) => secondNotifications++;

            Assert.That(first.TryChangeState(GameState.Playing), Is.True);
            Assert.That(second.CurrentState, Is.EqualTo(GameState.Ready));
            Assert.That(secondNotifications, Is.Zero);
        }

        [Test]
        public void SceneLoad_StaleOrForeignTokensCannotReleaseNewReservation()
        {
            GameManager manager = CreateAt(GameState.Ready);
            Assert.That(manager.TryBeginSceneLoad(out var oldToken), Is.True);
            Assert.That(manager.TryCompleteSceneLoad(oldToken, GameState.Playing), Is.True);
            Assert.That(manager.TryBeginSceneLoad(out var newToken), Is.True);
            GameManager other = CreateAt(GameState.Ready);
            Assert.That(other.TryBeginSceneLoad(out var foreignToken), Is.True);

            Assert.That(manager.TryCompleteSceneLoad(oldToken, GameState.Ready), Is.False);
            Assert.That(manager.TryRecoverSceneLoad(oldToken), Is.False);
            Assert.That(manager.TryRecoverSceneLoad(foreignToken), Is.False);
            Assert.That(manager.ActiveReservation, Is.SameAs(newToken));
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Loading));
            Assert.That(manager.TryRecoverSceneLoad(newToken), Is.True);
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Playing));
            Assert.That(manager.ActiveReservation, Is.Null);
        }

        [TestCase(GameState.Ready)]
        [TestCase(GameState.Playing)]
        [TestCase(GameState.Paused)]
        public void SceneLoad_RejectedDestinationCanRecoverUsingOwnedToken(GameState source)
        {
            GameManager manager = CreateAt(source);
            Assert.That(manager.TryBeginSceneLoad(out var token), Is.True);
            Assert.That(manager.TryCompleteSceneLoad(token, (GameState)99), Is.False);
            Assert.That(manager.OwnsReservation(token), Is.True);
            Assert.That(manager.TryRecoverSceneLoad(token), Is.True);
            Assert.That(manager.CurrentState, Is.EqualTo(source));
            Assert.That(manager.ActiveReservation, Is.Null);
            Assert.That(manager.TryRecoverSceneLoad(token), Is.False);
        }

        [Test]
        public void Initialize_RejectsReentrantCallsDuringInitialNotification()
        {
            var manager = new GameManager();
            bool repeated = true;
            bool changed = true;
            manager.StateChanged += (_, __) =>
            {
                repeated = manager.Initialize();
                changed = manager.TryChangeState(GameState.Playing);
            };

            Assert.That(manager.Initialize(), Is.True);
            Assert.That(repeated, Is.False);
            Assert.That(changed, Is.False);
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Ready));
        }

        [Test]
        public void StateChanged_UsesSubscriberSnapshot()
        {
            GameManager manager = CreateAt(GameState.Ready);
            int removedCalls = 0;
            int addedCalls = 0;
            Action<GameState, GameState> removed = (_, __) => removedCalls++;
            Action<GameState, GameState> added = (_, __) => addedCalls++;
            manager.StateChanged += (_, __) =>
            {
                manager.StateChanged -= removed;
                manager.StateChanged += added;
            };
            manager.StateChanged += removed;

            Assert.That(manager.TryChangeState(GameState.Playing), Is.True);
            Assert.That(removedCalls, Is.EqualTo(1));
            Assert.That(addedCalls, Is.Zero);
            Assert.That(manager.TryChangeState(GameState.Paused), Is.True);
            Assert.That(removedCalls, Is.EqualTo(1));
            Assert.That(addedCalls, Is.EqualTo(1));
        }

        private static GameManager CreateAt(GameState state)
        {
            var manager = new GameManager();
            Assert.That(manager.Initialize(), Is.True);
            if (state == GameState.Paused)
            {
                Assert.That(manager.TryChangeState(GameState.Playing), Is.True);
            }

            if (state != GameState.Ready)
            {
                Assert.That(manager.TryChangeState(state), Is.True);
            }

            return manager;
        }
    }
}
