# GPU Rendering Visuals Explained

This document is a human-readable explanation for design discussion and system understanding. For implementation invariants and the compact source of truth, read `Docs/GpuRenderingVisuals.md`.

## Big Picture

Arena Shooter is visually built around readable neon silhouettes. The game can have dark rooms, black wall interiors, glowing pickups, many droids, projectiles, and a large shield dome. The outline system exists so important objects keep their identity even when the scene gets busy.

The core idea is simple: gameplay objects are grouped into visual categories, each category gets a rendering layer mask, and a URP renderer feature creates colored screen-space outlines from those masks.

The current art direction is clean neon silhouette first. The line should read like a thin glowing stroke with a restrained halo, not a hot fuzzy smear. Decorative detail lines can be added as separate authored geometry later; the shared outline pass is responsible for object readability and silhouette identity.

## The Category System

`DroidRenderSetup` is the central place that maps gameplay categories to rendering layer masks and colors.

The current categories are floor, wall, droid, medical, ammo, gun, and none. For example, walls receive the wall rendering layer and a blue outline, droids receive the droid rendering layer and a gold outline, and ammo uses a yellow outline.

This category assignment is more important than the object name once rendering begins. If a renderer has the wrong rendering layer mask, the outline pass will classify it incorrectly or ignore it.

## How An Object Gets Into The Outline System

Most generated objects pass through helper setup:

- Droids call into `DroidVisuals` and `DroidRenderSetup`.
- Arena geometry created by `ArenaGenerator` assigns wall or floor categories where appropriate.
- Pickups and resources are built by pickup visual helpers and assigned medical, ammo, or gun categories.
- Destructible wall pieces may create invisible outline proxy geometry so the outline remains stable while the body mesh changes.

`DroidRenderSetup` also disables shadows, light probes, and reflection probes for these stylized renderers. The game is leaning on explicit emissive/neon readability rather than realistic lighting.

## What The Renderer Feature Does

`DroidOutlineRendererFeature` plugs into URP. The class still has its older droid-oriented name so existing Unity renderer assets keep their feature reference, but its job is now general stylized neon outlining for all categories.

For each enabled outline band, it creates a mask pass after opaque rendering. That mask pass draws only renderers whose rendering layer mask matches the band. The mask shader stores enough information for the composite shader to find both silhouette edges and normal edges.

Then the composite pass runs before post-processing. It samples the current scene color, detects edges in the mask texture, applies distance scaling, and adds the category's outline glow into the scene. The composite shader separates alpha/silhouette edges from normal/internal edges, and also separates the crisp hard edge from the softer halo, so wall tops, wall side lines, and other category outlines can be tuned independently.

That means the outline is not a mesh outline around every object. It is a screen-space effect built from category masks.

## Why There Are Multiple Bands

Each band has a different color and rendering layer mask. The default bands are:

- Floor Purple
- Wall Blue
- Droid Gold
- Medical Red
- Ammo Yellow
- Gun Cyan

This makes the battlefield readable at a glance. Ammo and health resources should not visually blend into walls. Droids should remain distinct from the arena. Weapons should read differently from resource pickups.

Each band also has its own style profile. The profile controls hard-edge thickness, glow radius, glow strength, intensity, normal-edge contribution, and distance fade. Walls and pillars use a silhouette-first profile with very little normal-edge emphasis, because their broad outer shape matters more than every internal face. Droids, guns, and pickups can keep a little more normal-edge response so their forms still read in dark scenes.

The broader wall and pillar glow is now a different layer: small non-colliding neon base accents are generated where structural wall and corridor-opening pillar pieces touch the floor. Wall and pillar screen-space outlines now separate silhouette/alpha edges from normal/internal side lines, so top edges can be held back while the side-line cyan stays bright and close to the reference style. The bright broad glow is reserved for the floor-contact base accents.

## Distance Behavior

The composite shader scales outline radius and intensity with distance. Far objects still need to read, but a far wall or distant group of droids should not fill the screen with glow.

The important mental model is that nearby objects get a clear neon core and a small halo. Distant objects keep smaller, dimmer outlines so the scene stays legible instead of noisy.

## Silhouettes, Proxies, And Detail Lines

The silhouette outline is screen-space. It is generated from category masks and is best at camera-consistent object borders.

Destructible walls add one extra layer: they can create invisible outline proxy geometry. The proxy tells the outline pass what the surviving wall shape is after chunks are destroyed or collapse. That keeps the wall border following the remaining material without changing gameplay collision. When a proxy edge would overlap a valid shot/shatter torn contour, the proxy yields to the contour so clean straight chunk borders do not sit on top of jagged damage.

Damage contours are different. They are real generated geometry placed around shot/shatter holes. They are useful for torn-edge readability, but they are not the same thing as the screen-space silhouette outline. Wall silhouettes and wall damage contours use the same shared wall-line color source, but the systems stay separate so damage details can be toned down or replaced later without breaking wall silhouettes.

Other generated neon strips, floor lines, gate trims, wall/pillar base accents, pickup accents, and weapon beams come from `ArenaTheme` materials. Those materials now use the same cleaner cyan, magenta, and violet palette as the screen-space outlines, with lower emission values than the older hot-pink/high-HDR look.

## Presentation LOD

Large All Out War battles also use presentation LOD around soldiers. This sits above the outline shader and reduces visual/audio work for far or offscreen actors.

World health bars only update every frame when they are close and in the camera relevance area. Far or offscreen bars can hide or update at a slower cadence. Droid walk animation can skip frames for far/offscreen soldiers, and very far/offscreen poses may pause until the soldier becomes relevant again. Non-player footsteps are suppressed when the soldier is far enough away or outside the camera relevance area. Background AI-vs-AI shots can still apply gameplay damage and ammo cost without spawning visible beam/audio presentation that the player cannot see.

This visual LOD is not gameplay truth. If a far beam is suppressed, the shot still happened. If a far health bar is hidden, the soldier's health still changed. If walk animation updates less often, NavMesh movement and squad state still continue. Presentation LOD is shared visual optimization; All Out War droid tiering remains the system that decides tactical AI cadence.

For the broader performance story, read `Docs/DesignNotes/AllOutWarPerformanceLOD_Explained.md`.

## Droid Body And Animation

Droids currently prefer the imported `CyberBattleDroid` body when that model is valid and visibly renderable. The generated blaster arm and gun are still separate runtime presentation pieces, so the imported body is responsible for the torso, head, legs, and base body silhouette while `DroidBlasterRig` handles weapon aiming.

Imported droid bodies do not use the old procedural limb posing. That limb poser was written for the primitive fallback body, where the code owns every thigh, shin, knee, arm, and hand transform. The imported FBX has similarly named parts, and applying the primitive poser to those parts pulls the model apart. For imported droids, the current safe behavior is whole-body bob, lean, and crouch presentation only.

The procedural fallback body still keeps the old jointed limb animation path. That fallback matters if the imported model is missing, hidden, or rejected by the visibility checks.

The current project files do not contain a playable imported droid walk clip. `Assets/Models/CyberBattleDroid.blend` has no saved Blender actions, and Unity's imported `CyberBattleDroid.fbx` assets have no configured walk animation clips. A true imported-body walk cycle is future work: save or recreate a Blender action, export it into the FBX, configure Unity's clip import, then attach runtime playback to the imported body without disturbing the separate blaster rig.

## The Shield Dome

The shield dome is separate from the outline pipeline. `ShieldDomeBackdrop` procedurally builds curved unlit mesh bands, horizon glow bands, and faint diagonal/latitude seams. It uses generated geometry and unlit materials rather than the screen-space outline feature.

In All Out War, the dome's radius comes from the arena layout. It should visually match the arena footprint, but it is not the physical collision shell. The actual "do not leave the map" behavior comes from the invisible vertical boundary ring generated by the arena.

The dome's horizon and seam materials use the same cleaner cyan, magenta, and violet direction as the rest of the linework, but they remain faint backdrop geometry rather than hard gameplay silhouettes.

## Diagnostics

The outline system has intentionally detailed diagnostics. When playing, it can log shader discovery, pass decisions, renderer counts by category, per-band visibility, RenderGraph pass execution, and GPU readbacks.

These logs are useful when a category disappears. Typical failure patterns are:

- The renderer never received the right rendering layer mask.
- The shader was not found.
- The pass did not run for the active camera.
- The object is transparent or not in the expected render queue.
- The diagnostic band is filtering out everything except one category.

## Known Limitations

The outline system is tailored to URP. It is not a general-purpose renderer-agnostic solution.

Screen-space outlines depend on the camera view and mask textures. They are great for readability, but they are not gameplay truth. AI sight, projectile hits, collision, and pass-through logic should never depend on outline pixels.

The shield dome is visual only. If the player can leave the dome, the fix belongs in the arena boundary ring, not in dome mesh collision.

## Mental Model

Think of the visual stack as three layers:

1. Gameplay code builds objects and assigns visual categories.
2. GPU passes convert those categories into masks and outlines.
3. Generated meshes like the shield dome add world-scale atmosphere.

Most bugs happen when layer 1 fails to categorize an object, or when the renderer feature is not active for the camera that is actually drawing the game.
