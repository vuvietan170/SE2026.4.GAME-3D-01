using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("VT103.Autosave.EditModeTests")]

namespace HUSEscape.Persistence.Autosave
{
    /// <summary>Instance-scoped test seam; default production writer has no hooks.</summary>
    internal sealed class DraftIoHooks
    {
        // Scheduling observation only. Tests use task readiness, never elapsed sleeps,
        // to decide whether a competing request can run while the older commit is paused.
        internal Action<DraftRequest, Task> AdmissionObserved;
        // Called after real staging+flush and before the real filesystem replace/move.
        // Throwing here models a pre-commit I/O failure and exercises real cleanup.
        internal Action<DraftRequest> BeforeCommit;
    }
}