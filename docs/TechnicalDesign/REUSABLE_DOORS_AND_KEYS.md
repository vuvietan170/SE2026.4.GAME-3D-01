# Reusable doors and keys

This extends the existing DoorInteraction and PickupItem components. Inventory,
Inventory UI, PlayerController, camera, Animator controller, animations, scenes,
packages, and ProjectSettings are not replaced or edited.

## Assets and Inspector setup

- `Assets/_Project/Prefabs/Interactables/Door_Locked.prefab`
- `Assets/_Project/Prefabs/Items/Key_Pickup.prefab`

Both prefabs copy the currently authored objects in SampleScene, including their
mesh, collider, URP material, and local geometry. Door_Locked retains the hinge
root and offset door child. Root positions are reset for reusable placement;
existing scene objects are untouched. These use the existing primitive visuals,
not new art or an imported key model.

1. Drag the prefabs into the desired content scene. Place the door hinge at the
   intended pivot. The current door mesh is centered vertically around that pivot
   (a two-unit door therefore typically needs the hinge one unit above the floor).
2. On each DoorInteraction, set **Required Key Id**, for example `LabT3_Key`.
   Leave **Is Locked** enabled and **Consume Key** disabled for an ordinary key.
3. On its matching PickupItem, set **Item Id** to the exact same string, including
   case. **Item Name** is the display label, not the lock identifier.
4. Leave **Player** and **Player Animator** empty in the prefab. An active
   PlayerInventory in the same runtime scene supplies the actor and Animator.
   Keep the existing player with its existing movement/animation/inventory setup.
5. Play and press F near a key, then E near its matching door. The key remains in
   inventory after unlocking. Duplicate instances can use different IDs without
   changing the shared prefab asset. Do not apply a scene Player reference back
   into the prefab asset.

Existing manually assigned scene references continue to take priority. Automatic
binding accepts only one active PlayerInventory in the target's scene. It fails
safely when multiple candidates exist rather than choosing an arbitrary Player.
For that exceptional setup, explicitly assign the desired actor on the scene
instance. Cross-scene persistent actors require that explicit instance binding;
there is no global player registry or Singleton.

## Input selection

Existing input remains E for doors and F for pickups. Each press selects only
the nearest enabled, in-range target of that category for the same actor.
Equal-distance targets use a deterministic runtime entity-ID tie breaker.
Selection does not depend on Update ordering. Busy targets remain candidates,
so a repeat press during their animation cannot fall through to a nearby object.
Locked doors also remain candidates; a key mismatch does not open a farther door.
Only the selected object's interaction prompt is shown for each category.

Distance rules remain those of the existing implementation: doors use horizontal
distance, pickups use full 3D distance. This does not add aiming, raycasts,
line-of-sight checks, or a shared animation lock between simultaneous E and F.
Inventory-open behavior is unchanged. These policies can be expanded separately
if the level design requires them.

## Preserved settings and implementation scope

Door defaults copied from the scene: interaction range 1.6, turn duration 0.2,
open delay 0.6, cooldown 1.2, opening angle 90, rotation speed 60, message duration
2. Pickup defaults copied from the scene: interaction range 3, pickup delay 0.4.
The script defaults and existing coroutine bodies, Open/GrabTorch trigger names,
animation assets, and Animator speeds remain unchanged.

Only the actor binding, nearest-target gate, and prompt gate change in the two
existing scripts. `InteractionSelection.cs` is a stateless helper shared by them;
it introduces no registration lifecycle, new player component, or dependency on
Core. Existing script GUIDs, namespaces, class names, and serialized field names
are preserved. Key consumption remains the existing optional flag, default false.

## Validation boundary

The repository already contains two source files declaring the same
`HUSEscape.Player.PlayerController` type:
`Scripts/Player/PlayerController.cs` and `Scripts/Player/PlayerController1.cs`.
Resolving that existing duplicate is outside this prefab task. Neither file is
changed or removed. Full-project compilation must be checked after the owner
decides which player source should remain active.

Focused validation uses a separate temporary Unity project containing the exact
production DoorInteraction, PickupItem, InteractionSelection, PlayerInventory,
prefabs, existing input asset, settings assets, and existing package dependencies.
It excludes the unrelated player sources and does not modify any scene in this
repository. A temporary Play Mode observer injects virtual E/F input into the
real components and exercises multiple configured instances. It uses temporary
in-memory input settings for headless focus, restoring them afterward. This is
not a claim that the complete original project compiled or passed regression
tests.

Unity 6000.6.5f1 completed the focused native Play Mode probe with **28/28
assertions passed**, process exit code 0 and no timeout. The final compile log
contains no C# errors or warnings. It verified prefab import/material references,
null scene references, preserved settings, automatic actor binding, E without a
key, wrong-key rejection, independent RoomA/RoomB instances, one pickup per F,
one door per E, open/close, retained keys, busy-target selection, equal-distance
selection, and safe rejection of ambiguous actor binding. No Animator was attached
to the temporary actor; visual player animation playback was not tested.

An initial probe stopped on an incorrect object-null assertion in the temporary
harness; that assertion was corrected to use Unity object-null semantics. Initial
obsolete-search-API warnings in the new helper were also resolved before the
successful final run. Initial and final results, observer sources, process reports,
and Unity logs are retained in the external temporary validation directory.

In Unity, still verify the actual player animations and transitions, movement,
camera, twelve-slot inventory/Tab behavior, and final visual placement in your
current scene. Existing scene doors/keys have not been replaced automatically.
