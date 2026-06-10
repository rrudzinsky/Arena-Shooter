# All Out War Map Generation

This file is the compact implementation contract and invariant source of truth for All Out War arena generation.

## Purpose

All Out War maps should feel like circular battlefields inside the shield dome. The generated map, continuous dome floor, dome backdrop, boundary ring, spawn arc-bands, resource placement, and runtime NavMesh all derive from one shared arena footprint.

The goal is a dense battlefield scaled by active combat pressure, not total roster size.

## Key Code Areas

- `Assets/Scripts/ArenaShooter/MatchController.cs` starts All Out War, derives battle-size settings, builds resources, bakes NavMesh, and spawns armies.
- `Assets/Scripts/ArenaShooter/ArenaGenerator.cs` builds the circular room graph, clearings, walls, corridors, open spawn zones, continuous dome floor, and invisible boundary ring.
- `Assets/Scripts/ArenaShooter/ArenaLayout.cs` stores circular arena metadata, room graph data, clearing groups, `ArmySpawnRegion` arc-band data, and generated tunnel route links.
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
- The `Hilly` map style must reserve three to five visible, high, traversable terrain-only hills on normal All Out War map sizes while preserving spawn-to-central-hub connectivity.
- Hilly terrain may occupy the exact center or central battlefield as long as a nearby buildable hub remains connected; hills must still avoid spawn regions and their immediate room buffer.
- Compact and medium Hilly hills are smaller in footprint/radius only; they must still read as real traversable hills, not low obstacles or pillar-like props.
- Hilly terrain-only hills may overlap at their outer shoulders to create double-hill formations, but their crest/core regions must remain separated and spawn-safe buildable paths must remain connected.
- Generated tunnels are battlefield shortcuts, not spawn tunnels. Subfloor tunnel endpoints are wall-owned portals on safe unused room sides, while hill-cut tunnel mouths belong to eligible hillside terrain; all sampled paths must avoid spawn arc-bands with extra safety padding.
- Tunnel route footprints must remain inside the dome radius, including subfloor ramp waypoints and tunnel-width padding.
- Hill-cut tunnels must keep the standard octagonal tunnel size and are only eligible when terrain-only hill surface is naturally tall and wide enough to contain that tunnel with roof clearance; they stay at surface floor height, do not descend below the map, use hillside-mouth entrance mode, support shoulder-overlapping compound hills when union clearance is continuous, cut a bore footprint through the hill terrain, and add colliding hex-patterned terrain collars so the upper hill and bored side faces remain physically and visually covered without stylized outlines.
- Subfloor tunnels must descend below the arena floor immediately after their portals, travel through underground bend waypoints, and re-emerge only at the far portal.
- Subfloor tunnels use wall-portal entrance mode. Their entrances are centered in safe unused room wall sides, not hallway-middle, clearing, or open-floor pads, and must avoid terrain-only hill boundary bands.
- Subfloor ramps and underground corridor spans must connect as one continuous route: the first underground span follows the entrance ramp direction, and the final underground span lines up with the exit ramp.
- Visual room floors that overlap subfloor portal/ramp footprints must be split around the cutout so the ramp mouth is not covered or z-fighting.
- Subfloor tunnel ramps require one-sided ramp-shaped dome-floor cutouts, colliding threshold landings, hex-patterned terrain rim collars without stylized outlines, and enclosed sloped octagonal ramp sleeves that meet the upright wall portal without triangular gaps; the underground middle stays covered by the dome floor.
- Tunnel geometry should be built as a continuous swept octagonal corridor with rounded bend samples, colliding floor and side-wall surfaces, and non-walkable visual roof/facet surfaces.
- Logical spawn rooms may remain in `ArenaLayout` for pathing, fronts, respawns, and resource exclusion.
- Clearings should open room groups by removing internal walls/corridor choke behavior.
- Resources should avoid spawn-reserved areas and tunnel portal/ramp footprints.
- Runtime NavMesh is baked once after generated colliders/geometry exist and before soldiers spawn.
- All Out War startup logs phase timings for map generation, terrain mesh creation, tunnel mesh creation, shield/lighting, NavMesh bake, pickups, and soldier setup.
- The continuous dome floor samples the terrain height field once and reuses those sampled vertices for both render and collider meshes; render/collider vertices must stay aligned.
- Flat terrain masks are spatially indexed after `BuildFlatMasks(...)` so terrain sampling only evaluates nearby room, corridor, spawn, structure, and tunnel masks. This is an optimization only: hill roundness, mesh resolution, tunnel bend sampling, and swept tunnel continuity must not be reduced for startup speed.

## Generation Flow

At a high level:

1. `MatchController` reads All Out War settings and derives active map scale.
2. `ArenaGenerator.GenerateAllOutWar(...)` builds a circular allowed room grid and reserves terrain-only hills for Hilly maps.
3. Clearing groups are added and connected toward center.
4. Army spawn regions are placed around the perimeter.
5. Safe tunnel route links are selected away from spawn regions.
6. Room floors, walls, corridors, open clearing links, and open spawn links are generated; subfloor wall portals add exact room-wall openings, while hill-cut tunnels do not.
7. Continuous dome floor, tunnel geometry, and invisible boundary ring are added.
8. `ShieldDomeBackdrop` builds the visible dome from the layout radius.
9. Runtime NavMesh is baked from generated arena physics colliders.
10. Match resources and healing stations are placed outside spawn-reserved zones and tunnel portal/ramp footprints.
11. Active soldiers spawn up to the battlefield cap.

## Known Boundaries

- The strategic map is still room-graph based even though movement uses NavMesh.
- Tunnel routes are extra graph links for fallback routing, but physical traversal still depends on the generated colliders and runtime NavMesh.
- Destructible wall holes do not trigger NavMesh rebakes.
- The dome is a visual shell. The boundary ring is the actual world-edge blocker.
- King of the Colosseum still uses gate/tunnel intro behavior. All Out War should not.

## Related Docs

- `Docs/DesignNotes/AllOutWarMapGeneration_Explained.md` is the narrative explanation for human/design understanding.
- `Docs/AllOutWarSquadAI.md` explains how squads use the generated layout after spawning.
