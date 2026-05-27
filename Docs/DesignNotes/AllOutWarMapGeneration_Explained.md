# All Out War Map Generation Explained

This document is a human-readable explanation for design discussion and system understanding. For implementation invariants and the compact source of truth, read `Docs/AllOutWarMapGeneration.md`.

## Big Picture

All Out War is supposed to feel like a full battlefield inside a shield dome. It is not a linear wave arena with a spawn tunnel. The map should fill the circular space inside the dome, armies should start on the inner perimeter, and the dome edge should be the visible edge of the world.

The map generation system creates that battlefield in layers: layout first, geometry second, visuals and collision around it, NavMesh after generated arena colliders exist, then resources and spawning.

## How Map Size Is Chosen

All Out War map size is based on active battle pressure.

`Soldiers on battlefield at one time` controls the physical scale because it determines how many active units are fighting at once. `Soldiers per army` controls reserves and match duration, not map size.

`MatchController` calculates an active battle size from the battlefield cap and army count, then derives a clamped `warGridRadius`. That radius drives the room count and the circular arena footprint.

This keeps high-roster, low-cap battles from becoming huge empty maps.

## The Shared Arena Footprint

The generated layout stores one shared circular definition:

- `CircularCenter`
- `CircularRadius`
- `DomeRadius`
- `PerimeterSpawnRadius`

Those values are the contract between generation, visuals, collision, spawning, and resources. If one system uses a different radius, the game starts to feel wrong: the dome may not line up with the boundary, the safety floor may not cover the arena, or spawns may drift away from the wall.

## Building The Room Graph

`ArenaGenerator.GenerateAllOutWar(...)` builds a circular room graph. It starts with allowed grid cells inside a circular radius, then turns those cells into layout rooms.

The result is still a room graph, but the footprint is circular. This gives the tactical systems a simple room structure while the overall battlefield reads as a dome-contained arena.

## Clearings

Clearings are groups of nearby rooms that are treated as more open space. Internal walls between rooms in the same clearing are skipped, and corridors between clearing rooms become open floor links rather than narrow hallway chokepoints.

Clearings matter because All Out War should not feel like every squad is always funneling through one hallway. They create larger fight spaces and let squad search vectors breathe.

## Perimeter Spawns

Each army gets an `ArmySpawnRegion` around the perimeter. This region is not supposed to be a physical room.

The actual spawn shape is an arc-band: a short curved section along the inner dome perimeter with some radial thickness. Active soldiers spawn across that arc-band in rows and columns, facing inward.

The logical spawn room still exists in `ArenaLayout` because AI fronts, pathing anchors, respawns, and resource exclusion need a room identity. But the physical space should stay open. Walls, doorway pillars, corridor side walls, raised floor borders, pickups, and obstacles should not intrude into the reserved arc-band.

## Walls, Corridors, And Open Links

Normal rooms get floors and walls. If adjacent rooms are connected, doorway segments and pillars are generated around the opening.

All Out War adds exceptions:

- Spawn rooms and anything intersecting spawn arc-bands skip wall/chute geometry.
- Corridors crossing spawn open zones become floor-only.
- Clearing links skip internal walls and use broader floor connections.
- Edge rooms can open outward toward the dome wall so the map does not become a sealed tunnel network.

This keeps the map structured without turning every perimeter spawn into a boxed room.

## Dome, Boundary, And Safety Floor

The visible dome is built by `ShieldDomeBackdrop` using the layout's dome radius. It creates colored curved bands, horizon glow, and faint hex-like seams.

The physical edge blocker is separate. `ArenaGenerator` creates an invisible circular boundary ring from box-collider segments positioned around the dome base. This is a cheap vertical wall ring, not a full dome collision shell.

The safety floor disk uses the same center and radius model so the player cannot fall through small uncovered gaps at the arena edge.

## Resources

All Out War pickups and healing stations are placed after layout generation. They avoid spawn-reserved arc-band positions.

The counts are based on active battle scale, army count, room count, and arena radius. This keeps resources relevant without flooding smaller maps.

Ammo matters because All Out War soldiers and the player use limited ammo. Health and ammo resources also feed into squad logistics.

## Runtime NavMesh

After geometry and colliders exist, All Out War bakes a runtime NavMesh from physics colliders. This happens before soldiers spawn.

The NavMesh is for physical routing. It does not choose squad tactics. Squad AI decides objectives; NavMesh helps droids walk through actual generated space instead of blindly following room centers and doorway centers.

The NavMesh is currently baked once. Destructible wall holes do not trigger rebakes.

## What Happens At Match Start

The high-level flow is:

1. Read All Out War settings.
2. Compute grid radius from battlefield cap.
3. Generate the circular layout, geometry, safety floor, and boundary.
4. Build dome visuals and lights.
5. Bake runtime NavMesh.
6. Create player at army 0 perimeter spawn.
7. Place pickups and healing resources.
8. Initialize army fronts and rosters.
9. Spawn active soldiers up to the battlefield cap.
10. Start replacement spawning, pickup refill, and victory checks.

## Known Limitations

The strategic structure is still a room graph. NavMesh improves movement, but map generation and squad objective selection still reason in rooms.

The map does not currently use a full spatial influence map, terrain heatmap, or cover analysis pass.

Destroyed walls do not affect NavMesh walkability after the initial bake.

The dome boundary is intentionally a vertical ring. It blocks walking out at ground level but does not simulate collision over the whole curved dome surface.

## Mental Model

Think of the All Out War arena as one circular contract shared by several systems:

1. Layout defines the circle, rooms, clearings, and spawn arc-bands.
2. Geometry turns that layout into floors, walls, corridors, and open zones.
3. Dome, boundary, resources, NavMesh, and spawning all attach to the same layout.

Most map-generation bugs happen when one of those layers forgets the shared circular contract.
