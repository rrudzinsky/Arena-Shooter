using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Pure diagnostics for the user's real play configuration: All Out War, Hilly map,
/// one opponent army (2 total), 10 soldiers, battlefield cap 10 => grid radius 3,
/// 29 target rooms. Logs rooms/tunnels/hills/spawn-buffer/scenery stats per seed so
/// regressions in the smallest skirmish map are visible at a glance.
/// </summary>
public sealed class AllOutWarSmallSkirmishDiagnosticsTests
{
    private const float RoomSize = 10f;
    private const float CorridorLength = 6f;
    private const float CorridorWidth = 4f;
    private const float WallHeight = 4f;
    private const int TotalArmies = 2;
    private const int GridRadius = 3;
    private const int TargetRooms = 29;
    private const string HillyMapStyle = "Hilly";

    private static readonly BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;
    private static readonly BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly System.Type ArenaGeneratorType = System.Type.GetType("ArenaShooter.ArenaGenerator, Assembly-CSharp");
    private static readonly System.Type ArenaThemeType = System.Type.GetType("ArenaShooter.ArenaTheme, Assembly-CSharp");
    private static readonly System.Type ArenaLayoutType = System.Type.GetType("ArenaShooter.ArenaLayout, Assembly-CSharp");
    private static readonly System.Type AllOutWarMapStyleType = System.Type.GetType("ArenaShooter.AllOutWarMapStyle, Assembly-CSharp");
    private static readonly System.Type AllOutWarTerrainProfileType = System.Type.GetType("ArenaShooter.ArenaGenerator+AllOutWarTerrainProfile, Assembly-CSharp");

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
    public void LogSmallestHillySkirmishStats()
    {
        var seeds = new[] { 11111, 22222, 33333, 44444, 55555, 66666, 77777, 88888 };
        foreach (var seed in seeds)
        {
            var report = new StringBuilder();
            report.AppendLine($"[Skirmish Diagnostics] seed={seed} armies={TotalArmies} grid={GridRadius} targetRooms={TargetRooms} style={HillyMapStyle}");

            var root = new GameObject($"Skirmish Diagnostics {seed}");
            generatedRoots.Add(root);
            var generator = root.AddComponent(ArenaGeneratorType);
            var layout = ArenaGeneratorType.GetMethod("GenerateAllOutWar", PublicInstance).Invoke(
                generator,
                new object[]
                {
                    System.Activator.CreateInstance(ArenaThemeType),
                    root.transform,
                    seed,
                    TotalArmies,
                    TargetRooms,
                    GridRadius,
                    RoomSize,
                    CorridorLength,
                    CorridorWidth,
                    WallHeight,
                    6,
                    HillyMapStyle
                });

            var rooms = (HashSet<Vector2Int>)ArenaLayoutType.GetField("Rooms", PublicInstance).GetValue(layout);
            var tunnels = (System.Collections.IList)ArenaLayoutType.GetField("TunnelRoutes", PublicInstance).GetValue(layout);
            report.AppendLine($"  rooms={rooms.Count} tunnels={tunnels.Count}");
            foreach (var tunnel in tunnels)
            {
                var kind = tunnel.GetType().GetProperty("Kind", PublicInstance).GetValue(tunnel);
                var fromRoom = tunnel.GetType().GetProperty("FromRoom", PublicInstance).GetValue(tunnel);
                var toRoom = tunnel.GetType().GetProperty("ToRoom", PublicInstance).GetValue(tunnel);
                report.AppendLine($"  tunnel kind={kind} from={fromRoom} to={toRoom}");
            }

            // Mirror of the generator's reservation inputs (BuildCircularRoomGraph):
            // hilly small grids use the compact 4-room cross per spawn sector and the
            // sector count is Max(3, armies).
            var sectorArmies = Mathf.Max(3, TotalArmies);
            var allowed = BuildAllowedCells(GridRadius);
            var spacing = RoomSize + CorridorLength;
            var terrainProfile = System.Activator.CreateInstance(
                AllOutWarTerrainProfileType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[] { seed, spacing, System.Enum.Parse(AllOutWarMapStyleType, "Hilly") },
                null);
            var compactBuffers = ArenaGeneratorType.GetMethod("BuildCompactArmySpawnProtectedRooms", PrivateStatic);
            var protectedRooms = (HashSet<Vector2Int>)compactBuffers.Invoke(null, new object[] { allowed, sectorArmies, GridRadius });
            AllOutWarTerrainProfileType.GetMethod("BuildTerrainOnlyHillReservations", PublicInstance).Invoke(
                terrainProfile,
                new object[] { allowed, protectedRooms, sectorArmies, GridRadius, spacing, RoomSize });
            var hillCount = (int)AllOutWarTerrainProfileType.GetProperty("TerrainOnlyHillRegionCount", PublicInstance).GetValue(terrainProfile);
            var noBuild = 0;
            foreach (var cell in allowed)
            {
                if ((bool)AllOutWarTerrainProfileType.GetMethod("IsTerrainOnlyHillNoBuildCell", PublicInstance).Invoke(terrainProfile, new object[] { cell }))
                {
                    noBuild++;
                }
            }

            report.AppendLine($"  hills={hillCount} allowedCells={allowed.Count} noBuildCells={noBuild} protectedCells={protectedRooms.Count}");

            // Per-sector spawn buffer membership: which buffer cells became real rooms.
            var frontTarget = ArenaGeneratorType.GetMethod("GetArmySpawnFrontTarget", PrivateStatic);
            var nearest = ArenaGeneratorType.GetMethod(
                "FindNearestAllowedCell",
                PrivateStatic,
                null,
                new[] { typeof(HashSet<Vector2Int>), typeof(Vector2) },
                null);
            var regions = (System.Collections.IList)ArenaLayoutType.GetField("ArmySpawnRegions", PublicInstance).GetValue(layout);
            for (var team = 0; team < sectorArmies; team++)
            {
                var target = (Vector2)frontTarget.Invoke(null, new object[] { team, sectorArmies, GridRadius });
                var front = (Vector2Int)nearest.Invoke(null, new object[] { allowed, target });
                var neighborRooms = new List<string>();
                for (var x = -1; x <= 1; x++)
                {
                    for (var y = -1; y <= 1; y++)
                    {
                        var cell = front + new Vector2Int(x, y);
                        if (rooms.Contains(cell))
                        {
                            neighborRooms.Add(cell.ToString());
                        }
                    }
                }

                string regionInfo = "(no region)";
                foreach (var region in regions)
                {
                    var teamId = (int)region.GetType().GetProperty("TeamId", PublicInstance).GetValue(region);
                    if (teamId == team)
                    {
                        var regionRoom = (Vector2Int)region.GetType().GetProperty("Room", PublicInstance).GetValue(region);
                        var center = (Vector3)region.GetType().GetProperty("Center", PublicInstance).GetValue(region);
                        regionInfo = $"regionRoom={regionRoom} spawn=({center.x:F1},{center.z:F1})";
                    }
                }

                report.AppendLine($"  team={team} front={front} {regionInfo} bufferRoomsNearFront={neighborRooms.Count} [{string.Join(" ", neighborRooms)}]");
            }

            // Scenery inventory under the generated root.
            var sceneryRoot = root.transform.Find("Hill Scenery");
            if (sceneryRoot == null)
            {
                report.AppendLine("  scenery=MISSING (no Hill Scenery root)");
            }
            else
            {
                var byKind = new Dictionary<string, int>();
                foreach (Transform prop in sceneryRoot)
                {
                    var key = prop.name.Split(' ')[0];
                    byKind[key] = byKind.TryGetValue(key, out var current) ? current + 1 : 1;
                }

                var summary = new List<string>();
                foreach (var pair in byKind)
                {
                    summary.Add($"{pair.Key}={pair.Value}");
                }

                report.AppendLine($"  sceneryTotal={sceneryRoot.childCount} [{string.Join(" ", summary)}]");
            }

            Debug.Log(report.ToString());
        }
    }

    private static HashSet<Vector2Int> BuildAllowedCells(int gridRadius)
    {
        // Mirrors BuildCircularRoomGraph's allowed-cell construction, including
        // insertion order so FindNearestAllowedCell tie-breaks match the generator.
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
}
