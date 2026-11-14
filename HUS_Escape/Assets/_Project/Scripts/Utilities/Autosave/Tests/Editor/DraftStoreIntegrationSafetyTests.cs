using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace HUSEscape.Persistence.Autosave.Tests
{
    /// <summary>Focused checks of new integration primitives using the real writer/filesystem.</summary>
    public sealed class DraftStoreIntegrationSafetyTests
    {
        string path;
        [SetUp] public void Setup()
        {
            var directory = Path.Combine(Environment.GetEnvironmentVariable("VT103_TEST_DATA_ROOT") ?? @"D:\Game3D-01\VT103_Work\Evidence\VT103\integration-data", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); path = Path.Combine(directory, "floor01-draft-v1.json");
            Assert.That(new DraftStore(path).SaveAsync(new DraftRequest(42, "seed", "original")).GetAwaiter().GetResult(), Is.EqualTo(DraftWriteStatus.Committed));
        }
        [Test] public void S01_ReservationsExceedDiskAndOtherInstancesWithoutWriting()
        {
            byte[] original = File.ReadAllBytes(path);
            var first = new DraftStore(path).ReserveRevisionAsync().GetAwaiter().GetResult();
            var second = new DraftStore(Path.Combine(Path.GetDirectoryName(path), ".", Path.GetFileName(path))).ReserveRevisionAsync().GetAwaiter().GetResult();
            Assert.That(first, Is.EqualTo(43)); Assert.That(second, Is.EqualTo(44));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
        }
        [Test] public void S02_CancelAfterRealStagingPreservesHeadAndCleansOwnPendingFile()
        {
            byte[] original = File.ReadAllBytes(path);
            using (var cancel = new CancellationTokenSource())
            {
                var hooks = new DraftIoHooks { BeforeCommit = _ => cancel.Cancel() };
                var operation = new DraftStore(path, hooks).SaveAsync(new DraftRequest(43, "canceled", "must-not-persist"), cancel.Token);
                Assert.Catch<OperationCanceledException>(() => operation.GetAwaiter().GetResult());
            }
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(path), "*.pending"), Is.Empty);
            Assert.That(new DraftStore(path).SaveAsync(new DraftRequest(43, "retry", "valid")).GetAwaiter().GetResult(), Is.EqualTo(DraftWriteStatus.Committed));
        }
        [Test] public void S03_RestoreFenceBlocksWriterAcrossInstancesUntilDisposed()
        {
            byte[] original = File.ReadAllBytes(path); Task<DraftWriteStatus> waiting;
            using (new DraftStore(path).SuspendWrites())
            {
                waiting = new DraftStore(path).SaveAsync(new DraftRequest(43, "wait-for-restore", "later"));
                Assert.That(waiting.IsCompleted, Is.False, "A commit cannot occur inside the restoration fence.");
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            }
            Assert.That(waiting.GetAwaiter().GetResult(), Is.EqualTo(DraftWriteStatus.Committed));
        }
    }
}
