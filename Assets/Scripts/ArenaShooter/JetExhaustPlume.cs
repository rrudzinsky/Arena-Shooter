using UnityEngine;
using UnityEngine.Rendering;

namespace ArenaShooter
{
    /// <summary>
    /// Animated jet thruster for the floating stadium furniture: a dark nacelle
    /// with a three-stage cyan plume that flickers, stretches and sways like live
    /// exhaust. Each stage is anchored at its top face so it stretches DOWNWARD,
    /// driven by layered noise — fast combustion flicker over a slow breathing
    /// cycle, with occasional surge bursts — that animates both the geometry and
    /// the glow every frame.
    /// </summary>
    public sealed class JetExhaustPlume : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private struct Stage
        {
            public Transform Transform;
            public Material Material;
            public Color BaseColor;
            public Color EmissionColor;
            public Vector3 RestScale;
            public float TopY;
            public float Stretch;
            public float Sway;
        }

        private readonly Stage[] stages = new Stage[3];
        private float seed;

        /// <summary>
        /// Builds a complete pod (nacelle plus animated plume) under the parent.
        /// Sizes are in pod-local units; <paramref name="scale"/> sizes the whole
        /// pod, and any parent scale multiplies on top of that.
        /// </summary>
        public static JetExhaustPlume Create(Transform parent, Vector3 localPosition, float scale, string name, float yawDegrees)
        {
            var pod = new GameObject(name);
            pod.transform.SetParent(parent, false);
            pod.transform.localPosition = localPosition;
            pod.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            pod.transform.localScale = Vector3.one * scale;

            var plume = pod.AddComponent<JetExhaustPlume>();
            plume.seed = Mathf.Repeat(localPosition.x * 13.7f + localPosition.z * 7.9f + parent.position.sqrMagnitude * 0.01f, 64f);

            CreatePart(pod.transform, "Nacelle", Vector3.zero, new Vector3(2.6f, 2.2f, 3.4f),
                new Color(0.006f, 0.006f, 0.016f), Color.black, out _);

            plume.stages[0] = CreateStage(pod.transform, "Plume Core", -1.1f, new Vector3(1.9f, 1.6f, 2.6f),
                new Color(0.20f, 1.05f, 1.25f), new Color(0.50f, 2.9f, 3.3f), 0.22f, 0.02f);
            plume.stages[1] = CreateStage(pod.transform, "Plume Mid", -2.7f, new Vector3(1.3f, 1.4f, 1.8f),
                new Color(0.09f, 0.62f, 0.80f), new Color(0.22f, 1.6f, 2.0f), 0.50f, 0.08f);
            plume.stages[2] = CreateStage(pod.transform, "Plume Tip", -4.1f, new Vector3(0.7f, 1.2f, 1.0f),
                new Color(0.035f, 0.30f, 0.42f), new Color(0.09f, 0.75f, 1.05f), 0.95f, 0.18f);

            return plume;
        }

        private void Update()
        {
            var time = Time.time;
            for (var i = 0; i < stages.Length; i++)
            {
                var stage = stages[i];
                // Layered noise: fast combustion flicker, slow breathing, and a
                // rare surge when the slow band crests.
                var fast = Mathf.PerlinNoise(seed + i * 7.31f, time * 7.5f);
                var slow = Mathf.PerlinNoise(seed + i * 3.17f + 40f, time * 0.9f);
                var surge = Mathf.Max(0f, Mathf.PerlinNoise(seed + 80f, time * 0.45f) - 0.7f) * 3f;
                var pulse = 0.62f + fast * 0.5f + slow * 0.28f + surge;

                // Stretch downward from the anchored top face; the flame thins as
                // it lengthens so the volume reads as conserved exhaust.
                var lengthScale = Mathf.Lerp(1f, pulse, stage.Stretch);
                var widthScale = Mathf.Lerp(1f, 1f / Mathf.Sqrt(Mathf.Max(0.4f, pulse)), stage.Stretch * 0.6f);
                stage.Transform.localScale = new Vector3(stage.RestScale.x * widthScale, stage.RestScale.y * lengthScale, stage.RestScale.z * widthScale);

                var swayX = (Mathf.PerlinNoise(seed + 11f + i * 5f, time * 2.6f) - 0.5f) * 2f * stage.Sway;
                var swayZ = (Mathf.PerlinNoise(seed + 23f + i * 5f, time * 2.6f) - 0.5f) * 2f * stage.Sway;
                stage.Transform.localPosition = new Vector3(swayX, stage.TopY - stage.RestScale.y * lengthScale * 0.5f, swayZ);

                var glow = 0.55f + pulse * 0.7f;
                stage.Material.SetColor(BaseColorId, stage.BaseColor * glow);
                stage.Material.SetColor(EmissionColorId, stage.EmissionColor * glow);
            }
        }

        private static Stage CreateStage(Transform parent, string name, float topY, Vector3 size, Color baseColor, Color emission, float stretch, float sway)
        {
            CreatePart(parent, name, new Vector3(0f, topY - size.y * 0.5f, 0f), size, baseColor, emission, out var part);
            return new Stage
            {
                Transform = part.transform,
                Material = part.GetComponent<Renderer>().sharedMaterial,
                BaseColor = baseColor,
                EmissionColor = emission,
                RestScale = size,
                TopY = topY,
                Stretch = stretch,
                Sway = sway
            };
        }

        private static void CreatePart(Transform parent, string name, Vector3 localPosition, Vector3 size, Color baseColor, Color emission, out GameObject part)
        {
            part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = size;
            if (part.TryGetComponent<Collider>(out var collider))
            {
                Destroy(collider);
            }

            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateUnlitMaterial($"{name} Material", baseColor, emission);
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
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", baseColor);
            }

            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", emission);
                }
            }

            return material;
        }
    }
}
