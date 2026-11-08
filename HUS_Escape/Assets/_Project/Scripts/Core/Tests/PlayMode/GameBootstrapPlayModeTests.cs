using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HUSEscape.Core
{
    /// <summary>Validates real bootstrap callbacks without unloading the Test Runner scene.</summary>
    public sealed class GameBootstrapPlayModeTests
    {
        private readonly List<GameObject> roots = new List<GameObject>();
        private Scene temporaryScene;
        private Scene originalScene;

        [SetUp]
        public void SetUp()
        {
            originalScene = SceneManager.GetActiveScene();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (originalScene.IsValid() && originalScene.isLoaded) { SceneManager.SetActiveScene(originalScene); }
            foreach (GameObject root in roots)
            {
                if (root != null) { Object.Destroy(root); }
            }
            roots.Clear();
            yield return null;
            if (temporaryScene.IsValid() && temporaryScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(temporaryScene);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Initialization_PublishesBoundReadyServicesAndPersistentRoot()
        {
            GameBootstrap bootstrap = CreateBootstrap();
            Assert.That(bootstrap.TryGetServices(out var manager, out var loader), Is.True);
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Ready));
            Assert.That(loader.IsInitializedFor(manager), Is.True);
            Assert.That(bootstrap.gameObject.scene, Is.Not.EqualTo(originalScene));
            Assert.That(manager.ActiveReservation, Is.Null);
            Assert.That(manager.Initialize(), Is.False);
            yield return null;
            Assert.That(bootstrap.TryGetServices(out _, out _), Is.True);
        }

        [UnityTest]
        public IEnumerator Duplicate_IsRejectedAndCannotBeReenabled()
        {
            GameBootstrap owner = CreateBootstrap();
            LogAssert.Expect(LogType.Error, "A GameBootstrap already owns Core initialization; this duplicate is disabled.");
            GameBootstrap duplicate = CreateBootstrap();
            Assert.That(duplicate.enabled, Is.False);
            Assert.That(duplicate.GetComponent<SceneLoader>().enabled, Is.False);
            Assert.That(duplicate.TryGetServices(out var manager, out var loader), Is.False);
            Assert.That(manager, Is.Null);
            Assert.That(loader, Is.Null);
            Assert.That(owner.TryGetServices(out _, out _), Is.True);
            LogAssert.Expect(LogType.Error, "This GameBootstrap did not initialize and cannot be enabled.");
            duplicate.enabled = true;
            yield return null;
            Assert.That(duplicate.enabled, Is.False);
            Assert.That(duplicate.IsInitialized, Is.False);
        }

        [UnityTest]
        public IEnumerator DisabledOwner_RetainsOwnershipAndReenablesSameServices()
        {
            GameBootstrap owner = CreateBootstrap();
            Assert.That(owner.TryGetServices(out var manager, out var loader), Is.True);
            Assert.That(manager.TryChangeState(GameState.Playing), Is.True);
            int notifications = 0;
            manager.StateChanged += (_, __) => notifications++;
            owner.enabled = false;
            Assert.That(owner.TryGetServices(out _, out _), Is.False);
            Assert.That(loader.enabled, Is.False);
            Assert.That(owner.IsInitialized, Is.True);
            LogAssert.Expect(LogType.Error, "A GameBootstrap already owns Core initialization; this duplicate is disabled.");
            GameBootstrap duplicate = CreateBootstrap();
            Assert.That(duplicate.IsInitialized, Is.False);
            owner.enabled = true;
            yield return null;
            Assert.That(owner.TryGetServices(out var restored, out var restoredLoader), Is.True);
            Assert.That(restored, Is.SameAs(manager));
            Assert.That(restoredLoader, Is.SameAs(loader));
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Playing));
            Assert.That(notifications, Is.Zero);
        }

        [UnityTest]
        public IEnumerator InitiallyDisabledBootstrap_InitializesOnlyWhenEnabled()
        {
            GameObject root = NewRoot();
            root.SetActive(false);
            GameBootstrap bootstrap = root.AddComponent<GameBootstrap>();
            bootstrap.enabled = false;
            root.SetActive(true);
            yield return null;
            Assert.That(bootstrap.TryGetServices(out _, out _), Is.False);
            Assert.That(bootstrap.IsInitialized, Is.False);
            bootstrap.enabled = true;
            Assert.That(bootstrap.TryGetServices(out _, out _), Is.True);
        }

        [UnityTest]
        public IEnumerator InactiveOwner_RetainsOwnershipAndRestoresAccess()
        {
            GameBootstrap owner = CreateBootstrap();
            GameManager manager = owner.Manager;
            owner.gameObject.SetActive(false);
            LogAssert.Expect(LogType.Error, "A GameBootstrap already owns Core initialization; this duplicate is disabled.");
            GameBootstrap duplicate = CreateBootstrap();
            Assert.That(duplicate.enabled, Is.False);
            owner.gameObject.SetActive(true);
            yield return null;
            Assert.That(owner.TryGetServices(out var restored, out _), Is.True);
            Assert.That(restored, Is.SameAs(manager));
        }

        [UnityTest]
        public IEnumerator PersistentOwner_SurvivesSceneActivationAndSourceUnload()
        {
            temporaryScene = SceneManager.CreateScene("FO002_BootstrapLifecycle");
            GameObject root = NewRoot();
            SceneManager.MoveGameObjectToScene(root, temporaryScene);
            GameBootstrap bootstrap = root.AddComponent<GameBootstrap>();
            Assert.That(bootstrap.TryGetServices(out var manager, out _), Is.True);
            Assert.That(SceneManager.SetActiveScene(temporaryScene), Is.True);
            Assert.That(SceneManager.SetActiveScene(originalScene), Is.True);
            yield return SceneManager.UnloadSceneAsync(temporaryScene);
            Assert.That(bootstrap != null, Is.True);
            Assert.That(bootstrap.TryGetServices(out var restored, out _), Is.True);
            Assert.That(restored, Is.SameAs(manager));
            Assert.That(originalScene.isLoaded, Is.True);
        }

        [UnityTest]
        public IEnumerator NestedBootstrap_IsRejectedBeforeServicesBecomeAvailable()
        {
            GameObject parent = NewRoot();
            GameObject child = NewRoot();
            child.transform.SetParent(parent.transform);
            LogAssert.Expect(LogType.Error, "GameBootstrap requires a root GameObject and an enabled SceneLoader.");
            GameBootstrap bootstrap = child.AddComponent<GameBootstrap>();
            yield return null;
            Assert.That(bootstrap.TryGetServices(out _, out _), Is.False);
            Assert.That(bootstrap.Manager, Is.Null);
        }

        private GameObject NewRoot()
        {
            var root = new GameObject("FO002_PlayBootstrap");
            roots.Add(root);
            return root;
        }

        private GameBootstrap CreateBootstrap() => NewRoot().AddComponent<GameBootstrap>();
    }
}
