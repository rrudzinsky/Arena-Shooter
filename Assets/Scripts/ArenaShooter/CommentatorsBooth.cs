using UnityEngine;
using UnityEngine.Rendering;

namespace ArenaShooter
{
    /// <summary>
    /// The NEON SECTOR LEAGUE commentators booth: a glass-fronted broadcast pod
    /// that slowly cruises the perimeter of the playfield — inside the stands
    /// ring, banked gently into its orbit, glass front and logo strip always
    /// aimed down at the action like a touring camera platform. Two silhouetted
    /// commentators sit behind a lit back panel, the league logo strip runs under
    /// the glass, animated cyan jet pods carry the hull, and the interior light
    /// surges when the crowd cheers.
    /// </summary>
    public sealed class CommentatorsBooth : MonoBehaviour
    {
        public const string LeagueName = "NEON SECTOR LEAGUE";

        private const float BuildDelaySeconds = 1.6f;
        private const float OrbitStartDegrees = 36f;
        private const float OrbitDegreesPerSecond = 2.3f;
        // Lean into the direction of travel, like a vehicle in a gentle turn.
        private const float BankDegrees = -2.5f;
        private const float BobAmplitude = 1.2f;
        private const float BobCyclesPerSecond = 0.07f;
        private const float CheerRiseRate = 2.2f;
        private const float CheerFallRate = 0.4f;

        private static readonly Color BoothBlack = new Color(0.004f, 0.004f, 0.011f);
        private static readonly Color TrimCyan = new Color(0.15f, 0.92f, 1f);
        private static readonly Color InteriorWarm = new Color(1f, 0.74f, 0.42f);

        private StadiumVisualMetrics metrics;
        private Transform boothRoot;
        private Material interiorMaterial;
        private float orbitDegrees;
        private float orbitRadius;
        private float baseAltitude;
        private float cheerLevel;
        private float buildAt;
        private bool built;

        internal void Initialize(StadiumVisualMetrics stadiumMetrics)
        {
            metrics = stadiumMetrics;
            // The chandelier scoreboard spawns alongside the dome; wait for it so
            // the booth can hang underneath at the right height.
            buildAt = Time.time + BuildDelaySeconds;
        }

        private void Update()
        {
            if (!built)
            {
                if (Time.time >= buildAt)
                {
                    built = true;
                    BuildBooth();
                }

                return;
            }

            if (boothRoot == null)
            {
                return;
            }

            orbitDegrees += OrbitDegreesPerSecond * Time.deltaTime;
            UpdateOrbitPose();

            // Interior light surges with the crowd.
            var audio = ArenaAudio.Instance;
            var cheering = audio != null && audio.IsCrowdCheering ? 1f : 0f;
            cheerLevel = Mathf.MoveTowards(cheerLevel, cheering, (cheering > cheerLevel ? CheerRiseRate : CheerFallRate) * Time.deltaTime);
            if (interiorMaterial != null && interiorMaterial.HasProperty("_EmissionColor"))
            {
                interiorMaterial.SetColor("_EmissionColor", InteriorWarm * Mathf.Lerp(0.55f, 1.35f, cheerLevel));
            }
        }

        private void UpdateOrbitPose()
        {
            // Cruise the playfield perimeter, hover-bobbing, with the glass front
            // aimed down at the centre of the action and a slight bank into the
            // direction of travel.
            var phi = orbitDegrees * Mathf.Deg2Rad;
            var bob = Mathf.Sin(Time.time * BobCyclesPerSecond * Mathf.PI * 2f) * BobAmplitude;
            var position = metrics.Center + new Vector3(Mathf.Cos(phi) * orbitRadius, baseAltitude + bob, Mathf.Sin(phi) * orbitRadius);
            boothRoot.position = position;

            var lookTarget = metrics.Center + Vector3.up * 10f;
            boothRoot.rotation = Quaternion.LookRotation((lookTarget - position).normalized, Vector3.up)
                               * Quaternion.Euler(0f, 0f, BankDegrees);
        }

        private void BuildBooth()
        {
            // Orbit just inside the stands ring, clear of the lower terrace lips,
            // at terrace altitude so the pod patrols in front of the crowd.
            orbitRadius = Mathf.Max(55f, FloatingStandsRing.CurrentRingRadius - 55f);
            baseAltitude = FloatingStandsRing.CurrentPlatformAltitude + 6f;
            orbitDegrees = OrbitStartDegrees;

            boothRoot = new GameObject("NSL Commentators Booth").transform;
            boothRoot.SetParent(transform, false);
            // Scale with the stands ring so the booth (and its text) stays big
            // and legible from the map.
            var boothScale = Mathf.Clamp(FloatingStandsRing.CurrentRingRadius * 0.03f, 3.2f, 6.5f);
            boothRoot.localScale = Vector3.one * boothScale;

            var blackMaterial = CreateUnlitMaterial("IGL Booth Hull", BoothBlack, Color.black);
            var trimMaterial = CreateUnlitMaterial("IGL Booth Trim", TrimCyan * 0.5f, TrimCyan * 2.2f);
            var glassMaterial = CreateUnlitMaterial("IGL Booth Glass", new Color(0.025f, 0.05f, 0.105f), new Color(0.03f, 0.06f, 0.13f));
            interiorMaterial = CreateUnlitMaterial("IGL Booth Interior Light", InteriorWarm * 0.35f, InteriorWarm * 0.55f);
            var liveMaterial = CreateUnlitMaterial("IGL Booth Live Tag", new Color(1f, 0.1f, 0.45f) * 0.6f, new Color(1f, 0.1f, 0.45f) * 2.4f);

            // Hull with the glass face on +Z.
            AddPart(boothRoot, PrimitiveType.Cube, "Hull", new Vector3(0f, 0f, -0.6f), new Vector3(11f, 3.6f, 5.4f), blackMaterial);
            AddPart(boothRoot, PrimitiveType.Cube, "Glass Front", new Vector3(0f, 0.35f, 2.18f), new Vector3(10.4f, 2.0f, 0.12f), glassMaterial);
            AddPart(boothRoot, PrimitiveType.Cube, "Interior Back Light", new Vector3(0f, 0.35f, -1.9f), new Vector3(10f, 2.3f, 0.1f), interiorMaterial);

            // Trim edges framing the glass.
            AddPart(boothRoot, PrimitiveType.Cube, "Trim Top", new Vector3(0f, 1.5f, 2.2f), new Vector3(10.9f, 0.12f, 0.16f), trimMaterial);
            AddPart(boothRoot, PrimitiveType.Cube, "Trim Left", new Vector3(-5.32f, 0.35f, 2.2f), new Vector3(0.12f, 2.4f, 0.16f), trimMaterial);
            AddPart(boothRoot, PrimitiveType.Cube, "Trim Right", new Vector3(5.32f, 0.35f, 2.2f), new Vector3(0.12f, 2.4f, 0.16f), trimMaterial);

            // Desk with a glowing lip, just inside the glass.
            AddPart(boothRoot, PrimitiveType.Cube, "Desk", new Vector3(0f, -0.45f, 1.35f), new Vector3(9.6f, 0.5f, 1.3f), blackMaterial);
            AddPart(boothRoot, PrimitiveType.Cube, "Desk Lip", new Vector3(0f, -0.30f, 2.02f), new Vector3(9.7f, 0.10f, 0.10f), trimMaterial);

            BuildCommentator(boothRoot, blackMaterial, trimMaterial, -1.9f);
            BuildCommentator(boothRoot, blackMaterial, trimMaterial, 1.9f);

            // Logo strip under the glass, with the league name filling the band so
            // it reads from across the arena. The LIVE tag sits up in the glass
            // corner, broadcast-style — on the strip it collided with the name.
            AddPart(boothRoot, PrimitiveType.Cube, "Logo Strip", new Vector3(0f, -1.45f, 2.22f), new Vector3(11.2f, 1.2f, 0.10f), blackMaterial);
            AddPart(boothRoot, PrimitiveType.Cube, "Logo Strip Edge", new Vector3(0f, -2.12f, 2.24f), new Vector3(11.2f, 0.09f, 0.10f), trimMaterial);
            AddBoothText(boothRoot, "League Logo", LeagueName, new Vector3(0f, -1.45f, 2.30f), 0.85f, TrimCyan, TextAnchor.MiddleCenter);
            AddPart(boothRoot, PrimitiveType.Cube, "Live Tag Plate", new Vector3(4.4f, 1.05f, 2.26f), new Vector3(1.6f, 0.7f, 0.08f), liveMaterial);
            AddBoothText(boothRoot, "Live Tag", "LIVE", new Vector3(4.4f, 1.05f, 2.34f), 0.5f, Color.white, TextAnchor.MiddleCenter);

            // Animated jet pods carrying the hull (JetExhaustPlume drives the
            // flicker, stretch and sway every frame).
            for (var i = 0; i < 2; i++)
            {
                var sx = i == 0 ? 1f : -1f;
                JetExhaustPlume.Create(boothRoot, new Vector3(3.4f * sx, -2.3f, -0.6f), 0.55f, $"Jet Pod {i + 1}", 0f);
            }

            UpdateOrbitPose();
        }

        private void BuildCommentator(Transform parent, Material bodyMaterial, Material trimMaterial, float offsetX)
        {
            var seat = new GameObject($"Commentator {(offsetX < 0f ? "Left" : "Right")}").transform;
            seat.SetParent(parent, false);
            seat.localPosition = new Vector3(offsetX, -0.15f, 0.2f);

            AddPart(seat, PrimitiveType.Capsule, "Torso", Vector3.zero, new Vector3(0.95f, 0.55f, 0.6f), bodyMaterial);
            AddPart(seat, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.78f, 0f), Vector3.one * 0.46f, bodyMaterial);
            // Headset: ear cups plus a glowing mic tip.
            AddPart(seat, PrimitiveType.Cube, "Ear Cup L", new Vector3(-0.24f, 0.78f, 0f), new Vector3(0.10f, 0.16f, 0.16f), bodyMaterial);
            AddPart(seat, PrimitiveType.Cube, "Ear Cup R", new Vector3(0.24f, 0.78f, 0f), new Vector3(0.10f, 0.16f, 0.16f), bodyMaterial);
            AddPart(seat, PrimitiveType.Cube, "Headband", new Vector3(0f, 0.99f, 0f), new Vector3(0.5f, 0.06f, 0.10f), bodyMaterial);
            AddPart(seat, PrimitiveType.Sphere, "Mic Tip", new Vector3(-0.18f, 0.62f, 0.26f), Vector3.one * 0.07f, trimMaterial);
        }

        private static void AddBoothText(Transform parent, string name, string value, Vector3 localPosition, float letterHeight, Color color, TextAnchor anchor)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            // TextMesh glyphs read correctly from the local -Z side; the viewer
            // (the arena) is out past the +Z glass front, so flip the text to face
            // them or it renders mirrored from the map.
            textObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var text = textObject.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = anchor;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = letterHeight / (64f * 0.1f);
            text.color = color;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                text.font = font;
                if (textObject.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    renderer.sharedMaterial = font.material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
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
