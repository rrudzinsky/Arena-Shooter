using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace ArenaShooter
{
    public enum StylizedOutlineCategory
    {
        None,
        Floor,
        Wall,
        Droid,
        Medical,
        Ammo,
        Gun
    }

    public static class DroidRenderSetup
    {
        public const string OutlineLayerName = "DroidOutline";
        public const int FallbackOutlineLayer = 6;
        public const uint DefaultRenderingLayer = 1u << 0;
        public const uint FloorRenderingLayer = 1u << 1;
        public const uint WallRenderingLayer = 1u << 2;
        public const uint DroidRenderingLayer = 1u << 3;
        public const uint MedicalRenderingLayer = 1u << 4;
        public const uint AmmoRenderingLayer = 1u << 5;
        public const uint GunRenderingLayer = 1u << 6;
        public const float DefaultOutlineIntensity = 2.35f;
        private const int MaxAssignmentExampleLogsPerCategory = 8;
        private static readonly Dictionary<StylizedOutlineCategory, int> AssignmentCounts = new Dictionary<StylizedOutlineCategory, int>();
        private static readonly HashSet<string> LoggedAssignmentExamples = new HashSet<string>();

        public static int OutlineLayer
        {
            get
            {
                var layer = LayerMask.NameToLayer(OutlineLayerName);
                return layer >= 0 ? layer : FallbackOutlineLayer;
            }
        }

        public static void Apply(GameObject root)
        {
            Apply(root, StylizedOutlineCategory.Droid);
        }

        public static void Apply(GameObject root, StylizedOutlineCategory category)
        {
            if (root == null)
            {
                return;
            }

            var renderingLayerMask = ResolveRenderingLayerMask(category);
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                Apply(renderer, renderingLayerMask);
            }

            LogAssignment(root, category, renderingLayerMask, renderers);
        }

        public static void ApplyRenderer(Renderer renderer, StylizedOutlineCategory category)
        {
            var renderingLayerMask = ResolveRenderingLayerMask(category);
            Apply(renderer, renderingLayerMask);
            LogAssignment(renderer != null ? renderer.gameObject : null, category, renderingLayerMask, renderer != null ? new[] { renderer } : null);
        }

        public static uint ResolveRenderingLayerMask(StylizedOutlineCategory category)
        {
            return category switch
            {
                StylizedOutlineCategory.Floor => FloorRenderingLayer,
                StylizedOutlineCategory.Wall => WallRenderingLayer,
                StylizedOutlineCategory.Droid => DroidRenderingLayer,
                StylizedOutlineCategory.Medical => MedicalRenderingLayer,
                StylizedOutlineCategory.Ammo => AmmoRenderingLayer,
                StylizedOutlineCategory.Gun => GunRenderingLayer,
                _ => DefaultRenderingLayer
            };
        }

        public static Color ResolveOutlineColor(StylizedOutlineCategory category)
        {
            return category switch
            {
                StylizedOutlineCategory.Floor => new Color(4.2f, 0.04f, 3.1f, 1f),
                StylizedOutlineCategory.Wall => new Color(0.12f, 0.26f, 1.85f, 1f),
                StylizedOutlineCategory.Droid => new Color(1f, 0.64f, 0.08f, 1f),
                StylizedOutlineCategory.Medical => new Color(4.2f, 0.15f, 0.08f, 1f),
                StylizedOutlineCategory.Ammo => new Color(3.6f, 2.8f, 0.08f, 1f),
                StylizedOutlineCategory.Gun => new Color(0.1f, 2.6f, 3.8f, 1f),
                _ => Color.clear
            };
        }

        public static Color ResolveEffectiveOutlineColor(StylizedOutlineCategory category, float intensity = DefaultOutlineIntensity)
        {
            var color = ResolveOutlineColor(category);
            return new Color(color.r * intensity, color.g * intensity, color.b * intensity, color.a);
        }

        public static string DescribeRenderingLayerMask(uint renderingLayerMask)
        {
            if (renderingLayerMask == FloorRenderingLayer)
            {
                return "Floor";
            }

            if (renderingLayerMask == WallRenderingLayer)
            {
                return "Wall";
            }

            if (renderingLayerMask == DroidRenderingLayer)
            {
                return "Droid";
            }

            if (renderingLayerMask == MedicalRenderingLayer)
            {
                return "Medical";
            }

            if (renderingLayerMask == AmmoRenderingLayer)
            {
                return "Ammo";
            }

            if (renderingLayerMask == GunRenderingLayer)
            {
                return "Gun";
            }

            if (renderingLayerMask == DefaultRenderingLayer)
            {
                return "Default";
            }

            return "Mixed/Custom";
        }

        private static void Apply(Renderer renderer, uint renderingLayerMask)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.renderingLayerMask = renderingLayerMask;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void LogAssignment(GameObject root, StylizedOutlineCategory category, uint renderingLayerMask, Renderer[] renderers)
        {
            if (!Application.isPlaying || root == null || renderers == null)
            {
                return;
            }

            AssignmentCounts.TryGetValue(category, out var categoryCount);
            categoryCount++;
            AssignmentCounts[category] = categoryCount;

            var key = $"{category}|{root.name}|{renderers.Length}|{categoryCount <= MaxAssignmentExampleLogsPerCategory}";
            var shouldLog = categoryCount <= MaxAssignmentExampleLogsPerCategory || categoryCount == 25 || categoryCount == 100 || categoryCount % 250 == 0;
            if (!shouldLog || !LoggedAssignmentExamples.Add(key))
            {
                return;
            }

            var activeRenderers = 0;
            var enabledRenderers = 0;
            var zeroBounds = 0;
            var missingMaterials = 0;
            var transparentMaterials = 0;
            var bounds = default(Bounds);
            var hasBounds = false;
            var examples = string.Empty;

            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (renderer.gameObject.activeInHierarchy)
                {
                    activeRenderers++;
                }

                if (renderer.enabled)
                {
                    enabledRenderers++;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                if (renderer.bounds.size.sqrMagnitude <= 0.000001f)
                {
                    zeroBounds++;
                }

                var material = renderer.sharedMaterial;
                if (material == null)
                {
                    missingMaterials++;
                }
                else if (material.renderQueue > 2500)
                {
                    transparentMaterials++;
                }

                if (examples.Length < 850)
                {
                    examples +=
                        $" [{renderer.name}|active={renderer.gameObject.activeInHierarchy}|enabled={renderer.enabled}|layerMask={renderer.renderingLayerMask}|queue={(material != null ? material.renderQueue.ToString() : "none")}|shader={(material != null && material.shader != null ? material.shader.name : "none")}|worldBounds={renderer.bounds.size}]";
                }
            }

            Debug.Log(
                "[Arena Shooter Outline Diagnostics] Rendering layer assignment: " +
                $"category={category} categoryAssignments={categoryCount} " +
                $"root={root.name} rootActive={root.activeInHierarchy} " +
                $"rootLayer={root.layer} outlineUnityLayer={OutlineLayer} " +
                $"mask={renderingLayerMask} maskName={DescribeRenderingLayerMask(renderingLayerMask)} " +
                $"renderers={renderers.Length} activeRenderers={activeRenderers} enabledRenderers={enabledRenderers} " +
                $"transparentMaterials={transparentMaterials} missingMaterials={missingMaterials} zeroBounds={zeroBounds} " +
                $"aggregateBounds={(hasBounds ? bounds.size.ToString() : "none")} examples={examples}");
        }
    }
}
