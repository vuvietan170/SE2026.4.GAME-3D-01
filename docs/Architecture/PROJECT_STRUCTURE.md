# HUS Escape project structure

Project: HUS Escape – Đêm Kinh Hoàng Trước Deadline
Unity: 6000.6.5f1 (Unity 6.6), Universal Render Pipeline (URP)
Language: C# | Target: TBD (pending official confirmation)

## Repository layout

| Folder or file | Purpose |
| --- | --- |
| `HUS_Escape/` | Unity project root; open this directory in Unity Hub. |
| `HUS_Escape/Assets/` | Unity-imported content and its versioned `.meta` files. |
| `HUS_Escape/Packages/` | Package manifest and dependency lock file; change only for intentional dependency work. |
| `HUS_Escape/ProjectSettings/` | Shared editor and project configuration; review changes explicitly. |
| `docs/GDD/` | Reserved design-document area; folder presence does not imply content is complete. |
| `assignments/VT105/` | Standalone .NET save-slot exercise, outside Unity imports; see SAVE_SYSTEMS.md. |
| `tools/Validation/` | Disposable-project compile/test/build/player-smoke runner. |
| `docs/TechnicalDesign/` | System specifications, implementation plans, and technical constraints. |
| `docs/Architecture/` | Project organization, architectural decisions, and system relationships. |
| `docs/Development/` | Contributor setup, workflows, and development procedures. |
| `.gitignore` | Excludes generated caches, local settings, IDE files, and build output. |
| `README.md` | Repository entry point. |

Unity generates `Library/`, `Temp/`, `Logs/`, `Obj/`, and `UserSettings/`
inside `HUS_Escape/`; these directories must remain ignored by Git.

## Project-owned assets

All new project-owned Unity assets and C# code belong in
`HUS_Escape/Assets/_Project/`.

| Folder under `_Project/` | Purpose and placement rules |
| --- | --- |
| `Art/` | Visual source assets. Use `Characters/`, `Environment/`, and `Props/` for category-specific art; `Textures/`, `Models/`, and `Animations/` for shared assets of those types. Keep one authoritative copy of each asset. |
| `Audio/` | Audio clips organized into `Music/`, `SFX/`, `Ambient/`, and `Voice/`. |
| `Materials/` | Project-owned materials; group by feature or environment when needed. |
| `Prefabs/` | Reusable configured GameObjects organized into `Characters/`, `NPCs/`, `Environment/`, `Interactables/`, `Items/`, and `UI/`. Player prefabs belong in `Characters/`; non-player characters belong in `NPCs/`. |
| `Scenes/` | Active scenes: `Bootstrap/Bootstrap.unity`, `MainMenu/MainMenu.unity`, and `Floor01_Prototype.unity`, `Campus_Prototype.unity`, `TrainingOffice_Prototype.unity`. `Zone01..04` are reserved folders, not implemented scenes; Main Gate MVP is in Campus. |
| `Scripts/` | C# source grouped by responsibility as described below. |
| `Settings/` | Project-owned configuration assets, such as render profiles and future ScriptableObject configuration. Unity's root `ProjectSettings/` remains the location for engine configuration. |
| `UI/` | UI source assets in `Fonts/`, `Icons/`, `Sprites/`, and `Layouts/`; reusable UI GameObjects belong in `Prefabs/UI/`. |
| `Resources/` | Only assets deliberately loaded through Unity's Resources API. Prefer ordinary asset references by default; this is not a general storage folder. |

### Script responsibilities

| Folder under `Scripts/` | Responsibility |
| --- | --- |
| `Core/` | Application lifecycle, bootstrapping, and shared system coordination. |
| `Player/` | Player movement, input handling, and player-specific behavior. |
| `AI/` | NPC decisions, perception, and navigation behavior. |
| `Interaction/` | Interaction contracts and interaction handling. |
| `Inventory/` | Inventory state, storage, and inventory operations. |
| `Items/` | Item definitions and item-specific behavior. |
| `Puzzle/` | Puzzle rules, state, and puzzle-specific logic. |
| `UI/` | UI presentation, menus, and HUD behavior. |
| `Audio/` | Audio playback and audio system coordination. |
| `Utilities/` | Small reusable helpers with no feature-specific ownership; avoid using this as a catch-all. |

The initial FO-001 layout reserved ownership only. Current gameplay exists in the scenes/scripts listed above; folders containing only `.gitkeep`/`.meta` remain placeholders. See [FEATURE_STATUS.md](../Development/FEATURE_STATUS.md).

FO-003B implements the `Scripts/UI/` boundary as the `HUSEscape.UI` assembly and
namespace. It references Core and existing Unity UI/Input System assemblies;
Core never references UI. MainMenuFlow belongs to the persistent BootstrapCore
owner, while MainMenuView and its EventSystem belong to the MainMenu scene.
Keep dependencies explicit and release scene references on unload. UI tests live
in separate `Tests/Editor/` and `Tests/PlayMode/` assemblies. See
[MAIN_MENU.md](../TechnicalDesign/MAIN_MENU.md) for lifecycle and input contracts.

## Current assemblies and save ownership

- `HUSEscape.Core`: lifecycle and SceneLoader; does not reference UI/Player.
- `HUSEscape.UI`: references Core, Unity UI and Input System; owns the Bootstrap-bound menu flow.
- `HUSEscape.Player`: references Core and Input System; owns controller/camera/input/pause code.
- `Assembly-CSharp`: current AI, interaction, inventory, puzzle and Utilities composition/persistence. These have not been split into speculative empty asmdefs. Editor tooling lives under Editor folders; Core/UI tests have separate test assemblies.
- Root-level `Assets/Scripts/SaveSystem` was verified as standalone VT-105, then moved unchanged to `assignments/VT105/SaveSystem`; the .NET 8 harness explicitly compiles it. No game assembly references it. [Save-system relationship](SAVE_SYSTEMS.md).

## Third-party and existing content

`HUS_Escape/Assets/ThirdParty/` holds externally maintained assets and dependencies
imported into Assets. Preserve vendor organization, licenses, and attribution.
Keep project-specific wrappers, adaptations, and prefab variants in `_Project/`
so vendor updates do not overwrite them. Package Manager dependencies remain in
`Packages/`; do not copy their contents into Assets.

Existing template content, including `Assets/Scenes/`, `Assets/Settings/`,
`Assets/TutorialInfo/`, and other existing root assets, is preserved in place by
FO-001. Use `_Project/` for new project-owned work. Any later migration must be a
separate reviewed change that preserves GUIDs and checks scene and asset references.

## Naming conventions

- Use descriptive English names and PascalCase for new folders and asset base
  names; avoid spaces, accents, and names such as `NewFolder` or `Final2`.
- Preserve the established `_Project`, `ThirdParty`, `SFX`, `NPCs`, and `UI`
  spellings. Use the existing `ZoneNN_Name` convention for additional zones.
- Name C# files after their primary type, for example `PlayerController.cs`.
  Use PascalCase for types and methods, camelCase for parameters and local
  variables, and `HUSEscape` as the namespace root with responsibility-based
  suffixes, for example `HUSEscape.Player`.
- Give scenes clear purpose-based names, for example `Bootstrap.unity`,
  `MainMenu.unity`, or `Zone01_LabT3.unity`, inside their matching scene folders.
- Give assets descriptive names, for example `LabDesk.prefab`,
  `LabDesk_Wood.mat`, `LabDesk_BaseColor.png`, and `Campus_Night_Ambient.wav`.
  Use consistent suffixes to distinguish variants and texture channels.
- Name documentation descriptively; use uppercase snake case for architecture
  reference documents such as `PROJECT_STRUCTURE.md`.

## Git and future expansion

- Empty leaf folders contain `.gitkeep` so a fresh checkout restores the layout.
  Parent folders are retained through their tracked children. Remove a leaf's
  `.gitkeep` when real content is added.
- Unity asset folders include versioned `.meta` files with stable unique GUIDs.
  Commit every asset together with its metadata. Dot-prefixed `.gitkeep` files
  are ignored by Unity's asset importer and do not need metadata.
- When moving or renaming assets in future work, use Unity's Project window and
  commit the corresponding metadata changes. Never regenerate an existing GUID
  to resolve a folder organization issue.
- Extend the nearest existing responsibility folder when a real feature requires
  it. Add subfolders when they improve navigation; avoid speculative hierarchies,
  duplicated assets, and miscellaneous dumping grounds.
- Keep editor-only scripts in an `Editor/` subfolder under their owning area
  when such tooling is actually introduced. New assembly boundaries require dependency planning; preserve the implemented Core/UI/Player boundaries.
- Document new ownership boundaries here. Review dependency and ProjectSettings
  changes separately from folder organization work.
