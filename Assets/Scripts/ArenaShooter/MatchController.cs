using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ArenaShooter
{
    public class MatchController : MonoBehaviour
    {
        private enum ArenaGameMode
        {
            KingOfTheColosseum,
            AllOutWar
        }

        [Header("Arena")]
        [SerializeField] private int seed;
        [SerializeField] private bool randomizeSeed = true;
        [SerializeField] private int roomCount = 11;
        [SerializeField] private int gridRadius = 3;
        [SerializeField] private float roomSize = 10f;
        [SerializeField] private float corridorLength = 6f;
        [SerializeField] private float corridorWidth = 3.2f;
        [SerializeField] private float wallHeight = 4.8f;
        [SerializeField] private int weaponPickupCount = 4;
        [SerializeField] private int perimeterGateCount = 6;
        [SerializeField] private int healthPickupCount = 4;

        [Header("Actors")]
        [SerializeField] private float playerHealth = 100f;
        [SerializeField] private float opponentHealth = 100f;

        [Header("Wave Prototype")]
        [SerializeField] private int finalWave = 5;
        [SerializeField] private int firstWaveDroids = 6;
        [SerializeField] private int baseScrapDrop = 9;

        private const float WaveCountdownSeconds = 45f;
        private const int AllOutWarSquadSize = 4;
        private const float AllOutWarPickupRespawnSeconds = 30f;

        private readonly List<WeaponPickup> activePickups = new();
        private readonly List<AmmoPickup> activeAmmoPickups = new();
        private readonly List<HealthPickup> activeHealthPickups = new();
        private readonly List<HealingStation> healingStations = new();
        private readonly List<CombatantHealth> activeDroids = new();
        private readonly List<CombatantHealth> allOutWarAiSoldiers = new();
        private readonly List<PickupPadSlot> pickupPadSlots = new();
        private readonly List<PlayerTurret> activeTurrets = new();
        private readonly List<GameObject> activeCompanions = new();
        private readonly List<ArmyThreatReport> armyThreatReports = new();
        private readonly Dictionary<GameObject, ArmyUnitUpgradeState> armyUpgradeStates = new();
        private readonly Dictionary<int, AllOutWarArmyFrontState> allOutWarFronts = new();
        private readonly Dictionary<int, Dictionary<Vector2Int, float>> allOutWarRoomSearchTimes = new();
        private readonly Dictionary<CombatantHealth, AllOutWarSquadMemberInfo> allOutWarSquadMembers = new();
        private static readonly string[] CompanionNames =
        {
            "Samuel",
            "Arthur",
            "Henry",
            "Milo",
            "Elliot",
            "Theo",
            "Oliver",
            "Calvin",
            "Felix",
            "Jonah"
        };
        private GameObject matchRoot;
        private ArenaTheme theme;
        private PrototypeHud hud;
        private ArenaLayout currentLayout;
        private GameObject playerObject;
        private CombatantHealth playerCombatant;
        private CombatantHealth opponentCombatant;
        private WeaponInventory playerWeapons;
        private GameObject selectedArmyUnit;
        private Vector3 fabricatorRoomCenter;
        private Coroutine waveRoutine;
        private int currentWave;
        private int scrap;
        private int damageUpgradeLevel;
        private int companionUpgradeLevel;
        private int companionDamageUpgradeLevel;
        private int turretArmorUpgradeLevel;
        private int turretKits;
        private bool turretPlacementMode;
        private bool turretMobilityUpgrade;
        private bool hasFabricatorRoom;
        private ArenaGameMode currentGameMode = ArenaGameMode.KingOfTheColosseum;
        private AllOutWarSettings allOutWarSettings;
        private int[] allOutWarRosterRemaining;
        private int[] allOutWarActiveByTeam;
        private float[] allOutWarNextSpawnAt;
        private int allOutWarSpawnCursor;
        private bool playerRespawning;
        private bool allOutWarMatchEnded;

        private sealed class AllOutWarSettings
        {
            public int OpponentArmies;
            public int SoldiersPerArmy;
            public int BattlefieldCap;
            public string MapStyle;
            public int TotalArmies => Mathf.Max(2, OpponentArmies + 1);

            public static AllOutWarSettings FromPlayerPrefs()
            {
                return new AllOutWarSettings
                {
                    OpponentArmies = Mathf.Clamp(PlayerPrefs.GetInt("AllOutWarOpponentArmies", 3), 1, 7),
                    SoldiersPerArmy = Mathf.Clamp(PlayerPrefs.GetInt("AllOutWarSoldiersPerArmy", 100), 1, 500),
                    BattlefieldCap = Mathf.Clamp(PlayerPrefs.GetInt("AllOutWarBattlefieldCap", 80), 1, 600),
                    MapStyle = PlayerPrefs.GetString("AllOutWarMapStyle", "Randomly Generate")
                };
            }
        }

        private sealed class PickupPadSlot
        {
            public Vector3 Position;
            public GameObject Occupant;
        }

        private sealed class ArmyUnitUpgradeState
        {
            public bool HealthBoostInstalled;
            public bool ShieldInstalled;
            public bool MobilityInstalled;
        }

        private struct ArmyThreatReport
        {
            public Vector3 Position;
            public Vector3 Direction;
            public float ReportedAt;
        }

        private enum AllOutWarSearchSector
        {
            Center,
            Left,
            Right,
            WideLeft,
            WideRight,
            Reserve
        }

        private enum AllOutWarSearchPhase
        {
            Advance,
            Sweep,
            Collapse,
            Reinforce
        }

        public enum AllOutWarSquadSignal
        {
            None,
            EnemySpotted,
            ShotsExchanged,
            TakingDamage,
            AllyKilled,
            EnemyKilled,
            ContactLost,
            ResourceFound
        }

        public enum AllOutWarEngagementScale
        {
            None,
            ProbeContact,
            Skirmish,
            Firefight,
            HeavyEngagement,
            Overrun
        }

        public enum AllOutWarSquadDecision
        {
            Search,
            Probe,
            Fix,
            Flank,
            Collapse,
            Hold,
            Regroup,
            Heal,
            Resupply,
            ResumeSearch
        }

        private readonly struct AllOutWarSquadMemberInfo
        {
            public readonly int TeamId;
            public readonly int SquadId;
            public readonly int SlotIndex;

            public AllOutWarSquadMemberInfo(int teamId, int squadId, int slotIndex)
            {
                TeamId = teamId;
                SquadId = squadId;
                SlotIndex = slotIndex;
            }
        }

        private sealed class AllOutWarArmyFrontState
        {
            public int TeamId;
            public ArmySpawnRegion Region;
            public Vector3 PushDirection;
            public readonly Dictionary<Vector2Int, int> DistanceFromSpawn = new();
            public readonly Dictionary<int, AllOutWarSquadFrontState> Squads = new();
        }

        private sealed class AllOutWarSquadFrontState
        {
            public int SquadId;
            public AllOutWarSearchSector Sector;
            public AllOutWarSearchPhase Phase;
            public Vector3 SearchVector;
            public Vector2Int ObjectiveRoom;
            public int AdvanceIndex = -1;
            public Vector2Int LastSearchedRoom;
            public float AssignedAt;
            public bool HasObjective;
            public float HealthRatio = 1f;
            public AllOutWarSquadSignal Signal;
            public AllOutWarEngagementScale EngagementScale;
            public AllOutWarSquadDecision Decision = AllOutWarSquadDecision.Search;
            public Vector3 SignalPosition;
            public int SignalEnemyTeamId = -1;
        }

        public bool IsMatchActive { get; private set; }
        public bool IsAllOutWarMode => currentGameMode == ArenaGameMode.AllOutWar;
        public int AllOutWarArmyCount => allOutWarRosterRemaining != null
            ? allOutWarRosterRemaining.Length
            : allOutWarSettings != null
                ? allOutWarSettings.TotalArmies
                : 0;
        public int CurrentWave => currentWave;
        public int FinalWave => finalWave;
        public int Scrap => scrap;
        public int ActiveDroidCount => activeDroids.Count;
        public int WeaponUpgradeLevel => damageUpgradeLevel;
        public int TurretKits => turretKits;
        public bool IsTurretPlacementMode => turretPlacementMode;
        public float CurrentNormalDroidHealth => 52f + Mathf.Max(1, currentWave) * 9f;
        public int NextUpgradeCost => 45 + damageUpgradeLevel * 35;
        public IReadOnlyList<CombatantHealth> ActiveDroids => activeDroids;
        public bool IsFabricatorMenuOpen { get; private set; }
        public float InputSuppressedUntil { get; private set; }

        public int GetAllOutWarArmyActive(int teamId)
        {
            if (allOutWarActiveByTeam == null || teamId < 0 || teamId >= allOutWarActiveByTeam.Length)
            {
                return 0;
            }

            return Mathf.Max(0, allOutWarActiveByTeam[teamId]);
        }

        public int GetAllOutWarArmyReserve(int teamId)
        {
            if (allOutWarRosterRemaining == null || teamId < 0 || teamId >= allOutWarRosterRemaining.Length)
            {
                return 0;
            }

            return Mathf.Max(0, allOutWarRosterRemaining[teamId]);
        }

        public int GetAllOutWarArmyRemaining(int teamId)
        {
            var remaining = GetAllOutWarArmyActive(teamId) + GetAllOutWarArmyReserve(teamId);
            if (teamId == 0 && IsAllOutWarPlayerInMatch())
            {
                remaining++;
            }

            return remaining;
        }

        public int GetAllOutWarArmyStartingCount(int teamId)
        {
            return GetAllOutWarAiRosterSize(teamId) + (teamId == 0 ? 1 : 0);
        }

        private int GetAllOutWarAiRosterSize(int teamId)
        {
            var configuredRoster = allOutWarSettings != null ? Mathf.Max(1, allOutWarSettings.SoldiersPerArmy) : 1;
            return teamId == 0 ? Mathf.Max(0, configuredRoster - 1) : configuredRoster;
        }

        public CombatantHealth GetBestDroidTarget(Vector3 fromPosition)
        {
            CombatantHealth best = null;
            var bestDistance = float.PositiveInfinity;
            ConsiderTarget(playerCombatant, fromPosition, ref best, ref bestDistance);

            for (var i = activeCompanions.Count - 1; i >= 0; i--)
            {
                var companion = activeCompanions[i];
                if (companion == null)
                {
                    activeCompanions.RemoveAt(i);
                    continue;
                }

                if (companion.TryGetComponent<CombatantHealth>(out var companionHealth))
                {
                    ConsiderTarget(companionHealth, fromPosition, ref best, ref bestDistance);
                }
            }

            for (var i = activeTurrets.Count - 1; i >= 0; i--)
            {
                var turret = activeTurrets[i];
                if (turret == null)
                {
                    activeTurrets.RemoveAt(i);
                    continue;
                }

                if (turret.TryGetComponent<CombatantHealth>(out var turretHealth))
                {
                    ConsiderTarget(turretHealth, fromPosition, ref best, ref bestDistance);
                }
            }

            return best;
        }

        public CombatantHealth GetBestEnemyTarget(CombatantHealth seeker, Vector3 fromPosition)
        {
            CombatantHealth best = null;
            var bestDistance = float.PositiveInfinity;
            var targets = new List<CombatantHealth>();
            CollectEnemyTargets(seeker, targets);
            foreach (var target in targets)
            {
                ConsiderTarget(target, fromPosition, ref best, ref bestDistance);
            }

            return best;
        }

        public void CollectDroidTargets(List<CombatantHealth> targets)
        {
            if (targets == null)
            {
                return;
            }

            targets.Clear();
            AddDroidTarget(playerCombatant, targets);

            for (var i = activeCompanions.Count - 1; i >= 0; i--)
            {
                var companion = activeCompanions[i];
                if (companion == null)
                {
                    activeCompanions.RemoveAt(i);
                    continue;
                }

                if (companion.TryGetComponent<CombatantHealth>(out var companionHealth))
                {
                    AddDroidTarget(companionHealth, targets);
                }
            }

            for (var i = activeTurrets.Count - 1; i >= 0; i--)
            {
                var turret = activeTurrets[i];
                if (turret == null)
                {
                    activeTurrets.RemoveAt(i);
                    continue;
                }

                if (turret.TryGetComponent<CombatantHealth>(out var turretHealth))
                {
                    AddDroidTarget(turretHealth, targets);
                }
            }
        }

        public void CollectEnemyTargets(CombatantHealth seeker, List<CombatantHealth> targets)
        {
            if (targets == null)
            {
                return;
            }

            if (currentGameMode != ArenaGameMode.AllOutWar)
            {
                CollectDroidTargets(targets);
                return;
            }

            targets.Clear();
            AddEnemyTarget(seeker, playerCombatant, targets);

            for (var i = activeDroids.Count - 1; i >= 0; i--)
            {
                var droid = activeDroids[i];
                if (droid == null)
                {
                    activeDroids.RemoveAt(i);
                    continue;
                }

                AddEnemyTarget(seeker, droid, targets);
            }

            for (var i = activeCompanions.Count - 1; i >= 0; i--)
            {
                var companion = activeCompanions[i];
                if (companion == null)
                {
                    activeCompanions.RemoveAt(i);
                    continue;
                }

                if (companion.TryGetComponent<CombatantHealth>(out var companionHealth))
                {
                    AddEnemyTarget(seeker, companionHealth, targets);
                }
            }

            for (var i = activeTurrets.Count - 1; i >= 0; i--)
            {
                var turret = activeTurrets[i];
                if (turret == null)
                {
                    activeTurrets.RemoveAt(i);
                    continue;
                }

                if (turret.TryGetComponent<CombatantHealth>(out var turretHealth))
                {
                    AddEnemyTarget(seeker, turretHealth, targets);
                }
            }
        }

        public bool TryFindNearestHealthPickup(Vector3 fromPosition, float maxDistance, out HealthPickup pickup)
        {
            pickup = null;
            var bestDistance = maxDistance * maxDistance;
            for (var i = activeHealthPickups.Count - 1; i >= 0; i--)
            {
                var candidate = activeHealthPickups[i];
                if (candidate == null)
                {
                    activeHealthPickups.RemoveAt(i);
                    continue;
                }

                var distance = (candidate.transform.position - fromPosition).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    pickup = candidate;
                }
            }

            return pickup != null;
        }

        public bool TryFindNearestHealingStation(Vector3 fromPosition, out HealingStation station)
        {
            station = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var candidate in healingStations)
            {
                if (candidate == null)
                {
                    continue;
                }

                var distance = (candidate.transform.position - fromPosition).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    station = candidate;
                }
            }

            return station != null;
        }

        public bool TryFindNearestSafeHealthPickup(
            Vector3 fromPosition,
            float maxDistance,
            CombatantHealth seeker,
            float resourceDangerRadius,
            float routeDangerRadius,
            out HealthPickup pickup)
        {
            pickup = null;
            var bestDistance = maxDistance * maxDistance;
            for (var i = activeHealthPickups.Count - 1; i >= 0; i--)
            {
                var candidate = activeHealthPickups[i];
                if (candidate == null)
                {
                    activeHealthPickups.RemoveAt(i);
                    continue;
                }

                var candidatePosition = candidate.transform.position;
                var distance = (candidatePosition - fromPosition).sqrMagnitude;
                if (distance >= bestDistance || IsHealingResourceContested(fromPosition, candidatePosition, seeker, resourceDangerRadius, routeDangerRadius))
                {
                    continue;
                }

                bestDistance = distance;
                pickup = candidate;
            }

            return pickup != null;
        }

        public bool TryFindNearestSafeAmmoPickup(
            Vector3 fromPosition,
            float maxDistance,
            CombatantHealth seeker,
            float resourceDangerRadius,
            float routeDangerRadius,
            out AmmoPickup pickup)
        {
            pickup = null;
            var bestDistance = maxDistance * maxDistance;
            for (var i = activeAmmoPickups.Count - 1; i >= 0; i--)
            {
                var candidate = activeAmmoPickups[i];
                if (candidate == null)
                {
                    activeAmmoPickups.RemoveAt(i);
                    continue;
                }

                var candidatePosition = candidate.transform.position;
                var distance = (candidatePosition - fromPosition).sqrMagnitude;
                if (distance >= bestDistance || IsHealingResourceContested(fromPosition, candidatePosition, seeker, resourceDangerRadius, routeDangerRadius))
                {
                    continue;
                }

                bestDistance = distance;
                pickup = candidate;
            }

            return pickup != null;
        }

        public bool TryFindNearestSafeHealingStation(
            Vector3 fromPosition,
            CombatantHealth seeker,
            float resourceDangerRadius,
            float routeDangerRadius,
            out HealingStation station)
        {
            station = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var candidate in healingStations)
            {
                if (candidate == null)
                {
                    continue;
                }

                var candidatePosition = candidate.transform.position;
                var distance = (candidatePosition - fromPosition).sqrMagnitude;
                if (distance >= bestDistance || IsHealingResourceContested(fromPosition, candidatePosition, seeker, resourceDangerRadius, routeDangerRadius))
                {
                    continue;
                }

                bestDistance = distance;
                station = candidate;
            }

            return station != null;
        }

        private bool IsHealingResourceContested(
            Vector3 fromPosition,
            Vector3 resourcePosition,
            CombatantHealth seeker,
            float resourceDangerRadius,
            float routeDangerRadius)
        {
            return CountNearbyDroidTargets(resourcePosition, resourceDangerRadius, seeker) > 0 ||
                CountDroidTargetsNearRoute(fromPosition, resourcePosition, routeDangerRadius, seeker) > 0;
        }

        public void ReportArmyThreat(Vector3 position, Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = Vector3.forward;
            }

            armyThreatReports.Add(new ArmyThreatReport
            {
                Position = position,
                Direction = direction.normalized,
                ReportedAt = Time.time
            });

            while (armyThreatReports.Count > 12)
            {
                armyThreatReports.RemoveAt(0);
            }
        }

        public bool TryGetRecentArmyThreat(out Vector3 position, out Vector3 direction)
        {
            position = Vector3.zero;
            direction = Vector3.forward;
            for (var i = armyThreatReports.Count - 1; i >= 0; i--)
            {
                var report = armyThreatReports[i];
                if (Time.time - report.ReportedAt > 8f)
                {
                    armyThreatReports.RemoveAt(i);
                    continue;
                }

                position = report.Position;
                direction = report.Direction;
                return true;
            }

            return false;
        }

        public int CountNearbyCompanions(Vector3 position, float radius)
        {
            var count = 0;
            var radiusSqr = radius * radius;
            for (var i = activeCompanions.Count - 1; i >= 0; i--)
            {
                var companion = activeCompanions[i];
                if (companion == null)
                {
                    activeCompanions.RemoveAt(i);
                    continue;
                }

                if (!companion.TryGetComponent<CombatantHealth>(out var health) || !health.IsAlive)
                {
                    continue;
                }

                var delta = companion.transform.position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr)
                {
                    count++;
                }
            }

            return Mathf.Max(0, count - 1);
        }

        public int CountNearbyEnemies(Vector3 position, float radius)
        {
            var count = 0;
            var radiusSqr = radius * radius;
            foreach (var droid in activeDroids)
            {
                if (droid == null || !droid.IsAlive)
                {
                    continue;
                }

                var delta = droid.transform.position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountEnemiesNearRoute(Vector3 from, Vector3 to, float radius)
        {
            var count = 0;
            var route = to - from;
            route.y = 0f;
            var lengthSqr = route.sqrMagnitude;
            if (lengthSqr < 0.01f)
            {
                return CountNearbyEnemies(from, radius);
            }

            foreach (var droid in activeDroids)
            {
                if (droid == null || !droid.IsAlive)
                {
                    continue;
                }

                var point = droid.transform.position;
                point.y = from.y;
                var t = Mathf.Clamp01(Vector3.Dot(point - from, route) / lengthSqr);
                var closest = from + route * t;
                if ((point - closest).sqrMagnitude <= radius * radius)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountNearbyDroidAllies(Vector3 position, float radius, CombatantHealth self)
        {
            var count = 0;
            var radiusSqr = radius * radius;
            foreach (var droid in activeDroids)
            {
                if (droid == null || !droid.IsAlive || droid == self)
                {
                    continue;
                }

                if (currentGameMode == ArenaGameMode.AllOutWar && !IsSameTeam(self, droid))
                {
                    continue;
                }

                var delta = droid.transform.position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountNearbyAlliesFor(CombatantHealth soldier, Vector3 position, float radius)
        {
            if (soldier == null)
            {
                return 0;
            }

            var count = 0;
            var radiusSqr = radius * radius;
            for (var i = activeDroids.Count - 1; i >= 0; i--)
            {
                var droid = activeDroids[i];
                if (droid == null)
                {
                    activeDroids.RemoveAt(i);
                    continue;
                }

                if (!droid.IsAlive || droid == soldier || !IsSameTeam(soldier, droid))
                {
                    continue;
                }

                var delta = droid.transform.position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr)
                {
                    count++;
                }
            }

            return count;
        }

        public Vector3 GetAllOutWarFrontObjective(CombatantHealth soldier, int teamId, int squadId, int slotIndex, Vector3 currentPosition, bool forceNewObjective)
        {
            if (currentGameMode != ArenaGameMode.AllOutWar ||
                currentLayout == null ||
                soldier == null ||
                !allOutWarFronts.TryGetValue(teamId, out var front))
            {
                return GetAllOutWarObjective(soldier, slotIndex, teamId * 1000 + squadId * AllOutWarSquadSize + slotIndex, currentPosition);
            }

            var squad = GetAllOutWarSquadState(front, squadId);
            RefreshAllOutWarSquadHealth(front, squad);
            var currentRoom = currentLayout.GetNearestRoom(currentPosition);
            var arrived = squad.HasObjective && currentRoom == squad.ObjectiveRoom;
            var expired = squad.HasObjective && Time.time - squad.AssignedAt > 13.5f;
            MarkAllOutWarRoomSearched(teamId, currentRoom);
            if (forceNewObjective || !squad.HasObjective || arrived || expired)
            {
                if (arrived && squad.HasObjective)
                {
                    MarkAllOutWarRoomSearched(teamId, squad.ObjectiveRoom);
                    squad.LastSearchedRoom = squad.ObjectiveRoom;
                }

                AssignNextAllOutWarSquadObjective(front, squad, soldier, currentPosition);
            }
            UpdateAllOutWarSquadMovementDecision(squad);

            if (!currentLayout.TryGetCenter(squad.ObjectiveRoom, out var center))
            {
                center = front.Region != null ? front.Region.EntryTarget : currentPosition;
            }

            return center + GetAllOutWarSlotOffset(slotIndex, squad.ObjectiveRoom, front.Region != null ? front.Region.Room : currentRoom) + Vector3.up * 1.1f;
        }

        public bool TryGetAllOutWarSquadPublicState(
            int teamId,
            int squadId,
            out float healthRatio,
            out AllOutWarSquadSignal signal,
            out AllOutWarEngagementScale engagementScale,
            out AllOutWarSquadDecision decision)
        {
            healthRatio = 0f;
            signal = AllOutWarSquadSignal.None;
            engagementScale = AllOutWarEngagementScale.None;
            decision = AllOutWarSquadDecision.Search;

            if (!allOutWarFronts.TryGetValue(teamId, out var front) ||
                !front.Squads.TryGetValue(squadId, out var squad))
            {
                return false;
            }

            RefreshAllOutWarSquadHealth(front, squad);
            healthRatio = squad.HealthRatio;
            signal = squad.Signal;
            engagementScale = squad.EngagementScale;
            decision = squad.Decision;
            return true;
        }

        public void ReportAllOutWarSquadSignal(CombatantHealth reporter, AllOutWarSquadSignal signal, Vector3 position, CombatantHealth enemy = null)
        {
            if (currentGameMode != ArenaGameMode.AllOutWar ||
                reporter == null ||
                !allOutWarSquadMembers.TryGetValue(reporter, out var member) ||
                !allOutWarFronts.TryGetValue(member.TeamId, out var front))
            {
                return;
            }

            var squad = GetAllOutWarSquadState(front, member.SquadId);
            RefreshAllOutWarSquadHealth(front, squad);
            squad.Signal = signal;
            squad.SignalPosition = position;
            squad.SignalEnemyTeamId = CombatantTeam.TryGetTeam(enemy, out var enemyTeamId) ? enemyTeamId : -1;
            squad.EngagementScale = DeriveAllOutWarEngagementScale(front, squad, reporter, signal, position);
            squad.Decision = DeriveAllOutWarSquadDecision(squad, signal);
        }

        public void ReportAllOutWarSquadDecision(CombatantHealth reporter, AllOutWarSquadDecision decision, Vector3 position)
        {
            if (currentGameMode != ArenaGameMode.AllOutWar ||
                reporter == null ||
                !allOutWarSquadMembers.TryGetValue(reporter, out var member) ||
                !allOutWarFronts.TryGetValue(member.TeamId, out var front))
            {
                return;
            }

            var squad = GetAllOutWarSquadState(front, member.SquadId);
            RefreshAllOutWarSquadHealth(front, squad);
            squad.Decision = decision;
            if (decision == AllOutWarSquadDecision.Search &&
                (squad.Signal == AllOutWarSquadSignal.ContactLost || squad.Signal == AllOutWarSquadSignal.ResourceFound))
            {
                squad.Signal = AllOutWarSquadSignal.None;
                squad.EngagementScale = AllOutWarEngagementScale.None;
            }

            if (position != Vector3.zero)
            {
                squad.SignalPosition = position;
            }
        }

        private void RegisterAllOutWarSquadMember(CombatantHealth health, int teamId, int squadId, int slotIndex)
        {
            if (health == null)
            {
                return;
            }

            allOutWarSquadMembers[health] = new AllOutWarSquadMemberInfo(teamId, squadId, slotIndex);
            if (allOutWarFronts.TryGetValue(teamId, out var front))
            {
                RefreshAllOutWarSquadHealth(front, GetAllOutWarSquadState(front, squadId));
            }
        }

        private void UnregisterAllOutWarSquadMember(CombatantHealth health)
        {
            if (health == null || !allOutWarSquadMembers.TryGetValue(health, out var member))
            {
                return;
            }

            allOutWarSquadMembers.Remove(health);
            if (allOutWarFronts.TryGetValue(member.TeamId, out var front) &&
                front.Squads.TryGetValue(member.SquadId, out var squad))
            {
                RefreshAllOutWarSquadHealth(front, squad);
            }
        }

        private void RefreshAllOutWarSquadHealth(AllOutWarArmyFrontState front, AllOutWarSquadFrontState squad)
        {
            if (front == null || squad == null)
            {
                return;
            }

            var slotContributions = new float[AllOutWarSquadSize];
            foreach (var pair in allOutWarSquadMembers)
            {
                var soldier = pair.Key;
                var member = pair.Value;
                if (member.TeamId != front.TeamId || member.SquadId != squad.SquadId || soldier == null || !soldier.IsAlive)
                {
                    continue;
                }

                var slot = Mathf.Clamp(member.SlotIndex, 0, AllOutWarSquadSize - 1);
                var healthContribution = soldier.MaxHealth > 0f ? soldier.CurrentHealth / soldier.MaxHealth : 0f;
                var shieldContribution = soldier.MaxShield > 0f ? (soldier.CurrentShield / soldier.MaxShield) * 0.25f : 0f;
                slotContributions[slot] = Mathf.Max(slotContributions[slot], Mathf.Clamp01(healthContribution + shieldContribution));
            }

            var total = 0f;
            for (var i = 0; i < slotContributions.Length; i++)
            {
                total += slotContributions[i];
            }

            squad.HealthRatio = Mathf.Clamp01(total / AllOutWarSquadSize);
            if (squad.HealthRatio <= 0.18f)
            {
                squad.EngagementScale = AllOutWarEngagementScale.Overrun;
                if (squad.Decision != AllOutWarSquadDecision.Heal)
                {
                    squad.Decision = AllOutWarSquadDecision.Regroup;
                }
            }
        }

        private AllOutWarEngagementScale DeriveAllOutWarEngagementScale(AllOutWarArmyFrontState front, AllOutWarSquadFrontState squad, CombatantHealth reporter, AllOutWarSquadSignal signal, Vector3 position)
        {
            if (signal == AllOutWarSquadSignal.None ||
                signal == AllOutWarSquadSignal.ContactLost ||
                signal == AllOutWarSquadSignal.ResourceFound)
            {
                return AllOutWarEngagementScale.None;
            }

            var enemyCount = reporter != null ? CountNearbyDroidTargets(position, Mathf.Max(10f, roomSize * 1.25f), reporter) : 0;
            var alliedSignals = CountAllOutWarAlliedSquadSignalsNear(front, squad.SquadId, position, Mathf.Max(12f, roomSize * 1.6f));
            if (squad.HealthRatio <= 0.18f || (signal == AllOutWarSquadSignal.AllyKilled && squad.HealthRatio <= 0.42f))
            {
                return AllOutWarEngagementScale.Overrun;
            }

            if (enemyCount >= 6 || (signal == AllOutWarSquadSignal.AllyKilled && enemyCount >= 2) || (alliedSignals >= 2 && enemyCount >= 3))
            {
                return AllOutWarEngagementScale.HeavyEngagement;
            }

            if (enemyCount >= 3 || alliedSignals >= 1 || signal == AllOutWarSquadSignal.TakingDamage)
            {
                return AllOutWarEngagementScale.Firefight;
            }

            if (signal == AllOutWarSquadSignal.ShotsExchanged ||
                signal == AllOutWarSquadSignal.EnemyKilled ||
                signal == AllOutWarSquadSignal.AllyKilled)
            {
                return AllOutWarEngagementScale.Skirmish;
            }

            return AllOutWarEngagementScale.ProbeContact;
        }

        private int CountAllOutWarAlliedSquadSignalsNear(AllOutWarArmyFrontState front, int ignoredSquadId, Vector3 position, float radius)
        {
            if (front == null)
            {
                return 0;
            }

            var radiusSqr = radius * radius;
            var count = 0;
            foreach (var squad in front.Squads.Values)
            {
                if (squad.SquadId == ignoredSquadId || squad.Signal == AllOutWarSquadSignal.None)
                {
                    continue;
                }

                var delta = squad.SignalPosition - position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr)
                {
                    count++;
                }
            }

            return count;
        }

        private static AllOutWarSquadDecision DeriveAllOutWarSquadDecision(AllOutWarSquadFrontState squad, AllOutWarSquadSignal signal)
        {
            if (squad.HealthRatio <= 0.18f)
            {
                return squad.Decision == AllOutWarSquadDecision.Heal ? AllOutWarSquadDecision.Heal : AllOutWarSquadDecision.Regroup;
            }

            return signal switch
            {
                AllOutWarSquadSignal.EnemySpotted => AllOutWarSquadDecision.Probe,
                AllOutWarSquadSignal.ShotsExchanged => AllOutWarSquadDecision.Fix,
                AllOutWarSquadSignal.TakingDamage => AllOutWarSquadDecision.Fix,
                AllOutWarSquadSignal.AllyKilled => squad.HealthRatio <= 0.45f ? AllOutWarSquadDecision.Regroup : AllOutWarSquadDecision.Fix,
                AllOutWarSquadSignal.EnemyKilled => AllOutWarSquadDecision.Collapse,
                AllOutWarSquadSignal.ContactLost => AllOutWarSquadDecision.ResumeSearch,
                AllOutWarSquadSignal.ResourceFound => squad.Decision,
                _ => AllOutWarSquadDecision.Search
            };
        }

        private static void UpdateAllOutWarSquadMovementDecision(AllOutWarSquadFrontState squad)
        {
            if (squad == null || squad.Signal != AllOutWarSquadSignal.None)
            {
                return;
            }

            squad.Decision = squad.HealthRatio <= 0.18f
                ? AllOutWarSquadDecision.Regroup
                : squad.Phase == AllOutWarSearchPhase.Collapse
                    ? AllOutWarSquadDecision.Collapse
                    : AllOutWarSquadDecision.Search;
            if (squad.Decision == AllOutWarSquadDecision.Search)
            {
                squad.EngagementScale = AllOutWarEngagementScale.None;
            }
        }

        private AllOutWarSquadFrontState GetAllOutWarSquadState(AllOutWarArmyFrontState front, int squadId)
        {
            if (front.Squads.TryGetValue(squadId, out var squad))
            {
                return squad;
            }

            var sector = GetAllOutWarSearchSector(squadId);
            var searchVector = GetAllOutWarSectorDirection(front.PushDirection, sector);
            squad = new AllOutWarSquadFrontState
            {
                SquadId = squadId,
                Sector = sector,
                Phase = sector == AllOutWarSearchSector.Reserve ? AllOutWarSearchPhase.Reinforce : AllOutWarSearchPhase.Advance,
                SearchVector = searchVector
            };
            front.Squads[squadId] = squad;
            return squad;
        }

        private void AssignNextAllOutWarSquadObjective(AllOutWarArmyFrontState front, AllOutWarSquadFrontState squad, CombatantHealth soldier, Vector3 currentPosition)
        {
            if (front == null || squad == null || currentLayout == null)
            {
                return;
            }

            var bestRoom = front.Region != null ? front.Region.Room : currentLayout.GetNearestRoom(currentPosition);
            var bestScore = float.NegativeInfinity;
            var bestDistance = 0;
            var bestEnemyPressure = 0;
            var bestAlignment = -1f;
            var home = front.Region != null ? front.Region.Center : currentLayout.CircularCenter;
            var searchVector = squad.SearchVector.sqrMagnitude > 0.01f ? squad.SearchVector.normalized : front.PushDirection.normalized;
            var tangent = Vector3.Cross(Vector3.up, searchVector).normalized;
            var spacing = Mathf.Max(1f, roomSize + corridorLength);

            foreach (var room in currentLayout.Rooms)
            {
                if (front.Region != null && room == front.Region.Room)
                {
                    continue;
                }

                if (!currentLayout.TryGetCenter(room, out var center))
                {
                    continue;
                }

                var fromHome = center - home;
                fromHome.y = 0f;
                var distanceFromHome = fromHome.magnitude;
                var directionFromHome = distanceFromHome > 0.01f ? fromHome / distanceFromHome : searchVector;
                var alignment = Vector3.Dot(directionFromHome, searchVector);
                var forwardAlignment = Mathf.Max(0f, alignment);
                var forwardProjection = Vector3.Dot(fromHome, searchVector);
                var lateralOffset = Mathf.Abs(Vector3.Dot(fromHome, tangent));
                var lateralAlignment = distanceFromHome > 0.01f ? lateralOffset / distanceFromHome : 0f;
                var searchBandWidth = spacing * (squad.Sector == AllOutWarSearchSector.Center ? 1.25f : 1.65f);
                var sidewaysPenalty = Mathf.Max(0f, lateralOffset - searchBandWidth) * 0.42f;
                var behindPenalty = forwardProjection < 0f ? Mathf.Abs(forwardProjection) * 1.5f : 0f;
                var distance = front.DistanceFromSpawn.TryGetValue(room, out var knownDistance) ? knownDistance : Mathf.RoundToInt(distanceFromHome / spacing);
                var searchedScore = GetAllOutWarSearchFreshnessScore(front.TeamId, room);
                var enemyPressure = CountNearbyDroidTargets(center, roomSize * 0.95f, soldier);
                var enemyPressureScore = enemyPressure * 18f;
                var distanceScore = distance * 2.2f + Mathf.Max(0f, forwardProjection) * 0.16f;
                var sectorScore = squad.Sector == AllOutWarSearchSector.Reserve
                    ? lateralAlignment * 10f + GetAllOutWarUndercoveredScore(front, room) * 14f
                    : forwardAlignment * 34f + alignment * 12f - sidewaysPenalty - behindPenalty;
                var claimedPenalty = CountAllOutWarSquadsClaimingRoom(front, room, squad.SquadId) * 38f;
                var claimedNearbyPenalty = CountAllOutWarSquadsClaimingNear(front, center, squad.SquadId, spacing * 1.45f) * 24f;
                var travelPenalty = Vector3.Distance(currentPosition, center) * 0.06f;
                var recentOwnPenalty = room == squad.LastSearchedRoom ? 12f : 0f;
                var score = sectorScore + distanceScore + searchedScore + enemyPressureScore - claimedPenalty - claimedNearbyPenalty - travelPenalty - recentOwnPenalty;

                if (squad.Phase == AllOutWarSearchPhase.Sweep)
                {
                    score += lateralAlignment * 12f + searchedScore * 0.5f - Mathf.Max(0f, sidewaysPenalty) * 0.35f;
                }
                else if (squad.Phase == AllOutWarSearchPhase.Collapse)
                {
                    score += enemyPressureScore * 1.5f;
                }
                else if (squad.Phase == AllOutWarSearchPhase.Reinforce)
                {
                    score += GetAllOutWarUndercoveredScore(front, room) * 10f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestRoom = room;
                    bestDistance = distance;
                    bestEnemyPressure = enemyPressure;
                    bestAlignment = alignment;
                }
            }

            if (bestEnemyPressure > 0)
            {
                squad.Phase = AllOutWarSearchPhase.Collapse;
            }
            else if (squad.Sector == AllOutWarSearchSector.Reserve)
            {
                squad.Phase = AllOutWarSearchPhase.Reinforce;
            }
            else if (bestAlignment < 0.18f || bestDistance <= squad.AdvanceIndex)
            {
                squad.Phase = AllOutWarSearchPhase.Sweep;
            }
            else
            {
                squad.Phase = AllOutWarSearchPhase.Advance;
            }

            squad.AdvanceIndex = Mathf.Max(squad.AdvanceIndex, bestDistance);
            squad.ObjectiveRoom = bestRoom;
            squad.HasObjective = true;
            squad.AssignedAt = Time.time;
        }

        private static float GetAllOutWarRoomPushScore(AllOutWarArmyFrontState front, Vector3 roomCenter)
        {
            if (front == null || front.Region == null)
            {
                return 0f;
            }

            var fromHome = roomCenter - front.Region.EntryTarget;
            fromHome.y = 0f;
            if (fromHome.sqrMagnitude <= 0.01f)
            {
                return 0f;
            }

            return Vector3.Dot(fromHome.normalized, front.PushDirection);
        }

        private static AllOutWarSearchSector GetAllOutWarSearchSector(int squadId)
        {
            return (Mathf.Abs(squadId) % 6) switch
            {
                0 => AllOutWarSearchSector.Center,
                1 => AllOutWarSearchSector.Left,
                2 => AllOutWarSearchSector.Right,
                3 => AllOutWarSearchSector.WideLeft,
                4 => AllOutWarSearchSector.WideRight,
                _ => AllOutWarSearchSector.Reserve
            };
        }

        private static Vector3 GetAllOutWarSectorDirection(Vector3 pushDirection, AllOutWarSearchSector sector)
        {
            var baseDirection = pushDirection.sqrMagnitude > 0.01f ? pushDirection.normalized : Vector3.forward;
            var angle = sector switch
            {
                AllOutWarSearchSector.Left => -32f,
                AllOutWarSearchSector.Right => 32f,
                AllOutWarSearchSector.WideLeft => -58f,
                AllOutWarSearchSector.WideRight => 58f,
                AllOutWarSearchSector.Reserve => 0f,
                _ => 0f
            };

            return Quaternion.AngleAxis(angle, Vector3.up) * baseDirection;
        }

        private void MarkAllOutWarRoomSearched(int teamId, Vector2Int room)
        {
            if (!allOutWarRoomSearchTimes.TryGetValue(teamId, out var searchedRooms))
            {
                searchedRooms = new Dictionary<Vector2Int, float>();
                allOutWarRoomSearchTimes[teamId] = searchedRooms;
            }

            searchedRooms[room] = Time.time;
        }

        private float GetAllOutWarSearchFreshnessScore(int teamId, Vector2Int room)
        {
            if (!allOutWarRoomSearchTimes.TryGetValue(teamId, out var searchedRooms) ||
                !searchedRooms.TryGetValue(room, out var searchedAt))
            {
                return 26f;
            }

            var age = Mathf.Max(0f, Time.time - searchedAt);
            return Mathf.Clamp01(age / 42f) * 26f;
        }

        private float GetAllOutWarUndercoveredScore(AllOutWarArmyFrontState front, Vector2Int room)
        {
            if (front == null || currentLayout == null || !currentLayout.TryGetCenter(room, out var center))
            {
                return 0f;
            }

            var claimingSquads = CountAllOutWarSquadsClaimingRoom(front, room, -1);
            var nearbyAllies = 0;
            foreach (var soldier in allOutWarAiSoldiers)
            {
                if (soldier == null || !soldier.IsAlive || !CombatantTeam.TryGetTeam(soldier, out var teamId) || teamId != front.TeamId)
                {
                    continue;
                }

                var delta = soldier.transform.position - center;
                delta.y = 0f;
                if (delta.sqrMagnitude <= roomSize * roomSize * 1.7f)
                {
                    nearbyAllies++;
                }
            }

            return Mathf.Max(0f, 4f - claimingSquads - nearbyAllies * 0.35f);
        }

        private int CountAllOutWarSquadsClaimingRoom(AllOutWarArmyFrontState front, Vector2Int room, int ignoredSquadId)
        {
            var count = 0;
            foreach (var squad in front.Squads.Values)
            {
                if (squad.SquadId != ignoredSquadId && squad.HasObjective && squad.ObjectiveRoom == room)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountAllOutWarSquadsClaimingNear(AllOutWarArmyFrontState front, Vector3 center, int ignoredSquadId, float radius)
        {
            if (front == null || currentLayout == null)
            {
                return 0;
            }

            var radiusSqr = radius * radius;
            var count = 0;
            foreach (var squad in front.Squads.Values)
            {
                if (squad.SquadId == ignoredSquadId || !squad.HasObjective || !currentLayout.TryGetCenter(squad.ObjectiveRoom, out var objectiveCenter))
                {
                    continue;
                }

                var delta = objectiveCenter - center;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr)
                {
                    count++;
                }
            }

            return count;
        }

        private Vector3 GetAllOutWarSlotOffset(int slotIndex, Vector2Int objectiveRoom, Vector2Int homeRoom)
        {
            var outward = new Vector3(objectiveRoom.x - homeRoom.x, 0f, objectiveRoom.y - homeRoom.y);
            if (outward.sqrMagnitude < 0.01f)
            {
                outward = Vector3.forward;
            }

            outward.Normalize();
            var side = Vector3.Cross(Vector3.up, outward);
            return (Mathf.Abs(slotIndex) % AllOutWarSquadSize) switch
            {
                0 => outward * 0.65f,
                1 => side * 1.35f,
                2 => -side * 1.35f,
                _ => -outward * 1.05f
            };
        }

        public Vector3 GetAllOutWarObjective(CombatantHealth soldier, int roleIndex, int objectiveSeed, Vector3 currentPosition)
        {
            if (currentLayout == null || currentLayout.RoomCenters.Count == 0 || soldier == null)
            {
                return currentPosition;
            }

            var candidates = new List<Vector3>();
            AddAllOutWarRoleObjectives(soldier, roleIndex, candidates);
            if (candidates.Count == 0)
            {
                foreach (var center in currentLayout.RoomCenters.Values)
                {
                    candidates.Add(center + Vector3.up * 1.1f);
                }
            }

            var best = candidates[Mathf.Abs(objectiveSeed + Mathf.FloorToInt(Time.time * 0.17f)) % candidates.Count];
            var bestScore = float.NegativeInfinity;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[(i + objectiveSeed) % candidates.Count];
                var crowdPenalty = CountNearbyAlliesFor(soldier, candidate, roomSize * 0.72f) * 34f;
                var travelPenalty = Vector3.Distance(currentPosition, candidate) * 0.18f;
                var jitter = Mathf.PerlinNoise(objectiveSeed * 0.173f, Time.time * 0.037f + i) * 8f;
                var score = jitter - crowdPenalty - travelPenalty;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private void AddAllOutWarRoleObjectives(CombatantHealth soldier, int roleIndex, List<Vector3> candidates)
        {
            var ownTeam = CombatantTeam.TryGetTeam(soldier, out var teamId) ? teamId : -1;
            var ownRegion = currentLayout.ArmySpawnRegions.Find(region => region.TeamId == ownTeam);

            if (roleIndex == 3 && ownRegion != null)
            {
                candidates.Add(ownRegion.EntryTarget);
                if (currentLayout.TryGetCenter(ownRegion.Room, out var homeCenter))
                {
                    candidates.Add(homeCenter + Vector3.up * 1.1f);
                }
            }

            if (roleIndex == 4 && currentLayout.ClearingCenters.Count > 0)
            {
                foreach (var clearing in currentLayout.ClearingCenters)
                {
                    candidates.Add(clearing + Vector3.up * 1.1f);
                }
            }

            foreach (var region in currentLayout.ArmySpawnRegions)
            {
                if (region.TeamId == ownTeam)
                {
                    continue;
                }

                if (roleIndex == 1 || roleIndex == 2)
                {
                    candidates.Add(region.EntryTarget);
                }

                if (currentLayout.TryGetCenter(region.Room, out var enemyRoomCenter))
                {
                    candidates.Add(enemyRoomCenter + Vector3.up * 1.1f);
                }
            }

            candidates.Add(currentLayout.CircularCenter + Vector3.up * 1.1f);

            if (ownRegion != null)
            {
                var outward = ownRegion.Center - currentLayout.CircularCenter;
                outward.y = 0f;
                if (outward.sqrMagnitude > 0.01f)
                {
                    var tangent = Vector3.Cross(Vector3.up, outward.normalized);
                    var flankSign = roleIndex == 2 ? 1f : -1f;
                    candidates.Add(currentLayout.CircularCenter + tangent * flankSign * currentLayout.CircularRadius * 0.38f + Vector3.up * 1.1f);
                }
            }
        }

        public int CountNearbyDroidTargets(Vector3 position, float radius, CombatantHealth seeker)
        {
            if (currentGameMode != ArenaGameMode.AllOutWar)
            {
                return CountNearbyDroidTargets(position, radius);
            }

            var count = 0;
            var radiusSqr = radius * radius;
            var targets = new List<CombatantHealth>();
            CollectEnemyTargets(seeker, targets);
            foreach (var target in targets)
            {
                CountDroidTargetNearPosition(target, position, radiusSqr, ref count);
            }

            return count;
        }

        public int CountNearbyDroidTargets(Vector3 position, float radius)
        {
            var count = 0;
            var radiusSqr = radius * radius;
            CountDroidTargetNearPosition(playerCombatant, position, radiusSqr, ref count);

            for (var i = activeCompanions.Count - 1; i >= 0; i--)
            {
                var companion = activeCompanions[i];
                if (companion == null)
                {
                    activeCompanions.RemoveAt(i);
                    continue;
                }

                if (companion.TryGetComponent<CombatantHealth>(out var companionHealth))
                {
                    CountDroidTargetNearPosition(companionHealth, position, radiusSqr, ref count);
                }
            }

            for (var i = activeTurrets.Count - 1; i >= 0; i--)
            {
                var turret = activeTurrets[i];
                if (turret == null)
                {
                    activeTurrets.RemoveAt(i);
                    continue;
                }

                if (turret.TryGetComponent<CombatantHealth>(out var turretHealth))
                {
                    CountDroidTargetNearPosition(turretHealth, position, radiusSqr, ref count);
                }
            }

            return count;
        }

        public int CountDroidTargetsNearRoute(Vector3 from, Vector3 to, float radius, CombatantHealth seeker)
        {
            if (currentGameMode != ArenaGameMode.AllOutWar)
            {
                return CountDroidTargetsNearRoute(from, to, radius);
            }

            var count = 0;
            var route = to - from;
            route.y = 0f;
            var lengthSqr = route.sqrMagnitude;
            if (lengthSqr < 0.01f)
            {
                return CountNearbyDroidTargets(from, radius, seeker);
            }

            var targets = new List<CombatantHealth>();
            CollectEnemyTargets(seeker, targets);
            foreach (var target in targets)
            {
                CountDroidTargetNearRoute(target, from, route, lengthSqr, radius, ref count);
            }

            return count;
        }

        public int CountDroidTargetsNearRoute(Vector3 from, Vector3 to, float radius)
        {
            var count = 0;
            var route = to - from;
            route.y = 0f;
            var lengthSqr = route.sqrMagnitude;
            if (lengthSqr < 0.01f)
            {
                return CountNearbyDroidTargets(from, radius);
            }

            CountDroidTargetNearRoute(playerCombatant, from, route, lengthSqr, radius, ref count);

            for (var i = activeCompanions.Count - 1; i >= 0; i--)
            {
                var companion = activeCompanions[i];
                if (companion == null)
                {
                    activeCompanions.RemoveAt(i);
                    continue;
                }

                if (companion.TryGetComponent<CombatantHealth>(out var companionHealth))
                {
                    CountDroidTargetNearRoute(companionHealth, from, route, lengthSqr, radius, ref count);
                }
            }

            for (var i = activeTurrets.Count - 1; i >= 0; i--)
            {
                var turret = activeTurrets[i];
                if (turret == null)
                {
                    activeTurrets.RemoveAt(i);
                    continue;
                }

                if (turret.TryGetComponent<CombatantHealth>(out var turretHealth))
                {
                    CountDroidTargetNearRoute(turretHealth, from, route, lengthSqr, radius, ref count);
                }
            }

            return count;
        }

        private static void AddDroidTarget(CombatantHealth candidate, List<CombatantHealth> targets)
        {
            if (candidate != null && candidate.IsAlive)
            {
                targets.Add(candidate);
            }
        }

        private static void AddEnemyTarget(CombatantHealth seeker, CombatantHealth candidate, List<CombatantHealth> targets)
        {
            if (candidate != null && candidate.IsAlive && candidate != seeker && CombatantTeam.AreEnemies(seeker, candidate))
            {
                targets.Add(candidate);
            }
        }

        private static bool IsSameTeam(CombatantHealth a, CombatantHealth b)
        {
            return CombatantTeam.TryGetTeam(a, out var aTeam) &&
                   CombatantTeam.TryGetTeam(b, out var bTeam) &&
                   aTeam == bTeam;
        }

        private static void ConsiderTarget(CombatantHealth candidate, Vector3 fromPosition, ref CombatantHealth best, ref float bestDistance)
        {
            if (candidate == null || !candidate.IsAlive)
            {
                return;
            }

            var distance = (candidate.transform.position - fromPosition).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        private static void CountDroidTargetNearPosition(CombatantHealth target, Vector3 position, float radiusSqr, ref int count)
        {
            if (target == null || !target.IsAlive)
            {
                return;
            }

            var delta = target.transform.position - position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= radiusSqr)
            {
                count++;
            }
        }

        private static void CountDroidTargetNearRoute(CombatantHealth target, Vector3 from, Vector3 route, float lengthSqr, float radius, ref int count)
        {
            if (target == null || !target.IsAlive)
            {
                return;
            }

            var point = target.transform.position;
            point.y = from.y;
            var t = Mathf.Clamp01(Vector3.Dot(point - from, route) / lengthSqr);
            var closest = from + route * t;
            if ((point - closest).sqrMagnitude <= radius * radius)
            {
                count++;
            }
        }

        public void SetPlayerAiming(bool aiming)
        {
            hud?.SetAiming(aiming);
        }

        private void Start()
        {
            StartNewMatch();
        }

        public void RestartMatch()
        {
            StartNewMatch();
        }

        public void NotifyPickupTaken(WeaponPickup pickup)
        {
            activePickups.Remove(pickup);
            if (pickup != null)
            {
                NotifyPickupPadEmptied(pickup.gameObject);
            }
        }

        public void NotifyHealthPickupTaken(HealthPickup pickup)
        {
            activeHealthPickups.Remove(pickup);
            if (pickup != null)
            {
                NotifyPickupPadEmptied(pickup.gameObject);
            }
        }

        public void NotifyAmmoPickupTaken(AmmoPickup pickup)
        {
            activeAmmoPickups.Remove(pickup);
            if (pickup != null)
            {
                NotifyPickupPadEmptied(pickup.gameObject);
            }
        }

        private void StartNewMatch()
        {
            currentGameMode = ResolveSelectedGameMode();
            if (currentGameMode == ArenaGameMode.AllOutWar)
            {
                StartAllOutWarMatch();
                return;
            }

            StartKingOfTheColosseumMatch();
        }

        private static ArenaGameMode ResolveSelectedGameMode()
        {
            return PlayerPrefs.GetString("ArenaGameMode", "KingOfTheColosseum") == "AllOutWar"
                ? ArenaGameMode.AllOutWar
                : ArenaGameMode.KingOfTheColosseum;
        }

        private void ResetSharedMatchState()
        {
            StopAllCoroutines();
            IsMatchActive = false;
            activePickups.Clear();
            activeAmmoPickups.Clear();
            activeHealthPickups.Clear();
            healingStations.Clear();
            activeDroids.Clear();
            allOutWarAiSoldiers.Clear();
            pickupPadSlots.Clear();
            activeTurrets.Clear();
            activeCompanions.Clear();
            armyThreatReports.Clear();
            armyUpgradeStates.Clear();
            allOutWarFronts.Clear();
            allOutWarSquadMembers.Clear();
            currentWave = 0;
            scrap = 0;
            damageUpgradeLevel = 0;
            companionUpgradeLevel = 0;
            companionDamageUpgradeLevel = 0;
            turretArmorUpgradeLevel = 0;
            turretKits = 0;
            turretPlacementMode = false;
            turretMobilityUpgrade = false;
            hasFabricatorRoom = false;
            allOutWarSettings = null;
            allOutWarRosterRemaining = null;
            allOutWarActiveByTeam = null;
            allOutWarNextSpawnAt = null;
            allOutWarSpawnCursor = 0;
            playerRespawning = false;
            allOutWarMatchEnded = false;
            selectedArmyUnit = null;
            IsFabricatorMenuOpen = false;
            InputSuppressedUntil = 0f;
            ClearPreviousMatch();
            DisableStarterCameras();
        }

        private void StartKingOfTheColosseumMatch()
        {
            ResetSharedMatchState();
            theme = new ArenaTheme();
            matchRoot = new GameObject("Generated Cyber Arena Match");
            matchRoot.AddComponent<ArenaAudio>();

            var generatorObject = new GameObject("Arena Generator");
            generatorObject.transform.SetParent(matchRoot.transform, false);
            var generator = generatorObject.AddComponent<ArenaGenerator>();

            var chosenSeed = randomizeSeed || seed == 0 ? Random.Range(1000, 999999) : seed;
            var layout = generator.Generate(theme, matchRoot.transform, chosenSeed, roomCount, gridRadius, roomSize, corridorLength, corridorWidth, wallHeight, weaponPickupCount, perimeterGateCount);
            currentLayout = layout;

            ShieldDomeBackdrop.Build(matchRoot.transform, layout, roomSize, wallHeight);
            AddLighting(layout);
            var player = CreatePlayer(layout.PlayerSpawn, layout.PlayerRotation);
            playerObject = player;
            BeginGateIntro(player, layout.PlayerGate, 2.9f);
            CreatePickups(layout);
            CreateHealthItems(layout);
            CreateFabricatorStation(layout);
            CreateHud(player, null);
            Rendering.DroidOutlineRendererFeature.LogRuntimeRendererCategorySummary("after StartNewMatch setup");

            playerCombatant.Died += OnPlayerDied;

            IsMatchActive = true;
            waveRoutine = StartCoroutine(RunWaveLoop(player.transform, layout));
        }

        private void StartAllOutWarMatch()
        {
            ResetSharedMatchState();
            allOutWarSettings = AllOutWarSettings.FromPlayerPrefs();

            theme = new ArenaTheme();
            matchRoot = new GameObject("Generated All Out War Match");
            matchRoot.AddComponent<ArenaAudio>();

            var generatorObject = new GameObject("All Out War Arena Generator");
            generatorObject.transform.SetParent(matchRoot.transform, false);
            var generator = generatorObject.AddComponent<ArenaGenerator>();

            var chosenSeed = randomizeSeed || seed == 0 ? Random.Range(1000, 999999) : seed;
            var totalArmies = allOutWarSettings.TotalArmies;
            var activeBattleSize = Mathf.Max(allOutWarSettings.BattlefieldCap, totalArmies * AllOutWarSquadSize);
            var warGridRadius = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(activeBattleSize) * 0.38f) + 3, 4, 9);
            var warRoomCount = Mathf.Clamp(Mathf.CeilToInt(Mathf.PI * warGridRadius * warGridRadius), 24, 260);
            var layout = generator.GenerateAllOutWar(theme, matchRoot.transform, chosenSeed, totalArmies, warRoomCount, warGridRadius, roomSize, corridorLength, corridorWidth, wallHeight, weaponPickupCount + healthPickupCount);
            currentLayout = layout;

            ShieldDomeBackdrop.Build(matchRoot.transform, layout, roomSize, wallHeight);
            AddLighting(layout);
            BuildAllOutWarNavMesh(matchRoot.transform, layout);

            var playerRegion = layout.ArmySpawnRegions.Count > 0 ? layout.ArmySpawnRegions[0] : null;
            var playerSpawn = playerRegion != null ? playerRegion.GetSpawnPosition(0) : layout.PlayerSpawn;
            var playerRotation = playerRegion != null ? playerRegion.Rotation : layout.PlayerRotation;
            var player = CreatePlayer(playerSpawn, playerRotation);
            playerObject = player;
            AssignTeam(player, 0);
            EquipAllOutWarPlayerWeapon();

            CreateAllOutWarPickups(layout);
            CreateAllOutWarHealthItems(layout);
            CreateHud(player, null);
            hud?.SetWaveCountdown("");
            hud?.SetCenterMessage("");
            Rendering.DroidOutlineRendererFeature.LogRuntimeRendererCategorySummary("after All Out War setup");

            playerCombatant.Died += OnPlayerDied;
            InitializeAllOutWarSpawning();

            IsMatchActive = true;
            SpawnInitialAllOutWarSoldiers(layout, player.transform);
            StartCoroutine(RunAllOutWarPickupRefillLoop());
            waveRoutine = StartCoroutine(RunAllOutWarLoop(player.transform, layout));
        }

        private void BeginGateIntro(GameObject actor, ArenaGateSpawn gate, float walkSpeed)
        {
            if (actor == null)
            {
                return;
            }

            var walker = actor.AddComponent<SpawnIntroWalker>();
            walker.Hold();
            if (gate == null)
            {
                Debug.LogWarning($"[Arena Shooter] Missing gate for {actor.name}; releasing intro hold.");
                walker.Begin(actor.transform.position + actor.transform.forward * 2f, actor.transform.position + actor.transform.forward * 2f, walkSpeed);
                return;
            }

            StartCoroutine(PlayGateIntro(walker, gate, walkSpeed));
        }

        private IEnumerator PlayGateIntro(SpawnIntroWalker walker, ArenaGateSpawn gate, float walkSpeed)
        {
            ArenaAudio.Instance?.BeginGateCrowdSwell(1.4f);
            yield return new WaitForSeconds(1.4f);
            ArenaAudio.Instance?.PlayGateOpenCrowd();
            if (gate.Door == null)
            {
                Debug.LogWarning($"[Arena Shooter] Gate door missing for {gate.Room}/{gate.Direction}; intro will continue without door animation.");
            }
            else
            {
                gate.Door.OpenThenClose(4f);
            }

            yield return new WaitForSeconds(0.95f);
            walker.Begin(gate.EntryTarget, gate.InnerTarget + Vector3.up * 1.1f, walkSpeed);
        }

        private void BuildAllOutWarNavMesh(Transform root, ArenaLayout layout)
        {
            if (root == null || layout == null)
            {
                return;
            }

            var surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.defaultArea = 0;
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.22f;
            surface.overrideTileSize = true;
            surface.tileSize = 128;
            surface.minRegionArea = Mathf.Max(2f, roomSize * roomSize * 0.08f);
            surface.BuildNavMesh();

            var triangulation = NavMesh.CalculateTriangulation();
            Debug.Log($"[Arena Shooter] All Out War runtime NavMesh built with {triangulation.vertices.Length} vertices.");
        }

        private void ClearPreviousMatch()
        {
            if (matchRoot != null)
            {
                Destroy(matchRoot);
            }

            if (hud != null)
            {
                Destroy(hud.gameObject);
                hud = null;
            }
        }

        private void DisableStarterCameras()
        {
            var cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var camera in cameras)
            {
                camera.enabled = false;
            }

            var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            foreach (var listener in listeners)
            {
                listener.enabled = false;
            }
        }

        private GameObject CreatePlayer(Vector3 position, Quaternion rotation)
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player Combatant";
            player.transform.SetParent(matchRoot.transform, false);
            player.transform.position = position;
            player.transform.rotation = rotation;

            if (player.TryGetComponent<Collider>(out var primitiveCollider))
            {
                Destroy(primitiveCollider);
            }

            if (player.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = theme.Player;
            }

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.9f;
            controller.radius = 0.36f;
            controller.center = new Vector3(0f, 0f, 0f);

            playerCombatant = player.AddComponent<CombatantHealth>();
            playerCombatant.Configure("Player", playerHealth);
            playerCombatant.ConfigureShieldRecharge(5.2f, playerHealth / 3.4f);

            var cameraObject = new GameObject("Player Camera");
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0.64f, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.03f;
            camera.fieldOfView = 62f;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.targetTexture = null;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.004f, 0.028f, 0.04f);
            cameraObject.AddComponent<ArenaCameraFraming>();
            cameraObject.AddComponent<AudioListener>();

            var viewModel = cameraObject.AddComponent<FirstPersonViewModel>();
            viewModel.Build(cameraObject.transform, theme);

            var weapons = player.AddComponent<WeaponInventory>();
            weapons.Configure(playerCombatant, viewModel.Muzzle, theme.Beam);
            weapons.ConfigureViewModel(viewModel);
            playerWeapons = weapons;

            var fps = player.AddComponent<PlayerFpsController>();
            fps.Configure(this, camera);
            player.AddComponent<FootstepAudio>().Configure(true);

            return player;
        }

        private IEnumerator RunWaveLoop(Transform player, ArenaLayout layout)
        {
            while (IsMatchActive && currentWave < finalWave)
            {
                var nextWave = currentWave + 1;
                yield return StartCoroutine(ShowWaveCountdown(nextWave, WaveCountdownSeconds));

                currentWave++;
                hud?.SetWaveCountdown($"Wave {currentWave} Incoming");
                yield return new WaitForSeconds(1.4f);
                hud?.SetWaveCountdown("");

                AlertCompanionsForIncomingWave(layout);
                yield return StartCoroutine(SpawnWave(layout, player, currentWave));
                while (IsMatchActive && activeDroids.Count > 0)
                {
                    yield return null;
                }

                if (!IsMatchActive)
                {
                    yield break;
                }

                if (currentWave >= finalWave)
                {
                    IsMatchActive = false;
                    hud?.SetCenterMessage($"ESCAPE ROUTE OPEN\nPrototype victory achieved\n{RestartPrompt()}");
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    yield break;
                }

                SendCompanionToNearestHealingStation();
                RefillEmptyPickupPads();
            }
        }

        private IEnumerator ShowWaveCountdown(int waveNumber, float seconds)
        {
            var remaining = Mathf.Max(0f, seconds);
            while (IsMatchActive && remaining > 0f)
            {
                hud?.SetWaveCountdown($"Next Wave in {Mathf.CeilToInt(remaining)}");
                yield return new WaitForSeconds(1f);
                remaining -= 1f;
            }

            hud?.SetWaveCountdown("");
        }

        private void SendCompanionToNearestHealingStation()
        {
            if (healingStations.Count == 0)
            {
                return;
            }

            for (var i = activeCompanions.Count - 1; i >= 0; i--)
            {
                var companionObject = activeCompanions[i];
                if (companionObject == null)
                {
                    activeCompanions.RemoveAt(i);
                    continue;
                }

                if (!companionObject.TryGetComponent<CombatantHealth>(out var health) ||
                    !health.IsAlive ||
                    health.CurrentHealth >= health.MaxHealth - 0.5f ||
                    !companionObject.TryGetComponent<CompanionDroidController>(out var companion))
                {
                    continue;
                }

                HealingStation nearest = null;
                var bestDistance = float.PositiveInfinity;
                foreach (var station in healingStations)
                {
                    if (station == null)
                    {
                        continue;
                    }

                    var distance = (station.transform.position - companionObject.transform.position).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        nearest = station;
                    }
                }

                if (nearest != null)
                {
                    companion.SendToHealingStation(nearest);
                }
            }
        }

        private void AlertCompanionsForIncomingWave(ArenaLayout layout)
        {
            if (layout == null || activeCompanions.Count == 0)
            {
                return;
            }

            var threatPoints = new List<Vector3>();
            foreach (var gate in GetWaveGates(layout))
            {
                if (gate != null)
                {
                    threatPoints.Add(gate.EntryTarget);
                }
            }

            if (threatPoints.Count == 0)
            {
                return;
            }

            var orderedCompanions = new List<CompanionDroidController>();
            for (var i = activeCompanions.Count - 1; i >= 0; i--)
            {
                var companionObject = activeCompanions[i];
                if (companionObject == null)
                {
                    activeCompanions.RemoveAt(i);
                    continue;
                }

                if (companionObject.TryGetComponent<CombatantHealth>(out var health) &&
                    health.IsAlive &&
                    companionObject.TryGetComponent<CompanionDroidController>(out var companion))
                {
                    orderedCompanions.Add(companion);
                }
            }

            for (var i = 0; i < orderedCompanions.Count; i++)
            {
                var threat = threatPoints[i % threatPoints.Count];
                if (TryPickWaveDefensePosition(layout, threat, i / Mathf.Max(1, threatPoints.Count), out var position))
                {
                    orderedCompanions[i].AssignWaveDefense(position, threat);
                }
            }
        }

        private bool TryPickWaveDefensePosition(ArenaLayout layout, Vector3 threat, int slot, out Vector3 position)
        {
            position = Vector3.zero;
            if (playerObject == null)
            {
                return false;
            }

            var playerRoom = layout.GetNearestRoom(playerObject.transform.position);
            var threatRoom = layout.GetNearestRoom(threat);
            var defendRoom = playerRoom;
            if (layout.TryGetCenter(playerRoom, out var playerCenter) &&
                layout.TryGetCenter(threatRoom, out var threatRoomCenter) &&
                Vector3.Distance(playerCenter, threatRoomCenter) < roomSize * 1.8f)
            {
                defendRoom = threatRoom;
            }

            if (!layout.TryGetCenter(defendRoom, out var roomCenter))
            {
                return false;
            }

            var approachPoint = threat;
            var pathToThreat = layout.FindPath(defendRoom, threatRoom);
            if (pathToThreat.Count > 0 && layout.TryGetDoorwayPoint(defendRoom, pathToThreat[0], out var doorway))
            {
                approachPoint = doorway;
            }

            var toApproach = approachPoint - roomCenter;
            toApproach.y = 0f;
            if (toApproach.sqrMagnitude < 0.01f)
            {
                toApproach = playerObject.transform.forward;
            }

            toApproach.Normalize();
            var right = Vector3.Cross(Vector3.up, toApproach).normalized;
            var side = slot % 2 == 0 ? -1f : 1f;
            var row = slot / 2;
            var insideRoom = Vector3.Lerp(approachPoint, roomCenter, 0.42f);
            var candidate = insideRoom + right * side * (1.35f + row * 0.55f) - toApproach * (0.35f + row * 0.22f);
            if (TryGroundTacticalPoint(candidate, out position))
            {
                return true;
            }

            var fallback = roomCenter + toApproach * 2.2f + right * side * 1.15f;
            if (TryGroundTacticalPoint(fallback, out position))
            {
                return true;
            }

            position = roomCenter + Vector3.up * 1.1f;
            return true;
        }

        private static bool TryGroundTacticalPoint(Vector3 candidate, out Vector3 grounded)
        {
            grounded = candidate;
            if (!Physics.Raycast(candidate + Vector3.up * 3f, Vector3.down, out var hit, 7f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (hit.normal.y < 0.65f)
            {
                return false;
            }

            grounded = hit.point + Vector3.up * 1.1f;
            return true;
        }

        private IEnumerator SpawnWave(ArenaLayout layout, Transform player, int wave)
        {
            var gates = GetWaveGates(layout);
            if (gates.Count == 0)
            {
                yield break;
            }

            var totalDroids = firstWaveDroids + (wave - 1) * 2;
            ShuffleGates(gates);
            var minimumSquads = wave == 1 ? 3 : 2;
            var squads = Mathf.Clamp(minimumSquads + wave / 2, 1, Mathf.Min(4, gates.Count, totalDroids));
            var spawned = 0;
            for (var squad = 0; squad < squads && spawned < totalDroids; squad++)
            {
                var gate = gates[squad % gates.Count];
                var squadCount = Mathf.Min(totalDroids - spawned, Mathf.CeilToInt(totalDroids / (float)squads));
                StartCoroutine(SpawnDroidSquad(gate, player, wave, squadCount));
                spawned += squadCount;
                yield return new WaitForSeconds(0.45f);
            }
        }

        private void ShuffleGates(List<ArenaGateSpawn> gates)
        {
            for (var i = 0; i < gates.Count; i++)
            {
                var swapIndex = Random.Range(i, gates.Count);
                (gates[i], gates[swapIndex]) = (gates[swapIndex], gates[i]);
            }
        }

        private IEnumerator SpawnDroidSquad(ArenaGateSpawn gate, Transform player, int wave, int count)
        {
            gate.Door?.OpenThenClose(5.5f);
            yield return new WaitForSeconds(0.55f);

            var right = gate.SpawnRotation * Vector3.right;
            for (var i = 0; i < count; i++)
            {
                var lane = (i - (count - 1) * 0.5f) * 0.48f;
                var sideOffset = right * lane;
                var droid = CreateDroid(gate.SpawnPosition + sideOffset, gate.SpawnRotation, currentLayout, player, wave, i);
                var walker = droid.AddComponent<SpawnIntroWalker>();
                walker.Hold();
                var gateDirection = (gate.EntryTarget - gate.SpawnPosition);
                gateDirection.y = 0f;
                gateDirection = gateDirection.sqrMagnitude > 0.01f ? gateDirection.normalized : gate.SpawnRotation * Vector3.forward;
                var clearGateTarget = gate.SpawnPosition + gateDirection * 4.6f + sideOffset * 0.45f;
                clearGateTarget.y = gate.SpawnPosition.y;
                walker.Begin(clearGateTarget, gate.EntryTarget + sideOffset * 0.45f, 2.9f + wave * 0.1f, 1.55f);
                yield return new WaitForSeconds(0.28f);
            }
        }

        private List<ArenaGateSpawn> GetWaveGates(ArenaLayout layout)
        {
            var gates = new List<ArenaGateSpawn>();
            foreach (var gate in layout.GateSpawns)
            {
                if (gate != null && gate != layout.PlayerGate)
                {
                    gates.Add(gate);
                }
            }

            if (gates.Count == 0 && layout.PlayerGate != null)
            {
                gates.Add(layout.PlayerGate);
            }

            return gates;
        }

        private void InitializeAllOutWarSpawning()
        {
            var totalArmies = allOutWarSettings != null ? allOutWarSettings.TotalArmies : 2;
            allOutWarRosterRemaining = new int[totalArmies];
            allOutWarActiveByTeam = new int[totalArmies];
            allOutWarNextSpawnAt = new float[totalArmies];
            allOutWarMatchEnded = false;
            allOutWarRoomSearchTimes.Clear();
            allOutWarSquadMembers.Clear();
            BuildAllOutWarFronts();
            allOutWarSpawnCursor = 0;
            for (var team = 0; team < totalArmies; team++)
            {
                allOutWarRosterRemaining[team] = GetAllOutWarAiRosterSize(team);
                allOutWarNextSpawnAt[team] = Time.time;
            }
        }

        private void BuildAllOutWarFronts()
        {
            allOutWarFronts.Clear();
            if (currentLayout == null)
            {
                return;
            }

            foreach (var region in currentLayout.ArmySpawnRegions)
            {
                if (region == null)
                {
                    continue;
                }

                var front = new AllOutWarArmyFrontState
                {
                    TeamId = region.TeamId,
                    Region = region,
                    PushDirection = GetAllOutWarPushDirection(region)
                };

                foreach (var room in currentLayout.Rooms)
                {
                    if (room == region.Room)
                    {
                        front.DistanceFromSpawn[room] = 0;
                        continue;
                    }

                    var pathFromSpawn = currentLayout.FindPath(region.Room, room);
                    if (pathFromSpawn.Count == 0)
                    {
                        continue;
                    }

                    front.DistanceFromSpawn[room] = pathFromSpawn.Count;
                }

                allOutWarFronts[region.TeamId] = front;
            }
        }

        private Vector3 GetAllOutWarPushDirection(ArmySpawnRegion region)
        {
            if (currentLayout == null || region == null)
            {
                return Vector3.forward;
            }

            var push = currentLayout.CircularCenter - region.Center;
            push.y = 0f;
            if (push.sqrMagnitude <= 0.01f)
            {
                push = region.EntryTarget - region.Center;
                push.y = 0f;
            }

            return push.sqrMagnitude > 0.01f ? push.normalized : Vector3.forward;
        }

        private IEnumerator RunAllOutWarLoop(Transform player, ArenaLayout layout)
        {
            hud?.SetWaveCountdown("ALL OUT WAR");
            yield return new WaitForSeconds(1.25f);
            hud?.SetWaveCountdown("");

            while (IsMatchActive && currentGameMode == ArenaGameMode.AllOutWar)
            {
                CleanupAllOutWarSoldierList();
                EvaluateAllOutWarVictory();
                if (allOutWarMatchEnded)
                {
                    yield break;
                }

                var cap = Mathf.Max(1, allOutWarSettings != null ? allOutWarSettings.BattlefieldCap : 24);
                var spawnedThisTick = false;
                var guard = allOutWarRosterRemaining != null ? allOutWarRosterRemaining.Length : 0;
                while (CountLivingAllOutWarAi() < cap && guard-- > 0)
                {
                    if (TrySpawnNextAllOutWarSoldier(layout, player, false, true))
                    {
                        spawnedThisTick = true;
                        break;
                    }
                }

                EvaluateAllOutWarVictory();
                yield return new WaitForSeconds(spawnedThisTick ? 0.16f : 0.45f);
            }
        }

        private IEnumerator RunAllOutWarPickupRefillLoop()
        {
            yield return new WaitForSeconds(AllOutWarPickupRespawnSeconds);
            while (IsMatchActive && currentGameMode == ArenaGameMode.AllOutWar)
            {
                RefillEmptyPickupPads(true);
                yield return new WaitForSeconds(AllOutWarPickupRespawnSeconds);
            }
        }

        private void SpawnInitialAllOutWarSoldiers(ArenaLayout layout, Transform player)
        {
            var cap = Mathf.Max(1, allOutWarSettings != null ? allOutWarSettings.BattlefieldCap : 24);
            var guard = Mathf.Max(cap * Mathf.Max(1, allOutWarSettings != null ? allOutWarSettings.TotalArmies : 2), cap);
            while (CountLivingAllOutWarAi() < cap && guard-- > 0)
            {
                if (!TrySpawnNextAllOutWarSoldier(layout, player, true, false))
                {
                    break;
                }
            }
        }

        private bool TrySpawnNextAllOutWarSoldier(ArenaLayout layout, Transform player, bool ignoreSpawnDelay, bool playSpawnIntro)
        {
            if (layout == null || layout.ArmySpawnRegions.Count == 0 || allOutWarRosterRemaining == null || allOutWarActiveByTeam == null)
            {
                return false;
            }

            var totalArmies = allOutWarRosterRemaining.Length;
            for (var attempt = 0; attempt < totalArmies; attempt++)
            {
                var team = allOutWarSpawnCursor % totalArmies;
                allOutWarSpawnCursor = (allOutWarSpawnCursor + 1) % totalArmies;
                if (allOutWarRosterRemaining[team] <= 0 || (!ignoreSpawnDelay && Time.time < allOutWarNextSpawnAt[team]))
                {
                    continue;
                }

                var region = layout.ArmySpawnRegions.Find(candidate => candidate.TeamId == team);
                if (region == null)
                {
                    continue;
                }

                var spawnIndex = allOutWarActiveByTeam[team] + (GetAllOutWarAiRosterSize(team) - allOutWarRosterRemaining[team]);
                var droid = CreateDroid(region.GetSpawnPosition(spawnIndex), region.Rotation, layout, player, 1, spawnIndex, team);
                if (playSpawnIntro)
                {
                    var walker = droid.AddComponent<SpawnIntroWalker>();
                    walker.Hold();
                    walker.Begin(region.EntryTarget + Vector3.up * 0.05f, region.EntryTarget + Vector3.up * 0.05f, 3.1f, 0.75f);
                }

                allOutWarRosterRemaining[team]--;
                allOutWarActiveByTeam[team]++;
                allOutWarNextSpawnAt[team] = Time.time + Random.Range(0.55f, 1.35f);
                return true;
            }

            return false;
        }

        private int CountLivingAllOutWarAi()
        {
            var count = 0;
            for (var i = allOutWarAiSoldiers.Count - 1; i >= 0; i--)
            {
                var soldier = allOutWarAiSoldiers[i];
                if (soldier == null)
                {
                    allOutWarAiSoldiers.RemoveAt(i);
                    continue;
                }

                if (soldier.IsAlive)
                {
                    count++;
                }
            }

            return count;
        }

        private void CleanupAllOutWarSoldierList()
        {
            for (var i = allOutWarAiSoldiers.Count - 1; i >= 0; i--)
            {
                if (allOutWarAiSoldiers[i] == null)
                {
                    allOutWarAiSoldiers.RemoveAt(i);
                }
            }
        }

        private bool IsAllOutWarPlayerInMatch()
        {
            if (currentGameMode != ArenaGameMode.AllOutWar)
            {
                return false;
            }

            if (playerRespawning)
            {
                return true;
            }

            return playerCombatant != null && playerCombatant.IsAlive;
        }

        private void EvaluateAllOutWarVictory()
        {
            if (currentGameMode != ArenaGameMode.AllOutWar ||
                allOutWarMatchEnded ||
                allOutWarRosterRemaining == null ||
                !IsMatchActive)
            {
                return;
            }

            var aliveArmies = 0;
            var winningTeam = -1;
            var armyCount = AllOutWarArmyCount;
            for (var team = 0; team < armyCount; team++)
            {
                if (GetAllOutWarArmyRemaining(team) <= 0)
                {
                    continue;
                }

                aliveArmies++;
                winningTeam = team;
                if (aliveArmies > 1)
                {
                    return;
                }
            }

            allOutWarMatchEnded = true;
            IsMatchActive = false;
            hud?.SetWaveCountdown("");
            if (winningTeam == 0)
            {
                hud?.SetCenterMessage($"VICTORY\nARMY 0 STANDS ALONE\n{RestartPrompt()}");
            }
            else if (winningTeam > 0)
            {
                hud?.SetCenterMessage($"DEFEATED\nARMY {winningTeam} WINS\n{RestartPrompt()}");
            }
            else
            {
                hud?.SetCenterMessage($"WAR ENDED\nNO ARMY STANDS\n{RestartPrompt()}");
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private GameObject CreateDroid(Vector3 position, Quaternion rotation, ArenaLayout layout, Transform player, int wave, int squadMemberIndex, int teamId = -1)
        {
            var droid = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            droid.name = teamId >= 0 ? $"Army {teamId} Battle Droid" : $"Wave {wave} Battle Droid";
            droid.transform.SetParent(matchRoot.transform, false);
            droid.transform.position = position;
            droid.transform.rotation = rotation;

            if (droid.TryGetComponent<Collider>(out var primitiveCollider))
            {
                Destroy(primitiveCollider);
            }

            if (droid.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.enabled = false;
            }

            DroidVisuals.Build(droid.transform, theme);
            DroidHitboxRig.Build(droid.transform);

            var controller = droid.AddComponent<CharacterController>();
            controller.height = 1.9f;
            controller.radius = 0.32f;

            var health = droid.AddComponent<CombatantHealth>();
            health.Configure(teamId >= 0 ? $"Army {teamId} Soldier" : $"Wave {wave} Droid", teamId >= 0 ? 68f : 52f + wave * 9f);
            health.Died += OnDroidDied;
            activeDroids.Add(health);
            if (teamId >= 0)
            {
                AssignTeam(droid, teamId);
                allOutWarAiSoldiers.Add(health);
            }

            var blasterRig = droid.AddComponent<DroidBlasterRig>();
            var muzzle = blasterRig.Build(theme);

            var weapons = droid.AddComponent<WeaponInventory>();
            weapons.Configure(health, muzzle.transform, theme.Beam);
            weapons.Equip(new WeaponDefinition
            {
                DisplayName = teamId >= 0 ? "Army Blaster" : "Droid Blaster",
                Damage = teamId >= 0 ? 11f : 8f + wave * 1.4f,
                Range = 70f,
                Cooldown = 0.62f,
                Ammo = teamId >= 0 ? 36 : 999
            });

            droid.AddComponent<CombatantVisualWalkAnimator>();
            droid.AddComponent<DroidCrouchPose>();
            var ai = droid.AddComponent<DroidController>();
            if (teamId >= 0)
            {
                var region = layout != null ? layout.ArmySpawnRegions.Find(candidate => candidate.TeamId == teamId) : null;
                var squadId = Mathf.Max(0, squadMemberIndex / AllOutWarSquadSize);
                var slotIndex = Mathf.Abs(squadMemberIndex) % AllOutWarSquadSize;
                RegisterAllOutWarSquadMember(health, teamId, squadId, slotIndex);
                ai.ConfigureAllOutWar(this, layout, teamId, region != null ? region.Center : position, teamId * 1000 + squadMemberIndex, squadId, slotIndex);
            }
            else
            {
                ai.Configure(this, layout, player, wave, squadMemberIndex);
            }
            droid.AddComponent<FootstepAudio>().Configure(false);

            var playerCamera = player.GetComponentInChildren<Camera>();
            if (playerCamera != null)
            {
                var barObject = new GameObject("Droid Health Bar");
                barObject.transform.SetParent(droid.transform, false);
                barObject.transform.localPosition = new Vector3(0f, 1.35f, 0f);
                barObject.AddComponent<WorldHealthBar>().Build(health, playerCamera, teamId == 0);
            }

            return droid;
        }

        private void AssignTeam(GameObject actor, int teamId)
        {
            if (actor == null)
            {
                return;
            }

            var team = actor.GetComponent<CombatantTeam>();
            if (team == null)
            {
                team = actor.AddComponent<CombatantTeam>();
            }

            team.Configure(teamId);
        }

        private void EquipAllOutWarPlayerWeapon()
        {
            playerWeapons?.Equip(new WeaponDefinition
            {
                DisplayName = "Army Rifle",
                Damage = 14f,
                Range = 76f,
                Cooldown = 0.46f,
                Ammo = 36
            });
        }

        private GameObject CreateCompanionDroid()
        {
            if (playerObject == null)
            {
                return null;
            }

            var player = playerObject.transform;
            if (!TryFindCompanionSpawnPosition(out var spawnPosition))
            {
                return null;
            }

            var companionName = GenerateCompanionName();
            var companion = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            companion.name = $"Rifleman {companionName}";
            companion.transform.SetParent(matchRoot.transform, false);
            companion.transform.position = spawnPosition;
            companion.transform.rotation = Quaternion.LookRotation(player.forward, Vector3.up);

            if (companion.TryGetComponent<Collider>(out var primitiveCollider))
            {
                Destroy(primitiveCollider);
            }

            if (companion.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.enabled = false;
            }

            DroidVisuals.Build(companion.transform, theme);
            DroidHitboxRig.Build(companion.transform);

            var controller = companion.AddComponent<CharacterController>();
            controller.height = 1.9f;
            controller.radius = 0.32f;

            var health = companion.AddComponent<CombatantHealth>();
            var baselineWave = Mathf.Max(1, currentWave);
            var normalDroidHealth = 52f + baselineWave * 9f;
            var companionMaxHealth = normalDroidHealth * 2f;
            health.Configure(companionName, companionMaxHealth);
            health.ConfigureShieldRecharge(4.8f, companionMaxHealth / 4f);
            health.Died += OnCompanionDied;

            var blasterRig = companion.AddComponent<DroidBlasterRig>();
            var muzzle = blasterRig.Build(theme);

            var weapons = companion.AddComponent<WeaponInventory>();
            weapons.Configure(health, muzzle.transform, theme.Beam);
            weapons.Equip(new WeaponDefinition
            {
                DisplayName = "Rifleman Blaster",
                Damage = 16f,
                Range = 76f,
                Cooldown = 0.42f,
                Ammo = 999
            });

            companion.AddComponent<CombatantVisualWalkAnimator>();
            companion.AddComponent<DroidCrouchPose>();
            companion.AddComponent<FootstepAudio>().Configure(false);
            companion.AddComponent<CompanionDroidController>().Configure(this, currentLayout, player, 1.18f);
            var playerCamera = player.GetComponentInChildren<Camera>();
            if (playerCamera != null)
            {
                var barObject = new GameObject("Friendly Companion Health Bar");
                barObject.transform.SetParent(companion.transform, false);
                barObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
                barObject.AddComponent<WorldHealthBar>().Build(health, playerCamera, true);
            }

            return companion;
        }

        private bool TryFindCompanionSpawnPosition(out Vector3 spawnPosition)
        {
            spawnPosition = Vector3.zero;
            var center = hasFabricatorRoom
                ? fabricatorRoomCenter
                : currentLayout != null && playerObject != null && currentLayout.TryGetCenter(currentLayout.GetNearestRoom(playerObject.transform.position), out var playerRoomCenter)
                    ? playerRoomCenter
                    : Vector3.zero;

            if (center == Vector3.zero && !hasFabricatorRoom)
            {
                return false;
            }

            var forward = Vector3.forward;
            var right = Vector3.right;
            var offsets = new[]
            {
                Vector3.zero,
                -right * 1.8f,
                right * 1.8f,
                forward * 1.8f,
                -forward * 1.8f,
                -right * 2.4f + forward * 1.35f,
                right * 2.4f + forward * 1.35f,
                -right * 2.4f - forward * 1.35f,
                right * 2.4f - forward * 1.35f
            };

            for (var i = 0; i < offsets.Length; i++)
            {
                if (TryGroundCompanionSpawn(center + offsets[i], out spawnPosition))
                {
                    return true;
                }
            }

            if (TryForceGroundCompanionSpawn(center, out spawnPosition))
            {
                return true;
            }

            spawnPosition = center + Vector3.up * 1.1f;
            return true;
        }

        private bool TryGroundCompanionSpawn(Vector3 candidate, out Vector3 grounded)
        {
            grounded = Vector3.zero;
            var start = candidate + Vector3.up * 3.4f;
            if (!Physics.Raycast(start, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (hit.normal.y < 0.72f || !IsFloorLike(hit.collider))
            {
                return false;
            }

            grounded = hit.point + Vector3.up * 1.1f;
            return IsCompanionSpawnClear(grounded);
        }

        private bool TryForceGroundCompanionSpawn(Vector3 candidate, out Vector3 grounded)
        {
            grounded = Vector3.zero;
            var start = candidate + Vector3.up * 3.4f;
            if (!Physics.Raycast(start, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            grounded = hit.point + Vector3.up * 1.1f;
            return true;
        }

        private bool IsCompanionSpawnClear(Vector3 position)
        {
            var overlaps = Physics.OverlapCapsule(position + Vector3.down * 0.78f, position + Vector3.up * 0.86f, 0.42f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var overlap in overlaps)
            {
                if (overlap == null || overlap.isTrigger || IsFloorLike(overlap))
                {
                    continue;
                }

                if (playerObject != null && overlap.transform.IsChildOf(playerObject.transform))
                {
                    continue;
                }

                if (overlap.GetComponentInParent<DestructibleArenaPiece>() != null ||
                    overlap.GetComponentInParent<CombatantHealth>() != null ||
                    overlap.GetComponentInParent<FabricatorStation>() != null ||
                    overlap.GetComponentInParent<HealingStation>() != null ||
                    overlap.GetComponentInParent<PlayerTurret>() != null)
                {
                    return false;
                }

                if (!overlap.name.ToLowerInvariant().Contains("floor"))
                {
                    return false;
                }
            }

            return true;
        }

        private static string GenerateCompanionName()
        {
            return CompanionNames[Random.Range(0, CompanionNames.Length)];
        }

        public void ToggleTurretPlacementMode()
        {
            SetTurretPlacementMode(!turretPlacementMode);
        }

        public void SetTurretPlacementMode(bool active)
        {
            turretPlacementMode = active && turretKits > 0 && !IsFabricatorMenuOpen;
            if (turretPlacementMode)
            {
                SetInteractionHint(Gamepad.current != null
                    ? "TURRET KIT\nRight trigger to place | Left trigger to cancel"
                    : "TURRET KIT\nLeft click to place | Right click to cancel");
            }
        }

        public GameObject CreateTurretPlacementPreview()
        {
            var preview = new GameObject("Turret Placement Preview");
            preview.transform.SetParent(matchRoot != null ? matchRoot.transform : null, false);
            PlayerTurret.BuildVisuals(preview.transform, theme, true);
            foreach (var collider in preview.GetComponentsInChildren<Collider>(true))
            {
                Destroy(collider);
            }

            preview.SetActive(false);
            return preview;
        }

        public bool TryResolveTurretPlacement(Ray ray, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (turretKits <= 0 || playerObject == null)
            {
                return false;
            }

            if (!Physics.Raycast(ray, out var hit, 7.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (hit.normal.y < 0.72f || hit.point.y > 1.2f || !IsFloorLike(hit.collider))
            {
                return false;
            }

            position = hit.point + Vector3.up * 0.05f;
            var player = playerObject.transform;
            var flatForward = player.forward;
            flatForward.y = 0f;
            rotation = Quaternion.LookRotation(flatForward.sqrMagnitude > 0.01f ? flatForward.normalized : Vector3.forward, Vector3.up);
            return IsTurretPlacementClear(position);
        }

        public bool TryPlaceTurret(Ray ray)
        {
            if (!TryResolveTurretPlacement(ray, out var position, out var rotation))
            {
                SetInteractionHint("TURRET KIT\nCannot place there");
                return false;
            }

            var turret = CreatePlayerTurret(position, rotation);
            if (turret != null)
            {
                activeTurrets.Add(turret);
                turretKits = Mathf.Max(0, turretKits - 1);
                if (turretKits == 0)
                {
                    turretPlacementMode = false;
                }

                SetInteractionHint(turretKits > 0
                    ? $"TURRET PLACED\n{turretKits} kit(s) remaining"
                    : "TURRET PLACED");
                return true;
            }

            return false;
        }

        private bool IsTurretPlacementClear(Vector3 position)
        {
            var overlaps = Physics.OverlapCapsule(position + Vector3.up * 0.28f, position + Vector3.up * 1.22f, 0.52f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var overlap in overlaps)
            {
                if (overlap == null || IsFloorLike(overlap))
                {
                    continue;
                }

                if (playerObject != null && overlap.transform.IsChildOf(playerObject.transform))
                {
                    continue;
                }

                if (overlap.GetComponentInParent<PlayerTurret>() != null ||
                    overlap.GetComponentInParent<CombatantHealth>() != null ||
                    overlap.GetComponentInParent<DestructibleArenaPiece>() != null ||
                    overlap.GetComponentInParent<FabricatorStation>() != null ||
                    overlap.GetComponentInParent<HealingStation>() != null)
                {
                    return false;
                }

                if (!overlap.isTrigger && overlap.name.ToLowerInvariant().Contains("wall"))
                {
                    return false;
                }
            }

            return true;
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

        private PlayerTurret CreatePlayerTurret(Vector3 deployPosition, Quaternion deployRotation)
        {
            if (playerObject == null)
            {
                return null;
            }

            var turretObject = new GameObject("Player Auto Turret");
            turretObject.transform.SetParent(matchRoot.transform, false);
            turretObject.transform.position = deployPosition;
            turretObject.transform.rotation = deployRotation;
            var turret = turretObject.AddComponent<PlayerTurret>().Configure(this, theme);
            if (turretMobilityUpgrade)
            {
                turret.EnableMobility(currentLayout, playerObject.transform);
            }

            var playerCamera = playerObject.GetComponentInChildren<Camera>();
            if (playerCamera != null && turretObject.TryGetComponent<CombatantHealth>(out var health))
            {
                var barObject = new GameObject("Friendly Turret Health Bar");
                barObject.transform.SetParent(turretObject.transform, false);
                barObject.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                barObject.AddComponent<WorldHealthBar>().Build(health, playerCamera, true);
            }

            return turret;
        }

        private Vector3 GetGroundedDeployPosition(Vector3 position)
        {
            var start = position + Vector3.up * 2f;
            if (Physics.Raycast(start, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point + Vector3.up * 0.05f;
            }

            return position;
        }

        private GameObject CreateOpponent(Vector3 position, ArenaLayout layout, Transform player)
        {
            var opponent = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            opponent.name = "Opponent Combatant";
            opponent.transform.SetParent(matchRoot.transform, false);
            opponent.transform.position = position;
            opponent.transform.rotation = layout.OpponentRotation;

            if (opponent.TryGetComponent<Collider>(out var primitiveCollider))
            {
                Destroy(primitiveCollider);
            }

            if (opponent.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = theme.Opponent;
            }

            AddOpponentVisuals(opponent.transform);

            var controller = opponent.AddComponent<CharacterController>();
            controller.height = 1.9f;
            controller.radius = 0.38f;

            opponentCombatant = opponent.AddComponent<CombatantHealth>();
            opponentCombatant.Configure("Unknown Gladiator", opponentHealth);

            var muzzle = new GameObject("Opponent Muzzle");
            muzzle.transform.SetParent(opponent.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.62f, 0.28f);

            var weapons = opponent.AddComponent<WeaponInventory>();
            weapons.Configure(opponentCombatant, muzzle.transform, theme.Beam);

            var ai = opponent.AddComponent<OpponentController>();
            ai.Configure(this, layout, player);
            opponent.AddComponent<FootstepAudio>().Configure(false);

            var playerCamera = player.GetComponentInChildren<Camera>();
            if (playerCamera != null)
            {
                var barObject = new GameObject("Opponent Health Bar");
                barObject.transform.SetParent(opponent.transform, false);
                barObject.transform.localPosition = new Vector3(0f, 2.15f, 0f);
                barObject.AddComponent<WorldHealthBar>().Build(opponentCombatant, playerCamera);
            }

            return opponent;
        }

        private void AddOpponentVisuals(Transform opponent)
        {
            if (OpponentCombatantAsset.TryBuild(opponent, theme))
            {
                if (opponent.TryGetComponent<Renderer>(out var parentRenderer))
                {
                    parentRenderer.enabled = false;
                }

                opponent.gameObject.AddComponent<CombatantVisualWalkAnimator>();
                AddOpponentTrackingLight(opponent);
                return;
            }

            CreateChildPrimitive(opponent, "Opponent Head", PrimitiveType.Sphere, theme.Opponent, new Vector3(0f, 0.98f, 0f), new Vector3(0.42f, 0.42f, 0.42f), Vector3.zero);
            CreateChildPrimitive(opponent, "Opponent Left Arm", PrimitiveType.Cylinder, theme.NeonB, new Vector3(-0.46f, 0.18f, 0f), new Vector3(0.07f, 0.35f, 0.07f), new Vector3(10f, 0f, 18f));
            CreateChildPrimitive(opponent, "Opponent Right Arm", PrimitiveType.Cylinder, theme.NeonB, new Vector3(0.46f, 0.18f, 0f), new Vector3(0.07f, 0.35f, 0.07f), new Vector3(10f, 0f, -18f));
            CreateChildPrimitive(opponent, "Opponent Beacon", PrimitiveType.Cube, theme.Pickup, new Vector3(0f, 1.75f, 0f), new Vector3(0.32f, 0.32f, 0.32f), new Vector3(35f, 45f, 35f));

            opponent.gameObject.AddComponent<CombatantVisualWalkAnimator>();
            AddOpponentTrackingLight(opponent);
        }

        private void AddOpponentTrackingLight(Transform opponent)
        {
            var lightObject = new GameObject("Opponent Tracking Light");
            lightObject.transform.SetParent(opponent, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.color = new Color(1f, 0.08f, 0.72f);
            light.range = 6f;
            light.intensity = 2.4f;
        }

        private GameObject CreateChildPrimitive(Transform parent, string objectName, PrimitiveType primitiveType, Material material, Vector3 localPosition, Vector3 localScale, Vector3 localRotation)
        {
            var primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            primitive.transform.localRotation = Quaternion.Euler(localRotation);

            if (primitive.TryGetComponent<Collider>(out var collider))
            {
                Destroy(collider);
            }

            if (primitive.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
            }

            return primitive;
        }

        private void CreatePickups(ArenaLayout layout)
        {
            foreach (var point in layout.PickupPoints)
            {
                var slot = CreatePickupPadSlot(point);
                SpawnPickupOnPad(slot, PickupKind.Weapon);
            }

            CreateAmmoPickups(layout);
        }

        private void CreateAmmoPickups(ArenaLayout layout)
        {
            var rooms = new List<Vector3>(layout.RoomCenters.Values);
            for (var i = 1; i < rooms.Count; i += 3)
            {
                var slot = CreatePickupPadSlot(rooms[i] + new Vector3(Random.Range(-2.4f, 2.4f), 0.72f, Random.Range(-2.4f, 2.4f)));
                SpawnPickupOnPad(slot, PickupKind.Ammo);
            }
        }

        private void CreateHealthItems(ArenaLayout layout)
        {
            var rooms = new List<Vector3>(layout.RoomCenters.Values);
            rooms.Sort((a, b) => (a - layout.PlayerSpawn).sqrMagnitude.CompareTo((b - layout.PlayerSpawn).sqrMagnitude));

            var created = 0;
            for (var i = rooms.Count - 1; i >= 0 && created < healthPickupCount; i -= 2)
            {
                var position = rooms[i] + new Vector3(Random.Range(-2.8f, 2.8f), 0.65f, Random.Range(-2.8f, 2.8f));
                CreateHealthPickup(position);
                created++;
            }

            if (rooms.Count > 2)
            {
                var stationRoom = rooms[Mathf.Clamp(rooms.Count / 2, 0, rooms.Count - 1)];
                CreateHealingStation(stationRoom + new Vector3(0f, 0.08f, 0f));
            }
        }

        private void CreateAllOutWarPickups(ArenaLayout layout)
        {
            var rooms = GetAllOutWarResourceRooms(layout, false);
            ShuffleList(rooms);
            var targetCount = CalculateAllOutWarPickupPadCount(layout, rooms.Count);
            for (var i = 0; i < rooms.Count && i < targetCount; i++)
            {
                if (!TryPickAllOutWarResourcePosition(layout, rooms[i].Value, 0.72f, 3.35f, out var position))
                {
                    continue;
                }

                var slot = CreatePickupPadSlot(position);
                SpawnPickupOnPad(slot, ChooseAllOutWarPickupKind());
            }
        }

        private int CalculateAllOutWarPickupPadCount(ArenaLayout layout, int eligibleRoomCount)
        {
            if (eligibleRoomCount <= 0)
            {
                return 0;
            }

            var armies = allOutWarSettings != null ? allOutWarSettings.TotalArmies : Mathf.Max(2, AllOutWarArmyCount);
            var activeCap = allOutWarSettings != null ? Mathf.Max(1, allOutWarSettings.BattlefieldCap) : 24;
            var roomScale = layout != null ? layout.RoomCenters.Count : eligibleRoomCount;
            var radiusScale = layout != null ? layout.CircularRadius / Mathf.Max(1f, roomSize + corridorLength) : 0f;
            var desired = Mathf.CeilToInt(armies * 3.2f + activeCap / 12f + roomScale * 0.18f + radiusScale * 0.65f);
            var minimum = Mathf.Min(eligibleRoomCount, Mathf.Max(6, armies * 2));
            var maximum = Mathf.Min(eligibleRoomCount, Mathf.Max(12, Mathf.CeilToInt(eligibleRoomCount * 0.48f)));
            return Mathf.Clamp(desired, minimum, maximum);
        }

        private void CreateAllOutWarHealthItems(ArenaLayout layout)
        {
            var rooms = GetAllOutWarResourceRooms(layout, true);
            ShuffleList(rooms);
            var targetStations = CalculateAllOutWarHealingStationCount(layout);
            var placed = new List<Vector3>();
            var minSpacing = Mathf.Max(roomSize + corridorLength, 12f) * 1.55f;
            foreach (var room in rooms)
            {
                if (placed.Count >= targetStations)
                {
                    break;
                }

                if (!IsFarFromPositions(room.Value, placed, minSpacing))
                {
                    continue;
                }

                if (!TryPickAllOutWarResourcePosition(layout, room.Value, 0.08f, 1.8f, out var position))
                {
                    continue;
                }

                CreateHealingStation(position);
                placed.Add(room.Value);
            }

            if (placed.Count < targetStations)
            {
                foreach (var room in rooms)
                {
                if (placed.Count >= targetStations)
                {
                    break;
                }

                if (!IsFarFromPositions(room.Value, placed, 0.1f))
                {
                    continue;
                }

                if (!TryPickAllOutWarResourcePosition(layout, room.Value, 0.08f, 1.8f, out var position))
                {
                    continue;
                }

                CreateHealingStation(position);
                placed.Add(room.Value);
                }
            }
        }

        private int CalculateAllOutWarHealingStationCount(ArenaLayout layout)
        {
            var armies = allOutWarSettings != null ? allOutWarSettings.TotalArmies : Mathf.Max(2, AllOutWarArmyCount);
            var activeCap = allOutWarSettings != null ? Mathf.Max(1, allOutWarSettings.BattlefieldCap) : 24;
            var roomBonus = layout != null ? layout.RoomCenters.Count / 34 : 0;
            var activeBonus = Mathf.Max(0, (activeCap - 24) / 72);
            return Mathf.Clamp(Mathf.CeilToInt(armies * 0.45f) + roomBonus + activeBonus, 2, 7);
        }

        private List<KeyValuePair<Vector2Int, Vector3>> GetAllOutWarResourceRooms(ArenaLayout layout, bool excludeSpawnRooms)
        {
            var rooms = new List<KeyValuePair<Vector2Int, Vector3>>();
            if (layout == null)
            {
                return rooms;
            }

            foreach (var room in layout.RoomCenters)
            {
                if (layout.ClearingRoomGroups.ContainsKey(room.Key))
                {
                    continue;
                }

                if (excludeSpawnRooms && IsAllOutWarSpawnRoom(layout, room.Key))
                {
                    continue;
                }

                if (IsAllOutWarSpawnReservedPosition(layout, room.Value, roomSize * 0.75f))
                {
                    continue;
                }

                rooms.Add(room);
            }

            return rooms;
        }

        private static bool TryPickAllOutWarResourcePosition(ArenaLayout layout, Vector3 roomCenter, float height, float horizontalRange, out Vector3 position)
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                position = roomCenter + new Vector3(Random.Range(-horizontalRange, horizontalRange), height, Random.Range(-horizontalRange, horizontalRange));
                if (!IsAllOutWarSpawnReservedPosition(layout, position, 1.2f))
                {
                    return true;
                }
            }

            position = roomCenter + Vector3.up * height;
            return !IsAllOutWarSpawnReservedPosition(layout, position, 1.2f);
        }

        private static bool IsAllOutWarSpawnReservedPosition(ArenaLayout layout, Vector3 position, float extraPadding = 0f)
        {
            if (layout == null)
            {
                return false;
            }

            foreach (var region in layout.ArmySpawnRegions)
            {
                if (region != null && region.ContainsPoint(position, extraPadding))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAllOutWarSpawnRoom(ArenaLayout layout, Vector2Int room)
        {
            foreach (var region in layout.ArmySpawnRegions)
            {
                if (region != null && region.Room == room)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFarFromPositions(Vector3 position, List<Vector3> others, float minimumDistance)
        {
            var minSqr = minimumDistance * minimumDistance;
            foreach (var other in others)
            {
                if ((position - other).sqrMagnitude < minSqr)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ShuffleList<T>(List<T> items)
        {
            for (var i = 0; i < items.Count; i++)
            {
                var swap = Random.Range(i, items.Count);
                (items[i], items[swap]) = (items[swap], items[i]);
            }
        }

        private void CreateFabricatorStation(ArenaLayout layout)
        {
            var roomEntries = new List<KeyValuePair<Vector2Int, Vector3>>(layout.RoomCenters);
            roomEntries.Sort((a, b) => (a.Value - layout.PlayerSpawn).sqrMagnitude.CompareTo((b.Value - layout.PlayerSpawn).sqrMagnitude));
            if (!TryChooseFabricatorWall(layout, roomEntries, out var stationRoom, out var outward))
            {
                return;
            }

            fabricatorRoomCenter = stationRoom;
            hasFabricatorRoom = true;
            var stationPosition = stationRoom + outward * (roomSize * 0.5f - 0.9f) + Vector3.up * 0.02f;
            var station = new GameObject("Weapon Fabricator Station");
            station.transform.SetParent(matchRoot.transform, false);
            station.transform.position = stationPosition;
            station.transform.rotation = Quaternion.LookRotation(-outward, Vector3.up);

            var trigger = station.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.75f;

            var platform = station.AddComponent<BoxCollider>();
            platform.center = new Vector3(0f, 0.12f, 0f);
            platform.size = new Vector3(1.75f, 0.24f, 1.75f);

            station.AddComponent<FabricatorStation>().Configure(this, theme);
        }

        private bool TryChooseFabricatorWall(ArenaLayout layout, List<KeyValuePair<Vector2Int, Vector3>> roomEntries, out Vector3 roomCenter, out Vector3 outward)
        {
            roomCenter = Vector3.zero;
            outward = Vector3.forward;
            foreach (var entry in roomEntries)
            {
                var solidDirections = GetSolidFabricatorWallDirections(layout, entry.Key);
                if (solidDirections.Count == 0)
                {
                    continue;
                }

                var choice = solidDirections[Random.Range(0, solidDirections.Count)];
                roomCenter = entry.Value;
                outward = new Vector3(choice.x, 0f, choice.y).normalized;
                return true;
            }

            return false;
        }

        private List<Vector2Int> GetSolidFabricatorWallDirections(ArenaLayout layout, Vector2Int room)
        {
            var directions = new[]
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };

            var unconnected = new List<Vector2Int>();
            foreach (var direction in directions)
            {
                if (!layout.Rooms.Contains(room + direction) && !HasGateOnWall(layout, room, direction))
                {
                    unconnected.Add(direction);
                }
            }

            return unconnected;
        }

        private static bool HasGateOnWall(ArenaLayout layout, Vector2Int room, Vector2Int direction)
        {
            foreach (var gate in layout.GateSpawns)
            {
                if (gate != null && gate.Room == room && gate.Direction == direction)
                {
                    return true;
                }
            }

            return false;
        }

        public void AddScrap(int amount)
        {
            scrap += Mathf.Max(0, amount);
        }

        public void TryBuyWeaponUpgrade()
        {
            if (playerWeapons == null)
            {
                return;
            }

            var cost = NextUpgradeCost;
            if (scrap < cost)
            {
                SetInteractionHint($"FABRICATOR\nNeed {cost} scrap for damage level {damageUpgradeLevel + 1}");
                return;
            }

            scrap -= cost;
            damageUpgradeLevel++;
            playerWeapons.SetDamageMultiplier(1f + damageUpgradeLevel * 0.12f);
            SetInteractionHint($"DAMAGE LEVEL {damageUpgradeLevel}\nPlayer damage +{damageUpgradeLevel * 12}% | Scrap {scrap}");
        }

        public int GetFabricatorRowCount(FabricatorTab tab)
        {
            if (tab == FabricatorTab.Buy)
            {
                return 4;
            }

            if (TryGetSelectedArmyUnit(out var selected))
            {
                return selected.GetComponent<PlayerTurret>() != null ? 4 : 3;
            }

            return Mathf.Clamp(ActiveArmyUnitCount(), 1, 6);
        }

        public string GetFabricatorRowName(FabricatorTab tab, int index)
        {
            if (tab == FabricatorTab.Buy)
            {
                return index switch
                {
                    0 => $"Damage level {damageUpgradeLevel + 1}",
                    1 => "Player shield",
                    2 => "Rifleman soldier",
                    3 => "Turret deployment kit",
                    _ => ""
                };
            }

            if (TryGetSelectedArmyUnit(out var selectedUnit))
            {
                var upgrades = GetUpgradeState(selectedUnit);
                if (selectedUnit.GetComponent<PlayerTurret>() != null)
                {
                    return index switch
                    {
                        0 => $"Back to army list",
                        1 => upgrades.MobilityInstalled ? "Walker legs installed" : "Install walker legs",
                        2 => upgrades.HealthBoostInstalled ? "Health boost installed" : "Increase health +50%",
                        3 => upgrades.ShieldInstalled ? "Shield installed" : "Install shield",
                        _ => ""
                    };
                }

                return index switch
                {
                    0 => $"Back to army list",
                    1 => upgrades.HealthBoostInstalled ? "Health boost installed" : "Increase health +50%",
                    2 => upgrades.ShieldInstalled ? "Shield installed" : "Install shield",
                    _ => ""
                };
            }

            return GetArmyUnitRowName(index);
        }

        public int GetFabricatorRowCost(FabricatorTab tab, int index)
        {
            if (tab == FabricatorTab.Buy)
            {
                return index switch
                {
                    0 => NextUpgradeCost,
                    1 => ShieldCost,
                    2 => CompanionCost,
                    3 => TurretCost,
                    _ => 0
                };
            }

            if (TryGetSelectedArmyUnit(out var selectedUnit))
            {
                var upgrades = GetUpgradeState(selectedUnit);
                if (selectedUnit.GetComponent<PlayerTurret>() != null)
                {
                    return index switch
                    {
                        0 => 0,
                        1 => upgrades.MobilityInstalled ? 0 : TurretMobilityCost,
                        2 => upgrades.HealthBoostInstalled ? 0 : TurretHealthBoostCost,
                        3 => upgrades.ShieldInstalled ? 0 : TurretShieldCost,
                        _ => 0
                    };
                }

                return index switch
                {
                    0 => 0,
                    1 => upgrades.HealthBoostInstalled ? 0 : CompanionHealthBoostCost,
                    2 => upgrades.ShieldInstalled ? 0 : CompanionShieldCost,
                    _ => 0
                };
            }

            return 0;
        }

        public string GetFabricatorRowCostLabel(FabricatorTab tab, int index)
        {
            var cost = GetFabricatorRowCost(tab, index);
            if (tab == FabricatorTab.UpgradeArmy)
            {
                if (!TryGetSelectedArmyUnit(out _))
                {
                    return ActiveArmyUnitCount() > 0 ? "SELECT" : "--";
                }

                if (index == 0)
                {
                    return "BACK";
                }
            }

            return cost > 0 ? $"{cost} SCRAP" : "ACTIVE";
        }

        public bool CanAffordFabricatorRow(FabricatorTab tab, int index)
        {
            var cost = GetFabricatorRowCost(tab, index);
            return cost <= 0 || scrap >= cost;
        }

        public bool IsFabricatorRowAvailable(FabricatorTab tab, int index)
        {
            if (tab == FabricatorTab.Buy)
            {
                return index switch
                {
                    0 => playerWeapons != null,
                    _ => true
                };
            }

            if (TryGetSelectedArmyUnit(out var selectedUnit))
            {
                var health = selectedUnit.GetComponent<CombatantHealth>();
                if (health == null || !health.IsAlive)
                {
                    selectedArmyUnit = null;
                    return false;
                }

                var upgrades = GetUpgradeState(selectedUnit);
                if (selectedUnit.GetComponent<PlayerTurret>() != null)
                {
                    return index switch
                    {
                        0 => true,
                        1 => !upgrades.MobilityInstalled,
                        2 => !upgrades.HealthBoostInstalled,
                        3 => !upgrades.ShieldInstalled,
                        _ => false
                    };
                }

                return index switch
                {
                    0 => true,
                    1 => !upgrades.HealthBoostInstalled,
                    2 => !upgrades.ShieldInstalled,
                    _ => false
                };
            }

            return ActiveArmyUnitCount() > 0 && index < ActiveArmyUnitCount();
        }

        public void ShowFabricatorMenu(FabricatorTab tab, int selectedIndex, bool controller)
        {
            IsFabricatorMenuOpen = true;
            hud?.ShowFabricatorMenu(tab, selectedIndex, controller);
        }

        public void HideFabricatorMenu()
        {
            IsFabricatorMenuOpen = false;
            InputSuppressedUntil = Time.time + 0.16f;
            hud?.HideFabricatorMenu();
        }

        public void TryBuyFabricatorSelection(FabricatorTab tab, int index)
        {
            if (tab == FabricatorTab.Buy)
            {
                switch (index)
                {
                    case 0:
                        TryBuyWeaponUpgrade();
                        break;
                    case 1:
                        TryBuyShield();
                        break;
                    case 2:
                        TryBuyCompanion();
                        break;
                    case 3:
                        TryBuyTurret();
                        break;
                }

                return;
            }

            if (!TryGetSelectedArmyUnit(out _))
            {
                TrySelectOrBackArmyUnit(index);
                return;
            }

            if (index == 0)
            {
                selectedArmyUnit = null;
                return;
            }

            TryUpgradeSelectedArmyUnit(index);
        }

        private void TrySelectOrBackArmyUnit(int index)
        {
            selectedArmyUnit = GetArmyUnitAt(index);
            if (selectedArmyUnit != null && selectedArmyUnit.TryGetComponent<CombatantHealth>(out var health))
            {
                SetInteractionHint($"{health.DisplayName.ToUpperInvariant()}\nSelect an upgrade");
            }
        }

        private const int ShieldCost = 35;
        private const int CompanionCost = 60;
        private const int CompanionHealthBoostCost = 35;
        private const int CompanionShieldCost = 35;
        private const int CompanionUpgradeCost = CompanionHealthBoostCost;
        private const int CompanionDamageUpgradeCost = 70;
        private const int TurretCost = 90;
        private const int TurretMobilityCost = 65;
        private const int TurretHealthBoostCost = 35;
        private const int TurretShieldCost = 50;
        private const int TurretArmorUpgradeCost = TurretHealthBoostCost;
        private const int FieldHealCost = 25;

        private bool TryGetSelectedArmyUnit(out GameObject unit)
        {
            unit = selectedArmyUnit;
            if (unit == null)
            {
                return false;
            }

            if (!unit.TryGetComponent<CombatantHealth>(out var health) || !health.IsAlive)
            {
                selectedArmyUnit = null;
                unit = null;
                return false;
            }

            return true;
        }

        private int ActiveArmyUnitCount()
        {
            return ActiveCompanionCount() + ActiveTurretCount();
        }

        private int ActiveTurretCount()
        {
            var count = 0;
            for (var i = activeTurrets.Count - 1; i >= 0; i--)
            {
                var turret = activeTurrets[i];
                if (turret == null)
                {
                    activeTurrets.RemoveAt(i);
                    continue;
                }

                if (turret.TryGetComponent<CombatantHealth>(out var health) && health.IsAlive)
                {
                    count++;
                }
            }

            return count;
        }

        private GameObject GetArmyUnitAt(int index)
        {
            var current = 0;
            for (var i = activeCompanions.Count - 1; i >= 0; i--)
            {
                var companion = activeCompanions[i];
                if (companion == null)
                {
                    activeCompanions.RemoveAt(i);
                    continue;
                }

                if (!companion.TryGetComponent<CombatantHealth>(out var health) || !health.IsAlive)
                {
                    continue;
                }

                if (current == index)
                {
                    return companion;
                }

                current++;
            }

            for (var i = activeTurrets.Count - 1; i >= 0; i--)
            {
                var turret = activeTurrets[i];
                if (turret == null)
                {
                    activeTurrets.RemoveAt(i);
                    continue;
                }

                if (!turret.TryGetComponent<CombatantHealth>(out var health) || !health.IsAlive)
                {
                    continue;
                }

                if (current == index)
                {
                    return turret.gameObject;
                }

                current++;
            }

            return null;
        }

        private string GetArmyUnitRowName(int index)
        {
            var unit = GetArmyUnitAt(index);
            if (unit == null)
            {
                return ActiveArmyUnitCount() == 0 ? "No army units deployed" : "";
            }

            if (!unit.TryGetComponent<CombatantHealth>(out var health))
            {
                return unit.name;
            }

            var label = unit.GetComponent<PlayerTurret>() != null ? "Turret" : "Rifleman";
            return $"{label}: {health.DisplayName}  HP {Mathf.CeilToInt(health.CurrentHealth)}/{Mathf.CeilToInt(health.MaxHealth)}";
        }

        private ArmyUnitUpgradeState GetUpgradeState(GameObject unit)
        {
            if (!armyUpgradeStates.TryGetValue(unit, out var state))
            {
                state = new ArmyUnitUpgradeState();
                armyUpgradeStates[unit] = state;
            }

            return state;
        }

        private void TryUpgradeSelectedArmyUnit(int index)
        {
            if (!TryGetSelectedArmyUnit(out var unit))
            {
                return;
            }

            if (unit.GetComponent<PlayerTurret>() != null)
            {
                TryUpgradeSelectedTurret(unit, index);
            }
            else
            {
                TryUpgradeSelectedRifleman(unit, index);
            }
        }

        private void TryUpgradeSelectedRifleman(GameObject unit, int index)
        {
            var health = unit.GetComponent<CombatantHealth>();
            var upgrades = GetUpgradeState(unit);
            switch (index)
            {
                case 1:
                    if (upgrades.HealthBoostInstalled)
                    {
                        SetInteractionHint("RIFLEMAN HEALTH\nAlready upgraded");
                        return;
                    }

                    if (!SpendScrap(CompanionHealthBoostCost, "rifleman health"))
                    {
                        return;
                    }

                    upgrades.HealthBoostInstalled = true;
                    health.IncreaseMaxHealth(health.MaxHealth * 0.5f, true);
                    SetInteractionHint($"{health.DisplayName.ToUpperInvariant()} HEALTH BOOST\nMax health +50% | Scrap {scrap}");
                    break;
                case 2:
                    if (upgrades.ShieldInstalled)
                    {
                        SetInteractionHint("RIFLEMAN SHIELD\nAlready installed");
                        return;
                    }

                    if (!SpendScrap(CompanionShieldCost, "rifleman shield"))
                    {
                        return;
                    }

                    upgrades.ShieldInstalled = true;
                    health.ConfigureShieldRecharge(4.8f, health.MaxHealth / 4f);
                    health.AddShield(health.MaxHealth, health.MaxHealth);
                    SetInteractionHint($"{health.DisplayName.ToUpperInvariant()} SHIELD ONLINE\nRecharging shield installed | Scrap {scrap}");
                    break;
            }
        }

        private void TryUpgradeSelectedTurret(GameObject unit, int index)
        {
            var health = unit.GetComponent<CombatantHealth>();
            var upgrades = GetUpgradeState(unit);
            switch (index)
            {
                case 1:
                    if (upgrades.MobilityInstalled)
                    {
                        SetInteractionHint("TURRET LEGS\nAlready installed");
                        return;
                    }

                    if (!SpendScrap(TurretMobilityCost, "turret walker legs"))
                    {
                        return;
                    }

                    upgrades.MobilityInstalled = true;
                    if (unit.TryGetComponent<PlayerTurret>(out var turret) && playerObject != null)
                    {
                        turret.EnableMobility(currentLayout, playerObject.transform);
                    }

                    SetInteractionHint("TURRET LEGS INSTALLED\nThis turret can reposition | Scrap " + scrap);
                    break;
                case 2:
                    if (upgrades.HealthBoostInstalled)
                    {
                        SetInteractionHint("TURRET HEALTH\nAlready upgraded");
                        return;
                    }

                    if (!SpendScrap(TurretHealthBoostCost, "turret health"))
                    {
                        return;
                    }

                    upgrades.HealthBoostInstalled = true;
                    health.IncreaseMaxHealth(health.MaxHealth * 0.5f, true);
                    SetInteractionHint($"TURRET HEALTH BOOST\nMax health +50% | Scrap {scrap}");
                    break;
                case 3:
                    if (upgrades.ShieldInstalled)
                    {
                        SetInteractionHint("TURRET SHIELD\nAlready installed");
                        return;
                    }

                    if (!SpendScrap(TurretShieldCost, "turret shield"))
                    {
                        return;
                    }

                    upgrades.ShieldInstalled = true;
                    health.ConfigureShieldRecharge(5.2f, health.MaxHealth / 4f);
                    health.AddShield(health.MaxHealth, health.MaxHealth);
                    SetInteractionHint($"TURRET SHIELD ONLINE\nRecharging shield installed | Scrap {scrap}");
                    break;
            }
        }

        private void TryBuyShield()
        {
            if (playerCombatant == null)
            {
                return;
            }

            if (playerCombatant.CurrentShield >= playerCombatant.MaxHealth - 0.5f)
            {
                SetInteractionHint($"SHIELD FULL\nShield {Mathf.CeilToInt(playerCombatant.CurrentShield)} | Scrap {scrap}");
                return;
            }

            if (!SpendScrap(ShieldCost, "player shield"))
            {
                return;
            }

            playerCombatant.AddShield(playerCombatant.MaxHealth, playerCombatant.MaxHealth);
            playerCombatant.ConfigureShieldRecharge(5.2f, playerCombatant.MaxHealth / 3.4f);
            SetInteractionHint($"SHIELD CHARGED\nShield {Mathf.CeilToInt(playerCombatant.CurrentShield)} | Scrap {scrap}");
        }

        private void TryBuyCompanionUpgrade()
        {
            if (ActiveCompanionCount() == 0)
            {
                SetInteractionHint("RIFLEMAN UPGRADE\nDeploy a rifleman first");
                return;
            }

            if (companionUpgradeLevel > 0)
            {
                SetInteractionHint("RIFLEMAN UPGRADE ACTIVE\nArmor and shield already installed");
                return;
            }

            if (!SpendScrap(CompanionUpgradeCost, "companion upgrade"))
            {
                return;
            }

            companionUpgradeLevel = 1;
            ApplyCompanionArmorUpgradeToAll();

            SetInteractionHint($"RIFLEMEN UPGRADED\nArmor + recharging shields | Scrap {scrap}");
        }

        private void TryBuyCompanionDamageUpgrade()
        {
            if (ActiveCompanionCount() == 0)
            {
                SetInteractionHint("RIFLEMAN DAMAGE\nDeploy a rifleman first");
                return;
            }

            if (!SpendScrap(CompanionDamageUpgradeCost, "rifleman damage"))
            {
                return;
            }

            companionDamageUpgradeLevel++;
            ApplyCompanionDamageUpgradeToAll();
            SetInteractionHint($"RIFLEMAN DAMAGE {companionDamageUpgradeLevel}\nSquad damage +{companionDamageUpgradeLevel * 14}% | Scrap {scrap}");
        }

        private void TryBuyCompanion()
        {
            if (!SpendScrap(CompanionCost, "rifleman soldier"))
            {
                return;
            }

            var companionObject = CreateCompanionDroid();
            if (companionObject == null)
            {
                scrap += CompanionCost;
                SetInteractionHint("RIFLEMAN DEPLOY FAILED\nMove to a clearer spot and try again");
                return;
            }

            activeCompanions.Add(companionObject);
            var companionName = companionObject != null && companionObject.TryGetComponent<CombatantHealth>(out var health)
                ? health.DisplayName
                : "Rifleman";
            SetInteractionHint($"{companionName.ToUpperInvariant()} DEPLOYED\nScrap {scrap}");
        }

        private void TryBuyTurret()
        {
            if (!SpendScrap(TurretCost, "turret"))
            {
                return;
            }

            turretKits++;
            SetTurretPlacementMode(true);
            SetInteractionHint(Gamepad.current != null
                ? $"TURRET KIT ACQUIRED\nD-pad down selects kit | Right trigger places | Scrap {scrap}"
                : $"TURRET KIT ACQUIRED\nPress 4 to select kit | Left click places | Scrap {scrap}");
        }

        private void TryBuyTurretMobilityUpgrade()
        {
            if (turretMobilityUpgrade)
            {
                SetInteractionHint("TURRET LEGS ONLINE\nAll turrets can reposition");
                return;
            }

            if (!SpendScrap(TurretMobilityCost, "turret walker legs"))
            {
                return;
            }

            turretMobilityUpgrade = true;
            foreach (var turret in activeTurrets)
            {
                if (turret != null && playerObject != null)
                {
                    turret.EnableMobility(currentLayout, playerObject.transform);
                }
            }

            SetInteractionHint($"TURRET LEGS ONLINE\nTurrets move, deploy, then fire | Scrap {scrap}");
        }

        private void TryBuyTurretArmorUpgrade()
        {
            if (activeTurrets.Count == 0 && turretKits <= 0)
            {
                SetInteractionHint("TURRET ARMOR\nPlace or buy a turret first");
                return;
            }

            if (!SpendScrap(TurretArmorUpgradeCost, "turret armor"))
            {
                return;
            }

            turretArmorUpgradeLevel++;
            foreach (var turret in activeTurrets)
            {
                if (turret != null && turret.TryGetComponent<CombatantHealth>(out var health))
                {
                    health.IncreaseMaxHealth(health.MaxHealth * 0.25f, true);
                }
            }

            SetInteractionHint($"TURRET ARMOR {turretArmorUpgradeLevel}\nCurrent and future turrets tougher | Scrap {scrap}");
        }

        private int ActiveCompanionCount()
        {
            var count = 0;
            for (var i = activeCompanions.Count - 1; i >= 0; i--)
            {
                var companion = activeCompanions[i];
                if (companion == null)
                {
                    activeCompanions.RemoveAt(i);
                    continue;
                }

                if (companion.TryGetComponent<CombatantHealth>(out var health) && health.IsAlive)
                {
                    count++;
                }
            }

            return count;
        }

        private void ApplyCompanionArmorUpgradeToAll()
        {
            foreach (var companion in activeCompanions)
            {
                if (companion != null && companion.TryGetComponent<CombatantHealth>(out var health))
                {
                    var addedHealth = health.MaxHealth * 0.3f;
                    health.IncreaseMaxHealth(addedHealth, true);
                    health.ConfigureShieldRecharge(4.8f, health.MaxHealth / 4f);
                    health.AddShield(health.MaxHealth * 0.5f, health.MaxHealth * 0.5f);
                }
            }
        }

        private void ApplyCompanionDamageUpgradeToAll()
        {
            foreach (var companion in activeCompanions)
            {
                if (companion != null && companion.TryGetComponent<WeaponInventory>(out var weapons))
                {
                    weapons.SetDamageMultiplier(1f + companionDamageUpgradeLevel * 0.14f);
                }
            }
        }

        private bool SpendScrap(int cost, string itemName)
        {
            if (scrap < cost)
            {
                SetInteractionHint($"FABRICATOR\nNeed {cost} scrap for {itemName}");
                return false;
            }

            scrap -= cost;
            return true;
        }

        public void SetInteractionHint(string message)
        {
            hud?.SetCenterMessage(message);
        }

        public void ClearInteractionHint()
        {
            if (IsMatchActive)
            {
                hud?.SetCenterMessage("");
            }
        }

        private void CreateHealthPickup(Vector3 position)
        {
            var slot = CreatePickupPadSlot(position);
            SpawnPickupOnPad(slot, PickupKind.Health);
        }

        private PickupPadSlot CreatePickupPadSlot(Vector3 position)
        {
            var slot = new PickupPadSlot
            {
                Position = position
            };
            pickupPadSlots.Add(slot);
            return slot;
        }

        private enum PickupKind
        {
            Weapon,
            Ammo,
            Health
        }

        private void RefillEmptyPickupPads(bool useAllOutWarWeights = false)
        {
            foreach (var slot in pickupPadSlots)
            {
                if (slot.Occupant != null)
                {
                    continue;
                }

                SpawnPickupOnPad(slot, useAllOutWarWeights ? ChooseAllOutWarPickupKind() : ChooseRandomPickupKind());
            }
        }

        private PickupKind ChooseRandomPickupKind()
        {
            var roll = Random.value;
            if (roll < 0.34f)
            {
                return PickupKind.Weapon;
            }

            if (roll < 0.72f)
            {
                return PickupKind.Ammo;
            }

            return PickupKind.Health;
        }

        private PickupKind ChooseAllOutWarPickupKind()
        {
            var roll = Random.value;
            if (roll < 0.15f)
            {
                return PickupKind.Weapon;
            }

            if (roll < 0.60f)
            {
                return PickupKind.Ammo;
            }

            return PickupKind.Health;
        }

        private void SpawnPickupOnPad(PickupPadSlot slot, PickupKind kind)
        {
            if (slot == null || slot.Occupant != null)
            {
                return;
            }

            switch (kind)
            {
                case PickupKind.Weapon:
                    slot.Occupant = CreateWeaponPickup(slot.Position);
                    break;
                case PickupKind.Ammo:
                    slot.Occupant = CreateAmmoPickup(slot.Position);
                    break;
                default:
                    slot.Occupant = CreateHealthPickupObject(slot.Position);
                    break;
            }
        }

        private GameObject CreateWeaponPickup(Vector3 position)
        {
            var pickup = new GameObject("Pulse Pistol Pickup");
            pickup.transform.SetParent(matchRoot.transform, false);
            pickup.transform.position = position;
            PickupVisuals.BuildGunPickup(pickup.transform, theme);

            var collider = pickup.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.9f;

            var pickupBehaviour = pickup.AddComponent<WeaponPickup>();
            pickupBehaviour.Configure(this, new WeaponDefinition());
            activePickups.Add(pickupBehaviour);
            return pickup;
        }

        private GameObject CreateAmmoPickup(Vector3 position)
        {
            var ammo = new GameObject("Ammo Pickup");
            ammo.transform.SetParent(matchRoot.transform, false);
            ammo.transform.position = position;
            PickupVisuals.BuildAmmoPickup(ammo.transform, theme);
            var collider = ammo.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.75f;
            var pickup = ammo.AddComponent<AmmoPickup>();
            pickup.Configure(this, 18);
            activeAmmoPickups.Add(pickup);
            return ammo;
        }

        private GameObject CreateHealthPickupObject(Vector3 position)
        {
            var pickup = new GameObject("Health Drop");
            pickup.transform.SetParent(matchRoot.transform, false);
            pickup.transform.position = position;
            PickupVisuals.BuildHealthPickup(pickup.transform, theme);

            var collider = pickup.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.85f;

            var healthPickup = pickup.AddComponent<HealthPickup>();
            healthPickup.Configure(this, 35f);
            activeHealthPickups.Add(healthPickup);
            return pickup;
        }

        public void NotifyPickupPadEmptied(GameObject pickupObject)
        {
            foreach (var slot in pickupPadSlots)
            {
                if (slot.Occupant == pickupObject)
                {
                    slot.Occupant = null;
                    return;
                }
            }
        }

        private void CreateHealingStation(Vector3 position)
        {
            var station = new GameObject("Health Pad Room Station");
            station.transform.SetParent(matchRoot.transform, false);
            station.transform.position = position;

            if (!HealingStationAsset.TryBuild(station.transform, theme))
            {
                var baseObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                baseObject.name = "Health Pad";
                baseObject.transform.SetParent(station.transform, false);
                baseObject.transform.localPosition = Vector3.zero;
                baseObject.transform.localScale = new Vector3(1.6f, 0.08f, 1.6f);

                if (baseObject.TryGetComponent<Renderer>(out var renderer))
                {
                    renderer.sharedMaterial = theme.Health;
                }

                if (baseObject.TryGetComponent<Collider>(out var baseCollider))
                {
                    Destroy(baseCollider);
                }
            }

            var trigger = station.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2.15f;

            var platformCollider = station.AddComponent<BoxCollider>();
            platformCollider.center = new Vector3(0f, 0.055f, 0f);
            platformCollider.size = new Vector3(2.35f, 0.22f, 2.35f);

            var light = station.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.color = new Color(1f, 0.08f, 0.05f);
            light.range = 6f;
            light.intensity = 2.8f;

            var stationBehaviour = station.AddComponent<HealingStation>();
            stationBehaviour.Configure(55f, 14f, theme.Health, theme.Floor);
            healingStations.Add(stationBehaviour);
        }

        private void AddLighting(ArenaLayout layout)
        {
            var ambient = new GameObject("Arena Ambient Light");
            ambient.transform.SetParent(matchRoot.transform, false);
            ambient.transform.position = Vector3.up * 7f;
            var light = ambient.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            light.color = new Color(0.36f, 0.32f, 0.62f);
            light.intensity = 0.22f;
            ambient.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            foreach (var room in layout.RoomCenters.Values)
            {
                var roomLightObject = new GameObject("Arena Room Neon Light");
                roomLightObject.transform.SetParent(matchRoot.transform, false);
                roomLightObject.transform.position = room + new Vector3(0f, wallHeight - 0.3f, 0f);
                var roomLight = roomLightObject.AddComponent<Light>();
                roomLight.type = LightType.Point;
                roomLight.shadows = LightShadows.None;
                roomLight.color = Random.value > 0.35f ? new Color(1f, 0.05f, 0.85f) : new Color(0.58f, 0.12f, 1f);
                roomLight.range = roomSize * 0.95f;
                roomLight.intensity = 0.9f;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.001f, 0.001f, 0.003f);
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
        }

        private void CreateHud(GameObject player, GameObject opponent)
        {
            var hudObject = new GameObject("Prototype HUD");
            hud = hudObject.AddComponent<PrototypeHud>();
            var playerCamera = player.GetComponentInChildren<Camera>();
            hud.Build(playerCamera);
            hud.Bind(playerCombatant, player.GetComponent<WeaponInventory>(), opponentCombatant, player.transform, opponent != null ? opponent.transform : null);
            hud.BindWaveState(this);
            hud.SetCenterMessage("");
            hud.SetWaveCountdown("");
        }

        private void OnDroidDied(CombatantHealth combatant)
        {
            if (combatant == null)
            {
                return;
            }

            combatant.Died -= OnDroidDied;
            activeDroids.Remove(combatant);
            allOutWarAiSoldiers.Remove(combatant);
            if (currentGameMode == ArenaGameMode.AllOutWar)
            {
                var deathPosition = combatant.transform.position;
                var attacker = combatant.LastAttacker;
                ReportAllOutWarSquadSignal(combatant, AllOutWarSquadSignal.AllyKilled, deathPosition, attacker);
                if (attacker != null && CombatantTeam.AreEnemies(combatant, attacker))
                {
                    ReportAllOutWarSquadSignal(attacker, AllOutWarSquadSignal.EnemyKilled, deathPosition, combatant);
                }

                if (CombatantTeam.TryGetTeam(combatant, out var teamId) &&
                    allOutWarActiveByTeam != null &&
                    teamId >= 0 &&
                    teamId < allOutWarActiveByTeam.Length)
                {
                    allOutWarActiveByTeam[teamId] = Mathf.Max(0, allOutWarActiveByTeam[teamId] - 1);
                    allOutWarNextSpawnAt[teamId] = Time.time + Random.Range(1.5f, 3.5f);
                }

                UnregisterAllOutWarSquadMember(combatant);
                CrumpleDroidBody(combatant.gameObject);
                StartCoroutine(RemoveDroidBody(combatant.gameObject));
                EvaluateAllOutWarVictory();
                return;
            }

            CrumpleDroidBody(combatant.gameObject);
            CreateScrapPickup(GetGroundedDropPosition(combatant.transform.position), baseScrapDrop + currentWave * 2);
            StartCoroutine(RemoveDroidBody(combatant.gameObject));
        }

        private void OnCompanionDied(CombatantHealth combatant)
        {
            if (combatant == null)
            {
                return;
            }

            combatant.Died -= OnCompanionDied;
            activeCompanions.Remove(combatant.gameObject);
            armyUpgradeStates.Remove(combatant.gameObject);

            CrumpleDroidBody(combatant.gameObject);
            StartCoroutine(RemoveDroidBody(combatant.gameObject));
        }

        private Vector3 GetGroundedDropPosition(Vector3 origin)
        {
            var start = origin + Vector3.up * 0.8f;
            if (Physics.Raycast(start, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point + Vector3.up * 0.52f;
            }

            return origin + Vector3.up * 0.35f;
        }

        private void CreateScrapPickup(Vector3 position, int amount)
        {
            var pickup = new GameObject("Scrap Drop");
            pickup.transform.SetParent(matchRoot.transform, false);
            pickup.transform.position = position;

            var trigger = pickup.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.82f;

            pickup.AddComponent<ScrapPickup>().Configure(this, theme, amount);
        }

        private IEnumerator RemoveDroidBody(GameObject droid)
        {
            if (droid == null)
            {
                yield break;
            }

            yield return new WaitForSeconds(8f);
            Destroy(droid);
        }

        private void CrumpleDroidBody(GameObject droid)
        {
            if (droid == null)
            {
                return;
            }

            var controller = droid.GetComponent<DroidController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            var walker = droid.GetComponent<SpawnIntroWalker>();
            if (walker != null)
            {
                walker.enabled = false;
            }

            var visualAnimator = droid.GetComponent<CombatantVisualWalkAnimator>();
            if (visualAnimator != null)
            {
                visualAnimator.enabled = false;
            }

            var blasterRig = droid.GetComponent<DroidBlasterRig>();
            if (blasterRig != null)
            {
                blasterRig.enabled = false;
            }

            var character = droid.GetComponent<CharacterController>();
            if (character != null)
            {
                character.enabled = false;
            }

            foreach (var collider in droid.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            var crumple = droid.GetComponent<DroidDeathCrumple>();
            if (crumple == null)
            {
                crumple = droid.AddComponent<DroidDeathCrumple>();
            }

            crumple.Begin();
        }

        private void OnPlayerDied(CombatantHealth combatant)
        {
            if (currentGameMode == ArenaGameMode.AllOutWar)
            {
                if (!playerRespawning)
                {
                    StartCoroutine(RespawnAllOutWarPlayer());
                }

                EvaluateAllOutWarVictory();
                return;
            }

            IsMatchActive = false;
            hud?.SetCenterMessage($"DEFEATED\n{RestartPrompt()}");
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private IEnumerator RespawnAllOutWarPlayer()
        {
            playerRespawning = true;
            InputSuppressedUntil = Time.time + 2.25f;
            hud?.SetCenterMessage("RESPAWNING");
            yield return new WaitForSeconds(1.35f);

            if (playerObject != null && currentLayout != null)
            {
                var region = currentLayout.ArmySpawnRegions.Count > 0 ? currentLayout.ArmySpawnRegions[0] : null;
                var spawn = region != null ? region.GetSpawnPosition(Random.Range(0, 6)) : currentLayout.PlayerSpawn;
                var rotation = region != null ? region.Rotation : currentLayout.PlayerRotation;
                var controller = playerObject.GetComponent<CharacterController>();
                if (controller != null)
                {
                    controller.enabled = false;
                }

                playerObject.transform.position = spawn;
                playerObject.transform.rotation = rotation;
                if (controller != null)
                {
                    controller.enabled = true;
                }

                playerCombatant.Configure("Player", playerHealth);
                playerCombatant.ConfigureShieldRecharge(5.2f, playerHealth / 3.4f);
                playerCombatant.AddShield(playerHealth, playerHealth);
                AssignTeam(playerObject, 0);
                EquipAllOutWarPlayerWeapon();
            }

            hud?.SetCenterMessage("");
            InputSuppressedUntil = Time.time + 0.35f;
            playerRespawning = false;
            EvaluateAllOutWarVictory();
        }

        private void OnOpponentDied(CombatantHealth combatant)
        {
            IsMatchActive = false;
            hud?.SetCenterMessage($"VICTORY\nThe crowd goes silent\n{RestartPrompt()}");
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static string RestartPrompt()
        {
            return Gamepad.current != null ? "Press Start to regenerate the arena" : "Press R to regenerate the arena";
        }
    }
}
