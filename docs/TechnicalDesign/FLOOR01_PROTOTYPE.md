# Floor 01 prototype

Open `HUS_Escape/Assets/_Project/Scenes/Floor01_Prototype.unity` in the existing
Unity 6000.6.5f1 project and press Play. The scene is already authored and saved;
no runtime builder or setup command is required. SampleScene, Bootstrap, MainMenu,
Build Settings, existing prefabs, and gameplay source files remain unchanged by
this level-authoring task. MainMenu Start now loads Floor01_Prototype; see [JOURNEY_INTEGRATION.md](JOURNEY_INTEGRATION.md) for current startup and validation.

## Layout and route

The floor is flat at world Y=0. The player's existing CharacterController is
2 m tall with radius 0.5 m, centered on its root; spawn is `(0, 1.08, 8)`.
The corridor runs along +Z, is 14 m wide and 62 m long. Three rooms are on the
east/+X side, each approximately 16 x 14 m, with a 5.4 m ceiling. These deliberately
spacious prototype dimensions accommodate the existing 6.3 m third-person camera;
they are not a final architectural floor plan.

| Room | Interior Z span | Door center | Required key | Key location |
| --- | --- | --- | --- | --- |
| A101 | 10–24 | `(7, 1.4, 17)` | `Key_A101` | Corridor stand near `(0, 0.79, 11.5)` |
| A102 | 28–42 | `(7, 1.4, 35)` | `Key_A102` | A101 desk near `(12, 0.79, 21)` |
| LAB | 46–60 | `(7, 1.4, 53)` | `Key_Lab` | A102 desk near `(12, 0.79, 39)` |

The listed door center is the center of the opening; the actual hinge is 0.9 m
earlier along Z. Each gap is 2.2 m wide and 3 m high with separate jambs/header.
The leaf is 1.8 x 2.8 m. A -90 degree hinge orientation makes the existing positive
90 degree open motion swing into the room. Door frames stay fixed; simple handles
are children of the moving hinge. Decorative handles have no blocking collider.

Playable route:

1. Walk forward from spawn and press F near the corridor stand for Key_A101.
2. Approach A101, press E, and allow the existing opening motion to complete.
3. Enter A101 and pick up Key_A102 from the desk toward its far end.
4. Return to the corridor, go to A102 and repeat; its desk holds Key_Lab.
5. Open LAB and reach its far-side completion sign.

All three doors use instances of the existing Door_Locked prefab and keep
Consume Key=false. All pickups use instances of Key_Pickup. No required key is
behind its own locked door. New instance IDs contain no whitespace. Inventory
capacity remains 12; new scene-only display entries label Key_A102 and Key_Lab.

## Editing the saved scene

- `Floor01_Architecture`: corridor, room walls/floors/ceilings, frames and desks.
- `Floor01_Doors_And_Keys`: three door instances and three pickup instances.
- `Floor01_Signs_And_Spawn`: start marker, instructions and LAB finish sign.
- `Floor01_Lighting`: one directional and eleven point lights, without shadows.
- `Player`, `Main Camera`: copies of the current SampleScene setup, with references
  rebound to each other in this scene. Original source objects/assets are untouched.

Room signs are saved world-space UI canvases: A101, A102 and PHONG MAY TINH.
Their text sits in front of the backing, faces the corridor, uses a built-in font,
and has positive scale. Key labels remain as route markers after collection.
Ceilings are actual saved cubes; temporarily hide them in Scene View if an overhead
editing view is needed. No wall segment spans the door opening below its lintel.

## Created assets

- `Assets/_Project/Scenes/Floor01_Prototype.unity` and metadata.
- Seven URP/Lit material assets under `Assets/_Project/Materials/`, all prefixed
  Floor01_: Floor, Wall, Ceiling, Door, Metal, Key, Furniture, with metadata.
- `Assets/_Project/Scripts/Utilities/Editor/Floor01PrototypeBuilder.cs`, metadata,
  and its new Editor folder metadata.
- This document.

No new gameplay prefabs or runtime gameplay scripts are introduced. The editor
menu `HUS Escape > Build Floor 01 Prototype (new scene only)` is for authoring an
absent scene in Edit Mode and refuses to overwrite an existing Floor01_Prototype.
It saves geometry through Unity Editor APIs, not during Play. Do not rerun it to
replace manual scene edits; edit the existing saved scene normally.

## Executed validation

Unity 6000.6.5f1 authored the scene using the existing temporary verification copy
of this project, then only the new scene/material assets were transferred back.
There is no new deliverable Unity project. The original open Editor was not
terminated or driven automatically. The final saved scene/assets match those
used by the native validation process.

The native Play Mode probe passed **76/76 checks**, exit code 0, no timeout and
zero captured runtime errors. It drove the existing PlayerController through
1,749 WASD input frames using virtual devices and deterministic frame time; it
did not teleport between navigation waypoints or replace gameplay logic.

Verified:

- Saved scene loads with existing Player, CharacterController, Animator, Avatar,
  camera, inventory and UI references correctly connected.
- Three prefab door/key pairs have the exact required IDs and non-consuming keys.
- Closed door colliders block the portals; opened portals have no blocking wall,
  jamb, header or leaf along a capsule crossing matching the player size.
- Each hinge sweep sampled at 0, 15, 30, 45, 60, 75 and 90 degrees clears static
  geometry using Physics.ComputePenetration (5 mm tolerance).
- Actual walking stays grounded, enters all three rooms and completes the route.
- Each F adds exactly one intended key; each E unlocks exactly one intended door
  using the same PlayerInventory; all three keys remain afterward.
- The default camera is indoors and has a clear physics ray to the player at the
  A101/A102 key stops and LAB finish. Tab opens the existing inventory UI.
- Exact room text, font references and corridor-facing orientation are saved.

The first authoring attempt stopped on Unity's unsaved Untitled-scene restriction;
the builder was corrected to open its source as a saved scene before creating the
new scene. The final authoring run and both successful navigation runs are recorded
outside the repository. The existing unused isJumping field produces CS0414;
gameplay code was not changed to silence it. No C# compilation errors occurred.

## Limits and manual check

The existing camera has no wall/ceiling collision avoidance. Its code, 6.3 m
distance and pitch limits are preserved. Extreme orbit angles, proximity to walls,
jumping near ceilings and transient wall crossings may clip or occlude the view;
the test proves the default viewing poses, not every orbit angle. This level does
not add a camera collision system.

The probe runs headlessly and does not establish visual readability on a physical
display, final lighting quality, or the appearance of every animation transition.
Open the scene in the original Editor, confirm signs/lighting, and try movement,
mouse orbit, E/F, jump/crouch and Tab manually. Camera/animation assets and tuned
Open/GrabTorch speeds are unchanged. There are no stairs, NavMesh, puzzles,
horror effects or final art in this prototype.
