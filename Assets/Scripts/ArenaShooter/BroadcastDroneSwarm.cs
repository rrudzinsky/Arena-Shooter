using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArenaShooter
{
    /// <summary>
    /// Three TV-network camera drones broadcasting the match: they pick a subject
    /// (the player or a roaming point over the battlefield), glide to a filming
    /// standoff, hover with a gentle bob while keeping the lens on the subject,
    /// then cut to the next shot. Each network has its own trim colour, and the
    /// red REC strobe blinks while they film.
    /// </summary>
    public sealed class BroadcastDroneSwarm : MonoBehaviour
    {
        private const float HoverBobAmplitude = 0.55f;
        private const float RepositionSmoothTime = 3.2f;
        private const float MinShotSeconds = 7f;
        private const float MaxShotSeconds = 18f;
        private const float MinAltitude = 14f;
        private const float MaxAltitude = 30f;
        private const float MinStandoff = 13f;
        private const float MaxStandoff = 24f;
        private const float RotorSpinDegreesPerSecond = 2400f;

        private static readonly (string Network, Color Trim)[] Networks =
        {
            ("GRID-1", new Color(0.15f, 0.92f, 1f)),
            ("NEON SPORTS", new Color(1f, 0.12f, 0.9f)),
            ("AOW-TV", new Color(1f, 0.72f, 0.12f)),
        };

        private sealed class Drone
        {
            public Transform Root;
            public Transform Gimbal;
            public List<Transform> Rotors;
            public Material RecLightMaterial;
            public Color TrimColor;
            public Vector3 Velocity;
            public Vector3 FilmingPost;
            public Vector3 Subject;
            public float NextCutAt;
            public float BobPhase;
            public bool TrackingPlayer;
        }

        private readonly List<Drone> drones = new();
        private StadiumVisualMetrics metrics;
        private System.Random rng;
        private float launchAt;
        private bool initialized;

        internal void Initialize(StadiumVisualMetrics stadiumMetrics)
        {
            metrics = stadiumMetrics;
            rng = new System.Random(771239);
            // Launch after the match intro settles (the booth builds at t+1.6s)
            // so the swarm rises into an already-dressed stadium.
            launchAt = Time.time + 2.2f;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            if (drones.Count == 0)
            {
                if (Time.time < launchAt)
                {
                    return;
                }

                for (var i = 0; i < Networks.Length; i++)
                {
                    drones.Add(BuildDrone(Networks[i].Network, Networks[i].Trim, i));
                }
            }

            foreach (var drone in drones)
            {
                if (drone.Root == null)
                {
                    continue;
                }

                if (Time.time >= drone.NextCutAt)
                {
                    CutToNextShot(drone);
                }

                if (drone.TrackingPlayer && Camera.main != null)
                {
                    drone.Subject = Camera.main.transform.position;
                }

                // Glide toward the filming post with a slow hover bob layered on top.
                var bob = Mathf.Sin(Time.time * 0.9f + drone.BobPhase) * HoverBobAmplitude;
                var desired = drone.FilmingPost + Vector3.up * bob;
                drone.Root.position = Vector3.SmoothDamp(drone.Root.position, desired, ref drone.Velocity, RepositionSmoothTime);

                // Body yaws toward the subject; the gimbal pitches the lens onto it.
                var toSubject = drone.Subject - drone.Root.position;
                var flat = new Vector3(toSubject.x, 0f, toSubject.z);
                if (flat.sqrMagnitude > 0.01f)
                {
                    var yaw = Quaternion.LookRotation(flat, Vector3.up);
                    // Lean into lateral motion like a real quad.
                    var lateral = Vector3.Dot(drone.Velocity, drone.Root.right);
                    var forward = Vector3.Dot(drone.Velocity, drone.Root.forward);
                    var lean = Quaternion.Euler(Mathf.Clamp(forward * 1.6f, -14f, 14f), 0f, Mathf.Clamp(-lateral * 1.6f, -14f, 14f));
                    drone.Root.rotation = Quaternion.Slerp(drone.Root.rotation, yaw * lean, Time.deltaTime * 2.2f);
                }

                if (drone.Gimbal != null && toSubject.sqrMagnitude > 0.01f)
                {
                    var lensTarget = Quaternion.LookRotation(toSubject, Vector3.up);
                    drone.Gimbal.rotation = Quaternion.Slerp(drone.Gimbal.rotation, lensTarget, Time.deltaTime * 4f);
                }

                foreach (var rotor in drone.Rotors)
                {
                    rotor.Rotate(0f, RotorSpinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
                }

                // REC strobe: sharp red double-blink, like broadcast gear.
                var blink = Mathf.Repeat(Time.time * 1.4f + drone.BobPhase, 1f);
                var recOn = blink < 0.08f || (blink > 0.16f && blink < 0.24f);
                SetEmission(drone.RecLightMaterial, recOn ? new Color(1.8f, 0.05f, 0.05f) : new Color(0.08f, 0.005f, 0.005f));
            }
        }

        private void CutToNextShot(Drone drone)
        {
            drone.NextCutAt = Time.time + Mathf.Lerp(MinShotSeconds, MaxShotSeconds, (float)rng.NextDouble());

            // The star of the broadcast is the player; the rest is battlefield B-roll.
            drone.TrackingPlayer = rng.NextDouble() < 0.55 && Camera.main != null;
            if (drone.TrackingPlayer)
            {
                drone.Subject = Camera.main.transform.position;
            }
            else
            {
                var roamRadius = Mathf.Max(20f, metrics.Radius * 0.32f);
                var angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                var distance = Mathf.Sqrt((float)rng.NextDouble()) * roamRadius;
                drone.Subject = metrics.Center + new Vector3(Mathf.Cos(angle) * distance, 1.5f, Mathf.Sin(angle) * distance);
            }

            var standoff = Mathf.Lerp(MinStandoff, MaxStandoff, (float)rng.NextDouble());
            var altitude = Mathf.Lerp(MinAltitude, MaxAltitude, (float)rng.NextDouble());
            var postAngle = (float)rng.NextDouble() * Mathf.PI * 2f;
            drone.FilmingPost = drone.Subject
                              + new Vector3(Mathf.Cos(postAngle) * standoff, 0f, Mathf.Sin(postAngle) * standoff);
            drone.FilmingPost.y = altitude;
        }

        private Drone BuildDrone(string network, Color trim, int index)
        {
            var root = new GameObject($"Broadcast Drone {network}");
            root.transform.SetParent(transform, false);
            var startAngle = index * Mathf.PI * 2f / Networks.Length;
            root.transform.position = metrics.Center + new Vector3(Mathf.Cos(startAngle) * 30f, 22f, Mathf.Sin(startAngle) * 30f);

            var bodyMaterial = CreateUnlitMaterial($"{network} Drone Body", new Color(0.004f, 0.004f, 0.01f), Color.black);
            var trimMaterial = CreateUnlitMaterial($"{network} Drone Trim", trim * 0.5f, trim * 2.4f);
            var lensMaterial = CreateUnlitMaterial($"{network} Drone Lens", new Color(0.02f, 0.04f, 0.10f), new Color(0.25f, 0.55f, 1.4f));
            var recMaterial = CreateUnlitMaterial($"{network} Drone Rec Light", new Color(0.10f, 0.004f, 0.004f), new Color(0.08f, 0.005f, 0.005f));

            // Body: a flat hull with a glowing network-colour trim band.
            AddPart(root.transform, PrimitiveType.Cube, "Hull", Vector3.zero, new Vector3(1.5f, 0.32f, 1.0f), bodyMaterial);
            AddPart(root.transform, PrimitiveType.Cube, "Trim Band", Vector3.zero, new Vector3(1.56f, 0.10f, 1.06f), trimMaterial);

            // Four arms with rotor discs.
            var rotors = new List<Transform>();
            for (var arm = 0; arm < 4; arm++)
            {
                var sx = arm % 2 == 0 ? 1f : -1f;
                var sz = arm < 2 ? 1f : -1f;
                var armPosition = new Vector3(0.95f * sx, 0.06f, 0.72f * sz);
                AddPart(root.transform, PrimitiveType.Cube, $"Arm {arm}", armPosition * 0.55f, new Vector3(0.8f, 0.07f, 0.10f), bodyMaterial)
                    .transform.localRotation = Quaternion.Euler(0f, Mathf.Atan2(sz, sx) * Mathf.Rad2Deg, 0f);
                var rotor = AddPart(root.transform, PrimitiveType.Cylinder, $"Rotor {arm}", armPosition + Vector3.up * 0.12f, new Vector3(0.62f, 0.015f, 0.62f), trimMaterial);
                rotors.Add(rotor.transform);
            }

            // Camera gimbal slung under the hull: a small mount with a bright lens.
            var gimbal = new GameObject("Camera Gimbal");
            gimbal.transform.SetParent(root.transform, false);
            gimbal.transform.localPosition = new Vector3(0f, -0.34f, 0.18f);
            AddPart(gimbal.transform, PrimitiveType.Sphere, "Gimbal Mount", Vector3.zero, Vector3.one * 0.34f, bodyMaterial);
            AddPart(gimbal.transform, PrimitiveType.Cylinder, "Lens Barrel", new Vector3(0f, 0f, 0.24f), new Vector3(0.18f, 0.12f, 0.18f), bodyMaterial)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            AddPart(gimbal.transform, PrimitiveType.Sphere, "Lens Glass", new Vector3(0f, 0f, 0.37f), Vector3.one * 0.16f, lensMaterial);

            // REC light on top.
            AddPart(root.transform, PrimitiveType.Cube, "Rec Light", new Vector3(0f, 0.24f, -0.32f), Vector3.one * 0.14f, recMaterial);

            return new Drone
            {
                Root = root.transform,
                Gimbal = gimbal.transform,
                Rotors = rotors,
                RecLightMaterial = recMaterial,
                TrimColor = trim,
                FilmingPost = root.transform.position,
                Subject = metrics.Center,
                NextCutAt = Time.time + 1.5f + index * 2.5f,
                BobPhase = index * 2.1f,
            };
        }

        private static GameObject AddPart(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            if (part.TryGetComponent<Collider>(out var collider))
            {
                Destroy(collider);
            }

            if (part.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            return part;
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

            SetEmission(material, emission);
            return material;
        }

        private static void SetEmission(Material material, Color emission)
        {
            if (material == null)
            {
                return;
            }

            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emission);
            }
        }
    }
}
// touch
