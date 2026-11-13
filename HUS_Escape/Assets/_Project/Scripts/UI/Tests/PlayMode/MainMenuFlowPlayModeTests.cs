using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HUSEscape.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HUSEscape.UI
{
    /// <summary>Exercises real menu composition and input while protecting the runner from Single loads.</summary>
    public sealed class MainMenuFlowPlayModeTests
    {
        private const string MenuPath = "Assets/_Project/Scenes/MainMenu/MainMenu.unity";
        private readonly List<GameObject> extraRoots = new List<GameObject>();
        private readonly List<Scene> scenes = new List<Scene>();
        private Scene original;
        private Scene menu;
        private GameObject core;
        private GameBootstrap bootstrap;
        private GameManager manager;
        private SceneLoader loader;
        private MainMenuFlow flow;
        private MainMenuView view;
        private Button button;
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings originalInputSettings;
        private InputSettings testInputSettings;
        private HideFlags originalInputFlags;
        private bool initiallyDisabled;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            keyboard = null;
            mouse = null;
            originalInputSettings = null;
            testInputSettings = null;
            original = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync(MenuPath, LoadSceneMode.Additive);
            menu = SceneManager.GetSceneByPath(MenuPath);
            scenes.Add(menu);
            SceneManager.SetActiveScene(menu);
            view = menu.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuView>()).Single();
            button = (Button)Field(view, "startButton");
            initiallyDisabled = !view.CanStart && !button.interactable;
            core = new GameObject("FO003B_TestCore");
            core.SetActive(false);
            bootstrap = core.AddComponent<GameBootstrap>();
            flow = core.AddComponent<MainMenuFlow>();
            SetField(flow, "bootstrap", bootstrap);
            core.SetActive(true);
            Assert.That(bootstrap.TryGetServices(out manager, out loader), Is.True);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (original.IsValid() && original.isLoaded) { SceneManager.SetActiveScene(original); }
            if (keyboard != null && keyboard.added) { InputSystem.RemoveDevice(keyboard); }
            if (mouse != null && mouse.added) { InputSystem.RemoveDevice(mouse); }
            if (originalInputSettings != null)
            {
                InputSystem.settings = originalInputSettings;
                originalInputSettings.hideFlags = originalInputFlags;
                Assert.That(InputSystem.settings, Is.SameAs(originalInputSettings));
            }
            if (testInputSettings != null) { Object.Destroy(testInputSettings); }
            if (core != null) { Object.Destroy(core); }
            foreach (var root in extraRoots) { if (root != null) { Object.Destroy(root); } }
            extraRoots.Clear();
            yield return null;
            foreach (Scene scene in scenes) { if (scene.IsValid() && scene.isLoaded) { yield return SceneManager.UnloadSceneAsync(scene); } }
            scenes.Clear();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LateInitialization_BindsOnlyReadyCoreAndFocusesStart()
        {
            Assert.That(initiallyDisabled, Is.True);
            Assert.That(flow.BoundView, Is.SameAs(view));
            Assert.That(view.IsBound && view.CanStart, Is.True);
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Ready));
            Assert.That(((EventSystem)Field(view, "eventSystem")).currentSelectedGameObject, Is.SameAs(button.gameObject));
            Assert.That(view.Status, Is.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedBinding_DoesNotDuplicateAnySubscription()
        {
            flow.BindLoadedScene(menu);
            flow.BindLoadedScene(menu);
            Assert.That(ListenerCount(view, "StartRequested", flow), Is.EqualTo(1));
            Assert.That(ListenerCount(view, "Destroying", flow), Is.EqualTo(1));
            Assert.That(ListenerCount(loader, "SceneLoaded", flow), Is.EqualTo(1));
            Assert.That(ListenerCount(manager, "StateChanged", flow), Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartCommand_IsAcceptedOnceAndShowsLoadingBeforeDispatch()
        {
            int requests = 0;
            bool feedbackBeforeRequest = false;
            SetRequest((string path, GameState state, out string error) =>
            {
                requests++;
                Assert.That(path, Is.EqualTo("Assets/_Project/Scenes/Floor01_Prototype.unity"));
                Assert.That(state, Is.EqualTo(GameState.Playing));
                feedbackBeforeRequest = !view.CanStart && view.Status == "Loading...";
                error = null;
                manager.TryChangeState(GameState.Loading);
                return true;
            });
            button.onClick.Invoke();
            button.onClick.Invoke();
            yield return null;
            button.onClick.Invoke();
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(feedbackBeforeRequest, Is.True);
            Assert.That(flow.IsLoading, Is.True);
            Assert.That(view.CanStart, Is.False);
        }

        [UnityTest]
        public IEnumerator RealLoaderRejection_ShowsSafeRetryFeedbackWithoutChangingScene()
        {
            SetField(flow, "playScenePath", "Assets/Scenes/Missing.unity");
            LogAssert.Expect(LogType.Error, new Regex("Main menu Start rejected: Scene path must exactly match"));
            button.onClick.Invoke();
            yield return null;
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Ready));
            Assert.That(loader.IsLoading || flow.IsLoading, Is.False);
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(menu.handle));
            Assert.That(view.CanStart, Is.True);
            Assert.That(view.Status, Is.EqualTo("Unable to start. Please try again."));
            Assert.That(view.Status, Does.Not.Contain("Assets/"));
            Assert.That(flow.LastError, Does.Contain("Missing.unity"));
            int retried = 0;
            SetRequest((string path, GameState state, out string error) => { retried++; error = null; return true; });
            button.onClick.Invoke();
            Assert.That(retried, Is.EqualTo(1));
            Assert.That(flow.IsLoading, Is.True);
            // Deliver the real instance event's subscriber snapshot. Core tests separately verify
            // native-boundary failure publication; this verifies UI recovery without a Single load.
            var failureListeners = (Action<string, string>)Field(loader, "SceneLoadFailed");
            failureListeners("Assets/_Project/Scenes/Floor01_Prototype.unity", "Synthetic completion failure");
            Assert.That(flow.IsLoading, Is.False);
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Ready));
            Assert.That(flow.LastError, Is.EqualTo("Synthetic completion failure"));
            Assert.That(view.CanStart, Is.True);
            Assert.That(view.Status, Is.EqualTo("Unable to start. Please try again."));
        }

        [UnityTest]
        public IEnumerator DisabledFlow_UnbindsAndReenablesWithoutDuplicatingListeners()
        {
            flow.enabled = false;
            Assert.That(flow.BoundView, Is.Null);
            Assert.That(view.IsBound || view.CanStart, Is.False);
            Assert.That(ListenerCount(loader, "SceneLoaded", flow), Is.Zero);
            Assert.That(ListenerCount(manager, "StateChanged", flow), Is.Zero);
            flow.enabled = true;
            yield return null;
            yield return null;
            Assert.That(flow.BoundView, Is.SameAs(view));
            Assert.That(view.CanStart, Is.True);
            Assert.That(ListenerCount(view, "StartRequested", flow), Is.EqualTo(1));
            Assert.That(ListenerCount(loader, "SceneLoaded", flow), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator OwnerAvailabilityChange_DisablesAndRestoresStart()
        {
            bootstrap.enabled = false;
            yield return null;
            Assert.That(view.CanStart, Is.False);
            bootstrap.enabled = true;
            yield return null;
            Assert.That(view.CanStart, Is.True);
            Assert.That(bootstrap.Manager, Is.SameAs(manager));
        }

        [UnityTest]
        public IEnumerator NonReadyState_CannotEmitStart()
        {
            manager.TryChangeState(GameState.Playing);
            yield return null;
            Assert.That(view.CanStart, Is.False);
            button.onClick.Invoke();
            Assert.That(loader.IsLoading, Is.False);
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Playing));
        }

        [UnityTest]
        public IEnumerator SceneUnloading_ReleasesAllViewReferencesImmediately()
        {
            SceneManager.SetActiveScene(original);
            yield return SceneManager.UnloadSceneAsync(menu);
            Assert.That(flow.BoundView, Is.Null);
            Assert.That(loader.IsLoading, Is.False);
            Assert.That(bootstrap.TryGetServices(out _, out _), Is.True);
        }

        [UnityTest]
        public IEnumerator StaleViewDestroyCallback_CannotClearNewBinding()
        {
            var callback = (Action<MainMenuView>)Field(view, "Destroying");
            Scene replacement = SceneManager.CreateScene("FO003B_ReplacementView");
            scenes.Add(replacement);
            var root = new GameObject("ReplacementView");
            SceneManager.MoveGameObjectToScene(root, replacement);
            MainMenuView replacementView = root.AddComponent<MainMenuView>();
            SceneManager.SetActiveScene(replacement);
            flow.BindLoadedScene(replacement);
            callback(view);
            Assert.That(flow.BoundView, Is.SameAs(replacementView));
            Assert.That(view.IsBound, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MultipleViews_AreRejectedWithoutGlobalFallback()
        {
            var extra = new GameObject("ExtraView");
            extraRoots.Add(extra);
            extra.AddComponent<MainMenuView>();
            LogAssert.Expect(LogType.Error, "A menu scene must contain exactly one MainMenuView.");
            flow.BindLoadedScene(menu);
            Assert.That(flow.BoundView, Is.Null);
            Assert.That(view.CanStart, Is.False);
            Object.Destroy(extra);
            yield return null;
            flow.BindLoadedScene(menu);
            Assert.That(flow.BoundView, Is.SameAs(view));
        }

        [UnityTest]
        public IEnumerator MissingOwner_IsRejectedWithoutFindingAnotherCore()
        {
            var invalid = new GameObject("FO003B_InvalidFlow");
            extraRoots.Add(invalid);
            LogAssert.Expect(LogType.Error, "MainMenuFlow requires its active initialized BootstrapCore owner.");
            MainMenuFlow invalidFlow = invalid.AddComponent<MainMenuFlow>();
            yield return null;
            Assert.That(invalidFlow.enabled, Is.False);
            Assert.That(invalidFlow.BoundView, Is.Null);
            Assert.That(flow.BoundView, Is.SameAs(view));
        }

        [UnityTest]
        public IEnumerator KeyboardNavigationAndSubmit_UseRealInputModule()
        {
            ConfigureHeadlessInput();
            keyboard = InputSystem.AddDevice<Keyboard>();
            yield return null;
            int moves = 0;
            var trigger = button.gameObject.AddComponent<EventTrigger>();
            var move = new EventTrigger.Entry { eventID = EventTriggerType.Move };
            move.callback.AddListener(_ => moves++);
            trigger.triggers.Add(move);
            int requests = InstallAcceptedRequest();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.DownArrow));
            yield return null;
            yield return null;
            Assert.That(moves, Is.GreaterThan(0), InputDiagnostics());
            Assert.That(((EventSystem)Field(view, "eventSystem")).currentSelectedGameObject, Is.SameAs(button.gameObject));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That((int)requestCount, Is.EqualTo(requests + 1));
            Assert.That(view.CanStart, Is.False);
        }

        [UnityTest]
        public IEnumerator MousePointAndClick_UseRealInputModuleAndStartOnce()
        {
            ConfigureHeadlessInput();
            mouse = InputSystem.AddDevice<Mouse>();
            yield return null;
            InstallAcceptedRequest();
            Canvas.ForceUpdateCanvases();
            RectTransform rect = button.GetComponent<RectTransform>();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left, true));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            yield return null;
            Assert.That(requestCount, Is.EqualTo(1), InputDiagnostics());
            Assert.That(view.Status, Is.EqualTo("Loading..."));
        }

        private string InputDiagnostics()
        {
            var events = (EventSystem)Field(view, "eventSystem");
            var module = events.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            return $"update={InputSystem.settings.updateMode}, background={InputSystem.settings.backgroundBehavior}, "
                + $"deviceKeyboard={keyboard?.enabled}/{keyboard?.downArrowKey.ReadValue()}, deviceMouse={mouse?.enabled}/{mouse?.position.ReadValue()}, "
                + $"controls={module.move.action.controls.Count}/{module.point.action.controls.Count}, mask={module.actionsAsset.bindingMask}, "
                + $"focused={events.isFocused}, current={EventSystem.current == events}, module={events.currentInputModule}, "
                + $"move={module.move.action.enabled}/{module.move.action.ReadValue<Vector2>()}, "
                + $"point={module.point.action.enabled}/{module.point.action.ReadValue<Vector2>()}, "
                + $"click={module.leftClick.action.enabled}/{module.leftClick.action.ReadValue<float>()}";
        }

        private void ConfigureHeadlessInput()
        {
            // Background Editor routes real keyboard/mouse events away from Game View.
            // Use a disposable settings clone for this test; never edit the project's asset.
            originalInputSettings = InputSystem.settings;
            originalInputFlags = originalInputSettings.hideFlags;
            // InputManager destroys the previous object if its flags equal HideAndDontSave.
            // Retain the existing transient defaults while swapping, then restore their exact flags.
            if (originalInputFlags == HideFlags.HideAndDontSave) { originalInputSettings.hideFlags = HideFlags.DontSave; }
            testInputSettings = Object.Instantiate(originalInputSettings);
            testInputSettings.hideFlags = HideFlags.HideAndDontSave;
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInputSettings;
        }

        private int requestCount;
        private int InstallAcceptedRequest()
        {
            requestCount = 0;
            SetRequest((string path, GameState destination, out string error) =>
            {
                requestCount++;
                error = null;
                manager.TryChangeState(GameState.Loading);
                return true;
            });
            return requestCount;
        }

        private void SetRequest(MainMenuFlow.SceneRequest request) => SetField(flow, "requestScene", request);
        private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static int ListenerCount(object source, string name, object target)
        {
            var listeners = (Delegate)Field(source, name);
            return listeners == null ? 0 : listeners.GetInvocationList().Count(listener => ReferenceEquals(listener.Target, target));
        }
    }
}
