using System.Collections.Generic;
using UnityEngine;

namespace ArenaShooter
{
    internal enum AllOutWarMapStyle
    {
        RandomlyGenerate,
        Hilly
    }

    internal static class AllOutWarMapStyleNames
    {
        public const string RandomlyGenerate = "Randomly Generate";
        public const string Hilly = "Hilly";

        public static AllOutWarMapStyle Parse(string value)
        {
            return value == Hilly ? AllOutWarMapStyle.Hilly : AllOutWarMapStyle.RandomlyGenerate;
        }

        public static string ToDisplayName(AllOutWarMapStyle style)
        {
            return style == AllOutWarMapStyle.Hilly ? Hilly : RandomlyGenerate;
        }
    }

    public sealed class ArenaGenerator : MonoBehaviour
    {
        private ArenaTheme activeTheme;
        private ArenaTheme wallBaseAccentTheme;
        private Material wallBaseAccentMaterial;
        private ArenaTheme allOutWarHexFloorTheme;
        private Material allOutWarHexFloorMaterial;
        private AllOutWarTerrainProfile activeAllOutWarTerrainProfile;
        private bool generatingAllOutWar;
        private const float DefaultCorridorFloorLengthPadding = 0.4f;
        private const float AllOutWarCorridorFloorLengthPadding = 1.2f;
        private const float AllOutWarTunnelSubfloorMinDepth = 5.75f;
        private const float AllOutWarHillCutRoofClearance = 0.85f;
        private const float AllOutWarHillCutTunnelChance = 0.72f;
        private const float AllOutWarHillCutMouthFloorAccessHeight = 0.42f;
        private const float AllOutWarTunnelSpawnExtraPadding = 8.5f;
        private const float AllOutWarTunnelTraceRingSpacing = 2.75f;
        private const float AllOutWarTunnelTraceRingThickness = 0.055f;
        private const float AllOutWarTunnelTracePortalMargin = 1.35f;
        private const int AllOutWarTunnelRouteAttempts = 260;
        private const int AllOutWarHillyMinimumTerrainOnlyHills = 6;

        private sealed class AllOutWarPhaseTimer
        {
            private readonly string label;
            private readonly System.Diagnostics.Stopwatch total = System.Diagnostics.Stopwatch.StartNew();
            private readonly System.Diagnostics.Stopwatch phase = System.Diagnostics.Stopwatch.StartNew();

            public AllOutWarPhaseTimer(string label)
            {
                this.label = label;
            }

            public void Mark(string phaseName)
            {
                Debug.Log($"[Arena Shooter] {label} - {phaseName}: {phase.ElapsedMilliseconds} ms ({total.ElapsedMilliseconds} ms total).");
                phase.Restart();
            }
        }

        private static readonly int HexFloorBaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int HexFloorLineColorId = Shader.PropertyToID("_LineColor");
        private static readonly int HexFloorHexSizeId = Shader.PropertyToID("_HexSize");
        private static readonly int HexFloorLineWidthId = Shader.PropertyToID("_LineWidth");
        private static readonly int HexFloorEmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
        private static readonly int HexFloorPulseSpeedId = Shader.PropertyToID("_PulseSpeed");
        private static readonly int HexFloorPulseStrengthId = Shader.PropertyToID("_PulseStrength");
        private static readonly int HexFloorPatternOriginId = Shader.PropertyToID("_PatternOrigin");
        private static readonly int HexFloorNormalFadeStartId = Shader.PropertyToID("_NormalFadeStart");
        private static readonly int HexFloorNormalFadeEndId = Shader.PropertyToID("_NormalFadeEnd");
        private static readonly int HexFloorWaveDirectionId = Shader.PropertyToID("_WaveDirection");
        private static readonly int HexFloorWaveSpeedId = Shader.PropertyToID("_WaveSpeed");
        private static readonly int HexFloorWavePeriodId = Shader.PropertyToID("_WavePeriod");
        private static readonly int HexFloorWaveTravelSpanId = Shader.PropertyToID("_WaveTravelSpan");
        private static readonly int HexFloorWaveRestartGapId = Shader.PropertyToID("_WaveRestartGap");
        private static readonly int HexFloorWaveWidthId = Shader.PropertyToID("_WaveWidth");
        private static readonly int HexFloorWaveSoftnessId = Shader.PropertyToID("_WaveSoftness");
        private static readonly int HexFloorIdleLineStrengthId = Shader.PropertyToID("_IdleLineStrength");
        private static readonly int HexFloorWaveLineStrengthId = Shader.PropertyToID("_WaveLineStrength");
        private static readonly int HexFloorHillIdleLineStrengthId = Shader.PropertyToID("_HillIdleLineStrength");
        private static readonly int HexFloorHillIdleHeightStartId = Shader.PropertyToID("_HillIdleHeightStart");
        private static readonly int HexFloorHillIdleHeightEndId = Shader.PropertyToID("_HillIdleHeightEnd");

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        private readonly struct HillCutEndpointCandidate
        {
            public readonly Vector2Int Room;
            public readonly Vector2Int Direction;
            public readonly Vector3 Portal;
            public readonly int HillIndex;
            public readonly float Score;

            public HillCutEndpointCandidate(Vector2Int room, Vector2Int direction, Vector3 portal, int hillIndex, float score)
            {
                Room = room;
                Direction = direction;
                Portal = portal;
                HillIndex = hillIndex;
                Score = score;
            }

            public Vector3 WorldDirection => ToWorldDirection(Direction);
        }

        private enum SubfloorEndpointSource
        {
            WallPortal
        }

        private readonly struct SubfloorEndpointCandidate
        {
            public readonly Vector2Int Room;
            public readonly Vector2Int Direction;
            public readonly Vector3 Portal;
            public readonly float Score;
            public readonly SubfloorEndpointSource Source;

            public SubfloorEndpointCandidate(Vector2Int room, Vector2Int direction, Vector3 portal, float score, SubfloorEndpointSource source)
            {
                Room = room;
                Direction = direction;
                Portal = portal;
                Score = score;
                Source = source;
            }

            public Vector3 WorldDirection => ToWorldDirection(Direction);
        }

        private readonly struct SubfloorPortalFootprint
        {
            public readonly Vector3 Portal;
            public readonly Vector3 Direction;
            public readonly Vector3 ThresholdBack;
            public readonly Vector3 RampEnd;
            public readonly float CutoutHalfWidth;
            public readonly float VisualHalfWidth;
            public readonly float VisualMargin;
            public readonly float ThresholdDepth;
            public readonly float RampLength;

            public SubfloorPortalFootprint(Vector3 portal, Vector3 direction, float tunnelWidth, float rampLength)
            {
                Portal = portal;
                Direction = Flatten(direction).sqrMagnitude > 0.001f ? Flatten(direction).normalized : Vector3.forward;
                ThresholdDepth = GetAllOutWarSubfloorThresholdDepth(tunnelWidth);
                RampLength = Mathf.Max(0f, rampLength);
                CutoutHalfWidth = tunnelWidth * 0.76f;
                VisualMargin = Mathf.Max(0.35f, tunnelWidth * 0.16f);
                VisualHalfWidth = CutoutHalfWidth + VisualMargin;
                ThresholdBack = Portal - Direction * ThresholdDepth;
                RampEnd = Portal + Direction * RampLength;
            }

            public Vector3 Side => new Vector3(-Direction.z, 0f, Direction.x);
        }

        public static int EstimateAllOutWarTerrainGridBonus(int seed, float spacing, string mapStyle = AllOutWarMapStyleNames.RandomlyGenerate)
        {
            return AllOutWarTerrainProfile.EstimateGridBonus(seed, spacing, AllOutWarMapStyleNames.Parse(mapStyle));
        }

        public ArenaLayout Generate(ArenaTheme theme, Transform root, int seed, int targetRooms, int gridRadius, float roomSize, float corridorLength, float corridorWidth, float wallHeight, int pickupCount, int gateCount)
        {
            activeTheme = theme;
            generatingAllOutWar = false;
            var random = new System.Random(seed);
            var layout = BuildRoomGraph(random, targetRooms, gridRadius, roomSize + corridorLength);
            AddGateSpawns(layout, random, gateCount, roomSize, corridorLength);
            AddSpawnsAndPickups(layout, random, pickupCount);

            foreach (var room in layout.Rooms)
            {
                CreateRoom(theme, root, layout, room, roomSize, corridorWidth, wallHeight);
            }

            foreach (var room in layout.Rooms)
            {
                foreach (var direction in new[] { Vector2Int.up, Vector2Int.right })
                {
                    var neighbor = room + direction;
                    if (layout.Rooms.Contains(neighbor))
                    {
                        CreateCorridor(theme, root, layout.RoomCenters[room], layout.RoomCenters[neighbor], direction, roomSize, corridorLength, corridorWidth, wallHeight);
                    }
                }
            }

            foreach (var gate in GetActiveSpawnGates(layout))
            {
                CreateGate(theme, root, gate, roomSize, corridorLength, corridorWidth, wallHeight);
            }

            return layout;
        }

        public ArenaLayout GenerateAllOutWar(ArenaTheme theme, Transform root, int seed, int totalArmies, int targetRooms, int gridRadius, float roomSize, float corridorLength, float corridorWidth, float wallHeight, int pickupCount, string mapStyle = AllOutWarMapStyleNames.RandomlyGenerate)
        {
            var timer = new AllOutWarPhaseTimer("All Out War arena generation");
            activeTheme = theme;
            generatingAllOutWar = true;
            var random = new System.Random(seed);
            var spacing = roomSize + corridorLength;
            var resolvedMapStyle = AllOutWarMapStyleNames.Parse(mapStyle);
            var terrainProfile = new AllOutWarTerrainProfile(seed, spacing, resolvedMapStyle);
            activeAllOutWarTerrainProfile = terrainProfile;
            var layout = BuildCircularRoomGraph(random, Mathf.Max(3, totalArmies), targetRooms, gridRadius, roomSize, spacing, terrainProfile);
            timer.Mark("layout/terrain reservation");
            AddAllOutWarTunnelRoutes(layout, random, terrainProfile, resolvedMapStyle, gridRadius, roomSize, corridorLength, corridorWidth);
            timer.Mark("tunnel route selection");
            ConfigureAllOutWarHexFloorMaterial(theme, corridorWidth, layout);
            terrainProfile.BuildFlatMasks(layout, roomSize, corridorLength, corridorWidth);
            timer.Mark("flat mask build");

            foreach (var room in layout.Rooms)
            {
                CreateRoom(theme, root, layout, room, roomSize, corridorWidth, wallHeight);
            }

            foreach (var room in layout.Rooms)
            {
                foreach (var direction in new[] { Vector2Int.up, Vector2Int.right })
                {
                    var neighbor = room + direction;
                    if (layout.Rooms.Contains(neighbor))
                    {
                        CreateCorridor(theme, root, layout, room, neighbor, direction, roomSize, corridorLength, corridorWidth, wallHeight);
                    }
                }
            }
            timer.Mark("room/wall geometry");

            CreateAllOutWarDomeFloor(theme, root, layout.CircularCenter, layout.DomeRadius, terrainProfile);
            timer.Mark("terrain render/collider mesh generation");
            CreateAllOutWarTunnels(theme, root, layout, roomSize, corridorLength, corridorWidth, wallHeight);
            timer.Mark("tunnel mesh generation");
            CreateInvisibleCircularBoundary(root, layout.CircularCenter, layout.DomeRadius, wallHeight);
            timer.Mark("boundary geometry");
            generatingAllOutWar = false;
            activeAllOutWarTerrainProfile = null;
            return layout;
        }

        private ArenaLayout BuildCircularRoomGraph(System.Random random, int totalArmies, int targetRooms, int gridRadius, float roomSize, float spacing, AllOutWarTerrainProfile terrainProfile)
        {
            var layout = new ArenaLayout
            {
                CellSpacing = spacing,
                CircularCenter = Vector3.zero
            };

            var allowed = new HashSet<Vector2Int>();
            var radiusSqr = gridRadius * gridRadius + 0.35f;
            for (var x = -gridRadius; x <= gridRadius; x++)
            {
                for (var y = -gridRadius; y <= gridRadius; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (x * x + y * y <= radiusSqr)
                    {
                        allowed.Add(cell);
                    }
                }
            }

            var spawnProtectedRooms = BuildArmySpawnProtectedRooms(allowed, totalArmies, gridRadius);
            terrainProfile?.BuildTerrainOnlyHillReservations(allowed, spawnProtectedRooms, totalArmies, gridRadius, spacing, roomSize);
            BuildAllOutWarTerrainAwareRoomSet(layout, allowed, spawnProtectedRooms, random, totalArmies, targetRooms, gridRadius, terrainProfile);
            AddCircularClearingRooms(layout, layout.Rooms, spawnProtectedRooms, random, totalArmies, targetRooms, gridRadius, terrainProfile);

            foreach (var room in layout.Rooms)
            {
                layout.RoomCenters[room] = new Vector3(room.x * spacing, 0f, room.y * spacing);
            }

            ConfigureAllOutWarBounds(layout, gridRadius, spacing);
            AddArmySpawnRegions(layout, totalArmies, gridRadius, spacing);
            return layout;
        }

        private static void BuildAllOutWarTerrainAwareRoomSet(
            ArenaLayout layout,
            HashSet<Vector2Int> allowed,
            HashSet<Vector2Int> spawnProtectedRooms,
            System.Random random,
            int totalArmies,
            int targetRooms,
            int gridRadius,
            AllOutWarTerrainProfile terrainProfile)
        {
            var centralHub = FindAllOutWarConnectivityHub(allowed, terrainProfile);
            AddAllOutWarCenterRooms(layout.Rooms, allowed, terrainProfile, centralHub);

            foreach (var room in spawnProtectedRooms)
            {
                layout.Rooms.Add(room);
            }

            for (var team = 0; team < totalArmies; team++)
            {
                var frontRoom = FindNearestAllowedCell(allowed, GetArmySpawnFrontTarget(team, totalArmies, gridRadius), cell => !IsAllOutWarNoBuildCell(terrainProfile, cell));
                CarvePathToCenter(layout, allowed, frontRoom, terrainProfile, centralHub);
            }

            var buildableRooms = new List<Vector2Int>();
            foreach (var cell in allowed)
            {
                if (layout.Rooms.Contains(cell) || IsAllOutWarNoBuildCell(terrainProfile, cell))
                {
                    continue;
                }

                buildableRooms.Add(cell);
            }

            ShuffleRoomList(buildableRooms, random);
            foreach (var room in buildableRooms)
            {
                layout.Rooms.Add(room);
            }

            var minimumRooms = CalculateAllOutWarMinimumRoomCount(CountAllOutWarBuildableRoomCells(allowed, terrainProfile), totalArmies, targetRooms);
            KeepRoomsConnectedToHub(layout.Rooms, centralHub);
            if (layout.Rooms.Count < minimumRooms && terrainProfile != null)
            {
                GrowConnectedRoomSet(
                    layout.Rooms,
                    allowed,
                    minimumRooms,
                    candidate => !IsAllOutWarNoBuildCell(terrainProfile, candidate));
            }

            if (layout.Rooms.Count < minimumRooms)
            {
                GrowConnectedRoomSet(layout.Rooms, allowed, minimumRooms, candidate => !IsAllOutWarNoBuildCell(terrainProfile, candidate));
            }
        }

        private static int CalculateAllOutWarMinimumRoomCount(int allowedRoomCount, int totalArmies, int targetRooms)
        {
            return Mathf.Min(allowedRoomCount, Mathf.Max(totalArmies * 5 + 12, Mathf.CeilToInt(targetRooms * 0.58f)));
        }

        private static int CountAllOutWarBuildableRoomCells(HashSet<Vector2Int> allowed, AllOutWarTerrainProfile terrainProfile)
        {
            var count = 0;
            foreach (var cell in allowed)
            {
                if (!IsAllOutWarNoBuildCell(terrainProfile, cell))
                {
                    count++;
                }
            }

            return count;
        }

        private static void KeepRoomsConnectedToCenter(HashSet<Vector2Int> rooms)
        {
            KeepRoomsConnectedToHub(rooms, Vector2Int.zero);
        }

        private static void KeepRoomsConnectedToHub(HashSet<Vector2Int> rooms, Vector2Int hub)
        {
            if (!rooms.Contains(hub))
            {
                return;
            }

            var connected = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            connected.Add(hub);
            queue.Enqueue(hub);

            while (queue.Count > 0)
            {
                var room = queue.Dequeue();
                foreach (var direction in Directions)
                {
                    var neighbor = room + direction;
                    if (rooms.Contains(neighbor) && connected.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            var disconnected = new List<Vector2Int>();
            foreach (var room in rooms)
            {
                if (!connected.Contains(room))
                {
                    disconnected.Add(room);
                }
            }

            foreach (var room in disconnected)
            {
                rooms.Remove(room);
            }
        }

        private static void GrowConnectedRoomSet(
            HashSet<Vector2Int> rooms,
            HashSet<Vector2Int> allowed,
            int targetCount,
            System.Func<Vector2Int, bool> candidateFilter)
        {
            var guard = 0;
            while (rooms.Count < targetCount && guard++ < allowed.Count)
            {
                var candidates = new HashSet<Vector2Int>();
                foreach (var room in rooms)
                {
                    foreach (var direction in Directions)
                    {
                        var candidate = room + direction;
                        if (allowed.Contains(candidate) &&
                            !rooms.Contains(candidate) &&
                            (candidateFilter == null || candidateFilter(candidate)))
                        {
                            candidates.Add(candidate);
                        }
                    }
                }

                if (candidates.Count == 0)
                {
                    return;
                }

                var best = Vector2Int.zero;
                var bestScore = float.PositiveInfinity;
                foreach (var candidate in candidates)
                {
                    var score = candidate.sqrMagnitude * 0.001f;
                    if (score < bestScore)
                    {
                        best = candidate;
                        bestScore = score;
                    }
                }

                rooms.Add(best);
            }
        }

        private static void AddForcedCenterRooms(HashSet<Vector2Int> rooms, HashSet<Vector2Int> allowed)
        {
            var centerRooms = new[]
            {
                Vector2Int.zero,
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left
            };

            foreach (var room in centerRooms)
            {
                if (allowed.Contains(room))
                {
                    rooms.Add(room);
                }
            }
        }

        private static void AddAllOutWarCenterRooms(HashSet<Vector2Int> rooms, HashSet<Vector2Int> allowed, AllOutWarTerrainProfile terrainProfile, Vector2Int centralHub)
        {
            if (terrainProfile == null)
            {
                AddForcedCenterRooms(rooms, allowed);
                return;
            }

            var centerRooms = new[]
            {
                Vector2Int.zero,
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left
            };

            foreach (var room in centerRooms)
            {
                if (allowed.Contains(room) && !IsAllOutWarNoBuildCell(terrainProfile, room))
                {
                    rooms.Add(room);
                }
            }

            if (allowed.Contains(centralHub) && !IsAllOutWarNoBuildCell(terrainProfile, centralHub))
            {
                rooms.Add(centralHub);
            }
        }

        private static Vector2Int FindAllOutWarConnectivityHub(HashSet<Vector2Int> allowed, AllOutWarTerrainProfile terrainProfile)
        {
            if (terrainProfile == null || allowed == null || allowed.Count == 0)
            {
                return Vector2Int.zero;
            }

            return FindNearestAllowedCell(allowed, Vector2.zero, cell => !IsAllOutWarNoBuildCell(terrainProfile, cell));
        }

        private static void ShuffleRoomList(List<Vector2Int> rooms, System.Random random)
        {
            for (var i = 0; i < rooms.Count; i++)
            {
                var swapIndex = random.Next(i, rooms.Count);
                (rooms[i], rooms[swapIndex]) = (rooms[swapIndex], rooms[i]);
            }
        }

        private static void ConfigureAllOutWarBounds(ArenaLayout layout, int gridRadius, float spacing)
        {
            var playableRadius = gridRadius * spacing + Mathf.Max(4f, spacing * 0.35f);
            layout.CircularCenter = Vector3.zero;
            layout.CircularRadius = playableRadius;
            layout.DomeRadius = playableRadius;
            layout.PerimeterSpawnRadius = Mathf.Max(spacing, playableRadius - 2.2f);
        }

        private void AddCircularClearingRooms(ArenaLayout layout, HashSet<Vector2Int> roomPool, HashSet<Vector2Int> spawnProtectedRooms, System.Random random, int totalArmies, int targetRooms, int gridRadius, AllOutWarTerrainProfile terrainProfile)
        {
            var clearingCount = Mathf.Clamp(
                Mathf.CeilToInt(gridRadius * 0.42f) + Mathf.Max(0, targetRooms - 54) / 50 + Mathf.Max(0, totalArmies - 3) / 4,
                2,
                Mathf.Clamp(gridRadius, 2, 7));
            var candidates = new List<Vector2Int>();
            foreach (var room in roomPool)
            {
                if (!IsAllOutWarNoBuildCell(terrainProfile, room))
                {
                    candidates.Add(room);
                }
            }

            for (var clearing = 0; clearing < clearingCount && candidates.Count > 0; clearing++)
            {
                var center = candidates[random.Next(candidates.Count)];
                var radius = ChooseAllOutWarClearingRadius(random, gridRadius, totalArmies);
                if (center == Vector2Int.zero ||
                    center.sqrMagnitude < 2 ||
                    !TryBuildClearingFootprint(center, radius, roomPool, spawnProtectedRooms, terrainProfile, layout.CellSpacing, out var footprint))
                {
                    clearing--;
                    candidates.Remove(center);
                    continue;
                }

                var groupId = layout.ClearingCenters.Count;
                foreach (var room in footprint)
                {
                    layout.ClearingRoomGroups[room] = groupId;
                }

                layout.ClearingCenters.Add(new Vector3(center.x * layout.CellSpacing, 0f, center.y * layout.CellSpacing));

                candidates.Remove(center);
            }
        }

        private static int ChooseAllOutWarClearingRadius(System.Random random, int gridRadius, int totalArmies)
        {
            var mapLimit = Mathf.Max(1, Mathf.Min(3, gridRadius - 3));
            var maxClearingRadius = Mathf.Clamp(1 + gridRadius / 4 + totalArmies / 7, 1, mapLimit);
            var radius = 1;
            while (radius < maxClearingRadius && random.NextDouble() > 0.68)
            {
                radius++;
            }

            return radius;
        }

        private static bool TryBuildClearingFootprint(
            Vector2Int center,
            int radius,
            HashSet<Vector2Int> allowed,
            HashSet<Vector2Int> protectedRooms,
            AllOutWarTerrainProfile terrainProfile,
            float spacing,
            out List<Vector2Int> footprint)
        {
            footprint = new List<Vector2Int>();
            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    if (x * x + y * y > radius * radius + 1)
                    {
                        continue;
                    }

                    var room = center + new Vector2Int(x, y);
                    if (!allowed.Contains(room))
                    {
                        footprint.Clear();
                        return false;
                    }

                    if (IsAllOutWarNoBuildCell(terrainProfile, room))
                    {
                        footprint.Clear();
                        return false;
                    }

                    if (protectedRooms != null && protectedRooms.Contains(room))
                    {
                        footprint.Clear();
                        return false;
                    }

                    footprint.Add(room);
                }
            }

            return footprint.Count > 0 && (terrainProfile == null || terrainProfile.IsClearingFootprintBuildable(footprint));
        }

        private static HashSet<Vector2Int> BuildArmySpawnProtectedRooms(HashSet<Vector2Int> allowed, int totalArmies, int gridRadius)
        {
            var protectedRooms = new HashSet<Vector2Int>();
            for (var team = 0; team < totalArmies; team++)
            {
                var angle = Mathf.PI * 2f * team / Mathf.Max(1, totalArmies);
                var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var tangent = new Vector2(-outward.y, outward.x);
                var frontRoom = FindNearestAllowedCell(allowed, GetArmySpawnFrontTarget(team, totalArmies, gridRadius));
                protectedRooms.Add(frontRoom);

                for (var radial = -1; radial <= 1; radial++)
                {
                    for (var lateral = -1; lateral <= 1; lateral++)
                    {
                        var sample = new Vector2(frontRoom.x, frontRoom.y) + outward * radial + tangent * lateral;
                        var room = FindNearestAllowedCell(allowed, sample);
                        protectedRooms.Add(room);
                    }
                }
            }

            return protectedRooms;
        }

        private static Vector2 GetArmySpawnFrontTarget(int team, int totalArmies, int gridRadius)
        {
            var angle = Mathf.PI * 2f * team / Mathf.Max(1, totalArmies);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * GetArmySpawnFrontTargetRadius(gridRadius);
        }

        private static float GetArmySpawnFrontTargetRadius(int gridRadius)
        {
            return Mathf.Max(1f, gridRadius - 0.20f);
        }

        private void AddArmySpawnRooms(ArenaLayout layout, HashSet<Vector2Int> allowed, int totalArmies, int gridRadius)
        {
            for (var team = 0; team < totalArmies; team++)
            {
                var room = FindNearestAllowedCell(allowed, GetArmySpawnFrontTarget(team, totalArmies, gridRadius));
                CarvePathToCenter(layout, allowed, room);
            }
        }

        private static Vector2Int FindNearestAllowedCell(HashSet<Vector2Int> allowed, Vector2 target)
        {
            return FindNearestAllowedCell(allowed, target, null);
        }

        private static Vector2Int FindNearestAllowedCell(HashSet<Vector2Int> allowed, Vector2 target, System.Func<Vector2Int, bool> cellFilter)
        {
            var best = Vector2Int.zero;
            var bestDistance = float.PositiveInfinity;
            foreach (var cell in allowed)
            {
                if (cellFilter != null && !cellFilter(cell))
                {
                    continue;
                }

                var distance = (new Vector2(cell.x, cell.y) - target).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = cell;
                }
            }

            if (float.IsPositiveInfinity(bestDistance) && cellFilter != null)
            {
                return FindNearestAllowedCell(allowed, target);
            }

            return best;
        }

        private static void CarvePathToCenter(ArenaLayout layout, HashSet<Vector2Int> allowed, Vector2Int start)
        {
            CarvePathToCenter(layout, allowed, start, null, Vector2Int.zero);
        }

        private static void CarvePathToCenter(ArenaLayout layout, HashSet<Vector2Int> allowed, Vector2Int start, AllOutWarTerrainProfile terrainProfile)
        {
            CarvePathToCenter(layout, allowed, start, terrainProfile, FindAllOutWarConnectivityHub(allowed, terrainProfile));
        }

        private static void CarvePathToCenter(ArenaLayout layout, HashSet<Vector2Int> allowed, Vector2Int start, AllOutWarTerrainProfile terrainProfile, Vector2Int centralHub)
        {
            if (terrainProfile != null && TryFindPathAroundNoBuild(allowed, start, centralHub, terrainProfile, out var path))
            {
                foreach (var room in path)
                {
                    layout.Rooms.Add(room);
                }

                return;
            }

            if (terrainProfile != null)
            {
                return;
            }

            var current = start;
            var guard = 0;
            while (guard++ < 64 && allowed.Contains(current))
            {
                layout.Rooms.Add(current);
                if (current == centralHub)
                {
                    return;
                }

                var next = current;
                if (Mathf.Abs(next.x) >= Mathf.Abs(next.y) && next.x != 0)
                {
                    next.x += next.x > 0 ? -1 : 1;
                }
                else if (next.y != 0)
                {
                    next.y += next.y > 0 ? -1 : 1;
                }
                else
                {
                    next.x += next.x > 0 ? -1 : 1;
                }

                current = next;
            }
        }

        private static bool TryFindPathAroundNoBuild(
            HashSet<Vector2Int> allowed,
            Vector2Int start,
            Vector2Int goal,
            AllOutWarTerrainProfile terrainProfile,
            out List<Vector2Int> path)
        {
            path = new List<Vector2Int>();
            if (!allowed.Contains(start) ||
                !allowed.Contains(goal) ||
                IsAllOutWarNoBuildCell(terrainProfile, start) ||
                IsAllOutWarNoBuildCell(terrainProfile, goal))
            {
                return false;
            }

            var parents = new Dictionary<Vector2Int, Vector2Int>();
            var visited = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == goal)
                {
                    var room = current;
                    path.Add(room);
                    while (room != start)
                    {
                        room = parents[room];
                        path.Add(room);
                    }

                    path.Reverse();
                    return true;
                }

                foreach (var direction in Directions)
                {
                    var neighbor = current + direction;
                    if (!allowed.Contains(neighbor) ||
                        visited.Contains(neighbor) ||
                        IsAllOutWarNoBuildCell(terrainProfile, neighbor))
                    {
                        continue;
                    }

                    parents[neighbor] = current;
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            return false;
        }

        private static bool IsAllOutWarNoBuildCell(AllOutWarTerrainProfile terrainProfile, Vector2Int cell)
        {
            return terrainProfile != null && terrainProfile.IsTerrainOnlyHillNoBuildCell(cell);
        }

        private void AddArmySpawnRegions(ArenaLayout layout, int totalArmies, int gridRadius, float spacing)
        {
            for (var team = 0; team < totalArmies; team++)
            {
                var angle = Mathf.PI * 2f * team / totalArmies;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var target = GetArmySpawnFrontTarget(team, totalArmies, gridRadius);
                var room = FindNearestLayoutRoom(layout, target);
                var roomCenter = layout.RoomCenters.TryGetValue(room, out var center) ? center : new Vector3(room.x * spacing, 0f, room.y * spacing);
                var spawn = layout.CircularCenter + outward * layout.PerimeterSpawnRadius + Vector3.up * 1.1f;
                var rotation = Quaternion.LookRotation((-outward).sqrMagnitude > 0.01f ? -outward : Vector3.forward, Vector3.up);
                var perArmyArcLength = Mathf.PI * 2f * Mathf.Max(1f, layout.PerimeterSpawnRadius) / Mathf.Max(1, totalArmies);
                var arcHalfWidth = Mathf.Min(Mathf.Clamp(spacing * 0.55f, 6.5f, 12.5f), perArmyArcLength * 0.34f);
                var radialThickness = Mathf.Clamp(spacing * 0.55f, 7f, 12f);
                var clearancePadding = Mathf.Clamp(spacing * 0.12f, 1.6f, 2.6f);
                layout.ArmySpawnRegions.Add(new ArmySpawnRegion(
                    team,
                    room,
                    spawn,
                    rotation,
                    roomCenter + Vector3.up * 1.1f,
                    System.Array.Empty<Vector3>(),
                    layout.CircularCenter,
                    outward,
                    layout.PerimeterSpawnRadius,
                    arcHalfWidth,
                    radialThickness,
                    clearancePadding));
            }

            if (layout.ArmySpawnRegions.Count > 0)
            {
                layout.PlayerSpawn = layout.ArmySpawnRegions[0].Center;
                layout.PlayerRotation = layout.ArmySpawnRegions[0].Rotation;
            }
        }

        private void AddAllOutWarTunnelRoutes(
            ArenaLayout layout,
            System.Random random,
            AllOutWarTerrainProfile terrainProfile,
            AllOutWarMapStyle mapStyle,
            int gridRadius,
            float roomSize,
            float corridorLength,
            float corridorWidth)
        {
            if (layout == null || random == null)
            {
                return;
            }

            var targetCount = CalculateAllOutWarTunnelTargetCount(layout, gridRadius);
            if (targetCount <= 0)
            {
                return;
            }

            var wantsHillCut = mapStyle == AllOutWarMapStyle.Hilly || (terrainProfile != null && terrainProfile.HasTerrainOnlyHills);
            var hillCutAdded = false;
            if (wantsHillCut)
            {
                hillCutAdded = TryAddAllOutWarTunnelRoute(
                    layout,
                    random,
                    terrainProfile,
                    ArenaTunnelKind.HillCut,
                    roomSize,
                    corridorLength,
                    corridorWidth);
            }

            // A hill cut must not consume a small map's only tunnel slot, or underground
            // tunnels can never appear on hilly skirmish maps.
            var totalTarget = targetCount + (hillCutAdded && targetCount <= 1 ? 1 : 0);
            while (layout.TunnelRoutes.Count < totalTarget)
            {
                if (!TryAddAllOutWarTunnelRoute(
                        layout,
                        random,
                        terrainProfile,
                        ArenaTunnelKind.Subfloor,
                        roomSize,
                        corridorLength,
                        corridorWidth))
                {
                    break;
                }
            }
        }

        private static int CalculateAllOutWarTunnelTargetCount(ArenaLayout layout, int gridRadius)
        {
            // Small skirmish maps hold roughly 15-25 rooms; they still deserve a tunnel.
            if (layout == null || layout.Rooms.Count < 12)
            {
                return 0;
            }

            var roomBonus = layout.Rooms.Count / 54;
            var radiusBonus = gridRadius >= 7 ? 1 : 0;
            return Mathf.Clamp(1 + roomBonus + radiusBonus, 1, 3);
        }

        private bool TryAddAllOutWarTunnelRoute(
            ArenaLayout layout,
            System.Random random,
            AllOutWarTerrainProfile terrainProfile,
            ArenaTunnelKind kind,
            float roomSize,
            float corridorLength,
            float corridorWidth)
        {
            if (kind == ArenaTunnelKind.HillCut)
            {
                return TryAddAllOutWarHillCutTunnelRoute(layout, random, terrainProfile, roomSize, corridorLength, corridorWidth);
            }

            var tunnelWidth = Mathf.Clamp(corridorWidth * 1.28f, 4.2f, 5.8f);
            var tunnelHeight = Mathf.Clamp(tunnelWidth * 0.86f, 3.55f, 4.85f);
            var subfloorDepth = CalculateAllOutWarTunnelSubfloorDepth(tunnelHeight, tunnelWidth);
            var rampLength = CalculateAllOutWarTunnelSubfloorRampLength(tunnelWidth, subfloorDepth, roomSize);
            var portalRadius = Mathf.Max(tunnelWidth * 0.95f, roomSize * 0.34f);
            var candidates = BuildAllOutWarSubfloorEndpointCandidates(layout, terrainProfile, roomSize, corridorWidth, tunnelWidth, rampLength);
            if (candidates.Count < 2)
            {
                return false;
            }

            if (!TrySelectAllOutWarSubfloorTunnelRoute(
                    layout,
                    random,
                    candidates,
                    tunnelWidth,
                    tunnelHeight,
                    subfloorDepth,
                    rampLength,
                    portalRadius,
                    5,
                    0.45f,
                    out var bestRoute) &&
                !TrySelectAllOutWarSubfloorTunnelRoute(
                    layout,
                    random,
                    candidates,
                    tunnelWidth,
                    tunnelHeight,
                    subfloorDepth,
                    rampLength,
                    portalRadius,
                    3,
                    -0.2f,
                    out bestRoute))
            {
                return false;
            }

            layout.TunnelRoutes.Add(bestRoute);
            return true;
        }

        private static bool TrySelectAllOutWarSubfloorTunnelRoute(
            ArenaLayout layout,
            System.Random random,
            List<SubfloorEndpointCandidate> candidates,
            float tunnelWidth,
            float tunnelHeight,
            float subfloorDepth,
            float rampLength,
            float portalRadius,
            int minimumGraphDistance,
            float minimumEntranceAlignment,
            out ArenaTunnelRoute bestRoute)
        {
            bestRoute = null;
            if (layout == null || random == null || candidates == null || candidates.Count < 2)
            {
                return false;
            }

            var bestScore = float.NegativeInfinity;
            for (var fromIndex = 0; fromIndex < candidates.Count; fromIndex++)
            {
                var from = candidates[fromIndex];
                if (IsAllOutWarTunnelEndpointAlreadyUsed(layout, from.Room))
                {
                    continue;
                }

                for (var toIndex = 0; toIndex < candidates.Count; toIndex++)
                {
                    if (fromIndex == toIndex)
                    {
                        continue;
                    }

                    var to = candidates[toIndex];
                    if (from.Room == to.Room ||
                        IsAllOutWarTunnelEndpointAlreadyUsed(layout, to.Room))
                    {
                        continue;
                    }

                    var graphDistance = FindAllOutWarCardinalPathLength(layout, from.Room, to.Room);
                    if (graphDistance == int.MaxValue || graphDistance < minimumGraphDistance)
                    {
                        continue;
                    }

                    var routeDirection = Flatten(to.Portal - from.Portal);
                    if (routeDirection.sqrMagnitude <= 0.001f)
                    {
                        continue;
                    }

                    routeDirection.Normalize();
                    var entranceAlignment =
                        Vector3.Dot(from.WorldDirection, routeDirection) +
                        Vector3.Dot(to.WorldDirection, -routeDirection);
                    if (entranceAlignment < minimumEntranceAlignment)
                    {
                        continue;
                    }

                    var worldDistance = FlatDistance(from.Portal, to.Portal);
                    if (worldDistance < rampLength * 2f + 5f)
                    {
                        continue;
                    }

                    var route = new ArenaTunnelRoute(
                        ArenaTunnelKind.Subfloor,
                        from.Room,
                        to.Room,
                        from.Direction,
                        to.Direction,
                        from.Portal,
                        to.Portal,
                        tunnelWidth,
                        tunnelHeight,
                        subfloorDepth,
                        rampLength,
                        portalRadius,
                        ArenaTunnelEntranceMode.WallPortal,
                        ArenaTunnelEntranceMode.WallPortal);
                    if (!IsAllOutWarTunnelRouteSafe(layout, route, tunnelWidth))
                    {
                        continue;
                    }

                    var score = graphDistance * 9.5f +
                        worldDistance * 0.22f +
                        Mathf.Max(0f, entranceAlignment) * 2.7f +
                        (from.Score + to.Score) * 4.2f +
                        (float)random.NextDouble() * 1.75f;
                    if (score > bestScore)
                    {
                        bestRoute = route;
                        bestScore = score;
                    }
                }
            }

            return bestRoute != null;
        }

        private bool TryAddAllOutWarHillCutTunnelRoute(
            ArenaLayout layout,
            System.Random random,
            AllOutWarTerrainProfile terrainProfile,
            float roomSize,
            float corridorLength,
            float corridorWidth)
        {
            if (layout == null ||
                random == null ||
                terrainProfile == null ||
                !terrainProfile.HasTerrainOnlyHills ||
                random.NextDouble() > AllOutWarHillCutTunnelChance)
            {
                return false;
            }

            var tunnelWidth = Mathf.Clamp(corridorWidth * 1.28f, 4.2f, 5.8f);
            var tunnelHeight = Mathf.Clamp(tunnelWidth * 0.86f, 3.55f, 4.85f);
            var portalRadius = Mathf.Max(tunnelWidth * 0.95f, roomSize * 0.34f);
            var candidates = BuildAllOutWarHillCutEndpointCandidates(layout, terrainProfile, roomSize, tunnelWidth, tunnelHeight);
            if (candidates.Count < 2)
            {
                return false;
            }

            ArenaTunnelRoute bestRoute = null;
            var bestScore = float.NegativeInfinity;
            for (var attempt = 0; attempt < AllOutWarTunnelRouteAttempts; attempt++)
            {
                var from = candidates[random.Next(candidates.Count)];
                var to = candidates[random.Next(candidates.Count)];
                if (from.Room == to.Room ||
                    IsAllOutWarTunnelEndpointAlreadyUsed(layout, from.Room) ||
                    IsAllOutWarTunnelEndpointAlreadyUsed(layout, to.Room))
                {
                    continue;
                }

                if (Vector3.Dot(from.WorldDirection, to.WorldDirection) > -0.35f)
                {
                    continue;
                }

                var graphDistance = FindAllOutWarCardinalPathLength(layout, from.Room, to.Room);
                if (graphDistance == int.MaxValue || graphDistance < 3)
                {
                    continue;
                }

                var worldDistance = FlatDistance(from.Portal, to.Portal);
                if (worldDistance < roomSize * 1.9f ||
                    !terrainProfile.TryScoreTerrainOnlyHillCutRoute(
                        from.HillIndex,
                        to.HillIndex,
                        from.Portal,
                        to.Portal,
                        from.WorldDirection,
                        to.WorldDirection,
                        tunnelWidth,
                        tunnelHeight,
                        portalRadius,
                        out var hillScore))
                {
                    continue;
                }

                var route = new ArenaTunnelRoute(
                    ArenaTunnelKind.HillCut,
                    from.Room,
                    to.Room,
                    from.Direction,
                    to.Direction,
                    from.Portal,
                    to.Portal,
                    tunnelWidth,
                    tunnelHeight,
                    0f,
                    0f,
                    portalRadius,
                    from.HillIndex,
                    to.HillIndex);
                if (!IsAllOutWarTunnelRouteSafe(layout, route, tunnelWidth))
                {
                    continue;
                }

                var score = graphDistance * 8.25f +
                    worldDistance * 0.16f +
                    hillScore * 42f +
                    (from.Score + to.Score) * 4.5f +
                    (float)random.NextDouble() * 2.25f;
                if (score > bestScore)
                {
                    bestRoute = route;
                    bestScore = score;
                }
            }

            if (bestRoute == null)
            {
                return false;
            }

            layout.TunnelRoutes.Add(bestRoute);
            return true;
        }

        private static List<HillCutEndpointCandidate> BuildAllOutWarHillCutEndpointCandidates(
            ArenaLayout layout,
            AllOutWarTerrainProfile terrainProfile,
            float roomSize,
            float tunnelWidth,
            float tunnelHeight)
        {
            var candidates = new List<HillCutEndpointCandidate>();
            if (layout == null || terrainProfile == null)
            {
                return candidates;
            }

            var portalOffset = Mathf.Max(0.25f, roomSize * 0.5f - 0.2f);
            for (var hillIndex = 0; hillIndex < terrainProfile.TerrainOnlyHillRegionCount; hillIndex++)
            {
                foreach (var direction in Directions)
                {
                    var worldDirection = ToWorldDirection(direction);
                    if (!terrainProfile.TryBuildTerrainOnlyHillCutEndpoint(
                            hillIndex,
                            worldDirection,
                            tunnelWidth,
                            tunnelHeight,
                            portalOffset,
                            out var portal,
                            out var endpointScore) ||
                        !IsAllOutWarTunnelEndpointSafe(layout, portal, roomSize, tunnelWidth) ||
                        !IsAllOutWarHillCutMouthApronSafe(
                            layout,
                            terrainProfile,
                            hillIndex,
                            portal,
                            worldDirection,
                            tunnelWidth,
                            tunnelHeight) ||
                        layout.IsTunnelReservedPosition(portal, tunnelWidth * 1.15f) ||
                        !TryFindAllOutWarHillCutRoutingRoom(
                            layout,
                            portal,
                            worldDirection,
                            roomSize,
                            tunnelWidth,
                            out var room,
                            out var roomScore))
                    {
                        continue;
                    }

                    candidates.Add(new HillCutEndpointCandidate(room, direction, portal, hillIndex, endpointScore + roomScore));
                }
            }

            return candidates;
        }

        private static bool TryFindAllOutWarHillCutRoutingRoom(
            ArenaLayout layout,
            Vector3 portal,
            Vector3 inwardDirection,
            float roomSize,
            float tunnelWidth,
            out Vector2Int room,
            out float score)
        {
            room = default;
            score = 0f;
            if (layout == null || layout.RoomCenters.Count == 0)
            {
                return false;
            }

            inwardDirection = Flatten(inwardDirection);
            if (inwardDirection.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            inwardDirection.Normalize();
            var outwardDirection = -inwardDirection;
            var maxLinkDistance = Mathf.Max(roomSize * 2.8f, layout.CellSpacing * 2.4f);
            var bestScore = float.NegativeInfinity;
            var found = false;
            foreach (var candidate in layout.RoomCenters)
            {
                if (IsAllOutWarTunnelEndpointAlreadyUsed(layout, candidate.Key) ||
                    !IsAllOutWarTunnelEndpointSafe(layout, candidate.Value, roomSize, roomSize * 0.45f) ||
                    layout.IsTunnelReservedPosition(candidate.Value, roomSize * 0.65f))
                {
                    continue;
                }

                var toRoom = Flatten(candidate.Value - portal);
                var distance = toRoom.magnitude;
                if (distance <= 0.001f || distance > maxLinkDistance)
                {
                    continue;
                }

                var alignment = Vector3.Dot(toRoom / distance, outwardDirection);
                if (alignment < -0.1f ||
                    !IsAllOutWarTunnelSegmentClear(layout, candidate.Value, portal, tunnelWidth))
                {
                    continue;
                }

                var candidateScore = alignment * 2.4f + Mathf.InverseLerp(maxLinkDistance, roomSize * 0.75f, distance);
                if (!found || candidateScore > bestScore)
                {
                    room = candidate.Key;
                    score = candidateScore;
                    bestScore = candidateScore;
                    found = true;
                }
            }

            return found;
        }

        private static float CalculateAllOutWarTunnelSubfloorDepth(float tunnelHeight, float tunnelWidth)
        {
            return Mathf.Max(AllOutWarTunnelSubfloorMinDepth, tunnelHeight + Mathf.Max(1.55f, tunnelWidth * 0.3f));
        }

        private static float CalculateAllOutWarTunnelSubfloorRampLength(float tunnelWidth, float subfloorDepth, float roomSize)
        {
            var roomFitLimit = Mathf.Max(5.2f, roomSize * 0.62f);
            return Mathf.Clamp(Mathf.Max(subfloorDepth * 0.92f, tunnelWidth * 1.12f), 5.25f, roomFitLimit);
        }

        private static float GetAllOutWarHillCutRequiredTerrainHeight(float tunnelHeight)
        {
            return tunnelHeight + AllOutWarHillCutRoofClearance;
        }

        private static float GetAllOutWarHillCutMouthInnerDepth(float tunnelWidth)
        {
            return Mathf.Clamp(tunnelWidth * 0.72f, 2.25f, 4.25f);
        }

        private static float GetAllOutWarHillCutMouthInnerHalfWidth(float tunnelWidth)
        {
            return Mathf.Max(tunnelWidth * 0.68f, 2.65f);
        }

        private static float GetAllOutWarHillCutMouthOuterHalfWidth(float tunnelWidth)
        {
            return Mathf.Max(tunnelWidth * 0.54f, 2.15f);
        }

        private static float GetAllOutWarHillCutMaxMouthApronLength(float tunnelWidth)
        {
            return Mathf.Max(tunnelWidth * 5.2f, 20f);
        }

        private static float GetAllOutWarSubfloorRampCutoutHalfWidth(ArenaTunnelRoute tunnel)
        {
            return tunnel != null ? tunnel.Width * 0.76f : 0.1f;
        }

        private static float GetAllOutWarSubfloorThresholdRampOverlap()
        {
            return 0.28f;
        }

        private static float GetAllOutWarSubfloorThresholdDepth(float tunnelWidth)
        {
            return Mathf.Clamp(tunnelWidth * 0.34f, 1.15f, 1.75f);
        }

        private static List<SubfloorEndpointCandidate> BuildAllOutWarSubfloorEndpointCandidates(
            ArenaLayout layout,
            AllOutWarTerrainProfile terrainProfile,
            float roomSize,
            float corridorWidth,
            float tunnelWidth,
            float rampLength)
        {
            var candidates = new List<SubfloorEndpointCandidate>();
            if (layout == null || layout.RoomCenters.Count == 0)
            {
                return candidates;
            }

            var seen = new HashSet<string>();
            foreach (var room in layout.RoomCenters)
            {
                if (IsAllOutWarTunnelEndpointAlreadyUsed(layout, room.Key) ||
                    !IsAllOutWarTunnelEndpointSafe(layout, room.Value, roomSize, roomSize * 0.45f) ||
                    layout.IsTunnelReservedPosition(room.Value, roomSize * 0.65f))
                {
                    continue;
                }

                foreach (var direction in Directions)
                {
                    if (!IsAllOutWarSubfloorWallSideAvailable(layout, room.Key, direction, room.Value, roomSize, corridorWidth, tunnelWidth))
                    {
                        continue;
                    }

                    var worldDirection = ToWorldDirection(direction);
                    var portal = room.Value + worldDirection * (roomSize * 0.5f);
                    portal.y = 0f;
                    var footprint = BuildAllOutWarSubfloorPortalFootprint(portal, worldDirection, tunnelWidth, rampLength);
                    if (!IsAllOutWarSubfloorEntranceSafe(
                            layout,
                            terrainProfile,
                            footprint,
                            roomSize,
                            tunnelWidth))
                    {
                        continue;
                    }

                    AddAllOutWarSubfloorEndpointCandidate(
                        candidates,
                        seen,
                        room.Key,
                        direction,
                        portal,
                        0.75f,
                        SubfloorEndpointSource.WallPortal);
                }
            }

            return candidates;
        }

        private static bool IsAllOutWarSubfloorWallSideAvailable(
            ArenaLayout layout,
            Vector2Int room,
            Vector2Int direction,
            Vector3 center,
            float roomSize,
            float corridorWidth,
            float tunnelWidth)
        {
            if (layout == null ||
                layout.Rooms.Contains(room + direction) ||
                HasGate(layout, room, direction) ||
                IsAllOutWarDomeEdgeOpening(layout, room, direction))
            {
                return false;
            }

            var wallCenter = center + ToWorldDirection(direction) * (roomSize * 0.5f);
            var horizontal = direction == Vector2Int.up || direction == Vector2Int.down;
            var wallStart = wallCenter + (horizontal ? Vector3.left : Vector3.back) * (roomSize * 0.5f);
            var wallEnd = wallCenter + (horizontal ? Vector3.right : Vector3.forward) * (roomSize * 0.5f);
            var padding = Mathf.Max(corridorWidth * 0.5f, tunnelWidth * 0.6f);
            return IsAllOutWarTunnelPointInsideDome(layout, wallCenter, tunnelWidth) &&
                !IsInAllOutWarSpawnOpenZone(layout, wallCenter, padding) &&
                !IsSegmentInAllOutWarSpawnOpenZone(layout, wallStart, wallEnd, padding);
        }

        private static void AddAllOutWarSubfloorEndpointCandidate(
            List<SubfloorEndpointCandidate> candidates,
            HashSet<string> seen,
            Vector2Int room,
            Vector2Int direction,
            Vector3 portal,
            float score,
            SubfloorEndpointSource source)
        {
            if (seen == null || candidates == null)
            {
                return;
            }

            var key = room.x + ":" + room.y + ":" + direction.x + ":" + direction.y + ":" +
                Mathf.RoundToInt(portal.x * 10f) + ":" + Mathf.RoundToInt(portal.z * 10f);
            if (seen.Add(key))
            {
                candidates.Add(new SubfloorEndpointCandidate(room, direction, portal, score, source));
            }
        }

        private static bool IsAllOutWarSubfloorEntranceSafe(
            ArenaLayout layout,
            AllOutWarTerrainProfile terrainProfile,
            SubfloorPortalFootprint footprint,
            float roomSize,
            float tunnelWidth)
        {
            if (layout == null ||
                !IsAllOutWarTunnelEndpointSafe(layout, footprint.Portal, roomSize, tunnelWidth) ||
                layout.IsTunnelReservedPosition(footprint.Portal, tunnelWidth * 1.15f))
            {
                return false;
            }

            var spawnPadding = AllOutWarTunnelSpawnExtraPadding + tunnelWidth * 0.65f;
            var hillBoundaryPadding = Mathf.Max(tunnelWidth * 0.9f, roomSize * 0.24f);
            foreach (var point in GetAllOutWarSubfloorFootprintSamplePoints(footprint, 6, footprint.CutoutHalfWidth))
            {
                if (!IsAllOutWarTunnelPointInsideDome(layout, point, tunnelWidth) ||
                    IsInAllOutWarSpawnOpenZone(layout, point, spawnPadding) ||
                    layout.IsTunnelReservedPosition(point, tunnelWidth * 1.15f) ||
                    (terrainProfile != null && terrainProfile.IsNearTerrainOnlyHillBoundary(point, hillBoundaryPadding)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAllOutWarHillCutMouthApronSafe(
            ArenaLayout layout,
            AllOutWarTerrainProfile terrainProfile,
            int hillIndex,
            Vector3 portal,
            Vector3 inwardDirection,
            float tunnelWidth,
            float tunnelHeight)
        {
            if (layout == null ||
                terrainProfile == null ||
                !terrainProfile.TryBuildTerrainOnlyHillMouthApron(
                    hillIndex,
                    portal,
                    inwardDirection,
                    tunnelWidth,
                    tunnelHeight,
                    out var outerApproach,
                    out var innerMouth,
                    out var outerHalfWidth,
                    out var innerHalfWidth))
            {
                return false;
            }

            var direction = Flatten(inwardDirection);
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Flatten(innerMouth - outerApproach);
            }

            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            var side = new Vector3(-direction.z, 0f, direction.x);
            var spawnPadding = AllOutWarTunnelSpawnExtraPadding + tunnelWidth * 0.65f;
            const int samples = 6;
            for (var i = 0; i <= samples; i++)
            {
                var t = i / (float)samples;
                var center = Vector3.Lerp(outerApproach, innerMouth, t);
                var halfWidth = Mathf.Lerp(outerHalfWidth, innerHalfWidth, t);
                for (var sideIndex = -1; sideIndex <= 1; sideIndex++)
                {
                    var point = center + side * (halfWidth * sideIndex);
                    if (!IsAllOutWarTunnelPointInsideDome(layout, point, tunnelWidth) ||
                        IsInAllOutWarSpawnOpenZone(layout, point, spawnPadding) ||
                        layout.IsTunnelReservedPosition(point, tunnelWidth * 0.72f))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static SubfloorPortalFootprint BuildAllOutWarSubfloorPortalFootprint(Vector3 portal, Vector3 direction, float tunnelWidth, float rampLength)
        {
            portal.y = 0f;
            return new SubfloorPortalFootprint(portal, direction, tunnelWidth, rampLength);
        }

        private static SubfloorPortalFootprint BuildAllOutWarSubfloorPortalFootprint(ArenaTunnelRoute tunnel, bool fromEndpoint)
        {
            if (tunnel == null)
            {
                return new SubfloorPortalFootprint(Vector3.zero, Vector3.forward, 1f, 1f);
            }

            return BuildAllOutWarSubfloorPortalFootprint(
                fromEndpoint ? tunnel.FromPortal : tunnel.ToPortal,
                fromEndpoint ? tunnel.FromWorldDirection : tunnel.ToWorldDirectionValue,
                tunnel.Width,
                tunnel.RampLength);
        }

        private static IEnumerable<Vector3> GetAllOutWarSubfloorFootprintSamplePoints(SubfloorPortalFootprint footprint, int samples, float halfWidth)
        {
            var safeSamples = Mathf.Max(1, samples);
            var side = footprint.Side;
            for (var i = 0; i <= safeSamples; i++)
            {
                var point = Vector3.Lerp(footprint.ThresholdBack, footprint.RampEnd, i / (float)safeSamples);
                yield return point;
                yield return point + side * halfWidth;
                yield return point - side * halfWidth;
            }
        }

        private static bool IsAllOutWarTunnelEndpointAlreadyUsed(ArenaLayout layout, Vector2Int room)
        {
            if (layout == null)
            {
                return false;
            }

            foreach (var tunnel in layout.TunnelRoutes)
            {
                if (tunnel != null && (tunnel.FromRoom == room || tunnel.ToRoom == room))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAllOutWarTunnelEndpointSafe(ArenaLayout layout, Vector3 position, float roomSize, float tunnelWidth)
        {
            if (layout == null)
            {
                return false;
            }

            var toCenter = Flatten(position - layout.CircularCenter);
            if (layout.DomeRadius > 0f && toCenter.magnitude > layout.DomeRadius - Mathf.Max(roomSize * 0.42f, tunnelWidth))
            {
                return false;
            }

            var padding = AllOutWarTunnelSpawnExtraPadding + Mathf.Max(roomSize * 0.18f, tunnelWidth * 0.55f);
            return !IsInAllOutWarSpawnOpenZone(layout, position, padding);
        }

        private static bool IsAllOutWarTunnelSegmentClear(ArenaLayout layout, Vector3 start, Vector3 end, float tunnelWidth)
        {
            const int samples = 6;
            var padding = tunnelWidth * 0.58f;
            for (var i = 0; i <= samples; i++)
            {
                var t = i / (float)samples;
                var point = Vector3.Lerp(start, end, t);
                if (IsInAllOutWarSpawnOpenZone(layout, point, padding))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAllOutWarTunnelRouteSafe(ArenaLayout layout, ArenaTunnelRoute route, float tunnelWidth)
        {
            if (layout == null || route == null || route.Waypoints.Count < 2)
            {
                return false;
            }

            const int samples = 8;
            var padding = AllOutWarTunnelSpawnExtraPadding + tunnelWidth * 0.65f;
            for (var pointIndex = 1; pointIndex < route.Waypoints.Count; pointIndex++)
            {
                var start = route.Waypoints[pointIndex - 1];
                var end = route.Waypoints[pointIndex];
                for (var i = 0; i <= samples; i++)
                {
                    var t = i / (float)samples;
                    var point = Vector3.Lerp(start, end, t);
                    if (!IsAllOutWarTunnelPointInsideDome(layout, point, tunnelWidth) ||
                        IsInAllOutWarSpawnOpenZone(layout, point, padding) ||
                        layout.IsTunnelReservedPosition(point, tunnelWidth * 1.15f))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsAllOutWarTunnelPointInsideDome(ArenaLayout layout, Vector3 point, float tunnelWidth)
        {
            if (layout == null)
            {
                return false;
            }

            if (layout.DomeRadius <= 0f)
            {
                return true;
            }

            var domePadding = Mathf.Max(0.75f, tunnelWidth * 0.68f);
            var toCenter = Flatten(point - layout.CircularCenter);
            return toCenter.magnitude <= layout.DomeRadius - domePadding;
        }

        private static int FindAllOutWarCardinalPathLength(ArenaLayout layout, Vector2Int start, Vector2Int goal)
        {
            if (layout == null || !layout.Rooms.Contains(start) || !layout.Rooms.Contains(goal))
            {
                return int.MaxValue;
            }

            var queue = new Queue<Vector2Int>();
            var distance = new Dictionary<Vector2Int, int>();
            queue.Enqueue(start);
            distance[start] = 0;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == goal)
                {
                    return distance[current];
                }

                foreach (var direction in Directions)
                {
                    var next = current + direction;
                    if (!layout.Rooms.Contains(next) || distance.ContainsKey(next))
                    {
                        continue;
                    }

                    distance[next] = distance[current] + 1;
                    queue.Enqueue(next);
                }
            }

            return int.MaxValue;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        private static Vector2Int FindNearestLayoutRoom(ArenaLayout layout, Vector2 target)
        {
            var best = Vector2Int.zero;
            var bestDistance = float.PositiveInfinity;
            foreach (var room in layout.Rooms)
            {
                var distance = (new Vector2(room.x, room.y) - target).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = room;
                }
            }

            return best;
        }

        private void CreateCircularPerimeter(ArenaTheme theme, Transform root, Vector3 center, float radius, float wallHeight)
        {
            var segmentCount = Mathf.Clamp(Mathf.CeilToInt(radius * 0.7f), 28, 72);
            var arcLength = (Mathf.PI * 2f * radius) / segmentCount;
            for (var i = 0; i < segmentCount; i++)
            {
                var angle = Mathf.PI * 2f * i / segmentCount;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var tangentAngle = -angle * Mathf.Rad2Deg;
                var rotation = Quaternion.Euler(0f, tangentAngle, 0f);
                CreateRawCube(
                    "Arena Wall",
                    root,
                    center + outward * radius + Vector3.up * (wallHeight * 0.5f),
                    new Vector3(arcLength * 1.08f, wallHeight, 0.65f),
                    theme.Wall,
                    rotation: rotation);
            }
        }

        private void CreateInvisibleCircularBoundary(Transform root, Vector3 center, float radius, float wallHeight)
        {
            var segmentCount = Mathf.Clamp(Mathf.CeilToInt(radius * 0.9f), 72, 160);
            var arcLength = (Mathf.PI * 2f * radius) / segmentCount;
            var height = Mathf.Max(wallHeight + 7f, 10f);
            const float baseY = -0.85f;
            for (var i = 0; i < segmentCount; i++)
            {
                var angle = Mathf.PI * 2f * i / segmentCount;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var boundary = new GameObject("All Out War Bubble Boundary");
                boundary.transform.SetParent(root, false);
                boundary.transform.position = center + outward * radius + Vector3.up * (baseY + height * 0.5f);
                boundary.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);
                var collider = boundary.AddComponent<BoxCollider>();
                collider.size = new Vector3(arcLength * 1.22f, height, 2.25f);
            }
        }

        private void CreateAllOutWarDomeFloor(ArenaTheme theme, Transform root, Vector3 center, float radius, AllOutWarTerrainProfile terrainProfile)
        {
            var floor = new GameObject("All Out War Continuous Dome Floor");
            floor.transform.SetParent(root, false);

            var terrainDisk = CreateTerrainDiskMeshData(center, radius, terrainProfile);
            var mesh = CreateTerrainDiskMesh("All Out War Continuous Dome Floor Mesh", terrainDisk, Vector3.down * 0.003f);
            var meshFilter = floor.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var renderer = floor.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetFloorMaterial(theme);
            DroidRenderSetup.ApplyRenderer(renderer, StylizedOutlineCategory.None);

            var collider = floor.AddComponent<MeshCollider>();
            collider.sharedMesh = CreateTerrainDiskMesh("All Out War Continuous Dome Floor Collider Mesh", terrainDisk, Vector3.zero);
        }

        private readonly struct TerrainDiskMeshData
        {
            public readonly Vector3[] Vertices;
            public readonly int[] Triangles;

            public TerrainDiskMeshData(Vector3[] vertices, List<int> triangles)
            {
                Vertices = vertices;
                Triangles = triangles.ToArray();
            }
        }

        private static TerrainDiskMeshData CreateTerrainDiskMeshData(Vector3 center, float radius, AllOutWarTerrainProfile terrainProfile)
        {
            var hillyTerrain = terrainProfile != null && terrainProfile.UsesHillyProfile;
            var ringSpacing = hillyTerrain ? 1.1f : 1.75f;
            var ringCount = Mathf.Clamp(Mathf.CeilToInt(radius / ringSpacing), hillyTerrain ? 64 : 40, hillyTerrain ? 160 : 104);
            var segmentCount = Mathf.Clamp(Mathf.CeilToInt(radius * (hillyTerrain ? 3.1f : 2.15f)), hillyTerrain ? 224 : 160, hillyTerrain ? 560 : 384);
            var vertices = new Vector3[ringCount * segmentCount + 1];
            var triangles = new List<int>(segmentCount * 3 + Mathf.Max(0, ringCount - 1) * segmentCount * 6);
            vertices[0] = AddTerrainHeight(center, terrainProfile);

            for (var ring = 1; ring <= ringCount; ring++)
            {
                var ringRadius = radius * ring / ringCount;
                var ringStart = 1 + (ring - 1) * segmentCount;
                for (var segment = 0; segment < segmentCount; segment++)
                {
                    var angle = Mathf.PI * 2f * segment / segmentCount;
                    var position = center + new Vector3(Mathf.Cos(angle) * ringRadius, 0f, Mathf.Sin(angle) * ringRadius);
                    vertices[ringStart + segment] = AddTerrainHeight(position, terrainProfile);
                }
            }

            for (var segment = 0; segment < segmentCount; segment++)
            {
                var next = (segment + 1) % segmentCount;
                AddTerrainTriangle(triangles, vertices, terrainProfile, 0, 1 + next, 1 + segment);
            }

            for (var ring = 2; ring <= ringCount; ring++)
            {
                var innerStart = 1 + (ring - 2) * segmentCount;
                var outerStart = 1 + (ring - 1) * segmentCount;
                for (var segment = 0; segment < segmentCount; segment++)
                {
                    var next = (segment + 1) % segmentCount;
                    var innerCurrent = innerStart + segment;
                    var innerNext = innerStart + next;
                    var outerCurrent = outerStart + segment;
                    var outerNext = outerStart + next;

                    AddTerrainTriangle(triangles, vertices, terrainProfile, innerCurrent, innerNext, outerCurrent);
                    AddTerrainTriangle(triangles, vertices, terrainProfile, innerNext, outerNext, outerCurrent);
                }
            }

            return new TerrainDiskMeshData(vertices, triangles);
        }

        private static Mesh CreateTerrainDiskMesh(string meshName, TerrainDiskMeshData data, Vector3 vertexOffset)
        {
            var vertices = data.Vertices;
            if (vertexOffset != Vector3.zero)
            {
                vertices = new Vector3[data.Vertices.Length];
                for (var i = 0; i < data.Vertices.Length; i++)
                {
                    vertices[i] = data.Vertices[i] + vertexOffset;
                }
            }

            var mesh = new Mesh { name = meshName };
            mesh.vertices = vertices;
            mesh.SetTriangles(data.Triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddTerrainTriangle(List<int> triangles, Vector3[] vertices, AllOutWarTerrainProfile terrainProfile, int a, int b, int c)
        {
            if (terrainProfile != null)
            {
                var center = (vertices[a] + vertices[b] + vertices[c]) / 3f;
                if (terrainProfile.IsTerrainCutout(center))
                {
                    return;
                }
            }

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        private static Vector3 AddTerrainHeight(Vector3 position, AllOutWarTerrainProfile terrainProfile)
        {
            if (terrainProfile != null)
            {
                position.y += terrainProfile.SampleHeight(position);
            }

            return position;
        }

        private static float GetAllOutWarCorridorFloorLength(float corridorLength)
        {
            return corridorLength + AllOutWarCorridorFloorLengthPadding;
        }

        private static float GetCorridorFloorLength(float corridorLength, bool allOutWar)
        {
            return corridorLength + (allOutWar ? AllOutWarCorridorFloorLengthPadding : DefaultCorridorFloorLengthPadding);
        }

        private sealed class AllOutWarTerrainProfile
        {
            private const float FlatPatchWeightEpsilon = 0.0001f;
            private const int CorridorPatchPriority = 8;
            private const int RoomPatchPriority = 12;
            private const int StructurePatchPriority = 26;
            private const int TunnelPatchPriority = 34;
            private const int SpawnPatchPriority = 30;
            private const int ClearingPatchPriority = 40;
            private const float TacticalHillMinimumHeight = 4.5f;
            private const float TacticalHillMaximumHeight = 7.25f;
            private const float TacticalHillSignalThreshold = 0.52f;
            private const int TacticalHillMinimumPlateauRooms = 3;
            private const int TacticalHillMinimumSupportRooms = 7;
            private const float TacticalHillShoulderSlopeDegrees = 24f;
            private const float TacticalHillCrestEdgeHeightScale = 0.92f;

            private enum TerrainOnlyHillSizeClass
            {
                Compact,
                Medium,
                Large
            }

            private readonly List<FlatPatch> flatPatches = new();
            private readonly Dictionary<Vector2Int, List<int>> flatPatchSpatialIndex = new();
            private readonly List<TerrainCutout> terrainCutouts = new();
            private readonly List<TerrainOnlyHillRegion> terrainOnlyHillRegions = new();
            private readonly HashSet<Vector2Int> terrainOnlyHillNoBuildCells = new();
            private readonly HashSet<Vector2Int> terrainOnlyHillHardCells = new();
            private readonly Vector2 tacticalHillOffset;
            private readonly float cellSpacing;
            private readonly float tacticalHillScale;
            private readonly float tacticalHillPeakHeight;
            private readonly AllOutWarMapStyle mapStyle;

            public AllOutWarTerrainProfile(int seed, float spacing, AllOutWarMapStyle mapStyle)
            {
                this.mapStyle = mapStyle;
                cellSpacing = spacing;
                var settings = CreateSettings(seed, spacing, mapStyle);
                tacticalHillOffset = settings.TacticalHillOffset;
                tacticalHillScale = settings.TacticalHillScale;
                tacticalHillPeakHeight = settings.TacticalHillPeakHeight;
            }

            public static int EstimateGridBonus(int seed, float spacing, AllOutWarMapStyle mapStyle)
            {
                if (mapStyle == AllOutWarMapStyle.Hilly)
                {
                    return 2;
                }

                var settings = CreateSettings(seed, spacing, mapStyle);
                return settings.TacticalHillPeakHeight >= 5.7f && settings.TacticalHillScale >= Mathf.Max(88f, spacing * 7.6f) ? 1 : 0;
            }

            public bool IsTerrainOnlyHillNoBuildCell(Vector2Int cell)
            {
                return terrainOnlyHillNoBuildCells.Contains(cell);
            }

            public bool UsesHillyProfile => mapStyle == AllOutWarMapStyle.Hilly || terrainOnlyHillRegions.Count > 0;
            public bool HasTerrainOnlyHills => terrainOnlyHillRegions.Count > 0;
            public int TerrainOnlyHillRegionCount => terrainOnlyHillRegions.Count;

            private int reservationGridRadius = 8;

            public void BuildTerrainOnlyHillReservations(
                HashSet<Vector2Int> allowed,
                HashSet<Vector2Int> spawnProtectedRooms,
                int totalArmies,
                int gridRadius,
                float spacing,
                float roomSize)
            {
                terrainOnlyHillRegions.Clear();
                terrainOnlyHillNoBuildCells.Clear();
                terrainOnlyHillHardCells.Clear();
                reservationGridRadius = gridRadius;
                // Small battlefield grids hold far fewer cells, so the room-count minimums
                // scale down with them; hills themselves shrink to match (see
                // CreateTerrainOnlyHillRegion) so the hilly style keeps its guaranteed hills.
                var minimumSupportRooms = GetScaledTacticalHillMinimumSupportRooms(gridRadius);
                if (allowed == null || allowed.Count < minimumSupportRooms + 8)
                {
                    return;
                }

                var protectedCells = BuildTerrainOnlyHillProtectedCells(allowed, spawnProtectedRooms, totalArmies, gridRadius);
                var eligibleCells = new HashSet<Vector2Int>();
                foreach (var cell in allowed)
                {
                    if (!protectedCells.Contains(cell))
                    {
                        eligibleCells.Add(cell);
                    }
                }

                if (eligibleCells.Count < minimumSupportRooms)
                {
                    return;
                }

                var candidates = new List<Vector2Int>(eligibleCells);
                candidates.Sort((a, b) => GetTacticalHillCandidateScore(b, spacing).CompareTo(GetTacticalHillCandidateScore(a, spacing)));

                var hilly = mapStyle == AllOutWarMapStyle.Hilly;
                var signalThreshold = hilly ? 0.34f : TacticalHillSignalThreshold;
                var targetHillCount = hilly
                    ? Mathf.Clamp(AllOutWarHillyMinimumTerrainOnlyHills + eligibleCells.Count / 78, AllOutWarHillyMinimumTerrainOnlyHills, 8)
                    : Mathf.Clamp(eligibleCells.Count / 78, 0, 1);
                var acceptedHills = 0;

                for (var i = 0; i < candidates.Count && acceptedHills < targetHillCount; i++)
                {
                    if (TryReserveTerrainOnlyHillCandidate(
                        allowed,
                        eligibleCells,
                        protectedCells,
                        candidates[i],
                        totalArmies,
                        gridRadius,
                        signalThreshold,
                        roomSize,
                        false))
                    {
                        acceptedHills++;
                    }
                }

                if (hilly && acceptedHills < targetHillCount)
                {
                    for (var i = 0; i < candidates.Count && acceptedHills < targetHillCount; i++)
                    {
                        if (TryReserveTerrainOnlyHillCandidate(
                            allowed,
                            eligibleCells,
                            protectedCells,
                            candidates[i],
                            totalArmies,
                            gridRadius,
                            signalThreshold,
                            roomSize,
                            true))
                        {
                            acceptedHills++;
                        }
                    }
                }
            }

            public void BuildFlatMasks(ArenaLayout layout, float roomSize, float corridorLength, float corridorWidth)
            {
                flatPatches.Clear();
                flatPatchSpatialIndex.Clear();
                terrainCutouts.Clear();
                if (layout == null)
                {
                    return;
                }

                var clearingFlatRooms = BuildClearingFlatProtectedRooms(layout);
                foreach (var room in layout.Rooms)
                {
                    if (!layout.RoomCenters.TryGetValue(room, out var center))
                    {
                        continue;
                    }

                    center.y = 0f;
                    layout.RoomCenters[room] = center;
                }

                var roomFalloff = Mathf.Max(2.0f, roomSize * 0.24f);
                foreach (var center in layout.RoomCenters.Values)
                {
                    AddFlatRect(ToXZ(center), roomSize * 0.58f, roomSize * 0.58f, roomFalloff, 0f, RoomPatchPriority);
                }

                AddClearingFlatProtectedMasks(layout, clearingFlatRooms, roomSize, corridorLength, roomFalloff);

                foreach (var room in layout.Rooms)
                {
                    foreach (var direction in new[] { Vector2Int.up, Vector2Int.right })
                    {
                        var neighbor = room + direction;
                        if (!layout.Rooms.Contains(neighbor) ||
                            !layout.RoomCenters.TryGetValue(room, out var from) ||
                            !layout.RoomCenters.TryGetValue(neighbor, out var to))
                        {
                            continue;
                        }

                        var corridorCenter = (from + to) * 0.5f;
                        var openClearingLink = layout.AreRoomsInSameClearing(room, neighbor);
                        var openSpawnZoneLink = IsInAllOutWarSpawnOpenZone(layout, corridorCenter, corridorWidth * 0.5f) ||
                            IsSegmentInAllOutWarSpawnOpenZone(layout, from, to, corridorWidth * 0.5f);
                        var openFloorLink = openClearingLink || openSpawnZoneLink;
                        var center = ToXZ(corridorCenter);
                        var floorHalfWidth = openFloorLink
                            ? roomSize * 0.54f
                            : corridorWidth * 0.5f + 0.35f;
                        var floorHalfLength = ArenaGenerator.GetAllOutWarCorridorFloorLength(corridorLength) * 0.5f;
                        var targetHeight = (from.y + to.y) * 0.5f;
                        if (direction == Vector2Int.up)
                        {
                            AddFlatRect(center, floorHalfWidth, floorHalfLength, roomFalloff, targetHeight, CorridorPatchPriority);
                        }
                        else
                        {
                            AddFlatRect(center, floorHalfLength, floorHalfWidth, roomFalloff, targetHeight, CorridorPatchPriority);
                        }
                    }
                }

                AddTunnelFlatMasks(layout, roomSize, corridorLength, corridorWidth);
                AddStructureBaseFlatMasks(layout, roomSize, corridorLength, corridorWidth, Mathf.Max(1.25f, roomSize * 0.15f));

                foreach (var region in layout.ArmySpawnRegions)
                {
                    if (region == null)
                    {
                        continue;
                    }

                    var spawnRadius = Mathf.Max(region.ArcHalfWidth, region.RadialThickness) + region.ClearancePadding + 1.4f;
                    AddFlatCircle(ToXZ(region.Center), spawnRadius, Mathf.Max(2.4f, roomSize * 0.24f), 0f, SpawnPatchPriority);
                    AddFlatCircle(ToXZ(region.EntryTarget), Mathf.Max(roomSize * 0.5f, 5.5f), Mathf.Max(2.0f, roomSize * 0.20f), 0f, SpawnPatchPriority);
                }

                RebuildFlatPatchSpatialIndex();
            }

            private void AddTunnelFlatMasks(ArenaLayout layout, float roomSize, float corridorLength, float corridorWidth)
            {
                if (layout == null || layout.TunnelRoutes.Count == 0)
                {
                    return;
                }

                var portalFalloff = Mathf.Max(1.8f, roomSize * 0.22f);
                var pathFalloff = Mathf.Max(1.55f, corridorLength * 0.28f);
                foreach (var tunnel in layout.TunnelRoutes)
                {
                    if (tunnel == null)
                    {
                        continue;
                    }

                    if (tunnel.Kind == ArenaTunnelKind.HillCut)
                    {
                        AddHillCutMouthCutout(tunnel, tunnel.FromHillRegionIndex, tunnel.FromPortal, tunnel.FromWorldDirection);
                        AddHillCutMouthCutout(tunnel, tunnel.ToHillRegionIndex, tunnel.ToPortal, tunnel.ToWorldDirectionValue);
                        continue;
                    }

                    var padRadius = Mathf.Max(tunnel.PortalRadius, corridorWidth * 0.9f);
                    AddFlatCircle(ToXZ(tunnel.FromPortal), padRadius, portalFalloff, 0f, TunnelPatchPriority);
                    AddFlatCircle(ToXZ(tunnel.ToPortal), padRadius, portalFalloff, 0f, TunnelPatchPriority);

                    var cutoutHalfWidth = GetAllOutWarSubfloorRampCutoutHalfWidth(tunnel);
                    AddTunnelFlatSegments(tunnel, cutoutHalfWidth + 0.45f, pathFalloff, true);
                    var fromFootprint = ArenaGenerator.BuildAllOutWarSubfloorPortalFootprint(tunnel, true);
                    var toFootprint = ArenaGenerator.BuildAllOutWarSubfloorPortalFootprint(tunnel, false);
                    AddTerrainCutoutRamp(ToXZ(fromFootprint.Portal), ToXZ(fromFootprint.RampEnd), fromFootprint.CutoutHalfWidth);
                    AddTerrainCutoutRamp(ToXZ(toFootprint.Portal), ToXZ(toFootprint.RampEnd), toFootprint.CutoutHalfWidth);
                }
            }

            private void AddTunnelFlatSegments(ArenaTunnelRoute tunnel, float halfWidth, float falloff, bool rampsOnly)
            {
                if (tunnel == null || tunnel.Waypoints.Count < 2)
                {
                    return;
                }

                for (var i = 1; i < tunnel.Waypoints.Count; i++)
                {
                    if (rampsOnly && i != 1 && i != tunnel.Waypoints.Count - 1)
                    {
                        continue;
                    }

                    AddFlatSegment(ToXZ(tunnel.Waypoints[i - 1]), ToXZ(tunnel.Waypoints[i]), halfWidth, falloff, 0f, TunnelPatchPriority);
                }
            }

            private void AddHillCutMouthCutout(ArenaTunnelRoute tunnel, int hillRegionIndex, Vector3 portal, Vector3 inwardDirection)
            {
                if (tunnel == null ||
                    !TryBuildTerrainOnlyHillMouthApron(
                        hillRegionIndex,
                        portal,
                        inwardDirection,
                        tunnel.Width,
                        tunnel.Height,
                        out var outerApproach,
                        out var innerMouth,
                        out var outerHalfWidth,
                        out var innerHalfWidth))
                {
                    return;
                }

                AddTerrainCutoutHillMouth(
                    ToXZ(outerApproach),
                    ToXZ(innerMouth),
                    outerHalfWidth,
                    innerHalfWidth);
            }

            private void AddStructureBaseFlatMasks(ArenaLayout layout, float roomSize, float corridorLength, float corridorWidth, float falloff)
            {
                if (layout == null)
                {
                    return;
                }

                const float sourceWallThickness = 0.35f;
                var structuralThickness = sourceWallThickness * 1.35f;
                var wallHalfDepth = structuralThickness * 0.5f + Mathf.Max(0.72f, roomSize * 0.065f);
                var wallEndPadding = Mathf.Max(0.35f, structuralThickness);

                foreach (var room in layout.Rooms)
                {
                    if (!layout.RoomCenters.TryGetValue(room, out var center))
                    {
                        continue;
                    }

                    foreach (var direction in Directions)
                    {
                        AddRoomWallBaseFlatMasks(layout, room, direction, center, roomSize, corridorWidth, structuralThickness, wallHalfDepth, wallEndPadding, falloff);
                    }
                }

                foreach (var room in layout.Rooms)
                {
                    foreach (var direction in new[] { Vector2Int.up, Vector2Int.right })
                    {
                        var neighbor = room + direction;
                        if (!layout.Rooms.Contains(neighbor) ||
                            !layout.RoomCenters.TryGetValue(room, out var from) ||
                            !layout.RoomCenters.TryGetValue(neighbor, out var to))
                        {
                            continue;
                        }

                        AddCorridorWallBaseFlatMasks(layout, room, neighbor, direction, from, to, roomSize, corridorLength, corridorWidth, structuralThickness, wallHalfDepth, falloff);
                    }
                }
            }

            private void AddRoomWallBaseFlatMasks(
                ArenaLayout layout,
                Vector2Int room,
                Vector2Int direction,
                Vector3 center,
                float roomSize,
                float doorwayWidth,
                float structuralThickness,
                float wallHalfDepth,
                float wallEndPadding,
                float falloff)
            {
                var half = roomSize * 0.5f;
                var offset = direction == Vector2Int.up
                    ? new Vector3(0f, 0f, half)
                    : direction == Vector2Int.down
                        ? new Vector3(0f, 0f, -half)
                        : direction == Vector2Int.right
                            ? new Vector3(half, 0f, 0f)
                            : new Vector3(-half, 0f, 0f);
                var wallCenter = center + offset;
                var horizontal = direction == Vector2Int.up || direction == Vector2Int.down;
                var wallStart = wallCenter + (horizontal ? Vector3.left : Vector3.back) * half;
                var wallEnd = wallCenter + (horizontal ? Vector3.right : Vector3.forward) * half;
                if (IsInAllOutWarSpawnOpenZone(layout, wallCenter, structuralThickness) ||
                    IsSegmentInAllOutWarSpawnOpenZone(layout, wallStart, wallEnd, structuralThickness + 0.8f))
                {
                    return;
                }

                var neighbor = room + direction;
                var hasDoor = layout.Rooms.Contains(neighbor);
                if (hasDoor && layout.AreRoomsInSameClearing(room, neighbor))
                {
                    return;
                }

                var hasTunnelOpening = ArenaGenerator.TryGetAllOutWarSubfloorWallOpeningWidth(layout, room, direction, doorwayWidth, roomSize, out var openingWidth);
                var hasOpening = hasDoor || hasTunnelOpening;
                if (!hasOpening && IsAllOutWarDomeEdgeOpening(layout, room, direction))
                {
                    return;
                }

                var halfDoorway = openingWidth * 0.5f;
                if (!hasOpening)
                {
                    AddAxisAlignedStructureFlatRect(wallCenter, horizontal, roomSize * 0.5f + wallEndPadding, wallHalfDepth, center.y, falloff);
                    return;
                }

                var segmentLength = half - halfDoorway;
                if (segmentLength <= 0.1f)
                {
                    return;
                }

                var segmentHalfLength = segmentLength * 0.5f + wallEndPadding;
                if (horizontal)
                {
                    var left = center + offset + new Vector3(-(halfDoorway + segmentLength * 0.5f), 0f, 0f);
                    var right = center + offset + new Vector3(halfDoorway + segmentLength * 0.5f, 0f, 0f);
                    AddAxisAlignedStructureFlatRect(left, true, segmentHalfLength, wallHalfDepth, center.y, falloff);
                    AddAxisAlignedStructureFlatRect(right, true, segmentHalfLength, wallHalfDepth, center.y, falloff);
                    AddPillarBaseFlatMask(wallCenter + new Vector3(-halfDoorway, 0f, 0f), structuralThickness, center.y, falloff);
                    AddPillarBaseFlatMask(wallCenter + new Vector3(halfDoorway, 0f, 0f), structuralThickness, center.y, falloff);
                    return;
                }

                var lower = center + offset + new Vector3(0f, 0f, -(halfDoorway + segmentLength * 0.5f));
                var upper = center + offset + new Vector3(0f, 0f, halfDoorway + segmentLength * 0.5f);
                AddAxisAlignedStructureFlatRect(lower, false, segmentHalfLength, wallHalfDepth, center.y, falloff);
                AddAxisAlignedStructureFlatRect(upper, false, segmentHalfLength, wallHalfDepth, center.y, falloff);
                AddPillarBaseFlatMask(wallCenter + new Vector3(0f, 0f, -halfDoorway), structuralThickness, center.y, falloff);
                AddPillarBaseFlatMask(wallCenter + new Vector3(0f, 0f, halfDoorway), structuralThickness, center.y, falloff);
            }

            private void AddCorridorWallBaseFlatMasks(
                ArenaLayout layout,
                Vector2Int room,
                Vector2Int neighbor,
                Vector2Int direction,
                Vector3 from,
                Vector3 to,
                float roomSize,
                float corridorLength,
                float corridorWidth,
                float structuralThickness,
                float wallHalfDepth,
                float falloff)
            {
                var corridorCenter = (from + to) * 0.5f;
                var openClearingLink = layout.AreRoomsInSameClearing(room, neighbor);
                var openSpawnZoneLink = IsInAllOutWarSpawnOpenZone(layout, corridorCenter, corridorWidth * 0.5f) ||
                    IsSegmentInAllOutWarSpawnOpenZone(layout, from, to, corridorWidth * 0.5f);
                if (openClearingLink || openSpawnZoneLink)
                {
                    return;
                }

                var floorHalfLength = ArenaGenerator.GetAllOutWarCorridorFloorLength(corridorLength) * 0.5f;
                var sideOffset = corridorWidth * 0.5f;
                var targetHeight = (from.y + to.y) * 0.5f;
                if (direction == Vector2Int.up)
                {
                    var left = corridorCenter + Vector3.left * sideOffset;
                    var right = corridorCenter + Vector3.right * sideOffset;
                    AddFlatRect(ToXZ(left), wallHalfDepth, floorHalfLength, falloff, targetHeight, StructurePatchPriority);
                    AddFlatRect(ToXZ(right), wallHalfDepth, floorHalfLength, falloff, targetHeight, StructurePatchPriority);

                    return;
                }

                var lower = corridorCenter + Vector3.back * sideOffset;
                var upper = corridorCenter + Vector3.forward * sideOffset;
                AddFlatRect(ToXZ(lower), floorHalfLength, wallHalfDepth, falloff, targetHeight, StructurePatchPriority);
                AddFlatRect(ToXZ(upper), floorHalfLength, wallHalfDepth, falloff, targetHeight, StructurePatchPriority);
            }

            private void AddAxisAlignedStructureFlatRect(Vector3 center, bool horizontal, float halfLength, float halfDepth, float targetHeight, float falloff)
            {
                if (horizontal)
                {
                    AddFlatRect(ToXZ(center), halfLength, halfDepth, falloff, targetHeight, StructurePatchPriority);
                    return;
                }

                AddFlatRect(ToXZ(center), halfDepth, halfLength, falloff, targetHeight, StructurePatchPriority);
            }

            private void AddPillarBaseFlatMask(Vector3 center, float structuralThickness, float targetHeight, float falloff)
            {
                var halfSize = structuralThickness * 1.25f + Mathf.Max(0.55f, structuralThickness);
                AddFlatRect(ToXZ(center), halfSize, halfSize, falloff, targetHeight, StructurePatchPriority);
            }

            private bool TryReserveTerrainOnlyHillCandidate(
                HashSet<Vector2Int> allowed,
                HashSet<Vector2Int> eligibleCells,
                HashSet<Vector2Int> protectedCells,
                Vector2Int seed,
                int totalArmies,
                int gridRadius,
                float signalThreshold,
                float roomSize,
                bool forceMinimumHill)
            {
                var signal = SampleTacticalHillSignal(RoomPoint(seed, cellSpacing));
                if (!forceMinimumHill && signal < signalThreshold)
                {
                    return false;
                }

                var peakHeight = CalculateTacticalHillPeakHeight(allowed.Count, signal, signalThreshold, forceMinimumHill);
                var center = RoomPoint(seed, cellSpacing);
                var sizeClass = ChooseTerrainOnlyHillSizeClass(seed, signal, forceMinimumHill);
                var region = CreateTerrainOnlyHillRegion(center, peakHeight, roomSize, sizeClass, gridRadius);
                if (!HasTerrainOnlyHillCoreSeparation(region))
                {
                    return false;
                }

                if (center.magnitude + region.OuterRadius > gridRadius * cellSpacing - roomSize * 0.5f)
                {
                    return false;
                }

                var crestCells = BuildTerrainOnlyHillRegionCells(eligibleCells, region.Center, region.CrestRadius + cellSpacing * 0.55f);
                var actualHillCells = BuildTerrainOnlyHillRegionCells(eligibleCells, region.Center, region.NoStructureRadius);
                if (crestCells.Count < GetScaledTacticalHillMinimumPlateauRooms(gridRadius) ||
                    actualHillCells.Count < GetScaledTacticalHillMinimumSupportRooms(gridRadius))
                {
                    return false;
                }

                var hardNoStructureCells = BuildTerrainOnlyHillRegionCells(allowed, region.Center, region.NoStructureRadius);
                if (hardNoStructureCells.Count == 0 ||
                    OverlapsTerrainOnlyHillCells(hardNoStructureCells, protectedCells))
                {
                    return false;
                }

                var optionalSpacingCells = BuildTerrainOnlyHillRegionCells(allowed, region.Center, region.SpacingRadius);
                optionalSpacingCells.ExceptWith(hardNoStructureCells);
                optionalSpacingCells.ExceptWith(protectedCells);
                optionalSpacingCells.ExceptWith(terrainOnlyHillHardCells);

                var candidateNoBuildCells = CombineTerrainOnlyHillCells(hardNoStructureCells, optionalSpacingCells);
                if (!HasRequiredRoomConnectivity(allowed, terrainOnlyHillNoBuildCells, candidateNoBuildCells, totalArmies, gridRadius) &&
                    !TryClipTerrainOnlyHillOptionalSpacingForConnectivity(
                        allowed,
                        terrainOnlyHillNoBuildCells,
                        hardNoStructureCells,
                        optionalSpacingCells,
                        totalArmies,
                        gridRadius,
                        out candidateNoBuildCells))
                {
                    return false;
                }

                terrainOnlyHillRegions.Add(region);
                foreach (var cell in hardNoStructureCells)
                {
                    terrainOnlyHillHardCells.Add(cell);
                }

                foreach (var cell in candidateNoBuildCells)
                {
                    terrainOnlyHillNoBuildCells.Add(cell);
                }

                return true;
            }

            private bool HasTerrainOnlyHillCoreSeparation(TerrainOnlyHillRegion candidate)
            {
                foreach (var existing in terrainOnlyHillRegions)
                {
                    var minDistance = GetTerrainOnlyHillCoreSeparationRadius(existing) + GetTerrainOnlyHillCoreSeparationRadius(candidate);
                    if (Vector2.Distance(existing.Center, candidate.Center) < minDistance)
                    {
                        return false;
                    }
                }

                return true;
            }

            private float GetTerrainOnlyHillCoreSeparationRadius(TerrainOnlyHillRegion region)
            {
                // Small battlefields cannot afford the full big-map spread between hills:
                // neighboring-cell hills with overlapping shoulders are explicitly allowed
                // there, otherwise six hills can never pack onto the smallest grid.
                var paddingScale = reservationGridRadius <= 4 ? 0.42f : 1f;
                var corePadding = Mathf.Max(cellSpacing * 0.35f * paddingScale, region.CrestRadius * 0.18f);
                return Mathf.Min(region.OuterRadius, region.CrestRadius + corePadding);
            }

            private TerrainOnlyHillSizeClass ChooseTerrainOnlyHillSizeClass(Vector2Int seed, float signal, bool forceMinimumHill)
            {
                if (reservationGridRadius <= 4)
                {
                    // Only compact hills fit a small battlefield three times over.
                    return TerrainOnlyHillSizeClass.Compact;
                }

                if (mapStyle != AllOutWarMapStyle.Hilly)
                {
                    return TerrainOnlyHillSizeClass.Large;
                }

                var largeCount = 0;
                foreach (var region in terrainOnlyHillRegions)
                {
                    if (region.SizeClass == TerrainOnlyHillSizeClass.Large)
                    {
                        largeCount++;
                    }
                }

                var hash = Mathf.Abs(seed.x * 73856093 ^ seed.y * 19349663 ^ terrainOnlyHillRegions.Count * 83492791);
                var bucket = hash % 100;
                if (largeCount < 2 && (signal >= 0.68f || bucket >= 82))
                {
                    return TerrainOnlyHillSizeClass.Large;
                }

                return bucket < 48 ? TerrainOnlyHillSizeClass.Compact : TerrainOnlyHillSizeClass.Medium;
            }

            private static int GetScaledTacticalHillMinimumSupportRooms(int gridRadius)
            {
                // Small-map hills are deliberately single-cell footprints.
                return gridRadius <= 4 ? 1 : TacticalHillMinimumSupportRooms;
            }

            private static int GetScaledTacticalHillMinimumPlateauRooms(int gridRadius)
            {
                return gridRadius <= 4 ? 1 : TacticalHillMinimumPlateauRooms;
            }

            private TerrainOnlyHillRegion CreateTerrainOnlyHillRegion(Vector2 center, float peakHeight, float roomSize, TerrainOnlyHillSizeClass sizeClass, int gridRadius)
            {
                // Hills shrink with the battlefield so a radius-3 skirmish map can still fit
                // the hilly style's minimum of three; the slope-derived shoulder run is never
                // scaled, so every hill stays traversable.
                var sizeScale = Mathf.Lerp(0.55f, 1f, Mathf.InverseLerp(3f, 6f, gridRadius));
                var hilly = mapStyle == AllOutWarMapStyle.Hilly;
                var crestSpacingScale = hilly
                    ? sizeClass == TerrainOnlyHillSizeClass.Compact ? 0.62f : sizeClass == TerrainOnlyHillSizeClass.Medium ? 0.75f : 0.92f
                    : 0.82f;
                var crestRoomScale = hilly
                    ? sizeClass == TerrainOnlyHillSizeClass.Compact ? 0.95f : sizeClass == TerrainOnlyHillSizeClass.Medium ? 1.05f : 1.1f
                    : 1.1f;
                var shoulderSpacingScale = hilly
                    ? sizeClass == TerrainOnlyHillSizeClass.Compact ? 0.85f : sizeClass == TerrainOnlyHillSizeClass.Medium ? 1.05f : 1.45f
                    : 1.25f;
                var shoulderSlopeDegrees = hilly
                    ? sizeClass == TerrainOnlyHillSizeClass.Compact ? 34f : sizeClass == TerrainOnlyHillSizeClass.Medium ? 30f : TacticalHillShoulderSlopeDegrees
                    : TacticalHillShoulderSlopeDegrees;
                var noStructurePadding = hilly
                    ? sizeClass == TerrainOnlyHillSizeClass.Compact ? roomSize * 0.18f : sizeClass == TerrainOnlyHillSizeClass.Medium ? roomSize * 0.32f : roomSize * 0.5f
                    : roomSize * 0.5f;
                var spacingPadding = hilly
                    ? sizeClass == TerrainOnlyHillSizeClass.Compact ? Mathf.Max(roomSize * 0.18f, cellSpacing * 0.12f) :
                        sizeClass == TerrainOnlyHillSizeClass.Medium ? Mathf.Max(roomSize * 0.28f, cellSpacing * 0.20f) :
                        Mathf.Max(roomSize * 0.55f, cellSpacing * 0.45f)
                    : Mathf.Max(roomSize * 0.55f, cellSpacing * 0.45f);
                // Deterministic per-hill variety: an elliptical crest (asymmetry) and a
                // variable plateau (peakedness). The shoulder run is identical in every
                // direction, so slopes never exceed the size class's traversable angle.
                // Large hills keep the classic symmetric profile — they host hill-cut
                // tunnels, whose mouth/clearance solving assumes the radial shape.
                // Note: the variety must not perturb the reservation radii — acceptance order
                // (and therefore which hills exist per seed) has to stay deterministic. Only
                // the sampled surface shape varies.
                var varietyRandom = new System.Random(
                    Mathf.Abs(Mathf.RoundToInt(center.x * 73.856f) * 73856093 ^
                        Mathf.RoundToInt(center.y * 19.349f) * 19349663 ^
                        terrainOnlyHillRegions.Count * 83492791));
                var shapeVariety = hilly && sizeClass != TerrainOnlyHillSizeClass.Large;
                var crestEccentricity = shapeVariety ? Mathf.Lerp(0f, 0.35f, (float)varietyRandom.NextDouble()) : 0f;
                var axisAngle = (float)varietyRandom.NextDouble() * Mathf.PI;
                var elongationAxis = new Vector2(Mathf.Cos(axisAngle), Mathf.Sin(axisAngle));
                var crestSharpness = shapeVariety ? Mathf.Lerp(0f, 0.7f, (float)varietyRandom.NextDouble()) : 0f;

                if (gridRadius <= 4)
                {
                    // Slightly steeper (still walkable) shoulders keep small-map hill
                    // footprints inside a single grid cell so three can coexist.
                    shoulderSlopeDegrees = Mathf.Max(shoulderSlopeDegrees, 38f);
                }

                var crestRadius = Mathf.Max(roomSize * crestRoomScale, cellSpacing * crestSpacingScale) * sizeScale;
                var shoulderRun = Mathf.Max(
                    cellSpacing * shoulderSpacingScale * sizeScale,
                    peakHeight * Mathf.PI / (2f * Mathf.Tan(shoulderSlopeDegrees * Mathf.Deg2Rad)));
                var outerRadius = crestRadius + shoulderRun;
                var noStructureRadius = outerRadius + noStructurePadding * sizeScale;
                if (gridRadius <= 4)
                {
                    noStructureRadius = Mathf.Min(noStructureRadius, cellSpacing * 0.97f);
                }

                var spacingRadius = noStructureRadius + spacingPadding * sizeScale;
                return new TerrainOnlyHillRegion(
                    center,
                    peakHeight,
                    crestRadius,
                    outerRadius,
                    noStructureRadius,
                    spacingRadius,
                    sizeClass,
                    elongationAxis,
                    crestEccentricity,
                    crestSharpness);
            }

            private HashSet<Vector2Int> BuildTerrainOnlyHillRegionCells(HashSet<Vector2Int> allowed, Vector2 center, float radius)
            {
                var cells = new HashSet<Vector2Int>();
                if (allowed == null || radius <= 0f)
                {
                    return cells;
                }

                var radiusSqr = radius * radius;
                foreach (var cell in allowed)
                {
                    if ((RoomPoint(cell, cellSpacing) - center).sqrMagnitude <= radiusSqr)
                    {
                        cells.Add(cell);
                    }
                }

                return cells;
            }

            private static HashSet<Vector2Int> BuildTerrainOnlyHillProtectedCells(
                HashSet<Vector2Int> allowed,
                HashSet<Vector2Int> spawnProtectedRooms,
                int totalArmies,
                int gridRadius)
            {
                var protectedCells = new HashSet<Vector2Int>();

                if (spawnProtectedRooms != null)
                {
                    foreach (var room in spawnProtectedRooms)
                    {
                        protectedCells.Add(room);
                    }
                }

                for (var team = 0; team < totalArmies; team++)
                {
                    protectedCells.Add(FindNearestAllowedCell(allowed, GetArmySpawnFrontTarget(team, totalArmies, gridRadius)));
                }

                return protectedCells;
            }

            private static bool OverlapsTerrainOnlyHillCells(HashSet<Vector2Int> candidateCells, HashSet<Vector2Int> protectedCells)
            {
                foreach (var cell in candidateCells)
                {
                    if (protectedCells.Contains(cell))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static HashSet<Vector2Int> CombineTerrainOnlyHillCells(HashSet<Vector2Int> hardNoStructureCells, HashSet<Vector2Int> optionalSpacingCells)
            {
                var cells = new HashSet<Vector2Int>();
                if (hardNoStructureCells != null)
                {
                    foreach (var cell in hardNoStructureCells)
                    {
                        cells.Add(cell);
                    }
                }

                if (optionalSpacingCells != null)
                {
                    foreach (var cell in optionalSpacingCells)
                    {
                        cells.Add(cell);
                    }
                }

                return cells;
            }

            private static bool TryClipTerrainOnlyHillOptionalSpacingForConnectivity(
                HashSet<Vector2Int> allowed,
                HashSet<Vector2Int> existingNoBuildCells,
                HashSet<Vector2Int> hardNoStructureCells,
                HashSet<Vector2Int> optionalSpacingCells,
                int totalArmies,
                int gridRadius,
                out HashSet<Vector2Int> candidateNoBuildCells)
            {
                candidateNoBuildCells = CombineTerrainOnlyHillCells(hardNoStructureCells, optionalSpacingCells);
                if (!TryCollectRequiredRouteCells(allowed, existingNoBuildCells, hardNoStructureCells, totalArmies, gridRadius, out var routeCells))
                {
                    return false;
                }

                var clippedOptionalCells = new HashSet<Vector2Int>(optionalSpacingCells);
                clippedOptionalCells.ExceptWith(routeCells);
                candidateNoBuildCells = CombineTerrainOnlyHillCells(hardNoStructureCells, clippedOptionalCells);
                return HasRequiredRoomConnectivity(allowed, existingNoBuildCells, candidateNoBuildCells, totalArmies, gridRadius);
            }

            private static bool HasRequiredRoomConnectivity(
                HashSet<Vector2Int> allowed,
                HashSet<Vector2Int> existingNoBuildCells,
                HashSet<Vector2Int> candidateNoBuildCells,
                int totalArmies,
                int gridRadius)
            {
                if (!TryFindTerrainConnectivityHub(allowed, existingNoBuildCells, candidateNoBuildCells, out var hub))
                {
                    return false;
                }

                for (var team = 0; team < totalArmies; team++)
                {
                    var target = GetArmySpawnFrontTarget(team, totalArmies, gridRadius);
                    var frontRoom = FindNearestAllowedCell(
                        allowed,
                        target,
                        cell => !IsReservedTerrainOnlyHillCell(cell, existingNoBuildCells, candidateNoBuildCells));
                    if (IsReservedTerrainOnlyHillCell(frontRoom, existingNoBuildCells, candidateNoBuildCells) ||
                        !HasPathAroundReservedTerrainOnlyHillCells(allowed, frontRoom, hub, existingNoBuildCells, candidateNoBuildCells))
                    {
                        return false;
                    }
                }

                return true;
            }

            private static bool TryCollectRequiredRouteCells(
                HashSet<Vector2Int> allowed,
                HashSet<Vector2Int> existingNoBuildCells,
                HashSet<Vector2Int> candidateHardNoBuildCells,
                int totalArmies,
                int gridRadius,
                out HashSet<Vector2Int> routeCells)
            {
                routeCells = new HashSet<Vector2Int>();
                if (!TryFindTerrainConnectivityHub(allowed, existingNoBuildCells, candidateHardNoBuildCells, out var hub))
                {
                    return false;
                }

                for (var team = 0; team < totalArmies; team++)
                {
                    var target = GetArmySpawnFrontTarget(team, totalArmies, gridRadius);
                    var frontRoom = FindNearestAllowedCell(
                        allowed,
                        target,
                        cell => !IsReservedTerrainOnlyHillCell(cell, existingNoBuildCells, candidateHardNoBuildCells));
                    if (IsReservedTerrainOnlyHillCell(frontRoom, existingNoBuildCells, candidateHardNoBuildCells) ||
                        !TryGetPathAroundReservedTerrainOnlyHillCells(allowed, frontRoom, hub, existingNoBuildCells, candidateHardNoBuildCells, out var path))
                    {
                        return false;
                    }

                    foreach (var cell in path)
                    {
                        routeCells.Add(cell);
                    }
                }

                return true;
            }

            private static bool TryFindTerrainConnectivityHub(
                HashSet<Vector2Int> allowed,
                HashSet<Vector2Int> existingNoBuildCells,
                HashSet<Vector2Int> candidateNoBuildCells,
                out Vector2Int hub)
            {
                hub = default;
                if (allowed == null || allowed.Count == 0)
                {
                    return false;
                }

                var found = false;
                var bestDistance = float.PositiveInfinity;
                foreach (var cell in allowed)
                {
                    if (IsReservedTerrainOnlyHillCell(cell, existingNoBuildCells, candidateNoBuildCells))
                    {
                        continue;
                    }

                    var distance = cell.sqrMagnitude;
                    if (!found || distance < bestDistance)
                    {
                        hub = cell;
                        bestDistance = distance;
                        found = true;
                    }
                }

                return found;
            }

            private static bool HasPathAroundReservedTerrainOnlyHillCells(
                HashSet<Vector2Int> allowed,
                Vector2Int start,
                Vector2Int goal,
                HashSet<Vector2Int> existingNoBuildCells,
                HashSet<Vector2Int> candidateNoBuildCells)
            {
                return TryGetPathAroundReservedTerrainOnlyHillCells(allowed, start, goal, existingNoBuildCells, candidateNoBuildCells, out _);
            }

            private static bool TryGetPathAroundReservedTerrainOnlyHillCells(
                HashSet<Vector2Int> allowed,
                Vector2Int start,
                Vector2Int goal,
                HashSet<Vector2Int> existingNoBuildCells,
                HashSet<Vector2Int> candidateNoBuildCells,
                out List<Vector2Int> path)
            {
                path = new List<Vector2Int>();
                if (!allowed.Contains(start) ||
                    !allowed.Contains(goal) ||
                    IsReservedTerrainOnlyHillCell(start, existingNoBuildCells, candidateNoBuildCells) ||
                    IsReservedTerrainOnlyHillCell(goal, existingNoBuildCells, candidateNoBuildCells))
                {
                    return false;
                }

                var parents = new Dictionary<Vector2Int, Vector2Int>();
                var visited = new HashSet<Vector2Int> { start };
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (current == goal)
                    {
                        var room = current;
                        path.Add(room);
                        while (room != start)
                        {
                            room = parents[room];
                            path.Add(room);
                        }

                        path.Reverse();
                        return true;
                    }

                    foreach (var direction in Directions)
                    {
                        var neighbor = current + direction;
                        if (!allowed.Contains(neighbor) ||
                            visited.Contains(neighbor) ||
                            IsReservedTerrainOnlyHillCell(neighbor, existingNoBuildCells, candidateNoBuildCells))
                        {
                            continue;
                    }

                    parents[neighbor] = current;
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
                }

                return false;
            }

            private static bool IsReservedTerrainOnlyHillCell(
                Vector2Int cell,
                HashSet<Vector2Int> existingNoBuildCells,
                HashSet<Vector2Int> candidateNoBuildCells)
            {
                return (existingNoBuildCells != null && existingNoBuildCells.Contains(cell)) ||
                    (candidateNoBuildCells != null && candidateNoBuildCells.Contains(cell));
            }

            private static HashSet<Vector2Int> BuildClearingFlatProtectedRooms(ArenaLayout layout)
            {
                var protectedRooms = new HashSet<Vector2Int>();
                if (layout == null || layout.ClearingRoomGroups.Count == 0)
                {
                    return protectedRooms;
                }

                foreach (var clearingRoom in layout.ClearingRoomGroups.Keys)
                {
                    if (!layout.Rooms.Contains(clearingRoom))
                    {
                        continue;
                    }

                    protectedRooms.Add(clearingRoom);
                    foreach (var direction in Directions)
                    {
                        var neighbor = clearingRoom + direction;
                        if (layout.Rooms.Contains(neighbor))
                        {
                            protectedRooms.Add(neighbor);
                        }
                    }
                }

                return protectedRooms;
            }

            private void AddClearingFlatProtectedMasks(ArenaLayout layout, HashSet<Vector2Int> clearingFlatRooms, float roomSize, float corridorLength, float roomFalloff)
            {
                if (layout == null || clearingFlatRooms == null || clearingFlatRooms.Count == 0)
                {
                    return;
                }

                foreach (var room in clearingFlatRooms)
                {
                    if (layout.RoomCenters.TryGetValue(room, out var center))
                    {
                        AddFlatRect(ToXZ(center), roomSize * 0.58f, roomSize * 0.58f, roomFalloff, 0f, ClearingPatchPriority);
                    }
                }

                foreach (var room in layout.Rooms)
                {
                    foreach (var direction in new[] { Vector2Int.up, Vector2Int.right })
                    {
                        var neighbor = room + direction;
                        if (!layout.Rooms.Contains(neighbor) ||
                            !layout.RoomCenters.TryGetValue(room, out var from) ||
                            !layout.RoomCenters.TryGetValue(neighbor, out var to) ||
                            !IsClearingFlatProtectedLink(layout, clearingFlatRooms, room, neighbor))
                        {
                            continue;
                        }

                        var center = ToXZ((from + to) * 0.5f);
                        var floorHalfLength = ArenaGenerator.GetAllOutWarCorridorFloorLength(corridorLength) * 0.5f;
                        if (direction == Vector2Int.up)
                        {
                            AddFlatRect(center, roomSize * 0.54f, floorHalfLength, roomFalloff, 0f, ClearingPatchPriority);
                        }
                        else
                        {
                            AddFlatRect(center, floorHalfLength, roomSize * 0.54f, roomFalloff, 0f, ClearingPatchPriority);
                        }
                    }
                }
            }

            private static bool IsClearingFlatProtectedLink(ArenaLayout layout, HashSet<Vector2Int> clearingFlatRooms, Vector2Int room, Vector2Int neighbor)
            {
                if (layout == null || clearingFlatRooms == null)
                {
                    return false;
                }

                if (layout.AreRoomsInSameClearing(room, neighbor))
                {
                    return true;
                }

                if (clearingFlatRooms.Contains(room) && clearingFlatRooms.Contains(neighbor))
                {
                    return true;
                }

                var roomIsClearing = layout.ClearingRoomGroups.ContainsKey(room);
                var neighborIsClearing = layout.ClearingRoomGroups.ContainsKey(neighbor);
                return (roomIsClearing && clearingFlatRooms.Contains(neighbor)) ||
                       (neighborIsClearing && clearingFlatRooms.Contains(room));
            }

            public float SampleHeight(Vector3 position)
            {
                return SampleHeight(position, true);
            }

            public float SampleHeightBruteForceForTests(Vector3 position)
            {
                return SampleHeight(position, false);
            }

            private float SampleHeight(Vector3 position, bool useSpatialIndex)
            {
                var point = new Vector2(position.x, position.z);
                var rawHeight = SampleRawHeight(point);
                var flatWeight = 0f;
                var targetHeight = 0f;
                var selectedPriority = int.MinValue;
                var hasSelectedPatch = false;

                if (useSpatialIndex && flatPatchSpatialIndex.Count > 0)
                {
                    var cell = ToFlatPatchIndexCell(point, FlatPatchIndexCellSize);
                    if (flatPatchSpatialIndex.TryGetValue(cell, out var patchIndices))
                    {
                        foreach (var patchIndex in patchIndices)
                        {
                            ApplyFlatPatchSample(
                                flatPatches[patchIndex],
                                point,
                                ref flatWeight,
                                ref targetHeight,
                                ref selectedPriority,
                                ref hasSelectedPatch);
                        }
                    }
                }
                else
                {
                    for (var i = 0; i < flatPatches.Count; i++)
                    {
                        ApplyFlatPatchSample(
                            flatPatches[i],
                            point,
                            ref flatWeight,
                            ref targetHeight,
                            ref selectedPriority,
                            ref hasSelectedPatch);
                    }
                }

                if (!hasSelectedPatch)
                {
                    return rawHeight;
                }

                var smoothedWeight = flatWeight * flatWeight * (3f - 2f * flatWeight);
                return Mathf.Lerp(rawHeight, targetHeight, smoothedWeight);
            }

            private static void ApplyFlatPatchSample(
                FlatPatch patch,
                Vector2 point,
                ref float flatWeight,
                ref float targetHeight,
                ref int selectedPriority,
                ref bool hasSelectedPatch)
            {
                if (!patch.MayAffect(point))
                {
                    return;
                }

                var weight = patch.Evaluate(point);
                if (weight <= 0f)
                {
                    return;
                }

                var fullCoveragePriorityWin = patch.HasFullCoverage(point) &&
                    (!hasSelectedPatch || flatWeight < 1f - FlatPatchWeightEpsilon || patch.Priority > selectedPriority);
                var isStrongerPatch = weight > flatWeight + FlatPatchWeightEpsilon &&
                    flatWeight < 1f - FlatPatchWeightEpsilon;
                var isPriorityTieBreak = Mathf.Abs(weight - flatWeight) <= FlatPatchWeightEpsilon && patch.Priority > selectedPriority;
                if (!hasSelectedPatch || fullCoveragePriorityWin || isStrongerPatch || isPriorityTieBreak)
                {
                    flatWeight = weight;
                    targetHeight = patch.TargetHeight();
                    selectedPriority = patch.Priority;
                    hasSelectedPatch = true;
                }
            }

            private float GetTacticalHillCandidateScore(Vector2Int room, float spacing)
            {
                // Mix per-seed placement noise into the Perlin signal so hills scatter across
                // the map instead of always clustering on the strongest noise blob.
                var signal = SampleTacticalHillSignal(RoomPoint(room, spacing));
                var hash = Mathf.Abs(
                    room.x * 73856093 ^
                    room.y * 19349663 ^
                    Mathf.RoundToInt(tacticalHillOffset.x * 17f + tacticalHillOffset.y * 29f) * 83492791);
                return signal * 0.62f + (hash % 1000) / 1000f * 0.38f;
            }

            private float SampleTacticalHillSignal(Vector2 point)
            {
                return Mathf.PerlinNoise((point.x + tacticalHillOffset.x) / tacticalHillScale, (point.y + tacticalHillOffset.y) / tacticalHillScale);
            }

            private float CalculateTacticalHillPeakHeight(int roomCount, float signal, float signalThreshold, bool forceMinimumHill)
            {
                var mapScale = Mathf.InverseLerp(18f, 64f, roomCount);
                var signalStrength = forceMinimumHill ? 0.72f : Mathf.InverseLerp(signalThreshold, 0.86f, signal);
                var mapPeak = Mathf.Lerp(TacticalHillMinimumHeight, tacticalHillPeakHeight, mapScale);
                var height = Mathf.Lerp(TacticalHillMinimumHeight, mapPeak, signalStrength);
                if (forceMinimumHill)
                {
                    // The forced floor follows the map-scaled peak (identical on full-size
                    // maps); the unscaled value forced oversized hills onto small grids,
                    // whose footprints could never fit three times.
                    height = Mathf.Max(height, Mathf.Lerp(TacticalHillMinimumHeight, mapPeak, 0.62f));
                }

                return Mathf.Clamp(height, TacticalHillMinimumHeight, TacticalHillMaximumHeight);
            }

            public bool IsClearingFootprintBuildable(List<Vector2Int> footprint)
            {
                if (footprint == null || footprint.Count == 0)
                {
                    return false;
                }

                foreach (var room in footprint)
                {
                    if (terrainOnlyHillNoBuildCells.Contains(room))
                    {
                        return false;
                    }
                }

                return true;
            }

            public bool TryScoreTerrainOnlyHillCutEndpoint(
                Vector3 portal,
                Vector3 direction,
                float tunnelWidth,
                float tunnelHeight,
                out int hillIndex,
                out float score)
            {
                hillIndex = -1;
                score = 0f;
                if (terrainOnlyHillRegions.Count == 0 ||
                    !TryNormalize(ToXZ(direction), out var flatDirection))
                {
                    return false;
                }

                var point = ToXZ(portal);
                var requiredHeight = ArenaGenerator.GetAllOutWarHillCutRequiredTerrainHeight(tunnelHeight);
                var bestScore = float.NegativeInfinity;
                for (var i = 0; i < terrainOnlyHillRegions.Count; i++)
                {
                    var region = terrainOnlyHillRegions[i];
                    if (region.PeakHeight < requiredHeight)
                    {
                        continue;
                    }

                    var radial = region.Center - point;
                    if (radial.sqrMagnitude <= 0.001f)
                    {
                        continue;
                    }

                    var distance = radial.magnitude;
                    var mouthReach = Mathf.Max(tunnelWidth * 1.85f, 4.5f);
                    if (distance > region.OuterRadius + mouthReach ||
                        distance < region.CrestRadius * 0.45f)
                    {
                        continue;
                    }

                    var alignment = Vector2.Dot(flatDirection, radial / distance);
                    if (alignment < 0.64f)
                    {
                        continue;
                    }

                    var innerProbe = point + flatDirection * Mathf.Max(tunnelWidth * 1.25f, 3f);
                    if (Vector2.Distance(innerProbe, region.Center) > region.OuterRadius - tunnelWidth * 0.25f)
                    {
                        continue;
                    }

                    var portalPoint = new Vector3(point.x, 0f, point.y);
                    if (!TryBuildTerrainOnlyHillMouthApron(
                        i,
                        portalPoint,
                        direction,
                        tunnelWidth,
                        tunnelHeight,
                        out _,
                        out _,
                        out _,
                        out _))
                    {
                        continue;
                    }

                    var hillsideScore = Mathf.InverseLerp(region.OuterRadius + mouthReach, region.CrestRadius, distance);
                    var heightScore = Mathf.InverseLerp(requiredHeight, requiredHeight + 1.75f, region.PeakHeight);
                    var candidateScore = alignment * 1.5f + hillsideScore + heightScore;
                    if (candidateScore > bestScore)
                    {
                        bestScore = candidateScore;
                        hillIndex = i;
                    }
                }

                if (hillIndex < 0)
                {
                    return false;
                }

                score = bestScore;
                return true;
            }

            public bool TryBuildTerrainOnlyHillCutEndpoint(
                int hillIndex,
                Vector3 inwardDirection,
                float tunnelWidth,
                float tunnelHeight,
                float mouthLength,
                out Vector3 portal,
                out float score)
            {
                portal = default;
                score = 0f;
                if (hillIndex < 0 ||
                    hillIndex >= terrainOnlyHillRegions.Count ||
                    !TryNormalize(ToXZ(inwardDirection), out var flatDirection))
                {
                    return false;
                }

                var region = terrainOnlyHillRegions[hillIndex];
                var requiredHeight = ArenaGenerator.GetAllOutWarHillCutRequiredTerrainHeight(tunnelHeight);
                if (region.PeakHeight < requiredHeight)
                {
                    return false;
                }

                // Asymmetric hills have a direction-dependent crest; the mouth search window
                // must follow the actual shoulder on the approach side or every sample lands
                // on already-fallen terrain.
                var insideMargin = Mathf.Max(tunnelWidth * 0.34f, 1.1f);
                var directionalCrest = region.DirectionalCrestRadius(-flatDirection);
                var shoulderRun = region.OuterRadius - region.CrestRadius;
                var outerLimit = directionalCrest + shoulderRun - insideMargin;
                var innerLimit = directionalCrest + Mathf.Max(0.65f, tunnelWidth * 0.16f);
                if (outerLimit <= innerLimit)
                {
                    return false;
                }

                var side = new Vector2(-flatDirection.y, flatDirection.x);
                var sideExtent = tunnelWidth * 0.48f;
                var probeLength = Mathf.Max(mouthLength, tunnelWidth * 1.25f);
                const int samples = 28;
                for (var i = 0; i <= samples; i++)
                {
                    var t = i / (float)samples;
                    var distance = Mathf.Lerp(outerLimit, innerLimit, t);
                    var point = region.Center - flatDirection * distance;
                    if (!TerrainOnlyHillCutPointHasClearance(region, point, side, sideExtent, requiredHeight, insideMargin))
                    {
                        continue;
                    }

                    var innerProbe = point + flatDirection * probeLength;
                    if (!TerrainOnlyHillCutPointHasClearance(region, innerProbe, side, sideExtent, requiredHeight, insideMargin))
                    {
                        continue;
                    }

                    portal = new Vector3(point.x, 0f, point.y);
                    if (!TryBuildTerrainOnlyHillMouthApron(
                        hillIndex,
                        portal,
                        inwardDirection,
                        tunnelWidth,
                        tunnelHeight,
                        out var outerApproach,
                        out _,
                        out _,
                        out _))
                    {
                        continue;
                    }

                    var apronLength = FlatDistance(portal, outerApproach);
                    var hillsideScore = Mathf.InverseLerp(outerLimit, innerLimit, distance);
                    var heightScore = Mathf.InverseLerp(requiredHeight, requiredHeight + 2.2f, region.SampleHeight(point));
                    var apronScore = 1f - Mathf.Clamp01(apronLength / ArenaGenerator.GetAllOutWarHillCutMaxMouthApronLength(tunnelWidth));
                    score = 0.42f + hillsideScore * 0.3f + heightScore * 0.18f + apronScore * 0.1f;
                    return true;
                }

                return false;
            }

            public bool TryBuildTerrainOnlyHillMouthApron(
                int hillIndex,
                Vector3 portal,
                Vector3 inwardDirection,
                float tunnelWidth,
                float tunnelHeight,
                out Vector3 outerApproach,
                out Vector3 innerMouth,
                out float outerHalfWidth,
                out float innerHalfWidth)
            {
                outerApproach = default;
                innerMouth = default;
                outerHalfWidth = ArenaGenerator.GetAllOutWarHillCutMouthOuterHalfWidth(tunnelWidth);
                innerHalfWidth = ArenaGenerator.GetAllOutWarHillCutMouthInnerHalfWidth(tunnelWidth);
                if (hillIndex < 0 ||
                    hillIndex >= terrainOnlyHillRegions.Count ||
                    !TryNormalize(ToXZ(inwardDirection), out var flatDirection))
                {
                    return false;
                }

                var region = terrainOnlyHillRegions[hillIndex];
                var requiredHeight = ArenaGenerator.GetAllOutWarHillCutRequiredTerrainHeight(tunnelHeight);
                if (region.PeakHeight < requiredHeight)
                {
                    return false;
                }

                var point = ToXZ(portal);
                var side = new Vector2(-flatDirection.y, flatDirection.x);
                var sideExtent = tunnelWidth * 0.48f;
                var insideMargin = Mathf.Max(tunnelWidth * 0.24f, 0.85f);
                var innerPoint = point + flatDirection * ArenaGenerator.GetAllOutWarHillCutMouthInnerDepth(tunnelWidth);
                if (!TerrainOnlyHillCutPointHasClearance(region, point, side, sideExtent, requiredHeight, insideMargin) ||
                    !TerrainOnlyHillCutPointHasClearance(region, innerPoint, side, sideExtent, requiredHeight, insideMargin))
                {
                    return false;
                }

                var maxApronLength = ArenaGenerator.GetAllOutWarHillCutMaxMouthApronLength(tunnelWidth);
                const int samples = 34;
                for (var i = 1; i <= samples; i++)
                {
                    var distance = maxApronLength * i / samples;
                    var candidate = point - flatDirection * distance;
                    if (!TerrainOnlyHillMouthApproachHasFloorAccess(candidate, side, outerHalfWidth))
                    {
                        continue;
                    }

                    outerApproach = new Vector3(candidate.x, 0f, candidate.y);
                    innerMouth = new Vector3(innerPoint.x, 0f, innerPoint.y);
                    return true;
                }

                return false;
            }

            public bool IsNearTerrainOnlyHillBoundary(Vector3 position, float padding)
            {
                if (terrainOnlyHillRegions.Count == 0)
                {
                    return false;
                }

                var point = ToXZ(position);
                var safePadding = Mathf.Max(0f, padding);
                foreach (var region in terrainOnlyHillRegions)
                {
                    if (Vector2.Distance(point, region.Center) <= region.OuterRadius + safePadding)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool TerrainOnlyHillCutPointHasClearance(
                TerrainOnlyHillRegion region,
                Vector2 center,
                Vector2 side,
                float sideExtent,
                float requiredHeight,
                float insideMargin)
            {
                for (var sideIndex = -1; sideIndex <= 1; sideIndex++)
                {
                    var samplePoint = center + side * (sideExtent * sideIndex);
                    if (Vector2.Distance(samplePoint, region.Center) > region.OuterRadius - insideMargin ||
                        region.SampleHeight(samplePoint) < requiredHeight)
                    {
                        return false;
                    }
                }

                return true;
            }

            private bool TerrainOnlyHillMouthApproachHasFloorAccess(
                Vector2 center,
                Vector2 side,
                float halfWidth)
            {
                for (var sideIndex = -2; sideIndex <= 2; sideIndex++)
                {
                    var samplePoint = center + side * (halfWidth * sideIndex * 0.5f);
                    if (SampleTerrainOnlyHillHeight(samplePoint) > AllOutWarHillCutMouthFloorAccessHeight)
                    {
                        return false;
                    }
                }

                return true;
            }

            public bool TryScoreTerrainOnlyHillCutRoute(
                Vector3 fromPortal,
                Vector3 toPortal,
                Vector3 fromDirection,
                Vector3 toDirection,
                float tunnelWidth,
                float tunnelHeight,
                float portalRadius,
                out float score)
            {
                score = 0f;
                for (var i = 0; i < terrainOnlyHillRegions.Count; i++)
                {
                    if (TryScoreTerrainOnlyHillCutRoute(
                        i,
                        fromPortal,
                        toPortal,
                        fromDirection,
                        toDirection,
                        tunnelWidth,
                        tunnelHeight,
                        portalRadius,
                        out var candidateScore) &&
                        candidateScore > score)
                    {
                        score = candidateScore;
                    }
                }

                return score > 0f;
            }

            public bool TryScoreTerrainOnlyHillCutRoute(
                int hillIndex,
                Vector3 fromPortal,
                Vector3 toPortal,
                Vector3 fromDirection,
                Vector3 toDirection,
                float tunnelWidth,
                float tunnelHeight,
                float portalRadius,
                out float score)
            {
                return TryScoreTerrainOnlyHillCutRoute(
                    hillIndex,
                    hillIndex,
                    fromPortal,
                    toPortal,
                    fromDirection,
                    toDirection,
                    tunnelWidth,
                    tunnelHeight,
                    portalRadius,
                    out score);
            }

            public bool TryScoreTerrainOnlyHillCutRoute(
                int fromHillIndex,
                int toHillIndex,
                Vector3 fromPortal,
                Vector3 toPortal,
                Vector3 fromDirection,
                Vector3 toDirection,
                float tunnelWidth,
                float tunnelHeight,
                float portalRadius,
                out float score)
            {
                score = 0f;
                if (fromHillIndex < 0 ||
                    fromHillIndex >= terrainOnlyHillRegions.Count ||
                    toHillIndex < 0 ||
                    toHillIndex >= terrainOnlyHillRegions.Count ||
                    !TryNormalize(ToXZ(fromDirection), out var fromFlatDirection) ||
                    !TryNormalize(ToXZ(toDirection), out var toFlatDirection))
                {
                    return false;
                }

                var fromRegion = terrainOnlyHillRegions[fromHillIndex];
                var toRegion = terrainOnlyHillRegions[toHillIndex];
                var requiredHeight = ArenaGenerator.GetAllOutWarHillCutRequiredTerrainHeight(tunnelHeight);
                if (fromRegion.PeakHeight < requiredHeight || toRegion.PeakHeight < requiredHeight)
                {
                    return false;
                }

                var fromPoint = ToXZ(fromPortal);
                var toPoint = ToXZ(toPortal);
                if (!TryNormalize(fromPoint - fromRegion.Center, out var fromRadial) ||
                    !TryNormalize(toPoint - toRegion.Center, out var toRadial))
                {
                    return false;
                }

                if (Vector2.Dot(fromFlatDirection, -fromRadial) < 0.62f ||
                    Vector2.Dot(toFlatDirection, -toRadial) < 0.62f)
                {
                    return false;
                }

                if (fromHillIndex == toHillIndex)
                {
                    if (Vector2.Dot(fromRadial, toRadial) > -0.35f)
                    {
                        return false;
                    }
                }
                else if (!DoTerrainOnlyHillShouldersOverlap(fromRegion, toRegion))
                {
                    return false;
                }

                var mouthLength = Mathf.Max(portalRadius * 0.75f, tunnelWidth * 0.82f);
                var fromInner = fromPoint + fromFlatDirection * mouthLength;
                var toInner = toPoint + toFlatDirection * mouthLength;
                var insideMargin = tunnelWidth * 0.32f;
                if (Vector2.Distance(fromInner, fromRegion.Center) > fromRegion.OuterRadius - insideMargin ||
                    Vector2.Distance(toInner, toRegion.Center) > toRegion.OuterRadius - insideMargin)
                {
                    return false;
                }

                var chord = toInner - fromInner;
                if (chord.sqrMagnitude <= tunnelWidth * tunnelWidth)
                {
                    return false;
                }

                var fromCenterMiss = DistanceToSegment(fromRegion.Center, fromInner, toInner);
                var toCenterMiss = DistanceToSegment(toRegion.Center, fromInner, toInner);
                if (fromCenterMiss > fromRegion.CrestRadius + tunnelWidth * 0.55f ||
                    toCenterMiss > toRegion.CrestRadius + tunnelWidth * 0.55f)
                {
                    return false;
                }

                if (!TryBuildTerrainOnlyHillMouthApron(
                        fromHillIndex,
                        fromPortal,
                        fromDirection,
                        tunnelWidth,
                        tunnelHeight,
                        out _,
                        out _,
                        out _,
                        out _) ||
                    !TryBuildTerrainOnlyHillMouthApron(
                        toHillIndex,
                        toPortal,
                        toDirection,
                        tunnelWidth,
                        tunnelHeight,
                        out _,
                        out _,
                        out _,
                        out _))
                {
                    return false;
                }

                var side = new Vector2(-chord.y, chord.x).normalized;
                var sideExtent = tunnelWidth * 0.46f;
                var minimumHeight = float.MaxValue;
                const int samples = 18;
                for (var i = 0; i <= samples; i++)
                {
                    var t = i / (float)samples;
                    var center = Vector2.Lerp(fromInner, toInner, t);
                    for (var sideIndex = -1; sideIndex <= 1; sideIndex++)
                    {
                        var samplePoint = center + side * (sideExtent * sideIndex);
                        if (!IsPointInsideTerrainOnlyHillUnion(samplePoint, insideMargin))
                        {
                            return false;
                        }

                        var height = SampleTerrainOnlyHillHeight(samplePoint);
                        if (height < requiredHeight)
                        {
                            return false;
                        }

                        minimumHeight = Mathf.Min(minimumHeight, height);
                    }
                }

                var heightScore = Mathf.InverseLerp(requiredHeight, requiredHeight + 2.5f, minimumHeight);
                var centerMiss = Mathf.Max(
                    fromCenterMiss / Mathf.Max(0.1f, fromRegion.CrestRadius + tunnelWidth * 0.55f),
                    toCenterMiss / Mathf.Max(0.1f, toRegion.CrestRadius + tunnelWidth * 0.55f));
                var centerScore = 1f - Mathf.Clamp01(centerMiss);
                var oppositionScore = Mathf.InverseLerp(-0.35f, -1f, Vector2.Dot(fromRadial, toRadial));
                score = 0.35f + heightScore * 0.36f + centerScore * 0.2f + oppositionScore * 0.09f;
                return true;
            }

            private bool DoTerrainOnlyHillShouldersOverlap(TerrainOnlyHillRegion first, TerrainOnlyHillRegion second)
            {
                var distance = Vector2.Distance(first.Center, second.Center);
                return distance < first.OuterRadius + second.OuterRadius &&
                    distance > GetTerrainOnlyHillCoreSeparationRadius(first) + GetTerrainOnlyHillCoreSeparationRadius(second);
            }

            private bool IsPointInsideTerrainOnlyHillUnion(Vector2 point, float insideMargin)
            {
                var safeInsideMargin = Mathf.Max(0f, insideMargin);
                foreach (var region in terrainOnlyHillRegions)
                {
                    if (Vector2.Distance(point, region.Center) <= region.OuterRadius - safeInsideMargin)
                    {
                        return true;
                    }
                }

                return false;
            }

            public bool IsTerrainCutout(Vector3 position)
            {
                if (terrainCutouts.Count == 0)
                {
                    return false;
                }

                var point = ToXZ(position);
                foreach (var cutout in terrainCutouts)
                {
                    if (cutout.Contains(point))
                    {
                        return true;
                    }
                }

                return false;
            }

            private float FlatPatchIndexCellSize => Mathf.Max(8f, cellSpacing * 0.75f);

            private void RebuildFlatPatchSpatialIndex()
            {
                flatPatchSpatialIndex.Clear();
                if (flatPatches.Count == 0)
                {
                    return;
                }

                var cellSize = FlatPatchIndexCellSize;
                for (var patchIndex = 0; patchIndex < flatPatches.Count; patchIndex++)
                {
                    var patch = flatPatches[patchIndex];
                    var minCell = ToFlatPatchIndexCell(patch.BoundsMin, cellSize);
                    var maxCell = ToFlatPatchIndexCell(patch.BoundsMax, cellSize);
                    for (var x = minCell.x; x <= maxCell.x; x++)
                    {
                        for (var y = minCell.y; y <= maxCell.y; y++)
                        {
                            var cell = new Vector2Int(x, y);
                            if (!flatPatchSpatialIndex.TryGetValue(cell, out var indices))
                            {
                                indices = new List<int>();
                                flatPatchSpatialIndex[cell] = indices;
                            }

                            indices.Add(patchIndex);
                        }
                    }
                }
            }

            private static Vector2Int ToFlatPatchIndexCell(Vector2 point, float cellSize)
            {
                return new Vector2Int(
                    Mathf.FloorToInt(point.x / cellSize),
                    Mathf.FloorToInt(point.y / cellSize));
            }

            private float SampleRawHeight(Vector2 point)
            {
                return SampleTerrainOnlyHillHeight(point);
            }

            private float SampleTerrainOnlyHillHeight(Vector2 point)
            {
                if (terrainOnlyHillRegions.Count == 0)
                {
                    return 0f;
                }

                var height = 0f;
                foreach (var region in terrainOnlyHillRegions)
                {
                    height = Mathf.Max(height, region.SampleHeight(point));
                }

                return height;
            }

            private static float SmootherStep(float t)
            {
                t = Mathf.Clamp01(t);
                return t * t * t * (t * (t * 6f - 15f) + 10f);
            }

            private static Vector2 RoomPoint(Vector2Int room, float spacing)
            {
                return new Vector2(room.x * spacing, room.y * spacing);
            }

            private void AddFlatRect(Vector2 center, float halfWidth, float halfDepth, float falloff, float targetHeight, int priority)
            {
                flatPatches.Add(FlatPatch.Rect(center, Mathf.Max(0.1f, halfWidth), Mathf.Max(0.1f, halfDepth), Mathf.Max(0.1f, falloff), targetHeight, priority));
            }

            private void AddFlatCircle(Vector2 center, float radius, float falloff, float targetHeight, int priority)
            {
                flatPatches.Add(FlatPatch.Circle(center, Mathf.Max(0.1f, radius), Mathf.Max(0.1f, falloff), targetHeight, priority));
            }

            private void AddFlatSegment(Vector2 start, Vector2 end, float halfWidth, float falloff, float targetHeight, int priority)
            {
                flatPatches.Add(FlatPatch.Segment(start, end, Mathf.Max(0.1f, halfWidth), Mathf.Max(0.1f, falloff), targetHeight, priority));
            }

            private void AddTerrainCutoutRamp(Vector2 portal, Vector2 underground, float halfWidth)
            {
                terrainCutouts.Add(TerrainCutout.DirectionalRamp(
                    portal,
                    underground,
                    Mathf.Max(0.1f, halfWidth),
                    ArenaGenerator.GetAllOutWarSubfloorThresholdRampOverlap()));
            }

            private void AddTerrainCutoutHillMouth(Vector2 outerApproach, Vector2 innerMouth, float outerHalfWidth, float innerHalfWidth)
            {
                terrainCutouts.Add(TerrainCutout.HillMouth(
                    outerApproach,
                    innerMouth,
                    Mathf.Max(0.1f, outerHalfWidth),
                    Mathf.Max(0.1f, innerHalfWidth)));
            }

            private static TerrainSettings CreateSettings(int seed, float spacing, AllOutWarMapStyle mapStyle)
            {
                var random = new System.Random(seed & 0x7fffffff);
                random.NextDouble();
                var tacticalHillRoll = (float)random.NextDouble();
                var hilly = mapStyle == AllOutWarMapStyle.Hilly;
                RandomOffset(random);
                RandomOffset(random);
                RandomOffset(random);
                RandomOffset(random);
                var tacticalHillOffset = RandomOffset(random);
                random.NextDouble();
                var tacticalHillScale = hilly
                    ? Mathf.Lerp(Mathf.Max(76f, spacing * 6.2f), Mathf.Max(118f, spacing * 8.8f), (float)random.NextDouble())
                    : Mathf.Lerp(Mathf.Max(92f, spacing * 7.4f), Mathf.Max(152f, spacing * 11.2f), (float)random.NextDouble());

                return new TerrainSettings(
                    tacticalHillOffset,
                    tacticalHillScale,
                    hilly
                        ? Mathf.Lerp(5.75f, TacticalHillMaximumHeight, tacticalHillRoll)
                        : Mathf.Lerp(TacticalHillMinimumHeight, TacticalHillMaximumHeight, tacticalHillRoll));
            }

            private static Vector2 RandomOffset(System.Random random)
            {
                return new Vector2((float)random.NextDouble() * 4000f + 250f, (float)random.NextDouble() * 4000f + 250f);
            }

            private static Vector2 ToXZ(Vector3 position)
            {
                return new Vector2(position.x, position.z);
            }

            private static bool TryNormalize(Vector2 vector, out Vector2 normalized)
            {
                if (vector.sqrMagnitude <= 0.001f)
                {
                    normalized = Vector2.zero;
                    return false;
                }

                normalized = vector.normalized;
                return true;
            }

            private readonly struct TerrainSettings
            {
                public readonly Vector2 TacticalHillOffset;
                public readonly float TacticalHillScale;
                public readonly float TacticalHillPeakHeight;

                public TerrainSettings(
                    Vector2 tacticalHillOffset,
                    float tacticalHillScale,
                    float tacticalHillPeakHeight)
                {
                    TacticalHillOffset = tacticalHillOffset;
                    TacticalHillScale = tacticalHillScale;
                    TacticalHillPeakHeight = tacticalHillPeakHeight;
                }
            }

            private readonly struct TerrainOnlyHillRegion
            {
                public readonly Vector2 Center;
                public readonly float PeakHeight;
                public readonly float CrestRadius;
                public readonly float OuterRadius;
                public readonly float NoStructureRadius;
                public readonly float SpacingRadius;
                public readonly TerrainOnlyHillSizeClass SizeClass;
                public readonly Vector2 ElongationAxis;
                public readonly float CrestEccentricity;
                public readonly float CrestSharpness;

                public TerrainOnlyHillRegion(Vector2 center, float peakHeight, float crestRadius, float outerRadius, float noStructureRadius, float spacingRadius, TerrainOnlyHillSizeClass sizeClass)
                    : this(center, peakHeight, crestRadius, outerRadius, noStructureRadius, spacingRadius, sizeClass, Vector2.right, 0f, 0f)
                {
                }

                public TerrainOnlyHillRegion(
                    Vector2 center,
                    float peakHeight,
                    float crestRadius,
                    float outerRadius,
                    float noStructureRadius,
                    float spacingRadius,
                    TerrainOnlyHillSizeClass sizeClass,
                    Vector2 elongationAxis,
                    float crestEccentricity,
                    float crestSharpness)
                {
                    Center = center;
                    PeakHeight = Mathf.Max(0f, peakHeight);
                    CrestRadius = Mathf.Max(0.1f, crestRadius);
                    OuterRadius = Mathf.Max(CrestRadius + 0.1f, outerRadius);
                    NoStructureRadius = Mathf.Max(OuterRadius, noStructureRadius);
                    SpacingRadius = Mathf.Max(NoStructureRadius, spacingRadius);
                    SizeClass = sizeClass;
                    ElongationAxis = elongationAxis.sqrMagnitude > 0.0001f ? elongationAxis.normalized : Vector2.right;
                    CrestEccentricity = Mathf.Clamp(crestEccentricity, 0f, 0.45f);
                    CrestSharpness = Mathf.Clamp01(crestSharpness);
                }

                public float DirectionalCrestRadius(Vector2 outwardDirection)
                {
                    if (CrestEccentricity <= 0.0001f || outwardDirection.sqrMagnitude <= 0.0001f)
                    {
                        return CrestRadius;
                    }

                    var alongness = Vector2.Dot(outwardDirection.normalized, ElongationAxis);
                    return CrestRadius * (1f - CrestEccentricity * alongness * alongness);
                }

                public float SampleHeight(Vector2 point)
                {
                    var delta = point - Center;
                    var distance = delta.magnitude;
                    if (distance >= OuterRadius)
                    {
                        return 0f;
                    }

                    // The crest is an ellipse (shorter across the elongation axis) while the
                    // shoulder run stays the same length in every direction, so the hill is
                    // asymmetric without ever steepening past the designed slope.
                    var directionalCrest = CrestRadius;
                    if (CrestEccentricity > 0.0001f && distance > 0.0001f)
                    {
                        var alongness = Vector2.Dot(delta / distance, ElongationAxis);
                        directionalCrest = CrestRadius * (1f - CrestEccentricity * alongness * alongness);
                    }

                    var shoulderRun = Mathf.Max(0.1f, OuterRadius - CrestRadius);
                    var crestEdgeHeight = PeakHeight * TacticalHillCrestEdgeHeightScale;
                    if (distance <= directionalCrest)
                    {
                        // Sharpness shrinks the flat top toward a rounded peak; SmootherStep
                        // keeps the apex and the crest edge slope-continuous.
                        var plateauRadius = directionalCrest * Mathf.Lerp(1f, 0.45f, CrestSharpness);
                        var crestT = SmootherStep(Mathf.Clamp01(distance / Mathf.Max(0.1f, plateauRadius)));
                        return Mathf.Lerp(PeakHeight, crestEdgeHeight, crestT);
                    }

                    var shoulderT = Mathf.Clamp01((distance - directionalCrest) / shoulderRun);
                    var shoulder = Mathf.Cos(shoulderT * Mathf.PI) * 0.5f + 0.5f;
                    return crestEdgeHeight * shoulder;
                }
            }

            private readonly struct FlatPatch
            {
                private readonly Vector2 center;
                private readonly Vector2 halfSize;
                private readonly Vector2 start;
                private readonly Vector2 end;
                private readonly float radius;
                private readonly float falloff;
                private readonly float targetHeight;
                private readonly int priority;
                private readonly bool circular;
                private readonly bool segment;
                private readonly Vector2 boundsMin;
                private readonly Vector2 boundsMax;

                private FlatPatch(Vector2 center, Vector2 halfSize, Vector2 start, Vector2 end, float radius, float falloff, float targetHeight, int priority, bool circular, bool segment)
                {
                    this.center = center;
                    this.halfSize = halfSize;
                    this.start = start;
                    this.end = end;
                    this.radius = radius;
                    this.falloff = falloff;
                    this.targetHeight = targetHeight;
                    this.priority = priority;
                    this.circular = circular;
                    this.segment = segment;

                    if (segment)
                    {
                        var influence = new Vector2(radius + falloff, radius + falloff);
                        boundsMin = Vector2.Min(start, end) - influence;
                        boundsMax = Vector2.Max(start, end) + influence;
                    }
                    else if (circular)
                    {
                        var influence = new Vector2(radius + falloff, radius + falloff);
                        boundsMin = center - influence;
                        boundsMax = center + influence;
                    }
                    else
                    {
                        var influence = halfSize + new Vector2(falloff, falloff);
                        boundsMin = center - influence;
                        boundsMax = center + influence;
                    }
                }

                public int Priority => priority;
                public Vector2 BoundsMin => boundsMin;
                public Vector2 BoundsMax => boundsMax;

                public static FlatPatch Rect(Vector2 center, float halfWidth, float halfDepth, float falloff, float targetHeight, int priority)
                {
                    return new FlatPatch(center, new Vector2(halfWidth, halfDepth), Vector2.zero, Vector2.zero, 0f, falloff, targetHeight, priority, false, false);
                }

                public static FlatPatch Circle(Vector2 center, float radius, float falloff, float targetHeight, int priority)
                {
                    return new FlatPatch(center, Vector2.zero, Vector2.zero, Vector2.zero, radius, falloff, targetHeight, priority, true, false);
                }

                public static FlatPatch Segment(Vector2 start, Vector2 end, float halfWidth, float falloff, float targetHeight, int priority)
                {
                    return new FlatPatch(Vector2.zero, Vector2.zero, start, end, halfWidth, falloff, targetHeight, priority, false, true);
                }

                public float Evaluate(Vector2 point)
                {
                    var distance = Distance(point);
                    if (distance <= 0f)
                    {
                        return 1f;
                    }

                    if (distance >= falloff)
                    {
                        return 0f;
                    }

                    var t = Mathf.Clamp01(distance / falloff);
                    return 1f - t * t * (3f - 2f * t);
                }

                public bool HasFullCoverage(Vector2 point)
                {
                    return Distance(point) <= 0f;
                }

                public bool MayAffect(Vector2 point)
                {
                    return point.x >= boundsMin.x &&
                        point.x <= boundsMax.x &&
                        point.y >= boundsMin.y &&
                        point.y <= boundsMax.y;
                }

                public float TargetHeight()
                {
                    return targetHeight;
                }

                private float RectDistance(Vector2 point)
                {
                    var delta = new Vector2(Mathf.Abs(point.x - center.x) - halfSize.x, Mathf.Abs(point.y - center.y) - halfSize.y);
                    var outside = new Vector2(Mathf.Max(0f, delta.x), Mathf.Max(0f, delta.y));
                    return outside.magnitude;
                }

                private float Distance(Vector2 point)
                {
                    if (circular)
                    {
                        return Vector2.Distance(point, center) - radius;
                    }

                    if (segment)
                    {
                        return DistanceToSegment(point, start, end) - radius;
                    }

                    return RectDistance(point);
                }
            }

            private readonly struct TerrainCutout
            {
                private readonly Vector2 center;
                private readonly Vector2 start;
                private readonly Vector2 end;
                private readonly float radius;
                private readonly float endRadius;
                private readonly bool segment;
                private readonly bool directional;
                private readonly bool tapered;
                private readonly float thresholdOverlap;

                private TerrainCutout(Vector2 center, Vector2 start, Vector2 end, float radius, float endRadius, bool segment, bool directional, bool tapered, float thresholdOverlap)
                {
                    this.center = center;
                    this.start = start;
                    this.end = end;
                    this.radius = radius;
                    this.endRadius = endRadius;
                    this.segment = segment;
                    this.directional = directional;
                    this.tapered = tapered;
                    this.thresholdOverlap = thresholdOverlap;
                }

                public static TerrainCutout Circle(Vector2 center, float radius)
                {
                    return new TerrainCutout(center, Vector2.zero, Vector2.zero, radius, radius, false, false, false, 0f);
                }

                public static TerrainCutout Segment(Vector2 start, Vector2 end, float halfWidth)
                {
                    return new TerrainCutout(Vector2.zero, start, end, halfWidth, halfWidth, true, false, false, 0f);
                }

                public static TerrainCutout DirectionalRamp(Vector2 portal, Vector2 underground, float halfWidth, float thresholdOverlap)
                {
                    return new TerrainCutout(Vector2.zero, portal, underground, halfWidth, halfWidth, true, true, false, Mathf.Max(0f, thresholdOverlap));
                }

                public static TerrainCutout HillMouth(Vector2 outerApproach, Vector2 innerMouth, float outerHalfWidth, float innerHalfWidth)
                {
                    return new TerrainCutout(Vector2.zero, outerApproach, innerMouth, outerHalfWidth, innerHalfWidth, true, true, true, 0f);
                }

                public bool Contains(Vector2 point)
                {
                    if (!segment)
                    {
                        return Vector2.Distance(point, center) <= radius;
                    }

                    if (!directional)
                    {
                        return DistanceToSegment(point, start, end) <= radius;
                    }

                    var segmentVector = end - start;
                    var lengthSqr = segmentVector.sqrMagnitude;
                    if (lengthSqr <= 0.001f)
                    {
                        return false;
                    }

                    var length = Mathf.Sqrt(lengthSqr);
                    var direction = segmentVector / length;
                    var localForward = Vector2.Dot(point - start, direction);
                    if (localForward < -thresholdOverlap || localForward > length)
                    {
                        return false;
                    }

                    var lateral = Mathf.Abs(Cross(point - start, direction));
                    if (!tapered)
                    {
                        return lateral <= radius;
                    }

                    var widthT = Mathf.Clamp01(localForward / length);
                    return lateral <= Mathf.Lerp(radius, endRadius, widthT);
                }
            }

            private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
            {
                var segmentVector = end - start;
                var lengthSqr = segmentVector.sqrMagnitude;
                if (lengthSqr <= 0.001f)
                {
                    return Vector2.Distance(point, start);
                }

                var t = Mathf.Clamp01(Vector2.Dot(point - start, segmentVector) / lengthSqr);
                return Vector2.Distance(point, start + segmentVector * t);
            }

            private static float Cross(Vector2 a, Vector2 b)
            {
                return a.x * b.y - a.y * b.x;
            }
        }

        private ArenaLayout BuildRoomGraph(System.Random random, int targetRooms, int gridRadius, float spacing)
        {
            var layout = new ArenaLayout { CellSpacing = spacing };
            var current = Vector2Int.zero;
            layout.Rooms.Add(current);

            var guard = 0;
            while (layout.Rooms.Count < targetRooms && guard++ < targetRooms * 80)
            {
                var direction = Directions[random.Next(Directions.Length)];
                var candidate = current + direction;

                if (Mathf.Abs(candidate.x) <= gridRadius && Mathf.Abs(candidate.y) <= gridRadius)
                {
                    current = candidate;
                    layout.Rooms.Add(current);
                }

                if (random.NextDouble() < 0.28)
                {
                    current = Vector2Int.zero;
                }
            }

            foreach (var room in layout.Rooms)
            {
                layout.RoomCenters[room] = new Vector3(room.x * spacing, 0f, room.y * spacing);
            }

            return layout;
        }

        private void CreateRoom(ArenaTheme theme, Transform root, ArenaLayout layout, Vector2Int room, float roomSize, float corridorWidth, float wallHeight)
        {
            var center = layout.RoomCenters[room];
            var openSpawnRoom = IsAllOutWarSpawnOpenRoom(layout, room, center, roomSize * 0.75f);
            if (!TryCreateAllOutWarSplitRoomFloor(theme, root, layout, room, center, roomSize))
            {
                CreateRoomFloor(theme, root, center + new Vector3(0f, -0.1f, 0f), new Vector3(roomSize, 0.2f, roomSize), openSpawnRoom);
            }

            CreateRoomWalls(theme, root, layout, room, center, roomSize, corridorWidth, wallHeight);
        }

        private void CreateRoomFloor(ArenaTheme theme, Transform root, Vector3 position, Vector3 scale, bool forcePrimitive = false)
        {
            if (generatingAllOutWar)
            {
                CreateVisualRoomFloor(theme, root, position, scale, forcePrimitive);
                return;
            }

            if (!forcePrimitive && ArenaRoomFloorAsset.TryBuild(root, theme, position, scale, out var floor))
            {
                floor.name = "Room Floor";
                AddDestructibleIfNeeded(floor, floor.name, scale, theme.Floor);
                return;
            }

            CreateCube("Room Floor", root, position, scale, theme.Floor);
        }

        private void CreateVisualRoomFloor(ArenaTheme theme, Transform root, Vector3 position, Vector3 scale, bool forcePrimitive)
        {
            if (!forcePrimitive && ArenaRoomFloorAsset.TryBuild(root, theme, position, scale, out var floor))
            {
                floor.name = "Room Floor";
                RemoveCollidersInChildren(floor);
                ApplyAllOutWarFloorMaterial(floor, theme);
                return;
            }

            CreateVisualCube("Room Floor", root, position, scale, GetFloorMaterial(theme));
        }

        private bool TryCreateAllOutWarSplitRoomFloor(ArenaTheme theme, Transform root, ArenaLayout layout, Vector2Int room, Vector3 center, float roomSize)
        {
            if (!generatingAllOutWar ||
                !TryGetAllOutWarSubfloorFootprintForRoom(layout, room, center, roomSize, out var footprint) ||
                !TryGetAllOutWarSubfloorRoomCutBounds(footprint, center, roomSize, out var cutMinX, out var cutMaxX, out var cutMinZ, out var cutMaxZ))
            {
                return false;
            }

            var roomMinX = center.x - roomSize * 0.5f;
            var roomMaxX = center.x + roomSize * 0.5f;
            var roomMinZ = center.z - roomSize * 0.5f;
            var roomMaxZ = center.z + roomSize * 0.5f;
            const float floorThickness = 0.2f;
            var y = center.y - floorThickness * 0.5f;
            var created = 0;
            created += CreateAllOutWarSplitRoomFloorPanel(theme, root, roomMinX, cutMinX, roomMinZ, roomMaxZ, y, floorThickness);
            created += CreateAllOutWarSplitRoomFloorPanel(theme, root, cutMaxX, roomMaxX, roomMinZ, roomMaxZ, y, floorThickness);
            created += CreateAllOutWarSplitRoomFloorPanel(theme, root, cutMinX, cutMaxX, roomMinZ, cutMinZ, y, floorThickness);
            created += CreateAllOutWarSplitRoomFloorPanel(theme, root, cutMinX, cutMaxX, cutMaxZ, roomMaxZ, y, floorThickness);
            return created > 0;
        }

        private int CreateAllOutWarSplitRoomFloorPanel(ArenaTheme theme, Transform root, float minX, float maxX, float minZ, float maxZ, float y, float thickness)
        {
            var width = maxX - minX;
            var depth = maxZ - minZ;
            if (width <= 0.08f || depth <= 0.08f)
            {
                return 0;
            }

            var position = new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f);
            var scale = new Vector3(width, thickness, depth);
            CreateVisualCube("Room Floor Split Panel", root, position, scale, GetFloorMaterial(theme));
            return 1;
        }

        private static bool TryGetAllOutWarSubfloorFootprintForRoom(
            ArenaLayout layout,
            Vector2Int room,
            Vector3 center,
            float roomSize,
            out SubfloorPortalFootprint footprint)
        {
            if (layout != null)
            {
                foreach (var tunnel in layout.TunnelRoutes)
                {
                    if (tunnel == null || tunnel.Kind != ArenaTunnelKind.Subfloor)
                    {
                        continue;
                    }

                    var fromFootprint = BuildAllOutWarSubfloorPortalFootprint(tunnel, true);
                    if ((tunnel.FromRoom == room || DoesAllOutWarSubfloorFootprintOverlapRoom(fromFootprint, center, roomSize)) &&
                        DoesAllOutWarSubfloorFootprintOverlapRoom(fromFootprint, center, roomSize))
                    {
                        footprint = fromFootprint;
                        return true;
                    }

                    var toFootprint = BuildAllOutWarSubfloorPortalFootprint(tunnel, false);
                    if ((tunnel.ToRoom == room || DoesAllOutWarSubfloorFootprintOverlapRoom(toFootprint, center, roomSize)) &&
                        DoesAllOutWarSubfloorFootprintOverlapRoom(toFootprint, center, roomSize))
                    {
                        footprint = toFootprint;
                        return true;
                    }
                }
            }

            footprint = default;
            return false;
        }

        private static bool DoesAllOutWarSubfloorFootprintOverlapRoom(SubfloorPortalFootprint footprint, Vector3 center, float roomSize)
        {
            return TryGetAllOutWarSubfloorRoomCutBounds(footprint, center, roomSize, out _, out _, out _, out _);
        }

        private static bool TryGetAllOutWarSubfloorRoomCutBounds(
            SubfloorPortalFootprint footprint,
            Vector3 roomCenter,
            float roomSize,
            out float cutMinX,
            out float cutMaxX,
            out float cutMinZ,
            out float cutMaxZ)
        {
            var side = footprint.Side;
            var points = new[]
            {
                footprint.ThresholdBack + side * footprint.VisualHalfWidth,
                footprint.ThresholdBack - side * footprint.VisualHalfWidth,
                footprint.RampEnd + side * footprint.VisualHalfWidth,
                footprint.RampEnd - side * footprint.VisualHalfWidth
            };

            var rawMinX = float.PositiveInfinity;
            var rawMaxX = float.NegativeInfinity;
            var rawMinZ = float.PositiveInfinity;
            var rawMaxZ = float.NegativeInfinity;
            foreach (var point in points)
            {
                rawMinX = Mathf.Min(rawMinX, point.x);
                rawMaxX = Mathf.Max(rawMaxX, point.x);
                rawMinZ = Mathf.Min(rawMinZ, point.z);
                rawMaxZ = Mathf.Max(rawMaxZ, point.z);
            }

            var roomMinX = roomCenter.x - roomSize * 0.5f;
            var roomMaxX = roomCenter.x + roomSize * 0.5f;
            var roomMinZ = roomCenter.z - roomSize * 0.5f;
            var roomMaxZ = roomCenter.z + roomSize * 0.5f;
            cutMinX = Mathf.Clamp(rawMinX, roomMinX, roomMaxX);
            cutMaxX = Mathf.Clamp(rawMaxX, roomMinX, roomMaxX);
            cutMinZ = Mathf.Clamp(rawMinZ, roomMinZ, roomMaxZ);
            cutMaxZ = Mathf.Clamp(rawMaxZ, roomMinZ, roomMaxZ);
            return cutMaxX - cutMinX > 0.08f && cutMaxZ - cutMinZ > 0.08f;
        }

        private void CreateRoomWalls(ArenaTheme theme, Transform root, ArenaLayout layout, Vector2Int room, Vector3 center, float roomSize, float doorwayWidth, float wallHeight)
        {
            const float thickness = 0.35f;
            var half = roomSize * 0.5f;
            var y = wallHeight * 0.5f;

            CreateDirectionalWall(theme, root, layout, room, Vector2Int.up, center, new Vector3(0f, y, half), roomSize, doorwayWidth, wallHeight, thickness);
            CreateDirectionalWall(theme, root, layout, room, Vector2Int.down, center, new Vector3(0f, y, -half), roomSize, doorwayWidth, wallHeight, thickness);
            CreateDirectionalWall(theme, root, layout, room, Vector2Int.right, center, new Vector3(half, y, 0f), roomSize, doorwayWidth, wallHeight, thickness);
            CreateDirectionalWall(theme, root, layout, room, Vector2Int.left, center, new Vector3(-half, y, 0f), roomSize, doorwayWidth, wallHeight, thickness);
        }

        private void CreateDirectionalWall(ArenaTheme theme, Transform root, ArenaLayout layout, Vector2Int room, Vector2Int direction, Vector3 center, Vector3 offset, float roomSize, float doorwayWidth, float wallHeight, float thickness)
        {
            var wallCenter = center + offset;
            var horizontal = direction == Vector2Int.up || direction == Vector2Int.down;
            var wallStart = wallCenter + (horizontal ? Vector3.left : Vector3.back) * (roomSize * 0.5f);
            var wallEnd = wallCenter + (horizontal ? Vector3.right : Vector3.forward) * (roomSize * 0.5f);

            if (IsInAllOutWarSpawnOpenZone(layout, wallCenter, thickness) ||
                IsSegmentInAllOutWarSpawnOpenZone(layout, wallStart, wallEnd, thickness + 0.8f))
            {
                return;
            }

            var hasDoor = layout.Rooms.Contains(room + direction);
            var hasGate = HasGate(layout, room, direction);
            if (hasDoor && layout.AreRoomsInSameClearing(room, room + direction))
            {
                return;
            }

            var hasTunnelOpening = TryGetAllOutWarSubfloorWallOpeningWidth(layout, room, direction, doorwayWidth, roomSize, out var tunnelOpeningWidth);
            var hasHillsideOpening = !hasDoor && !hasGate && !hasTunnelOpening && ShouldOpenWallToHillTerrain(room, direction);
            var hasOpening = hasDoor || hasGate || hasTunnelOpening || hasHillsideOpening;
            if (!hasOpening && IsAllOutWarDomeEdgeOpening(layout, room, direction))
            {
                return;
            }

            var structuralThickness = thickness * 1.35f;
            var halfRoom = roomSize * 0.5f;
            var halfDoorway = (hasTunnelOpening ? tunnelOpeningWidth : doorwayWidth) * 0.5f;

            if (!hasOpening)
            {
                // Perpendicular walls butt at room corners instead of overlapping: the
                // X-spanning walls own the corner volume and the Z-spanning walls pull back
                // half a wall thickness per end, so corner damage cannot expose embedded
                // material from the crossing wall.
                var scale = horizontal
                    ? new Vector3(roomSize, wallHeight, structuralThickness)
                    : new Vector3(structuralThickness, wallHeight, roomSize - structuralThickness);
                CreateCube("Arena Wall", root, wallCenter, scale, theme.Wall);
                return;
            }

            // Opening pillars are structuralThickness * 1.25f wide and centered at ±halfDoorway,
            // so the flanking segments stop at the pillar's outer face instead of its center.
            var pillarHalfWidth = structuralThickness * 0.625f;
            var segmentStart = halfDoorway + pillarHalfWidth;

            if (horizontal)
            {
                var segmentLength = halfRoom - segmentStart;
                if (segmentLength <= 0.1f)
                {
                    return;
                }

                var leftOffset = offset + new Vector3(-(segmentStart + segmentLength * 0.5f), 0f, 0f);
                var rightOffset = offset + new Vector3(segmentStart + segmentLength * 0.5f, 0f, 0f);
                var scale = new Vector3(segmentLength, wallHeight, structuralThickness);
                CreateCube("Arena Wall Door Segment", root, center + leftOffset, scale, theme.Wall);
                CreateCube("Arena Wall Door Segment", root, center + rightOffset, scale, theme.Wall);
                CreateOpeningPillars(theme, root, center + offset, true, halfDoorway, wallHeight, structuralThickness);
            }
            else
            {
                // Z-spanning door segments stop half a thickness shy of the room corner so
                // they butt against the X-spanning walls instead of crossing through them.
                var outerEnd = halfRoom - structuralThickness * 0.5f;
                var segmentLength = outerEnd - segmentStart;
                if (segmentLength <= 0.1f)
                {
                    return;
                }

                var lowerOffset = offset + new Vector3(0f, 0f, -(segmentStart + segmentLength * 0.5f));
                var upperOffset = offset + new Vector3(0f, 0f, segmentStart + segmentLength * 0.5f);
                var scale = new Vector3(structuralThickness, wallHeight, segmentLength);
                CreateCube("Arena Wall Door Segment", root, center + lowerOffset, scale, theme.Wall);
                CreateCube("Arena Wall Door Segment", root, center + upperOffset, scale, theme.Wall);
                CreateOpeningPillars(theme, root, center + offset, false, halfDoorway, wallHeight, structuralThickness);
            }
        }

        // Most walls facing open hill terrain become hallway-style doorways so the hills are
        // reachable from the rooms instead of fenced off; a deterministic minority stay
        // solid so layouts still vary.
        private bool ShouldOpenWallToHillTerrain(Vector2Int room, Vector2Int direction)
        {
            if (!generatingAllOutWar || activeAllOutWarTerrainProfile == null)
            {
                return false;
            }

            if (!activeAllOutWarTerrainProfile.IsTerrainOnlyHillNoBuildCell(room + direction))
            {
                return false;
            }

            var hash = Mathf.Abs(room.x * 73856093 ^ room.y * 19349663 ^ (direction.x * 3 + direction.y * 7) * 83492791);
            return hash % 100 < 70;
        }

        private static bool TryGetAllOutWarSubfloorWallOpeningWidth(
            ArenaLayout layout,
            Vector2Int room,
            Vector2Int direction,
            float doorwayWidth,
            float roomSize,
            out float openingWidth)
        {
            openingWidth = doorwayWidth;
            if (layout == null || layout.TunnelRoutes.Count == 0)
            {
                return false;
            }

            foreach (var tunnel in layout.TunnelRoutes)
            {
                if (tunnel == null || tunnel.Kind != ArenaTunnelKind.Subfloor)
                {
                    continue;
                }

                var matchesFrom = tunnel.FromRoom == room &&
                    tunnel.FromDirection == direction &&
                    tunnel.FromEntranceMode == ArenaTunnelEntranceMode.WallPortal;
                var matchesTo = tunnel.ToRoom == room &&
                    tunnel.ToDirection == direction &&
                    tunnel.ToEntranceMode == ArenaTunnelEntranceMode.WallPortal;
                if (!matchesFrom && !matchesTo)
                {
                    continue;
                }

                openingWidth = Mathf.Clamp(
                    Mathf.Max(doorwayWidth, tunnel.Width + 1.1f),
                    doorwayWidth,
                    Mathf.Max(doorwayWidth, roomSize - 1.25f));
                return true;
            }

            return false;
        }

        private void CreateOpeningPillars(ArenaTheme theme, Transform root, Vector3 wallCenter, bool horizontalWall, float halfDoorway, float wallHeight, float wallThickness)
        {
            var pillarDepth = wallThickness * 1.9f;
            var pillarWidth = wallThickness * 1.25f;

            if (horizontalWall)
            {
                var scale = new Vector3(pillarWidth, wallHeight, pillarDepth);
                CreateOpeningPillar(theme, root, wallCenter + new Vector3(-halfDoorway, 0f, 0f), scale, Vector3.right);
                CreateOpeningPillar(theme, root, wallCenter + new Vector3(halfDoorway, 0f, 0f), scale, Vector3.left);
                return;
            }

            var verticalScale = new Vector3(pillarDepth, wallHeight, pillarWidth);
            CreateOpeningPillar(theme, root, wallCenter + new Vector3(0f, 0f, -halfDoorway), verticalScale, Vector3.forward);
            CreateOpeningPillar(theme, root, wallCenter + new Vector3(0f, 0f, halfDoorway), verticalScale, Vector3.back);
        }

        private GameObject CreateOpeningPillar(ArenaTheme theme, Transform root, Vector3 position, Vector3 scale, Vector3 biteDirection)
        {
            const string pillarName = "Room Corridor Opening Pillar";
            if (ArenaCornerPostAsset.TryBuild(root, position, scale, out var pillar))
            {
                pillar.name = pillarName;
                AddDestructibleIfNeeded(pillar, pillarName, scale, theme.Pillar, DestructibleDamageProfile.CornerPillar, biteDirection);
                CreateWallBaseAccentIfNeeded(pillarName, root, position, scale, Quaternion.identity);
                return pillar;
            }

            return CreateRawCube(pillarName, root, position, scale, theme.Pillar, DestructibleDamageProfile.CornerPillar, biteDirection);
        }

        private static bool IsAllOutWarDomeEdgeOpening(ArenaLayout layout, Vector2Int room, Vector2Int direction)
        {
            if (layout == null || layout.CircularRadius <= 0f)
            {
                return false;
            }

            var roomVector = new Vector2(room.x, room.y);
            if (roomVector.sqrMagnitude < 0.01f)
            {
                return false;
            }

            var directionVector = new Vector2(direction.x, direction.y);
            if (Vector2.Dot(roomVector.normalized, directionVector) <= 0.45f)
            {
                return false;
            }

            var spacing = Mathf.Max(1f, layout.CellSpacing);
            var edgeBand = Mathf.Max(1.25f, layout.CircularRadius / spacing - 2.2f);
            return roomVector.magnitude >= edgeBand;
        }

        private static bool IsAllOutWarSpawnOpenRoom(ArenaLayout layout, Vector2Int room)
        {
            if (layout == null)
            {
                return false;
            }

            return layout.RoomCenters.TryGetValue(room, out var center) && IsAllOutWarSpawnOpenRoom(layout, room, center);
        }

        private static bool IsAllOutWarSpawnOpenRoom(ArenaLayout layout, Vector2Int room, Vector3 roomCenter, float extraRadius = 0f)
        {
            return IsInAllOutWarSpawnOpenZone(layout, roomCenter, extraRadius);
        }

        private static bool IsInAllOutWarSpawnOpenZone(ArenaLayout layout, Vector3 position, float extraRadius = 0f)
        {
            if (layout == null || layout.ArmySpawnRegions.Count == 0)
            {
                return false;
            }

            foreach (var region in layout.ArmySpawnRegions)
            {
                if (region == null)
                {
                    continue;
                }

                if (region.ContainsPoint(position, extraRadius))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSegmentInAllOutWarSpawnOpenZone(ArenaLayout layout, Vector3 start, Vector3 end, float extraRadius = 0f)
        {
            if (layout == null || layout.ArmySpawnRegions.Count == 0)
            {
                return false;
            }

            foreach (var region in layout.ArmySpawnRegions)
            {
                if (region == null)
                {
                    continue;
                }

                if (region.IntersectsSegment(start, end, extraRadius))
                {
                    return true;
                }
            }

            return false;
        }

        private void CreateCorridor(ArenaTheme theme, Transform root, Vector3 from, Vector3 to, Vector2Int direction, float roomSize, float corridorLength, float corridorWidth, float wallHeight)
        {
            CreateCorridor(theme, root, null, Vector2Int.zero, Vector2Int.zero, direction, roomSize, corridorLength, corridorWidth, wallHeight, from, to);
        }

        private void CreateCorridor(ArenaTheme theme, Transform root, ArenaLayout layout, Vector2Int fromRoom, Vector2Int toRoom, Vector2Int direction, float roomSize, float corridorLength, float corridorWidth, float wallHeight)
        {
            CreateCorridor(theme, root, layout, fromRoom, toRoom, direction, roomSize, corridorLength, corridorWidth, wallHeight, layout.RoomCenters[fromRoom], layout.RoomCenters[toRoom]);
        }

        private void CreateCorridor(ArenaTheme theme, Transform root, ArenaLayout layout, Vector2Int fromRoom, Vector2Int toRoom, Vector2Int direction, float roomSize, float corridorLength, float corridorWidth, float wallHeight, Vector3 from, Vector3 to)
        {
            const float thickness = 0.35f;
            // The corridor spans between the two room wall planes, which are also the center planes
            // of the doorway opening pillars (depth structuralThickness * 1.9f). Pull both corridor
            // wall ends back to the pillars' corridor-side faces so they do not penetrate the pillars.
            var openingPillarDepth = thickness * 1.35f * 1.9f;
            var corridorWallLength = Mathf.Max(0.1f, corridorLength - openingPillarDepth);
            var center = (from + to) * 0.5f;
            var vertical = direction == Vector2Int.up || direction == Vector2Int.down;
            var openClearingLink = layout != null && layout.AreRoomsInSameClearing(fromRoom, toRoom);
            var openSpawnZoneLink = IsInAllOutWarSpawnOpenZone(layout, center, corridorWidth * 0.5f) ||
                IsSegmentInAllOutWarSpawnOpenZone(layout, from, to, corridorWidth * 0.5f);
            var openFloorLink = openClearingLink || openSpawnZoneLink;
            var floorLength = GetCorridorFloorLength(corridorLength, generatingAllOutWar);

            if (vertical)
            {
                var floorScale = openFloorLink
                    ? new Vector3(roomSize, 0.2f, floorLength)
                    : new Vector3(corridorWidth, 0.2f, floorLength);
                var floorPosition = center + new Vector3(0f, -0.1f, 0f);
                CreateFloorCube(theme, root, "Corridor Floor", floorPosition, floorScale);
                if (openFloorLink)
                {
                    return;
                }

                CreateCube("Corridor Wall", root, center + new Vector3(-corridorWidth * 0.5f, wallHeight * 0.5f, 0f), new Vector3(thickness, wallHeight, corridorWallLength), theme.Wall);
                CreateCube("Corridor Wall", root, center + new Vector3(corridorWidth * 0.5f, wallHeight * 0.5f, 0f), new Vector3(thickness, wallHeight, corridorWallLength), theme.Wall);
                AddCorridorWallPanel(theme, root, center + new Vector3(-corridorWidth * 0.5f + 0.08f, wallHeight * 0.68f, 0f), Quaternion.Euler(0f, 90f, 0f));
                AddCorridorWallPanel(theme, root, center + new Vector3(corridorWidth * 0.5f - 0.08f, wallHeight * 0.68f, 0f), Quaternion.Euler(0f, -90f, 0f));
                CreateFloorTrimChannel(theme, root, center + new Vector3(0f, 0.031f, 0f), new Vector3(0.055f, 0.018f, corridorLength), false, 0.018f);
            }
            else
            {
                var floorScale = openFloorLink
                    ? new Vector3(floorLength, 0.2f, roomSize)
                    : new Vector3(floorLength, 0.2f, corridorWidth);
                var floorPosition = center + new Vector3(0f, -0.1f, 0f);
                CreateFloorCube(theme, root, "Corridor Floor", floorPosition, floorScale);
                if (openFloorLink)
                {
                    return;
                }

                CreateCube("Corridor Wall", root, center + new Vector3(0f, wallHeight * 0.5f, -corridorWidth * 0.5f), new Vector3(corridorWallLength, wallHeight, thickness), theme.Wall);
                CreateCube("Corridor Wall", root, center + new Vector3(0f, wallHeight * 0.5f, corridorWidth * 0.5f), new Vector3(corridorWallLength, wallHeight, thickness), theme.Wall);
                AddCorridorWallPanel(theme, root, center + new Vector3(0f, wallHeight * 0.68f, -corridorWidth * 0.5f + 0.08f), Quaternion.identity);
                AddCorridorWallPanel(theme, root, center + new Vector3(0f, wallHeight * 0.68f, corridorWidth * 0.5f - 0.08f), Quaternion.Euler(0f, 180f, 0f));
                CreateFloorTrimChannel(theme, root, center + new Vector3(0f, 0.031f, 0f), new Vector3(corridorLength, 0.018f, 0.055f), false, 0.018f);
            }
        }

        private void AddCorridorWallPanel(ArenaTheme theme, Transform root, Vector3 position, Quaternion rotation)
        {
            ArenaWallPanelAsset.TryBuild(root, theme, position, rotation);
        }

        private void CreateAllOutWarTunnels(ArenaTheme theme, Transform root, ArenaLayout layout, float roomSize, float corridorLength, float corridorWidth, float wallHeight)
        {
            if (theme == null || root == null || layout == null || layout.TunnelRoutes.Count == 0)
            {
                return;
            }

            foreach (var tunnel in layout.TunnelRoutes)
            {
                if (tunnel == null)
                {
                    continue;
                }

                var tunnelRoot = new GameObject(tunnel.Kind == ArenaTunnelKind.HillCut ? "All Out War Hill-Cut Tunnel" : "All Out War Subfloor Tunnel");
                tunnelRoot.transform.SetParent(root, false);

                CreateAllOutWarTunnelPortal(theme, tunnelRoot.transform, tunnel, tunnel.FromPortal, tunnel.FromWorldDirection);
                CreateAllOutWarTunnelPortal(theme, tunnelRoot.transform, tunnel, tunnel.ToPortal, tunnel.ToWorldDirectionValue);
                if (tunnel.Kind == ArenaTunnelKind.Subfloor)
                {
                    CreateAllOutWarTunnelThreshold(theme, tunnelRoot.transform, tunnel, tunnel.FromPortal, tunnel.FromWorldDirection);
                    CreateAllOutWarTunnelThreshold(theme, tunnelRoot.transform, tunnel, tunnel.ToPortal, tunnel.ToWorldDirectionValue);
                    CreateAllOutWarSubfloorTunnelTerrainCollar(theme, tunnelRoot.transform, tunnel, tunnel.FromPortal, tunnel.FromWorldDirection);
                    CreateAllOutWarSubfloorTunnelTerrainCollar(theme, tunnelRoot.transform, tunnel, tunnel.ToPortal, tunnel.ToWorldDirectionValue);
                }

                CreateAllOutWarContinuousTunnel(theme, tunnelRoot.transform, tunnel);
                if (tunnel.Kind == ArenaTunnelKind.HillCut && activeAllOutWarTerrainProfile != null)
                {
                    CreateAllOutWarHillTunnelMouthSeams(theme, tunnelRoot.transform, tunnel, activeAllOutWarTerrainProfile);
                }
            }
        }

        private readonly struct TunnelSweepFrame
        {
            public readonly Vector3 Center;
            public readonly Vector3 Tangent;
            public readonly Vector3 Side;
            public readonly Vector3 Up;

            public TunnelSweepFrame(Vector3 center, Vector3 tangent, Vector3 side, Vector3 up)
            {
                Center = center;
                Tangent = tangent;
                Side = side;
                Up = up;
            }
        }

        private void CreateAllOutWarContinuousTunnel(ArenaTheme theme, Transform root, ArenaTunnelRoute tunnel)
        {
            var points = BuildAllOutWarTunnelSweepPoints(tunnel);
            if (points.Count < 2)
            {
                return;
            }

            var frames = BuildAllOutWarTunnelSweepFrames(points, tunnel);
            var floorMaterial = GetFloorMaterial(theme);
            var wallMaterial = ResolveTunnelWallMaterial(theme, tunnel.Kind);
            var neonMaterial = theme.NeonA != null ? theme.NeonA : theme.NeonB;

            CreateTunnelMeshObject(
                "All Out War Tunnel Floor",
                root,
                CreateTunnelFloorSweepMesh("All Out War Tunnel Floor Mesh", frames, tunnel.Width),
                floorMaterial,
                true,
                StylizedOutlineCategory.Floor);
            CreateTunnelMeshObject(
                "All Out War Tunnel Continuous Shell",
                root,
                CreateTunnelOctagonalShellMesh("All Out War Tunnel Continuous Shell Mesh", frames, tunnel.Width, tunnel.Height),
                wallMaterial,
                false,
                StylizedOutlineCategory.Wall);
            CreateTunnelMeshObject(
                "All Out War Tunnel Side Wall",
                root,
                CreateTunnelSideCollisionMesh("All Out War Tunnel Side Collision Mesh", frames, tunnel.Width, tunnel.Height),
                wallMaterial,
                true,
                StylizedOutlineCategory.Wall);

            if (neonMaterial != null)
            {
                CreateTunnelMeshObject(
                    "All Out War Tunnel Octagon Trace Rings",
                    root,
                    CreateTunnelOctagonTraceRingMesh("All Out War Tunnel Octagon Trace Rings Mesh", frames, tunnel.Width, tunnel.Height),
                    neonMaterial,
                    false,
                    StylizedOutlineCategory.None);
            }
        }

        private static List<Vector3> BuildAllOutWarTunnelSweepPoints(ArenaTunnelRoute tunnel)
        {
            var points = new List<Vector3>();
            if (tunnel == null || tunnel.Waypoints.Count == 0)
            {
                return points;
            }

            var waypoints = new List<Vector3>(tunnel.Waypoints);
            points.Add(waypoints[0]);
            var cornerRadius = Mathf.Max(1.1f, tunnel.Width * 0.72f);
            for (var i = 1; i < waypoints.Count - 1; i++)
            {
                var previous = waypoints[i - 1];
                var current = waypoints[i];
                var next = waypoints[i + 1];
                var inVector = current - previous;
                var outVector = next - current;
                var inLength = inVector.magnitude;
                var outLength = outVector.magnitude;
                if (inLength <= 0.01f || outLength <= 0.01f)
                {
                    AddTunnelLineSamples(points, current, tunnel.Width);
                    continue;
                }

                var inDirection = inVector / inLength;
                var outDirection = outVector / outLength;
                if (Vector3.Dot(inDirection, outDirection) > 0.985f)
                {
                    AddTunnelLineSamples(points, current, tunnel.Width);
                    continue;
                }

                var radius = Mathf.Min(cornerRadius, inLength * 0.38f, outLength * 0.38f);
                var entry = current - inDirection * radius;
                var exit = current + outDirection * radius;
                AddTunnelLineSamples(points, entry, tunnel.Width);

                const int curveSamples = 6;
                for (var sample = 1; sample <= curveSamples; sample++)
                {
                    var t = sample / (float)curveSamples;
                    var oneMinusT = 1f - t;
                    var curvePoint = oneMinusT * oneMinusT * entry +
                        2f * oneMinusT * t * current +
                        t * t * exit;
                    AddUniqueTunnelPoint(points, curvePoint);
                }
            }

            AddTunnelLineSamples(points, waypoints[^1], tunnel.Width);
            return points;
        }

        private static void AddTunnelLineSamples(List<Vector3> points, Vector3 target, float tunnelWidth)
        {
            if (points == null || points.Count == 0)
            {
                points?.Add(target);
                return;
            }

            var start = points[^1];
            var distance = Vector3.Distance(start, target);
            var samples = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(1.1f, tunnelWidth * 0.48f)));
            for (var i = 1; i <= samples; i++)
            {
                AddUniqueTunnelPoint(points, Vector3.Lerp(start, target, i / (float)samples));
            }
        }

        private static void AddUniqueTunnelPoint(List<Vector3> points, Vector3 point)
        {
            if (points.Count == 0 || Vector3.Distance(points[^1], point) > 0.025f)
            {
                points.Add(point);
            }
        }

        private static List<TunnelSweepFrame> BuildAllOutWarTunnelSweepFrames(List<Vector3> points, ArenaTunnelRoute tunnel)
        {
            var frames = new List<TunnelSweepFrame>();
            if (points == null || points.Count == 0 || tunnel == null)
            {
                return frames;
            }

            for (var i = 0; i < points.Count; i++)
            {
                Vector3 tangent;
                if (i == 0)
                {
                    tangent = tunnel.Kind == ArenaTunnelKind.Subfloor ? tunnel.FromWorldDirection : points[Mathf.Min(1, points.Count - 1)] - points[0];
                }
                else if (i == points.Count - 1)
                {
                    tangent = tunnel.Kind == ArenaTunnelKind.Subfloor ? -tunnel.ToWorldDirectionValue : points[i] - points[i - 1];
                }
                else
                {
                    tangent = points[i + 1] - points[i - 1];
                }

                if (tangent.sqrMagnitude <= 0.001f)
                {
                    tangent = Vector3.forward;
                }

                tangent.Normalize();
                var side = Vector3.Cross(Vector3.up, tangent);
                if (side.sqrMagnitude <= 0.001f)
                {
                    side = Vector3.right;
                }

                side.Normalize();
                var up = Vector3.Cross(tangent, side);
                if (i == 0 || i == points.Count - 1)
                {
                    up = Vector3.up;
                    side = Vector3.Cross(up, tangent);
                    if (side.sqrMagnitude <= 0.001f)
                    {
                        side = Vector3.right;
                    }

                    side.Normalize();
                }
                else
                {
                    up.Normalize();
                }

                frames.Add(new TunnelSweepFrame(points[i], tangent, side, up));
            }

            return frames;
        }

        private static Vector3[] BuildTunnelOctagonRing(TunnelSweepFrame frame, float width, float height)
        {
            var local = BuildTunnelPortalOctagon(width, height, 0f);
            var ring = new Vector3[local.Length];
            for (var i = 0; i < local.Length; i++)
            {
                ring[i] = frame.Center + frame.Side * local[i].x + frame.Up * local[i].y;
            }

            return ring;
        }

        private static Mesh CreateTunnelOctagonTraceRingMesh(string name, List<TunnelSweepFrame> frames, float width, float height)
        {
            var distances = BuildTunnelFrameDistances(frames);
            var totalDistance = distances.Length > 0 ? distances[^1] : 0f;
            var ringCount = EstimateTunnelTraceRingCount(totalDistance);
            var vertices = new List<Vector3>(ringCount * 32);
            var triangles = new List<int>(ringCount * 96);

            if (frames == null || frames.Count < 2 || totalDistance <= 0.01f)
            {
                return BuildTunnelMesh(name, vertices, triangles);
            }

            var firstDistance = AllOutWarTunnelTracePortalMargin;
            var lastDistance = totalDistance - AllOutWarTunnelTracePortalMargin;
            if (lastDistance < firstDistance)
            {
                firstDistance = totalDistance * 0.5f;
                lastDistance = firstDistance;
            }

            for (var distance = firstDistance; distance <= lastDistance + 0.001f; distance += AllOutWarTunnelTraceRingSpacing)
            {
                var frame = SampleTunnelFrameAtDistance(frames, distances, distance);
                AddTunnelTraceRing(vertices, triangles, frame, width, height, AllOutWarTunnelTraceRingThickness);
            }

            return BuildTunnelMesh(name, vertices, triangles);
        }

        private static int EstimateTunnelTraceRingCount(float totalDistance)
        {
            if (totalDistance <= 0.01f)
            {
                return 0;
            }

            var usableDistance = totalDistance - AllOutWarTunnelTracePortalMargin * 2f;
            if (usableDistance <= 0f)
            {
                return 1;
            }

            return Mathf.Max(1, Mathf.FloorToInt(usableDistance / AllOutWarTunnelTraceRingSpacing) + 1);
        }

        private static float[] BuildTunnelFrameDistances(List<TunnelSweepFrame> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                return System.Array.Empty<float>();
            }

            var distances = new float[frames.Count];
            for (var i = 1; i < frames.Count; i++)
            {
                distances[i] = distances[i - 1] + Vector3.Distance(frames[i - 1].Center, frames[i].Center);
            }

            return distances;
        }

        private static TunnelSweepFrame SampleTunnelFrameAtDistance(List<TunnelSweepFrame> frames, float[] distances, float targetDistance)
        {
            if (frames == null || frames.Count == 0)
            {
                return new TunnelSweepFrame(Vector3.zero, Vector3.forward, Vector3.right, Vector3.up);
            }

            if (frames.Count == 1 || distances == null || distances.Length != frames.Count)
            {
                return frames[0];
            }

            targetDistance = Mathf.Clamp(targetDistance, 0f, distances[^1]);
            var segment = 0;
            while (segment < distances.Length - 2 && distances[segment + 1] < targetDistance)
            {
                segment++;
            }

            var segmentLength = distances[segment + 1] - distances[segment];
            var t = segmentLength > 0.001f ? (targetDistance - distances[segment]) / segmentLength : 0f;
            return InterpolateTunnelFrame(frames[segment], frames[segment + 1], t);
        }

        private static TunnelSweepFrame InterpolateTunnelFrame(TunnelSweepFrame from, TunnelSweepFrame to, float t)
        {
            var center = Vector3.Lerp(from.Center, to.Center, t);
            var tangent = Vector3.Lerp(from.Tangent, to.Tangent, t);
            if (tangent.sqrMagnitude <= 0.001f)
            {
                tangent = to.Tangent.sqrMagnitude > 0.001f ? to.Tangent : Vector3.forward;
            }

            tangent.Normalize();
            var side = Vector3.Lerp(from.Side, to.Side, t);
            side -= tangent * Vector3.Dot(side, tangent);
            if (side.sqrMagnitude <= 0.001f)
            {
                side = Vector3.Cross(Vector3.up, tangent);
            }

            if (side.sqrMagnitude <= 0.001f)
            {
                side = Vector3.right;
            }

            side.Normalize();
            var up = Vector3.Cross(tangent, side);
            if (up.sqrMagnitude <= 0.001f)
            {
                up = Vector3.up;
            }

            up.Normalize();
            return new TunnelSweepFrame(center, tangent, side, up);
        }

        private static void AddTunnelTraceRing(
            List<Vector3> vertices,
            List<int> triangles,
            TunnelSweepFrame frame,
            float width,
            float height,
            float thickness)
        {
            var local = BuildTunnelPortalOctagon(width, height, 0f);
            var localCenter = new Vector2(0f, height * 0.5f);
            var inwardThickness = Mathf.Max(0.005f, thickness);
            for (var edge = 0; edge < local.Length; edge++)
            {
                var next = (edge + 1) % local.Length;
                var start = local[edge];
                var end = local[next];
                var inward = localCenter - (start + end) * 0.5f;
                if (inward.sqrMagnitude <= 0.0001f)
                {
                    inward = Vector2.up;
                }

                inward.Normalize();
                var innerStart = start + inward * inwardThickness;
                var innerEnd = end + inward * inwardThickness;
                var baseIndex = vertices.Count;
                vertices.Add(TunnelLocalToWorld(frame, start));
                vertices.Add(TunnelLocalToWorld(frame, end));
                vertices.Add(TunnelLocalToWorld(frame, innerEnd));
                vertices.Add(TunnelLocalToWorld(frame, innerStart));
                AddDoubleSidedQuad(triangles, baseIndex, baseIndex + 1, baseIndex + 2, baseIndex + 3);
            }
        }

        private static Vector3 TunnelLocalToWorld(TunnelSweepFrame frame, Vector2 local)
        {
            return frame.Center + frame.Side * local.x + frame.Up * local.y;
        }

        private static Mesh CreateTunnelFloorSweepMesh(string name, List<TunnelSweepFrame> frames, float width)
        {
            var vertices = new List<Vector3>(frames.Count * 2);
            var triangles = new List<int>(Mathf.Max(0, frames.Count - 1) * 12);
            var halfWidth = width * 0.5f;
            foreach (var frame in frames)
            {
                vertices.Add(frame.Center - frame.Side * halfWidth + Vector3.down * 0.015f);
                vertices.Add(frame.Center + frame.Side * halfWidth + Vector3.down * 0.015f);
            }

            for (var i = 0; i < frames.Count - 1; i++)
            {
                var a = i * 2;
                AddDoubleSidedQuad(triangles, a, a + 1, a + 3, a + 2);
            }

            return BuildTunnelMesh(name, vertices, triangles);
        }

        private static Mesh CreateTunnelOctagonalShellMesh(string name, List<TunnelSweepFrame> frames, float width, float height)
        {
            var vertices = new List<Vector3>(frames.Count * 8);
            var triangles = new List<int>(Mathf.Max(0, frames.Count - 1) * 8 * 12);
            foreach (var frame in frames)
            {
                vertices.AddRange(BuildTunnelOctagonRing(frame, width, height));
            }

            for (var row = 0; row < frames.Count - 1; row++)
            {
                var current = row * 8;
                var next = current + 8;
                for (var edge = 1; edge < 8; edge++)
                {
                    var edgeNext = (edge + 1) % 8;
                    AddDoubleSidedQuad(triangles, current + edge, current + edgeNext, next + edgeNext, next + edge);
                }

                AddDoubleSidedQuad(triangles, current + 7, current, next, next + 7);
            }

            return BuildTunnelMesh(name, vertices, triangles);
        }

        private static Mesh CreateTunnelSideCollisionMesh(string name, List<TunnelSweepFrame> frames, float width, float height)
        {
            var vertices = new List<Vector3>(frames.Count * 4);
            var triangles = new List<int>(Mathf.Max(0, frames.Count - 1) * 24);
            var halfWidth = width * 0.5f;
            var sideHeight = height * 0.72f;
            foreach (var frame in frames)
            {
                vertices.Add(frame.Center - frame.Side * halfWidth);
                vertices.Add(frame.Center - frame.Side * halfWidth + frame.Up * sideHeight);
                vertices.Add(frame.Center + frame.Side * halfWidth);
                vertices.Add(frame.Center + frame.Side * halfWidth + frame.Up * sideHeight);
            }

            for (var row = 0; row < frames.Count - 1; row++)
            {
                var current = row * 4;
                var next = current + 4;
                AddDoubleSidedQuad(triangles, current, next, next + 1, current + 1);
                AddDoubleSidedQuad(triangles, current + 2, current + 3, next + 3, next + 2);
            }

            return BuildTunnelMesh(name, vertices, triangles);
        }

        private static Mesh CreateTunnelRibbonMesh(string name, List<TunnelSweepFrame> frames, float width, float heightOffset, float sideOffset, float stripWidth, bool bothSides)
        {
            var vertices = new List<Vector3>(frames.Count * (bothSides ? 2 : 4));
            var triangles = new List<int>(Mathf.Max(0, frames.Count - 1) * (bothSides ? 12 : 24));
            if (bothSides)
            {
                foreach (var frame in frames)
                {
                    vertices.Add(frame.Center - frame.Side * sideOffset + frame.Up * width);
                    vertices.Add(frame.Center + frame.Side * sideOffset + frame.Up * width);
                }

                for (var row = 0; row < frames.Count - 1; row++)
                {
                    var current = row * 2;
                    AddDoubleSidedQuad(triangles, current, current + 1, current + 3, current + 2);
                }
            }
            else
            {
                var halfStrip = stripWidth * 0.5f;
                foreach (var frame in frames)
                {
                    var leftCenter = frame.Center - frame.Side * sideOffset + frame.Up * heightOffset;
                    var rightCenter = frame.Center + frame.Side * sideOffset + frame.Up * heightOffset;
                    vertices.Add(leftCenter - frame.Side * halfStrip);
                    vertices.Add(leftCenter + frame.Side * halfStrip);
                    vertices.Add(rightCenter - frame.Side * halfStrip);
                    vertices.Add(rightCenter + frame.Side * halfStrip);
                }

                for (var row = 0; row < frames.Count - 1; row++)
                {
                    var current = row * 4;
                    var next = current + 4;
                    AddDoubleSidedQuad(triangles, current, current + 1, next + 1, next);
                    AddDoubleSidedQuad(triangles, current + 2, next + 2, next + 3, current + 3);
                }
            }

            return BuildTunnelMesh(name, vertices, triangles);
        }

        private static Mesh BuildTunnelMesh(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private GameObject CreateTunnelMeshObject(string name, Transform root, Mesh mesh, Material material, bool colliding, StylizedOutlineCategory outlineCategory)
        {
            var target = new GameObject(name);
            target.transform.SetParent(root, false);
            var filter = target.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            DroidRenderSetup.ApplyRenderer(renderer, outlineCategory);
            if (colliding)
            {
                var collider = target.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }

            return target;
        }

        private void CreateAllOutWarHillTunnelMouthSeams(ArenaTheme theme, Transform root, ArenaTunnelRoute tunnel, AllOutWarTerrainProfile terrainProfile)
        {
            if (terrainProfile == null || tunnel == null)
            {
                return;
            }

            CreateAllOutWarHillTunnelMouthSeam(theme, root, tunnel, terrainProfile, tunnel.FromHillRegionIndex, tunnel.FromPortal, tunnel.FromWorldDirection);
            CreateAllOutWarHillTunnelMouthSeam(theme, root, tunnel, terrainProfile, tunnel.ToHillRegionIndex, tunnel.ToPortal, tunnel.ToWorldDirectionValue);
        }

        private void CreateAllOutWarHillTunnelMouthSeam(
            ArenaTheme theme,
            Transform root,
            ArenaTunnelRoute tunnel,
            AllOutWarTerrainProfile terrainProfile,
            int hillRegionIndex,
            Vector3 portal,
            Vector3 inwardDirection)
        {
            if (!terrainProfile.TryBuildTerrainOnlyHillMouthApron(
                    hillRegionIndex,
                    portal,
                    inwardDirection,
                    tunnel.Width,
                    tunnel.Height,
                    out var outerApproach,
                    out var innerMouth,
                    out var outerHalfWidth,
                    out var innerHalfWidth))
            {
                return;
            }

            var terrainMaterial = GetFloorMaterial(theme);
            CreateTunnelMeshObject(
                "All Out War Hill Tunnel Mouth Apron Floor",
                root,
                CreateHillMouthApronFloorMesh(
                    "All Out War Hill Tunnel Mouth Apron Floor Mesh",
                    outerApproach,
                    innerMouth,
                    inwardDirection,
                    outerHalfWidth,
                    innerHalfWidth),
                terrainMaterial,
                true,
                StylizedOutlineCategory.None);

            CreateTunnelMeshObject(
                "All Out War Hill Tunnel Mouth Side Collar",
                root,
                CreateHillMouthSideCollarMesh(
                    "All Out War Hill Tunnel Mouth Left Side Collar Mesh",
                    terrainProfile,
                    outerApproach,
                    innerMouth,
                    inwardDirection,
                    outerHalfWidth,
                    innerHalfWidth,
                    -1f),
                terrainMaterial,
                true,
                StylizedOutlineCategory.None);
            CreateTunnelMeshObject(
                "All Out War Hill Tunnel Mouth Side Collar",
                root,
                CreateHillMouthSideCollarMesh(
                    "All Out War Hill Tunnel Mouth Right Side Collar Mesh",
                    terrainProfile,
                    outerApproach,
                    innerMouth,
                    inwardDirection,
                    outerHalfWidth,
                    innerHalfWidth,
                    1f),
                terrainMaterial,
                true,
                StylizedOutlineCategory.None);
            CreateTunnelMeshObject(
                "All Out War Hill Tunnel Hilltop Cap",
                root,
                CreateHillMouthHilltopCapMesh(
                    "All Out War Hill Tunnel Hilltop Cap Mesh",
                    terrainProfile,
                    outerApproach,
                    portal,
                    innerMouth,
                    inwardDirection,
                    outerHalfWidth,
                    innerHalfWidth,
                    tunnel.Height),
                terrainMaterial,
                true,
                StylizedOutlineCategory.None);
        }

        private static Mesh CreateHillMouthApronFloorMesh(
            string name,
            Vector3 outerApproach,
            Vector3 innerMouth,
            Vector3 inwardDirection,
            float outerHalfWidth,
            float innerHalfWidth)
        {
            var direction = Flatten(inwardDirection);
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Flatten(innerMouth - outerApproach);
            }

            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            var side = new Vector3(-direction.z, 0f, direction.x);
            outerApproach.y = 0.018f;
            innerMouth.y = 0.018f;
            var vertices = new List<Vector3>(4)
            {
                outerApproach - side * outerHalfWidth,
                outerApproach + side * outerHalfWidth,
                innerMouth + side * innerHalfWidth,
                innerMouth - side * innerHalfWidth
            };
            var triangles = new List<int>(6);
            var normals = BuildUniformNormals(vertices.Count, Vector3.up);
            AddSingleSidedQuad(triangles, 0, 1, 2, 3);
            return BuildTerrainMouthMesh(name, vertices, triangles, normals);
        }

        private static Mesh CreateHillMouthSideCollarMesh(
            string name,
            AllOutWarTerrainProfile terrainProfile,
            Vector3 outerApproach,
            Vector3 innerMouth,
            Vector3 inwardDirection,
            float outerHalfWidth,
            float innerHalfWidth,
            float sideSign)
        {
            var direction = Flatten(inwardDirection);
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Flatten(innerMouth - outerApproach);
            }

            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            var side = new Vector3(-direction.z, 0f, direction.x) * Mathf.Sign(sideSign == 0f ? 1f : sideSign);
            var outerOverlap = Mathf.Max(0.55f, innerHalfWidth * 0.22f);
            var floorY = 0.018f;
            var surfaceLift = 0.035f;
            var wallNormal = UpBiasedNormal(-side);
            const int samples = 8;
            var vertices = new List<Vector3>((samples + 1) * 3);
            var normals = new List<Vector3>((samples + 1) * 3);
            var triangles = new List<int>(samples * 12);
            for (var i = 0; i <= samples; i++)
            {
                var t = i / (float)samples;
                var halfWidth = Mathf.Lerp(outerHalfWidth, innerHalfWidth, t);
                var edge = Vector3.Lerp(outerApproach, innerMouth, t) + side * halfWidth;
                var outside = edge + side * outerOverlap;
                var bottomEdge = edge;
                var topEdge = edge;
                bottomEdge.y = floorY;
                topEdge.y = Mathf.Max(floorY + 0.02f, terrainProfile.SampleHeight(topEdge) + surfaceLift);
                outside.y = Mathf.Max(topEdge.y, terrainProfile.SampleHeight(outside) + surfaceLift);
                vertices.Add(bottomEdge);
                vertices.Add(topEdge);
                vertices.Add(outside);
                normals.Add(wallNormal);
                normals.Add(wallNormal);
                normals.Add(Vector3.up);
            }

            for (var i = 0; i < samples; i++)
            {
                var a = i * 3;
                var b = a + 3;
                AddSingleSidedQuadFacing(triangles, vertices, a, b, b + 1, a + 1, -side);
                AddSingleSidedQuadFacing(triangles, vertices, a + 1, b + 1, b + 2, a + 2, Vector3.up);
            }

            return BuildTerrainMouthMesh(name, vertices, triangles, normals);
        }

        private static Mesh CreateHillMouthHilltopCapMesh(
            string name,
            AllOutWarTerrainProfile terrainProfile,
            Vector3 outerApproach,
            Vector3 portal,
            Vector3 innerMouth,
            Vector3 inwardDirection,
            float outerHalfWidth,
            float innerHalfWidth,
            float tunnelHeight)
        {
            var direction = Flatten(inwardDirection);
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Flatten(innerMouth - portal);
            }

            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            var side = new Vector3(-direction.z, 0f, direction.x);
            var totalMouthLength = Mathf.Max(0.001f, Vector3.Dot(innerMouth - outerApproach, direction));
            var surfaceLift = 0.025f;
            var roofBottomY = Mathf.Max(tunnelHeight + 0.055f, 0.08f);
            var browDepth = Mathf.Clamp(Vector3.Dot(portal - outerApproach, direction) * 0.16f, 0.35f, 0.9f);
            var sideOverlap = Mathf.Max(0.22f, innerHalfWidth * 0.09f);
            const int samples = 7;
            var vertices = new List<Vector3>((samples + 1) * 2);
            var normals = new List<Vector3>((samples + 1) * 2);
            var triangles = new List<int>(samples * 6);

            for (var i = 0; i <= samples; i++)
            {
                var t = i == 0 ? 0f : (i - 1) / Mathf.Max(1f, samples - 1f);
                var center = i == 0
                    ? portal - direction * browDepth
                    : Vector3.Lerp(portal, innerMouth, t);
                var mouthT = Mathf.Clamp01(Vector3.Dot(center - outerApproach, direction) / totalMouthLength);
                var halfWidth = Mathf.Lerp(outerHalfWidth, innerHalfWidth, mouthT) + sideOverlap;
                var left = center - side * halfWidth;
                var right = center + side * halfWidth;
                if (i == 0)
                {
                    left.y = roofBottomY;
                    right.y = roofBottomY;
                }
                else
                {
                    left.y = Mathf.Max(roofBottomY + 0.04f, terrainProfile.SampleHeight(left) + surfaceLift);
                    right.y = Mathf.Max(roofBottomY + 0.04f, terrainProfile.SampleHeight(right) + surfaceLift);
                }

                vertices.Add(left);
                vertices.Add(right);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
            }

            for (var i = 0; i < samples; i++)
            {
                var a = i * 2;
                AddSingleSidedQuad(triangles, a, a + 1, a + 3, a + 2);
            }

            return BuildTerrainMouthMesh(name, vertices, triangles, normals);
        }

        private void CreateAllOutWarSubfloorTunnelTerrainCollar(ArenaTheme theme, Transform root, ArenaTunnelRoute tunnel, Vector3 portal, Vector3 direction)
        {
            if (tunnel == null)
            {
                return;
            }

            var fromEndpoint = FlatDistance(portal, tunnel.FromPortal) <= FlatDistance(portal, tunnel.ToPortal);
            var footprint = BuildAllOutWarSubfloorPortalFootprint(tunnel, fromEndpoint);
            CreateTunnelMeshObject(
                "All Out War Subfloor Tunnel Terrain Collar",
                root,
                CreateSubfloorTunnelTerrainCollarMesh(
                    "All Out War Subfloor Tunnel Terrain Collar Mesh",
                    footprint,
                    tunnel.Width,
                    tunnel.SubfloorDepth),
                GetFloorMaterial(theme),
                true,
                StylizedOutlineCategory.None);
        }

        private static Mesh CreateSubfloorTunnelTerrainCollarMesh(
            string name,
            SubfloorPortalFootprint footprint,
            float tunnelWidth,
            float subfloorDepth)
        {
            var direction = footprint.Direction.sqrMagnitude > 0.001f ? footprint.Direction.normalized : Vector3.forward;
            var side = footprint.Side.sqrMagnitude > 0.001f ? footprint.Side.normalized : Vector3.right;
            var visualHalfWidth = Mathf.Max(footprint.VisualHalfWidth, footprint.CutoutHalfWidth + 0.2f);
            var clearHalfWidth = Mathf.Min(visualHalfWidth - 0.08f, Mathf.Max(tunnelWidth * 0.5f + 0.12f, tunnelWidth * 0.5f));
            clearHalfWidth = Mathf.Max(tunnelWidth * 0.5f, clearHalfWidth);
            var topY = 0.018f;
            var portalOverlap = Mathf.Min(GetAllOutWarSubfloorThresholdRampOverlap(), footprint.ThresholdDepth * 0.22f);
            var landingBack = footprint.ThresholdBack;
            var landingFront = footprint.Portal + direction * portalOverlap;
            var endCoverDepth = Mathf.Max(0.42f, tunnelWidth * 0.12f);
            var rampEndCover = footprint.RampEnd + direction * endCoverDepth;
            const int rampSamples = 8;

            var vertices = new List<Vector3>(96);
            var normals = new List<Vector3>(96);
            var triangles = new List<int>(144);

            AddFlatTerrainStrip(
                vertices,
                normals,
                triangles,
                landingBack,
                landingFront,
                side,
                -visualHalfWidth,
                visualHalfWidth,
                topY);

            for (var sideSign = -1; sideSign <= 1; sideSign += 2)
            {
                var signedSide = side * sideSign;
                var startIndex = vertices.Count;
                for (var i = 0; i <= rampSamples; i++)
                {
                    var t = i / (float)rampSamples;
                    var center = Vector3.Lerp(landingFront, footprint.RampEnd, t);
                    var rampForward = Mathf.Clamp01(Vector3.Dot(center - footprint.Portal, direction) / Mathf.Max(0.1f, footprint.RampLength));
                    var bottomY = Mathf.Min(-0.12f, -subfloorDepth * rampForward - 0.08f);
                    var innerTop = center + signedSide * clearHalfWidth;
                    var outerTop = center + signedSide * visualHalfWidth;
                    var innerBottom = innerTop;
                    innerTop.y = topY;
                    outerTop.y = topY;
                    innerBottom.y = bottomY;
                    vertices.Add(innerBottom);
                    vertices.Add(innerTop);
                    vertices.Add(outerTop);
                    normals.Add(UpBiasedNormal(-signedSide));
                    normals.Add(UpBiasedNormal(-signedSide));
                    normals.Add(Vector3.up);
                }

                for (var i = 0; i < rampSamples; i++)
                {
                    var a = startIndex + i * 3;
                    var b = a + 3;
                    AddSingleSidedQuadFacing(triangles, vertices, a, b, b + 1, a + 1, -signedSide);
                    AddSingleSidedQuadFacing(triangles, vertices, a + 1, b + 1, b + 2, a + 2, Vector3.up);
                }
            }

            AddFlatTerrainStrip(
                vertices,
                normals,
                triangles,
                footprint.RampEnd,
                rampEndCover,
                side,
                -visualHalfWidth,
                visualHalfWidth,
                topY);

            return BuildTerrainMouthMesh(name, vertices, triangles, normals);
        }

        private static void AddFlatTerrainStrip(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            Vector3 back,
            Vector3 front,
            Vector3 side,
            float minSideOffset,
            float maxSideOffset,
            float y)
        {
            back.y = y;
            front.y = y;
            var start = vertices.Count;
            vertices.Add(back + side * minSideOffset);
            vertices.Add(back + side * maxSideOffset);
            vertices.Add(front + side * maxSideOffset);
            vertices.Add(front + side * minSideOffset);
            normals.Add(Vector3.up);
            normals.Add(Vector3.up);
            normals.Add(Vector3.up);
            normals.Add(Vector3.up);
            AddSingleSidedQuadFacing(triangles, vertices, start, start + 1, start + 2, start + 3, Vector3.up);
        }

        private static Vector3 UpBiasedNormal(Vector3 lateralNormal)
        {
            if (lateralNormal.sqrMagnitude <= 0.001f)
            {
                return Vector3.up;
            }

            return (Vector3.up * 0.92f + lateralNormal.normalized * 0.24f).normalized;
        }

        private static List<Vector3> BuildUniformNormals(int count, Vector3 normal)
        {
            var normals = new List<Vector3>(count);
            var safeNormal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
            for (var i = 0; i < count; i++)
            {
                normals.Add(safeNormal);
            }

            return normals;
        }

        private static Mesh BuildTerrainMouthMesh(string name, List<Vector3> vertices, List<int> triangles, List<Vector3> normals)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            if (normals != null && normals.Count == vertices.Count)
            {
                mesh.SetNormals(normals);
            }
            else
            {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material ResolveTunnelWallMaterial(ArenaTheme theme, ArenaTunnelKind kind)
        {
            if (theme == null)
            {
                return null;
            }

            if (kind == ArenaTunnelKind.Subfloor)
            {
                return theme.Wall != null ? theme.Wall : theme.GateInterior;
            }

            return theme.GateInterior != null ? theme.GateInterior : theme.Wall;
        }

        private void CreateAllOutWarTunnelThreshold(ArenaTheme theme, Transform root, ArenaTunnelRoute tunnel, Vector3 portal, Vector3 direction)
        {
            var fromEndpoint = FlatDistance(portal, tunnel.FromPortal) <= FlatDistance(portal, tunnel.ToPortal);
            var footprint = BuildAllOutWarSubfloorPortalFootprint(tunnel, fromEndpoint);
            var thresholdDepth = footprint.ThresholdDepth;
            var thresholdWidth = footprint.CutoutHalfWidth * 2f + 0.35f;
            var thresholdThickness = 0.22f;
            var overlapIntoRamp = Mathf.Min(GetAllOutWarSubfloorThresholdRampOverlap(), thresholdDepth * 0.22f);
            var center = footprint.Portal - footprint.Direction * (thresholdDepth * 0.5f - overlapIntoRamp);
            center.y = -thresholdThickness * 0.5f;
            var rotation = Quaternion.LookRotation(footprint.Direction, Vector3.up);
            var material = ResolveTunnelWallMaterial(theme, ArenaTunnelKind.Subfloor);
            CreateTunnelBox("All Out War Tunnel Threshold", root, center, rotation, new Vector3(thresholdWidth, thresholdThickness, thresholdDepth), material, true);
        }

        private GameObject CreateTunnelBox(string name, Transform root, Vector3 position, Quaternion rotation, Vector3 scale, Material material, bool colliding)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(root, false);
            box.transform.position = position;
            box.transform.rotation = rotation;
            box.transform.localScale = scale;

            if (!colliding)
            {
                RemoveCollider(box);
            }

            if (box.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
                DroidRenderSetup.ApplyRenderer(renderer, ResolveOutlineCategory(name, material));
            }

            return box;
        }

        private void CreateAllOutWarTunnelPortal(ArenaTheme theme, Transform root, ArenaTunnelRoute tunnel, Vector3 position, Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector3.forward;
            }

            var portalRoot = new GameObject("All Out War Octagonal Tunnel Portal");
            portalRoot.transform.SetParent(root, false);
            portalRoot.transform.position = position;
            portalRoot.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            var frame = new GameObject("All Out War Tunnel Portal Frame");
            frame.transform.SetParent(portalRoot.transform, false);
            var frameFilter = frame.AddComponent<MeshFilter>();
            frameFilter.sharedMesh = CreateOctagonalTunnelPortalMesh("All Out War Tunnel Portal Frame Mesh", tunnel.Width, tunnel.Height, 0.46f, 0.34f);
            var frameRenderer = frame.AddComponent<MeshRenderer>();
            frameRenderer.sharedMaterial = theme.Wall;
            DroidRenderSetup.ApplyRenderer(frameRenderer, StylizedOutlineCategory.Wall);

            var neonMaterial = theme.NeonB != null ? theme.NeonB : theme.NeonA;
            if (neonMaterial == null)
            {
                return;
            }

            var neon = new GameObject("All Out War Tunnel Portal Neon Ring");
            neon.transform.SetParent(portalRoot.transform, false);
            neon.transform.localPosition = new Vector3(0f, 0f, -0.08f);
            var neonFilter = neon.AddComponent<MeshFilter>();
            neonFilter.sharedMesh = CreateOctagonalTunnelPortalMesh("All Out War Tunnel Portal Neon Mesh", tunnel.Width, tunnel.Height, 0.08f, 0.055f);
            var neonRenderer = neon.AddComponent<MeshRenderer>();
            neonRenderer.sharedMaterial = neonMaterial;
            DroidRenderSetup.ApplyRenderer(neonRenderer, StylizedOutlineCategory.None);
        }

        private static Mesh CreateOctagonalTunnelPortalMesh(string name, float width, float height, float padding, float depth)
        {
            var inner = BuildTunnelPortalOctagon(width, height, 0f);
            var outer = BuildTunnelPortalOctagon(width, height, padding);
            var vertices = new List<Vector3>(32);
            var triangles = new List<int>(8 * 24);
            var halfDepth = Mathf.Max(0.01f, depth) * 0.5f;

            for (var i = 0; i < 8; i++)
            {
                vertices.Add(new Vector3(outer[i].x, outer[i].y, -halfDepth));
                vertices.Add(new Vector3(inner[i].x, inner[i].y, -halfDepth));
                vertices.Add(new Vector3(outer[i].x, outer[i].y, halfDepth));
                vertices.Add(new Vector3(inner[i].x, inner[i].y, halfDepth));
            }

            for (var i = 0; i < 8; i++)
            {
                var next = (i + 1) % 8;
                var outerFront = i * 4;
                var innerFront = outerFront + 1;
                var outerBack = outerFront + 2;
                var innerBack = outerFront + 3;
                var nextOuterFront = next * 4;
                var nextInnerFront = nextOuterFront + 1;
                var nextOuterBack = nextOuterFront + 2;
                var nextInnerBack = nextOuterFront + 3;

                AddDoubleSidedQuad(triangles, outerFront, nextOuterFront, nextInnerFront, innerFront);
                AddDoubleSidedQuad(triangles, nextOuterBack, outerBack, innerBack, nextInnerBack);
                AddDoubleSidedQuad(triangles, nextOuterFront, outerFront, outerBack, nextOuterBack);
                AddDoubleSidedQuad(triangles, innerFront, nextInnerFront, nextInnerBack, innerBack);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector2[] BuildTunnelPortalOctagon(float width, float height, float padding)
        {
            var halfWidth = width * 0.5f + padding;
            var bottom = -padding;
            var top = height + padding;
            var bevelX = Mathf.Min(halfWidth * 0.42f, width * 0.18f + padding);
            var bevelY = Mathf.Min((top - bottom) * 0.28f, height * 0.18f + padding);
            return new[]
            {
                new Vector2(-halfWidth + bevelX, bottom),
                new Vector2(halfWidth - bevelX, bottom),
                new Vector2(halfWidth, bottom + bevelY),
                new Vector2(halfWidth, top - bevelY),
                new Vector2(halfWidth - bevelX, top),
                new Vector2(-halfWidth + bevelX, top),
                new Vector2(-halfWidth, top - bevelY),
                new Vector2(-halfWidth, bottom + bevelY)
            };
        }

        private void CreateFloorTrimChannel(ArenaTheme theme, Transform root, Vector3 position, Vector3 channelScale, bool accentA, float lightThickness)
        {
        }

        private void CreateNeonStrip(ArenaTheme theme, Transform root, Vector3 position, Vector3 scale, bool accentA)
        {
            var strip = CreateCube("Neon Floor Strip", root, position, scale, accentA ? theme.NeonA : theme.NeonB);
            if (strip.TryGetComponent<Collider>(out var collider))
            {
                Destroy(collider);
            }
        }

        private void CreateGateFloor(ArenaTheme theme, Transform root, string name, Vector3 position, Vector3 scale)
        {
            CreateCube(name, root, position, scale, theme.GateInterior);
        }

        private void AddSpawnsAndPickups(ArenaLayout layout, System.Random random, int pickupCount)
        {
            var rooms = new List<Vector2Int>(layout.Rooms);
            rooms.Sort((a, b) => layout.RoomCenters[a].sqrMagnitude.CompareTo(layout.RoomCenters[b].sqrMagnitude));

            if (layout.GateSpawns.Count >= 2)
            {
                var playerGateIndex = random.Next(layout.GateSpawns.Count);
                var opponentGateIndex = random.Next(layout.GateSpawns.Count - 1);
                if (opponentGateIndex >= playerGateIndex)
                {
                    opponentGateIndex++;
                }

                var playerGate = layout.GateSpawns[playerGateIndex];
                var opponentGate = layout.GateSpawns[opponentGateIndex];
                layout.PlayerSpawn = playerGate.SpawnPosition;
                layout.PlayerRotation = playerGate.SpawnRotation;
                layout.PlayerGate = playerGate;
                layout.OpponentSpawn = opponentGate.SpawnPosition;
                layout.OpponentRotation = opponentGate.SpawnRotation;
                layout.OpponentGate = opponentGate;
            }
            else
            {
                var playerRoom = rooms[0];
                var opponentRoom = rooms[^1];
                layout.PlayerSpawn = layout.RoomCenters[playerRoom] + new Vector3(0f, 1.1f, 0f);
                layout.OpponentSpawn = layout.RoomCenters[opponentRoom] + new Vector3(0f, 1.1f, 0f);
                layout.PlayerRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                layout.OpponentRotation = Quaternion.LookRotation((layout.RoomCenters[playerRoom] - layout.RoomCenters[opponentRoom]).normalized, Vector3.up);
            }

            var shuffled = new List<Vector2Int>(rooms);
            for (var i = 0; i < shuffled.Count; i++)
            {
                var swapIndex = random.Next(i, shuffled.Count);
                (shuffled[i], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[i]);
            }

            foreach (var room in shuffled)
            {
                if (layout.PickupPoints.Count >= pickupCount)
                {
                    break;
                }

                if (IsSpawnRoom(layout, room))
                {
                    continue;
                }

                var offset = new Vector3((float)(random.NextDouble() - 0.5) * 3.5f, 0.55f, (float)(random.NextDouble() - 0.5) * 3.5f);
                layout.PickupPoints.Add(layout.RoomCenters[room] + offset);
            }

            if (layout.PickupPoints.Count == 0)
            {
                layout.PickupPoints.Add(layout.RoomCenters[rooms[0]] + new Vector3(2.25f, 0.55f, 2.25f));
            }
        }

        private void AddGateSpawns(ArenaLayout layout, System.Random random, int gateCount, float roomSize, float gateLength)
        {
            var candidates = new List<(Vector2Int room, Vector2Int direction)>();
            foreach (var room in layout.Rooms)
            {
                foreach (var direction in Directions)
                {
                    if (!layout.Rooms.Contains(room + direction) && IsGateCandidateClear(layout, room, direction, roomSize, gateLength, roomSize * 0.5f))
                    {
                        candidates.Add((room, direction));
                    }
                }
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var swap = random.Next(i, candidates.Count);
                (candidates[i], candidates[swap]) = (candidates[swap], candidates[i]);
            }

            var targetCount = Mathf.Min(Mathf.Max(2, gateCount), candidates.Count);
            var accepted = new List<(Vector2Int room, Vector2Int direction)>();
            for (var i = 0; i < candidates.Count && accepted.Count < targetCount; i++)
            {
                var candidate = candidates[i];
                if (OverlapsAcceptedGateFootprint(layout, candidate.room, candidate.direction, accepted, roomSize, gateLength))
                {
                    continue;
                }

                accepted.Add(candidate);
                var direction = ToWorldDirection(candidate.direction);
                var roomCenter = layout.RoomCenters[candidate.room];
                var entranceLength = gateLength * 0.9f;
                var connectorLength = gateLength * 1.55f;
                var spawn = roomCenter + direction * (roomSize * 0.5f + connectorLength + entranceLength * 0.72f) + Vector3.up * 1.1f;
                var rotation = Quaternion.LookRotation(-direction, Vector3.up);
                var entryTarget = roomCenter + direction * (roomSize * 0.5f - 1.1f) + Vector3.up * 1.1f;
                layout.GateSpawns.Add(new ArenaGateSpawn(candidate.room, candidate.direction, spawn, rotation, roomCenter, entryTarget));
            }
        }

        private bool OverlapsAcceptedGateFootprint(ArenaLayout layout, Vector2Int room, Vector2Int direction, List<(Vector2Int room, Vector2Int direction)> accepted, float roomSize, float gateLength)
        {
            GetGateFootprintBounds(layout, room, direction, roomSize, gateLength, out var min, out var max);

            foreach (var gate in accepted)
            {
                GetGateFootprintBounds(layout, gate.room, gate.direction, roomSize, gateLength, out var otherMin, out var otherMax);
                if (BoundsOverlap(min, max, otherMin, otherMax))
                {
                    return true;
                }
            }

            return false;
        }

        private void GetGateFootprintBounds(ArenaLayout layout, Vector2Int room, Vector2Int direction, float roomSize, float gateLength, out Vector2 min, out Vector2 max)
        {
            var worldDirection = ToWorldDirection(direction);
            var sideDirection = new Vector3(-worldDirection.z, 0f, worldDirection.x);
            var entranceLength = gateLength * 0.9f;
            var connectorLength = gateLength * 1.55f;
            var outwardStart = roomSize * 0.5f + 0.15f;
            var outwardEnd = roomSize * 0.5f + connectorLength + entranceLength * 3.35f;
            var forwardHalf = (outwardEnd - outwardStart) * 0.5f;
            var forwardCenter = (outwardStart + outwardEnd) * 0.5f;
            var sideHalf = 2.45f;
            var center = layout.RoomCenters[room] + worldDirection * forwardCenter;

            var halfX = Mathf.Abs(worldDirection.x) * forwardHalf + Mathf.Abs(sideDirection.x) * sideHalf;
            var halfZ = Mathf.Abs(worldDirection.z) * forwardHalf + Mathf.Abs(sideDirection.z) * sideHalf;
            min = new Vector2(center.x - halfX, center.z - halfZ);
            max = new Vector2(center.x + halfX, center.z + halfZ);
        }

        private static bool BoundsOverlap(Vector2 min, Vector2 max, Vector2 otherMin, Vector2 otherMax)
        {
            const float buffer = 0.65f;
            return min.x < otherMax.x + buffer &&
                   max.x > otherMin.x - buffer &&
                   min.y < otherMax.y + buffer &&
                   max.y > otherMin.y - buffer;
        }

        private bool IsGateCandidateClear(ArenaLayout layout, Vector2Int room, Vector2Int direction, float roomSize, float gateLength, float roomHalfExtent)
        {
            if (!layout.RoomCenters.TryGetValue(room, out var roomCenter))
            {
                return false;
            }

            var worldDirection = ToWorldDirection(direction);
            var sideDirection = new Vector3(-worldDirection.z, 0f, worldDirection.x);
            var entranceLength = gateLength * 0.9f;
            var connectorLength = gateLength * 1.55f;
            var outwardReach = connectorLength + entranceLength * 3.5f;
            var corridorHalfWidth = 1.9f;

            foreach (var otherRoom in layout.Rooms)
            {
                if (otherRoom == room)
                {
                    continue;
                }

                var offset = layout.RoomCenters[otherRoom] - roomCenter;
                var forward = Vector3.Dot(offset, worldDirection);
                if (forward <= roomSize * 0.5f)
                {
                    continue;
                }

                var side = Mathf.Abs(Vector3.Dot(offset, sideDirection));
                var overlapsForward = forward - roomHalfExtent < roomSize * 0.5f + outwardReach;
                var overlapsSide = side < roomHalfExtent + corridorHalfWidth;
                if (overlapsForward && overlapsSide)
                {
                    return false;
                }
            }

            return true;
        }

        private void CreateGate(ArenaTheme theme, Transform root, ArenaGateSpawn gate, float roomSize, float gateLength, float corridorWidth, float wallHeight)
        {
            var direction = ToWorldDirection(gate.Direction);
            var entranceLength = gateLength * 0.9f;
            var connectorLength = gateLength * 1.55f;
            var entranceCenter = gate.InnerTarget + direction * (roomSize * 0.5f + connectorLength + entranceLength * 0.5f);
            var connectorCenter = gate.InnerTarget + direction * (roomSize * 0.5f + connectorLength * 0.5f);
            var vertical = gate.Direction == Vector2Int.up || gate.Direction == Vector2Int.down;
            var gateRoot = new GameObject("Perimeter Spawn Gate");
            gateRoot.transform.SetParent(root, false);
            gateRoot.transform.position = entranceCenter;
            gateRoot.transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);

            var gateMarker = VerticalMarkerBeam.Attach(
                gateRoot.transform,
                "Red Spawn Gate Location Beam",
                new Color(1f, 0.08f, 0.04f),
                22f,
                0.92f,
                6.5f);
            gateMarker.transform.localPosition = new Vector3(0f, Mathf.Max(0f, wallHeight - 2.4f), connectorLength + entranceLength * 0.5f);

            if (vertical)
            {
                CreateGateFloor(theme, root, "Gate Floor", entranceCenter + new Vector3(0f, -0.1f, 0f), new Vector3(corridorWidth, 0.2f, entranceLength));
                CreateCube("Gate Roof", root, entranceCenter + new Vector3(0f, wallHeight + 0.1f, 0f), new Vector3(corridorWidth + 0.3f, 0.34f, entranceLength), theme.GateInterior);
                CreateCube("Gate Wall", root, entranceCenter + new Vector3(-corridorWidth * 0.5f, wallHeight * 0.5f, 0f), new Vector3(0.35f, wallHeight, entranceLength), theme.GateInterior);
                CreateCube("Gate Wall", root, entranceCenter + new Vector3(corridorWidth * 0.5f, wallHeight * 0.5f, 0f), new Vector3(0.35f, wallHeight, entranceLength), theme.GateInterior);
                CreateGateFloor(theme, root, "Gate Connector Floor", connectorCenter + new Vector3(0f, -0.1f, 0f), new Vector3(corridorWidth, 0.2f, connectorLength));
                CreateCube("Gate Connector Roof", root, connectorCenter + new Vector3(0f, wallHeight + 0.1f, 0f), new Vector3(corridorWidth + 0.3f, 0.34f, connectorLength), theme.GateInterior);
                CreateCube("Gate Connector Wall", root, connectorCenter + new Vector3(-corridorWidth * 0.5f, wallHeight * 0.5f, 0f), new Vector3(0.35f, wallHeight, connectorLength), theme.GateInterior);
                CreateCube("Gate Connector Wall", root, connectorCenter + new Vector3(corridorWidth * 0.5f, wallHeight * 0.5f, 0f), new Vector3(0.35f, wallHeight, connectorLength), theme.GateInterior);
            }
            else
            {
                CreateGateFloor(theme, root, "Gate Floor", entranceCenter + new Vector3(0f, -0.1f, 0f), new Vector3(entranceLength, 0.2f, corridorWidth));
                CreateCube("Gate Roof", root, entranceCenter + new Vector3(0f, wallHeight + 0.1f, 0f), new Vector3(entranceLength, 0.34f, corridorWidth + 0.3f), theme.GateInterior);
                CreateCube("Gate Wall", root, entranceCenter + new Vector3(0f, wallHeight * 0.5f, -corridorWidth * 0.5f), new Vector3(entranceLength, wallHeight, 0.35f), theme.GateInterior);
                CreateCube("Gate Wall", root, entranceCenter + new Vector3(0f, wallHeight * 0.5f, corridorWidth * 0.5f), new Vector3(entranceLength, wallHeight, 0.35f), theme.GateInterior);
                CreateGateFloor(theme, root, "Gate Connector Floor", connectorCenter + new Vector3(0f, -0.1f, 0f), new Vector3(connectorLength, 0.2f, corridorWidth));
                CreateCube("Gate Connector Roof", root, connectorCenter + new Vector3(0f, wallHeight + 0.1f, 0f), new Vector3(connectorLength, 0.34f, corridorWidth + 0.3f), theme.GateInterior);
                CreateCube("Gate Connector Wall", root, connectorCenter + new Vector3(0f, wallHeight * 0.5f, -corridorWidth * 0.5f), new Vector3(connectorLength, wallHeight, 0.35f), theme.GateInterior);
                CreateCube("Gate Connector Wall", root, connectorCenter + new Vector3(0f, wallHeight * 0.5f, corridorWidth * 0.5f), new Vector3(connectorLength, wallHeight, 0.35f), theme.GateInterior);
            }

            CreateLocalPrimitive(
                "Pitch Black Spawn Ramp",
                PrimitiveType.Cube,
                theme.GateInterior,
                gateRoot.transform,
                new Vector3(0f, -0.035f, -entranceLength * 0.06f),
                new Vector3(corridorWidth * 0.92f, 0.11f, entranceLength * 0.92f),
                new Vector3(-4f, 0f, 0f));
            var spawnTunnelFloor = CreateLocalStructuralPrimitive(
                "Spawn Tunnel Floor Extension",
                PrimitiveType.Cube,
                theme.GateInterior,
                gateRoot.transform,
                new Vector3(0f, -0.1f, -entranceLength * 1.55f),
                new Vector3(corridorWidth, 0.2f, entranceLength * 2.35f),
                Vector3.zero);
            CreateLocalStructuralPrimitive(
                "Spawn Tunnel Roof Extension",
                PrimitiveType.Cube,
                theme.GateInterior,
                gateRoot.transform,
                new Vector3(0f, wallHeight + 0.1f, -entranceLength * 1.55f),
                new Vector3(corridorWidth + 0.3f, 0.34f, entranceLength * 2.35f),
                Vector3.zero);
            CreateLocalStructuralPrimitive(
                "Spawn Tunnel Left Wall Extension",
                PrimitiveType.Cube,
                theme.GateInterior,
                gateRoot.transform,
                new Vector3(-corridorWidth * 0.5f, wallHeight * 0.5f, -entranceLength * 1.55f),
                new Vector3(0.35f, wallHeight, entranceLength * 2.35f),
                Vector3.zero);
            CreateLocalStructuralPrimitive(
                "Spawn Tunnel Right Wall Extension",
                PrimitiveType.Cube,
                theme.GateInterior,
                gateRoot.transform,
                new Vector3(corridorWidth * 0.5f, wallHeight * 0.5f, -entranceLength * 1.55f),
                new Vector3(0.35f, wallHeight, entranceLength * 2.35f),
                Vector3.zero);
            CreateLocalStructuralPrimitive(
                "Spawn Corridor Rear Wall",
                PrimitiveType.Cube,
                theme.GateEndWall,
                gateRoot.transform,
                new Vector3(0f, wallHeight * 0.5f, -entranceLength * 2.78f),
                new Vector3(corridorWidth + 0.3f, wallHeight, 0.35f),
                Vector3.zero);

            var lanternPosition = new Vector3(-corridorWidth * 0.5f + 0.28f, wallHeight * 0.54f, -gateLength * 0.18f);
            GameObject lantern;
            if (WallLanternAsset.TryBuild(gateRoot.transform, theme, lanternPosition))
            {
                lantern = new GameObject("Cyan Wall Lantern Light Anchor");
                lantern.transform.SetParent(gateRoot.transform, false);
                lantern.transform.localPosition = lanternPosition;
            }
            else
            {
                var lanternMount = CreateLocalPrimitive("Lantern Mount", PrimitiveType.Cube, theme.Wall, gateRoot.transform, new Vector3(-corridorWidth * 0.5f + 0.08f, wallHeight * 0.56f, -gateLength * 0.22f), new Vector3(0.08f, 0.68f, 0.34f), Vector3.zero);
                lantern = CreateLocalPrimitive("Cyan Wall Lantern", PrimitiveType.Cube, theme.NeonA, gateRoot.transform, lanternPosition, new Vector3(0.055f, 0.48f, 0.18f), Vector3.zero);
                lanternMount.name = "Gate Corridor Lantern Mount";
            }

            var lanternLight = lantern.AddComponent<Light>();
            lanternLight.type = LightType.Point;
            lanternLight.shadows = LightShadows.None;
            lanternLight.color = new Color(0.15f, 0.75f, 1f);
            lanternLight.range = 4.2f;
            lanternLight.intensity = 2.25f;

            var doorLocalClosed = new Vector3(0f, wallHeight * 0.5f, entranceLength * 0.5f);
            var doorLocalOpen = doorLocalClosed + Vector3.up * (wallHeight + 0.8f);
            var door = new GameObject("Closing Bar Gate");
            door.transform.SetParent(gateRoot.transform, false);
            door.transform.localPosition = doorLocalClosed;
            CreateBarGate(theme, door.transform, corridorWidth, wallHeight);
            var doorBlocker = door.AddComponent<BoxCollider>();
            doorBlocker.center = Vector3.zero;
            doorBlocker.size = new Vector3(corridorWidth + 0.45f, wallHeight, 0.32f);

            var trigger = gateRoot.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.1f, entranceLength * 0.2f);
            trigger.size = new Vector3(corridorWidth, 2.2f, 1.2f);
            var gateDoor = gateRoot.AddComponent<GateDoor>();
            gateDoor.Configure(door.transform, doorLocalOpen, doorLocalClosed);
            gate.Door = gateDoor;
        }

        private static bool HasGate(ArenaLayout layout, Vector2Int room, Vector2Int direction)
        {
            foreach (var gate in GetActiveSpawnGates(layout))
            {
                if (gate.Room == room && gate.Direction == direction)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSpawnRoom(ArenaLayout layout, Vector2Int room)
        {
            foreach (var gate in GetActiveSpawnGates(layout))
            {
                if (gate.Room == room)
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<ArenaGateSpawn> GetActiveSpawnGates(ArenaLayout layout)
        {
            foreach (var gate in layout.GateSpawns)
            {
                yield return gate;
            }
        }

        private static Vector3 ToWorldDirection(Vector2Int direction)
        {
            return new Vector3(direction.x, 0f, direction.y).normalized;
        }

        private void CreateBarGate(ArenaTheme theme, Transform parent, float gateWidth, float gateHeight)
        {
            if (GateDoorAsset.TryBuild(parent, theme, gateWidth + 0.35f, gateHeight))
            {
                return;
            }

            var barMaterial = theme.Wall;
            var trimMaterial = theme.NeonB;
            var halfHeight = gateHeight * 0.5f;
            var usableWidth = gateWidth + 0.35f;

            CreateLocalPrimitive("Gate Top Rail", PrimitiveType.Cube, barMaterial, parent, new Vector3(0f, halfHeight - 0.1f, 0f), new Vector3(usableWidth, 0.16f, 0.18f), Vector3.zero);
            CreateLocalPrimitive("Gate Bottom Rail", PrimitiveType.Cube, barMaterial, parent, new Vector3(0f, -halfHeight + 0.18f, 0f), new Vector3(usableWidth, 0.16f, 0.18f), Vector3.zero);
            CreateLocalPrimitive("Gate Middle Rail", PrimitiveType.Cube, barMaterial, parent, new Vector3(0f, 0f, 0f), new Vector3(usableWidth, 0.11f, 0.16f), Vector3.zero);

            const int barCount = 7;
            for (var i = 0; i < barCount; i++)
            {
                var t = barCount == 1 ? 0.5f : i / (float)(barCount - 1);
                var x = Mathf.Lerp(-usableWidth * 0.46f, usableWidth * 0.46f, t);
                CreateLocalPrimitive("Vertical Gate Bar", PrimitiveType.Cube, barMaterial, parent, new Vector3(x, 0f, 0f), new Vector3(0.12f, gateHeight - 0.35f, 0.16f), Vector3.zero);
            }

            CreateLocalPrimitive("Gate Neon Top Trim", PrimitiveType.Cube, trimMaterial, parent, new Vector3(0f, halfHeight - 0.28f, -0.11f), new Vector3(usableWidth * 0.92f, 0.035f, 0.045f), Vector3.zero);
            CreateLocalPrimitive("Gate Neon Left Trim", PrimitiveType.Cube, trimMaterial, parent, new Vector3(-usableWidth * 0.5f, 0f, -0.11f), new Vector3(0.035f, gateHeight * 0.82f, 0.045f), Vector3.zero);
            CreateLocalPrimitive("Gate Neon Right Trim", PrimitiveType.Cube, trimMaterial, parent, new Vector3(usableWidth * 0.5f, 0f, -0.11f), new Vector3(0.035f, gateHeight * 0.82f, 0.045f), Vector3.zero);
        }

        private GameObject CreateCube(string name, Transform root, Vector3 position, Vector3 scale, Material material, bool createBaseAccent = true)
        {
            if (activeTheme != null && IsStructuralWallName(name) && ArenaWallBlockAsset.TryBuild(root, position, scale, out var wallBlock))
            {
                wallBlock.name = name;
                AddDestructibleIfNeeded(wallBlock, name, scale, material);
                if (createBaseAccent)
                {
                    CreateWallBaseAccentIfNeeded(name, root, position, scale, Quaternion.identity);
                }

                return wallBlock;
            }

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(root, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;

            if (cube.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
                DroidRenderSetup.ApplyRenderer(renderer, ResolveOutlineCategory(name, material));
            }

            AddDestructibleIfNeeded(cube, name, scale, material);
            if (createBaseAccent)
            {
                CreateWallBaseAccentIfNeeded(name, root, position, scale, Quaternion.identity);
            }

            return cube;
        }

        private GameObject CreateFloorCube(ArenaTheme theme, Transform root, string name, Vector3 position, Vector3 scale)
        {
            return generatingAllOutWar
                ? CreateVisualCube(name, root, position, scale, GetFloorMaterial(theme))
                : CreateCube(name, root, position, scale, theme.Floor);
        }

        private GameObject CreateVisualCube(string name, Transform root, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(root, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;

            RemoveCollider(cube);
            if (cube.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
                DroidRenderSetup.ApplyRenderer(renderer, ResolveOutlineCategory(name, material));
            }

            return cube;
        }

        private GameObject CreateRawCube(string name, Transform root, Vector3 position, Vector3 scale, Material material, DestructibleDamageProfile damageProfile = DestructibleDamageProfile.Wall, Vector3 biteDirection = default, Quaternion? rotation = null)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(root, false);
            cube.transform.position = position;
            var resolvedRotation = rotation ?? Quaternion.identity;
            cube.transform.rotation = resolvedRotation;
            cube.transform.localScale = scale;

            if (cube.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
                DroidRenderSetup.ApplyRenderer(renderer, ResolveOutlineCategory(name, material));
            }

            AddDestructibleIfNeeded(cube, name, scale, material, damageProfile, biteDirection);
            CreateWallBaseAccentIfNeeded(name, root, position, scale, resolvedRotation);
            return cube;
        }

        private void CreateWallBaseAccentIfNeeded(string sourceName, Transform root, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            if (GetWallBaseAccentMaterial() == null || !ShouldCreateWallBaseAccent(sourceName))
            {
                return;
            }

            const float accentThickness = 0.06f;
            const float accentOffset = 0.024f;
            const float accentLengthPadding = 0.08f;
            const float floorLift = 0.001f;
            var baseY = position.y - scale.y * 0.5f + floorLift;
            var basePosition = new Vector3(position.x, baseY, position.z);
            var isPillar = sourceName == "Room Corridor Opening Pillar";
            var horizontalMajor = scale.x >= scale.z;

            if (isPillar)
            {
                var xLength = Mathf.Max(accentThickness, scale.x + accentLengthPadding);
                var zLength = Mathf.Max(accentThickness, scale.z + accentLengthPadding);
                var zOffset = scale.z * 0.5f + accentOffset;
                var xOffset = scale.x * 0.5f + accentOffset;
                CreateNonCollidingFloorAccentRail("Pillar Base Neon Accent", root, basePosition + rotation * new Vector3(0f, 0f, zOffset), new Vector2(xLength, accentThickness), rotation);
                CreateNonCollidingFloorAccentRail("Pillar Base Neon Accent", root, basePosition + rotation * new Vector3(0f, 0f, -zOffset), new Vector2(xLength, accentThickness), rotation);
                CreateNonCollidingFloorAccentRail("Pillar Base Neon Accent", root, basePosition + rotation * new Vector3(xOffset, 0f, 0f), new Vector2(accentThickness, zLength), rotation);
                CreateNonCollidingFloorAccentRail("Pillar Base Neon Accent", root, basePosition + rotation * new Vector3(-xOffset, 0f, 0f), new Vector2(accentThickness, zLength), rotation);
                return;
            }

            if (horizontalMajor)
            {
                var length = Mathf.Max(accentThickness, scale.x + accentLengthPadding);
                var zOffset = scale.z * 0.5f + accentOffset;
                var xOffset = scale.x * 0.5f + accentOffset;
                var capLength = Mathf.Max(accentThickness, scale.z + accentOffset * 2f + accentThickness);
                CreateNonCollidingFloorAccentRail("Wall Base Neon Accent", root, basePosition + rotation * new Vector3(0f, 0f, zOffset), new Vector2(length, accentThickness), rotation);
                CreateNonCollidingFloorAccentRail("Wall Base Neon Accent", root, basePosition + rotation * new Vector3(0f, 0f, -zOffset), new Vector2(length, accentThickness), rotation);
                CreateNonCollidingFloorAccentRail("Wall Base Neon Accent", root, basePosition + rotation * new Vector3(xOffset, 0f, 0f), new Vector2(accentThickness, capLength), rotation);
                CreateNonCollidingFloorAccentRail("Wall Base Neon Accent", root, basePosition + rotation * new Vector3(-xOffset, 0f, 0f), new Vector2(accentThickness, capLength), rotation);
                return;
            }

            var zLengthWall = Mathf.Max(accentThickness, scale.z + accentLengthPadding);
            var xFaceOffset = scale.x * 0.5f + accentOffset;
            var zFaceOffset = scale.z * 0.5f + accentOffset;
            var sideCapLength = Mathf.Max(accentThickness, scale.x + accentOffset * 2f + accentThickness);
            CreateNonCollidingFloorAccentRail("Wall Base Neon Accent", root, basePosition + rotation * new Vector3(xFaceOffset, 0f, 0f), new Vector2(accentThickness, zLengthWall), rotation);
            CreateNonCollidingFloorAccentRail("Wall Base Neon Accent", root, basePosition + rotation * new Vector3(-xFaceOffset, 0f, 0f), new Vector2(accentThickness, zLengthWall), rotation);
            CreateNonCollidingFloorAccentRail("Wall Base Neon Accent", root, basePosition + rotation * new Vector3(0f, 0f, zFaceOffset), new Vector2(sideCapLength, accentThickness), rotation);
            CreateNonCollidingFloorAccentRail("Wall Base Neon Accent", root, basePosition + rotation * new Vector3(0f, 0f, -zFaceOffset), new Vector2(sideCapLength, accentThickness), rotation);
        }

        private GameObject CreateNonCollidingFloorAccentRail(string name, Transform root, Vector3 position, Vector2 size, Quaternion rotation)
        {
            var rail = new GameObject(name);
            rail.transform.SetParent(root, false);
            rail.transform.position = position;
            rail.transform.rotation = rotation;

            var mesh = CreateRoundedFloorAccentRailMesh(name + " Mesh", size);
            var meshFilter = rail.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            var renderer = rail.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetWallBaseAccentMaterial();
            DroidRenderSetup.ApplyRenderer(renderer, StylizedOutlineCategory.None);
            return rail;
        }

        private static Mesh CreateRoundedFloorAccentRailMesh(string name, Vector2 size)
        {
            const int crossSegments = 8;
            var runsAlongX = size.x >= size.y;
            var length = Mathf.Max(0.001f, runsAlongX ? size.x : size.y);
            var width = Mathf.Max(0.001f, runsAlongX ? size.y : size.x);
            var halfLength = length * 0.5f;
            var halfWidth = width * 0.5f;
            var height = Mathf.Clamp(width * 0.82f, 0.024f, 0.06f);
            var vertices = new List<Vector3>((crossSegments + 1) * 2);
            var triangles = new List<int>(crossSegments * 12 + crossSegments * 12);

            for (var end = 0; end < 2; end++)
            {
                var axial = end == 0 ? -halfLength : halfLength;
                for (var i = 0; i <= crossSegments; i++)
                {
                    var angle = i / (float)crossSegments * Mathf.PI;
                    var across = Mathf.Cos(angle) * halfWidth;
                    var y = Mathf.Sin(angle) * height;
                    vertices.Add(runsAlongX
                        ? new Vector3(axial, y, across)
                        : new Vector3(across, y, axial));
                }
            }

            var row = crossSegments + 1;
            for (var i = 0; i < crossSegments; i++)
            {
                var a = i;
                var b = i + 1;
                var c = row + i + 1;
                var d = row + i;
                AddDoubleSidedTriangle(triangles, a, b, c);
                AddDoubleSidedTriangle(triangles, a, c, d);
            }

            for (var i = 1; i < crossSegments; i++)
            {
                AddDoubleSidedTriangle(triangles, 0, i, i + 1);
                AddDoubleSidedTriangle(triangles, row, row + i + 1, row + i);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddDoubleSidedTriangle(List<int> triangles, int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(b);
        }

        private static void AddDoubleSidedQuad(List<int> triangles, int a, int b, int c, int d)
        {
            AddDoubleSidedTriangle(triangles, a, b, c);
            AddDoubleSidedTriangle(triangles, a, c, d);
        }

        private static void AddSingleSidedQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(d);
        }

        private static void AddSingleSidedQuadFacing(List<int> triangles, List<Vector3> vertices, int a, int b, int c, int d, Vector3 desiredNormal)
        {
            if (vertices == null ||
                a < 0 || b < 0 || c < 0 || d < 0 ||
                a >= vertices.Count || b >= vertices.Count || c >= vertices.Count || d >= vertices.Count ||
                desiredNormal.sqrMagnitude <= 0.001f)
            {
                AddSingleSidedQuad(triangles, a, b, c, d);
                return;
            }

            var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (normal.sqrMagnitude > 0.001f && Vector3.Dot(normal, desiredNormal) < 0f)
            {
                AddSingleSidedQuad(triangles, a, d, c, b);
                return;
            }

            AddSingleSidedQuad(triangles, a, b, c, d);
        }

        private void ConfigureAllOutWarHexFloorMaterial(ArenaTheme theme, float corridorWidth, ArenaLayout layout)
        {
            var shader = Shader.Find("ArenaShooter/WorldSpaceHexFloor");
            if (shader == null)
            {
                allOutWarHexFloorTheme = theme;
                allOutWarHexFloorMaterial = null;
                return;
            }

            if (allOutWarHexFloorMaterial == null ||
                allOutWarHexFloorTheme != theme ||
                allOutWarHexFloorMaterial.shader != shader)
            {
                allOutWarHexFloorTheme = theme;
                allOutWarHexFloorMaterial = new Material(shader)
                {
                    name = "All Out War Red Hex Floor"
                };
            }

            var hexSize = Mathf.Clamp(corridorWidth * 0.15f, 0.38f, 0.55f);
            var lineWidth = Mathf.Clamp(hexSize * 0.006f, 0.0025f, 0.005f);
            var domeRadius = layout != null ? Mathf.Max(1f, layout.DomeRadius) : 72f;
            var origin = layout != null ? layout.CircularCenter : Vector3.zero;
            var waveWidth = Mathf.Clamp(domeRadius * 0.065f, 5.5f, 8.5f);
            var waveSoftness = Mathf.Clamp(waveWidth * 1.45f, 8f, 13f);
            var waveTravelSpan = domeRadius * 2f + waveWidth * 2f + waveSoftness * 2f;
            var waveRestartGap = Mathf.Clamp(domeRadius * 0.45f, 32f, 72f);
            var wavePeriod = waveTravelSpan + waveRestartGap;
            var waveSpeed = Mathf.Clamp(domeRadius * 0.18f, 12f, 22f);
            var wallMatteBlack = ResolveMaterialBaseColor(theme?.Wall, new Color(0.0015f, 0.001f, 0.004f, 1f));
            SetMaterialColor(allOutWarHexFloorMaterial, HexFloorBaseColorId, wallMatteBlack);
            SetMaterialColor(allOutWarHexFloorMaterial, HexFloorLineColorId, new Color(0.85f, 0.018f, 0.012f, 1f));
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorHexSizeId, hexSize);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorLineWidthId, lineWidth);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorEmissionStrengthId, 0.45f);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorPulseSpeedId, 0f);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorPulseStrengthId, 0f);
            SetMaterialVector(allOutWarHexFloorMaterial, HexFloorPatternOriginId, new Vector4(origin.x, 0f, origin.z, 0f));
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorNormalFadeStartId, 0.18f);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorNormalFadeEndId, 0.42f);
            SetMaterialVector(allOutWarHexFloorMaterial, HexFloorWaveDirectionId, new Vector4(0.85f, 0.48f, 0f, 0f));
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorWaveSpeedId, waveSpeed);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorWavePeriodId, wavePeriod);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorWaveTravelSpanId, waveTravelSpan);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorWaveRestartGapId, waveRestartGap);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorWaveWidthId, waveWidth);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorWaveSoftnessId, waveSoftness);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorIdleLineStrengthId, 0f);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorWaveLineStrengthId, 0.78f);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorHillIdleLineStrengthId, 0.16f);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorHillIdleHeightStartId, 0.12f);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorHillIdleHeightEndId, 1.15f);
        }

        private static Color ResolveMaterialBaseColor(Material material, Color fallback)
        {
            if (material == null)
            {
                return fallback;
            }

            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            if (material.HasProperty("_Color"))
            {
                return material.GetColor("_Color");
            }

            return fallback;
        }

        private Material GetFloorMaterial(ArenaTheme theme)
        {
            if (generatingAllOutWar && allOutWarHexFloorMaterial != null)
            {
                return allOutWarHexFloorMaterial;
            }

            return theme.Floor;
        }

        private void ApplyAllOutWarFloorMaterial(GameObject target, ArenaTheme theme)
        {
            if (!generatingAllOutWar || target == null)
            {
                return;
            }

            var material = GetFloorMaterial(theme);
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private Material GetWallBaseAccentMaterial()
        {
            if (activeTheme == null || activeTheme.NeonA == null)
            {
                return null;
            }

            if (wallBaseAccentMaterial != null && wallBaseAccentTheme == activeTheme)
            {
                return wallBaseAccentMaterial;
            }

            wallBaseAccentTheme = activeTheme;
            wallBaseAccentMaterial = new Material(activeTheme.NeonA)
            {
                name = "Hot Purple Wall Base Rail"
            };
            var hotPurple = new Color(1.55f, 0.18f, 3.2f, 1f);
            SetMaterialColor(wallBaseAccentMaterial, hotPurple);
            wallBaseAccentMaterial.EnableKeyword("_EMISSION");
            if (wallBaseAccentMaterial.HasProperty("_EmissionColor"))
            {
                wallBaseAccentMaterial.SetColor("_EmissionColor", hotPurple);
            }

            return wallBaseAccentMaterial;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static void SetMaterialColor(Material material, int propertyId, Color color)
        {
            if (material != null && material.HasProperty(propertyId))
            {
                material.SetColor(propertyId, color);
            }
        }

        private static void SetMaterialFloat(Material material, int propertyId, float value)
        {
            if (material != null && material.HasProperty(propertyId))
            {
                material.SetFloat(propertyId, value);
            }
        }

        private static void SetMaterialVector(Material material, int propertyId, Vector4 value)
        {
            if (material != null && material.HasProperty(propertyId))
            {
                material.SetVector(propertyId, value);
            }
        }

        private static void RemoveCollider(GameObject target)
        {
            if (target != null && target.TryGetComponent<Collider>(out var collider))
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }
        }

        private static void RemoveCollidersInChildren(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            foreach (var collider in target.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }
        }

        private void AddDestructibleIfNeeded(GameObject target, string name, Vector3 scale, Material material, DestructibleDamageProfile damageProfile = DestructibleDamageProfile.Wall, Vector3 biteDirection = default)
        {
            if (!IsDestructibleName(name))
            {
                return;
            }

            var destructible = target.GetComponent<DestructibleArenaPiece>();
            if (destructible == null)
            {
                destructible = target.AddComponent<DestructibleArenaPiece>();
            }

            var sizeScore = Mathf.Max(scale.x, scale.z) + scale.y * 0.35f;
            var outlineCategory = ResolveDamageContourCategory(name, material);
            if (damageProfile == DestructibleDamageProfile.Wall && outlineCategory == StylizedOutlineCategory.Floor)
            {
                damageProfile = DestructibleDamageProfile.Floor;
            }

            destructible.Configure(Mathf.Lerp(220f, 620f, Mathf.Clamp01(sizeScore / 12f)), scale, material, outlineCategory, damageProfile, biteDirection);
        }

        private static bool IsStructuralWallName(string name)
        {
            return name == "Arena Wall" ||
                   name == "Arena Wall Door Segment" ||
                   name == "Corridor Wall" ||
                   name == "Gate Wall" ||
                   name == "Gate Connector Wall";
        }

        private static bool ShouldCreateWallBaseAccent(string name)
        {
            return IsStructuralWallName(name) || name == "Room Corridor Opening Pillar";
        }

        private static bool IsDestructibleName(string name)
        {
            return name == "Arena Wall" ||
                   name == "Arena Wall Door Segment" ||
                   name == "Corridor Wall" ||
                   name == "Gate Wall" ||
                   name == "Gate Connector Wall" ||
                   name == "Room Corridor Opening Pillar" ||
                   name == "Room Floor" ||
                   name == "Corridor Floor" ||
                   name == "Gate Floor" ||
                   name == "Gate Connector Floor" ||
                   name == "Spawn Tunnel Floor Extension";
        }

        private StylizedOutlineCategory ResolveDamageContourCategory(string name, Material material)
        {
            var key = name.ToLowerInvariant();
            if (key.Contains("floor"))
            {
                return StylizedOutlineCategory.Floor;
            }

            if (key.Contains("wall") ||
                key.Contains("pillar") ||
                key.Contains("gate"))
            {
                return StylizedOutlineCategory.Wall;
            }

            if (activeTheme != null && (material == activeTheme.Floor || material == activeTheme.GateInterior))
            {
                return StylizedOutlineCategory.Floor;
            }

            if (activeTheme != null && (material == activeTheme.Wall || material == activeTheme.Pillar || material == activeTheme.GateEndWall))
            {
                return StylizedOutlineCategory.Wall;
            }

            return StylizedOutlineCategory.None;
        }

        private GameObject CreateLocalPrimitive(string name, PrimitiveType primitiveType, Material material, Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 localRotation)
        {
            var primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
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
                DroidRenderSetup.ApplyRenderer(renderer, ResolveOutlineCategory(name, material));
            }

            return primitive;
        }

        private GameObject CreateLocalStructuralPrimitive(string name, PrimitiveType primitiveType, Material material, Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 localRotation)
        {
            var primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            primitive.transform.localRotation = Quaternion.Euler(localRotation);

            if (primitive.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
                DroidRenderSetup.ApplyRenderer(renderer, ResolveOutlineCategory(name, material));
            }

            return primitive;
        }

        private StylizedOutlineCategory ResolveOutlineCategory(string name, Material material)
        {
            var key = name.ToLowerInvariant();
            if (key.Contains("boundary line") || key.Contains("grid line") || key.Contains("neon"))
            {
                return StylizedOutlineCategory.None;
            }

            if (key.Contains("floor"))
            {
                return StylizedOutlineCategory.None;
            }

            if (key.Contains("wall") ||
                key.Contains("pillar") ||
                key.Contains("gate") ||
                key.Contains("lantern") ||
                key.Contains("rail") ||
                key.Contains("bar"))
            {
                return StylizedOutlineCategory.Wall;
            }

            if (activeTheme != null && (material == activeTheme.Floor || material == activeTheme.GateInterior))
            {
                return StylizedOutlineCategory.None;
            }

            if (activeTheme != null && (material == activeTheme.Wall || material == activeTheme.Pillar || material == activeTheme.GateEndWall))
            {
                return StylizedOutlineCategory.Wall;
            }

            return StylizedOutlineCategory.None;
        }
    }
}
