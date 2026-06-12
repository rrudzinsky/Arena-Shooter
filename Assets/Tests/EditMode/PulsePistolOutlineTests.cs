using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PulsePistolOutlineTests
{
    private static readonly BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly BindingFlags PublicStatic = BindingFlags.Static | BindingFlags.Public;
    private static readonly BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;
    private static readonly Type OutlineFeatureType = Type.GetType("ArenaShooter.Rendering.DroidOutlineRendererFeature, Assembly-CSharp");
    private static readonly Type RenderSetupType = Type.GetType("ArenaShooter.DroidRenderSetup, Assembly-CSharp");
    private static readonly Type OutlineCategoryType = Type.GetType("ArenaShooter.StylizedOutlineCategory, Assembly-CSharp");
    private static readonly Type ViewModelType = Type.GetType("ArenaShooter.FirstPersonViewModel, Assembly-CSharp");
    private static readonly Type PulsePistolAssetType = Type.GetType("ArenaShooter.PulsePistolAsset, Assembly-CSharp");
    private static readonly Type ThemeType = Type.GetType("ArenaShooter.ArenaTheme, Assembly-CSharp");
    private const float GunHardEdgePixels = 0.55f;
    private const float GunGlowPixels = 0.55f;
    private const float GunIntensity = 1.45f;
    private const float GunGlowStrength = 0.0f;
    private const float GunHardEdgeStrength = 1.12f;
    private const float GunEdgeStrength = 0.9f;
    private const float GunNormalEdgeThreshold = 0.08f;
    private const float GunDistantGlowScale = 0.20f;
    private const float PistolHardEdgePixels = 0.50f;
    private const float PistolGlowPixels = 0.50f;
    private const float PistolNormalEdgeStrength = 0.12f;
    private const float PistolNormalEdgeThreshold = 0.18f;
    private const float PistolSightNormalEdgeStrength = 0.0f;
    private const string GlowLensNameToken = "glow lens";
    private const string SightNameToken = "sight";
    private const string FrontSightBlockNameToken = "front squared compensator";
    private static readonly string[] KnownIronSightObjectNames =
    {
        "CPP_rear sight center square post",
        "CPP_rear sight left ear",
        "CPP_rear sight right ear",
        "CPP_front squared compensator"
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

    [Test]
    public void GunOutlineStyleMatchesSilhouetteAndDetailEdges()
    {
        var style = CreateGunReferenceStyle();
        var alphaEdgeStrength = GetStyleField<float>(style, "AlphaEdgeStrength");
        var normalEdgeStrength = GetStyleField<float>(style, "NormalEdgeStrength");

        Assert.That(alphaEdgeStrength, Is.EqualTo(GunEdgeStrength).Within(0.0001f));
        Assert.That(normalEdgeStrength, Is.EqualTo(GunEdgeStrength).Within(0.0001f));
        Assert.That(alphaEdgeStrength, Is.EqualTo(normalEdgeStrength).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "HardEdgePixels"), Is.EqualTo(GunHardEdgePixels).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "GlowPixels"), Is.EqualTo(GunGlowPixels).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "GlowStrength"), Is.EqualTo(GunGlowStrength).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "HardEdgeStrength"), Is.EqualTo(GunHardEdgeStrength).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "Intensity"), Is.EqualTo(GunIntensity).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "NormalEdgeThreshold"), Is.EqualTo(GunNormalEdgeThreshold).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "DistantGlowScale"), Is.EqualTo(GunDistantGlowScale).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "HardEdgePixels"), Is.GreaterThanOrEqualTo(0.5f));
        Assert.That(GetStyleField<float>(style, "GlowPixels"), Is.GreaterThanOrEqualTo(0.5f));
        Assert.That(GetStyleField<float>(style, "Intensity"), Is.GreaterThan(1.3f));
        Assert.That(GetStyleField<float>(style, "GlowStrength"), Is.Zero);
        Assert.That(GetStyleField<float>(style, "NormalEdgeThreshold"), Is.LessThanOrEqualTo(0.1f));
    }

    [Test]
    public void FirstPersonPistolOutlineStyleKeepsSilhouetteButRestrainsDetailEdges()
    {
        var style = CreateReferenceStyle(GetFirstPersonPistolRenderingLayer());
        var alphaEdgeStrength = GetStyleField<float>(style, "AlphaEdgeStrength");
        var normalEdgeStrength = GetStyleField<float>(style, "NormalEdgeStrength");

        Assert.That(GetStyleField<float>(style, "HardEdgePixels"), Is.EqualTo(PistolHardEdgePixels).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "GlowPixels"), Is.EqualTo(PistolGlowPixels).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "GlowStrength"), Is.EqualTo(GunGlowStrength).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "HardEdgeStrength"), Is.EqualTo(GunHardEdgeStrength).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "Intensity"), Is.EqualTo(GunIntensity).Within(0.0001f));
        Assert.That(alphaEdgeStrength, Is.EqualTo(GunEdgeStrength).Within(0.0001f));
        Assert.That(normalEdgeStrength, Is.EqualTo(PistolNormalEdgeStrength).Within(0.0001f));
        Assert.That(normalEdgeStrength, Is.LessThan(alphaEdgeStrength));
        Assert.That(GetStyleField<float>(style, "NormalEdgeThreshold"), Is.EqualTo(PistolNormalEdgeThreshold).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "DistantGlowScale"), Is.EqualTo(GunDistantGlowScale).Within(0.0001f));
    }

    [Test]
    public void FirstPersonPistolSightOutlineStyleUsesOutsideOnlySilhouette()
    {
        var style = CreateReferenceStyle(GetFirstPersonPistolSightRenderingLayer());

        Assert.That(GetStyleField<float>(style, "HardEdgePixels"), Is.EqualTo(PistolHardEdgePixels).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "GlowPixels"), Is.EqualTo(PistolGlowPixels).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "GlowStrength"), Is.EqualTo(GunGlowStrength).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "HardEdgeStrength"), Is.EqualTo(GunHardEdgeStrength).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "Intensity"), Is.EqualTo(GunIntensity).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "AlphaEdgeStrength"), Is.EqualTo(GunEdgeStrength).Within(0.0001f));
        Assert.That(GetStyleField<float>(style, "NormalEdgeStrength"), Is.EqualTo(PistolSightNormalEdgeStrength).Within(0.0001f));
        Assert.That(GetStyleField<bool>(style, "OutsideOnlyAlphaEdge"), Is.True);
        Assert.That(GetStyleField<float>(style, "NormalEdgeThreshold"), Is.EqualTo(PistolNormalEdgeThreshold).Within(0.0001f));
    }

    [Test]
    public void GunOutlineColorRemainsCyan()
    {
        var color = ResolveGunOutlineColor();
        var pistolColor = ResolveOutlineColor("FirstPersonPistol");
        var pistolSightColor = ResolveOutlineColor("FirstPersonPistolSight");

        Assert.That(color.b, Is.GreaterThan(color.r * 8f));
        Assert.That(color.g, Is.GreaterThan(color.r * 5f));
        Assert.That(color.a, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(pistolColor, Is.EqualTo(color));
        Assert.That(pistolSightColor, Is.EqualTo(color));
    }

    [Test]
    public void ImportedViewModelUsesFirstPersonPistolOutlineWhileSuppressingGlowLensAndSights()
    {
        Assert.That(ViewModelType, Is.Not.Null);
        Assert.That(ThemeType, Is.Not.Null);
        var camera = new GameObject("Pulse pistol outline camera");
        roots.Add(camera);
        var viewModel = camera.AddComponent(ViewModelType);
        var theme = Activator.CreateInstance(ThemeType);
        ViewModelType.GetMethod("Build", PublicInstance).Invoke(viewModel, new[] { camera.transform, theme });

        var lens = FindDeepChild(camera.transform, "CPP_muzzle blue glow lens");
        var knownIronSightRenderers = new List<Renderer>();
        foreach (var sightName in KnownIronSightObjectNames)
        {
            var sight = FindDeepChild(camera.transform, sightName);
            if (sight == null)
            {
                Assert.Inconclusive("Imported CyberPulsePistolView resource was not available in this test environment.");
            }

            knownIronSightRenderers.Add(FindRenderer(sight));
        }

        if (lens == null)
        {
            Assert.Inconclusive("Imported CyberPulsePistolView resource was not available in this test environment.");
        }

        var lensMask = FindRenderer(lens).renderingLayerMask;
        var sightRenderers = FindSightRenderers(camera.transform);
        var outlinedRenderer = FindRendererWithLayer(camera.transform, GetFirstPersonPistolRenderingLayer());
        var bodyMaterial = outlinedRenderer.sharedMaterial;
        Assert.That(sightRenderers, Is.Not.Empty);
        Assert.That(bodyMaterial, Is.Not.Null);
        Assert.That((lensMask & GetGunRenderingLayer()), Is.EqualTo(0u));
        Assert.That((lensMask & GetFirstPersonPistolRenderingLayer()), Is.EqualTo(0u));
        Assert.That((lensMask & GetFirstPersonPistolSightRenderingLayer()), Is.EqualTo(0u));
        Assert.That((lensMask & GetDefaultRenderingLayer()), Is.Not.EqualTo(0u));
        Assert.That((lensMask & GetFirstPersonWeaponOccluderRenderingLayer()), Is.Not.EqualTo(0u));

        foreach (var sightRenderer in sightRenderers)
        {
            Assert.That((sightRenderer.renderingLayerMask & GetGunRenderingLayer()), Is.EqualTo(0u), sightRenderer.name);
            Assert.That((sightRenderer.renderingLayerMask & GetFirstPersonPistolRenderingLayer()), Is.EqualTo(0u), sightRenderer.name);
            Assert.That((sightRenderer.renderingLayerMask & GetFirstPersonPistolSightRenderingLayer()), Is.Not.EqualTo(0u), sightRenderer.name);
            Assert.That((sightRenderer.renderingLayerMask & GetDefaultRenderingLayer()), Is.EqualTo(0u), sightRenderer.name);
            Assert.That((sightRenderer.renderingLayerMask & GetFirstPersonWeaponOccluderRenderingLayer()), Is.Not.EqualTo(0u), sightRenderer.name);

            // neon accents on the sights (e.g. the fiber-optic style cyan glow dot) keep the
            // sight outline band but intentionally use an emissive theme material
            if (RendererMetadataContains(sightRenderer, camera.transform, "glow") ||
                RendererMetadataContains(sightRenderer, camera.transform, "cyan"))
            {
                continue;
            }

            foreach (var material in sightRenderer.sharedMaterials)
            {
                Assert.That(material, Is.SameAs(bodyMaterial), sightRenderer.name);
            }
        }

        foreach (var knownIronSightRenderer in knownIronSightRenderers)
        {
            Assert.That(sightRenderers, Does.Contain(knownIronSightRenderer), knownIronSightRenderer.name);
        }

        Assert.That(IsIronSightRenderer(outlinedRenderer, camera.transform), Is.False, outlinedRenderer.name);
    }

    [Test]
    public void PickupModelKeepsSharedGunOutlineLayer()
    {
        Assert.That(PulsePistolAssetType, Is.Not.Null);
        Assert.That(ThemeType, Is.Not.Null);
        var root = new GameObject("Pulse pistol pickup outline test");
        roots.Add(root);
        var theme = Activator.CreateInstance(ThemeType);
        var method = PulsePistolAssetType.GetMethod("TryBuildPickupModel", PublicStatic);
        Assert.That(method, Is.Not.Null);
        var built = (bool)method.Invoke(null, new[] { root.transform, theme });
        if (!built)
        {
            Assert.Inconclusive("Imported CyberPulsePistol resource was not available in this test environment.");
        }

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        Assert.That(renderers, Is.Not.Empty);
        var foundOutlinedPickupRenderer = false;
        foreach (var renderer in renderers)
        {
            if (RendererMetadataContains(renderer, root.transform, GlowLensNameToken))
            {
                Assert.That((renderer.renderingLayerMask & GetGunRenderingLayer()), Is.EqualTo(0u), renderer.name);
                Assert.That((renderer.renderingLayerMask & GetDefaultRenderingLayer()), Is.Not.EqualTo(0u), renderer.name);
            }
            else
            {
                foundOutlinedPickupRenderer = true;
                Assert.That((renderer.renderingLayerMask & GetGunRenderingLayer()), Is.Not.EqualTo(0u), renderer.name);
            }

            Assert.That((renderer.renderingLayerMask & GetFirstPersonPistolRenderingLayer()), Is.EqualTo(0u), renderer.name);
            Assert.That((renderer.renderingLayerMask & GetFirstPersonPistolSightRenderingLayer()), Is.EqualTo(0u), renderer.name);
            Assert.That((renderer.renderingLayerMask & GetFirstPersonWeaponOccluderRenderingLayer()), Is.EqualTo(0u), renderer.name);
        }

        Assert.That(foundOutlinedPickupRenderer, Is.True);
    }

    [TestCase("Assets/Settings/PC_Renderer.asset")]
    [TestCase("Assets/Settings/Mobile_Renderer.asset")]
    public void SerializedRendererAssetsKeepSharedGunOutlineUnchanged(string rendererAssetPath)
    {
        var text = File.ReadAllText(rendererAssetPath);
        var gunBand = ExtractSerializedBand(text, "Gun Cyan");
        var alphaEdgeStrength = ReadSerializedFloat(gunBand, "alphaEdgeStrength");
        var normalEdgeStrength = ReadSerializedFloat(gunBand, "normalEdgeStrength");

        Assert.That(alphaEdgeStrength, Is.EqualTo(GunEdgeStrength).Within(0.0001f));
        Assert.That(normalEdgeStrength, Is.EqualTo(GunEdgeStrength).Within(0.0001f));
        Assert.That(alphaEdgeStrength, Is.EqualTo(normalEdgeStrength).Within(0.0001f));
        Assert.That(ReadSerializedFloat(gunBand, "hardEdgePixels"), Is.EqualTo(GunHardEdgePixels).Within(0.0001f));
        Assert.That(ReadSerializedFloat(gunBand, "glowPixels"), Is.EqualTo(GunGlowPixels).Within(0.0001f));
        Assert.That(ReadSerializedFloat(gunBand, "intensity"), Is.EqualTo(GunIntensity).Within(0.0001f));
        Assert.That(ReadSerializedFloat(gunBand, "glowStrength"), Is.EqualTo(GunGlowStrength).Within(0.0001f));
        Assert.That(ReadSerializedFloat(gunBand, "hardEdgeStrength"), Is.EqualTo(GunHardEdgeStrength).Within(0.0001f));
        Assert.That(ReadSerializedFloat(gunBand, "normalEdgeThreshold"), Is.EqualTo(GunNormalEdgeThreshold).Within(0.0001f));
        Assert.That(ReadSerializedFloat(gunBand, "distantGlowScale"), Is.EqualTo(GunDistantGlowScale).Within(0.0001f));
        Assert.That(ReadSerializedFloat(gunBand, "hardEdgePixels"), Is.GreaterThanOrEqualTo(0.5f));
        Assert.That(ReadSerializedFloat(gunBand, "glowPixels"), Is.GreaterThanOrEqualTo(0.5f));
        Assert.That(ReadSerializedFloat(gunBand, "intensity"), Is.GreaterThan(1.3f));
        Assert.That(ReadSerializedFloat(gunBand, "glowStrength"), Is.Zero);
        Assert.That(ReadSerializedFloat(gunBand, "normalEdgeThreshold"), Is.LessThanOrEqualTo(0.1f));
        Assert.That(text, Does.Contain("suppressWorldOutlinesBehindFirstPersonWeapon: 1"));
    }

    [TestCase("Assets/Settings/PC_Renderer.asset")]
    [TestCase("Assets/Settings/Mobile_Renderer.asset")]
    public void SerializedRendererAssetsIncludeFirstPersonPistolOutlineBand(string rendererAssetPath)
    {
        var text = File.ReadAllText(rendererAssetPath);
        var pistolBand = ExtractSerializedBand(text, "First Person Pistol Cyan");

        Assert.That(ReadSerializedFloat(pistolBand, "renderingLayerMask"), Is.EqualTo(GetFirstPersonPistolRenderingLayer()).Within(0.0001f));
        Assert.That(ReadSerializedFloat(pistolBand, "hardEdgePixels"), Is.EqualTo(PistolHardEdgePixels).Within(0.0001f));
        Assert.That(ReadSerializedFloat(pistolBand, "glowPixels"), Is.EqualTo(PistolGlowPixels).Within(0.0001f));
        Assert.That(ReadSerializedFloat(pistolBand, "intensity"), Is.EqualTo(GunIntensity).Within(0.0001f));
        Assert.That(ReadSerializedFloat(pistolBand, "glowStrength"), Is.EqualTo(GunGlowStrength).Within(0.0001f));
        Assert.That(ReadSerializedFloat(pistolBand, "hardEdgeStrength"), Is.EqualTo(GunHardEdgeStrength).Within(0.0001f));
        Assert.That(ReadSerializedFloat(pistolBand, "alphaEdgeStrength"), Is.EqualTo(GunEdgeStrength).Within(0.0001f));
        Assert.That(ReadSerializedFloat(pistolBand, "normalEdgeStrength"), Is.EqualTo(PistolNormalEdgeStrength).Within(0.0001f));
        Assert.That(ReadSerializedFloat(pistolBand, "normalEdgeThreshold"), Is.EqualTo(PistolNormalEdgeThreshold).Within(0.0001f));
        Assert.That(ReadSerializedFloat(pistolBand, "distantGlowScale"), Is.EqualTo(GunDistantGlowScale).Within(0.0001f));
    }

    [TestCase("Assets/Settings/PC_Renderer.asset")]
    [TestCase("Assets/Settings/Mobile_Renderer.asset")]
    public void SerializedRendererAssetsIncludeFirstPersonPistolSightOutlineBand(string rendererAssetPath)
    {
        var text = File.ReadAllText(rendererAssetPath);
        var sightBand = ExtractSerializedBand(text, "First Person Pistol Sight Cyan");

        Assert.That(ReadSerializedFloat(sightBand, "renderingLayerMask"), Is.EqualTo(GetFirstPersonPistolSightRenderingLayer()).Within(0.0001f));
        Assert.That(ReadSerializedFloat(sightBand, "hardEdgePixels"), Is.EqualTo(PistolHardEdgePixels).Within(0.0001f));
        Assert.That(ReadSerializedFloat(sightBand, "glowPixels"), Is.EqualTo(PistolGlowPixels).Within(0.0001f));
        Assert.That(ReadSerializedFloat(sightBand, "intensity"), Is.EqualTo(GunIntensity).Within(0.0001f));
        Assert.That(ReadSerializedFloat(sightBand, "glowStrength"), Is.EqualTo(GunGlowStrength).Within(0.0001f));
        Assert.That(ReadSerializedFloat(sightBand, "hardEdgeStrength"), Is.EqualTo(GunHardEdgeStrength).Within(0.0001f));
        Assert.That(ReadSerializedFloat(sightBand, "alphaEdgeStrength"), Is.EqualTo(GunEdgeStrength).Within(0.0001f));
        Assert.That(ReadSerializedFloat(sightBand, "normalEdgeStrength"), Is.EqualTo(PistolSightNormalEdgeStrength).Within(0.0001f));
        Assert.That(ReadSerializedBool(sightBand, "outsideOnlyAlphaEdge"), Is.True);
        Assert.That(ReadSerializedFloat(sightBand, "normalEdgeThreshold"), Is.EqualTo(PistolNormalEdgeThreshold).Within(0.0001f));
        Assert.That(ReadSerializedFloat(sightBand, "distantGlowScale"), Is.EqualTo(GunDistantGlowScale).Within(0.0001f));
    }

    [Test]
    public void CompositePassKeepsPointSampledGunOutlineRadiiVisible()
    {
        const string sourcePath = "Assets/Scripts/ArenaShooter/Rendering/DroidOutlineRendererFeature.cs";
        var source = File.ReadAllText(sourcePath);

        Assert.That(source, Does.Contain("Mathf.Max(0.5f, style.HardEdgePixels)"));
        Assert.That(source, Does.Contain("Mathf.Max(0.5f, style.GlowPixels)"));
        Assert.That(source, Does.Not.Contain("Mathf.Max(0.05f, style.HardEdgePixels)"));
        Assert.That(source, Does.Not.Contain("Mathf.Max(0.05f, style.GlowPixels)"));
        Assert.That(source, Does.Not.Contain("Mathf.Max(0.25f, style.HardEdgePixels)"));
        Assert.That(source, Does.Not.Contain("Mathf.Max(0.25f, style.GlowPixels)"));
    }

    [Test]
    public void DefaultOutlineSettingsSuppressWorldOutlinesBehindFirstPersonWeapon()
    {
        Assert.That(OutlineFeatureType, Is.Not.Null);
        var settingsType = OutlineFeatureType.GetNestedType("OutlineSettings", BindingFlags.Public);
        Assert.That(settingsType, Is.Not.Null);
        var settings = Activator.CreateInstance(settingsType);
        var field = settingsType.GetField("suppressWorldOutlinesBehindFirstPersonWeapon", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(field, Is.Not.Null);
        Assert.That((bool)field.GetValue(settings), Is.True);
    }

    [Test]
    public void WeaponOccluderSuppressionSkipsStructuralAndFirstPersonBandsOnly()
    {
        var settings = CreateOutlineSettings();
        var wallBand = FindDefaultBand(GetWallRenderingLayer());
        var droidBand = FindDefaultBand(GetDroidRenderingLayer());
        var medicalBand = FindDefaultBand(GetMedicalRenderingLayer());
        var ammoBand = FindDefaultBand(GetAmmoRenderingLayer());
        var gunBand = FindDefaultBand(GetGunRenderingLayer());
        var pistolBand = FindDefaultBand(GetFirstPersonPistolRenderingLayer());
        var pistolSightBand = FindDefaultBand(GetFirstPersonPistolSightRenderingLayer());

        Assert.That(InvokeShouldSuppressByWeaponOccluder(settings, wallBand, false), Is.False);
        Assert.That(InvokeShouldSuppressByWeaponOccluder(settings, wallBand, true), Is.False);
        Assert.That(InvokeShouldSuppressByWeaponOccluder(settings, droidBand, false), Is.False);
        Assert.That(InvokeShouldSuppressByWeaponOccluder(settings, droidBand, true), Is.True);
        Assert.That(InvokeShouldSuppressByWeaponOccluder(settings, medicalBand, true), Is.True);
        Assert.That(InvokeShouldSuppressByWeaponOccluder(settings, ammoBand, true), Is.True);
        Assert.That(InvokeShouldSuppressByWeaponOccluder(settings, gunBand, true), Is.False);
        Assert.That(InvokeShouldSuppressByWeaponOccluder(settings, pistolBand, true), Is.False);
        Assert.That(InvokeShouldSuppressByWeaponOccluder(settings, pistolSightBand, true), Is.False);

        SetPublicField(settings, "suppressWorldOutlinesBehindFirstPersonWeapon", false);
        Assert.That(InvokeShouldSuppressByWeaponOccluder(settings, droidBand, true), Is.False);
    }

    [Test]
    public void CompositeShaderSuppressesFirstPersonWeaponOutlineNeighborhood()
    {
        const string shaderPath = "Assets/Shaders/DroidOutlineComposite.shader";
        var source = File.ReadAllText(shaderPath);

        Assert.That(source, Does.Contain("WeaponOccluderTouchesRadius(float2 uv, float radius)"));
        Assert.That(source, Does.Contain("WeaponOccluderTouchesOutlineNeighborhood(float2 uv)"));
        Assert.That(source, Does.Contain("WeaponOccluderTouchesRadius(uv, _OutlineParams.x + 1.0)"));
        Assert.That(source, Does.Contain("half weaponOccupancy = WeaponOccluderTouchesOutlineNeighborhood(uv);"));
        Assert.That(source, Does.Contain("half outsideOnlyAlphaEdge = (1.0h - centerOccupancy) * sampleOccupancy;"));
        Assert.That(source, Does.Contain("half alphaEdge = _OutsideOnlyAlphaEdge != 0 ? outsideOnlyAlphaEdge : twoSidedAlphaEdge;"));
        Assert.That(source, Does.Not.Contain("half weaponOccupancy = Occupancy(SAMPLE_TEXTURE2D_X(_DroidOutlineWeaponOccluderTex"));
    }

    [Test]
    public void RenderGraphMaskTexturesArePublishedOnlyAfterValidityChecks()
    {
        const string sourcePath = "Assets/Scripts/ArenaShooter/Rendering/DroidOutlineRendererFeature.cs";
        var source = File.ReadAllText(sourcePath);
        var validityCheck = source.IndexOf("!destination.IsValid() || !resourceData.activeDepthTexture.IsValid()", StringComparison.Ordinal);
        var setWeapon = source.IndexOf("textureData.SetWeaponOccluderTexture(destination)", StringComparison.Ordinal);
        var setMask = source.IndexOf("textureData.SetMaskTexture(bandIndex, destination)", StringComparison.Ordinal);

        Assert.That(validityCheck, Is.GreaterThanOrEqualTo(0));
        Assert.That(setWeapon, Is.GreaterThan(validityCheck));
        Assert.That(setMask, Is.GreaterThan(validityCheck));
    }

    private static object CreateGunReferenceStyle()
    {
        return CreateReferenceStyle(GetGunRenderingLayer());
    }

    private static object CreateReferenceStyle(uint renderingLayerMask)
    {
        Assert.That(OutlineFeatureType, Is.Not.Null);
        var method = OutlineFeatureType.GetMethod("CreateReferenceStyle", PrivateStatic);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(null, new object[] { renderingLayerMask, null });
    }

    private static object CreateOutlineSettings()
    {
        Assert.That(OutlineFeatureType, Is.Not.Null);
        var settingsType = OutlineFeatureType.GetNestedType("OutlineSettings", BindingFlags.Public);
        Assert.That(settingsType, Is.Not.Null);
        return Activator.CreateInstance(settingsType);
    }

    private static object FindDefaultBand(uint renderingLayerMask)
    {
        Assert.That(OutlineFeatureType, Is.Not.Null);
        var method = OutlineFeatureType.GetMethod("CreateDefaultBands", PublicStatic);
        Assert.That(method, Is.Not.Null);
        var bands = (Array)method.Invoke(null, Array.Empty<object>());
        foreach (var band in bands)
        {
            if (GetStyleField<uint>(band, "renderingLayerMask") == renderingLayerMask)
            {
                return band;
            }
        }

        Assert.Fail("Missing outline band for mask " + renderingLayerMask);
        return null;
    }

    private static bool InvokeShouldSuppressByWeaponOccluder(object settings, object band, bool maskProduced)
    {
        Assert.That(OutlineFeatureType, Is.Not.Null);
        var method = OutlineFeatureType.GetMethod("ShouldSuppressByWeaponOccluder", PrivateStatic);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(null, new[] { settings, band, maskProduced });
    }

    private static void SetPublicField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private static T GetStyleField<T>(object style, string fieldName)
    {
        var field = style.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, fieldName);
        return (T)field.GetValue(style);
    }

    private static Color ResolveGunOutlineColor()
    {
        return ResolveOutlineColor("Gun");
    }

    private static Color ResolveOutlineColor(string categoryName)
    {
        Assert.That(RenderSetupType, Is.Not.Null);
        Assert.That(OutlineCategoryType, Is.Not.Null);
        var method = RenderSetupType.GetMethod("ResolveOutlineColor", PublicStatic);
        Assert.That(method, Is.Not.Null);
        var category = Enum.Parse(OutlineCategoryType, categoryName);
        return (Color)method.Invoke(null, new[] { category });
    }

    private static uint GetGunRenderingLayer()
    {
        Assert.That(RenderSetupType, Is.Not.Null);
        var field = RenderSetupType.GetField("GunRenderingLayer", PublicStatic);
        Assert.That(field, Is.Not.Null);
        return (uint)field.GetRawConstantValue();
    }

    private static uint GetFirstPersonPistolRenderingLayer()
    {
        Assert.That(RenderSetupType, Is.Not.Null);
        var field = RenderSetupType.GetField("FirstPersonPistolRenderingLayer", PublicStatic);
        Assert.That(field, Is.Not.Null);
        return (uint)field.GetRawConstantValue();
    }

    private static uint GetFirstPersonPistolSightRenderingLayer()
    {
        Assert.That(RenderSetupType, Is.Not.Null);
        var field = RenderSetupType.GetField("FirstPersonPistolSightRenderingLayer", PublicStatic);
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

    private static uint GetDroidRenderingLayer()
    {
        Assert.That(RenderSetupType, Is.Not.Null);
        var field = RenderSetupType.GetField("DroidRenderingLayer", PublicStatic);
        Assert.That(field, Is.Not.Null);
        return (uint)field.GetRawConstantValue();
    }

    private static uint GetMedicalRenderingLayer()
    {
        Assert.That(RenderSetupType, Is.Not.Null);
        var field = RenderSetupType.GetField("MedicalRenderingLayer", PublicStatic);
        Assert.That(field, Is.Not.Null);
        return (uint)field.GetRawConstantValue();
    }

    private static uint GetAmmoRenderingLayer()
    {
        Assert.That(RenderSetupType, Is.Not.Null);
        var field = RenderSetupType.GetField("AmmoRenderingLayer", PublicStatic);
        Assert.That(field, Is.Not.Null);
        return (uint)field.GetRawConstantValue();
    }

    private static uint GetDefaultRenderingLayer()
    {
        Assert.That(RenderSetupType, Is.Not.Null);
        var field = RenderSetupType.GetField("DefaultRenderingLayer", PublicStatic);
        Assert.That(field, Is.Not.Null);
        return (uint)field.GetRawConstantValue();
    }

    private static uint GetFirstPersonWeaponOccluderRenderingLayer()
    {
        Assert.That(RenderSetupType, Is.Not.Null);
        var field = RenderSetupType.GetField("FirstPersonWeaponOccluderRenderingLayer", PublicStatic);
        Assert.That(field, Is.Not.Null);
        return (uint)field.GetRawConstantValue();
    }

    private static string ExtractSerializedBand(string text, string bandName)
    {
        var start = text.IndexOf("- name: " + bandName, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        var nextBand = text.IndexOf("    - name:", start + 1, StringComparison.Ordinal);
        var settingsEnd = text.IndexOf("    thicknessPixels:", start, StringComparison.Ordinal);
        var end = nextBand >= 0 && nextBand < settingsEnd ? nextBand : settingsEnd;
        Assert.That(end, Is.GreaterThan(start));
        return text.Substring(start, end - start);
    }

    private static float ReadSerializedFloat(string yamlBlock, string key)
    {
        var marker = key + ":";
        foreach (var line in yamlBlock.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith(marker, StringComparison.Ordinal))
            {
                continue;
            }

            var value = trimmed.Substring(marker.Length).Trim();
            return float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        Assert.Fail("Missing serialized key: " + key);
        return 0f;
    }

    private static bool ReadSerializedBool(string yamlBlock, string key)
    {
        return ReadSerializedFloat(yamlBlock, key) > 0.5f;
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                return child;
            }

            var found = FindDeepChild(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Renderer FindRenderer(Transform transform)
    {
        var renderer = transform.GetComponent<Renderer>();
        if (renderer == null)
        {
            renderer = transform.GetComponentInChildren<Renderer>(true);
        }

        Assert.That(renderer, Is.Not.Null, transform.name);
        return renderer;
    }

    private static Renderer FindRendererWithLayer(Transform root, uint renderingLayer)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null &&
                (renderer.renderingLayerMask & renderingLayer) != 0 &&
                !IsIronSightRenderer(renderer, root) &&
                !RendererMetadataContains(renderer, root, GlowLensNameToken))
            {
                return renderer;
            }
        }

        Assert.Fail("Missing non-sight renderer with layer " + renderingLayer);
        return null;
    }

    private static List<Renderer> FindSightRenderers(Transform root)
    {
        var renderers = new List<Renderer>();
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (IsIronSightRenderer(renderer, root))
            {
                renderers.Add(renderer);
            }
        }

        return renderers;
    }

    private static bool IsIronSightRenderer(Renderer renderer, Transform root)
    {
        return RendererMetadataContains(renderer, root, SightNameToken) ||
            RendererMetadataContains(renderer, root, FrontSightBlockNameToken);
    }

    private static bool RendererMetadataContains(Renderer renderer, Transform root, string token)
    {
        if (renderer == null)
        {
            return false;
        }

        if (TransformHierarchyContains(renderer.transform, root, token) ||
            RendererMeshNameContains(renderer, token))
        {
            return true;
        }

        foreach (var material in renderer.sharedMaterials)
        {
            if (NameContains(material != null ? material.name : null, token))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TransformHierarchyContains(Transform transform, Transform root, string token)
    {
        var current = transform;
        while (current != null)
        {
            if (NameContains(current.name, token))
            {
                return true;
            }

            if (current == root)
            {
                break;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool RendererMeshNameContains(Renderer renderer, string token)
    {
        if (renderer is SkinnedMeshRenderer skinnedRenderer &&
            NameContains(skinnedRenderer.sharedMesh != null ? skinnedRenderer.sharedMesh.name : null, token))
        {
            return true;
        }

        var meshFilter = renderer.GetComponent<MeshFilter>();
        return meshFilter != null &&
            NameContains(meshFilter.sharedMesh != null ? meshFilter.sharedMesh.name : null, token);
    }

    private static bool NameContains(string name, string token)
    {
        return !string.IsNullOrEmpty(name) &&
            name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

}
