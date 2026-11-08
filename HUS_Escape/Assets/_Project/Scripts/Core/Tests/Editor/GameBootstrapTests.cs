using NUnit.Framework;
using UnityEngine;

namespace HUSEscape.Core
{
    /// <summary>Checks service availability and the production ownership selector without Play Mode.</summary>
    public sealed class GameBootstrapTests
    {
        private GameObject first;
        private GameObject second;

        [SetUp]
        public void SetUp()
        {
            first = new GameObject("FO002_EditBootstrapA");
            second = new GameObject("FO002_EditBootstrapB");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        [Test]
        public void Services_AreUnavailableBeforeInitialization()
        {
            GameBootstrap bootstrap = first.AddComponent<GameBootstrap>();
            Assert.That(bootstrap.TryGetServices(out var manager, out var loader), Is.False);
            Assert.That(manager, Is.Null);
            Assert.That(loader, Is.Null);
            Assert.That(bootstrap.IsInitialized, Is.False);
        }

        [Test]
        public void OwnershipSelection_IsIndependentOfCandidateOrder()
        {
            GameBootstrap a = first.AddComponent<GameBootstrap>();
            GameBootstrap b = second.AddComponent<GameBootstrap>();
            GameBootstrap expected = a.GetEntityId().CompareTo(b.GetEntityId()) < 0 ? a : b;

            Assert.That(GameBootstrap.SelectOwner(new[] { a, b }), Is.SameAs(expected));
            Assert.That(GameBootstrap.SelectOwner(new[] { b, a }), Is.SameAs(expected));
            Assert.That(a.IsInitialized || b.IsInitialized, Is.False);
        }

        [Test]
        public void OwnershipSelection_ExcludesDisabledInactiveAndNestedCandidates()
        {
            GameBootstrap a = first.AddComponent<GameBootstrap>();
            GameBootstrap b = second.AddComponent<GameBootstrap>();
            a.enabled = false;
            Assert.That(GameBootstrap.SelectOwner(new[] { a, b }), Is.SameAs(b));
            a.enabled = true;
            first.SetActive(false);
            Assert.That(GameBootstrap.SelectOwner(new[] { a, b }), Is.SameAs(b));
            first.SetActive(true);
            first.transform.SetParent(second.transform);
            Assert.That(GameBootstrap.SelectOwner(new[] { a, b }), Is.SameAs(b));
            first.transform.SetParent(null);
            a.GetComponent<SceneLoader>().enabled = false;
            Assert.That(GameBootstrap.SelectOwner(new[] { a, b }), Is.SameAs(b));
        }
    }
}
