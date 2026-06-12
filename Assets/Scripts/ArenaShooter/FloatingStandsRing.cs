using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArenaShooter
{
    /// <summary>
    /// Floating two-terrace grandstands around the playfield: five big levitating
    /// arc segments built like real stadium bowls — each terrace carries three
    /// stepped seating rows so heads rise above heads, a glowing fascia band runs
    /// across the lower lip, a canopy with a lit leading edge roofs the upper
    /// deck, and the whole segment pitches toward the arena so the crowd visibly
    /// looks DOWN at the action. Segments hover-bob gently and ride animated cyan
    /// jet pods (JetExhaustPlume). The ring radius and altitude scale with the
    /// PLAYFIELD (the dome bubble is a fixed 340 m regardless of map size, so
    /// anchoring to the bubble parked the stands ~265 m out — an illegible flat
    /// speck from any normal map).
    /// </summary>
    public static class FloatingStandsRing
    {
        public const int SegmentCount = 5;
        // 45 degrees of 72-degree spacing leaves 27-degree gaps between segments.
        public const float SegmentArcDegrees = 45f;
        // Outer clamp only: enormous maps still keep the ring inside the bubble.
        public const float RingRadiusFraction = 0.78f;
        // Pitch toward the arena centre so the terraces face the action instead
        // of presenting their undersides to the players below.
        public const float TiltDegrees = 12f;

        /// <summary>Ring radius resolved for the current match; the booth orbits inside it.</summary>
        public static float CurrentRingRadius { get; private set; } = 170f;
        /// <summary>Terrace altitude resolved for the current match; the booth floats near it.</summary>
        public static float CurrentPlatformAltitude { get; private set; } = 90f;

        // Radial offsets from the ring radius. The lower terrace reaches well in
        // toward the playfield; the upper terrace rises behind it and runs out
        // toward the dome shell.
        private const float LowerInnerOffset = -32f;
        private const float TerraceStepOffset = -6f;
        private const float UpperOuterOffset = 20f;
        private const float TerraceRise = 14f;
        private const float StepRise = 1.8f;
        private const float PlatformThickness = 3f;
        private const float BackWallHeight = 12f;
        private const int ArcSteps = 16;

        private static readonly Color PlatformTop = new Color(0.020f, 0.022f, 0.048f);
        private static readonly Color PlatformSide = new Color(0.007f, 0.007f, 0.018f);
        private static readonly Color WallColor = new Color(0.060f, 0.024f, 0.13f);
        private static readonly Color RailColor = new Color(0.25f, 1.5f, 1.7f);
        // LED fascia band across the lower terrace lip — the big glowing stroke
        // that makes the bowl read from the map.
        private static readonly Color FasciaColor = new Color(0.12f, 0.85f, 1.05f);
        private static readonly Color CrowdColor = new Color(0.008f, 0.009f, 0.022f);
        private static readonly Color[] GlowStickColors =
        {
            new Color(0.25f, 2.0f, 2.4f),
            new Color(2.4f, 0.2f, 2.0f),
            new Color(0.35f, 0.8f, 2.4f),
            new Color(2.4f, 0.3f, 0.3f),
        };

        internal static void Build(Transform parent, StadiumVisualMetrics metrics, float playfieldRadius)
        {
            var root = new GameObject("Floating Stands Ring");
            root.transform.SetParent(parent, false);
            root.transform.position = metrics.Center;

            // Park the lower terrace's front edge just outside the playable area
            // and ride high enough to sit clearly above the map horizon — but not
            // so high the players mostly see the undersides.
            CurrentRingRadius = Mathf.Clamp(playfieldRadius * 1.05f + 38f, 90f, metrics.Radius * RingRadiusFraction);
            CurrentPlatformAltitude = Mathf.Clamp(CurrentRingRadius * 0.52f, 46f, 100f);

            var material = CreateVertexColorMaterial();
            var rng = new System.Random(905531);
            for (var segment = 0; segment < SegmentCount; segment++)
            {
                var centerDegrees = segment * (360f / SegmentCount);
                BuildSegment(root.transform, material, rng, CurrentRingRadius, CurrentPlatformAltitude, centerDegrees, segment);
            }
        }

        private static void BuildSegment(Transform parent, Material material, System.Random rng, float radius, float altitude, float centerDegrees, int index)
        {
            var phi0 = (centerDegrees - SegmentArcDegrees * 0.5f) * Mathf.Deg2Rad;
            var phi1 = (centerDegrees + SegmentArcDegrees * 0.5f) * Mathf.Deg2Rad;
            var lowerInnerR = radius + LowerInnerOffset;
            var stepR = radius + TerraceStepOffset;
            var upperOuterR = radius + UpperOuterOffset;
            var lowerY = altitude;
            var upperY = altitude + TerraceRise;

            // Each segment lives on its own root, pivoted at the arc centre and
            // pitched about the ring tangent so the inner (playfield) edge dips
            // toward the action; structure, crowd and jet pods all ride it, and
            // the root hover-bobs so the whole stand floats.
            var phiCenter = centerDegrees * Mathf.Deg2Rad;
            var pivot = ArcPoint(radius, phiCenter, altitude);
            var tangent = new Vector3(-Mathf.Sin(phiCenter), 0f, Mathf.Cos(phiCenter));
            var segmentRoot = new GameObject($"Stand Segment {index + 1}");
            segmentRoot.transform.SetParent(parent, false);
            segmentRoot.transform.localPosition = pivot;
            segmentRoot.transform.localRotation = Quaternion.AngleAxis(TiltDegrees, tangent);
            var bob = segmentRoot.AddComponent<HoverBob>();
            bob.Amplitude = 0.45f;
            bob.CyclesPerSecond = 0.07f;
            bob.Phase = index * 0.27f;

            // ----- structure -----
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            // Three stepped treads per terrace, each rising behind the last so
            // the seating rakes like a real stadium bowl. Tread edges are pulled
            // a touch short of the riser/back walls to avoid coplanar faces.
            for (var step = 0; step < 3; step++)
            {
                var inner = Mathf.Lerp(lowerInnerR, stepR - 0.6f, step / 3f);
                var outer = Mathf.Lerp(lowerInnerR, stepR - 0.6f, (step + 1) / 3f);
                AddArcSlab(vertices, colors, triangles, inner, outer, lowerY - PlatformThickness, lowerY + step * StepRise, phi0, phi1, PlatformTop, PlatformSide);

                var upperInner = Mathf.Lerp(stepR - 0.2f, upperOuterR - 0.5f, step / 3f);
                var upperOuter = Mathf.Lerp(stepR - 0.2f, upperOuterR - 0.5f, (step + 1) / 3f);
                AddArcSlab(vertices, colors, triangles, upperInner, upperOuter, upperY - PlatformThickness, upperY + step * StepRise, phi0, phi1, PlatformTop, PlatformSide);
            }

            // Riser face between the terraces — the vertical step that makes the
            // two tiers read from the map.
            AddArcSlab(vertices, colors, triangles, stepR - 0.6f, stepR, lowerY, upperY, phi0, phi1, WallColor, WallColor);
            // Back wall along the outer edge.
            AddArcSlab(vertices, colors, triangles, upperOuterR - 0.6f, upperOuterR, upperY, upperY + BackWallHeight, phi0, phi1, WallColor, PlatformSide);
            // Canopy roofing the upper deck, with a glowing leading edge.
            var canopyY = upperY + BackWallHeight;
            AddArcSlab(vertices, colors, triangles, upperOuterR - 17f, upperOuterR + 0.5f, canopyY, canopyY + 0.9f, phi0, phi1, PlatformSide, PlatformSide);
            AddArcSlab(vertices, colors, triangles, upperOuterR - 17.7f, upperOuterR - 17f, canopyY - 0.15f, canopyY + 1.05f, phi0, phi1, RailColor, RailColor);
            // LED fascia band across the lower terrace's front face.
            AddArcSlab(vertices, colors, triangles, lowerInnerR - 0.15f, lowerInnerR + 0.1f, lowerY - 2.4f, lowerY - 0.9f, phi0, phi1, FasciaColor, FasciaColor);
            // Chunky neon hand rails along both terrace front edges.
            AddArcSlab(vertices, colors, triangles, lowerInnerR + 0.3f, lowerInnerR + 0.8f, lowerY + 1.1f, lowerY + 1.55f, phi0, phi1, RailColor, RailColor);
            AddArcSlab(vertices, colors, triangles, stepR + 0.3f, stepR + 0.8f, upperY + 1.1f, upperY + 1.55f, phi0, phi1, RailColor, RailColor);

            CreateMeshObject(segmentRoot.transform, material, "Structure", vertices, colors, triangles, pivot);

            // ----- crowd: one row per tread, stepping up the rake -----
            var crowdVertices = new List<Vector3>();
            var crowdColors = new List<Color>();
            var crowdTriangles = new List<int>();
            for (var step = 0; step < 3; step++)
            {
                var lowerRow = Mathf.Lerp(lowerInnerR, stepR - 0.6f, (step + 0.5f) / 3f);
                AddCrowdRow(crowdVertices, crowdColors, crowdTriangles, rng, lowerRow, lowerY + step * StepRise, phi0, phi1);
                var upperRow = Mathf.Lerp(stepR - 0.2f, upperOuterR - 0.5f, (step + 0.5f) / 3f);
                AddCrowdRow(crowdVertices, crowdColors, crowdTriangles, rng, upperRow, upperY + step * StepRise, phi0, phi1);
            }

            CreateMeshObject(segmentRoot.transform, material, "Crowd", crowdVertices, crowdColors, crowdTriangles, pivot);

            // ----- animated jet pods under the hull -----
            for (var pod = 0; pod < 5; pod++)
            {
                var phi = Mathf.Lerp(phi0, phi1, (pod + 0.5f) / 5f);
                var podPosition = ArcPoint(stepR, phi, lowerY - PlatformThickness - 1.1f) - pivot;
                JetExhaustPlume.Create(segmentRoot.transform, podPosition, 1f, $"Jet Pod {pod + 1}", -phi * Mathf.Rad2Deg - 90f);
            }
        }

        private static void AddCrowdRow(List<Vector3> vertices, List<Color> colors, List<int> triangles, System.Random rng, float rowRadius, float floorY, float phi0, float phi1)
        {
            // Oversized stylised spectators: human-scale silhouettes vanished at
            // stadium distance, so the crowd runs ~1.4x life size.
            var arcLength = (phi1 - phi0) * rowRadius;
            var seats = Mathf.Max(4, Mathf.FloorToInt(arcLength / 1.55f));
            for (var seat = 0; seat < seats; seat++)
            {
                if (rng.NextDouble() < 0.12)
                {
                    continue; // empty seat
                }

                var phi = Mathf.Lerp(phi0, phi1, (seat + 0.5f) / seats) + (float)(rng.NextDouble() - 0.5) * 0.004f;
                var lean = (float)(rng.NextDouble() - 0.5) * 0.3f;
                var height = 1.35f + (float)rng.NextDouble() * 0.40f;
                var shade = 0.8f + (float)rng.NextDouble() * 0.5f;
                var bodyColor = CrowdColor * shade;
                var basePoint = ArcPoint(rowRadius + (float)(rng.NextDouble() - 0.5) * 0.9f, phi, floorY);

                AddBox(vertices, colors, triangles, basePoint + new Vector3(lean * 0.2f, height * 0.5f, 0f), new Vector3(0.78f, height, 0.56f), phi, bodyColor);
                AddBox(vertices, colors, triangles, basePoint + new Vector3(lean * 0.3f, height + 0.24f, 0f), new Vector3(0.42f, 0.45f, 0.39f), phi, bodyColor);

                if (rng.NextDouble() < 0.22)
                {
                    var stickColor = GlowStickColors[rng.Next(GlowStickColors.Length)];
                    AddBox(vertices, colors, triangles, basePoint + new Vector3(lean * 0.3f + 0.45f, height + 0.78f, 0.11f), new Vector3(0.13f, 0.74f, 0.13f), phi, stickColor);
                }
            }
        }

        private static Vector3 ArcPoint(float radius, float phi, float y)
        {
            return new Vector3(Mathf.Cos(phi) * radius, y, Mathf.Sin(phi) * radius);
        }

        private static void AddArcSlab(List<Vector3> vertices, List<Color> colors, List<int> triangles, float innerR, float outerR, float y0, float y1, float phi0, float phi1, Color topColor, Color sideColor)
        {
            for (var step = 0; step < ArcSteps; step++)
            {
                var a = Mathf.Lerp(phi0, phi1, step / (float)ArcSteps);
                var b = Mathf.Lerp(phi0, phi1, (step + 1) / (float)ArcSteps);
                var innerA0 = ArcPoint(innerR, a, y0);
                var innerA1 = ArcPoint(innerR, a, y1);
                var innerB0 = ArcPoint(innerR, b, y0);
                var innerB1 = ArcPoint(innerR, b, y1);
                var outerA0 = ArcPoint(outerR, a, y0);
                var outerA1 = ArcPoint(outerR, a, y1);
                var outerB0 = ArcPoint(outerR, b, y0);
                var outerB1 = ArcPoint(outerR, b, y1);

                AddQuad(vertices, colors, triangles, innerA1, outerA1, outerB1, innerB1, topColor);   // top
                AddQuad(vertices, colors, triangles, innerA0, innerB0, outerB0, outerA0, sideColor);  // bottom
                AddQuad(vertices, colors, triangles, innerA0, innerA1, innerB1, innerB0, sideColor);  // inner face
                AddQuad(vertices, colors, triangles, outerA0, outerB0, outerB1, outerA1, sideColor);  // outer face
                if (step == 0)
                {
                    AddQuad(vertices, colors, triangles, innerA0, innerA1, outerA1, outerA0, sideColor);
                }

                if (step == ArcSteps - 1)
                {
                    AddQuad(vertices, colors, triangles, innerB0, outerB0, outerB1, innerB1, sideColor);
                }
            }
        }

        private static void AddBox(List<Vector3> vertices, List<Color> colors, List<int> triangles, Vector3 center, Vector3 size, float yawRadians, Color color)
        {
            // Box oriented so its local X axis points along the ring tangent.
            var rotation = Quaternion.Euler(0f, -yawRadians * Mathf.Rad2Deg - 90f, 0f);
            var half = size * 0.5f;
            var corners = new Vector3[8];
            for (var i = 0; i < 8; i++)
            {
                var local = new Vector3(
                    (i & 1) == 0 ? -half.x : half.x,
                    (i & 2) == 0 ? -half.y : half.y,
                    (i & 4) == 0 ? -half.z : half.z);
                corners[i] = center + rotation * local;
            }

            AddQuad(vertices, colors, triangles, corners[0], corners[1], corners[3], corners[2], color); // -z
            AddQuad(vertices, colors, triangles, corners[4], corners[6], corners[7], corners[5], color); // +z
            AddQuad(vertices, colors, triangles, corners[0], corners[2], corners[6], corners[4], color); // -x
            AddQuad(vertices, colors, triangles, corners[1], corners[5], corners[7], corners[3], color); // +x
            AddQuad(vertices, colors, triangles, corners[2], corners[3], corners[7], corners[6], color); // +y
            AddQuad(vertices, colors, triangles, corners[0], corners[4], corners[5], corners[1], color); // -y
        }

        private static void AddQuad(List<Vector3> vertices, List<Color> colors, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            var baseIndex = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            for (var i = 0; i < 4; i++)
            {
                colors.Add(color);
            }

            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
        }

        private static void CreateMeshObject(Transform parent, Material material, string name, List<Vector3> vertices, List<Color> colors, List<int> triangles, Vector3 pivot)
        {
            // Geometry is authored in ring space; rebasing it onto the segment
            // pivot lets the segment root pitch and bob the whole stand as one.
            for (var i = 0; i < vertices.Count; i++)
            {
                vertices[i] -= pivot;
            }

            var meshObject = new GameObject(name);
            meshObject.transform.SetParent(parent, false);
            var mesh = new Mesh { name = $"{parent.name} {name} Mesh" };
            mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = meshObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Material CreateVertexColorMaterial()
        {
            var shader = Shader.Find("ArenaShooter/DomeShellTint");
            if (shader != null)
            {
                return new Material(shader) { name = "Floating Stands Ring" };
            }

            shader = Shader.Find("Universal Render Pipeline/Unlit");
            return new Material(shader != null ? shader : Shader.Find("Standard")) { name = "Floating Stands Ring" };
        }
    }
}
