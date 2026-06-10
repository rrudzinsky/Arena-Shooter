using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PillarOutlineTests
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BindingFlags PublicStatic = BindingFlags.Static | BindingFlags.Public;
    private static readonly Type ArenaGeneratorType = Type.GetType("ArenaShooter.ArenaGenerator, Assembly-CSharp");
    private static readonly Type ArenaThemeType = Type.GetType("ArenaShooter.ArenaTheme, Assembly-CSharp");
    private static readonly Type DestructibleArenaPieceType = Type.GetType("ArenaShooter.DestructibleArenaPiece, Assembly-CSharp");
    private static readonly Type RenderSetupType = Type.GetType("ArenaShooter.DroidRenderSetup, Assembly-CSharp");
    private static readonly Color ExpectedMatteBlack = new(0.0015f, 0.001f, 0.004f, 1f);
    private const float MatteBlackTolerance = 0.015f;
    private const float OutlineBoundsTolerance = 0.08f;
    private const string CombinedWallBodyName = "Combined Destructible Wall Body";
    private const string StructuralWallOutlineSourceName = "Destructible Wall Outline Source";
    private const string DamageContoursName = "Destructible Damage Contours";
    private const string IntactWallOutlineSourceName = "Intact Destructible Wall Outline Source";
    private static readonly string[] RemovedPillarOutlineObjectNames =
    {
        "Pillar Fine Edge Rail",
        "Intact Corner Pillar Outline Source",
        "Intact Pillar Wall Outline Source",
        IntactWallOutlineSourceName
    };

    private readonly List<GameObject> roots = new();

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

    [TestCase("Assets/Models/CyberArenaCornerPost.fbx")]
    [TestCase("Assets/Resources/Models/CyberArenaCornerPost.fbx")]
    [TestCase("Assets/Models/CyberArenaWallBlock.fbx")]
    [TestCase("Assets/Resources/Models/CyberArenaWallBlock.fbx")]
    public void SourceWallAndPillarFbxsImportAsMatteBlack(string assetPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        Assert.That(prefab, Is.Not.Null, assetPath);

        var materials = prefab.GetComponentsInChildren<Renderer>(true)
            .SelectMany(renderer => renderer.sharedMaterials)
            .Where(material => material != null)
            .Distinct()
            .ToArray();

        Assert.That(materials, Is.Not.Empty, assetPath);
        Assert.That(materials, Has.All.Matches<Material>(SourceMaterialIsMatteBlack));
    }

    [Test]
    public void WallDamageMaskShaderDrawsProxyPlanesDoubleSidedAndDepthBiased()
    {
        const string shaderPath = "Assets/Shaders/DroidOutlineWallDamageMask.shader";
        Assert.That(File.Exists(shaderPath), Is.True, shaderPath);

        var shaderSource = File.ReadAllText(shaderPath);
        Assert.That(shaderSource, Does.Contain("ZTest LEqual"));
        Assert.That(shaderSource, Does.Contain("Cull Off"));
        Assert.That(shaderSource, Does.Contain("Offset -1, -1"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void OpeningPillarsUseWallRuntimeVisualPathAtSpawn(bool horizontalWall)
    {
        const float wallHeight = 4f;
        var root = CreateOpeningPillarRoot(horizontalWall, wallHeight);

        var pillars = FindChildren(root, "Room Corridor Opening Pillar");
        var baseAccents = FindChildren(root, "Pillar Base Neon Accent");

        Assert.That(pillars.Count, Is.EqualTo(2));
        Assert.That(baseAccents.Count, Is.EqualTo(8));
        AssertNoRemovedPillarOutlineObjects(root);
        Assert.That(FindChildren(root, CombinedWallBodyName).Count, Is.EqualTo(4));
        Assert.That(FindChildren(root, StructuralWallOutlineSourceName).Count, Is.EqualTo(4));
        Assert.That(FindChildren(root, DamageContoursName).Count, Is.EqualTo(4));
        foreach (var pillar in pillars)
        {
            AssertPillarUsesGeneratedWallRuntimeVisualPath(pillar, wallHeight);
        }

        Assert.That(baseAccents, Has.All.Matches<GameObject>(accent => HasOnlyDefaultRenderingLayer(accent)));
        Assert.That(baseAccents.Count(accent => HasCollider(accent)), Is.EqualTo(0));
    }

    [Test]
    public void GeneratedStructuralWallsAndPillarsBothFeedWallMask()
    {
        var pillarRoot = CreateOpeningPillarRoot(true, 4f);
        var wallRoot = CreateGeneratedWallRoot();
        var pillarBodies = FindChildren(pillarRoot, CombinedWallBodyName);
        var wallBodies = FindChildren(wallRoot, CombinedWallBodyName);

        Assert.That(pillarBodies.Count, Is.EqualTo(4));
        Assert.That(wallBodies.Count, Is.EqualTo(1));
        Assert.That(pillarBodies.Concat(wallBodies), Has.All.Matches<GameObject>(HasDefaultAndWallRenderingLayers));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DamagedOpeningPillarKeepsMatteBlackVisualPath(bool horizontalWall)
    {
        var root = CreateOpeningPillarRoot(horizontalWall, 4f);
        var pillar = FindChildren(root, "Room Corridor Opening Pillar").First();
        Assert.That(DestructibleArenaPieceType, Is.Not.Null);
        var destructible = pillar.GetComponent(DestructibleArenaPieceType);
        Assert.That(destructible, Is.Not.Null);
        var intactRenderers = GetIntactCornerPostRenderers(pillar);
        Assert.That(intactRenderers, Is.Not.Empty);
        Assert.That(intactRenderers, Has.All.Matches<Renderer>(renderer => !renderer.enabled));
        Assert.That(intactRenderers, Has.All.Matches<Renderer>(RendererUsesMatteBlackMaterial));
        AssertNoRemovedPillarOutlineObjects(pillar);
        var initialCombinedBodies = FindChildren(pillar, CombinedWallBodyName);
        Assert.That(initialCombinedBodies.Count, Is.EqualTo(2));
        var initialOutlineSources = FindChildren(pillar, StructuralWallOutlineSourceName);
        Assert.That(initialOutlineSources.Count, Is.EqualTo(2));
        foreach (var source in initialOutlineSources)
        {
            AssertStructuralWallOutlineSource(source, GetConfiguredPillarSize(pillar));
            AssertIntactPillarWallStyleOutlineSource(source);
        }

        var initialDamageContours = FindChildren(pillar, DamageContoursName);
        Assert.That(initialDamageContours.Count, Is.EqualTo(2));

        var takeDamage = DestructibleArenaPieceType.GetMethod(
            "TakeDamage",
            new[] { typeof(float), typeof(Vector3), typeof(Vector3) });
        Assert.That(takeDamage, Is.Not.Null);
        takeDamage.Invoke(destructible, new object[] { 1000f, pillar.transform.position, Vector3.forward });

        var combinedBodies = FindChildren(pillar, CombinedWallBodyName);
        var outlineSources = FindChildren(pillar, StructuralWallOutlineSourceName);
        var damageContours = FindChildren(pillar, DamageContoursName);
        Assert.That(intactRenderers, Has.All.Matches<Renderer>(renderer => !renderer.enabled));
        AssertNoRemovedPillarOutlineObjects(pillar);
        Assert.That(combinedBodies.Count, Is.EqualTo(2));
        foreach (var body in combinedBodies)
        {
            AssertGeneratedWallBody(body);
        }

        Assert.That(outlineSources.Count, Is.EqualTo(2));
        Assert.That(outlineSources, Has.All.Matches<GameObject>(source => HasWallRenderingLayer(source)));
        Assert.That(outlineSources.Count(source => HasCollider(source)), Is.EqualTo(0));
        Assert.That(damageContours.Count, Is.EqualTo(2));
        Assert.That(damageContours.Count(ContourHasUpdatedMesh), Is.GreaterThanOrEqualTo(1));
    }

    private static bool ContourHasUpdatedMesh(GameObject contour)
    {
        var meshFilter = contour.GetComponent<MeshFilter>();
        return meshFilter != null && meshFilter.sharedMesh != null && meshFilter.sharedMesh.vertexCount > 0;
    }

    private GameObject CreateOpeningPillarRoot(bool horizontalWall, float wallHeight)
    {
        var root = new GameObject("pillar outline test root");
        roots.Add(root);
        Assert.That(ArenaGeneratorType, Is.Not.Null);
        Assert.That(ArenaThemeType, Is.Not.Null);
        var generator = root.AddComponent(ArenaGeneratorType);
        var theme = Activator.CreateInstance(ArenaThemeType);
        ArenaGeneratorType.GetField("activeTheme", PrivateInstance).SetValue(generator, theme);
        const float wallThickness = 0.48f;
        var wallCenter = new Vector3(0f, wallHeight * 0.5f, 0f);

        ArenaGeneratorType
            .GetMethod("CreateOpeningPillars", PrivateInstance)
            .Invoke(generator, new object[] { theme, root.transform, wallCenter, horizontalWall, 2.2f, wallHeight, wallThickness });

        return root;
    }

    private GameObject CreateGeneratedWallRoot()
    {
        var root = new GameObject("wall outline parity test root");
        roots.Add(root);
        Assert.That(ArenaGeneratorType, Is.Not.Null);
        Assert.That(ArenaThemeType, Is.Not.Null);
        var generator = root.AddComponent(ArenaGeneratorType);
        var theme = Activator.CreateInstance(ArenaThemeType);
        ArenaGeneratorType.GetField("activeTheme", PrivateInstance).SetValue(generator, theme);
        var wallMaterial = (Material)ArenaThemeType.GetProperty("Wall").GetValue(theme);
        var createCube = ArenaGeneratorType.GetMethod("CreateCube", PrivateInstance);
        Assert.That(createCube, Is.Not.Null);
        createCube.Invoke(
            generator,
            new object[]
            {
                "Arena Wall",
                root.transform,
                Vector3.zero,
                new Vector3(4f, 4f, 0.48f),
                wallMaterial,
                false
            });

        return root;
    }

    private static void AssertPillarUsesGeneratedWallRuntimeVisualPath(GameObject pillar, float wallHeight)
    {
        Assert.That(FindChildren(pillar, "Imported Cyber Arena Corner Post Mesh").Count, Is.EqualTo(1));
        Assert.That(pillar.GetComponent(DestructibleArenaPieceType), Is.Not.Null);
        var combinedBodies = FindChildren(pillar, CombinedWallBodyName);
        Assert.That(combinedBodies.Count, Is.EqualTo(2));
        foreach (var body in combinedBodies)
        {
            AssertGeneratedWallBody(body);
        }

        var outlineSources = FindChildren(pillar, StructuralWallOutlineSourceName);
        Assert.That(outlineSources.Count, Is.EqualTo(2));
        foreach (var source in outlineSources)
        {
            AssertStructuralWallOutlineSource(source, GetConfiguredPillarSize(pillar));
            AssertIntactPillarWallStyleOutlineSource(source);
        }

        var damageContours = FindChildren(pillar, DamageContoursName);
        Assert.That(damageContours.Count, Is.EqualTo(2));
        foreach (var contour in damageContours)
        {
            AssertDamageContourObject(contour);
        }

        AssertNoRemovedPillarOutlineObjects(pillar);

        var renderers = GetIntactCornerPostRenderers(pillar);
        Assert.That(renderers, Is.Not.Empty);
        Assert.That(renderers, Has.All.Matches<Renderer>(renderer => !renderer.enabled));
        Assert.That(renderers, Has.All.Matches<Renderer>(IsOpaqueRenderer));
        Assert.That(renderers, Has.All.Matches<Renderer>(RendererUsesMatteBlackMaterial));
        Assert.That(HasFullHeightCollider(pillar, wallHeight), Is.True);
    }

    private static List<GameObject> FindChildren(GameObject root, string name)
    {
        var matches = new List<GameObject>();
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform.gameObject.name == name)
            {
                matches.Add(transform.gameObject);
            }
        }

        return matches;
    }

    private static void AssertNoRemovedPillarOutlineObjects(GameObject root)
    {
        foreach (var removedName in RemovedPillarOutlineObjectNames)
        {
            Assert.That(FindChildren(root, removedName), Is.Empty, removedName + " should stay removed.");
        }
    }

    private static void AssertGeneratedWallBody(GameObject body)
    {
        Assert.That(body, Is.Not.Null);
        Assert.That(HasDefaultAndWallRenderingLayers(body), Is.True);
        var renderer = body.GetComponent<Renderer>();
        Assert.That(RendererUsesMatteBlackColor(renderer), Is.True);
        var meshFilter = body.GetComponent<MeshFilter>();
        Assert.That(meshFilter, Is.Not.Null);
        Assert.That(meshFilter.sharedMesh, Is.Not.Null);
        Assert.That(meshFilter.sharedMesh.vertexCount, Is.GreaterThan(0));
    }

    private static void AssertStructuralWallOutlineSource(GameObject source, Vector3 expectedSize)
    {
        Assert.That(source, Is.Not.Null);
        Assert.That(HasCollider(source), Is.False);
        Assert.That(HasWallRenderingLayer(source), Is.True);

        var renderer = source.GetComponent<MeshRenderer>();
        Assert.That(renderer, Is.Not.Null);
        Assert.That(renderer.enabled, Is.True);
        Assert.That(renderer.sharedMaterial, Is.Not.Null);
        Assert.That(renderer.sharedMaterial.name, Does.StartWith("Invisible Destructible Wall Outline Source"));

        var meshFilter = source.GetComponent<MeshFilter>();
        Assert.That(meshFilter, Is.Not.Null);
        Assert.That(meshFilter.sharedMesh, Is.Not.Null);
        Assert.That(meshFilter.sharedMesh.vertexCount, Is.GreaterThan(0));
        Assert.That(meshFilter.sharedMesh.triangles.Length, Is.GreaterThan(0));

        var boundsSize = Vector3.Scale(meshFilter.sharedMesh.bounds.size, source.transform.lossyScale);
        Assert.That(boundsSize.x, Is.GreaterThanOrEqualTo(expectedSize.x - OutlineBoundsTolerance));
        Assert.That(boundsSize.y, Is.GreaterThanOrEqualTo(expectedSize.y - OutlineBoundsTolerance));
        Assert.That(boundsSize.z, Is.GreaterThanOrEqualTo(expectedSize.z - OutlineBoundsTolerance));
        var floorSeamSinkAllowance = Mathf.Max(0.35f, expectedSize.y * 1.1f, 0.06f);
        Assert.That(boundsSize.x, Is.LessThanOrEqualTo(expectedSize.x + 0.2f));
        Assert.That(boundsSize.y, Is.LessThanOrEqualTo(expectedSize.y + floorSeamSinkAllowance + 0.2f));
        Assert.That(boundsSize.z, Is.LessThanOrEqualTo(expectedSize.z + 0.2f));
    }

    private static void AssertIntactPillarWallStyleOutlineSource(GameObject source)
    {
        var mesh = source.GetComponent<MeshFilter>().sharedMesh;
        Assert.That(mesh.vertexCount, Is.EqualTo(12));
        Assert.That(mesh.triangles.Length, Is.EqualTo(12));

        var normalKeys = mesh.normals.Select(DominantNormalKey).Distinct().ToArray();
        Assert.That(normalKeys.Length, Is.EqualTo(2));
    }

    private static void AssertDamageContourObject(GameObject contour)
    {
        Assert.That(contour, Is.Not.Null);
        Assert.That(HasOnlyDefaultRenderingLayer(contour), Is.True);
        Assert.That(contour.GetComponent<MeshFilter>(), Is.Not.Null);
        Assert.That(contour.GetComponent<MeshRenderer>(), Is.Not.Null);
    }

    private static void AssertDamageContourMeshUpdated(GameObject contour)
    {
        AssertDamageContourObject(contour);
        var mesh = contour.GetComponent<MeshFilter>().sharedMesh;
        Assert.That(mesh, Is.Not.Null);
        Assert.That(mesh.vertexCount, Is.GreaterThan(0));
    }

    private static Renderer[] GetIntactCornerPostRenderers(GameObject pillar)
    {
        return pillar.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer != null &&
                               renderer.transform != pillar.transform &&
                               !renderer.gameObject.name.StartsWith("Combined Destructible", StringComparison.Ordinal) &&
                               !renderer.gameObject.name.StartsWith("Destructible ", StringComparison.Ordinal))
            .ToArray();
    }

    private static Vector3 GetConfiguredPillarSize(GameObject pillar)
    {
        var collider = GetEnabledPillarBoxCollider(pillar);
        Assert.That(collider, Is.Not.Null);
        return Vector3.Scale(collider.size, AbsVector(collider.transform.lossyScale));
    }

    private static bool HasCollider(GameObject target)
    {
        return target.GetComponent<Collider>() != null || target.GetComponentInChildren<Collider>(true) != null;
    }

    private static bool HasOnlyDefaultRenderingLayer(GameObject target)
    {
        var renderer = target.GetComponent<Renderer>();
        return renderer != null && renderer.renderingLayerMask == GetDefaultRenderingLayer();
    }

    private static bool HasDefaultAndWallRenderingLayers(GameObject target)
    {
        var renderer = target.GetComponent<Renderer>();
        return renderer != null &&
               (renderer.renderingLayerMask & GetDefaultRenderingLayer()) != 0u &&
               (renderer.renderingLayerMask & GetWallRenderingLayer()) != 0u;
    }

    private static bool HasWallRenderingLayer(GameObject target)
    {
        var renderer = target.GetComponent<Renderer>();
        return RendererHasWallRenderingLayer(renderer);
    }

    private static bool RendererHasWallRenderingLayer(Renderer renderer)
    {
        return renderer != null && renderer.renderingLayerMask == GetWallRenderingLayer();
    }

    private static bool IsOpaqueRenderer(Renderer renderer)
    {
        return renderer != null &&
               renderer.sharedMaterial != null &&
               renderer.sharedMaterial.renderQueue <= 2500;
    }

    private static bool RendererUsesMatteBlackMaterial(Renderer renderer)
    {
        return renderer != null &&
               renderer.sharedMaterials != null &&
               renderer.sharedMaterials.Length > 0 &&
               renderer.sharedMaterials.All(SourceMaterialIsMatteBlack);
    }

    private static bool RendererUsesMatteBlackColor(Renderer renderer)
    {
        return renderer != null &&
               renderer.sharedMaterials != null &&
               renderer.sharedMaterials.Length > 0 &&
               renderer.sharedMaterials.All(MaterialColorIsMatteBlack);
    }

    private static bool SourceMaterialIsMatteBlack(Material material)
    {
        if (material == null)
        {
            return false;
        }

        if (!material.name.StartsWith("Arena Source Matte Black", StringComparison.Ordinal))
        {
            return false;
        }

        return MaterialColorIsMatteBlack(material);
    }

    private static bool MaterialColorIsMatteBlack(Material material)
    {
        if (material == null)
        {
            return false;
        }

        var color = GetMaterialColor(material);
        return ColorApproximately(color, ExpectedMatteBlack, MatteBlackTolerance) ||
               ColorApproximately(color, ExpectedMatteBlack.gamma, MatteBlackTolerance);
    }

    private static Color GetMaterialColor(Material material)
    {
        if (material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }

        if (material.HasProperty("_Color"))
        {
            return material.GetColor("_Color");
        }

        return material.color;
    }

    private static bool ColorApproximately(Color actual, Color expected, float tolerance)
    {
        return Mathf.Abs(actual.r - expected.r) <= tolerance &&
               Mathf.Abs(actual.g - expected.g) <= tolerance &&
               Mathf.Abs(actual.b - expected.b) <= tolerance &&
               Mathf.Abs(actual.a - expected.a) <= tolerance;
    }

    private static bool HasFullHeightCollider(GameObject pillar, float wallHeight)
    {
        var collider = GetEnabledPillarBoxCollider(pillar);
        if (collider == null)
        {
            return false;
        }

        var worldHeight = collider.size.y * Mathf.Abs(collider.transform.lossyScale.y);
        var bottom = collider.transform.TransformPoint(collider.center).y - worldHeight * 0.5f;
        return Mathf.Abs(worldHeight - wallHeight) <= 0.001f &&
               Mathf.Abs(bottom) <= 0.001f;
    }

    private static Vector3 AbsVector(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private static string DominantNormalKey(Vector3 normal)
    {
        var abs = AbsVector(normal);
        if (abs.x >= abs.y && abs.x >= abs.z)
        {
            return normal.x >= 0f ? "+X" : "-X";
        }

        if (abs.y >= abs.x && abs.y >= abs.z)
        {
            return normal.y >= 0f ? "+Y" : "-Y";
        }

        return normal.z >= 0f ? "+Z" : "-Z";
    }

    private static BoxCollider GetEnabledPillarBoxCollider(GameObject pillar)
    {
        return pillar.GetComponents<BoxCollider>().FirstOrDefault(collider => collider != null && collider.enabled);
    }

    private static uint GetDefaultRenderingLayer()
    {
        Assert.That(RenderSetupType, Is.Not.Null);
        var field = RenderSetupType.GetField("DefaultRenderingLayer", PublicStatic);
        Assert.That(field, Is.Not.Null);
        return (uint)field.GetRawConstantValue();
    }

    private static uint GetWallRenderingLayer()
    {
        Assert.That(RenderSetupType, Is.Not.Null);
        var field = RenderSetupType.GetField("WallRenderingLayer", PublicStatic);
        Assert.That(field, Is.Not.Null);
        return (uint)field.GetRawConstantValue();
    }
}
