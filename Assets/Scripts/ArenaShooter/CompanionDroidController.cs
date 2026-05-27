using System.Collections.Generic;
using UnityEngine;

namespace ArenaShooter
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CombatantHealth))]
    [RequireComponent(typeof(WeaponInventory))]
    public sealed class CompanionDroidController : MonoBehaviour
    {
        private MatchController match;
        private ArenaLayout layout;
        private Transform player;
        private CharacterController controller;
        private CombatantHealth health;
        private WeaponInventory weapons;
        private DroidBlasterRig blasterRig;
        private DroidCrouchPose crouchPose;
        private readonly List<Vector2Int> path = new();
        private float speedMultiplier = 1f;
        private float followSide = 1f;
        private float nextShotAt;
        private float repathAt;
        private float stuckTimer;
        private float recoveryUntil;
        private float verticalVelocity;
        private Vector3 targetPoint;
        private Vector3 lastPosition;
        private Vector3 lastPlayerPosition;
        private Vector3 playerTravelDirection = Vector3.forward;
        private Vector3 recoveryDirection;
        private Vector3 coverDestination;
        private Vector3 threatPosition;
        private float underFireUntil;
        private float crouchUntil;
        private float coverUntil;
        private float nextCoverDecisionAt;
        private float damagePressure;
        private float lastCombatAt;
        private float nextCombatMoveAt;
        private Vector3 combatDestination;
        private bool hasLastPosition;
        private HealingStation healingStationTarget;
        private HealthPickup healthPickupTarget;
        private Vector3 healingDestination;
        private float nextScanAt;
        private float scanUntil;
        private Vector3 scanPoint;
        private CompanionSurvivalState survivalState;
        private float nextSurvivalAssessmentAt;
        private float waitForShieldUntil;
        private float lastHealRunScore;
        private Vector3 assignedDefensePoint;
        private Vector3 assignedThreatPoint;
        private float defenseOrderUntil;

        private enum CompanionSurvivalState
        {
            None,
            WaitForShield,
            RetreatForHeal,
            LastStand
        }

        public void Configure(MatchController owner, ArenaLayout arenaLayout, Transform playerTransform, float movementScale)
        {
            match = owner;
            layout = arenaLayout;
            player = playerTransform;
            speedMultiplier = Mathf.Max(0.8f, movementScale);
            followSide = Random.value > 0.5f ? 1f : -1f;
            targetPoint = transform.position;
            if (player != null)
            {
                lastPlayerPosition = player.position;
                playerTravelDirection = Flatten(player.forward).sqrMagnitude > 0.01f ? Flatten(player.forward).normalized : Vector3.forward;
            }
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<CombatantHealth>();
            weapons = GetComponent<WeaponInventory>();
            blasterRig = GetComponent<DroidBlasterRig>();
            crouchPose = GetComponent<DroidCrouchPose>();
            if (health != null)
            {
                health.Damaged += OnDamaged;
            }

            ArenaNoise.Gunfire += OnGunfire;
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
            }

            ArenaNoise.Gunfire -= OnGunfire;
        }

        private void Update()
        {
            if (match == null || player == null || health == null || !health.IsAlive || !match.IsMatchActive)
            {
                return;
            }

            UpdatePlayerTravelDirection();

            var target = FindBestTarget();
            var canSeeTarget = target != null && CanSeeTarget(target);
            if (TryUpdateSurvivalState(canSeeTarget))
            {
                hasLastPosition = true;
                lastPosition = transform.position;
                lastPlayerPosition = player.position;
                return;
            }

            if (target == null && TryUpdateHealingObjective())
            {
                hasLastPosition = true;
                lastPosition = transform.position;
                lastPlayerPosition = player.position;
                return;
            }

            damagePressure = Mathf.MoveTowards(damagePressure, 0f, Time.deltaTime * 0.42f);
            if (canSeeTarget)
            {
                lastCombatAt = Time.time;
                AimAndFireAt(target);
            }
            else
            {
                blasterRig?.ClearAim();
            }

            UpdateDefensiveStance(target, canSeeTarget);
            if (!TryUpdateWaveDefense(canSeeTarget))
            {
                MoveTactically(target, canSeeTarget);
            }
            if (Time.time < lastCombatAt + 2.25f && !canSeeTarget)
            {
                UpdateAlertScan();
            }

            hasLastPosition = true;
            lastPosition = transform.position;
            lastPlayerPosition = player.position;
        }

        public void SendToHealingStation(HealingStation station)
        {
            if (station == null || health == null || !health.IsAlive || health.CurrentHealth >= health.MaxHealth - 0.5f)
            {
                return;
            }

            survivalState = CompanionSurvivalState.RetreatForHeal;
            healingStationTarget = station;
            healthPickupTarget = null;
            healingDestination = station.transform.position + Vector3.up * 1.1f;
            path.Clear();
            repathAt = 0f;
        }

        public void AssignWaveDefense(Vector3 position, Vector3 threat)
        {
            assignedDefensePoint = position;
            assignedThreatPoint = threat;
            threatPosition = threat;
            defenseOrderUntil = Time.time + 18f;
            underFireUntil = Mathf.Max(underFireUntil, Time.time + 4.5f);
            crouchUntil = Mathf.Max(crouchUntil, Time.time + Random.Range(1.1f, 2.2f));
            coverDestination = position;

            path.Clear();
            repathAt = 0f;
        }

        private bool TryUpdateHealingObjective()
        {
            if (health == null || !health.IsAlive || health.CurrentHealth >= health.MaxHealth - 0.5f)
            {
                survivalState = CompanionSurvivalState.None;
                healingStationTarget = null;
                healthPickupTarget = null;
                return false;
            }

            if (healthPickupTarget != null)
            {
                healingDestination = healthPickupTarget.transform.position + Vector3.up * 1.1f;
                if (FlatDistance(transform.position, healthPickupTarget.transform.position) <= 0.95f)
                {
                    healthPickupTarget = null;
                }
            }
            else if (healingStationTarget != null)
            {
                if (FlatDistance(transform.position, healingStationTarget.transform.position) <= 1.85f)
                {
                    if (healingStationTarget.TryHealToFull(health) || health.CurrentHealth >= health.MaxHealth - 0.5f)
                    {
                        healingStationTarget = null;
                        survivalState = CompanionSurvivalState.None;
                        ApplyGravityOnly();
                        return true;
                    }
                }

                healingDestination = healingStationTarget.transform.position + Vector3.up * 1.1f;
            }
            else
            {
                if (!TryBeginHealingRetreat())
                {
                    survivalState = CompanionSurvivalState.LastStand;
                    return false;
                }
            }

            if (health.CurrentHealth >= health.MaxHealth - 0.5f)
            {
                healingStationTarget = null;
                healthPickupTarget = null;
                survivalState = CompanionSurvivalState.None;
                ApplyGravityOnly();
                return true;
            }

            if (healingStationTarget == null && healthPickupTarget == null)
            {
                if (!TryBeginHealingRetreat())
                {
                    ApplyGravityOnly();
                    return true;
                }
            }

            ApplyCrouchPose(false, 5.2f);
            MoveAlongPath(healingDestination, 5.2f * speedMultiplier, 1.15f);
            return true;
        }

        private bool ShouldRetreatForHealing(bool canSeeTarget)
        {
            if (health == null || health.MaxHealth <= 0f || health.CurrentHealth / health.MaxHealth > 0.42f)
            {
                return false;
            }

            return Time.time < underFireUntil || canSeeTarget || damagePressure >= 2.2f;
        }

        private bool TryUpdateSurvivalState(bool canSeeTarget)
        {
            if (health == null || health.MaxHealth <= 0f)
            {
                return false;
            }

            var healthPercent = health.CurrentHealth / health.MaxHealth;
            if (healthPercent > 0.42f)
            {
                survivalState = CompanionSurvivalState.None;
                nextSurvivalAssessmentAt = 0f;
                return false;
            }

            if (survivalState == CompanionSurvivalState.RetreatForHeal)
            {
                return TryUpdateHealingObjective();
            }

            if (survivalState == CompanionSurvivalState.WaitForShield)
            {
                var enemyTooClose = match != null && match.CountNearbyEnemies(transform.position, 5.5f) > 0;
                var shieldUseful = health.MaxShield > 0f && health.CurrentShield / health.MaxShield >= 0.38f;
                if (!enemyTooClose && Time.time < waitForShieldUntil && !shieldUseful)
                {
                    UpdateDefensiveStance(null, canSeeTarget);
                    HoldCoverOrScan();
                    return true;
                }

                survivalState = CompanionSurvivalState.None;
                nextSurvivalAssessmentAt = Time.time;
            }

            if (survivalState == CompanionSurvivalState.LastStand && Time.time < nextSurvivalAssessmentAt)
            {
                UpdateDefensiveStance(null, canSeeTarget);
                HoldCoverOrScan();
                return true;
            }

            if (nextSurvivalAssessmentAt <= 0f)
            {
                nextSurvivalAssessmentAt = Time.time + Random.Range(2.2f, 3.8f);
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
                survivalState = CompanionSurvivalState.RetreatForHeal;
                return TryUpdateHealingObjective();
            }

            if (shouldWaitForShield)
            {
                survivalState = CompanionSurvivalState.WaitForShield;
                waitForShieldUntil = Time.time + Random.Range(2.2f, 3.6f);
                HoldCoverOrScan();
                return true;
            }

            survivalState = CompanionSurvivalState.LastStand;
            HoldCoverOrScan();
            return true;
        }

        private float CalculateHealRunScore(out bool shouldWaitForShield)
        {
            shouldWaitForShield = false;
            var destination = FindPreferredHealingDestination(out var hasHealingTarget, out var targetIsPickup);
            if (!hasHealingTarget)
            {
                return 0f;
            }

            var support = match != null ? match.CountNearbyCompanions(transform.position, 9f) : 0;
            var nearbyEnemies = match != null ? match.CountNearbyEnemies(transform.position, 10f) : 0;
            var routeEnemies = match != null ? match.CountEnemiesNearRoute(transform.position, destination, 4.5f) : 0;
            var threat = Vector3.zero;
            var direction = Vector3.forward;
            var hasRecentThreat = match != null && match.TryGetRecentArmyThreat(out threat, out direction);
            return TacticalSurvivalModel.CalculateHealRunScore(
                health,
                transform.position,
                destination,
                targetIsPickup,
                nearbyEnemies,
                routeEnemies,
                support,
                damagePressure,
                Time.time < underFireUntil,
                CanSafelyWaitForShield(),
                hasRecentThreat,
                hasRecentThreat ? threat : Vector3.zero,
                hasRecentThreat ? direction : Vector3.forward,
                out shouldWaitForShield);
        }

        private Vector3 FindPreferredHealingDestination(out bool found, out bool targetIsPickup)
        {
            found = false;
            targetIsPickup = false;
            var bestPosition = transform.position;
            var bestScore = float.NegativeInfinity;

            if (match != null && match.TryFindNearestHealthPickup(transform.position, 28f, out var pickup))
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
                var stationScore = 14f - distance * 0.7f;
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

            var nearbyEnemies = match != null ? match.CountNearbyEnemies(transform.position, 7f) : 0;
            var support = match != null ? match.CountNearbyCompanions(transform.position, 8f) : 0;
            var hasCover = TryPickDefensivePosition(null, out _);
            return hasCover && nearbyEnemies <= 1 + support && Time.time > underFireUntil - 0.6f;
        }

        private bool TryBeginHealingRetreat()
        {
            if (health == null || health.CurrentHealth >= health.MaxHealth - 0.5f)
            {
                return false;
            }

            if (match != null && match.TryFindNearestHealthPickup(transform.position, 24f, out var pickup))
            {
                healthPickupTarget = pickup;
                healingStationTarget = null;
                healingDestination = pickup.transform.position + Vector3.up * 1.1f;
                path.Clear();
                repathAt = 0f;
                return true;
            }

            if (match != null && match.TryFindNearestHealingStation(transform.position, out var station))
            {
                SendToHealingStation(station);
                return true;
            }

            if (layout != null)
            {
                var currentRoom = layout.GetNearestRoom(transform.position);
                if (layout.TryGetCenter(currentRoom, out var roomCenter))
                {
                    var away = Flatten(roomCenter - threatPosition);
                    if (away.sqrMagnitude < 0.1f)
                    {
                        away = -transform.forward;
                    }

                    healingDestination = roomCenter + away.normalized * 3.2f;
                    healthPickupTarget = null;
                    healingStationTarget = null;
                    path.Clear();
                    repathAt = 0f;
                    return true;
                }
            }

            return false;
        }

        private CombatantHealth FindBestTarget()
        {
            CombatantHealth best = null;
            var bestScore = float.PositiveInfinity;
            var companionRoom = layout != null ? layout.GetNearestRoom(transform.position) : Vector2Int.zero;
            var playerRoom = layout != null ? layout.GetNearestRoom(player.position) : Vector2Int.zero;
            foreach (var droid in match.ActiveDroids)
            {
                if (droid == null || !droid.IsAlive)
                {
                    continue;
                }

                var visible = CanSeeTarget(droid);
                if (!visible && layout != null)
                {
                    var droidRoom = layout.GetNearestRoom(droid.transform.position);
                    if (!IsSameOrAdjacentRoom(droidRoom, playerRoom) && !IsSameOrAdjacentRoom(droidRoom, companionRoom))
                    {
                        continue;
                    }
                }

                var companionDistance = FlatDistance(droid.transform.position, transform.position);
                var playerDistance = FlatDistance(droid.transform.position, player.position);
                var score = companionDistance * 0.35f + playerDistance * 0.65f;
                if (visible)
                {
                    score -= 18f;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    best = droid;
                }
            }

            return best;
        }

        private void AimAndFireAt(CombatantHealth target)
        {
            var aimPoint = target.transform.position + Vector3.up * 0.72f;
            blasterRig?.AimAt(aimPoint);
            FaceTowards(aimPoint);

            if (Time.time < nextShotAt || weapons == null || !weapons.HasWeapon)
            {
                return;
            }

            nextShotAt = Time.time + Random.Range(0.24f, 0.42f);
            var shotOrigin = blasterRig != null && blasterRig.Muzzle != null
                ? blasterRig.Muzzle.position
                : transform.position + Vector3.up * 0.78f;
            var direction = (aimPoint - shotOrigin).normalized;
            weapons.TryFire(shotOrigin, direction);
        }

        private void MoveTactically(CombatantHealth currentTarget, bool canSeeTarget)
        {
            var playerDistance = FlatDistance(transform.position, player.position);
            var playerMoving = FlatDistance(player.position, lastPlayerPosition) > 0.025f;
            if (Time.time < coverUntil)
            {
                if (FlatDistance(transform.position, coverDestination) <= 1.05f)
                {
                    UpdateAlertScan();
                    ApplyGravityOnly();
                    return;
                }

                MoveAlongPath(coverDestination, 4.1f * speedMultiplier, 0.75f);
                return;
            }

            var inCombat = currentTarget != null && (canSeeTarget || Time.time < underFireUntil || Time.time < lastCombatAt + 4.5f);
            if (inCombat && playerDistance < 18f)
            {
                var combatMoveTarget = GetCombatManeuverPosition(currentTarget, canSeeTarget);
                var combatSpeed = canSeeTarget ? 3.65f * speedMultiplier : 4.45f * speedMultiplier;
                MoveAlongPath(combatMoveTarget, combatSpeed, canSeeTarget ? 1.1f : 0.85f);
                return;
            }

            if (Time.time < lastCombatAt + 3.5f && playerDistance < 20f)
            {
                var alertMoveTarget = GetAlertCoverPosition();
                MoveAlongPath(alertMoveTarget, 4.25f * speedMultiplier, 0.85f);
                return;
            }

            var shouldAnchorToPlayer = currentTarget == null || !canSeeTarget || playerDistance > 8.5f;
            var desired = shouldAnchorToPlayer
                ? GetPlayerFollowPosition(playerMoving)
                : GetCombatSupportPosition(currentTarget);
            var followDistance = playerDistance > 11f
                ? 1.15f
                : playerMoving ? 1.45f : 2.05f;
            if (!shouldAnchorToPlayer)
            {
                followDistance = 2.45f;
            }

            var speed = playerDistance > 13f
                ? 5.75f * speedMultiplier
                : shouldAnchorToPlayer ? 4.25f * speedMultiplier : 4.65f * speedMultiplier;
            MoveAlongPath(desired, speed, followDistance);
        }

        private bool TryUpdateWaveDefense(bool canSeeTarget)
        {
            if (canSeeTarget || Time.time >= defenseOrderUntil)
            {
                return false;
            }

            threatPosition = assignedThreatPoint != Vector3.zero ? assignedThreatPoint : threatPosition;
            var distance = FlatDistance(transform.position, assignedDefensePoint);
            if (distance > 0.95f)
            {
                MoveAlongPath(assignedDefensePoint, 4.6f * speedMultiplier, 0.78f);
            }
            else
            {
                ApplyGravityOnly();
                crouchUntil = Mathf.Max(crouchUntil, Time.time + 0.65f);
                UpdateAlertScan();
            }

            return true;
        }

        private void HoldCoverOrScan()
        {
            if ((coverDestination == Vector3.zero || Time.time >= nextCoverDecisionAt) &&
                TryPickDefensivePosition(null, out var cover))
            {
                coverDestination = cover;
                nextCoverDecisionAt = Time.time + Random.Range(2.2f, 3.4f);
            }

            if (coverDestination != Vector3.zero && FlatDistance(transform.position, coverDestination) > 1.35f)
            {
                MoveAlongPath(coverDestination, 3.8f * speedMultiplier, 0.9f);
                return;
            }

            ApplyGravityOnly();
            crouchUntil = Mathf.Max(crouchUntil, Time.time + 0.65f);
            UpdateAlertScan();
        }

        private void MoveAlongPath(Vector3 destination, float speed, float stopDistance)
        {
            if (layout == null)
            {
                MoveDirectly(destination, speed, stopDistance);
                return;
            }

            if (Time.time < recoveryUntil)
            {
                FaceTowards(transform.position + recoveryDirection);
                Move(recoveryDirection * speed);
                return;
            }

            UpdatePath(destination, 0.26f);
            var currentRoom = layout.GetNearestRoom(transform.position);
            while (path.Count > 0 && path[0] == currentRoom)
            {
                path.RemoveAt(0);
            }

            var navigatingPath = false;
            if (path.Count > 0 && layout.TryGetDoorwayPoint(currentRoom, path[0], out var doorway))
            {
                navigatingPath = true;
                targetPoint = GetThroughDoorwayPoint(currentRoom, path[0], doorway);
                if (FlatDistance(transform.position, targetPoint) < 1.05f)
                {
                    path.RemoveAt(0);
                }
            }
            else if (path.Count > 0 && layout.TryGetCenter(path[0], out var roomCenter))
            {
                navigatingPath = true;
                targetPoint = roomCenter;
            }
            else
            {
                targetPoint = destination;
            }

            MoveDirectly(targetPoint, speed, navigatingPath ? 0.62f : stopDistance);
        }

        private void MoveDirectly(Vector3 destination, float speed, float stopDistance)
        {
            var toDesired = Flatten(destination - transform.position);
            if (toDesired.magnitude <= stopDistance)
            {
                ApplyGravityOnly();
                return;
            }

            var direction = toDesired.sqrMagnitude > 0.04f ? toDesired.normalized : Vector3.zero;
            if (direction != Vector3.zero)
            {
                FaceTowards(transform.position + direction);
            }

            Move(direction * speed);
        }

        private void UpdatePath(Vector3 destination, float interval)
        {
            if (layout == null || (Time.time < repathAt && path.Count > 0))
            {
                return;
            }

            repathAt = Time.time + interval;
            path.Clear();
            path.AddRange(layout.FindPath(layout.GetNearestRoom(transform.position), layout.GetNearestRoom(destination)));
        }

        private void Move(Vector3 planarVelocity)
        {
            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1f;
            }

            planarVelocity.y = verticalVelocity;
            controller.Move(planarVelocity * Time.deltaTime);

            var tryingToMove = Flatten(planarVelocity).sqrMagnitude > 2f;
            var moved = hasLastPosition ? FlatDistance(transform.position, lastPosition) : 1f;
            if (tryingToMove && moved < 0.018f)
            {
                stuckTimer += Time.deltaTime;
            }
            else
            {
                stuckTimer = 0f;
            }

            if (stuckTimer >= 0.48f)
            {
                BeginStuckRecovery(planarVelocity);
            }
        }

        private void ApplyGravityOnly()
        {
            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1f;
            }

            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
        }

        private void BeginStuckRecovery(Vector3 attemptedVelocity)
        {
            stuckTimer = 0f;
            recoveryUntil = Time.time + 0.55f;
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

            var currentRoom = layout != null ? layout.GetNearestRoom(transform.position) : Vector2Int.zero;
            var towardRoomCenter = layout != null && layout.TryGetCenter(currentRoom, out var center)
                ? Flatten(center - transform.position).normalized
                : Vector3.zero;
            recoveryDirection = (side * 0.72f + towardRoomCenter * 0.95f).normalized;
            if (recoveryDirection.sqrMagnitude < 0.1f)
            {
                recoveryDirection = side.normalized;
            }

            path.Clear();
            repathAt = 0f;
        }

        private void UpdatePlayerTravelDirection()
        {
            var playerDelta = Flatten(player.position - lastPlayerPosition);
            if (playerDelta.sqrMagnitude > 0.0064f)
            {
                playerTravelDirection = Vector3.Slerp(playerTravelDirection, playerDelta.normalized, 0.42f).normalized;
            }
        }

        private Vector3 GetPlayerFollowPosition(bool playerMoving)
        {
            if (layout == null)
            {
                return player.position - playerTravelDirection * 2.9f + GetTravelRight() * (1.75f * followSide);
            }

            var currentRoom = layout.GetNearestRoom(transform.position);
            var playerRoom = layout.GetNearestRoom(player.position);
            if (currentRoom == playerRoom)
            {
                var playerDistance = FlatDistance(transform.position, player.position);
                if (!playerMoving && playerDistance >= 2.35f && playerDistance <= 6.1f)
                {
                    return transform.position;
                }

                var roomCenter = layout.TryGetCenter(playerRoom, out var center) ? center : player.position;
                var directionFromPlayer = Flatten(transform.position - player.position);
                if (directionFromPlayer.sqrMagnitude < 0.2f)
                {
                    directionFromPlayer = -playerTravelDirection + GetTravelRight() * followSide;
                }

                directionFromPlayer.Normalize();
                var trailingDistance = playerMoving ? 3.05f : playerDistance < 2.35f ? 3.35f : 4.7f;
                var sideDistance = playerMoving ? 1.85f : 0.45f;
                var looseOffset = playerMoving
                    ? -playerTravelDirection * trailingDistance + GetTravelRight() * (sideDistance * followSide)
                    : directionFromPlayer * trailingDistance + GetTravelRight() * (sideDistance * followSide);
                var desired = player.position + looseOffset;
                desired = Vector3.Lerp(desired, roomCenter, 0.08f);
                desired.y = player.position.y;
                return desired;
            }

            return player.position;
        }

        private Vector3 GetCombatSupportPosition(CombatantHealth target)
        {
            if (target == null)
            {
                return GetPlayerFollowPosition(false);
            }

            var toTarget = Flatten(target.transform.position - player.position);
            var aimDirection = toTarget.sqrMagnitude > 0.1f ? toTarget.normalized : playerTravelDirection;
            var right = Vector3.Cross(Vector3.up, aimDirection).normalized;
            var desired = player.position - aimDirection * 0.85f + right * (2.15f * followSide);
            if (layout != null)
            {
                var playerRoom = layout.GetNearestRoom(player.position);
                if (layout.TryGetCenter(playerRoom, out var roomCenter))
                {
                    desired = Vector3.Lerp(desired, roomCenter, 0.05f);
                }
            }

            desired.y = player.position.y;
            return desired;
        }

        private Vector3 GetCombatManeuverPosition(CombatantHealth target, bool canSeeTarget)
        {
            if (target == null)
            {
                return transform.position;
            }

            var distanceToTarget = FlatDistance(transform.position, target.transform.position);
            var shouldReposition = Time.time >= nextCombatMoveAt ||
                                   FlatDistance(transform.position, combatDestination) < 0.9f ||
                                   combatDestination == Vector3.zero;
            if (!shouldReposition)
            {
                return combatDestination;
            }

            nextCombatMoveAt = Time.time + Random.Range(1.1f, 1.9f);
            if (!canSeeTarget)
            {
                combatDestination = target.transform.position;
                return combatDestination;
            }

            if (TryPickDefensivePosition(target, out var cover))
            {
                combatDestination = cover;
                return combatDestination;
            }

            var away = Flatten(transform.position - target.transform.position);
            if (away.sqrMagnitude < 0.1f)
            {
                away = -transform.forward;
            }

            away.Normalize();
            var side = Vector3.Cross(Vector3.up, away).normalized * followSide;
            var rangeBias = distanceToTarget < 7f
                ? away * 2.2f
                : distanceToTarget > 14f ? -away * 2.1f : Vector3.zero;
            combatDestination = transform.position + side * Random.Range(1.4f, 2.4f) + rangeBias;
            combatDestination.y = player != null ? player.position.y : transform.position.y;
            return combatDestination;
        }

        private Vector3 GetAlertCoverPosition()
        {
            if (Time.time < coverUntil)
            {
                return coverDestination;
            }

            if (Time.time >= nextCombatMoveAt || combatDestination == Vector3.zero || FlatDistance(transform.position, combatDestination) < 0.9f)
            {
                nextCombatMoveAt = Time.time + Random.Range(1.1f, 1.8f);
                if (TryPickDefensivePosition(null, out var cover))
                {
                    combatDestination = cover;
                    return combatDestination;
                }

                var away = Flatten(transform.position - threatPosition);
                if (away.sqrMagnitude < 0.1f)
                {
                    away = -transform.forward;
                }

                combatDestination = transform.position + away.normalized * 2.4f;
                combatDestination.y = player != null ? player.position.y : transform.position.y;
            }

            return combatDestination;
        }

        private void UpdateAlertScan()
        {
            if (Time.time >= nextScanAt)
            {
                nextScanAt = Time.time + Random.Range(1.15f, 2.05f);
                scanUntil = Time.time + Random.Range(0.45f, 0.8f);
                var toThreat = Flatten(threatPosition - transform.position);
                if (toThreat.sqrMagnitude < 0.1f)
                {
                    toThreat = transform.forward;
                }

                var side = Vector3.Cross(Vector3.up, toThreat.normalized) * (Random.value > 0.5f ? 1f : -1f);
                scanPoint = transform.position + (toThreat.normalized * 6f + side * Random.Range(1.8f, 3.2f));
            }

            FaceTowards(Time.time < scanUntil ? scanPoint : threatPosition);
        }


        private void UpdateDefensiveStance(CombatantHealth target, bool canSeeTarget)
        {
            var threatened = Time.time < underFireUntil || canSeeTarget;
            if (threatened && Time.time >= nextCoverDecisionAt)
            {
                nextCoverDecisionAt = Time.time + Random.Range(0.85f, 1.45f);
                var wantsCover = Time.time < underFireUntil || damagePressure >= 1.8f || Random.value < 0.28f;
                if (wantsCover && TryPickDefensivePosition(target, out coverDestination))
                {
                    coverUntil = Time.time + Random.Range(damagePressure >= 2.4f ? 1.45f : 0.85f, damagePressure >= 2.4f ? 2.45f : 1.35f);
                }
            }

            if (Time.time < underFireUntil || canSeeTarget && Random.value < Time.deltaTime * 0.45f)
            {
                crouchUntil = Mathf.Max(crouchUntil, Time.time + Random.Range(0.8f, 1.35f));
            }

            var crouching = Time.time < crouchUntil;
            ApplyCrouchPose(crouching, 5.2f);
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

        private void OnDamaged(CombatantHealth damagedHealth)
        {
            lastCombatAt = Time.time;
            underFireUntil = Time.time + 3.1f;
            damagePressure = Mathf.Min(4f, damagePressure + 1f);
            crouchUntil = Time.time + Random.Range(1f, 1.8f);
            if (damagedHealth != null && damagedHealth.LastAttacker != null)
            {
                threatPosition = damagedHealth.LastAttacker.transform.position;
                match?.ReportArmyThreat(threatPosition, transform.position - threatPosition);
            }
            else
            {
                threatPosition = match != null && match.TryGetRecentArmyThreat(out var recentThreat, out _)
                    ? recentThreat
                    : transform.position - transform.forward * 5f;
            }

            if (TryPickDefensivePosition(null, out coverDestination))
            {
                coverUntil = Time.time + Random.Range(1.05f, 1.65f);
            }

            path.Clear();
            repathAt = 0f;
        }

        private void OnGunfire(Vector3 origin, Vector3 direction, CombatantHealth shooter, float radius)
        {
            if (match == null || !match.IsMatchActive || health == null || !health.IsAlive || shooter == health)
            {
                return;
            }

            var distance = Vector3.Distance(transform.position, origin);
            if (distance > radius)
            {
                return;
            }

            var shooterIsEnemy = shooter != null && shooter.GetComponent<DroidController>() != null;
            var shooterIsFriendly = shooter != null &&
                                    (shooter.GetComponent<PlayerFpsController>() != null ||
                                     shooter.GetComponent<PlayerTurret>() != null ||
                                     shooter.GetComponent<CompanionDroidController>() != null);

            lastCombatAt = Time.time;
            threatPosition = shooterIsEnemy
                ? origin
                : origin + direction * Mathf.Min(12f, Mathf.Max(3f, distance * 0.55f));

            if (shooterIsEnemy)
            {
                match?.ReportArmyThreat(origin, direction);
                underFireUntil = Mathf.Max(underFireUntil, Time.time + 2.8f);
                damagePressure = Mathf.Min(4f, damagePressure + 0.55f);
                crouchUntil = Mathf.Max(crouchUntil, Time.time + Random.Range(1f, 1.9f));
                if (TryPickDefensivePosition(null, out coverDestination))
                {
                    coverUntil = Mathf.Max(coverUntil, Time.time + Random.Range(1.1f, 2.1f));
                    combatDestination = coverDestination;
                    nextCombatMoveAt = Time.time + Random.Range(1.2f, 2f);
                }
            }
            else if (shooterIsFriendly)
            {
                if (match != null && match.TryGetRecentArmyThreat(out var recentThreat, out _))
                {
                    threatPosition = recentThreat;
                    lastCombatAt = Time.time;
                }
            }
            else
            {
                crouchUntil = Mathf.Max(crouchUntil, Time.time + 0.55f);
            }

            path.Clear();
            repathAt = 0f;
        }

        private bool TryPickDefensivePosition(CombatantHealth target, out Vector3 destination)
        {
            destination = transform.position;
            if (layout == null)
            {
                return false;
            }

            var currentRoom = layout.GetNearestRoom(transform.position);
            if (!layout.TryGetCenter(currentRoom, out var roomCenter))
            {
                return false;
            }

            var threat = target != null ? target.transform.position : threatPosition;
            if (TryPickCoverObjectPosition(threat, out destination))
            {
                return true;
            }

            var away = Flatten(transform.position - threat);
            if (away.sqrMagnitude < 0.1f)
            {
                away = Flatten(roomCenter - threat);
            }

            if (away.sqrMagnitude < 0.1f)
            {
                away = -transform.forward;
            }

            away.Normalize();
            var side = Vector3.Cross(Vector3.up, away).normalized * (Random.value > 0.5f ? 1f : -1f);
            destination = roomCenter + side * Random.Range(2.2f, 3.2f) + away * Random.Range(0.7f, 1.35f);
            destination.y = player != null ? player.position.y : transform.position.y;
            return true;
        }

        private bool TryPickCoverObjectPosition(Vector3 threat, out Vector3 destination)
        {
            destination = transform.position;
            var currentRoom = layout != null ? layout.GetNearestRoom(transform.position) : Vector2Int.zero;
            var colliders = Physics.OverlapSphere(transform.position, 9f, ~0, QueryTriggerInteraction.Ignore);
            var bestScore = float.NegativeInfinity;
            for (var i = 0; i < colliders.Length; i++)
            {
                var cover = colliders[i];
                if (cover == null || cover.isTrigger || IsFloorLike(cover) || cover.transform.IsChildOf(transform))
                {
                    continue;
                }

                var coverName = cover.name.ToLowerInvariant();
                var parentName = cover.transform.parent != null ? cover.transform.parent.name.ToLowerInvariant() : "";
                var coverLike = coverName.Contains("pillar") ||
                                parentName.Contains("pillar") ||
                                coverName.Contains("wall") ||
                                parentName.Contains("wall");
                if (!coverLike)
                {
                    continue;
                }

                var center = cover.bounds.center;
                if (layout != null && layout.GetNearestRoom(center) != currentRoom)
                {
                    continue;
                }

                var away = Flatten(center - threat);
                if (away.sqrMagnitude < 0.1f)
                {
                    continue;
                }

                away.Normalize();
                var side = Vector3.Cross(Vector3.up, away).normalized * (Random.value > 0.5f ? 1f : -1f);
                var candidate = center + away * 1.05f + side * (damagePressure >= 2.2f ? 0.25f : 0.68f);
                if (!TryGroundCoverPoint(candidate, out candidate))
                {
                    continue;
                }

                var threatDot = Vector3.Dot(Flatten(candidate - center).normalized, away);
                var score = threatDot * 4f - FlatDistance(candidate, transform.position) * 0.18f + Random.Range(-0.4f, 0.4f);
                if (score > bestScore)
                {
                    bestScore = score;
                    destination = candidate;
                }
            }

            return bestScore > float.NegativeInfinity;
        }

        private bool TryGroundCoverPoint(Vector3 candidate, out Vector3 grounded)
        {
            grounded = candidate;
            if (!Physics.Raycast(candidate + Vector3.up * 2.4f, Vector3.down, out var hit, 5.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (hit.normal.y < 0.68f || !IsFloorLike(hit.collider))
            {
                return false;
            }

            grounded = hit.point + Vector3.up * 1.1f;
            var overlaps = Physics.OverlapCapsule(grounded + Vector3.down * 0.78f, grounded + Vector3.up * 0.86f, 0.38f, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < overlaps.Length; i++)
            {
                var overlap = overlaps[i];
                if (overlap == null || overlap.isTrigger || IsFloorLike(overlap) || overlap.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (overlap.GetComponentInParent<CombatantHealth>() != null)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private Vector3 GetThroughDoorwayPoint(Vector2Int fromRoom, Vector2Int toRoom, Vector3 doorway)
        {
            if (layout != null && layout.TryGetCenter(toRoom, out var nextCenter))
            {
                var direction = Flatten(nextCenter - doorway);
                if (direction.sqrMagnitude > 0.01f)
                {
                    doorway += direction.normalized * 1.45f;
                }
            }

            doorway.y = player != null ? player.position.y : transform.position.y;
            return doorway;
        }

        private bool CanSeeTarget(CombatantHealth target)
        {
            var origin = blasterRig != null && blasterRig.Muzzle != null
                ? blasterRig.Muzzle.position
                : transform.position + Vector3.up * 0.78f;
            var aimPoint = target.transform.position + Vector3.up * 0.72f;
            var toTarget = aimPoint - origin;
            if (toTarget.sqrMagnitude < 0.01f)
            {
                return true;
            }

            var hits = Physics.RaycastAll(origin, toTarget.normalized, toTarget.magnitude + 0.18f, ~0, QueryTriggerInteraction.Ignore);
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

        private void FaceTowards(Vector3 point)
        {
            var flat = point - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 520f * Time.deltaTime);
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        private Vector3 GetTravelRight()
        {
            var right = Vector3.Cross(Vector3.up, playerTravelDirection);
            if (right.sqrMagnitude < 0.01f)
            {
                right = Flatten(player.right);
            }

            return right.sqrMagnitude > 0.01f ? right.normalized : Vector3.right;
        }

        private static bool IsSameOrAdjacentRoom(Vector2Int a, Vector2Int b)
        {
            var delta = a - b;
            return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) <= 1;
        }

        private static bool IsFloorLike(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            var name = collider.name.ToLowerInvariant();
            var parentName = collider.transform.parent != null ? collider.transform.parent.name.ToLowerInvariant() : "";
            return name.Contains("floor") ||
                   parentName.Contains("floor") ||
                   name.Contains("plate") ||
                   parentName.Contains("plate");
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            return Flatten(a - b).magnitude;
        }
    }
}
