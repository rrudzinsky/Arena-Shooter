using System.Collections.Generic;
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
        // Wide enough that a band of open sky separates the equalizer skyline from
        // the mirror bowl — that's where the floating stands ring lives.
        public const float VisualBubbleRadius = 340f;
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
        private const int ShellLatitudeSteps = 36;

        // The mirror gallery: six tiers of glass tiles running from the dome base
        // all the way up to the black apex iris, sized so every tile LOOKS the
        // same from a player on the arena floor. Apparent width from mid-arena is
        // simply 360deg / count regardless of ring size, so all tiers share one
        // count; apparent height shrinks the higher (closer, more overhead) a
        // tier sits, so the latitude spans taper going up. Each tier subtends an
        // equal ~10.8deg of elevation from a mid-arena eye at 1.7 m, solved
        // against the dome ellipsoid (R 340, H 162, base -1.2). The top edge runs
        // to 0.884 — over the purple oculus ring (the panes render in front of
        // it) and flush against the black apex iris, the chandelier base.
        // Mirrored in DomeGalleryGlass.shader.
        private static readonly float[] GalleryTierBounds = { 0f, 0.2495f, 0.4548f, 0.6081f, 0.7222f, 0.8110f, 0.884f };
        private static readonly int[] GalleryTierWindowCounts = { 36, 36, 36, 36, 36, 36 };
        // The window grid lines are drawn inside the glass shader, not as separate
        // geometry: floating frame meshes in front of the panes opened parallax
        // slivers and their coarse segmentation wobbled the arcs.
        // The glass sits well clear of the dome shell: at 0.42 m the depth buffer
        // could not separate the two surfaces past ~150 m and the dark shell bled
        // through the panes as view-dependent diamond/dot bands (z-fighting).
        private const float GalleryPaneInset = -2.2f;

        /// <summary>
        /// The live gallery glass material. The All Out War scoreboard streams the
        /// music equalizer column levels into it (the bars are drawn inside the
        /// mirror tiles by DomeGalleryGlass.shader).
        /// </summary>
        internal static Material GalleryPaneMaterial { get; private set; }

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

            CreateDomeShell(backdrop.transform, metrics);
            GalleryPaneMaterial = CreateGalleryPanes(backdrop.transform, metrics);
            CreateApexOculus(backdrop.transform, metrics);
            // The stands (and the booth that parks on their ring) scale with the
            // PLAYFIELD, not the fixed visual bubble, so they stay close enough
            // to read on normal-sized maps.
            FloatingStandsRing.Build(backdrop.transform, metrics, CalculatePlayfieldRadius(layout));
            backdrop.AddComponent<BroadcastDroneSwarm>().Initialize(metrics);
            backdrop.AddComponent<CommentatorsBooth>().Initialize(metrics);
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

        private static float CalculatePlayfieldRadius(ArenaLayout layout)
        {
            // Same playfield measure the dome scoreboard uses to scale the
            // chandelier: farthest room from the circular centre plus a margin.
            var radius = 80f;
            if (layout?.RoomCenters == null)
            {
                return radius;
            }

            foreach (var room in layout.RoomCenters.Values)
            {
                var delta = room - layout.CircularCenter;
                delta.y = 0f;
                radius = Mathf.Max(radius, delta.magnitude + 30f);
            }

            return radius;
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

        private static void CreateDomeShell(Transform parent, StadiumVisualMetrics metrics)
        {
            // Pure black shell: the sunset gradient that used to live here is now
            // displayed by the mirror gallery itself (DomeGalleryGlass.shader), so
            // the dome behind the panes — and in the seams between them — reads as
            // featureless darkness.
            var vertices = new List<Vector3>((ShellLatitudeSteps + 1) * (DomeSegments + 1));
            var colors = new List<Color>((ShellLatitudeSteps + 1) * (DomeSegments + 1));
            var triangles = new List<int>(ShellLatitudeSteps * DomeSegments * 6);
            for (var step = 0; step <= ShellLatitudeSteps; step++)
            {
                var latitude = step / (float)ShellLatitudeSteps;
                for (var i = 0; i <= DomeSegments; i++)
                {
                    var phi = i / (float)DomeSegments * Mathf.PI * 2f;
                    vertices.Add(AllOutWarStadiumVisuals.DomePoint(metrics, latitude, phi, 0f));
                    colors.Add(Color.black);
                }
            }

            for (var step = 0; step < ShellLatitudeSteps; step++)
            {
                for (var i = 0; i < DomeSegments; i++)
                {
                    var a = step * (DomeSegments + 1) + i;
                    var b = a + DomeSegments + 1;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(b + 1);
                }
            }

            var shell = new GameObject("Shield Dome Tinted Shell");
            shell.transform.SetParent(parent, false);
            var mesh = new Mesh { name = "Shield Dome Tinted Shell Mesh" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            shell.AddComponent<MeshFilter>().sharedMesh = mesh;

            var shader = Shader.Find("ArenaShooter/DomeShellTint");
            var material = shader != null
                ? new Material(shader) { name = "Shield Dome Tinted Shell" }
                : CreateUnlitMaterial("Shield Dome Tinted Shell", new Color(0.02f, 0.006f, 0.04f), Color.black);
            ConfigureRenderer(shell.AddComponent<MeshRenderer>(), material);
        }

        private static Material CreateGalleryPanes(Transform parent, StadiumVisualMetrics metrics)
        {
            var vertices = new List<Vector3>();
            var paneUVs = new List<Vector2>();
            var paneData = new List<Vector2>();
            var triangles = new List<int>();

            for (var tier = 0; tier < GalleryTierWindowCounts.Length; tier++)
            {
                var windowCount = GalleryTierWindowCounts[tier];
                for (var window = 0; window < windowCount; window++)
                {
                    AddGalleryPane(vertices, paneUVs, paneData, triangles, metrics,
                        GalleryTierBounds[tier], GalleryTierBounds[tier + 1],
                        window / (float)windowCount * Mathf.PI * 2f,
                        (window + 1) / (float)windowCount * Mathf.PI * 2f,
                        tier, (window + 0.5f) / windowCount);
                }
            }

            var panes = new GameObject("Shield Dome Gallery Panes");
            panes.transform.SetParent(parent, false);
            var mesh = new Mesh { name = "Shield Dome Gallery Panes Mesh" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, paneUVs);
            mesh.SetUVs(1, paneData);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            panes.AddComponent<MeshFilter>().sharedMesh = mesh;

            var shader = Shader.Find("ArenaShooter/DomeGalleryGlass");
            var material = shader != null
                ? new Material(shader) { name = "Shield Dome Gallery Glass" }
                : CreateUnlitMaterial("Shield Dome Gallery Panes", new Color(0.018f, 0.02f, 0.042f), Color.black);
            ConfigureRenderer(panes.AddComponent<MeshRenderer>(), material);
            return material;
        }

        private static void AddGalleryPane(List<Vector3> vertices, List<Vector2> paneUVs, List<Vector2> paneData, List<int> triangles, StadiumVisualMetrics metrics, float latitude0, float latitude1, float phi0, float phi1, int tier, float phi01)
        {
            // The glass look is fully procedural in the DomeGalleryGlass shader; the
            // mesh just carries pane-local UVs plus the tier and ring position for
            // the tile cutout, the sunset gradient and the equalizer bars.
            const int phiSegments = 8;
            const int latitudeSegments = 6;
            var packedTierPhi = tier + Mathf.Clamp01(phi01) * 0.999f;
            var baseIndex = vertices.Count;
            for (var latStep = 0; latStep <= latitudeSegments; latStep++)
            {
                var latT = latStep / (float)latitudeSegments;
                var latitude = Mathf.Lerp(latitude0, latitude1, latT);
                for (var phiStep = 0; phiStep <= phiSegments; phiStep++)
                {
                    var phiT = phiStep / (float)phiSegments;
                    var phi = Mathf.Lerp(phi0, phi1, phiT);
                    vertices.Add(AllOutWarStadiumVisuals.DomePoint(metrics, latitude, phi, GalleryPaneInset));
                    paneUVs.Add(new Vector2(phiT, latT));
                    paneData.Add(new Vector2(0f, packedTierPhi));
                }
            }

            for (var latStep = 0; latStep < latitudeSegments; latStep++)
            {
                for (var phiStep = 0; phiStep < phiSegments; phiStep++)
                {
                    var a = baseIndex + latStep * (phiSegments + 1) + phiStep;
                    var b = a + phiSegments + 1;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(b + 1);
                }
            }
        }

        private static void CreateApexOculus(Transform parent, StadiumVisualMetrics metrics)
        {
            var ringMaterial = CreateUnlitMaterial("Shield Dome Apex Oculus Ring", new Color(0.5f, 0.02f, 1.1f), new Color(1.3f, 0.05f, 2.8f));
            var irisMaterial = CreateUnlitMaterial("Shield Dome Apex Iris", new Color(0.018f, 0.006f, 0.04f), new Color(0.035f, 0.008f, 0.08f));
            CreateDomeBand(parent, "Shield Dome Apex Oculus Ring", metrics, 0.868f, 0.884f, ringMaterial, -0.3f);
            CreateDomeBand(parent, "Shield Dome Apex Iris", metrics, 0.884f, 0.985f, irisMaterial, -0.3f);
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
