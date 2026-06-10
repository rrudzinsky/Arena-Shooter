using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class DestructibleArenaPieceCantileverTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags AllInstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly System.Type PieceType = System.Type.GetType("ArenaShooter.DestructibleArenaPiece, Assembly-CSharp");
    private static readonly System.Type DamageStampType = PieceType.GetNestedType("DamageStamp", BindingFlags.NonPublic);

    private GameObject testObject;
    private Component piece;

    [SetUp]
    public void SetUp()
    {
        testObject = new GameObject("Destructible cantilever test");
        piece = testObject.AddComponent(PieceType);
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));
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
    public void ThinLintelAboveWideHoleFalls()
    {
        AddRectStamp(-4.5f, 4.5f, 0.8f, 2.2f);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(IsPointInsideWallDamageUnion(new Vector2(0f, 2.6f)), Is.True);
        Assert.That(CountHiddenCleanupStamps(), Is.GreaterThanOrEqualTo(1));
        Assert.That(GameObject.Find("Falling Wall Slab"), Is.Not.Null);
    }

    [Test]
    public void ThickLintelAboveNarrowHoleStays()
    {
        AddRectStamp(-0.8f, 0.8f, 0.8f, 1.6f);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(IsPointInsideWallDamageUnion(new Vector2(0f, 2.2f)), Is.False);
        Assert.That(CountHiddenCleanupStamps(), Is.Zero);
        Assert.That(GameObject.Find("Falling Wall Slab"), Is.Null);
    }

    [Test]
    public void LongThinSideSpikeFalls()
    {
        AddRectStamp(-5f, 5f, 0.4f, 3f);
        AddRectStamp(-5f, 5f, -3f, -0.4f);
        AddRectStamp(-2f, 5f, -0.45f, 0.45f);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(IsPointInsideWallDamageUnion(new Vector2(-3.5f, 0f)), Is.True);
        Assert.That(CountHiddenCleanupStamps(), Is.GreaterThanOrEqualTo(1));
        Assert.That(GameObject.Find("Falling Wall Slab"), Is.Not.Null);
    }

    [Test]
    public void FullHeightColumnFromFloorStays()
    {
        AddRectStamp(-5f, -0.5f, -3f, 3f);
        AddRectStamp(0.5f, 5f, -3f, 3f);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(IsPointInsideWallDamageUnion(new Vector2(0f, 0f)), Is.False);
        Assert.That(CountHiddenCleanupStamps(), Is.Zero);
        Assert.That(GameObject.Find("Falling Wall Slab"), Is.Null);
    }

    [Test]
    public void FragmentAttachedOnlyToTopEdgeFalls()
    {
        AddRectStamp(-5f, 5f, -3f, 2f);
        AddRectStamp(-5f, -1f, 2f, 3f);
        AddRectStamp(1f, 5f, 2f, 3f);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(IsPointInsideWallDamageUnion(new Vector2(0f, 2.5f)), Is.True);
        Assert.That(CountHiddenCleanupStamps(), Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void FallingSlabHasAnimationAndNoPhysics()
    {
        AddRectStamp(-4.5f, 4.5f, 0.8f, 2.2f);

        InvokeRemoveUnsupportedWallIslands();

        var slab = GameObject.Find("Falling Wall Slab");
        Assert.That(slab, Is.Not.Null);
        Assert.That(slab.GetComponent<Rigidbody>(), Is.Null);
        Assert.That(slab.GetComponent<Collider>(), Is.Null);
        Assert.That(slab.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
        Assert.That(FindComponentByTypeName(slab, "FallingWallSlabAnimation"), Is.Not.Null);
    }

    private void AddRectStamp(float minU, float maxU, float minV, float maxV)
    {
        var points = new[]
        {
            new Vector2(minU, minV),
            new Vector2(maxU, minV),
            new Vector2(maxU, maxV),
            new Vector2(minU, maxV)
        };
        var stamp = System.Activator.CreateInstance(DamageStampType, nonPublic: true);
        SetStampField(stamp, "Normal", Vector3.forward);
        SetStampField(stamp, "U", Vector3.right);
        SetStampField(stamp, "V", Vector3.up);
        SetStampField(stamp, "Plane", 1f);
        SetStampField(stamp, "Min", new Vector2(minU, minV));
        SetStampField(stamp, "Max", new Vector2(maxU, maxV));
        SetStampField(stamp, "Points", points);
        SetStampField(stamp, "RenderClosed", true);
        SetStampField(stamp, "RenderContour", true);
        GetStampList().Add(stamp);
    }

    private static void SetStampField(object stamp, string name, object value)
    {
        DamageStampType.GetField(name, AllInstanceFields).SetValue(stamp, value);
    }

    private IList GetStampList()
    {
        return (IList)PieceType.GetField("wallDamageStamps", PrivateInstance).GetValue(piece);
    }

    private void SetConfiguredSize(Vector3 size)
    {
        PieceType.GetField("configuredSize", PrivateInstance).SetValue(piece, size);
    }

    private void InvokeRemoveUnsupportedWallIslands()
    {
        PieceType.GetMethod(
            "RemoveUnsupportedContourOwnedWallIslands",
            PrivateInstance,
            null,
            System.Type.EmptyTypes,
            null).Invoke(piece, null);
    }

    private bool IsPointInsideWallDamageUnion(Vector2 point)
    {
        var method = PieceType.GetMethod("IsPointInsideWallDamageUnion", PrivateInstance);
        return (bool)method.Invoke(piece, new object[] { point });
    }

    private int CountHiddenCleanupStamps()
    {
        var count = 0;
        foreach (var stamp in GetStampList())
        {
            if (!(bool)DamageStampType.GetField("RenderContour", AllInstanceFields).GetValue(stamp))
            {
                count++;
            }
        }

        return count;
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

    private static void DestroyTransientObjects()
    {
        var doomed = new System.Collections.Generic.List<GameObject>();
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
