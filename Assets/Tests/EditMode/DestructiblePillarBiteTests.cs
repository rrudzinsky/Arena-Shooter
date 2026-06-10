using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class DestructiblePillarBiteTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags AllInstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly System.Type PieceType = System.Type.GetType("ArenaShooter.DestructibleArenaPiece, Assembly-CSharp");
    private static readonly System.Type ChunkType = PieceType.GetNestedType("Chunk", BindingFlags.NonPublic);
    private static readonly System.Type ProfileType = System.Type.GetType("ArenaShooter.DestructibleDamageProfile, Assembly-CSharp");
    private static readonly System.Type OutlineCategoryType = System.Type.GetType("ArenaShooter.StylizedOutlineCategory, Assembly-CSharp");

    private GameObject testObject;
    private Component piece;

    [SetUp]
    public void SetUp()
    {
        testObject = new GameObject("Destructible pillar bite test");
        piece = testObject.AddComponent(PieceType);
        ConfigurePillar(new Vector3(0.6f, 4f, 0.9f));
    }

    [TearDown]
    public void TearDown()
    {
        if (testObject != null)
        {
            Object.DestroyImmediate(testObject);
        }

        DestroyTransientObjects();
    }

    [Test]
    public void BiteOnNearestCornerCreatesSetbackWithoutDestroyingChunk()
    {
        InvokeTakeDamage(10f, new Vector3(0.29f, 1f, 0.44f), Vector3.right);

        var chunk = FindChunkWithBite();
        Assert.That(chunk, Is.Not.Null);
        var setbacks = GetSetbacks(chunk);
        Assert.That(setbacks[0], Is.GreaterThan(0f));
        Assert.That(setbacks[1], Is.Zero);
        Assert.That(setbacks[2], Is.Zero);
        Assert.That(setbacks[3], Is.Zero);
        Assert.That(GetChunkBool(chunk, "Destroyed"), Is.False);
        Assert.That(GetChunkBool(chunk, "HasCornerBite"), Is.True);
    }

    [Test]
    public void BiteSpawnsDebrisSprayFromTheDiagonalCorner()
    {
        InvokeTakeDamage(10f, new Vector3(0.29f, 1f, 0.44f), Vector3.right);

        var burst = GameObject.Find("Wall Material Spray Burst");
        Assert.That(burst, Is.Not.Null);
        var diagonal = new Vector3(1f, 0f, 1f).normalized;
        Assert.That(Vector3.Dot(burst.transform.position - testObject.transform.position, diagonal), Is.GreaterThan(0.2f));
        Assert.That(CountObjectsNamed("Wall Material Spray Chip"), Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public void BittenCrossSectionHasSixPointsAndStaysConvex()
    {
        InvokeTakeDamage(10f, new Vector3(0.29f, 1f, 0.44f), Vector3.right);

        var chunk = FindChunkWithBite();
        var points = InvokeBuildCrossSection(chunk);
        Assert.That(points.Count, Is.EqualTo(6));
        Assert.That(PolygonIsConvex(points), Is.True);
    }

    [Test]
    public void BiteRebuildsBodyAndContourMeshes()
    {
        InvokeTakeDamage(10f, new Vector3(0.29f, 1f, 0.44f), Vector3.right);

        var body = FindChild("Combined Destructible Wall Body");
        var contour = FindChild("Destructible Damage Contours");
        Assert.That(body.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(0));
        Assert.That(contour.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
        Assert.That(contour.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(0));
    }

    [Test]
    public void RepeatedBitesSeverSlabAndConsumeColumnAbove()
    {
        for (var i = 0; i < 12; i++)
        {
            var sign = SignsForIteration(i);
            InvokeTakeDamage(10f, new Vector3(0.29f * sign.x, 1f, 0.44f * sign.y), Vector3.right);
        }

        var severed = FindChunkAtHeight(1f);
        Assert.That(GetChunkBool(severed, "Destroyed"), Is.True);
        var aboveDestroyedCount = 0;
        foreach (var chunk in GetChunks())
        {
            var index = (Vector3Int)ChunkType.GetField("Index", AllInstanceFields).GetValue(chunk);
            var destroyed = GetChunkBool(chunk, "Destroyed");
            if (index.y > GetChunkIndex(severed).y)
            {
                Assert.That(destroyed, Is.True);
                aboveDestroyedCount++;
            }

            if (index.y < GetChunkIndex(severed).y)
            {
                Assert.That(destroyed, Is.False);
            }
        }

        Assert.That(aboveDestroyedCount, Is.GreaterThan(0));
        Assert.That(GameObject.Find("Falling Wall Slab"), Is.Not.Null);
    }

    [Test]
    public void FallingPillarSegmentHasAnimationAndNoPhysics()
    {
        for (var i = 0; i < 12; i++)
        {
            var sign = SignsForIteration(i);
            InvokeTakeDamage(10f, new Vector3(0.29f * sign.x, 1f, 0.44f * sign.y), Vector3.right);
        }

        var slab = GameObject.Find("Falling Wall Slab");
        Assert.That(slab, Is.Not.Null);
        Assert.That(slab.GetComponent<Rigidbody>(), Is.Null);
        Assert.That(slab.GetComponent<Collider>(), Is.Null);
        Assert.That(FindComponentByTypeName(slab, "FallingWallSlabAnimation"), Is.Not.Null);
    }

    private static Vector2 SignsForIteration(int i)
    {
        switch (i % 4)
        {
            case 0:
                return new Vector2(1f, 1f);
            case 1:
                return new Vector2(-1f, -1f);
            case 2:
                return new Vector2(1f, -1f);
            default:
                return new Vector2(-1f, 1f);
        }
    }

    private void ConfigurePillar(Vector3 size)
    {
        var configure = PieceType.GetMethod(
            "Configure",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(float), typeof(Vector3), typeof(Material), OutlineCategoryType, ProfileType, typeof(Vector3) },
            null);
        Assert.That(configure, Is.Not.Null);
        configure.Invoke(piece, new object[]
        {
            300f,
            size,
            null,
            System.Enum.Parse(OutlineCategoryType, "Wall"),
            System.Enum.Parse(ProfileType, "CornerPillar"),
            Vector3.right
        });
    }

    private void InvokeTakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        PieceType.GetMethod(
            "TakeDamage",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(float), typeof(Vector3), typeof(Vector3) },
            null).Invoke(piece, new object[] { amount, hitPoint, hitNormal });
    }

    private IList GetChunks()
    {
        return (IList)PieceType.GetField("chunks", PrivateInstance).GetValue(piece);
    }

    private object FindChunkWithBite()
    {
        foreach (var chunk in GetChunks())
        {
            if (GetChunkBool(chunk, "HasCornerBite"))
            {
                return chunk;
            }
        }

        return null;
    }

    private object FindChunkAtHeight(float y)
    {
        object best = null;
        var bestDistance = float.PositiveInfinity;
        foreach (var chunk in GetChunks())
        {
            var position = (Vector3)ChunkType.GetField("LocalPosition", AllInstanceFields).GetValue(chunk);
            var distance = Mathf.Abs(position.y - y);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = chunk;
            }
        }

        return best;
    }

    private Vector3Int GetChunkIndex(object chunk)
    {
        return (Vector3Int)ChunkType.GetField("Index", AllInstanceFields).GetValue(chunk);
    }

    private float[] GetSetbacks(object chunk)
    {
        return (float[])ChunkType.GetField("CornerBiteSetbacks", AllInstanceFields).GetValue(chunk);
    }

    private bool GetChunkBool(object chunk, string fieldName)
    {
        return (bool)ChunkType.GetField(fieldName, AllInstanceFields).GetValue(chunk);
    }

    private List<Vector2> InvokeBuildCrossSection(object chunk)
    {
        var method = PieceType.GetMethod("BuildBittenPillarCrossSection", PrivateInstance);
        return (List<Vector2>)method.Invoke(piece, new object[] { chunk, 0f, null });
    }

    private GameObject FindChild(string name)
    {
        foreach (var transform in testObject.GetComponentsInChildren<Transform>(true))
        {
            if (transform.gameObject.name == name)
            {
                return transform.gameObject;
            }
        }

        return null;
    }

    private static bool PolygonIsConvex(List<Vector2> points)
    {
        var sign = 0f;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            var c = points[(i + 2) % points.Count];
            var cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
            if (Mathf.Abs(cross) <= 0.000001f)
            {
                continue;
            }

            if (sign == 0f)
            {
                sign = Mathf.Sign(cross);
            }
            else if (Mathf.Sign(cross) != sign)
            {
                return false;
            }
        }

        return true;
    }

    private static Component FindComponentByTypeName(GameObject target, string typeName)
    {
        foreach (var component in target.GetComponents<Component>())
        {
            if (component.GetType().Name == typeName)
            {
                return component;
            }
        }

        return null;
    }

    private static int CountObjectsNamed(string name)
    {
        var count = 0;
        foreach (var candidate in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (candidate.name == name)
            {
                count++;
            }
        }

        return count;
    }

    private static void DestroyTransientObjects()
    {
        var doomed = new List<GameObject>();
        foreach (var candidate in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (candidate != null &&
                (candidate.name == "Wall Material Spray Burst" ||
                 candidate.name == "Wall Material Spray Chip" ||
                 candidate.name == "Falling Wall Slab"))
            {
                doomed.Add(candidate);
            }
        }

        foreach (var target in doomed)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
