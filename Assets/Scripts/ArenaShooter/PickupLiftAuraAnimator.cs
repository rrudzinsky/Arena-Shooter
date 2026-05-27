using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArenaShooter
{
    public sealed class PickupLiftAuraAnimator : MonoBehaviour
    {
        private Material auraVolumeMaterial;
        private readonly List<Material> auraLayerMaterials = new List<Material>();
        private static bool auraBuildDiagnosticLogged;
        private Color auraColor = new Color(0.55f, 1f, 0.94f);
        private Light baseGlow;
        private Light upwardBeam;
        private float seed;

        public void Configure(Material material, Color color, float lightRange)
        {
            auraColor = color;
            seed = Random.value * 100f;
            HideAuthoredMeshes();
            BuildTaperedLightVolume();
            BuildLights(lightRange);
            LogAuraDiagnostics(lightRange);
        }

        private void Awake()
        {
            seed = Random.value * 100f;
        }

        private void Update()
        {
            var time = Time.time + seed;
            var pulse = 0.92f + Mathf.Sin(time * 2.1f) * 0.08f;

            for (var i = 0; i < auraLayerMaterials.Count; i++)
            {
                var material = auraLayerMaterials[i];
                if (material != null && material.HasProperty("_Alpha"))
                {
                    material.SetFloat("_Alpha", (0.016f - i * 0.0017f) * pulse);
                }
            }

            if (baseGlow != null)
            {
                baseGlow.intensity = 0.58f + Mathf.Sin(time * 2.6f) * 0.08f;
            }

            if (upwardBeam != null)
            {
                upwardBeam.intensity = 0.72f + Mathf.Sin(time * 1.8f) * 0.08f;
            }
        }

        private void HideAuthoredMeshes()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;
            }
        }

        private void BuildTaperedLightVolume()
        {
            var volume = new GameObject("Smooth Tapered Pickup Lift Light Volume");
            volume.transform.SetParent(transform, false);
            volume.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            auraLayerMaterials.Clear();

            for (var i = 0; i < 6; i++)
            {
                var t = (i + 1f) / 6f;
                var layer = new GameObject($"Axisymmetric Lift Light Layer {i + 1}");
                layer.transform.SetParent(volume.transform, false);

                var meshFilter = layer.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = CreateOpenFrustumMesh(Mathf.Lerp(0.045f, 0.18f, t), Mathf.Lerp(0.14f, 0.62f, t), 1.08f, 72);

                var meshRenderer = layer.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                var material = CreateAuraVolumeMaterial(0.016f - i * 0.0017f, 0.56f - i * 0.035f);
                meshRenderer.sharedMaterial = material;
                auraLayerMaterials.Add(material);
                if (i == 0)
                {
                    auraVolumeMaterial = material;
                }
            }
        }

        private static Mesh CreateOpenFrustumMesh(float bottomRadius, float topRadius, float height, int segments)
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
                vertices.Add(new Vector3(x * bottomRadius, 0f, z * bottomRadius));
                vertices.Add(new Vector3(x * topRadius, height, z * topRadius));
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

            var mesh = new Mesh { name = "Axisymmetric Tapered Pickup Lift Light Volume Mesh" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material CreateAuraVolumeMaterial(float alpha, float emissionStrength)
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

            var material = new Material(shader) { name = "Faint Smooth Pickup Lift Volume" };
            var color = Color.Lerp(auraColor, Color.white, 0.08f);
            color.a = 0.04f;

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
                material.SetFloat("_FlowSpeed", 0.42f);
            }
            if (material.HasProperty("_FlowStrength"))
            {
                material.SetFloat("_FlowStrength", 0.34f);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }
            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }
            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.renderQueue = 3000;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_BLENDMODE_ALPHA");
            return material;
        }

        private void BuildLights(float lightRange)
        {
            baseGlow = CreatePointLight("Lift Jet Base Light", new Vector3(0f, 0.08f, 0f), Mathf.Min(lightRange, 1.25f), 0.58f);

            var beamObject = new GameObject("Lift Jet Upward Spot Light");
            beamObject.transform.SetParent(transform, false);
            beamObject.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            beamObject.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            upwardBeam = beamObject.AddComponent<Light>();
            upwardBeam.type = LightType.Spot;
            upwardBeam.color = auraColor;
            upwardBeam.range = 1.55f;
            upwardBeam.spotAngle = 36f;
            upwardBeam.intensity = 0.72f;
            upwardBeam.shadows = LightShadows.None;
        }

        private Light CreatePointLight(string name, Vector3 localPosition, float range, float intensity)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = localPosition;

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = auraColor;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        private void LogAuraDiagnostics(float lightRange)
        {
            if (!Application.isPlaying || auraBuildDiagnosticLogged)
            {
                return;
            }

            auraBuildDiagnosticLogged = true;
            Debug.Log(
                "[Arena Shooter Model Diagnostics] Pickup lift aura runtime build: " +
                $"root={name} auraColor={auraColor} requestedLightRange={lightRange} " +
                $"layerMaterials={auraLayerMaterials.Count} volumeMaterial={(auraVolumeMaterial != null ? auraVolumeMaterial.shader.name : "null")} " +
                $"baseGlow={(baseGlow != null ? $"{baseGlow.range}/{baseGlow.intensity}" : "null")} " +
                $"upwardBeam={(upwardBeam != null ? $"{upwardBeam.range}/{upwardBeam.intensity}/{upwardBeam.spotAngle}" : "null")}.");
        }
    }
}
