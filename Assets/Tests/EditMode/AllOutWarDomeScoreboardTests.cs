using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class AllOutWarDomeScoreboardTests
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;
    private static readonly Type ScoreboardType = Type.GetType("ArenaShooter.AllOutWarDomeScoreboard, Assembly-CSharp");
    private static readonly Type MatchControllerType = Type.GetType("ArenaShooter.MatchController, Assembly-CSharp");
    private static readonly Type ArenaLayoutType = Type.GetType("ArenaShooter.ArenaLayout, Assembly-CSharp");

    private readonly System.Collections.Generic.List<GameObject> roots = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var root in roots)
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        roots.Clear();
    }

    [Test]
    public void BlueEqualizerKeepsBlueColorButUsesNonBlueBehaviorRange()
    {
        var color = InvokePrivateStatic<Color>("GetEqualizerColor", 0.12f);
        var behavior = InvokePrivateStatic<float>("GetEqualizerBehaviorT", 0.12f);

        Assert.That(color.b, Is.GreaterThan(color.r * 8f));
        Assert.That(color.g, Is.GreaterThan(color.r * 5f));
        Assert.That(behavior, Is.EqualTo(0.62f).Within(0.0001f));
        Assert.That(behavior, Is.GreaterThanOrEqualTo(0.24f));
    }

    [Test]
    public void NonBlueEqualizerBehaviorIsUnchanged()
    {
        foreach (var colorT in new[] { 0.24f, 0.43f, 0.64f, 0.88f, 1f })
        {
            var behavior = InvokePrivateStatic<float>("GetEqualizerBehaviorT", colorT);
            Assert.That(behavior, Is.EqualTo(colorT).Within(0.0001f));
        }
    }

    [Test]
    public void BlueSkylineSamplingNoLongerUsesRawLowEndPosition()
    {
        var rawBlueT = 0.08f;
        var behavior = InvokePrivateStatic<float>("GetEqualizerBehaviorT", rawBlueT);
        var skylineFromRawPosition = InvokePrivateStatic<int>("GetEqualizerSkylineTiles", rawBlueT, 10);
        var skylineFromBehavior = InvokePrivateStatic<int>("GetEqualizerSkylineTiles", behavior, 10);

        Assert.That(behavior, Is.GreaterThan(0.24f));
        Assert.That(skylineFromBehavior, Is.Not.EqualTo(skylineFromRawPosition));
    }

    [Test]
    public void ScoreboardLocalProjectionUsesEightyPercentScale()
    {
        var scaled = InvokePrivateStatic<Vector2>("ScaleScoreboardLocalPosition", new Vector2(100f, -50f));

        Assert.That(scaled.x, Is.EqualTo(80f).Within(0.0001f));
        Assert.That(scaled.y, Is.EqualTo(-40f).Within(0.0001f));
    }

    [Test]
    public void EqualizerDomeProjectionIgnoresScoreboardScale()
    {
        var root = new GameObject("Dome scoreboard projection test");
        roots.Add(root);
        var owner = root.AddComponent(MatchControllerType);
        var scoreboard = root.AddComponent(ScoreboardType);
        var layout = Activator.CreateInstance(ArenaLayoutType);
        ScoreboardType.GetMethod("Build", PublicInstance).Invoke(scoreboard, new[] { owner, layout, 4.8f });

        var patch = CreateDomePatch(0f, 0.22f, 0f);
        var localPosition = new Vector2(0f, 100f);
        var actual = InvokePrivateInstance<Vector3>(scoreboard, "DomePoint", patch, localPosition, 0f);
        var expected = InvokeStadiumDomePoint(0.22f + localPosition.y / GetPrivateConstant<float>("DomeVerticalUnitsPerLatitude"), 0f, 0f);

        Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f));
    }

    private static T InvokePrivateStatic<T>(string methodName, params object[] args)
    {
        Assert.That(ScoreboardType, Is.Not.Null);
        var method = ScoreboardType.GetMethod(methodName, PrivateStatic);
        Assert.That(method, Is.Not.Null, methodName);
        return (T)method.Invoke(null, args);
    }

    private static T InvokePrivateInstance<T>(object instance, string methodName, params object[] args)
    {
        Assert.That(ScoreboardType, Is.Not.Null);
        var method = ScoreboardType.GetMethod(methodName, PrivateInstance);
        Assert.That(method, Is.Not.Null, methodName);
        return (T)method.Invoke(instance, args);
    }

    private static T GetPrivateConstant<T>(string fieldName)
    {
        Assert.That(ScoreboardType, Is.Not.Null);
        var field = ScoreboardType.GetField(fieldName, PrivateStatic);
        Assert.That(field, Is.Not.Null, fieldName);
        return (T)field.GetRawConstantValue();
    }

    private static object CreateDomePatch(float phi, float latitude, float rollDegrees)
    {
        Assert.That(ScoreboardType, Is.Not.Null);
        var patchType = ScoreboardType.GetNestedType("DomePatch", BindingFlags.NonPublic);
        Assert.That(patchType, Is.Not.Null);
        return Activator.CreateInstance(patchType, phi, latitude, rollDegrees);
    }

    private static Vector3 InvokeStadiumDomePoint(float latitude, float phi, float radiusInset)
    {
        var visualsType = Type.GetType("ArenaShooter.AllOutWarStadiumVisuals, Assembly-CSharp");
        Assert.That(visualsType, Is.Not.Null);
        var metrics = visualsType.GetMethod("CreateMetrics", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, new object[] { Vector3.zero });
        return (Vector3)visualsType.GetMethod("DomePoint", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, new[] { metrics, latitude, phi, radiusInset });
    }
}
