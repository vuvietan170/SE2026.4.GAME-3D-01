using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using HUSEscape.Persistence.Autosave;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HUSEscape.Persistence.Autosave.Tests
{
    // Reflection only crosses Unity's predefined Assembly-CSharp boundary.
    // All snapshots, quest events, inventories and writes are production components/code.
    public sealed class Floor01AutosaveIntegrationTests
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        Scene scene, original;
        Component save, quests, inventory, auto;
        string directory;
        bool automaticallyBound;
        float previousCaptureDelta;
        readonly string[] slots = { "floor01-v1.json", "campus-v1.json", "trainingoffice-v1.json" };
        byte[][] manualBytes;
        static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object o, string p) => o.GetType().GetProperty(p, All).GetValue(o);
        static object Call(object o, string m, params object[] args) => o.GetType().GetMethod(m, All).Invoke(o, args);
        T Find<T>(string type) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).First(c => c.GetType().FullName == type);
        void BeforeStart(Scene loaded, LoadSceneMode mode)
        {
            if (loaded.name != "Floor01_Prototype") return;
            foreach (var b in loaded.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)))
                if (b != null && b.GetType().Name == "JourneySceneEntry") b.enabled = false;
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            original = SceneManager.GetActiveScene(); Time.timeScale = 1;
            previousCaptureDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60f;
            directory = Path.Combine(Environment.GetEnvironmentVariable("VT103_TEST_DATA_ROOT") ?? @"D:\Game3D-01\VT103_Work\Evidence\VT103\integration-data", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            foreach (string slot in slots) File.WriteAllText(Path.Combine(directory, slot), "manual-slot-unchanged:" + slot);
            manualBytes = slots.Select(slot => File.ReadAllBytes(Path.Combine(directory, slot))).ToArray();
            SceneManager.sceneLoaded += BeforeStart;
            yield return SceneManager.LoadSceneAsync("Assets/_Project/Scenes/Floor01_Prototype.unity", LoadSceneMode.Additive);
            SceneManager.sceneLoaded -= BeforeStart;
            scene = SceneManager.GetSceneByName("Floor01_Prototype"); SceneManager.SetActiveScene(scene);
            save = Find<MonoBehaviour>("HUSEscape.Persistence.Floor01SaveSystem");
            quests = Find<MonoBehaviour>("HUSEscape.Quests.QuestManager");
            inventory = (Component)Get(quests, "Inventory");
            save.GetType().GetProperty("StorageDirectoryOverride", All).SetValue(save, directory);
            auto = save.GetComponent(RuntimeType("HUSEscape.Persistence.Floor01Autosave"));
            automaticallyBound = auto != null;
            if (auto == null) auto = save.gameObject.AddComponent(RuntimeType("HUSEscape.Persistence.Floor01Autosave"));
            Call(auto, "Bind", save, quests);
            // Allow the actual CharacterController to settle. No synthetic ground flags.
            float until = Time.realtimeSinceStartup + 5;
            while (!Find<CharacterController>("UnityEngine.CharacterController").isGrounded && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(Find<CharacterController>("UnityEngine.CharacterController").isGrounded, Is.True, "Fixture must reach real ground before testing captures.");
            AssertStorageIsolation();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded -= BeforeStart;
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Time.timeScale = 1; Time.captureDeltaTime = previousCaptureDelta;
        }
        void Progress()
        {
            Assert.That((bool)Call(inventory, "AddItem", "Key_A101"), Is.True);
            Call(quests, "Signal", "item:Key_A101");
        }
        IEnumerator Until(Func<bool> condition, float seconds = 4)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) { AssertStorageIsolation(); yield return null; }
            AssertStorageIsolation();
        }
        void AssertStorageIsolation()
        {
            Assert.That((string)Get(save, "SavePath"), Is.EqualTo(Path.Combine(directory, slots[0])), "An Editor helper must not redirect the fixture's manual slot.");
            Assert.That((string)Get(auto, "DraftPath"), Is.EqualTo(Path.Combine(directory, "floor01-draft-v1.json")), "An Editor helper must not rebind the fixture's autosave.");
        }
        void AssertManualBytes()
        {
            for (int i = 0; i < slots.Length; i++)
                Assert.That(File.ReadAllBytes(Path.Combine(directory, slots[i])), Is.EqualTo(manualBytes[i]), slots[i]);
        }
        DraftDocument Read()
        { using (var f = File.OpenRead((string)Get(auto, "DraftPath"))) return (DraftDocument)new DataContractJsonSerializer(typeof(DraftDocument)).ReadObject(f); }
        [UnityTest] public IEnumerator I01_NewQuestProgressCreatesValidatedFloorDraft()
        {
            Progress(); yield return Until(() => (int)Get(auto, "CommitCount") == 1);
            if ((int)Get(auto, "CommitCount") == 0)
            {
                var diagnostics = "error=" + Get(auto, "LastError") + "; pending=" + Get(auto, "HasPendingChanges");
                foreach (var b in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)))
                    if (b != null && new[] { "DoorInteraction", "DoorHandleIK", "PickupItem", "CodeLockCabinet", "SequenceLockPanel" }.Contains(b.GetType().Name))
                        foreach (var property in b.GetType().GetProperties(All).Where(p => new[] { "IsBusy", "IsPrepared", "CurrentWeight", "IsOpening" }.Contains(p.Name)))
                            diagnostics += "\n" + b.name + ":" + property.Name + "=" + property.GetValue(b);
                diagnostics += "\ngrounded=" + Find<CharacterController>("UnityEngine.CharacterController").isGrounded + "; pos=" + inventory.transform.position + "; delta=" + Time.deltaTime + "; timescale=" + Time.timeScale;
                File.WriteAllText(Path.Combine(directory, "capture-diagnostics.txt"), diagnostics);
            }
            Assert.That((int)Get(auto, "CommitCount"), Is.EqualTo(1), "New quest progress must commit a draft.");
            var head = Read(); Assert.That(head.revision, Is.GreaterThan(0));
            var snapshotType = RuntimeType("HUSEscape.Persistence.Floor01SaveData");
            var snapshot = JsonUtility.FromJson(head.payload, snapshotType);
            Assert.That(snapshotType.GetField("version").GetValue(snapshot), Is.EqualTo(1));
            Assert.That(snapshotType.GetField("scene").GetValue(snapshot), Is.EqualTo("Floor01_Prototype"));
            Assert.That((string[])snapshotType.GetField("inventory").GetValue(snapshot), Does.Contain("Key_A101"));
            Assert.That((string[])snapshotType.GetField("facts").GetValue(snapshot), Does.Contain("item:Key_A101"));
        }
        [UnityTest] public IEnumerator I02_RepeatedFactAndIdleFramesDoNotCommitAgain()
        {
            Progress(); yield return Until(() => (int)Get(auto, "CommitCount") == 1);
            Assert.That((int)Get(auto, "CommitCount"), Is.EqualTo(1), "Initial progress must save before duplicate checks.");
            byte[] before = File.ReadAllBytes((string)Get(auto, "DraftPath"));
            Assert.That((bool)Call(quests, "Signal", "item:Key_A101"), Is.False);
            yield return Until(() => false, .8f);
            Assert.That((int)Get(auto, "CommitCount"), Is.EqualTo(1));
            Assert.That(File.ReadAllBytes((string)Get(auto, "DraftPath")), Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator I03_ReversedCapturedRequestsCannotRegressDiskRevision()
        {
            var first = (Task<DraftRequest>)Call(auto, "CaptureSnapshotAsync"); yield return Until(() => first.IsCompleted);
            Assert.That(first.IsCompleted && !first.IsFaulted, Is.True, "First real Floor01 capture must succeed.");
            var second = (Task<DraftRequest>)Call(auto, "CaptureSnapshotAsync"); yield return Until(() => second.IsCompleted);
            Assert.That(second.IsCompleted && !second.IsFaulted, Is.True);
            Assert.That(second.Result.Revision, Is.GreaterThan(first.Result.Revision), "Capture revisions must increase.");
            var newer = (Task<DraftWriteStatus>)Call(auto, "CommitCapturedAsync", second.Result); yield return Until(() => newer.IsCompleted);
            Assert.That(newer.Result, Is.EqualTo(DraftWriteStatus.Committed));
            var older = (Task<DraftWriteStatus>)Call(auto, "CommitCapturedAsync", first.Result); yield return Until(() => older.IsCompleted);
            Assert.That(older.Result, Is.EqualTo(DraftWriteStatus.RejectedStale));
            Assert.That(Read().revision, Is.EqualTo(second.Result.Revision));
        }
        [UnityTest] public IEnumerator I04_AutosavePreservesAllManualSlotsByteForByte()
        {
            Progress(); yield return Until(() => (int)Get(auto, "CommitCount") == 1);
            Assert.That((int)Get(auto, "CommitCount"), Is.EqualTo(1), "Must actually autosave before checking slot isolation.");
            AssertManualBytes();
            Assert.That(Path.GetFileName((string)Get(auto, "DraftPath")), Is.EqualTo("floor01-draft-v1.json"));
        }
        [UnityTest] public IEnumerator I05_PersistenceFailurePreservesTheActiveSession()
        {
            // A directory at the draft file path produces a real, controlled filesystem failure.
            // No fake store or production save is touched.
            Directory.CreateDirectory(Path.Combine(directory, "floor01-draft-v1.json"));
            Progress(); yield return Until(() => !string.IsNullOrEmpty((string)Get(auto, "LastError")));
            Assert.That((string)Get(auto, "LastError"), Is.Not.Empty, "Persistence failure must be exposed, never acknowledged as saved.");
            Assert.That((int)Get(auto, "CommitCount"), Is.EqualTo(0));
            Assert.That((bool)Get(auto, "HasPendingChanges"), Is.True, "Failed persistence must retain dirty gameplay progress.");
            Assert.That(Directory.Exists((string)Get(auto, "DraftPath")), Is.True);
            Assert.That(Directory.GetFiles(directory, "*.pending"), Is.Empty);
            Assert.That((bool)Call(inventory, "HasItem", "Key_A101"), Is.True);
            Assert.That((bool)Call(quests, "HasObserved", "item:Key_A101"), Is.True);
            Assert.That(((Behaviour)quests).isActiveAndEnabled, Is.True);
            AssertManualBytes();
        }

        [UnityTest] public IEnumerator I06_FloorSceneAutomaticallyComposesOneAutosaveOwner()
        {
            Assert.That(automaticallyBound, Is.True, "Floor01SaveSystem.Awake must attach autosave without a scene edit.");
            Assert.That(save.GetComponents(RuntimeType("HUSEscape.Persistence.Floor01Autosave")).Length, Is.EqualTo(1));
            Assert.That((int)Get(auto, "CommitCount"), Is.EqualTo(0), "Scene startup alone is not new progress.");
            yield return null;
        }
        [UnityTest] public IEnumerator I07_NewGameCancelsPendingProgressWithoutAutosavingRestoredState()
        {
            Progress();
            Assert.That((bool)Get(auto, "HasPendingChanges"), Is.True, "New Game must cancel actual pending progress.");
            Assert.That((bool)Call(save, "TryNewGame", true), Is.True);
            yield return Until(() => false, .8f);
            Assert.That((int)Get(auto, "CommitCount"), Is.EqualTo(0));
            Assert.That(File.Exists((string)Get(auto, "DraftPath")), Is.False);
            Assert.That((bool)Get(auto, "HasPendingChanges"), Is.False);
            Assert.That((bool)Call(inventory, "HasItem", "Key_A101"), Is.False);
            AssertManualBytes();
        }
        [UnityTest] public IEnumerator I08_UnsafeAirborneCaptureDefersThenRetriesAfterGrounding()
        {
            auto.GetType().GetField("debounceSeconds", All).SetValue(auto, .05f);
            var player = Find<CharacterController>("UnityEngine.CharacterController");
            player.Move(Vector3.up);
            Assert.That(player.isGrounded, Is.False, "Real CharacterController must be airborne for this test.");
            var unsafeCapture = Assert.Throws<TargetInvocationException>(() => Call(auto, "CaptureSnapshotAsync"));
            Assert.That(unsafeCapture.InnerException, Is.InstanceOf<InvalidOperationException>(), "The real safe capture API must reject airborne state.");
            Progress();
            int airborneFrames = 0;
            while (!player.isGrounded && airborneFrames < 180)
            {
                Assert.That((int)Get(auto, "CommitCount"), Is.EqualTo(0), "No commit while the actual player is airborne.");
                Assert.That((bool)Get(auto, "HasPendingChanges"), Is.True);
                airborneFrames++; yield return null;
            }
            Assert.That(airborneFrames, Is.GreaterThan(0));
            yield return Until(() => (int)Get(auto, "CommitCount") == 1);
            Assert.That((int)Get(auto, "CommitCount"), Is.EqualTo(1), "Pending progress must save when safe again.");
        }
        [UnityTest] public IEnumerator I09_ReenableDoesNotDuplicateEventSubscriptions()
        {
            for (int i = 0; i < 3; i++) { ((Behaviour)auto).enabled = false; ((Behaviour)auto).enabled = true; }
            Progress(); yield return Until(() => (int)Get(auto, "CommitCount") == 1);
            Assert.That((int)Get(auto, "CommitCount"), Is.EqualTo(1));
            yield return Until(() => false, .8f);
            Assert.That((int)Get(auto, "CommitCount"), Is.EqualTo(1));
            // An unloaded/disabled owner must stop receiving new quest progress.
            ((Behaviour)auto).enabled = false;
            Call(quests, "RestoreFacts", new object[] { Array.Empty<string>() });
            Call(quests, "Signal", "item:Key_A101");
            Assert.That((bool)Get(auto, "HasPendingChanges"), Is.False);
        }
        [UnityTest] public IEnumerator I10_CorruptDraftIsPreservedAndDoesNotLoadIntoGameplay()
        {
            string path = (string)Get(auto, "DraftPath"); File.WriteAllText(path, "not a valid draft");
            Progress(); yield return Until(() => !string.IsNullOrEmpty((string)Get(auto, "LastError")));
            Assert.That((string)Get(auto, "LastError"), Is.Not.Empty);
            Assert.That(File.ReadAllText(path), Is.EqualTo("not a valid draft"));
            Assert.That((int)Get(auto, "CommitCount"), Is.EqualTo(0));
            Assert.That((bool)Call(inventory, "HasItem", "Key_A101"), Is.True);
            Assert.That((bool)Get(auto, "HasPendingChanges"), Is.True);
            AssertManualBytes();
        }
        [UnityTest] public IEnumerator I11_ExistingHeadSeedsRevisionWithoutRestoringItsPayload()
        {
            var seed = new DraftStore((string)Get(auto, "DraftPath")).SaveAsync(new DraftRequest(100, "existing-head", "opaque-old-data"));
            yield return Until(() => seed.IsCompleted);
            Assert.That(seed.Result, Is.EqualTo(DraftWriteStatus.Committed));
            Assert.That((bool)Call(inventory, "HasItem", "Key_A101"), Is.False, "A draft must never auto-restore.");
            Progress(); yield return Until(() => (int)Get(auto, "CommitCount") == 1);
            Assert.That((int)Get(auto, "CommitCount"), Is.EqualTo(1));
            Assert.That(Read().revision, Is.GreaterThan(100));
            Assert.That(Read().payload, Does.Contain("item:Key_A101"));
        }
        [UnityTest] public IEnumerator I12_ManualContinueRestoresOnlyManualSlotAndDoesNotAutosave()
        {
            Assert.That((bool)Call(save, "TrySave", true), Is.True, "Create a valid real manual checkpoint before progress.");
            byte[] manual = File.ReadAllBytes(Path.Combine(directory, "floor01-v1.json"));
            manualBytes[0] = manual;
            Progress(); yield return Until(() => (int)Get(auto, "CommitCount") == 1);
            Assert.That((int)Get(auto, "CommitCount"), Is.EqualTo(1));
            byte[] draft = File.ReadAllBytes((string)Get(auto, "DraftPath"));
            Assert.That((bool)Call(save, "TryContinue"), Is.True);
            Assert.That((bool)Call(inventory, "HasItem", "Key_A101"), Is.False, "Continue must use the manual slot, not the newer draft.");
            yield return Until(() => false, .8f);
            Assert.That((int)Get(auto, "CommitCount"), Is.EqualTo(1));
            Assert.That(File.ReadAllBytes((string)Get(auto, "DraftPath")), Is.EqualTo(draft));
            Assert.That(File.ReadAllBytes(Path.Combine(directory, "floor01-v1.json")), Is.EqualTo(manual));
            AssertManualBytes();
        }
    }
}
