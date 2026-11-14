using System;

namespace CampusEscape.SaveSystem {
    /// <summary>Immutable business configuration supplied by the composition root.</summary>
    public sealed class SaveSlotPolicy {
        public int RestoreRetentionHours { get; }

        public SaveSlotPolicy() : this(24) { }

        public SaveSlotPolicy(int restoreRetentionHours) {
            if (restoreRetentionHours < 1 || restoreRetentionHours > 720) {
                throw new ArgumentOutOfRangeException(nameof(restoreRetentionHours),
                    "Restore retention must be between 1 and 720 hours.");
            }
            RestoreRetentionHours = restoreRetentionHours;
        }
    }
}
