# All Out War Performance LOD Explained

This document is a human-readable explanation of the All Out War performance LOD upgrade. It describes why the system exists, what changed, and what it does differently from a full fake/background battle simulation.

## Big Picture

All Out War is meant to support larger fights than King of the Colosseum. A 30-soldier fight can often run acceptably with every soldier doing full work every frame. A 100-200 soldier fight is different.

The expensive part is not just drawing the droids. Each soldier can run AI updates, enemy list scans, line-of-sight raycasts, NavMesh path rebuilds, `CharacterController` movement, crouch and cover decisions, health bar updates, walk animation, footstep audio, gunfire audio, beam visuals, ammo use, and damage checks.

The V1 performance strategy is live-soldier LOD. Soldiers are still real objects in the world. They are not removed, replaced by abstract squad records, or simulated as invisible dice rolls. Instead, the game spends less frame-by-frame work and presentation work on soldiers the player is unlikely to notice moment by moment.

## Why This Strategy

There are three broad ways to improve performance:

1. Optimize existing live soldiers.
2. Convert far squads into true background simulation.
3. Rewrite major systems around ECS/DOTS.

V1 chooses the first path because it is the safest. It preserves the existing GameObject/MonoBehaviour architecture, keeps combat behavior real, and gives immediate performance gains without needing to solve hydration, reactivation, teleport correction, or fake battle believability.

True squad-level background simulation may still be useful later. ECS/DOTS may also be useful for much larger battles. But both are larger architectural shifts. Live-soldier LOD is the practical first step.

## The Three Soldier Tiers

All Out War droids classify themselves into three performance tiers.

`Full` is for soldiers that are near the player, recently inside the player camera's relevance area, recently damaged, tracking a remembered player target, or otherwise important right now. Full soldiers keep the normal feel: frequent target scans, line-of-sight checks, path rebuilds, cover/crouch choices, beam visuals, and normal presentation.

`Reduced` is mostly for soldiers in the middle distance: not close enough or camera-relevant enough to deserve `Full`, but still close enough that their behavior should remain reasonably responsive. Reduced soldiers still move, fight, heal, resupply, and follow squad objectives, but expensive checks are staggered.

`Background` is for soldiers that are far from the player, offscreen, and not in immediate player-relevant combat. Background soldiers remain active, but they scan less often, rebuild paths less often, share resources less frequently, skip cover/crouch micro-decisions when not seeing a target, suppress far beam presentation, and allow visual/audio helpers to do less work.

## Current Implementation Map

The upgrade is split between tactical AI tiering and shared presentation helpers.

`DroidController` owns the All Out War performance tier. That tier controls target scan cadence, current-target LOS cadence, path rebuild cadence, squad resource sharing cadence, background cover/crouch micro-decisions, and whether background AI shots ask `WeaponInventory` to show beam/audio presentation.

`WorldHealthBar`, `CombatantVisualWalkAnimator`, and `FootstepAudio` are presentation helpers. They use camera distance and viewport relevance to hide, throttle, or suppress far/offscreen visual and audio work. Viewport relevance is not an occlusion test; a soldier behind a wall can still be camera-relevant if it is inside the camera frustum. These helpers do not choose squad tactics and do not decide whether damage, ammo, or healing happened.

`WeaponInventory.TryFire(...)` can run gameplay fire logic without beam/audio presentation. That lets far background AI-vs-AI shots still spend ammo and apply raycast damage while avoiding visual effects the player cannot reasonably see.

So the mental split is: All Out War droid tiering is the tactical/performance driver; visual helpers are presentation LOD.

## What Still Remains Real

LOD does not change battlefield cap semantics. It does not despawn soldiers. It does not remove soldiers from squads. It does not replace a soldier's health, ammo, movement, or damage with a fake result.

A background soldier can still:

- belong to a squad
- consume squad objectives
- move through the battlefield
- see enemies when its throttled scan runs
- fire and spend ammo
- damage enemies
- take damage
- die
- heal or resupply
- report squad signals

The main difference is update frequency and presentation. A far soldier does not need to ask every frame, "Can I see every possible enemy right now?" A far AI-vs-AI shot does not need to spawn a beam visual if the player cannot notice it.

## AI Work That Gets Reduced

The biggest CPU savings come from reducing repeated per-soldier expensive work.

Target scans are staggered. Full soldiers can scan normally, but Reduced and Background soldiers wait longer between scans.

Line-of-sight checks are cached briefly by tier. These checks use raycasts, so doing fewer of them across many soldiers matters.

Path rebuilds are throttled. Soldiers still move along existing NavMesh paths, but far/background soldiers do not rebuild paths as aggressively.

Squad resource sharing checks happen less often for far/background soldiers. Squad inventory still exists, and resources can still be distributed; the system simply avoids asking every frame for every soldier.

Cover and crouch micro-decisions are reduced for background soldiers unless they are in visible combat. Squad-level movement remains the important behavior at that distance.

## Presentation Work That Gets Reduced

Presentation LOD handles work that affects what the player sees or hears, not gameplay truth.

World health bars hide or update more slowly when they are far/offscreen. A hidden health bar does not mean the soldier stopped taking damage.

Walk presentation can update less often for far/offscreen soldiers, and very far/offscreen poses may pause until the soldier becomes relevant again. The soldier can still move physically; only the visual helper work is reduced. For imported droid bodies, this currently means throttling whole-body bob, lean, and crouch presentation, not a true imported leg gait. For procedural fallback droids, the older jointed limb animation path still exists. When animation resumes, it samples movement over the elapsed interval and clamps pose advancement so the droid does not leap through a huge animation jump.

Non-player footstep audio is suppressed when far enough away or outside the camera relevance area. Nearby footsteps still work.

Background AI-vs-AI shots can suppress beam/audio presentation while still applying ammo cost, raycast hit logic, destructible damage, and combat damage. If the player gets close enough for that shot to matter visually, the soldier wakes into a more expensive tier.

## Why Visibility Alone Is Not Enough

The system does not use only "is this soldier visible on screen?" A soldier behind the player might still be close enough to shoot, be heard, or enter view immediately. A soldier far away but recently inside the camera relevance area should also not instantly drop to the cheapest tier.

That is why tiering considers distance, camera viewport relevance, recent camera relevance, current target memory, and recent damage/contact state. Camera-frustum relevance promotes a soldier to `Full`, not merely `Reduced`, because the player may be looking at that soldier or about to notice it. The goal is a relevance bubble, not a simple camera-culling rule. The current V1 relevance check is intentionally conservative and does not perform expensive occlusion checks just to choose a performance tier.

## Why This Is Better Than Immediate Fake Simulation

Fake offscreen simulation can be powerful, but it creates harder design problems:

- What happens when the player turns a corner and fake soldiers need to become real?
- Where exactly should each soldier appear?
- How do bullets, ammo, health, destructible walls, and resources stay believable?
- How do squads avoid seeming to teleport or resolve fights unfairly?

Live-soldier LOD avoids those problems for V1. The soldiers remain physically present, so reactivation is just a tier change. That makes it less dramatic architecturally and easier to trust while the squad AI is still evolving.

## Diagnostics

The code includes optional performance diagnostics for All Out War LOD. They are off by default.

When enabled, the diagnostics can report rough counts for:

- Full/Reduced/Background tier frames
- target scans
- line-of-sight checks
- path rebuilds
- beam visuals spawned

These counters are meant for debugging and tuning. They are not player-facing UI.

## Known Limitations

V1 still keeps all active soldiers as GameObjects. That means `Update()` still runs and `CharacterController` movement still exists for every active soldier.

The LOD tiers are heuristic. They are based on distance, camera viewport relevance, and recent contact rather than a full tactical importance model.

Background soldiers still use real per-soldier simulation, just less often. This is cheaper than full fidelity, but it is not as cheap as true squad-level background simulation.

Suppressed far beam visuals can make distant AI-vs-AI firefights less visually noisy, but if the player expects to watch a far battle through a long sightline, tier thresholds may need tuning.

## Future V2 Direction

If V1 is not enough for very large battles, the next step should be true squad-level background simulation.

In that version, far and fully irrelevant squads could be dehydrated into lightweight squad state:

- approximate squad position
- health ratio
- ammo ratio
- current decision
- contact pressure
- recent casualties or resource changes

When the player approaches, the squad would hydrate back into individual soldiers at plausible positions. That would be much cheaper, but also much harder to make believable.

ECS/DOTS remains a larger future option if the game eventually needs hundreds or thousands of simple soldiers. For the current project shape, live-soldier LOD is the better first scaling layer.

## Mental Model

Think of All Out War performance as a ladder:

1. Full-fidelity nearby soldiers.
2. Reduced-frequency live soldiers.
3. Background live soldiers with presentation suppressed.
4. Future squad-level abstract simulation.
5. Possible far-future ECS/DOTS rewrite.

The current upgrade implements steps 1-3. It is meant to let larger battles breathe without changing what a soldier fundamentally is.
