using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Throwaway diagnostic: logs terrain-only hill placement statistics for a batch of
// hilly All Out War seeds so dispersion changes can be verified without booting a match.
public static class HillSpreadDebugWindow
{
    private const float RoomSize = 10f;
    private const float CorridorLength = 6f;
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

    [MenuItem("Tools/Arena Shooter/Log Hill Spread Stats")]
    public static void LogHillSpreadStats()
    {
        var generatorType = System.Type.GetType("ArenaShooter.ArenaGenerator, Assembly-CSharp");
        var profileType = System.Type.GetType("ArenaShooter.ArenaGenerator+AllOutWarTerrainProfile, Assembly-CSharp");
        var styleType = System.Type.GetType("ArenaShooter.AllOutWarMapStyle, Assembly-CSharp");
        var regionType = profileType.GetNestedType("TerrainOnlyHillRegion", BindingFlags.NonPublic);

        var spacing = RoomSize + CorridorLength;
        var report = new System.Text.StringBuilder();
        foreach (var config in new[] { (3, 2), (4, 4) })
        {
        var (gridRadius, configuredArmies) = config;
        // Mirror GenerateAllOutWar exactly: the room graph always reserves at least
        // three spawn sectors, and small hilly maps use the compact spawn buffer.
        var totalArmies = Mathf.Max(3, configuredArmies);
        var mapRadius = gridRadius * spacing;

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

        var protectedMethod = gridRadius <= 4 ? "BuildCompactArmySpawnProtectedRooms" : "BuildArmySpawnProtectedRooms";
        var spawnProtected = (HashSet<Vector2Int>)generatorType
            .GetMethod(protectedMethod, PrivateStatic)
            .Invoke(null, new object[] { allowed, totalArmies, gridRadius });

        report.AppendLine($"[HillSpread] grid {gridRadius}, {totalArmies} armies ({configuredArmies} configured), map radius {mapRadius:F0}");
        foreach (var seed in new[] { 11, 222, 3333, 44444, 20000, 20005, 24680, 97531, 1234, 55555, 314159, 271828, 7777, 421337, 90210, 600613 })
        {
            var profile = System.Activator.CreateInstance(
                profileType,
                PublicInstance | BindingFlags.NonPublic | BindingFlags.CreateInstance,
                null,
                new object[] { seed, spacing, System.Enum.Parse(styleType, "Hilly") },
                null);
            profileType.GetMethod("BuildTerrainOnlyHillReservations", PublicInstance).Invoke(
                profile,
                new object[] { allowed, spawnProtected, totalArmies, gridRadius, spacing, RoomSize });

            var count = (int)profileType.GetProperty("TerrainOnlyHillRegionCount", PublicInstance).GetValue(profile);
            var readRegion = profileType.GetMethod("GetTerrainOnlyHillRegionForTests", PublicInstance);
            var centers = new List<Vector2>();
            var radii = new List<float>();
            var sizeTags = new List<string>();
            var tunnelCapable = 0;
            // Mirrors the generator: tunnelHeight = clamp(clamp(cw*1.28,4.2,5.8)*0.86, 3.55, 4.85),
            // hill-cut needs peak >= tunnelHeight + 0.85 roof clearance.
            const float corridorWidth = 4f;
            var tunnelWidth = Mathf.Clamp(corridorWidth * 1.28f, 4.2f, 5.8f);
            var requiredPeak = Mathf.Clamp(tunnelWidth * 0.86f, 3.55f, 4.85f) + 0.85f;
            if (readRegion == null)
            {
                var regionsField = profileType.GetField("terrainOnlyHillRegions", BindingFlags.NonPublic | BindingFlags.Instance);
                var regions = (System.Collections.IList)regionsField.GetValue(profile);
                foreach (var region in regions)
                {
                    centers.Add((Vector2)regionType.GetField("Center", PublicInstance).GetValue(region));
                    radii.Add((float)regionType.GetField("OuterRadius", PublicInstance).GetValue(region));
                    sizeTags.Add(regionType.GetField("SizeClass", PublicInstance).GetValue(region).ToString().Substring(0, 1));
                    if ((float)regionType.GetField("PeakHeight", PublicInstance).GetValue(region) >= requiredPeak)
                    {
                        tunnelCapable++;
                    }
                }
            }

            var noBuildCheck = profileType.GetMethod("IsTerrainOnlyHillNoBuildCell", PublicInstance);
            var line = new System.Text.StringBuilder();
            line.Append($"[HillSpread] seed {seed}: {count} hills [{string.Join("", sizeTags)}] tunnelable={tunnelCapable} |");
            for (var i = 0; i < centers.Count; i++)
            {
                var minGap = float.MaxValue;
                foreach (var cell in allowed)
                {
                    // Spawn-protected cells are force-built as buffer rooms even inside
                    // no-build rings, so they always count toward the wall gap.
                    if (!spawnProtected.Contains(cell) && (bool)noBuildCheck.Invoke(profile, new object[] { cell }))
                    {
                        continue;
                    }

                    var cellCenter = new Vector2(cell.x, cell.y) * spacing;
                    var wallDistance = Vector2.Distance(cellCenter, centers[i]) - RoomSize * 0.5f;
                    minGap = Mathf.Min(minGap, wallDistance - radii[i]);
                }

                line.Append($" gap={minGap:F1}");
            }

            line.Append(" |");
            for (var i = 0; i < centers.Count; i++)
            {
                var nearest = float.MaxValue;
                for (var j = 0; j < centers.Count; j++)
                {
                    if (j != i)
                    {
                        nearest = Mathf.Min(nearest, Vector2.Distance(centers[i], centers[j]));
                    }
                }

                var overlap = false;
                for (var j = 0; j < centers.Count; j++)
                {
                    if (j != i && Vector2.Distance(centers[i], centers[j]) < radii[i] + radii[j])
                    {
                        overlap = true;
                    }
                }

                line.Append($" r={centers[i].magnitude / mapRadius:F2} nn={nearest:F0}{(overlap ? "*" : "")}");
            }

            report.AppendLine(line.ToString());
        }
        }

        Debug.Log(report.ToString());
    }
}
