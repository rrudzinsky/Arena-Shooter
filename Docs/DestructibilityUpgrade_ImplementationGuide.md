# Destructibility Upgrade — Implementation Guide

**Audience:** an AI coding agent executing this guide step by step. Follow it to a T.
**Scope:** two features in `Assets/Scripts/ArenaShooter/DestructibleArenaPiece.cs` plus two new test files.

1. **Pillar corner bites** — shots on a `CornerPillar` piece carve jagged wedge "bites" out of the
   nearest vertical corner (the diagonal), with debris spraying outward along that diagonal.
   This replaces the current behavior where one shot deletes a full horizontal slab of the pillar
   and draws an arc stamp on a flat face.
2. **Wall cantilever collapse** — wall material that is left hanging like a too-long shelf
   (long horizontal reach, thin attachment neck) breaks off, spawns a falling slab + debris spray,
   and leaves a hole. Material attached only to the top edge of a wall also falls.

---

## 0. Rules of engagement

- **Never refactor, rename, or reorder existing code.** Only make the edits listed here.
  Every edit gives an **anchor** (exact existing code to find). If an anchor is not found
  verbatim, search for its method name, re-read that method, and adapt the anchor — do not guess.
- After **every** numbered step that changes code, run the **checkpoint** for that step.
- Work through parts in order: Part 1 → Part 2 → Part 3 → Part 4. Commit to git after each part
  passes its checkpoints (`git add -A; git commit -m "..."`).
- All new code goes in `Assets/Scripts/ArenaShooter/DestructibleArenaPiece.cs` unless a step says
  otherwise. Match the file's style: 4-space indent, explicit braces, `var`, private methods,
  no LINQ, no local functions, no comments inside method bodies.
- If something fails and the decision trees in §5 don't cover it, **stop and report** rather than
  improvising large changes.

### The verification loop (use after every step)

1. Recompile: call the Unity MCP tool `recompile_scripts`.
2. Check for compile errors: call `get_console_logs` (error filter). Zero errors required.
3. Where a checkpoint says "run tests", call the Unity MCP tool `run_tests` with **EditMode**
   and the stated filter. A full-suite EditMode run is required at the end of each part —
   **all pre-existing tests must stay green**, especially:
   - `DestructibleArenaPieceIslandTests`
   - `PillarOutlineTests`
   - `AllOutWarDomeScoreboardTests`, `AllOutWarTunnelGenerationTests`, `PulsePistolOutlineTests`

### How the current system works (context — read once, don't act)

- `DestructibleArenaPiece` has two damage paths chosen by `damageProfile`:
  - **Contour-owned walls** (`Wall` profile, non-Floor outline): every shot appends a jagged
    polygon `DamageStamp` to `wallDamageStamps`. The hole is the union of stamp polygons —
    purely position-based, no mesh booleans; a shader clips the wall body. After each hit,
    `RemoveUnsupportedContourOwnedWallIslands` scans a 2D grid over the wall plane
    (`UnsupportedIslandScanGrid`), finds material disconnected from "perimeter support",
    removes it with hidden cleanup stamps (`RenderContour = false`) and sprays chips.
    Today **all four wall edges count as support, including the top** — that is gravity-blind,
    which Part 3 fixes additively (the old rules stay; the new rule adds gravity failures).
  - **Chunk grid** (`CornerPillar`, `Floor`): the piece is split into box chunks
    (`TargetChunkSize` 0.68). Opening pillars are ~1×6×1 chunks (one chunk = a full horizontal
    slab). Today `DamageChunk` instantly sets `Destroyed = true` on the hit chunk, which deletes
    the whole slab — that's the wrong look Part 2 fixes.
- Coordinate quirk to respect: `GetFaceBasis` returns face axes where, for an X-normal wall,
  `u = Vector3.up` and `v = Vector3.forward` (U is vertical!), while for a Z-normal wall
  `u = Vector3.right` and `v = Vector3.up` (V is vertical). Part 3 handles both via
  `TryGetWallUvUpAxis`.
- Debris chips already exist (`SpawnWallMaterialSpray`) and are pure visual animations —
  **no Rigidbody, no colliders**. Keep it that way for everything new.

---

## Part 1 — Shared falling-debris machinery

### Step 1.1 — Add new tuning constants

**Anchor** (around line 56):

```csharp
        private const int WallHitSprayMinChips = 5;
        private const int WallHitSprayMaxChips = 9;
```

**Edit:** insert immediately **after** those two lines:

```csharp
        private const float PillarBiteSetbackPerHit = 0.30f;
        private const float PillarBiteMaxSetbackFraction = 0.46f;
        private const float PillarBiteDestroyAreaFraction = 0.30f;
        private const float CantileverSlendernessLimit = 2.6f;
        private const float CantileverMinimumOverhangReach = 0.2f;
        private const float CantileverMinimumNeckThickness = 0.06f;
        private const int CantileverMaxFailurePasses = 4;
        private const int MaxFallingSlabsPerDamage = 6;
        private const float FallingSlabMinimumArea = 0.02f;
        private const float FallingSlabLifetimeSeconds = 1.35f;
        private const float FallingSlabMaxTipDegreesPerSecond = 55f;
```

### Step 1.2 — Add the falling slab animation component

**Anchor:** the closing brace of the nested class `UnsupportedIslandSprayBurstCleanup`
(it ends with an `Update()` that destroys its gameObject; around line 405).

**Edit:** insert this whole class immediately **after** that class's closing brace, at the same
nesting level (inside `DestructibleArenaPiece`):

```csharp
        private sealed class FallingWallSlabAnimation : MonoBehaviour
        {
            private const float Gravity = 14f;
            private Vector3 velocity;
            private Vector3 tipAxis;
            private float tipDegreesPerSecond;
            private float duration;
            private float elapsed;
            private Vector3 startScale;

            public void Initialize(Vector3 initialVelocity, Vector3 tipAxisWorld, float tipSpeed, float lifetimeSeconds)
            {
                velocity = initialVelocity;
                tipAxis = tipAxisWorld.sqrMagnitude > 0.0001f ? tipAxisWorld.normalized : Vector3.right;
                tipDegreesPerSecond = tipSpeed;
                duration = Mathf.Max(0.1f, lifetimeSeconds);
                startScale = transform.localScale;
                elapsed = 0f;
            }

            private void Update()
            {
                elapsed += Time.deltaTime;
                velocity += Vector3.down * (Gravity * Time.deltaTime);
                transform.position += velocity * Time.deltaTime;
                transform.Rotate(tipAxis, tipDegreesPerSecond * Time.deltaTime, Space.World);
                var t = Mathf.Clamp01(elapsed / duration);
                var shrink = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, t));
                transform.localScale = startScale * Mathf.Max(0.001f, shrink);
                if (elapsed >= duration)
                {
                    Destroy(gameObject);
                }
            }
        }
```

### Step 1.3 — Add the shared slab spawn helpers

**Anchor:** the closing brace of the method `SpawnWallMaterialSpray` (it ends with
`sprayBudget--; return true;`; around line 2880).

**Edit:** insert immediately **after** that method:

```csharp
        private void SpawnFallingWallSlab(
            UnsupportedWallIsland island,
            Vector3 normal,
            Vector3 u,
            Vector3 v,
            float halfN,
            ref int slabBudget)
        {
            if (island == null ||
                island.Points == null ||
                island.Points.Count < 3 ||
                slabBudget <= 0 ||
                island.Area < FallingSlabMinimumArea)
            {
                return;
            }

            if (!TryBuildConvexHull(new List<Vector2>(island.Points), out var hull))
            {
                return;
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var centroid = island.Centroid;
            for (var i = 1; i < hull.Count - 1; i++)
            {
                AddTriangleOriented(
                    vertices,
                    triangles,
                    SlabPointToMeshLocal(hull[0], centroid, u, v, normal, halfN),
                    SlabPointToMeshLocal(hull[i], centroid, u, v, normal, halfN),
                    SlabPointToMeshLocal(hull[i + 1], centroid, u, v, normal, halfN),
                    normal);
                AddTriangleOriented(
                    vertices,
                    triangles,
                    SlabPointToMeshLocal(hull[0], centroid, u, v, normal, -halfN),
                    SlabPointToMeshLocal(hull[i], centroid, u, v, normal, -halfN),
                    SlabPointToMeshLocal(hull[i + 1], centroid, u, v, normal, -halfN),
                    -normal);
            }

            for (var i = 0; i < hull.Count; i++)
            {
                var a = hull[i];
                var b = hull[(i + 1) % hull.Count];
                var edge = b - a;
                if (edge.sqrMagnitude <= 0.0000001f)
                {
                    continue;
                }

                var outward2D = new Vector2(edge.y, -edge.x);
                if (Vector2.Dot(outward2D, (a + b) * 0.5f - centroid) < 0f)
                {
                    outward2D = -outward2D;
                }

                var outward = u * outward2D.x + v * outward2D.y;
                AddQuadOriented(
                    vertices,
                    triangles,
                    SlabPointToMeshLocal(a, centroid, u, v, normal, halfN),
                    SlabPointToMeshLocal(b, centroid, u, v, normal, halfN),
                    SlabPointToMeshLocal(b, centroid, u, v, normal, -halfN),
                    SlabPointToMeshLocal(a, centroid, u, v, normal, -halfN),
                    outward);
            }

            var mesh = CreateMesh("Falling Wall Slab Mesh", vertices, new[] { triangles });
            var seed = CalculateUnsupportedIslandSpraySeed(island, transform.TransformDirection(normal));
            var centerLocal = u * centroid.x + v * centroid.y;
            SpawnFallingDebrisObject(mesh, transform.TransformPoint(centerLocal), transform.rotation, seed);
            slabBudget--;
        }

        private static Vector3 SlabPointToMeshLocal(Vector2 point, Vector2 centroid, Vector3 u, Vector3 v, Vector3 normal, float depth)
        {
            return u * (point.x - centroid.x) + v * (point.y - centroid.y) + normal * depth;
        }

        private void SpawnFallingDebrisObject(Mesh mesh, Vector3 worldPosition, Quaternion worldRotation, int seed)
        {
            var slab = new GameObject("Falling Wall Slab");
            slab.transform.position = worldPosition;
            slab.transform.rotation = worldRotation;
            var meshFilter = slab.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            var renderer = slab.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetUnclippedDestructibleBodyMaterial();
            var outlineCategory = configuredOutlineCategory == StylizedOutlineCategory.None
                ? StylizedOutlineCategory.Wall
                : configuredOutlineCategory;
            DroidRenderSetup.ApplyRenderer(renderer, outlineCategory);

            var lateral = new Vector3(
                Mathf.Lerp(-0.35f, 0.35f, Hash01(seed ^ 0x3d1)),
                0f,
                Mathf.Lerp(-0.35f, 0.35f, Hash01(seed ^ 0x77b)));
            var tipAxis = Vector3.Cross(Vector3.up, lateral.sqrMagnitude > 0.0001f ? lateral.normalized : Vector3.forward);
            var tipSpeed = Mathf.Lerp(18f, FallingSlabMaxTipDegreesPerSecond, Hash01(seed ^ 0x215)) *
                (Hash01(seed ^ 0x6c2) > 0.5f ? 1f : -1f);
            slab.AddComponent<FallingWallSlabAnimation>().Initialize(lateral, tipAxis, tipSpeed, FallingSlabLifetimeSeconds);
        }
```

### Checkpoint 1

1. Recompile; zero console errors.
2. Run the full EditMode test suite. Everything that passed before must still pass
   (no behavior changed yet — these are additions only).
3. Commit: `Part 1: shared falling-slab machinery`.

---

## Part 2 — Pillar corner bites

### Step 2.1 — Extend the Chunk class

**Anchor** (inside `private sealed class Chunk`):

```csharp
            public float Damage;
            public bool Destroyed;
```

**Edit:** insert immediately **after** `public bool Destroyed;` (inside the class):

```csharp
            public readonly float[] CornerBiteSetbacks = new float[4];
            public bool HasCornerBite;
```

### Step 2.2 — Add corner tables

**Anchor:** the static array field `IslandScanNeighborOffsets` (top of the class, around line 61).
**Edit:** insert immediately **before** `private static readonly Vector2Int[] IslandScanNeighborOffsets =`:

```csharp
        private static readonly Vector2[] PillarCornerSigns =
        {
            new(1f, 1f),
            new(-1f, 1f),
            new(-1f, -1f),
            new(1f, -1f)
        };
```

### Step 2.3 — Route pillar damage into the bite path

**Anchor** (inside `TakeDamage(float, Vector3, Vector3, Collider)`):

```csharp
            var chunk = FindHitChunk(hitPoint);
            if (chunk == null || chunk.Destroyed)
            {
                return;
            }

            DamageChunk(chunk, amount, hitNormal, true);
            RebuildCombinedMesh();
```

**Edit:** replace that block with:

```csharp
            var chunk = FindHitChunk(hitPoint);
            if (chunk == null || chunk.Destroyed)
            {
                return;
            }

            if (damageProfile == DestructibleDamageProfile.CornerPillar)
            {
                DamagePillarChunkBite(chunk, amount, hitPoint, hitNormal);
                RebuildCombinedMesh();
                return;
            }

            DamageChunk(chunk, amount, hitNormal, true);
            RebuildCombinedMesh();
```

### Step 2.4 — Add the pillar bite methods

**Anchor:** the closing brace of the method `ResolveCornerPillarBiteAxis`
(ends with `return Vector2.right;`; around line 3966).

**Edit:** insert this entire block immediately **after** that method:

```csharp
        private static int GetPillarCornerIndex(float signX, float signZ)
        {
            if (signX > 0f)
            {
                return signZ > 0f ? 0 : 3;
            }

            return signZ > 0f ? 1 : 2;
        }

        private int CalculatePillarBiteSeed(Chunk chunk, int corner)
        {
            unchecked
            {
                var hash = 0x5bd1e995;
                hash = hash * 31 + HashPosition(transform.position);
                hash = hash * 31 + HashIndex(chunk.Index);
                hash = hash * 31 + corner;
                return hash;
            }
        }

        private void DamagePillarChunkBite(Chunk chunk, float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            var localHit = transform.InverseTransformPoint(hitPoint);
            var delta = localHit - chunk.LocalPosition;
            var corner = GetPillarCornerIndex(delta.x >= 0f ? 1f : -1f, delta.z >= 0f ? 1f : -1f);
            var horizontalMin = Mathf.Min(chunk.BaseScale.x, chunk.BaseScale.z);
            var setbackStep = horizontalMin * PillarBiteSetbackPerHit;
            var maxSetback = horizontalMin * PillarBiteMaxSetbackFraction;
            chunk.CornerBiteSetbacks[corner] = Mathf.Min(maxSetback, chunk.CornerBiteSetbacks[corner] + setbackStep);
            chunk.HasCornerBite = true;
            chunk.Damage += amount;
            var localNormal = transform.InverseTransformDirection(
                hitNormal.sqrMagnitude > 0.001f ? hitNormal.normalized : Vector3.forward);
            chunk.LastLocalNormal = ResolveDamagePlaneNormal(localNormal);
            SpawnPillarBiteSpray(chunk, corner);
            if (CalculatePillarBiteAreaFraction(chunk) >= PillarBiteDestroyAreaFraction)
            {
                DestroyPillarColumnFrom(chunk);
            }
        }

        private static float CalculatePillarBiteAreaFraction(Chunk chunk)
        {
            var horizontalMin = Mathf.Min(chunk.BaseScale.x, chunk.BaseScale.z);
            if (horizontalMin <= 0.01f)
            {
                return 1f;
            }

            var bitten = 0f;
            for (var i = 0; i < 4; i++)
            {
                bitten += 0.5f * chunk.CornerBiteSetbacks[i] * chunk.CornerBiteSetbacks[i];
            }

            return bitten / (horizontalMin * horizontalMin);
        }

        private void SpawnPillarBiteSpray(Chunk chunk, int corner)
        {
            var half = chunk.BaseScale * 0.5f;
            var sign = PillarCornerSigns[corner];
            var cornerOffset = new Vector3(sign.x * half.x, 0f, sign.y * half.z);
            if (cornerOffset.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var normalLocal = cornerOffset.normalized;
            var uLocal = Vector3.Cross(Vector3.up, normalLocal).normalized;
            var vLocal = Vector3.up;
            var cornerLocal = chunk.LocalPosition + cornerOffset;
            var centerU = Vector3.Dot(cornerLocal, uLocal);
            var centerV = Vector3.Dot(cornerLocal, vLocal);
            var radius = Mathf.Max(0.05f, Mathf.Min(half.x, half.z) * 0.55f);
            var points = new List<Vector2>(8);
            for (var i = 0; i < 8; i++)
            {
                var angle = i / 8f * Mathf.PI * 2f;
                var radiusScale = (i & 1) == 0 ? 1f : 0.66f;
                points.Add(new Vector2(centerU, centerV) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * radiusScale);
            }

            CalculatePolygonBounds(points, out var min, out var max);
            var island = new UnsupportedWallIsland(points, new Vector2(centerU, centerV), min, max, Mathf.PI * radius * radius * 0.7f);
            var halfN = Vector3.Dot(cornerLocal, normalLocal);
            var budget = 1;
            SpawnWallMaterialSpray(
                island,
                normalLocal,
                uLocal,
                vLocal,
                halfN,
                transform.TransformDirection(normalLocal),
                ref budget,
                WallHitSprayMinChips,
                WallHitSprayMaxChips);
        }

        private void DestroyPillarColumnFrom(Chunk chunk)
        {
            var columnChunks = new List<Chunk>();
            foreach (var candidate in chunks)
            {
                if (candidate.Index.x == chunk.Index.x &&
                    candidate.Index.z == chunk.Index.z &&
                    candidate.Index.y >= chunk.Index.y &&
                    !candidate.Destroyed)
                {
                    columnChunks.Add(candidate);
                }
            }

            if (columnChunks.Count == 0)
            {
                return;
            }

            var minY = float.PositiveInfinity;
            var maxY = float.NegativeInfinity;
            foreach (var columnChunk in columnChunks)
            {
                minY = Mathf.Min(minY, columnChunk.LocalPosition.y - columnChunk.BaseScale.y * 0.5f);
                maxY = Mathf.Max(maxY, columnChunk.LocalPosition.y + columnChunk.BaseScale.y * 0.5f);
                MarkPillarChunkConsumed(columnChunk);
            }

            SpawnFallingPillarSegment(chunk, minY, maxY);
        }

        private void MarkPillarChunkConsumed(Chunk chunk)
        {
            if (chunk.Destroyed)
            {
                return;
            }

            chunk.Destroyed = true;
            if (chunk.Stamp != null)
            {
                return;
            }

            GetFaceBasis(chunk.LastLocalNormal, chunk.BaseScale, out var normal, out var u, out var v, out var halfN, out _, out _);
            var centerU = Vector3.Dot(chunk.LocalPosition, u);
            var centerV = Vector3.Dot(chunk.LocalPosition, v);
            var points = new[]
            {
                new Vector2(centerU - 0.01f, centerV - 0.01f),
                new Vector2(centerU + 0.01f, centerV - 0.01f),
                new Vector2(centerU, centerV + 0.01f)
            };
            chunk.Stamp = new DamageStamp
            {
                Normal = normal,
                U = u,
                V = v,
                Plane = Vector3.Dot(chunk.LocalPosition, normal) + halfN + DamageContourInset,
                Min = new Vector2(centerU - 0.01f, centerV - 0.01f),
                Max = new Vector2(centerU + 0.01f, centerV + 0.01f),
                Points = points,
                RenderContour = false
            };
            chunk.OppositeStamp = CreateOppositeFaceDamageStamp(chunk.Stamp, chunk, normal, halfN);
        }

        private void SpawnFallingPillarSegment(Chunk chunk, float minY, float maxY)
        {
            var points = BuildBittenPillarCrossSection(chunk, 0f, null);
            if (points.Count < 3 || maxY - minY <= 0.01f)
            {
                return;
            }

            var halfHeight = (maxY - minY) * 0.5f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var i = 1; i < points.Count - 1; i++)
            {
                AddTriangleOriented(
                    vertices,
                    triangles,
                    new Vector3(points[0].x, halfHeight, points[0].y),
                    new Vector3(points[i].x, halfHeight, points[i].y),
                    new Vector3(points[i + 1].x, halfHeight, points[i + 1].y),
                    Vector3.up);
                AddTriangleOriented(
                    vertices,
                    triangles,
                    new Vector3(points[0].x, -halfHeight, points[0].y),
                    new Vector3(points[i].x, -halfHeight, points[i].y),
                    new Vector3(points[i + 1].x, -halfHeight, points[i + 1].y),
                    Vector3.down);
            }

            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                if ((b - a).sqrMagnitude <= 0.0000001f)
                {
                    continue;
                }

                var outward2D = GetCrossSectionEdgeOutward(a, b);
                AddQuadOriented(
                    vertices,
                    triangles,
                    new Vector3(a.x, -halfHeight, a.y),
                    new Vector3(b.x, -halfHeight, b.y),
                    new Vector3(b.x, halfHeight, b.y),
                    new Vector3(a.x, halfHeight, a.y),
                    new Vector3(outward2D.x, 0f, outward2D.y));
            }

            var mesh = CreateMesh("Falling Pillar Segment Mesh", vertices, new[] { triangles });
            var centerLocal = new Vector3(chunk.LocalPosition.x, (minY + maxY) * 0.5f, chunk.LocalPosition.z);
            SpawnFallingDebrisObject(mesh, transform.TransformPoint(centerLocal), transform.rotation, CalculatePillarBiteSeed(chunk, 0));
        }

        private List<Vector2> BuildBittenPillarCrossSection(Chunk chunk, float inflate, List<bool> edgeIsCut)
        {
            var halfX = chunk.BaseScale.x * 0.5f + inflate;
            var halfZ = chunk.BaseScale.z * 0.5f + inflate;
            var corners = new Vector2[4];
            for (var i = 0; i < 4; i++)
            {
                corners[i] = new Vector2(PillarCornerSigns[i].x * halfX, PillarCornerSigns[i].y * halfZ);
            }

            var points = new List<Vector2>(12);
            var cutFlags = new List<bool>(12);
            for (var i = 0; i < 4; i++)
            {
                var corner = corners[i];
                var previous = corners[(i + 3) % 4];
                var next = corners[(i + 1) % 4];
                var setback = Mathf.Min(
                    chunk.CornerBiteSetbacks[i],
                    (corner - previous).magnitude * 0.48f,
                    (next - corner).magnitude * 0.48f);
                if (setback <= 0.0001f)
                {
                    points.Add(corner);
                    cutFlags.Add(false);
                    continue;
                }

                var entry = corner - (corner - previous).normalized * setback;
                var exit = corner + (next - corner).normalized * setback;
                var seed = CalculatePillarBiteSeed(chunk, i);
                var mid = Vector2.Lerp((entry + exit) * 0.5f, corner, Mathf.Lerp(0.15f, 0.55f, Hash01(seed)));
                points.Add(entry);
                cutFlags.Add(true);
                points.Add(mid);
                cutFlags.Add(true);
                points.Add(exit);
                cutFlags.Add(false);
            }

            if (edgeIsCut != null)
            {
                edgeIsCut.Clear();
                edgeIsCut.AddRange(cutFlags);
            }

            return points;
        }

        private static Vector3 PillarCrossSectionToLocal(Chunk chunk, Vector2 point, float y)
        {
            return new Vector3(chunk.LocalPosition.x + point.x, y, chunk.LocalPosition.z + point.y);
        }

        private static Vector2 GetCrossSectionEdgeOutward(Vector2 a, Vector2 b)
        {
            var edge = b - a;
            var outward = new Vector2(edge.y, -edge.x);
            var mid = (a + b) * 0.5f;
            if (Vector2.Dot(outward, mid) < 0f)
            {
                outward = -outward;
            }

            return outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector2.right;
        }

        private void AddBittenPillarChunkGeometry(
            List<Vector3> vertices,
            List<int> triangles,
            List<Vector3> contourVertices,
            List<int> contourTriangles,
            Chunk chunk)
        {
            var edgeIsCut = new List<bool>();
            var points = BuildBittenPillarCrossSection(chunk, 0f, edgeIsCut);
            if (points.Count < 3)
            {
                return;
            }

            var halfY = chunk.BaseScale.y * 0.5f;
            var top = chunk.LocalPosition.y + halfY;
            var bottom = chunk.LocalPosition.y - halfY;
            for (var i = 1; i < points.Count - 1; i++)
            {
                AddTriangleOriented(
                    vertices,
                    triangles,
                    PillarCrossSectionToLocal(chunk, points[0], top),
                    PillarCrossSectionToLocal(chunk, points[i], top),
                    PillarCrossSectionToLocal(chunk, points[i + 1], top),
                    Vector3.up);
                AddTriangleOriented(
                    vertices,
                    triangles,
                    PillarCrossSectionToLocal(chunk, points[0], bottom),
                    PillarCrossSectionToLocal(chunk, points[i], bottom),
                    PillarCrossSectionToLocal(chunk, points[i + 1], bottom),
                    Vector3.down);
            }

            var neighborAboveBitten = chunksByIndex.TryGetValue(chunk.Index + Vector3Int.up, out var above) &&
                !above.Destroyed &&
                above.HasCornerBite;
            var neighborBelowBitten = chunksByIndex.TryGetValue(chunk.Index + Vector3Int.down, out var below) &&
                !below.Destroyed &&
                below.HasCornerBite;
            var thickness = Mathf.Clamp(
                Mathf.Min(chunk.BaseScale.x, chunk.BaseScale.y, chunk.BaseScale.z) * 0.5f * 0.08f,
                0.018f,
                0.05f);
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                if ((b - a).sqrMagnitude <= 0.0000001f)
                {
                    continue;
                }

                var outward2D = GetCrossSectionEdgeOutward(a, b);
                var outward = new Vector3(outward2D.x, 0f, outward2D.y);
                AddQuadOriented(
                    vertices,
                    triangles,
                    PillarCrossSectionToLocal(chunk, a, bottom),
                    PillarCrossSectionToLocal(chunk, b, bottom),
                    PillarCrossSectionToLocal(chunk, b, top),
                    PillarCrossSectionToLocal(chunk, a, top),
                    outward);

                if (!edgeIsCut[i])
                {
                    continue;
                }

                if (!neighborAboveBitten)
                {
                    AddContourSegment(
                        contourVertices,
                        contourTriangles,
                        PillarCrossSectionToLocal(chunk, a, top),
                        PillarCrossSectionToLocal(chunk, b, top),
                        Vector3.up,
                        thickness);
                }

                if (!neighborBelowBitten)
                {
                    AddContourSegment(
                        contourVertices,
                        contourTriangles,
                        PillarCrossSectionToLocal(chunk, a, bottom),
                        PillarCrossSectionToLocal(chunk, b, bottom),
                        Vector3.down,
                        thickness);
                }

                AddContourSegment(
                    contourVertices,
                    contourTriangles,
                    PillarCrossSectionToLocal(chunk, a, bottom),
                    PillarCrossSectionToLocal(chunk, a, top),
                    outward,
                    thickness);
                AddContourSegment(
                    contourVertices,
                    contourTriangles,
                    PillarCrossSectionToLocal(chunk, b, bottom),
                    PillarCrossSectionToLocal(chunk, b, top),
                    outward,
                    thickness);
            }
        }

        private void AddBittenCornerPillarOutlineSourceGeometry(List<Vector3> vertices, List<int> triangles)
        {
            foreach (var chunk in chunks)
            {
                if (chunk.Destroyed)
                {
                    continue;
                }

                var points = BuildBittenPillarCrossSection(chunk, OutlineShellInflation, null);
                if (points.Count < 3)
                {
                    continue;
                }

                var halfY = chunk.BaseScale.y * 0.5f + OutlineShellInflation;
                var top = chunk.LocalPosition.y + halfY;
                var bottom = chunk.LocalPosition.y - halfY;
                var suppressBottomCap = chunk.Index.y == 0;
                if (chunk.Index.y == 0)
                {
                    bottom -= CalculateWallOutlineFloorSeamSink(chunk.BaseScale.y);
                }

                for (var i = 1; i < points.Count - 1; i++)
                {
                    AddTriangleOriented(
                        vertices,
                        triangles,
                        PillarCrossSectionToLocal(chunk, points[0], top),
                        PillarCrossSectionToLocal(chunk, points[i], top),
                        PillarCrossSectionToLocal(chunk, points[i + 1], top),
                        Vector3.up);
                    if (!suppressBottomCap)
                    {
                        AddTriangleOriented(
                            vertices,
                            triangles,
                            PillarCrossSectionToLocal(chunk, points[0], bottom),
                            PillarCrossSectionToLocal(chunk, points[i], bottom),
                            PillarCrossSectionToLocal(chunk, points[i + 1], bottom),
                            Vector3.down);
                    }
                }

                for (var i = 0; i < points.Count; i++)
                {
                    var a = points[i];
                    var b = points[(i + 1) % points.Count];
                    if ((b - a).sqrMagnitude <= 0.0000001f)
                    {
                        continue;
                    }

                    var outward2D = GetCrossSectionEdgeOutward(a, b);
                    AddQuadOriented(
                        vertices,
                        triangles,
                        PillarCrossSectionToLocal(chunk, a, bottom),
                        PillarCrossSectionToLocal(chunk, b, bottom),
                        PillarCrossSectionToLocal(chunk, b, top),
                        PillarCrossSectionToLocal(chunk, a, top),
                        new Vector3(outward2D.x, 0f, outward2D.y));
                }
            }
        }
```

### Step 2.5 — Render bitten chunks in the combined mesh

**Anchor** (inside `RebuildCombinedMesh`, the chunk-grid branch):

```csharp
            foreach (var chunk in chunks)
            {
                if (!chunk.Destroyed)
                {
                    AddVisibleChunkSurface(vertices, triangles, chunk);
                }
            }
```

**Edit:** replace with:

```csharp
            foreach (var chunk in chunks)
            {
                if (chunk.Destroyed)
                {
                    continue;
                }

                if (damageProfile == DestructibleDamageProfile.CornerPillar && chunk.HasCornerBite)
                {
                    AddBittenPillarChunkGeometry(vertices, triangles, contourVertices, contourTriangles, chunk);
                    continue;
                }

                AddVisibleChunkSurface(vertices, triangles, chunk);
            }
```

### Step 2.6 — Keep the outline source honest for bitten pillars

**Edit A — Anchor** (inside `UsesIntactCornerPillarWallOutlineSourceGeometry`):

```csharp
            foreach (var chunk in chunks)
            {
                if (chunk.Destroyed)
                {
                    return false;
                }
            }
```

Replace with:

```csharp
            foreach (var chunk in chunks)
            {
                if (chunk.Destroyed || chunk.HasCornerBite)
                {
                    return false;
                }
            }
```

**Edit B — Anchor** (inside `RebuildOutlineSourceMesh`):

```csharp
            if (UsesIntactCornerPillarWallOutlineSourceGeometry())
            {
                AddCornerPillarWallOutlineSourceGeometry(vertices, triangles);
                outlineSourceMeshFilter.sharedMesh = vertices.Count == 0
                    ? null
                    : CreateMesh("Destructible Wall Outline Source Mesh", vertices, new[] { triangles });
                return;
            }
```

Insert immediately **after** that `if` block (before the `var outlineSolidChunks = ...` line):

```csharp
            if (damageProfile == DestructibleDamageProfile.CornerPillar &&
                configuredOutlineCategory != StylizedOutlineCategory.Floor)
            {
                AddBittenCornerPillarOutlineSourceGeometry(vertices, triangles);
                outlineSourceMeshFilter.sharedMesh = vertices.Count == 0
                    ? null
                    : CreateMesh("Destructible Wall Outline Source Mesh", vertices, new[] { triangles });
                return;
            }
```

### Step 2.7 — New test file

Create `Assets/Tests/EditMode/DestructiblePillarBiteTests.cs` with exactly this content:

```csharp
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class DestructiblePillarBiteTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags AllInstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly System.Type PieceType = System.Type.GetType("ArenaShooter.DestructibleArenaPiece, Assembly-CSharp");
    private static readonly System.Type ChunkType = PieceType.GetNestedType("Chunk", BindingFlags.NonPublic);
    private static readonly System.Type ProfileType = System.Type.GetType("ArenaShooter.DestructibleDamageProfile, Assembly-CSharp");
    private static readonly System.Type OutlineCategoryType = System.Type.GetType("ArenaShooter.StylizedOutlineCategory, Assembly-CSharp");

    private GameObject testObject;
    private Component piece;

    [SetUp]
    public void SetUp()
    {
        testObject = new GameObject("Destructible pillar bite test");
        piece = testObject.AddComponent(PieceType);
        ConfigurePillar(new Vector3(0.6f, 4f, 0.9f));
    }

    [TearDown]
    public void TearDown()
    {
        if (testObject != null)
        {
            Object.DestroyImmediate(testObject);
        }

        DestroyTransientObjects();
    }

    [Test]
    public void BiteOnNearestCornerCreatesSetbackWithoutDestroyingChunk()
    {
        InvokeTakeDamage(10f, new Vector3(0.29f, 1f, 0.44f), Vector3.right);

        var chunk = FindChunkWithBite();
        Assert.That(chunk, Is.Not.Null);
        var setbacks = GetSetbacks(chunk);
        Assert.That(setbacks[0], Is.GreaterThan(0f));
        Assert.That(setbacks[1], Is.Zero);
        Assert.That(setbacks[2], Is.Zero);
        Assert.That(setbacks[3], Is.Zero);
        Assert.That(GetChunkBool(chunk, "Destroyed"), Is.False);
        Assert.That(GetChunkBool(chunk, "HasCornerBite"), Is.True);
    }

    [Test]
    public void BiteSpawnsDebrisSprayFromTheDiagonalCorner()
    {
        InvokeTakeDamage(10f, new Vector3(0.29f, 1f, 0.44f), Vector3.right);

        var burst = GameObject.Find("Wall Material Spray Burst");
        Assert.That(burst, Is.Not.Null);
        var diagonal = new Vector3(1f, 0f, 1f).normalized;
        Assert.That(Vector3.Dot(burst.transform.position - testObject.transform.position, diagonal), Is.GreaterThan(0.2f));
        Assert.That(CountObjectsNamed("Wall Material Spray Chip"), Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public void BittenCrossSectionHasSixPointsAndStaysConvex()
    {
        InvokeTakeDamage(10f, new Vector3(0.29f, 1f, 0.44f), Vector3.right);

        var chunk = FindChunkWithBite();
        var points = InvokeBuildCrossSection(chunk);
        Assert.That(points.Count, Is.EqualTo(6));
        Assert.That(PolygonIsConvex(points), Is.True);
    }

    [Test]
    public void BiteRebuildsBodyAndContourMeshes()
    {
        InvokeTakeDamage(10f, new Vector3(0.29f, 1f, 0.44f), Vector3.right);

        var body = FindChild("Combined Destructible Wall Body");
        var contour = FindChild("Destructible Damage Contours");
        Assert.That(body.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(0));
        Assert.That(contour.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
        Assert.That(contour.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(0));
    }

    [Test]
    public void RepeatedBitesSeverSlabAndConsumeColumnAbove()
    {
        for (var i = 0; i < 12; i++)
        {
            var sign = SignsForIteration(i);
            InvokeTakeDamage(10f, new Vector3(0.29f * sign.x, 1f, 0.44f * sign.y), Vector3.right);
        }

        var severed = FindChunkAtHeight(1f);
        Assert.That(GetChunkBool(severed, "Destroyed"), Is.True);
        var aboveDestroyedCount = 0;
        foreach (var chunk in GetChunks())
        {
            var index = (Vector3Int)ChunkType.GetField("Index", AllInstanceFields).GetValue(chunk);
            var destroyed = GetChunkBool(chunk, "Destroyed");
            if (index.y > GetChunkIndex(severed).y)
            {
                Assert.That(destroyed, Is.True);
                aboveDestroyedCount++;
            }

            if (index.y < GetChunkIndex(severed).y)
            {
                Assert.That(destroyed, Is.False);
            }
        }

        Assert.That(aboveDestroyedCount, Is.GreaterThan(0));
        Assert.That(GameObject.Find("Falling Wall Slab"), Is.Not.Null);
    }

    [Test]
    public void FallingPillarSegmentHasAnimationAndNoPhysics()
    {
        for (var i = 0; i < 12; i++)
        {
            var sign = SignsForIteration(i);
            InvokeTakeDamage(10f, new Vector3(0.29f * sign.x, 1f, 0.44f * sign.y), Vector3.right);
        }

        var slab = GameObject.Find("Falling Wall Slab");
        Assert.That(slab, Is.Not.Null);
        Assert.That(slab.GetComponent<Rigidbody>(), Is.Null);
        Assert.That(slab.GetComponent<Collider>(), Is.Null);
        Assert.That(FindComponentByTypeName(slab, "FallingWallSlabAnimation"), Is.Not.Null);
    }

    private static Vector2 SignsForIteration(int i)
    {
        switch (i % 4)
        {
            case 0:
                return new Vector2(1f, 1f);
            case 1:
                return new Vector2(-1f, -1f);
            case 2:
                return new Vector2(1f, -1f);
            default:
                return new Vector2(-1f, 1f);
        }
    }

    private void ConfigurePillar(Vector3 size)
    {
        var configure = PieceType.GetMethod(
            "Configure",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(float), typeof(Vector3), typeof(Material), OutlineCategoryType, ProfileType, typeof(Vector3) },
            null);
        Assert.That(configure, Is.Not.Null);
        configure.Invoke(piece, new object[]
        {
            300f,
            size,
            null,
            System.Enum.Parse(OutlineCategoryType, "Wall"),
            System.Enum.Parse(ProfileType, "CornerPillar"),
            Vector3.right
        });
    }

    private void InvokeTakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        PieceType.GetMethod(
            "TakeDamage",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(float), typeof(Vector3), typeof(Vector3) },
            null).Invoke(piece, new object[] { amount, hitPoint, hitNormal });
    }

    private IList GetChunks()
    {
        return (IList)PieceType.GetField("chunks", PrivateInstance).GetValue(piece);
    }

    private object FindChunkWithBite()
    {
        foreach (var chunk in GetChunks())
        {
            if (GetChunkBool(chunk, "HasCornerBite"))
            {
                return chunk;
            }
        }

        return null;
    }

    private object FindChunkAtHeight(float y)
    {
        object best = null;
        var bestDistance = float.PositiveInfinity;
        foreach (var chunk in GetChunks())
        {
            var position = (Vector3)ChunkType.GetField("LocalPosition", AllInstanceFields).GetValue(chunk);
            var distance = Mathf.Abs(position.y - y);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = chunk;
            }
        }

        return best;
    }

    private Vector3Int GetChunkIndex(object chunk)
    {
        return (Vector3Int)ChunkType.GetField("Index", AllInstanceFields).GetValue(chunk);
    }

    private float[] GetSetbacks(object chunk)
    {
        return (float[])ChunkType.GetField("CornerBiteSetbacks", AllInstanceFields).GetValue(chunk);
    }

    private bool GetChunkBool(object chunk, string fieldName)
    {
        return (bool)ChunkType.GetField(fieldName, AllInstanceFields).GetValue(chunk);
    }

    private List<Vector2> InvokeBuildCrossSection(object chunk)
    {
        var method = PieceType.GetMethod("BuildBittenPillarCrossSection", PrivateInstance);
        return (List<Vector2>)method.Invoke(piece, new object[] { chunk, 0f, null });
    }

    private GameObject FindChild(string name)
    {
        foreach (var transform in testObject.GetComponentsInChildren<Transform>(true))
        {
            if (transform.gameObject.name == name)
            {
                return transform.gameObject;
            }
        }

        return null;
    }

    private static bool PolygonIsConvex(List<Vector2> points)
    {
        var sign = 0f;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            var c = points[(i + 2) % points.Count];
            var cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
            if (Mathf.Abs(cross) <= 0.000001f)
            {
                continue;
            }

            if (sign == 0f)
            {
                sign = Mathf.Sign(cross);
            }
            else if (Mathf.Sign(cross) != sign)
            {
                return false;
            }
        }

        return true;
    }

    private static Component FindComponentByTypeName(GameObject target, string typeName)
    {
        foreach (var component in target.GetComponents<Component>())
        {
            if (component.GetType().Name == typeName)
            {
                return component;
            }
        }

        return null;
    }

    private static int CountObjectsNamed(string name)
    {
        var count = 0;
        foreach (var candidate in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (candidate.name == name)
            {
                count++;
            }
        }

        return count;
    }

    private static void DestroyTransientObjects()
    {
        var doomed = new List<GameObject>();
        foreach (var candidate in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (candidate != null &&
                (candidate.name == "Wall Material Spray Burst" ||
                 candidate.name == "Wall Material Spray Chip" ||
                 candidate.name == "Falling Wall Slab"))
            {
                doomed.Add(candidate);
            }
        }

        foreach (var target in doomed)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
```

### Checkpoint 2

1. Recompile; zero errors. If `System.Enum.Parse(OutlineCategoryType, "Wall")` throws at test
   time because the type is null, grep for `enum StylizedOutlineCategory` to learn its namespace
   and fix the `Type.GetType` string.
2. Run EditMode tests filtered to `DestructiblePillarBiteTests` — all must pass.
3. Run the **full** EditMode suite. `PillarOutlineTests` must stay green. Known intentional
   behavior change: a single `TakeDamage` on a pillar now bites instead of deleting a slab;
   `PillarOutlineTests.DamagedOpeningPillarKeepsMatteBlackVisualPath` still passes because the
   bite updates the same body / outline source / damage contour objects.
4. Commit: `Part 2: pillar corner bites`.

---

## Part 3 — Wall cantilever collapse

Concept: build the existing scan grid, collapse each grid column (along world-up) into vertical
**runs** of solid cells. A run is **anchored** (hops = 0) if it touches the floor row or either
side edge of the wall (the top edge is *not* an anchor). BFS across horizontally-adjacent runs
counts **hops**; `reach = maxHops × horizontalStep` is how far material sticks out sideways from
support, and the **neck** is the total interface height where the overhang meets anchored
material. Fail when `reach > CantileverSlendernessLimit × neck` (or when a component can't reach
any anchor at all). Failed components are removed with the existing cleanup-stamp machinery and
spawn a falling slab + chip spray.

### Step 3.1 — Add the cantilever run class

**Anchor:** the closing brace of the nested class `UnsupportedWallIsland` (around line 346).
**Edit:** insert immediately **after** it, same nesting level:

```csharp
        private sealed class CantileverRun
        {
            public int HorizontalIndex;
            public int VerticalStart;
            public int VerticalEnd;
            public int Hops = int.MaxValue;
            public int ComponentLabel = -1;
        }
```

### Step 3.2 — Add the cantilever algorithm

**Anchor:** the closing brace of the method `RemoveUnsupportedContourOwnedWallIslands(Vector3 hitNormalWorld)`
(the overload taking a Vector3; it ends with the `if (!removedAnyIsland) { break; }` pass loop).

**Edit:** insert this entire block immediately **after** that method:

```csharp
        private bool TryGetWallUvUpAxis(Vector3 u, Vector3 v, out bool upIsU, out float upSign)
        {
            var uDot = Vector3.Dot(transform.TransformDirection(u), Vector3.up);
            if (Mathf.Abs(uDot) >= 0.5f)
            {
                upIsU = true;
                upSign = Mathf.Sign(uDot);
                return true;
            }

            var vDot = Vector3.Dot(transform.TransformDirection(v), Vector3.up);
            if (Mathf.Abs(vDot) >= 0.5f)
            {
                upIsU = false;
                upSign = Mathf.Sign(vDot);
                return true;
            }

            upIsU = false;
            upSign = 1f;
            return false;
        }

        private static int CantileverHorizontalCount(UnsupportedIslandScanGrid grid, bool upIsU)
        {
            return upIsU ? grid.Rows : grid.Columns;
        }

        private static int CantileverVerticalCount(UnsupportedIslandScanGrid grid, bool upIsU)
        {
            return upIsU ? grid.Columns : grid.Rows;
        }

        private static float CantileverHorizontalStep(UnsupportedIslandScanGrid grid, bool upIsU)
        {
            return upIsU ? grid.StepV : grid.StepU;
        }

        private static float CantileverVerticalStep(UnsupportedIslandScanGrid grid, bool upIsU)
        {
            return upIsU ? grid.StepU : grid.StepV;
        }

        private static int CantileverCellIndex(UnsupportedIslandScanGrid grid, bool upIsU, int horizontalIndex, int verticalIndex)
        {
            return upIsU ? grid.Index(verticalIndex, horizontalIndex) : grid.Index(horizontalIndex, verticalIndex);
        }

        private static List<CantileverRun>[] BuildCantileverRuns(UnsupportedIslandScanGrid grid, bool upIsU, float upSign)
        {
            var horizontalCount = CantileverHorizontalCount(grid, upIsU);
            var verticalCount = CantileverVerticalCount(grid, upIsU);
            var bottomVerticalIndex = upSign > 0f ? 0 : verticalCount - 1;
            var runsByColumn = new List<CantileverRun>[horizontalCount];
            for (var h = 0; h < horizontalCount; h++)
            {
                var runs = new List<CantileverRun>();
                runsByColumn[h] = runs;
                var runStart = -1;
                for (var w = 0; w <= verticalCount; w++)
                {
                    var solid = w < verticalCount && grid.Solid[CantileverCellIndex(grid, upIsU, h, w)];
                    if (solid && runStart < 0)
                    {
                        runStart = w;
                    }
                    else if (!solid && runStart >= 0)
                    {
                        var run = new CantileverRun
                        {
                            HorizontalIndex = h,
                            VerticalStart = runStart,
                            VerticalEnd = w - 1
                        };
                        if (h == 0 ||
                            h == horizontalCount - 1 ||
                            (bottomVerticalIndex >= runStart && bottomVerticalIndex <= w - 1))
                        {
                            run.Hops = 0;
                        }

                        runs.Add(run);
                        runStart = -1;
                    }
                }
            }

            return runsByColumn;
        }

        private static void ComputeCantileverHops(List<CantileverRun>[] runsByColumn)
        {
            var queue = new Queue<CantileverRun>();
            for (var h = 0; h < runsByColumn.Length; h++)
            {
                foreach (var run in runsByColumn[h])
                {
                    if (run.Hops == 0)
                    {
                        queue.Enqueue(run);
                    }
                }
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                RelaxCantileverNeighbors(runsByColumn, current, current.HorizontalIndex - 1, queue);
                RelaxCantileverNeighbors(runsByColumn, current, current.HorizontalIndex + 1, queue);
            }
        }

        private static void RelaxCantileverNeighbors(
            List<CantileverRun>[] runsByColumn,
            CantileverRun current,
            int neighborColumn,
            Queue<CantileverRun> queue)
        {
            if (neighborColumn < 0 || neighborColumn >= runsByColumn.Length)
            {
                return;
            }

            foreach (var neighbor in runsByColumn[neighborColumn])
            {
                if (neighbor.Hops != int.MaxValue ||
                    neighbor.VerticalStart > current.VerticalEnd ||
                    neighbor.VerticalEnd < current.VerticalStart)
                {
                    continue;
                }

                neighbor.Hops = current.Hops + 1;
                queue.Enqueue(neighbor);
            }
        }

        private static List<List<CantileverRun>> GatherCantileverOverhangComponents(List<CantileverRun>[] runsByColumn)
        {
            var components = new List<List<CantileverRun>>();
            for (var h = 0; h < runsByColumn.Length; h++)
            {
                foreach (var run in runsByColumn[h])
                {
                    if (run.Hops == 0 || run.ComponentLabel >= 0)
                    {
                        continue;
                    }

                    var component = new List<CantileverRun>();
                    var queue = new Queue<CantileverRun>();
                    run.ComponentLabel = components.Count;
                    queue.Enqueue(run);
                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        component.Add(current);
                        QueueCantileverComponentNeighbors(runsByColumn, current, current.HorizontalIndex - 1, components.Count, queue);
                        QueueCantileverComponentNeighbors(runsByColumn, current, current.HorizontalIndex + 1, components.Count, queue);
                    }

                    components.Add(component);
                }
            }

            return components;
        }

        private static void QueueCantileverComponentNeighbors(
            List<CantileverRun>[] runsByColumn,
            CantileverRun current,
            int neighborColumn,
            int label,
            Queue<CantileverRun> queue)
        {
            if (neighborColumn < 0 || neighborColumn >= runsByColumn.Length)
            {
                return;
            }

            foreach (var neighbor in runsByColumn[neighborColumn])
            {
                if (neighbor.Hops == 0 ||
                    neighbor.ComponentLabel >= 0 ||
                    neighbor.VerticalStart > current.VerticalEnd ||
                    neighbor.VerticalEnd < current.VerticalStart)
                {
                    continue;
                }

                neighbor.ComponentLabel = label;
                queue.Enqueue(neighbor);
            }
        }

        private static int MeasureCantileverNeckOverlap(List<CantileverRun>[] runsByColumn, CantileverRun run, int neighborColumn)
        {
            if (neighborColumn < 0 || neighborColumn >= runsByColumn.Length)
            {
                return 0;
            }

            var overlap = 0;
            foreach (var neighbor in runsByColumn[neighborColumn])
            {
                if (neighbor.Hops != 0)
                {
                    continue;
                }

                var start = Mathf.Max(run.VerticalStart, neighbor.VerticalStart);
                var end = Mathf.Min(run.VerticalEnd, neighbor.VerticalEnd);
                if (end >= start)
                {
                    overlap += end - start + 1;
                }
            }

            return overlap;
        }

        private static bool CantileverComponentShouldFall(
            List<CantileverRun>[] runsByColumn,
            List<CantileverRun> component,
            UnsupportedIslandScanGrid grid,
            bool upIsU)
        {
            var horizontalStep = CantileverHorizontalStep(grid, upIsU);
            var verticalStep = CantileverVerticalStep(grid, upIsU);
            var maxHops = 0;
            var disconnected = false;
            var neckThickness = 0f;
            foreach (var run in component)
            {
                if (run.Hops == int.MaxValue)
                {
                    disconnected = true;
                }
                else
                {
                    maxHops = Mathf.Max(maxHops, run.Hops);
                }

                if (run.Hops != 1)
                {
                    continue;
                }

                neckThickness += MeasureCantileverNeckOverlap(runsByColumn, run, run.HorizontalIndex - 1) * verticalStep;
                neckThickness += MeasureCantileverNeckOverlap(runsByColumn, run, run.HorizontalIndex + 1) * verticalStep;
            }

            if (disconnected)
            {
                return true;
            }

            var reach = maxHops * horizontalStep;
            if (reach < CantileverMinimumOverhangReach)
            {
                return false;
            }

            return reach > CantileverSlendernessLimit * Mathf.Max(neckThickness, CantileverMinimumNeckThickness);
        }

        private static List<int> CollectCantileverComponentCells(
            UnsupportedIslandScanGrid grid,
            bool upIsU,
            List<CantileverRun> component,
            bool[] componentMask)
        {
            var cells = new List<int>();
            foreach (var run in component)
            {
                for (var w = run.VerticalStart; w <= run.VerticalEnd; w++)
                {
                    var index = CantileverCellIndex(grid, upIsU, run.HorizontalIndex, w);
                    cells.Add(index);
                    componentMask[index] = true;
                }
            }

            return cells;
        }

        private static void ClearCantileverComponentMask(bool[] componentMask, List<int> cells)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                componentMask[cells[i]] = false;
            }
        }

        private void RemoveCantileverCollapsedWallSections(Vector3 hitNormalWorld)
        {
            if (!UsesContourOwnedWallDamage() ||
                wallDamageStamps.Count == 0 ||
                !TryGetContourOwnedWallBasis(0f, out var normal, out var u, out var v, out var halfN, out var bounds))
            {
                return;
            }

            if (!TryGetWallUvUpAxis(u, v, out var upIsU, out var upSign))
            {
                return;
            }

            var sprayDirectionWorld = hitNormalWorld.sqrMagnitude > 0.0001f
                ? (-hitNormalWorld.normalized + Vector3.down).normalized
                : (transform.TransformDirection(-normal) + Vector3.down).normalized;
            var slabBudget = MaxFallingSlabsPerDamage;
            var sprayBudget = MaxUnsupportedIslandSpraysPerDamage;
            for (var pass = 0; pass < CantileverMaxFailurePasses; pass++)
            {
                var grid = BuildUnsupportedIslandScanGrid(bounds);
                if (grid.Count == 0)
                {
                    return;
                }

                var runsByColumn = BuildCantileverRuns(grid, upIsU, upSign);
                ComputeCantileverHops(runsByColumn);
                var components = GatherCantileverOverhangComponents(runsByColumn);
                var removedAny = false;
                var componentMask = new bool[grid.Count];
                foreach (var component in components)
                {
                    if (!CantileverComponentShouldFall(runsByColumn, component, grid, upIsU))
                    {
                        continue;
                    }

                    var cells = CollectCantileverComponentCells(grid, upIsU, component, componentMask);
                    if (!TryCreateUnsupportedIslandFromCells(grid, cells, componentMask, out var island))
                    {
                        ClearCantileverComponentMask(componentMask, cells);
                        continue;
                    }

                    AddUnsupportedWallIslandCleanupStamp(island, normal, u, v, halfN);
                    SpawnFallingWallSlab(island, normal, u, v, halfN, ref slabBudget);
                    if (island.RequiresSpray && sprayBudget > 0)
                    {
                        SpawnUnsupportedWallIslandSpray(island, normal, u, v, halfN, sprayDirectionWorld, ref sprayBudget);
                    }

                    ClearCantileverComponentMask(componentMask, cells);
                    removedAny = true;
                }

                if (!removedAny)
                {
                    return;
                }
            }
        }
```

### Step 3.3 — Hook it into the damage flow

**Anchor:** the **end** of `RemoveUnsupportedContourOwnedWallIslands(Vector3 hitNormalWorld)` —
its final lines are:

```csharp
                if (!removedAnyIsland)
                {
                    break;
                }
            }
        }
```

**Edit:** insert a call before the method's closing brace, so the end becomes:

```csharp
                if (!removedAnyIsland)
                {
                    break;
                }
            }

            RemoveCantileverCollapsedWallSections(hitNormalWorld);
        }
```

### Step 3.4 — New test file

Create `Assets/Tests/EditMode/DestructibleArenaPieceCantileverTests.cs` with exactly this content:

```csharp
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class DestructibleArenaPieceCantileverTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags AllInstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly System.Type PieceType = System.Type.GetType("ArenaShooter.DestructibleArenaPiece, Assembly-CSharp");
    private static readonly System.Type DamageStampType = PieceType.GetNestedType("DamageStamp", BindingFlags.NonPublic);

    private GameObject testObject;
    private Component piece;

    [SetUp]
    public void SetUp()
    {
        testObject = new GameObject("Destructible cantilever test");
        piece = testObject.AddComponent(PieceType);
        SetConfiguredSize(new Vector3(10f, 6f, 0.5f));
    }

    [TearDown]
    public void TearDown()
    {
        if (testObject != null)
        {
            Object.DestroyImmediate(testObject);
        }

        DestroyTransientObjects();
    }

    [Test]
    public void ThinLintelAboveWideHoleFalls()
    {
        AddRectStamp(-4.5f, 4.5f, 0.8f, 2.2f);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(IsPointInsideWallDamageUnion(new Vector2(0f, 2.6f)), Is.True);
        Assert.That(CountHiddenCleanupStamps(), Is.GreaterThanOrEqualTo(1));
        Assert.That(GameObject.Find("Falling Wall Slab"), Is.Not.Null);
    }

    [Test]
    public void ThickLintelAboveNarrowHoleStays()
    {
        AddRectStamp(-0.8f, 0.8f, 0.8f, 1.6f);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(IsPointInsideWallDamageUnion(new Vector2(0f, 2.2f)), Is.False);
        Assert.That(CountHiddenCleanupStamps(), Is.Zero);
        Assert.That(GameObject.Find("Falling Wall Slab"), Is.Null);
    }

    [Test]
    public void LongThinSideSpikeFalls()
    {
        AddRectStamp(-5f, 5f, 0.3f, 3f);
        AddRectStamp(-5f, 5f, -3f, -0.3f);
        AddRectStamp(-2f, 5f, -0.35f, 0.35f);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(IsPointInsideWallDamageUnion(new Vector2(-3.5f, 0f)), Is.True);
        Assert.That(CountHiddenCleanupStamps(), Is.GreaterThanOrEqualTo(1));
        Assert.That(GameObject.Find("Falling Wall Slab"), Is.Not.Null);
    }

    [Test]
    public void FullHeightColumnFromFloorStays()
    {
        AddRectStamp(-5f, -0.5f, -3f, 3f);
        AddRectStamp(0.5f, 5f, -3f, 3f);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(IsPointInsideWallDamageUnion(new Vector2(0f, 0f)), Is.False);
        Assert.That(CountHiddenCleanupStamps(), Is.Zero);
        Assert.That(GameObject.Find("Falling Wall Slab"), Is.Null);
    }

    [Test]
    public void FragmentAttachedOnlyToTopEdgeFalls()
    {
        AddRectStamp(-5f, 5f, -3f, 2f);
        AddRectStamp(-5f, -1f, 2f, 3f);
        AddRectStamp(1f, 5f, 2f, 3f);

        InvokeRemoveUnsupportedWallIslands();

        Assert.That(IsPointInsideWallDamageUnion(new Vector2(0f, 2.5f)), Is.True);
        Assert.That(CountHiddenCleanupStamps(), Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void FallingSlabHasAnimationAndNoPhysics()
    {
        AddRectStamp(-4.5f, 4.5f, 0.8f, 2.2f);

        InvokeRemoveUnsupportedWallIslands();

        var slab = GameObject.Find("Falling Wall Slab");
        Assert.That(slab, Is.Not.Null);
        Assert.That(slab.GetComponent<Rigidbody>(), Is.Null);
        Assert.That(slab.GetComponent<Collider>(), Is.Null);
        Assert.That(slab.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
        Assert.That(FindComponentByTypeName(slab, "FallingWallSlabAnimation"), Is.Not.Null);
    }

    private void AddRectStamp(float minU, float maxU, float minV, float maxV)
    {
        var points = new[]
        {
            new Vector2(minU, minV),
            new Vector2(maxU, minV),
            new Vector2(maxU, maxV),
            new Vector2(minU, maxV)
        };
        var stamp = System.Activator.CreateInstance(DamageStampType, nonPublic: true);
        SetStampField(stamp, "Normal", Vector3.forward);
        SetStampField(stamp, "U", Vector3.right);
        SetStampField(stamp, "V", Vector3.up);
        SetStampField(stamp, "Plane", 1f);
        SetStampField(stamp, "Min", new Vector2(minU, minV));
        SetStampField(stamp, "Max", new Vector2(maxU, maxV));
        SetStampField(stamp, "Points", points);
        SetStampField(stamp, "RenderClosed", true);
        SetStampField(stamp, "RenderContour", true);
        GetStampList().Add(stamp);
    }

    private static void SetStampField(object stamp, string name, object value)
    {
        DamageStampType.GetField(name, AllInstanceFields).SetValue(stamp, value);
    }

    private IList GetStampList()
    {
        return (IList)PieceType.GetField("wallDamageStamps", PrivateInstance).GetValue(piece);
    }

    private void SetConfiguredSize(Vector3 size)
    {
        PieceType.GetField("configuredSize", PrivateInstance).SetValue(piece, size);
    }

    private void InvokeRemoveUnsupportedWallIslands()
    {
        PieceType.GetMethod(
            "RemoveUnsupportedContourOwnedWallIslands",
            PrivateInstance,
            null,
            System.Type.EmptyTypes,
            null).Invoke(piece, null);
    }

    private bool IsPointInsideWallDamageUnion(Vector2 point)
    {
        var method = PieceType.GetMethod("IsPointInsideWallDamageUnion", PrivateInstance);
        return (bool)method.Invoke(piece, new object[] { point });
    }

    private int CountHiddenCleanupStamps()
    {
        var count = 0;
        foreach (var stamp in GetStampList())
        {
            if (!(bool)DamageStampType.GetField("RenderContour", AllInstanceFields).GetValue(stamp))
            {
                count++;
            }
        }

        return count;
    }

    private static Component FindComponentByTypeName(GameObject target, string typeName)
    {
        foreach (var component in target.GetComponents<Component>())
        {
            if (component.GetType().Name == typeName)
            {
                return component;
            }
        }

        return null;
    }

    private static void DestroyTransientObjects()
    {
        var doomed = new System.Collections.Generic.List<GameObject>();
        foreach (var candidate in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (candidate != null &&
                (candidate.name == "Wall Material Spray Burst" ||
                 candidate.name == "Wall Material Spray Chip" ||
                 candidate.name == "Falling Wall Slab"))
            {
                doomed.Add(candidate);
            }
        }

        foreach (var target in doomed)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
```

### Checkpoint 3

1. Recompile; zero errors.
2. Run EditMode tests filtered to `DestructibleArenaPieceCantileverTests` — all must pass.
3. Run the **full** EditMode suite. `DestructibleArenaPieceIslandTests` must stay fully green —
   the cantilever pass runs after the island pass inside `RemoveUnsupportedContourOwnedWallIslands`,
   and was designed not to disturb those scenarios. If any island test regresses, see §5.3.
4. Commit: `Part 3: wall cantilever collapse`.

---

## Part 4 — Final acceptance

1. Full EditMode suite green (all old + 12 new tests).
2. Console clean after a recompile (no errors, no new warnings spamming).
3. Report to the user that in-game visual verification is needed (the agent cannot judge looks):
   - Shoot a doorway pillar near a corner → a jagged wedge disappears from that corner, neon rim
     around the cut, debris flies out along the diagonal.
   - Two bites on adjacent corners of the same slab look like two separate wedges.
   - Six-plus bites on one slab sever the pillar; the top section drops and fades.
   - Shoot a horizontal line of holes across a wall low down → the wall section above breaks off,
     falls as a slab, and the hole opens up.
   - Wall holes still merge when shots land next to each other (unchanged behavior).

## Tunables (all in DestructibleArenaPiece.cs)

| Constant | Effect | Raise it | Lower it |
|---|---|---|---|
| `PillarBiteSetbackPerHit` (0.30) | wedge growth per hit | fewer hits to chew a corner | more hits |
| `PillarBiteMaxSetbackFraction` (0.46) | max wedge size | deeper max bites | shallower |
| `PillarBiteDestroyAreaFraction` (0.30) | when a slab severs | harder to sever | easier |
| `CantileverSlendernessLimit` (2.6) | reach ÷ neck failure ratio | walls hold longer shelves | collapse sooner |
| `CantileverMinimumOverhangReach` (0.2) | ignore tiny ledges | fewer collapses | hair-trigger |
| `FallingSlabLifetimeSeconds` (1.35) | slab visual lifetime | longer falls | snappier |
| `MaxFallingSlabsPerDamage` (6) | slab spawn budget per damage event | more debris | cheaper |

## 5. Failure decision trees

### 5.1 Compile errors after an edit
- Read the error from `get_console_logs`. Find which step's code block it is in (search the
  method name). Re-compare your inserted code character-by-character against this guide.
- `CS0103 (name does not exist)`: you inserted a block before its dependency step, or an
  existing helper has a different name in the file. Grep for the helper (`AddContourSegment`,
  `TryBuildConvexHull`, `CalculatePolygonBounds`, `AddUnsupportedWallIslandCleanupStamp`,
  `TryCreateUnsupportedIslandFromCells`). If a signature differs, adapt the **call**, not the helper.
- `CS0111 (duplicate member)`: you inserted a block twice. Remove the duplicate.

### 5.2 New pillar tests fail
- `BiteOnNearestCornerCreatesSetback` fails with no bite found → check Step 2.3 (the routing
  `if` must come **after** the `chunk == null || chunk.Destroyed` early return).
- Spray assertions fail → confirm `SpawnPillarBiteSpray` passes
  `transform.TransformDirection(normalLocal)` as the spray direction and the burst name is
  exactly `"Wall Material Spray Burst"` (created inside the existing `SpawnWallMaterialSpray`).
- Sever test never destroys → log `CalculatePillarBiteAreaFraction` for the chunk after each hit;
  it must exceed 0.30 by hit ~6. If it plateaus below, the setbacks are being clamped — check
  `PillarBiteMaxSetbackFraction` and that the area formula divides by `horizontalMin * horizontalMin`
  (NOT by `BaseScale.x * BaseScale.z`).

### 5.3 Existing island tests regress after Part 3
- Identify the failing test. If the failure is "extra cleanup stamps" or "extra spray bursts",
  the cantilever pass is firing on material that should hold. Check, in this order:
  1. `BuildCantileverRuns` anchoring: side columns `h == 0` and `h == horizontalCount - 1`
     must be anchors, and the bottom check must use `bottomVerticalIndex` (sign-aware).
  2. `CantileverComponentShouldFall`: the `reach < CantileverMinimumOverhangReach` early-out
     must come **before** the slenderness comparison.
  3. Neck measurement must use `verticalStep` (not horizontal).
- If a test fails because objects leak between tests, ensure both new test files destroy
  `"Falling Wall Slab"` objects in teardown (the old test file does not know about slabs).

### 5.4 `run_tests` cannot find the new tests
- The file must be under `Assets/Tests/EditMode/` next to the existing test files (that folder
  already has an EditMode asmdef-equivalent setup). Recompile first, then re-run.

## Known accepted limitations (do NOT fix)

- The pillar outline proxy ignores per-corner bites until the first bite exists; after that it
  rebuilds from bitten prisms each damage event. Slight overdraw at chunk seams is expected and
  invisible (interior faces).
- Falling slabs use the convex hull of the broken region, so a concave broken piece falls as a
  slightly fuller slab than the hole it leaves. The hole itself is exact.
- Cantilever evaluation skips walls that are tilted more than ~60° from vertical
  (`TryGetWallUvUpAxis` returns false) — by design.
- Pillar bites do not shatter vertical neighbor chunks (each bite is local) — by design.
