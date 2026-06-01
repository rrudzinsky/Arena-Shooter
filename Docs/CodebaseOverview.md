# Codebase Overview

## Project Shape

Arena Shooter is a Unity/C# first-person arena shooter. Most gameplay code lives under `Assets/Scripts/ArenaShooter`. Generated assets, models, audio, shaders, scenes, and UI live under their matching `Assets` subfolders.

## Match Flow And Game Modes

`MatchController.cs` is the central runtime coordinator. It owns match setup, game mode state, spawning, All Out War armies, wave/encounter flow, pickups, score/scrap, win/loss state, and many cross-system helpers.

When changing match behavior, inspect `MatchController` first, then follow calls into specific systems.

## Arena Generation

`ArenaGenerator.cs` builds procedural arenas, including rooms, corridors, obstacles, floors, dome/backdrop behavior, spawn regions, pickups, and healing resources.

`ArenaLayout.cs` stores room graph data and path/layout helpers used by AI and generation. It is the bridge between generated geometry and tactical/search logic.

All Out War map generation is documented in `Docs/AllOutWarMapGeneration.md`. For a narrative explanation, read `Docs/DesignNotes/AllOutWarMapGeneration_Explained.md`.

## AI And Droids

`DroidController.cs` controls droid movement, combat, sight checks, cover, survival behavior, All Out War squad movement, and NavMesh/manual routing.

Related files include:

- `CombatantHealth.cs` for health, damage, death, and attacker state.
- `CombatantTeam.cs` for team relationships.
- `DroidBlasterRig.cs`, `DroidCrouchPose.cs`, and droid visual/animation helpers for presentation.
- `CompanionDroidController.cs` and `OpponentController.cs` for other AI actors.

All Out War squad design is documented in `Docs/AllOutWarSquadAI.md`. For a more narrative explanation of how the system behaves, read `Docs/DesignNotes/AllOutWarSquadAI_Explained.md`.

All Out War soldier scaling and performance LOD are explained in `Docs/DesignNotes/AllOutWarPerformanceLOD_Explained.md`. Start there when changing soldier LOD tiers, AI scan throttling, LOS/pathing cadence, presentation throttling, or future squad background simulation.

## Player

`PlayerFpsController.cs` handles first-person movement and player control. `FirstPersonViewModel.cs` and related asset builders control weapon/hand presentation. `PrototypeHud.cs` displays player-facing HUD data.

## Weapons, Ammo, And Pickups

`WeaponDefinition.cs` defines weapon data. `WeaponInventory.cs` owns equipped weapons, ammo, fire behavior, and ammo clamping.

Pickup/resource files include:

- `WeaponPickup.cs`
- `AmmoPickup.cs`
- `HealthPickup.cs`
- `ScrapPickup.cs`
- `HealingStation.cs`
- `PickupVisuals.cs`
- `PickupLiftAuraAnimator.cs`

All Out War AI uses limited ammo and squad-carried resource behavior, so changes to ammo or health pickups may also affect squad logistics.

## Destructible Arena Blocks

`DestructibleArenaPiece.cs` owns chunked destructible walls/floors/pillars, projectile pass-through checks, damage contours, neighbor shatter, and destructible outline proxy behavior.

Destructible arena behavior is currently owned by `Assets/Scripts/ArenaShooter/DestructibleArenaPiece.cs`; the old chunk-owned destructible docs were removed after the wall visual model moved to contour-owned cuts.

## UI And Menus

`MainMenuController.cs` owns menu flow, mode setup controls, sliders, controller navigation, and match setting submission.

`PrototypeHud.cs` owns in-game HUD display such as ammo, health, wave/match info, and All Out War status.

## Visuals, Assets, And Audio

Many files ending in `Asset.cs` procedurally construct or configure runtime/editor assets for arena pieces, droids, weapons, pickups, gates, and healing stations.

Droid visuals are assembled from imported body validation, fallback procedural body construction, a separate generated blaster rig, crouch/root presentation helpers, and visual walk animation helpers. Current imported droid bodies use stable whole-body presentation only; procedural fallback droids keep the old limb animation path. For design context on this split, read `Docs/DesignNotes/GpuRenderingVisuals_Explained.md`.

Stylized rendering and outline behavior is centered on `Assets/Scripts/ArenaShooter/Rendering/DroidOutlineRendererFeature.cs`, `DroidRenderSetup.cs`, `ArenaTheme.cs`, and shaders under `Assets/Shaders`. The outline feature keeps its legacy droid-oriented class name, but it functions as the general stylized neon silhouette pipeline with per-category style profiles.

GPU/stylized visuals are documented in `Docs/GpuRenderingVisuals.md`. For a narrative explanation, read `Docs/DesignNotes/GpuRenderingVisuals_Explained.md`.

The All Out War performance LOD narrative also covers visual/presentation throttling for health bars, animation, footsteps, and far beam effects. Use it with the GPU visuals docs when tuning visual-only LOD behavior: `Docs/DesignNotes/AllOutWarPerformanceLOD_Explained.md`.

Audio helpers include:

- `ArenaAudio.cs`
- `FootstepAudio.cs`
- `ArenaNoise.cs`

Rendering helpers and shaders live under `Assets/Scripts/ArenaShooter/Rendering` and `Assets/Shaders`.

## Debugging Protocols

Debug protocols are opt-in. Use them when the user asks for a review, bug hunt, audit, system-specific debugging, or an explicitly bug-fix/debugging implementation task.

Current protocol docs:

- `Docs/DebugProtocols/AllOutWarSquadAI_DebugChecklist.md`
- `Docs/DebugProtocols/AllOutWarSquadAI_Scenarios.md`

For All Out War squad AI, read the design contract first:

- `Docs/AllOutWarSquadAI.md`

For human-readable design context, read:

- `Docs/DesignNotes/AllOutWarSquadAI_Explained.md`
