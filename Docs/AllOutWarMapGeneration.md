# All Out War Map Generation

This file is the compact implementation contract and invariant source of truth for All Out War arena generation.

## Purpose

All Out War maps should feel like circular battlefields inside the shield dome. The generated map, continuous dome floor, dome backdrop, boundary ring, spawn arc-bands, resource placement, and runtime NavMesh all derive from one shared arena footprint.

The goal is a dense battlefield scaled by active combat pressure, not total roster size.

## Key Code Areas

- `Assets/Scripts/ArenaShooter/MatchController.cs` starts All Out War, derives battle-size settings, builds resources, bakes NavMesh, and spawns armies.
- `Assets/Scripts/ArenaShooter/ArenaGenerator.cs` builds the circular room graph, clearings, walls, corridors, open spawn zones, continuous dome floor, and invisible boundary ring.
- `Assets/Scripts/ArenaShooter/ArenaLayout.cs` stores circular arena metadata, room graph data, clearing groups, and `ArmySpawnRegion` arc-band data.
- `Assets/Scripts/ArenaShooter/ShieldDomeBackdrop.cs` builds the visible shield dome from the layout's dome radius.

## Arena Scale

All Out War map size should scale from active battlefield pressure:

- `BattlefieldCap` controls how large the active fight should feel.
- `SoldiersPerArmy` controls roster depth and match duration, not physical map size.
- `warGridRadius` is clamped to a compact range and drives room count, dome radius, continuous floor radius, and spawn perimeter radius.

## Invariants

- `ArenaLayout.CircularCenter`, `CircularRadius`, `DomeRadius`, and `PerimeterSpawnRadius` are the shared arena footprint values.
- The visible dome, invisible boundary ring, continuous dome floor, and perimeter spawn placement must stay aligned to the same center/radius model.
- The continuous dome floor must physically cover the full ground footprint inside the dome, including hidden pockets between rooms, corridors, and clearings.
- The physical boundary is a vertical ring at ground-level perimeter, not collision over the whole dome shell.
- All Out War spawn regions are arc-bands on the inner perimeter, not boxed rooms, long tunnels, or single points.
- Spawn arc-bands reserve geometry-free space: walls, doorway pillars, corridor side walls, obstacles, pickups, and raised floor borders must not intrude into them.
- Logical spawn rooms may remain in `ArenaLayout` for pathing, fronts, respawns, and resource exclusion.
- Clearings should open room groups by removing internal walls/corridor choke behavior.
- Resources should avoid spawn-reserved areas.
- Runtime NavMesh is baked once after generated colliders/geometry exist and before soldiers spawn.

## Generation Flow

At a high level:

1. `MatchController` reads All Out War settings and derives active map scale.
2. `ArenaGenerator.GenerateAllOutWar(...)` builds a circular allowed room grid.
3. Clearing groups are added and connected toward center.
4. Army spawn regions are placed around the perimeter.
5. Room floors, walls, corridors, open clearing links, and open spawn links are generated.
6. Continuous dome floor and invisible boundary ring are added.
7. `ShieldDomeBackdrop` builds the visible dome from the layout radius.
8. Runtime NavMesh is baked from generated arena physics colliders.
9. Match resources and healing stations are placed outside spawn-reserved zones.
10. Active soldiers spawn up to the battlefield cap.

## Known Boundaries

- The strategic map is still room-graph based even though movement uses NavMesh.
- Destructible wall holes do not trigger NavMesh rebakes.
- The dome is a visual shell. The boundary ring is the actual world-edge blocker.
- King of the Colosseum still uses gate/tunnel intro behavior. All Out War should not.

## Related Docs

- `Docs/DesignNotes/AllOutWarMapGeneration_Explained.md` is the narrative explanation for human/design understanding.
- `Docs/AllOutWarSquadAI.md` explains how squads use the generated layout after spawning.
