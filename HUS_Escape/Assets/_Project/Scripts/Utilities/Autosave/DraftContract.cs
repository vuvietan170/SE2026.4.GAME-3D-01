using System;
using System.Runtime.Serialization;

namespace HUSEscape.Persistence.Autosave
{
    // Separate draft format; never changes floor01-v1.json or the journey slots.
    [Serializable, DataContract]
    public sealed class DraftDocument
    {
        [DataMember] public int formatVersion = 1;
        [DataMember] public long revision;
        [DataMember] public string requestId;
        [DataMember] public string payload;
        // Optional for the pre-patch V1 fixtures. When present it must match the payload.
        [DataMember(EmitDefaultValue = false)] public string payloadFingerprint;
    }

    /// <summary>Immutable capture-time identity. Revision is not allocated at write completion.</summary>
    public sealed class DraftRequest
    {
        public long Revision { get; }
        public string RequestId { get; }
        public string Payload { get; }

        public DraftRequest(long revision, string requestId, string payload)
        {
            Revision = revision;
            RequestId = requestId;
            Payload = payload;
        }
    }

    public enum DraftWriteStatus
    {
        Committed,
        RejectedStale,
        Duplicate,
        Conflict
    }
}