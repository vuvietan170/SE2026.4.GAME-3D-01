using System.Linq;
using HUSEscape.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HUSEscape.UI
{
    /// <summary>Validates saved scene composition and exact existing input-action references.</summary>
    public sealed class MainMenuSceneTests
    {
        private const string MenuPath = "Assets/_Project/Scenes/MainMenu/MainMenu.unity";
        private const string BootstrapPath = "Assets/_Project/Scenes/Bootstrap/Bootstrap.unity";

        [Test]
        public void MenuScene_HasOneUnboundViewAndOneInputSystemEventSystem()
        {
            WithScene(MenuPath, scene =>
            {
                GameObject[] roots = scene.GetRootGameObjects();
                MainMenuView[] views = roots.SelectMany(root => root.GetComponentsInChildren<MainMenuView>(true)).ToArray();
                EventSystem[] systems = roots.SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).ToArray();
                Assert.That(views, Has.Length.EqualTo(1));
                Assert.That(systems, Has.Length.EqualTo(1));
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<GameBootstrap>(true)), Is.Empty);
                MainMenuView view = views[0];
                Assert.That(view.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(view.GetComponent<GraphicRaycaster>(), Is.Not.Null);
                Assert.That(view.GetComponent<CanvasScaler>().uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                var serialized = new SerializedObject(view);
                Button button = (Button)serialized.FindProperty("startButton").objectReferenceValue;
                Assert.That(button, Is.Not.Null);
                Assert.That(button.interactable, Is.False);
                Assert.That(serialized.FindProperty("statusText").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("eventSystem").objectReferenceValue, Is.SameAs(systems[0]));
                Assert.That(systems[0].firstSelectedGameObject, Is.SameAs(button.gameObject));
                Assert.That(systems[0].sendNavigationEvents, Is.True);
                Assert.That(systems[0].GetComponents<BaseInputModule>(), Has.Length.EqualTo(1));
                Assert.That(systems[0].GetComponent<InputSystemUIInputModule>(), Is.Not.Null);
            });
        }

        [Test]
        public void InputModule_ReferencesExistingExactUiActions()
        {
            WithScene(MenuPath, scene =>
            {
                var module = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<InputSystemUIInputModule>(true)).Single();
                var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
                Assert.That(module.actionsAsset, Is.SameAs(asset));
                Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)), Is.EqualTo("052faaac586de48259a63d0c4782560b"));
                Assert.That(module.point.action.id, Is.EqualTo(asset.FindAction("UI/Point", true).id));
                Assert.That(module.leftClick.action.id, Is.EqualTo(asset.FindAction("UI/Click", true).id));
                Assert.That(module.move.action.id, Is.EqualTo(asset.FindAction("UI/Navigate", true).id));
                Assert.That(module.submit.action.id, Is.EqualTo(asset.FindAction("UI/Submit", true).id));
                Assert.That(module.cancel.action.id, Is.EqualTo(asset.FindAction("UI/Cancel", true).id));
                Assert.That(module.point.action.type, Is.EqualTo(InputActionType.PassThrough));
                Assert.That(module.leftClick.action.type, Is.EqualTo(InputActionType.PassThrough));
                Assert.That(module.move.action.expectedControlType, Is.EqualTo("Vector2"));
                Assert.That(module.submit.action.type, Is.EqualTo(InputActionType.Button));
                Assert.That(module.cancel.action.type, Is.EqualTo(InputActionType.Button));
                Assert.That(module.point.action.bindings.Any(binding => binding.path == "<Mouse>/position"), Is.True);
                Assert.That(module.leftClick.action.bindings.Any(binding => binding.path == "<Mouse>/leftButton"), Is.True);
                Assert.That(module.move.action.bindings.Any(binding => binding.path == "<Keyboard>/downArrow"), Is.True);
                Assert.That(module.submit.action.bindings.Single().path, Is.EqualTo("*/{Submit}"));
                Assert.That(module.cancel.action.bindings.Single().path, Is.EqualTo("*/{Cancel}"));
            });
        }

        [Test]
        public void Bootstrap_WiresOnlyForwardUiDependencyAndReadyDestination()
        {
            WithScene(BootstrapPath, scene =>
            {
                GameObject root = scene.GetRootGameObjects().Single();
                GameBootstrap bootstrap = root.GetComponent<GameBootstrap>();
                MainMenuFlow flow = root.GetComponent<MainMenuFlow>();
                Assert.That(flow, Is.Not.Null);
                Assert.That(root.GetComponents<MonoBehaviour>(), Has.Length.EqualTo(4));
                var serialized = new SerializedObject(flow);
                Assert.That(serialized.FindProperty("bootstrap").objectReferenceValue, Is.SameAs(bootstrap));
                Assert.That(serialized.FindProperty("playScenePath").stringValue, Is.EqualTo("Assets/Scenes/SampleScene.unity"));
                var startup = new SerializedObject(root.GetComponent<BootstrapSceneEntry>());
                Assert.That(startup.FindProperty("destinationScenePath").stringValue, Is.EqualTo(MenuPath));
                Assert.That(startup.FindProperty("destinationState").intValue, Is.EqualTo((int)GameState.Ready));
            });
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path), Is.EqualTo(new[] { BootstrapPath, MenuPath, "Assets/Scenes/SampleScene.unity", "Assets/_Project/Scenes/Floor01_Prototype.unity", "Assets/_Project/Scenes/Campus_Prototype.unity", "Assets/_Project/Scenes/TrainingOffice_Prototype.unity" }));
            Assert.That(EditorBuildSettings.scenes.All(scene => scene.enabled), Is.True);
        }

        private static void WithScene(string path, System.Action<Scene> check)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) { scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); }
            try { check(scene); }
            finally { if (opened) { EditorSceneManager.CloseScene(scene, true); } }
        }
    }
}
