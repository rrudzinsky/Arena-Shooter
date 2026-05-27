# All Out War Squad AI Explained

This document is a human-readable explanation for design discussion and system understanding. For implementation invariants and the compact source of truth, read `Docs/AllOutWarSquadAI.md`.

## Big Picture

All Out War is meant to feel like armies spreading across a generated battlefield instead of a normal wave shooter. The important idea is that the squad is the tactical unit.

Individual soldiers still do soldier-level things: they aim, shoot, move along NavMesh paths, crouch, seek cover, use formation offsets, and react to immediate danger. But the larger question of "where should this group go?" belongs to the squad system in `MatchController`.

That split matters because individual anti-clump behavior can easily make soldiers scatter or pile into one doorway. Squad logic should instead make a whole squad search, probe, fix, flank, hold, regroup, heal, or resupply together.

## Match Start And Squad Formation

At the start of All Out War, active soldiers spawn up to `Soldiers on battlefield at one time`. Total soldiers per army are the roster/reserve count, not the number of soldiers spawned simultaneously.

Those active soldiers spawn across a short arc-band along the inner perimeter of the dome. They should not emerge from one point, a boxed spawn room, or a long tunnel. The arc-band gives each army a small curved staging area inside the dome wall, with enough thickness for formation rows.

After spawning, soldiers are grouped into squads by spawn index:

- `squadId = spawnIndex / AllOutWarSquadSize`
- `slotIndex = spawnIndex % AllOutWarSquadSize`

The current squad size is four. A squad is expected to behave like a small fireteam: members share a broad objective but keep local offsets so they are not standing on top of one another.

## Opening Search

Each squad receives a persistent search direction based on its army's perimeter spawn direction. This is how squads fan out instead of all pushing down the same center route.

The repeated sector pattern is:

- Squad 0: center
- Squad 1: left
- Squad 2: right
- Squad 3: wide-left
- Squad 4: wide-right
- Squad 5: reserve
- Then repeat.

The squad objective chooser scores rooms using that search vector. It prefers objectives that are ahead of the squad, farther from spawn as the squad advances, stale or unsearched, and not already claimed by allied squads. Combat pressure and resource needs can temporarily change those priorities.

## NavMesh Versus Squad Logic

Squad logic chooses the strategic target. NavMesh chooses the physical route.

For example, the squad system might decide, "Squad 2 should probe a room to the right of the contact." NavMesh then helps each soldier walk through the actual generated geometry to reach that target without being forced through room-center or doorway-center graph paths.

The current NavMesh is baked once when the All Out War arena is generated. Destructible wall holes do not currently rebake the NavMesh.

## Public Squad State

Each squad publishes state so the rest of the squad system can reason about it.

`HealthRatio` summarizes squad condition. Dead or missing members count as zero, and badly wounded living members pull the number down. This is why a squad with four nearly dead soldiers is still considered weak even if everyone is technically alive.

`Signal` is the latest unresolved event. It answers, "What just happened to this squad?" Examples include enemy spotted, shots exchanged, taking damage, ally killed, enemy killed, contact lost, and resource found.

`EngagementScale` describes how intense the current contact appears to be. It ranges from no contact, to probe contact, to skirmish, firefight, heavy engagement, and overrun.

`Decision` is the squad's current posture. It answers, "What is the squad trying to do right now?" Examples include search, probe, fix, flank, collapse, hold, regroup, heal, resupply, and resume search.

## What The Decisions Mean

`Search` means the squad is following its search vector and looking for enemies or stale areas to sweep.

`Probe` means the squad has spotted something uncertain and is moving cautiously around that contact.

`Fix` means the squad is exchanging fire and trying to hold enemy attention.

`Flank` means the squad is supporting another squad's fixed contact by approaching from a side angle.

`Collapse` means the squad has enough advantage or enemy weakness to push toward the contact.

`Hold` means the squad should avoid overextending because the situation is unclear, heavy, or already being handled.

`Regroup` means the squad is weak, separated, out of useful resources, or otherwise needs to gather before continuing.

`Heal` means squad health is urgent enough to use carried med packs or seek a safe healing resource.

`Resupply` means squad ammo is low enough to use carried ammo cells or seek a safe ammo pickup.

`ResumeSearch` means the squad checked or lost a contact and should transition back into normal search.

## What Happens When A Squad Spots An Enemy

When a soldier sees an enemy, it reports `EnemySpotted` to its squad. The squad evaluator updates the squad's public state. Usually that becomes `Probe` unless the engagement already looks too dangerous, in which case the squad may hold.

Nearby allied squads may also be considered for support. Local non-reserve squads are considered first. Reserve squads are fallback support, not automatic participants in every fight.

## What Happens When Shots Are Exchanged

When a squad fires at a visible enemy, it reports `ShotsExchanged`. This often turns the squad into `Fix`, meaning it is holding the enemy's attention.

If a squad is fixing an enemy, the support system may try to assign one flanking squad. Repeated shots at the same contact should not constantly reroute support squads; support objective versioning is throttled so squads can settle into their route.

## What Happens When A Squad Takes Damage

Taking damage reports `TakingDamage`. Depending on health, enemy count, and support, the squad may keep fixing, hold, regroup, heal, or resupply.

Damage also gives individual soldiers immediate local context, such as facing the attacker or refreshing a known target. The squad decision still belongs to `MatchController`.

## What Happens When A Soldier Dies

The dead soldier's squad receives `AllyKilled`. If the attacker is another All Out War droid, the attacker squad can receive `EnemyKilled`.

An ally death can push a squad toward `Regroup` if the squad is weak or the engagement is heavy. An enemy kill can push healthy squads toward `Collapse`.

## Support Squads And Reserves

Support squads are chosen carefully because careless support assignment causes armies to collapse into one giant ball.

The current intended order is:

1. Consider nearby non-reserve squads whose squad centroid is near the contact.
2. Skip squads with no living members.
3. Skip squads that are weak, healing, resupplying, regrouping, or fighting a different contact.
4. Assign support roles such as `Flank`, `Hold`, or `Collapse`.
5. If local support is missing, consider one eligible reserve squad as fallback.

Reserve squads are not meant to join every fight. They exist to fill gaps, support uncovered areas, or provide fallback help when local squads cannot.

## Objective Versions

Squads have an `ObjectiveVersion`. Each droid remembers the last version it consumed.

This solves a subtle problem: if only one soldier notices a squad objective change, the other squadmates might keep walking to an old cached destination. Whenever the squad's objective changes in a squad-wide way, `ObjectiveVersion` increments so every member knows to refresh.

`ObjectiveAssignedVersion` records which version the current squad objective belongs to. This lets one droid select the new objective for the squad, while other droids follow the same squad objective instead of each one rolling a new target.

## Contact Lost And Cleanup

Support contact is temporary. If a support squad reaches or probes the contact area and no enemy is truly visible or active there, the squad should resolve the contact as stale.

When that happens:

- `Signal` becomes `ContactLost`
- `EngagementScale` becomes `None`
- `Decision` becomes `ResumeSearch`
- The search phase moves toward sweeping
- `ObjectiveVersion` increments

That last part is important. It tells all squad members to stop following the stale support destination.

## Logistics And Shared Resources

All Out War soldiers use limited ammo. Ammo pickups matter because squads can run low or empty.

AI squads can bank resources:

- Ammo pickups become carried squad ammo cells.
- Health pickups become carried squad med packs.

These resources are shared at the squad level. The game does not simulate a detailed handoff animation. Instead, if a squadmate is nearby and needs healing or ammo, the squad can spend a carried resource on that member.

Resource pickup itself should not interrupt a fight. `ResourceFound` is an inventory event. A squad should only switch to `Heal` or `Resupply` if the squad's health or ammo situation is urgent enough.

## What Happens When Ammo Runs Low

If a squad has low ammo but still has enough shots and visible enemies, it can keep fighting.

If one or more members are empty or the squad ammo ratio is too low, the squad can choose `Resupply`. It may use carried ammo cells or send a runner toward a safe ammo pickup.

Empty soldiers should not stand around uselessly if safe ammo exists, but they also should not blindly run through danger for ammo.

## What Happens When Health Is Low

If squad health becomes critical, the squad can choose `Heal` or `Regroup`.

If the squad has carried med packs, it can distribute them to nearby wounded members. If not, a soldier may seek a safe healing pickup or healing station. If healing is unsafe or impossible, the squad may regroup instead.

## Known Limitations

The system still uses generated rooms as strategic objectives. NavMesh improves physical routing, but tactical objective choice is not yet a full spatial influence-map or utility-AI system.

NavMesh is baked once at match start. Destroyed wall holes do not currently cause a NavMesh rebake.

Squad communication is still simple. Squads share public state, but they do not yet have a deeper planner that reasons about all allied squads as a complete battlefield formation.

Reserve behavior is intentionally conservative. Future versions may add more explicit reserve doctrines, such as defending rear areas, reinforcing weak sectors, or hunting late-game hidden enemies.

Resource sharing is abstracted through squad inventory and proximity. There is no detailed carrier identity, item handoff animation, or player-style inventory UI for AI squads.

## Mental Model

Think of the current system as three layers:

1. Squad strategy picks a posture and objective.
2. NavMesh and movement code get each soldier there.
3. Individual combat code handles aiming, firing, cover, survival, and local spacing.

Most bugs happen when those layers disagree. For example, the squad changes objective but a droid keeps a cached destination, or an individual resource event overwrites a squad combat decision. That is why the compact contract and debug checklist focus so heavily on decision ownership, objective versioning, and cleanup.
