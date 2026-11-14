using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace HUSEscape.Persistence.Autosave.Tests
{
    [TestFixture, Category("VT103")]
    public sealed class DraftStoreRedTests
    {
        private string directory, path;
        private DraftStore store;

        [SetUp]
        public void SetUp()
        {
            string root = Environment.GetEnvironmentVariable("VT103_TEST_DATA_ROOT");
            if (string.IsNullOrWhiteSpace(root))
                root = @"D:\Game3D-01\VT103_Work\Evidence\VT103\test-data";
            Assert.That(Path.GetPathRoot(Path.GetFullPath(root)), Is.EqualTo(@"D:\").IgnoreCase,
                "Test files must remain on D; never use real persistentDataPath slots.");
            directory = Path.Combine(root, TestContext.CurrentContext.Test.Name + "_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "floor01-draft-v1.json");
            store = new DraftStore(path);
            TestContext.Progress.WriteLine("Fixture: " + path);
        }

        // Independent known-format fixture, not a second save/store implementation.
        private void Seed20()
        {
            File.WriteAllText(path,
                "{\"formatVersion\":1,\"revision\":20,\"requestId\":\"capture-20\",\"payload\":\"floor-state-20\"}",
                new UTF8Encoding(false));
        }

        private DraftDocument ReadDisk() => JsonUtility.FromJson<DraftDocument>(File.ReadAllText(path));

        [Test]
        public void VT103_T01_NewerRevisionPersistsItsOwnPayload()
        {
            Seed20();
            DraftWriteStatus result = store.SaveAsync(new DraftRequest(21, "capture-21", "floor-state-21")).GetAwaiter().GetResult();
            DraftDocument actual = ReadDisk();
            Assert.That(actual.revision, Is.EqualTo(21), "AC1: newer snapshot must actually reach the draft file.");
            Assert.That(actual.payload, Is.EqualTo("floor-state-21"), "Revision and payload must be committed together.");
            Assert.That(result, Is.EqualTo(DraftWriteStatus.Committed));
        }

        [Test]
        public void VT103_T02_LateOlderRequestIsExplicitlyRejectedWithoutOverwrite()
        {
            Seed20();
            byte[] original = File.ReadAllBytes(path);
            DraftWriteStatus result = store.SaveAsync(new DraftRequest(19, "capture-19", "floor-state-19")).GetAwaiter().GetResult();
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original), "AC2: stale write must not change durable bytes.");
            Assert.That(result, Is.EqualTo(DraftWriteStatus.RejectedStale), "AC2: a no-op without stale classification is not a completed implementation.");
        }

        [Test]
        public void VT103_T03_RetrySameIdentityAcknowledgesDuplicateWithoutAnotherCommit()
        {
            Seed20();
            byte[] original = File.ReadAllBytes(path);
            string[] files = Directory.GetFiles(directory);
            var request = new DraftRequest(20, "capture-20", "floor-state-20");
            // Observe the required acknowledgment without an OS lock first: a mutant
            // must fail an assertion, not be counted as killed by a sharing exception.
            Assert.That(store.SaveAsync(request).GetAwaiter().GetResult(), Is.EqualTo(DraftWriteStatus.Duplicate),
                "AC3: an unlocked retry of a committed identity must be Duplicate, never a second Committed write.");
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            // A real Windows read-only sharing lease permits reading but denies rewriting/replacing
            // the committed file. A duplicate must be acknowledged without needing write access.
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var first = store.SaveAsync(request).GetAwaiter().GetResult();
                var retry = store.SaveAsync(request).GetAwaiter().GetResult();
                Assert.That(new[] { first, retry }, Is.All.EqualTo(DraftWriteStatus.Duplicate),
                    "AC3: both retries refer to the already committed capture-20.");
            }
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(directory), Is.EquivalentTo(files), "No staging artifact from a duplicate.");
            // The committed head can advance, but an observed ID must not be reused
            // for different content under a higher revision in this same process.
            Assert.That(store.SaveAsync(new DraftRequest(21, "capture-21", "floor-state-21")).GetAwaiter().GetResult(),
                Is.EqualTo(DraftWriteStatus.Committed));
            byte[] advanced = File.ReadAllBytes(path);
            Assert.That(store.SaveAsync(new DraftRequest(22, "capture-20", "changed-content")).GetAwaiter().GetResult(),
                Is.EqualTo(DraftWriteStatus.Conflict), "Reusing an observed historical ID must conflict even above the current revision.");
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(advanced));
            Assert.That(store.SaveAsync(request).GetAwaiter().GetResult(), Is.EqualTo(DraftWriteStatus.RejectedStale),
                "A genuinely older retry remains Stale after the head advances.");
        }

        [Test, Timeout(15000)]
        public void VT103_T04_TwoConcurrentRequestsLeaveMaximumRevisionOnDisk()
        {
            Seed20();
            using (var ready = new CountdownEvent(2))
            using (var start = new ManualResetEventSlim(false))
            {
                // Different store instances and path spellings must still synchronize on one file.
                var secondStore = new DraftStore(Path.Combine(directory, ".", "FLOOR01-DRAFT-V1.JSON"));
                Func<DraftStore, DraftRequest, Task<DraftWriteStatus>> submit = (writer, request) => Task.Run(async () =>
                {
                    ready.Signal();
                    if (!start.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Start barrier was not released.");
                    return await writer.SaveAsync(request).ConfigureAwait(false);
                });
                var older = submit(store, new DraftRequest(21, "capture-21", "floor-state-21"));
                var newer = submit(secondStore, new DraftRequest(22, "capture-22", "floor-state-22"));
                bool bothReady = ready.Wait(TimeSpan.FromSeconds(5));
                start.Set();
                bool completed = Task.WaitAll(new Task[] { older, newer }, TimeSpan.FromSeconds(5));
                Assert.That(bothReady && completed, Is.True, "Harness must complete; timeout is not behavioral RED.");
                DraftDocument actual = ReadDisk();
                Assert.That(actual.revision, Is.EqualTo(22), "AC4: after both valid requests finish, durable revision must be max(20,21,22).");
                Assert.That(actual.payload, Is.EqualTo("floor-state-22"));
                Assert.That(newer.Result, Is.EqualTo(DraftWriteStatus.Committed));
                Assert.That(older.Result, Is.EqualTo(DraftWriteStatus.Committed).Or.EqualTo(DraftWriteStatus.RejectedStale));
            }
        }
        [Test]
        public void VT103_T05_SameIdentityDifferentPayloadIsConflict()
        {
            Seed20(); byte[] before = File.ReadAllBytes(path);
            Assert.That(store.SaveAsync(new DraftRequest(20, "capture-20", "different-payload")).GetAwaiter().GetResult(), Is.EqualTo(DraftWriteStatus.Conflict));
            Assert.That(store.SaveAsync(new DraftRequest(21, "capture-20", "different-payload")).GetAwaiter().GetResult(), Is.EqualTo(DraftWriteStatus.Conflict));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before), "Conflicts must preserve committed bytes.");
        }

        [Test]
        public void VT103_T06_SameRevisionDifferentIdentityIsConflict()
        {
            Seed20(); byte[] before = File.ReadAllBytes(path);
            Assert.That(store.SaveAsync(new DraftRequest(20, "other-id", "other-payload")).GetAwaiter().GetResult(), Is.EqualTo(DraftWriteStatus.Conflict));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
        }

        [Test]
        public void VT103_T07_InjectedCommitFailurePreservesHeadAndRetry()
        {
            Seed20(); byte[] before = File.ReadAllBytes(path); string[] files = Directory.GetFiles(directory);
            bool staged = false;
            var failing = new DraftStore(path, new DraftIoHooks {
                BeforeCommit = request => {
                    staged = Directory.GetFiles(directory, "*.pending").Length == 1;
                    throw new IOException("VT103 controlled pre-replace persistence failure");
                }
            });
            DraftWriteStatus? acknowledgment = null; Exception error = null;
            try { acknowledgment = failing.SaveAsync(new DraftRequest(21, "capture-21", "floor-state-21")).GetAwaiter().GetResult(); }
            catch (Exception e) { error = e; }
            Assert.That(staged, Is.True, "The real staging file must exist before the injected failure.");
            Assert.That(error, Is.InstanceOf<IOException>());
            Assert.That(acknowledgment, Is.Null, "A persistence failure must never acknowledge Committed.");
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            Assert.That(Directory.GetFiles(directory), Is.EquivalentTo(files), "Failed staging must not affect the next read.");
            Assert.That(store.SaveAsync(new DraftRequest(21, "capture-21", "floor-state-21")).GetAwaiter().GetResult(), Is.EqualTo(DraftWriteStatus.Committed));
            Assert.That(ReadDisk().revision, Is.EqualTo(21));
        }

        [Test, Timeout(20000)]
        public void VT103_T08_ControlledInterleavingCannotRegressRevision()
        {
            Seed20();
            using (var olderAtCommit = new ManualResetEventSlim(false))
            using (var releaseOlder = new ManualResetEventSlim(false))
            {
                Task newerAdmission = null;
                var olderStore = new DraftStore(path, new DraftIoHooks {
                    BeforeCommit = request => {
                        olderAtCommit.Set();
                        if (!releaseOlder.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Controlled scheduling seam timed out.");
                    }
                });
                var newerStore = new DraftStore(Path.Combine(directory, ".", "FLOOR01-DRAFT-V1.JSON"), new DraftIoHooks {
                    AdmissionObserved = (request, admission) => newerAdmission = admission
                });
                Task<DraftWriteStatus> older = null, newer = null;
                try
                {
                    older = olderStore.SaveAsync(new DraftRequest(21, "capture-21", "floor-state-21"));
                    if (!olderAtCommit.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Older write did not reach staging seam.");
                    newer = newerStore.SaveAsync(new DraftRequest(22, "capture-22", "floor-state-22"));
                    if (newerAdmission == null) throw new InvalidOperationException("Admission observer did not execute.");
                    // This branch controls scheduling, NOT the oracle. With the real gate,
                    // the new request is blocked; without it, drain the new commit first.
                    // The expected disk revision below is always 22 for both schedules.
                    if (newerAdmission.IsCompleted)
                    {
                        if (!newer.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Newer admitted write did not finish.");
                        File.AppendAllText(Path.Combine(directory, "schedule.txt"), "Newer completed: " + newer.Result + "\nDurable before old release: " + ReadDisk().revision + "\n");
                    }
                    else File.AppendAllText(Path.Combine(directory, "schedule.txt"), "Newer admission pending: release old before draining both.\n");
                }
                finally
                {
                    releaseOlder.Set();
                    Task[] tasks = newer != null ? new Task[] { older, newer } : older != null ? new Task[] { older } : new Task[0];
                    if (!Task.WaitAll(tasks, TimeSpan.FromSeconds(5))) throw new TimeoutException("Controlled requests did not drain.");
                }
                DraftDocument final = ReadDisk();
                File.AppendAllText(Path.Combine(directory, "schedule.txt"), "Durable after both writes: " + final.revision + "\n");
                Assert.That(final.revision, Is.EqualTo(22), "AC4 invariant: an older prepared write must never replace durable revision 22.");
                Assert.That(final.payload, Is.EqualTo("floor-state-22"));
                Assert.That(newer.Result, Is.EqualTo(DraftWriteStatus.Committed));
            }
        }
    }
}