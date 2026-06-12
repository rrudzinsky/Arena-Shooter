using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace ArenaShooter.Rendering
{
    public sealed class DroidOutlineRendererFeature : ScriptableRendererFeature
    {
        public enum OutlineDiagnosticMode
        {
            Off,
            ShowMaskAndEdges,
            ShowCompositeColor
        }

        [System.Serializable]
        public sealed class OutlineSettings
        {
            public OutlineBand[] outlineBands = CreateDefaultBands();
            [Range(1, 4)] public int thicknessPixels = 2;
            [Range(1, 4)] public int glowPixels = 3;
            [Range(1, 4)] public int downsample = 1;
            [Range(0.01f, 0.5f)] public float normalEdgeThreshold = 0.12f;
            [Range(0.1f, 8f)] public float intensity = DroidRenderSetup.DefaultOutlineIntensity;
            [Range(1f, 120f)] public float distanceFadeStart = 24f;
            [Range(2f, 180f)] public float distanceFadeEnd = 70f;
            [Range(0.35f, 1f)] public float distantHardEdgeScale = 0.48f;
            [Range(0.15f, 1f)] public float distantGlowScale = 0.24f;
            public bool useReferenceNeonStyle = true;
            public bool suppressWorldOutlinesBehindFirstPersonWeapon = true;
            public OutlineDiagnosticMode diagnosticMode = OutlineDiagnosticMode.Off;
            public int diagnosticBandIndex = -1;
            public bool diagnosticIgnoreRenderingLayerFilter;
        }

        [System.Serializable]
        public sealed class OutlineBand
        {
            public string name;
            public bool enabled = true;
            public uint renderingLayerMask;
            public Color outlineColor;
            [Range(0f, 4f)] public float hardEdgePixels;
            [Range(0f, 8f)] public float glowPixels;
            [Range(0f, 6f)] public float intensity;
            [Range(0f, 1f)] public float glowStrength;
            [Range(0f, 2f)] public float hardEdgeStrength;
            [Range(-1f, 1f)] public float alphaEdgeStrength = -1f;
            [Range(-1f, 1f)] public float normalEdgeStrength = -1f;
            public bool outsideOnlyAlphaEdge;
            [Range(0f, 0.5f)] public float normalEdgeThreshold;
            [Range(0f, 120f)] public float distanceFadeStart;
            [Range(0f, 180f)] public float distanceFadeEnd;
            [Range(0f, 1f)] public float distantHardEdgeScale;
            [Range(0f, 1f)] public float distantGlowScale;
            [Range(0f, 1f)] public float flowStrength;
            [Range(0f, 200f)] public float flowSpeedPixelsPerSecond;
            [Range(0f, 160f)] public float flowWavelengthPixels;
            [Range(0f, 2f)] public float flowWigglePixels;
            [Range(0f, 4f)] public float flowWiggleSpeed;
            [Range(0f, 60f)] public float flowCloseStartMeters;
            [Range(0f, 80f)] public float flowCloseEndMeters;
            [Range(0f, 2f)] public float flowHotBoost;

            public OutlineBand(string name, uint renderingLayerMask, Color outlineColor)
            {
                this.name = name;
                this.renderingLayerMask = renderingLayerMask;
                this.outlineColor = outlineColor;
            }
        }

        private readonly struct ResolvedOutlineStyle
        {
            public readonly Color Color;
            public readonly float HardEdgePixels;
            public readonly float GlowPixels;
            public readonly float Intensity;
            public readonly float GlowStrength;
            public readonly float HardEdgeStrength;
            public readonly float AlphaEdgeStrength;
            public readonly float NormalEdgeStrength;
            public readonly bool OutsideOnlyAlphaEdge;
            public readonly float NormalEdgeThreshold;
            public readonly float DistanceFadeStart;
            public readonly float DistanceFadeEnd;
            public readonly float DistantHardEdgeScale;
            public readonly float DistantGlowScale;

            public ResolvedOutlineStyle(
                Color color,
                float hardEdgePixels,
                float glowPixels,
                float intensity,
                float glowStrength,
                float hardEdgeStrength,
                float alphaEdgeStrength,
                float normalEdgeStrength,
                bool outsideOnlyAlphaEdge,
                float normalEdgeThreshold,
                float distanceFadeStart,
                float distanceFadeEnd,
                float distantHardEdgeScale,
                float distantGlowScale)
            {
                Color = color;
                HardEdgePixels = hardEdgePixels;
                GlowPixels = glowPixels;
                Intensity = intensity;
                GlowStrength = glowStrength;
                HardEdgeStrength = hardEdgeStrength;
                AlphaEdgeStrength = alphaEdgeStrength;
                NormalEdgeStrength = normalEdgeStrength;
                OutsideOnlyAlphaEdge = outsideOnlyAlphaEdge;
                NormalEdgeThreshold = normalEdgeThreshold;
                DistanceFadeStart = distanceFadeStart;
                DistanceFadeEnd = distanceFadeEnd;
                DistantHardEdgeScale = distantHardEdgeScale;
                DistantGlowScale = distantGlowScale;
            }
        }

        // Liquid "life" energy flowing along the outline. Resolved separately from
        // ResolvedOutlineStyle so reflection-pinned style tests keep their exact shape.
        private readonly struct ResolvedOutlineFlowStyle
        {
            public readonly float Strength;
            public readonly float SpeedPixelsPerSecond;
            public readonly float WavelengthPixels;
            public readonly float WigglePixels;
            public readonly float WiggleSpeed;
            public readonly float CloseStartMeters;
            public readonly float CloseEndMeters;
            public readonly float HotBoost;

            public ResolvedOutlineFlowStyle(
                float strength,
                float speedPixelsPerSecond,
                float wavelengthPixels,
                float wigglePixels,
                float wiggleSpeed,
                float closeStartMeters,
                float closeEndMeters,
                float hotBoost)
            {
                Strength = strength;
                SpeedPixelsPerSecond = speedPixelsPerSecond;
                WavelengthPixels = wavelengthPixels;
                WigglePixels = wigglePixels;
                WiggleSpeed = wiggleSpeed;
                CloseStartMeters = closeStartMeters;
                CloseEndMeters = closeEndMeters;
                HotBoost = hotBoost;
            }

            public bool IsActive =>
                (Strength > 0.001f || WigglePixels > 0.001f) &&
                CloseEndMeters > CloseStartMeters + 0.01f;
        }

        private const string MaskShaderName = "Hidden/ArenaShooter/DroidOutlineMask";
        private const string WallDamageMaskShaderName = "Hidden/ArenaShooter/DroidOutlineWallDamageMask";
        private const string CompositeShaderName = "Hidden/ArenaShooter/DroidOutlineComposite";

        public OutlineSettings settings = new OutlineSettings();

        private Material maskMaterial;
        private Material wallDamageMaskMaterial;
        private Material compositeMaterial;
        private readonly List<DroidMaskPass> maskPasses = new List<DroidMaskPass>();
        private readonly List<DroidCompositePass> compositePasses = new List<DroidCompositePass>();
        private DroidMaskPass weaponOccluderMaskPass;
        private static bool createDiagnosticLogged;
        private static bool rendererSummaryWithObjectsLogged;
        private static readonly HashSet<string> AddPassDiagnosticCameraNames = new HashSet<string>();
        private static readonly HashSet<string> DetailedRendererDiagnosticKeys = new HashSet<string>();

        public sealed class DroidOutlineTextureData : ContextItem
        {
            private TextureHandle[] maskTextures;
            private TextureHandle weaponOccluderTexture;

            public override void Reset()
            {
                weaponOccluderTexture = TextureHandle.nullHandle;
                if (maskTextures == null)
                {
                    return;
                }

                for (var i = 0; i < maskTextures.Length; i++)
                {
                    maskTextures[i] = TextureHandle.nullHandle;
                }
            }

            public void SetMaskTexture(int bandIndex, TextureHandle texture)
            {
                EnsureCapacity(bandIndex + 1);
                maskTextures[bandIndex] = texture;
            }

            public TextureHandle GetMaskTexture(int bandIndex)
            {
                return maskTextures != null && bandIndex >= 0 && bandIndex < maskTextures.Length
                    ? maskTextures[bandIndex]
                    : TextureHandle.nullHandle;
            }

            public void SetWeaponOccluderTexture(TextureHandle texture)
            {
                weaponOccluderTexture = texture;
            }

            public TextureHandle GetWeaponOccluderTexture()
            {
                return weaponOccluderTexture;
            }

            private void EnsureCapacity(int requiredLength)
            {
                if (maskTextures != null && maskTextures.Length >= requiredLength)
                {
                    return;
                }

                var nextLength = Mathf.Max(requiredLength, maskTextures != null ? maskTextures.Length * 2 : 8);
                var next = new TextureHandle[nextLength];
                for (var i = 0; i < next.Length; i++)
                {
                    next[i] = TextureHandle.nullHandle;
                }

                if (maskTextures != null)
                {
                    for (var i = 0; i < maskTextures.Length; i++)
                    {
                        next[i] = maskTextures[i];
                    }
                }

                maskTextures = next;
            }
        }

        public override void Create()
        {
            EnsureDefaultBands();
            NormalizeOutlineBandStyles();
            var maskShader = Shader.Find(MaskShaderName);
            var wallDamageMaskShader = Shader.Find(WallDamageMaskShaderName);
            var compositeShader = Shader.Find(CompositeShaderName);
            maskMaterial = maskShader != null ? CoreUtils.CreateEngineMaterial(maskShader) : null;
            wallDamageMaskMaterial = wallDamageMaskShader != null ? CoreUtils.CreateEngineMaterial(wallDamageMaskShader) : null;
            compositeMaterial = compositeShader != null ? CoreUtils.CreateEngineMaterial(compositeShader) : null;
            LogCreateDiagnostics(maskShader, wallDamageMaskShader, compositeShader);

            maskPasses.Clear();
            compositePasses.Clear();
            weaponOccluderMaskPass = new DroidMaskPass(
                settings,
                CreateWeaponOccluderBand(),
                -1,
                maskMaterial,
                true)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques
            };

            for (var i = 0; i < settings.outlineBands.Length; i++)
            {
                var band = settings.outlineBands[i];
                var maskPass = new DroidMaskPass(
                    settings,
                    band,
                    i,
                    ResolveMaskMaterial(band))
                {
                    renderPassEvent = RenderPassEvent.AfterRenderingOpaques
                };
                var compositePass = new DroidCompositePass(settings, band, i, compositeMaterial)
                {
                    renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
                };
                maskPasses.Add(maskPass);
                compositePasses.Add(compositePass);
            }
        }

        private Material ResolveMaskMaterial(OutlineBand band)
        {
            if (band != null &&
                band.renderingLayerMask == DroidRenderSetup.WallRenderingLayer &&
                wallDamageMaskMaterial != null)
            {
                return wallDamageMaskMaterial;
            }

            return maskMaterial;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            LogAddPassDiagnostics(renderer, ref renderingData);

            if (compositeMaterial == null ||
                renderingData.cameraData.renderType != CameraRenderType.Base)
            {
                return;
            }

            var useWeaponOccluderMask = ShouldUseWeaponOccluderMask();
            if (useWeaponOccluderMask && weaponOccluderMaskPass != null && weaponOccluderMaskPass.HasMaterial)
            {
                renderer.EnqueuePass(weaponOccluderMaskPass);
            }

            for (var i = 0; i < maskPasses.Count; i++)
            {
                var band = settings.outlineBands[i];
                if (!band.enabled || band.renderingLayerMask == 0 || !maskPasses[i].HasMaterial)
                {
                    continue;
                }

                renderer.EnqueuePass(maskPasses[i]);
                compositePasses[i].Setup(
                    maskPasses[i].MaskTexture,
                    useWeaponOccluderMask
                        ? weaponOccluderMaskPass
                        : null);
                renderer.EnqueuePass(compositePasses[i]);

                if (settings.diagnosticMode != OutlineDiagnosticMode.Off && i == settings.diagnosticBandIndex)
                {
                    break;
                }
            }
        }

        private bool ShouldUseWeaponOccluderMask()
        {
            return settings != null &&
                settings.suppressWorldOutlinesBehindFirstPersonWeapon &&
                maskMaterial != null;
        }

        private static OutlineBand CreateWeaponOccluderBand()
        {
            return new OutlineBand(
                "First Person Weapon Occluder",
                DroidRenderSetup.FirstPersonWeaponOccluderRenderingLayer,
                Color.white);
        }

        private static bool ShouldSuppressByWeaponOccluder(
            OutlineSettings settings,
            OutlineBand band,
            bool weaponOccluderMaskProduced)
        {
            return settings != null &&
                settings.suppressWorldOutlinesBehindFirstPersonWeapon &&
                weaponOccluderMaskProduced &&
                band != null &&
                band.renderingLayerMask != DroidRenderSetup.WallRenderingLayer &&
                band.renderingLayerMask != DroidRenderSetup.GunRenderingLayer &&
                band.renderingLayerMask != DroidRenderSetup.FirstPersonPistolRenderingLayer &&
                band.renderingLayerMask != DroidRenderSetup.FirstPersonPistolSightRenderingLayer;
        }

        private void LogCreateDiagnostics(Shader maskShader, Shader wallDamageMaskShader, Shader compositeShader)
        {
            if (!Application.isPlaying || createDiagnosticLogged)
            {
                return;
            }

            createDiagnosticLogged = true;
            Debug.Log(
                "[Arena Shooter Outline Diagnostics] Feature Create: " +
                $"maskShader={(maskShader != null ? "found" : "MISSING")} " +
                $"wallDamageMaskShader={(wallDamageMaskShader != null ? "found" : "MISSING")} " +
                $"compositeShader={(compositeShader != null ? "found" : "MISSING")} " +
                $"bands={(settings?.outlineBands != null ? settings.outlineBands.Length : 0)} " +
                $"pipeline={(GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "built-in/null")}.");
        }

        private void LogAddPassDiagnostics(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var camera = renderingData.cameraData.camera;
            var cameraName = camera != null ? camera.name : "null";
            if (!AddPassDiagnosticCameraNames.Add(cameraName))
            {
                TryLogRendererCategorySummary("render-pass " + cameraName, false);
                return;
            }

            var reason = "enqueue-ok";
            if (maskMaterial == null)
            {
                reason = "mask-material-null";
            }
            else if (compositeMaterial == null)
            {
                reason = "composite-material-null";
            }
            else if (renderingData.cameraData.renderType != CameraRenderType.Base)
            {
                reason = "non-base-camera";
            }

            Debug.Log(
                "[Arena Shooter Outline Diagnostics] AddRenderPasses: " +
                $"camera={cameraName} " +
                $"renderType={renderingData.cameraData.renderType} " +
                $"cameraType={renderingData.cameraData.cameraType} " +
                $"descriptor={renderingData.cameraData.cameraTargetDescriptor.width}x{renderingData.cameraData.cameraTargetDescriptor.height} " +
                $"cameraTarget={renderingData.cameraData.targetTexture?.name ?? "backbuffer"} " +
                $"renderer={(renderer != null ? renderer.GetType().Name : "null")} " +
                $"maskMaterial={(maskMaterial != null ? "ok" : "null")} " +
                $"wallDamageMaskMaterial={(wallDamageMaskMaterial != null ? "ok" : "null")} " +
                $"compositeMaterial={(compositeMaterial != null ? "ok" : "null")} " +
                $"passPlan={BuildPassPlan()} " +
                $"diagnosticMode={settings.diagnosticMode} " +
                $"diagnosticBandIndex={settings.diagnosticBandIndex} " +
                $"diagnosticIgnoreRenderingLayerFilter={settings.diagnosticIgnoreRenderingLayerFilter} " +
                $"decision={reason}.");

            TryLogRendererCategorySummary("render-pass " + cameraName, false);
            TryLogDetailedRendererDiagnostics(camera, "render-pass " + cameraName);
        }

        private string BuildPassPlan()
        {
            if (settings?.outlineBands == null)
            {
                return "none";
            }

            var plan = string.Empty;
            for (var i = 0; i < settings.outlineBands.Length; i++)
            {
                var band = settings.outlineBands[i];
                var willRun = band.enabled && band.renderingLayerMask != 0 &&
                    (settings.diagnosticMode == OutlineDiagnosticMode.Off || i <= settings.diagnosticBandIndex);
                if (plan.Length > 0)
                {
                    plan += ",";
                }

                plan += $"{i}:{band.name}:{(willRun ? "run" : "skip")}:mask={band.renderingLayerMask}:source={(willRun && IsFirstEnabledBand(i) ? "original-scene" : "prior-composite")}";
            }

            return plan;
        }

        private bool IsFirstEnabledBand(int bandIndex)
        {
            if (settings?.outlineBands == null)
            {
                return false;
            }

            for (var i = 0; i < settings.outlineBands.Length; i++)
            {
                var band = settings.outlineBands[i];
                if (!band.enabled || band.renderingLayerMask == 0)
                {
                    continue;
                }

                return i == bandIndex;
            }

            return false;
        }

        public static void LogRuntimeRendererCategorySummary(string label)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            LogRendererCategorySummary(label, CreateDefaultBands());
        }

        private void TryLogRendererCategorySummary(string label, bool force)
        {
            if (!Application.isPlaying || settings?.outlineBands == null)
            {
                return;
            }

            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (!force && renderers.Length == 0)
            {
                return;
            }

            if (!force && rendererSummaryWithObjectsLogged)
            {
                return;
            }

            if (!force && CountCategorizedRenderers(settings.outlineBands, renderers) == 0)
            {
                LogRendererCategorySummary(label + " empty-pre-match", settings.outlineBands, renderers);
                return;
            }

            rendererSummaryWithObjectsLogged = true;
            LogRendererCategorySummary(label, settings.outlineBands, renderers);
        }

        private static void LogRendererCategorySummary(string label, OutlineBand[] bands)
        {
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            LogRendererCategorySummary(label, bands, renderers);
        }

        private static void LogRendererCategorySummary(string label, OutlineBand[] bands, Renderer[] renderers)
        {
            var summary = $"totalRenderers={renderers.Length}";
            foreach (var band in bands)
            {
                var count = 0;
                foreach (var renderer in renderers)
                {
                    if ((renderer.renderingLayerMask & band.renderingLayerMask) != 0)
                    {
                        count++;
                    }
                }

                summary += $" | {band.name}:{count}";
            }

            Debug.Log("[Arena Shooter Outline Diagnostics] Renderer renderingLayerMask counts (" + label + "): " + summary + ".");
        }

        private static int CountCategorizedRenderers(OutlineBand[] bands, Renderer[] renderers)
        {
            if (bands == null || renderers == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                foreach (var band in bands)
                {
                    if (band != null && (renderer.renderingLayerMask & band.renderingLayerMask) != 0)
                    {
                        count++;
                        break;
                    }
                }
            }

            return count;
        }

        private void TryLogDetailedRendererDiagnostics(Camera camera, string label)
        {
            if (!Application.isPlaying || camera == null || settings?.outlineBands == null)
            {
                return;
            }

            var key = label + "|" + camera.name;
            if (!DetailedRendererDiagnosticKeys.Add(key))
            {
                return;
            }

            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            for (var bandIndex = 0; bandIndex < settings.outlineBands.Length; bandIndex++)
            {
                var band = settings.outlineBands[bandIndex];
                var examples = string.Empty;
                var total = 0;
                var activeEnabled = 0;
                var visibleFlag = 0;
                var frustum = 0;
                var opaqueQueue = 0;
                var transparentQueue = 0;
                var invalidMaterial = 0;

                foreach (var renderer in renderers)
                {
                    if ((renderer.renderingLayerMask & band.renderingLayerMask) == 0)
                    {
                        continue;
                    }

                    total++;
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                    {
                        activeEnabled++;
                    }

                    if (renderer.isVisible)
                    {
                        visibleFlag++;
                    }

                    var isInFrustum = GeometryUtility.TestPlanesAABB(planes, renderer.bounds);
                    if (isInFrustum)
                    {
                        frustum++;
                    }

                    var material = renderer.sharedMaterial;
                    if (material == null)
                    {
                        invalidMaterial++;
                    }
                    else if (material.renderQueue <= 2500)
                    {
                        opaqueQueue++;
                    }
                    else
                    {
                        transparentQueue++;
                    }

                    if (isInFrustum && examples.Length < 900)
                    {
                        examples +=
                            $" [{renderer.name}|active={renderer.gameObject.activeInHierarchy}|enabled={renderer.enabled}|visible={renderer.isVisible}|layerMask={renderer.renderingLayerMask}|queue={(material != null ? material.renderQueue.ToString() : "none")}|shader={(material != null && material.shader != null ? material.shader.name : "none")}|bounds={renderer.bounds.size}]";
                    }
                }

                Debug.Log(
                    "[Arena Shooter Outline Diagnostics] Per-band renderer visibility: " +
                    $"label={label} camera={camera.name} bandIndex={bandIndex} band={band.name} " +
                    $"mask={band.renderingLayerMask} maskName={DroidRenderSetup.DescribeRenderingLayerMask(band.renderingLayerMask)} total={total} activeEnabled={activeEnabled} " +
                    $"rendererIsVisible={visibleFlag} frustumApprox={frustum} opaqueQueue={opaqueQueue} " +
                    $"transparentQueue={transparentQueue} invalidMaterial={invalidMaterial} examples={examples}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            foreach (var pass in maskPasses)
            {
                pass.Dispose();
            }

            foreach (var pass in compositePasses)
            {
                pass.Dispose();
            }

            CoreUtils.Destroy(maskMaterial);
            CoreUtils.Destroy(wallDamageMaskMaterial);
            CoreUtils.Destroy(compositeMaterial);
        }

        public static OutlineBand[] CreateDefaultBands()
        {
            return new[]
            {
                CreateDefaultBand("Floor Violet", DroidRenderSetup.FloorRenderingLayer),
                CreateDefaultBand("Wall Green Teal", DroidRenderSetup.WallRenderingLayer),
                CreateDefaultBand("Droid Amber", DroidRenderSetup.DroidRenderingLayer),
                CreateDefaultBand("Medical Rose", DroidRenderSetup.MedicalRenderingLayer),
                CreateDefaultBand("Ammo Gold", DroidRenderSetup.AmmoRenderingLayer),
                CreateDefaultBand("Gun Cyan", DroidRenderSetup.GunRenderingLayer),
                CreateDefaultBand("First Person Pistol Cyan", DroidRenderSetup.FirstPersonPistolRenderingLayer),
                CreateDefaultBand("First Person Pistol Sight Cyan", DroidRenderSetup.FirstPersonPistolSightRenderingLayer)
            };
        }

        private static OutlineBand CreateDefaultBand(string name, uint renderingLayerMask)
        {
            var style = CreateReferenceStyle(renderingLayerMask, null);
            var flowStyle = CreateReferenceFlowStyle(renderingLayerMask);
            return new OutlineBand(name, renderingLayerMask, style.Color)
            {
                hardEdgePixels = style.HardEdgePixels,
                glowPixels = style.GlowPixels,
                intensity = style.Intensity,
                glowStrength = style.GlowStrength,
                hardEdgeStrength = style.HardEdgeStrength,
                alphaEdgeStrength = style.AlphaEdgeStrength,
                normalEdgeStrength = style.NormalEdgeStrength,
                outsideOnlyAlphaEdge = style.OutsideOnlyAlphaEdge,
                normalEdgeThreshold = style.NormalEdgeThreshold,
                distanceFadeStart = style.DistanceFadeStart,
                distanceFadeEnd = style.DistanceFadeEnd,
                distantHardEdgeScale = style.DistantHardEdgeScale,
                distantGlowScale = style.DistantGlowScale,
                flowStrength = flowStyle.Strength,
                flowSpeedPixelsPerSecond = flowStyle.SpeedPixelsPerSecond,
                flowWavelengthPixels = flowStyle.WavelengthPixels,
                flowWigglePixels = flowStyle.WigglePixels,
                flowWiggleSpeed = flowStyle.WiggleSpeed,
                flowCloseStartMeters = flowStyle.CloseStartMeters,
                flowCloseEndMeters = flowStyle.CloseEndMeters,
                flowHotBoost = flowStyle.HotBoost
            };
        }

        private void EnsureDefaultBands()
        {
            if (settings == null)
            {
                settings = new OutlineSettings();
            }

            if (settings.outlineBands == null || settings.outlineBands.Length == 0)
            {
                settings.outlineBands = CreateDefaultBands();
            }
        }

        private void NormalizeOutlineBandStyles()
        {
            if (settings?.outlineBands == null)
            {
                return;
            }

            if (!settings.useReferenceNeonStyle && UsesLegacyHotNeonColors(settings.outlineBands))
            {
                settings.useReferenceNeonStyle = true;
            }

            for (var i = 0; i < settings.outlineBands.Length; i++)
            {
                var band = settings.outlineBands[i];
                if (band == null)
                {
                    continue;
                }

                if (settings.useReferenceNeonStyle)
                {
                    var reference = CreateReferenceStyle(band.renderingLayerMask, settings);
                    var referenceFlow = CreateReferenceFlowStyle(band.renderingLayerMask);
                    band.outlineColor = reference.Color;
                    band.hardEdgePixels = reference.HardEdgePixels;
                    band.glowPixels = reference.GlowPixels;
                    band.intensity = reference.Intensity;
                    band.glowStrength = reference.GlowStrength;
                    band.hardEdgeStrength = reference.HardEdgeStrength;
                    band.alphaEdgeStrength = reference.AlphaEdgeStrength;
                    band.normalEdgeStrength = reference.NormalEdgeStrength;
                    band.outsideOnlyAlphaEdge = reference.OutsideOnlyAlphaEdge;
                    band.normalEdgeThreshold = reference.NormalEdgeThreshold;
                    band.distanceFadeStart = reference.DistanceFadeStart;
                    band.distanceFadeEnd = reference.DistanceFadeEnd;
                    band.distantHardEdgeScale = reference.DistantHardEdgeScale;
                    band.distantGlowScale = reference.DistantGlowScale;
                    band.flowStrength = referenceFlow.Strength;
                    band.flowSpeedPixelsPerSecond = referenceFlow.SpeedPixelsPerSecond;
                    band.flowWavelengthPixels = referenceFlow.WavelengthPixels;
                    band.flowWigglePixels = referenceFlow.WigglePixels;
                    band.flowWiggleSpeed = referenceFlow.WiggleSpeed;
                    band.flowCloseStartMeters = referenceFlow.CloseStartMeters;
                    band.flowCloseEndMeters = referenceFlow.CloseEndMeters;
                    band.flowHotBoost = referenceFlow.HotBoost;
                    continue;
                }

                var style = ResolveOutlineStyle(settings, band);
                if (band.hardEdgePixels <= 0f)
                {
                    band.hardEdgePixels = style.HardEdgePixels;
                }

                if (band.glowPixels <= 0f)
                {
                    band.glowPixels = style.GlowPixels;
                }

                if (band.intensity <= 0f)
                {
                    band.intensity = style.Intensity;
                }

                if (band.glowStrength <= 0f)
                {
                    band.glowStrength = style.GlowStrength;
                }

                if (band.hardEdgeStrength <= 0f)
                {
                    band.hardEdgeStrength = style.HardEdgeStrength;
                }

                if (band.alphaEdgeStrength < 0f)
                {
                    band.alphaEdgeStrength = style.AlphaEdgeStrength;
                }

                if (band.normalEdgeStrength < 0f)
                {
                    band.normalEdgeStrength = style.NormalEdgeStrength;
                }

                if (band.normalEdgeThreshold <= 0f)
                {
                    band.normalEdgeThreshold = style.NormalEdgeThreshold;
                }

                if (band.distanceFadeStart <= 0f)
                {
                    band.distanceFadeStart = style.DistanceFadeStart;
                }

                if (band.distanceFadeEnd <= 0f)
                {
                    band.distanceFadeEnd = style.DistanceFadeEnd;
                }

                if (band.distantHardEdgeScale <= 0f)
                {
                    band.distantHardEdgeScale = style.DistantHardEdgeScale;
                }

                if (band.distantGlowScale <= 0f)
                {
                    band.distantGlowScale = style.DistantGlowScale;
                }
            }
        }

        private static bool UsesLegacyHotNeonColors(OutlineBand[] bands)
        {
            if (bands == null)
            {
                return false;
            }

            for (var i = 0; i < bands.Length; i++)
            {
                var band = bands[i];
                if (band != null && band.outlineColor.maxColorComponent > 3f)
                {
                    return true;
                }
            }

            return false;
        }

        private static ResolvedOutlineStyle ResolveOutlineStyle(OutlineSettings settings, OutlineBand band)
        {
            var reference = CreateReferenceStyle(band != null ? band.renderingLayerMask : 0u, settings);
            if (settings == null || settings.useReferenceNeonStyle)
            {
                return reference;
            }

            var color = band != null && band.outlineColor.maxColorComponent > 0.001f
                ? band.outlineColor
                : reference.Color;

            return new ResolvedOutlineStyle(
                color,
                band != null && band.hardEdgePixels > 0f ? band.hardEdgePixels : reference.HardEdgePixels,
                band != null && band.glowPixels > 0f ? band.glowPixels : reference.GlowPixels,
                band != null && band.intensity > 0f ? band.intensity : reference.Intensity,
                band != null && band.glowStrength > 0f ? band.glowStrength : reference.GlowStrength,
                band != null && band.hardEdgeStrength > 0f ? band.hardEdgeStrength : reference.HardEdgeStrength,
                band != null && band.alphaEdgeStrength >= 0f ? band.alphaEdgeStrength : reference.AlphaEdgeStrength,
                band != null && band.normalEdgeStrength >= 0f ? band.normalEdgeStrength : reference.NormalEdgeStrength,
                band != null ? band.outsideOnlyAlphaEdge : reference.OutsideOnlyAlphaEdge,
                band != null && band.normalEdgeThreshold > 0f ? band.normalEdgeThreshold : reference.NormalEdgeThreshold,
                band != null && band.distanceFadeStart > 0f ? band.distanceFadeStart : reference.DistanceFadeStart,
                band != null && band.distanceFadeEnd > 0f ? band.distanceFadeEnd : reference.DistanceFadeEnd,
                band != null && band.distantHardEdgeScale > 0f ? band.distantHardEdgeScale : reference.DistantHardEdgeScale,
                band != null && band.distantGlowScale > 0f ? band.distantGlowScale : reference.DistantGlowScale);
        }

        private static ResolvedOutlineStyle CreateReferenceStyle(uint renderingLayerMask, OutlineSettings settings)
        {
            var hard = settings != null ? Mathf.Max(1f, settings.thicknessPixels) : 1.25f;
            var glow = settings != null ? Mathf.Max(1f, settings.glowPixels) : 2.2f;
            var normalThreshold = settings != null ? Mathf.Max(0.01f, settings.normalEdgeThreshold) : 0.14f;
            var fadeStart = settings != null ? Mathf.Max(1f, settings.distanceFadeStart) : 24f;
            var fadeEnd = settings != null ? Mathf.Max(fadeStart + 1f, settings.distanceFadeEnd) : 76f;
            var distantHard = settings != null ? Mathf.Clamp(settings.distantHardEdgeScale, 0.35f, 1f) : 0.58f;
            var distantGlow = settings != null ? Mathf.Clamp(settings.distantGlowScale, 0.15f, 1f) : 0.22f;

            if (renderingLayerMask == DroidRenderSetup.WallRenderingLayer)
            {
                return new ResolvedOutlineStyle(
                    DroidRenderSetup.ResolveOutlineColor(StylizedOutlineCategory.Wall),
                    0.85f,
                    1.0f,
                    0.72f,
                    0.0f,
                    0.85f,
                    0.85f,
                    0.45f,
                    false,
                    0.18f,
                    fadeStart,
                    fadeEnd,
                    0.72f,
                    0.0f);
            }

            if (renderingLayerMask == DroidRenderSetup.FloorRenderingLayer)
            {
                return new ResolvedOutlineStyle(
                    DroidRenderSetup.ResolveOutlineColor(StylizedOutlineCategory.Floor),
                    1.0f,
                    2.0f,
                    0.62f,
                    0.14f,
                    0.92f,
                    1.0f,
                    0.03f,
                    false,
                    0.18f,
                    fadeStart,
                    fadeEnd,
                    0.58f,
                    0.16f);
            }

            if (renderingLayerMask == DroidRenderSetup.DroidRenderingLayer)
            {
                return new ResolvedOutlineStyle(
                    DroidRenderSetup.ResolveOutlineColor(StylizedOutlineCategory.Droid),
                    1.05f,
                    2.0f,
                    0.78f,
                    0.22f,
                    1.0f,
                    1.0f,
                    0.28f,
                    false,
                    0.14f,
                    fadeStart,
                    fadeEnd,
                    0.52f,
                    0.20f);
            }

            if (renderingLayerMask == DroidRenderSetup.MedicalRenderingLayer)
            {
                return new ResolvedOutlineStyle(
                    DroidRenderSetup.ResolveOutlineColor(StylizedOutlineCategory.Medical),
                    1.2f,
                    2.25f,
                    0.82f,
                    0.22f,
                    1.0f,
                    1.0f,
                    0.16f,
                    false,
                    0.15f,
                    fadeStart,
                    fadeEnd,
                    0.52f,
                    0.20f);
            }

            if (renderingLayerMask == DroidRenderSetup.AmmoRenderingLayer)
            {
                return new ResolvedOutlineStyle(
                    DroidRenderSetup.ResolveOutlineColor(StylizedOutlineCategory.Ammo),
                    1.2f,
                    2.25f,
                    0.78f,
                    0.22f,
                    1.0f,
                    1.0f,
                    0.16f,
                    false,
                    0.15f,
                    fadeStart,
                    fadeEnd,
                    0.52f,
                    0.20f);
            }

            if (renderingLayerMask == DroidRenderSetup.GunRenderingLayer)
            {
                return new ResolvedOutlineStyle(
                    DroidRenderSetup.ResolveOutlineColor(StylizedOutlineCategory.Gun),
                    0.55f,
                    0.55f,
                    1.45f,
                    0.0f,
                    1.12f,
                    0.9f,
                    0.9f,
                    false,
                    0.08f,
                    fadeStart,
                    fadeEnd,
                    0.50f,
                    0.20f);
            }

            if (renderingLayerMask == DroidRenderSetup.FirstPersonPistolRenderingLayer)
            {
                return new ResolvedOutlineStyle(
                    DroidRenderSetup.ResolveOutlineColor(StylizedOutlineCategory.FirstPersonPistol),
                    0.50f,
                    0.50f,
                    1.45f,
                    0.0f,
                    1.12f,
                    0.9f,
                    0.12f,
                    false,
                    0.18f,
                    fadeStart,
                    fadeEnd,
                    0.50f,
                    0.20f);
            }

            if (renderingLayerMask == DroidRenderSetup.FirstPersonPistolSightRenderingLayer)
            {
                return new ResolvedOutlineStyle(
                    DroidRenderSetup.ResolveOutlineColor(StylizedOutlineCategory.FirstPersonPistolSight),
                    0.50f,
                    0.50f,
                    1.45f,
                    0.0f,
                    1.12f,
                    0.9f,
                    0.0f,
                    true,
                    0.18f,
                    fadeStart,
                    fadeEnd,
                    0.50f,
                    0.20f);
            }

            return new ResolvedOutlineStyle(
                Color.white,
                hard,
                glow,
                settings != null ? Mathf.Max(0.1f, settings.intensity) : DroidRenderSetup.DefaultOutlineIntensity,
                0.22f,
                    1.0f,
                    1.0f,
                    0.12f,
                    false,
                    normalThreshold,
                fadeStart,
                fadeEnd,
                distantHard,
                distantGlow);
        }

        private static ResolvedOutlineFlowStyle CreateReferenceFlowStyle(uint renderingLayerMask)
        {
            if (renderingLayerMask == DroidRenderSetup.DroidRenderingLayer)
            {
                // The droids' life-blood: liquid gold streaming along their edge lines,
                // with a gentle alive wiggle. A close-range detail by design - full
                // strength inside ~6m, completely static beyond ~14m so distant thin
                // lines never shimmer.
                return new ResolvedOutlineFlowStyle(0.55f, 42f, 36f, 0.9f, 1.25f, 6f, 14f, 0.7f);
            }

            return default;
        }

        private static ResolvedOutlineFlowStyle ResolveOutlineFlowStyle(OutlineSettings settings, OutlineBand band)
        {
            if (settings == null || settings.useReferenceNeonStyle)
            {
                return CreateReferenceFlowStyle(band != null ? band.renderingLayerMask : 0u);
            }

            if (band == null)
            {
                return default;
            }

            return new ResolvedOutlineFlowStyle(
                band.flowStrength,
                band.flowSpeedPixelsPerSecond,
                band.flowWavelengthPixels,
                band.flowWigglePixels,
                band.flowWiggleSpeed,
                band.flowCloseStartMeters,
                band.flowCloseEndMeters,
                band.flowHotBoost);
        }

        private static Vector4 CreateDistanceParams(OutlineSettings settings, OutlineBand band)
        {
            var style = ResolveOutlineStyle(settings, band);
            var start = Mathf.Max(0.01f, style.DistanceFadeStart);
            var end = Mathf.Max(start + 0.01f, style.DistanceFadeEnd);
            var inverseRange = 1f / (end - start);
            var hardScale = Mathf.Clamp(style.DistantHardEdgeScale, 0.35f, 1f);
            var glowScale = Mathf.Clamp(style.DistantGlowScale, 0.15f, 1f);
            return new Vector4(start, inverseRange, hardScale, glowScale);
        }

        private sealed class DroidMaskPass : ScriptableRenderPass
        {
            private static readonly int OutlineDistanceParamsId = Shader.PropertyToID("_OutlineDistanceParams");
            private static readonly int WallDamageClipEnabledId = Shader.PropertyToID("_WallDamageClipEnabled");
            private static readonly int WallDamageStampCountId = Shader.PropertyToID("_WallDamageStampCount");
            private static readonly HashSet<string> LoggedExecuteBands = new HashSet<string>();
            private static readonly HashSet<string> LoggedRenderGraphBands = new HashSet<string>();
            private static readonly HashSet<string> LoggedRenderFuncBands = new HashSet<string>();
            private static readonly HashSet<string> RequestedMaskReadbacks = new HashSet<string>();
            private static readonly List<ShaderTagId> ShaderTagIds = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalGBuffer"),
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit")
            };

            private readonly OutlineSettings settings;
            private readonly OutlineBand band;
            private readonly int bandIndex;
            private readonly Material material;
            private readonly bool storeAsWeaponOccluder;
            private readonly ProfilingSampler sampler;
            private RTHandle maskTexture;
            private bool maskProducedThisFrame;

            public RTHandle MaskTexture => maskTexture;
            public bool HasMaterial => material != null;
            public bool HasProducedMask => maskProducedThisFrame && maskTexture != null && maskTexture.rt != null;

            public DroidMaskPass(
                OutlineSettings settings,
                OutlineBand band,
                int bandIndex,
                Material material,
                bool storeAsWeaponOccluder = false)
            {
                this.settings = settings;
                this.band = band;
                this.bandIndex = bandIndex;
                this.material = material;
                this.storeAsWeaponOccluder = storeAsWeaponOccluder;
                sampler = new ProfilingSampler($"{band.name} Outline Mask");
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

#pragma warning disable 0618, 0672
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                maskProducedThisFrame = false;
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                descriptor.graphicsFormat = GetMaskGraphicsFormat();

                RenderingUtils.ReAllocateIfNeeded(
                    ref maskTexture,
                    descriptor,
                    FilterMode.Point,
                    TextureWrapMode.Clamp,
                    name: $"_{band.name.Replace(" ", string.Empty)}OutlineMaskTex");

                ConfigureTarget(maskTexture, renderingData.cameraData.renderer.cameraDepthTargetHandle);
                ConfigureClear(ClearFlag.Color, Color.clear);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (material == null)
                {
                    return;
                }

                LogExecuteDiagnostic(renderingData.cameraData.camera);

                var cameraData = renderingData.cameraData;
                var filteringSettings = CreateFilteringSettings(band.renderingLayerMask);
                var drawingSettings = RenderingUtils.CreateDrawingSettings(ShaderTagIds, ref renderingData, cameraData.defaultOpaqueSortFlags);
                drawingSettings.overrideMaterial = material;
                drawingSettings.perObjectData = PerObjectData.None;
                drawingSettings.enableDynamicBatching = true;

                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, sampler))
                {
                    var distanceParams = CreateDistanceParams(settings, band);
                    material.SetVector(OutlineDistanceParamsId, distanceParams);
                    ResetWallDamageClipGlobals(cmd);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                    context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
                maskProducedThisFrame = maskTexture != null && maskTexture.rt != null;
            }
#pragma warning restore 0618, 0672

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null)
                {
                    return;
                }

                var renderingData = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var lightData = frameData.Get<UniversalLightData>();
                var resourceData = frameData.Get<UniversalResourceData>();
                var textureData = frameData.GetOrCreate<DroidOutlineTextureData>();
                LogRenderGraphDiagnostic(cameraData.camera);

                var descriptor = cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                descriptor.graphicsFormat = GetMaskGraphicsFormat();

                var destination = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    descriptor,
                    $"_{band.name.Replace(" ", string.Empty)}OutlineMaskTex",
                    true,
                    FilterMode.Point);

                if (!destination.IsValid() || !resourceData.activeDepthTexture.IsValid())
                {
                    return;
                }

                if (storeAsWeaponOccluder)
                {
                    textureData.SetWeaponOccluderTexture(destination);
                }
                else
                {
                    textureData.SetMaskTexture(bandIndex, destination);
                }

                using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>($"{band.name} Outline Mask", out var passData, sampler))
                {
                    passData.cameraName = cameraData.camera != null ? cameraData.camera.name : "null";
                    passData.bandName = band.name;
                    passData.bandIndex = bandIndex;
                    passData.renderingLayerMask = band.renderingLayerMask;
                    passData.distanceParams = CreateDistanceParams(settings, band);
                    var drawSettings = RenderingUtils.CreateDrawingSettings(
                        ShaderTagIds,
                        renderingData,
                        cameraData,
                        lightData,
                        cameraData.defaultOpaqueSortFlags);
                    drawSettings.overrideMaterial = material;
                    drawSettings.perObjectData = PerObjectData.None;
                    drawSettings.enableDynamicBatching = true;

                    var ignoredRenderingLayerFilter = settings.diagnosticMode != OutlineDiagnosticMode.Off &&
                        settings.diagnosticIgnoreRenderingLayerFilter;
                    var filteringSettings = CreateFilteringSettings(band.renderingLayerMask, ignoredRenderingLayerFilter);
                    var rendererListParams = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
                    rendererListParams.filteringSettings.batchLayerMask = uint.MaxValue;

                    passData.rendererList = renderGraph.CreateRendererList(rendererListParams);

                    passData.ignoredRenderingLayerFilter = ignoredRenderingLayerFilter;
                    builder.UseRendererList(passData.rendererList);

                    builder.SetRenderAttachment(destination, 0, AccessFlags.WriteAll);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((MaskPassData data, RasterGraphContext context) =>
                    {
                        LogRenderFuncDiagnostic(data.cameraName, data.bandName, data.renderingLayerMask, data.ignoredRenderingLayerFilter);
                        context.cmd.SetGlobalVector(OutlineDistanceParamsId, data.distanceParams);
                        ResetWallDamageClipGlobals(context.cmd);
                        context.cmd.ClearRenderTarget(false, true, Color.clear);
                        context.cmd.DrawRendererList(data.rendererList);
                    });
                }

                AddMaskReadbackDiagnostic(renderGraph, destination, cameraData.camera, descriptor.width, descriptor.height);
            }

            private sealed class MaskPassData
            {
                public RendererListHandle rendererList;
                public string cameraName;
                public string bandName;
                public int bandIndex;
                public uint renderingLayerMask;
                public Vector4 distanceParams;
                public bool ignoredRenderingLayerFilter;
            }

            private sealed class MaskReadbackPassData
            {
                public TextureHandle texture;
                public NativeArray<byte> pixels;
                public string cameraName;
                public string bandName;
                public int width;
                public int height;
            }

            private GraphicsFormat GetMaskGraphicsFormat()
            {
                return GraphicsFormat.R8G8B8A8_UNorm;
            }

            private static FilteringSettings CreateFilteringSettings(uint renderingLayerMask, bool ignoreRenderingLayerFilter = false)
            {
                var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, -1);
                filteringSettings.renderingLayerMask = ignoreRenderingLayerFilter ? uint.MaxValue : renderingLayerMask;
                return filteringSettings;
            }

            private static void ResetWallDamageClipGlobals(CommandBuffer cmd)
            {
                cmd.SetGlobalInt(WallDamageClipEnabledId, 0);
                cmd.SetGlobalInt(WallDamageStampCountId, 0);
            }

            private static void ResetWallDamageClipGlobals(RasterCommandBuffer cmd)
            {
                cmd.SetGlobalInt(WallDamageClipEnabledId, 0);
                cmd.SetGlobalInt(WallDamageStampCountId, 0);
            }

            public void Dispose()
            {
                maskTexture?.Release();
            }

            private void LogExecuteDiagnostic(Camera camera)
            {
                if (!Application.isPlaying || LoggedExecuteBands.Contains(band.name))
                {
                    return;
                }

                LoggedExecuteBands.Add(band.name);
                Debug.Log(
                    "[Arena Shooter Outline Diagnostics] Mask Execute: " +
                    $"band={band.name} mask={band.renderingLayerMask} " +
                    $"camera={(camera != null ? camera.name : "null")} " +
                    $"maskTexture={(maskTexture != null ? $"{maskTexture.rt.width}x{maskTexture.rt.height}" : "null")}.");
            }

            private void LogRenderGraphDiagnostic(Camera camera)
            {
                var cameraName = camera != null ? camera.name : "null";
                var key = cameraName + "|" + band.name;
                if (!Application.isPlaying || LoggedRenderGraphBands.Contains(key))
                {
                    return;
                }

                LoggedRenderGraphBands.Add(key);
                Debug.Log(
                    "[Arena Shooter Outline Diagnostics] Mask RecordRenderGraph: " +
                    $"bandIndex={bandIndex} band={band.name} mask={band.renderingLayerMask} " +
                    $"camera={cameraName}.");
            }

            private static void LogRenderFuncDiagnostic(string cameraName, string bandName, uint renderingLayerMask, bool ignoredRenderingLayerFilter)
            {
                var key = cameraName + "|" + bandName;
                if (!Application.isPlaying || LoggedRenderFuncBands.Contains(key))
                {
                    return;
                }

                LoggedRenderFuncBands.Add(key);
                Debug.Log(
                    "[Arena Shooter Outline Diagnostics] Mask RenderFunc reached: " +
                    $"camera={cameraName} band={bandName} mask={renderingLayerMask} " +
                    $"ignoredRenderingLayerFilter={ignoredRenderingLayerFilter}.");
            }

            private void AddMaskReadbackDiagnostic(RenderGraph renderGraph, TextureHandle mask, Camera camera, int width, int height)
            {
                var cameraName = camera != null ? camera.name : "null";
                var key = cameraName + "|" + band.name;
                if (!Application.isPlaying ||
                    settings == null ||
                    settings.diagnosticMode == OutlineDiagnosticMode.Off ||
                    !mask.IsValid() ||
                    width <= 0 ||
                    height <= 0)
                {
                    return;
                }

                if (!HasActiveRendererForBand())
                {
                    return;
                }

                if (!RequestedMaskReadbacks.Add(key))
                {
                    return;
                }

                var pixels = new NativeArray<byte>(width * height * 4, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                using (var builder = renderGraph.AddUnsafePass<MaskReadbackPassData>($"{band.name} Mask Readback Diagnostic", out var passData))
                {
                    passData.texture = mask;
                    passData.pixels = pixels;
                    passData.cameraName = cameraName;
                    passData.bandName = band.name;
                    passData.width = width;
                    passData.height = height;

                    builder.UseTexture(mask, AccessFlags.Read);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((MaskReadbackPassData data, UnsafeGraphContext context) =>
                    {
                        context.cmd.RequestAsyncReadbackIntoNativeArray(
                            ref data.pixels,
                            data.texture,
                            0,
                            GraphicsFormat.R8G8B8A8_UNorm,
                            request => LogMaskReadbackResult(request, data));
                    });
                }
            }

            private bool HasActiveRendererForBand()
            {
                var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var renderer in renderers)
                {
                    if (renderer != null &&
                        renderer.enabled &&
                        renderer.gameObject.activeInHierarchy &&
                        (renderer.renderingLayerMask & band.renderingLayerMask) != 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static void LogMaskReadbackResult(AsyncGPUReadbackRequest request, MaskReadbackPassData data)
            {
                try
                {
                    if (request.hasError)
                    {
                        Debug.LogWarning(
                            "[Arena Shooter Outline Diagnostics] Mask GPU readback failed: " +
                            $"camera={data.cameraName} band={data.bandName} size={data.width}x{data.height}.");
                        return;
                    }

                    var nonZeroAlpha = 0;
                    var strongAlpha = 0;
                    var maxAlpha = 0;
                    var firstX = -1;
                    var firstY = -1;
                    var minX = data.width;
                    var minY = data.height;
                    var maxX = -1;
                    var maxY = -1;
                    for (var pixel = 0; pixel < data.width * data.height; pixel++)
                    {
                        var alpha = data.pixels[pixel * 4 + 3];
                        if (alpha <= 0)
                        {
                            continue;
                        }

                        nonZeroAlpha++;
                        if (alpha > 128)
                        {
                            strongAlpha++;
                        }

                        if (alpha > maxAlpha)
                        {
                            maxAlpha = alpha;
                        }

                        if (firstX < 0)
                        {
                            firstX = pixel % data.width;
                            firstY = pixel / data.width;
                        }

                        var x = pixel % data.width;
                        var y = pixel / data.width;
                        if (x < minX)
                        {
                            minX = x;
                        }

                        if (y < minY)
                        {
                            minY = y;
                        }

                        if (x > maxX)
                        {
                            maxX = x;
                        }

                        if (y > maxY)
                        {
                            maxY = y;
                        }
                    }

                    var coverage = data.width > 0 && data.height > 0
                        ? (nonZeroAlpha / (float)(data.width * data.height))
                        : 0f;
                    var bounds = nonZeroAlpha > 0
                        ? $"bounds=({minX},{minY})-({maxX},{maxY})"
                        : "bounds=empty";
                    Debug.Log(
                        "[Arena Shooter Outline Diagnostics] Mask GPU readback result: " +
                        $"camera={data.cameraName} band={data.bandName} size={data.width}x{data.height} " +
                        $"nonZeroAlpha={nonZeroAlpha} strongAlpha={strongAlpha} maxAlpha={maxAlpha} " +
                        $"coverage={coverage:P4} firstPixel=({firstX},{firstY}) {bounds}.");
                }
                finally
                {
                    if (data.pixels.IsCreated)
                    {
                        data.pixels.Dispose();
                    }
                }
            }
        }

        private sealed class DroidCompositePass : ScriptableRenderPass
        {
            private static readonly HashSet<string> LoggedExecuteBands = new HashSet<string>();
            private static readonly HashSet<string> LoggedRenderGraphBands = new HashSet<string>();
            private static readonly HashSet<string> LoggedRenderFuncBands = new HashSet<string>();
            private static readonly HashSet<string> LoggedResourceStateBands = new HashSet<string>();
            private static readonly HashSet<string> LoggedWriteTargetBands = new HashSet<string>();
            private static readonly HashSet<string> RequestedSourceReadbacks = new HashSet<string>();
            private static readonly int MaskTextureId = Shader.PropertyToID("_DroidOutlineMaskTex");
            private static readonly int MaskTexelSizeId = Shader.PropertyToID("_DroidOutlineMaskTexelSize");
            private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
            private static readonly int OutlineParamsId = Shader.PropertyToID("_OutlineParams");
            private static readonly int OutlineStyleParamsId = Shader.PropertyToID("_OutlineStyleParams");
            private static readonly int OutlineDistanceParamsId = Shader.PropertyToID("_OutlineDistanceParams");
            private static readonly int OutlineFlowParamsId = Shader.PropertyToID("_OutlineFlowParams");
            private static readonly int OutlineFlowParams2Id = Shader.PropertyToID("_OutlineFlowParams2");
            private static readonly int FlowEnabledId = Shader.PropertyToID("_FlowEnabled");
            private static readonly int WeaponOccluderTextureId = Shader.PropertyToID("_DroidOutlineWeaponOccluderTex");
            private static readonly int SuppressByWeaponOccluderId = Shader.PropertyToID("_SuppressByWeaponOccluder");
            private static readonly int OutsideOnlyAlphaEdgeId = Shader.PropertyToID("_OutsideOnlyAlphaEdge");
            private static readonly int DiagnosticModeId = Shader.PropertyToID("_DiagnosticMode");
            private static readonly int ApplyMatteSceneId = Shader.PropertyToID("_ApplyMatteScene");

            private readonly OutlineSettings settings;
            private readonly OutlineBand band;
            private readonly int bandIndex;
            private readonly Material material;
            private readonly ProfilingSampler sampler;
            private RTHandle maskTexture;
            private DroidMaskPass weaponOccluderMaskPass;
            private RTHandle copyTexture;

            public DroidCompositePass(OutlineSettings settings, OutlineBand band, int bandIndex, Material material)
            {
                this.settings = settings;
                this.band = band;
                this.bandIndex = bandIndex;
                this.material = material;
                sampler = new ProfilingSampler($"{band.name} Outline Composite");
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
            }

            public void Setup(RTHandle sourceMask, DroidMaskPass sourceWeaponOccluderMaskPass)
            {
                maskTexture = sourceMask;
                weaponOccluderMaskPass = sourceWeaponOccluderMaskPass;
            }

#pragma warning disable 0618, 0672
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;

                RenderingUtils.ReAllocateIfNeeded(
                    ref copyTexture,
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: $"_{band.name.Replace(" ", string.Empty)}OutlineCompositeCopy");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (material == null || maskTexture == null)
                {
                    return;
                }

                LogExecuteDiagnostic(renderingData.cameraData.camera);

                var cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, sampler))
                {
                    var maskRt = maskTexture.rt;
                    var style = ResolveOutlineStyle(settings, band);
                    material.SetTexture(MaskTextureId, maskTexture);
                    material.SetVector(MaskTexelSizeId, new Vector4(1f / maskRt.width, 1f / maskRt.height, maskRt.width, maskRt.height));
                    material.SetColor(OutlineColorId, style.Color);
                    material.SetInt(DiagnosticModeId, (int)settings.diagnosticMode);
                    material.SetInt(ApplyMatteSceneId, IsFirstEnabledBand() ? 1 : 0);
                    material.SetInt(OutsideOnlyAlphaEdgeId, style.OutsideOnlyAlphaEdge ? 1 : 0);
                    var weaponOccluderMask = GetProducedWeaponOccluderMask();
                    var suppressByWeapon = ShouldSuppressByWeaponOccluder(settings, band, weaponOccluderMask != null);
                    material.SetInt(SuppressByWeaponOccluderId, suppressByWeapon ? 1 : 0);
                    if (suppressByWeapon)
                    {
                        material.SetTexture(WeaponOccluderTextureId, weaponOccluderMask);
                    }

                    material.SetVector(
                        OutlineParamsId,
                        new Vector4(
                            Mathf.Max(0.5f, style.HardEdgePixels),
                            Mathf.Max(0.5f, style.GlowPixels),
                            Mathf.Max(0.01f, style.NormalEdgeThreshold),
                            Mathf.Max(0.1f, style.Intensity)));
                    material.SetVector(
                        OutlineStyleParamsId,
                        new Vector4(
                            Mathf.Clamp01(style.NormalEdgeStrength),
                            Mathf.Clamp01(style.GlowStrength),
                            Mathf.Max(0f, style.HardEdgeStrength),
                            Mathf.Clamp01(style.AlphaEdgeStrength)));
                    material.SetVector(OutlineDistanceParamsId, CreateDistanceParams(settings, band));
                    var flowStyle = ResolveOutlineFlowStyle(settings, band);
                    material.SetVector(
                        OutlineFlowParamsId,
                        new Vector4(
                            flowStyle.Strength,
                            flowStyle.SpeedPixelsPerSecond,
                            flowStyle.WavelengthPixels,
                            flowStyle.WigglePixels));
                    material.SetVector(
                        OutlineFlowParams2Id,
                        new Vector4(
                            flowStyle.WiggleSpeed,
                            flowStyle.HotBoost,
                            flowStyle.CloseStartMeters,
                            flowStyle.CloseEndMeters));
                    material.SetInt(FlowEnabledId, flowStyle.IsActive ? 1 : 0);

                    Blitter.BlitCameraTexture(cmd, cameraColor, copyTexture);
                    Blitter.BlitCameraTexture(cmd, copyTexture, cameraColor, material, 0);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#pragma warning restore 0618, 0672

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null)
                {
                    return;
                }

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var textureData = frameData.GetOrCreate<DroidOutlineTextureData>();
                var activeColor = resourceData.activeColorTexture;
                var mask = textureData.GetMaskTexture(bandIndex);
                var weaponOccluderMask = textureData.GetWeaponOccluderTexture();
                LogRenderGraphDiagnostic(cameraData.camera, activeColor.IsValid(), mask.IsValid());
                if (!activeColor.IsValid() || !mask.IsValid())
                {
                    return;
                }

                var sourceDescriptor = renderGraph.GetTextureDesc(activeColor);
                sourceDescriptor.name = $"_{band.name.Replace(" ", string.Empty)}OutlineCompositeSourceCopy";
                sourceDescriptor.clearBuffer = false;
                var sourceCopy = renderGraph.CreateTexture(sourceDescriptor);
                renderGraph.AddBlitPass(activeColor, sourceCopy, Vector2.one, Vector2.zero, passName: $"{band.name} Outline Source Copy");

                var destination = activeColor;

                LogRenderGraphResourceState(
                    renderGraph,
                    cameraData.camera,
                    sourceCopy,
                    mask,
                    destination,
                    resourceData.isActiveTargetBackBuffer);
                AddCompositeSourceReadbackDiagnostic(renderGraph, sourceCopy, cameraData.camera, IsFirstEnabledBand());

                if (!destination.IsValid())
                {
                    return;
                }

                using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>($"{band.name} Outline Composite", out var passData, sampler))
                {
                    var maskInfo = renderGraph.GetRenderTargetInfo(mask);
                    passData.cameraName = cameraData.camera != null ? cameraData.camera.name : "null";
                    passData.bandName = band.name;
                    passData.material = material;
                    passData.source = sourceCopy;
                    passData.mask = mask;
                    passData.sourceValid = sourceCopy.IsValid();
                    passData.maskValid = mask.IsValid();
                    passData.weaponOccluderMask = weaponOccluderMask;
                    passData.weaponOccluderValid = weaponOccluderMask.IsValid();
                    passData.maskTexelSize = new Vector4(1f / maskInfo.width, 1f / maskInfo.height, maskInfo.width, maskInfo.height);
                    var style = ResolveOutlineStyle(settings, band);
                    passData.outlineColor = style.Color;
                    passData.diagnosticMode = (int)settings.diagnosticMode;
                    passData.applyMatteScene = IsFirstEnabledBand() ? 1 : 0;
                    passData.outsideOnlyAlphaEdge = style.OutsideOnlyAlphaEdge ? 1 : 0;
                    passData.suppressByWeaponOccluder = ShouldSuppressByWeaponOccluder(settings, band, weaponOccluderMask.IsValid()) ? 1 : 0;
                    passData.outlineParams = new Vector4(
                        Mathf.Max(0.5f, style.HardEdgePixels),
                        Mathf.Max(0.5f, style.GlowPixels),
                        Mathf.Max(0.01f, style.NormalEdgeThreshold),
                        Mathf.Max(0.1f, style.Intensity));
                    passData.styleParams = new Vector4(
                        Mathf.Clamp01(style.NormalEdgeStrength),
                        Mathf.Clamp01(style.GlowStrength),
                        Mathf.Max(0f, style.HardEdgeStrength),
                        Mathf.Clamp01(style.AlphaEdgeStrength));
                    passData.distanceParams = CreateDistanceParams(settings, band);
                    var flowStyle = ResolveOutlineFlowStyle(settings, band);
                    passData.flowParams = new Vector4(
                        flowStyle.Strength,
                        flowStyle.SpeedPixelsPerSecond,
                        flowStyle.WavelengthPixels,
                        flowStyle.WigglePixels);
                    passData.flowParams2 = new Vector4(
                        flowStyle.WiggleSpeed,
                        flowStyle.HotBoost,
                        flowStyle.CloseStartMeters,
                        flowStyle.CloseEndMeters);
                    passData.flowEnabled = flowStyle.IsActive ? 1 : 0;

                    builder.UseTexture(sourceCopy, AccessFlags.Read);
                    builder.UseTexture(mask, AccessFlags.Read);
                    if (passData.suppressByWeaponOccluder != 0)
                    {
                        builder.UseTexture(weaponOccluderMask, AccessFlags.Read);
                    }

                    if (resourceData.cameraDepthTexture.IsValid())
                    {
                        builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    }

                    builder.SetRenderAttachment(destination, 0, AccessFlags.WriteAll);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((CompositePassData data, RasterGraphContext context) =>
                    {
                        LogRenderFuncDiagnostic(data.cameraName, data.bandName, data.diagnosticMode, data.sourceValid, data.maskValid);
                        context.cmd.SetGlobalTexture(MaskTextureId, data.mask);
                        context.cmd.SetGlobalVector(MaskTexelSizeId, data.maskTexelSize);
                        context.cmd.SetGlobalColor(OutlineColorId, data.outlineColor);
                        context.cmd.SetGlobalInt(DiagnosticModeId, data.diagnosticMode);
                        context.cmd.SetGlobalInt(ApplyMatteSceneId, data.applyMatteScene);
                        context.cmd.SetGlobalInt(OutsideOnlyAlphaEdgeId, data.outsideOnlyAlphaEdge);
                        context.cmd.SetGlobalInt(SuppressByWeaponOccluderId, data.suppressByWeaponOccluder);
                        if (data.suppressByWeaponOccluder != 0 && data.weaponOccluderValid)
                        {
                            context.cmd.SetGlobalTexture(WeaponOccluderTextureId, data.weaponOccluderMask);
                        }

                        context.cmd.SetGlobalVector(OutlineParamsId, data.outlineParams);
                        context.cmd.SetGlobalVector(OutlineStyleParamsId, data.styleParams);
                        context.cmd.SetGlobalVector(OutlineDistanceParamsId, data.distanceParams);
                        context.cmd.SetGlobalVector(OutlineFlowParamsId, data.flowParams);
                        context.cmd.SetGlobalVector(OutlineFlowParams2Id, data.flowParams2);
                        context.cmd.SetGlobalInt(FlowEnabledId, data.flowEnabled);
                        Blitter.BlitTexture(context.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), data.material, 0);
                    });
                }

                LogCameraColorAssignment(cameraData.camera, band.name, resourceData.isActiveTargetBackBuffer);
            }

            private sealed class CompositePassData
            {
                public string cameraName;
                public string bandName;
                public Material material;
                public TextureHandle source;
                public TextureHandle mask;
                public TextureHandle weaponOccluderMask;
                public Vector4 maskTexelSize;
                public Color outlineColor;
                public Vector4 outlineParams;
                public Vector4 styleParams;
                public Vector4 distanceParams;
                public Vector4 flowParams;
                public Vector4 flowParams2;
                public int flowEnabled;
                public int diagnosticMode;
                public int applyMatteScene;
                public int outsideOnlyAlphaEdge;
                public int suppressByWeaponOccluder;
                public bool sourceValid;
                public bool maskValid;
                public bool weaponOccluderValid;
            }

            private sealed class SourceReadbackPassData
            {
                public TextureHandle texture;
                public NativeArray<byte> pixels;
                public string cameraName;
                public string bandName;
                public int width;
                public int height;
                public bool applyMatteScene;
            }

            public void Dispose()
            {
                copyTexture?.Release();
            }

            private void LogExecuteDiagnostic(Camera camera)
            {
                if (!Application.isPlaying || LoggedExecuteBands.Contains(band.name))
                {
                    return;
                }

                LoggedExecuteBands.Add(band.name);
                Debug.Log(
                    "[Arena Shooter Outline Diagnostics] Composite Execute: " +
                    $"band={band.name} camera={(camera != null ? camera.name : "null")} " +
                    $"maskTexture={(maskTexture != null ? $"{maskTexture.rt.width}x{maskTexture.rt.height}" : "null")} " +
                    $"copyTexture={(copyTexture != null ? $"{copyTexture.rt.width}x{copyTexture.rt.height}" : "null")}.");
            }

            private bool IsFirstEnabledBand()
            {
                if (settings?.outlineBands == null)
                {
                    return true;
                }

                foreach (var candidate in settings.outlineBands)
                {
                    if (!candidate.enabled || candidate.renderingLayerMask == 0)
                    {
                        continue;
                    }

                    return ReferenceEquals(candidate, band);
                }

                return true;
            }

            private RTHandle GetProducedWeaponOccluderMask()
            {
                return weaponOccluderMaskPass != null && weaponOccluderMaskPass.HasProducedMask
                    ? weaponOccluderMaskPass.MaskTexture
                    : null;
            }

            private void LogRenderGraphDiagnostic(Camera camera, bool sourceValid, bool maskValid)
            {
                var cameraName = camera != null ? camera.name : "null";
                var key = cameraName + "|" + band.name;
                if (!Application.isPlaying || LoggedRenderGraphBands.Contains(key))
                {
                    return;
                }

                LoggedRenderGraphBands.Add(key);
                Debug.Log(
                    "[Arena Shooter Outline Diagnostics] Composite RecordRenderGraph: " +
                    $"band={band.name} camera={cameraName} " +
                    $"sourceValid={sourceValid} maskValid={maskValid} " +
                    $"applyMatteScene={IsFirstEnabledBand()}.");
            }

            private static void LogRenderFuncDiagnostic(string cameraName, string bandName, int diagnosticMode, bool sourceValid, bool maskValid)
            {
                var key = cameraName + "|" + bandName;
                if (!Application.isPlaying || LoggedRenderFuncBands.Contains(key))
                {
                    return;
                }

                LoggedRenderFuncBands.Add(key);
                Debug.Log(
                    "[Arena Shooter Outline Diagnostics] Composite RenderFunc reached: " +
                    $"camera={cameraName} band={bandName} diagnosticMode={diagnosticMode} " +
                    $"sourceValidAtRecord={sourceValid} maskValidAtRecord={maskValid}.");
            }

            private void LogRenderGraphResourceState(
                RenderGraph renderGraph,
                Camera camera,
                TextureHandle source,
                TextureHandle mask,
                TextureHandle destination,
                bool isActiveTargetBackBuffer)
            {
                var cameraName = camera != null ? camera.name : "null";
                var key = cameraName + "|" + band.name;
                if (!Application.isPlaying || LoggedResourceStateBands.Contains(key))
                {
                    return;
                }

                LoggedResourceStateBands.Add(key);
                var sourceInfo = source.IsValid() ? renderGraph.GetRenderTargetInfo(source) : default;
                var maskInfo = mask.IsValid() ? renderGraph.GetRenderTargetInfo(mask) : default;
                var destinationInfo = destination.IsValid() ? renderGraph.GetRenderTargetInfo(destination) : default;
                Debug.Log(
                    "[Arena Shooter Outline Diagnostics] Composite resource state: " +
                    $"camera={cameraName} band={band.name} " +
                    $"isActiveTargetBackBuffer={isActiveTargetBackBuffer} " +
                    $"materialPasses={(material != null ? material.passCount : -1)} " +
                    $"shaderSupported={(material != null && material.shader != null ? material.shader.isSupported.ToString() : "no-shader")} " +
                    $"sourceValid={source.IsValid()} source={sourceInfo.width}x{sourceInfo.height} " +
                    $"maskValid={mask.IsValid()} mask={maskInfo.width}x{maskInfo.height} " +
                    $"destinationValid={destination.IsValid()} destination={destinationInfo.width}x{destinationInfo.height} " +
                    $"diagnosticMode={(int)settings.diagnosticMode} " +
                    $"applyMatteScene={IsFirstEnabledBand()} " +
                    $"note={(IsFirstEnabledBand() ? "first enabled band now preserves original scene color and adds outlines" : "later band preserves previous composite")}.");
            }

            private static void LogCameraColorAssignment(Camera camera, string bandName, bool isActiveTargetBackBuffer)
            {
                var cameraName = camera != null ? camera.name : "null";
                var key = cameraName + "|" + bandName;
                if (!Application.isPlaying || LoggedWriteTargetBands.Contains(key))
                {
                    return;
                }

                LoggedWriteTargetBands.Add(key);
                Debug.Log(
                    "[Arena Shooter Outline Diagnostics] Composite writes back into activeColorTexture: " +
                    $"camera={cameraName} band={bandName} " +
                    $"isActiveTargetBackBuffer={isActiveTargetBackBuffer}.");
            }

            private void AddCompositeSourceReadbackDiagnostic(RenderGraph renderGraph, TextureHandle source, Camera camera, bool applyMatteScene)
            {
                var cameraName = camera != null ? camera.name : "null";
                var key = cameraName + "|" + band.name;
                if (!Application.isPlaying ||
                    settings == null ||
                    settings.diagnosticMode == OutlineDiagnosticMode.Off ||
                    !RequestedSourceReadbacks.Add(key) ||
                    !source.IsValid())
                {
                    return;
                }

                var sourceInfo = renderGraph.GetRenderTargetInfo(source);
                var width = sourceInfo.width;
                var height = sourceInfo.height;
                if (width <= 0 || height <= 0)
                {
                    return;
                }

                var pixels = new NativeArray<byte>(width * height * 4, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                using (var builder = renderGraph.AddUnsafePass<SourceReadbackPassData>($"{band.name} Composite Source Readback Diagnostic", out var passData))
                {
                    passData.texture = source;
                    passData.pixels = pixels;
                    passData.cameraName = cameraName;
                    passData.bandName = band.name;
                    passData.width = width;
                    passData.height = height;
                    passData.applyMatteScene = applyMatteScene;

                    builder.UseTexture(source, AccessFlags.Read);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((SourceReadbackPassData data, UnsafeGraphContext context) =>
                    {
                        context.cmd.RequestAsyncReadbackIntoNativeArray(
                            ref data.pixels,
                            data.texture,
                            0,
                            GraphicsFormat.R8G8B8A8_UNorm,
                            request => LogSourceReadbackResult(request, data));
                    });
                }
            }

            private static void LogSourceReadbackResult(AsyncGPUReadbackRequest request, SourceReadbackPassData data)
            {
                try
                {
                    if (request.hasError)
                    {
                        Debug.LogWarning(
                            "[Arena Shooter Outline Diagnostics] Composite source GPU readback failed: " +
                            $"camera={data.cameraName} band={data.bandName} size={data.width}x{data.height}.");
                        return;
                    }

                    var totalPixels = data.width * data.height;
                    var nonBlack = 0;
                    var bright = 0;
                    var neonPreservedByMatte = 0;
                    var magenta = 0;
                    var cyanBlue = 0;
                    var red = 0;
                    var yellow = 0;
                    var alphaNotOpaque = 0;
                    var firstNonBlackX = -1;
                    var firstNonBlackY = -1;
                    long sumR = 0;
                    long sumG = 0;
                    long sumB = 0;

                    for (var pixel = 0; pixel < totalPixels; pixel++)
                    {
                        var offset = pixel * 4;
                        var rByte = data.pixels[offset];
                        var gByte = data.pixels[offset + 1];
                        var bByte = data.pixels[offset + 2];
                        var aByte = data.pixels[offset + 3];
                        sumR += rByte;
                        sumG += gByte;
                        sumB += bByte;

                        if (aByte < 250)
                        {
                            alphaNotOpaque++;
                        }

                        if (rByte > 5 || gByte > 5 || bByte > 5)
                        {
                            nonBlack++;
                            if (firstNonBlackX < 0)
                            {
                                firstNonBlackX = pixel % data.width;
                                firstNonBlackY = pixel / data.width;
                            }
                        }

                        if (rByte > 80 || gByte > 80 || bByte > 80)
                        {
                            bright++;
                        }

                        var r = rByte / 255f;
                        var g = gByte / 255f;
                        var b = bByte / 255f;
                        var isMagenta = r >= 0.16f && b >= 0.12f && r >= g * 1.65f && b >= g * 1.45f;
                        var isCyanBlue = b >= 0.12f && b >= r * 1.25f && b >= g * 0.55f;
                        var isRed = r >= 0.14f && r >= g * 1.4f && r >= b * 1.4f;
                        var isYellow = r >= 0.13f && g >= 0.10f && r >= b * 1.55f && g >= b * 1.35f;

                        if (isMagenta)
                        {
                            magenta++;
                        }

                        if (isCyanBlue)
                        {
                            cyanBlue++;
                        }

                        if (isRed)
                        {
                            red++;
                        }

                        if (isYellow)
                        {
                            yellow++;
                        }

                        if (isMagenta || isCyanBlue || isRed || isYellow)
                        {
                            neonPreservedByMatte++;
                        }
                    }

                    var invTotal = totalPixels > 0 ? 1f / totalPixels : 0f;
                    Debug.Log(
                        "[Arena Shooter Outline Diagnostics] Composite source GPU readback result: " +
                        $"camera={data.cameraName} band={data.bandName} size={data.width}x{data.height} " +
                        $"applyMatteScene={data.applyMatteScene} nonBlack={nonBlack} ({nonBlack * invTotal:P4}) " +
                        $"bright={bright} ({bright * invTotal:P4}) " +
                        $"neonPreservedByMatte={neonPreservedByMatte} ({neonPreservedByMatte * invTotal:P4}) " +
                        $"magenta={magenta} cyanBlue={cyanBlue} red={red} yellow={yellow} " +
                        $"alphaNotOpaque={alphaNotOpaque} avgRgb=({sumR * invTotal:F1},{sumG * invTotal:F1},{sumB * invTotal:F1}) " +
                        $"firstNonBlackPixel=({firstNonBlackX},{firstNonBlackY}) " +
                        $"interpretation={(data.applyMatteScene ? "this is the original scene source for the first outline composite" : "source already contains prior outline composites")}.");
                }
                finally
                {
                    if (data.pixels.IsCreated)
                    {
                        data.pixels.Dispose();
                    }
                }
            }
        }
    }
}
