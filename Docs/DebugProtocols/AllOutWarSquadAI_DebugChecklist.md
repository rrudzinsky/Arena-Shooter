# All Out War Squad AI Debug Checklist

Use this checklist when reviewing or debugging All Out War squad AI.

## Required Review Flow

1. Read `Docs/AllOutWarSquadAI.md`.
2. Inspect current code paths in `MatchController` and `DroidController`.
3. Review invariants before looking for style issues.
4. Run `dotnet build "Arena Shooter.slnx"`.
5. Report findings by severity with file and line references.

## Decision Ownership

- Verify `EvaluateAllOutWarSquadDecision` remains the authority for squad tactical decisions.
- Verify droids report events or needs rather than directly setting squad `Decision`.
- Search for direct writes to `Decision`, `Signal`, `SupportContactPending`, `ObjectiveVersion`, and `ObjectiveAssignedVersion`.
- Confirm logistics helpers do not reset a whole squad to `Search` from an individual droid path.

## Objective Refresh

- Every squad-wide objective change must increment `ObjectiveVersion`.
- `ObjectiveAssignedVersion` must be updated when the squad selects a new objective.
- Every droid must compare its consumed objective version to the squad version before using a cached `searchDestination`.
- `ContactLost`, support assignment, support cleanup, and major decision changes should be checked for stale cached destinations.

## Search Behavior

- Squad search should be vector-based, not doorway-lane based.
- Squads should prefer unsearched or stale rooms in their sector.
- Squads should not all claim the same objective unless combat pressure justifies it.
- Reserve squads should reinforce gaps or fallback support needs, not replace normal search squads.

## Combat Support

- Support candidates must have living members.
- Local non-reserve support should be considered before reserve support.
- Reserve support should be limited to the missing fallback role unless a future rule explicitly broadens it.
- A squad already fighting a different contact must not be reassigned.
- Same-contact support updates should not churn `ObjectiveVersion`.
- Support contact must resolve to `ContactLost` / `ResumeSearch` when stale.

## Contact And Visibility

- Support-contact visibility should match droid combat visibility.
- Destructible wall holes that allow projectile pass-through should not falsely block support contact.
- Raw distance to an enemy is not enough to keep contact alive.
- Contact cleanup should use real visible/contact pressure.

## Logistics

- Squads should always be allowed to collect extra ammo and med packs.
- `ResourceFound` is an inventory event, not a combat command.
- `Heal`, `Resupply`, and `Regroup` should override combat only when logistics are urgent enough.
- Health and ammo runners must be released when objectives end, resources are collected, or needs disappear.
- Resource distribution should use squad inventory plus proximity, not individual permanent ownership.

## Ammo And Health

- All Out War soldiers should use limited ammo.
- Empty or very-low-ammo squads should seek ammo or use carried ammo cells.
- Wounded squads should use carried med packs or safe healing resources.
- A squad with multiple badly wounded living members should still have low `HealthRatio`.

## Multi-Squad Risks

Look specifically for these bug shapes:

- One squad member refreshes but other squadmates keep stale destinations.
- One separated member causes the whole squad to support.
- A dead reserve squad consumes the flanker role.
- Repeated `ShotsExchanged` causes support objective churn.
- Two separate fights steal squads from each other.
- Resource pickup during combat resets a squad decision incorrectly.
- Contact lost by one member leaves other squad members in support mode.

## Build And Report

Always run:

```powershell
dotnet build "Arena Shooter.slnx"
```

In the final report, include:

- Confirmed bugs first.
- Severity and gameplay consequence.
- File and line references.
- Whether the build passed.
- Any residual risk or manual scenario that still needs playtesting.
