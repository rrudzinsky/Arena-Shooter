# All Out War Squad AI

This file is the compact implementation contract and invariant source of truth for All Out War squad AI.

## Purpose

All Out War treats squads as the tactical unit. Individual soldiers aim, fire, path, take cover, and apply local formation offsets, but squad-level movement and public tactical posture belong to the squad system in `MatchController`.

The goal is for armies to spawn at the dome perimeter, fan out by squad search vectors, search stale or unvisited areas, react to contact, share limited logistics resources, and support nearby allied squads without collapsing into one crowd.

## Core Concepts

- A squad is currently four active slots, assigned by `squadId = spawnIndex / AllOutWarSquadSize` and `slotIndex = spawnIndex % AllOutWarSquadSize`.
- Squads publish four main tactical fields:
  - `HealthRatio`
  - `Signal`
  - `EngagementScale`
  - `Decision`
- Squad logistics also track ammo ratio, wounded members, empty-ammo members, carried med packs, carried ammo cells, and current health or ammo runners.
- Squad movement uses a persistent search vector derived from the army's perimeter spawn direction.
- NavMesh is used for physical routing. Squad strategy still chooses objectives; NavMesh only answers how to walk there.

## Public Squad State

`Signal` is the latest unresolved event:

- `None`
- `EnemySpotted`
- `ShotsExchanged`
- `TakingDamage`
- `AllyKilled`
- `EnemyKilled`
- `ContactLost`
- `ResourceFound`

`EngagementScale` summarizes combat intensity:

- `None`
- `ProbeContact`
- `Skirmish`
- `Firefight`
- `HeavyEngagement`
- `Overrun`

`Decision` is the squad's current tactical posture:

- `Search`
- `Probe`
- `Fix`
- `Flank`
- `Collapse`
- `Hold`
- `Regroup`
- `Heal`
- `Resupply`
- `ResumeSearch`

## Invariants

- `MatchController` is the authority for squad-level `Decision`.
- Individual droids may report events, request logistics, and consume squad objective versions, but they must not directly overwrite squad tactical state.
- Any squad-wide objective change must increment `ObjectiveVersion`.
- Each droid tracks the last squad objective version it consumed so all squad members refresh after support or cleanup changes.
- `ObjectiveAssignedVersion` records which squad objective was selected for the current version, so one droid can choose the squad objective and the rest can follow it.
- `ContactLost` / `ResumeSearch` is a squad-wide objective change and must increment `ObjectiveVersion`.
- A squad must have living members before it can receive `Flank`, `Hold`, or `Collapse`.
- Reserve squads are fallback support, not automatic participants in every combat call.
- A squad already fighting a different contact must not be reassigned to support another fight.
- Repeated same-contact `ShotsExchanged` should not churn objective versions.
- Resources can be collected opportunistically, but `ResourceFound` must not interrupt active combat unless logistics are urgent.
- Combat visibility and support-contact visibility should agree, including destructible wall pass-through.

## Opening Search

At match start, squads spread by search vectors instead of all pushing to the same doorway or room:

- Squad 0: center
- Squad 1: left
- Squad 2: right
- Squad 3: wide-left
- Squad 4: wide-right
- Squad 5: reserve
- Then repeat.

Squad objectives prefer rooms aligned with the squad search vector, farther from spawn as the squad advances, unsearched or stale rooms, and uncrowded areas. Combat and resource decisions can temporarily override search.

## Combat Support

When one squad reports combat contact, nearby allied squads may support:

- Local non-reserve squads are considered first.
- Reserve squads are considered only as fallback.
- A `Fix` contact can request one flanker.
- A kill/contact advantage can call for `Collapse`.
- Unclear or heavy support usually becomes `Hold`.

Support assignment must preserve these protections:

- No dead or empty squads.
- No weak squads that should regroup.
- No squads in `Heal`, `Resupply`, or `Regroup`.
- No squads already fighting a different contact.
- No repeated objective churn for the same support contact.

## Logistics

All Out War AI squads use limited ammo and squad-carried resources.

- Ammo pickups become carried squad ammo cells.
- Health pickups become carried squad med packs.
- Carried resources are distributed to nearby needy squadmates.
- A squad may choose `Heal`, `Resupply`, or `Regroup` based on squad-level health, ammo, resource availability, and contact state.
- Opportunistic pickup collection should not pull squads out of combat unless health or ammo is critical.

## Contact Cleanup

Support contact is temporary. If a support squad reaches or probes a stale contact area and has no real visible contact, it should transition to:

- `Signal = ContactLost`
- `EngagementScale = None`
- `Decision = ResumeSearch`
- `Phase = Sweep`

That cleanup must increment `ObjectiveVersion` so all squad members abandon stale support destinations and resume vector search promptly.

## Related Docs

- `Docs/DesignNotes/AllOutWarSquadAI_Explained.md` is the narrative explanation for human/design understanding.
- `Docs/DebugProtocols/AllOutWarSquadAI_DebugChecklist.md` is the review/debugging checklist.
- `Docs/DebugProtocols/AllOutWarSquadAI_Scenarios.md` lists manual break-test scenarios.
