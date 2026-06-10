using System.Collections.Generic;
using UnityEngine;

namespace ArenaShooter
{
    public sealed class ArenaLayout
    {
        public readonly HashSet<Vector2Int> Rooms = new();
        public readonly Dictionary<Vector2Int, Vector3> RoomCenters = new();
        public readonly List<Vector3> PickupPoints = new();
        public readonly List<ArenaGateSpawn> GateSpawns = new();
        public readonly List<ArmySpawnRegion> ArmySpawnRegions = new();
        public readonly List<ArenaTunnelRoute> TunnelRoutes = new();
        public readonly List<Vector3> ClearingCenters = new();
        public readonly Dictionary<Vector2Int, int> ClearingRoomGroups = new();

        public Vector3 PlayerSpawn { get; set; }
        public Quaternion PlayerRotation { get; set; } = Quaternion.identity;
        public ArenaGateSpawn PlayerGate { get; set; }
        public Vector3 OpponentSpawn { get; set; }
        public Quaternion OpponentRotation { get; set; } = Quaternion.identity;
        public ArenaGateSpawn OpponentGate { get; set; }
        public float CellSpacing { get; set; }
        public Vector3 CircularCenter { get; set; }
        public float CircularRadius { get; set; }
        public float DomeRadius { get; set; }
        public float PerimeterSpawnRadius { get; set; }

        public Vector2Int GetNearestRoom(Vector3 worldPosition)
        {
            var bestRoom = Vector2Int.zero;
            var bestDistance = float.PositiveInfinity;

            foreach (var room in RoomCenters)
            {
                var distance = (room.Value - worldPosition).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestRoom = room.Key;
                }
            }

            return bestRoom;
        }

        public bool TryGetCenter(Vector2Int room, out Vector3 center)
        {
            return RoomCenters.TryGetValue(room, out center);
        }

        public List<Vector2Int> GetConnectedNeighbors(Vector2Int room)
        {
            var neighbors = new List<Vector2Int>();
            foreach (var neighbor in GetNeighbors(room))
            {
                neighbors.Add(neighbor);
            }

            return neighbors;
        }

        public bool TryGetDoorwayPoint(Vector2Int from, Vector2Int to, out Vector3 point)
        {
            point = Vector3.zero;
            var delta = to - from;
            if (!Rooms.Contains(from) || !Rooms.Contains(to))
            {
                return false;
            }

            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
            {
                if (TryGetTunnelRoute(from, to, out var tunnel))
                {
                    point = tunnel.GetTraversalPoint(from);
                    return true;
                }

                return false;
            }

            if (!RoomCenters.TryGetValue(from, out var fromCenter) || !RoomCenters.TryGetValue(to, out var toCenter))
            {
                return false;
            }

            point = (fromCenter + toCenter) * 0.5f;
            return true;
        }

        public bool TryGetTunnelRoute(Vector2Int from, Vector2Int to, out ArenaTunnelRoute route)
        {
            foreach (var tunnel in TunnelRoutes)
            {
                if (tunnel != null && tunnel.Connects(from, to))
                {
                    route = tunnel;
                    return true;
                }
            }

            route = null;
            return false;
        }

        public bool IsTunnelReservedPosition(Vector3 position, float extraPadding = 0f)
        {
            foreach (var tunnel in TunnelRoutes)
            {
                if (tunnel != null && tunnel.ContainsReservedPosition(position, extraPadding))
                {
                    return true;
                }
            }

            return false;
        }

        public bool AreRoomsInSameClearing(Vector2Int a, Vector2Int b)
        {
            return ClearingRoomGroups.TryGetValue(a, out var aGroup) &&
                   ClearingRoomGroups.TryGetValue(b, out var bGroup) &&
                   aGroup == bGroup;
        }

        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
        {
            var path = new List<Vector2Int>();
            if (!Rooms.Contains(start) || !Rooms.Contains(goal))
            {
                return path;
            }

            var queue = new Queue<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            queue.Enqueue(start);
            cameFrom[start] = start;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == goal)
                {
                    break;
                }

                foreach (var next in GetNeighbors(current))
                {
                    if (cameFrom.ContainsKey(next))
                    {
                        continue;
                    }

                    cameFrom[next] = current;
                    queue.Enqueue(next);
                }
            }

            if (!cameFrom.ContainsKey(goal))
            {
                return path;
            }

            var step = goal;
            while (step != start)
            {
                path.Add(step);
                step = cameFrom[step];
            }

            path.Reverse();
            return path;
        }

        private IEnumerable<Vector2Int> GetNeighbors(Vector2Int room)
        {
            var emitted = new HashSet<Vector2Int>();
            var directions = new[]
            {
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left
            };

            foreach (var direction in directions)
            {
                var neighbor = room + direction;
                if (Rooms.Contains(neighbor) && emitted.Add(neighbor))
                {
                    yield return neighbor;
                }
            }

            foreach (var tunnel in TunnelRoutes)
            {
                if (tunnel == null || !tunnel.TryGetOtherRoom(room, out var other))
                {
                    continue;
                }

                if (Rooms.Contains(other) && emitted.Add(other))
                {
                    yield return other;
                }
            }
        }
    }

    public enum ArenaTunnelKind
    {
        HillCut,
        Subfloor
    }

    public enum ArenaTunnelEntranceMode
    {
        WallPortal,
        HillsideMouth
    }

    public sealed class ArenaTunnelRoute
    {
        public ArenaTunnelRoute(
            ArenaTunnelKind kind,
            Vector2Int fromRoom,
            Vector2Int toRoom,
            Vector2Int fromDirection,
            Vector2Int toDirection,
            Vector3 fromPortal,
            Vector3 toPortal,
            float width,
            float height,
            float subfloorDepth,
            float rampLength,
            float portalRadius)
            : this(
                kind,
                fromRoom,
                toRoom,
                fromDirection,
                toDirection,
                fromPortal,
                toPortal,
                width,
                height,
                subfloorDepth,
                rampLength,
                portalRadius,
                DefaultEntranceMode(kind),
                DefaultEntranceMode(kind),
                -1)
        {
        }

        public ArenaTunnelRoute(
            ArenaTunnelKind kind,
            Vector2Int fromRoom,
            Vector2Int toRoom,
            Vector2Int fromDirection,
            Vector2Int toDirection,
            Vector3 fromPortal,
            Vector3 toPortal,
            float width,
            float height,
            float subfloorDepth,
            float rampLength,
            float portalRadius,
            int hillRegionIndex)
            : this(
                kind,
                fromRoom,
                toRoom,
                fromDirection,
                toDirection,
                fromPortal,
                toPortal,
                width,
                height,
                subfloorDepth,
                rampLength,
                portalRadius,
                DefaultEntranceMode(kind),
                DefaultEntranceMode(kind),
                hillRegionIndex)
        {
        }

        public ArenaTunnelRoute(
            ArenaTunnelKind kind,
            Vector2Int fromRoom,
            Vector2Int toRoom,
            Vector2Int fromDirection,
            Vector2Int toDirection,
            Vector3 fromPortal,
            Vector3 toPortal,
            float width,
            float height,
            float subfloorDepth,
            float rampLength,
            float portalRadius,
            ArenaTunnelEntranceMode fromEntranceMode,
            ArenaTunnelEntranceMode toEntranceMode)
            : this(
                kind,
                fromRoom,
                toRoom,
                fromDirection,
                toDirection,
                fromPortal,
                toPortal,
                width,
                height,
                subfloorDepth,
                rampLength,
                portalRadius,
                fromEntranceMode,
                toEntranceMode,
                -1)
        {
        }

        public ArenaTunnelRoute(
            ArenaTunnelKind kind,
            Vector2Int fromRoom,
            Vector2Int toRoom,
            Vector2Int fromDirection,
            Vector2Int toDirection,
            Vector3 fromPortal,
            Vector3 toPortal,
            float width,
            float height,
            float subfloorDepth,
            float rampLength,
            float portalRadius,
            ArenaTunnelEntranceMode fromEntranceMode,
            ArenaTunnelEntranceMode toEntranceMode,
            int hillRegionIndex)
            : this(
                kind,
                fromRoom,
                toRoom,
                fromDirection,
                toDirection,
                fromPortal,
                toPortal,
                width,
                height,
                subfloorDepth,
                rampLength,
                portalRadius,
                fromEntranceMode,
                toEntranceMode,
                hillRegionIndex,
                hillRegionIndex)
        {
        }

        public ArenaTunnelRoute(
            ArenaTunnelKind kind,
            Vector2Int fromRoom,
            Vector2Int toRoom,
            Vector2Int fromDirection,
            Vector2Int toDirection,
            Vector3 fromPortal,
            Vector3 toPortal,
            float width,
            float height,
            float subfloorDepth,
            float rampLength,
            float portalRadius,
            int fromHillRegionIndex,
            int toHillRegionIndex)
            : this(
                kind,
                fromRoom,
                toRoom,
                fromDirection,
                toDirection,
                fromPortal,
                toPortal,
                width,
                height,
                subfloorDepth,
                rampLength,
                portalRadius,
                DefaultEntranceMode(kind),
                DefaultEntranceMode(kind),
                fromHillRegionIndex,
                toHillRegionIndex)
        {
        }

        public ArenaTunnelRoute(
            ArenaTunnelKind kind,
            Vector2Int fromRoom,
            Vector2Int toRoom,
            Vector2Int fromDirection,
            Vector2Int toDirection,
            Vector3 fromPortal,
            Vector3 toPortal,
            float width,
            float height,
            float subfloorDepth,
            float rampLength,
            float portalRadius,
            ArenaTunnelEntranceMode fromEntranceMode,
            ArenaTunnelEntranceMode toEntranceMode,
            int fromHillRegionIndex,
            int toHillRegionIndex)
        {
            Kind = kind;
            FromRoom = fromRoom;
            ToRoom = toRoom;
            FromDirection = NormalizeCardinal(fromDirection);
            ToDirection = NormalizeCardinal(toDirection);
            FromEntranceMode = fromEntranceMode;
            ToEntranceMode = toEntranceMode;
            FromPortal = fromPortal;
            ToPortal = toPortal;
            Width = Mathf.Max(0.1f, width);
            Height = Mathf.Max(0.1f, height);
            SubfloorDepth = Mathf.Max(0f, subfloorDepth);
            RampLength = Mathf.Max(0f, rampLength);
            PortalRadius = Mathf.Max(0.1f, portalRadius);
            FromHillRegionIndex = kind == ArenaTunnelKind.HillCut ? fromHillRegionIndex : -1;
            ToHillRegionIndex = kind == ArenaTunnelKind.HillCut ? toHillRegionIndex : -1;
            HillRegionIndex = FromHillRegionIndex;
            routeWaypoints = BuildRouteWaypoints();
        }

        private readonly Vector3[] routeWaypoints;

        public ArenaTunnelKind Kind { get; }
        public Vector2Int FromRoom { get; }
        public Vector2Int ToRoom { get; }
        public Vector2Int FromDirection { get; }
        public Vector2Int ToDirection { get; }
        public ArenaTunnelEntranceMode FromEntranceMode { get; }
        public ArenaTunnelEntranceMode ToEntranceMode { get; }
        public Vector3 FromPortal { get; }
        public Vector3 ToPortal { get; }
        public float Width { get; }
        public float Height { get; }
        public float SubfloorDepth { get; }
        public float RampLength { get; }
        public float PortalRadius { get; }
        public int HillRegionIndex { get; }
        public int FromHillRegionIndex { get; }
        public int ToHillRegionIndex { get; }

        public Vector3 FlatDirection
        {
            get
            {
                var direction = ToPortal - FromPortal;
                direction.y = 0f;
                return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            }
        }

        public Vector3 FromWorldDirection => ToWorldDirection(FromDirection);
        public Vector3 ToWorldDirectionValue => ToWorldDirection(ToDirection);
        public Vector3 FromSubfloorPoint => FromPortal + FromWorldDirection * RampLength + Vector3.down * SubfloorDepth;
        public Vector3 ToSubfloorPoint => ToPortal + ToWorldDirectionValue * RampLength + Vector3.down * SubfloorDepth;
        public IReadOnlyList<Vector3> Waypoints => routeWaypoints;

        public bool Connects(Vector2Int a, Vector2Int b)
        {
            return (FromRoom == a && ToRoom == b) || (FromRoom == b && ToRoom == a);
        }

        public bool TryGetOtherRoom(Vector2Int room, out Vector2Int other)
        {
            if (room == FromRoom)
            {
                other = ToRoom;
                return true;
            }

            if (room == ToRoom)
            {
                other = FromRoom;
                return true;
            }

            other = default;
            return false;
        }

        public Vector3 GetTraversalPoint(Vector2Int fromRoom)
        {
            return fromRoom == FromRoom ? ToPortal : FromPortal;
        }

        public bool ContainsReservedPosition(Vector3 position, float extraPadding = 0f)
        {
            var padding = Mathf.Max(0f, extraPadding);
            var portalRadius = PortalRadius + padding;
            if (FlatDistance(position, FromPortal) <= portalRadius ||
                FlatDistance(position, ToPortal) <= portalRadius)
            {
                return true;
            }

            var halfWidth = Width * 0.68f + padding;
            for (var i = 1; i < routeWaypoints.Length; i++)
            {
                if (FlatDistanceToSegment(position, routeWaypoints[i - 1], routeWaypoints[i]) <= halfWidth)
                {
                    return true;
                }
            }

            return false;
        }

        private Vector3[] BuildRouteWaypoints()
        {
            if (Kind == ArenaTunnelKind.Subfloor)
            {
                return BuildSubfloorRouteWaypoints();
            }

            var leadLength = Mathf.Max(PortalRadius * 0.75f, Width * 0.82f);
            return new[]
            {
                FromPortal,
                FromPortal + FromWorldDirection * leadLength,
                ToPortal + ToWorldDirectionValue * leadLength,
                ToPortal
            };
        }

        private Vector3[] BuildSubfloorRouteWaypoints()
        {
            var undergroundStart = FromSubfloorPoint;
            var undergroundEnd = ToSubfloorPoint;
            var direct = undergroundEnd - undergroundStart;
            direct.y = 0f;
            if (direct.sqrMagnitude <= 0.001f)
            {
                return new[]
                {
                    FromPortal,
                    undergroundStart,
                    undergroundEnd,
                    ToPortal
                };
            }

            var flatLength = direct.magnitude;
            var leadLength = Mathf.Min(
                Mathf.Max(Width * 1.45f, RampLength * 0.55f),
                Mathf.Max(Width * 1.45f, flatLength * 0.26f));
            var fromLead = undergroundStart + FromWorldDirection * leadLength;
            var toLead = undergroundEnd + ToWorldDirectionValue * leadLength;
            var snakeDirect = toLead - fromLead;
            snakeDirect.y = 0f;
            if (snakeDirect.sqrMagnitude <= Width * Width)
            {
                return new[]
                {
                    FromPortal,
                    undergroundStart,
                    undergroundEnd,
                    ToPortal
                };
            }

            var forward = snakeDirect.normalized;
            var side = new Vector3(-forward.z, 0f, forward.x);
            var sideSign = (BuildRouteHash() & 1) == 0 ? 1f : -1f;
            var snakeLength = snakeDirect.magnitude;
            var bendOffset = Mathf.Clamp(snakeLength * 0.12f, Width * 0.75f, Width * 1.65f);
            var firstBend = Vector3.Lerp(fromLead, toLead, 0.35f) + side * (bendOffset * sideSign);
            var secondBend = Vector3.Lerp(fromLead, toLead, 0.68f) - side * (bendOffset * 0.82f * sideSign);
            fromLead.y = -SubfloorDepth;
            firstBend.y = -SubfloorDepth;
            secondBend.y = -SubfloorDepth;
            toLead.y = -SubfloorDepth;

            return new[]
            {
                FromPortal,
                undergroundStart,
                fromLead,
                firstBend,
                secondBend,
                toLead,
                undergroundEnd,
                ToPortal
            };
        }

        private int BuildRouteHash()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + FromRoom.x;
                hash = hash * 31 + FromRoom.y;
                hash = hash * 31 + ToRoom.x;
                hash = hash * 31 + ToRoom.y;
                hash = hash * 31 + FromDirection.x;
                hash = hash * 31 + FromDirection.y;
                hash = hash * 31 + ToDirection.x;
                hash = hash * 31 + ToDirection.y;
                return hash;
            }
        }

        private static ArenaTunnelEntranceMode DefaultEntranceMode(ArenaTunnelKind kind)
        {
            return kind == ArenaTunnelKind.HillCut
                ? ArenaTunnelEntranceMode.HillsideMouth
                : ArenaTunnelEntranceMode.WallPortal;
        }

        private static Vector2Int NormalizeCardinal(Vector2Int direction)
        {
            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            {
                if (direction.x > 0)
                {
                    return Vector2Int.right;
                }

                if (direction.x < 0)
                {
                    return Vector2Int.left;
                }
            }

            if (direction.y > 0)
            {
                return Vector2Int.up;
            }

            if (direction.y < 0)
            {
                return Vector2Int.down;
            }

            return Vector2Int.up;
        }

        private static Vector3 ToWorldDirection(Vector2Int direction)
        {
            return new Vector3(direction.x, 0f, direction.y).normalized;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static float FlatDistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            var p = new Vector2(point.x, point.z);
            var a = new Vector2(start.x, start.z);
            var b = new Vector2(end.x, end.z);
            var ab = b - a;
            var lengthSqr = ab.sqrMagnitude;
            if (lengthSqr <= 0.001f)
            {
                return Vector2.Distance(p, a);
            }

            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSqr);
            return Vector2.Distance(p, a + ab * t);
        }
    }

    public sealed class ArmySpawnRegion
    {
        private const float ArcSpawnTangentSpacing = 1.15f;
        private const float ArcSpawnRadialSpacing = 0.9f;

        public int TeamId { get; }
        public Vector2Int Room { get; }
        public Vector3 Center { get; }
        public Quaternion Rotation { get; }
        public Vector3 EntryTarget { get; }
        public Vector3[] SpawnOffsets { get; }
        public Vector3 ArenaCenter { get; }
        public Vector3 OutwardDirection { get; }
        public Vector3 TangentDirection { get; }
        public float SpawnRadius { get; }
        public float ArcHalfWidth { get; }
        public float RadialThickness { get; }
        public float ClearancePadding { get; }

        public ArmySpawnRegion(int teamId, Vector2Int room, Vector3 center, Quaternion rotation, Vector3 entryTarget, Vector3[] spawnOffsets)
            : this(teamId, room, center, rotation, entryTarget, spawnOffsets, Vector3.zero, Vector3.zero, 0f, 0f, 0f, 0f)
        {
        }

        public ArmySpawnRegion(
            int teamId,
            Vector2Int room,
            Vector3 center,
            Quaternion rotation,
            Vector3 entryTarget,
            Vector3[] spawnOffsets,
            Vector3 arenaCenter,
            Vector3 outwardDirection,
            float spawnRadius,
            float arcHalfWidth,
            float radialThickness,
            float clearancePadding)
        {
            TeamId = teamId;
            Room = room;
            Center = center;
            Rotation = rotation;
            EntryTarget = entryTarget;
            SpawnOffsets = spawnOffsets ?? System.Array.Empty<Vector3>();
            ArenaCenter = arenaCenter;
            OutwardDirection = Flatten(outwardDirection).sqrMagnitude > 0.001f ? Flatten(outwardDirection).normalized : Vector3.forward;
            TangentDirection = new Vector3(-OutwardDirection.z, 0f, OutwardDirection.x).normalized;
            SpawnRadius = Mathf.Max(0f, spawnRadius);
            ArcHalfWidth = Mathf.Max(0f, arcHalfWidth);
            RadialThickness = Mathf.Max(0f, radialThickness);
            ClearancePadding = Mathf.Max(0f, clearancePadding);
        }

        public Vector3 GetSpawnPosition(int index)
        {
            if (SpawnRadius > 0.1f && ArcHalfWidth > 0.1f && RadialThickness > 0.1f)
            {
                var arcIndex = Mathf.Abs(index);
                var usableHalfWidth = Mathf.Max(0.65f, ArcHalfWidth - 0.55f);
                var columns = Mathf.FloorToInt(usableHalfWidth * 2f / ArcSpawnTangentSpacing) + 1;
                columns = Mathf.Clamp(columns | 1, 3, 15);
                var rows = Mathf.Max(1, Mathf.FloorToInt(Mathf.Max(0.8f, RadialThickness - 0.9f) / ArcSpawnRadialSpacing) + 1);
                var arcColumn = arcIndex % columns;
                var arcRow = (arcIndex / columns) % rows;
                var arcTangentOffset = GetCenterOutColumnOffset(arcColumn) * ArcSpawnTangentSpacing;
                arcTangentOffset = Mathf.Clamp(arcTangentOffset, -usableHalfWidth, usableHalfWidth);

                var angle = arcTangentOffset / Mathf.Max(1f, SpawnRadius);
                var rotatedOutward = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.up) * OutwardDirection;
                var radius = Mathf.Clamp(
                    SpawnRadius - 0.45f - arcRow * ArcSpawnRadialSpacing,
                    Mathf.Max(0f, SpawnRadius - RadialThickness + 0.45f),
                    SpawnRadius - 0.35f);

                return ArenaCenter + rotatedOutward.normalized * radius + Vector3.up * Center.y;
            }

            if (SpawnOffsets.Length == 0)
            {
                return Center;
            }

            var safeIndex = Mathf.Abs(index);
            if (safeIndex < SpawnOffsets.Length)
            {
                return Center + Rotation * SpawnOffsets[safeIndex];
            }

            var columnCount = 8;
            var row = safeIndex / columnCount;
            var column = safeIndex % columnCount;
            var tangentOffset = (column - (columnCount - 1) * 0.5f) * 0.78f;
            var inwardOffset = 0.65f + row * 0.62f;
            return Center + Rotation * new Vector3(tangentOffset, 0f, inwardOffset);
        }

        public bool ContainsPoint(Vector3 position, float extraPadding = 0f)
        {
            if (SpawnRadius <= 0.1f || ArcHalfWidth <= 0.1f || RadialThickness <= 0.1f)
            {
                return false;
            }

            var toPosition = Flatten(position - ArenaCenter);
            var radius = toPosition.magnitude;
            if (radius <= 0.001f)
            {
                return false;
            }

            var padding = ClearancePadding + Mathf.Max(0f, extraPadding);
            var minRadius = Mathf.Max(0f, SpawnRadius - RadialThickness - padding);
            var maxRadius = SpawnRadius + padding;
            if (radius < minRadius || radius > maxRadius)
            {
                return false;
            }

            var direction = toPosition / radius;
            var angle = Mathf.Atan2(Vector3.Dot(direction, TangentDirection), Vector3.Dot(direction, OutwardDirection));
            var halfAngle = (ArcHalfWidth + padding) / Mathf.Max(1f, SpawnRadius);
            return Mathf.Abs(angle) <= halfAngle;
        }

        public bool IntersectsSegment(Vector3 start, Vector3 end, float extraPadding = 0f)
        {
            const int sampleCount = 8;
            for (var i = 0; i <= sampleCount; i++)
            {
                var t = i / (float)sampleCount;
                if (ContainsPoint(Vector3.Lerp(start, end, t), extraPadding))
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetCenterOutColumnOffset(int column)
        {
            if (column == 0)
            {
                return 0;
            }

            var step = (column + 1) / 2;
            return column % 2 == 1 ? step : -step;
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }

    public sealed class ArenaGateSpawn
    {
        public Vector2Int Room { get; }
        public Vector2Int Direction { get; }
        public Vector3 SpawnPosition { get; }
        public Quaternion SpawnRotation { get; }
        public Vector3 InnerTarget { get; }
        public Vector3 EntryTarget { get; }
        public GateDoor Door { get; set; }

        public ArenaGateSpawn(Vector2Int room, Vector2Int direction, Vector3 spawnPosition, Quaternion spawnRotation, Vector3 innerTarget, Vector3 entryTarget)
        {
            Room = room;
            Direction = direction;
            SpawnPosition = spawnPosition;
            SpawnRotation = spawnRotation;
            InnerTarget = innerTarget;
            EntryTarget = entryTarget;
        }
    }
}
