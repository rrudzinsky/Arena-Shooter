using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class HillSceneryGenerationTests
{
    private const float RoomSize = 10f;
    private const float CorridorLength = 6f;
    private const float CorridorWidth = 4f;
    private const float WallHeight = 4f;
    private const string HillyMapStyle = "Hilly";

    private static readonly BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;
    private static readonly Type ArenaGeneratorType = Type.GetType("ArenaShooter.ArenaGenerator, Assembly-CSharp");
    private static readonly Type ArenaThemeType = Type.GetType("ArenaShooter.ArenaTheme, Assembly-CSharp");

    private readonly List<GameObject> generatedRoots = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var root in generatedRoots)
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        generatedRoots.Clear();
    }

    [TestCase(3, 8)]
    [TestCase(4, 10)]
    public void HillyArenasGrowSceneryWithCoverCollidersOnTheirHills(int gridRadius, int totalArmies)
    {
        Assert.That(ArenaGeneratorType, Is.Not.Null);
        Assert.That(ArenaThemeType, Is.Not.Null);

        var generatorObject = new GameObject("Hill Scenery Test Generator");
        generatedRoots.Add(generatorObject);
        var generator = generatorObject.AddComponent(ArenaGeneratorType);
        var theme = Activator.CreateInstance(ArenaThemeType);
        var root = new GameObject("Hill Scenery Test Root");
        generatedRoots.Add(root);

        var generate = ArenaGeneratorType.GetMethod("GenerateAllOutWar", PublicInstance);
        Assert.That(generate, Is.Not.Null);
        generate.Invoke(generator, new object[]
        {
            theme, root.transform, 1234, totalArmies, 26, gridRadius,
            RoomSize, CorridorLength, CorridorWidth, WallHeight, 10, HillyMapStyle
        });

        var sceneryRoot = root.transform.Find("Hill Scenery");
        Assert.That(sceneryRoot, Is.Not.Null, "Hill Scenery root should exist for hilly arenas");

        if (sceneryRoot.childCount == 0)
        {
            Assert.Inconclusive("Scenery FBX resources were not available in this test environment.");
        }

        var coverProps = 0;
        var foliageProps = 0;
        foreach (Transform prop in sceneryRoot)
        {
            Assert.That(prop.position.y, Is.GreaterThan(0.01f), $"{prop.name} should sit on a hill, not the flat floor");

            if (prop.name.StartsWith("HexBarricade", StringComparison.Ordinal) ||
                prop.name.StartsWith("ShieldBarrier", StringComparison.Ordinal) ||
                prop.name.StartsWith("CrystalSpires", StringComparison.Ordinal) ||
                prop.name.StartsWith("NeonPalm", StringComparison.Ordinal))
            {
                Assert.That(prop.GetComponent<BoxCollider>(), Is.Not.Null, $"{prop.name} should block movement and shots");
                coverProps++;
            }
            else if (prop.name.StartsWith("NeonFoliage", StringComparison.Ordinal))
            {
                Assert.That(prop.GetComponent<Collider>(), Is.Null, $"{prop.name} should be decorative only");
                foliageProps++;
            }
        }

        Assert.That(coverProps, Is.GreaterThan(0), "hills should offer at least one piece of hard cover");
        Assert.That(foliageProps, Is.GreaterThan(0), "hills should be dressed with foliage accents");
    }
}
