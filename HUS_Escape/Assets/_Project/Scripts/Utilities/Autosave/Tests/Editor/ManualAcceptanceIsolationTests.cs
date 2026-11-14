using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace HUSEscape.Persistence.Autosave.Tests
{
    public sealed class ManualAcceptanceIsolationTests
    {
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        const string EnabledKey = "VT103.ManualAcceptance.Enabled";
        static Type Helper => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("VT103ManualAcceptanceSandbox")).First(t => t != null);

        sealed class StorageOwner
        {
            internal string StorageDirectoryOverride { get; set; }
        }

        static bool Claim(StorageOwner owner) => (bool)Helper.GetMethod("SetStorageOverride", Static)
            .Invoke(null, new object[] { typeof(StorageOwner), owner, @"D:\Game3D-01\VT103_Work\Evidence\manual" });

        [Test] public void ExplicitFixtureStorageCannotBeRedirectedByManualHelper()
        {
            var owner = new StorageOwner { StorageDirectoryOverride = @"D:\Game3D-01\VT103_Work\Evidence\fixture" };
            string before = owner.StorageDirectoryOverride;
            Assert.That(Claim(owner), Is.False);
            Assert.That(owner.StorageDirectoryOverride, Is.EqualTo(before));
        }

        [Test] public void ManualHelperCanClaimUnownedStorageAndRecognizeItsOwnPath()
        {
            var owner = new StorageOwner();
            Assert.That(Claim(owner), Is.True);
            string claimed = owner.StorageDirectoryOverride;
            Assert.That(Claim(owner), Is.True);
            Assert.That(owner.StorageDirectoryOverride, Is.EqualTo(claimed));
        }

        [Test] public void TestRunnerRevokesManualOptInBeforePlayMode()
        {
            bool previous = SessionState.GetBool(EnabledKey, false);
            try
            {
                SessionState.SetBool(EnabledKey, true);
                var callback = (ICallbacks)Activator.CreateInstance(Helper.GetNestedType("TestIsolation", BindingFlags.NonPublic), true);
                callback.RunStarted(null);
                Assert.That(SessionState.GetBool(EnabledKey, true), Is.False);
            }
            finally { SessionState.SetBool(EnabledKey, previous); }
        }

        [Test] public void DisabledManualHelperDoesNotCreateSessionOnEnteringPlayMode()
        {
            bool previous = SessionState.GetBool(EnabledKey, false);
            var root = Helper.GetField("sessionRoot", Static);
            object before = root.GetValue(null);
            try
            {
                SessionState.SetBool(EnabledKey, false);
                Helper.GetMethod("OnPlayModeChanged", Static).Invoke(null, new object[] { PlayModeStateChange.EnteredPlayMode });
                Assert.That(root.GetValue(null), Is.EqualTo(before));
            }
            finally { SessionState.SetBool(EnabledKey, previous); }
        }
    }
}
