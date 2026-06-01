using UnityEngine;
using UnityEngine.Rendering;

namespace ArenaShooter
{
    internal readonly struct StadiumVisualMetrics
    {
        public StadiumVisualMetrics(Vector3 center, float radius, float height, float baseY)
        {
            Center = center;
            Radius = radius;
            Height = height;
            BaseY = baseY;
        }

        public Vector3 Center { get; }
        public float Radius { get; }
        public float Height { get; }
        public float BaseY { get; }
    }

    internal static class AllOutWarStadiumVisuals
    {
        public const float VisualBubbleRadius = 260f;
        public const float VisualBubbleHeight = 162f;
        public const float VisualBubbleBaseY = -1.2f;

        public static StadiumVisualMetrics CreateMetrics(Vector3 center)
        {
            return new StadiumVisualMetrics(center, VisualBubbleRadius, VisualBubbleHeight, VisualBubbleBaseY);
        }

        public static float LatitudeToTheta(float latitude)
        {
            return Mathf.Lerp(0f, Mathf.PI * 0.485f, Mathf.Clamp01(latitude));
        }

        public static Vector3 DomePoint(StadiumVisualMetrics metrics, float latitude, float phi, float radiusInset = 0f)
        {
            var theta = LatitudeToTheta(latitude);
            var flatRadius = Mathf.Max(1f, metrics.Radius + radiusInset) * Mathf.Cos(theta);
            var y = metrics.BaseY + metrics.Height * Mathf.Sin(theta);
            return metrics.Center + new Vector3(Mathf.Cos(phi) * flatRadius, y, Mathf.Sin(phi) * flatRadius);
        }
    }

    public static class ShieldDomeBackdrop
    {
        private const int DomeSegments = 72;
        private const int DomeBands = 9;

        public static void Build(Transform root, ArenaLayout layout, float roomSize, float wallHeight)
        {
            if (root == null || layout == null)
            {
                return;
            }

            var center = layout.CircularCenter;
            var metrics = layout.DomeRadius > 0f
                ? AllOutWarStadiumVisuals.CreateMetrics(center)
                : CreateLegacyMetrics(center, CalculateArenaRadius(layout, roomSize), wallHeight);
            var backdrop = new GameObject("Hex Shield Dome Backdrop");
            backdrop.transform.SetParent(root, false);

            CreateDomeBands(backdrop.transform, metrics);
            CreateHorizonBands(backdrop.transform, metrics);
        }

        private static Vector3 CalculateArenaCenter(ArenaLayout layout)
        {
            var center = Vector3.zero;
            var count = 0;
            foreach (var room in layout.RoomCenters.Values)
            {
                center += room;
                count++;
            }

            return count > 0 ? center / count : Vector3.zero;
        }

        private static float CalculateArenaRadius(ArenaLayout layout, float roomSize)
        {
            var center = CalculateArenaCenter(layout);
            var radius = roomSize * 1.75f;
            foreach (var room in layout.RoomCenters.Values)
            {
                var delta = room - center;
                delta.y = 0f;
                radius = Mathf.Max(radius, delta.magnitude + roomSize * 1.05f);
            }

            return radius;
        }

        private static StadiumVisualMetrics CreateLegacyMetrics(Vector3 center, float radius, float wallHeight)
        {
            return new StadiumVisualMetrics(center, radius, Mathf.Max(wallHeight + 18f, radius * 0.58f), AllOutWarStadiumVisuals.VisualBubbleBaseY);
        }

        private static void CreateDomeBands(Transform parent, StadiumVisualMetrics metrics)
        {
            var colors = new[]
            {
                new Color(0.0012f, 0.001f, 0.0025f),
                new Color(0.0015f, 0.001f, 0.003f),
                new Color(0.002f, 0.0012f, 0.004f),
                new Color(0.0024f, 0.0014f, 0.005f),
                new Color(0.002f, 0.0012f, 0.004f),
                new Color(0.0016f, 0.001f, 0.0034f),
                new Color(0.0012f, 0.001f, 0.0028f),
                new Color(0.001f, 0.0008f, 0.0024f),
                new Color(0.0008f, 0.0007f, 0.002f)
            };

            for (var i = 0; i < DomeBands; i++)
            {
                var t0 = i / (float)DomeBands;
                var t1 = (i + 1) / (float)DomeBands;
                var material = CreateUnlitMaterial($"Shield Dome Color Band {i + 1}", colors[Mathf.Min(i, colors.Length - 1)], Color.black);
                CreateDomeBand(parent, $"Shield Dome Color Band {i + 1}", metrics, t0, t1, material, 0f);
            }
        }

        private static void CreateHorizonBands(Transform parent, StadiumVisualMetrics metrics)
        {
            var lowerTint = CreateUnlitMaterial("Shield Dome Black Purple Lower Tint", new Color(0.012f, 0.003f, 0.024f), new Color(0.035f, 0.004f, 0.07f));
            var upperRim = CreateUnlitMaterial("Shield Dome Faint Purple Rim", new Color(0.018f, 0.004f, 0.04f), new Color(0.07f, 0.006f, 0.14f));

            CreateDomeBand(parent, "Shield Dome Low Black Purple Tint", metrics, 0.065f, 0.085f, lowerTint, -0.18f);
            CreateDomeBand(parent, "Shield Dome Mid Black Purple Tint", metrics, 0.18f, 0.193f, upperRim, -0.22f);
        }

        private static void CreateDomeBand(Transform parent, string name, StadiumVisualMetrics metrics, float t0, float t1, Material material, float inset)
        {
            var meshObject = new GameObject(name);
            meshObject.transform.SetParent(parent, false);
            var meshFilter = meshObject.AddComponent<MeshFilter>();
            var renderer = meshObject.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material);

            var vertices = new Vector3[(DomeSegments + 1) * 2];
            var triangles = new int[DomeSegments * 6];
            for (var i = 0; i <= DomeSegments; i++)
            {
                var phi = i / (float)DomeSegments * Mathf.PI * 2f;
                vertices[i * 2] = AllOutWarStadiumVisuals.DomePoint(metrics, t0, phi, inset);
                vertices[i * 2 + 1] = AllOutWarStadiumVisuals.DomePoint(metrics, t1, phi, inset);
            }

            for (var i = 0; i < DomeSegments; i++)
            {
                var vertex = i * 2;
                var tri = i * 6;
                triangles[tri] = vertex;
                triangles[tri + 1] = vertex + 1;
                triangles[tri + 2] = vertex + 2;
                triangles[tri + 3] = vertex + 1;
                triangles[tri + 4] = vertex + 3;
                triangles[tri + 5] = vertex + 2;
            }

            meshFilter.sharedMesh = BuildMesh(name, vertices, triangles);
        }

        private static Mesh BuildMesh(string name, Vector3[] vertices, int[] triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void ConfigureRenderer(Renderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Material CreateUnlitMaterial(string name, Color baseColor, Color emission)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader) { name = name };
            SetColor(material, baseColor);
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", emission);
                }
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", (int)CullMode.Off);
            }

            return material;
        }

        private static void SetColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }
    }
}
