using System.Collections.Generic;
using UnityEngine;

namespace ArenaShooter
{
    public sealed class ArenaGenerator : MonoBehaviour
    {
        private ArenaTheme activeTheme;
        private ArenaTheme wallBaseAccentTheme;
        private Material wallBaseAccentMaterial;
        private ArenaTheme allOutWarHexFloorTheme;
        private Material allOutWarHexFloorMaterial;
        private bool generatingAllOutWar;

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

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        public static int EstimateAllOutWarTerrainGridBonus(int seed, float spacing)
        {
            return AllOutWarTerrainProfile.EstimateGridBonus(seed, spacing);
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

        public ArenaLayout GenerateAllOutWar(ArenaTheme theme, Transform root, int seed, int totalArmies, int targetRooms, int gridRadius, float roomSize, float corridorLength, float corridorWidth, float wallHeight, int pickupCount)
        {
            activeTheme = theme;
            generatingAllOutWar = true;
            ConfigureAllOutWarHexFloorMaterial(theme, corridorWidth);
            var random = new System.Random(seed);
            var spacing = roomSize + corridorLength;
            var terrainProfile = new AllOutWarTerrainProfile(seed, spacing);
            var layout = BuildCircularRoomGraph(random, Mathf.Max(3, totalArmies), targetRooms, gridRadius, roomSize, spacing, terrainProfile);
            terrainProfile.BuildFlatMasks(layout, roomSize, corridorLength, corridorWidth);

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

            CreateAllOutWarDomeFloor(theme, root, layout.CircularCenter, layout.DomeRadius, terrainProfile);
            CreateInvisibleCircularBoundary(root, layout.CircularCenter, layout.DomeRadius, wallHeight);
            generatingAllOutWar = false;
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
            BuildAllOutWarTerrainAwareRoomSet(layout, allowed, spawnProtectedRooms, random, totalArmies, targetRooms, gridRadius, roomSize, spacing, terrainProfile);
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
            float roomSize,
            float spacing,
            AllOutWarTerrainProfile terrainProfile)
        {
            AddForcedCenterRooms(layout.Rooms, allowed);

            foreach (var room in spawnProtectedRooms)
            {
                layout.Rooms.Add(room);
            }

            for (var team = 0; team < totalArmies; team++)
            {
                var frontRoom = FindNearestAllowedCell(allowed, GetArmySpawnFrontTarget(team, totalArmies, gridRadius));
                CarvePathToCenter(layout, allowed, frontRoom);
            }

            var buildableRooms = new List<Vector2Int>();
            foreach (var cell in allowed)
            {
                if (layout.Rooms.Contains(cell))
                {
                    continue;
                }

                if (terrainProfile == null || terrainProfile.IsRoomCellBuildable(cell, spacing, roomSize, false))
                {
                    buildableRooms.Add(cell);
                }
            }

            ShuffleRoomList(buildableRooms, random);
            foreach (var room in buildableRooms)
            {
                layout.Rooms.Add(room);
            }

            var minimumRooms = CalculateAllOutWarMinimumRoomCount(allowed.Count, totalArmies, targetRooms);
            KeepRoomsConnectedToCenter(layout.Rooms);
            if (layout.Rooms.Count < minimumRooms && terrainProfile != null)
            {
                GrowConnectedRoomSet(
                    layout.Rooms,
                    allowed,
                    terrainProfile,
                    spacing,
                    roomSize,
                    minimumRooms,
                    candidate => terrainProfile.IsRoomCellBuildable(candidate, spacing, roomSize, true));
            }

            if (layout.Rooms.Count < minimumRooms)
            {
                GrowConnectedRoomSet(layout.Rooms, allowed, terrainProfile, spacing, roomSize, minimumRooms, null);
            }
        }

        private static int CalculateAllOutWarMinimumRoomCount(int allowedRoomCount, int totalArmies, int targetRooms)
        {
            return Mathf.Min(allowedRoomCount, Mathf.Max(totalArmies * 5 + 12, Mathf.CeilToInt(targetRooms * 0.58f)));
        }

        private static void KeepRoomsConnectedToCenter(HashSet<Vector2Int> rooms)
        {
            if (!rooms.Contains(Vector2Int.zero))
            {
                return;
            }

            var connected = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            connected.Add(Vector2Int.zero);
            queue.Enqueue(Vector2Int.zero);

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
            AllOutWarTerrainProfile terrainProfile,
            float spacing,
            float roomSize,
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
                    var score = terrainProfile != null
                        ? terrainProfile.GetRoomCellTerrainScore(candidate, spacing, roomSize)
                        : candidate.sqrMagnitude * 0.001f;
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
            var candidates = new List<Vector2Int>(roomPool);
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

                    if (protectedRooms != null && protectedRooms.Contains(room))
                    {
                        footprint.Clear();
                        return false;
                    }

                    footprint.Add(room);
                }
            }

            return footprint.Count > 0 && (terrainProfile == null || terrainProfile.IsClearingFootprintBuildable(footprint, spacing));
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
            var best = Vector2Int.zero;
            var bestDistance = float.PositiveInfinity;
            foreach (var cell in allowed)
            {
                var distance = (new Vector2(cell.x, cell.y) - target).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = cell;
                }
            }

            return best;
        }

        private static void CarvePathToCenter(ArenaLayout layout, HashSet<Vector2Int> allowed, Vector2Int start)
        {
            var current = start;
            var guard = 0;
            while (guard++ < 64 && allowed.Contains(current))
            {
                layout.Rooms.Add(current);
                if (current == Vector2Int.zero)
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

        private void AddAllOutWarPickups(ArenaLayout layout, System.Random random, int pickupCount)
        {
            var rooms = new List<Vector2Int>(layout.Rooms);
            for (var i = 0; i < rooms.Count; i++)
            {
                var swapIndex = random.Next(i, rooms.Count);
                (rooms[i], rooms[swapIndex]) = (rooms[swapIndex], rooms[i]);
            }

            foreach (var room in rooms)
            {
                if (layout.PickupPoints.Count >= pickupCount)
                {
                    break;
                }

                var offset = new Vector3((float)(random.NextDouble() - 0.5) * 4.2f, 0.55f, (float)(random.NextDouble() - 0.5) * 4.2f);
                if (layout.RoomCenters.TryGetValue(room, out var center))
                {
                    var position = center + offset;
                    if (IsAllOutWarSpawnOpenRoom(layout, room, center, 4.2f) || IsInAllOutWarSpawnOpenZone(layout, position, 1.2f))
                    {
                        continue;
                    }

                    layout.PickupPoints.Add(position);
                }
                else
                {
                    var position = new Vector3(room.x * layout.CellSpacing, 0.55f, room.y * layout.CellSpacing) + offset;
                    if (IsInAllOutWarSpawnOpenZone(layout, position, 1.2f))
                    {
                        continue;
                    }

                    layout.PickupPoints.Add(position);
                }
            }
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

            var mesh = CreateTerrainDiskMesh("All Out War Continuous Dome Floor Mesh", center + Vector3.down * 0.003f, radius, terrainProfile);
            var meshFilter = floor.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var renderer = floor.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetFloorMaterial(theme);
            DroidRenderSetup.ApplyRenderer(renderer, StylizedOutlineCategory.None);

            var collider = floor.AddComponent<MeshCollider>();
            collider.sharedMesh = CreateTerrainDiskMesh("All Out War Continuous Dome Floor Collider Mesh", center, radius, terrainProfile);
        }

        private static Mesh CreateTerrainDiskMesh(string meshName, Vector3 center, float radius, AllOutWarTerrainProfile terrainProfile)
        {
            var ringCount = Mathf.Clamp(Mathf.CeilToInt(radius / 3.2f), 32, 72);
            var segmentCount = Mathf.Clamp(Mathf.CeilToInt(radius * 1.15f), 128, 224);
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
                triangles.Add(0);
                triangles.Add(1 + next);
                triangles.Add(1 + segment);
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

                    triangles.Add(innerCurrent);
                    triangles.Add(innerNext);
                    triangles.Add(outerCurrent);
                    triangles.Add(innerNext);
                    triangles.Add(outerNext);
                    triangles.Add(outerCurrent);
                }
            }

            var mesh = new Mesh { name = meshName };
            mesh.vertices = vertices;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 AddTerrainHeight(Vector3 position, AllOutWarTerrainProfile terrainProfile)
        {
            if (terrainProfile != null)
            {
                position.y += terrainProfile.SampleHeight(position);
            }

            return position;
        }

        private sealed class AllOutWarTerrainProfile
        {
            private const float FlatPatchWeightEpsilon = 0.0001f;
            private const int CorridorPatchPriority = 0;
            private const int RoomPatchPriority = 10;
            private const int SpawnPatchPriority = 20;

            private readonly List<FlatPatch> flatPatches = new();
            private readonly Vector2 primaryOffset;
            private readonly Vector2 secondaryOffset;
            private readonly Vector2 valleyOffset;
            private readonly Vector2 largeHillOffset;
            private readonly float primaryScale;
            private readonly float secondaryScale;
            private readonly float valleyScale;
            private readonly float largeHillScale;
            private readonly float largeHillAmplitude;

            public AllOutWarTerrainProfile(int seed, float spacing)
            {
                var settings = CreateSettings(seed, spacing);
                primaryOffset = settings.PrimaryOffset;
                secondaryOffset = settings.SecondaryOffset;
                valleyOffset = settings.ValleyOffset;
                largeHillOffset = settings.LargeHillOffset;
                primaryScale = settings.PrimaryScale;
                secondaryScale = settings.SecondaryScale;
                valleyScale = settings.ValleyScale;
                largeHillScale = settings.LargeHillScale;
                largeHillAmplitude = settings.LargeHillAmplitude;
            }

            public static int EstimateGridBonus(int seed, float spacing)
            {
                var settings = CreateSettings(seed, spacing);
                return settings.LargeHillAmplitude >= 2.35f && settings.LargeHillScale >= Mathf.Max(82f, spacing * 7.3f) ? 1 : 0;
            }

            public void BuildFlatMasks(ArenaLayout layout, float roomSize, float corridorLength, float corridorWidth)
            {
                flatPatches.Clear();
                if (layout == null)
                {
                    return;
                }

                ApplyHilltopRoomHeights(layout, roomSize, corridorWidth);

                var roomFalloff = Mathf.Max(1.8f, roomSize * 0.22f);
                foreach (var center in layout.RoomCenters.Values)
                {
                    AddFlatRect(ToXZ(center), roomSize * 0.55f, roomSize * 0.55f, roomFalloff, center.y, RoomPatchPriority);
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

                        var corridorCenter = (from + to) * 0.5f;
                        var openClearingLink = layout.AreRoomsInSameClearing(room, neighbor);
                        var openSpawnZoneLink = IsInAllOutWarSpawnOpenZone(layout, corridorCenter, corridorWidth * 0.5f) ||
                            IsSegmentInAllOutWarSpawnOpenZone(layout, from, to, corridorWidth * 0.5f);
                        var openFloorLink = openClearingLink || openSpawnZoneLink;
                        var center = ToXZ(corridorCenter);
                        var floorHalfWidth = openFloorLink
                            ? roomSize * 0.5f
                            : corridorWidth * 0.5f;
                        var floorHalfLength = (corridorLength + 0.4f) * 0.5f;
                        var heightDelta = Mathf.Abs(from.y - to.y);
                        if (direction == Vector2Int.up)
                        {
                            if (heightDelta > 0.08f)
                            {
                                AddRampRect(center, floorHalfWidth, floorHalfLength, from.y, to.y, false, roomFalloff, CorridorPatchPriority);
                            }
                            else
                            {
                                AddFlatRect(center, floorHalfWidth, floorHalfLength, roomFalloff, (from.y + to.y) * 0.5f, CorridorPatchPriority);
                            }
                        }
                        else
                        {
                            if (heightDelta > 0.08f)
                            {
                                AddRampRect(center, floorHalfLength, floorHalfWidth, from.y, to.y, true, roomFalloff, CorridorPatchPriority);
                            }
                            else
                            {
                                AddFlatRect(center, floorHalfLength, floorHalfWidth, roomFalloff, (from.y + to.y) * 0.5f, CorridorPatchPriority);
                            }
                        }
                    }
                }

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
            }

            public float SampleHeight(Vector3 position)
            {
                var point = new Vector2(position.x, position.z);
                var rawHeight = SampleRawHeight(point);
                var flatWeight = 0f;
                var targetHeight = 0f;
                var selectedPriority = int.MinValue;
                var hasSelectedPatch = false;
                for (var i = 0; i < flatPatches.Count; i++)
                {
                    var patch = flatPatches[i];
                    var weight = patch.Evaluate(point);
                    if (weight <= 0f)
                    {
                        continue;
                    }

                    var isStrongerPatch = weight > flatWeight + FlatPatchWeightEpsilon;
                    var isPriorityTieBreak = Mathf.Abs(weight - flatWeight) <= FlatPatchWeightEpsilon && patch.Priority > selectedPriority;
                    if (!hasSelectedPatch || isStrongerPatch || isPriorityTieBreak)
                    {
                        flatWeight = weight;
                        targetHeight = patch.TargetHeight(point);
                        selectedPriority = patch.Priority;
                        hasSelectedPatch = true;
                    }
                }

                if (!hasSelectedPatch)
                {
                    return rawHeight;
                }

                var smoothedWeight = flatWeight * flatWeight * (3f - 2f * flatWeight);
                return Mathf.Lerp(rawHeight, targetHeight, smoothedWeight);
            }

            private void ApplyHilltopRoomHeights(ArenaLayout layout, float roomSize, float doorwayWidth)
            {
                var hilltopHeights = new Dictionary<Vector2Int, float>();
                foreach (var room in layout.Rooms)
                {
                    if (!layout.RoomCenters.TryGetValue(room, out var center))
                    {
                        continue;
                    }

                    center.y = 0f;
                    layout.RoomCenters[room] = center;
                    if (layout.ClearingRoomGroups.ContainsKey(room) ||
                        IsSpawnProtectedHilltopRoom(layout, room, center, roomSize) ||
                        !IsHilltopArchitectureEligible(layout, room, center, roomSize, doorwayWidth) ||
                        !TryGetHilltopRoomHeight(room, layout.CellSpacing, roomSize, out var hilltopHeight))
                    {
                        continue;
                    }

                    hilltopHeights[room] = hilltopHeight;
                }

                foreach (var hilltop in hilltopHeights)
                {
                    var center = layout.RoomCenters[hilltop.Key];
                    center.y = hilltop.Value;
                    layout.RoomCenters[hilltop.Key] = center;
                }
            }

            public bool IsRoomCellBuildable(Vector2Int cell, float spacing, float roomSize, bool relaxed)
            {
                GetRoomCellTerrainMetrics(cell, spacing, roomSize, out var heightSpread, out var maxSlope, out _);
                var spreadLimit = relaxed ? 2.25f : 1.35f;
                var slopeLimit = relaxed ? 0.26f : 0.18f;
                return heightSpread <= spreadLimit && maxSlope <= slopeLimit;
            }

            public float GetRoomCellTerrainScore(Vector2Int cell, float spacing, float roomSize)
            {
                GetRoomCellTerrainMetrics(cell, spacing, roomSize, out var heightSpread, out var maxSlope, out var centerHeight);
                return heightSpread + maxSlope * 10f + Mathf.Abs(centerHeight) * 0.08f;
            }

            public bool IsClearingFootprintBuildable(List<Vector2Int> footprint, float spacing)
            {
                if (footprint == null || footprint.Count == 0)
                {
                    return false;
                }

                var minHeight = float.PositiveInfinity;
                var maxHeight = float.NegativeInfinity;
                var maxSlope = 0f;
                foreach (var room in footprint)
                {
                    var point = new Vector2(room.x * spacing, room.y * spacing);
                    var height = SampleRawHeight(point);
                    minHeight = Mathf.Min(minHeight, height);
                    maxHeight = Mathf.Max(maxHeight, height);
                    maxSlope = Mathf.Max(maxSlope, EstimateRawSlope(point, spacing));
                }

                var spreadLimit = 1.25f + Mathf.Sqrt(footprint.Count) * 0.32f;
                return maxHeight - minHeight <= spreadLimit && maxSlope <= 0.18f;
            }

            private bool TryGetHilltopRoomHeight(Vector2Int cell, float spacing, float roomSize, out float height)
            {
                GetRoomCellTerrainMetrics(cell, spacing, roomSize, out var heightSpread, out var maxSlope, out var centerHeight);
                if (centerHeight < 1.15f || heightSpread > 1.2f || maxSlope > 0.16f)
                {
                    height = 0f;
                    return false;
                }

                height = Mathf.Clamp(centerHeight, 1.15f, 4.75f);
                return true;
            }

            private void GetRoomCellTerrainMetrics(Vector2Int cell, float spacing, float roomSize, out float heightSpread, out float maxSlope, out float centerHeight)
            {
                var center = new Vector2(cell.x * spacing, cell.y * spacing);
                var edge = Mathf.Max(2f, roomSize * 0.46f);
                var corner = edge * 0.78f;
                var minHeight = float.PositiveInfinity;
                var maxHeight = float.NegativeInfinity;
                maxSlope = 0f;

                IncludeTerrainMetricSample(center, spacing, ref minHeight, ref maxHeight, ref maxSlope);
                centerHeight = SampleRawHeight(center);
                IncludeTerrainMetricSample(center + new Vector2(edge, 0f), spacing, ref minHeight, ref maxHeight, ref maxSlope);
                IncludeTerrainMetricSample(center + new Vector2(-edge, 0f), spacing, ref minHeight, ref maxHeight, ref maxSlope);
                IncludeTerrainMetricSample(center + new Vector2(0f, edge), spacing, ref minHeight, ref maxHeight, ref maxSlope);
                IncludeTerrainMetricSample(center + new Vector2(0f, -edge), spacing, ref minHeight, ref maxHeight, ref maxSlope);
                IncludeTerrainMetricSample(center + new Vector2(corner, corner), spacing, ref minHeight, ref maxHeight, ref maxSlope);
                IncludeTerrainMetricSample(center + new Vector2(corner, -corner), spacing, ref minHeight, ref maxHeight, ref maxSlope);
                IncludeTerrainMetricSample(center + new Vector2(-corner, corner), spacing, ref minHeight, ref maxHeight, ref maxSlope);
                IncludeTerrainMetricSample(center + new Vector2(-corner, -corner), spacing, ref minHeight, ref maxHeight, ref maxSlope);

                heightSpread = maxHeight - minHeight;
            }

            private void IncludeTerrainMetricSample(Vector2 point, float spacing, ref float minHeight, ref float maxHeight, ref float maxSlope)
            {
                var height = SampleRawHeight(point);
                minHeight = Mathf.Min(minHeight, height);
                maxHeight = Mathf.Max(maxHeight, height);
                maxSlope = Mathf.Max(maxSlope, EstimateRawSlope(point, spacing));
            }

            private float EstimateRawSlope(Vector2 point, float spacing)
            {
                var sampleDistance = Mathf.Max(4f, spacing * 0.35f);
                var xSlope = Mathf.Abs(SampleRawHeight(point + Vector2.right * sampleDistance) - SampleRawHeight(point - Vector2.right * sampleDistance)) / (sampleDistance * 2f);
                var zSlope = Mathf.Abs(SampleRawHeight(point + Vector2.up * sampleDistance) - SampleRawHeight(point - Vector2.up * sampleDistance)) / (sampleDistance * 2f);
                return Mathf.Sqrt(xSlope * xSlope + zSlope * zSlope);
            }

            private float SampleRawHeight(Vector2 point)
            {
                var primary = Mathf.PerlinNoise((point.x + primaryOffset.x) / primaryScale, (point.y + primaryOffset.y) / primaryScale) * 2f - 1f;
                var secondary = Mathf.PerlinNoise((point.x + secondaryOffset.x) / secondaryScale, (point.y + secondaryOffset.y) / secondaryScale) * 2f - 1f;
                var valley = Mathf.PerlinNoise((point.x + valleyOffset.x) / valleyScale, (point.y + valleyOffset.y) / valleyScale) * 2f - 1f;
                var largeHill = Mathf.PerlinNoise((point.x + largeHillOffset.x) / largeHillScale, (point.y + largeHillOffset.y) / largeHillScale) * 2f - 1f;
                return primary * 2.2f + secondary * 0.75f + valley * 1.25f + Mathf.Max(0f, largeHill) * largeHillAmplitude;
            }

            private void AddFlatRect(Vector2 center, float halfWidth, float halfDepth, float falloff, float targetHeight, int priority)
            {
                flatPatches.Add(FlatPatch.Rect(center, Mathf.Max(0.1f, halfWidth), Mathf.Max(0.1f, halfDepth), Mathf.Max(0.1f, falloff), targetHeight, priority));
            }

            private void AddFlatCircle(Vector2 center, float radius, float falloff, float targetHeight, int priority)
            {
                flatPatches.Add(FlatPatch.Circle(center, Mathf.Max(0.1f, radius), Mathf.Max(0.1f, falloff), targetHeight, priority));
            }

            private void AddRampRect(Vector2 center, float halfWidth, float halfDepth, float startHeight, float endHeight, bool alongX, float falloff, int priority)
            {
                flatPatches.Add(FlatPatch.RampRect(center, Mathf.Max(0.1f, halfWidth), Mathf.Max(0.1f, halfDepth), startHeight, endHeight, alongX, Mathf.Max(0.1f, falloff), priority));
            }

            private static TerrainSettings CreateSettings(int seed, float spacing)
            {
                var random = new System.Random(seed & 0x7fffffff);
                var largeHillRoll = (float)random.NextDouble();
                return new TerrainSettings(
                    RandomOffset(random),
                    RandomOffset(random),
                    RandomOffset(random),
                    RandomOffset(random),
                    Mathf.Max(42f, spacing * 4.1f),
                    Mathf.Max(26f, spacing * 2.35f),
                    Mathf.Max(58f, spacing * 5.6f),
                    Mathf.Lerp(Mathf.Max(74f, spacing * 6.0f), Mathf.Max(124f, spacing * 9.6f), (float)random.NextDouble()),
                    Mathf.Lerp(0.8f, 3.2f, largeHillRoll));
            }

            private static Vector2 RandomOffset(System.Random random)
            {
                return new Vector2((float)random.NextDouble() * 4000f + 250f, (float)random.NextDouble() * 4000f + 250f);
            }

            private static Vector2 ToXZ(Vector3 position)
            {
                return new Vector2(position.x, position.z);
            }

            private readonly struct TerrainSettings
            {
                public readonly Vector2 PrimaryOffset;
                public readonly Vector2 SecondaryOffset;
                public readonly Vector2 ValleyOffset;
                public readonly Vector2 LargeHillOffset;
                public readonly float PrimaryScale;
                public readonly float SecondaryScale;
                public readonly float ValleyScale;
                public readonly float LargeHillScale;
                public readonly float LargeHillAmplitude;

                public TerrainSettings(
                    Vector2 primaryOffset,
                    Vector2 secondaryOffset,
                    Vector2 valleyOffset,
                    Vector2 largeHillOffset,
                    float primaryScale,
                    float secondaryScale,
                    float valleyScale,
                    float largeHillScale,
                    float largeHillAmplitude)
                {
                    PrimaryOffset = primaryOffset;
                    SecondaryOffset = secondaryOffset;
                    ValleyOffset = valleyOffset;
                    LargeHillOffset = largeHillOffset;
                    PrimaryScale = primaryScale;
                    SecondaryScale = secondaryScale;
                    ValleyScale = valleyScale;
                    LargeHillScale = largeHillScale;
                    LargeHillAmplitude = largeHillAmplitude;
                }
            }

            private readonly struct FlatPatch
            {
                private readonly Vector2 center;
                private readonly Vector2 halfSize;
                private readonly float radius;
                private readonly float falloff;
                private readonly float targetHeight;
                private readonly float endHeight;
                private readonly int priority;
                private readonly bool circular;
                private readonly bool ramp;
                private readonly bool rampAlongX;

                private FlatPatch(Vector2 center, Vector2 halfSize, float radius, float falloff, float targetHeight, float endHeight, int priority, bool circular, bool ramp, bool rampAlongX)
                {
                    this.center = center;
                    this.halfSize = halfSize;
                    this.radius = radius;
                    this.falloff = falloff;
                    this.targetHeight = targetHeight;
                    this.endHeight = endHeight;
                    this.priority = priority;
                    this.circular = circular;
                    this.ramp = ramp;
                    this.rampAlongX = rampAlongX;
                }

                public int Priority => priority;

                public static FlatPatch Rect(Vector2 center, float halfWidth, float halfDepth, float falloff, float targetHeight, int priority)
                {
                    return new FlatPatch(center, new Vector2(halfWidth, halfDepth), 0f, falloff, targetHeight, targetHeight, priority, false, false, false);
                }

                public static FlatPatch Circle(Vector2 center, float radius, float falloff, float targetHeight, int priority)
                {
                    return new FlatPatch(center, Vector2.zero, radius, falloff, targetHeight, targetHeight, priority, true, false, false);
                }

                public static FlatPatch RampRect(Vector2 center, float halfWidth, float halfDepth, float startHeight, float endHeight, bool alongX, float falloff, int priority)
                {
                    return new FlatPatch(center, new Vector2(halfWidth, halfDepth), 0f, falloff, startHeight, endHeight, priority, false, true, alongX);
                }

                public float Evaluate(Vector2 point)
                {
                    var distance = circular ? Vector2.Distance(point, center) - radius : RectDistance(point);
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

                public float TargetHeight(Vector2 point)
                {
                    if (!ramp)
                    {
                        return targetHeight;
                    }

                    var axisOffset = rampAlongX
                        ? (point.x - center.x) / Mathf.Max(0.1f, halfSize.x)
                        : (point.y - center.y) / Mathf.Max(0.1f, halfSize.y);
                    var t = Mathf.Clamp01(axisOffset * 0.5f + 0.5f);
                    return Mathf.Lerp(targetHeight, endHeight, t);
                }

                private float RectDistance(Vector2 point)
                {
                    var delta = new Vector2(Mathf.Abs(point.x - center.x) - halfSize.x, Mathf.Abs(point.y - center.y) - halfSize.y);
                    var outside = new Vector2(Mathf.Max(0f, delta.x), Mathf.Max(0f, delta.y));
                    return outside.magnitude;
                }
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
            CreateRoomFloor(theme, root, center + new Vector3(0f, -0.1f, 0f), new Vector3(roomSize, 0.2f, roomSize), openSpawnRoom);
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

            if (!hasDoor && !hasGate && IsAllOutWarDomeEdgeOpening(layout, room, direction))
            {
                return;
            }

            var structuralThickness = thickness * 1.35f;
            var halfRoom = roomSize * 0.5f;
            var halfDoorway = doorwayWidth * 0.5f;

            if (!hasDoor && !hasGate)
            {
                var scale = horizontal
                    ? new Vector3(roomSize, wallHeight, structuralThickness)
                    : new Vector3(structuralThickness, wallHeight, roomSize);
                CreateCube("Arena Wall", root, wallCenter, scale, theme.Wall);
                return;
            }

            if (horizontal)
            {
                var segmentLength = halfRoom - halfDoorway;
                if (segmentLength <= 0.1f)
                {
                    return;
                }

                var leftOffset = offset + new Vector3(-(halfDoorway + segmentLength * 0.5f), 0f, 0f);
                var rightOffset = offset + new Vector3(halfDoorway + segmentLength * 0.5f, 0f, 0f);
                var scale = new Vector3(segmentLength, wallHeight, structuralThickness);
                CreateCube("Arena Wall Door Segment", root, center + leftOffset, scale, theme.Wall);
                CreateCube("Arena Wall Door Segment", root, center + rightOffset, scale, theme.Wall);
                CreateOpeningPillars(theme, root, center + offset, true, halfDoorway, wallHeight, structuralThickness);
            }
            else
            {
                var segmentLength = halfRoom - halfDoorway;
                if (segmentLength <= 0.1f)
                {
                    return;
                }

                var lowerOffset = offset + new Vector3(0f, 0f, -(halfDoorway + segmentLength * 0.5f));
                var upperOffset = offset + new Vector3(0f, 0f, halfDoorway + segmentLength * 0.5f);
                var scale = new Vector3(structuralThickness, wallHeight, segmentLength);
                CreateCube("Arena Wall Door Segment", root, center + lowerOffset, scale, theme.Wall);
                CreateCube("Arena Wall Door Segment", root, center + upperOffset, scale, theme.Wall);
                CreateOpeningPillars(theme, root, center + offset, false, halfDoorway, wallHeight, structuralThickness);
            }
        }

        private void CreateOpeningPillars(ArenaTheme theme, Transform root, Vector3 wallCenter, bool horizontalWall, float halfDoorway, float wallHeight, float wallThickness)
        {
            var pillarDepth = wallThickness * 1.9f;
            var pillarWidth = wallThickness * 1.25f;

            if (horizontalWall)
            {
                var scale = new Vector3(pillarWidth, wallHeight, pillarDepth);
                CreateRawCube("Room Corridor Opening Pillar", root, wallCenter + new Vector3(-halfDoorway, 0f, 0f), scale, theme.Pillar, DestructibleDamageProfile.CornerPillar, Vector3.right);
                CreateRawCube("Room Corridor Opening Pillar", root, wallCenter + new Vector3(halfDoorway, 0f, 0f), scale, theme.Pillar, DestructibleDamageProfile.CornerPillar, Vector3.left);
                return;
            }

            var verticalScale = new Vector3(pillarDepth, wallHeight, pillarWidth);
            CreateRawCube("Room Corridor Opening Pillar", root, wallCenter + new Vector3(0f, 0f, -halfDoorway), verticalScale, theme.Pillar, DestructibleDamageProfile.CornerPillar, Vector3.forward);
            CreateRawCube("Room Corridor Opening Pillar", root, wallCenter + new Vector3(0f, 0f, halfDoorway), verticalScale, theme.Pillar, DestructibleDamageProfile.CornerPillar, Vector3.back);
        }

        private enum HilltopWallSideKind
        {
            Open,
            FullWall,
            Doorway
        }

        private static bool IsHilltopArchitectureEligible(ArenaLayout layout, Vector2Int room, Vector3 center, float roomSize, float doorwayWidth)
        {
            const float thickness = 0.35f;
            const int maxWallPieces = 4;
            const float maxEnclosureScore = 2.05f;
            var wallPieces = 0;
            var enclosingSides = 0;
            var enclosureScore = 0f;
            var doorwayClosedFraction = Mathf.Clamp01((roomSize - Mathf.Clamp(doorwayWidth, 0f, roomSize)) / Mathf.Max(0.001f, roomSize));
            foreach (var direction in Directions)
            {
                var sideKind = GetHilltopWallSideKind(layout, room, direction, center, roomSize, thickness);
                if (sideKind == HilltopWallSideKind.Open)
                {
                    continue;
                }

                enclosingSides++;
                if (sideKind == HilltopWallSideKind.FullWall)
                {
                    wallPieces += 1;
                    enclosureScore += 1f;
                }
                else
                {
                    wallPieces += 4;
                    enclosureScore += doorwayClosedFraction;
                }

                if (wallPieces > maxWallPieces ||
                    enclosingSides > 2 ||
                    enclosureScore > maxEnclosureScore)
                {
                    return false;
                }
            }

            return wallPieces > 0;
        }

        private static HilltopWallSideKind GetHilltopWallSideKind(ArenaLayout layout, Vector2Int room, Vector2Int direction, Vector3 center, float roomSize, float thickness)
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
            if (IsInAllOutWarSpawnOpenZone(layout, wallCenter, thickness) ||
                IsSegmentInAllOutWarSpawnOpenZone(layout, wallStart, wallEnd, thickness + 0.8f))
            {
                return HilltopWallSideKind.Open;
            }

            var hasDoor = layout.Rooms.Contains(room + direction);
            if (hasDoor && layout.AreRoomsInSameClearing(room, room + direction))
            {
                return HilltopWallSideKind.Open;
            }

            if (!hasDoor && IsAllOutWarDomeEdgeOpening(layout, room, direction))
            {
                return HilltopWallSideKind.Open;
            }

            return hasDoor ? HilltopWallSideKind.Doorway : HilltopWallSideKind.FullWall;
        }

        private static bool IsSpawnProtectedHilltopRoom(ArenaLayout layout, Vector2Int room, Vector3 roomCenter, float roomSize)
        {
            if (IsAllOutWarSpawnOpenRoom(layout, room, roomCenter, roomSize * 0.85f))
            {
                return true;
            }

            foreach (var region in layout.ArmySpawnRegions)
            {
                if (region == null)
                {
                    continue;
                }

                if (room == region.Room)
                {
                    return true;
                }

                if (IsOnSpawnPathToCenter(region.Room, room))
                {
                    return true;
                }

                var delta = new Vector2(room.x - region.Room.x, room.y - region.Room.y);
                var outward = new Vector2(region.OutwardDirection.x, region.OutwardDirection.z);
                var tangent = new Vector2(region.TangentDirection.x, region.TangentDirection.z);
                if (Mathf.Abs(Vector2.Dot(delta, outward)) <= 1.25f &&
                    Mathf.Abs(Vector2.Dot(delta, tangent)) <= 1.25f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOnSpawnPathToCenter(Vector2Int start, Vector2Int room)
        {
            var current = start;
            var guard = 0;
            while (guard++ < 64)
            {
                if (current == room)
                {
                    return true;
                }

                if (current == Vector2Int.zero)
                {
                    return false;
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

            return false;
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
            var center = (from + to) * 0.5f;
            var vertical = direction == Vector2Int.up || direction == Vector2Int.down;
            var openClearingLink = layout != null && layout.AreRoomsInSameClearing(fromRoom, toRoom);
            var openSpawnZoneLink = IsInAllOutWarSpawnOpenZone(layout, center, corridorWidth * 0.5f) ||
                IsSegmentInAllOutWarSpawnOpenZone(layout, from, to, corridorWidth * 0.5f);
            var openFloorLink = openClearingLink || openSpawnZoneLink;
            var heightChangingLink = generatingAllOutWar && Mathf.Abs(from.y - to.y) > 0.08f;

            if (vertical)
            {
                var floorScale = openFloorLink
                    ? new Vector3(roomSize, 0.2f, corridorLength + 0.4f)
                    : new Vector3(corridorWidth, 0.2f, corridorLength + 0.4f);
                if (heightChangingLink)
                {
                    CreateSlopedCorridorFloor(theme, root, center, from, to, floorScale.x, corridorLength + 0.4f);
                    return;
                }

                var floorPosition = center + new Vector3(0f, -0.1f, 0f);
                CreateFloorCube(theme, root, "Corridor Floor", floorPosition, floorScale);
                if (openFloorLink)
                {
                    return;
                }

                CreateCube("Corridor Wall", root, center + new Vector3(-corridorWidth * 0.5f, wallHeight * 0.5f, 0f), new Vector3(thickness, wallHeight, corridorLength), theme.Wall);
                CreateCube("Corridor Wall", root, center + new Vector3(corridorWidth * 0.5f, wallHeight * 0.5f, 0f), new Vector3(thickness, wallHeight, corridorLength), theme.Wall);
                AddCorridorWallPanel(theme, root, center + new Vector3(-corridorWidth * 0.5f + 0.08f, wallHeight * 0.68f, 0f), Quaternion.Euler(0f, 90f, 0f));
                AddCorridorWallPanel(theme, root, center + new Vector3(corridorWidth * 0.5f - 0.08f, wallHeight * 0.68f, 0f), Quaternion.Euler(0f, -90f, 0f));
                CreateFloorTrimChannel(theme, root, center + new Vector3(0f, 0.031f, 0f), new Vector3(0.055f, 0.018f, corridorLength), false, 0.018f);
            }
            else
            {
                var floorScale = openFloorLink
                    ? new Vector3(corridorLength + 0.4f, 0.2f, roomSize)
                    : new Vector3(corridorLength + 0.4f, 0.2f, corridorWidth);
                if (heightChangingLink)
                {
                    CreateSlopedCorridorFloor(theme, root, center, from, to, floorScale.z, corridorLength + 0.4f);
                    return;
                }

                var floorPosition = center + new Vector3(0f, -0.1f, 0f);
                CreateFloorCube(theme, root, "Corridor Floor", floorPosition, floorScale);
                if (openFloorLink)
                {
                    return;
                }

                CreateCube("Corridor Wall", root, center + new Vector3(0f, wallHeight * 0.5f, -corridorWidth * 0.5f), new Vector3(corridorLength, wallHeight, thickness), theme.Wall);
                CreateCube("Corridor Wall", root, center + new Vector3(0f, wallHeight * 0.5f, corridorWidth * 0.5f), new Vector3(corridorLength, wallHeight, thickness), theme.Wall);
                AddCorridorWallPanel(theme, root, center + new Vector3(0f, wallHeight * 0.68f, -corridorWidth * 0.5f + 0.08f), Quaternion.identity);
                AddCorridorWallPanel(theme, root, center + new Vector3(0f, wallHeight * 0.68f, corridorWidth * 0.5f - 0.08f), Quaternion.Euler(0f, 180f, 0f));
                CreateFloorTrimChannel(theme, root, center + new Vector3(0f, 0.031f, 0f), new Vector3(corridorLength, 0.018f, 0.055f), false, 0.018f);
            }
        }

        private void CreateSlopedCorridorFloor(ArenaTheme theme, Transform root, Vector3 center, Vector3 from, Vector3 to, float floorWidth, float floorLength)
        {
            var forward = new Vector3(to.x - from.x, 0f, to.z - from.z);
            if (forward.sqrMagnitude <= 0.001f)
            {
                return;
            }

            forward.Normalize();
            var side = new Vector3(forward.z, 0f, -forward.x);
            var halfWidth = floorWidth * 0.5f;
            var halfLength = floorLength * 0.5f;
            var start = -forward * halfLength + Vector3.up * (from.y + 0.012f);
            var end = forward * halfLength + Vector3.up * (to.y + 0.012f);

            var mesh = new Mesh { name = "Sloped Corridor Floor Mesh" };
            mesh.vertices = new[]
            {
                start - side * halfWidth,
                end - side * halfWidth,
                start + side * halfWidth,
                end + side * halfWidth
            };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var floor = new GameObject("Sloped Corridor Floor");
            floor.transform.SetParent(root, false);
            floor.transform.position = new Vector3(center.x, 0f, center.z);
            var meshFilter = floor.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            var renderer = floor.AddComponent<MeshRenderer>();
            var material = GetFloorMaterial(theme);
            renderer.sharedMaterial = material;
            DroidRenderSetup.ApplyRenderer(renderer, ResolveOutlineCategory("Corridor Floor", material));
        }

        private void AddCorridorWallPanel(ArenaTheme theme, Transform root, Vector3 position, Quaternion rotation)
        {
            ArenaWallPanelAsset.TryBuild(root, theme, position, rotation);
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

        private bool HasGate(ArenaLayout layout, Vector2Int room, Vector2Int direction)
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

        private GameObject CreateCube(string name, Transform root, Vector3 position, Vector3 scale, Material material)
        {
            if (activeTheme != null && IsStructuralWallName(name) && ArenaWallBlockAsset.TryBuild(root, activeTheme, position, scale, material, out var wallBlock))
            {
                wallBlock.name = name;
                AddDestructibleIfNeeded(wallBlock, name, scale, material);
                CreateWallBaseAccentIfNeeded(name, root, position, scale, Quaternion.identity);
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
            CreateWallBaseAccentIfNeeded(name, root, position, scale, Quaternion.identity);
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

        private void ConfigureAllOutWarHexFloorMaterial(ArenaTheme theme, float corridorWidth)
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

            var hexSize = Mathf.Clamp(corridorWidth * 0.22f, 0.55f, 0.85f);
            var lineWidth = Mathf.Clamp(hexSize * 0.010f, 0.006f, 0.012f);
            SetMaterialColor(allOutWarHexFloorMaterial, HexFloorBaseColorId, new Color(0.0015f, 0.001f, 0.004f, 1f));
            SetMaterialColor(allOutWarHexFloorMaterial, HexFloorLineColorId, new Color(1.65f, 0.025f, 0.015f, 1f));
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorHexSizeId, hexSize);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorLineWidthId, lineWidth);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorEmissionStrengthId, 1.25f);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorPulseSpeedId, 0f);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorPulseStrengthId, 0f);
            SetMaterialVector(allOutWarHexFloorMaterial, HexFloorPatternOriginId, Vector4.zero);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorNormalFadeStartId, 0.18f);
            SetMaterialFloat(allOutWarHexFloorMaterial, HexFloorNormalFadeEndId, 0.42f);
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
                Destroy(collider);
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
                Destroy(collider);
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
