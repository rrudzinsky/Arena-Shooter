# Destructible Arena Blocks Explained

This document is a human-readable explanation for design discussion and system understanding. For implementation invariants and the compact source of truth, read `Docs/DestructibleArenaBlocks.md`.

## Big Picture

Destructible arena blocks are meant to make the battlefield feel physical. Walls, floors, and pillars are not just static cover. They can be shot, damaged, and opened enough for projectiles to pass through.

The system is not a full voxel engine. It is a practical destructible-piece system: a generated wall or floor can be divided into chunks, individual chunks can be destroyed, and the visible mesh is rebuilt to show the missing material.

## When A Piece Becomes Destructible

`ArenaGenerator` decides which generated objects should receive `DestructibleArenaPiece`. The component usually sits on arena walls, wall door segments, pillars, corridor walls, and some floor pieces.

The destructible piece does not immediately rebuild itself when it is created. It initializes lazily the first time it takes damage. This avoids paying chunk setup cost for every destructible wall before anything has been shot.

## What Happens On First Damage

On first damage, the component:

1. Finds the source size and intact material.
2. Disables original child renderers and colliders.
3. Adds a new surface box collider.
4. Builds a local chunk grid.
5. Creates a combined body mesh renderer.
6. Creates damage contour rendering.
7. Creates an invisible outline source for wall outlines when needed.

After this point, the piece is controlled by generated meshes rather than the original cube or imported model.

## Chunks And Damage

Each destructible piece is split into a local grid of chunks. When a projectile hits, the system finds the chunk closest to the hit point, marks it destroyed, applies neighbor shatter damage, then rebuilds the combined mesh.

Destroyed chunks disappear from the body mesh. Intact chunks still draw their visible surfaces. The collider remains a broad surface collider, while projectile pass-through is handled by checking whether the hit chunk is already destroyed.

## Holes, Stamps, And Contours

The neon outline around a hole is generated from damage stamps. A stamp is a jagged 2D shape projected onto a damage plane.

When multiple neighboring chunks are destroyed, their stamps are union-clipped so the system draws one connected torn outline instead of a stack of overlapping circles. Small interior contour islands are removed so a 2x2 square hole does not leave a tiny floating bit in the middle.

For normal walls, the system also creates an opposite stamp on the back side of the wall and bridges the interior. That is what makes the hole read as a through-hole, not only a front-face decal.

## Why Mixed-Side Hits Needed A Fix

The important wall rule is that flat wall chunks use one canonical damage plane. It should not matter whether the last shot came from the front or the back.

Before this rule, three chunks destroyed from the front and one chunk destroyed from the back could be treated as separate contour components. That made the visual union fail, leaving weird central material in square holes.

Now flat walls resolve their damage plane from the wall's thin local axis and use a stable signed normal. Back-side hits still get impact debris from the hit side, but the wall hole merges with neighboring wall damage on the same canonical plane.

## Why Pillars Are Different

Corner pillars use `CornerPillar` damage. Pillars are not just flat wall slabs, so the side they are hit from matters visually. A pillar hit on the left side should bite from that side, not always from the same canonical face.

That is why corner pillars keep side-specific bite direction while flat walls do not.

## Projectile Pass-Through

`AllowsProjectilePassThrough(...)` is the gameplay bridge between destruction and shooting. If a projectile hits an already destroyed chunk or a missing damaged area, the destructible piece can report that the projectile should pass through.

This matters for player shots, AI shots, and support-contact visibility through wall holes. It does not mean the NavMesh changes. Soldiers do not automatically path through destroyed wall holes unless a future system rebakes or updates navigation.

## Visual Relationship To Outlines

Destructible pieces participate in the neon visual style in two ways.

The damage contour mesh uses bright wall/floor outline colors so holes are readable. Wall pieces may also create an invisible outline proxy so the outline renderer still sees the wall category even when the visible body mesh is being rebuilt.

The actual damage contour is geometry, not a screen-space decal. The broader category outline is handled by the GPU outline system.

## Known Limitations

The system rebuilds meshes when chunks are damaged, so very high destruction density can become expensive.

The collider is not a perfect per-chunk collision mesh. Projectile pass-through checks compensate for shooting, but character navigation does not become fully voxel-aware.

NavMesh is baked once for All Out War. Destroyed holes do not make new AI walking routes in the current implementation.

## Mental Model

Think of a destructible wall as three connected systems:

1. A chunk grid decides what material still exists.
2. Damage stamps draw readable torn edges around missing chunks.
3. Projectile pass-through lets shots use the new openings.

Most bugs happen when those systems disagree, especially when neighboring destroyed chunks do not share the same damage plane.
