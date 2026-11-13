using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HUSEscape.Core
{
    /// <summary>Checks startup configuration and authored scene references without running native loads.</summary>
    public sealed class BootstrapSceneEntryTests
    {
        private const string BootstrapPath = "Assets/_Project/Scenes/Bootstrap/Bootstrap.unity";
        private const string MenuPath = "Assets/_Project/Scenes/MainMenu/MainMenu.unity";
        private const string SamplePath = "Assets/Scenes/SampleScene.unity";
        private GameObject root;
        private BootstrapSceneEntry entry;
        private GameBootstrap bootstrap;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("FO003A_EditEntry");
            bootstrap = root.AddComponent<GameBootstrap>();
            entry = root.AddComponent<BootstrapSceneEntry>();
            Configure(bootstrap, SamplePath, GameState.Playing);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [TestCase(GameState.Ready)]
        [TestCase(GameState.Playing)]
        public void ValidConfiguration_DoesNotInitializeOrLoad(GameState destination)
        {
            Configure(bootstrap, SamplePath, destination);
            Assert.That(entry.TryValidateConfiguration(out var error), Is.True);
            Assert.That(error, Is.Null);
            Assert.That(entry.HasStarted, Is.False);
            Assert.That(entry.HasFinished, Is.False);
            Assert.That(bootstrap.IsInitialized, Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("SampleScene")]
        [TestCase("assets/Scenes/SampleScene.unity")]
        [TestCase("Assets/Scenes/SampleScene.UNITY")]
        public void InvalidPathFormat_IsRejected(string path)
        {
            Configure(bootstrap, path, GameState.Playing);
            Assert.That(entry.TryValidateConfiguration(out var error), Is.False);
            Assert.That(error, Does.Contain("full Assets/ path"));
        }

        [Test]
        public void SelfDestination_IsRejected()
        {
            Configure(bootstrap, BootstrapPath, GameState.Playing);
            Assert.That(entry.TryValidateConfiguration(out var error), Is.False);
            Assert.That(error, Does.Contain("cannot load itself"));
        }

        [TestCase(GameState.Initializing)]
        [TestCase(GameState.Loading)]
        [TestCase(GameState.Paused)]
        [TestCase((GameState)99)]
        public void UnsupportedDestinationState_IsRejected(GameState state)
        {
            Configure(bootstrap, SamplePath, state);
            Assert.That(entry.TryValidateConfiguration(out _), Is.False);
        }

        [Test]
        public void MissingAndForeignBootstrapReferences_AreRejected()
        {
            Configure(null, SamplePath, GameState.Playing);
            Assert.That(entry.TryValidateConfiguration(out _), Is.False);
            var other = new GameObject("FO003A_ForeignOwner");
            try
            {
                Configure(other.AddComponent<GameBootstrap>(), SamplePath, GameState.Playing);
                Assert.That(entry.TryValidateConfiguration(out _), Is.False);
            }
            finally { Object.DestroyImmediate(other); }
        }

        [Test]
        public void UnavailableServices_FinishOnceWithoutChangingCore()
        {
            LogAssert.Expect(LogType.Error, "Bootstrap startup failed: The referenced bootstrap is not the active initialized Core owner.");
            Assert.That(entry.TryBeginStartup(out var error), Is.False);
            Assert.That(error, Is.EqualTo(entry.LastError));
            Assert.That(entry.HasStarted && entry.HasFinished, Is.True);
            Assert.That(entry.enabled, Is.False);
            string initialError = entry.LastError;
            Assert.That(entry.TryBeginStartup(out var repeatedError), Is.False);
            Assert.That(repeatedError, Does.Contain("already been attempted"));
            Assert.That(entry.LastError, Is.EqualTo(initialError));
            Assert.That(bootstrap.IsInitialized, Is.False);
        }

        [Test]
        public void BuildSettings_HaveBootstrapMenuThenPreservedSampleScene()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Assert.That(scenes.Select(scene => scene.path), Is.EqualTo(new[] { BootstrapPath, MenuPath, SamplePath, "Assets/_Project/Scenes/Floor01_Prototype.unity", "Assets/_Project/Scenes/Campus_Prototype.unity", "Assets/_Project/Scenes/TrainingOffice_Prototype.unity" }));
            Assert.That(scenes.All(scene => scene.enabled), Is.True);
            Assert.That(AssetDatabase.AssetPathToGUID(SamplePath), Is.EqualTo("99c9720ab356a0642a771bea13969a05"));
            Assert.That(AssetDatabase.AssetPathToGUID(BootstrapPath), Has.Length.EqualTo(32));
        }

        [Test]
        public void AuthoredScene_HasOneBoundActivePersistentCoreRoot()
        {
            Scene scene = SceneManager.GetSceneByPath(BootstrapPath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) { scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Additive); }
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                Assert.That(roots, Has.Length.EqualTo(1));
                GameObject core = roots[0];
                Assert.That(core.name, Is.EqualTo("BootstrapCore"));
                Assert.That(core.activeSelf, Is.True);
                Assert.That(core.transform.parent, Is.Null);
                Assert.That(core.GetComponents<MonoBehaviour>(), Has.Length.EqualTo(4));
                GameBootstrap owner = core.GetComponent<GameBootstrap>();
                SceneLoader loader = core.GetComponent<SceneLoader>();
                BootstrapSceneEntry startup = core.GetComponent<BootstrapSceneEntry>();
                Assert.That(owner != null && loader != null && startup != null, Is.True);
                Assert.That(owner.enabled && loader.enabled && startup.enabled, Is.True);
                var serialized = new SerializedObject(startup);
                Assert.That(serialized.FindProperty("bootstrap").objectReferenceValue, Is.SameAs(owner));
                Assert.That(serialized.FindProperty("destinationScenePath").stringValue, Is.EqualTo(MenuPath));
                Assert.That(serialized.FindProperty("destinationState").intValue, Is.EqualTo((int)GameState.Ready));
                Assert.That(startup.TryValidateConfiguration(out _), Is.True);
                Assert.That(core.GetComponentsInChildren<Camera>(), Is.Empty);
            }
            finally { if (openedHere) { EditorSceneManager.CloseScene(scene, true); } }
        }

        private void Configure(GameBootstrap owner, string path, GameState state)
        {
            var serialized = new SerializedObject(entry);
            serialized.FindProperty("bootstrap").objectReferenceValue = owner;
            serialized.FindProperty("destinationScenePath").stringValue = path;
            serialized.FindProperty("destinationState").intValue = (int)state;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
