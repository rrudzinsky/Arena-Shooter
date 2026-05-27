using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ArenaShooter
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CombatantHealth))]
    [RequireComponent(typeof(WeaponInventory))]
    public sealed class DroidController : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 3.4f;
        [SerializeField] private float runSpeed = 5.1f;
        [SerializeField] private float turnSpeed = 420f;
        [SerializeField] private float preferredRange = 9.5f;
        [SerializeField] private float visionRange = 85f;
        [SerializeField] private float aimSpreadDegrees = 6.5f;
        [SerializeField] private float lastKnownPlayerMemory = 7f;

        private const float AllOutWarHealPickupSearchRadius = 30f;
        private const float AllOutWarHealResourceDangerRadius = 8f;
        private const float AllOutWarHealRouteDangerRadius = 4.5f;
        private const int AllOutWarAmmoSeekThreshold = 9;
        private const float AllOutWarAmmoPickupSearchRadius = 34f;
        private const float AllOutWarAmmoResourceDangerRadius = 8f;
        private const float AllOutWarAmmoRouteDangerRadius = 4.5f;

        private enum SquadRole
        {
            CrouchingRifle,
            StandingRifle,
            Flanker
        }

        private enum TacticalSurvivalState
        {
            None,
            WaitForShield,
            RetreatForHeal,
            Resupply,
            LastStand
        }

        private readonly List<Vector2Int> path = new();
        private readonly List<CombatantHealth> targetCandidates = new();
        private NavMeshPath navMeshPath;
        private Vector3[] navMeshCorners = System.Array.Empty<Vector3>();
        private int navMeshCornerIndex;
        private bool hasNavMeshPath;
        private MatchController match;
        private ArenaLayout layout;
        private Transform player;
        private Transform playerAnchor;
        private CharacterController controller;
        private CombatantHealth health;
        private WeaponInventory weapons;
        private DroidBlasterRig blasterRig;
        private SpawnIntroWalker introWalker;
        private DroidCrouchPose crouchPose;
        private Vector3 targetPoint;
        private Vector3 lastPosition;
        private float repathAt;
        private float verticalVelocity;
        private float nextCrouchDecisionAt;
        private float crouchUntil;
        private float nextBurstAt;
        private float stuckTimer;
        private float recoveryUntil;
        private float playerKnownUntil;
        private float nextSearchPickAt;
        private float nextCoverCheckAt;
        private float coverUntil;
        private float underFireUntil;
        private float damagePressure;
        private float nextSurvivalAssessmentAt;
        private float waitForShieldUntil;
        private float lastHealRunScore;
        private Vector3 recoveryDirection;
        private Vector3 lastKnownPlayerPosition;
        private Vector3 searchDestination;
        private Vector3 coverDestination;
        private Vector3 healingDestination;
        private Vector3 allOutWarHomePosition;
        private HealingStation healingStationTarget;
        private HealthPickup healthPickupTarget;
        private AmmoPickup ammoPickupTarget;
        private TacticalSurvivalState survivalState;
        private SquadRole squadRole;
        private int allOutWarTeamId = -1;
        private int allOutWarObjectiveSeed;
        private int allOutWarRoleIndex;
        private int allOutWarSquadId;
        private int allOutWarSlotIndex;
        private int allOutWarObjectiveVersion = -1;
        private bool autonomousWarMode;
        private bool allOutWarForceObjectiveRefresh;
        private bool hasLastPosition;
        private bool wasIntroWalking;
        private bool wasSeeingPlayer;
        private CombatantHealth lastReportedSquadTarget;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<CombatantHealth>();
            weapons = GetComponent<WeaponInventory>();
            blasterRig = GetComponent<DroidBlasterRig>();
            introWalker = GetComponent<SpawnIntroWalker>();
            crouchPose = GetComponent<DroidCrouchPose>();
            navMeshPath = new NavMeshPath();
            if (health != null)
            {
                health.Damaged += OnDamaged;
            }

            ArenaNoise.PlayerNoise += OnPlayerNoise;
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
            }

            ArenaNoise.PlayerNoise -= OnPlayerNoise;
        }

        public void Configure(MatchController owner, ArenaLayout arenaLayout, Transform playerTransform, int wave, int squadMemberIndex = 0)
        {
            match = owner;
            layout = arenaLayout;
            player = playerTransform;
            playerAnchor = playerTransform;
            crouchPose = GetComponent<DroidCrouchPose>();
            targetPoint = transform.position;
            lastKnownPlayerPosition = playerTransform != null ? playerTransform.position : transform.position;
            squadRole = (SquadRole)(Mathf.Abs(squadMemberIndex) % 3);
            walkSpeed += wave * 0.08f;
            runSpeed += wave * 0.1f;
            aimSpreadDegrees = Mathf.Max(2.8f, aimSpreadDegrees - wave * 0.35f);
            if (squadRole == SquadRole.Flanker)
            {
                preferredRange += 2.8f;
                runSpeed += 0.45f;
            }
        }

        public void ConfigureAllOutWar(MatchController owner, ArenaLayout arenaLayout, int teamId, Vector3 homePosition, int objectiveSeed, int squadId, int slotIndex)
        {
            match = owner;
            layout = arenaLayout;
            player = null;
            playerAnchor = null;
            crouchPose = GetComponent<DroidCrouchPose>();
            targetPoint = transform.position;
            searchDestination = transform.position;
            lastKnownPlayerPosition = transform.position;
            autonomousWarMode = true;
            allOutWarTeamId = Mathf.Max(0, teamId);
            allOutWarHomePosition = homePosition;
            allOutWarObjectiveSeed = Mathf.Abs(objectiveSeed);
            allOutWarRoleIndex = allOutWarObjectiveSeed % 5;
            allOutWarSquadId = Mathf.Max(0, squadId);
            allOutWarSlotIndex = Mathf.Max(0, slotIndex);
            allOutWarObjectiveVersion = -1;
            squadRole = (SquadRole)(allOutWarObjectiveSeed % 3);
            preferredRange += allOutWarRoleIndex == 2 ? 2.2f : allOutWarRoleIndex == 3 ? -1.4f : 0f;
            runSpeed += allOutWarRoleIndex == 0 || allOutWarRoleIndex == 2 ? 0.35f : 0f;
            aimSpreadDegrees = 3.2f;
            nextSearchPickAt = 0f;
        }

        private void Update()
        {
            if (introWalker == null)
            {
                introWalker = GetComponent<SpawnIntroWalker>();
            }

            if (player == null)
            {
                player = playerAnchor;
            }

            if (match == null || layout == null || (!autonomousWarMode && playerAnchor == null) || health == null || !health.IsAlive || !match.IsMatchActive)
            {
                return;
            }

            if (autonomousWarMode)
            {
                match.TryDistributeAllOutWarSquadResources(health, transform.position);
            }

            var eye = transform.position + Vector3.up * 0.68f;
            var activeTarget = FindBestVisibleTarget(eye);
            if (!autonomousWarMode && activeTarget == null)
            {
                activeTarget = match.GetBestEnemyTarget(health, transform.position);
            }
            else if (autonomousWarMode && activeTarget != null && activeTarget != lastReportedSquadTarget)
            {
                lastReportedSquadTarget = activeTarget;
                match.ReportAllOutWarSquadSignal(health, MatchController.AllOutWarSquadSignal.EnemySpotted, activeTarget.transform.position, activeTarget);
            }

            if (autonomousWarMode)
            {
                if (activeTarget != null)
                {
                    player = activeTarget.transform;
                }
                else if (Time.time >= playerKnownUntil)
                {
                    player = null;
                }
            }
            else
            {
                player = activeTarget != null ? activeTarget.transform : playerAnchor;
            }

            if (player == null)
            {
                if (!autonomousWarMode)
                {
                    return;
                }

                if (TryUpdateAllOutWarResupplyState(false, lastKnownPlayerPosition + Vector3.up * 0.64f))
                {
                    hasLastPosition = true;
                    lastPosition = transform.position;
                    return;
                }

                if (TryUpdateSurvivalState(false, lastKnownPlayerPosition + Vector3.up * 0.64f))
                {
                    hasLastPosition = true;
                    lastPosition = transform.position;
                    return;
                }

                MoveAllOutWarFrontSearch();
                hasLastPosition = true;
                lastPosition = transform.position;
                return;
            }

            var playerEye = player.position + Vector3.up * 0.64f;
            var toPlayer = playerEye - eye;
            var flatToPlayer = Flatten(player.position - transform.position);
            var canSeePlayer = CanSeePlayer(eye, playerEye, toPlayer);
            if (autonomousWarMode && !canSeePlayer && Time.time >= playerKnownUntil)
            {
                match.ReportAllOutWarSquadSignal(health, MatchController.AllOutWarSquadSignal.ContactLost, lastKnownPlayerPosition, null);
                lastReportedSquadTarget = null;
                player = null;
                blasterRig?.ClearAim();
                if (TryUpdateAllOutWarResupplyState(false, lastKnownPlayerPosition + Vector3.up * 0.64f))
                {
                    hasLastPosition = true;
                    lastPosition = transform.position;
                    return;
                }

                if (TryUpdateSurvivalState(false, lastKnownPlayerPosition + Vector3.up * 0.64f))
                {
                    hasLastPosition = true;
                    lastPosition = transform.position;
                    return;
                }

                MoveAllOutWarFrontSearch();
                hasLastPosition = true;
                lastPosition = transform.position;
                return;
            }

            if (canSeePlayer || Time.time < playerKnownUntil)
            {
                blasterRig?.AimAt(canSeePlayer ? playerEye : lastKnownPlayerPosition + Vector3.up * 0.64f);
            }
            else
            {
                blasterRig?.ClearAim();
            }

            if (introWalker != null && introWalker.IsWalking)
            {
                wasIntroWalking = true;
                if (introWalker.AllowsCombat && canSeePlayer)
                {
                    lastKnownPlayerPosition = player.position;
                    playerKnownUntil = Time.time + lastKnownPlayerMemory;
                    FaceTowards(playerEye);
                    if (Time.time >= nextBurstAt)
                    {
                        nextBurstAt = Time.time + Random.Range(0.28f, 0.56f);
                        FireSloppyBurst(eye, toPlayer);
                    }
                }

                return;
            }

            if (wasIntroWalking)
            {
                wasIntroWalking = false;
                ForceHuntTarget();
            }

            if (TryUpdateSurvivalState(canSeePlayer, playerEye))
            {
                hasLastPosition = true;
                lastPosition = transform.position;
                return;
            }

            if (TryUpdateAllOutWarResupplyState(canSeePlayer, playerEye))
            {
                hasLastPosition = true;
                lastPosition = transform.position;
                return;
            }

            UpdateCrouch(canSeePlayer);

            if (canSeePlayer)
            {
                lastKnownPlayerPosition = player.position;
                playerKnownUntil = Time.time + lastKnownPlayerMemory;
                FaceTowards(playerEye);
                if (Time.time >= nextBurstAt)
                {
                    nextBurstAt = Time.time + Random.Range(0.28f, 0.56f);
                    FireSloppyBurst(eye, toPlayer);
                }
            }

            if (flatToPlayer.magnitude > preferredRange || !canSeePlayer)
            {
                var huntDestination = GetHuntDestination(canSeePlayer);
                UpdatePath(huntDestination, 0.28f);
                MoveAlongPath(huntDestination, canSeePlayer ? walkSpeed : runSpeed, canSeePlayer ? preferredRange * 0.82f : 0.75f);
            }
            else if (ShouldMoveToCover(canSeePlayer))
            {
                UpdatePath(coverDestination, 0.22f);
                MoveAlongPath(coverDestination, squadRole == SquadRole.Flanker ? runSpeed : walkSpeed, 0.65f);
            }
            else
            {
                StrafeAndHoldRange(flatToPlayer);
            }

            hasLastPosition = true;
            lastPosition = transform.position;
        }

        private CombatantHealth FindBestVisibleTarget(Vector3 eye)
        {
            match.CollectEnemyTargets(health, targetCandidates);
            CombatantHealth best = null;
            var bestScore = float.PositiveInfinity;
            for (var i = 0; i < targetCandidates.Count; i++)
            {
                var candidate = targetCandidates[i];
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                var aimPoint = candidate.transform.position + Vector3.up * 0.64f;
                var toTarget = aimPoint - eye;
                if (toTarget.magnitude > visionRange || !CanSeeTarget(candidate, eye, aimPoint, toTarget))
                {
                    continue;
                }

                var distance = Flatten(candidate.transform.position - transform.position).sqrMagnitude;
                var priority = candidate.GetComponent<PlayerTurret>() != null ? -10f : 0f;
                var score = distance + priority;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private void ForceHuntTarget()
        {
            if (autonomousWarMode)
            {
                if (player != null && Time.time < playerKnownUntil)
                {
                    lastKnownPlayerPosition = player.position;
                    searchDestination = lastKnownPlayerPosition;
                }
                else
                {
                    player = null;
                    playerKnownUntil = 0f;
                    allOutWarForceObjectiveRefresh = true;
                    searchDestination = ChooseAllOutWarSearchDestination(true);
                }

                nextSearchPickAt = Time.time + Random.Range(8f, 13f);
                ClearCurrentPath();
                repathAt = 0f;
                recoveryUntil = 0f;
                stuckTimer = 0f;
                return;
            }

            lastKnownPlayerPosition = player != null ? player.position : playerAnchor != null ? playerAnchor.position : allOutWarHomePosition != Vector3.zero ? allOutWarHomePosition : transform.position;
            playerKnownUntil = Time.time + lastKnownPlayerMemory;
            searchDestination = lastKnownPlayerPosition;
            nextSearchPickAt = 0f;
            ClearCurrentPath();
            repathAt = 0f;
            recoveryUntil = 0f;
            stuckTimer = 0f;
        }

        private Vector3 GetHuntDestination(bool canSeePlayer)
        {
            if (canSeePlayer || Time.time < playerKnownUntil)
            {
                return lastKnownPlayerPosition;
            }

            if (autonomousWarMode)
            {
                var squadObjectiveVersion = match != null ? match.GetAllOutWarSquadObjectiveVersion(health) : -1;
                var squadObjectiveNeedsRefresh = squadObjectiveVersion >= 0 && squadObjectiveVersion != allOutWarObjectiveVersion;
                if (!squadObjectiveNeedsRefresh &&
                    Time.time < nextSearchPickAt &&
                    (searchDestination - transform.position).sqrMagnitude > 2.2f)
                {
                    return searchDestination;
                }

                var forceNew = allOutWarForceObjectiveRefresh ||
                    (searchDestination - transform.position).sqrMagnitude <= 2.2f;
                allOutWarForceObjectiveRefresh = false;
                nextSearchPickAt = Time.time + Random.Range(8f, 14f);
                searchDestination = ChooseAllOutWarSearchDestination(forceNew);
                if (squadObjectiveVersion >= 0)
                {
                    allOutWarObjectiveVersion = squadObjectiveVersion;
                }

                return searchDestination;
            }

            if (Time.time < nextSearchPickAt && (searchDestination - transform.position).sqrMagnitude > 1.4f)
            {
                return searchDestination;
            }

            nextSearchPickAt = Time.time + Random.Range(0.85f, 1.4f);
            searchDestination = ChooseSearchDestination();
            return searchDestination;
        }

        private void MoveAllOutWarFrontSearch()
        {
            var huntDestination = GetHuntDestination(false);
            UpdatePath(huntDestination, 0.42f);
            MoveAlongPath(huntDestination, runSpeed, 0.85f);
        }

        private Vector3 ChooseSearchDestination()
        {
            if (autonomousWarMode)
            {
                return ChooseAllOutWarSearchDestination();
            }

            if (player == null || layout == null)
            {
                player = playerAnchor;
                if (player == null)
                {
                    return transform.position;
                }
            }

            var currentRoom = layout.GetNearestRoom(transform.position);
            var playerRoom = layout.GetNearestRoom(player.position);
            var pathToPlayer = layout.FindPath(currentRoom, playerRoom);
            if (pathToPlayer.Count > 0 && layout.TryGetDoorwayPoint(currentRoom, pathToPlayer[0], out var doorway))
            {
                return doorway;
            }

            if (pathToPlayer.Count > 0 && layout.TryGetCenter(pathToPlayer[0], out var pathCenter))
            {
                return pathCenter;
            }

            var neighbors = layout.GetConnectedNeighbors(currentRoom);
            if (neighbors.Count > 0)
            {
                var best = neighbors[Random.Range(0, neighbors.Count)];
                var bestDistance = float.PositiveInfinity;
                foreach (var neighbor in neighbors)
                {
                    if (!layout.TryGetCenter(neighbor, out var center))
                    {
                        continue;
                    }

                    var distance = (center - player.position).sqrMagnitude;
                    if (distance < bestDistance || Random.value < 0.18f)
                    {
                        bestDistance = distance;
                        best = neighbor;
                    }
                }

                if (layout.TryGetDoorwayPoint(currentRoom, best, out var neighborDoorway))
                {
                    return neighborDoorway;
                }

                if (layout.TryGetCenter(best, out var neighborCenter))
                {
                    return neighborCenter;
                }
            }

            return player.position;
        }

        private Vector3 ChooseAllOutWarSearchDestination(bool forceNewObjective = false)
        {
            if (match == null || layout == null || health == null)
            {
                return transform.position;
            }

            return match.GetAllOutWarFrontObjective(health, allOutWarTeamId, allOutWarSquadId, allOutWarSlotIndex, transform.position, forceNewObjective);
        }

        private void FireSloppyBurst(Vector3 eye, Vector3 toPlayer)
        {
            if (weapons == null || !weapons.HasWeapon)
            {
                return;
            }

            var shotOrigin = blasterRig != null && blasterRig.Muzzle != null ? blasterRig.Muzzle.position : eye;
            var targetPoint = player != null ? player.position + Vector3.up * 0.64f : eye + toPlayer;
            var shotDirection = targetPoint - shotOrigin;
            if (shotDirection.sqrMagnitude < 0.01f)
            {
                shotDirection = toPlayer;
            }

            var noisyDirection = Quaternion.Euler(
                Random.Range(-aimSpreadDegrees, aimSpreadDegrees),
                Random.Range(-aimSpreadDegrees, aimSpreadDegrees),
                0f) * shotDirection.normalized;

            if (weapons.TryFire(shotOrigin, noisyDirection) && autonomousWarMode && player != null)
            {
                var targetHealth = player.GetComponentInParent<CombatantHealth>();
                match.ReportAllOutWarSquadSignal(health, MatchController.AllOutWarSquadSignal.ShotsExchanged, player.position, targetHealth);
            }
        }

        private bool ShouldMoveToCover(bool canSeePlayer)
        {
            if (!canSeePlayer)
            {
                return false;
            }

            if (Time.time < coverUntil)
            {
                return true;
            }

            if (Time.time < nextCoverCheckAt)
            {
                return false;
            }

            nextCoverCheckAt = Time.time + Random.Range(1.25f, 2.1f);
            var wantsCover = squadRole == SquadRole.CrouchingRifle ? 0.42f : squadRole == SquadRole.Flanker ? 0.32f : 0.22f;
            if (Random.value > wantsCover || !TryPickCoverDestination(out coverDestination))
            {
                return false;
            }

            coverUntil = Time.time + Random.Range(0.85f, 1.6f);
            return true;
        }

        private bool TryPickCoverDestination(out Vector3 destination)
        {
            destination = transform.position;
            if (layout == null || player == null)
            {
                return false;
            }

            var currentRoom = layout.GetNearestRoom(transform.position);
            var neighbors = layout.GetConnectedNeighbors(currentRoom);
            if (neighbors.Count == 0)
            {
                return false;
            }

            var awayFromPlayer = Flatten(transform.position - player.position).normalized;
            var bestScore = float.NegativeInfinity;
            foreach (var neighbor in neighbors)
            {
                if (!layout.TryGetDoorwayPoint(currentRoom, neighbor, out var doorway))
                {
                    continue;
                }

                var toDoor = Flatten(doorway - transform.position);
                if (toDoor.sqrMagnitude < 0.1f)
                {
                    continue;
                }

                var score = Vector3.Dot(toDoor.normalized, awayFromPlayer) + Random.Range(-0.25f, 0.25f);
                if (squadRole == SquadRole.Flanker)
                {
                    var side = Mathf.Abs(Vector3.Dot(toDoor.normalized, Vector3.Cross(Vector3.up, awayFromPlayer)));
                    score += side * 0.7f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    destination = doorway + toDoor.normalized * 0.9f;
                }
            }

            return bestScore > float.NegativeInfinity;
        }

        private bool TryUpdateSurvivalState(bool canSeePlayer, Vector3 targetEye)
        {
            if (health == null || health.MaxHealth <= 0f)
            {
                return false;
            }

            damagePressure = Mathf.MoveTowards(damagePressure, 0f, Time.deltaTime * 0.35f);
            var healthRatio = health.CurrentHealth / health.MaxHealth;
            if (healthRatio > 0.38f)
            {
                if (autonomousWarMode && survivalState == TacticalSurvivalState.RetreatForHeal)
                {
                    ClearHealingTargets();
                    match?.ReevaluateAllOutWarSquadDecision(health, transform.position);
                }

                survivalState = TacticalSurvivalState.None;
                nextSurvivalAssessmentAt = 0f;
                return false;
            }

            if (autonomousWarMode)
            {
                return TryUpdateAllOutWarSurvivalState(canSeePlayer, targetEye);
            }

            if (survivalState == TacticalSurvivalState.RetreatForHeal)
            {
                return TryUpdateHealingObjective(canSeePlayer, targetEye);
            }

            if (survivalState == TacticalSurvivalState.WaitForShield)
            {
                var targetTooClose = match != null && match.CountNearbyDroidTargets(transform.position, 5.8f, health) > 0;
                var shieldUseful = health.MaxShield > 0f && health.CurrentShield / health.MaxShield >= 0.38f;
                if (!targetTooClose && Time.time < waitForShieldUntil && !shieldUseful)
                {
                    UpdateCrouch(true);
                    HoldCoverOrScan(canSeePlayer, targetEye);
                    return true;
                }

                survivalState = TacticalSurvivalState.None;
                nextSurvivalAssessmentAt = Time.time;
            }

            if (survivalState == TacticalSurvivalState.LastStand && Time.time < nextSurvivalAssessmentAt)
            {
                UpdateCrouch(canSeePlayer);
                if (canSeePlayer)
                {
                    FaceTowards(targetEye);
                    StrafeAndHoldRange(Flatten(player.position - transform.position));
                }
                else
                {
                    HoldCoverOrScan(canSeePlayer, targetEye);
                }

                return true;
            }

            if (nextSurvivalAssessmentAt <= 0f)
            {
                nextSurvivalAssessmentAt = Time.time + Random.Range(2f, 3.4f);
                return false;
            }

            if (Time.time < nextSurvivalAssessmentAt)
            {
                return false;
            }

            nextSurvivalAssessmentAt = Time.time + Random.Range(3f, 4.8f);
            lastHealRunScore = CalculateHealRunScore(out var shouldWaitForShield);
            if (lastHealRunScore >= 48f && TryBeginHealingRetreat())
            {
                survivalState = TacticalSurvivalState.RetreatForHeal;
                return TryUpdateHealingObjective(canSeePlayer, targetEye);
            }

            if (shouldWaitForShield)
            {
                survivalState = TacticalSurvivalState.WaitForShield;
                waitForShieldUntil = Time.time + Random.Range(2.1f, 3.6f);
                HoldCoverOrScan(canSeePlayer, targetEye);
                return true;
            }

            survivalState = TacticalSurvivalState.LastStand;
            HoldCoverOrScan(canSeePlayer, targetEye);
            return true;
        }

        private bool TryUpdateAllOutWarSurvivalState(bool canSeePlayer, Vector3 targetEye)
        {
            if (survivalState == TacticalSurvivalState.RetreatForHeal)
            {
                if (!HasSafeAllOutWarHealingDestination())
                {
                    ClearHealingTargets();
                    return ResumeAllOutWarFrontOrCombat(canSeePlayer);
                }

                return TryUpdateHealingObjective(canSeePlayer, targetEye);
            }

            if (match.IsAllOutWarSquadCarryingMedPacks(health))
            {
                match.ReevaluateAllOutWarSquadDecision(health, transform.position);
                return ResumeAllOutWarFrontOrCombat(false);
            }

            if (TryBeginAllOutWarSafeHealingRetreat())
            {
                survivalState = TacticalSurvivalState.RetreatForHeal;
                nextSurvivalAssessmentAt = Time.time + Random.Range(1.6f, 2.4f);
                return TryUpdateHealingObjective(canSeePlayer, targetEye);
            }

            return ResumeAllOutWarFrontOrCombat(canSeePlayer);
        }

        private bool TryUpdateAllOutWarResupplyState(bool canSeeTarget, Vector3 targetEye)
        {
            if (!autonomousWarMode || match == null || weapons == null || !weapons.HasWeapon)
            {
                return false;
            }

            if (weapons.Ammo > AllOutWarAmmoSeekThreshold)
            {
                if (survivalState == TacticalSurvivalState.Resupply)
                {
                    survivalState = TacticalSurvivalState.None;
                    match.ReevaluateAllOutWarSquadDecision(health, transform.position);
                }

                ClearAmmoTarget();
                return false;
            }

            var empty = weapons.Ammo <= 0;
            if (canSeeTarget && !empty)
            {
                return false;
            }

            if (match.TryDistributeAllOutWarSquadResources(health, transform.position))
            {
                return false;
            }

            if (match.IsAllOutWarSquadCarryingAmmoCells(health))
            {
                match.ReevaluateAllOutWarSquadDecision(health, transform.position);
                MoveAllOutWarFrontSearch();
                return true;
            }

            if (!empty && health != null && health.MaxHealth > 0f && health.CurrentHealth / health.MaxHealth <= 0.38f)
            {
                return false;
            }

            if (ammoPickupTarget != null)
            {
                if (!IsAllOutWarAmmoDestinationSafe(ammoPickupTarget.transform.position))
                {
                    ClearAmmoTarget();
                    survivalState = TacticalSurvivalState.None;
                    return false;
                }
            }
            else if (!TryBeginAllOutWarSafeAmmoResupply())
            {
                return false;
            }

            if (ammoPickupTarget == null)
            {
                return false;
            }

            survivalState = TacticalSurvivalState.Resupply;
            var destination = ammoPickupTarget.transform.position + Vector3.up * 1.1f;
            UpdateCrouch(false);
            if (canSeeTarget && Random.value < Time.deltaTime * 1.2f)
            {
                FaceTowards(targetEye);
            }

            MoveAlongPath(destination, runSpeed * 1.08f, 1.05f);
            return true;
        }

        private bool ResumeAllOutWarFrontOrCombat(bool canSeePlayer)
        {
            survivalState = TacticalSurvivalState.None;
            nextSurvivalAssessmentAt = Time.time + Random.Range(1.1f, 1.8f);

            if (canSeePlayer)
            {
                return false;
            }

            UpdateCrouch(false);
            MoveAllOutWarFrontSearch();
            return true;
        }

        private bool TryUpdateHealingObjective(bool canSeePlayer, Vector3 targetEye)
        {
            if (health == null || !health.IsAlive || health.CurrentHealth >= health.MaxHealth - 0.5f)
            {
                survivalState = TacticalSurvivalState.None;
                ClearHealingTargets();
                return false;
            }

            if (healthPickupTarget != null)
            {
                if (autonomousWarMode && !IsAllOutWarHealingDestinationSafe(healthPickupTarget.transform.position))
                {
                    ClearHealingTargets();
                    return ResumeAllOutWarFrontOrCombat(canSeePlayer);
                }

                healingDestination = healthPickupTarget.transform.position + Vector3.up * 1.1f;
                if (FlatDistance(transform.position, healthPickupTarget.transform.position) <= 0.95f)
                {
                    healthPickupTarget = null;
                }
            }
            else if (healingStationTarget != null)
            {
                if (autonomousWarMode && !IsAllOutWarHealingDestinationSafe(healingStationTarget.transform.position))
                {
                    ClearHealingTargets();
                    return ResumeAllOutWarFrontOrCombat(canSeePlayer);
                }

                if (FlatDistance(transform.position, healingStationTarget.transform.position) <= 1.85f &&
                    (healingStationTarget.TryHealToFull(health) || health.CurrentHealth >= health.MaxHealth - 0.5f))
                {
                    ClearHealingTargets();
                    survivalState = TacticalSurvivalState.None;
                    ApplyGravityOnly();
                    return true;
                }

                healingDestination = healingStationTarget.transform.position + Vector3.up * 1.1f;
            }
            else if (!(autonomousWarMode ? TryBeginAllOutWarSafeHealingRetreat() : TryBeginHealingRetreat()))
            {
                if (autonomousWarMode)
                {
                    ClearHealingTargets();
                    return ResumeAllOutWarFrontOrCombat(canSeePlayer);
                }

                survivalState = TacticalSurvivalState.LastStand;
                return false;
            }

            UpdateCrouch(false);
            if (canSeePlayer && Random.value < Time.deltaTime * 1.6f)
            {
                FaceTowards(targetEye);
            }

            MoveAlongPath(healingDestination, runSpeed * 1.08f, 1.1f);
            return true;
        }

        private float CalculateHealRunScore(out bool shouldWaitForShield)
        {
            shouldWaitForShield = false;
            var destination = FindPreferredHealingDestination(out var found, out var targetIsPickup);
            if (!found)
            {
                return 0f;
            }

            var canWaitForShield = CanSafelyWaitForShield();
            var support = match != null ? match.CountNearbyDroidAllies(transform.position, 9f, health) : 0;
            var nearbyThreats = match != null ? match.CountNearbyDroidTargets(transform.position, 10f, health) : 0;
            var routeThreats = match != null ? match.CountDroidTargetsNearRoute(transform.position, destination, 4.5f, health) : 0;
            var hasThreat = player != null && Time.time < playerKnownUntil;
            var threatDirection = hasThreat ? Flatten(transform.position - lastKnownPlayerPosition) : Vector3.forward;
            if (threatDirection.sqrMagnitude < 0.01f)
            {
                threatDirection = transform.forward;
            }

            return TacticalSurvivalModel.CalculateHealRunScore(
                health,
                transform.position,
                destination,
                targetIsPickup,
                nearbyThreats,
                routeThreats,
                support,
                damagePressure,
                Time.time < underFireUntil,
                canWaitForShield,
                hasThreat,
                lastKnownPlayerPosition,
                threatDirection.normalized,
                out shouldWaitForShield);
        }

        private Vector3 FindPreferredHealingDestination(out bool found, out bool targetIsPickup)
        {
            found = false;
            targetIsPickup = false;
            var bestPosition = transform.position;
            var bestScore = float.NegativeInfinity;

            if (match != null && match.TryFindNearestHealthPickup(transform.position, 30f, out var pickup))
            {
                var distance = FlatDistance(transform.position, pickup.transform.position);
                bestScore = 20f - distance;
                bestPosition = pickup.transform.position;
                found = true;
                targetIsPickup = true;
            }

            if (match != null && match.TryFindNearestHealingStation(transform.position, out var station))
            {
                var distance = FlatDistance(transform.position, station.transform.position);
                var stationScore = 15f - distance * 0.7f + (station.IsAvailable ? 4f : -5f);
                if (stationScore > bestScore)
                {
                    bestPosition = station.transform.position;
                    found = true;
                    targetIsPickup = false;
                }
            }

            return bestPosition;
        }

        private bool CanSafelyWaitForShield()
        {
            if (health.MaxShield <= 0f || health.CurrentShield / health.MaxShield >= 0.38f)
            {
                return false;
            }

            var nearbyThreats = match != null ? match.CountNearbyDroidTargets(transform.position, 7f, health) : 0;
            var support = match != null ? match.CountNearbyDroidAllies(transform.position, 8f, health) : 0;
            return TryPickCoverDestination(out _) && nearbyThreats <= 1 + support && Time.time > underFireUntil - 0.6f;
        }

        private bool TryBeginHealingRetreat()
        {
            if (health == null || health.CurrentHealth >= health.MaxHealth - 0.5f)
            {
                return false;
            }

            if (match != null && match.TryFindNearestHealthPickup(transform.position, 26f, out var pickup))
            {
                healthPickupTarget = pickup;
                healingStationTarget = null;
                healingDestination = pickup.transform.position + Vector3.up * 1.1f;
                ClearCurrentPath();
                repathAt = 0f;
                return true;
            }

            if (match != null && match.TryFindNearestHealingStation(transform.position, out var station))
            {
                healingStationTarget = station;
                healthPickupTarget = null;
                healingDestination = station.transform.position + Vector3.up * 1.1f;
                ClearCurrentPath();
                repathAt = 0f;
                return true;
            }

            return false;
        }

        private bool TryBeginAllOutWarSafeHealingRetreat()
        {
            if (health == null || health.CurrentHealth >= health.MaxHealth - 0.5f || match == null)
            {
                return false;
            }

            if (match.TryFindNearestSafeHealthPickup(
                transform.position,
                AllOutWarHealPickupSearchRadius,
                health,
                AllOutWarHealResourceDangerRadius,
                AllOutWarHealRouteDangerRadius,
                out var pickup))
            {
                if (!match.TryClaimAllOutWarHealthRunner(health))
                {
                    return false;
                }

                healthPickupTarget = pickup;
                healingStationTarget = null;
                healingDestination = pickup.transform.position + Vector3.up * 1.1f;
                ClearCurrentPath();
                repathAt = 0f;
                match.ReportAllOutWarSquadLogisticsObjective(health, pickup.transform.position, true);
                return true;
            }

            if (match.TryFindNearestSafeHealingStation(
                transform.position,
                health,
                AllOutWarHealResourceDangerRadius,
                AllOutWarHealRouteDangerRadius,
                out var station))
            {
                if (!match.TryClaimAllOutWarHealthRunner(health))
                {
                    return false;
                }

                healingStationTarget = station;
                healthPickupTarget = null;
                healingDestination = station.transform.position + Vector3.up * 1.1f;
                ClearCurrentPath();
                repathAt = 0f;
                match.ReportAllOutWarSquadLogisticsObjective(health, station.transform.position, true);
                return true;
            }

            return false;
        }

        private bool HasSafeAllOutWarHealingDestination()
        {
            if (healthPickupTarget != null)
            {
                return IsAllOutWarHealingDestinationSafe(healthPickupTarget.transform.position);
            }

            if (healingStationTarget != null)
            {
                return IsAllOutWarHealingDestinationSafe(healingStationTarget.transform.position);
            }

            return false;
        }

        private bool IsAllOutWarHealingDestinationSafe(Vector3 destination)
        {
            if (match == null || health == null)
            {
                return false;
            }

            return match.CountNearbyDroidTargets(destination, AllOutWarHealResourceDangerRadius, health) <= 0 &&
                match.CountDroidTargetsNearRoute(transform.position, destination, AllOutWarHealRouteDangerRadius, health) <= 0;
        }

        private bool TryBeginAllOutWarSafeAmmoResupply()
        {
            if (!autonomousWarMode || match == null || health == null || weapons == null || !weapons.HasWeapon || weapons.Ammo > AllOutWarAmmoSeekThreshold)
            {
                return false;
            }

            if (!match.TryFindNearestSafeAmmoPickup(
                transform.position,
                AllOutWarAmmoPickupSearchRadius,
                health,
                AllOutWarAmmoResourceDangerRadius,
                AllOutWarAmmoRouteDangerRadius,
                out var pickup))
            {
                return false;
            }

            if (!match.TryClaimAllOutWarAmmoRunner(health))
            {
                return false;
            }

            ammoPickupTarget = pickup;
            ClearCurrentPath();
            repathAt = 0f;
            match.ReportAllOutWarSquadLogisticsObjective(health, pickup.transform.position, false);
            return true;
        }

        private bool IsAllOutWarAmmoDestinationSafe(Vector3 destination)
        {
            if (match == null || health == null)
            {
                return false;
            }

            return match.CountNearbyDroidTargets(destination, AllOutWarAmmoResourceDangerRadius, health) <= 0 &&
                match.CountDroidTargetsNearRoute(transform.position, destination, AllOutWarAmmoRouteDangerRadius, health) <= 0;
        }

        private void ClearAmmoTarget()
        {
            if (autonomousWarMode && match != null)
            {
                match.ReleaseAllOutWarAmmoRunner(health);
            }

            ammoPickupTarget = null;
        }

        private void ClearHealingTargets()
        {
            if (autonomousWarMode && match != null)
            {
                match.ReleaseAllOutWarHealthRunner(health);
            }

            healingStationTarget = null;
            healthPickupTarget = null;
            healingDestination = Vector3.zero;
        }

        private void HoldCoverOrScan(bool canSeePlayer, Vector3 targetEye)
        {
            if (canSeePlayer)
            {
                FaceTowards(targetEye);
                if (Time.time >= nextBurstAt)
                {
                    nextBurstAt = Time.time + Random.Range(0.42f, 0.72f);
                    FireSloppyBurst(transform.position + Vector3.up * 0.68f, targetEye - (transform.position + Vector3.up * 0.68f));
                }
            }

            if ((coverDestination == Vector3.zero || Time.time >= nextCoverCheckAt) && TryPickCoverDestination(out var cover))
            {
                coverDestination = cover;
                nextCoverCheckAt = Time.time + Random.Range(1.4f, 2.3f);
            }

            if (coverDestination != Vector3.zero && FlatDistance(transform.position, coverDestination) > 1.25f)
            {
                MoveAlongPath(coverDestination, walkSpeed, 0.85f);
                return;
            }

            ApplyGravityOnly();
        }

        private void OnDamaged(CombatantHealth damagedHealth)
        {
            if (autonomousWarMode)
            {
                var attacker = damagedHealth != null ? damagedHealth.LastAttacker : null;
                if (attacker != null && CombatantTeam.AreEnemies(health, attacker))
                {
                    player = attacker.transform;
                    lastKnownPlayerPosition = attacker.transform.position;
                    playerKnownUntil = Time.time + lastKnownPlayerMemory;
                    searchDestination = lastKnownPlayerPosition;
                    nextSearchPickAt = 0f;
                    allOutWarForceObjectiveRefresh = true;
                    ClearCurrentPath();
                    repathAt = 0f;
                    recoveryUntil = 0f;
                    underFireUntil = Time.time + 3.2f;
                    damagePressure = Mathf.Min(4f, damagePressure + 1f);
                    nextSurvivalAssessmentAt = Mathf.Min(nextSurvivalAssessmentAt <= 0f ? Time.time + 1.8f : nextSurvivalAssessmentAt, Time.time + 1.8f);
                    nextBurstAt = Mathf.Min(nextBurstAt, Time.time + 0.18f);
                    FaceTowards(attacker.transform.position + Vector3.up * 0.64f);
                    match.ReportAllOutWarSquadSignal(health, MatchController.AllOutWarSquadSignal.TakingDamage, attacker.transform.position, attacker);
                }

                return;
            }

            if (player == null || !health.IsAlive)
            {
                player = playerAnchor;
                if (player == null || !health.IsAlive)
                {
                    return;
                }
            }

            lastKnownPlayerPosition = player.position;
            playerKnownUntil = Time.time + lastKnownPlayerMemory;
            searchDestination = lastKnownPlayerPosition;
            nextSearchPickAt = 0f;
            ClearCurrentPath();
            repathAt = 0f;
            recoveryUntil = 0f;
            underFireUntil = Time.time + 3.2f;
            damagePressure = Mathf.Min(4f, damagePressure + 1f);
            nextSurvivalAssessmentAt = Mathf.Min(nextSurvivalAssessmentAt <= 0f ? Time.time + 1.8f : nextSurvivalAssessmentAt, Time.time + 1.8f);
            nextBurstAt = Mathf.Min(nextBurstAt, Time.time + 0.18f);
            FaceTowards(player.position + Vector3.up * 0.64f);
        }

        private void OnPlayerNoise(Vector3 position, float radius)
        {
            if (autonomousWarMode)
            {
                return;
            }

            if (match == null || !match.IsMatchActive || health == null || !health.IsAlive || player == null)
            {
                player = playerAnchor;
                if (match == null || !match.IsMatchActive || health == null || !health.IsAlive || player == null)
                {
                    return;
                }
            }

            var distance = Vector3.Distance(transform.position, position);
            if (distance > radius)
            {
                return;
            }

            lastKnownPlayerPosition = position;
            playerKnownUntil = Time.time + Mathf.Lerp(2.6f, lastKnownPlayerMemory, 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, radius)));
            searchDestination = position;
            nextSearchPickAt = 0f;
            ClearCurrentPath();
            repathAt = 0f;
            recoveryUntil = 0f;
            FaceTowards(position + Vector3.up * 0.6f);
        }

        private void UpdateCrouch(bool canSeePlayer)
        {
            if (Time.time >= nextCrouchDecisionAt)
            {
                nextCrouchDecisionAt = Time.time + Random.Range(1.2f, 2.4f);
                var crouchChance = squadRole == SquadRole.CrouchingRifle ? 0.88f : squadRole == SquadRole.StandingRifle ? 0.22f : 0.34f;
                if (canSeePlayer && Random.value < crouchChance)
                {
                    crouchUntil = Time.time + Random.Range(1.15f, 2.25f);
                }
            }

            var crouching = Time.time < crouchUntil;
            ApplyCrouchPose(crouching, 5.5f);
        }

        private void ApplyCrouchPose(bool crouching, float transitionSpeed)
        {
            if (crouchPose == null)
            {
                crouchPose = GetComponent<DroidCrouchPose>();
                if (crouchPose == null)
                {
                    crouchPose = gameObject.AddComponent<DroidCrouchPose>();
                }
            }

            crouchPose.Apply(crouching, transitionSpeed);
        }

        private bool CanSeePlayer(Vector3 eye, Vector3 playerEye, Vector3 toPlayer)
        {
            if (toPlayer.magnitude > visionRange)
            {
                return false;
            }

            if (toPlayer.sqrMagnitude <= 0.01f)
            {
                return true;
            }

            var targetHealth = player != null ? player.GetComponent<CombatantHealth>() : null;
            return targetHealth != null && CanSeeTarget(targetHealth, eye, playerEye, toPlayer);
        }

        private bool CanSeeTarget(CombatantHealth target, Vector3 eye, Vector3 targetEye, Vector3 toTarget)
        {
            if (target == null)
            {
                return false;
            }

            var hits = Physics.RaycastAll(eye, toTarget.normalized, toTarget.magnitude + 0.2f, ~0, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0)
            {
                return true;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                var hitHealth = hit.collider.GetComponentInParent<CombatantHealth>();
                if (hitHealth == target)
                {
                    return true;
                }

                if (hitHealth != null)
                {
                    return false;
                }

                var destructible = hit.collider.GetComponentInParent<DestructibleArenaPiece>();
                if (destructible != null && destructible.AllowsProjectilePassThrough(hit.point, hit.normal))
                {
                    continue;
                }

                if (hit.collider.isTrigger)
                {
                    continue;
                }

                return false;
            }

            return false;
        }

        private void UpdatePath(Vector3 destination, float interval)
        {
            if (Time.time < repathAt && (hasNavMeshPath || path.Count > 0))
            {
                return;
            }

            repathAt = Time.time + interval;
            ClearCurrentPath();
            if (autonomousWarMode && TryBuildNavMeshPath(destination))
            {
                return;
            }

            path.AddRange(layout.FindPath(layout.GetNearestRoom(transform.position), layout.GetNearestRoom(destination)));
        }

        private void MoveAlongPath(Vector3 destination, float speed, float stopDistance)
        {
            if (Time.time < recoveryUntil)
            {
                FaceTowards(transform.position + recoveryDirection);
                Move(recoveryDirection * TacticalMoveSpeed(runSpeed));
                return;
            }

            if (autonomousWarMode && hasNavMeshPath && TryMoveAlongNavMeshPath(destination, speed, stopDistance))
            {
                return;
            }

            var currentRoom = layout.GetNearestRoom(transform.position);
            while (path.Count > 0 && path[0] == currentRoom)
            {
                path.RemoveAt(0);
            }

            if (path.Count > 0 && layout.TryGetDoorwayPoint(currentRoom, path[0], out var doorwayPoint))
            {
                targetPoint = doorwayPoint;
                if (Vector3.Distance(Flatten(transform.position), Flatten(targetPoint)) < 1.15f)
                {
                    path.RemoveAt(0);
                }
            }
            else if (path.Count > 0 && layout.TryGetCenter(path[0], out var roomCenter))
            {
                targetPoint = roomCenter;
            }
            else
            {
                targetPoint = destination;
            }

            var flatToTarget = Flatten(targetPoint - transform.position);
            if (flatToTarget.magnitude <= stopDistance)
            {
                ApplyGravityOnly();
                return;
            }

            var direction = flatToTarget.sqrMagnitude > 0.04f ? flatToTarget.normalized : Vector3.zero;
            if (direction != Vector3.zero)
            {
                FaceTowards(transform.position + direction);
            }

            Move(direction * TacticalMoveSpeed(speed));
        }

        private bool TryBuildNavMeshPath(Vector3 destination)
        {
            navMeshPath ??= new NavMeshPath();
            if (!NavMesh.SamplePosition(transform.position, out var startHit, 3.5f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(destination, out var endHit, 5.5f, NavMesh.AllAreas))
            {
                return false;
            }

            if (!NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, navMeshPath) ||
                navMeshPath.status == NavMeshPathStatus.PathInvalid ||
                navMeshPath.corners == null ||
                navMeshPath.corners.Length == 0)
            {
                return false;
            }

            navMeshCorners = navMeshPath.corners;
            navMeshCornerIndex = navMeshCorners.Length > 1 && FlatDistance(transform.position, navMeshCorners[0]) < 0.75f ? 1 : 0;
            hasNavMeshPath = true;
            return true;
        }

        private bool TryMoveAlongNavMeshPath(Vector3 destination, float speed, float stopDistance)
        {
            if (navMeshCorners == null || navMeshCorners.Length == 0)
            {
                ClearNavMeshPath();
                return false;
            }

            while (navMeshCornerIndex < navMeshCorners.Length &&
                   FlatDistance(transform.position, navMeshCorners[navMeshCornerIndex]) < 0.82f)
            {
                navMeshCornerIndex++;
            }

            targetPoint = navMeshCornerIndex < navMeshCorners.Length ? navMeshCorners[navMeshCornerIndex] : destination;
            var flatToTarget = Flatten(targetPoint - transform.position);
            if (flatToTarget.magnitude <= stopDistance)
            {
                if (navMeshCornerIndex >= navMeshCorners.Length)
                {
                    ClearNavMeshPath();
                }

                ApplyGravityOnly();
                return true;
            }

            var direction = flatToTarget.sqrMagnitude > 0.04f ? flatToTarget.normalized : Vector3.zero;
            if (direction != Vector3.zero)
            {
                FaceTowards(transform.position + direction);
            }

            Move(direction * TacticalMoveSpeed(speed));
            return true;
        }

        private void ClearCurrentPath()
        {
            path.Clear();
            ClearNavMeshPath();
        }

        private void ClearNavMeshPath()
        {
            hasNavMeshPath = false;
            navMeshCornerIndex = 0;
            navMeshCorners = System.Array.Empty<Vector3>();
        }

        private void StrafeAndHoldRange(Vector3 flatToPlayer)
        {
            var side = Vector3.Cross(Vector3.up, flatToPlayer.normalized);
            if (Mathf.Sin(Time.time * 0.72f + GetInstanceID()) < 0f)
            {
                side = -side;
            }

            var distanceBias = flatToPlayer.magnitude < preferredRange * 0.72f ? -flatToPlayer.normalized * 0.55f : Vector3.zero;
            Move((side * 0.68f + distanceBias).normalized * TacticalMoveSpeed(walkSpeed));
        }

        private float TacticalMoveSpeed(float speed)
        {
            return Time.time < crouchUntil ? speed * 0.46f : speed;
        }

        private void Move(Vector3 planarVelocity)
        {
            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1.5f;
            }

            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            planarVelocity.y = verticalVelocity;
            controller.Move(planarVelocity * Time.deltaTime);

            var tryingToMove = planarVelocity.sqrMagnitude > 2f;
            var moved = hasLastPosition ? Flatten(transform.position - lastPosition).magnitude : 1f;
            if (tryingToMove && moved < 0.018f)
            {
                stuckTimer += Time.deltaTime;
            }
            else
            {
                stuckTimer = 0f;
            }

            if (stuckTimer >= 0.45f)
            {
                BeginStuckRecovery(planarVelocity);
            }
        }

        private void BeginStuckRecovery(Vector3 attemptedVelocity)
        {
            stuckTimer = 0f;
            recoveryUntil = Time.time + 0.48f;
            var attempted = Flatten(attemptedVelocity);
            if (attempted.sqrMagnitude < 0.1f && player != null)
            {
                attempted = Flatten(player.position - transform.position);
            }

            if (attempted.sqrMagnitude < 0.1f)
            {
                attempted = transform.forward;
            }

            var side = Vector3.Cross(Vector3.up, attempted.normalized);
            if (Random.value < 0.5f)
            {
                side = -side;
            }

            var currentRoom = layout.GetNearestRoom(transform.position);
            var towardRoomCenter = layout.TryGetCenter(currentRoom, out var center)
                ? Flatten(center - transform.position).normalized
                : Vector3.zero;
            recoveryDirection = (side * 0.72f + towardRoomCenter * 0.85f).normalized;
            if (recoveryDirection.sqrMagnitude < 0.1f)
            {
                recoveryDirection = side.normalized;
            }

            ClearCurrentPath();
            repathAt = 0f;
            if (autonomousWarMode)
            {
                allOutWarForceObjectiveRefresh = true;
                nextSearchPickAt = 0f;
            }
        }
        private void ApplyGravityOnly()
        {
            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1.5f;
            }

            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
        }

        private void FaceTowards(Vector3 worldPoint)
        {
            var direction = Flatten(worldPoint - transform.position);
            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            return Flatten(a - b).magnitude;
        }
    }
}
