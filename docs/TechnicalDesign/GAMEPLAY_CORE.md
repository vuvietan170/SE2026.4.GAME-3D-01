# Gameplay Core foundation (FO-002)

## Scope and architecture

Core lives in `HUS_Escape/Assets/_Project/Scripts/Core/`, under the
`HUSEscape.Core` namespace. It establishes application state and scene-loading
coordination only. It implements no player, AI, inventory, puzzle, input, UI,
time-scale, or audio behavior. The release platform remains TBD.

| Component | Responsibility |
| --- | --- |
| `GameState` | Defines `Initializing`, `Ready`, `Loading`, `Playing`, and `Paused`. |
| `GameManager` | Owns state, initialization, transition validation, and instance-based notifications. It is an ordinary C# object, with Unity logging for failed event subscribers. |
| `GameBootstrap` | Elects one Core owner, constructs and connects the manager and loader, persists its root, and completes initialization. |
| `SceneLoader` | Validates exact enabled build-scene paths, coordinates exclusive asynchronous Single-mode loads, verifies activation, and reports completion or failure. |
| `SceneLoadBackend` | Internal, instance-scoped boundary around Unity path lookup, native operation creation, and activation observation. Test doubles supply engine outcomes while production SceneLoader retains all coordination logic. |

Dependencies are one-way: bootstrap composes manager and loader; loader depends on
manager; manager depends on the state enum. There is no static instance accessor,
global event bus, dependency-injection container, or scene search in gameplay code.
Ownership discovery runs only during bootstrap initialization.

`HUSEscape.Core.asmdef` defines the runtime assembly. The nested Editor-only
`HUSEscape.Core.EditModeTests.asmdef` and separate
`HUSEscape.Core.PlayModeTests.asmdef` reference Core and Unity's existing test
assemblies. Both use the `HUSEscape.Core` namespace and are excluded from normal
player builds. Both receive `InternalsVisibleTo` access for the scoped backend
and reservation APIs. There is no mutable static backend, global owner registry,
or Singleton. No packages are added or changed.

## Initialization lifecycle

1. FO-003A authors `GameBootstrap` on the dedicated active BootstrapCore root in
   `Assets/_Project/Scenes/Bootstrap/Bootstrap.unity`. Its required `SceneLoader`
   is enabled on the same root. Do not put unrelated scene content under this
   persistent root. See [BOOTSTRAP_SCENE.md](BOOTSTRAP_SCENE.md) for the separate
   one-shot BootstrapSceneEntry coordinator and serialized destination.
2. `Awake` validates the root and elects ownership. An already initialized bootstrap
   wins, even if temporarily inactive. Otherwise, the eligible active bootstrap
   with the lowest Unity entity ID wins, independent of `Awake` ordering.
   Entity IDs are runtime identifiers, not stable identifiers across sessions.
3. Rejected bootstrap and loader components are disabled, with an error explaining
   why. Their GameObjects and authored assets are not deleted. Re-enabling a
   rejected bootstrap does not bypass the ownership check. Inactive, uninitialized
   bootstraps participate when they are first activated.
4. The owner constructs `GameManager`, calls `SceneLoader.Initialize(manager)`, and
   marks its root `DontDestroyOnLoad`. The loader has no independent initialization
   in `Awake`, so component callback ordering cannot race manager creation.
5. The bootstrap calls `GameManager.Initialize()`. The manager commits
   `Initializing -> Ready` and notifies listeners once. Repeat initialization
   returns false without changing state or publishing an event.

An initially disabled bootstrap defers initialization until its first `OnEnable`.
An initialized owner retains its claim and manager state when its component or
GameObject is disabled. Its loader is disabled too, guarded service access returns
false, and later bootstraps are rejected. Re-enabling the owner enables its loader
and exposes the same services without initialization or another Ready event.
Rejected duplicates stay rejected when re-enabled. Destroying the owner ends its
claim; previously rejected duplicates do not automatically take over.

Consumers should obtain the owner through an explicit scene/serialized reference
and call `TryGetServices(out manager, out loader)` in `Start` or a later explicit
integration step. False means both outputs are null: initialization is incomplete,
the owner/loader is inactive, or this reference identifies a rejected duplicate.
The loader must also be bound to the same manager. Existing `Manager`, `Loader`,
and `IsInitialized` properties are preserved for compatibility; they do not grant
permission to use inactive services. A reference to a rejected duplicate never
silently resolves to a different owner. `Manager` is null before initialization
or after rejection.
Initialization does not load a scene or claim a MainMenu scene exists. This
FO-002 feature created or modified no scenes, prefabs, or build settings.
FO-003A supplies the Bootstrap entry scene and places it first in Build Settings;
opening SampleScene directly still does not automatically initialize Core.

## State transitions

The complete state transition matrix is below. A dash means rejection, including
self-transitions and transitions back to `Initializing`.

| From / To | Initializing | Ready | Loading | Playing | Paused |
| --- | --- | --- | --- | --- | --- |
| Initializing | - | Initialize only | - | - | - |
| Ready | - | - | Yes | Yes | - |
| Loading | - | Yes | - | Yes | Yes |
| Playing | - | - | Yes | - | Yes |
| Paused | - | - | Yes | Yes | - |

`CanTransitionTo` checks the current rules. `TryChangeState` returns false for an
invalid request and leaves state and events unchanged. Undefined enum values are
rejected. Before initialization all ordinary state-change requests are rejected.

`StateChanged(previous, current)` runs synchronously after state is committed.
Reentrant transitions during notification are rejected, preventing nested events
from producing contradictory state observations. Each subscriber is isolated:
exceptions are logged and remaining subscribers still run. Subscribers should
schedule follow-up transitions for a later frame rather than perform them inside
the event callback. All Core APIs are intended for Unity's main thread.

State-only transitions do not load scenes or pause simulation. Future modules
implement the consequences of `Playing` and `Paused`. For an actual scene change,
always use `SceneLoader` rather than setting `Loading` directly. The loader uses
an internal reservation that blocks all public transitions until completion.
Each reservation is a distinct object token capturing the previous stable state.
Completion and recovery require identity equality with the active token; null,
foreign-manager, consumed, or stale tokens cannot change state or release a newer
reservation. Only the owning request can release that reservation. Successful loads end in `Ready` or
`Playing`; `Loading -> Paused` supports restoration after a failed load begun
while paused.

## Scene loading flow and failures

After successful guarded service retrieval, call
`loader.TryLoadScene(scenePath, loadedState, out error)`, passing an
exact path such as `Assets/Scenes/SampleScene.unity`. The path must match an
enabled scene in the active build scene list, including case and `.unity`
extension. Bare names, disabled/unlisted scenes, empty paths, and unsupported
destination states are rejected without beginning a load or changing state.
The destination must be `Ready` or `Playing`.

1. Validate initialization, component availability, scene path, destination, and
   transition eligibility. Reject overlapping requests rather than queue them.
2. Capture the prior state in the manager's reservation token before publishing `Loading`.
   Requests submitted reentrantly during state notification are rejected too.
3. Start `SceneManager.LoadSceneAsync` in Single mode. Keep scene activation
   enabled. Use the operation's completion callback rather than a polling
   coroutine, so disabling a component does not discard the completion observer.
4. On completion, verify that the active scene is valid, loaded, and has the
   requested path. Commit the destination on success, or restore the previous
   logical state using the matching token on an observable failure. If the
   destination commitment is rejected, use the same token to recover the captured
   previous state. Do not discard the request while recovery is deferred.
   Completion during another manager notification is retried by `Update`; the
   loader stays busy until state finalization is safe. Replayed callbacks carry
   their original request identity and cannot finish a newer request.
5. Publish `SceneLoaded(path)` or `SceneLoadFailed(path, error)` after finalizing
   state. Subscriber exceptions are logged independently. Completion listeners may
   submit a new load. Rejections return false and populate `error`/`LastError`;
   they do not publish `SceneLoadFailed`. Startup failures return false, restore
   state, log the failure, and publish `SceneLoadFailed`. A true return value means
   the operation started, not that it succeeded.

`Progress` exposes Unity's raw progress while pending and returns zero afterward.
`LastError` records the latest rejection or failure and is cleared when a new
request is accepted or a load completes successfully.

`IsLoading` is true while either a local request or any reservation on its bound
manager remains pending. A stale local result never releases another request's
token, and the loader cannot report idle while that manager is reserved. State
notifications run before the request is discarded; completion event listeners
can therefore submit another request only after finalization.

### Unity limitations and ownership contract

- Unity's `AsyncOperation` has no general scene-load cancellation API. Do not
  promise cancellation or implement a timeout that unlocks state while the native
  operation is still running. This foundation deliberately keeps a pending load
  reserved until Unity reports completion.
- Holding `allowSceneActivation` false can stop progress at 0.9 and stall the
  asynchronous operation queue. Core leaves activation enabled and does not
  provide deferred activation or cancellation controls.
- Unity exposes no general error result on `AsyncOperation`. Core catches startup
  exceptions/null operations and checks the active scene at completion; it cannot
  detect or repair every engine-level failure, scene script exception, or hang.
- Single-mode loading unloads the previous scene. Restoring the prior logical
  state after failure does not recreate an unloaded scene or undo native side
  effects. Recovery UI and fallback-scene policies require a later feature.
- The persistent bootstrap root owns loader lifetime. Do not destroy its root,
  remove its loader, or create independent native scene-loading code while a load
  is pending. The completion callback is managed independently of coroutine
  lifetime, but it cannot make destroying Core a supported recovery strategy.
- Other code must not call Unity scene-loading APIs concurrently. Core serializes
  its own requests; it does not globally intercept unrelated engine calls.
- Disabling an owner does not cancel its native operation. Managed completion
  observation survives disabled components; any finalization specifically
  deferred during state notification waits for an enabled loader's `Update`.

References: Unity's [LoadSceneAsync API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html)
and [allowSceneActivation behavior](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AsyncOperation-allowSceneActivation.html).

## Future integration

- Pass manager/loader references explicitly to modules. Subscribe to instance
  events when enabled and unsubscribe when disabled or disposed.
- Keep feature code in its existing responsibility folder. Core must not depend
  on Player, AI, Inventory, Puzzle, or UI assemblies.
- Handle false transition/load results. Keep state event handlers short and do
  not depend on subscriber execution order.
- Add states only with an updated transition matrix, documentation, and tests.
  Add scene-specific recovery, pause consequences, and menu flows separately.
- FO-003A supplies Bootstrap scene wiring and validates startup to the existing
  SampleScene. Future MainMenu authoring can replace that development destination
  with a menu scene targeting Ready. Those scene flows remain outside FO-002.

## Validation and tests

| Suite | Responsibility |
| --- | --- |
| Edit Mode `GameManagerTests` | Initialization, the full transition matrix, event snapshot/reentrancy/exception behavior, token ownership, foreign/stale token rejection, and previous-state recovery. |
| Edit Mode `GameBootstrapTests` | Guarded access before initialization and the actual ownership selector with reversed candidate ordering and ineligible candidates. |
| Edit Mode `SceneLoaderTests` | Production loader validation, overlap/reentrancy, startup exceptions/null operations, activation failure, success/failure events, replayed callbacks, replacement reservations, deferred finalization, recovery after rejected completion, and independent backends. |
| Play Mode `GameBootstrapPlayModeTests` | Actual Awake/OnEnable/OnDisable initialization, bound service readiness, duplicate rejection, initially disabled owners, ownership retained while disabled/inactive, and DontDestroyOnLoad persistence after the source scene unloads. |
| Play Mode `SceneLoaderPlayModeTests` | Native asynchronous SampleScene loading, explicit activation, overlap rejection, completion while the loader is disabled, final state/events, and preservation of Test Runner scenes. |

Edit Mode loader tests instantiate the production component. Their fake backend
models engine results only, without implementing a separate loader or state
machine. Two narrowly scoped reflection injections exercise the otherwise
unreachable invalid-destination and notification-deferral safety branches.

Play Mode native loading uses a per-instance `UnitySceneLoadBackend` configured
internally for Additive mode. A test-owned `sceneLoaded` handler activates only
the newly loaded SampleScene. The production default remains Single mode. The
test records all original scene handles, verifies that they remain loaded,
restores the previous active scene, and unloads only its newly created scene.
It never relocates or destroys Test Runner roots. Teardown waits for any pending
native load before removing its observer, because cancellation is unsupported.
An engine hang can therefore also block teardown; that is not reported as a pass.
Bootstrap persistence tests create only temporary runtime scenes, not scene assets.

### Remediation verification results

The remediation was compiled and executed using Unity 6000.6.5f1 on an isolated
copy of Assets, Packages, and ProjectSettings. The working project's configuration
and assets were not opened or changed by the validation runs.

| Validation | Passed | Failed | Blocked / skipped |
| --- | --- | --- | --- |
| Unity Edit Mode | 81 | 0 | 0 |
| Unity Play Mode | 8 | 0 | 0 |
| Standalone NUnit manager subset (also covered above) | 45 | 0 | 0 executed cases; one Unity-log case deliberately excluded |

Both Unity runs completed with exit code 0 and produced NUnit XML results. Unity
imported and compiled Core and both test assemblies with no C# compiler errors
or warnings in the validation logs. Separate Roslyn compilation against the
installed 6000.6.5f1, NUnit, and Test Runner references also passed with warnings
treated as errors and public runtime XML documentation enabled.

The initial sandboxed Unity attempt timed out connecting to the Licensing Client.
The same temporary-copy tests subsequently ran successfully outside the sandbox;
there are no remaining licensing-blocked cases in these final results. The
standalone results are supplementary and are not presented as Unity execution.

Native SampleScene integration is intentionally Additive to preserve the Test
Runner environment. Default production Single-mode unloading is not exercised by
that test; bootstrap persistence is separately verified by unloading a temporary
runtime source scene. These tests do not validate rendering, gameplay, or future
Bootstrap/MainMenu scene wiring.

Run them in Unity's Test Runner under EditMode, or from PowerShell with an
activated Unity 6000.6.5f1 editor (adjust output paths as needed):

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe' `
  -batchmode -nographics -projectPath 'D:/Game3D-01/SE2026.4.GAME-3D-01/HUS_Escape' `
  -runTests -testPlatform EditMode -assemblyNames HUSEscape.Core.EditModeTests `
  -testResults "$env:TEMP/fo-002-editmode-results.xml" `
  -logFile "$env:TEMP/fo-002-editmode.log"
```

Do not add `-quit` to this test command: the test runner exits after execution.
For Play Mode, use `-testPlatform PlayMode`,
`-assemblyNames HUSEscape.Core.PlayModeTests`, and separate log/result paths.
Report actual Unity compilation and test execution separately from source-level
or standalone C# validation. These tests do not replace Play Mode validation of
bootstrap ownership, native scene loading, and scene activation.
