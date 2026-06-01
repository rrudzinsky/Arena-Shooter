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
        private const float MinimumWallOutlineFloorSeamSink = 0.35f;
        private const float WallOutlineProxyDepthOffset = OutlineShellInflation;
        private const float WallDamageRimDepthBias = 0.01f;
        private const float WallDamageRimThickness = 0.006f;
        private const string ContourClippedWallBodyShaderName = "Hidden/ArenaShooter/ContourClippedWallBody";
        private const int DamageStampSegments = 32;
        private const float DamageContourJaggedness = 0.26f;
        private const float MinimumVaultOpeningWidth = 0.92f;
        private const float MinimumVaultOpeningHeight = 0.82f;
        private const float MaximumVaultOpeningBottom = 1.08f;
        private const float MinimumVaultOpeningTop = 0.78f;
        private const float MaximumVaultApproachDistance = 2.25f;
        private const float MaximumVaultPlaneDistance = 1.25f;
        private static readonly Vector3Int[] OutlineNeighborOffsets =
        {
            Vector3Int.right,
            Vector3Int.left,
            Vector3Int.up,
            Vector3Int.down,
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1)
        };

        private static readonly Vector3[] OutlineFaceNormals =
        {
            Vector3.right,
            Vector3.left,
            Vector3.up,
            Vector3.down,
            Vector3.forward,
            Vector3.back
        };

        private readonly Dictionary<Vector3Int, Chunk> chunksByIndex = new();
        private readonly List<Chunk> chunks = new();
        private readonly List<DamageStamp> wallDamageStamps = new();
        private static readonly int WallDamageStampCountId = Shader.PropertyToID("_WallDamageStampCount");
        private static readonly int WallDamagePointCountId = Shader.PropertyToID("_WallDamagePointCount");
        private static readonly int WallDamageClipEnabledId = Shader.PropertyToID("_WallDamageClipEnabled");
        private static readonly int WallDamageUId = Shader.PropertyToID("_WallDamageU");
        private static readonly int WallDamageVId = Shader.PropertyToID("_WallDamageV");
        private static readonly int WallDamageStampBoundsId = Shader.PropertyToID("_WallDamageStampBounds");
        private static readonly int WallDamageStampPointsId = Shader.PropertyToID("_WallDamageStampPoints");
        private Vector3 configuredSize;
        private Material configuredIntactMaterial;
        private Material intactMaterial;
        private Material destructibleBodyMaterial;
        private Material clippedWallBodyMaterial;
        private Material damageContourMaterial;
        private Material outlineProxyMaterial;
        private ComputeBuffer wallDamageStampBoundsBuffer;
        private ComputeBuffer wallDamageStampPointsBuffer;
        private MaterialPropertyBlock wallDamagePropertyBlock;
        private int wallDamageStampBoundsCapacity;
        private int wallDamageStampPointsCapacity;
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
        private Vector3Int chunkCounts;
        private bool initialized;

        public readonly struct PlayerVaultSolution
        {
            public readonly Vector3 EntryPosition;
            public readonly Vector3 ExitPosition;
            public readonly Vector3 ApexPosition;
            public readonly Collider SourceCollider;

            public PlayerVaultSolution(Vector3 entryPosition, Vector3 exitPosition, Vector3 apexPosition, Collider sourceCollider)
            {
                EntryPosition = entryPosition;
                ExitPosition = exitPosition;
                ApexPosition = apexPosition;
                SourceCollider = sourceCollider;
            }
        }

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

        private readonly struct DamageComponentPlaneBounds
        {
            public readonly float MinU;
            public readonly float MaxU;
            public readonly float MinV;
            public readonly float MaxV;

            public DamageComponentPlaneBounds(float minU, float maxU, float minV, float maxV)
            {
                MinU = minU;
                MaxU = maxU;
                MinV = minV;
                MaxV = maxV;
            }

            public float Width => MaxU - MinU;
            public float Height => MaxV - MinV;
            public bool IsValid => MaxU > MinU && MaxV > MinV;
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

        private sealed class DamageContourPlan
        {
            public readonly List<ContourSegment2D> FrontSegments;
            public readonly float Thickness;

            public DamageContourPlan(
                List<ContourSegment2D> frontSegments,
                float thickness)
            {
                FrontSegments = frontSegments;
                Thickness = thickness;
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
            clippedWallBodyMaterial = null;
            wallDamageStamps.Clear();
            configuredBiteDirectionLocal = biteDirectionWorld.sqrMagnitude > 0.0001f
                ? transform.InverseTransformDirection(biteDirectionWorld.normalized)
                : Vector3.zero;

            if (UsesContourOwnedWallDamage())
            {
                InitializeChunks();
            }
        }

        private void OnDestroy()
        {
            ReleaseWallDamageShaderBuffers();
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
            if (UsesContourOwnedWallDamage())
            {
                AddContourOwnedWallDamage(hitPoint);
                RebuildCombinedMesh();
                SpawnImpactDebris(hitPoint, hitNormal, intactMaterial, 4);
                return;
            }

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

            if (UsesContourOwnedWallDamage())
            {
                var localPoint = transform.InverseTransformPoint(hitPoint);
                return IsPointInsideWallDamageUnion(ProjectLocalPointToWallUv(localPoint));
            }

            var chunk = FindHitChunk(hitPoint);
            return chunk == null || chunk.Destroyed;
        }

        public bool IsFloorSurface()
        {
            return damageProfile == DestructibleDamageProfile.Floor || configuredOutlineCategory == StylizedOutlineCategory.Floor;
        }

        public bool TryResolvePlayerVault(Vector3 playerPosition, Vector3 playerForward, float playerRadius, float playerHeight, out PlayerVaultSolution solution)
        {
            solution = default;
            if (!initialized ||
                damageProfile != DestructibleDamageProfile.Wall ||
                configuredOutlineCategory == StylizedOutlineCategory.Floor ||
                playerForward.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            var localPlayer = transform.InverseTransformPoint(playerPosition);
            var localForward = transform.InverseTransformDirection(playerForward.normalized);
            localForward.y = 0f;
            if (localForward.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            localForward.Normalize();
            var wallNormal = GetWallNormalLocal();
            var sideSign = Mathf.Sign(Vector3.Dot(localPlayer, wallNormal));
            if (Mathf.Abs(sideSign) <= 0.001f)
            {
                sideSign = Vector3.Dot(localForward, wallNormal) <= 0f ? 1f : -1f;
            }

            var sideNormal = wallNormal * sideSign;
            if (Vector3.Dot(localForward, -sideNormal) < 0.34f || Mathf.Abs(Vector3.Dot(localPlayer, wallNormal)) > MaximumVaultPlaneDistance)
            {
                return false;
            }

            if (UsesContourOwnedWallDamage())
            {
                return TryResolveContourOwnedWallVault(playerPosition, localPlayer, localForward, wallNormal, sideNormal, playerRadius, playerHeight, out solution);
            }

            return false;
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

            if (!UsesContourOwnedWallDamage())
            {
                BuildChunkGrid(sourceSize);
            }

            if (configuredOutlineCategory != StylizedOutlineCategory.None && configuredOutlineCategory != StylizedOutlineCategory.Floor)
            {
                var outlineSource = new GameObject("Destructible Wall Outline Source");
                outlineSource.transform.SetParent(transform, false);
                outlineSourceMeshFilter = outlineSource.AddComponent<MeshFilter>();
                outlineSourceRenderer = outlineSource.AddComponent<MeshRenderer>();
                outlineSourceRenderer.sharedMaterial = GetOutlineProxyMaterial();
                DroidRenderSetup.ApplyRenderer(outlineSourceRenderer, configuredOutlineCategory);

                RebuildOutlineSourceMesh();
            }

            var contour = new GameObject("Destructible Damage Contours");
            contour.transform.SetParent(transform, false);
            damageContourMeshFilter = contour.AddComponent<MeshFilter>();
            damageContourRenderer = contour.AddComponent<MeshRenderer>();
            damageContourRenderer.sharedMaterial = GetDamageContourMaterial();
            DroidRenderSetup.ApplyRenderer(damageContourRenderer, StylizedOutlineCategory.None);

            RebuildCombinedMesh();
        }

        private void RebuildOutlineSourceMesh()
        {
            if (outlineSourceMeshFilter == null)
            {
                return;
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            if (UsesContourOwnedWallDamage())
            {
                AddContourOwnedWallOutlineSourceGeometry(vertices, triangles);
                outlineSourceMeshFilter.sharedMesh = vertices.Count == 0
                    ? null
                    : CreateMesh("Destructible Wall Outline Source Mesh", vertices, new[] { triangles });
                return;
            }

            var outlineSolidChunks = BuildOutlineProxySolidChunkSet();
            foreach (var chunk in chunks)
            {
                if (!outlineSolidChunks.Contains(chunk))
                {
                    continue;
                }

                AddOutlineProxyChunkFaces(vertices, triangles, chunk, outlineSolidChunks);
            }

            if (vertices.Count == 0)
            {
                outlineSourceMeshFilter.sharedMesh = null;
                return;
            }

            outlineSourceMeshFilter.sharedMesh = CreateMesh("Destructible Wall Outline Source Mesh", vertices, new[] { triangles });
        }

        private HashSet<Chunk> BuildOutlineProxySolidChunkSet()
        {
            var solid = new HashSet<Chunk>();
            var exteriorDestroyed = GatherExteriorConnectedDestroyedChunksForOutline();
            foreach (var chunk in chunks)
            {
                if (!chunk.Destroyed || !exteriorDestroyed.Contains(chunk))
                {
                    solid.Add(chunk);
                }
            }

            return solid;
        }

        private HashSet<Chunk> GatherExteriorConnectedDestroyedChunksForOutline()
        {
            var exterior = new HashSet<Chunk>();
            var queue = new Queue<Chunk>();
            GetPlaneNeighborDirections(GetWallNormalLocal(), out var right, out var up);
            foreach (var chunk in chunks)
            {
                if (!chunk.Destroyed || !TouchesOutlinePlanePerimeter(chunk))
                {
                    continue;
                }

                exterior.Add(chunk);
                queue.Enqueue(chunk);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                TryQueueOutlineDestroyedNeighbor(current, right, exterior, queue);
                TryQueueOutlineDestroyedNeighbor(current, -right, exterior, queue);
                TryQueueOutlineDestroyedNeighbor(current, up, exterior, queue);
                TryQueueOutlineDestroyedNeighbor(current, -up, exterior, queue);
            }

            return exterior;
        }

        private void TryQueueOutlineDestroyedNeighbor(Chunk chunk, Vector3Int offset, HashSet<Chunk> exterior, Queue<Chunk> queue)
        {
            if (!chunksByIndex.TryGetValue(chunk.Index + offset, out var neighbor) ||
                !neighbor.Destroyed ||
                exterior.Contains(neighbor))
            {
                return;
            }

            exterior.Add(neighbor);
            queue.Enqueue(neighbor);
        }

        private bool TouchesOutlinePlanePerimeter(Chunk chunk)
        {
            if (damageProfile == DestructibleDamageProfile.Floor || configuredOutlineCategory == StylizedOutlineCategory.Floor)
            {
                return true;
            }

            if (damageProfile == DestructibleDamageProfile.CornerPillar)
            {
                return chunk.Index.x == 0 ||
                       chunk.Index.x == chunkCounts.x - 1 ||
                       chunk.Index.y == 0 ||
                       chunk.Index.y == chunkCounts.y - 1 ||
                       chunk.Index.z == 0 ||
                       chunk.Index.z == chunkCounts.z - 1;
            }

            return false;
        }

        private void AddOutlineProxyChunkFaces(List<Vector3> vertices, List<int> triangles, Chunk chunk, HashSet<Chunk> outlineSolidChunks)
        {
            var inflatedSize = chunk.BaseScale + Vector3.one * (OutlineShellInflation * 2f);
            for (var i = 0; i < OutlineNeighborOffsets.Length; i++)
            {
                var neighborOffset = OutlineNeighborOffsets[i];
                if (chunksByIndex.TryGetValue(chunk.Index + neighborOffset, out var neighbor))
                {
                    if (outlineSolidChunks.Contains(neighbor))
                    {
                        continue;
                    }
                }
                else if (!IsOriginalOuterBoundaryFace(chunk, neighborOffset))
                {
                    continue;
                }
                else
                {
                    AddOutlineProxyChunkFace(vertices, triangles, chunk, inflatedSize, neighborOffset, OutlineFaceNormals[i]);
                    continue;
                }

                if (neighbor != null && neighbor.Destroyed)
                {
                    continue;
                }

                AddOutlineProxyChunkFace(vertices, triangles, chunk, inflatedSize, neighborOffset, OutlineFaceNormals[i]);
            }
        }

        private void AddOutlineProxyChunkFace(List<Vector3> vertices, List<int> triangles, Chunk chunk, Vector3 inflatedSize, Vector3Int neighborOffset, Vector3 normal)
        {
            if (ShouldSuppressOutlineProxyFloorContactFace(chunk, neighborOffset))
            {
                return;
            }

            var center = chunk.LocalPosition;
            var size = inflatedSize;
            if (ShouldSinkOutlineProxyFloorContactEdge(chunk, normal))
            {
                var sink = CalculateWallOutlineFloorSeamSink(size.y);
                center += Vector3.down * (sink * 0.5f);
                size.y += sink;
            }

            AddBoxFace(vertices, triangles, center, size, normal);
        }

        private bool ShouldSuppressOutlineProxyFloorContactFace(Chunk chunk, Vector3Int neighborOffset)
        {
            return damageProfile == DestructibleDamageProfile.CornerPillar &&
                configuredOutlineCategory != StylizedOutlineCategory.Floor &&
                chunk.Index.y == 0 &&
                neighborOffset.y < 0;
        }

        private bool ShouldSinkOutlineProxyFloorContactEdge(Chunk chunk, Vector3 normal)
        {
            return damageProfile == DestructibleDamageProfile.CornerPillar &&
                configuredOutlineCategory != StylizedOutlineCategory.Floor &&
                chunk.Index.y == 0 &&
                Mathf.Abs(normal.y) < 0.5f;
        }

        private bool IsOriginalOuterBoundaryFace(Chunk chunk, Vector3Int offset)
        {
            var index = chunk.Index;
            if (offset.x < 0)
            {
                return index.x == 0;
            }

            if (offset.x > 0)
            {
                return index.x == chunkCounts.x - 1;
            }

            if (offset.y < 0)
            {
                return index.y == 0;
            }

            if (offset.y > 0)
            {
                return index.y == chunkCounts.y - 1;
            }

            if (offset.z < 0)
            {
                return index.z == 0;
            }

            if (offset.z > 0)
            {
                return index.z == chunkCounts.z - 1;
            }

            return false;
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

            if (UsesContourOwnedWallDamage())
            {
                return GetContourClippedWallBodyMaterial();
            }

            return GetUnclippedDestructibleBodyMaterial();
        }

        private Material GetUnclippedDestructibleBodyMaterial()
        {
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

        private Material GetContourClippedWallBodyMaterial()
        {
            if (clippedWallBodyMaterial != null)
            {
                return clippedWallBodyMaterial;
            }

            var shader = Shader.Find(ContourClippedWallBodyShaderName);
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                return intactMaterial;
            }

            var color = GetMaterialBaseColor(intactMaterial, new Color(0.0015f, 0.001f, 0.004f, 1f));
            clippedWallBodyMaterial = new Material(shader) { name = "Destructible Contour Clipped Wall Body" };
            SetMaterialColor(clippedWallBodyMaterial, color);
            return clippedWallBodyMaterial;
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
            chunkCounts = counts;
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

        private Vector3 GetWallNormalLocal()
        {
            var sourceSize = GetSourceSize();
            return sourceSize.x <= sourceSize.z ? Vector3.right : Vector3.forward;
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

            if (UsesContourOwnedWallDamage())
            {
                RebuildContourOwnedWallMesh();
                return;
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var contourVertices = new List<Vector3>();
            var contourTriangles = new List<int>();
            var visited = new HashSet<Chunk>();
            var damagePlans = new List<DamageContourPlan>();
            foreach (var chunk in chunks)
            {
                if (!chunk.Destroyed || visited.Contains(chunk))
                {
                    continue;
                }

                var plan = BuildDamageContourPlan(GatherDestroyedContourComponent(chunk, visited));
                if (plan != null)
                {
                    damagePlans.Add(plan);
                }
            }

            foreach (var chunk in chunks)
            {
                if (!chunk.Destroyed)
                {
                    AddVisibleChunkSurface(vertices, triangles, chunk);
                }
            }

            foreach (var plan in damagePlans)
            {
                AddPlannedDamageGeometry(vertices, triangles, contourVertices, contourTriangles, plan);
            }

            combinedMeshFilter.sharedMesh = CreateMesh("Combined Destructible Wall Mesh", vertices, new[] { triangles });
            RebuildOutlineSourceMesh();
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

        private bool UsesContourOwnedWallDamage()
        {
            return damageProfile == DestructibleDamageProfile.Wall &&
                configuredOutlineCategory != StylizedOutlineCategory.Floor;
        }

        private void AddContourOwnedWallDamage(Vector3 hitPoint)
        {
            if (!TryGetContourOwnedWallBasis(0f, out var normal, out var u, out var v, out var halfN, out var bounds))
            {
                return;
            }

            var localHit = transform.InverseTransformPoint(hitPoint);
            var center = new Vector2(Vector3.Dot(localHit, u), Vector3.Dot(localHit, v));
            var halfU = Mathf.Max(0.12f, bounds.Width / Mathf.Max(1, CalculateCellCount(bounds.Width)) * 0.5f);
            var halfV = Mathf.Max(0.12f, bounds.Height / Mathf.Max(1, CalculateCellCount(bounds.Height)) * 0.5f);
            var stamp = CreateContourOwnedWallDamageStamp(
                center,
                normal,
                u,
                v,
                halfN,
                halfU * 1.08f,
                halfV * 1.08f,
                CalculateContourOwnedWallDamageSeed(center));
            wallDamageStamps.Add(stamp);
        }

        private DamageStamp CreateContourOwnedWallDamageStamp(
            Vector2 center,
            Vector3 normal,
            Vector3 u,
            Vector3 v,
            float halfN,
            float radiusU,
            float radiusV,
            int seed)
        {
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
                    center.x + Mathf.Cos(angle) * radiusU * radiusScale,
                    center.y + Mathf.Sin(angle) * radiusV * radiusScale);
                points[i] = point;
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            var stamp = new DamageStamp
            {
                Normal = normal,
                U = u,
                V = v,
                Plane = halfN + DamageContourInset,
                Min = min,
                Max = max,
                Points = points
            };
            stamp.Opposite = CreateOppositeContourOwnedWallStamp(stamp, normal, halfN);
            return stamp;
        }

        private static DamageStamp CreateOppositeContourOwnedWallStamp(DamageStamp source, Vector3 normal, float halfN)
        {
            var opposite = new DamageStamp
            {
                Normal = -normal,
                U = source.U,
                V = source.V,
                Plane = halfN + DamageContourInset,
                Min = source.Min,
                Max = source.Max,
                Points = source.Points,
                RenderClosed = source.RenderClosed
            };
            opposite.Opposite = source;
            return opposite;
        }

        private int CalculateContourOwnedWallDamageSeed(Vector2 center)
        {
            unchecked
            {
                var hash = 0x62b35a7d;
                hash = hash * 31 + HashPosition(transform.position);
                hash = hash * 31 + Mathf.RoundToInt(center.x * 100f);
                hash = hash * 31 + Mathf.RoundToInt(center.y * 100f);
                hash = hash * 31 + wallDamageStamps.Count;
                return hash;
            }
        }

        private Vector2 ProjectLocalPointToWallUv(Vector3 localPoint)
        {
            if (!TryGetContourOwnedWallBasis(0f, out _, out var u, out var v, out _, out _))
            {
                return Vector2.zero;
            }

            return new Vector2(Vector3.Dot(localPoint, u), Vector3.Dot(localPoint, v));
        }

        private bool IsPointInsideWallDamageUnion(Vector2 point)
        {
            for (var i = 0; i < wallDamageStamps.Count; i++)
            {
                var stamp = wallDamageStamps[i];
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

        private void UploadWallDamageShaderData()
        {
            if (!UsesContourOwnedWallDamage() ||
                !TryGetContourOwnedWallBasis(0f, out _, out var u, out var v, out _, out _))
            {
                return;
            }

            var stampCount = wallDamageStamps.Count;
            EnsureWallDamageShaderBufferCapacity(
                Mathf.Max(1, stampCount),
                Mathf.Max(1, stampCount * DamageStampSegments));

            var stampBounds = new Vector4[Mathf.Max(1, stampCount)];
            var stampPoints = new Vector4[Mathf.Max(1, stampCount * DamageStampSegments)];
            for (var stampIndex = 0; stampIndex < stampCount; stampIndex++)
            {
                var stamp = wallDamageStamps[stampIndex];
                stampBounds[stampIndex] = new Vector4(stamp.Min.x, stamp.Min.y, stamp.Max.x, stamp.Max.y);
                for (var pointIndex = 0; pointIndex < DamageStampSegments; pointIndex++)
                {
                    var point = stamp.Points[pointIndex];
                    stampPoints[stampIndex * DamageStampSegments + pointIndex] = new Vector4(point.x, point.y, 0f, 0f);
                }
            }

            wallDamageStampBoundsBuffer.SetData(stampBounds);
            wallDamageStampPointsBuffer.SetData(stampPoints);
            ApplyWallDamagePropertyBlock(combinedRenderer, stampCount, u, v);
            ApplyWallDamagePropertyBlock(outlineSourceRenderer, stampCount, u, v);
        }

        private void EnsureWallDamageShaderBufferCapacity(int stampCapacity, int pointCapacity)
        {
            if (wallDamageStampBoundsBuffer == null || wallDamageStampBoundsCapacity < stampCapacity)
            {
                wallDamageStampBoundsBuffer?.Release();
                wallDamageStampBoundsCapacity = Mathf.Max(1, stampCapacity);
                wallDamageStampBoundsBuffer = new ComputeBuffer(wallDamageStampBoundsCapacity, sizeof(float) * 4);
            }

            if (wallDamageStampPointsBuffer == null || wallDamageStampPointsCapacity < pointCapacity)
            {
                wallDamageStampPointsBuffer?.Release();
                wallDamageStampPointsCapacity = Mathf.Max(1, pointCapacity);
                wallDamageStampPointsBuffer = new ComputeBuffer(wallDamageStampPointsCapacity, sizeof(float) * 4);
            }
        }

        private void ApplyWallDamagePropertyBlock(Renderer renderer, int stampCount, Vector3 u, Vector3 v)
        {
            if (renderer == null)
            {
                return;
            }

            wallDamagePropertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(wallDamagePropertyBlock);
            wallDamagePropertyBlock.SetInt(WallDamageClipEnabledId, 1);
            wallDamagePropertyBlock.SetInt(WallDamageStampCountId, stampCount);
            wallDamagePropertyBlock.SetInt(WallDamagePointCountId, DamageStampSegments);
            wallDamagePropertyBlock.SetVector(WallDamageUId, new Vector4(u.x, u.y, u.z, 0f));
            wallDamagePropertyBlock.SetVector(WallDamageVId, new Vector4(v.x, v.y, v.z, 0f));
            wallDamagePropertyBlock.SetBuffer(WallDamageStampBoundsId, wallDamageStampBoundsBuffer);
            wallDamagePropertyBlock.SetBuffer(WallDamageStampPointsId, wallDamageStampPointsBuffer);
            renderer.SetPropertyBlock(wallDamagePropertyBlock);
        }

        private void ReleaseWallDamageShaderBuffers()
        {
            wallDamageStampBoundsBuffer?.Release();
            wallDamageStampBoundsBuffer = null;
            wallDamageStampBoundsCapacity = 0;

            wallDamageStampPointsBuffer?.Release();
            wallDamageStampPointsBuffer = null;
            wallDamageStampPointsCapacity = 0;
        }

        private void RebuildContourOwnedWallMesh()
        {
            var vertices = new List<Vector3>();
            var slabTriangles = new List<int>();
            var bridgeTriangles = new List<int>();
            AddContourOwnedWallBodyGeometry(vertices, slabTriangles, 0f);

            var thickness = GetContourOwnedWallContourThickness();
            var visibleSegments = GetVisibleContourOwnedWallSegments(thickness);
            if (visibleSegments.Count > 0)
            {
                AddContourInteriorBridge(vertices, bridgeTriangles, visibleSegments);
            }

            combinedMeshFilter.sharedMesh = CreateMesh("Combined Destructible Wall Mesh", vertices, new[] { slabTriangles, bridgeTriangles });
            if (combinedRenderer != null)
            {
                combinedRenderer.sharedMaterials = new[]
                {
                    GetContourClippedWallBodyMaterial(),
                    GetUnclippedDestructibleBodyMaterial()
                };
            }

            RebuildOutlineSourceMesh();
            RebuildContourOwnedWallDamageContourMesh(visibleSegments);
            UploadWallDamageShaderData();
        }

        private void RebuildContourOwnedWallDamageContourMesh(List<ContourSegment2D> visibleSegments)
        {
            if (damageContourMeshFilter == null)
            {
                return;
            }

            if (!UsesContourOwnedWallDamage() || visibleSegments == null || visibleSegments.Count == 0)
            {
                damageContourMeshFilter.sharedMesh = null;
                return;
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            AddContourOwnedWallDamageRimSegments(vertices, triangles, visibleSegments);
            damageContourMeshFilter.sharedMesh = vertices.Count == 0
                ? null
                : CreateMesh("Destructible Wall Damage Contour Mesh", vertices, new[] { triangles });
        }

        private void AddContourOwnedWallDamageRimSegments(List<Vector3> vertices, List<int> triangles, List<ContourSegment2D> visibleSegments)
        {
            for (var i = 0; i < visibleSegments.Count; i++)
            {
                var segment = visibleSegments[i];
                if (segment.Stamp == null)
                {
                    continue;
                }

                AddSingleSidedContourSegment(
                    vertices,
                    triangles,
                    DamageStampPointToLocal(segment.Stamp, segment.Start, WallDamageRimDepthBias),
                    DamageStampPointToLocal(segment.Stamp, segment.End, WallDamageRimDepthBias),
                    segment.Stamp.Normal,
                    WallDamageRimThickness);

                AddThroughThicknessDamageRimSegment(vertices, triangles, segment, segment.Start, segment.End);
                AddThroughThicknessDamageRimSegment(vertices, triangles, segment, segment.End, segment.Start);

                var opposite = segment.Stamp.Opposite;
                if (opposite == null)
                {
                    continue;
                }

                AddSingleSidedContourSegment(
                    vertices,
                    triangles,
                    DamageStampPointToLocal(opposite, segment.Start, WallDamageRimDepthBias),
                    DamageStampPointToLocal(opposite, segment.End, WallDamageRimDepthBias),
                    opposite.Normal,
                    WallDamageRimThickness);
            }
        }

        private void AddThroughThicknessDamageRimSegment(
            List<Vector3> vertices,
            List<int> triangles,
            ContourSegment2D segment,
            Vector2 point,
            Vector2 tangentPoint)
        {
            var stamp = segment.Stamp;
            var opposite = stamp != null ? stamp.Opposite : null;
            if (opposite == null)
            {
                return;
            }

            var tangent2D = tangentPoint - point;
            var tangent = stamp.U * tangent2D.x + stamp.V * tangent2D.y;
            if (tangent.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            tangent.Normalize();
            var halfWidth = tangent * (WallDamageRimThickness * 0.5f);
            var front = DamageStampPointToLocal(stamp, point, WallDamageRimDepthBias);
            var back = DamageStampPointToLocal(opposite, point, WallDamageRimDepthBias);
            var through = back - front;
            if (through.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            var outward = Vector3.Cross(through, halfWidth);
            if (outward.sqrMagnitude <= 0.000001f)
            {
                outward = stamp.Normal;
            }
            else
            {
                outward.Normalize();
            }

            AddQuadOriented(vertices, triangles, front - halfWidth, back - halfWidth, back + halfWidth, front + halfWidth, outward);
            AddQuadOriented(vertices, triangles, front - halfWidth, front + halfWidth, back + halfWidth, back - halfWidth, -outward);
        }

        private void AddContourOwnedWallBodyGeometry(List<Vector3> vertices, List<int> triangles, float shellInflation)
        {
            if (!TryGetContourOwnedWallBasis(shellInflation, out var normal, out var u, out var v, out var halfN, out var bounds))
            {
                return;
            }

            AddContourOwnedWallBodyGeometry(vertices, triangles, normal, u, v, halfN, bounds);
        }

        private void AddContourOwnedWallOutlineSourceGeometry(List<Vector3> vertices, List<int> triangles)
        {
            if (!TryGetContourOwnedWallBasis(0f, out var normal, out var u, out var v, out var halfN, out var bounds))
            {
                return;
            }

            var outlineBounds = SinkContourOwnedWallOutlineFloorEdge(bounds, u, v);
            var outlineHalfN = halfN + WallOutlineProxyDepthOffset;
            AddContourOwnedWallPlaneQuad(
                vertices,
                triangles,
                normal,
                u,
                v,
                outlineHalfN,
                outlineBounds.MinU,
                outlineBounds.MaxU,
                outlineBounds.MinV,
                outlineBounds.MaxV);
            AddContourOwnedWallPlaneQuad(
                vertices,
                triangles,
                -normal,
                u,
                v,
                outlineHalfN,
                outlineBounds.MinU,
                outlineBounds.MaxU,
                outlineBounds.MinV,
                outlineBounds.MaxV);
        }

        private void AddContourOwnedWallBodyGeometry(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 normal,
            Vector3 u,
            Vector3 v,
            float halfN,
            DamageComponentPlaneBounds bounds)
        {
            AddContourOwnedWallPlaneQuad(vertices, triangles, normal, u, v, halfN, bounds.MinU, bounds.MaxU, bounds.MinV, bounds.MaxV);
            AddContourOwnedWallPlaneQuad(vertices, triangles, -normal, u, v, halfN, bounds.MinU, bounds.MaxU, bounds.MinV, bounds.MaxV);
            AddContourOwnedWallFullOuterSide(vertices, triangles, normal, u, v, halfN, bounds.MinU, bounds.MinV, bounds.MaxV, true, -u);
            AddContourOwnedWallFullOuterSide(vertices, triangles, normal, u, v, halfN, bounds.MaxU, bounds.MinV, bounds.MaxV, true, u);
            AddContourOwnedWallFullOuterSide(vertices, triangles, normal, u, v, halfN, bounds.MinV, bounds.MinU, bounds.MaxU, false, -v);
            AddContourOwnedWallFullOuterSide(vertices, triangles, normal, u, v, halfN, bounds.MaxV, bounds.MinU, bounds.MaxU, false, v);
        }

        private static DamageComponentPlaneBounds SinkContourOwnedWallOutlineFloorEdge(DamageComponentPlaneBounds bounds, Vector3 u, Vector3 v)
        {
            var uVertical = Vector3.Dot(u, Vector3.up);
            if (Mathf.Abs(uVertical) > 0.5f)
            {
                var sink = CalculateWallOutlineFloorSeamSink(bounds.Width);
                return uVertical > 0f
                    ? new DamageComponentPlaneBounds(bounds.MinU - sink, bounds.MaxU, bounds.MinV, bounds.MaxV)
                    : new DamageComponentPlaneBounds(bounds.MinU, bounds.MaxU + sink, bounds.MinV, bounds.MaxV);
            }

            var vVertical = Vector3.Dot(v, Vector3.up);
            if (Mathf.Abs(vVertical) > 0.5f)
            {
                var sink = CalculateWallOutlineFloorSeamSink(bounds.Height);
                return vVertical > 0f
                    ? new DamageComponentPlaneBounds(bounds.MinU, bounds.MaxU, bounds.MinV - sink, bounds.MaxV)
                    : new DamageComponentPlaneBounds(bounds.MinU, bounds.MaxU, bounds.MinV, bounds.MaxV + sink);
            }

            return bounds;
        }

        private static float CalculateWallOutlineFloorSeamSink(float verticalSpan)
        {
            return Mathf.Max(MinimumWallOutlineFloorSeamSink, Mathf.Abs(verticalSpan) * 1.1f, OutlineShellInflation * 4f);
        }

        private bool TryGetContourOwnedWallBasis(
            float shellInflation,
            out Vector3 normal,
            out Vector3 u,
            out Vector3 v,
            out float halfN,
            out DamageComponentPlaneBounds bounds)
        {
            normal = Vector3.forward;
            u = Vector3.right;
            v = Vector3.up;
            halfN = 0f;
            bounds = default;
            if (damageProfile != DestructibleDamageProfile.Wall || configuredOutlineCategory == StylizedOutlineCategory.Floor)
            {
                return false;
            }

            var sourceSize = GetSourceSize() + Vector3.one * (shellInflation * 2f);
            GetFaceBasis(GetWallNormalLocal(), sourceSize, out normal, out u, out v, out halfN, out var halfU, out var halfV);
            bounds = new DamageComponentPlaneBounds(-halfU, halfU, -halfV, halfV);
            return bounds.IsValid && halfN > 0f;
        }

        private float GetContourOwnedWallContourThickness()
        {
            var sourceSize = GetSourceSize();
            var smallest = Mathf.Min(sourceSize.x, Mathf.Min(sourceSize.y, sourceSize.z));
            return Mathf.Clamp(smallest * 0.08f, 0.018f, 0.05f);
        }

        private List<ContourSegment2D> GetVisibleContourOwnedWallSegments(float thickness)
        {
            var result = new List<ContourSegment2D>();
            if (wallDamageStamps.Count == 0 ||
                !TryGetContourOwnedWallBasis(0f, out _, out _, out _, out _, out var bounds))
            {
                return result;
            }

            var rawSegments = BuildStampUnionDamageSegments(wallDamageStamps, thickness);
            for (var i = 0; i < rawSegments.Count; i++)
            {
                var segment = rawSegments[i];
                if (TryClipSegmentToBounds(segment.Start, segment.End, bounds, out var clippedStart, out var clippedEnd))
                {
                    result.Add(new ContourSegment2D(segment.Stamp, clippedStart, clippedEnd));
                }
            }

            return result;
        }

        private void AddContourOwnedWallPlaneQuad(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 normal,
            Vector3 u,
            Vector3 v,
            float plane,
            float minU,
            float maxU,
            float minV,
            float maxV)
        {
            AddQuadOriented(
                vertices,
                triangles,
                normal * plane + u * minU + v * minV,
                normal * plane + u * maxU + v * minV,
                normal * plane + u * maxU + v * maxV,
                normal * plane + u * minU + v * maxV,
                normal);
        }

        private void AddContourOwnedWallFullOuterSide(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 normal,
            Vector3 u,
            Vector3 v,
            float halfN,
            float sideCoordinate,
            float minAlong,
            float maxAlong,
            bool uSide,
            Vector3 outward)
        {
            var localA = uSide ? u * sideCoordinate + v * minAlong : u * minAlong + v * sideCoordinate;
            var localB = uSide ? u * sideCoordinate + v * maxAlong : u * maxAlong + v * sideCoordinate;
            AddQuadOriented(
                vertices,
                triangles,
                normal * halfN + localA,
                normal * halfN + localB,
                -normal * halfN + localB,
                -normal * halfN + localA,
                outward);
        }

        private static bool TryClipSegmentToBounds(Vector2 start, Vector2 end, DamageComponentPlaneBounds bounds, out Vector2 clippedStart, out Vector2 clippedEnd)
        {
            clippedStart = start;
            clippedEnd = end;
            var delta = end - start;
            var t0 = 0f;
            var t1 = 1f;
            if (!ClipLineTest(-delta.x, start.x - bounds.MinU, ref t0, ref t1) ||
                !ClipLineTest(delta.x, bounds.MaxU - start.x, ref t0, ref t1) ||
                !ClipLineTest(-delta.y, start.y - bounds.MinV, ref t0, ref t1) ||
                !ClipLineTest(delta.y, bounds.MaxV - start.y, ref t0, ref t1) ||
                t1 - t0 <= 0.0001f)
            {
                return false;
            }

            clippedStart = start + delta * t0;
            clippedEnd = start + delta * t1;
            return true;
        }

        private static bool ClipLineTest(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Abs(p) <= 0.000001f)
            {
                return q >= 0f;
            }

            var r = q / p;
            if (p < 0f)
            {
                if (r > t1)
                {
                    return false;
                }

                if (r > t0)
                {
                    t0 = r;
                }

                return true;
            }

            if (r < t0)
            {
                return false;
            }

            if (r < t1)
            {
                t1 = r;
            }

            return true;
        }

        private bool TryResolveContourOwnedWallVault(
            Vector3 playerPosition,
            Vector3 localPlayer,
            Vector3 localForward,
            Vector3 wallNormal,
            Vector3 sideNormal,
            float playerRadius,
            float playerHeight,
            out PlayerVaultSolution solution)
        {
            solution = default;
            if (wallDamageStamps.Count == 0 ||
                playerHeight < 1.1f ||
                !TryGetContourOwnedWallBasis(0f, out _, out var u, out var v, out var halfN, out var bounds))
            {
                return false;
            }

            var thickness = GetContourOwnedWallContourThickness();
            var visibleSegments = GetVisibleContourOwnedWallSegments(thickness);
            if (visibleSegments.Count == 0)
            {
                return false;
            }

            var groups = BuildContourSegmentGroups(visibleSegments, Mathf.Max(0.002f, thickness * 0.35f));
            var verticalIsU = Mathf.Abs(u.y) > Mathf.Abs(v.y);
            var wallBottom = -GetSourceSize().y * 0.5f;
            var bestScore = float.NegativeInfinity;
            var bestEntry = Vector3.zero;
            var bestExit = Vector3.zero;
            var bestApex = Vector3.zero;
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                var width = verticalIsU ? group.Span.y : group.Span.x;
                var height = verticalIsU ? group.Span.x : group.Span.y;
                var bottom = verticalIsU ? group.Min.x : group.Min.y;
                var top = verticalIsU ? group.Max.x : group.Max.y;
                if (width < MinimumVaultOpeningWidth ||
                    height < MinimumVaultOpeningHeight ||
                    bottom - wallBottom > MaximumVaultOpeningBottom ||
                    top - wallBottom < MinimumVaultOpeningTop)
                {
                    continue;
                }

                var center = group.Centroid;
                var centerLocal = u * center.x + v * center.y;
                var openingCenterWorld = transform.TransformPoint(centerLocal);
                var toOpening = openingCenterWorld - playerPosition;
                toOpening.y = 0f;
                var approachDistance = toOpening.magnitude;
                if (approachDistance > MaximumVaultApproachDistance || approachDistance <= 0.05f)
                {
                    continue;
                }

                var worldForward = transform.TransformDirection(localForward);
                worldForward.y = 0f;
                worldForward.Normalize();
                var facing = Vector3.Dot(worldForward, toOpening / approachDistance);
                if (facing < 0.42f)
                {
                    continue;
                }

                var lateralMiss = Vector3.Cross(worldForward, toOpening).magnitude;
                if (lateralMiss > Mathf.Max(0.8f, width * 0.65f))
                {
                    continue;
                }

                var localEntry = centerLocal + sideNormal * (halfN + playerRadius * 0.75f);
                var localExit = centerLocal - sideNormal * (halfN + playerRadius + 0.5f);
                var entry = transform.TransformPoint(localEntry);
                var exit = transform.TransformPoint(localExit);
                entry.y = playerPosition.y;
                exit.y = playerPosition.y;
                var apex = transform.TransformPoint(centerLocal + Vector3.up * Mathf.Max(0.35f, height * 0.32f));
                apex.y = Mathf.Max(apex.y, playerPosition.y + 0.52f);
                var score = width * 1.6f + height + facing - approachDistance * 0.2f;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestEntry = entry;
                bestExit = exit;
                bestApex = apex;
            }

            if (bestScore <= float.NegativeInfinity * 0.5f)
            {
                return false;
            }

            solution = new PlayerVaultSolution(bestEntry, bestExit, bestApex, surfaceCollider);
            return true;
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

        private DamageContourPlan BuildDamageContourPlan(List<Chunk> component)
        {
            if (component == null || component.Count == 0)
            {
                return null;
            }

            var contourChunks = new List<Chunk>();
            foreach (var chunk in component)
            {
                EnsureDamageStamp(chunk);
                contourChunks.Add(chunk);
            }

            if (contourChunks.Count == 0)
            {
                return null;
            }

            if (!HasContourAgainstSurvivingMaterial(contourChunks))
            {
                return null;
            }

            var half = contourChunks[0].BaseScale * 0.5f;
            var thickness = Mathf.Clamp(Mathf.Min(half.x, half.y, half.z) * 0.08f, 0.018f, 0.05f);
            var frontStamps = CollectDamageStamps(contourChunks, false);
            var frontSegments = BuildStampUnionDamageSegments(frontStamps, thickness);

            if (frontSegments.Count == 0)
            {
                return null;
            }

            return new DamageContourPlan(
                frontSegments,
                thickness);
        }

        private void AddPlannedDamageGeometry(
            List<Vector3> bodyVertices,
            List<int> bodyTriangles,
            List<Vector3> contourVertices,
            List<int> contourTriangles,
            DamageContourPlan plan)
        {
            if (plan == null)
            {
                return;
            }

            if (damageProfile != DestructibleDamageProfile.CornerPillar)
            {
                AddContourInteriorBridge(bodyVertices, bodyTriangles, plan.FrontSegments);
            }

            AddDamageContourSegments(contourVertices, contourTriangles, plan.FrontSegments, plan.Thickness);
            AddOppositeDamageContourSegments(contourVertices, contourTriangles, plan.FrontSegments, plan.Thickness);
        }

        private bool HasContourAgainstSurvivingMaterial(List<Chunk> component)
        {
            if (component == null || component.Count == 0)
            {
                return false;
            }

            GetPlaneNeighborDirections(component[0].LastLocalNormal, out var right, out var up);
            for (var i = 0; i < component.Count; i++)
            {
                var chunk = component[i];
                if (HasIntactNeighbor(chunk, right) ||
                    HasIntactNeighbor(chunk, -right) ||
                    HasIntactNeighbor(chunk, up) ||
                    HasIntactNeighbor(chunk, -up))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasIntactNeighbor(Chunk chunk, Vector3Int offset)
        {
            return chunksByIndex.TryGetValue(chunk.Index + offset, out var neighbor) && !neighbor.Destroyed;
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

        private static Vector3 DamageStampPointToLocal(DamageStamp stamp, Vector2 point, float planeOffset)
        {
            return stamp.Normal * (stamp.Plane + planeOffset) + stamp.U * point.x + stamp.V * point.y;
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

        private void AddSingleSidedContourSegment(List<Vector3> vertices, List<int> triangles, Vector3 start, Vector3 end, Vector3 normal, float thickness)
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

        private void AddVisibleChunkSurface(List<Vector3> vertices, List<int> triangles, Chunk chunk)
        {
            var center = chunk.LocalPosition;
            var size = chunk.BaseScale;
            if (configuredOutlineCategory == StylizedOutlineCategory.Floor)
            {
                AddBoxFace(vertices, triangles, center, size, Vector3.up);
                return;
            }

            var half = size * 0.5f;
            if (half.x <= half.y && half.x <= half.z)
            {
                AddVisibleChunkFaceIfExposed(vertices, triangles, chunk, new Vector3Int(1, 0, 0), Vector3.right);
                AddVisibleChunkFaceIfExposed(vertices, triangles, chunk, new Vector3Int(-1, 0, 0), Vector3.left);
                AddVisibleChunkFaceIfExposed(vertices, triangles, chunk, new Vector3Int(0, 1, 0), Vector3.up);
                AddVisibleChunkFaceIfExposed(vertices, triangles, chunk, new Vector3Int(0, -1, 0), Vector3.down);
                AddVisibleChunkFaceIfExposed(vertices, triangles, chunk, new Vector3Int(0, 0, 1), Vector3.forward);
                AddVisibleChunkFaceIfExposed(vertices, triangles, chunk, new Vector3Int(0, 0, -1), Vector3.back);
                return;
            }

            if (half.z <= half.x && half.z <= half.y)
            {
                AddVisibleChunkFaceIfExposed(vertices, triangles, chunk, new Vector3Int(0, 0, 1), Vector3.forward);
                AddVisibleChunkFaceIfExposed(vertices, triangles, chunk, new Vector3Int(0, 0, -1), Vector3.back);
                AddVisibleChunkFaceIfExposed(vertices, triangles, chunk, new Vector3Int(1, 0, 0), Vector3.right);
                AddVisibleChunkFaceIfExposed(vertices, triangles, chunk, new Vector3Int(-1, 0, 0), Vector3.left);
                AddVisibleChunkFaceIfExposed(vertices, triangles, chunk, new Vector3Int(0, 1, 0), Vector3.up);
                AddVisibleChunkFaceIfExposed(vertices, triangles, chunk, new Vector3Int(0, -1, 0), Vector3.down);
                return;
            }

            for (var i = 0; i < OutlineNeighborOffsets.Length; i++)
            {
                AddVisibleChunkFaceIfExposed(vertices, triangles, chunk, OutlineNeighborOffsets[i], OutlineFaceNormals[i]);
            }
        }

        private void AddVisibleChunkFaceIfExposed(List<Vector3> vertices, List<int> triangles, Chunk chunk, Vector3Int offset, Vector3 normal)
        {
            if (chunksByIndex.TryGetValue(chunk.Index + offset, out var neighbor))
            {
                if (!neighbor.Destroyed)
                {
                    return;
                }

                AddBoxFace(vertices, triangles, chunk.LocalPosition, chunk.BaseScale, normal);
                return;
            }

            if (IsOriginalOuterBoundaryFace(chunk, offset))
            {
                AddBoxFace(vertices, triangles, chunk.LocalPosition, chunk.BaseScale, normal);
            }
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
            if (vertices.Count > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

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
                var color = configuredOutlineCategory == StylizedOutlineCategory.Floor
                    ? DroidRenderSetup.ResolveEffectiveOutlineColor(StylizedOutlineCategory.Floor)
                    : DroidRenderSetup.ResolveOutlineColor(StylizedOutlineCategory.Wall);
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
