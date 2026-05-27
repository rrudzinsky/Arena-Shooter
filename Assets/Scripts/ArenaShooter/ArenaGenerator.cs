using System.Collections.Generic;
using UnityEngine;

namespace ArenaShooter
{
    public sealed class ArenaGenerator : MonoBehaviour
    {
        private ArenaTheme activeTheme;

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        public ArenaLayout Generate(ArenaTheme theme, Transform root, int seed, int targetRooms, int gridRadius, float roomSize, float corridorLength, float corridorWidth, float wallHeight, int pickupCount, int gateCount)
        {
            activeTheme = theme;
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
            var random = new System.Random(seed);
            var spacing = roomSize + corridorLength;
            var layout = BuildCircularRoomGraph(random, Mathf.Max(3, totalArmies), targetRooms, gridRadius, spacing);

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

            CreateAllOutWarSafetyFloor(theme, root, layout.CircularCenter, layout.DomeRadius);
            CreateInvisibleCircularBoundary(root, layout.CircularCenter, layout.DomeRadius, wallHeight);
            return layout;
        }

        private ArenaLayout BuildCircularRoomGraph(System.Random random, int totalArmies, int targetRooms, int gridRadius, float spacing)
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

            foreach (var cell in allowed)
            {
                layout.Rooms.Add(cell);
            }

            AddCircularClearingRooms(layout, allowed, random, totalArmies, targetRooms, gridRadius);

            foreach (var room in layout.Rooms)
            {
                layout.RoomCenters[room] = new Vector3(room.x * spacing, 0f, room.y * spacing);
            }

            ConfigureAllOutWarBounds(layout, gridRadius, spacing);
            AddArmySpawnRegions(layout, totalArmies, gridRadius, spacing);
            return layout;
        }

        private static void ConfigureAllOutWarBounds(ArenaLayout layout, int gridRadius, float spacing)
        {
            var playableRadius = gridRadius * spacing + Mathf.Max(4f, spacing * 0.35f);
            layout.CircularCenter = Vector3.zero;
            layout.CircularRadius = playableRadius;
            layout.DomeRadius = playableRadius;
            layout.PerimeterSpawnRadius = Mathf.Max(spacing, playableRadius - 2.2f);
        }

        private void AddCircularClearingRooms(ArenaLayout layout, HashSet<Vector2Int> allowed, System.Random random, int totalArmies, int targetRooms, int gridRadius)
        {
            var clearingCount = Mathf.Clamp(Mathf.CeilToInt(gridRadius * 0.45f) + Mathf.Max(0, targetRooms - 54) / 42, 2, Mathf.Clamp(gridRadius - 1, 2, 6));
            var candidates = new List<Vector2Int>(allowed);
            var spawnRooms = GetArmySpawnCandidateRooms(allowed, totalArmies, gridRadius);
            for (var clearing = 0; clearing < clearingCount && candidates.Count > 0; clearing++)
            {
                var center = candidates[random.Next(candidates.Count)];
                if (center == Vector2Int.zero || center.sqrMagnitude < 2 || IsNearAnyRoom(center, spawnRooms, 2))
                {
                    clearing--;
                    candidates.Remove(center);
                    continue;
                }

                var groupId = layout.ClearingCenters.Count;
                var radius = random.NextDouble() < 0.62 ? 1 : 2;
                var addedAny = false;
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
                            continue;
                        }

                        layout.Rooms.Add(room);
                        layout.ClearingRoomGroups[room] = groupId;
                        addedAny = true;
                    }
                }

                CarvePathToCenter(layout, allowed, center);
                if (addedAny)
                {
                    layout.ClearingCenters.Add(new Vector3(center.x * layout.CellSpacing, 0f, center.y * layout.CellSpacing));
                }

                candidates.Remove(center);
            }
        }

        private static List<Vector2Int> GetArmySpawnCandidateRooms(HashSet<Vector2Int> allowed, int totalArmies, int gridRadius)
        {
            var rooms = new List<Vector2Int>();
            for (var team = 0; team < totalArmies; team++)
            {
                var angle = Mathf.PI * 2f * team / totalArmies;
                var target = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Max(1f, gridRadius - 0.55f);
                rooms.Add(FindNearestAllowedCell(allowed, target));
            }

            return rooms;
        }

        private static bool IsNearAnyRoom(Vector2Int room, List<Vector2Int> others, int range)
        {
            var rangeSqr = range * range;
            foreach (var other in others)
            {
                if ((room - other).sqrMagnitude <= rangeSqr)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddArmySpawnRooms(ArenaLayout layout, HashSet<Vector2Int> allowed, int totalArmies, int gridRadius)
        {
            for (var team = 0; team < totalArmies; team++)
            {
                var angle = Mathf.PI * 2f * team / totalArmies;
                var target = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Max(1f, gridRadius - 0.55f);
                var room = FindNearestAllowedCell(allowed, target);
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
                var target = new Vector2(outward.x, outward.z) * Mathf.Max(1f, gridRadius - 0.55f);
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
                var wall = CreateRawCube(
                    "Arena Wall",
                    root,
                    center + outward * radius + Vector3.up * (wallHeight * 0.5f),
                    new Vector3(arcLength * 1.08f, wallHeight, 0.65f),
                    theme.Wall);
                wall.transform.rotation = Quaternion.Euler(0f, tangentAngle, 0f);
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

        private void CreateAllOutWarSafetyFloor(ArenaTheme theme, Transform root, Vector3 center, float radius)
        {
            var floor = new GameObject("All Out War Dome Floor Safety Disk");
            floor.transform.SetParent(root, false);

            var mesh = CreateDiskMesh("All Out War Dome Floor Safety Mesh", center + Vector3.down * 0.155f, radius);
            var meshFilter = floor.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var renderer = floor.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = theme.Floor;
            DroidRenderSetup.ApplyRenderer(renderer, StylizedOutlineCategory.None);

            var collider = floor.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        private static Mesh CreateDiskMesh(string meshName, Vector3 center, float radius)
        {
            var segmentCount = Mathf.Clamp(Mathf.CeilToInt(radius * 0.85f), 96, 192);
            var vertices = new Vector3[segmentCount + 1];
            var triangles = new int[segmentCount * 3];
            vertices[0] = center;

            for (var i = 0; i < segmentCount; i++)
            {
                var angle = Mathf.PI * 2f * i / segmentCount;
                vertices[i + 1] = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }

            for (var i = 0; i < segmentCount; i++)
            {
                var next = (i + 1) % segmentCount;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = next + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            var mesh = new Mesh { name = meshName };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
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
            if (!forcePrimitive && ArenaRoomFloorAsset.TryBuild(root, theme, position, scale, out var floor))
            {
                floor.name = "Room Floor";
                AddDestructibleIfNeeded(floor, floor.name, scale, theme.Floor);
                return;
            }

            CreateCube("Room Floor", root, position, scale, theme.Floor);
        }

        private void CreateRoomWalls(ArenaTheme theme, Transform root, ArenaLayout layout, Vector2Int room, Vector3 center, float roomSize, float doorwayWidth, float wallHeight)
        {
            if (IsAllOutWarSpawnOpenRoom(layout, room, center))
            {
                return;
            }

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

            if (IsAllOutWarSpawnOpenRoom(layout, room, center) ||
                IsAllOutWarSpawnOpenRoom(layout, room + direction) ||
                IsInAllOutWarSpawnOpenZone(layout, wallCenter, thickness) ||
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

        private bool IsAllOutWarDomeEdgeOpening(ArenaLayout layout, Vector2Int room, Vector2Int direction)
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

        private static bool IsAllOutWarArmySpawnRoom(ArenaLayout layout, Vector2Int room)
        {
            if (layout == null || layout.ArmySpawnRegions.Count == 0)
            {
                return false;
            }

            foreach (var region in layout.ArmySpawnRegions)
            {
                if (region != null && region.Room == room)
                {
                    return true;
                }
            }

            return false;
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
            return IsAllOutWarArmySpawnRoom(layout, room) || IsInAllOutWarSpawnOpenZone(layout, roomCenter, extraRadius);
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
            var openSpawnLink = IsAllOutWarArmySpawnRoom(layout, fromRoom) || IsAllOutWarArmySpawnRoom(layout, toRoom);
            var openSpawnZoneLink = openSpawnLink ||
                IsInAllOutWarSpawnOpenZone(layout, center, corridorWidth * 0.5f) ||
                IsSegmentInAllOutWarSpawnOpenZone(layout, from, to, corridorWidth * 0.5f);
            var openFloorLink = openClearingLink || openSpawnZoneLink;

            if (vertical)
            {
                var floorScale = openFloorLink
                    ? new Vector3(roomSize, 0.2f, corridorLength + 0.4f)
                    : new Vector3(corridorWidth, 0.2f, corridorLength + 0.4f);
                var floorPosition = center + new Vector3(0f, -0.1f, 0f);
                CreateCube("Corridor Floor", root, floorPosition, floorScale, theme.Floor);
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
                var floorPosition = center + new Vector3(0f, -0.1f, 0f);
                CreateCube("Corridor Floor", root, floorPosition, floorScale, theme.Floor);
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
            return cube;
        }

        private GameObject CreateRawCube(string name, Transform root, Vector3 position, Vector3 scale, Material material, DestructibleDamageProfile damageProfile = DestructibleDamageProfile.Wall, Vector3 biteDirection = default)
        {
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

            AddDestructibleIfNeeded(cube, name, scale, material, damageProfile, biteDirection);
            return cube;
        }

        private static void RemoveCollider(GameObject target)
        {
            if (target != null && target.TryGetComponent<Collider>(out var collider))
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
