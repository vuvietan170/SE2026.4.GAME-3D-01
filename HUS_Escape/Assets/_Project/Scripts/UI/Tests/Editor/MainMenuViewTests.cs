using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HUSEscape.UI
{
    /// <summary>Checks production view state and event wiring without scene loading.</summary>
    public sealed class MainMenuViewTests
    {
        private GameObject root;
        private GameObject events;
        private MainMenuView view;
        private Button button;
        private EventSystem eventSystem;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("FO003B_EditView", typeof(RectTransform));
            var buttonObject = new GameObject("Start", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(root.transform, false);
            button = buttonObject.GetComponent<Button>();
            var status = new GameObject("Status", typeof(RectTransform), typeof(Text));
            status.transform.SetParent(root.transform, false);
            events = new GameObject("FO003B_Events", typeof(EventSystem));
            eventSystem = events.GetComponent<EventSystem>();
            view = root.AddComponent<MainMenuView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("startButton").objectReferenceValue = button;
            serialized.FindProperty("statusText").objectReferenceValue = status.GetComponent<Text>();
            serialized.FindProperty("eventSystem").objectReferenceValue = eventSystem;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Invoke("Awake");
            Invoke("OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(events);
        }

        [Test]
        public void UnboundView_CannotEmitStartEvenWhenAvailabilityIsRequested()
        {
            int requests = 0;
            view.StartRequested += _ => requests++;
            view.SetState(true, "");
            button.onClick.Invoke();
            Assert.That(view.IsBound, Is.False);
            Assert.That(view.CanStart, Is.False);
            Assert.That(requests, Is.Zero);
        }

        [Test]
        public void BoundReadyView_EnablesStartAndSelectsIt()
        {
            view.Bind();
            Assert.That(view.CanStart, Is.False);
            view.SetState(true, "");
            Assert.That(view.IsBound && view.CanStart, Is.True);
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(button.gameObject));
        }

        [Test]
        public void LoadingState_DisablesStartAndShowsFeedback()
        {
            view.Bind();
            view.SetState(true, "");
            view.SetState(false, "Loading...");
            Assert.That(view.CanStart, Is.False);
            Assert.That(view.Status, Is.EqualTo("Loading..."));
        }

        [Test]
        public void RepeatedClick_IsLatchedBeforeNotifyingListeners()
        {
            int requests = 0;
            bool disabledDuringEvent = false;
            view.StartRequested += source => { requests++; disabledDuringEvent = !source.CanStart; };
            view.Bind();
            view.SetState(true, "");
            button.onClick.Invoke();
            button.onClick.Invoke();
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(disabledDuringEvent, Is.True);
        }

        [Test]
        public void RepeatedEnable_DoesNotDuplicateButtonSubscriptions()
        {
            int requests = 0;
            view.StartRequested += _ => requests++;
            Invoke("OnEnable");
            Invoke("OnEnable");
            view.Bind();
            view.SetState(true, "");
            button.onClick.Invoke();
            Assert.That(requests, Is.EqualTo(1));
        }

        [Test]
        public void Unbind_ClearsSelectionAndPreventsRequests()
        {
            view.Bind();
            view.SetState(true, "");
            view.Unbind();
            Assert.That(view.IsBound || view.CanStart, Is.False);
            Assert.That(eventSystem.currentSelectedGameObject, Is.Null);
            Assert.That(view.Status, Is.Empty);
        }

        [Test]
        public void DisabledView_RejectsDirectButtonInvocations()
        {
            int requests = 0;
            view.StartRequested += _ => requests++;
            view.Bind();
            view.SetState(true, "");
            view.enabled = false;
            Invoke("OnDisable");
            button.onClick.Invoke();
            Assert.That(requests, Is.Zero);
            Assert.That(view.CanStart, Is.False);
        }

        [Test]
        public void Destroying_NotifiesOnceAndClearsEventReferences()
        {
            int notifications = 0;
            view.Destroying += _ => notifications++;
            Invoke("OnDestroy");
            Invoke("OnDestroy");
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingSubscriber_DoesNotPreventOtherSubscribers()
        {
            int requests = 0;
            view.StartRequested += _ => throw new InvalidOperationException("expected UI listener failure");
            view.StartRequested += _ => requests++;
            view.Bind();
            view.SetState(true, "");
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: expected UI listener failure"));
            button.onClick.Invoke();
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(view.CanStart, Is.False);
        }

        private void Invoke(string method) => typeof(MainMenuView).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
    }
}
