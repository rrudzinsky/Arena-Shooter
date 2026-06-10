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
        private const string WallOutlineSourceName = "Destructible Wall Outline Source";
        private const int MaxUnsupportedIslandSpraysPerDamage = 18;
        private const float UnsupportedIslandSpraySourceClearance = 0.035f;
        private const float UnsupportedIslandSprayMinLifetime = 0.42f;
        private const float UnsupportedIslandSprayMaxLifetime = 0.7f;
        private const float UnsupportedIslandSprayMinSpeed = 2.1f;
        private const float UnsupportedIslandSprayMaxSpeed = 4.6f;
        private const float UnsupportedIslandSprayLateralSpread = 0.7f;
        private const float UnsupportedIslandSprayChipMinSize = 0.035f;
        private const float UnsupportedIslandSprayChipMaxSize = 0.12f;
        private const int UnsupportedIslandCleanupPasses = 6;
        private const float UnsupportedIslandScanTargetCellSize = 0.045f;
        private const int UnsupportedIslandScanMaxCellsPerAxis = 512;
        private const float UnsupportedIslandSupportBridgeWidth = 0.16f;
        private const float UnsupportedIslandPerimeterSupportMinSpan = 0.48f;
        private const float UnsupportedIslandCleanupPadding = 0.028f;
        private const int UnsupportedIslandMinimumSolidCellSamples = 2;
        private const float UnsupportedIslandSprayMinimumArea = 0.006f;
        private const int UnsupportedIslandSprayMinChips = 3;
        private const int UnsupportedIslandSprayMaxChips = 7;
        private const int WallHitSprayMinChips = 5;
        private const int WallHitSprayMaxChips = 9;
        private const float PillarBiteSetbackPerHit = 0.30f;
        private const float PillarBiteMaxSetbackFraction = 0.46f;
        private const float PillarBiteDestroyAreaFraction = 0.30f;
        private const float CantileverSlendernessLimit = 2.6f;
        private const float CantileverMinimumOverhangReach = 0.2f;
        private const float CantileverMinimumNeckThickness = 0.06f;
        private const int CantileverMaxFailurePasses = 4;
        private const int MaxFallingSlabsPerDamage = 6;
        private const float FallingSlabMinimumArea = 0.02f;
        private const float FallingSlabLifetimeSeconds = 2.4f;
        private const float FallingSlabMaxTipDegreesPerSecond = 55f;
        private const float PedestalWidthAmplificationLimit = 3.5f;
        private const float PedestalMinimumCrushMass = 0.25f;
        private const int PedestalMaxFailurePasses = 3;
        private const float UnsupportedIslandCellSampleInset = 0.32f;
        private const float OpenContourShardSupportProbeScale = 1.6f;
        private const float OpenContourShardBoundaryWidthScale = 0.72f;
        private const float OpenContourShardMinimumSegmentLength = 0.015f;
        private static readonly Vector2[] PillarCornerSigns =
        {
            new(1f, 1f),
            new(-1f, 1f),
            new(-1f, -1f),
            new(1f, -1f)
        };
        private static readonly Vector2Int[] IslandScanNeighborOffsets =
        {
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1)
        };
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
        private static readonly int WallDamageClipEnabledId = Shader.PropertyToID("_WallDamageClipEnabled");
        private static readonly int WallDamageUId = Shader.PropertyToID("_WallDamageU");
        private static readonly int WallDamageVId = Shader.PropertyToID("_WallDamageV");
        private static readonly int WallDamageStampBoundsId = Shader.PropertyToID("_WallDamageStampBounds");
        private static readonly int WallDamageStampPointsId = Shader.PropertyToID("_WallDamageStampPoints");
        private static readonly int WallDamageStampPointOffsetsId = Shader.PropertyToID("_WallDamageStampPointOffsets");
        private static readonly int WallDamageStampPointCountsId = Shader.PropertyToID("_WallDamageStampPointCounts");
        private Vector3 configuredSize;
        private Material configuredIntactMaterial;
        private Material intactMaterial;
        private Material destructibleBodyMaterial;
        private Material clippedWallBodyMaterial;
        private Material damageContourMaterial;
        private Material outlineProxyMaterial;
        private ComputeBuffer wallDamageStampBoundsBuffer;
        private ComputeBuffer wallDamageStampPointsBuffer;
        private ComputeBuffer wallDamageStampPointOffsetsBuffer;
        private ComputeBuffer wallDamageStampPointCountsBuffer;
        private MaterialPropertyBlock wallDamagePropertyBlock;
        private int wallDamageStampBoundsCapacity;
        private int wallDamageStampPointsCapacity;
        private int wallDamageStampPointOffsetsCapacity;
        private int wallDamageStampPointCountsCapacity;
        private StylizedOutlineCategory configuredOutlineCategory = StylizedOutlineCategory.None;
        private DestructibleDamageProfile damageProfile = DestructibleDamageProfile.Wall;
        private Vector3 configuredBiteDirectionLocal = Vector3.zero;
        private MeshFilter combinedMeshFilter;
        private MeshRenderer combinedRenderer;
        private MeshFilter outlineSourceMeshFilter;
        private MeshRenderer outlineSourceRenderer;
        private MeshFilter damageContourMeshFilter;
        private MeshRenderer damageContourRenderer;
        private MeshFilter interiorBridgeMeshFilter;
        private MeshRenderer interiorBridgeRenderer;
        private BoxCollider surfaceCollider;
        private Vector3Int chunkCounts;
        private bool initialized;
        private bool chunkGridBuilt;

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
            public readonly float[] CornerBiteSetbacks = new float[4];
            public bool HasCornerBite;
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
            public bool RenderContour = true;
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

        private readonly struct BoundaryEdge
        {
            public readonly Vector2Int Start;
            public readonly Vector2Int End;

            public BoundaryEdge(Vector2Int start, Vector2Int end)
            {
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
            public float TotalLength;

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

        private sealed class UnsupportedIslandScanGrid
        {
            public readonly DamageComponentPlaneBounds Bounds;
            public readonly int Columns;
            public readonly int Rows;
            public readonly int SupportRadiusCells;
            public readonly float StepU;
            public readonly float StepV;
            public readonly bool[] Solid;
            public readonly bool[] SupportCore;

            public UnsupportedIslandScanGrid(
                DamageComponentPlaneBounds bounds,
                int columns,
                int rows,
                int supportRadiusCells,
                float stepU,
                float stepV,
                bool[] solid,
                bool[] supportCore)
            {
                Bounds = bounds;
                Columns = columns;
                Rows = rows;
                SupportRadiusCells = supportRadiusCells;
                StepU = stepU;
                StepV = stepV;
                Solid = solid;
                SupportCore = supportCore;
            }

            public int Count => Solid.Length;

            public int Index(int x, int y)
            {
                return y * Columns + x;
            }

            public int CellX(int index)
            {
                return index % Columns;
            }

            public int CellY(int index)
            {
                return index / Columns;
            }

            public bool ContainsCell(int x, int y)
            {
                return x >= 0 && x < Columns && y >= 0 && y < Rows;
            }

            public bool IsPerimeterCell(int x, int y)
            {
                return x == 0 || y == 0 || x == Columns - 1 || y == Rows - 1;
            }

            public Vector2 CellCenter(int x, int y)
            {
                return new Vector2(
                    Bounds.MinU + (x + 0.5f) * StepU,
                    Bounds.MinV + (y + 0.5f) * StepV);
            }

            public Vector2 CornerToUv(Vector2Int corner)
            {
                return new Vector2(
                    Bounds.MinU + corner.x * StepU,
                    Bounds.MinV + corner.y * StepV);
            }
        }

        private sealed class UnsupportedWallIsland
        {
            public readonly List<Vector2> Points;
            public readonly Vector2 Centroid;
            public readonly Vector2 Min;
            public readonly Vector2 Max;
            public readonly float Area;
            public readonly bool RequiresSpray;

            public UnsupportedWallIsland(List<Vector2> points, Vector2 centroid, Vector2 min, Vector2 max, float area)
                : this(points, centroid, min, max, area, true)
            {
            }

            public UnsupportedWallIsland(List<Vector2> points, Vector2 centroid, Vector2 min, Vector2 max, float area, bool requiresSpray)
            {
                Points = points;
                Centroid = centroid;
                Min = min;
                Max = max;
                Area = area;
                RequiresSpray = requiresSpray;
            }
        }

        private sealed class CantileverRun
        {
            public int HorizontalIndex;
            public int VerticalStart;
            public int VerticalEnd;
            public int Hops = int.MaxValue;
            public float Bottleneck = float.PositiveInfinity;
            public int ComponentLabel = -1;
        }

        private sealed class UnsupportedIslandSprayChipAnimation : MonoBehaviour
        {
            private Vector3 startPosition;
            private Vector3 velocity;
            private Vector3 spinAxis;
            private Vector3 startScale;
            private float angularSpeed;
            private float duration;
            private float elapsed;

            public void Initialize(Vector3 initialVelocity, Vector3 initialSpinAxis, float spinDegreesPerSecond, float lifetimeSeconds)
            {
                startPosition = transform.position;
                velocity = initialVelocity;
                spinAxis = initialSpinAxis.sqrMagnitude > 0.0001f ? initialSpinAxis.normalized : Vector3.up;
                angularSpeed = spinDegreesPerSecond;
                duration = Mathf.Max(0.05f, lifetimeSeconds);
                startScale = transform.localScale;
                elapsed = 0f;
            }

            private void Update()
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var easedDistanceTime = elapsed * Mathf.Lerp(1f, 0.68f, t);
                transform.position = startPosition + velocity * easedDistanceTime;
                transform.Rotate(spinAxis, angularSpeed * Time.deltaTime, Space.World);

                var shrink = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1f, t));
                transform.localScale = startScale * Mathf.Max(0.001f, shrink);
                if (elapsed >= duration)
                {
                    Destroy(gameObject);
                }
            }
        }

        private sealed class UnsupportedIslandSprayBurstCleanup : MonoBehaviour
        {
            private float duration;
            private float elapsed;

            public void Initialize(float lifetimeSeconds)
            {
                duration = Mathf.Max(0.05f, lifetimeSeconds);
                elapsed = 0f;
            }

            private void Update()
            {
                elapsed += Time.deltaTime;
                if (elapsed >= duration)
                {
                    Destroy(gameObject);
                }
            }
        }

        private sealed class FallingWallSlabAnimation : MonoBehaviour
        {
            private const float Gravity = 14f;
            private Vector3 velocity;
            private Vector3 tipAxis;
            private float tipDegreesPerSecond;
            private float duration;
            private float elapsed;
            private Vector3 startScale;
            private float groundY;
            private float restHalfHeight;
            private bool grounded;

            public void Initialize(
                Vector3 initialVelocity,
                Vector3 tipAxisWorld,
                float tipSpeed,
                float lifetimeSeconds,
                float groundLevelY,
                float groundedHalfHeight)
            {
                velocity = initialVelocity;
                tipAxis = tipAxisWorld.sqrMagnitude > 0.0001f ? tipAxisWorld.normalized : Vector3.right;
                tipDegreesPerSecond = tipSpeed;
                duration = Mathf.Max(0.1f, lifetimeSeconds);
                startScale = transform.localScale;
                elapsed = 0f;
                groundY = groundLevelY;
                restHalfHeight = Mathf.Max(0.02f, groundedHalfHeight);
                grounded = false;
            }

            private void Update()
            {
                elapsed += Time.deltaTime;
                if (!grounded)
                {
                    velocity += Vector3.down * (Gravity * Time.deltaTime);
                    transform.position += velocity * Time.deltaTime;
                    transform.Rotate(tipAxis, tipDegreesPerSecond * Time.deltaTime, Space.World);
                    if (transform.position.y - restHalfHeight <= groundY)
                    {
                        transform.position = new Vector3(
                            transform.position.x,
                            groundY + restHalfHeight,
                            transform.position.z);
                        if (velocity.y < -1.4f)
                        {
                            velocity = new Vector3(velocity.x * 0.55f, -velocity.y * 0.28f, velocity.z * 0.55f);
                            tipDegreesPerSecond *= 0.45f;
                        }
                        else
                        {
                            velocity = Vector3.zero;
                            tipDegreesPerSecond = 0f;
                            grounded = true;
                        }
                    }
                }

                var t = Mathf.Clamp01(elapsed / duration);
                var shrink = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, t));
                transform.localScale = startScale * Mathf.Max(0.001f, shrink);
                if (elapsed >= duration)
                {
                    Destroy(gameObject);
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
            if (!initialized)
            {
                chunks.Clear();
                chunksByIndex.Clear();
                chunkGridBuilt = false;
            }

            configuredBiteDirectionLocal = biteDirectionWorld.sqrMagnitude > 0.0001f
                ? transform.InverseTransformDirection(biteDirectionWorld.normalized)
                : Vector3.zero;

            if (UsesContourOwnedWallDamage() || ShouldInitializeStartupStructuralWallBody())
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
                var stamp = AddContourOwnedWallDamage(hitPoint);
                SpawnContourOwnedWallHitSpray(stamp, hitNormal);
                RemoveUnsupportedContourOwnedWallIslands(hitNormal);
                RebuildCombinedMesh();
                return;
            }

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
            var originalRenderers = GetOriginalSourceRenderers();
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
            ApplyGeneratedBodyRenderingLayer(combinedRenderer);

            if (!UsesContourOwnedWallDamage())
            {
                EnsureChunkGrid(sourceSize);
            }

            if (UsesStructuralWallOutlineSource())
            {
                EnsureStructuralWallOutlineSource();
            }

            var contour = new GameObject("Destructible Damage Contours");
            contour.transform.SetParent(transform, false);
            damageContourMeshFilter = contour.AddComponent<MeshFilter>();
            damageContourRenderer = contour.AddComponent<MeshRenderer>();
            damageContourRenderer.sharedMaterial = GetDamageContourMaterial();
            DroidRenderSetup.ApplyRenderer(damageContourRenderer, StylizedOutlineCategory.None);

            RebuildCombinedMesh();
        }

        private void ApplyGeneratedBodyRenderingLayer(Renderer renderer)
        {
            DroidRenderSetup.ApplyRenderer(renderer, StylizedOutlineCategory.None);
            if (renderer != null && UsesStructuralWallOutlineSource())
            {
                renderer.renderingLayerMask |= DroidRenderSetup.WallRenderingLayer;
            }
        }

        private Renderer[] GetOriginalSourceRenderers()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            var sourceRenderers = new List<Renderer>(renderers.Length);
            foreach (var renderer in renderers)
            {
                if (renderer == null ||
                    renderer == combinedRenderer ||
                    renderer == outlineSourceRenderer ||
                    renderer == damageContourRenderer)
                {
                    continue;
                }

                sourceRenderers.Add(renderer);
            }

            return sourceRenderers.ToArray();
        }

        private void EnsureStructuralWallOutlineSource()
        {
            if (!UsesStructuralWallOutlineSource())
            {
                return;
            }

            if (!UsesContourOwnedWallDamage())
            {
                EnsureChunkGrid(GetSourceSize());
            }

            if (outlineSourceMeshFilter == null || outlineSourceRenderer == null)
            {
                var outlineSource = transform.Find(WallOutlineSourceName);
                var outlineSourceObject = outlineSource != null
                    ? outlineSource.gameObject
                    : new GameObject(WallOutlineSourceName);
                if (outlineSource == null)
                {
                    outlineSourceObject.transform.SetParent(transform, false);
                }

                outlineSourceMeshFilter = outlineSourceObject.GetComponent<MeshFilter>();
                if (outlineSourceMeshFilter == null)
                {
                    outlineSourceMeshFilter = outlineSourceObject.AddComponent<MeshFilter>();
                }

                outlineSourceRenderer = outlineSourceObject.GetComponent<MeshRenderer>();
                if (outlineSourceRenderer == null)
                {
                    outlineSourceRenderer = outlineSourceObject.AddComponent<MeshRenderer>();
                }
            }

            outlineSourceRenderer.enabled = true;
            outlineSourceRenderer.sharedMaterial = GetOutlineProxyMaterial();
            DroidRenderSetup.ApplyRenderer(outlineSourceRenderer, configuredOutlineCategory);
            RebuildOutlineSourceMesh();
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

            if (UsesIntactCornerPillarWallOutlineSourceGeometry())
            {
                AddCornerPillarWallOutlineSourceGeometry(vertices, triangles);
                outlineSourceMeshFilter.sharedMesh = vertices.Count == 0
                    ? null
                    : CreateMesh("Destructible Wall Outline Source Mesh", vertices, new[] { triangles });
                return;
            }

            if (damageProfile == DestructibleDamageProfile.CornerPillar &&
                configuredOutlineCategory != StylizedOutlineCategory.Floor)
            {
                AddBittenCornerPillarOutlineSourceGeometry(vertices, triangles);
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

        private bool UsesIntactCornerPillarWallOutlineSourceGeometry()
        {
            if (damageProfile != DestructibleDamageProfile.CornerPillar ||
                configuredOutlineCategory == StylizedOutlineCategory.Floor)
            {
                return false;
            }

            foreach (var chunk in chunks)
            {
                if (chunk.Destroyed || chunk.HasCornerBite)
                {
                    return false;
                }
            }

            return true;
        }

        private void AddCornerPillarWallOutlineSourceGeometry(List<Vector3> vertices, List<int> triangles)
        {
            var sourceSize = GetSourceSize();
            if (sourceSize.x <= 0.01f || sourceSize.y <= 0.01f || sourceSize.z <= 0.01f)
            {
                return;
            }

            AddCornerPillarWallOutlinePlane(vertices, triangles, sourceSize, Vector3.right, true);
            AddCornerPillarWallOutlinePlane(vertices, triangles, sourceSize, Vector3.left, true);
            AddCornerPillarWallOutlinePlane(vertices, triangles, sourceSize, Vector3.forward, true);
            AddCornerPillarWallOutlinePlane(vertices, triangles, sourceSize, Vector3.back, true);
            AddCornerPillarWallOutlinePlane(vertices, triangles, sourceSize, Vector3.up, false);
        }

        private void AddCornerPillarWallOutlinePlane(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 sourceSize,
            Vector3 normal,
            bool sinkFloorEdge)
        {
            GetFaceBasis(normal, sourceSize, out var n, out var u, out var v, out var halfN, out var halfU, out var halfV);
            var bounds = new DamageComponentPlaneBounds(-halfU, halfU, -halfV, halfV);
            if (!bounds.IsValid || halfN <= 0f)
            {
                return;
            }

            if (sinkFloorEdge)
            {
                bounds = SinkContourOwnedWallOutlineFloorEdge(bounds, u, v);
            }

            AddContourOwnedWallPlaneQuad(
                vertices,
                triangles,
                n,
                u,
                v,
                halfN + WallOutlineProxyDepthOffset,
                bounds.MinU,
                bounds.MaxU,
                bounds.MinV,
                bounds.MaxV);
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

        private void EnsureChunkGrid(Vector3 size)
        {
            if (chunkGridBuilt)
            {
                return;
            }

            BuildChunkGrid(size);
        }

        private void BuildChunkGrid(Vector3 size)
        {
            chunks.Clear();
            chunksByIndex.Clear();
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

            chunkGridBuilt = true;
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

        private bool UsesStructuralWallOutlineSource()
        {
            return configuredOutlineCategory != StylizedOutlineCategory.None &&
                configuredOutlineCategory != StylizedOutlineCategory.Floor;
        }

        private bool ShouldInitializeStartupStructuralWallBody()
        {
            return damageProfile == DestructibleDamageProfile.CornerPillar &&
                UsesStructuralWallOutlineSource();
        }

        private DamageStamp AddContourOwnedWallDamage(Vector3 hitPoint)
        {
            if (!TryGetContourOwnedWallBasis(0f, out var normal, out var u, out var v, out var halfN, out var bounds))
            {
                return null;
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
            return stamp;
        }

        private void RemoveUnsupportedContourOwnedWallIslands()
        {
            var fallbackNormal = transform.TransformDirection(GetWallNormalLocal());
            RemoveUnsupportedContourOwnedWallIslands(fallbackNormal);
        }

        private void RemoveUnsupportedContourOwnedWallIslands(Vector3 hitNormalWorld)
        {
            if (!UsesContourOwnedWallDamage() ||
                wallDamageStamps.Count == 0 ||
                !TryGetContourOwnedWallBasis(0f, out var normal, out var u, out var v, out var halfN, out var bounds))
            {
                return;
            }

            var sprayDirectionWorld = hitNormalWorld.sqrMagnitude > 0.0001f
                ? -hitNormalWorld.normalized
                : transform.TransformDirection(-normal);
            var sprayBudget = MaxUnsupportedIslandSpraysPerDamage;
            var slabBudget = MaxFallingSlabsPerDamage;
            for (var pass = 0; pass < UnsupportedIslandCleanupPasses; pass++)
            {
                var islands = FindUnsupportedContourOwnedWallIslands(bounds);
                if (islands.Count == 0)
                {
                    break;
                }

                var removedAnyIsland = false;
                for (var i = 0; i < islands.Count; i++)
                {
                    var island = islands[i];
                    if (!island.RequiresSpray)
                    {
                        AddUnsupportedWallIslandCleanupStamp(island, normal, u, v, halfN);
                        SpawnFallingWallSlab(island, normal, u, v, halfN, ref slabBudget);
                        removedAnyIsland = true;
                        continue;
                    }

                    if (sprayBudget <= 0)
                    {
                        continue;
                    }

                    if (SpawnUnsupportedWallIslandSpray(island, normal, u, v, halfN, sprayDirectionWorld, ref sprayBudget))
                    {
                        AddUnsupportedWallIslandCleanupStamp(island, normal, u, v, halfN);
                        SpawnFallingWallSlab(island, normal, u, v, halfN, ref slabBudget);
                        removedAnyIsland = true;
                    }
                }

                if (!removedAnyIsland)
                {
                    break;
                }
            }

            RemoveCantileverCollapsedWallSections(hitNormalWorld);
            RemovePedestalCrushedWallSections(hitNormalWorld);
        }

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
                        if (bottomVerticalIndex >= runStart && bottomVerticalIndex <= w - 1)
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

        private static void ComputeCantileverHops(List<CantileverRun>[] runsByColumn, float verticalStep)
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
                RelaxCantileverNeighbors(runsByColumn, current, current.HorizontalIndex - 1, verticalStep, queue);
                RelaxCantileverNeighbors(runsByColumn, current, current.HorizontalIndex + 1, verticalStep, queue);
            }
        }

        private static void RelaxCantileverNeighbors(
            List<CantileverRun>[] runsByColumn,
            CantileverRun current,
            int neighborColumn,
            float verticalStep,
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

                var overlapCells = Mathf.Min(neighbor.VerticalEnd, current.VerticalEnd) -
                    Mathf.Max(neighbor.VerticalStart, current.VerticalStart) + 1;
                neighbor.Hops = current.Hops + 1;
                neighbor.Bottleneck = Mathf.Min(current.Bottleneck, overlapCells * verticalStep);
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

        private static List<CantileverRun> SelectCantileverFailingRuns(
            List<CantileverRun>[] runsByColumn,
            List<CantileverRun> component,
            UnsupportedIslandScanGrid grid,
            bool upIsU,
            int topVerticalIndex)
        {
            var failing = new List<CantileverRun>();
            var horizontalStep = CantileverHorizontalStep(grid, upIsU);
            var disconnected = false;
            var touchesFreeEdge = false;
            var seeds = new List<CantileverRun>();
            foreach (var run in component)
            {
                if (run.Hops == int.MaxValue)
                {
                    disconnected = true;
                    if ((topVerticalIndex >= run.VerticalStart && topVerticalIndex <= run.VerticalEnd) ||
                        run.HorizontalIndex == 0 ||
                        run.HorizontalIndex == runsByColumn.Length - 1)
                    {
                        touchesFreeEdge = true;
                    }

                    continue;
                }

                var reach = run.Hops * horizontalStep;
                if (reach < CantileverMinimumOverhangReach)
                {
                    continue;
                }

                if (reach > CantileverSlendernessLimit * Mathf.Max(run.Bottleneck, CantileverMinimumNeckThickness))
                {
                    seeds.Add(run);
                }
            }

            if (disconnected)
            {
                if (touchesFreeEdge)
                {
                    failing.AddRange(component);
                }

                return failing;
            }

            if (seeds.Count == 0)
            {
                return failing;
            }

            var failingSet = new HashSet<CantileverRun>(seeds);
            var queue = new Queue<CantileverRun>(seeds);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                QueueDownstreamCantileverRuns(runsByColumn, current, current.HorizontalIndex - 1, failingSet, queue);
                QueueDownstreamCantileverRuns(runsByColumn, current, current.HorizontalIndex + 1, failingSet, queue);
            }

            foreach (var run in component)
            {
                if (failingSet.Contains(run))
                {
                    failing.Add(run);
                }
            }

            return failing;
        }

        private static void QueueDownstreamCantileverRuns(
            List<CantileverRun>[] runsByColumn,
            CantileverRun current,
            int neighborColumn,
            HashSet<CantileverRun> failingSet,
            Queue<CantileverRun> queue)
        {
            if (neighborColumn < 0 || neighborColumn >= runsByColumn.Length)
            {
                return;
            }

            foreach (var neighbor in runsByColumn[neighborColumn])
            {
                if (neighbor.Hops == 0 ||
                    neighbor.Hops == int.MaxValue ||
                    neighbor.Hops < current.Hops ||
                    failingSet.Contains(neighbor) ||
                    neighbor.VerticalStart > current.VerticalEnd ||
                    neighbor.VerticalEnd < current.VerticalStart)
                {
                    continue;
                }

                failingSet.Add(neighbor);
                queue.Enqueue(neighbor);
            }
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
                ComputeCantileverHops(runsByColumn, CantileverVerticalStep(grid, upIsU));
                var components = GatherCantileverOverhangComponents(runsByColumn);
                var topVerticalIndex = upSign > 0f ? CantileverVerticalCount(grid, upIsU) - 1 : 0;
                var removedAny = false;
                var componentMask = new bool[grid.Count];
                foreach (var component in components)
                {
                    var failingRuns = SelectCantileverFailingRuns(runsByColumn, component, grid, upIsU, topVerticalIndex);
                    if (failingRuns.Count == 0)
                    {
                        continue;
                    }

                    var cells = CollectCantileverComponentCells(grid, upIsU, failingRuns, componentMask);
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

        private void RemovePedestalCrushedWallSections(Vector3 hitNormalWorld)
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
            for (var pass = 0; pass < PedestalMaxFailurePasses; pass++)
            {
                var grid = BuildUnsupportedIslandScanGrid(bounds);
                if (grid.Count == 0)
                {
                    return;
                }

                var crushedCells = FindPedestalCrushedCells(grid, upIsU, upSign);
                if (crushedCells.Count == 0)
                {
                    return;
                }

                var fallingCells = GatherPedestalFallingCells(grid, upIsU, upSign, crushedCells);
                if (fallingCells.Count == 0)
                {
                    return;
                }

                var fallingMask = new bool[grid.Count];
                for (var i = 0; i < fallingCells.Count; i++)
                {
                    fallingMask[fallingCells[i]] = true;
                }

                var visited = new bool[grid.Count];
                var componentMask = new bool[grid.Count];
                var removedAnyComponent = false;
                for (var i = 0; i < fallingCells.Count; i++)
                {
                    var seed = fallingCells[i];
                    if (visited[seed])
                    {
                        continue;
                    }

                    var cells = new List<int>();
                    var queue = new Queue<int>();
                    visited[seed] = true;
                    queue.Enqueue(seed);
                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        cells.Add(current);
                        componentMask[current] = true;
                        var x = grid.CellX(current);
                        var y = grid.CellY(current);
                        for (var offsetIndex = 0; offsetIndex < IslandScanNeighborOffsets.Length; offsetIndex++)
                        {
                            var neighborX = x + IslandScanNeighborOffsets[offsetIndex].x;
                            var neighborY = y + IslandScanNeighborOffsets[offsetIndex].y;
                            if (!grid.ContainsCell(neighborX, neighborY))
                            {
                                continue;
                            }

                            var neighbor = grid.Index(neighborX, neighborY);
                            if (!fallingMask[neighbor] || visited[neighbor])
                            {
                                continue;
                            }

                            visited[neighbor] = true;
                            queue.Enqueue(neighbor);
                        }
                    }

                    if (TryCreateUnsupportedIslandFromCells(grid, cells, componentMask, out var island))
                    {
                        AddUnsupportedWallIslandCleanupStamp(island, normal, u, v, halfN);
                        SpawnFallingWallSlab(island, normal, u, v, halfN, ref slabBudget);
                        if (island.RequiresSpray && sprayBudget > 0)
                        {
                            SpawnUnsupportedWallIslandSpray(island, normal, u, v, halfN, sprayDirectionWorld, ref sprayBudget);
                        }

                        removedAnyComponent = true;
                    }

                    ClearCantileverComponentMask(componentMask, cells);
                }

                if (!removedAnyComponent)
                {
                    return;
                }
            }
        }

        private static List<int> FindPedestalCrushedCells(UnsupportedIslandScanGrid grid, bool upIsU, float upSign)
        {
            var crushed = new List<int>();
            var horizontalCount = CantileverHorizontalCount(grid, upIsU);
            var verticalCount = CantileverVerticalCount(grid, upIsU);
            var cellArea = CantileverVerticalStep(grid, upIsU) * CantileverHorizontalStep(grid, upIsU);
            if (verticalCount < 2)
            {
                return crushed;
            }

            var loads = new float[grid.Count];
            var topW = upSign > 0f ? verticalCount - 1 : 0;
            var bottomW = upSign > 0f ? 0 : verticalCount - 1;
            var stepDir = upSign > 0f ? -1 : 1;
            for (var w = topW; w != bottomW; w += stepDir)
            {
                var belowW = w + stepDir;
                var h = 0;
                while (h < horizontalCount)
                {
                    if (!grid.Solid[CantileverCellIndex(grid, upIsU, h, w)])
                    {
                        h++;
                        continue;
                    }

                    var spanStart = h;
                    var spanLoad = 0f;
                    while (h < horizontalCount && grid.Solid[CantileverCellIndex(grid, upIsU, h, w)])
                    {
                        spanLoad += loads[CantileverCellIndex(grid, upIsU, h, w)] + 1f;
                        h++;
                    }

                    var spanEnd = h - 1;
                    var contactCount = 0;
                    for (var contact = spanStart; contact <= spanEnd; contact++)
                    {
                        if (grid.Solid[CantileverCellIndex(grid, upIsU, contact, belowW)])
                        {
                            contactCount++;
                        }
                    }

                    if (contactCount == 0)
                    {
                        continue;
                    }

                    var spanWidthCells = spanEnd - spanStart + 1;
                    if (spanWidthCells > contactCount * PedestalWidthAmplificationLimit &&
                        spanLoad * cellArea >= PedestalMinimumCrushMass)
                    {
                        for (var contact = spanStart; contact <= spanEnd; contact++)
                        {
                            var crushedIndex = CantileverCellIndex(grid, upIsU, contact, belowW);
                            if (grid.Solid[crushedIndex])
                            {
                                crushed.Add(crushedIndex);
                            }
                        }
                    }

                    var loadPerContact = spanLoad / contactCount;
                    for (var contact = spanStart; contact <= spanEnd; contact++)
                    {
                        var belowIndex = CantileverCellIndex(grid, upIsU, contact, belowW);
                        if (grid.Solid[belowIndex])
                        {
                            loads[belowIndex] += loadPerContact;
                        }
                    }
                }
            }

            return crushed;
        }

        private static List<int> GatherPedestalFallingCells(
            UnsupportedIslandScanGrid grid,
            bool upIsU,
            float upSign,
            List<int> crushedCells)
        {
            var reachableOriginal = ComputeFloorReachableCells(grid, upIsU, upSign, null);
            var crushedMask = new bool[grid.Count];
            for (var i = 0; i < crushedCells.Count; i++)
            {
                crushedMask[crushedCells[i]] = true;
            }

            var reachableBroken = ComputeFloorReachableCells(grid, upIsU, upSign, crushedMask);
            var falling = new List<int>();
            for (var index = 0; index < grid.Count; index++)
            {
                if (grid.Solid[index] && reachableOriginal[index] && !reachableBroken[index])
                {
                    falling.Add(index);
                }
            }

            return falling;
        }

        private static bool[] ComputeFloorReachableCells(UnsupportedIslandScanGrid grid, bool upIsU, float upSign, bool[] blockedMask)
        {
            var reachable = new bool[grid.Count];
            var queue = new Queue<int>();
            var horizontalCount = CantileverHorizontalCount(grid, upIsU);
            var verticalCount = CantileverVerticalCount(grid, upIsU);
            var bottomW = upSign > 0f ? 0 : verticalCount - 1;
            for (var h = 0; h < horizontalCount; h++)
            {
                var index = CantileverCellIndex(grid, upIsU, h, bottomW);
                if (grid.Solid[index] && (blockedMask == null || !blockedMask[index]) && !reachable[index])
                {
                    reachable[index] = true;
                    queue.Enqueue(index);
                }
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var x = grid.CellX(current);
                var y = grid.CellY(current);
                for (var i = 0; i < IslandScanNeighborOffsets.Length; i++)
                {
                    var neighborX = x + IslandScanNeighborOffsets[i].x;
                    var neighborY = y + IslandScanNeighborOffsets[i].y;
                    if (!grid.ContainsCell(neighborX, neighborY))
                    {
                        continue;
                    }

                    var neighbor = grid.Index(neighborX, neighborY);
                    if (!grid.Solid[neighbor] ||
                        reachable[neighbor] ||
                        (blockedMask != null && blockedMask[neighbor]))
                    {
                        continue;
                    }

                    reachable[neighbor] = true;
                    queue.Enqueue(neighbor);
                }
            }

            return reachable;
        }

        private List<UnsupportedWallIsland> FindUnsupportedContourOwnedWallIslands(DamageComponentPlaneBounds bounds)
        {
            var islands = new List<UnsupportedWallIsland>();
            if (!bounds.IsValid || wallDamageStamps.Count == 0)
            {
                return islands;
            }

            var grid = BuildUnsupportedIslandScanGrid(bounds);
            if (grid.Count == 0)
            {
                return islands;
            }

            var coreLabels = LabelSupportCoreComponents(grid, out var coreSupported);
            var ownerLabels = AssignSolidCellsToSupportCores(grid, coreLabels);
            var unsupportedCells = BuildUnsupportedSolidMask(grid, ownerLabels, coreSupported);
            AddUnsupportedIslandComponents(grid, unsupportedCells, islands);
            AddOpenContourShardIslands(bounds, grid, ownerLabels, coreSupported, islands);

            islands.Sort((a, b) => b.Area.CompareTo(a.Area));
            return islands;
        }

        private UnsupportedIslandScanGrid BuildUnsupportedIslandScanGrid(DamageComponentPlaneBounds bounds)
        {
            var cellSize = Mathf.Max(
                UnsupportedIslandScanTargetCellSize,
                bounds.Width / UnsupportedIslandScanMaxCellsPerAxis,
                bounds.Height / UnsupportedIslandScanMaxCellsPerAxis);
            var columns = Mathf.Clamp(Mathf.CeilToInt(bounds.Width / cellSize), 1, UnsupportedIslandScanMaxCellsPerAxis);
            var rows = Mathf.Clamp(Mathf.CeilToInt(bounds.Height / cellSize), 1, UnsupportedIslandScanMaxCellsPerAxis);
            var stepU = bounds.Width / columns;
            var stepV = bounds.Height / rows;
            var supportRadiusCells = Mathf.Max(
                1,
                Mathf.CeilToInt((UnsupportedIslandSupportBridgeWidth * 0.5f) / Mathf.Max(0.0001f, Mathf.Min(stepU, stepV))));
            var solid = new bool[columns * rows];
            var supportCore = new bool[solid.Length];
            var grid = new UnsupportedIslandScanGrid(bounds, columns, rows, supportRadiusCells, stepU, stepV, solid, supportCore);

            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < columns; x++)
                {
                    var index = grid.Index(x, y);
                    solid[index] = IsWallMaterialPresentInScanCell(grid, x, y);
                }
            }

            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < columns; x++)
                {
                    var index = grid.Index(x, y);
                    supportCore[index] = solid[index] && IsSupportCoreCell(grid, x, y);
                }
            }

            return grid;
        }

        private bool IsWallMaterialPresentInScanCell(UnsupportedIslandScanGrid grid, int x, int y)
        {
            if (!IsPointInsideWallDamageUnion(grid.CellCenter(x, y)))
            {
                return true;
            }

            var offsetU = grid.StepU * UnsupportedIslandCellSampleInset;
            var offsetV = grid.StepV * UnsupportedIslandCellSampleInset;
            var solidSamples = 0;
            solidSamples += IsWallMaterialPresentAtSample(grid, x, y, -offsetU, -offsetV) ? 1 : 0;
            solidSamples += IsWallMaterialPresentAtSample(grid, x, y, offsetU, -offsetV) ? 1 : 0;
            solidSamples += IsWallMaterialPresentAtSample(grid, x, y, offsetU, offsetV) ? 1 : 0;
            solidSamples += IsWallMaterialPresentAtSample(grid, x, y, -offsetU, offsetV) ? 1 : 0;
            solidSamples += IsWallMaterialPresentAtSample(grid, x, y, 0f, -offsetV) ? 1 : 0;
            solidSamples += IsWallMaterialPresentAtSample(grid, x, y, offsetU, 0f) ? 1 : 0;
            solidSamples += IsWallMaterialPresentAtSample(grid, x, y, 0f, offsetV) ? 1 : 0;
            solidSamples += IsWallMaterialPresentAtSample(grid, x, y, -offsetU, 0f) ? 1 : 0;
            return solidSamples >= UnsupportedIslandMinimumSolidCellSamples;
        }

        private bool IsWallMaterialPresentAtSample(UnsupportedIslandScanGrid grid, int x, int y, float offsetU, float offsetV)
        {
            var sample = grid.CellCenter(x, y) + new Vector2(offsetU, offsetV);
            return sample.x >= grid.Bounds.MinU &&
                sample.x <= grid.Bounds.MaxU &&
                sample.y >= grid.Bounds.MinV &&
                sample.y <= grid.Bounds.MaxV &&
                !IsPointInsideWallDamageUnion(sample);
        }

        private static bool IsSupportCoreCell(UnsupportedIslandScanGrid grid, int x, int y)
        {
            var radius = grid.SupportRadiusCells;
            var radiusSquared = radius * radius;
            var sampleCount = 0;
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radiusSquared)
                    {
                        continue;
                    }

                    var sampleX = x + dx;
                    var sampleY = y + dy;
                    if (!grid.ContainsCell(sampleX, sampleY))
                    {
                        continue;
                    }

                    sampleCount++;
                    if (!grid.Solid[grid.Index(sampleX, sampleY)])
                    {
                        return false;
                    }
                }
            }

            return sampleCount > 0;
        }

        private static int[] LabelSupportCoreComponents(UnsupportedIslandScanGrid grid, out bool[] coreSupported)
        {
            var labels = CreateFilledIntArray(grid.Count, -1);
            var supported = new List<bool>();
            var queue = new Queue<int>();
            for (var index = 0; index < grid.Count; index++)
            {
                if (!grid.SupportCore[index] || labels[index] >= 0)
                {
                    continue;
                }

                var label = supported.Count;
                var perimeterContactLength = 0f;
                var perimeterSideMask = 0;
                labels[index] = label;
                queue.Enqueue(index);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    var x = grid.CellX(current);
                    var y = grid.CellY(current);
                    AccumulatePerimeterSupportContact(grid, x, y, ref perimeterContactLength, ref perimeterSideMask);
                    QueueSupportCoreNeighbors(grid, labels, queue, x, y, label);
                }

                supported.Add(IsMeaningfulPerimeterSupport(grid, perimeterContactLength, perimeterSideMask));
            }

            coreSupported = supported.ToArray();
            return labels;
        }

        private static void AccumulatePerimeterSupportContact(
            UnsupportedIslandScanGrid grid,
            int x,
            int y,
            ref float contactLength,
            ref int sideMask)
        {
            if (x == 0)
            {
                contactLength += grid.StepV;
                sideMask |= 1;
            }

            if (x == grid.Columns - 1)
            {
                contactLength += grid.StepV;
                sideMask |= 2;
            }

            if (y == 0)
            {
                contactLength += grid.StepU;
                sideMask |= 4;
            }

            if (y == grid.Rows - 1)
            {
                contactLength += grid.StepU;
                sideMask |= 8;
            }
        }

        private static bool IsMeaningfulPerimeterSupport(UnsupportedIslandScanGrid grid, float contactLength, int sideMask)
        {
            if (sideMask == 0)
            {
                return false;
            }

            if (CountSetBits(sideMask) >= 2 && contactLength >= UnsupportedIslandSupportBridgeWidth)
            {
                return true;
            }

            return contactLength >= CalculatePerimeterSupportMinSpan(grid);
        }

        private static float CalculatePerimeterSupportMinSpan(UnsupportedIslandScanGrid grid)
        {
            return Mathf.Max(
                UnsupportedIslandPerimeterSupportMinSpan,
                UnsupportedIslandSupportBridgeWidth * 2.5f,
                Mathf.Min(grid.Bounds.Width, grid.Bounds.Height) * 0.08f);
        }

        private static int CountSetBits(int value)
        {
            var count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }

            return count;
        }

        private static void QueueSupportCoreNeighbors(
            UnsupportedIslandScanGrid grid,
            int[] labels,
            Queue<int> queue,
            int x,
            int y,
            int label)
        {
            for (var i = 0; i < IslandScanNeighborOffsets.Length; i++)
            {
                var neighbor = IslandScanNeighborOffsets[i];
                var neighborX = x + neighbor.x;
                var neighborY = y + neighbor.y;
                if (!grid.ContainsCell(neighborX, neighborY))
                {
                    continue;
                }

                var neighborIndex = grid.Index(neighborX, neighborY);
                if (!grid.SupportCore[neighborIndex] || labels[neighborIndex] >= 0)
                {
                    continue;
                }

                labels[neighborIndex] = label;
                queue.Enqueue(neighborIndex);
            }
        }

        private static int[] AssignSolidCellsToSupportCores(UnsupportedIslandScanGrid grid, int[] coreLabels)
        {
            var owners = CreateFilledIntArray(grid.Count, -1);
            var queue = new Queue<int>();
            for (var index = 0; index < grid.Count; index++)
            {
                if (coreLabels[index] < 0)
                {
                    continue;
                }

                owners[index] = coreLabels[index];
                queue.Enqueue(index);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var x = grid.CellX(current);
                var y = grid.CellY(current);
                for (var i = 0; i < IslandScanNeighborOffsets.Length; i++)
                {
                    var neighbor = IslandScanNeighborOffsets[i];
                    var neighborX = x + neighbor.x;
                    var neighborY = y + neighbor.y;
                    if (!grid.ContainsCell(neighborX, neighborY))
                    {
                        continue;
                    }

                    var neighborIndex = grid.Index(neighborX, neighborY);
                    if (!grid.Solid[neighborIndex] || owners[neighborIndex] >= 0)
                    {
                        continue;
                    }

                    owners[neighborIndex] = owners[current];
                    queue.Enqueue(neighborIndex);
                }
            }

            return owners;
        }

        private static bool[] BuildUnsupportedSolidMask(UnsupportedIslandScanGrid grid, int[] ownerLabels, bool[] coreSupported)
        {
            var unsupported = new bool[grid.Count];
            var noCoreVisited = new bool[grid.Count];
            var queue = new Queue<int>();
            var noCoreComponent = new List<int>();
            for (var index = 0; index < grid.Count; index++)
            {
                if (!grid.Solid[index])
                {
                    continue;
                }

                var owner = ownerLabels[index];
                if (owner >= 0)
                {
                    unsupported[index] = owner >= coreSupported.Length || !coreSupported[owner];
                    continue;
                }

                if (noCoreVisited[index])
                {
                    continue;
                }

                noCoreComponent.Clear();
                noCoreVisited[index] = true;
                queue.Enqueue(index);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    noCoreComponent.Add(current);
                    var x = grid.CellX(current);
                    var y = grid.CellY(current);
                    QueueNoCoreSolidNeighbors(grid, ownerLabels, noCoreVisited, queue, x, y);
                }

                for (var i = 0; i < noCoreComponent.Count; i++)
                {
                    unsupported[noCoreComponent[i]] = true;
                }
            }

            return unsupported;
        }

        private static void QueueNoCoreSolidNeighbors(
            UnsupportedIslandScanGrid grid,
            int[] ownerLabels,
            bool[] visited,
            Queue<int> queue,
            int x,
            int y)
        {
            for (var i = 0; i < IslandScanNeighborOffsets.Length; i++)
            {
                var neighbor = IslandScanNeighborOffsets[i];
                var neighborX = x + neighbor.x;
                var neighborY = y + neighbor.y;
                if (!grid.ContainsCell(neighborX, neighborY))
                {
                    continue;
                }

                var neighborIndex = grid.Index(neighborX, neighborY);
                if (!grid.Solid[neighborIndex] || ownerLabels[neighborIndex] >= 0 || visited[neighborIndex])
                {
                    continue;
                }

                visited[neighborIndex] = true;
                queue.Enqueue(neighborIndex);
            }
        }

        private static void AddUnsupportedIslandComponents(
            UnsupportedIslandScanGrid grid,
            bool[] unsupportedCells,
            List<UnsupportedWallIsland> islands)
        {
            var visited = new bool[grid.Count];
            var queue = new Queue<int>();
            var componentCells = new List<int>();
            var componentMask = new bool[grid.Count];
            for (var index = 0; index < grid.Count; index++)
            {
                if (!unsupportedCells[index] || visited[index])
                {
                    continue;
                }

                componentCells.Clear();
                visited[index] = true;
                queue.Enqueue(index);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    componentCells.Add(current);
                    componentMask[current] = true;
                    var x = grid.CellX(current);
                    var y = grid.CellY(current);
                    QueueUnsupportedIslandNeighbors(grid, unsupportedCells, visited, queue, x, y);
                }

                if (TryCreateUnsupportedIslandFromCells(grid, componentCells, componentMask, out var island))
                {
                    islands.Add(island);
                }

                for (var i = 0; i < componentCells.Count; i++)
                {
                    componentMask[componentCells[i]] = false;
                }
            }
        }

        private void AddOpenContourShardIslands(
            DamageComponentPlaneBounds bounds,
            UnsupportedIslandScanGrid grid,
            int[] ownerLabels,
            bool[] coreSupported,
            List<UnsupportedWallIsland> islands)
        {
            var thickness = GetContourOwnedWallContourThickness();
            var rawSegments = BuildClippedVisibleContourSegments(bounds, thickness, false, false);
            if (rawSegments.Count == 0)
            {
                return;
            }

            var groups = BuildContourSegmentGroups(rawSegments, Mathf.Max(0.002f, thickness * 0.35f));
            if (groups.Count == 0)
            {
                return;
            }

            var supportProbeDistance = Mathf.Max(
                thickness * OpenContourShardSupportProbeScale,
                OpenContourShardMinimumSegmentLength * 2f);
            var boundaryHalfWidth = Mathf.Max(
                thickness * OpenContourShardBoundaryWidthScale,
                OpenContourShardMinimumSegmentLength * 0.5f);
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (!IsContourShardCandidateGroup(group, supportProbeDistance) ||
                    ContourGroupRestsOnWallFloor(group, bounds, supportProbeDistance) ||
                    IsOpenContourGroupOwnedByUnsupportedIsland(group, rawSegments, islands, supportProbeDistance) ||
                    IsOpenContourGroupAdjacentToSupportedMaterial(group, rawSegments, grid, ownerLabels, coreSupported, supportProbeDistance))
                {
                    continue;
                }

                if (TryCreateOpenContourShardIsland(group, rawSegments, boundaryHalfWidth, out var island))
                {
                    islands.Add(island);
                }
            }
        }

        private static bool IsContourShardCandidateGroup(ContourSegmentGroup group, float supportProbeDistance)
        {
            if (group.SegmentIndexes.Count == 0)
            {
                return false;
            }

            if (!group.IsClosed)
            {
                var openMaxSpan = Mathf.Max(group.Span.x, group.Span.y);
                var openMinSpan = Mathf.Min(group.Span.x, group.Span.y);
                return group.SegmentIndexes.Count <= 2 ||
                    group.TotalLength <= supportProbeDistance * 6f ||
                    openMinSpan <= supportProbeDistance * 1.5f ||
                    openMaxSpan <= supportProbeDistance * 2.5f;
            }

            if (group.SegmentIndexes.Count > 8)
            {
                return false;
            }

            var maxSpan = Mathf.Max(group.Span.x, group.Span.y);
            var smallClosedShardLimit = Mathf.Max(
                supportProbeDistance * 1.25f,
                OpenContourShardMinimumSegmentLength * 4f);
            return maxSpan <= smallClosedShardLimit;
        }

        private static bool ContourGroupTouchesBounds(ContourSegmentGroup group, DamageComponentPlaneBounds bounds, float margin)
        {
            return group.Min.x <= bounds.MinU + margin ||
                group.Max.x >= bounds.MaxU - margin ||
                group.Min.y <= bounds.MinV + margin ||
                group.Max.y >= bounds.MaxV - margin;
        }

        private bool ContourGroupRestsOnWallFloor(ContourSegmentGroup group, DamageComponentPlaneBounds bounds, float margin)
        {
            const float boundaryEpsilon = 0.012f;
            if (ContourGroupTouchesBounds(group, bounds, boundaryEpsilon))
            {
                return true;
            }

            if (!TryGetContourOwnedWallBasis(0f, out _, out var u, out var v, out _, out _) ||
                !TryGetWallUvUpAxis(u, v, out var upIsU, out var upSign))
            {
                return ContourGroupTouchesBounds(group, bounds, margin);
            }

            if (upIsU)
            {
                return upSign > 0f
                    ? group.Min.x <= bounds.MinU + margin
                    : group.Max.x >= bounds.MaxU - margin;
            }

            return upSign > 0f
                ? group.Min.y <= bounds.MinV + margin
                : group.Max.y >= bounds.MaxV - margin;
        }

        private bool IsOpenContourGroupAdjacentToSupportedMaterial(
            ContourSegmentGroup group,
            List<ContourSegment2D> segments,
            UnsupportedIslandScanGrid grid,
            int[] ownerLabels,
            bool[] coreSupported,
            float probeDistance)
        {
            for (var i = 0; i < group.SegmentIndexes.Count; i++)
            {
                var segment = segments[group.SegmentIndexes[i]];
                if (OpenContourSegmentTouchesSupportedMaterial(segment.Start, segment.End, grid, ownerLabels, coreSupported, probeDistance))
                {
                    return true;
                }
            }

            return false;
        }

        private bool OpenContourSegmentTouchesSupportedMaterial(
            Vector2 start,
            Vector2 end,
            UnsupportedIslandScanGrid grid,
            int[] ownerLabels,
            bool[] coreSupported,
            float probeDistance)
        {
            var delta = end - start;
            var length = delta.magnitude;
            if (length <= 0.0001f)
            {
                return OpenContourSideProbeTouchesSupportedMaterial(start, Vector2.right, grid, ownerLabels, coreSupported, probeDistance);
            }

            var direction = delta / length;
            var perpendicular = new Vector2(-direction.y, direction.x);
            var sampleCount = Mathf.Clamp(
                Mathf.CeilToInt(length / Mathf.Max(0.0001f, probeDistance)),
                1,
                32);
            for (var i = 0; i <= sampleCount; i++)
            {
                var point = Vector2.Lerp(start, end, i / (float)sampleCount);
                if (OpenContourSideProbeTouchesSupportedMaterial(point, perpendicular, grid, ownerLabels, coreSupported, probeDistance))
                {
                    return true;
                }
            }

            return false;
        }

        private bool OpenContourSideProbeTouchesSupportedMaterial(
            Vector2 point,
            Vector2 perpendicular,
            UnsupportedIslandScanGrid grid,
            int[] ownerLabels,
            bool[] coreSupported,
            float probeDistance)
        {
            return IsOpenContourSupportedMaterialProbe(point + perpendicular * probeDistance, grid, ownerLabels, coreSupported) ||
                IsOpenContourSupportedMaterialProbe(point - perpendicular * probeDistance, grid, ownerLabels, coreSupported) ||
                IsOpenContourSupportedMaterialProbe(point + perpendicular * probeDistance * 0.5f, grid, ownerLabels, coreSupported) ||
                IsOpenContourSupportedMaterialProbe(point - perpendicular * probeDistance * 0.5f, grid, ownerLabels, coreSupported);
        }

        private bool IsOpenContourSupportedMaterialProbe(
            Vector2 point,
            UnsupportedIslandScanGrid grid,
            int[] ownerLabels,
            bool[] coreSupported)
        {
            if (IsPointInsideWallDamageUnion(point))
            {
                return false;
            }

            if (!TryGetScanCell(grid, point, out var x, out var y))
            {
                return false;
            }

            return IsSupportedSolidCell(grid, ownerLabels, coreSupported, x, y);
        }

        private static bool TryGetScanCell(UnsupportedIslandScanGrid grid, Vector2 point, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (point.x < grid.Bounds.MinU ||
                point.x > grid.Bounds.MaxU ||
                point.y < grid.Bounds.MinV ||
                point.y > grid.Bounds.MaxV)
            {
                return false;
            }

            x = Mathf.Clamp(Mathf.FloorToInt((point.x - grid.Bounds.MinU) / Mathf.Max(0.0001f, grid.StepU)), 0, grid.Columns - 1);
            y = Mathf.Clamp(Mathf.FloorToInt((point.y - grid.Bounds.MinV) / Mathf.Max(0.0001f, grid.StepV)), 0, grid.Rows - 1);
            return true;
        }

        private static bool IsSupportedSolidCell(
            UnsupportedIslandScanGrid grid,
            int[] ownerLabels,
            bool[] coreSupported,
            int x,
            int y)
        {
            if (!grid.ContainsCell(x, y))
            {
                return false;
            }

            var index = grid.Index(x, y);
            if (!grid.Solid[index] || ownerLabels == null || index >= ownerLabels.Length)
            {
                return false;
            }

            var owner = ownerLabels[index];
            return owner >= 0 && owner < coreSupported.Length && coreSupported[owner];
        }

        private static bool IsOpenContourGroupOwnedByUnsupportedIsland(
            ContourSegmentGroup group,
            List<ContourSegment2D> segments,
            List<UnsupportedWallIsland> islands,
            float margin)
        {
            if (islands.Count == 0)
            {
                return false;
            }

            for (var islandIndex = 0; islandIndex < islands.Count; islandIndex++)
            {
                var island = islands[islandIndex];
                if (island == null ||
                    !BoundsOverlap(
                        group.Min - Vector2.one * margin,
                        group.Max + Vector2.one * margin,
                        island.Min,
                        island.Max))
                {
                    continue;
                }

                var allSegmentsOwned = true;
                for (var segmentIndex = 0; segmentIndex < group.SegmentIndexes.Count; segmentIndex++)
                {
                    var segment = segments[group.SegmentIndexes[segmentIndex]];
                    if (!OpenContourSegmentOwnedByIsland(segment.Start, segment.End, island, margin))
                    {
                        allSegmentsOwned = false;
                        break;
                    }
                }

                if (allSegmentsOwned)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool OpenContourSegmentOwnedByIsland(Vector2 start, Vector2 end, UnsupportedWallIsland island, float margin)
        {
            var delta = end - start;
            var length = delta.magnitude;
            var sampleCount = Mathf.Clamp(Mathf.CeilToInt(length / Mathf.Max(0.0001f, margin)), 1, 16);
            for (var i = 0; i <= sampleCount; i++)
            {
                if (!OpenContourPointCoveredByIsland(Vector2.Lerp(start, end, i / (float)sampleCount), island, margin))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool OpenContourPointCoveredByIsland(Vector2 point, UnsupportedWallIsland island, float margin)
        {
            return island != null &&
                point.x >= island.Min.x - margin &&
                point.x <= island.Max.x + margin &&
                point.y >= island.Min.y - margin &&
                point.y <= island.Max.y + margin &&
                (IsPointInPolygon(point, island.Points) ||
                    IsPointNearPolygonBoundary(point, island.Points, margin));
        }

        private static bool IsPointNearPolygonBoundary(Vector2 point, List<Vector2> polygon, float margin)
        {
            if (polygon == null || polygon.Count < 2)
            {
                return false;
            }

            var marginSquared = margin * margin;
            for (var i = 0; i < polygon.Count; i++)
            {
                var next = (i + 1) % polygon.Count;
                if (DistancePointToSegmentSquared(point, polygon[i], polygon[next]) <= marginSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SimplifyPolygonLoop(List<Vector2> points, float tolerance)
        {
            if (points == null || tolerance <= 0f)
            {
                return;
            }

            var toleranceSquared = tolerance * tolerance;
            var removedAny = true;
            while (removedAny && points.Count > 4)
            {
                removedAny = false;
                for (var i = points.Count - 1; i >= 0 && points.Count > 4; i--)
                {
                    var previous = points[(i + points.Count - 1) % points.Count];
                    var next = points[(i + 1) % points.Count];
                    if (DistancePointToSegmentSquared(points[i], previous, next) <= toleranceSquared)
                    {
                        points.RemoveAt(i);
                        removedAny = true;
                    }
                }
            }
        }

        private static float DistancePointToSegmentSquared(Vector2 point, Vector2 start, Vector2 end)
        {
            var delta = end - start;
            var lengthSquared = delta.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
            {
                return (point - start).sqrMagnitude;
            }

            var t = Mathf.Clamp01(Vector2.Dot(point - start, delta) / lengthSquared);
            return (point - (start + delta * t)).sqrMagnitude;
        }

        private static bool TryCreateOpenContourShardIsland(
            ContourSegmentGroup group,
            List<ContourSegment2D> segments,
            float halfWidth,
            out UnsupportedWallIsland island)
        {
            island = null;
            var hullCandidates = new List<Vector2>();
            var weightedCentroid = Vector2.zero;
            var endpointSum = Vector2.zero;
            var endpointCount = 0;
            var totalLength = 0f;
            for (var i = 0; i < group.SegmentIndexes.Count; i++)
            {
                var segment = segments[group.SegmentIndexes[i]];
                var length = (segment.End - segment.Start).magnitude;
                endpointSum += segment.Start + segment.End;
                endpointCount += 2;
                if (length < OpenContourShardMinimumSegmentLength)
                {
                    continue;
                }

                AddInflatedOpenContourPointSamples(hullCandidates, segment.Start, halfWidth);
                AddInflatedOpenContourPointSamples(hullCandidates, segment.End, halfWidth);
                weightedCentroid += (segment.Start + segment.End) * 0.5f * length;
                totalLength += length;
            }

            if (totalLength < OpenContourShardMinimumSegmentLength)
            {
                var center = endpointCount > 0 ? endpointSum / endpointCount : group.Centroid;
                return TryCreatePinContourShardIsland(center, halfWidth, out island);
            }

            if (!TryBuildConvexHull(hullCandidates, out var points))
            {
                return false;
            }

            SanitizePolygonLoop(points);
            if (points.Count < 3)
            {
                return false;
            }

            var area = CalculateSignedPolygonArea(points);
            if (area < 0f)
            {
                points.Reverse();
                area = -area;
            }

            if (area <= 0.000001f)
            {
                return false;
            }

            CalculatePolygonBounds(points, out var min, out var max);
            var sprayAreaEstimate = totalLength * halfWidth;
            island = new UnsupportedWallIsland(
                points,
                weightedCentroid / Mathf.Max(0.0001f, totalLength),
                min,
                max,
                area,
                sprayAreaEstimate > UnsupportedIslandSprayMinimumArea);
            return true;
        }

        private static bool TryCreatePinContourShardIsland(Vector2 center, float radius, out UnsupportedWallIsland island)
        {
            island = null;
            var safeRadius = Mathf.Max(radius, OpenContourShardMinimumSegmentLength * 0.5f);
            var points = new List<Vector2>(8);
            for (var i = 0; i < 8; i++)
            {
                var angle = i / 8f * Mathf.PI * 2f;
                var radiusScale = (i & 1) == 0 ? 1f : 0.64f;
                points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * safeRadius * radiusScale);
            }

            SanitizePolygonLoop(points);
            if (points.Count < 3)
            {
                return false;
            }

            var area = CalculateSignedPolygonArea(points);
            if (area < 0f)
            {
                points.Reverse();
                area = -area;
            }

            if (area <= 0.000001f)
            {
                return false;
            }

            CalculatePolygonBounds(points, out var min, out var max);
            island = new UnsupportedWallIsland(points, center, min, max, area, false);
            return true;
        }

        private static void AddInflatedOpenContourPointSamples(List<Vector2> points, Vector2 center, float radius)
        {
            const int samples = 8;
            for (var i = 0; i < samples; i++)
            {
                var angle = i / (float)samples * Mathf.PI * 2f;
                points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private static bool TryBuildConvexHull(List<Vector2> candidates, out List<Vector2> hull)
        {
            hull = null;
            if (candidates == null || candidates.Count < 3)
            {
                return false;
            }

            candidates.Sort((a, b) =>
            {
                var compareX = a.x.CompareTo(b.x);
                return compareX != 0 ? compareX : a.y.CompareTo(b.y);
            });

            var unique = new List<Vector2>(candidates.Count);
            for (var i = 0; i < candidates.Count; i++)
            {
                if (unique.Count == 0 || (candidates[i] - unique[unique.Count - 1]).sqrMagnitude > 0.0000001f)
                {
                    unique.Add(candidates[i]);
                }
            }

            if (unique.Count < 3)
            {
                return false;
            }

            var lower = new List<Vector2>();
            for (var i = 0; i < unique.Count; i++)
            {
                while (lower.Count >= 2 &&
                    Cross2D(lower[lower.Count - 1] - lower[lower.Count - 2], unique[i] - lower[lower.Count - 1]) <= 0f)
                {
                    lower.RemoveAt(lower.Count - 1);
                }

                lower.Add(unique[i]);
            }

            var upper = new List<Vector2>();
            for (var i = unique.Count - 1; i >= 0; i--)
            {
                while (upper.Count >= 2 &&
                    Cross2D(upper[upper.Count - 1] - upper[upper.Count - 2], unique[i] - upper[upper.Count - 1]) <= 0f)
                {
                    upper.RemoveAt(upper.Count - 1);
                }

                upper.Add(unique[i]);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            hull = lower;
            hull.AddRange(upper);
            return hull.Count >= 3;
        }

        private static void QueueUnsupportedIslandNeighbors(
            UnsupportedIslandScanGrid grid,
            bool[] unsupportedCells,
            bool[] visited,
            Queue<int> queue,
            int x,
            int y)
        {
            for (var i = 0; i < IslandScanNeighborOffsets.Length; i++)
            {
                var neighbor = IslandScanNeighborOffsets[i];
                var neighborX = x + neighbor.x;
                var neighborY = y + neighbor.y;
                if (!grid.ContainsCell(neighborX, neighborY))
                {
                    continue;
                }

                var neighborIndex = grid.Index(neighborX, neighborY);
                if (!unsupportedCells[neighborIndex] || visited[neighborIndex])
                {
                    continue;
                }

                visited[neighborIndex] = true;
                queue.Enqueue(neighborIndex);
            }
        }

        private static bool TryCreateUnsupportedIslandFromCells(
            UnsupportedIslandScanGrid grid,
            List<int> cells,
            bool[] componentMask,
            out UnsupportedWallIsland island)
        {
            island = null;
            var cellArea = cells.Count * grid.StepU * grid.StepV;
            var boundaryCells = cells;
            var boundaryMask = componentMask;
            var cellsTouchBounds = CellsTouchScanBounds(grid, cells);
            if (cellsTouchBounds)
            {
                BuildExpandedUnsupportedIslandCleanupCells(grid, cells, out boundaryCells, out boundaryMask);
            }

            if (!TryBuildUnsupportedIslandBoundaryLoop(grid, boundaryCells, boundaryMask, out var points))
            {
                return false;
            }

            SanitizePolygonLoop(points);
            SimplifyPolygonLoop(points, Mathf.Max(grid.StepU, grid.StepV) * 0.75f);
            if (points.Count < 3)
            {
                return false;
            }

            var polygonArea = CalculateSignedPolygonArea(points);
            if (Mathf.Abs(polygonArea) <= 0.000001f)
            {
                return false;
            }

            if (polygonArea < 0f)
            {
                points.Reverse();
                polygonArea = -polygonArea;
            }

            CalculatePolygonBounds(points, out var min, out var max);
            var touchesBounds = IslandBoundsTouchScanBounds(grid, min, max);
            var area = Mathf.Max(cellArea, polygonArea);
            island = new UnsupportedWallIsland(
                points,
                CalculateUnsupportedIslandCellCentroid(grid, cells),
                min,
                max,
                area,
                !cellsTouchBounds && !touchesBounds && area > UnsupportedIslandSprayMinimumArea);
            return true;
        }

        private static bool CellsTouchScanBounds(UnsupportedIslandScanGrid grid, List<int> cells)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                var x = grid.CellX(cells[i]);
                var y = grid.CellY(cells[i]);
                if (grid.IsPerimeterCell(x, y))
                {
                    return true;
                }
            }

            return false;
        }

        private static void BuildExpandedUnsupportedIslandCleanupCells(
            UnsupportedIslandScanGrid grid,
            List<int> sourceCells,
            out List<int> expandedCells,
            out bool[] expandedMask)
        {
            expandedCells = new List<int>(sourceCells.Count);
            expandedMask = new bool[grid.Count];
            for (var i = 0; i < sourceCells.Count; i++)
            {
                var sourceX = grid.CellX(sourceCells[i]);
                var sourceY = grid.CellY(sourceCells[i]);
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var x = sourceX + dx;
                        var y = sourceY + dy;
                        if (!grid.ContainsCell(x, y))
                        {
                            continue;
                        }

                        var index = grid.Index(x, y);
                        if (expandedMask[index])
                        {
                            continue;
                        }

                        expandedMask[index] = true;
                        expandedCells.Add(index);
                    }
                }
            }
        }

        private static bool IslandBoundsTouchScanBounds(UnsupportedIslandScanGrid grid, Vector2 min, Vector2 max)
        {
            var margin = Mathf.Max(grid.StepU, grid.StepV) * 0.5f;
            return min.x <= grid.Bounds.MinU + margin ||
                max.x >= grid.Bounds.MaxU - margin ||
                min.y <= grid.Bounds.MinV + margin ||
                max.y >= grid.Bounds.MaxV - margin;
        }

        private static Vector2 CalculateUnsupportedIslandCellCentroid(UnsupportedIslandScanGrid grid, List<int> cells)
        {
            var centroid = Vector2.zero;
            for (var i = 0; i < cells.Count; i++)
            {
                centroid += grid.CellCenter(grid.CellX(cells[i]), grid.CellY(cells[i]));
            }

            return centroid / Mathf.Max(1, cells.Count);
        }

        private static bool TryBuildUnsupportedIslandBoundaryLoop(
            UnsupportedIslandScanGrid grid,
            List<int> cells,
            bool[] componentMask,
            out List<Vector2> points)
        {
            var edges = new List<BoundaryEdge>(cells.Count * 2);
            for (var i = 0; i < cells.Count; i++)
            {
                var index = cells[i];
                var x = grid.CellX(index);
                var y = grid.CellY(index);
                if (!IsComponentCell(grid, componentMask, x, y - 1))
                {
                    edges.Add(new BoundaryEdge(new Vector2Int(x, y), new Vector2Int(x + 1, y)));
                }

                if (!IsComponentCell(grid, componentMask, x + 1, y))
                {
                    edges.Add(new BoundaryEdge(new Vector2Int(x + 1, y), new Vector2Int(x + 1, y + 1)));
                }

                if (!IsComponentCell(grid, componentMask, x, y + 1))
                {
                    edges.Add(new BoundaryEdge(new Vector2Int(x + 1, y + 1), new Vector2Int(x, y + 1)));
                }

                if (!IsComponentCell(grid, componentMask, x - 1, y))
                {
                    edges.Add(new BoundaryEdge(new Vector2Int(x, y + 1), new Vector2Int(x, y)));
                }
            }

            return TryBuildLargestBoundaryLoop(grid, edges, out points);
        }

        private static bool IsComponentCell(UnsupportedIslandScanGrid grid, bool[] componentMask, int x, int y)
        {
            return grid.ContainsCell(x, y) && componentMask[grid.Index(x, y)];
        }

        private static bool TryBuildLargestBoundaryLoop(
            UnsupportedIslandScanGrid grid,
            List<BoundaryEdge> edges,
            out List<Vector2> points)
        {
            points = null;
            if (edges.Count < 3)
            {
                return false;
            }

            var outgoing = new Dictionary<Vector2Int, List<int>>();
            for (var i = 0; i < edges.Count; i++)
            {
                if (!outgoing.TryGetValue(edges[i].Start, out var edgeIndexes))
                {
                    edgeIndexes = new List<int>();
                    outgoing[edges[i].Start] = edgeIndexes;
                }

                edgeIndexes.Add(i);
            }

            var visited = new bool[edges.Count];
            var bestArea = 0f;
            for (var i = 0; i < edges.Count; i++)
            {
                if (visited[i])
                {
                    continue;
                }

                if (!TryBuildBoundaryCornerLoop(edges, outgoing, visited, i, out var corners))
                {
                    continue;
                }

                var loopPoints = new List<Vector2>(corners.Count);
                for (var cornerIndex = 0; cornerIndex < corners.Count; cornerIndex++)
                {
                    loopPoints.Add(grid.CornerToUv(corners[cornerIndex]));
                }

                var area = Mathf.Abs(CalculateSignedPolygonArea(loopPoints));
                if (area > bestArea)
                {
                    bestArea = area;
                    points = loopPoints;
                }
            }

            return points != null && points.Count >= 3;
        }

        private static bool TryBuildBoundaryCornerLoop(
            List<BoundaryEdge> edges,
            Dictionary<Vector2Int, List<int>> outgoing,
            bool[] visited,
            int firstEdgeIndex,
            out List<Vector2Int> corners)
        {
            corners = new List<Vector2Int>();
            var start = edges[firstEdgeIndex].Start;
            var edgeIndex = firstEdgeIndex;
            var guard = edges.Count + 1;
            while (guard-- > 0)
            {
                if (visited[edgeIndex])
                {
                    return false;
                }

                var edge = edges[edgeIndex];
                visited[edgeIndex] = true;
                if (corners.Count == 0)
                {
                    corners.Add(edge.Start);
                }

                if (edge.End == start)
                {
                    return corners.Count >= 3;
                }

                corners.Add(edge.End);
                if (!TryFindNextBoundaryEdge(outgoing, edge.End, visited, out edgeIndex))
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryFindNextBoundaryEdge(
            Dictionary<Vector2Int, List<int>> outgoing,
            Vector2Int start,
            bool[] visited,
            out int edgeIndex)
        {
            if (outgoing.TryGetValue(start, out var edgeIndexes))
            {
                for (var i = 0; i < edgeIndexes.Count; i++)
                {
                    var candidate = edgeIndexes[i];
                    if (!visited[candidate])
                    {
                        edgeIndex = candidate;
                        return true;
                    }
                }
            }

            edgeIndex = -1;
            return false;
        }

        private static int[] CreateFilledIntArray(int length, int value)
        {
            var values = new int[length];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = value;
            }

            return values;
        }

        private void AddUnsupportedWallIslandCleanupStamp(
            UnsupportedWallIsland island,
            Vector3 normal,
            Vector3 u,
            Vector3 v,
            float halfN)
        {
            if (island == null || island.Points == null || island.Points.Count < 3)
            {
                return;
            }

            wallDamageStamps.Add(CreateUnsupportedWallIslandCleanupStamp(normal, u, v, halfN, island.Points));
        }

        private static DamageStamp CreateUnsupportedWallIslandCleanupStamp(
            Vector3 normal,
            Vector3 u,
            Vector3 v,
            float halfN,
            List<Vector2> sourcePoints)
        {
            var points = CreatePaddedUnsupportedIslandCleanupPoints(sourcePoints);
            CalculatePolygonBounds(points, out var min, out var max);
            var stamp = new DamageStamp
            {
                Normal = normal,
                U = u,
                V = v,
                Plane = halfN + DamageContourInset,
                Min = min,
                Max = max,
                Points = points,
                RenderContour = false
            };
            stamp.Opposite = CreateOppositeContourOwnedWallStamp(stamp, normal, halfN);
            stamp.Opposite.RenderContour = false;
            return stamp;
        }

        private static Vector2[] CreatePaddedUnsupportedIslandCleanupPoints(List<Vector2> sourcePoints)
        {
            if (sourcePoints == null || sourcePoints.Count == 0)
            {
                return System.Array.Empty<Vector2>();
            }

            if (sourcePoints.Count == 4)
            {
                CalculatePolygonBounds(sourcePoints, out var rectMin, out var rectMax);
                rectMin -= Vector2.one * UnsupportedIslandCleanupPadding;
                rectMax += Vector2.one * UnsupportedIslandCleanupPadding;
                return new[]
                {
                    new Vector2(rectMin.x, rectMin.y),
                    new Vector2(rectMax.x, rectMin.y),
                    new Vector2(rectMax.x, rectMax.y),
                    new Vector2(rectMin.x, rectMax.y)
                };
            }

            var centroid = CalculatePointAverage(sourcePoints);
            var points = new Vector2[sourcePoints.Count];
            for (var i = 0; i < sourcePoints.Count; i++)
            {
                var direction = sourcePoints[i] - centroid;
                points[i] = direction.sqrMagnitude > 0.000001f
                    ? sourcePoints[i] + direction.normalized * UnsupportedIslandCleanupPadding
                    : sourcePoints[i];
            }

            return points;
        }

        private void SpawnContourOwnedWallHitSpray(DamageStamp stamp, Vector3 hitNormalWorld)
        {
            if (stamp == null || stamp.Points == null || stamp.Points.Length < 3)
            {
                return;
            }

            var points = new List<Vector2>(stamp.Points);
            SanitizePolygonLoop(points);
            if (points.Count < 3)
            {
                return;
            }

            var area = CalculateSignedPolygonArea(points);
            if (area < 0f)
            {
                points.Reverse();
                area = -area;
            }

            if (area <= 0.000001f)
            {
                return;
            }

            CalculatePolygonBounds(points, out var min, out var max);
            var island = new UnsupportedWallIsland(
                points,
                CalculatePointAverage(points),
                min,
                max,
                area);
            var sprayDirectionWorld = hitNormalWorld.sqrMagnitude > 0.0001f
                ? -hitNormalWorld.normalized
                : transform.TransformDirection(-stamp.Normal);
            var budget = 1;
            SpawnWallMaterialSpray(
                island,
                stamp.Normal,
                stamp.U,
                stamp.V,
                Mathf.Max(0f, stamp.Plane - DamageContourInset),
                sprayDirectionWorld,
                ref budget,
                WallHitSprayMinChips,
                WallHitSprayMaxChips);
        }

        private bool SpawnUnsupportedWallIslandSpray(
            UnsupportedWallIsland island,
            Vector3 normal,
            Vector3 u,
            Vector3 v,
            float halfN,
            Vector3 sprayDirectionWorld,
            ref int sprayBudget)
        {
            return SpawnWallMaterialSpray(
                island,
                normal,
                u,
                v,
                halfN,
                sprayDirectionWorld,
                ref sprayBudget,
                UnsupportedIslandSprayMinChips,
                UnsupportedIslandSprayMaxChips);
        }

        private bool SpawnWallMaterialSpray(
            UnsupportedWallIsland island,
            Vector3 normal,
            Vector3 u,
            Vector3 v,
            float halfN,
            Vector3 sprayDirectionWorld,
            ref int sprayBudget,
            int minChipCount,
            int maxChipCount)
        {
            if (island == null || island.Points == null || island.Points.Count < 3 || sprayBudget <= 0)
            {
                return false;
            }

            var safeSprayDirectionWorld = sprayDirectionWorld.sqrMagnitude > 0.0001f
                ? sprayDirectionWorld.normalized
                : transform.TransformDirection(-normal);
            var sprayDirectionLocal = transform.InverseTransformDirection(safeSprayDirectionWorld);
            if (sprayDirectionLocal.sqrMagnitude <= 0.0001f)
            {
                sprayDirectionLocal = -normal;
            }

            sprayDirectionLocal.Normalize();
            var seed = CalculateUnsupportedIslandSpraySeed(island, safeSprayDirectionWorld);
            var chipCount = CalculateUnsupportedIslandSprayChipCount(island, seed, minChipCount, maxChipCount);
            var centerLocal = u * island.Centroid.x + v * island.Centroid.y;
            var burst = new GameObject("Wall Material Spray Burst");
            burst.transform.position = transform.TransformPoint(centerLocal + sprayDirectionLocal * (halfN + UnsupportedIslandSpraySourceClearance));
            burst.transform.rotation = Quaternion.identity;
            var createdChips = 0;
            var maxLifetime = 0f;
            var material = GetUnclippedDestructibleBodyMaterial();
            var outlineCategory = configuredOutlineCategory == StylizedOutlineCategory.None
                ? StylizedOutlineCategory.Wall
                : configuredOutlineCategory;
            for (var i = 0; i < chipCount; i++)
            {
                var chipSeed = seed ^ (i * 73856093);
                var chipSize = Mathf.Lerp(UnsupportedIslandSprayChipMinSize, UnsupportedIslandSprayChipMaxSize, Hash01(chipSeed ^ 0x531));
                var chipDepth = chipSize * Mathf.Lerp(0.12f, 0.32f, Hash01(chipSeed ^ 0x93f));
                var mesh = CreateUnsupportedIslandSprayChipMesh(chipSize, chipDepth, chipSeed);
                if (mesh == null)
                {
                    continue;
                }

                var uv = SelectUnsupportedIslandSprayPoint(island, chipSeed, i);
                var sideOffset = u * Mathf.Lerp(-0.035f, 0.035f, Hash01(chipSeed ^ 0x71)) +
                    v * Mathf.Lerp(-0.035f, 0.035f, Hash01(chipSeed ^ 0x2b));
                var spawnLocal = u * uv.x + v * uv.y + sideOffset +
                    sprayDirectionLocal * (halfN + UnsupportedIslandSpraySourceClearance + Mathf.Lerp(0f, 0.035f, Hash01(chipSeed ^ 0x19)));

                var chip = new GameObject("Wall Material Spray Chip");
                chip.transform.SetParent(burst.transform, true);
                chip.transform.position = transform.TransformPoint(spawnLocal);
                chip.transform.rotation = CreateUnsupportedIslandSprayChipRotation(safeSprayDirectionWorld, transform.TransformDirection(v), chipSeed);
                chip.transform.localScale = Vector3.one;

                var meshFilter = chip.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = mesh;
                var renderer = chip.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                DroidRenderSetup.ApplyRenderer(renderer, outlineCategory);

                var lateralLocal = u * Mathf.Lerp(-UnsupportedIslandSprayLateralSpread, UnsupportedIslandSprayLateralSpread, Hash01(chipSeed ^ 0x24c)) +
                    v * Mathf.Lerp(-UnsupportedIslandSprayLateralSpread, UnsupportedIslandSprayLateralSpread, Hash01(chipSeed ^ 0x91d));
                var speed = Mathf.Lerp(UnsupportedIslandSprayMinSpeed, UnsupportedIslandSprayMaxSpeed, Hash01(chipSeed ^ 0x681));
                var velocity = safeSprayDirectionWorld * speed + transform.TransformDirection(lateralLocal);
                var spinAxis = (safeSprayDirectionWorld + transform.TransformDirection(lateralLocal * 0.45f)).normalized;
                var angularSpeed = Mathf.Lerp(260f, 760f, Hash01(chipSeed ^ 0x32f)) * (Hash01(chipSeed ^ 0x4e1) > 0.5f ? 1f : -1f);
                var lifetime = Mathf.Lerp(UnsupportedIslandSprayMinLifetime, UnsupportedIslandSprayMaxLifetime, Hash01(chipSeed ^ 0x7aa));
                chip.AddComponent<UnsupportedIslandSprayChipAnimation>().Initialize(velocity, spinAxis, angularSpeed, lifetime);
                maxLifetime = Mathf.Max(maxLifetime, lifetime);
                createdChips++;
            }

            if (createdChips == 0)
            {
                if (Application.isPlaying)
                {
                    Destroy(burst);
                }
                else
                {
                    DestroyImmediate(burst);
                }

                return false;
            }

            burst.AddComponent<UnsupportedIslandSprayBurstCleanup>().Initialize(maxLifetime + 0.08f);
            sprayBudget--;
            return true;
        }

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
            var drift = transform.TransformDirection(normal) *
                ((Hash01(seed ^ 0x9c7) > 0.5f ? 1f : -1f) * 0.55f);
            SpawnFallingDebrisObject(mesh, transform.TransformPoint(centerLocal), transform.rotation, seed, drift);
            slabBudget--;
        }

        private static Vector3 SlabPointToMeshLocal(Vector2 point, Vector2 centroid, Vector3 u, Vector3 v, Vector3 normal, float depth)
        {
            return u * (point.x - centroid.x) + v * (point.y - centroid.y) + normal * depth;
        }

        private void SpawnFallingDebrisObject(Mesh mesh, Vector3 worldPosition, Quaternion worldRotation, int seed, Vector3 driftVelocityWorld)
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

            var groundY = worldPosition.y - 80f;
            if (Physics.Raycast(worldPosition, Vector3.down, out var groundHit, 120f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                groundY = groundHit.point.y;
            }

            var restHalfHeight = Mathf.Max(0.02f, mesh.bounds.extents.y * 0.7f);
            var lateral = new Vector3(
                Mathf.Lerp(-0.35f, 0.35f, Hash01(seed ^ 0x3d1)),
                0f,
                Mathf.Lerp(-0.35f, 0.35f, Hash01(seed ^ 0x77b)));
            var tipAxis = Vector3.Cross(Vector3.up, lateral.sqrMagnitude > 0.0001f ? lateral.normalized : Vector3.forward);
            var tipSpeed = Mathf.Lerp(18f, FallingSlabMaxTipDegreesPerSecond, Hash01(seed ^ 0x215)) *
                (Hash01(seed ^ 0x6c2) > 0.5f ? 1f : -1f);
            slab.AddComponent<FallingWallSlabAnimation>().Initialize(
                lateral + driftVelocityWorld,
                tipAxis,
                tipSpeed,
                FallingSlabLifetimeSeconds,
                groundY,
                restHalfHeight);
        }

        private int CalculateUnsupportedIslandSpraySeed(UnsupportedWallIsland island, Vector3 sprayDirectionWorld)
        {
            unchecked
            {
                var hash = 0x38d42f5;
                hash = hash * 31 + HashPosition(transform.position);
                hash = hash * 31 + wallDamageStamps.Count;
                hash = hash * 31 + Mathf.RoundToInt(island.Centroid.x * 1000f);
                hash = hash * 31 + Mathf.RoundToInt(island.Centroid.y * 1000f);
                hash = hash * 31 + Mathf.RoundToInt(island.Area * 10000f);
                hash = hash * 31 + Mathf.RoundToInt(sprayDirectionWorld.x * 100f);
                hash = hash * 31 + Mathf.RoundToInt(sprayDirectionWorld.y * 100f);
                hash = hash * 31 + Mathf.RoundToInt(sprayDirectionWorld.z * 100f);
                return hash;
            }
        }

        private static int CalculateUnsupportedIslandSprayChipCount(UnsupportedWallIsland island, int seed, int minChipCount, int maxChipCount)
        {
            var areaContribution = Mathf.FloorToInt(Mathf.Sqrt(Mathf.Max(0f, island.Area)) * 2.5f);
            var seedContribution = Mathf.FloorToInt(Hash01(seed ^ 0x657) * 2f);
            return Mathf.Clamp(Mathf.Max(1, minChipCount) + areaContribution + seedContribution, minChipCount, maxChipCount);
        }

        private static Vector2 SelectUnsupportedIslandSprayPoint(UnsupportedWallIsland island, int seed, int index)
        {
            if (island.Points == null || island.Points.Count == 0)
            {
                return island.Centroid;
            }

            var pointIndex = (int)(((uint)seed + (uint)index * 2654435761u) % (uint)island.Points.Count);
            var blend = Mathf.Lerp(0.25f, 0.82f, Hash01(seed ^ (index * 0x45d9f3b)));
            return Vector2.Lerp(island.Centroid, island.Points[pointIndex], blend);
        }

        private static Quaternion CreateUnsupportedIslandSprayChipRotation(Vector3 forward, Vector3 up, int seed)
        {
            var safeForward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            var safeUp = up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.up;
            if (Vector3.Cross(safeForward, safeUp).sqrMagnitude <= 0.0001f)
            {
                safeUp = Mathf.Abs(Vector3.Dot(safeForward, Vector3.up)) < 0.95f ? Vector3.up : Vector3.right;
            }

            return Quaternion.LookRotation(safeForward, safeUp) *
                Quaternion.Euler(
                    Mathf.Lerp(-45f, 45f, Hash01(seed ^ 0x123)),
                    Mathf.Lerp(-35f, 35f, Hash01(seed ^ 0x456)),
                    Mathf.Lerp(0f, 360f, Hash01(seed ^ 0x789)));
        }

        private Mesh CreateUnsupportedIslandSprayChipMesh(float size, float depth, int seed)
        {
            var width = size * Mathf.Lerp(0.65f, 1.25f, Hash01(seed ^ 0x12c));
            var height = size * Mathf.Lerp(0.45f, 1.05f, Hash01(seed ^ 0x45f));
            var vertices = new List<Vector3>
            {
                new(-width, -height, 0f),
                new(width * 0.9f, -height * 0.72f, 0f),
                new(width * 0.35f, height, 0f),
                new(-width * 0.72f, height * 0.48f, 0f),
                new(Mathf.Lerp(-0.18f, 0.18f, Hash01(seed ^ 0x82a)) * width, Mathf.Lerp(-0.12f, 0.12f, Hash01(seed ^ 0x91b)) * height, -depth)
            };
            var triangles = new List<int>
            {
                0, 1, 2,
                0, 2, 3,
                0, 4, 1,
                1, 4, 2,
                2, 4, 3,
                3, 4, 0
            };
            return CreateMesh("Wall Material Spray Chip Mesh", vertices, new[] { triangles });
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
            var pointCount = CalculateWallDamageStampPointCount();
            EnsureWallDamageShaderBufferCapacity(
                Mathf.Max(1, stampCount),
                Mathf.Max(1, pointCount));

            var stampBounds = new Vector4[Mathf.Max(1, stampCount)];
            var stampPointOffsets = new int[Mathf.Max(1, stampCount)];
            var stampPointCounts = new int[Mathf.Max(1, stampCount)];
            var stampPoints = new Vector4[Mathf.Max(1, pointCount)];
            var pointOffset = 0;
            for (var stampIndex = 0; stampIndex < stampCount; stampIndex++)
            {
                var stamp = wallDamageStamps[stampIndex];
                stampBounds[stampIndex] = new Vector4(stamp.Min.x, stamp.Min.y, stamp.Max.x, stamp.Max.y);
                stampPointOffsets[stampIndex] = pointOffset;
                var points = stamp.Points;
                var count = points != null ? points.Length : 0;
                stampPointCounts[stampIndex] = count;
                for (var pointIndex = 0; pointIndex < count; pointIndex++)
                {
                    var point = points[pointIndex];
                    stampPoints[pointOffset + pointIndex] = new Vector4(point.x, point.y, 0f, 0f);
                }

                pointOffset += count;
            }

            wallDamageStampBoundsBuffer.SetData(stampBounds);
            wallDamageStampPointOffsetsBuffer.SetData(stampPointOffsets);
            wallDamageStampPointCountsBuffer.SetData(stampPointCounts);
            wallDamageStampPointsBuffer.SetData(stampPoints);
            ApplyWallDamagePropertyBlock(combinedRenderer, stampCount, u, v);
            ApplyWallDamagePropertyBlock(outlineSourceRenderer, stampCount, u, v);
        }

        private int CalculateWallDamageStampPointCount()
        {
            var pointCount = 0;
            for (var i = 0; i < wallDamageStamps.Count; i++)
            {
                pointCount += wallDamageStamps[i]?.Points?.Length ?? 0;
            }

            return pointCount;
        }

        private void EnsureWallDamageShaderBufferCapacity(int stampCapacity, int pointCapacity)
        {
            if (wallDamageStampBoundsBuffer == null || wallDamageStampBoundsCapacity < stampCapacity)
            {
                wallDamageStampBoundsBuffer?.Release();
                wallDamageStampBoundsCapacity = Mathf.Max(1, stampCapacity);
                wallDamageStampBoundsBuffer = new ComputeBuffer(wallDamageStampBoundsCapacity, sizeof(float) * 4);
            }

            if (wallDamageStampPointOffsetsBuffer == null || wallDamageStampPointOffsetsCapacity < stampCapacity)
            {
                wallDamageStampPointOffsetsBuffer?.Release();
                wallDamageStampPointOffsetsCapacity = Mathf.Max(1, stampCapacity);
                wallDamageStampPointOffsetsBuffer = new ComputeBuffer(wallDamageStampPointOffsetsCapacity, sizeof(int));
            }

            if (wallDamageStampPointCountsBuffer == null || wallDamageStampPointCountsCapacity < stampCapacity)
            {
                wallDamageStampPointCountsBuffer?.Release();
                wallDamageStampPointCountsCapacity = Mathf.Max(1, stampCapacity);
                wallDamageStampPointCountsBuffer = new ComputeBuffer(wallDamageStampPointCountsCapacity, sizeof(int));
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
            wallDamagePropertyBlock.SetVector(WallDamageUId, new Vector4(u.x, u.y, u.z, 0f));
            wallDamagePropertyBlock.SetVector(WallDamageVId, new Vector4(v.x, v.y, v.z, 0f));
            wallDamagePropertyBlock.SetBuffer(WallDamageStampBoundsId, wallDamageStampBoundsBuffer);
            wallDamagePropertyBlock.SetBuffer(WallDamageStampPointOffsetsId, wallDamageStampPointOffsetsBuffer);
            wallDamagePropertyBlock.SetBuffer(WallDamageStampPointCountsId, wallDamageStampPointCountsBuffer);
            wallDamagePropertyBlock.SetBuffer(WallDamageStampPointsId, wallDamageStampPointsBuffer);
            renderer.SetPropertyBlock(wallDamagePropertyBlock);
        }

        private void ReleaseWallDamageShaderBuffers()
        {
            wallDamageStampBoundsBuffer?.Release();
            wallDamageStampBoundsBuffer = null;
            wallDamageStampBoundsCapacity = 0;

            wallDamageStampPointOffsetsBuffer?.Release();
            wallDamageStampPointOffsetsBuffer = null;
            wallDamageStampPointOffsetsCapacity = 0;

            wallDamageStampPointCountsBuffer?.Release();
            wallDamageStampPointCountsBuffer = null;
            wallDamageStampPointCountsCapacity = 0;

            wallDamageStampPointsBuffer?.Release();
            wallDamageStampPointsBuffer = null;
            wallDamageStampPointsCapacity = 0;
        }

        private void RebuildContourOwnedWallMesh()
        {
            var vertices = new List<Vector3>();
            var slabTriangles = new List<int>();
            AddContourOwnedWallBodyGeometry(vertices, slabTriangles, 0f);

            var thickness = GetContourOwnedWallContourThickness();
            var visibleSegments = GetVisibleContourOwnedWallSegments(thickness);
            var bridgeSegments = GetContourOwnedWallBridgeSegments(thickness);
            var bridgeVertices = new List<Vector3>();
            var bridgeTriangles = new List<int>();
            if (bridgeSegments.Count > 0)
            {
                AddContourInteriorBridge(bridgeVertices, bridgeTriangles, bridgeSegments);
            }

            combinedMeshFilter.sharedMesh = CreateMesh("Combined Destructible Wall Mesh", vertices, new[] { slabTriangles });
            if (combinedRenderer != null)
            {
                combinedRenderer.sharedMaterials = new[]
                {
                    GetContourClippedWallBodyMaterial()
                };
            }

            EnsureInteriorBridgeObject();
            interiorBridgeMeshFilter.sharedMesh = bridgeVertices.Count == 0
                ? null
                : CreateMesh("Destructible Wall Interior Bridge Mesh", bridgeVertices, new[] { bridgeTriangles });

            RebuildOutlineSourceMesh();
            RebuildContourOwnedWallDamageContourMesh(visibleSegments);
            UploadWallDamageShaderData();
        }

        private void EnsureInteriorBridgeObject()
        {
            if (interiorBridgeMeshFilter != null && interiorBridgeRenderer != null)
            {
                return;
            }

            var bridge = new GameObject("Destructible Wall Interior Bridge");
            bridge.transform.SetParent(transform, false);
            interiorBridgeMeshFilter = bridge.AddComponent<MeshFilter>();
            interiorBridgeRenderer = bridge.AddComponent<MeshRenderer>();
            interiorBridgeRenderer.sharedMaterial = GetUnclippedDestructibleBodyMaterial();
            DroidRenderSetup.ApplyRenderer(interiorBridgeRenderer, StylizedOutlineCategory.None);
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

            var outlineHalfN = halfN + WallOutlineProxyDepthOffset;
            if (wallDamageStamps.Count == 0)
            {
                var outlineBounds = SinkContourOwnedWallOutlineFloorEdge(bounds, u, v);
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
                return;
            }

            AddSurvivingContourOwnedWallOutlineQuads(vertices, triangles, normal, u, v, outlineHalfN, bounds);
        }

        private void AddSurvivingContourOwnedWallOutlineQuads(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 normal,
            Vector3 u,
            Vector3 v,
            float outlineHalfN,
            DamageComponentPlaneBounds bounds)
        {
            var grid = BuildUnsupportedIslandScanGrid(bounds);
            if (grid.Count == 0)
            {
                return;
            }

            var uUp = Vector3.Dot(u, Vector3.up);
            var vUp = Vector3.Dot(v, Vector3.up);
            var uVertical = Mathf.Abs(uUp) > 0.5f;
            var vVertical = Mathf.Abs(vUp) > 0.5f;
            var sink = CalculateWallOutlineFloorSeamSink(uVertical ? bounds.Width : bounds.Height);
            for (var x = 0; x < grid.Columns; x++)
            {
                var columnMinU = bounds.MinU + x * grid.StepU;
                var columnMaxU = bounds.MinU + (x + 1) * grid.StepU;
                var runStart = -1;
                for (var y = 0; y <= grid.Rows; y++)
                {
                    var solid = y < grid.Rows && grid.Solid[grid.Index(x, y)];
                    if (solid && runStart < 0)
                    {
                        runStart = y;
                    }
                    else if (!solid && runStart >= 0)
                    {
                        var quadMinU = columnMinU;
                        var quadMaxU = columnMaxU;
                        var quadMinV = bounds.MinV + runStart * grid.StepV;
                        var quadMaxV = bounds.MinV + y * grid.StepV;
                        if (vVertical)
                        {
                            if (vUp > 0f && runStart == 0)
                            {
                                quadMinV -= sink;
                            }
                            else if (vUp < 0f && y == grid.Rows)
                            {
                                quadMaxV += sink;
                            }
                        }
                        else if (uVertical)
                        {
                            if (uUp > 0f && x == 0)
                            {
                                quadMinU -= sink;
                            }
                            else if (uUp < 0f && x == grid.Columns - 1)
                            {
                                quadMaxU += sink;
                            }
                        }

                        AddContourOwnedWallPlaneQuad(vertices, triangles, normal, u, v, outlineHalfN, quadMinU, quadMaxU, quadMinV, quadMaxV);
                        AddContourOwnedWallPlaneQuad(vertices, triangles, -normal, u, v, outlineHalfN, quadMinU, quadMaxU, quadMinV, quadMaxV);
                        runStart = -1;
                    }
                }
            }
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
            if (wallDamageStamps.Count == 0 ||
                !TryGetContourOwnedWallBasis(0f, out _, out _, out _, out _, out var bounds))
            {
                return new List<ContourSegment2D>();
            }

            return BuildClippedVisibleContourSegments(bounds, thickness, true, false);
        }

        private List<ContourSegment2D> GetContourOwnedWallBridgeSegments(float thickness)
        {
            if (wallDamageStamps.Count == 0 ||
                !TryGetContourOwnedWallBasis(0f, out _, out _, out _, out _, out var bounds))
            {
                return new List<ContourSegment2D>();
            }

            return BuildClippedVisibleContourSegments(bounds, thickness, true, true);
        }

        private List<ContourSegment2D> BuildClippedVisibleContourSegments(
            DamageComponentPlaneBounds bounds,
            float thickness,
            bool removeSmallInteriorIslands,
            bool includeNonContourOwners)
        {
            var result = new List<ContourSegment2D>();
            var rawSegments = BuildStampUnionDamageSegments(wallDamageStamps, thickness, includeNonContourOwners, removeSmallInteriorIslands);
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
            SpawnFallingDebrisObject(mesh, transform.TransformPoint(centerLocal), transform.rotation, CalculatePillarBiteSeed(chunk, 0), Vector3.zero);
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

        private List<ContourSegment2D> BuildStampUnionDamageSegments(List<DamageStamp> stamps, float thickness)
        {
            return BuildStampUnionDamageSegments(stamps, thickness, false, true);
        }

        private List<ContourSegment2D> BuildStampUnionDamageSegments(
            List<DamageStamp> stamps,
            float thickness,
            bool includeNonContourOwners,
            bool removeSmallInteriorIslands)
        {
            var segments = new List<ContourSegment2D>();
            for (var stampIndex = 0; stampIndex < stamps.Count; stampIndex++)
            {
                var stamp = stamps[stampIndex];
                if (stamp == null ||
                    (!includeNonContourOwners && !stamp.RenderContour) ||
                    stamp.Points == null ||
                    stamp.Points.Length < 3)
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

            if (removeSmallInteriorIslands)
            {
                RemoveSmallInteriorContourIslands(segments, stamps, thickness);
            }

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
                if (stamp != null && stamp.RenderContour && stamp.RenderClosed)
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
                if (stamp == null || !stamp.RenderContour || !stamp.RenderClosed)
                {
                    continue;
                }

                var span = stamp.Max - stamp.Min;
                smallestStampSpan = Mathf.Min(smallestStampSpan, Mathf.Max(span.x, span.y));
            }

            return float.IsPositiveInfinity(smallestStampSpan) ? 0f : smallestStampSpan * 0.95f;
        }

        private static void SanitizePolygonLoop(List<Vector2> points)
        {
            for (var i = points.Count - 1; i >= 0; i--)
            {
                var next = (i + 1) % points.Count;
                if ((points[i] - points[next]).sqrMagnitude <= 0.000001f)
                {
                    points.RemoveAt(i);
                }
            }

            var changed = true;
            while (changed && points.Count >= 3)
            {
                changed = false;
                for (var i = points.Count - 1; i >= 0; i--)
                {
                    var previous = points[(i - 1 + points.Count) % points.Count];
                    var current = points[i];
                    var next = points[(i + 1) % points.Count];
                    if (Mathf.Abs(Cross2D(current - previous, next - current)) <= 0.00001f)
                    {
                        points.RemoveAt(i);
                        changed = true;
                    }
                }
            }
        }

        private static void CalculatePolygonBounds(List<Vector2> points, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var i = 0; i < points.Count; i++)
            {
                min = Vector2.Min(min, points[i]);
                max = Vector2.Max(max, points[i]);
            }
        }

        private static void CalculatePolygonBounds(Vector2[] points, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            if (points == null)
            {
                return;
            }

            for (var i = 0; i < points.Length; i++)
            {
                min = Vector2.Min(min, points[i]);
                max = Vector2.Max(max, points[i]);
            }
        }

        private static Vector2 CalculatePointAverage(List<Vector2> points)
        {
            var average = Vector2.zero;
            if (points == null || points.Count == 0)
            {
                return average;
            }

            for (var i = 0; i < points.Count; i++)
            {
                average += points[i];
            }

            return average / points.Count;
        }

        private static float CalculateSignedPolygonArea(List<Vector2> points)
        {
            var twiceArea = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                var next = (i + 1) % points.Count;
                twiceArea += Cross2D(points[i], points[next]);
            }

            return twiceArea * 0.5f;
        }

        private static bool TryTriangulatePolygon(List<Vector2> points, List<int> triangles)
        {
            if (points == null || points.Count < 3 || triangles == null)
            {
                return false;
            }

            var remaining = new List<int>(points.Count);
            for (var i = 0; i < points.Count; i++)
            {
                remaining.Add(i);
            }

            var guard = points.Count * points.Count;
            while (remaining.Count > 3 && guard-- > 0)
            {
                var clippedEar = false;
                for (var i = 0; i < remaining.Count; i++)
                {
                    var previous = remaining[(i - 1 + remaining.Count) % remaining.Count];
                    var current = remaining[i];
                    var next = remaining[(i + 1) % remaining.Count];
                    if (!IsPolygonEar(points, remaining, previous, current, next))
                    {
                        continue;
                    }

                    triangles.Add(previous);
                    triangles.Add(current);
                    triangles.Add(next);
                    remaining.RemoveAt(i);
                    clippedEar = true;
                    break;
                }

                if (!clippedEar)
                {
                    return false;
                }
            }

            if (remaining.Count == 3)
            {
                triangles.Add(remaining[0]);
                triangles.Add(remaining[1]);
                triangles.Add(remaining[2]);
            }

            return triangles.Count >= 3;
        }

        private static bool IsPolygonEar(List<Vector2> points, List<int> remaining, int previous, int current, int next)
        {
            var a = points[previous];
            var b = points[current];
            var c = points[next];
            if (Cross2D(b - a, c - b) <= 0.000001f)
            {
                return false;
            }

            for (var i = 0; i < remaining.Count; i++)
            {
                var index = remaining[i];
                if (index == previous || index == current || index == next)
                {
                    continue;
                }

                if (IsPointInsideTriangle(points[index], a, b, c))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsPointInsideTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            var ab = Cross2D(b - a, point - a);
            var bc = Cross2D(c - b, point - b);
            var ca = Cross2D(a - c, point - c);
            return ab >= -0.000001f && bc >= -0.000001f && ca >= -0.000001f;
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
            group.TotalLength += (segment.End - segment.Start).magnitude;
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

        private static bool IsPointInPolygon(Vector2 point, List<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3)
            {
                return false;
            }

            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
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

    }
}
