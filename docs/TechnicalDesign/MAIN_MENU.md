# Main Menu foundation (FO-003B)

## Scope and scene flow

Unity 6000.6.5f1 / URP 17.6.0. This feature adds a Start-only menu with mouse and
keyboard interaction, loading feedback, and retry feedback after a rejected or
failed load. It adds no gameplay, settings screen, quit action, return-menu UI,
packages, Input Actions, or Core implementation changes. Release platform: TBD.

Enabled Build Settings order:

| Index | Scene | Destination state |
| --- | --- | --- |
| 0 | `Assets/_Project/Scenes/Bootstrap/Bootstrap.unity` | Startup source |
| 1 | `Assets/_Project/Scenes/MainMenu/MainMenu.unity` | `Ready` |
| 2 | `Assets/Scenes/SampleScene.unity` | `Playing` |

```text
Bootstrap -> Loading -> MainMenu / Ready
MainMenu -> Start -> Loading -> SampleScene / Playing
             BootstrapCore and its services persist
```

Open Bootstrap before entering Play Mode. Opening MainMenu directly does not
create Core or search for an owner; Start remains disabled without explicit
binding. SampleScene keeps its original asset, path, and GUID.

## Assembly and ownership

`Scripts/UI/HUSEscape.UI.asmdef` owns the `HUSEscape.UI` namespace and references
`HUSEscape.Core`, `UnityEngine.UI`, and `Unity.InputSystem`, all already available
in the project. Dependency direction is `UI -> Core`; Core never references UI.
There is no Singleton, service locator, global event bus, or mutable backend.

| Component | Responsibility and lifetime |
| --- | --- |
| `GameBootstrap` | Existing FO-002 owner. Its root, manager, and loader persist via the existing `DontDestroyOnLoad` contract. |
| `BootstrapSceneEntry` | Existing one-shot entry, configured in the scene to load MainMenu and finish in Ready. |
| `MainMenuFlow` | Lives on the persistent BootstrapCore root with an explicit serialized reference to the same root's GameBootstrap. Retrieves services through `TryGetServices`, binds one view in the completed active scene, and submits Start through the existing loader. |
| `MainMenuView` | Lives on MainMenuRoot in MainMenu. Owns serialized Button, status Text, and EventSystem references. Emits user intent without accessing Core or loading scenes. |

BootstrapCore contains Transform, GameBootstrap, SceneLoader,
BootstrapSceneEntry, and MainMenuFlow. GameManager and SceneLoadBackend remain
ordinary C# objects. No additional Core owner is placed in MainMenu or SampleScene.

## Initialization and cleanup

1. GameBootstrap initializes Core in Awake. All scene Awake callbacks complete
   before Start; no execution-order override is introduced.
2. MainMenuFlow.Start retrieves the explicitly assigned owner's manager and
   loader, then subscribes to their instance events. BootstrapSceneEntry.Start
   requests MainMenu. Flow works regardless of the relative order of these two
   Start callbacks: native completion occurs later, and Flow can also bind an
   already active menu during initialization.
3. On SceneLoader.SceneLoaded, Flow inspects roots of that completed active scene
   only. Exactly one MainMenuView is required. Zero means a normal content scene;
   multiple views fail safely with a diagnostic. No global view or service search
   is used.
4. View begins unbound with Start disabled. Flow explicitly binds it and enables
   Start only while guarded services are available, Core is Ready, and neither a
   menu request nor a loader operation is pending. Start gains keyboard selection.
5. Repeated binding of the same view is idempotent. View destruction notifies its
   owner; Flow clears scene references and unsubscribes. A stale destruction
   callback cannot clear a newer view. Loading content without a view also unbinds.
6. Disabling Flow removes all Core and view handlers and clears dependencies.
   Re-enabling defers rebinding by one frame so owner OnEnable can finish first.
   It retrieves the same services and binds once. Disabling GameBootstrap without
   disabling Flow makes Start unavailable; re-enabling restores availability.

View OnEnable removes then adds its Button listener; OnDisable removes it.
Instance view-event subscribers are isolated against exceptions. Flow never
destroys the owner, changes Core state directly, or cancels a native load.

## Start, loading, and failure behavior

View latches Start unavailable before publishing user intent. Flow validates the
bound source, guarded owner, Ready state, and configured content path; Bootstrap
and the active menu cannot be Start destinations. It marks the request pending
and shows `Loading...` before calling
`SceneLoader.TryLoadScene(path, GameState.Playing, out error)`. Further clicks or
submits cannot issue another request. Core provides independent overlap rejection
and token-owned Loading reservations.

The production backend remains unchanged: asynchronous Single loading with scene
activation enabled. MainMenu unloads; its UI and EventSystem are destroyed. The
same BootstrapCore survives. Core finalizes Playing and releases its reservation
before completion notification. Flow then has no scene view.

On synchronous rejection or an observed failure event, Flow clears its pending
flag, records a technical LastError, and refreshes the remaining view. The user
sees `Unable to start. Please try again.`; raw asset paths are console diagnostics.
Start becomes available again only if Core recovered to Ready and services remain
available. An unavailable owner shows `Menu unavailable.`.

Native loading cannot be cancelled. Disabling Flow does not undo a load; on
re-enable, the loader's current busy state still blocks Start. Logical recovery
does not reconstruct an already unloaded scene. A missing view after a destructive
native failure has no retry UI; fallback policy remains a future feature. Core's
reservation, stale-callback, and failure-recovery behavior is unchanged.

## Authored UI and existing input

MainMenuRoot uses a Screen Space Overlay Canvas, GraphicRaycaster, CanvasScaler
(1920 x 1080 reference, width/height match 0.5), background, title, Start Button,
and status Text. Text uses Unity's built-in LegacyRuntime font; no font package
or new artwork is introduced. A separate scene-owned EventSystem has only
InputSystemUIInputModule, with Start as first selection.

The module references the existing `Assets/InputSystem_Actions.inputactions`
(GUID `052faaac586de48259a63d0c4782560b`) and its existing UI actions:

| Action | Existing bindings used by this feature |
| --- | --- |
| `UI/Point` | `<Mouse>/position` |
| `UI/Click` | `<Mouse>/leftButton` |
| `UI/Navigate` | Keyboard WASD and arrow-key 2D vector composite |
| `UI/Submit` | `*/{Submit}`; keyboard Enter |
| `UI/Cancel` | `*/{Cancel}`; keyboard Escape |

Cancel is connected but has no menu action in this scope. There is one selectable
button, so navigation retains its selection. Space, Tab cycling, and NumpadEnter
are not promised by these existing bindings. Gamepad/touch bindings already in
the asset are preserved but are outside this feature's validation target.

## Tests and validation boundary

`HUSEscape.UI.EditModeTests` and `HUSEscape.UI.PlayModeTests` reference the existing
test packages, Core, and UI. Runtime assembly grants only these two test assemblies
internal access. Core's assembly definitions and internal-access contract do not
change. Test assemblies are excluded from normal player builds.

| Suite | Coverage |
| --- | --- |
| Edit Mode MainMenuViewTests | Disabled-before-bind, focus, loading, duplicate clicks/listeners, disable/unbind/destruction, subscriber exceptions. |
| Edit Mode MainMenuSceneTests | Authored scene components, serialized references, existing action IDs/bindings, Bootstrap owner wiring, build order. |
| Play Mode MainMenuFlowPlayModeTests | Real menu initialization, explicit binding, exactly-once Start dispatch, retry after real loader rejection, failure-notification recovery, owner/Flow disable and re-enable, subscription cleanup, unloading, stale callbacks, ambiguous views, missing owner, real mouse and keyboard UI module events. |

Play Mode tests load the authored MainMenu additively to protect Test Runner
scenes. Accepted Start tests replace only a private instance-scoped UI request
delegate; the production View, Flow, event handlers, and InputSystemUIInputModule
still execute. Rejection tests use the real SceneLoader. These tests do not claim
native failure reproduction: the UI failure-notification assertion invokes the
real loader event's subscriber snapshot; Core tests verify failure publication
and state recovery separately. These tests do not claim
native Single-mode scene replacement coverage. Headless input tests use a
disposable in-memory InputSettings clone to route events to Game View regardless
of background focus; otherwise the Editor disables or diverts keyboard/mouse
input. They create virtual devices, remove them during teardown, and restore the
original settings object even after failure. No settings asset is edited.

A separate disposable-project Editor process runs the authored Bootstrap and
unchanged UnitySceneLoadBackend without NUnit. Its in-memory observer records
startup to MainMenu/Ready, mouse Start to SampleScene/Playing, a harness-only
return request to MainMenu/Ready, and keyboard Start to SampleScene/Playing.
It verifies actual automatic Single activation, source/view destruction, the
same persistent services, loading feedback, exactly-once intent/completions,
reservation release, and scene-reference cleanup. The observer never calls
DontDestroyOnLoad and no harness files are added to the repository. A parent
600-second timeout terminates only its own Editor child; timeout is a failure.
Its virtual-input injection uses the same temporary focus-settings clone; scene
loading still runs through the unmodified default native backend.

## Execution results

Unity 6000.6.5f1 compiled the disposable project and executed:

| Suite | Core | UI | Passed | Failed | Blocked / skipped |
| --- | --- | --- | --- | --- | --- |
| Edit Mode | 98 | 12 | 110 | 0 | 0 |
| Play Mode | 20 | 13 | 33 | 0 | 0 |

The separate native startup smoke process exited 0 with 43/43 assertions passed,
including both mouse and keyboard Single-mode replacements. It recorded four
native completions (startup, mouse Start, harness return, keyboard Start), zero
failure events, the expected Ready/Loading/Playing sequence, and idle released
reservations after every completion. No process timeout was treated as success.

Initial headless input runs failed because of background focus routing and
InputManager's automatic disposal of temporary settings. The final fixture
preserves the original settings object and flags, uses an in-memory clone, and
restores the original object during teardown. Two initial smoke attempts also
failed their no-error assertion due to UnityEditor.Search startup indexing in
the temporary environment, despite passing scene assertions. Copying the existing
local `UserSettings/Search.index` and `Search.settings` into that disposable copy
resolved the Editor startup error; repository files were unchanged by that fix.
Initial failures, final XML/JSON results, harness sources, process reports, and
Editor logs are retained outside the repository with the validation artifacts.

No C# compiler errors or warnings occurred in the final Unity runs. Batch-mode
behavior does not replace visual inspection of layout, accessibility, or
interaction on a physical Windows PC. Engine hangs, physical-device behavior,
rendering, fallback after a destructive native failure, and gameplay remain
outside this validation.
