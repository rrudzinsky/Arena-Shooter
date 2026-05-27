using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArenaShooter
{
    public static class ShieldDomeBackdrop
    {
        private const int DomeSegments = 72;
        private const int DomeBands = 9;
        private const int SeamSegments = 12;
        private const int DiagonalSeamCount = 14;

        public static void Build(Transform root, ArenaLayout layout, float roomSize, float wallHeight)
        {
            if (root == null || layout == null)
            {
                return;
            }

            var center = layout.CircularCenter;
            var radius = layout.DomeRadius > 0f ? layout.DomeRadius : CalculateArenaRadius(layout, roomSize);
            var height = Mathf.Max(wallHeight + 18f, radius * 0.58f);
            var backdrop = new GameObject("Hex Shield Dome Backdrop");
            backdrop.transform.SetParent(root, false);

            CreateDomeBands(backdrop.transform, center, radius, height);
            CreateHorizonBands(backdrop.transform, center, radius, height);
            CreateHexSeams(backdrop.transform, center, radius, height);
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

        private static void CreateDomeBands(Transform parent, Vector3 center, float radius, float height)
        {
            var colors = new[]
            {
                new Color(0.035f, 0.34f, 0.42f),
                new Color(0.022f, 0.25f, 0.38f),
                new Color(0.052f, 0.13f, 0.31f),
                new Color(0.066f, 0.07f, 0.24f),
                new Color(0.035f, 0.055f, 0.18f),
                new Color(0.022f, 0.04f, 0.14f),
                new Color(0.018f, 0.03f, 0.105f),
                new Color(0.012f, 0.022f, 0.078f),
                new Color(0.008f, 0.016f, 0.052f)
            };

            for (var i = 0; i < DomeBands; i++)
            {
                var t0 = i / (float)DomeBands;
                var t1 = (i + 1) / (float)DomeBands;
                var material = CreateUnlitMaterial($"Shield Dome Color Band {i + 1}", colors[Mathf.Min(i, colors.Length - 1)], Color.black);
                CreateDomeBand(parent, $"Shield Dome Color Band {i + 1}", center, radius, height, t0, t1, material, 0f);
            }
        }

        private static void CreateHorizonBands(Transform parent, Vector3 center, float radius, float height)
        {
            var cyan = CreateUnlitMaterial("Shield Dome Cyan Horizon Glow", new Color(0.02f, 0.88f, 1.0f), new Color(0f, 1.8f, 2.6f));
            var magenta = CreateUnlitMaterial("Shield Dome Magenta Horizon Glow", new Color(0.95f, 0.08f, 0.72f), new Color(2.4f, 0.05f, 1.8f));
            var violet = CreateUnlitMaterial("Shield Dome Violet Upper Glow", new Color(0.32f, 0.1f, 0.92f), new Color(0.65f, 0.08f, 2.1f));

            CreateDomeBand(parent, "Shield Dome Low Cyan Horizon", center, radius, height, 0.065f, 0.085f, cyan, -0.18f);
            CreateDomeBand(parent, "Shield Dome Low Magenta Horizon", center, radius, height, 0.105f, 0.122f, magenta, -0.2f);
            CreateDomeBand(parent, "Shield Dome Mid Violet Horizon", center, radius, height, 0.185f, 0.197f, violet, -0.22f);
        }

        private static void CreateHexSeams(Transform parent, Vector3 center, float radius, float height)
        {
            var latitudeMaterial = CreateUnlitMaterial("Shield Dome Faint Cyan Hex Latitude", new Color(0.02f, 0.42f, 0.5f), new Color(0f, 0.4f, 0.55f));
            var diagonalMaterial = CreateUnlitMaterial("Shield Dome Faint Magenta Hex Diagonal", new Color(0.22f, 0.06f, 0.34f), new Color(0.35f, 0.02f, 0.55f));

            for (var i = 0; i < 5; i++)
            {
                var t = Mathf.Lerp(0.18f, 0.78f, i / 4f);
                CreateDomeBand(parent, $"Shield Dome Faint Latitude Seam {i + 1}", center, radius, height, t, t + 0.0035f, latitudeMaterial, -0.32f);
            }

            CreateDiagonalSeamFamily(parent, "Shield Dome Hex Diagonal A", center, radius, height, diagonalMaterial, 1f);
            CreateDiagonalSeamFamily(parent, "Shield Dome Hex Diagonal B", center, radius, height, diagonalMaterial, -1f);
        }

        private static void CreateDomeBand(Transform parent, string name, Vector3 center, float radius, float height, float t0, float t1, Material material, float inset)
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
                vertices[i * 2] = DomePoint(center, radius + inset, height, t0, phi);
                vertices[i * 2 + 1] = DomePoint(center, radius + inset, height, t1, phi);
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

        private static void CreateDiagonalSeamFamily(Transform parent, string name, Vector3 center, float radius, float height, Material material, float slope)
        {
            var meshObject = new GameObject(name);
            meshObject.transform.SetParent(parent, false);
            var meshFilter = meshObject.AddComponent<MeshFilter>();
            var renderer = meshObject.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material);

            var vertices = new List<Vector3>(DiagonalSeamCount * (SeamSegments + 1) * 2);
            var triangles = new List<int>(DiagonalSeamCount * SeamSegments * 6);
            const float width = 0.0042f;
            for (var seam = 0; seam < DiagonalSeamCount; seam++)
            {
                var basePhi = seam / (float)DiagonalSeamCount * Mathf.PI * 2f;
                var start = vertices.Count;
                for (var i = 0; i <= SeamSegments; i++)
                {
                    var t = Mathf.Lerp(0.14f, 0.86f, i / (float)SeamSegments);
                    var phi = basePhi + slope * t * 2.35f;
                    vertices.Add(DomePoint(center, radius - 0.42f, height, t, phi - width));
                    vertices.Add(DomePoint(center, radius - 0.42f, height, t, phi + width));
                }

                for (var i = 0; i < SeamSegments; i++)
                {
                    var a = start + i * 2;
                    triangles.Add(a);
                    triangles.Add(a + 1);
                    triangles.Add(a + 2);
                    triangles.Add(a + 1);
                    triangles.Add(a + 3);
                    triangles.Add(a + 2);
                }
            }

            meshFilter.sharedMesh = BuildMesh(name, vertices.ToArray(), triangles.ToArray());
        }

        private static Vector3 DomePoint(Vector3 center, float radius, float height, float t, float phi)
        {
            var theta = Mathf.Lerp(0f, Mathf.PI * 0.485f, Mathf.Clamp01(t));
            var flatRadius = radius * Mathf.Cos(theta);
            var y = -1.2f + height * Mathf.Sin(theta);
            return center + new Vector3(Mathf.Cos(phi) * flatRadius, y, Mathf.Sin(phi) * flatRadius);
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
