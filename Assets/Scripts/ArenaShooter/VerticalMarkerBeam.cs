using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArenaShooter
{
    public sealed class VerticalMarkerBeam : MonoBehaviour
    {
        private readonly List<Material> layerMaterials = new();
        private Color markerColor;
        private float seed;
        private Light topSpot;

        public static VerticalMarkerBeam Attach(Transform parent, string markerName, Color color, float height, float radius, float lightRange)
        {
            var marker = new GameObject(markerName);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = Vector3.zero;
            var beam = marker.AddComponent<VerticalMarkerBeam>();
            beam.Build(color, height, radius, lightRange);
            return beam;
        }

        private void Build(Color color, float height, float radius, float lightRange)
        {
            markerColor = color;
            seed = Random.value * 100f;
            BuildBeamVolume(height, radius);
            BuildLights(height, lightRange);
        }

        private void Update()
        {
            var time = Time.time + seed;
            var pulse = 0.86f + Mathf.Sin(time * 1.8f) * 0.14f;
            for (var i = 0; i < layerMaterials.Count; i++)
            {
                var material = layerMaterials[i];
                if (material != null && material.HasProperty("_Alpha"))
                {
                    material.SetFloat("_Alpha", (0.033f - i * 0.004f) * pulse);
                }
            }

            if (topSpot != null)
            {
                topSpot.intensity = 1.05f + Mathf.Sin(time * 2.1f) * 0.16f;
            }

        }

        private void BuildBeamVolume(float height, float radius)
        {
            layerMaterials.Clear();
            var bottomOffset = Mathf.Min(2.7f, height * 0.32f);
            var visibleHeight = height - bottomOffset;
            for (var i = 0; i < 5; i++)
            {
                var t = (i + 1f) / 5f;
                var layer = new GameObject($"Descending Marker Beam Layer {i + 1}");
                layer.transform.SetParent(transform, false);
                layer.transform.localPosition = Vector3.up * bottomOffset;

                var meshFilter = layer.AddComponent<MeshFilter>();
                var layerRadius = radius * Mathf.Lerp(0.72f, 1.06f, t);
                meshFilter.sharedMesh = CreateColumnBeamMesh(layerRadius, visibleHeight, 72);

                var renderer = layer.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                var material = CreateBeamMaterial(0.033f - i * 0.004f, 0.86f - i * 0.08f);
                renderer.sharedMaterial = material;
                layerMaterials.Add(material);
            }

        }

        private void BuildLights(float height, float lightRange)
        {
            var topObject = new GameObject("Marker Downward Spot Light");
            topObject.transform.SetParent(transform, false);
            topObject.transform.localPosition = new Vector3(0f, height, 0f);
            topObject.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            topSpot = topObject.AddComponent<Light>();
            topSpot.type = LightType.Spot;
            topSpot.color = markerColor;
            topSpot.range = Mathf.Max(1f, height - 1.4f);
            topSpot.spotAngle = 13f;
            topSpot.intensity = 1.05f;
            topSpot.shadows = LightShadows.None;

        }

        private Mesh CreateColumnBeamMesh(float radius, float height, int segments)
        {
            var vertices = new List<Vector3>((segments + 1) * 2);
            var uvs = new List<Vector2>((segments + 1) * 2);
            var triangles = new List<int>(segments * 6);

            for (var i = 0; i <= segments; i++)
            {
                var u = i / (float)segments;
                var angle = u * Mathf.PI * 2f;
                var x = Mathf.Cos(angle);
                var z = Mathf.Sin(angle);
                vertices.Add(new Vector3(x * radius, 0f, z * radius));
                vertices.Add(new Vector3(x * radius, height, z * radius));
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));
            }

            for (var i = 0; i < segments; i++)
            {
                var a = i * 2;
                triangles.Add(a);
                triangles.Add(a + 1);
                triangles.Add(a + 2);
                triangles.Add(a + 3);
                triangles.Add(a + 2);
                triangles.Add(a + 1);
            }

            var mesh = new Mesh { name = "Descending Marker Beam Mesh" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material CreateBeamMaterial(float alpha, float emissionStrength)
        {
            var shader = Shader.Find("ArenaShooter/SmoothPickupLiftAura");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            var material = new Material(shader) { name = "Vertical Marker Beam Volume" };
            var color = Color.Lerp(markerColor, Color.white, 0.1f);
            color.a = 0.06f;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Alpha"))
            {
                material.SetFloat("_Alpha", alpha);
            }

            if (material.HasProperty("_EmissionStrength"))
            {
                material.SetFloat("_EmissionStrength", emissionStrength);
            }

            if (material.HasProperty("_FlowSpeed"))
            {
                material.SetFloat("_FlowSpeed", -0.72f);
            }

            if (material.HasProperty("_FlowStrength"))
            {
                material.SetFloat("_FlowStrength", 0.42f);
            }

            material.renderQueue = 3000;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_BLENDMODE_ALPHA");
            return material;
        }

    }
}
