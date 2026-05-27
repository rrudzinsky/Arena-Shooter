using System.Collections.Generic;
using UnityEngine;

namespace ArenaShooter
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CombatantHealth))]
    [RequireComponent(typeof(WeaponInventory))]
    public sealed class OpponentController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5.1f;
        [SerializeField] private float weaponRushSpeed = 7.25f;
        [SerializeField] private float retreatSpeed = 6.6f;
        [SerializeField] private float turnSpeed = 540f;
        [SerializeField] private float eyeHeight = 0.65f;
        [SerializeField] private float stoppingDistance = 7.2f;
        [SerializeField] private float retreatHealthRatio = 0.38f;
        [SerializeField] private float awarenessRadius = 13f;
        [SerializeField] private float closeDiscoveryRadius = 3.5f;
        [SerializeField] private float lastKnownPlayerMemory = 6f;
        [SerializeField] private float playerVisionAngle = 58f;
        [SerializeField] private float minimumReactionTime = 0.45f;
        [SerializeField] private float maximumReactionTime = 0.9f;
        [SerializeField] private float sweepTowardArenaAfter = 18f;

        private MatchController match;
        private ArenaLayout layout;
        private CharacterController controller;
        private CombatantHealth health;
        private WeaponInventory weapons;
        private SpawnIntroWalker introWalker;
        private Transform player;
        private readonly List<Vector2Int> path = new();
        private readonly List<Transform> knownWeapons = new();
        private readonly List<Transform> knownHealing = new();
        private readonly HashSet<Vector2Int> discoveredRooms = new();
        private readonly Dictionary<Vector2Int, int> roomVisitCounts = new();
        private Vector3 targetPoint;
        private Vector3 lastKnownPlayerPosition;
        private Vector3 lastPosition;
        private float repathAt;
        private float verticalVelocity;
        private float nextEvadeAt;
        private float nextExplorePickAt;
        private float playerKnownUntil;
        private float playerEngageAt;
        private float lastPlayerContactAt;
        private float stuckTimer;
        private float evadeTimer;
        private float evadeDuration;
        private float evadeSpeed;
        private Vector3 evadeDirection;
        private bool sliding;
        private bool wasSeeingPlayer;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<CombatantHealth>();
            weapons = GetComponent<WeaponInventory>();
            introWalker = GetComponent<SpawnIntroWalker>();
            health.Damaged += OnDamaged;
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
            }
        }

        public void Configure(MatchController owner, ArenaLayout arenaLayout, Transform playerTransform)
        {
            match = owner;
            layout = arenaLayout;
            player = playerTransform;
            targetPoint = transform.position;
            var startRoom = layout.GetNearestRoom(transform.position);
            DiscoverRoom(startRoom);
            lastKnownPlayerPosition = transform.position;
            lastPosition = transform.position;
            lastPlayerContactAt = Time.time;
        }

        private void Update()
        {
            if (introWalker == null)
            {
                introWalker = GetComponent<SpawnIntroWalker>();
            }

            if (match == null || layout == null || !match.IsMatchActive || health == null || !health.IsAlive || player == null || (introWalker != null && introWalker.IsWalking))
            {
                return;
            }

            var eye = transform.position + Vector3.up * eyeHeight;
            var playerEye = player.position + Vector3.up * 0.65f;
            var canSeePlayer = CanSee(playerEye, eye);
            var canEngagePlayer = UpdatePlayerAwareness(canSeePlayer);
            var hurt = health.CurrentHealth / health.MaxHealth <= retreatHealthRatio;
            SenseWorld(eye);

            if (evadeTimer > 0f)
            {
                UpdateEvadeMovement();
                if (weapons.HasWeapon && canEngagePlayer)
                {
                    FaceTowards(playerEye);
                    weapons.TryFire(eye, playerEye - eye);
                }

                return;
            }

            if (!weapons.HasWeapon)
            {
                var knownWeapon = GetClosestKnown(knownWeapons);
                var weaponDestination = knownWeapon != null ? knownWeapon.position : ChooseExploreDestination();
                UpdatePath(weaponDestination, 0.22f);
                MoveAlongPath(weaponDestination, weaponRushSpeed, 0.45f);
                return;
            }

            if (hurt)
            {
                if (canSeePlayer)
                {
                    FaceTowards(playerEye);
                    if (canEngagePlayer)
                    {
                        weapons.TryFire(eye, playerEye - eye);
                        TryBeginEvade(preferSlide: false);
                    }
                }

                var healingTarget = GetClosestKnown(knownHealing);
                var retreatDestination = healingTarget != null ? healingTarget.position : ChooseRetreatExploreDestination();
                UpdatePath(retreatDestination, 0.35f);
                MoveAlongPath(retreatDestination, retreatSpeed, 0.65f);
                return;
            }

            if (weapons.HasWeapon && canSeePlayer)
            {
                FaceTowards(playerEye);
                if (canEngagePlayer)
                {
                    weapons.TryFire(eye, playerEye - eye);
                    TryBeginEvade(preferSlide: false);
                    StrafeNearPlayer(playerEye);
                }
                else
                {
                    ApplyGravityOnly();
                }

                return;
            }

            var destination = Time.time < playerKnownUntil ? lastKnownPlayerPosition : ChooseExploreDestination();
            UpdatePath(destination, 0.42f);
            MoveAlongPath(destination, moveSpeed, stoppingDistance);
        }

        private void SenseWorld(Vector3 eye)
        {
            var currentRoom = layout.GetNearestRoom(transform.position);
            DiscoverRoom(currentRoom);

            var hits = Physics.OverlapSphere(transform.position, awarenessRadius, ~0, QueryTriggerInteraction.Collide);
            foreach (var hit in hits)
            {
                var weapon = hit.GetComponentInParent<WeaponPickup>();
                if (weapon != null && CanDiscover(weapon.transform, eye))
                {
                    Remember(knownWeapons, weapon.transform);
                    continue;
                }

                var healthPickup = hit.GetComponentInParent<HealthPickup>();
                if (healthPickup != null && CanDiscover(healthPickup.transform, eye))
                {
                    Remember(knownHealing, healthPickup.transform);
                    continue;
                }

                var station = hit.GetComponentInParent<HealingStation>();
                if (station != null && station.IsAvailable && CanDiscover(station.transform, eye))
                {
                    Remember(knownHealing, station.transform);
                }
            }

            ForgetMissing(knownWeapons);
            ForgetMissing(knownHealing);
        }

        private bool CanDiscover(Transform target, Vector3 eye)
        {
            var toTarget = target.position - transform.position;
            if (toTarget.magnitude <= closeDiscoveryRadius)
            {
                return true;
            }

            var targetPoint = target.position + Vector3.up * 0.35f;
            var direction = targetPoint - eye;
            if (Vector3.Angle(transform.forward, direction) > 115f)
            {
                return false;
            }

            if (Physics.Raycast(eye, direction.normalized, out var hit, direction.magnitude + 0.1f, ~0, QueryTriggerInteraction.Collide))
            {
                return hit.transform == target || hit.transform.IsChildOf(target) || target.IsChildOf(hit.transform);
            }

            return false;
        }

        private void Remember(List<Transform> memory, Transform target)
        {
            if (target != null && !memory.Contains(target))
            {
                memory.Add(target);
            }
        }

        private void ForgetMissing(List<Transform> memory)
        {
            for (var i = memory.Count - 1; i >= 0; i--)
            {
                if (memory[i] == null)
                {
                    memory.RemoveAt(i);
                }
            }
        }

        private Transform GetClosestKnown(List<Transform> memory)
        {
            ForgetMissing(memory);
            Transform best = null;
            var bestDistance = float.PositiveInfinity;

            foreach (var target in memory)
            {
                var distance = (target.position - transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = target;
                }
            }

            return best;
        }

        private Vector3 ChooseExploreDestination()
        {
            if (Time.time < nextExplorePickAt && (targetPoint - transform.position).sqrMagnitude > 2.5f)
            {
                return targetPoint;
            }

            nextExplorePickAt = Time.time + Random.Range(0.55f, 1.2f);
            var currentRoom = layout.GetNearestRoom(transform.position);
            DiscoverRoom(currentRoom);
            var neighbors = layout.GetConnectedNeighbors(currentRoom);
            if (neighbors.Count == 0)
            {
                return transform.position;
            }

            var bestRoom = neighbors[Random.Range(0, neighbors.Count)];
            var bestScore = int.MaxValue;
            foreach (var neighbor in neighbors)
            {
                roomVisitCounts.TryGetValue(neighbor, out var visits);
                var score = visits + (discoveredRooms.Contains(neighbor) ? 1 : 0);
                if (Time.time - lastPlayerContactAt > sweepTowardArenaAfter && layout.TryGetCenter(neighbor, out var neighborCenter))
                {
                    score += Mathf.RoundToInt(neighborCenter.magnitude * 0.08f);
                }

                if (score < bestScore || (score == bestScore && Random.value < 0.35f))
                {
                    bestScore = score;
                    bestRoom = neighbor;
                }
            }

            DiscoverRoom(bestRoom);
            return layout.TryGetCenter(bestRoom, out var center) ? center : transform.position;
        }

        private Vector3 ChooseRetreatExploreDestination()
        {
            var currentRoom = layout.GetNearestRoom(transform.position);
            var neighbors = layout.GetConnectedNeighbors(currentRoom);
            if (neighbors.Count == 0)
            {
                return ChooseExploreDestination();
            }

            var best = transform.position;
            var bestDistance = -1f;
            foreach (var neighbor in neighbors)
            {
                if (!layout.TryGetCenter(neighbor, out var center))
                {
                    continue;
                }

                var distanceFromPlayer = (center - player.position).sqrMagnitude;
                if (distanceFromPlayer > bestDistance)
                {
                    bestDistance = distanceFromPlayer;
                    best = center;
                }
            }

            return best;
        }

        private void DiscoverRoom(Vector2Int room)
        {
            discoveredRooms.Add(room);
            roomVisitCounts.TryGetValue(room, out var visits);
            roomVisitCounts[room] = visits + 1;
        }

        private void UpdatePath(Vector3 destination, float interval)
        {
            if (Time.time < repathAt && path.Count > 0)
            {
                return;
            }

            repathAt = Time.time + interval;
            path.Clear();

            var start = layout.GetNearestRoom(transform.position);
            var goal = layout.GetNearestRoom(destination);
            path.AddRange(layout.FindPath(start, goal));
        }

        private void MoveAlongPath(Vector3 destination, float speed, float stopDistance)
        {
            if (path.Count > 0 && layout.TryGetCenter(path[0], out var roomCenter))
            {
                targetPoint = roomCenter;
                if (Vector3.Distance(Flatten(transform.position), Flatten(targetPoint)) < 1.1f)
                {
                    path.RemoveAt(0);
                }
            }
            else
            {
                targetPoint = destination;
            }

            var flatToTarget = Flatten(targetPoint - transform.position);
            if (flatToTarget.magnitude <= stopDistance && weapons.HasWeapon)
            {
                ApplyGravityOnly();
                return;
            }

            var direction = flatToTarget.sqrMagnitude > 0.04f ? flatToTarget.normalized : Vector3.zero;
            if (direction != Vector3.zero)
            {
                FaceTowards(transform.position + direction);
            }

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1.5f;
            }

            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            var movement = direction * speed;
            movement.y = verticalVelocity;
            controller.Move(movement * Time.deltaTime);
            UpdateStuckRecovery(direction);
        }

        private void StrafeNearPlayer(Vector3 playerEye)
        {
            var away = Flatten(transform.position - player.position);
            if (away.magnitude > 8.5f)
            {
                MoveAlongPath(player.position, moveSpeed, stoppingDistance);
                return;
            }

            var strafe = Vector3.Cross(Vector3.up, (playerEye - transform.position).normalized).normalized;
            if (Mathf.Sin(Time.time * 0.9f) < 0f)
            {
                strafe = -strafe;
            }

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1.5f;
            }

            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            var movement = strafe * (moveSpeed * 0.9f);
            movement.y = verticalVelocity;
            controller.Move(movement * Time.deltaTime);
            UpdateStuckRecovery(strafe);
        }

        private void TryBeginEvade(bool preferSlide)
        {
            if (Time.time < nextEvadeAt || !controller.isGrounded)
            {
                return;
            }

            if (Random.value > 0.22f)
            {
                return;
            }

            var toPlayer = Flatten(player.position - transform.position).normalized;
            var side = Vector3.Cross(Vector3.up, toPlayer).normalized;
            if (Random.value < 0.5f)
            {
                side = -side;
            }

            var wantsSlide = preferSlide || Random.value < 0.35f;
            evadeDirection = wantsSlide ? -toPlayer : side;
            evadeDuration = wantsSlide ? 0.42f : 0.22f;
            evadeSpeed = wantsSlide ? retreatSpeed * 1.2f : retreatSpeed * 1.45f;
            evadeTimer = evadeDuration;
            sliding = wantsSlide;
            nextEvadeAt = Time.time + Random.Range(0.85f, 1.45f);
        }

        private void UpdateEvadeMovement()
        {
            evadeTimer = Mathf.Max(0f, evadeTimer - Time.deltaTime);

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1.5f;
            }

            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            var movement = evadeDirection * evadeSpeed;
            movement.y = verticalVelocity;
            controller.Move(movement * Time.deltaTime);

            if (sliding)
            {
                controller.height = Mathf.MoveTowards(controller.height, 1.1f, Time.deltaTime * 7f);
                controller.center = new Vector3(0f, -0.36f, 0f);
            }

            if (evadeTimer <= 0f)
            {
                sliding = false;
                controller.height = 1.9f;
                controller.center = Vector3.zero;
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

        private bool CanSee(Vector3 target, Vector3 eye)
        {
            var direction = target - eye;
            if (Vector3.Angle(transform.forward, direction) > playerVisionAngle)
            {
                return false;
            }

            if (Physics.Raycast(eye, direction.normalized, out var hit, direction.magnitude + 0.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.collider.GetComponentInParent<PlayerFpsController>() != null;
            }

            return false;
        }

        private bool UpdatePlayerAwareness(bool canSeePlayer)
        {
            if (!canSeePlayer)
            {
                wasSeeingPlayer = false;
                return false;
            }

            lastPlayerContactAt = Time.time;
            lastKnownPlayerPosition = player.position;
            playerKnownUntil = Time.time + lastKnownPlayerMemory;

            if (!wasSeeingPlayer)
            {
                wasSeeingPlayer = true;
                playerEngageAt = Time.time + Random.Range(minimumReactionTime, maximumReactionTime);
                return false;
            }

            return Time.time >= playerEngageAt;
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

        private void UpdateStuckRecovery(Vector3 intendedDirection)
        {
            var flatDelta = Flatten(transform.position - lastPosition);
            var tryingToMove = intendedDirection.sqrMagnitude > 0.15f;

            if (tryingToMove && flatDelta.magnitude < 0.025f)
            {
                stuckTimer += Time.deltaTime;
            }
            else
            {
                stuckTimer = 0f;
            }

            lastPosition = transform.position;

            if (stuckTimer < 0.45f)
            {
                return;
            }

            stuckTimer = 0f;
            path.Clear();
            repathAt = 0f;

            var side = Vector3.Cross(Vector3.up, intendedDirection.normalized);
            if (Random.value < 0.5f)
            {
                side = -side;
            }

            evadeDirection = side.normalized;
            evadeDuration = 0.22f;
            evadeSpeed = retreatSpeed * 1.25f;
            evadeTimer = evadeDuration;
            sliding = false;
            nextExplorePickAt = 0f;
        }

        private void OnDamaged(CombatantHealth damagedHealth)
        {
            if (damagedHealth.CurrentHealth / damagedHealth.MaxHealth <= retreatHealthRatio)
            {
                TryBeginEvade(preferSlide: true);
            }
            else if (Random.value < 0.55f)
            {
                TryBeginEvade(preferSlide: false);
            }
        }
    }
}
