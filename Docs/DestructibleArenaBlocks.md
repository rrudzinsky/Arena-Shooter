# Destructible Arena Blocks

This file is the compact implementation contract and invariant source of truth for destructible arena walls, floors, pillars, and damage-hole visuals.

## Purpose

Arena Shooter's arena geometry should feel physically breakable without replacing every wall with expensive dynamic mesh simulation. `DestructibleArenaPiece` converts selected arena pieces into chunk grids, destroys chunks when shot, rebuilds the remaining visible mesh, and draws neon damage contours around holes.

The goal is readable tactical destruction: shots should open real projectile pass-through holes, damage should look like connected torn material, and mixed front/back wall shots should merge into clean holes.

## Key Code Areas

- `Assets/Scripts/ArenaShooter/DestructibleArenaPiece.cs` owns chunking, damage, mesh rebuilds, pass-through checks, damage contours, and debris.
- `Assets/Scripts/ArenaShooter/ArenaGenerator.cs` decides which generated cubes/asset pieces receive `DestructibleArenaPiece`.
- `Assets/Scripts/ArenaShooter/DroidRenderSetup.cs` provides wall/floor outline colors for damage contours and outline proxies.
- `Assets/Shaders/InvisibleOutlineProxy.shader` supports invisible geometry used as an outline source for destructible walls.

## Damage Profiles

`DestructibleDamageProfile` currently defines:

- `Wall`: flat walls use a canonical wall plane so front/back hits merge into one through-hole.
- `Floor`: floor damage uses the top plane.
- `CornerPillar`: pillars keep side-specific bite direction so corner damage still reads from the hit side.

## Invariants

- Destructible pieces initialize lazily on first damage.
- Original renderers and colliders are disabled during initialization.
- A new surface `BoxCollider` represents the destructible piece's collision volume.
- Chunks are indexed in local space and capped per axis to avoid unbounded mesh complexity.
- Destroyed chunks are removed from the visible body mesh.
- Neighbor shatter may destroy nearby chunks, but should preserve the same damage plane as the source chunk.
- Normal wall chunks must use a canonical plane normal, independent of whether the last hit came from the front or back face.
- Flat wall holes should generate both front and opposite/back contours from the same stamp data.
- Corner pillars may keep side-specific contour orientation.
- `AllowsProjectilePassThrough(...)` should return true when a projectile hits an already destroyed chunk or empty damaged area.

## Damage Contours

Damage contours are not arbitrary decals. They are generated from jagged 2D stamps projected onto the selected damage plane. Neighboring destroyed chunks with the same normal key are grouped, their stamp edges are union-clipped, small interior islands are removed, and front/back contours are bridged for wall holes.

This is why canonical wall normals matter: if neighboring destroyed chunks disagree about their damage plane, they cannot merge into one clean contour component.

## Known Boundaries

- NavMesh is not rebuilt when walls are destroyed.
- Destruction changes projectile pass-through behavior, but not long-term AI walkability.
- The system is chunk/grid based, not true voxel simulation.
- Damage contours are visual/readability geometry, not separate gameplay colliders.

## Related Docs

- `Docs/DesignNotes/DestructibleArenaBlocks_Explained.md` is the narrative explanation for human/design understanding.
- `Docs/GpuRenderingVisuals.md` explains the outline and contour color system used by destructible visuals.
