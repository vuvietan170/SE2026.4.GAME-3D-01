# Stage 13.1 — T3 ground-level exit route

## Inspection and architectural choice

The inspected Floor01 scene has corridor and all three room floor surfaces at
world Y=0, a 5.4 m clear ceiling, and a 2 m CharacterController whose grounded
root is approximately Y=1.08. The corridor occupies X=-7..7 and Z=0..62; A101,
A102 and LAB remain on its east side at Z=17, 35 and 53. Campus ground is also Y=0.

Scene origins alone do not establish a physical storey number. However, the
existing prototype documentation describes a flat floor, and there is no authored
lower storey, stairwell, elevation metadata or documented upper-floor designation.
This change therefore follows the requested ground-floor branch and preserves
the accepted layout. It does not raise the existing rooms or add an arbitrary
staircase. Downstairs traversal is **not applicable**, rather than an untested
staircase feature.

The old exit was a door-shaped mesh directly in front of a continuous end wall.
It is replaced by a real opening and a short, same-level exit vestibule.

## Saved layout

```text
Original corridor / return from LAB
        Z=53   wall signs + yellow floor arrows
        Z=60   approach; no scene-load interaction here
        Z=62.1 real doorway, 4.2 m wide / 3.6 m high
               two door leaves held open along the sides
        Z=62..72 ground-level exit vestibule, ~6.4 m wide
               floor, side walls, ceiling, two lights, wall instructions
        Z=70.5 stand in the final door's approach area
        Z=72   visible T3 exit door and push bar
               E after all 17 objectives -> Campus spawn (0, 1.08, 8)
```

The original end-wall object is retained as the left segment; a matching right
segment and lintel create the opening. Two existing skirting pieces are split
around that opening. New floor overlaps the existing slab slightly at the seam;
both top surfaces remain Y=0. The new floor reaches Z=72.4. There are no steps,
height changes or new storeys. Colliders cover the floor, walls, frames and door
leaves. Decorative arrows, handles, fixtures and signs have no blocking colliders.

New geometry is grouped under `T3_ExitVestibule`. Existing `Floor01_CampusExit`
and its original door/frame keep their object IDs and move to the final doorway.
The entrance leaves are static, visibly held open; no new door interaction or
animation system is introduced. The final T3 door retains E-only scene travel.

Signs are on the west wall near LAB/corridor approach, above the entrance flush
with its lintel, and on the vestibule wall. Floor arrows point along +Z. Low
centre-line billboards found during visual validation were moved onto the walls
so the third-person camera can pass without being covered by text.

All original scene blocks outside six exit-related blocks are byte-identical:
end-wall transform, two skirting transforms, exit-root transform/child list,
BuildingExit's serialized zone reference, and SceneRoots. No original object is
deleted. The added 205 YAML blocks form the vestibule/signs/lights/zone, bringing
the Floor scene to 3,844 documents. A101/A102/LAB geometry, furniture, door hinges,
handles/IK, Player, camera, quest definitions and animation data are preserved.

## Interaction and save compatibility

BuildingExit uses an Inspector-assigned BoxCollider trigger as a three-dimensional
approach volume, centred at `(0, 1.5, 70.7)`, size `(3.4, 3, 2.4)`. It does not
load on trigger entry. E is still required, and JourneySession still enforces
the 17 floor objectives, completed LAB, settled interactions and grounded pose.
The old distance fallback is retained for unconfigured legacy instances, now
using 3D distance. A disabled assigned zone does not silently fall back.

The prompt is a solid dark card with scaled white text and a green/amber accent.
It is hidden during other modal/Inventory UI and loading. Rejection feedback is
discarded when its recorded completion status no longer matches the current
Quest state, avoiding a stale incomplete-floor message after Continue/completion.

The only Save V1 logic change extends the accepted Floor position maximum Z from
62 to 73, covering the new ground-level vestibule. Format version, identifiers,
fields and both slot filenames are unchanged. Finite-value, capsule-overlap,
support, quest/item and reward consistency validation remains in place. Old
saves in Z=0..62 are still valid; new saves at the final door restore safely.
The shared validator also accepts that pose inside a Campus slot's embedded
Floor snapshot. No file migration, autosave or automatic overwrite is performed.

There is no new Player, SceneLoader, runtime component type, prefab, material,
package, scene registration or ProjectSettings change in this stage.

## Files

Modified:

- `HUS_Escape/Assets/_Project/Scenes/Floor01_Prototype.unity`
- `HUS_Escape/Assets/_Project/Scripts/Interaction/BuildingExit.cs`
- `HUS_Escape/Assets/_Project/Scripts/Utilities/Floor01SaveSystem.cs`
- `tools/Campus/CampusPlayProbe.cs` — actual exit-route and save regression
- `docs/TechnicalDesign/CAMPUS_FOUNDATION.md` — current route pointer

Created:

- This document.
- `tools/Campus/ExitVestibuleSetup.cs` — guarded one-time Scene authoring.
- `tools/Campus/ExitSignsPolish.cs` — recorded wall-mount correction.
- `tools/Campus/ExitRouteProbe.cs` — disposable late-frame camera audit.
- `tools/Campus/ExitVestibuleViewProbe.cs` and `ExitVestibuleVisualValidation.cs`
  — native Game View captures.

Tools are repository development files, installed only into the existing D:
verification project. They are not attached to the delivered Scene. Do not run
the authoring tool on an already updated Scene; it refuses duplicate geometry.

## Verification

Backups and evidence:
`D:/Game3D-01/TempTests/T3Exit_20261113_Stage13_1/`.
Original/ retains the pre-change Scene/assets/settings; existing real save slots
were copied separately if present. Previous Quest/Campus backups remain intact.

Unity 6000.6.5f1 runs in a separate D: verification project, with a 420-second
parent timeout, D: TEMP/GI cache and no termination of unrelated Editor processes.
Final production Assets are compared byte-for-byte against the main project.

| Actual check | Result / evidence |
| --- | --- |
| Accepted complete A101/A102/LAB and Save V1 suite | PASS 388/388 — FinalGameplay2/FloorRegression/floor-regression.json |
| Updated native Floor->Campus route, input, AI and two-slot continuation | PASS 81/81 — FinalGameplay2/floor-save-route.json |
| Camera body and near-plane safety on the exit route | 971 monitored frames, no detected overlap; four orbit directions tested |
| Cold Continue with final vestibule snapshot | PASS 11/11 — FinalCold/floor-save-route.json |
| Cold Continue with pre-13.1 saves | PASS 11/11 — LegacySaveCompatibility/floor-save-route.json |
| Existing Unity Edit Mode suite | PASS 110/110 — EditMode/results.xml |
| Existing Unity Play Mode suite | PASS 33/33 — PlayMode/results.xml |
| Unity AssetDatabase audit | PASS — two scenes, 27 script GUIDs resolved, zero missing MonoBehaviours — Audit/floor-save-route.json |
| Real Game View | Five captures — FinalVisual/: hall guide, entrance, final prompt at 720p/1080p, Campus arrival |
| Downstairs/stair traversal | NOT APPLICABLE — existing prototype kept ground-level |
| Manual input/visual acceptance in the user's original open Editor | NOT TESTED by automation |

The native route uses real Input System events through production controllers,
real physics, actual Save I/O and the unchanged production Single-mode backend.
It walks both ways through the entrance before completing quests, rejects E at
the final door until completion, then repeats the path after the real LAB route.
It checks that there is no interaction volume at the old corridor location,
rejects an actor at another height, saves at Z>69, loads Campus, restores the
vestibule save, and confirms New Game/Continue do not overwrite either slot.
Camera orbit parameters are changed only as test fixtures, never in the saved
camera component or script. Native scene transitions run outside NUnit.

The first native run exposed stale rejection text after rapid automated Quest
completion; that failure is retained under Gameplay/. The first visual captures
exposed a low billboard covering the camera; Visual/ is retained, while FinalVisual/
shows the corrected wall-mounted signs. No failed/intermediate result is relabelled
as passing. No performance benchmark is claimed for this geometry-only change.

Known Editor.Search startup exceptions and the existing Animator AnyState->Run
warning remain visible in raw logs; they are not fixed by changing packages,
Animator assets or ProjectSettings. Production runtime errors and the known
Editor startup error are reported separately. Compilation/test counts and final
scope are recorded in the machine-readable final verification report.

Final stage scope is five modified and six new files, with no deletions. All
existing metadata/GUIDs, Campus scene, Core, Player/Inventory/Quest gameplay code,
camera, IK, model/animation assets, Packages and ProjectSettings remain unchanged
against the Stage 13.1 baseline (the only persistence code delta is the Floor Z
limit). The existing CampusPlayProbe is extended, not replaced. `git diff --check`
passes; Git's inherited LF/CRLF conversion notices remain. Work stays on
feature/fo-005-quest-v1 without staging, commits, pushes or merges.

## Editor acceptance

Stop Play, preserve any independent unsaved Editor work, and reopen
Floor01_Prototype from the Project window to load the saved geometry. No builder
or manual component attachment is required.

1. Complete LAB and confirm its final guidance, or Continue a completed Floor save.
2. Return through the LAB door into the central corridor; turn toward +Z/the far
   end of LAB. Follow the yellow arrows and west-wall exit signs.
3. Walk through the two open blue entrance leaves at Z=62.1. Continue along the
   lit ground-level vestibule. No E press or jump is needed to pass this entrance.
4. At the final blue T3 door, near `(0, 1.08, 70.5)`, read the bottom prompt and
   press E. Campus should load at the existing position in front of T3.
5. Repeat before completion to check the locked progress message. Test F5 saves
   in the vestibule, Continue Floor/Campus, and mouse orbit along the new walls.

If later design establishes an upper-storey starting area, a real lower landing
and appropriately versioned vertical save bounds should be designed explicitly;
this stage does not invent that floor hierarchy.
