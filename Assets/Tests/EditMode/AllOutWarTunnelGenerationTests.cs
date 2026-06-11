using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class AllOutWarTunnelGenerationTests
{
    private const float RoomSize = 10f;
    private const float CorridorLength = 6f;
    private const float CorridorWidth = 4f;
    private const float SpawnTunnelPadding = 8.5f;
    private const string RandomMapStyle = "Randomly Generate";
    private const string HillyMapStyle = "Hilly";

    private static readonly BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;
    private static readonly BindingFlags PublicInstanceFields = BindingFlags.Instance | BindingFlags.Public;
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

    private static readonly System.Type ArenaLayoutType = System.Type.GetType("ArenaShooter.ArenaLayout, Assembly-CSharp");
    private static readonly System.Type ArenaTunnelRouteType = System.Type.GetType("ArenaShooter.ArenaTunnelRoute, Assembly-CSharp");
    private static readonly System.Type ArenaTunnelKindType = System.Type.GetType("ArenaShooter.ArenaTunnelKind, Assembly-CSharp");
    private static readonly System.Type ArenaTunnelEntranceModeType = System.Type.GetType("ArenaShooter.ArenaTunnelEntranceMode, Assembly-CSharp");
    private static readonly System.Type ArmySpawnRegionType = System.Type.GetType("ArenaShooter.ArmySpawnRegion, Assembly-CSharp");
    private static readonly System.Type AllOutWarMapStyleType = System.Type.GetType("ArenaShooter.AllOutWarMapStyle, Assembly-CSharp");
    private static readonly System.Type ArenaGeneratorType = System.Type.GetType("ArenaShooter.ArenaGenerator, Assembly-CSharp");
    private static readonly System.Type ArenaThemeType = System.Type.GetType("ArenaShooter.ArenaTheme, Assembly-CSharp");
    private static readonly System.Type RenderSetupType = System.Type.GetType("ArenaShooter.DroidRenderSetup, Assembly-CSharp");
    private static readonly System.Type AllOutWarTerrainProfileType = System.Type.GetType("ArenaShooter.ArenaGenerator+AllOutWarTerrainProfile, Assembly-CSharp");
    private static readonly System.Type SubfloorEndpointCandidateType = System.Type.GetType("ArenaShooter.ArenaGenerator+SubfloorEndpointCandidate, Assembly-CSharp");
    private static readonly System.Type SubfloorEndpointSourceType = System.Type.GetType("ArenaShooter.ArenaGenerator+SubfloorEndpointSource, Assembly-CSharp");
    private const float HillCutClearance = 0.85f;

    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    private readonly List<GameObject> generatedRoots = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var root in generatedRoots)
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        generatedRoots.Clear();
    }

    [Test]
    public void HillCutEligibilityRequiresFullTunnelClearance()
    {
        var terrainProfile = BuildHillyTerrainProfileWithRegion(out var region);
        var width = 4.5f;
        var eligibleHeight = Mathf.Min(4f, region.PeakHeight - HillCutClearance - 0.2f);

        Assert.That(eligibleHeight, Is.GreaterThan(3.2f));
        Assert.That(InvokeTryBuildHillCutEndpoint(terrainProfile, 0, Vector3.right, width, eligibleHeight, RoomSize * 0.5f, out var fromPortal), Is.True);
        Assert.That(InvokeTryBuildHillCutEndpoint(terrainProfile, 0, Vector3.left, width, eligibleHeight, RoomSize * 0.5f, out var toPortal), Is.True);
        Assert.That(InvokeTryScoreHillCutRoute(terrainProfile, 0, fromPortal, toPortal, Vector3.right, Vector3.left, width, eligibleHeight, 4.5f, out _), Is.True);
        Assert.That(InvokeTryScoreHillCutRoute(terrainProfile, 0, fromPortal, toPortal, Vector3.right, Vector3.left, width, region.PeakHeight, 4.5f, out _), Is.False);
    }

    [Test]
    public void HillCutFlatMasksPreserveHilltopAndCutOnlyMouthAprons()
    {
        var terrainProfile = BuildHillyTerrainProfileWithRegion(out var region);
        var center = new Vector3(region.Center.x, 0f, region.Center.y);
        Assert.That(InvokeTryBuildHillCutEndpoint(terrainProfile, 0, Vector3.right, 4.5f, 4f, RoomSize * 0.5f, out var fromPortal), Is.True);
        Assert.That(InvokeTryBuildHillCutEndpoint(terrainProfile, 0, Vector3.left, 4.5f, 4f, RoomSize * 0.5f, out var toPortal), Is.True);
        Assert.That(InvokeTryBuildHillMouthApron(
            terrainProfile,
            0,
            fromPortal,
            Vector3.right,
            4.5f,
            4f,
            out var fromOuterApproach,
            out _,
            out _,
            out _), Is.True);
        var layout = System.Activator.CreateInstance(ArenaLayoutType);
        SetProperty(layout, "CellSpacing", RoomSize + CorridorLength);
        Rooms(layout).Add(Vector2Int.zero);
        Rooms(layout).Add(new Vector2Int(4, 0));
        RoomCenters(layout)[Vector2Int.zero] = fromPortal - Vector3.right * (RoomSize * 0.5f - 0.2f);
        RoomCenters(layout)[new Vector2Int(4, 0)] = toPortal - Vector3.left * (RoomSize * 0.5f - 0.2f);
        TunnelRoutes(layout).Add(CreateHillCutTunnel(
            Vector2Int.zero,
            new Vector2Int(4, 0),
            Vector2Int.right,
            Vector2Int.left,
            fromPortal,
            toPortal,
            0));

        var before = InvokeSampleTerrainHeight(terrainProfile, center);
        AllOutWarTerrainProfileType.GetMethod("BuildFlatMasks", PublicInstance).Invoke(
            terrainProfile,
            new object[] { layout, RoomSize, CorridorLength, CorridorWidth });
        var after = InvokeSampleTerrainHeight(terrainProfile, center);
        var terrainCutouts = (IList)AllOutWarTerrainProfileType.GetField("terrainCutouts", PrivateInstance).GetValue(terrainProfile);
        var fromApronMidpoint = Vector3.Lerp(fromOuterApproach, fromPortal, 0.5f);

        Assert.That(before, Is.GreaterThan(4f + HillCutClearance));
        Assert.That(after, Is.GreaterThan(4f + HillCutClearance));
        Assert.That(after, Is.EqualTo(before).Within(0.12f));
        Assert.That(terrainCutouts.Count, Is.EqualTo(2));
        Assert.That(InvokeIsTerrainCutout(terrainProfile, fromApronMidpoint), Is.True);
        Assert.That(InvokeIsTerrainCutout(terrainProfile, fromPortal), Is.True);
        Assert.That(InvokeIsTerrainCutout(terrainProfile, toPortal), Is.True);
        Assert.That(InvokeIsTerrainCutout(terrainProfile, center), Is.False);
    }

    [Test]
    public void HillCutTunnelFloorsHaveColliders()
    {
        var root = new GameObject("All Out War hill tunnel collider test");
        generatedRoots.Add(root);
        var generator = root.AddComponent(ArenaGeneratorType);
        var layout = System.Activator.CreateInstance(ArenaLayoutType);
        var tunnel = CreateHillCutTunnel(
            Vector2Int.zero,
            new Vector2Int(4, 0),
            Vector2Int.right,
            Vector2Int.left,
            new Vector3(5f, 0f, 0f),
            new Vector3(59f, 0f, 0f));
        TunnelRoutes(layout).Add(tunnel);

        ArenaGeneratorType.GetMethod("CreateAllOutWarTunnels", PrivateInstance).Invoke(
            generator,
            new object[] { System.Activator.CreateInstance(ArenaThemeType), root.transform, layout, RoomSize, CorridorLength, CorridorWidth, 4f });

        var floorCount = 0;
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "All Out War Tunnel Floor")
            {
                continue;
            }

            floorCount++;
            Assert.That(child.TryGetComponent<Collider>(out _), Is.True);
        }

        Assert.That(floorCount, Is.GreaterThan(0));
    }

    [Test]
    public void HillCutTunnelCreatesCollidingHexTerrainMouthCollars()
    {
        var root = new GameObject("All Out War hill mouth seam test");
        generatedRoots.Add(root);
        var generator = root.AddComponent(ArenaGeneratorType);
        var hexShader = Shader.Find("ArenaShooter/WorldSpaceHexFloor");
        Assert.That(hexShader, Is.Not.Null);
        var hexMaterial = new Material(hexShader) { name = "Test All Out War Hex Floor Material" };
        ArenaGeneratorType.GetField("generatingAllOutWar", PrivateInstance).SetValue(generator, true);
        ArenaGeneratorType.GetField("allOutWarHexFloorMaterial", PrivateInstance).SetValue(generator, hexMaterial);
        var terrainProfile = BuildHillyTerrainProfileWithRegion(out _);
        Assert.That(InvokeTryBuildHillCutEndpoint(terrainProfile, 0, Vector3.right, 4.5f, 4f, RoomSize * 0.5f, out var fromPortal), Is.True);
        Assert.That(InvokeTryBuildHillCutEndpoint(terrainProfile, 0, Vector3.left, 4.5f, 4f, RoomSize * 0.5f, out var toPortal), Is.True);
        Assert.That(InvokeTryBuildHillMouthApron(
            terrainProfile,
            0,
            fromPortal,
            Vector3.right,
            4.5f,
            4f,
            out var fromOuterApproach,
            out var fromInnerMouth,
            out var fromOuterHalfWidth,
            out var fromInnerHalfWidth), Is.True);
        Assert.That(InvokeTryBuildHillMouthApron(
            terrainProfile,
            0,
            toPortal,
            Vector3.left,
            4.5f,
            4f,
            out var toOuterApproach,
            out var toInnerMouth,
            out var toOuterHalfWidth,
            out var toInnerHalfWidth), Is.True);
        var layout = System.Activator.CreateInstance(ArenaLayoutType);
        var tunnel = CreateHillCutTunnel(
            Vector2Int.zero,
            new Vector2Int(4, 0),
            Vector2Int.right,
            Vector2Int.left,
            fromPortal,
            toPortal,
            0);
        TunnelRoutes(layout).Add(tunnel);
        ArenaGeneratorType.GetField("activeAllOutWarTerrainProfile", PrivateInstance).SetValue(generator, terrainProfile);

        ArenaGeneratorType.GetMethod("CreateAllOutWarTunnels", PrivateInstance).Invoke(
            generator,
            new object[] { System.Activator.CreateInstance(ArenaThemeType), root.transform, layout, RoomSize, CorridorLength, CorridorWidth, 4f });

        var apronFloors = FindChildren(root, "All Out War Hill Tunnel Mouth Apron Floor");
        var sideCollars = FindChildren(root, "All Out War Hill Tunnel Mouth Side Collar");
        var caps = FindChildren(root, "All Out War Hill Tunnel Hilltop Cap");
        Assert.That(apronFloors.Count, Is.EqualTo(2));
        Assert.That(sideCollars.Count, Is.EqualTo(4));
        Assert.That(caps.Count, Is.EqualTo(2));
        foreach (var apron in apronFloors)
        {
            Assert.That(apron.TryGetComponent<Collider>(out _), Is.True);
        }

        foreach (var sideCollar in sideCollars)
        {
            Assert.That(sideCollar.TryGetComponent<Collider>(out var collider), Is.True);
            Assert.That(collider.bounds.min.y, Is.LessThan(0.12f));
            Assert.That(collider.bounds.max.y, Is.GreaterThan(4f + HillCutClearance));
        }

        foreach (var terrainMouthPiece in apronFloors.Concat(sideCollars).Concat(caps))
        {
            AssertTerrainMouthPieceUsesHexFloorSurface(terrainMouthPiece, hexMaterial);
        }

        Physics.SyncTransforms();
        AssertHillMouthSideCollarsSealCutFaces(
            sideCollars,
            terrainProfile,
            fromOuterApproach,
            fromInnerMouth,
            Vector3.right,
            fromOuterHalfWidth,
            fromInnerHalfWidth);
        AssertHillMouthSideCollarsSealCutFaces(
            sideCollars,
            terrainProfile,
            toOuterApproach,
            toInnerMouth,
            Vector3.left,
            toOuterHalfWidth,
            toInnerHalfWidth);
        foreach (var cap in caps)
        {
            Assert.That(cap.TryGetComponent<MeshRenderer>(out _), Is.True);
            Assert.That(cap.TryGetComponent<Collider>(out _), Is.True);
            Assert.That(cap.TryGetComponent<MeshFilter>(out var filter), Is.True);
            Assert.That(filter.sharedMesh, Is.Not.Null);

            var vertices = filter.sharedMesh.vertices;
            Assert.That(vertices.Length, Is.GreaterThan(0));
            var minY = float.PositiveInfinity;
            var maxY = float.NegativeInfinity;
            foreach (var vertex in vertices)
            {
                minY = Mathf.Min(minY, vertex.y);
                maxY = Mathf.Max(maxY, vertex.y);
            }

            Assert.That(minY, Is.GreaterThan(4f));
            Assert.That(maxY, Is.GreaterThan(4f + HillCutClearance));
            Assert.That(Mathf.Min(filter.sharedMesh.bounds.size.x, filter.sharedMesh.bounds.size.z), Is.GreaterThan(2f));
            Assert.That(Mathf.Max(filter.sharedMesh.bounds.size.x, filter.sharedMesh.bounds.size.z), Is.GreaterThan(4.5f));

            var bounds = cap.GetComponent<Collider>().bounds;
            var rayOrigin = new Vector3(bounds.center.x, bounds.max.y + 4f, bounds.center.z);
            Assert.That(Physics.Raycast(rayOrigin, Vector3.down, out var hit, 12f), Is.True);
            Assert.That(hit.transform, Is.EqualTo(cap));
            Assert.That(hit.point.y, Is.GreaterThan(4f));
        }

        Object.DestroyImmediate(hexMaterial);
    }

    [Test]
    public void CompoundHillCutTunnelUsesEndpointHillIdsAndUnionTerrainBore()
    {
        var root = new GameObject("All Out War compound hill mouth seam test");
        generatedRoots.Add(root);
        var generator = root.AddComponent(ArenaGeneratorType);
        var hexShader = Shader.Find("ArenaShooter/WorldSpaceHexFloor");
        Assert.That(hexShader, Is.Not.Null);
        var hexMaterial = new Material(hexShader) { name = "Test All Out War Compound Hex Floor Material" };
        ArenaGeneratorType.GetField("generatingAllOutWar", PrivateInstance).SetValue(generator, true);
        ArenaGeneratorType.GetField("allOutWarHexFloorMaterial", PrivateInstance).SetValue(generator, hexMaterial);
        var terrainProfile = BuildCompoundHillTerrainProfile();
        var fromPortal = new Vector3(-34f, 0f, 0f);
        var toPortal = new Vector3(34f, 0f, 0f);
        const float tunnelWidth = 4.5f;
        const float tunnelHeight = 4f;
        const float portalRadius = 4.5f;

        Assert.That(InvokeTryScoreCompoundHillCutRoute(
            terrainProfile,
            0,
            1,
            fromPortal,
            toPortal,
            Vector3.right,
            Vector3.left,
            tunnelWidth,
            tunnelHeight,
            portalRadius,
            out _), Is.True);
        Assert.That(InvokeTryScoreCompoundHillCutRoute(
            terrainProfile,
            0,
            1,
            fromPortal + Vector3.forward * 9f,
            toPortal + Vector3.forward * 9f,
            Vector3.right,
            Vector3.left,
            tunnelWidth,
            tunnelHeight,
            portalRadius,
            out _), Is.False);

        Assert.That(InvokeTryBuildHillMouthApron(
            terrainProfile,
            0,
            fromPortal,
            Vector3.right,
            tunnelWidth,
            tunnelHeight,
            out var fromOuterApproach,
            out var fromInnerMouth,
            out var fromOuterHalfWidth,
            out var fromInnerHalfWidth), Is.True);
        Assert.That(InvokeTryBuildHillMouthApron(
            terrainProfile,
            1,
            toPortal,
            Vector3.left,
            tunnelWidth,
            tunnelHeight,
            out var toOuterApproach,
            out var toInnerMouth,
            out var toOuterHalfWidth,
            out var toInnerHalfWidth), Is.True);
        Assert.That(InvokeSampleTerrainHeight(terrainProfile, fromOuterApproach), Is.LessThan(0.42f));
        Assert.That(InvokeSampleTerrainHeight(terrainProfile, toOuterApproach), Is.LessThan(0.42f));

        var layout = System.Activator.CreateInstance(ArenaLayoutType);
        var tunnel = CreateCompoundHillCutTunnel(
            Vector2Int.zero,
            new Vector2Int(4, 0),
            Vector2Int.right,
            Vector2Int.left,
            fromPortal,
            toPortal,
            0,
            1);
        TunnelRoutes(layout).Add(tunnel);
        Assert.That(GetInt(tunnel, "FromHillRegionIndex"), Is.EqualTo(0));
        Assert.That(GetInt(tunnel, "ToHillRegionIndex"), Is.EqualTo(1));
        ArenaGeneratorType.GetField("activeAllOutWarTerrainProfile", PrivateInstance).SetValue(generator, terrainProfile);

        ArenaGeneratorType.GetMethod("CreateAllOutWarTunnels", PrivateInstance).Invoke(
            generator,
            new object[] { System.Activator.CreateInstance(ArenaThemeType), root.transform, layout, RoomSize, CorridorLength, CorridorWidth, 4f });

        var apronFloors = FindChildren(root, "All Out War Hill Tunnel Mouth Apron Floor");
        var sideCollars = FindChildren(root, "All Out War Hill Tunnel Mouth Side Collar");
        var caps = FindChildren(root, "All Out War Hill Tunnel Hilltop Cap");
        Assert.That(apronFloors.Count, Is.EqualTo(2));
        Assert.That(sideCollars.Count, Is.EqualTo(4));
        Assert.That(caps.Count, Is.EqualTo(2));
        foreach (var terrainMouthPiece in apronFloors.Concat(sideCollars).Concat(caps))
        {
            AssertTerrainMouthPieceUsesHexFloorSurface(terrainMouthPiece, hexMaterial);
        }

        Physics.SyncTransforms();
        AssertHillMouthSideCollarsSealCutFaces(
            sideCollars,
            terrainProfile,
            fromOuterApproach,
            fromInnerMouth,
            Vector3.right,
            fromOuterHalfWidth,
            fromInnerHalfWidth);
        AssertHillMouthSideCollarsSealCutFaces(
            sideCollars,
            terrainProfile,
            toOuterApproach,
            toInnerMouth,
            Vector3.left,
            toOuterHalfWidth,
            toInnerHalfWidth);

        Object.DestroyImmediate(hexMaterial);
    }

    [Test]
    public void TunnelFallbackDoorwayTargetsFarPortal()
    {
        var layout = System.Activator.CreateInstance(ArenaLayoutType);
        var fromRoom = Vector2Int.zero;
        var toRoom = new Vector2Int(4, 0);
        Rooms(layout).Add(fromRoom);
        Rooms(layout).Add(toRoom);
        RoomCenters(layout)[fromRoom] = Vector3.zero;
        RoomCenters(layout)[toRoom] = new Vector3(40f, 0f, 0f);

        var tunnel = System.Activator.CreateInstance(
            ArenaTunnelRouteType,
            System.Enum.Parse(ArenaTunnelKindType, "Subfloor"),
            fromRoom,
            toRoom,
            Vector2Int.right,
            Vector2Int.left,
            new Vector3(5f, 0f, 0f),
            new Vector3(35f, 0f, 0f),
            4.5f,
            4f,
            3f,
            9f,
            4.5f);
        TunnelRoutes(layout).Add(tunnel);

        var path = InvokeFindPath(layout, fromRoom, toRoom);
        Assert.That(path, Is.EquivalentTo(new[] { toRoom }));
        Assert.That(InvokeTryGetDoorwayPoint(layout, fromRoom, toRoom, out var doorway), Is.True);
        Assert.That(Vector3.Distance(doorway, GetVector3(tunnel, "ToPortal")), Is.LessThan(0.001f));
    }

    [Test]
    public void SubfloorTunnelRouteLeavingDomeIsRejected()
    {
        var layout = System.Activator.CreateInstance(ArenaLayoutType);
        SetProperty(layout, "CircularCenter", Vector3.zero);
        SetProperty(layout, "DomeRadius", 20f);

        var tunnel = System.Activator.CreateInstance(
            ArenaTunnelRouteType,
            System.Enum.Parse(ArenaTunnelKindType, "Subfloor"),
            Vector2Int.zero,
            new Vector2Int(-2, 0),
            Vector2Int.right,
            Vector2Int.left,
            new Vector3(15.5f, 0f, 0f),
            new Vector3(-5f, 0f, 0f),
            4.5f,
            4f,
            3f,
            9f,
            4.5f);

        Assert.That(InvokeIsTunnelRouteSafe(layout, tunnel, 4.5f), Is.False);
    }

    [Test]
    public void HillCutTunnelRouteKeepsSurfaceWaypoints()
    {
        var tunnel = System.Activator.CreateInstance(
            ArenaTunnelRouteType,
            System.Enum.Parse(ArenaTunnelKindType, "HillCut"),
            Vector2Int.zero,
            new Vector2Int(4, 0),
            Vector2Int.right,
            Vector2Int.left,
            new Vector3(5f, 0f, 0f),
            new Vector3(35f, 0f, 0f),
            4.5f,
            4f,
            0f,
            0f,
            4.5f);

        foreach (var waypoint in GetWaypoints(tunnel))
        {
            Assert.That(waypoint.y, Is.EqualTo(0f).Within(0.001f));
        }
    }

    [Test]
    public void HillCutEndpointCandidatesUseHillsideMouthsNotRoomBoundaries()
    {
        var terrainProfile = BuildHillyTerrainProfileWithRegion(out var region);
        var layout = System.Activator.CreateInstance(ArenaLayoutType);
        SetProperty(layout, "CellSpacing", RoomSize + CorridorLength);
        SetProperty(layout, "CircularCenter", Vector3.zero);
        SetProperty(layout, "DomeRadius", 180f);
        var tunnelHeight = Mathf.Min(4f, region.PeakHeight - HillCutClearance - 0.2f);
        Assert.That(tunnelHeight, Is.GreaterThan(3.2f));
        var validMouths = 0;
        var firstSampledPortal = Vector3.zero;
        for (var i = 0; i < CardinalDirections.Length; i++)
        {
            var direction = ToWorldDirection(CardinalDirections[i]);
            if (!InvokeTryBuildHillCutEndpoint(terrainProfile, 0, direction, 4.5f, tunnelHeight, RoomSize * 0.5f, out var sampledPortal))
            {
                continue;
            }

            if (validMouths == 0)
            {
                firstSampledPortal = sampledPortal;
            }

            validMouths++;
            var room = CardinalDirections[i] * 7;
            Rooms(layout).Add(room);
            RoomCenters(layout)[room] = sampledPortal - direction * (RoomSize * 1.15f);
        }

        Assert.That(validMouths, Is.GreaterThanOrEqualTo(2));
        Assert.That(InvokeSampleTerrainHeight(terrainProfile, firstSampledPortal), Is.GreaterThanOrEqualTo(tunnelHeight + HillCutClearance));
        var candidates = InvokeBuildHillCutEndpointCandidates(layout, terrainProfile, RoomSize, 4.5f, tunnelHeight);

        Assert.That(candidates.Count, Is.GreaterThan(0));
        var candidate = candidates[0];
        var portal = GetField<Vector3>(candidate, "Portal");
        var roomKey = GetField<Vector2Int>(candidate, "Room");
        var roomCenter = RoomCenters(layout)[roomKey];
        Assert.That(InvokeSampleTerrainHeight(terrainProfile, portal), Is.GreaterThanOrEqualTo(tunnelHeight + HillCutClearance));
        Assert.That(FlatDistance(roomCenter, portal), Is.GreaterThan(RoomSize * 0.7f));
        Assert.That(FlatDistance(roomCenter, portal), Is.LessThan((RoomSize + CorridorLength) * 2.4f));
    }

    [Test]
    public void HillCutTunnelEndpointsDoNotCreateRoomWallOpenings()
    {
        var root = new GameObject("All Out War tunnel wall opening cleanup test");
        generatedRoots.Add(root);
        var generator = root.AddComponent(ArenaGeneratorType);
        var layout = System.Activator.CreateInstance(ArenaLayoutType);
        var fromRoom = Vector2Int.zero;
        var toRoom = new Vector2Int(4, 0);
        Rooms(layout).Add(fromRoom);
        Rooms(layout).Add(toRoom);
        RoomCenters(layout)[fromRoom] = Vector3.zero;
        RoomCenters(layout)[toRoom] = new Vector3(64f, 0f, 0f);
        var tunnel = CreateHillCutTunnel(
            fromRoom,
            toRoom,
            Vector2Int.right,
            Vector2Int.left,
            new Vector3(20f, 0f, 0f),
            new Vector3(44f, 0f, 0f));
        TunnelRoutes(layout).Add(tunnel);

        ArenaGeneratorType.GetMethod("CreateRoom", PrivateInstance).Invoke(
            generator,
            new object[] { System.Activator.CreateInstance(ArenaThemeType), root.transform, layout, fromRoom, RoomSize, CorridorWidth, 4f });

        var wallSegments = FindChildren(root, "Arena Wall Door Segment");
        var pillars = FindChildren(root, "Room Corridor Opening Pillar");
        var fullWalls = FindChildren(root, "Arena Wall");
        Assert.That(wallSegments.Count, Is.EqualTo(0));
        Assert.That(pillars.Count, Is.EqualTo(0));
        Assert.That(fullWalls.Count, Is.GreaterThan(0));
    }

    [Test]
    public void TunnelEntranceModesDefaultByTunnelKind()
    {
        var subfloor = CreateSubfloorTunnel(
            Vector2Int.zero,
            new Vector2Int(4, 0),
            Vector2Int.right,
            Vector2Int.left,
            new Vector3(0f, 0f, 0f),
            new Vector3(32f, 0f, 0f));
        var hillCut = CreateHillCutTunnel(
            Vector2Int.zero,
            new Vector2Int(4, 0),
            Vector2Int.right,
            Vector2Int.left,
            new Vector3(8f, 0f, 0f),
            new Vector3(28f, 0f, 0f));

        Assert.That(GetEnumName(subfloor, "FromEntranceMode"), Is.EqualTo("WallPortal"));
        Assert.That(GetEnumName(subfloor, "ToEntranceMode"), Is.EqualTo("WallPortal"));
        Assert.That(GetEnumName(hillCut, "FromEntranceMode"), Is.EqualTo("HillsideMouth"));
        Assert.That(GetEnumName(hillCut, "ToEntranceMode"), Is.EqualTo("HillsideMouth"));
        Assert.That(ArenaTunnelEntranceModeType, Is.Not.Null);
    }

    [Test]
    public void SubfloorTunnelFlatMasksUseOnlyRampCutoutSegments()
    {
        var layout = System.Activator.CreateInstance(ArenaLayoutType);
        SetProperty(layout, "CellSpacing", RoomSize + CorridorLength);
        SetProperty(layout, "CircularCenter", Vector3.zero);
        SetProperty(layout, "DomeRadius", 96f);
        Rooms(layout).Add(Vector2Int.zero);
        Rooms(layout).Add(new Vector2Int(4, 0));
        RoomCenters(layout)[Vector2Int.zero] = Vector3.zero;
        RoomCenters(layout)[new Vector2Int(4, 0)] = new Vector3(64f, 0f, 0f);
        var tunnel = CreateSubfloorTunnel(
            Vector2Int.zero,
            new Vector2Int(4, 0),
            Vector2Int.right,
            Vector2Int.left,
            new Vector3(5f, 0f, 0f),
            new Vector3(59f, 0f, 0f));
        TunnelRoutes(layout).Add(tunnel);

        var terrainProfile = System.Activator.CreateInstance(
            AllOutWarTerrainProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new object[] { 12345, RoomSize + CorridorLength, System.Enum.Parse(AllOutWarMapStyleType, "RandomlyGenerate") },
            null);
        AllOutWarTerrainProfileType.GetMethod("BuildFlatMasks", PublicInstance).Invoke(
            terrainProfile,
            new object[] { layout, RoomSize, CorridorLength, CorridorWidth });

        var terrainCutouts = (IList)AllOutWarTerrainProfileType.GetField("terrainCutouts", PrivateInstance).GetValue(terrainProfile);
        Assert.That(terrainCutouts.Count, Is.EqualTo(2));
        foreach (var cutout in terrainCutouts)
        {
            Assert.That((bool)cutout.GetType().GetField("segment", PrivateInstance).GetValue(cutout), Is.True);
            Assert.That((bool)cutout.GetType().GetField("directional", PrivateInstance).GetValue(cutout), Is.True);
        }

        var fromPortal = GetVector3(tunnel, "FromPortal");
        var fromDirection = ToWorldDirection(GetVector2Int(tunnel, "FromDirection"));
        var fromSide = new Vector3(-fromDirection.z, 0f, fromDirection.x);
        var cutoutHalfWidth = GetFloat(tunnel, "Width") * 0.76f;
        Assert.That(InvokeIsTerrainCutout(terrainProfile, fromPortal + fromDirection * 0.65f), Is.True);
        Assert.That(InvokeIsTerrainCutout(terrainProfile, fromPortal - fromDirection * 0.5f), Is.False);
        Assert.That(InvokeIsTerrainCutout(terrainProfile, fromPortal - fromDirection * 0.35f + fromSide * (cutoutHalfWidth * 0.82f)), Is.False);
    }

    [Test]
    public void SubfloorTunnelCreatesGroundedPortalThresholds()
    {
        var root = new GameObject("All Out War tunnel threshold test");
        generatedRoots.Add(root);
        var generator = root.AddComponent(ArenaGeneratorType);
        var hexShader = Shader.Find("ArenaShooter/WorldSpaceHexFloor");
        Assert.That(hexShader, Is.Not.Null);
        var hexMaterial = new Material(hexShader) { name = "Test All Out War Subfloor Hex Floor Material" };
        ArenaGeneratorType.GetField("generatingAllOutWar", PrivateInstance).SetValue(generator, true);
        ArenaGeneratorType.GetField("allOutWarHexFloorMaterial", PrivateInstance).SetValue(generator, hexMaterial);
        var layout = System.Activator.CreateInstance(ArenaLayoutType);
        var fromPortal = new Vector3(5f, 0f, 0f);
        var toPortal = new Vector3(59f, 0f, 0f);
        var tunnel = CreateSubfloorTunnel(
            Vector2Int.zero,
            new Vector2Int(4, 0),
            Vector2Int.right,
            Vector2Int.left,
            fromPortal,
            toPortal);
        TunnelRoutes(layout).Add(tunnel);

        ArenaGeneratorType.GetMethod("CreateAllOutWarTunnels", PrivateInstance).Invoke(
            generator,
            new object[] { System.Activator.CreateInstance(ArenaThemeType), root.transform, layout, RoomSize, CorridorLength, CorridorWidth, 4f });

        var thresholds = new List<Transform>();
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "All Out War Tunnel Threshold")
            {
                thresholds.Add(child);
            }
        }

        Assert.That(thresholds.Count, Is.EqualTo(2));
        var cutoutHalfWidth = GetFloat(tunnel, "Width") * 0.76f;
        AssertThresholdCoversRoomSide(thresholds, fromPortal, Vector3.right, cutoutHalfWidth);
        AssertThresholdCoversRoomSide(thresholds, toPortal, Vector3.left, cutoutHalfWidth);
        var collars = FindChildren(root, "All Out War Subfloor Tunnel Terrain Collar");
        Assert.That(collars.Count, Is.EqualTo(2));
        foreach (var collar in collars)
        {
            Assert.That(collar.TryGetComponent<Collider>(out _), Is.True);
            AssertTerrainMouthPieceUsesHexFloorSurface(collar, hexMaterial);
        }

        Physics.SyncTransforms();
        AssertSubfloorTerrainCollarSealsPortal(collars, tunnel, fromPortal, Vector3.right);
        AssertSubfloorTerrainCollarSealsPortal(collars, tunnel, toPortal, Vector3.left);
        AssertContinuousTunnelMeshCoversRoute(root, tunnel);
        Object.DestroyImmediate(hexMaterial);
    }

    [Test]
    public void SubfloorWallPortalCreatesMatchingRoomWallOpening()
    {
        var root = new GameObject("All Out War tunnel wall portal opening test");
        generatedRoots.Add(root);
        var generator = root.AddComponent(ArenaGeneratorType);
        var layout = System.Activator.CreateInstance(ArenaLayoutType);
        var fromRoom = Vector2Int.zero;
        var toRoom = new Vector2Int(4, 0);
        var fromPortal = new Vector3(5f, 0f, 0f);
        Rooms(layout).Add(fromRoom);
        Rooms(layout).Add(toRoom);
        RoomCenters(layout)[fromRoom] = Vector3.zero;
        RoomCenters(layout)[toRoom] = new Vector3(64f, 0f, 0f);
        TunnelRoutes(layout).Add(CreateSubfloorTunnel(
            fromRoom,
            toRoom,
            Vector2Int.right,
            Vector2Int.left,
            fromPortal,
            new Vector3(61.6f, 0f, 0f)));

        ArenaGeneratorType.GetMethod("CreateRoom", PrivateInstance).Invoke(
            generator,
            new object[] { System.Activator.CreateInstance(ArenaThemeType), root.transform, layout, fromRoom, RoomSize, CorridorWidth, 4f });

        Assert.That(FindChildren(root, "Arena Wall Door Segment").Count, Is.EqualTo(2));
        Assert.That(FindChildren(root, "Room Corridor Opening Pillar").Count, Is.EqualTo(2));
    }

    [Test]
    public void SubfloorEndpointCandidatesUseWallPortals()
    {
        var layout = CreateCircularLayout(4, RoomSize + CorridorLength);
        var candidates = InvokeBuildSubfloorEndpointCandidates(layout, null, RoomSize, CorridorWidth, 4.5f, 5.6f);

        Assert.That(candidates.Count, Is.GreaterThan(0));
        var candidate = candidates[0];
        var room = GetField<Vector2Int>(candidate, "Room");
        var direction = GetField<Vector2Int>(candidate, "Direction");
        var portal = GetField<Vector3>(candidate, "Portal");
        Assert.That(GetField<object>(candidate, "Source").ToString(), Is.EqualTo("WallPortal"));
        AssertSubfloorPortalIsRoomWallPortal(layout, room, direction, portal);
    }

    [Test]
    public void SubfloorEndpointCandidatesDoNotUseStandaloneOpenFloorPortals()
    {
        var layout = CreateCircularLayout(4, RoomSize + CorridorLength);
        var candidates = InvokeBuildSubfloorEndpointCandidates(layout, null, RoomSize, CorridorWidth, 4.5f, 5.6f);

        Assert.That(candidates.Count, Is.GreaterThan(0));
        Assert.That(FindCandidateBySource(candidates, "OpenFloor"), Is.Null);
        Assert.That(FindCandidateBySource(candidates, "ClearingInterior"), Is.Null);
    }

    [Test]
    public void SubfloorRouteFallbackAcceptsSafeWallPortalShortcutWhenStrictScoringRejects()
    {
        var layout = CreateLinearLayout(5, RoomSize + CorridorLength);
        var fromRoom = Vector2Int.zero;
        var toRoom = new Vector2Int(4, 0);
        var fromPortal = RoomCenters(layout)[fromRoom] + Vector3.forward * (RoomSize * 0.5f);
        var toPortal = RoomCenters(layout)[toRoom] + Vector3.forward * (RoomSize * 0.5f);
        var candidates = CreateSubfloorCandidateList(
            CreateSubfloorEndpointCandidate(fromRoom, Vector2Int.up, fromPortal),
            CreateSubfloorEndpointCandidate(toRoom, Vector2Int.up, toPortal));

        Assert.That(InvokeTrySelectSubfloorTunnelRoute(layout, candidates, 5, 0.45f, out _), Is.False);
        Assert.That(InvokeTrySelectSubfloorTunnelRoute(layout, candidates, 3, -0.2f, out var route), Is.True);
        TunnelRoutes(layout).Add(route);
        AssertSubfloorTunnelDescendsAndBends(route);
        AssertSubfloorPortalIsValidWallPortal(
            layout,
            route,
            GetVector2Int(route, "FromRoom"),
            GetVector3(route, "FromPortal"),
            GetVector2Int(route, "FromDirection"),
            true);
        AssertSubfloorPortalIsValidWallPortal(
            layout,
            route,
            GetVector2Int(route, "ToRoom"),
            GetVector3(route, "ToPortal"),
            GetVector2Int(route, "ToDirection"),
            false);
    }

    [Test]
    public void GeneratedRandomTunnelsHaveSafeExplicitEndpoints()
    {
        var layout = BuildFirstLayoutWithTunnel(RandomMapStyle);

        AssertGeneratedTunnelRoutesAreSafeAndExplicit(layout);
    }

    [Test]
    public void GeneratedHillyTunnelsHaveSafeExplicitEndpoints()
    {
        var layout = BuildFirstLayoutWithTunnel(HillyMapStyle);

        AssertGeneratedTunnelRoutesAreSafeAndExplicit(layout);
    }

    [Test]
    public void HillyTerrainReservationsCreateAtLeastThreeVisibleHills()
    {
        var terrainProfile = BuildHillyTerrainProfileForReservations(24680, 8, 4, out _);

        Assert.That(GetTerrainOnlyHillRegionCount(terrainProfile), Is.GreaterThanOrEqualTo(3));
        for (var i = 0; i < 3; i++)
        {
            var region = ReadTerrainOnlyHillRegion(terrainProfile, i);
            Assert.That(region.PeakHeight, Is.GreaterThan(0f));
            Assert.That(region.OuterRadius, Is.GreaterThan(region.CrestRadius));
            Assert.That(InvokeSampleTerrainHeight(terrainProfile, new Vector3(region.Center.x, 0f, region.Center.y)), Is.GreaterThan(3.5f));
        }
    }

    [Test]
    public void HillyTerrainReservationsKeepSpawnPathsConnected()
    {
        const int gridRadius = 8;
        const int totalArmies = 4;
        var terrainProfile = BuildHillyTerrainProfileForReservations(97531, gridRadius, totalArmies, out var allowed);

        Assert.That(GetTerrainOnlyHillRegionCount(terrainProfile), Is.GreaterThanOrEqualTo(3));
        var centralHub = FindNearestAllowedBuildableCell(allowed, Vector2.zero, terrainProfile);
        Assert.That(allowed.Contains(centralHub), Is.True);
        Assert.That(IsTerrainOnlyHillNoBuildCell(terrainProfile, centralHub), Is.False);
        for (var team = 0; team < totalArmies; team++)
        {
            var frontRoom = FindNearestAllowedBuildableCell(allowed, GetTestArmySpawnFrontTarget(team, totalArmies, gridRadius), terrainProfile);
            Assert.That(HasBuildablePath(allowed, centralHub, frontRoom, terrainProfile), Is.True);
        }
    }

    [Test]
    public void HillyTerrainCanReserveCenterHillAndUseNearbyBuildableHub()
    {
        const int gridRadius = 8;
        const int totalArmies = 4;
        var terrainProfile = CreateHillyTerrainProfile(13579, gridRadius, out var allowed);
        var protectedCells = BuildSpawnProtectedRooms(allowed, totalArmies, gridRadius);
        var eligibleCells = BuildEligibleHillCells(allowed, protectedCells);

        Assert.That(InvokeTryReserveTerrainOnlyHillCandidate(terrainProfile, allowed, eligibleCells, protectedCells, Vector2Int.zero, totalArmies, gridRadius, true), Is.True);
        Assert.That(IsTerrainOnlyHillNoBuildCell(terrainProfile, Vector2Int.zero), Is.True);
        var centralHub = FindNearestAllowedBuildableCell(allowed, Vector2.zero, terrainProfile);
        Assert.That(centralHub, Is.Not.EqualTo(Vector2Int.zero));
        Assert.That(HasBuildablePath(allowed, centralHub, FindNearestAllowedBuildableCell(allowed, GetTestArmySpawnFrontTarget(0, totalArmies, gridRadius), terrainProfile), terrainProfile), Is.True);
    }

    [Test]
    public void HillyTerrainCanReserveSpawnSideHillOutsideSpawnBuffer()
    {
        const int gridRadius = 8;
        const int totalArmies = 4;
        var terrainProfile = CreateHillyTerrainProfile(24681, gridRadius, out var allowed);
        var protectedCells = BuildSpawnProtectedRooms(allowed, totalArmies, gridRadius);
        var eligibleCells = BuildEligibleHillCells(allowed, protectedCells);
        var reserved = false;
        var seed = Vector2Int.zero;
        for (var x = 4; x <= 5 && !reserved; x++)
        {
            for (var y = -2; y <= 2 && !reserved; y++)
            {
                seed = new Vector2Int(x, y);
                if (!allowed.Contains(seed) || protectedCells.Contains(seed))
                {
                    continue;
                }

                reserved = InvokeTryReserveTerrainOnlyHillCandidate(terrainProfile, allowed, eligibleCells, protectedCells, seed, totalArmies, gridRadius, true);
            }
        }

        Assert.That(reserved, Is.True);
        var region = ReadTerrainOnlyHillRegion(terrainProfile, 0);
        Assert.That(region.Center.x, Is.GreaterThan(0f));
        foreach (var protectedCell in protectedCells)
        {
            var protectedPoint = new Vector2(protectedCell.x * (RoomSize + CorridorLength), protectedCell.y * (RoomSize + CorridorLength));
            Assert.That(Vector2.Distance(region.Center, protectedPoint), Is.GreaterThan(region.NoStructureRadius - 0.01f));
        }
    }

    [Test]
    public void HillyTerrainReservationsAllowShoulderOverlappingDoubleHills()
    {
        const int gridRadius = 8;
        const int totalArmies = 4;
        var baseProfile = CreateHillyTerrainProfile(13579, gridRadius, out var allowed);
        var protectedCells = BuildSpawnProtectedRooms(allowed, totalArmies, gridRadius);
        var eligibleCells = BuildEligibleHillCells(allowed, protectedCells);
        var firstSeed = Vector2Int.zero;
        Assert.That(InvokeTryReserveTerrainOnlyHillCandidate(baseProfile, allowed, eligibleCells, protectedCells, firstSeed, totalArmies, gridRadius, true), Is.True);
        var firstHill = ReadTerrainOnlyHillRegion(baseProfile, 0);

        var candidates = new List<Vector2Int>(allowed);
        candidates.Sort((a, b) =>
            Vector2.Distance(new Vector2(a.x, a.y) * (RoomSize + CorridorLength), firstHill.Center)
                .CompareTo(Vector2.Distance(new Vector2(b.x, b.y) * (RoomSize + CorridorLength), firstHill.Center)));

        object acceptedProfile = null;
        TestHillRegion acceptedFirst = default;
        TestHillRegion acceptedSecond = default;
        foreach (var candidate in candidates)
        {
            if (candidate == firstSeed || protectedCells.Contains(candidate))
            {
                continue;
            }

            var terrainProfile = CreateHillyTerrainProfile(13579, gridRadius, out _);
            Assert.That(InvokeTryReserveTerrainOnlyHillCandidate(terrainProfile, allowed, eligibleCells, protectedCells, firstSeed, totalArmies, gridRadius, true), Is.True);
            if (!InvokeTryReserveTerrainOnlyHillCandidate(terrainProfile, allowed, eligibleCells, protectedCells, candidate, totalArmies, gridRadius, true))
            {
                continue;
            }

            var first = ReadTerrainOnlyHillRegion(terrainProfile, 0);
            var second = ReadTerrainOnlyHillRegion(terrainProfile, 1);
            var distance = Vector2.Distance(first.Center, second.Center);
            if (distance < first.OuterRadius + second.OuterRadius &&
                distance > first.CrestRadius + second.CrestRadius)
            {
                acceptedProfile = terrainProfile;
                acceptedFirst = first;
                acceptedSecond = second;
                break;
            }
        }

        Assert.That(acceptedProfile, Is.Not.Null);
        var centerDistance = Vector2.Distance(acceptedFirst.Center, acceptedSecond.Center);
        Assert.That(centerDistance, Is.LessThan(acceptedFirst.OuterRadius + acceptedSecond.OuterRadius));
        Assert.That(centerDistance, Is.GreaterThan(acceptedFirst.CrestRadius + acceptedSecond.CrestRadius));

        var centralHub = FindNearestAllowedBuildableCell(allowed, Vector2.zero, acceptedProfile);
        for (var team = 0; team < totalArmies; team++)
        {
            var frontRoom = FindNearestAllowedBuildableCell(allowed, GetTestArmySpawnFrontTarget(team, totalArmies, gridRadius), acceptedProfile);
            Assert.That(HasBuildablePath(allowed, centralHub, frontRoom, acceptedProfile), Is.True);
        }
    }

    [Test]
    public void HillyTerrainUsesHighTraversableHillsWithVariableFootprints()
    {
        var classes = new HashSet<string>();
        var compactOrMedium = new List<TestHillRegion>();
        var large = new List<TestHillRegion>();
        for (var seed = 20000; seed < 20012; seed++)
        {
            var terrainProfile = BuildHillyTerrainProfileForReservations(seed, 8, 4, out _);
            var count = GetTerrainOnlyHillRegionCount(terrainProfile);
            Assert.That(count, Is.InRange(12, 20));
            for (var i = 0; i < count; i++)
            {
                var region = ReadTerrainOnlyHillRegion(terrainProfile, i);
                classes.Add(region.SizeClass);
                Assert.That(region.PeakHeight, Is.GreaterThanOrEqualTo(4.5f));
                Assert.That(region.CrestRadius, Is.GreaterThanOrEqualTo(RoomSize * 0.9f));
                Assert.That(region.OuterRadius - region.CrestRadius, Is.GreaterThanOrEqualTo(region.PeakHeight * 1.4f));
                if (region.SizeClass == "Large")
                {
                    large.Add(region);
                }
                else
                {
                    compactOrMedium.Add(region);
                }
            }
        }

        Assert.That(classes.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(compactOrMedium.Count, Is.GreaterThan(0));
        Assert.That(large.Count, Is.GreaterThan(0));
        Assert.That(MinNoStructureRadius(compactOrMedium), Is.LessThan(MinNoStructureRadius(large)));
    }

    [Test]
    public void HillyTerrainReservationsCreateThreeHillsOnSmallestBattlefield()
    {
        for (var seed = 30000; seed < 30006; seed++)
        {
            var terrainProfile = BuildHillyTerrainProfileForReservations(seed, 3, 2, out _);
            var count = GetTerrainOnlyHillRegionCount(terrainProfile);
            Assert.That(count, Is.GreaterThanOrEqualTo(6), $"seed {seed}");
            for (var i = 0; i < count; i++)
            {
                var region = ReadTerrainOnlyHillRegion(terrainProfile, i);
                Assert.That(region.PeakHeight, Is.GreaterThan(3f), $"seed {seed} hill {i}");
                Assert.That(
                    region.OuterRadius - region.CrestRadius,
                    Is.GreaterThan(region.PeakHeight * 1.4f),
                    $"seed {seed} hill {i} shoulder slope");
            }
        }
    }

    [Test]
    public void TerrainFlatPatchSpatialIndexMatchesBruteForceSampling()
    {
        var spacing = RoomSize + CorridorLength;
        var terrainProfile = BuildHillyTerrainProfileForReservations(24680, 8, 4, out _);
        var layout = CreateCircularLayout(5, spacing);
        AddTestSpawnRegion(layout, 0, new Vector2Int(0, -5), new Vector3(0f, 0f, -72f), Vector3.back);

        var fromRoom = new Vector2Int(-4, 0);
        var toRoom = new Vector2Int(4, 0);
        var fromPortal = RoomCenters(layout)[fromRoom] + Vector3.right * (RoomSize * 0.5f);
        var toPortal = RoomCenters(layout)[toRoom] + Vector3.left * (RoomSize * 0.5f);
        var tunnel = CreateSubfloorTunnel(fromRoom, toRoom, Vector2Int.right, Vector2Int.left, fromPortal, toPortal);
        TunnelRoutes(layout).Add(tunnel);

        AllOutWarTerrainProfileType.GetMethod("BuildFlatMasks", PublicInstance).Invoke(
            terrainProfile,
            new object[] { layout, RoomSize, CorridorLength, CorridorWidth });

        var hill = ReadTerrainOnlyHillRegion(terrainProfile, 0);
        var waypoints = GetWaypoints(tunnel);
        var samples = new List<Vector3>
        {
            RoomCenters(layout)[Vector2Int.zero],
            (RoomCenters(layout)[Vector2Int.zero] + RoomCenters(layout)[Vector2Int.right]) * 0.5f,
            RoomCenters(layout)[Vector2Int.zero] + new Vector3(RoomSize * 0.62f, 0f, RoomSize * 0.12f),
            new Vector3(hill.Center.x, 0f, hill.Center.y),
            new Vector3(hill.Center.x + hill.CrestRadius * 0.75f, 0f, hill.Center.y),
            new Vector3(0f, 0f, -72f),
            Vector3.Lerp(waypoints[0], waypoints[1], 0.5f),
            Vector3.Lerp(waypoints[^2], waypoints[^1], 0.5f),
        };

        for (var x = -4; x <= 4; x += 2)
        {
            for (var z = -4; z <= 4; z += 2)
            {
                samples.Add(new Vector3(x * spacing * 0.67f, 0f, z * spacing * 0.67f));
            }
        }

        foreach (var sample in samples)
        {
            AssertTerrainSampleMatchesBruteForce(terrainProfile, sample);
        }
    }

    [Test]
    public void TerrainDiskRenderAndColliderMeshesReuseAlignedSampledHeights()
    {
        var terrainProfile = BuildHillyTerrainProfileForReservations(13579, 8, 4, out _);
        var layout = CreateCircularLayout(4, RoomSize + CorridorLength);
        AllOutWarTerrainProfileType.GetMethod("BuildFlatMasks", PublicInstance).Invoke(
            terrainProfile,
            new object[] { layout, RoomSize, CorridorLength, CorridorWidth });

        var dataMethod = ArenaGeneratorType.GetMethod("CreateTerrainDiskMeshData", PrivateStatic);
        Assert.That(dataMethod, Is.Not.Null);
        var diskData = dataMethod.Invoke(null, new object[] { Vector3.zero, 42f, terrainProfile });
        var meshMethod = ArenaGeneratorType.GetMethod(
            "CreateTerrainDiskMesh",
            PrivateStatic,
            null,
            new[] { typeof(string), diskData.GetType(), typeof(Vector3) },
            null);
        Assert.That(meshMethod, Is.Not.Null);

        var renderMesh = (Mesh)meshMethod.Invoke(null, new object[] { "test render terrain", diskData, Vector3.down * 0.003f });
        var colliderMesh = (Mesh)meshMethod.Invoke(null, new object[] { "test collider terrain", diskData, Vector3.zero });
        try
        {
            Assert.That(renderMesh.vertexCount, Is.EqualTo(colliderMesh.vertexCount));
            Assert.That(renderMesh.GetTriangles(0), Is.EqualTo(colliderMesh.GetTriangles(0)));

            var renderVertices = renderMesh.vertices;
            var colliderVertices = colliderMesh.vertices;
            for (var i = 0; i < renderVertices.Length; i += Mathf.Max(1, renderVertices.Length / 32))
            {
                Assert.That(renderVertices[i].x, Is.EqualTo(colliderVertices[i].x).Within(0.0001f));
                Assert.That(renderVertices[i].z, Is.EqualTo(colliderVertices[i].z).Within(0.0001f));
                Assert.That(renderVertices[i].y, Is.EqualTo(colliderVertices[i].y - 0.003f).Within(0.0001f));
            }
        }
        finally
        {
            Object.DestroyImmediate(renderMesh);
            Object.DestroyImmediate(colliderMesh);
        }
    }

    private object BuildFirstLayoutWithTunnel(string mapStyle)
    {
        var seeds = new[]
        {
            10101,
            21212,
            32323,
            43434,
            54545,
            65656,
            76767,
            87878
        };

        foreach (var seed in seeds)
        {
            var root = new GameObject("All Out War tunnel generation test");
            generatedRoots.Add(root);
            var generator = root.AddComponent(ArenaGeneratorType);
            var gridRadius = mapStyle == HillyMapStyle ? 8 : 7;
            var layout = ArenaGeneratorType.GetMethod("GenerateAllOutWar", PublicInstance).Invoke(
                generator,
                new object[]
                {
                    System.Activator.CreateInstance(ArenaThemeType),
                    root.transform,
                    seed,
                    4,
                    58,
                    gridRadius,
                    RoomSize,
                    CorridorLength,
                    CorridorWidth,
                    4f,
                    12,
                    mapStyle
                });

            if (TunnelRoutes(layout).Count > 0)
            {
                return layout;
            }

            generatedRoots.Remove(root);
            Object.DestroyImmediate(root);
        }

        Assert.Fail($"Expected at least one generated tunnel for map style {mapStyle}.");
        return null;
    }

    private static object CreateCircularLayout(int gridRadius, float spacing)
    {
        var layout = System.Activator.CreateInstance(ArenaLayoutType);
        SetProperty(layout, "CellSpacing", spacing);
        SetProperty(layout, "CircularCenter", Vector3.zero);
        var playableRadius = gridRadius * spacing + Mathf.Max(4f, spacing * 0.35f);
        SetProperty(layout, "CircularRadius", playableRadius);
        SetProperty(layout, "DomeRadius", playableRadius);
        SetProperty(layout, "PerimeterSpawnRadius", Mathf.Max(spacing, playableRadius - 2.2f));

        var radiusSqr = gridRadius * gridRadius + 0.35f;
        for (var x = -gridRadius; x <= gridRadius; x++)
        {
            for (var y = -gridRadius; y <= gridRadius; y++)
            {
                if (x * x + y * y > radiusSqr)
                {
                    continue;
                }

                if ((x != 0 || y != 0) && x % 3 == 0 && y % 3 == 0)
                {
                    continue;
                }

                var room = new Vector2Int(x, y);
                Rooms(layout).Add(room);
                RoomCenters(layout)[room] = new Vector3(x * spacing, 0f, y * spacing);
            }
        }

        return layout;
    }

    private static object CreateLinearLayout(int roomCount, float spacing)
    {
        var layout = System.Activator.CreateInstance(ArenaLayoutType);
        SetProperty(layout, "CellSpacing", spacing);
        SetProperty(layout, "CircularCenter", Vector3.zero);
        SetProperty(layout, "CircularRadius", spacing * Mathf.Max(4, roomCount + 2));
        SetProperty(layout, "DomeRadius", spacing * Mathf.Max(4, roomCount + 2));
        SetProperty(layout, "PerimeterSpawnRadius", spacing * Mathf.Max(3, roomCount + 1));
        for (var i = 0; i < roomCount; i++)
        {
            var room = new Vector2Int(i, 0);
            Rooms(layout).Add(room);
            RoomCenters(layout)[room] = new Vector3(i * spacing, 0f, 0f);
        }

        return layout;
    }

    private static void AssertGeneratedTunnelRoutesAreSafeAndExplicit(object layout)
    {
        Assert.That(layout, Is.Not.Null);
        Assert.That(TunnelRoutes(layout).Count, Is.GreaterThan(0));

        foreach (var tunnel in TunnelRoutes(layout))
        {
            var fromRoom = GetVector2Int(tunnel, "FromRoom");
            var toRoom = GetVector2Int(tunnel, "ToRoom");
            var fromDirection = GetVector2Int(tunnel, "FromDirection");
            var toDirection = GetVector2Int(tunnel, "ToDirection");
            var fromPortal = GetVector3(tunnel, "FromPortal");
            var toPortal = GetVector3(tunnel, "ToPortal");
            var width = GetFloat(tunnel, "Width");

            Assert.That(IsCardinal(fromDirection), Is.True);
            Assert.That(IsCardinal(toDirection), Is.True);
            if (GetEnumName(tunnel, "Kind") == "Subfloor")
            {
                Assert.That(GetEnumName(tunnel, "FromEntranceMode"), Is.EqualTo("WallPortal"));
                Assert.That(GetEnumName(tunnel, "ToEntranceMode"), Is.EqualTo("WallPortal"));
                AssertSubfloorTunnelDescendsAndBends(tunnel);
                AssertSubfloorPortalIsValidWallPortal(layout, tunnel, fromRoom, fromPortal, fromDirection, true);
                AssertSubfloorPortalIsValidWallPortal(layout, tunnel, toRoom, toPortal, toDirection, false);
            }
            else
            {
                Assert.That(GetEnumName(tunnel, "FromEntranceMode"), Is.EqualTo("HillsideMouth"));
                Assert.That(GetEnumName(tunnel, "ToEntranceMode"), Is.EqualTo("HillsideMouth"));
                Assert.That(GetInt(tunnel, "HillRegionIndex"), Is.GreaterThanOrEqualTo(0));
                AssertHillCutTunnelCanRemainAtSurface(tunnel);
                AssertHillCutPortalIsDetachedFromRoomBoundary(layout, fromRoom, fromPortal);
                AssertHillCutPortalIsDetachedFromRoomBoundary(layout, toRoom, toPortal);
            }

            AssertTunnelAvoidsSpawnRegions(layout, tunnel);
            AssertTunnelStaysInsideDome(layout, tunnel);
            AssertTunnelFootprintIsReserved(layout, tunnel);

            Assert.That(InvokeTryGetDoorwayPoint(layout, fromRoom, toRoom, out var fromTraversal), Is.True);
            Assert.That(Vector3.Distance(fromTraversal, toPortal), Is.LessThan(0.001f));
            Assert.That(InvokeTryGetDoorwayPoint(layout, toRoom, fromRoom, out var toTraversal), Is.True);
            Assert.That(Vector3.Distance(toTraversal, fromPortal), Is.LessThan(0.001f));
        }
    }

    private static void AssertSubfloorTunnelDescendsAndBends(object tunnel)
    {
        var waypoints = GetWaypoints(tunnel);
        var height = GetFloat(tunnel, "Height");
        var depth = GetFloat(tunnel, "SubfloorDepth");
        Assert.That(waypoints.Count, Is.GreaterThanOrEqualTo(6));
        Assert.That(depth, Is.GreaterThan(height + 1f));
        Assert.That(waypoints[0].y, Is.EqualTo(0f).Within(0.001f));
        Assert.That(waypoints[^1].y, Is.EqualTo(0f).Within(0.001f));

        for (var i = 1; i < waypoints.Count - 1; i++)
        {
            Assert.That(waypoints[i].y, Is.LessThanOrEqualTo(-depth + 0.001f));
        }

        var undergroundStart = waypoints[1];
        var undergroundEnd = waypoints[^2];
        var width = GetFloat(tunnel, "Width");
        var fromDirection = ToWorldDirection(GetVector2Int(tunnel, "FromDirection"));
        var toDirection = ToWorldDirection(GetVector2Int(tunnel, "ToDirection"));
        var firstUndergroundDirection = Flatten(waypoints[2] - undergroundStart).normalized;
        var lastUndergroundDirection = Flatten(undergroundEnd - waypoints[^3]).normalized;
        Assert.That(Vector3.Dot(firstUndergroundDirection, fromDirection), Is.GreaterThan(0.98f));
        Assert.That(Vector3.Dot(lastUndergroundDirection, -toDirection), Is.GreaterThan(0.98f));
        Assert.That(FlatDistance(waypoints[2], undergroundStart), Is.GreaterThan(width * 1.2f));
        Assert.That(FlatDistance(undergroundEnd, waypoints[^3]), Is.GreaterThan(width * 1.2f));

        var hasBend = false;
        for (var i = 3; i < waypoints.Count - 3; i++)
        {
            if (FlatDistanceToSegment(waypoints[i], undergroundStart, undergroundEnd) > 0.25f)
            {
                hasBend = true;
                break;
            }
        }

        Assert.That(hasBend, Is.True);
    }

    private static void AssertHillCutTunnelCanRemainAtSurface(object tunnel)
    {
        Assert.That(GetFloat(tunnel, "SubfloorDepth"), Is.EqualTo(0f).Within(0.001f));
        Assert.That(GetFloat(tunnel, "RampLength"), Is.EqualTo(0f).Within(0.001f));
        foreach (var waypoint in GetWaypoints(tunnel))
        {
            Assert.That(waypoint.y, Is.GreaterThanOrEqualTo(-0.001f));
        }
    }

    private static void AssertContinuousTunnelMeshCoversRoute(GameObject root, object tunnel)
    {
        var floors = FindChildren(root, "All Out War Tunnel Floor");
        var shells = FindChildren(root, "All Out War Tunnel Continuous Shell");
        var sideWalls = FindChildren(root, "All Out War Tunnel Side Wall");
        var traceRings = FindChildren(root, "All Out War Tunnel Octagon Trace Rings");
        Assert.That(floors.Count, Is.EqualTo(1));
        Assert.That(shells.Count, Is.EqualTo(1));
        Assert.That(sideWalls.Count, Is.EqualTo(1));
        Assert.That(traceRings.Count, Is.EqualTo(1));
        Assert.That(FindChildren(root, "All Out War Tunnel Neon Edge Strip").Count, Is.Zero);
        Assert.That(FindChildren(root, "All Out War Tunnel Neon Ceiling Strip").Count, Is.Zero);
        Assert.That(floors[0].TryGetComponent<Collider>(out _), Is.True);
        Assert.That(sideWalls[0].TryGetComponent<Collider>(out _), Is.True);
        Assert.That(shells[0].TryGetComponent<Collider>(out _), Is.False);
        AssertTunnelTraceRingsAreVisualOnly(traceRings[0]);

        foreach (var waypoint in GetWaypoints(tunnel))
        {
            Assert.That(InvokeIsTunnelReservedPositionFromRoute(tunnel, waypoint), Is.True);
        }
    }

    private static void AssertTunnelTraceRingsAreVisualOnly(Transform traceRings)
    {
        Assert.That(traceRings.TryGetComponent<Collider>(out _), Is.False);
        Assert.That(traceRings.TryGetComponent<Renderer>(out var renderer), Is.True);
        Assert.That(renderer.sharedMaterial, Is.Not.Null);
        Assert.That(renderer.sharedMaterial.name, Does.Contain("Cyan"));
        Assert.That(renderer.renderingLayerMask, Is.EqualTo(GetDefaultRenderingLayer()));
        Assert.That(traceRings.TryGetComponent<MeshFilter>(out var filter), Is.True);
        Assert.That(filter.sharedMesh, Is.Not.Null);
        Assert.That(filter.sharedMesh.vertexCount, Is.GreaterThan(0));
        Assert.That(filter.sharedMesh.vertexCount % 32, Is.EqualTo(0));

        var ringCount = filter.sharedMesh.vertexCount / 32;
        Assert.That(ringCount, Is.GreaterThanOrEqualTo(2));
        var vertices = filter.sharedMesh.vertices;
        var firstCenter = AverageVertices(vertices, 0, 32);
        var farthestCenterDistance = 0f;
        for (var ring = 1; ring < ringCount; ring++)
        {
            var center = AverageVertices(vertices, ring * 32, 32);
            farthestCenterDistance = Mathf.Max(farthestCenterDistance, Vector3.Distance(firstCenter, center));
        }

        Assert.That(farthestCenterDistance, Is.GreaterThan(2f));
    }

    private static void AssertTerrainMouthPieceUsesHexFloorSurface(Transform piece, Material expectedMaterial)
    {
        Assert.That(piece.TryGetComponent<MeshRenderer>(out var renderer), Is.True);
        Assert.That(renderer.sharedMaterial, Is.EqualTo(expectedMaterial));
        Assert.That(renderer.renderingLayerMask, Is.EqualTo(GetDefaultRenderingLayer()));
        Assert.That(renderer.renderingLayerMask, Is.Not.EqualTo(GetFloorRenderingLayer()));
        Assert.That(piece.TryGetComponent<MeshFilter>(out var filter), Is.True);
        Assert.That(filter.sharedMesh, Is.Not.Null);

        var normals = filter.sharedMesh.normals;
        Assert.That(normals.Length, Is.EqualTo(filter.sharedMesh.vertexCount));
        foreach (var normal in normals)
        {
            Assert.That(Vector3.Dot(normal.normalized, Vector3.up), Is.GreaterThan(0.82f));
        }
    }

    private static void AssertHillMouthSideCollarsSealCutFaces(
        List<Transform> sideCollars,
        object terrainProfile,
        Vector3 outerApproach,
        Vector3 innerMouth,
        Vector3 inwardDirection,
        float outerHalfWidth,
        float innerHalfWidth)
    {
        var direction = inwardDirection;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : (innerMouth - outerApproach).normalized;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        var side = new Vector3(-direction.z, 0f, direction.x);
        const float sampleT = 0.72f;
        var center = Vector3.Lerp(outerApproach, innerMouth, sampleT);
        var halfWidth = Mathf.Lerp(outerHalfWidth, innerHalfWidth, sampleT);

        for (var sideSign = -1; sideSign <= 1; sideSign += 2)
        {
            var signedSide = side * sideSign;
            var edge = center + signedSide * halfWidth;
            var sampledHeight = InvokeSampleTerrainHeight(terrainProfile, edge);
            Assert.That(sampledHeight, Is.GreaterThan(4f));

            var horizontalOrigin = edge - signedSide * 0.65f;
            horizontalOrigin.y = Mathf.Clamp(sampledHeight * 0.42f, 1.1f, 2.35f);
            AssertRayHitsNamed(
                horizontalOrigin,
                signedSide,
                1.5f,
                "All Out War Hill Tunnel Mouth Side Collar");

            var topProbe = edge + signedSide * 0.5f;
            topProbe.y = sampledHeight + 4f;
            AssertRayHitsNamed(
                topProbe,
                Vector3.down,
                12f,
                "All Out War Hill Tunnel Mouth Side Collar");
        }
    }

    private static void AssertSubfloorTerrainCollarSealsPortal(List<Transform> collars, object tunnel, Vector3 portal, Vector3 direction)
    {
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        var side = new Vector3(-direction.z, 0f, direction.x);
        var width = GetFloat(tunnel, "Width");
        var rampLength = GetFloat(tunnel, "RampLength");
        var subfloorDepth = GetFloat(tunnel, "SubfloorDepth");
        var cutoutHalfWidth = width * 0.76f;
        var visualHalfWidth = cutoutHalfWidth + Mathf.Max(0.35f, width * 0.16f);
        var clearHalfWidth = Mathf.Min(visualHalfWidth - 0.08f, Mathf.Max(width * 0.5f + 0.12f, width * 0.5f));
        clearHalfWidth = Mathf.Max(width * 0.5f, clearHalfWidth);
        var thresholdDepth = Mathf.Clamp(width * 0.34f, 1.15f, 1.75f);

        for (var sideSign = -1; sideSign <= 1; sideSign += 2)
        {
            var signedSide = side * sideSign;
            var thresholdProbe = portal - direction * (thresholdDepth * 0.65f) + signedSide * (visualHalfWidth - 0.18f);
            thresholdProbe.y = 2f;
            AssertRayHitsOneOf(collars, thresholdProbe, Vector3.down, 4f);

            var rimProbe = portal + direction * (rampLength * 0.45f) + signedSide * (visualHalfWidth - 0.18f);
            rimProbe.y = 2f;
            AssertRayHitsOneOf(collars, rimProbe, Vector3.down, 4f);

            var skirtProbe = portal + direction * (rampLength * 0.45f) + signedSide * (clearHalfWidth - 0.035f);
            skirtProbe.y = -subfloorDepth * 0.45f - 0.06f;
            AssertRayHitsOneOf(collars, skirtProbe, signedSide, 0.5f);
        }

        var endProbe = portal + direction * (rampLength + 0.2f);
        endProbe.y = 2f;
        AssertRayHitsOneOf(collars, endProbe, Vector3.down, 4f);
    }

    private static RaycastHit AssertRayHitsNamed(Vector3 origin, Vector3 direction, float distance, string expectedName)
    {
        var hits = Physics.RaycastAll(origin, direction.normalized, distance);
        foreach (var hit in hits)
        {
            if (hit.transform.name == expectedName)
            {
                return hit;
            }
        }

        var hitNames = string.Join(", ", hits.Select(hit => hit.transform.name));
        Assert.Fail($"Expected ray from {origin} toward {direction} to hit {expectedName}. Hits: {hitNames}");
        return default;
    }

    private static RaycastHit AssertRayHitsOneOf(List<Transform> expectedTargets, Vector3 origin, Vector3 direction, float distance)
    {
        var hits = Physics.RaycastAll(origin, direction.normalized, distance);
        foreach (var hit in hits)
        {
            if (expectedTargets.Contains(hit.transform))
            {
                return hit;
            }
        }

        var hitNames = string.Join(", ", hits.Select(hit => hit.transform.name));
        Assert.Fail($"Expected ray from {origin} toward {direction} to hit a terrain collar. Hits: {hitNames}");
        return default;
    }

    private static Vector3 AverageVertices(Vector3[] vertices, int start, int count)
    {
        var sum = Vector3.zero;
        for (var i = 0; i < count; i++)
        {
            sum += vertices[start + i];
        }

        return sum / Mathf.Max(1, count);
    }

    private static void AssertThresholdCoversRoomSide(List<Transform> thresholds, Vector3 portal, Vector3 direction, float cutoutHalfWidth)
    {
        var best = thresholds[0];
        var bestDistance = Vector3.Distance(best.position, portal);
        for (var i = 1; i < thresholds.Count; i++)
        {
            var distance = Vector3.Distance(thresholds[i].position, portal);
            if (distance < bestDistance)
            {
                best = thresholds[i];
                bestDistance = distance;
            }
        }

        Assert.That(Vector3.Dot(best.position - portal, direction), Is.LessThan(0f));
        Assert.That(best.TryGetComponent<Collider>(out var collider), Is.True);
        AssertPointInsideUnitCube(best, portal - direction.normalized * 0.7f + Vector3.down * 0.08f);
        AssertPointInsideUnitCube(best, portal + direction.normalized * 0.05f + Vector3.down * 0.08f);
        var side = new Vector3(-direction.z, 0f, direction.x).normalized;
        AssertPointInsideUnitCube(best, portal - direction.normalized * 0.12f + side * (cutoutHalfWidth * 0.96f) + Vector3.down * 0.08f);
        AssertPointInsideUnitCube(best, portal - direction.normalized * 0.12f - side * (cutoutHalfWidth * 0.96f) + Vector3.down * 0.08f);
    }

    private static List<Transform> FindChildren(GameObject root, string childName)
    {
        var children = new List<Transform>();
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                children.Add(child);
            }
        }

        return children;
    }

    private static void AssertNoPanelContainsPoint(List<Transform> panels, Vector3 point)
    {
        foreach (var panel in panels)
        {
            Assert.That(IsPointInsideUnitCube(panel, point), Is.False);
        }
    }

    private static void AssertAnyPanelContainsPoint(List<Transform> panels, Vector3 point)
    {
        foreach (var panel in panels)
        {
            if (IsPointInsideUnitCube(panel, point))
            {
                return;
            }
        }

        Assert.Fail("Expected at least one split room-floor panel to cover the sample point.");
    }

    private static void AssertPointInsideUnitCube(Transform cube, Vector3 point)
    {
        Assert.That(IsPointInsideUnitCube(cube, point), Is.True);
    }

    private static bool IsPointInsideUnitCube(Transform cube, Vector3 point)
    {
        var local = cube.InverseTransformPoint(point);
        return Mathf.Abs(local.x) <= 0.5f &&
               Mathf.Abs(local.y) <= 0.5f &&
               Mathf.Abs(local.z) <= 0.5f;
    }

    private static void AssertSubfloorPortalIsRoomWallPortal(object layout, Vector2Int room, Vector2Int direction, Vector3 portal)
    {
        Assert.That(RoomCenters(layout).TryGetValue(room, out var center), Is.True);
        Assert.That(Rooms(layout).Contains(room + direction), Is.False);
        var expected = center + ToWorldDirection(direction) * (RoomSize * 0.5f);
        Assert.That(FlatDistance(portal, expected), Is.LessThan(0.001f));
    }

    private static void AssertSubfloorPortalIsValidWallPortal(object layout, object tunnel, Vector2Int room, Vector3 portal, Vector2Int direction, bool fromEndpoint)
    {
        Assert.That(portal.y, Is.EqualTo(0f).Within(0.001f));
        Assert.That(InvokeIsTunnelReservedPosition(layout, portal), Is.True);
        AssertSubfloorPortalIsRoomWallPortal(layout, room, direction, portal);

        var waypoints = GetWaypoints(tunnel);
        var endpoint = fromEndpoint ? waypoints[0] : waypoints[^1];
        var rampBottom = fromEndpoint ? waypoints[1] : waypoints[^2];
        Assert.That(Vector3.Distance(endpoint, portal), Is.LessThan(0.001f));
        Assert.That(rampBottom.y, Is.LessThan(portal.y - 0.5f));

        var expectedDirection = ToWorldDirection(direction);
        var actualDirection = Flatten(rampBottom - portal).normalized;
        Assert.That(Vector3.Dot(actualDirection, expectedDirection), Is.GreaterThan(0.98f));
        AssertPointInsideDome(layout, portal, GetFloat(tunnel, "Width"));

        foreach (var region in ArmySpawnRegions(layout))
        {
            Assert.That(InvokeContainsPoint(region, portal, SpawnTunnelPadding), Is.False);
        }
    }

    private static void AssertHillCutPortalIsDetachedFromRoomBoundary(object layout, Vector2Int room, Vector3 portal)
    {
        Assert.That(RoomCenters(layout).TryGetValue(room, out var center), Is.True);
        var distance = FlatDistance(center, portal);
        Assert.That(distance, Is.GreaterThan(RoomSize * 0.7f));
    }

    private static void AssertTunnelAvoidsSpawnRegions(object layout, object tunnel)
    {
        var fromPortal = GetVector3(tunnel, "FromPortal");
        var toPortal = GetVector3(tunnel, "ToPortal");
        foreach (var region in ArmySpawnRegions(layout))
        {
            Assert.That(InvokeContainsPoint(region, fromPortal, SpawnTunnelPadding), Is.False);
            Assert.That(InvokeContainsPoint(region, toPortal, SpawnTunnelPadding), Is.False);
            var waypoints = GetWaypoints(tunnel);
            for (var i = 1; i < waypoints.Count; i++)
            {
                var midpoint = (waypoints[i - 1] + waypoints[i]) * 0.5f;
                Assert.That(InvokeContainsPoint(region, midpoint, SpawnTunnelPadding), Is.False);
            }
        }
    }

    private static void AssertTunnelStaysInsideDome(object layout, object tunnel)
    {
        var domeRadius = GetFloat(layout, "DomeRadius");
        if (domeRadius <= 0f)
        {
            return;
        }

        var center = GetVector3(layout, "CircularCenter");
        var tunnelWidth = GetFloat(tunnel, "Width");
        var limit = domeRadius - Mathf.Max(0.75f, tunnelWidth * 0.68f) + 0.001f;
        var waypoints = GetWaypoints(tunnel);
        for (var pointIndex = 1; pointIndex < waypoints.Count; pointIndex++)
        {
            var start = waypoints[pointIndex - 1];
            var end = waypoints[pointIndex];
            for (var i = 0; i <= 8; i++)
            {
                var point = Vector3.Lerp(start, end, i / 8f);
                var toCenter = new Vector2(point.x - center.x, point.z - center.z);
                Assert.That(toCenter.magnitude, Is.LessThanOrEqualTo(limit));
            }
        }
    }

    private static void AssertPointInsideDome(object layout, Vector3 point, float tunnelWidth)
    {
        var domeRadius = GetFloat(layout, "DomeRadius");
        if (domeRadius <= 0f)
        {
            return;
        }

        var center = GetVector3(layout, "CircularCenter");
        var limit = domeRadius - Mathf.Max(0.75f, tunnelWidth * 0.68f) + 0.001f;
        var toCenter = new Vector2(point.x - center.x, point.z - center.z);
        Assert.That(toCenter.magnitude, Is.LessThanOrEqualTo(limit));
    }

    private static void AssertTunnelFootprintIsReserved(object layout, object tunnel)
    {
        Assert.That(InvokeIsTunnelReservedPosition(layout, GetVector3(tunnel, "FromPortal")), Is.True);
        Assert.That(InvokeIsTunnelReservedPosition(layout, GetVector3(tunnel, "ToPortal")), Is.True);
        var waypoints = GetWaypoints(tunnel);
        for (var i = 1; i < waypoints.Count; i++)
        {
            var midpoint = (waypoints[i - 1] + waypoints[i]) * 0.5f;
            Assert.That(InvokeIsTunnelReservedPosition(layout, midpoint), Is.True);
        }
    }

    private static HashSet<Vector2Int> Rooms(object layout)
    {
        return (HashSet<Vector2Int>)ArenaLayoutType.GetField("Rooms", PublicInstanceFields).GetValue(layout);
    }

    private static Dictionary<Vector2Int, Vector3> RoomCenters(object layout)
    {
        return (Dictionary<Vector2Int, Vector3>)ArenaLayoutType.GetField("RoomCenters", PublicInstanceFields).GetValue(layout);
    }

    private static IList TunnelRoutes(object layout)
    {
        return (IList)ArenaLayoutType.GetField("TunnelRoutes", PublicInstanceFields).GetValue(layout);
    }

    private static IList ArmySpawnRegions(object layout)
    {
        return (IList)ArenaLayoutType.GetField("ArmySpawnRegions", PublicInstanceFields).GetValue(layout);
    }

    private static void AddTestSpawnRegion(object layout, int teamId, Vector2Int room, Vector3 center, Vector3 outward)
    {
        var region = System.Activator.CreateInstance(
            ArmySpawnRegionType,
            teamId,
            room,
            center,
            Quaternion.LookRotation(-outward.normalized, Vector3.up),
            center - outward.normalized * 6f,
            System.Array.Empty<Vector3>(),
            Vector3.zero,
            outward,
            center.magnitude,
            8f,
            6f,
            2.5f);
        ArmySpawnRegions(layout).Add(region);
    }

    private static object CreateSubfloorTunnel(
        Vector2Int fromRoom,
        Vector2Int toRoom,
        Vector2Int fromDirection,
        Vector2Int toDirection,
        Vector3 fromPortal,
        Vector3 toPortal)
    {
        return System.Activator.CreateInstance(
            ArenaTunnelRouteType,
            System.Enum.Parse(ArenaTunnelKindType, "Subfloor"),
            fromRoom,
            toRoom,
            fromDirection,
            toDirection,
            fromPortal,
            toPortal,
            4.5f,
            4f,
            6f,
            10.5f,
            4.5f);
    }

    private static object CreateHillCutTunnel(
        Vector2Int fromRoom,
        Vector2Int toRoom,
        Vector2Int fromDirection,
        Vector2Int toDirection,
        Vector3 fromPortal,
        Vector3 toPortal,
        int hillRegionIndex = -1)
    {
        return System.Activator.CreateInstance(
            ArenaTunnelRouteType,
            System.Enum.Parse(ArenaTunnelKindType, "HillCut"),
            fromRoom,
            toRoom,
            fromDirection,
            toDirection,
            fromPortal,
            toPortal,
            4.5f,
            4f,
            0f,
            0f,
            4.5f,
            hillRegionIndex);
    }

    private static object CreateCompoundHillCutTunnel(
        Vector2Int fromRoom,
        Vector2Int toRoom,
        Vector2Int fromDirection,
        Vector2Int toDirection,
        Vector3 fromPortal,
        Vector3 toPortal,
        int fromHillRegionIndex,
        int toHillRegionIndex)
    {
        return System.Activator.CreateInstance(
            ArenaTunnelRouteType,
            System.Enum.Parse(ArenaTunnelKindType, "HillCut"),
            fromRoom,
            toRoom,
            fromDirection,
            toDirection,
            fromPortal,
            toPortal,
            4.5f,
            4f,
            0f,
            0f,
            4.5f,
            fromHillRegionIndex,
            toHillRegionIndex);
    }

    private static IList CreateSubfloorCandidateList(params object[] candidates)
    {
        var listType = typeof(List<>).MakeGenericType(SubfloorEndpointCandidateType);
        var list = (IList)System.Activator.CreateInstance(listType);
        foreach (var candidate in candidates)
        {
            list.Add(candidate);
        }

        return list;
    }

    private static object CreateSubfloorEndpointCandidate(Vector2Int room, Vector2Int direction, Vector3 portal)
    {
        return System.Activator.CreateInstance(
            SubfloorEndpointCandidateType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new object[]
            {
                room,
                direction,
                portal,
                0.75f,
                System.Enum.Parse(SubfloorEndpointSourceType, "WallPortal")
            },
            null);
    }

    private static object BuildHillyTerrainProfileWithRegion(out TestHillRegion region)
    {
        var spacing = RoomSize + CorridorLength;
        var gridRadius = 8;
        var allowed = BuildAllowedCells(gridRadius);

        for (var seed = 10000; seed < 10100; seed++)
        {
            var terrainProfile = System.Activator.CreateInstance(
                AllOutWarTerrainProfileType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[] { seed, spacing, System.Enum.Parse(AllOutWarMapStyleType, "Hilly") },
                null);
            AllOutWarTerrainProfileType.GetMethod("BuildTerrainOnlyHillReservations", PublicInstance).Invoke(
                terrainProfile,
                new object[] { allowed, new HashSet<Vector2Int>(), 4, gridRadius, spacing, RoomSize });
            if ((bool)AllOutWarTerrainProfileType.GetProperty("HasTerrainOnlyHills", PublicInstance).GetValue(terrainProfile))
            {
                // Dense hilly maps can block a particular hill's approach flats with a
                // neighboring hill's shoulder; every caller probes hill 0 along the X axis,
                // so pick a seed whose first hill actually supports that cut.
                if (!InvokeTryBuildHillCutEndpoint(terrainProfile, 0, Vector3.right, 4.5f, 4f, RoomSize * 0.5f, out _) ||
                    !InvokeTryBuildHillCutEndpoint(terrainProfile, 0, Vector3.left, 4.5f, 4f, RoomSize * 0.5f, out _))
                {
                    continue;
                }

                region = ReadTerrainOnlyHillRegion(terrainProfile, 0);
                return terrainProfile;
            }
        }

        Assert.Fail("Expected a hilly terrain profile with an X-axis hill-cut capable first hill.");
        region = default;
        return null;
    }

    private static object BuildCompoundHillTerrainProfile()
    {
        var terrainProfile = System.Activator.CreateInstance(
            AllOutWarTerrainProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new object[] { 424242, RoomSize + CorridorLength, System.Enum.Parse(AllOutWarMapStyleType, "Hilly") },
            null);
        var regionType = AllOutWarTerrainProfileType.GetNestedType("TerrainOnlyHillRegion", BindingFlags.NonPublic);
        var sizeType = AllOutWarTerrainProfileType.GetNestedType("TerrainOnlyHillSizeClass", BindingFlags.NonPublic);
        Assert.That(regionType, Is.Not.Null);
        Assert.That(sizeType, Is.Not.Null);
        var large = System.Enum.Parse(sizeType, "Large");
        var regions = (IList)AllOutWarTerrainProfileType.GetField("terrainOnlyHillRegions", PrivateInstance).GetValue(terrainProfile);
        regions.Add(System.Activator.CreateInstance(
            regionType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new object[] { new Vector2(-17f, 0f), 10f, 8f, 27f, 28f, 29f, large },
            null));
        regions.Add(System.Activator.CreateInstance(
            regionType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new object[] { new Vector2(17f, 0f), 10f, 8f, 27f, 28f, 29f, large },
            null));
        return terrainProfile;
    }

    private static object BuildHillyTerrainProfileForReservations(int seed, int gridRadius, int totalArmies, out HashSet<Vector2Int> allowed)
    {
        var terrainProfile = CreateHillyTerrainProfile(seed, gridRadius, out allowed);
        var spawnProtectedRooms = BuildSpawnProtectedRooms(allowed, totalArmies, gridRadius);
        AllOutWarTerrainProfileType.GetMethod("BuildTerrainOnlyHillReservations", PublicInstance).Invoke(
            terrainProfile,
            new object[] { allowed, spawnProtectedRooms, totalArmies, gridRadius, RoomSize + CorridorLength, RoomSize });
        return terrainProfile;
    }

    private static object CreateHillyTerrainProfile(int seed, int gridRadius, out HashSet<Vector2Int> allowed)
    {
        allowed = BuildAllowedCells(gridRadius);
        return System.Activator.CreateInstance(
            AllOutWarTerrainProfileType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new object[] { seed, RoomSize + CorridorLength, System.Enum.Parse(AllOutWarMapStyleType, "Hilly") },
            null);
    }

    private static HashSet<Vector2Int> BuildSpawnProtectedRooms(HashSet<Vector2Int> allowed, int totalArmies, int gridRadius)
    {
        return (HashSet<Vector2Int>)ArenaGeneratorType.GetMethod("BuildArmySpawnProtectedRooms", PrivateStatic).Invoke(
            null,
            new object[] { allowed, totalArmies, gridRadius });
    }

    private static HashSet<Vector2Int> BuildEligibleHillCells(HashSet<Vector2Int> allowed, HashSet<Vector2Int> protectedCells)
    {
        var eligibleCells = new HashSet<Vector2Int>(allowed);
        eligibleCells.ExceptWith(protectedCells);
        return eligibleCells;
    }

    private static float MinNoStructureRadius(List<TestHillRegion> regions)
    {
        var min = float.PositiveInfinity;
        foreach (var region in regions)
        {
            min = Mathf.Min(min, region.NoStructureRadius);
        }

        return min;
    }

    private static HashSet<Vector2Int> BuildAllowedCells(int gridRadius)
    {
        var allowed = new HashSet<Vector2Int>();
        var radiusSqr = gridRadius * gridRadius + 0.35f;
        for (var x = -gridRadius; x <= gridRadius; x++)
        {
            for (var y = -gridRadius; y <= gridRadius; y++)
            {
                if (x * x + y * y <= radiusSqr)
                {
                    allowed.Add(new Vector2Int(x, y));
                }
            }
        }

        return allowed;
    }

    private static int GetTerrainOnlyHillRegionCount(object terrainProfile)
    {
        return (int)AllOutWarTerrainProfileType.GetProperty("TerrainOnlyHillRegionCount", PublicInstance).GetValue(terrainProfile);
    }

    private static bool IsTerrainOnlyHillNoBuildCell(object terrainProfile, Vector2Int cell)
    {
        return (bool)AllOutWarTerrainProfileType.GetMethod("IsTerrainOnlyHillNoBuildCell", PublicInstance).Invoke(terrainProfile, new object[] { cell });
    }

    private static Vector2 GetTestArmySpawnFrontTarget(int team, int totalArmies, int gridRadius)
    {
        var angle = Mathf.PI * 2f * team / Mathf.Max(1, totalArmies);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Max(1f, gridRadius - 0.2f);
    }

    private static Vector2Int FindNearestAllowedBuildableCell(HashSet<Vector2Int> allowed, Vector2 target, object terrainProfile)
    {
        var best = Vector2Int.zero;
        var bestDistance = float.PositiveInfinity;
        foreach (var cell in allowed)
        {
            if (IsTerrainOnlyHillNoBuildCell(terrainProfile, cell))
            {
                continue;
            }

            var distance = (new Vector2(cell.x, cell.y) - target).sqrMagnitude;
            if (distance < bestDistance)
            {
                best = cell;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static bool HasBuildablePath(HashSet<Vector2Int> allowed, Vector2Int start, Vector2Int goal, object terrainProfile)
    {
        if (!allowed.Contains(start) ||
            !allowed.Contains(goal) ||
            IsTerrainOnlyHillNoBuildCell(terrainProfile, start) ||
            IsTerrainOnlyHillNoBuildCell(terrainProfile, goal))
        {
            return false;
        }

        var visited = new HashSet<Vector2Int> { start };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == goal)
            {
                return true;
            }

            foreach (var direction in CardinalDirections)
            {
                var next = current + direction;
                if (!allowed.Contains(next) ||
                    visited.Contains(next) ||
                    IsTerrainOnlyHillNoBuildCell(terrainProfile, next))
                {
                    continue;
                }

                visited.Add(next);
                queue.Enqueue(next);
            }
        }

        return false;
    }

    private static TestHillRegion ReadTerrainOnlyHillRegion(object terrainProfile, int index)
    {
        var regions = (IList)AllOutWarTerrainProfileType.GetField("terrainOnlyHillRegions", PrivateInstance).GetValue(terrainProfile);
        Assert.That(regions.Count, Is.GreaterThan(index));
        var region = regions[index];
        var type = region.GetType();
        return new TestHillRegion(
            (Vector2)type.GetField("Center", PublicInstanceFields).GetValue(region),
            (float)type.GetField("PeakHeight", PublicInstanceFields).GetValue(region),
            (float)type.GetField("CrestRadius", PublicInstanceFields).GetValue(region),
            (float)type.GetField("OuterRadius", PublicInstanceFields).GetValue(region),
            (float)type.GetField("NoStructureRadius", PublicInstanceFields).GetValue(region),
            (float)type.GetField("SpacingRadius", PublicInstanceFields).GetValue(region),
            type.GetField("SizeClass", PublicInstanceFields).GetValue(region).ToString());
    }

    private static void SetProperty(object target, string propertyName, object value)
    {
        target.GetType().GetProperty(propertyName, PublicInstance).SetValue(target, value);
    }

    private static List<Vector2Int> InvokeFindPath(object layout, Vector2Int start, Vector2Int goal)
    {
        return (List<Vector2Int>)ArenaLayoutType.GetMethod("FindPath", PublicInstance).Invoke(layout, new object[] { start, goal });
    }

    private static bool InvokeTryGetDoorwayPoint(object layout, Vector2Int from, Vector2Int to, out Vector3 point)
    {
        var args = new object[] { from, to, Vector3.zero };
        var found = (bool)ArenaLayoutType.GetMethod("TryGetDoorwayPoint", PublicInstance).Invoke(layout, args);
        point = (Vector3)args[2];
        return found;
    }

    private static bool InvokeIsTunnelReservedPosition(object layout, Vector3 position)
    {
        return (bool)ArenaLayoutType.GetMethod("IsTunnelReservedPosition", PublicInstance).Invoke(layout, new object[] { position, 0f });
    }

    private static bool InvokeIsTunnelReservedPositionFromRoute(object tunnel, Vector3 position)
    {
        return (bool)ArenaTunnelRouteType.GetMethod("ContainsReservedPosition", PublicInstance).Invoke(tunnel, new object[] { position, 0f });
    }

    private static uint GetDefaultRenderingLayer()
    {
        var field = RenderSetupType.GetField("DefaultRenderingLayer", BindingFlags.Static | BindingFlags.Public);
        Assert.That(field, Is.Not.Null);
        return (uint)field.GetRawConstantValue();
    }

    private static uint GetFloorRenderingLayer()
    {
        var field = RenderSetupType.GetField("FloorRenderingLayer", BindingFlags.Static | BindingFlags.Public);
        Assert.That(field, Is.Not.Null);
        return (uint)field.GetRawConstantValue();
    }

    private static bool InvokeIsTerrainCutout(object terrainProfile, Vector3 position)
    {
        return (bool)AllOutWarTerrainProfileType.GetMethod("IsTerrainCutout", PublicInstance).Invoke(terrainProfile, new object[] { position });
    }

    private static float InvokeSampleTerrainHeight(object terrainProfile, Vector3 position)
    {
        return (float)AllOutWarTerrainProfileType.GetMethod("SampleHeight", PublicInstance).Invoke(terrainProfile, new object[] { position });
    }

    private static float InvokeSampleTerrainHeightBruteForceForTests(object terrainProfile, Vector3 position)
    {
        return (float)AllOutWarTerrainProfileType.GetMethod("SampleHeightBruteForceForTests", PublicInstance).Invoke(terrainProfile, new object[] { position });
    }

    private static void AssertTerrainSampleMatchesBruteForce(object terrainProfile, Vector3 position)
    {
        var indexed = InvokeSampleTerrainHeight(terrainProfile, position);
        var bruteForce = InvokeSampleTerrainHeightBruteForceForTests(terrainProfile, position);
        Assert.That(indexed, Is.EqualTo(bruteForce).Within(0.0005f), $"Height mismatch at {position}");
    }

    private static bool InvokeTryScoreHillCutRoute(
        object terrainProfile,
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
        var method = AllOutWarTerrainProfileType.GetMethod(
            "TryScoreTerrainOnlyHillCutRoute",
            PublicInstance,
            null,
            new[]
            {
                typeof(int),
                typeof(Vector3),
                typeof(Vector3),
                typeof(Vector3),
                typeof(Vector3),
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(float).MakeByRefType()
            },
            null);
        Assert.That(method, Is.Not.Null);
        var args = new object[] { hillIndex, fromPortal, toPortal, fromDirection, toDirection, tunnelWidth, tunnelHeight, portalRadius, 0f };
        var result = (bool)method.Invoke(terrainProfile, args);
        score = (float)args[8];
        return result;
    }

    private static bool InvokeTryScoreCompoundHillCutRoute(
        object terrainProfile,
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
        var method = AllOutWarTerrainProfileType.GetMethod(
            "TryScoreTerrainOnlyHillCutRoute",
            PublicInstance,
            null,
            new[]
            {
                typeof(int),
                typeof(int),
                typeof(Vector3),
                typeof(Vector3),
                typeof(Vector3),
                typeof(Vector3),
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(float).MakeByRefType()
            },
            null);
        Assert.That(method, Is.Not.Null);
        var args = new object[] { fromHillIndex, toHillIndex, fromPortal, toPortal, fromDirection, toDirection, tunnelWidth, tunnelHeight, portalRadius, 0f };
        var result = (bool)method.Invoke(terrainProfile, args);
        score = (float)args[9];
        return result;
    }

    private static bool InvokeTryBuildHillCutEndpoint(
        object terrainProfile,
        int hillIndex,
        Vector3 inwardDirection,
        float tunnelWidth,
        float tunnelHeight,
        float mouthLength,
        out Vector3 portal)
    {
        var method = AllOutWarTerrainProfileType.GetMethod("TryBuildTerrainOnlyHillCutEndpoint", PublicInstance);
        Assert.That(method, Is.Not.Null);
        var args = new object[] { hillIndex, inwardDirection, tunnelWidth, tunnelHeight, mouthLength, Vector3.zero, 0f };
        var result = (bool)method.Invoke(terrainProfile, args);
        portal = (Vector3)args[5];
        return result;
    }

    private static bool InvokeTryBuildHillMouthApron(
        object terrainProfile,
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
        var method = AllOutWarTerrainProfileType.GetMethod("TryBuildTerrainOnlyHillMouthApron", PublicInstance);
        Assert.That(method, Is.Not.Null);
        var args = new object[] { hillIndex, portal, inwardDirection, tunnelWidth, tunnelHeight, Vector3.zero, Vector3.zero, 0f, 0f };
        var result = (bool)method.Invoke(terrainProfile, args);
        outerApproach = (Vector3)args[5];
        innerMouth = (Vector3)args[6];
        outerHalfWidth = (float)args[7];
        innerHalfWidth = (float)args[8];
        return result;
    }

    private static bool InvokeIsTunnelRouteSafe(object layout, object tunnel, float tunnelWidth)
    {
        var method = ArenaGeneratorType.GetMethod("IsAllOutWarTunnelRouteSafe", PrivateStatic);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(null, new object[] { layout, tunnel, tunnelWidth });
    }

    private static IList InvokeBuildHillCutEndpointCandidates(object layout, object terrainProfile, float roomSize, float tunnelWidth, float tunnelHeight)
    {
        var method = ArenaGeneratorType.GetMethod("BuildAllOutWarHillCutEndpointCandidates", PrivateStatic);
        Assert.That(method, Is.Not.Null);
        return (IList)method.Invoke(null, new object[] { layout, terrainProfile, roomSize, tunnelWidth, tunnelHeight });
    }

    private static IList InvokeBuildSubfloorEndpointCandidates(object layout, object terrainProfile, float roomSize, float corridorWidth, float tunnelWidth, float rampLength)
    {
        var method = ArenaGeneratorType.GetMethod("BuildAllOutWarSubfloorEndpointCandidates", PrivateStatic);
        Assert.That(method, Is.Not.Null);
        return (IList)method.Invoke(null, new object[] { layout, terrainProfile, roomSize, corridorWidth, tunnelWidth, rampLength });
    }

    private static bool InvokeTryReserveTerrainOnlyHillCandidate(
        object terrainProfile,
        HashSet<Vector2Int> allowed,
        HashSet<Vector2Int> eligibleCells,
        HashSet<Vector2Int> protectedCells,
        Vector2Int seed,
        int totalArmies,
        int gridRadius,
        bool forceMinimumHill)
    {
        var method = AllOutWarTerrainProfileType.GetMethod("TryReserveTerrainOnlyHillCandidate", PrivateInstance);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(
            terrainProfile,
            new object[] { allowed, eligibleCells, protectedCells, seed, totalArmies, gridRadius, 0.34f, RoomSize, forceMinimumHill });
    }

    private static bool InvokeTrySelectSubfloorTunnelRoute(object layout, IList candidates, int minimumGraphDistance, float minimumEntranceAlignment, out object route)
    {
        var method = ArenaGeneratorType.GetMethod("TrySelectAllOutWarSubfloorTunnelRoute", PrivateStatic);
        Assert.That(method, Is.Not.Null);
        var args = new object[]
        {
            layout,
            new System.Random(12345),
            candidates,
            4.5f,
            4f,
            6f,
            6f,
            4.5f,
            minimumGraphDistance,
            minimumEntranceAlignment,
            null
        };
        var found = (bool)method.Invoke(null, args);
        route = args[10];
        return found;
    }

    private static object FindCandidateBySource(IList candidates, string source)
    {
        foreach (var candidate in candidates)
        {
            if (GetField<object>(candidate, "Source").ToString() == source)
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool InvokeContainsPoint(object region, Vector3 position, float extraPadding)
    {
        return (bool)region.GetType().GetMethod("ContainsPoint", PublicInstance).Invoke(region, new object[] { position, extraPadding });
    }

    private static Vector2Int GetVector2Int(object target, string propertyName)
    {
        return (Vector2Int)target.GetType().GetProperty(propertyName, PublicInstance).GetValue(target);
    }

    private static Vector3 GetVector3(object target, string propertyName)
    {
        return (Vector3)target.GetType().GetProperty(propertyName, PublicInstance).GetValue(target);
    }

    private static float GetFloat(object target, string propertyName)
    {
        return (float)target.GetType().GetProperty(propertyName, PublicInstance).GetValue(target);
    }

    private static int GetInt(object target, string propertyName)
    {
        return (int)target.GetType().GetProperty(propertyName, PublicInstance).GetValue(target);
    }

    private static string GetEnumName(object target, string propertyName)
    {
        return target.GetType().GetProperty(propertyName, PublicInstance).GetValue(target).ToString();
    }

    private static T GetField<T>(object target, string fieldName)
    {
        return (T)target.GetType().GetField(fieldName, PublicInstanceFields).GetValue(target);
    }

    private static IReadOnlyList<Vector3> GetWaypoints(object tunnel)
    {
        return (IReadOnlyList<Vector3>)ArenaTunnelRouteType.GetProperty("Waypoints", PublicInstance).GetValue(tunnel);
    }

    private static bool IsCardinal(Vector2Int direction)
    {
        foreach (var cardinal in CardinalDirections)
        {
            if (direction == cardinal)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3 ToWorldDirection(Vector2Int direction)
    {
        return new Vector3(direction.x, 0f, direction.y).normalized;
    }

    private static Vector3 Flatten(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
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
        if (ab.sqrMagnitude <= 0.001f)
        {
            return Vector2.Distance(p, a);
        }

        var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    private readonly struct TestHillRegion
    {
        public readonly Vector2 Center;
        public readonly float PeakHeight;
        public readonly float CrestRadius;
        public readonly float OuterRadius;
        public readonly float NoStructureRadius;
        public readonly float SpacingRadius;
        public readonly string SizeClass;

        public TestHillRegion(Vector2 center, float peakHeight, float crestRadius, float outerRadius, float noStructureRadius, float spacingRadius, string sizeClass)
        {
            Center = center;
            PeakHeight = peakHeight;
            CrestRadius = crestRadius;
            OuterRadius = outerRadius;
            NoStructureRadius = noStructureRadius;
            SpacingRadius = spacingRadius;
            SizeClass = sizeClass;
        }
    }
}
