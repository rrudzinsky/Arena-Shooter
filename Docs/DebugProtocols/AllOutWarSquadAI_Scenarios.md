# All Out War Squad AI Debug Scenarios

Use these scenarios to manually test or reason about squad AI changes.

## Opening Fan-Out

Start All Out War with multiple armies and enough battlefield cap for several squads.

Expected:

- Squads leave the perimeter spawn region.
- Squads split by center, left, right, wide-left, wide-right, and reserve search vectors.
- Squadmates stay loosely together with formation offsets.
- Different squads do not all claim the same first objective.

## Stale Contact Cleanup

Let a support squad move toward a reported contact, then remove or hide the enemy before the support squad arrives.

Expected:

- The support squad resolves to `ContactLost` / `ResumeSearch`.
- `ObjectiveVersion` increments.
- All squad members abandon the stale support destination and resume vector search.

## Sustained Same-Contact Fire

Let two squads exchange fire for a while without deaths.

Expected:

- `ShotsExchanged` may refresh combat state.
- Support objective versions do not churn every burst.
- Support squads settle into their assigned route instead of repeatedly rerouting.

## Two Separate Fights

Create two different combat contacts far apart.

Expected:

- Squads already fighting one contact do not get pulled to support the other.
- Nearby squads can support their local fight.
- Reserve squads are only used as fallback.

## Dead Reserve Squad

Kill all members of a reserve squad, then trigger combat that needs support.

Expected:

- The dead reserve squad does not receive `Flank`, `Hold`, or `Collapse`.
- It does not consume the flanker role.
- It does not increment `ObjectiveVersion`.

## Reserve Fallback

Trigger combat with no nearby eligible non-reserve support.

Expected:

- One eligible reserve squad may respond.
- If the source squad is fixing the enemy and no flanker exists, the reserve becomes `Flank`.
- Otherwise the reserve receives the appropriate fallback role such as `Hold` or `Collapse`.
- Other reserves continue their current behavior.

## Split Squad

Separate one soldier from its squad while the rest of the squad is elsewhere, then trigger nearby combat by the isolated soldier.

Expected:

- One separated member should not drag the whole squad into support unless the squad centroid qualifies.
- If the split squad itself is the contact source, its own decision may still react to its member's event.

## Low Ammo During Combat

Let a squad fight until one or more members are low or empty on ammo.

Expected:

- If the squad still has ammo and visible enemies, combat can continue.
- If ammo is empty or critically low, squad decision can become `Resupply`.
- Carried ammo cells are distributed to nearby needy members.
- Resource collection does not reset an active unrelated combat decision unless logistics are urgent.

## Wounded Squad

Damage multiple members of a squad without killing them.

Expected:

- `HealthRatio` reflects wounded living members, not just deaths.
- Carried med packs are used on nearby wounded members.
- The squad can choose `Heal` or `Regroup` when health is critical.
- Healing runners are released once the need is gone.

## Resource Pickup During Combat

Let a squad member collect ammo or health during an active fight.

Expected:

- The pickup is banked into squad inventory.
- `ResourceFound` does not make the squad abandon combat by itself.
- The squad only changes to `Heal` or `Resupply` if squad logistics are urgent.

## Destructible Wall Visibility

Open a hole in a destructible wall between a support squad and an enemy.

Expected:

- Droid combat visibility sees through valid projectile pass-through holes.
- Support-contact visibility agrees with combat visibility.
- The squad does not falsely transition to `ContactLost` while it can see or shoot through the opening.

## Hidden Last Enemy

Leave one enemy alive and out of sight.

Expected:

- Squads do not idle permanently at the far boundary.
- Squads continue searching stale or unsearched rooms.
- Reserve and normal search squads continue coordinated coverage.
