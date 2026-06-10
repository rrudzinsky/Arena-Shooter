using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class DestructiblePillarRouterTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly System.Type PieceType = System.Type.GetType("ArenaShooter.DestructibleArenaPiece, Assembly-CSharp");
    private static readonly System.Type ProfileType = System.Type.GetType("ArenaShooter.DestructibleDamageProfile, Assembly-CSharp");
    private static readonly System.Type OutlineCategoryType = System.Type.GetType("ArenaShooter.StylizedOutlineCategory, Assembly-CSharp");

    private GameObject testObject;
    private Component piece;

    [SetUp]
    public void SetUp()
    {
        testObject = new GameObject("Destructible pillar router test");
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
    public void PillarCreatesTwoAxisWallSlabs()
    {
        var slabX = FindChild("Pillar Axis X Damage");
        var slabZ = FindChild("Pillar Axis Z Damage");
        Assert.That(slabX, Is.Not.Null);
        Assert.That(slabZ, Is.Not.Null);
        Assert.That(slabX.GetComponent(PieceType), Is.Not.Null);
        Assert.That(slabZ.GetComponent(PieceType), Is.Not.Null);
        Assert.That(CountChildren("Combined Destructible Wall Body"), Is.EqualTo(2));
        Assert.That(CountChildren("Destructible Wall Outline Source"), Is.EqualTo(2));
        Assert.That(CountChildren("Destructible Damage Contours"), Is.EqualTo(2));
    }

    [Test]
    public void OnlyParentColliderStaysEnabled()
    {
        Assert.That(testObject.GetComponent<BoxCollider>().enabled, Is.True);
        Assert.That(FindChild("Pillar Axis X Damage").GetComponent<BoxCollider>().enabled, Is.False);
        Assert.That(FindChild("Pillar Axis Z Damage").GetComponent<BoxCollider>().enabled, Is.False);
    }

    [Test]
    public void DamageRoutesToFacingAxisSlab()
    {
        InvokeTakeDamage(40f, new Vector3(0.3f, 1f, 0f), Vector3.right);

        Assert.That(GetStampCount("Pillar Axis X Damage"), Is.GreaterThanOrEqualTo(1));
        Assert.That(GetStampCount("Pillar Axis Z Damage"), Is.Zero);

        InvokeTakeDamage(40f, new Vector3(0f, 1.4f, 0.45f), Vector3.forward);

        Assert.That(GetStampCount("Pillar Axis Z Damage"), Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void ThroughHoleAllowsProjectilePassThrough()
    {
        InvokeTakeDamage(40f, new Vector3(0.3f, 1f, 0f), Vector3.right);

        var passThrough = (bool)PieceType.GetMethod(
            "AllowsProjectilePassThrough",
            BindingFlags.Instance | BindingFlags.Public).Invoke(piece, new object[] { new Vector3(0.3f, 1f, 0f), Vector3.right });
        Assert.That(passThrough, Is.True);
    }

    [Test]
    public void SeveringOneAxisMirrorsBandToSiblingAxis()
    {
        InvokeTakeDamage(40f, new Vector3(0.3f, 1f, -0.3f), Vector3.right);
        InvokeTakeDamage(40f, new Vector3(0.3f, 1f, -0.1f), Vector3.right);
        InvokeTakeDamage(40f, new Vector3(0.3f, 1f, 0.1f), Vector3.right);
        InvokeTakeDamage(40f, new Vector3(0.3f, 1f, 0.3f), Vector3.right);

        Assert.That(GameObject.Find("Falling Wall Slab"), Is.Not.Null);
        Assert.That(IsPointInsideUnion("Pillar Axis Z Damage", new Vector2(0f, 1.8f)), Is.True);
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

    private int CountChildren(string name)
    {
        var count = 0;
        foreach (var transform in testObject.GetComponentsInChildren<Transform>(true))
        {
            if (transform.gameObject.name == name)
            {
                count++;
            }
        }

        return count;
    }

    private int GetStampCount(string childName)
    {
        var child = FindChild(childName).GetComponent(PieceType);
        return ((IList)PieceType.GetField("wallDamageStamps", PrivateInstance).GetValue(child)).Count;
    }

    private bool IsPointInsideUnion(string childName, Vector2 point)
    {
        var child = FindChild(childName).GetComponent(PieceType);
        return (bool)PieceType.GetMethod("IsPointInsideWallDamageUnion", PrivateInstance).Invoke(child, new object[] { point });
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
