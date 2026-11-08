# Bootstrap scene entry point (FO-003A)

## Scope

Unity 6000.6.5f1 / URP 17.6.0. The Bootstrap scene connects the completed FO-002
Core to an application entry point. It introduces no gameplay, menu UI, loading
UI, packages, prefabs, Singleton, global event system, or changes to the existing
GameManager, GameBootstrap, SceneLoader, and SceneLoadBackend implementations.

## Scene and build configuration

Scene: `Assets/_Project/Scenes/Bootstrap/Bootstrap.unity`.

```text
BootstrapCore (active root GameObject)
  Transform
  GameBootstrap       enabled
  SceneLoader         enabled
  BootstrapSceneEntry enabled
```

The root contains no camera, light, or unrelated content. `GameBootstrap` persists
the entire root using its existing DontDestroyOnLoad behavior. `GameManager` and
`SceneLoadBackend` are ordinary C# objects, not scene components. No prefab is
needed for this single authored root.

`BootstrapSceneEntry` serializes these references and settings:

| Field | Value |
| --- | --- |
| `bootstrap` | GameBootstrap on the same BootstrapCore root |
| `destinationScenePath` | `Assets/Scenes/SampleScene.unity` |
| `destinationState` | `Playing` |

Both scenes are enabled in the global Build Settings:

| Index | Scene |
| --- | --- |
| 0 | `Assets/_Project/Scenes/Bootstrap/Bootstrap.unity` |
| 1 | `Assets/Scenes/SampleScene.unity` |

SampleScene remains at its original path, with GUID
`99c9720ab356a0642a771bea13969a05` and unchanged content. The Input System config
object in EditorBuildSettings is preserved. No other ProjectSettings are changed.

SampleScene is a temporary development destination, not a MainMenu implementation
or a final gameplay scene. A future MainMenu can use the same entry contract with
its enabled full scene path and `Ready` as its destination state. The release
platform remains TBD.

## Initialization and transition flow

1. A build starts at Bootstrap, index 0. In the editor, open Bootstrap explicitly
   before entering Play Mode; this feature does not override the editor's Play
   Mode start scene or silently inject Core into directly opened content scenes.
2. `GameBootstrap.Awake` elects the owner, constructs the manager, initializes the
   loader, persists the root, and enters `Ready`, using the unchanged FO-002 logic.
3. `BootstrapSceneEntry.Start` validates its configuration and calls
   `TryGetServices`. Start runs after scene Awake callbacks; no production script
   execution-order setting is required.
4. The entry subscribes to the loader's instance completion/failure events before
   requesting SampleScene through the public production `TryLoadScene` API.
5. Core reserves `Loading` and uses the default Unity backend in Single mode.
   SampleScene activates automatically, and the Bootstrap source scene unloads.
   BootstrapCore, its manager, loader, and startup observer survive together.
6. Core finalizes `Playing`, releases the reservation, and publishes completion.
   The entry records successful completion, removes both handlers, and disables
   itself. No further scene request is issued automatically.

```text
Initializing -> Ready -> Loading -> Playing
Bootstrap scene       ->           SampleScene
          BootstrapCore persists across the replacement
```

The existing manager, loader, transition matrix, reservation tokens, and backend
remain the only owners of Core state and native loading behavior. The entry
never calls SceneManager directly or changes GameState itself.

## Configuration, lifetime, and errors

- A bootstrap reference is required and must identify the same GameObject.
  Disabled, uninitialized, and rejected owners fail guarded service retrieval;
  startup never searches for another owner.
- Destination paths must use the full `Assets/` path and `.unity` extension.
  Bootstrap cannot target its own scene path. Only `Ready` and `Playing` are
  accepted destination states; SceneLoader verifies enabled build membership.
- Startup requires the manager to be `Ready`. It does not redirect an application
  that is already playing or loading. Bootstrap is an entry point, not a scene to
  revisit as an in-game navigation destination.
- `HasStarted` records the first attempt, including rejection. Re-enabling the
  entry or attempting startup again does not retry or publish another request.
- `HasFinished` records either success or failure. `LastError` is null on success
  and contains a diagnostic on failure. Errors log `Bootstrap startup failed:`
  with the component as context; no automatic retry or fallback scene is added.
- Synchronous failure can also raise SceneLoadFailed before TryLoadScene returns.
  The entry finalizes once and avoids logging its own failure twice.
- The originally requested path is captured. Editing Inspector configuration
  while loading does not invalidate completion matching for the accepted request.
- The entry stays subscribed through a pending load, including when disabled.
  Completion/failure removes both handlers; destroying the entry also removes
  them. Disabling or destroying this observer does not cancel a native load.
- Failure recovery belongs to SceneLoader and GameManager. Logical rollback does
  not recreate an already unloaded scene. Loading cancellation, loading UI,
  fallback-scene policies, and gameplay systems remain outside this feature.

If Bootstrap is renamed, update its self-target guard constant and scene-asset
tests along with Build Settings. Rename or move destination assets through Unity,
preserve their metadata, and update the serialized destination path explicitly.

## Automated validation

`Tests/Editor/BootstrapSceneEntryTests.cs` covers valid and invalid settings,
missing/foreign references, self-target prevention, unsupported states, safe
unavailable-service rejection, the one-attempt contract, authored hierarchy and
serialized references, preserved SampleScene GUID, and enabled build order.

`Tests/PlayMode/BootstrapSceneEntryPlayModeTests.cs` covers real Awake/Start
ordering, success targeting Playing or Ready, duplicate/disabled owners, missing
or unlisted destinations, non-Ready Core, startup and activation failures,
cleanup of both subscriptions, re-enable behavior, observer destruction, and
captured-path matching. The fixtures run actual entry and loader components.
Only the instance-scoped engine boundary is replaced through test-only reflection
between bootstrap Awake and entry Start; no production injection API is added.
These fixture loads cannot unload Unity's Test Runner scenes.

A separate disposable-project process verifies the actual authored Bootstrap
scene and unchanged native Single backend, without NUnit, fake backends, direct
scene-load calls from the harness, or manual SampleScene activation. Its observer
is attached in memory only, before Play Mode; the authored scene is not saved
with the observer. The harness does not call DontDestroyOnLoad, so survival relies
on production GameBootstrap. A parent process enforces a 600-second timeout and
can terminate only the editor process it launched.

### Unity test results

Unity 6000.6.5f1 executed the complete existing and new suites on a disposable
project copy:

| Suite | Existing FO-002 | New FO-003A | Passed | Failed | Skipped |
| --- | --- | --- | --- | --- | --- |
| Edit Mode | 81 | 17 | 98 | 0 | 0 |
| Play Mode | 8 | 12 | 20 | 0 | 0 |

Both test processes exited with code 0. A separate Unity 6000.6.5f1 native startup
process also exited with code 0 after all 33 assertions passed. It confirmed:

- Bootstrap is build index 0 and SampleScene is index 1.
- The authored scene's Core services are ready before the entry's Start runs.
- The unchanged production UnitySceneLoadBackend uses Single mode.
- SampleScene activates automatically and the Bootstrap source scene unloads.
- The same bootstrap, manager, and loader survive the replacement.
- State follows Ready -> Loading -> Playing; the reservation is released.
- Exactly one completion and zero failure events occur, and the loader is idle.
- The entry finishes without an error, removes both subscriptions, and disables
  itself. No extra startup request appears over the following observation frames.

No process timed out, and Unity reported no C# compiler errors or warnings. These
results verify startup and lifecycle behavior; they do not validate rendering,
gameplay, MainMenu content, or native engine-failure recovery. Machine-readable
results and logs are retained outside the repository with the implementation
verification artifacts.
