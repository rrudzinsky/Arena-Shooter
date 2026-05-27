using System.Collections.Generic;
using UnityEngine;

namespace ArenaShooter
{
    public enum DestructibleDamageProfile
    {
        Wall,
        Floor,
        CornerPillar
    }

    public sealed class DestructibleArenaPiece : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 140f;

        private const float TargetChunkSize = 0.68f;
        private const int MaxCellsPerAxis = 16;
        private const float NeighborShatterDamage = 64f;
        private const float DamageContourInset = 0.006f;
        private const float OutlineShellInflation = 0.015f;
        private const int DamageStampSegments = 32;
        private const float DamageContourJaggedness = 0.26f;

        private readonly Dictionary<Vector3Int, Chunk> chunksByIndex = new();
        private readonly List<Chunk> chunks = new();
        private Vector3 configuredSize;
        private Material configuredIntactMaterial;
        private Material intactMaterial;
        private Material destructibleBodyMaterial;
        private Material damageContourMaterial;
        private Material outlineProxyMaterial;
        private StylizedOutlineCategory configuredOutlineCategory = StylizedOutlineCategory.None;
        private DestructibleDamageProfile damageProfile = DestructibleDamageProfile.Wall;
        private Vector3 configuredBiteDirectionLocal = Vector3.zero;
        private MeshFilter combinedMeshFilter;
        private MeshRenderer combinedRenderer;
        private MeshFilter outlineSourceMeshFilter;
        private MeshRenderer outlineSourceRenderer;
        private MeshFilter damageContourMeshFilter;
        private MeshRenderer damageContourRenderer;
        private BoxCollider surfaceCollider;
        private bool initialized;

        private sealed class Chunk
        {
            public Vector3Int Index;
            public Vector3 LocalPosition;
            public Vector3 BaseScale;
            public Vector3 LastLocalNormal = Vector3.forward;
            public DamageStamp Stamp;
            public DamageStamp OppositeStamp;
            public float Damage;
            public bool Destroyed;
        }

        private sealed class DamageStamp
        {
            public Vector3 Normal;
            public Vector3 U;
            public Vector3 V;
            public float Plane;
            public Vector2 Min;
            public Vector2 Max;
            public Vector2[] Points;
            public DamageStamp Opposite;
            public bool RenderClosed = true;
        }

        private readonly struct ContourSegment2D
        {
            public readonly DamageStamp Stamp;
            public readonly Vector2 Start;
            public readonly Vector2 End;

            public ContourSegment2D(DamageStamp stamp, Vector2 start, Vector2 end)
            {
                Stamp = stamp;
                Start = start;
                End = end;
            }
        }

        private sealed class ContourSegmentGroup
        {
            public readonly List<int> SegmentIndexes = new();
            public readonly Dictionary<Vector2Int, int> EndpointCounts = new();
            public Vector2 Min = new(float.PositiveInfinity, float.PositiveInfinity);
            public Vector2 Max = new(float.NegativeInfinity, float.NegativeInfinity);
            public Vector2 MidpointSum;

            public Vector2 Centroid => SegmentIndexes.Count > 0 ? MidpointSum / SegmentIndexes.Count : Vector2.zero;
            public Vector2 Span => Max - Min;
            public bool IsClosed
            {
                get
                {
                    if (SegmentIndexes.Count < 3)
                    {
                        return false;
                    }

                    foreach (var endpointCount in EndpointCounts.Values)
                    {
                        if (endpointCount < 2)
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }
        }

        public void Configure(float health)
        {
            Configure(health, Vector3.zero);
        }

        public void Configure(float health, Vector3 sourceSize)
        {
            Configure(health, sourceSize, null);
        }

        public void Configure(float health, Vector3 sourceSize, Material baseMaterial)
        {
            Configure(health, sourceSize, baseMaterial, StylizedOutlineCategory.None);
        }

        public void Configure(float health, Vector3 sourceSize, Material baseMaterial, StylizedOutlineCategory outlineCategory)
        {
            var profile = outlineCategory == StylizedOutlineCategory.Floor ? DestructibleDamageProfile.Floor : DestructibleDamageProfile.Wall;
            Configure(health, sourceSize, baseMaterial, outlineCategory, profile, Vector3.zero);
        }

        public void Configure(float health, Vector3 sourceSize, Material baseMaterial, StylizedOutlineCategory outlineCategory, DestructibleDamageProfile profile, Vector3 biteDirectionWorld)
        {
            maxHealth = Mathf.Max(1f, health);
            configuredSize = new Vector3(Mathf.Abs(sourceSize.x), Mathf.Abs(sourceSize.y), Mathf.Abs(sourceSize.z));
            configuredIntactMaterial = baseMaterial;
            configuredOutlineCategory = outlineCategory;
            damageProfile = profile;
            destructibleBodyMaterial = null;
            configuredBiteDirectionLocal = biteDirectionWorld.sqrMagnitude > 0.0001f
                ? transform.InverseTransformDirection(biteDirectionWorld.normalized)
                : Vector3.zero;
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, transform.position, Vector3.up, null);
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            TakeDamage(amount, hitPoint, hitNormal, null);
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, Collider hitCollider)
        {
            if (amount <= 0f)
            {
                return;
            }

            InitializeChunks();
            var chunk = FindHitChunk(hitPoint);
            if (chunk == null || chunk.Destroyed)
            {
                return;
            }

            DamageChunk(chunk, amount, hitNormal, true);
            RebuildCombinedMesh();
            SpawnImpactDebris(hitPoint, hitNormal, intactMaterial, 4);
        }

        public bool AllowsProjectilePassThrough(Vector3 hitPoint, Vector3 hitNormal)
        {
            if (!initialized)
            {
                return false;
            }

            var chunk = FindHitChunk(hitPoint);
            return chunk == null || chunk.Destroyed;
        }

        private void InitializeChunks()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            var originalRenderers = GetComponentsInChildren<Renderer>(true);
            intactMaterial = configuredIntactMaterial != null ? configuredIntactMaterial : ResolveLargestRendererMaterial(originalRenderers);
            var sourceSize = GetSourceSize();

            foreach (var renderer in originalRenderers)
            {
                renderer.enabled = false;
            }

            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            transform.localScale = Vector3.one;
            surfaceCollider = gameObject.AddComponent<BoxCollider>();
            surfaceCollider.size = sourceSize;
            surfaceCollider.center = Vector3.zero;

            var combined = new GameObject("Combined Destructible Wall Body");
            combined.transform.SetParent(transform, false);
            combinedMeshFilter = combined.AddComponent<MeshFilter>();
            combinedRenderer = combined.AddComponent<MeshRenderer>();
            combinedRenderer.sharedMaterial = GetDestructibleBodyMaterial();
            DroidRenderSetup.ApplyRenderer(combinedRenderer, StylizedOutlineCategory.None);

            if (configuredOutlineCategory != StylizedOutlineCategory.None && configuredOutlineCategory != StylizedOutlineCategory.Floor)
            {
                var outlineSource = new GameObject("Destructible Wall Outline Source");
                outlineSource.transform.SetParent(transform, false);
                outlineSourceMeshFilter = outlineSource.AddComponent<MeshFilter>();
                outlineSourceRenderer = outlineSource.AddComponent<MeshRenderer>();
                outlineSourceRenderer.sharedMaterial = GetOutlineProxyMaterial();
                DroidRenderSetup.ApplyRenderer(outlineSourceRenderer, configuredOutlineCategory);
                RebuildOutlineSourceMesh(sourceSize);
            }

            var contour = new GameObject("Destructible Damage Contours");
            contour.transform.SetParent(transform, false);
            damageContourMeshFilter = contour.AddComponent<MeshFilter>();
            damageContourRenderer = contour.AddComponent<MeshRenderer>();
            damageContourRenderer.sharedMaterial = GetDamageContourMaterial();
            DroidRenderSetup.ApplyRenderer(damageContourRenderer, StylizedOutlineCategory.None);

            BuildChunkGrid(sourceSize);
            RebuildCombinedMesh();
        }

        private void RebuildOutlineSourceMesh(Vector3 sourceSize)
        {
            if (outlineSourceMeshFilter == null)
            {
                return;
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var inflatedSize = sourceSize + Vector3.one * (OutlineShellInflation * 2f);
            AddSolidBlock(vertices, triangles, Vector3.zero, inflatedSize);
            outlineSourceMeshFilter.sharedMesh = CreateMesh("Destructible Wall Outline Source Mesh", vertices, new[] { triangles });
        }

        private Material ResolveLargestRendererMaterial(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
            {
                return null;
            }

            var best = renderers[0];
            var bestSize = 0f;
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMaterial == null)
                {
                    continue;
                }

                var size = renderer.bounds.size.sqrMagnitude;
                if (size > bestSize)
                {
                    best = renderer;
                    bestSize = size;
                }
            }

            return best.sharedMaterial;
        }

        private Material GetDestructibleBodyMaterial()
        {
            if (damageProfile == DestructibleDamageProfile.Floor)
            {
                return intactMaterial;
            }

            if (destructibleBodyMaterial != null)
            {
                return destructibleBodyMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return intactMaterial;
            }

            var color = GetMaterialBaseColor(intactMaterial, new Color(0.0015f, 0.001f, 0.004f, 1f));
            destructibleBodyMaterial = new Material(shader) { name = "Destructible Matte Body" };
            SetMaterialColor(destructibleBodyMaterial, color);
            return destructibleBodyMaterial;
        }

        private static Color GetMaterialBaseColor(Material material, Color fallback)
        {
            if (material == null)
            {
                return fallback;
            }

            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            if (material.HasProperty("_Color"))
            {
                return material.GetColor("_Color");
            }

            return fallback;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private Vector3 GetSourceSize()
        {
            if (configuredSize.x > 0.01f && configuredSize.y > 0.01f && configuredSize.z > 0.01f)
            {
                return configuredSize;
            }

            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                return new Vector3(Mathf.Abs(box.size.x), Mathf.Abs(box.size.y), Mathf.Abs(box.size.z));
            }

            return new Vector3(Mathf.Abs(transform.localScale.x), Mathf.Abs(transform.localScale.y), Mathf.Abs(transform.localScale.z));
        }

        private void BuildChunkGrid(Vector3 size)
        {
            var counts = new Vector3Int(
                CalculateCellCount(size.x),
                CalculateCellCount(size.y),
                CalculateCellCount(size.z));
            var cell = new Vector3(size.x / counts.x, size.y / counts.y, size.z / counts.z);
            var origin = -size * 0.5f + cell * 0.5f;

            for (var x = 0; x < counts.x; x++)
            {
                for (var y = 0; y < counts.y; y++)
                {
                    for (var z = 0; z < counts.z; z++)
                    {
                        var index = new Vector3Int(x, y, z);
                        var chunk = new Chunk
                        {
                            Index = index,
                            LocalPosition = origin + new Vector3(cell.x * x, cell.y * y, cell.z * z),
                            BaseScale = cell * 1.002f
                        };
                        chunks.Add(chunk);
                        chunksByIndex[index] = chunk;
                    }
                }
            }
        }

        private int CalculateCellCount(float size)
        {
            if (size <= 0.55f)
            {
                return 1;
            }

            return Mathf.Clamp(Mathf.RoundToInt(size / TargetChunkSize), 1, MaxCellsPerAxis);
        }

        private Chunk FindHitChunk(Vector3 hitPoint)
        {
            var localPoint = transform.InverseTransformPoint(hitPoint);
            var bestDistance = float.PositiveInfinity;
            Chunk best = null;
            foreach (var chunk in chunks)
            {
                var half = chunk.BaseScale * 0.5f;
                var delta = localPoint - chunk.LocalPosition;
                if (Mathf.Abs(delta.x) <= half.x && Mathf.Abs(delta.y) <= half.y && Mathf.Abs(delta.z) <= half.z)
                {
                    return chunk;
                }

                var distance = delta.sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = chunk;
                }
            }

            return best;
        }

        private void DamageChunk(Chunk chunk, float amount, Vector3 hitNormal, bool damageNeighbors)
        {
            var localNormal = transform.InverseTransformDirection(hitNormal.sqrMagnitude > 0.001f ? hitNormal.normalized : Vector3.up);
            chunk.LastLocalNormal = ResolveDamagePlaneNormal(localNormal);
            chunk.Damage += amount;
            chunk.Destroyed = true;
            EnsureDamageStamp(chunk);
            if (damageNeighbors)
            {
                DamageShatterNeighbors(chunk, amount * 0.55f);
            }
        }

        private void DamageShatterNeighbors(Chunk source, float amount)
        {
            GetPlaneNeighborDirections(source.LastLocalNormal, out var right, out var up);
            DamageShatterNeighbor(source, right, amount);
            DamageShatterNeighbor(source, -right, amount);
            DamageShatterNeighbor(source, up, amount);
            DamageShatterNeighbor(source, -up, amount);
            DamageShatterNeighbor(source, right + up, amount * 0.62f);
            DamageShatterNeighbor(source, right - up, amount * 0.62f);
            DamageShatterNeighbor(source, -right + up, amount * 0.62f);
            DamageShatterNeighbor(source, -right - up, amount * 0.62f);
        }

        private void DamageShatterNeighbor(Chunk source, Vector3Int offset, float amount)
        {
            if (!chunksByIndex.TryGetValue(source.Index + offset, out var neighbor) || neighbor.Destroyed)
            {
                return;
            }

            var variance = Mathf.Lerp(0.72f, 1.08f, Hash01(HashIndex(neighbor.Index) ^ HashIndex(source.Index)));
            neighbor.LastLocalNormal = source.LastLocalNormal;
            neighbor.Damage += amount * variance;
            if (neighbor.Damage >= NeighborShatterDamage)
            {
                neighbor.Destroyed = true;
                EnsureDamageStamp(neighbor);
            }
        }

        private void RebuildCombinedMesh()
        {
            if (combinedMeshFilter == null)
            {
                return;
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var contourVertices = new List<Vector3>();
            var contourTriangles = new List<int>();
            var visited = new HashSet<Chunk>();
            foreach (var chunk in chunks)
            {
                if (!chunk.Destroyed)
                {
                    AddVisibleSurface(vertices, triangles, chunk.LocalPosition, chunk.BaseScale);
                    continue;
                }

                if (!visited.Contains(chunk))
                {
                    AddMergedDamageGeometry(vertices, triangles, contourVertices, contourTriangles, GatherDestroyedContourComponent(chunk, visited));
                }
            }

            combinedMeshFilter.sharedMesh = CreateMesh("Combined Destructible Wall Mesh", vertices, new[] { triangles });
            if (damageContourMeshFilter == null)
            {
                return;
            }

            if (contourVertices.Count == 0)
            {
                damageContourMeshFilter.sharedMesh = null;
                return;
            }

            damageContourMeshFilter.sharedMesh = CreateMesh("Destructible Damage Contour Mesh", contourVertices, new[] { contourTriangles });
        }

        private void AddSolidBlock(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 size)
        {
            AddBoxFace(vertices, triangles, center, size, Vector3.right);
            AddBoxFace(vertices, triangles, center, size, Vector3.left);
            AddBoxFace(vertices, triangles, center, size, Vector3.up);
            AddBoxFace(vertices, triangles, center, size, Vector3.down);
            AddBoxFace(vertices, triangles, center, size, Vector3.forward);
            AddBoxFace(vertices, triangles, center, size, Vector3.back);
        }

        private List<Chunk> GatherDestroyedContourComponent(Chunk start, HashSet<Chunk> visited)
        {
            var result = new List<Chunk>();
            var queue = new Queue<Chunk>();
            var normalKey = GetNormalKey(start.LastLocalNormal);
            GetPlaneNeighborDirections(start.LastLocalNormal, out var right, out var up);

            visited.Add(start);
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var chunk = queue.Dequeue();
                result.Add(chunk);
                TryQueueContourNeighbor(chunk, right, normalKey, visited, queue);
                TryQueueContourNeighbor(chunk, -right, normalKey, visited, queue);
                TryQueueContourNeighbor(chunk, up, normalKey, visited, queue);
                TryQueueContourNeighbor(chunk, -up, normalKey, visited, queue);
            }

            return result;
        }

        private void TryQueueContourNeighbor(Chunk chunk, Vector3Int offset, int normalKey, HashSet<Chunk> visited, Queue<Chunk> queue)
        {
            if (!chunksByIndex.TryGetValue(chunk.Index + offset, out var neighbor) ||
                !neighbor.Destroyed ||
                visited.Contains(neighbor) ||
                GetNormalKey(neighbor.LastLocalNormal) != normalKey)
            {
                return;
            }

            visited.Add(neighbor);
            queue.Enqueue(neighbor);
        }

        private void AddMergedDamageGeometry(
            List<Vector3> bodyVertices,
            List<int> bodyTriangles,
            List<Vector3> contourVertices,
            List<int> contourTriangles,
            List<Chunk> component)
        {
            if (component == null || component.Count == 0)
            {
                return;
            }

            foreach (var chunk in component)
            {
                EnsureDamageStamp(chunk);
            }

            var half = component[0].BaseScale * 0.5f;
            var thickness = Mathf.Clamp(Mathf.Min(half.x, half.y, half.z) * 0.08f, 0.018f, 0.05f);
            var frontStamps = CollectDamageStamps(component, false);
            var frontSegments = BuildStampUnionDamageSegments(frontStamps, thickness);
            AddDamageContourSegments(contourVertices, contourTriangles, frontSegments, thickness);

            if (damageProfile != DestructibleDamageProfile.CornerPillar)
            {
                AddContourInteriorBridge(bodyVertices, bodyTriangles, frontSegments);
            }

            AddOppositeDamageContourSegments(contourVertices, contourTriangles, frontSegments, thickness);
        }

        private List<DamageStamp> CollectDamageStamps(List<Chunk> component, bool oppositeFace)
        {
            var stamps = new List<DamageStamp>();
            foreach (var chunk in component)
            {
                var stamp = oppositeFace ? chunk.OppositeStamp : chunk.Stamp;
                if (stamp != null)
                {
                    stamps.Add(stamp);
                }
            }

            return stamps;
        }

        private void EnsureDamageStamp(Chunk chunk)
        {
            if (chunk == null || chunk.Stamp != null)
            {
                return;
            }

            GetFaceBasis(chunk.LastLocalNormal, chunk.BaseScale, out var normal, out var u, out var v, out var halfN, out var halfU, out var halfV);
            var centerU = Vector3.Dot(chunk.LocalPosition, u);
            var centerV = Vector3.Dot(chunk.LocalPosition, v);
            var radiusU = halfU * 1.08f;
            var radiusV = halfV * 1.08f;
            var seed = CalculateDamageStampSeed(chunk);
            if (damageProfile == DestructibleDamageProfile.CornerPillar)
            {
                chunk.Stamp = CreateCornerPillarDamageStamp(chunk, normal, u, v, halfN, centerU, centerV, radiusU, radiusV, seed);
                chunk.OppositeStamp = CreateOppositeFaceDamageStamp(chunk.Stamp, chunk, normal, halfN);
                return;
            }

            var points = new Vector2[DamageStampSegments];
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var i = 0; i < DamageStampSegments; i++)
            {
                var angle = i / (float)DamageStampSegments * Mathf.PI * 2f;
                var coarse = Hash01(seed ^ (i * 92837111));
                var fine = Hash01(seed ^ (i * 689287499) ^ 0x421);
                var spike = Hash01(seed ^ (i * 283923481) ^ 0x13427);
                var alternating = (i & 1) == 0 ? -0.38f : 0.34f;
                var shardSpike = spike > 0.8f ? Mathf.Lerp(0.16f, 0.42f, spike) : 0f;
                var radiusScale = Mathf.Clamp(
                    1f + (alternating + Mathf.Lerp(-0.34f, 0.34f, coarse) + Mathf.Lerp(-0.16f, 0.16f, fine) + shardSpike) * DamageContourJaggedness,
                    0.76f,
                    1.24f);
                var point = new Vector2(
                    centerU + Mathf.Cos(angle) * radiusU * radiusScale,
                    centerV + Mathf.Sin(angle) * radiusV * radiusScale);
                points[i] = point;
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            chunk.Stamp = new DamageStamp
            {
                Normal = normal,
                U = u,
                V = v,
                Plane = Vector3.Dot(chunk.LocalPosition, normal) + halfN + DamageContourInset,
                Min = min,
                Max = max,
                Points = points
            };
            chunk.OppositeStamp = CreateOppositeFaceDamageStamp(chunk.Stamp, chunk, normal, halfN);
        }

        private DamageStamp CreateCornerPillarDamageStamp(Chunk chunk, Vector3 normal, Vector3 u, Vector3 v, float halfN, float centerU, float centerV, float radiusU, float radiusV, int seed)
        {
            var bite = ResolveCornerPillarBiteAxis(chunk, normal, u, v, centerU, centerV);
            var perpendicular = new Vector2(-bite.y, bite.x);
            var center = new Vector2(centerU, centerV) - bite * Mathf.Min(radiusU, radiusV) * 0.28f;
            const float arcDegrees = 220f;
            var arcSegments = DamageStampSegments - 1;
            var points = new Vector2[DamageStampSegments];
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (var i = 0; i < arcSegments; i++)
            {
                var t = arcSegments == 1 ? 0.5f : i / (float)(arcSegments - 1);
                var angle = Mathf.Lerp(-arcDegrees * 0.5f, arcDegrees * 0.5f, t) * Mathf.Deg2Rad;
                var direction = bite * Mathf.Cos(angle) + perpendicular * Mathf.Sin(angle);
                var coarse = Hash01(seed ^ (i * 92837111));
                var fine = Hash01(seed ^ (i * 689287499) ^ 0x421);
                var radiusScale = Mathf.Clamp(
                    1f + (Mathf.Lerp(-0.34f, 0.34f, coarse) + Mathf.Lerp(-0.16f, 0.16f, fine)) * DamageContourJaggedness,
                    0.78f,
                    1.18f);
                var point = center + new Vector2(direction.x * radiusU, direction.y * radiusV) * radiusScale;
                points[i] = point;
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            points[points.Length - 1] = center - bite * Mathf.Min(radiusU, radiusV) * 0.18f;
            min = Vector2.Min(min, points[points.Length - 1]);
            max = Vector2.Max(max, points[points.Length - 1]);

            return new DamageStamp
            {
                Normal = normal,
                U = u,
                V = v,
                Plane = Vector3.Dot(chunk.LocalPosition, normal) + halfN + DamageContourInset,
                Min = min,
                Max = max,
                Points = points,
                RenderClosed = false
            };
        }

        private DamageStamp CreateOppositeFaceDamageStamp(DamageStamp source, Chunk chunk, Vector3 normal, float halfN)
        {
            if (source == null)
            {
                return null;
            }

            var oppositeNormal = -normal;
            var opposite = new DamageStamp
            {
                Normal = oppositeNormal,
                U = source.U,
                V = source.V,
                Plane = Vector3.Dot(chunk.LocalPosition, oppositeNormal) + halfN + DamageContourInset,
                Min = source.Min,
                Max = source.Max,
                Points = source.Points,
                RenderClosed = source.RenderClosed
            };
            source.Opposite = opposite;
            opposite.Opposite = source;
            return opposite;
        }

        private Vector2 ResolveCornerPillarBiteAxis(Chunk chunk, Vector3 normal, Vector3 u, Vector3 v, float centerU, float centerV)
        {
            var biteDirection = configuredBiteDirectionLocal.sqrMagnitude > 0.0001f
                ? configuredBiteDirectionLocal.normalized
                : Vector3.zero;
            var bite = new Vector2(Vector3.Dot(biteDirection, u), Vector3.Dot(biteDirection, v));
            if (bite.sqrMagnitude > 0.0001f)
            {
                return bite.normalized;
            }

            var centerAxis = new Vector2(centerU, centerV);
            if (centerAxis.sqrMagnitude > 0.0001f)
            {
                return centerAxis.normalized;
            }

            var localOffset = chunk.LocalPosition - Vector3.Scale(chunk.LocalPosition, AbsVector(normal));
            bite = new Vector2(Vector3.Dot(localOffset, u), Vector3.Dot(localOffset, v));
            if (bite.sqrMagnitude > 0.0001f)
            {
                return bite.normalized;
            }

            return Vector2.right;
        }

        private void AddStampUnionDamageContour(List<Vector3> vertices, List<int> triangles, List<DamageStamp> stamps, float thickness)
        {
            AddDamageContourSegments(vertices, triangles, BuildStampUnionDamageSegments(stamps, thickness), thickness);
        }

        private List<ContourSegment2D> BuildStampUnionDamageSegments(List<DamageStamp> stamps, float thickness)
        {
            var segments = new List<ContourSegment2D>();
            for (var stampIndex = 0; stampIndex < stamps.Count; stampIndex++)
            {
                var stamp = stamps[stampIndex];
                if (stamp == null || stamp.Points == null || stamp.Points.Length < 3)
                {
                    continue;
                }

                var edgeCount = stamp.RenderClosed ? stamp.Points.Length : stamp.Points.Length - 2;
                for (var i = 0; i < edgeCount; i++)
                {
                    var next = stamp.RenderClosed ? (i + 1) % stamp.Points.Length : i + 1;
                    AddClippedStampEdge(segments, stamps, stampIndex, stamp, stamp.Points[i], stamp.Points[next]);
                }
            }

            RemoveSmallInteriorContourIslands(segments, stamps, thickness);
            return segments;
        }

        private void AddDamageContourSegments(List<Vector3> vertices, List<int> triangles, List<ContourSegment2D> segments, float thickness)
        {
            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                AddContourSegment(
                    vertices,
                    triangles,
                    DamageStampPointToLocal(segment.Stamp, segment.Start),
                    DamageStampPointToLocal(segment.Stamp, segment.End),
                    segment.Stamp.Normal,
                    thickness);
            }
        }

        private void AddContourInteriorBridge(List<Vector3> vertices, List<int> triangles, List<ContourSegment2D> frontSegments)
        {
            for (var i = 0; i < frontSegments.Count; i++)
            {
                var segment = frontSegments[i];
                var opposite = segment.Stamp != null ? segment.Stamp.Opposite : null;
                if (opposite == null)
                {
                    continue;
                }

                AddInteriorBridgeSegment(
                    vertices,
                    triangles,
                    DamageStampPointToLocal(segment.Stamp, segment.Start),
                    DamageStampPointToLocal(segment.Stamp, segment.End),
                    DamageStampPointToLocal(opposite, segment.Start),
                    DamageStampPointToLocal(opposite, segment.End));
            }
        }

        private void AddOppositeDamageContourSegments(List<Vector3> vertices, List<int> triangles, List<ContourSegment2D> frontSegments, float thickness)
        {
            for (var i = 0; i < frontSegments.Count; i++)
            {
                var segment = frontSegments[i];
                var opposite = segment.Stamp != null ? segment.Stamp.Opposite : null;
                if (opposite == null)
                {
                    continue;
                }

                AddContourSegment(
                    vertices,
                    triangles,
                    DamageStampPointToLocal(opposite, segment.Start),
                    DamageStampPointToLocal(opposite, segment.End),
                    opposite.Normal,
                    thickness);
            }
        }

        private void AddClippedStampEdge(List<ContourSegment2D> segments, List<DamageStamp> stamps, int ownerIndex, DamageStamp ownerStamp, Vector2 start, Vector2 end)
        {
            var cuts = new List<float> { 0f, 1f };
            var edgeMin = Vector2.Min(start, end);
            var edgeMax = Vector2.Max(start, end);

            for (var i = 0; i < stamps.Count; i++)
            {
                if (i == ownerIndex)
                {
                    continue;
                }

                var other = stamps[i];
                if (other == null ||
                    other.Points == null ||
                    other.Points.Length < 3 ||
                    !BoundsOverlap(edgeMin, edgeMax, other.Min, other.Max))
                {
                    continue;
                }

                AddSegmentPolygonIntersectionCuts(start, end, other.Points, cuts);
            }

            cuts.Sort();
            for (var i = 0; i < cuts.Count - 1; i++)
            {
                var t0 = cuts[i];
                var t1 = cuts[i + 1];
                if (t1 - t0 <= 0.0001f)
                {
                    continue;
                }

                var midpoint = Vector2.Lerp(start, end, (t0 + t1) * 0.5f);
                if (IsPointInsideOtherDamageStamp(midpoint, stamps, ownerIndex))
                {
                    continue;
                }

                segments.Add(new ContourSegment2D(ownerStamp, Vector2.Lerp(start, end, t0), Vector2.Lerp(start, end, t1)));
            }
        }

        private void RemoveSmallInteriorContourIslands(List<ContourSegment2D> segments, List<DamageStamp> stamps, float thickness)
        {
            if (damageProfile == DestructibleDamageProfile.CornerPillar || segments.Count < 4 || CountClosedStamps(stamps) < 2)
            {
                return;
            }

            var smallIslandSpanLimit = CalculateSmallInteriorIslandSpanLimit(stamps);
            if (smallIslandSpanLimit <= 0f)
            {
                return;
            }

            var groups = BuildContourSegmentGroups(segments, Mathf.Max(0.002f, thickness * 0.35f));
            if (groups.Count < 2)
            {
                return;
            }

            var remove = new bool[segments.Count];
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                var span = group.Span;
                if (Mathf.Max(span.x, span.y) > smallIslandSpanLimit)
                {
                    continue;
                }

                var centroid = group.Centroid;
                for (var j = 0; j < groups.Count; j++)
                {
                    if (i == j ||
                        !groups[j].IsClosed ||
                        groups[j].SegmentIndexes.Count <= group.SegmentIndexes.Count ||
                        !BoundsContainPoint(groups[j].Min, groups[j].Max, centroid) ||
                        !IsPointInsideContourSegmentGroup(centroid, segments, groups[j]))
                    {
                        continue;
                    }

                    foreach (var segmentIndex in group.SegmentIndexes)
                    {
                        remove[segmentIndex] = true;
                    }

                    break;
                }
            }

            for (var i = segments.Count - 1; i >= 0; i--)
            {
                if (remove[i])
                {
                    segments.RemoveAt(i);
                }
            }
        }

        private static int CountClosedStamps(List<DamageStamp> stamps)
        {
            var count = 0;
            foreach (var stamp in stamps)
            {
                if (stamp != null && stamp.RenderClosed)
                {
                    count++;
                }
            }

            return count;
        }

        private static float CalculateSmallInteriorIslandSpanLimit(List<DamageStamp> stamps)
        {
            var smallestStampSpan = float.PositiveInfinity;
            foreach (var stamp in stamps)
            {
                if (stamp == null || !stamp.RenderClosed)
                {
                    continue;
                }

                var span = stamp.Max - stamp.Min;
                smallestStampSpan = Mathf.Min(smallestStampSpan, Mathf.Max(span.x, span.y));
            }

            return float.IsPositiveInfinity(smallestStampSpan) ? 0f : smallestStampSpan * 0.95f;
        }

        private static List<ContourSegmentGroup> BuildContourSegmentGroups(List<ContourSegment2D> segments, float snapStep)
        {
            var endpointToSegments = new Dictionary<Vector2Int, List<int>>();
            for (var i = 0; i < segments.Count; i++)
            {
                AddEndpointSegment(endpointToSegments, SnapContourPoint(segments[i].Start, snapStep), i);
                AddEndpointSegment(endpointToSegments, SnapContourPoint(segments[i].End, snapStep), i);
            }

            var groups = new List<ContourSegmentGroup>();
            var visited = new bool[segments.Count];
            var queue = new Queue<int>();
            for (var i = 0; i < segments.Count; i++)
            {
                if (visited[i])
                {
                    continue;
                }

                var group = new ContourSegmentGroup();
                visited[i] = true;
                queue.Enqueue(i);
                while (queue.Count > 0)
                {
                    var segmentIndex = queue.Dequeue();
                    AddSegmentToGroup(group, segments[segmentIndex], segmentIndex, snapStep);
                    QueueConnectedContourSegments(segments[segmentIndex].Start, snapStep, endpointToSegments, visited, queue);
                    QueueConnectedContourSegments(segments[segmentIndex].End, snapStep, endpointToSegments, visited, queue);
                }

                groups.Add(group);
            }

            return groups;
        }

        private static void AddEndpointSegment(Dictionary<Vector2Int, List<int>> endpointToSegments, Vector2Int endpoint, int segmentIndex)
        {
            if (!endpointToSegments.TryGetValue(endpoint, out var segmentIndexes))
            {
                segmentIndexes = new List<int>();
                endpointToSegments[endpoint] = segmentIndexes;
            }

            segmentIndexes.Add(segmentIndex);
        }

        private static void AddSegmentToGroup(ContourSegmentGroup group, ContourSegment2D segment, int segmentIndex, float snapStep)
        {
            group.SegmentIndexes.Add(segmentIndex);
            group.Min = Vector2.Min(group.Min, Vector2.Min(segment.Start, segment.End));
            group.Max = Vector2.Max(group.Max, Vector2.Max(segment.Start, segment.End));
            group.MidpointSum += (segment.Start + segment.End) * 0.5f;
            AddGroupEndpoint(group, SnapContourPoint(segment.Start, snapStep));
            AddGroupEndpoint(group, SnapContourPoint(segment.End, snapStep));
        }

        private static void AddGroupEndpoint(ContourSegmentGroup group, Vector2Int endpoint)
        {
            group.EndpointCounts.TryGetValue(endpoint, out var count);
            group.EndpointCounts[endpoint] = count + 1;
        }

        private static void QueueConnectedContourSegments(Vector2 point, float snapStep, Dictionary<Vector2Int, List<int>> endpointToSegments, bool[] visited, Queue<int> queue)
        {
            if (!endpointToSegments.TryGetValue(SnapContourPoint(point, snapStep), out var segmentIndexes))
            {
                return;
            }

            foreach (var segmentIndex in segmentIndexes)
            {
                if (visited[segmentIndex])
                {
                    continue;
                }

                visited[segmentIndex] = true;
                queue.Enqueue(segmentIndex);
            }
        }

        private static Vector2Int SnapContourPoint(Vector2 point, float snapStep)
        {
            return new Vector2Int(Mathf.RoundToInt(point.x / snapStep), Mathf.RoundToInt(point.y / snapStep));
        }

        private static bool BoundsContainPoint(Vector2 min, Vector2 max, Vector2 point)
        {
            return point.x > min.x && point.x < max.x && point.y > min.y && point.y < max.y;
        }

        private static bool IsPointInsideContourSegmentGroup(Vector2 point, List<ContourSegment2D> segments, ContourSegmentGroup group)
        {
            var inside = false;
            foreach (var segmentIndex in group.SegmentIndexes)
            {
                var start = segments[segmentIndex].Start;
                var end = segments[segmentIndex].End;
                var denominator = end.y - start.y;
                if ((start.y > point.y) != (end.y > point.y) &&
                    Mathf.Abs(denominator) > 0.000001f &&
                    point.x < (end.x - start.x) * (point.y - start.y) / denominator + start.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private void AddSegmentPolygonIntersectionCuts(Vector2 start, Vector2 end, Vector2[] polygon, List<float> cuts)
        {
            for (var i = 0; i < polygon.Length; i++)
            {
                var next = (i + 1) % polygon.Length;
                if (TrySegmentIntersectionParameter(start, end, polygon[i], polygon[next], out var t))
                {
                    AddUniqueCut(cuts, t);
                }
            }
        }

        private static bool TrySegmentIntersectionParameter(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out float t)
        {
            t = 0f;
            var r = b - a;
            var s = d - c;
            var denominator = Cross2D(r, s);
            if (Mathf.Abs(denominator) <= 0.000001f)
            {
                return false;
            }

            var delta = c - a;
            t = Cross2D(delta, s) / denominator;
            var u = Cross2D(delta, r) / denominator;
            return t > 0.0001f && t < 0.9999f && u > 0.0001f && u < 0.9999f;
        }

        private static void AddUniqueCut(List<float> cuts, float value)
        {
            value = Mathf.Clamp01(value);
            for (var i = 0; i < cuts.Count; i++)
            {
                if (Mathf.Abs(cuts[i] - value) <= 0.0001f)
                {
                    return;
                }
            }

            cuts.Add(value);
        }

        private static float Cross2D(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static bool BoundsOverlap(Vector2 aMin, Vector2 aMax, Vector2 bMin, Vector2 bMax)
        {
            return aMin.x <= bMax.x &&
                aMax.x >= bMin.x &&
                aMin.y <= bMax.y &&
                aMax.y >= bMin.y;
        }

        private bool IsPointInsideOtherDamageStamp(Vector2 point, List<DamageStamp> stamps, int ownerIndex)
        {
            for (var i = 0; i < stamps.Count; i++)
            {
                if (i == ownerIndex)
                {
                    continue;
                }

                var stamp = stamps[i];
                if (stamp != null &&
                    point.x >= stamp.Min.x &&
                    point.x <= stamp.Max.x &&
                    point.y >= stamp.Min.y &&
                    point.y <= stamp.Max.y &&
                    IsPointInPolygon(point, stamp.Points))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
        {
            if (polygon == null || polygon.Length < 3)
            {
                return false;
            }

            var inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];
                var denominator = pj.y - pi.y;
                if ((pi.y > point.y) != (pj.y > point.y) &&
                    Mathf.Abs(denominator) > 0.000001f &&
                    point.x < (pj.x - pi.x) * (point.y - pi.y) / denominator + pi.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static Vector3 DamageStampPointToLocal(DamageStamp stamp, Vector2 point)
        {
            return stamp.Normal * stamp.Plane + stamp.U * point.x + stamp.V * point.y;
        }

        private void AddContourSegment(List<Vector3> vertices, List<int> triangles, Vector3 start, Vector3 end, Vector3 normal, float thickness)
        {
            var direction = end - start;
            if (direction.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            direction.Normalize();
            var side = Vector3.Cross(normal, direction);
            if (side.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            side.Normalize();
            var halfWidth = side * (thickness * 0.5f);
            AddQuadOriented(vertices, triangles, start - halfWidth, end - halfWidth, end + halfWidth, start + halfWidth, normal);
            AddQuadOriented(vertices, triangles, start - halfWidth, start + halfWidth, end + halfWidth, end - halfWidth, -normal);
        }

        private void AddInteriorBridgeSegment(List<Vector3> vertices, List<int> triangles, Vector3 frontStart, Vector3 frontEnd, Vector3 backStart, Vector3 backEnd)
        {
            var normal = Vector3.Cross(frontEnd - frontStart, backStart - frontStart);
            if (normal.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            normal.Normalize();
            AddQuadOriented(vertices, triangles, frontStart, frontEnd, backEnd, backStart, normal);
            AddQuadOriented(vertices, triangles, frontStart, backStart, backEnd, frontEnd, -normal);
        }

        private void AddVisibleSurface(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 size)
        {
            if (configuredOutlineCategory == StylizedOutlineCategory.Floor)
            {
                AddBoxFace(vertices, triangles, center, size, Vector3.up);
                return;
            }

            var half = size * 0.5f;
            if (half.x <= half.y && half.x <= half.z)
            {
                AddBoxFace(vertices, triangles, center, size, Vector3.right);
                AddBoxFace(vertices, triangles, center, size, Vector3.left);
                return;
            }

            if (half.z <= half.x && half.z <= half.y)
            {
                AddBoxFace(vertices, triangles, center, size, Vector3.forward);
                AddBoxFace(vertices, triangles, center, size, Vector3.back);
                return;
            }

            AddBoxFace(vertices, triangles, center, size, Vector3.forward);
        }

        private static int HashIndex(Vector3Int index)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + index.x;
                hash = hash * 31 + index.y;
                hash = hash * 31 + index.z;
                return hash;
            }
        }

        private int CalculateDamageStampSeed(Chunk chunk)
        {
            unchecked
            {
                var hash = 0x4d2f1a35;
                hash = hash * 31 + GetNormalKey(chunk.LastLocalNormal);
                hash = hash * 31 + HashPosition(transform.position);
                hash = hash * 31 + HashIndex(chunk.Index);
                return hash;
            }
        }

        private static int HashPosition(Vector3 position)
        {
            unchecked
            {
                var hash = 23;
                hash = hash * 31 + Mathf.RoundToInt(position.x * 10f);
                hash = hash * 31 + Mathf.RoundToInt(position.y * 10f);
                hash = hash * 31 + Mathf.RoundToInt(position.z * 10f);
                return hash;
            }
        }

        private int GetNormalKey(Vector3 normal)
        {
            var n = DominantAxis(normal);
            if (Mathf.Abs(n.x) > 0.5f)
            {
                return n.x > 0f ? 1 : 2;
            }

            if (Mathf.Abs(n.y) > 0.5f)
            {
                return n.y > 0f ? 3 : 4;
            }

            return n.z > 0f ? 5 : 6;
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                var hash = (uint)value;
                hash ^= hash >> 16;
                hash *= 0x7feb352d;
                hash ^= hash >> 15;
                hash *= 0x846ca68b;
                hash ^= hash >> 16;
                return (hash & 0x00ffffff) / 16777215f;
            }
        }

        private void AddBoxFace(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 size, Vector3 normal)
        {
            GetFaceBasis(normal, size, out var n, out var u, out var v, out var halfN, out var halfU, out var halfV);
            var faceCenter = center + n * halfN;
            AddQuadOriented(
                vertices,
                triangles,
                faceCenter - u * halfU - v * halfV,
                faceCenter + u * halfU - v * halfV,
                faceCenter + u * halfU + v * halfV,
                faceCenter - u * halfU + v * halfV,
                n);
        }

        private void AddQuadOriented(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            AddTriangleOriented(vertices, triangles, a, b, c, outward);
            AddTriangleOriented(vertices, triangles, a, c, d, outward);
        }

        private void AddTriangleOriented(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            var index = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            var normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, outward) >= 0f)
            {
                triangles.Add(index);
                triangles.Add(index + 1);
                triangles.Add(index + 2);
                return;
            }

            triangles.Add(index);
            triangles.Add(index + 2);
            triangles.Add(index + 1);
        }

        private Mesh CreateMesh(string meshName, List<Vector3> vertices, List<int>[] submeshes)
        {
            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = submeshes.Length;
            for (var i = 0; i < submeshes.Length; i++)
            {
                mesh.SetTriangles(submeshes[i], i);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void GetFaceBasis(Vector3 normal, Vector3 size, out Vector3 n, out Vector3 u, out Vector3 v, out float halfN, out float halfU, out float halfV)
        {
            n = DominantAxis(normal);
            var half = size * 0.5f;
            if (Mathf.Abs(n.x) > 0.5f)
            {
                u = Vector3.up;
                v = Vector3.forward;
                halfN = half.x;
                halfU = half.y;
                halfV = half.z;
                return;
            }

            if (Mathf.Abs(n.y) > 0.5f)
            {
                u = Vector3.right;
                v = Vector3.forward;
                halfN = half.y;
                halfU = half.x;
                halfV = half.z;
                return;
            }

            u = Vector3.right;
            v = Vector3.up;
            halfN = half.z;
            halfU = half.x;
            halfV = half.y;
        }

        private void GetPlaneNeighborDirections(Vector3 normal, out Vector3Int right, out Vector3Int up)
        {
            var n = DominantAxis(normal);
            if (Mathf.Abs(n.x) > 0.5f)
            {
                right = Vector3Int.forward;
                up = Vector3Int.up;
                return;
            }

            if (Mathf.Abs(n.y) > 0.5f)
            {
                right = Vector3Int.right;
                up = Vector3Int.forward;
                return;
            }

            right = Vector3Int.right;
            up = Vector3Int.up;
        }

        private Vector3 ResolveDamagePlaneNormal(Vector3 localHitNormal)
        {
            if (damageProfile == DestructibleDamageProfile.CornerPillar)
            {
                return DominantAxis(localHitNormal);
            }

            if (damageProfile == DestructibleDamageProfile.Floor || configuredOutlineCategory == StylizedOutlineCategory.Floor)
            {
                return Vector3.up;
            }

            var sourceSize = configuredSize;
            if (sourceSize.x <= 0.01f || sourceSize.y <= 0.01f || sourceSize.z <= 0.01f)
            {
                sourceSize = GetSourceSize();
            }

            if (sourceSize.x <= sourceSize.z)
            {
                return Vector3.right;
            }

            return Vector3.forward;
        }

        private Vector3 DominantAxis(Vector3 localNormal)
        {
            var abs = new Vector3(Mathf.Abs(localNormal.x), Mathf.Abs(localNormal.y), Mathf.Abs(localNormal.z));
            if (abs.x >= abs.y && abs.x >= abs.z)
            {
                return new Vector3(Mathf.Sign(localNormal.x == 0f ? 1f : localNormal.x), 0f, 0f);
            }

            if (abs.y >= abs.x && abs.y >= abs.z)
            {
                return new Vector3(0f, Mathf.Sign(localNormal.y == 0f ? 1f : localNormal.y), 0f);
            }

            return new Vector3(0f, 0f, Mathf.Sign(localNormal.z == 0f ? 1f : localNormal.z));
        }

        private static Vector3 AbsVector(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private Material GetDamageContourMaterial()
        {
            if (damageContourMaterial == null)
            {
                var category = configuredOutlineCategory == StylizedOutlineCategory.Floor
                    ? StylizedOutlineCategory.Floor
                    : StylizedOutlineCategory.Wall;
                var color = DroidRenderSetup.ResolveEffectiveOutlineColor(category);
                damageContourMaterial = CreateUnlitDamageContourMaterial("Destructible Neon Damage Contour", color);
            }

            return damageContourMaterial;
        }

        private Material GetOutlineProxyMaterial()
        {
            if (outlineProxyMaterial == null)
            {
                var shader = Shader.Find("Hidden/ArenaShooter/InvisibleOutlineProxy");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                }

                outlineProxyMaterial = new Material(shader) { name = "Invisible Destructible Wall Outline Source" };
                outlineProxyMaterial.renderQueue = 2499;
                if (outlineProxyMaterial.HasProperty("_BaseColor"))
                {
                    outlineProxyMaterial.SetColor("_BaseColor", Color.clear);
                }
                else if (outlineProxyMaterial.HasProperty("_Color"))
                {
                    outlineProxyMaterial.SetColor("_Color", Color.clear);
                }
            }

            return outlineProxyMaterial;
        }

        private Material CreateUnlitDamageContourMaterial(string materialName, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            var material = new Material(shader) { name = materialName };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            }

            return material;
        }

        private void SpawnImpactDebris(Vector3 hitPoint, Vector3 hitNormal, Material material, int count)
        {
            var safeNormal = hitNormal.sqrMagnitude > 0.001f ? hitNormal.normalized : Vector3.up;
            for (var i = 0; i < count; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "Arena Bullet Chip";
                shard.transform.position = hitPoint + safeNormal * Random.Range(0.04f, 0.11f) + Random.insideUnitSphere * 0.045f;
                var size = Random.Range(0.045f, 0.13f);
                shard.transform.localScale = new Vector3(size * Random.Range(0.7f, 1.45f), size * Random.Range(0.45f, 0.9f), size * Random.Range(0.7f, 1.45f));
                shard.transform.rotation = Random.rotation;

                if (material != null && shard.TryGetComponent<Renderer>(out var renderer))
                {
                    renderer.sharedMaterial = material;
                }

                var body = shard.AddComponent<Rigidbody>();
                body.mass = 0.035f;
                body.linearDamping = 0.25f;
                body.AddForce((safeNormal + Random.insideUnitSphere * 0.5f + Vector3.up * 0.25f).normalized * Random.Range(0.7f, 1.8f), ForceMode.Impulse);
                body.AddTorque(Random.insideUnitSphere * Random.Range(0.3f, 1.2f), ForceMode.Impulse);
                Destroy(shard, Random.Range(1.8f, 3.2f));
            }
        }
    }
}
