using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HUSEscape.Persistence.Autosave
{
    /// <summary>
    /// Floor01 draft writer. All instances using the same normalized Windows path share
    /// a process-local gate. No cross-process locking. Gameplay snapshots remain opaque payloads.
    /// </summary>
    public sealed class DraftStore
    {
        // Retained for the process lifetime: removing a gate while another instance holds
        // it could give a third instance a different gate for the very same file.
        private readonly struct Identity
        {
            public readonly long Revision;
            public readonly string Fingerprint;
            public Identity(long revision, string fingerprint) { Revision = revision; Fingerprint = fingerprint; }
        }
        private sealed class PathState
        {
            public readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
            public long AllocatedRevision;
            public readonly Dictionary<string, Identity> CommittedIds = new Dictionary<string, Identity>(StringComparer.Ordinal);
        }
        private static readonly ConcurrentDictionary<string, PathState> Gates =
            new ConcurrentDictionary<string, PathState>(StringComparer.OrdinalIgnoreCase);
        private readonly PathState state;
        private readonly DraftIoHooks hooks;
        public string DraftPath { get; }

        public DraftStore(string draftPath) : this(draftPath, null) { }

        internal DraftStore(string draftPath, DraftIoHooks hooks)
        {
            this.hooks = hooks;
            DraftPath = Path.GetFullPath(draftPath).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            string name = Path.GetFileName(DraftPath);
            if (name.Equals("floor01-v1.json", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("campus-v1.json", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("trainingoffice-v1.json", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("DraftStore cannot target a manual save slot.", nameof(draftPath));
            state = Gates.GetOrAdd(DraftPath, _ => new PathState());
        }

        /// <summary>
        /// Commits only newer captures. Duplicate/stale/conflict results never write.
        /// Invalid input, corrupt files and I/O failures fault the task, never return Committed.
        /// </summary>
        public Task<DraftWriteStatus> SaveAsync(DraftRequest request) => SaveAsync(request, CancellationToken.None);

        /// <summary>Cancellable before commit; cancellation never interrupts an atomic replacement already in progress.</summary>
        public async Task<DraftWriteStatus> SaveAsync(DraftRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Revision <= 0 || string.IsNullOrWhiteSpace(request.RequestId) || request.Payload == null)
                throw new ArgumentException("A draft requires a positive revision, request ID and payload.", nameof(request));
            Task admission = state.Gate.WaitAsync();
            try { hooks?.AdmissionObserved?.Invoke(request, admission); }
            catch
            {
                // Even a broken observer must not strand a semaphore reservation.
                await admission.ConfigureAwait(false); state.Gate.Release(); throw;
            }
            await admission.ConfigureAwait(false);
            try
            {
                // Pure data and BCL file I/O only. No Unity API on the worker.
                return await Task.Run(() => SaveUnderGate(request, cancellationToken)).ConfigureAwait(false);
            }
            finally { state.Gate.Release(); }
        }

        /// <summary>
        /// Reserves a capture revision above both the validated disk head and all revisions
        /// reserved in this process for this canonical path. Gaps are valid after cancellation.
        /// A corrupt head faults rather than being silently replaced.
        /// </summary>
        public async Task<long> ReserveRevisionAsync()
        {
            await state.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                return await Task.Run(() => {
                    var head = ReadHead();
                    state.AllocatedRevision = checked(Math.Max(state.AllocatedRevision, head?.revision ?? 0) + 1);
                    return state.AllocatedRevision;
                }).ConfigureAwait(false);
            }
            finally { state.Gate.Release(); }
        }

        /// <summary>
        /// Drains the current commit, then fences writes while synchronous gameplay restoration
        /// runs. Dispose on the acquiring caller. Never acquire recursively or inside an I/O hook.
        /// This may briefly block the caller on file I/O; it does not cancel an atomic replace.
        /// </summary>
        public IDisposable SuspendWrites()
        {
            state.Gate.Wait();
            return new WriteLease(state.Gate);
        }
        private sealed class WriteLease : IDisposable
        {
            private SemaphoreSlim gate;
            public WriteLease(SemaphoreSlim gate) { this.gate = gate; }
            public void Dispose() { Interlocked.Exchange(ref gate, null)?.Release(); }
        }

        private DraftWriteStatus SaveUnderGate(DraftRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DraftDocument head = ReadHead();
            string fingerprint = Fingerprint(request.Payload);
            if (head != null)
            {
                // Old retries are stale, even when they reuse an identity from older history.
                if (request.Revision < head.revision) return DraftWriteStatus.RejectedStale;
                state.CommittedIds[head.requestId] = new Identity(head.revision, Fingerprint(head.payload));
            }
            // Remember observed successful identities across head advances and store instances
            // in this process. Faulted/uncommitted requests never reserve an identity.
            if (state.CommittedIds.TryGetValue(request.RequestId, out var identity) &&
                (identity.Revision != request.Revision || !string.Equals(identity.Fingerprint, fingerprint, StringComparison.Ordinal)))
                return DraftWriteStatus.Conflict;
            if (head != null)
            {
                if (string.Equals(request.RequestId, head.requestId, StringComparison.Ordinal))
                {
                    return request.Revision == head.revision &&
                        string.Equals(fingerprint, Fingerprint(head.payload), StringComparison.Ordinal) &&
                        string.Equals(request.Payload, head.payload, StringComparison.Ordinal)
                        ? DraftWriteStatus.Duplicate : DraftWriteStatus.Conflict;
                }
                if (request.Revision == head.revision) return DraftWriteStatus.Conflict;
            }

            var document = new DraftDocument {
                revision = request.Revision, requestId = request.RequestId,
                payload = request.Payload, payloadFingerprint = fingerprint
            };
            string directory = Path.GetDirectoryName(DraftPath);
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, Path.GetFileName(DraftPath) + "." + Guid.NewGuid().ToString("N") + ".pending");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(DraftDocument)).WriteObject(stream, document);
                    stream.Flush(true);
                }
                // Same-directory staging keeps replacement on the same volume. There is no
                // delete-then-move fallback, which would expose a missing/partial principal file.
                hooks?.BeforeCommit?.Invoke(request);
                cancellationToken.ThrowIfCancellationRequested();
                if (head == null) File.Move(temporary, DraftPath);
                else File.Replace(temporary, DraftPath, null);
                state.CommittedIds[request.RequestId] = new Identity(request.Revision, fingerprint);
                return DraftWriteStatus.Committed;
            }
            finally
            {
                // This uniquely owned scratch file is the only file cleanup may remove.
                // Preserve the original failure if cleanup itself is denied.
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private DraftDocument ReadHead()
        {
            if (!File.Exists(DraftPath)) return null;
            DraftDocument document;
            using (var stream = new FileStream(DraftPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                document = new DataContractJsonSerializer(typeof(DraftDocument)).ReadObject(stream) as DraftDocument;
            if (document == null || document.formatVersion != 1 || document.revision <= 0 ||
                string.IsNullOrWhiteSpace(document.requestId) || document.payload == null ||
                (document.payloadFingerprint != null &&
                 !string.Equals(document.payloadFingerprint, Fingerprint(document.payload), StringComparison.Ordinal)))
                throw new InvalidDataException("Invalid draft metadata or payload fingerprint; existing file preserved.");
            return document;
        }

        private static string Fingerprint(string payload)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-", "").ToLowerInvariant();
        }
    }
}