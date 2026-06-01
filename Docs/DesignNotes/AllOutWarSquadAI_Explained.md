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

All Out War droids use NavMesh as a path of corners. The droid should keep advancing through intermediate corners until it reaches the final destination area. Intermediate corners are not treated like final stop points; the final stop distance only applies to the last leg of the route. This matters because a soldier that stops slightly short of an intermediate corner can look idle even though it technically has a path.

If movement gets stuck, the droid clears its cached path, enters a short recovery movement, and forces a fresh All Out War objective refresh. That refresh also invalidates the droid's consumed squad objective version, so it does not keep retrying the same dead destination. If the droid was a healing or ammo runner when it got stuck, it releases that runner state and lets the squad reevaluate.

The current NavMesh is baked once when the All Out War arena is generated. Destructible wall holes do not currently rebake the NavMesh.

## Performance LOD

All Out War soldiers also use a live performance LOD system so larger battles can stay playable. This is not fake battle simulation, background squad dehydration, or despawning. The soldiers remain real GameObjects with health, ammo, squad membership, NavMesh movement, damage, healing, resupply, and squad decisions.

The LOD system changes how often expensive individual-soldier work happens:

- `Full` soldiers are near the player, recently inside the player camera relevance area, recently damaged, remembering player contact, or otherwise immediately relevant. They keep normal target scanning, line-of-sight checks, path rebuilds, cover/crouch behavior, beam presentation, and resource sharing cadence.
- `Reduced` soldiers are still tactically relevant but less immediate, usually in the middle distance rather than close to or inside the player camera relevance area. They scan and rebuild paths less often, while still moving and fighting normally enough to keep the battle progressing.
- `Background` soldiers are far from the player and not immediately visible or player-relevant. They remain alive and active, but target scans, LOS checks, path rebuilds, resource sharing, cover/crouch micro-decisions, and beam visuals are throttled or suppressed.

This matters because a large All Out War match can have far more soldiers than the player can inspect at one time. The squad system still chooses real objectives and decisions; LOD only reduces individual update frequency and presentation work for soldiers the player is unlikely to notice moment by moment.

For the full explanation of this upgrade, read `Docs/DesignNotes/AllOutWarPerformanceLOD_Explained.md`.

## Public Squad State

Each squad publishes state so the rest of the squad system can reason about it.

`HealthRatio` summarizes squad condition. The current code treats every squad as four expected slots. For each living registered squad member, it calculates `CurrentHealth / MaxHealth`, then adds a small shield contribution: `(CurrentShield / MaxShield) * 0.25f`. That slot contribution is clamped between `0` and `1`.

If multiple living entries ever map to the same slot, the slot keeps the highest contribution. Dead, missing, unregistered, or not-yet-active slots contribute `0`. The final `HealthRatio` is the average of the four slot contributions:

```text
HealthRatio = (slot0 + slot1 + slot2 + slot3) / 4
```

This means a squad with four badly wounded soldiers is unhealthy even if nobody has died, and a squad missing members is also unhealthy because those missing slots count as zero.

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

## How Decisions Actually Drive Squads

Most squad decisions do not run a completely separate movement system. The shared pattern is:

1. `EvaluateAllOutWarSquadDecision(...)` chooses the squad's public `Decision` from health, ammo, signal, engagement scale, carried resources, runners, and contact state.
2. `AssignNextAllOutWarSquadObjective(...)` scores candidate rooms and picks one objective room for the squad.
3. `GetAllOutWarDecisionObjectiveScore(...)` adds decision-specific scoring bias on top of the normal search-vector scoring.
4. Each droid consumes that squad objective, applies its local slot offset, and moves toward the resulting point.
5. In All Out War, droids try NavMesh routing first and fall back to the older movement/path logic if needed.

Individual droids can still interrupt their movement for local combat, damage reactions, healing, resupply, crouching, aiming, and firing. The squad decision answers "what area should this squad be trying to operate around?" It does not replace every moment-to-moment soldier behavior.

### Search

`Search` is the default decision when no combat or urgent logistics state is active.

The squad has a persistent search vector derived from the army's perimeter spawn direction and the squad's sector. The objective scorer prefers rooms ahead of that vector, rooms farther from the spawn as the squad advances, stale or unsearched rooms, and rooms that are not already claimed by allied squads.

It penalizes rooms that are too far sideways from the search band, behind the push direction, recently searched by the same squad, or crowded by other squads. This is the main anti-clumping layer: squads are discouraged from choosing the same room or the same nearby area unless combat pressure makes that worthwhile.

The chosen room can update the squad's search phase. A squad usually starts in `Advance`, can shift to `Sweep` if it runs out of good forward rooms, uses `Reinforce` if it is a reserve squad, and can enter `Collapse` if enemy pressure is detected near a candidate room.

Once a room is chosen, squadmates do not all walk to the exact center. `GetAllOutWarSlotOffset(...)` offsets each member around the objective: one slightly forward, two to either side, and one slightly back. In game, `Search` should look like a small group moving through its vector lane while spreading locally around the same squad objective.

### Probe

`Probe` is usually chosen when a squad reports `EnemySpotted` and the engagement is not already heavy.

The code stores the contact in `SignalPosition`. During objective scoring, `Probe` biases the squad toward a point slightly short of that contact, behind the squad's search vector. It does not simply order the squad to run directly onto the enemy position.

In game, this should look like the squad investigating or pressing toward the contact while keeping some distance. If the contact turns into exchanged fire or damage, later reevaluation can turn the squad into `Fix`, `Hold`, `Regroup`, `Heal`, or `Resupply`.

### Fix

`Fix` is usually chosen when the squad reports `ShotsExchanged` or `TakingDamage`, unless the engagement scale is `HeavyEngagement` or `Overrun`.

Like `Probe`, it uses `SignalPosition` as the contact point, but it biases the objective farther back from the contact than `Probe` does. The idea is that this squad is holding enemy attention rather than trying to immediately overrun the enemy.

In game, a fixing squad should continue fighting around the contact area. It may shoot, crouch, seek small local movement, or react to damage through individual droid logic, but the squad objective should keep it from wandering away from the fight.

`Fix` also matters for squad-to-squad support. A squad that is fixing a contact can cause nearby allied squads, or one reserve fallback squad, to receive support decisions such as `Flank`, `Hold`, or `Collapse`.

### Flank

`Flank` is assigned by support logic to an allied squad responding to another squad's contact.

It uses the supported contact's `SignalPosition`. Objective scoring then biases the squad toward a side position around the contact using the squad's search tangent. Left and wide-left sectors prefer one side, right and wide-right sectors prefer the other, and center or reserve squads fall back to squad id parity.

In game, a flanking squad should not stack on the fixing squad's route. It should try to approach from a lateral angle. This still happens through room scoring and NavMesh movement, so it is not perfect tactical cover movement, but it is intentionally different from simply pushing straight at the same room.

When a support assignment materially changes, the squad's objective version increments. Each droid tracks the last consumed objective version, so all members of the support squad refresh their cached destination instead of only one member noticing the new order.

### Collapse

`Collapse` is usually chosen after `EnemyKilled` when the squad has enough health and ammo, or assigned as support when enemy pressure looks favorable.

If a signal position exists, `Collapse` biases objective scoring directly toward that contact. If no signal position exists, nearby enemy pressure around candidate rooms gives extra score. Compared with `Fix`, this is the aggressive push decision.

In game, a collapsing squad should move into the fight rather than holding short. If combat pressure disappears and there is no pending support contact, cleanup logic can clear stale `Collapse` behavior and return the squad to normal vector search.

### Hold

`Hold` is chosen when the contact is heavy, unclear, or pushing would overextend the squad.

In objective scoring, `Hold` biases toward the squad's current position. That means the squad should avoid taking a new aggressive room while the situation is dangerous or already being handled.

`Hold` is not an idle state. Individual droids can still aim, shoot, crouch, track visible enemies, react to damage, and do local path movement. The public squad decision simply says, "do not advance the squad objective right now."

### Regroup

`Regroup` is chosen when the squad is extremely weak, has an urgent logistics problem without a usable resource, lost members, or needs to gather before continuing.

For objective scoring, `Regroup` biases toward a point near the army's home side, slightly along the army push direction. It shares this conservative home-side bias with `Heal` and `Resupply`.

In game, regrouping should pull the squad away from aggressive contact objectives and toward a safer shared area. It is also the fallback when the squad is too damaged or empty on ammo but cannot immediately solve the problem with carried resources or a safe pickup.

`Regroup` clears back toward `Search` only when the underlying conditions recover: health is above the urgent threshold, ammo is no longer critically low or empty, no health or ammo runner is active, and no useful carried resource need remains.

### Heal

`Heal` is chosen when squad health is urgent enough to justify healing behavior. This can happen because the squad has critical wounded members, very low `HealthRatio`, an active health runner, carried med packs, or access to a safe healing resource.

If the squad has carried med packs, the system can distribute them to nearby wounded squadmates. This is abstract squad inventory sharing, not a physical item handoff animation.

If carried med packs do not solve the problem, an individual droid may seek a safe health pickup or healing station. While that runner or healing need remains active, the public squad decision should stay `Heal` instead of being prematurely cleared.

Healing stations are not player-only. Enemy soldiers and allied soldiers both use the same `CombatantHealth` healing path. Whether a soldier actually goes to a station is controlled by the All Out War survival logic.

The normal All Out War healing retreat is conservative. A health pickup or healing station must have no enemy close to the resource, and the route to it must not be considered dangerous. This prevents wounded soldiers from blindly crossing a fight for healing.

There is also a critical-health fallback. If a soldier is at or below roughly `22%` health, it can relax the route-danger requirement and attempt a healing retreat as long as the healing destination itself is not enemy-occupied. This is meant to prevent very low-health soldiers from hiding forever simply because the route safety test is too strict.

If a healing route later becomes unsafe, the droid clears its healing target, releases the squad's health runner marker, and returns to squad reevaluation/search or combat. If it reaches a healing station, the station attempts to heal it to full. Once healing is no longer needed, the runner state is cleared and the squad can leave `Heal`.

In room objective scoring, `Heal` uses the same conservative home-side bias as `Regroup`. In moment-to-moment droid logic, a healing runner may move directly toward the selected healing pickup or station using the existing movement pathing.

In game, `Heal` should mean the squad is trying to restore wounded members without every soldier randomly scattering for health. Once the healing need is gone and no runner is active, squad-level reevaluation can return the squad to search or combat behavior.

### Resupply

`Resupply` is chosen when one or more squad members are empty on ammo, or when the squad ammo ratio is low enough and the squad has carried ammo cells or a safe ammo pickup available.

If the squad has carried ammo cells, the system can distribute ammo to nearby low or empty squadmates. Like med packs, this is abstract squad inventory sharing through proximity.

If carried ammo does not solve the problem, an empty or low-ammo droid may run toward a safe ammo pickup. If the droid still has ammo and can see an enemy, local combat can continue instead of immediately abandoning the fight.

In room objective scoring, `Resupply` uses the same conservative home-side bias as `Regroup`. In individual movement, an ammo runner may use a direct safe pickup target. This means the public squad posture says "we need ammo," while the actual pickup run can still be handled by the soldier most affected.

In game, `Resupply` should make ammo a squad logistics issue, not a reason for every member to independently scatter.

### ResumeSearch

`ResumeSearch` is chosen when support or contact cleanup resolves a stale contact as `ContactLost`.

The stale contact position can briefly bias objective scoring so the squad finishes checking the area. Then the normal cleanup path clears the signal and engagement scale and returns the decision to `Search`.

This decision is important because support orders are temporary. If a support squad reaches a contact area and there is no real visible enemy or active contact pressure, it should not stay in `Flank`, `Hold`, or `Collapse` forever.

When contact is lost, the squad objective version increments. That forces all squadmates to abandon stale cached support destinations and consume the new cleanup/search objective.

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

The current individual survival flow works like this:

1. If the droid is above the low-health threshold, it clears healing survival state and returns to normal behavior.
2. If it is already retreating for healing, it keeps going only while the destination is still valid.
3. If the squad has carried med packs, the droid asks the squad system to reevaluate and distribute resources instead of independently running away.
4. If no carried med pack solves the problem, the droid tries to claim the squad's health-runner slot and find a safe pickup or station.
5. If the droid is critically wounded, it can accept a route that is not perfectly safe as long as the actual healing destination is not occupied by enemies.
6. If no healing route is acceptable, the droid resumes squad movement or combat rather than freezing in a special healing state.

Only one health runner is normally allowed per squad. This keeps the whole squad from scattering toward different health resources. If the runner gets stuck, finishes healing, or loses its target, that runner marker is released and the squad reevaluates.

## Known Limitations

The system still uses generated rooms as strategic objectives. NavMesh improves physical routing, but tactical objective choice is not yet a full spatial influence-map or utility-AI system.

NavMesh is baked once at match start. Destroyed wall holes do not currently cause a NavMesh rebake.

Squad communication is still simple. Squads share public state, but they do not yet have a deeper planner that reasons about all allied squads as a complete battlefield formation.

Reserve behavior is intentionally conservative. Future versions may add more explicit reserve doctrines, such as defending rear areas, reinforcing weak sectors, or hunting late-game hidden enemies.

Resource sharing is abstracted through squad inventory and proximity. There is no detailed carrier identity, item handoff animation, or player-style inventory UI for AI squads.

Healing route safety is intentionally imperfect. The code uses enemy counts near the healing destination and near the route, not a full tactical risk map. Critical-health soldiers are allowed to be more desperate, but they still should not run to a healing resource that enemies are physically occupying.

All Out War AI has optional debug logs for healing, resupply, and stuck recovery, but they are off by default. They are intended for future debugging sessions, not normal gameplay.

## Mental Model

Think of the current system as three layers:

1. Squad strategy picks a posture and objective.
2. NavMesh and movement code get each soldier there.
3. Individual combat code handles aiming, firing, cover, survival, and local spacing.

Most bugs happen when those layers disagree. For example, the squad changes objective but a droid keeps a cached destination, or an individual resource event overwrites a squad combat decision. That is why the compact contract and debug checklist focus so heavily on decision ownership, objective versioning, and cleanup.

The newest movement cleanup specifically protects against the second layer getting stuck. If a droid stalls on a NavMesh corner, healing route, ammo route, or search route, the droid clears local path state and asks for a fresh squad objective instead of silently standing still.
