# Floor 01 visual polish — stage 8

Scene: `HUS_Escape/Assets/_Project/Scenes/Floor01_Prototype.unity`.

## Saved scene changes

- Corridor direction signs, instructions and key markers have opposing text
  faces with positive scales and a thin opaque board between them. This prevents
  the rear glyphs from appearing mirrored through the front. Key_A102 and
  Key_Lab markers face the west-side approach; their reverse faces remain readable
  when approached from the other side. Text strings and item/key IDs are retained.
- Block-style desks and key stands keep their original objects and tested
  collision proxies, with their old solid rendering disabled. New visible tops
  and shared metal leg frames fit inside the same footprint and retain the key
  support height. Other desks retain their tops and use the same frame mesh.
- Chairs retain their seat/back and colliders; the pedestal rendering is replaced
  by a shared four-leg/braced frame. These are still static scenery.
- LAB workstations gain visible monitor stems, raised screen housings, simple
  desktop graphics, shared keyboard keycaps and computer towers within the existing
  desk footprints. Screen/monitor geometry moves upward by 0.15 m; no aisle moves.
- The bookcase has shelves/books; the supply cabinet has doors/handles. Boards
  have trim. Windows gain polished blue glazing, crossbars and sills. Existing
  structural walls remain intact; glazing is an opaque reflective prototype panel.
- Corridor benches have visible frames/slatted backs; plants use green foliage
  and additional leaf clusters; bins gain lids/labels; notice boards gain papers.
- Floors use a small repeating tile texture; walls use pale plaster; ceilings
  and fixed door frames use ivory trim. Wall skirting follows existing solid
  segments and does not cross the door openings.

All additions are normal editable scene objects under `Floor01_VisualPolish`.
Existing objects are retained. No runtime construction script is delivered.
The original scene had 342 enabled Renderers; the final scene has 508, excluding
CanvasRenderer UI batches. Collider count remains 189. Three shared combined
meshes reduce the cost of individual legs/keycaps. New mesh objects use static
batching flags; no batching or pipeline ProjectSettings are changed.

## Exact file scope

Modified: `HUS_Escape/Assets/_Project/Scenes/Floor01_Prototype.unity`.

Created in `HUS_Escape/Assets/_Project/Materials/` (each with its generated `.meta`):

- `Floor01_WoodWarm.mat`
- `Floor01_TrimIvory.mat`
- `Floor01_Plaster.mat`
- `Floor01_Foliage.mat`
- `Floor01_WindowGlass.mat`
- `Floor01_Display.mat`
- `Floor01_TileHall.mat`
- `Floor01_TileRoom.mat`

Created in `HUS_Escape/Assets/_Project/Art/Models/` (each with `.meta`):

- `Floor01_ChairFrame.asset`
- `Floor01_DeskFrame.asset`
- `Floor01_KeyboardKeys.asset`

Created in `HUS_Escape/Assets/_Project/Art/Textures/` (with `.meta`):

- `Floor01_Tile.png` — 128 × 128, shared by both floor materials.

Created documentation: `docs/TechnicalDesign/FLOOR01_GRAPHICS.md` (this file).
Total: 1 modified file and 25 new files, counting metadata and documentation.
No existing material, mesh, prefab or metadata file is replaced.

## Protected gameplay and architecture

Static scene comparison against the pre-edit backup retains every original
serialized object. All 235 original script, Animator, Camera, CharacterController,
Light, AudioListener, BoxCollider and prefab-instance blocks compare unchanged
after trailing-whitespace normalization. All three HandleTarget transforms are
unchanged. Original floor/wall transforms and door hinge configuration are kept.
Only decorative furniture, labels and material assignments are revised.

Player/controller/camera scripts, DoorInteraction, DoorHandleIK, Inventory/UI,
Avatar/model, Animator Controller and eight Mixamo animation assets are not edited.
Open speed remains 2.2. SampleScene, Packages, Input Actions and ProjectSettings
are unchanged. The key route and non-consumable key behavior remain:
Key_A101 -> A101 -> Key_A102 -> A102 -> Key_Lab -> LAB.

## Backup and native validation

Before editing, scene and metadata were backed up outside the repository:
`C:/Users/ASUS/AppData/Local/Temp/HUS_Floor01_Graphics_88ad08bcaeb24da984eb7320a741c055/`.
Temporary folders can be cleaned by the OS; copy this backup elsewhere for
long-term retention. Do not import its duplicate-GUID scene into Assets.

Authoring/validation ran in the existing separate Unity verification copy:
`C:/Users/ASUS/AppData/Local/Temp/HUS_PlayerDuplicateFix_cba3350ccc274de684902c9ad701c1a4/`.
Original current ProjectSettings, asset settings and production scripts were
copied into that verification environment; the original project's settings were
only read. An initial blank capture exposed stale render settings in that copy;
those images were rejected. Final images use the actual URP configuration and
a warmed native Direct3D12 render, with an explicit nonblank-image check.

Unity 6000.6.5f1 final results:

- `floor-graphics-Play.json`: 107/107 production gameplay/IK checks pass,
  0 failures/captured runtime errors; PID 22804, exit 0, no timeout.
- `floor-graphics-regression-Test.json`: 33/33 IK/control checks pass,
  0 failures/captured runtime errors; PID 35492, exit 0, no timeout.
- `floor-graphics-visual-Setup.json`: 555 structural/material/label/image checks
  pass; PID 43364, exit 0, no timeout. Many checks are repeated material-slot
  validations, not independent gameplay scenarios.
- Native screenshots cover both corridor directions, A101, A102, LAB and the
  west-side Key_A102 approach. They were visually inspected for readable text,
  furniture and missing/pink rendering. Their names are `floor-graphics-*.png`.
- The real controller completes the key route and classroom/LAB aisles. E/F/Tab,
  correct keys, single-target selection, door clearance, initial IK contact,
  release/ownership and sampled camera visibility pass. Walking, sprint, jump,
  crouch and repeated interaction/leave-door recovery also pass.
- Final logs contain no C# errors, new C# warnings or captured runtime errors.
  These are standalone native Play Mode probes and rendered Editor captures,
  not a claim that NUnit suites or the original open Editor were driven.

The corresponding `.log` and `-process.json` files are in the same verification
directory. The delivered scene is whitespace-normalized after native saving;
that cleanup only removes spaces at line ends. Shared visual assets and metadata
are copied unchanged from the verified native authoring output.

Final scope/hash verification finds exactly the 1 modified scene and 25 new
files listed above, no deleted files, no duplicate asset GUIDs, no unresolved
local scene references and no temporary probe/capture objects saved in the scene.
The scene and all copied asset/metadata files match the verification copy.
`git diff --check` still reports seven pre-existing trailing-whitespace lines in
SampleScene; its hash is unchanged and those unrelated lines are not edited.
No files are staged, committed or pushed by this task.

## Opening and remaining acceptance

Stop Play, wait for imports and reopen Floor01_Prototype from disk. Preserve any
independent unsaved Editor work first. Expand Floor01_VisualPolish for the visual
additions; existing Floor01_Interiors remains available. Play the original route,
including looking back at the direction signs after passing them.

Rendered captures use temporary cameras in the verification copy. Manual review
of every angle in the original Game View, animated finger/mesh contact, camera
wall clipping and a real FPS/profile measurement remain outstanding. No player
camera tuning, animation speed or accepted IK pose was changed to improve the
captures. Lighting is retained; this stage does not add horror effects, realtime
lights, external assets or a new render pipeline.
