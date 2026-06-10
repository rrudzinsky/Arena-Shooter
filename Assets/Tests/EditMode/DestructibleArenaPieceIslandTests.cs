using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class DestructibleArenaPieceIslandTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags AllInstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly System.Type PieceType = System.Type.GetType("ArenaShooter.DestructibleArenaPiece, Assembly-CSharp");
    private static readonly System.Type DamageStampType = PieceType.GetNestedType("DamageStamp", BindingFlags.NonPublic);
    private static readonly System.Type BoundsType = PieceType.GetNestedType("DamageComponentPlaneBounds", BindingFlags.NonPublic);
    private static readonly System.Type UnsupportedIslandType = PieceType.GetNestedType("UnsupportedWallIsland", BindingFlags.NonPublic);

    private GameObject testObject;
    private Component piece;

    [SetUp]
    public void SetUp()
    {
        testObject = new GameObject("Destructible island test");
        piece = testObject.AddComponent(PieceType);
    }

    [TearDown]
    public void TearDown()
    {
        if (testObject != null)
        {
            Object.DestroyImmediate(testObject);
        }

        DestroySprayObjects();
    }

    [Test]
    public void SingleDamageHoleDoesNotCreateUnsupportedIsland()
    {
        AddRectStamp(-1f, 1f, -1f, 1f);

        Assert.That(FindUnsupportedIslandCount(), Is.Zero);
    }

    [Test]
    public void OverlappingDamageRingFindsCentralUnsupportedIsland()
    {
        AddDamageRing(Vector2.zero);

        Assert.That(FindUnsupportedIslandCount(), Is.EqualTo(1));
    }

    [Test]
    public void MultipleDamageRingsAreAllDetectedInOneScan()
    {
        AddDamageRing(new Vector2(-2.8f, 0f));
        AddDamageRing(new Vector2(2.8f, 0f));

        Assert.That(FindUnsupportedIslandCount(), Is.EqualTo(2));
    }

    [Test]
    public void IslandLoopTouchingWallPerimeterRemainsSupported()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));
        AddRectStamp(-5f, -3.8f, -1.2f, 1.2f);
        AddRectStamp(-5f, -1.8f, 1f, 2.2f);
        AddRectStamp(-5f, -1.8f, -2.2f, -1f);

        Assert.That(FindUnsupportedIslandCount(), Is.Zero);
    }

    [Test]
    public void SkinnyBorderFragmentTouchingOneWallSideIsDetected()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));
        AddSkinnyBorderFragmentTouchingLeftSide();

        Assert.That(FindUnsupportedIslandCount(), Is.GreaterThanOrEqualTo(1));

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(FindUnsupportedIslandCount(), Is.Zero);
    }

    [Test]
    public void WideBorderStripTouchingOneWallSideRemainsSupported()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));
        AddWideBorderStripTouchingLeftSide();

        Assert.That(FindUnsupportedIslandCount(), Is.Zero);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(CountHiddenCleanupStamps(), Is.Zero);
    }

    [Test]
    public void NoCorePerimeterFragmentIsNotAutomaticallySupported()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));
        AddNoCorePerimeterFragmentTouchingLeftSide();

        Assert.That(FindUnsupportedIslandCount(), Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void HairlineBridgeBelowThresholdDoesNotCountAsSupport()
    {
        AddMoatWithLeftBridge(0.035f);

        Assert.That(FindUnsupportedIslandCount(), Is.EqualTo(1));
    }

    [Test]
    public void WiderBridgeKeepsMaterialSupported()
    {
        AddMoatWithLeftBridge(0.14f);

        Assert.That(FindUnsupportedIslandCount(), Is.Zero);
    }

    [Test]
    public void DetachedSkinnyStripIsDetected()
    {
        AddDetachedSkinnyStrip(Vector2.zero);

        Assert.That(FindUnsupportedIslandCount(), Is.EqualTo(1));
    }

    [Test]
    public void BranchedNearOpenLeftoverIsDetectedWithNonRectangularBoundary()
    {
        AddBranchedLeftover(Vector2.zero);

        Assert.That(FindUnsupportedIslandCount(), Is.EqualTo(1));
        Assert.That(GetUnsupportedIslandPointCount(), Is.GreaterThan(4));
    }

    [Test]
    public void ThinJaggedIslandIsDetectedBySupportScan()
    {
        AddRectStamp(-0.22f, 0.22f, 0.55f, 1.2f);
        AddRectStamp(-0.22f, 0.22f, -1.2f, -0.55f);
        AddRectStamp(-0.55f, -0.18f, -1.2f, 1.2f);
        AddRectStamp(0.18f, 0.55f, -1.2f, 1.2f);

        Assert.That(FindUnsupportedIslandCount(), Is.EqualTo(1));
    }

    [Test]
    public void TinyOpenLineShardInsideRemovedPocketIsCleanedWithoutSpray()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));
        AddOpenLineShardPocket(new Vector2(-0.025f, 0.007f), new Vector2(0.025f, 0.007f), 0.004f);

        var islands = FindUnsupportedIslands(CreateBounds(-0.4f, 0.4f, -0.4f, 0.4f));
        Assert.That(islands.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(ContainsIslandRequiringSpray(islands, false), Is.True);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(CountSprayBurstObjects(), Is.Zero);
        Assert.That(CountHiddenCleanupStamps(), Is.GreaterThanOrEqualTo(1));
        Assert.That(FindUnsupportedIslandCount(CreateBounds(-0.4f, 0.4f, -0.4f, 0.4f)), Is.Zero);
    }

    [Test]
    public void UltraShortOpenLineShardInsideRemovedPocketIsCleanedWithoutSpray()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));
        AddOpenLineShardPocket(new Vector2(-0.002f, 0.007f), new Vector2(0.002f, 0.007f), 0.004f);

        var islands = FindUnsupportedIslands(CreateBounds(-0.4f, 0.4f, -0.4f, 0.4f));
        Assert.That(islands.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(ContainsIslandRequiringSpray(islands, false), Is.True);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(CountSprayBurstObjects(), Is.Zero);
        Assert.That(CountHiddenCleanupStamps(), Is.GreaterThanOrEqualTo(1));
        Assert.That(FindUnsupportedIslandCount(CreateBounds(-0.4f, 0.4f, -0.4f, 0.4f)), Is.Zero);
    }

    [Test]
    public void TinyClosedContourShardInsideRemovedPocketIsCleanedWithoutSpray()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));
        AddTinyClosedContourShardPocket(Vector2.zero, 0.0035f, 0.006f);

        var islands = FindUnsupportedIslands(CreateBounds(-0.4f, 0.4f, -0.4f, 0.4f));
        Assert.That(islands.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(ContainsIslandRequiringSpray(islands, false), Is.True);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(CountSprayBurstObjects(), Is.Zero);
        Assert.That(CountHiddenCleanupStamps(), Is.GreaterThanOrEqualTo(1));
        Assert.That(FindUnsupportedIslandCount(CreateBounds(-0.4f, 0.4f, -0.4f, 0.4f)), Is.Zero);
    }

    [Test]
    public void TwoSegmentOpenLineShardInsideRemovedPocketIsDetected()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));
        AddOpenLineShardPocket(
            new[]
            {
                new Vector2(-0.18f, -0.01f),
                new Vector2(0f, 0.035f),
                new Vector2(0.18f, -0.01f)
            },
            0.004f);

        Assert.That(FindUnsupportedIslandCount(CreateBounds(-0.5f, 0.5f, -0.5f, 0.5f)), Is.GreaterThanOrEqualTo(1));

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(FindUnsupportedIslandCount(CreateBounds(-0.5f, 0.5f, -0.5f, 0.5f)), Is.Zero);
        Assert.That(GetLastHiddenCleanupStampPointCount(), Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    public void LongMultiSegmentOpenLineShardInsideRemovedPocketIsDetected()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));
        AddOpenLineShardPocket(
            new[]
            {
                new Vector2(-0.35f, -0.02f),
                new Vector2(-0.16f, 0.032f),
                new Vector2(0.02f, -0.014f),
                new Vector2(0.19f, 0.026f),
                new Vector2(0.36f, -0.018f)
            },
            0.004f);

        Assert.That(FindUnsupportedIslandCount(CreateBounds(-0.6f, 0.6f, -0.5f, 0.5f)), Is.GreaterThanOrEqualTo(1));

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(FindUnsupportedIslandCount(CreateBounds(-0.6f, 0.6f, -0.5f, 0.5f)), Is.Zero);
    }

    [Test]
    public void ParallelOpenLineShardGroupInsideRemovedPocketIsDetected()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));
        AddParallelOpenLineShardPocket();

        Assert.That(FindUnsupportedIslandCount(CreateBounds(-0.6f, 0.6f, -0.35f, 0.35f)), Is.GreaterThanOrEqualTo(1));

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(FindUnsupportedIslandCount(CreateBounds(-0.6f, 0.6f, -0.35f, 0.35f)), Is.Zero);
    }

    [Test]
    public void OpenLineShardNearPerimeterSupportIsPreserved()
    {
        AddOpenStamp(new Vector2(-0.18f, -0.48f), new Vector2(0.18f, -0.48f));

        Assert.That(FindUnsupportedIslandCount(CreateBounds(-0.5f, 0.5f, -0.5f, 0.5f)), Is.Zero);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(CountHiddenCleanupStamps(), Is.Zero);
        Assert.That(GetVisibleContourOwnedWallSegmentCount(), Is.GreaterThan(0));
    }

    [Test]
    public void OpenLineShardAdjacentToSupportedMaterialIsPreserved()
    {
        AddOpenStamp(new Vector2(-0.2f, 0f), new Vector2(0.2f, 0f));

        Assert.That(FindUnsupportedIslandCount(CreateBounds(-0.5f, 0.5f, -0.5f, 0.5f)), Is.Zero);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(CountHiddenCleanupStamps(), Is.Zero);
        Assert.That(GetVisibleContourOwnedWallSegmentCount(), Is.GreaterThan(0));
    }

    [Test]
    public void PointLikeOpenShardAdjacentToSupportedMaterialIsPreserved()
    {
        AddOpenStamp(new Vector2(-0.002f, 0f), new Vector2(0.002f, 0f));

        Assert.That(FindUnsupportedIslandCount(CreateBounds(-0.5f, 0.5f, -0.5f, 0.5f)), Is.Zero);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(CountHiddenCleanupStamps(), Is.Zero);
        Assert.That(GetVisibleContourOwnedWallSegmentCount(), Is.GreaterThan(0));
    }

    [Test]
    public void PointLikeOpenShardNearPerimeterSupportIsPreserved()
    {
        AddOpenStamp(new Vector2(-0.002f, -0.48f), new Vector2(0.002f, -0.48f));

        Assert.That(FindUnsupportedIslandCount(CreateBounds(-0.5f, 0.5f, -0.5f, 0.5f)), Is.Zero);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(CountHiddenCleanupStamps(), Is.Zero);
        Assert.That(GetVisibleContourOwnedWallSegmentCount(), Is.GreaterThan(0));
    }

    [Test]
    public void LargeOpenLineShardSpawnsSprayBeforeCleanup()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));
        AddOpenLineShardPocket(new Vector2(-0.32f, 0.006f), new Vector2(0.32f, 0.006f), 0.004f);

        var islands = FindUnsupportedIslands(CreateBounds(-0.7f, 0.7f, -0.45f, 0.45f));
        Assert.That(islands.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(ContainsIslandRequiringSpray(islands, true), Is.True);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(CountSprayBurstObjects(), Is.GreaterThanOrEqualTo(1));
        Assert.That(CountHiddenCleanupStamps(), Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void SolidIslandAndTinyOpenLineShardCleanInOneCleanupCall()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));
        AddDamageRing(new Vector2(-2.1f, 0f));
        AddOpenLineShardPocket(new Vector2(1.75f, 0.007f), new Vector2(1.8f, 0.007f), 0.004f);

        var islands = FindUnsupportedIslands(CreateBounds(-5f, 5f, -3f, 3f));
        Assert.That(ContainsIslandRequiringSpray(islands, true), Is.True);
        Assert.That(ContainsIslandRequiringSpray(islands, false), Is.True);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(CountSprayBurstObjects(), Is.GreaterThanOrEqualTo(1));
        Assert.That(CountHiddenCleanupStamps(), Is.GreaterThanOrEqualTo(2));
        Assert.That(FindUnsupportedIslandCount(), Is.Zero);
    }

    [Test]
    public void SolidIslandAndLargeOpenLineShardBothSpawnSpray()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));
        AddDamageRing(new Vector2(-2.1f, 0f));
        AddOpenLineShardPocket(new Vector2(1.45f, 0.006f), new Vector2(2.1f, 0.006f), 0.004f);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(CountSprayBurstObjects(), Is.GreaterThanOrEqualTo(2));
        Assert.That(CountHiddenCleanupStamps(), Is.GreaterThanOrEqualTo(2));
        Assert.That(FindUnsupportedIslandCount(), Is.Zero);
    }

    [Test]
    public void TinyOpenLineShardCleansWhenSprayBudgetIsExhaustedBySolidIslands()
    {
        var maxSprays = GetPrivateConstantInt("MaxUnsupportedIslandSpraysPerDamage");
        SetConfiguredSize(new Vector3(70f, 30f, 0.5f));
        for (var i = 0; i <= maxSprays; i++)
        {
            var column = i % 5;
            var row = i / 5;
            AddDamageRing(new Vector2(-24f + column * 12f, -9f + row * 4f));
        }

        AddOpenLineShardPocket(new Vector2(30f, 0.007f), new Vector2(30.05f, 0.007f), 0.004f);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(CountSprayBurstObjects(), Is.EqualTo(maxSprays));
        Assert.That(CountHiddenCleanupStamps(), Is.GreaterThan(maxSprays));
        Assert.That(FindUnsupportedIslandCount(CreateBounds(-35f, 35f, -15f, 15f)), Is.EqualTo(1));
    }

    [Test]
    public void OpenLineGroupInsideSolidIslandDoesNotDuplicateSpray()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));
        AddDamageRing(Vector2.zero);
        AddOpenStamp(new Vector2(-0.2f, 0f), new Vector2(0.2f, 0f));

        Assert.That(FindUnsupportedIslandCount(), Is.EqualTo(1));

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(CountSprayBurstObjects(), Is.EqualTo(1));
        Assert.That(CountHiddenCleanupStamps(), Is.EqualTo(1));
        Assert.That(FindUnsupportedIslandCount(), Is.Zero);
    }

    [Test]
    public void CleanupStampClipsButDoesNotRenderVisibleContour()
    {
        AddRectStamp(-1f, 1f, -1f, 1f, renderContour: false);

        Assert.That(IsPointInsideWallDamageUnion(Vector2.zero), Is.True);
        Assert.That(BuildVisibleSegmentCount(), Is.Zero);
    }

    [Test]
    public void TinyUnsupportedComponentIsCleanedWithoutSpray()
    {
        AddTinyUnsupportedIsland();

        var islands = FindUnsupportedIslands(CreateBounds(-0.5f, 0.5f, -0.5f, 0.5f));
        Assert.That(islands.Count, Is.EqualTo(1));
        Assert.That(GetRequiresSpray(islands[0]), Is.False);
        Assert.That(IsPointInsideWallDamageUnion(Vector2.zero), Is.False);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(CountSprayBurstObjects(), Is.Zero);
        Assert.That(CountHiddenCleanupStamps(), Is.GreaterThanOrEqualTo(1));
        Assert.That(IsPointInsideWallDamageUnion(Vector2.zero), Is.True);
        Assert.That(FindUnsupportedIslandCount(CreateBounds(-0.5f, 0.5f, -0.5f, 0.5f)), Is.Zero);
    }

    [Test]
    public void MicroContourCleanupDoesNotAppendHiddenRectangleStamp()
    {
        AddRectStamp(-0.1f, 0.1f, -0.1f, 0.1f);
        var stampCountBefore = GetStampList().Count;

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(GetStampList().Count, Is.EqualTo(stampCountBefore));
        Assert.That(CountHiddenCleanupStamps(), Is.Zero);
        Assert.That(GetVisibleContourOwnedWallSegmentCount(), Is.GreaterThan(0));
    }

    [Test]
    public void UnsupportedCleanupStampUsesComponentBoundary()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));
        AddBranchedLeftover(Vector2.zero);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(CountSprayBurstObjects(), Is.GreaterThanOrEqualTo(1));
        Assert.That(GetLastHiddenCleanupStampPointCount(), Is.GreaterThan(4));
        Assert.That(FindUnsupportedIslandCount(), Is.Zero);
    }

    [Test]
    public void RepeatedContourDamageVisibleStampsRemainJaggedAfterCleanup()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));

        InvokeAddContourOwnedWallDamage(new Vector3(-1.2f, 0f, 0f));
        InvokeRemoveUnsupportedWallIslands();
        InvokeAddContourOwnedWallDamage(new Vector3(1.2f, 0.2f, 0f));
        InvokeRemoveUnsupportedWallIslands();

        Assert.That(CountVisibleStampsWithPointCount(32), Is.EqualTo(2));
        Assert.That(CountVisibleStampsWithPointCount(4), Is.Zero);
    }

    [Test]
    public void SingleContourDamageSpawnsGenericSprayWithoutIsland()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));

        InvokeTakeDamage(1f, Vector3.zero, Vector3.forward);

        Assert.That(CountVisibleStampsWithPointCount(32), Is.EqualTo(1));
        Assert.That(FindUnsupportedIslandCount(), Is.Zero);
        Assert.That(CountSprayBurstObjects(), Is.EqualTo(1));
        Assert.That(CountSprayChipObjects(), Is.InRange(5, 9));
    }

    [Test]
    public void GenericSprayUsesNoPhysicsAndFollowsHitNormal()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));

        InvokeTakeDamage(1f, Vector3.zero, Vector3.forward);

        var chip = GameObject.Find("Wall Material Spray Chip");
        Assert.That(chip, Is.Not.Null);
        Assert.That(chip.GetComponent<Rigidbody>(), Is.Null);
        Assert.That(chip.GetComponent<MeshCollider>(), Is.Null);
        var animation = FindComponentByTypeName(chip, "UnsupportedIslandSprayChipAnimation");
        Assert.That(animation, Is.Not.Null);
        var velocity = (Vector3)animation.GetType().GetField("velocity", PrivateInstance).GetValue(animation);
        Assert.That(Vector3.Dot(velocity, Vector3.back), Is.GreaterThan(0f));
    }

    [Test]
    public void GenericSprayIsRepeatableForSameHit()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));

        InvokeTakeDamage(1f, Vector3.zero, Vector3.forward);
        var firstSignature = ReadSpraySignature();
        DestroySprayObjects();
        GetStampList().Clear();

        InvokeTakeDamage(1f, Vector3.zero, Vector3.forward);
        var secondSignature = ReadSpraySignature();

        Assert.That(secondSignature, Is.EqualTo(firstSignature));
    }

    [Test]
    public void SprayIslandCreatesVisualChipsWithoutPhysics()
    {
        var budget = 1;
        var spawned = SpawnUnsupportedIslandSpray(
            CreateUnsupportedIsland(
                new List<Vector2>
                {
                    new(-0.5f, -0.35f),
                    new(0.45f, -0.3f),
                    new(0.35f, 0.4f),
                    new(-0.4f, 0.35f)
                }),
            Vector3.back,
            ref budget);

        var burst = GameObject.Find("Wall Material Spray Burst");
        var chip = GameObject.Find("Wall Material Spray Chip");
        Assert.That(spawned, Is.True);
        Assert.That(burst, Is.Not.Null);
        Assert.That(chip, Is.Not.Null);
        Assert.That(chip.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
        Assert.That(chip.GetComponent<MeshRenderer>(), Is.Not.Null);
        Assert.That(chip.GetComponent<Rigidbody>(), Is.Null);
        Assert.That(chip.GetComponent<MeshCollider>(), Is.Null);
        Assert.That(FindComponentByTypeName(chip, "UnsupportedIslandSprayChipAnimation"), Is.Not.Null);
        Assert.That(Vector3.Dot(chip.transform.position - testObject.transform.position, Vector3.back), Is.GreaterThan(0.25f));
        Assert.That(budget, Is.Zero);
    }

    [Test]
    public void SprayReturnsFalseWhenBudgetIsExhausted()
    {
        var budget = 0;
        var spawned = SpawnUnsupportedIslandSpray(
            CreateUnsupportedIsland(
                new List<Vector2>
                {
                    new(-0.5f, -0.5f),
                    new(0.5f, -0.5f),
                    new(0.5f, 0.5f),
                    new(-0.5f, 0.5f)
                }),
            Vector3.back,
            ref budget);

        Assert.That(spawned, Is.False);
        Assert.That(budget, Is.Zero);
        Assert.That(CountSprayBurstObjects(), Is.Zero);
    }

    [Test]
    public void CleanupDefersIslandsWhenSprayBudgetIsExhausted()
    {
        var maxSprays = GetPrivateConstantInt("MaxUnsupportedIslandSpraysPerDamage");
        SetConfiguredSize(new Vector3(0.5f, 8f, 80f));
        for (var i = 0; i <= maxSprays; i++)
        {
            AddDamageRing(new Vector2(0f, -27f + i * 3f));
        }

        Assert.That(FindUnsupportedIslandCount(CreateBounds(-4f, 4f, -40f, 40f)), Is.EqualTo(maxSprays + 1));

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(CountSprayBurstObjects(), Is.EqualTo(maxSprays));
        Assert.That(FindUnsupportedIslandCount(CreateBounds(-4f, 4f, -40f, 40f)), Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void ScreenshotStyleSkinnyLeftoversAreCleanedAfterSpraySpawns()
    {
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));
        AddDamageRing(new Vector2(-2.6f, 0.2f));
        AddMoatWithLeftBridge(0.035f);
        AddDetachedSkinnyStrip(new Vector2(2.4f, -0.7f));
        AddBranchedLeftover(new Vector2(2.2f, 1.25f));

        Assert.That(FindUnsupportedIslandCount(), Is.GreaterThanOrEqualTo(4));

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(CountSprayBurstObjects(), Is.GreaterThanOrEqualTo(4));
        Assert.That(FindUnsupportedIslandCount(), Is.Zero);
    }

    [Test]
    public void SprayChipAnimationMovesOutwardAndHasShortLifetime()
    {
        var budget = 1;
        Assert.That(SpawnUnsupportedIslandSpray(
            CreateUnsupportedIsland(
                new List<Vector2>
                {
                    new(-0.45f, -0.4f),
                    new(0.45f, -0.4f),
                    new(0.45f, 0.4f),
                    new(-0.45f, 0.4f)
                }),
            Vector3.back,
            ref budget), Is.True);

        var chip = GameObject.Find("Wall Material Spray Chip");
        Assert.That(chip, Is.Not.Null);
        var animation = FindComponentByTypeName(chip, "UnsupportedIslandSprayChipAnimation");
        Assert.That(animation, Is.Not.Null);
        var velocity = (Vector3)animation.GetType().GetField("velocity", PrivateInstance).GetValue(animation);
        var duration = (float)animation.GetType().GetField("duration", PrivateInstance).GetValue(animation);

        Assert.That(Vector3.Dot(velocity, Vector3.back), Is.GreaterThan(0f));
        Assert.That(duration, Is.GreaterThan(0.05f));
        Assert.That(duration, Is.LessThan(1f));
    }

    [Test]
    public void SprayIsRepeatableForSameIslandAndDirection()
    {
        var firstBudget = 1;
        Assert.That(SpawnUnsupportedIslandSpray(CreateUnsupportedIsland(CreateJaggedIslandPoints()), Vector3.back, ref firstBudget), Is.True);
        var firstSignature = ReadSpraySignature();
        DestroySprayObjects();

        var secondBudget = 1;
        Assert.That(SpawnUnsupportedIslandSpray(CreateUnsupportedIsland(CreateJaggedIslandPoints()), Vector3.back, ref secondBudget), Is.True);
        var secondSignature = ReadSpraySignature();

        Assert.That(secondSignature, Is.EqualTo(firstSignature));
    }

    [Test]
    public void JaggedUnsupportedIslandCreatesSprayChips()
    {
        var budget = 1;
        Assert.That(SpawnUnsupportedIslandSpray(CreateUnsupportedIsland(CreateJaggedIslandPoints()), Vector3.back, ref budget), Is.True);

        Assert.That(CountSprayBurstObjects(), Is.EqualTo(1));
        Assert.That(CountSprayChipObjects(), Is.GreaterThanOrEqualTo(3));
    }

    private void AddDamageRing(Vector2 center)
    {
        AddRectStamp(center.x - 1.25f, center.x + 1.25f, center.y + 0.55f, center.y + 1.25f);
        AddRectStamp(center.x - 1.25f, center.x + 1.25f, center.y - 1.25f, center.y - 0.55f);
        AddRectStamp(center.x - 1.25f, center.x - 0.55f, center.y - 1.25f, center.y + 1.25f);
        AddRectStamp(center.x + 0.55f, center.x + 1.25f, center.y - 1.25f, center.y + 1.25f);
    }

    private void AddSkinnyBorderFragmentTouchingLeftSide()
    {
        AddRectStamp(-5f, -4.75f, 0.2f, 3f);
        AddRectStamp(-5f, -4.75f, -3f, -0.2f);
        AddRectStamp(-4.75f, 5f, -3f, 3f);
    }

    private void AddWideBorderStripTouchingLeftSide()
    {
        AddRectStamp(-5f, -4.4f, 1f, 3f);
        AddRectStamp(-5f, -4.4f, -3f, -1f);
        AddRectStamp(-4.4f, 5f, -3f, 3f);
    }

    private void AddNoCorePerimeterFragmentTouchingLeftSide()
    {
        AddRectStamp(-5f, -4.95f, 1.5f, 3f);
        AddRectStamp(-5f, -4.95f, -3f, -1.5f);
        AddRectStamp(-4.95f, 5f, -3f, 3f);
    }

    private void AddTinyUnsupportedIsland()
    {
        AddRectStamp(-0.4f, 0.4f, 0.02f, 0.4f);
        AddRectStamp(-0.4f, 0.4f, -0.4f, -0.02f);
        AddRectStamp(-0.4f, -0.02f, -0.4f, 0.4f);
        AddRectStamp(0.02f, 0.4f, -0.4f, 0.4f);
    }

    private void AddMoatWithLeftBridge(float bridgeHalfHeight)
    {
        AddRectStamp(-1.25f, 1.25f, 0.55f, 1.25f);
        AddRectStamp(-1.25f, 1.25f, -1.25f, -0.55f);
        AddRectStamp(0.55f, 1.25f, -1.25f, 1.25f);
        AddRectStamp(-1.25f, -0.55f, bridgeHalfHeight, 1.25f);
        AddRectStamp(-1.25f, -0.55f, -1.25f, -bridgeHalfHeight);
    }

    private void AddDetachedSkinnyStrip(Vector2 center)
    {
        AddRectStamp(center.x - 0.42f, center.x + 0.42f, center.y + 1.45f, center.y + 2f);
        AddRectStamp(center.x - 0.42f, center.x + 0.42f, center.y - 2f, center.y - 1.45f);
        AddRectStamp(center.x - 0.42f, center.x - 0.055f, center.y - 2f, center.y + 2f);
        AddRectStamp(center.x + 0.055f, center.x + 0.42f, center.y - 2f, center.y + 2f);
    }

    private void AddBranchedLeftover(Vector2 center)
    {
        AddRectStamp(center.x - 1f, center.x + 0.8f, center.y + 0.7f, center.y + 1f);
        AddRectStamp(center.x + 0.6f, center.x + 0.8f, center.y - 0.7f, center.y + 0.7f);
        AddRectStamp(center.x - 1f, center.x + 0.8f, center.y - 1f, center.y - 0.7f);
        AddRectStamp(center.x - 1f, center.x - 0.7f, center.y - 1f, center.y + 1f);
        AddRectStamp(center.x - 0.2f, center.x + 0.6f, center.y - 0.2f, center.y + 0.7f);
    }

    private void AddOpenLineShardPocket(Vector2 start, Vector2 end, float halfGap)
    {
        AddOpenLineShardPocket(new[] { start, end }, halfGap);
    }

    private void AddOpenLineShardPocket(Vector2[] path, float halfGap)
    {
        CalculatePointBounds(new List<Vector2>(path), out var min, out var max);
        var sidePad = 0.12f;
        var verticalPad = 0.18f;
        AddRectStamp(min.x - sidePad, max.x + sidePad, max.y + halfGap, max.y + halfGap + verticalPad);
        AddRectStamp(min.x - sidePad, max.x + sidePad, min.y - halfGap - verticalPad, min.y - halfGap);
        AddRectStamp(min.x - sidePad, min.x, min.y - halfGap - verticalPad, max.y + halfGap + verticalPad);
        AddRectStamp(max.x, max.x + sidePad, min.y - halfGap - verticalPad, max.y + halfGap + verticalPad);
        AddOpenStamp(path);
    }

    private void AddParallelOpenLineShardPocket()
    {
        AddRectStamp(-0.4f, 0.4f, 0.04f, 0.22f);
        AddRectStamp(-0.4f, 0.4f, -0.22f, -0.04f);
        AddRectStamp(-0.4f, -0.32f, -0.22f, 0.22f);
        AddRectStamp(0.32f, 0.4f, -0.22f, 0.22f);
        AddOpenStamp(new Vector2(-0.28f, -0.012f), new Vector2(0.28f, -0.012f));
        AddOpenStamp(new Vector2(-0.28f, 0.012f), new Vector2(0.28f, 0.012f));
    }

    private void AddTinyClosedContourShardPocket(Vector2 center, float radius, float halfGap)
    {
        var sidePad = 0.12f;
        var verticalPad = 0.18f;
        AddRectStamp(center.x - sidePad, center.x + sidePad, center.y + halfGap, center.y + halfGap + verticalPad);
        AddRectStamp(center.x - sidePad, center.x + sidePad, center.y - halfGap - verticalPad, center.y - halfGap);
        AddRectStamp(center.x - sidePad, center.x - halfGap, center.y - halfGap - verticalPad, center.y + halfGap + verticalPad);
        AddRectStamp(center.x + halfGap, center.x + sidePad, center.y - halfGap - verticalPad, center.y + halfGap + verticalPad);
        AddClosedStamp(
            center + new Vector2(-radius, -radius * 0.45f),
            center + new Vector2(radius, -radius * 0.35f),
            center + new Vector2(radius * 0.15f, radius));
    }

    private void AddRectStamp(float minU, float maxU, float minV, float maxV, bool renderContour = true)
    {
        var points = new[]
        {
            new Vector2(minU, minV),
            new Vector2(maxU, minV),
            new Vector2(maxU, maxV),
            new Vector2(minU, maxV)
        };
        var stamp = System.Activator.CreateInstance(DamageStampType, nonPublic: true);
        SetField(stamp, "Normal", Vector3.forward);
        SetField(stamp, "U", Vector3.right);
        SetField(stamp, "V", Vector3.up);
        SetField(stamp, "Plane", 1f);
        SetField(stamp, "Min", new Vector2(minU, minV));
        SetField(stamp, "Max", new Vector2(maxU, maxV));
        SetField(stamp, "Points", points);
        SetField(stamp, "RenderClosed", true);
        SetField(stamp, "RenderContour", renderContour);
        GetStampList().Add(stamp);
    }

    private void AddClosedStamp(params Vector2[] points)
    {
        CalculatePointBounds(new List<Vector2>(points), out var min, out var max);
        var stamp = System.Activator.CreateInstance(DamageStampType, nonPublic: true);
        SetField(stamp, "Normal", Vector3.forward);
        SetField(stamp, "U", Vector3.right);
        SetField(stamp, "V", Vector3.up);
        SetField(stamp, "Plane", 1f);
        SetField(stamp, "Min", min);
        SetField(stamp, "Max", max);
        SetField(stamp, "Points", points);
        SetField(stamp, "RenderClosed", true);
        SetField(stamp, "RenderContour", true);
        GetStampList().Add(stamp);
    }

    private void AddOpenStamp(params Vector2[] path)
    {
        var points = new Vector2[path.Length + 1];
        for (var i = 0; i < path.Length; i++)
        {
            points[i] = path[i];
        }

        points[points.Length - 1] = path[path.Length - 1];
        CalculatePointBounds(new List<Vector2>(path), out var min, out var max);
        var stamp = System.Activator.CreateInstance(DamageStampType, nonPublic: true);
        SetField(stamp, "Normal", Vector3.forward);
        SetField(stamp, "U", Vector3.right);
        SetField(stamp, "V", Vector3.up);
        SetField(stamp, "Plane", 1f);
        SetField(stamp, "Min", min);
        SetField(stamp, "Max", max);
        SetField(stamp, "Points", points);
        SetField(stamp, "RenderClosed", false);
        SetField(stamp, "RenderContour", true);
        GetStampList().Add(stamp);
    }

    private int FindUnsupportedIslandCount()
    {
        return FindUnsupportedIslandCount(CreateBounds(-5f, 5f, -3f, 3f));
    }

    private int FindUnsupportedIslandCount(object bounds)
    {
        return FindUnsupportedIslands(bounds).Count;
    }

    private int GetUnsupportedIslandPointCount()
    {
        var islands = FindUnsupportedIslands(CreateBounds(-5f, 5f, -3f, 3f));
        var island = islands[0];
        var points = (ICollection)UnsupportedIslandType.GetField("Points", AllInstanceFields).GetValue(island);
        return points.Count;
    }

    private IList FindUnsupportedIslands(object bounds)
    {
        var method = PieceType.GetMethod("FindUnsupportedContourOwnedWallIslands", PrivateInstance);
        return (IList)method.Invoke(piece, new[] { bounds });
    }

    private bool IsPointInsideWallDamageUnion(Vector2 point)
    {
        var method = PieceType.GetMethod("IsPointInsideWallDamageUnion", PrivateInstance);
        return (bool)method.Invoke(piece, new object[] { point });
    }

    private int BuildVisibleSegmentCount()
    {
        var method = PieceType.GetMethod(
            "BuildStampUnionDamageSegments",
            PrivateInstance,
            null,
            new[] { GetStampList().GetType(), typeof(float) },
            null);
        var segments = (ICollection)method.Invoke(piece, new object[] { GetStampList(), 0.02f });
        return segments.Count;
    }

    private int GetVisibleContourOwnedWallSegmentCount()
    {
        var method = PieceType.GetMethod("GetVisibleContourOwnedWallSegments", PrivateInstance);
        var segments = (ICollection)method.Invoke(piece, new object[] { 0.02f });
        return segments.Count;
    }

    private IList GetStampList()
    {
        return (IList)PieceType.GetField("wallDamageStamps", PrivateInstance).GetValue(piece);
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

    private int GetLastHiddenCleanupStampPointCount()
    {
        var stamps = GetStampList();
        for (var i = stamps.Count - 1; i >= 0; i--)
        {
            var stamp = stamps[i];
            if (!(bool)DamageStampType.GetField("RenderContour", AllInstanceFields).GetValue(stamp))
            {
                return ((Vector2[])DamageStampType.GetField("Points", AllInstanceFields).GetValue(stamp)).Length;
            }
        }

        return 0;
    }

    private int CountVisibleStampsWithPointCount(int pointCount)
    {
        var count = 0;
        foreach (var stamp in GetStampList())
        {
            if ((bool)DamageStampType.GetField("RenderContour", AllInstanceFields).GetValue(stamp) &&
                ((Vector2[])DamageStampType.GetField("Points", AllInstanceFields).GetValue(stamp)).Length == pointCount)
            {
                count++;
            }
        }

        return count;
    }

    private bool GetRequiresSpray(object island)
    {
        return (bool)UnsupportedIslandType.GetField("RequiresSpray", AllInstanceFields).GetValue(island);
    }

    private bool ContainsIslandRequiringSpray(IList islands, bool requiresSpray)
    {
        foreach (var island in islands)
        {
            if (GetRequiresSpray(island) == requiresSpray)
            {
                return true;
            }
        }

        return false;
    }

    private bool SpawnUnsupportedIslandSpray(object island, Vector3 sprayDirection, ref int budget)
    {
        var method = PieceType.GetMethod("SpawnUnsupportedWallIslandSpray", PrivateInstance);
        var arguments = new object[]
        {
            island,
            Vector3.forward,
            Vector3.right,
            Vector3.up,
            0.25f,
            sprayDirection,
            budget
        };

        var spawned = (bool)method.Invoke(piece, arguments);
        budget = (int)arguments[6];
        return spawned;
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

    private void InvokeAddContourOwnedWallDamage(Vector3 hitPoint)
    {
        PieceType.GetMethod("AddContourOwnedWallDamage", PrivateInstance).Invoke(piece, new object[] { hitPoint });
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

    private void SetConfiguredSize(Vector3 size)
    {
        PieceType.GetField("configuredSize", PrivateInstance).SetValue(piece, size);
    }

    private static int CountSprayBurstObjects()
    {
        var count = 0;
        foreach (var spray in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (spray.name == "Wall Material Spray Burst")
            {
                count++;
            }
        }

        return count;
    }

    private static int CountSprayChipObjects()
    {
        var count = 0;
        foreach (var spray in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (spray.name == "Wall Material Spray Chip")
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

    private static int GetPrivateConstantInt(string name)
    {
        return (int)PieceType.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
    }

    private static string ReadSpraySignature()
    {
        var chips = new List<GameObject>();
        foreach (var spray in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (spray.name == "Wall Material Spray Chip")
            {
                chips.Add(spray);
            }
        }

        chips.Sort((a, b) => string.CompareOrdinal(FormatVector(a.transform.position), FormatVector(b.transform.position)));
        var signature = "chips=" + chips.Count;
        for (var i = 0; i < chips.Count; i++)
        {
            var animation = FindComponentByTypeName(chips[i], "UnsupportedIslandSprayChipAnimation");
            var velocity = animation != null
                ? (Vector3)animation.GetType().GetField("velocity", PrivateInstance).GetValue(animation)
                : Vector3.zero;
            signature += "|" + FormatVector(chips[i].transform.position) + ":" + FormatVector(velocity);
        }

        return signature;
    }

    private static string FormatVector(Vector3 value)
    {
        return $"{value.x:F4},{value.y:F4},{value.z:F4}";
    }

    private static void DestroySprayObjects()
    {
        var objects = new List<GameObject>();
        foreach (var spray in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (spray != null && (spray.name == "Wall Material Spray Burst" || spray.name == "Wall Material Spray Chip"))
            {
                objects.Add(spray);
            }
        }

        for (var i = 0; i < objects.Count; i++)
        {
            if (objects[i] != null)
            {
                Object.DestroyImmediate(objects[i]);
            }
        }
    }

    private static object CreateBounds(float minU, float maxU, float minV, float maxV)
    {
        var constructor = BoundsType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(float), typeof(float), typeof(float), typeof(float) },
            null);
        return constructor.Invoke(new object[] { minU, maxU, minV, maxV });
    }

    private static object CreateUnsupportedIsland(List<Vector2> points)
    {
        EnsureCounterClockwise(points);
        CalculatePointBounds(points, out var min, out var max);
        var constructor = UnsupportedIslandType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(List<Vector2>), typeof(Vector2), typeof(Vector2), typeof(Vector2), typeof(float) },
            null);
        return constructor.Invoke(new object[]
        {
            points,
            CalculatePointAverage(points),
            min,
            max,
            Mathf.Abs(CalculateSignedArea(points))
        });
    }

    private static List<Vector2> CreateJaggedIslandPoints()
    {
        return new List<Vector2>
        {
            new(-0.55f, -0.18f),
            new(-0.36f, -0.52f),
            new(0.03f, -0.42f),
            new(0.42f, -0.56f),
            new(0.56f, -0.08f),
            new(0.33f, 0.28f),
            new(0.08f, 0.5f),
            new(-0.2f, 0.34f),
            new(-0.5f, 0.46f),
            new(-0.38f, 0.05f)
        };
    }

    private static void EnsureCounterClockwise(List<Vector2> points)
    {
        if (CalculateSignedArea(points) < 0f)
        {
            points.Reverse();
        }
    }

    private static float CalculateSignedArea(List<Vector2> points)
    {
        var twiceArea = 0f;
        for (var i = 0; i < points.Count; i++)
        {
            var next = (i + 1) % points.Count;
            twiceArea += points[i].x * points[next].y - points[next].x * points[i].y;
        }

        return twiceArea * 0.5f;
    }

    private static Vector2 CalculatePointAverage(List<Vector2> points)
    {
        var average = Vector2.zero;
        for (var i = 0; i < points.Count; i++)
        {
            average += points[i];
        }

        return average / Mathf.Max(1, points.Count);
    }

    private static void CalculatePointBounds(List<Vector2> points, out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (var i = 0; i < points.Count; i++)
        {
            min = Vector2.Min(min, points[i]);
            max = Vector2.Max(max, points[i]);
        }
    }

    private static void SetField(object target, string name, object value)
    {
        DamageStampType.GetField(name, AllInstanceFields).SetValue(target, value);
    }
}
