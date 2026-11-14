using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HUSEscape.Persistence.Autosave;
using HUSEscape.Quests;
using UnityEngine;

namespace HUSEscape.Persistence
{
    /// <summary>
    /// Floor01-only, event-driven draft capture. Unity state/JSON capture stays on the main
    /// thread; only immutable payloads reach DraftStore. Manual slots and Continue are unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Floor01Autosave : MonoBehaviour
    {
        [SerializeField, Min(.05f)] private float debounceSeconds = .3f;
        [SerializeField, Min(.1f)] private float unsafeRetrySeconds = .5f;
        [SerializeField, Min(.5f)] private float errorRetrySeconds = 2f;
        [SerializeField] private string lastError = "";
        private Floor01SaveSystem source;
        private QuestManager quests;
        private DraftStore store;
        private CancellationTokenSource lifetime;
        private Task<DraftWriteStatus> write;
        private bool subscribed, pending;
        private long change, writingChange;
        private float nextAttempt;

        /// <summary>Separate draft file, never one of the manual save slots.</summary>
        public string DraftPath => store?.DraftPath;
        /// <summary>Successful commits observed by this live component only.</summary>
        public int CommitCount { get; private set; }
        /// <summary>Most recent persistence error; empty after an acknowledged success.</summary>
        public string LastError => lastError;
        /// <summary>True while new gameplay progress still needs a safe capture/commit.</summary>
        public bool HasPendingChanges => pending;
        /// <summary>Revision of the last successfully acknowledged draft in this component.</summary>
        public long LastCommittedRevision { get; private set; }
        private Task<DraftRequest> writingCapture;

        /// <summary>Binds the existing scene-local services explicitly; never loads a draft.</summary>
        public void Bind(Floor01SaveSystem saveSystem, QuestManager questManager)
        {
            Unsubscribe(); CancelWork();
            source = saveSystem; quests = questManager;
            if (source == null || quests == null) throw new ArgumentNullException("Floor01 autosave dependencies");
            store = new DraftStore(Path.Combine(Path.GetDirectoryName(source.SavePath), "floor01-draft-v1.json"));
            if (isActiveAndEnabled) Subscribe();
        }
        private void OnEnable() { if (source != null && quests != null) Subscribe(); }
        private void OnDisable() { Unsubscribe(); CancelWork(); }
        private void OnDestroy() { Unsubscribe(); CancelWork(); }
        private void Subscribe()
        {
            if (subscribed) return;
            lifetime = new CancellationTokenSource();
            quests.ProgressRecorded += Progress;
            subscribed = true;
        }
        private void Unsubscribe()
        {
            if (subscribed && quests != null) quests.ProgressRecorded -= Progress;
            subscribed = false;
        }
        private void CancelWork()
        {
            lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null;
            // Faults are also observed by the static continuation below after scene destruction.
            write = null; pending = false; change++;
        }
        private void Progress(string fact, int previous, int current)
        {
            if (!CanCapture()) return;
            bool floorFact = false;
            foreach (var definition in quests.Definitions)
                if (definition.CompletionEvent == fact) { floorFact = true; break; }
            if (!floorFact) return;
            pending = true; change++; nextAttempt = Time.unscaledTime + debounceSeconds;
        }
        private bool CanCapture() => source != null && source.isActiveAndEnabled && !source.IsRestoring &&
            source.gameObject.scene.name == "Floor01_Prototype" && quests != null && !quests.CampusActive && !quests.OfficeActive;

        private void Update()
        {
            if (write != null && write.IsCompleted)
            {
                var completed = write; write = null;
                if (completed.IsFaulted)
                {
                    lastError = completed.Exception.GetBaseException().Message;
                    nextAttempt = Time.unscaledTime + errorRetrySeconds;
                }
                else if (completed.IsCanceled) { nextAttempt = Time.unscaledTime + unsafeRetrySeconds; }
                else if (completed.Result == DraftWriteStatus.Committed || completed.Result == DraftWriteStatus.Duplicate)
                {
                    if (completed.Result == DraftWriteStatus.Committed) { CommitCount++; LastCommittedRevision = writingCapture.Result.Revision; }
                    lastError = "";
                    if (writingChange == change) pending = false;
                }
                else
                {
                    // Never acknowledge stale/conflicting payloads as saved. Recapture with a new identity.
                    lastError = "Draft not committed: " + completed.Result;
                    nextAttempt = Time.unscaledTime + errorRetrySeconds;
                }
            }
            if (!pending || write != null || !CanCapture() || Time.unscaledTime < nextAttempt) return;
            try
            {
                var capture = CaptureSnapshotAsync();
                writingCapture = capture;
                writingChange = change;
                write = WriteCapturedAsync(store, capture, lifetime.Token);
                ObserveFault(write);
            }
            catch (Exception)
            {
                // Capture is not safe yet (grounding, animation, IK, reward or snapshot validation).
                // No request is submitted. Keep the change pending, retry at a bounded frequency.
                nextAttempt = Time.unscaledTime + unsafeRetrySeconds;
            }
        }
        internal Task<DraftRequest> CaptureSnapshotAsync()
        {
            if (!CanCapture()) throw new InvalidOperationException("Floor01 is not ready for capture.");
            string payload = JsonUtility.ToJson(source.CaptureForAutosave());
            return PrepareAsync(store, payload);
        }
        private static async Task<DraftRequest> PrepareAsync(DraftStore target, string payload)
        {
            long revision = await target.ReserveRevisionAsync().ConfigureAwait(false);
            return new DraftRequest(revision, Guid.NewGuid().ToString("N"), payload);
        }
        internal Task<DraftWriteStatus> CommitCapturedAsync(DraftRequest request) => store.SaveAsync(request);
        private static async Task<DraftWriteStatus> WriteCapturedAsync(DraftStore target, Task<DraftRequest> capture, CancellationToken token)
        {
            DraftRequest request = await capture.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return await target.SaveAsync(request, token).ConfigureAwait(false);
        }
        private static void ObserveFault(Task task)
        {
            task.ContinueWith(t => { var observed = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        internal IDisposable SuspendForRestore()
        {
            CancelWork();
            if (subscribed) lifetime = new CancellationTokenSource();
            // Worker continuations use ConfigureAwait(false): draining cannot wait on Unity's thread.
            return store?.SuspendWrites();
        }
    }
}
