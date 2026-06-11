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
        private const float MaxWallDamageShearPerDepth = 1.2f;
        private const int ShearedTunnelBridgeSlices = 4;
        private const float BridgeSliceArtifactMaxSpan = 0.1f;
        private const float MinContourSegmentLength = 0.008f;
        private const float PocketProbePadding = 0.03f;
        private const float PocketAbsorptionMaxSpan = 0.12f;
        private const float PillarSeveranceSliverWidth = 0.1f;
        private const float PillarToppleTipMinDegreesPerSecond = 70f;
        private const float PillarToppleTipMaxDegreesPerSecond = 110f;
        private const float PillarToppleKickSpeed = 0.8f;
        private const float WallDebrisSpawnClearance = 0.02f;
        private const float WallDebrisShotImpulseMin = 3f;
        private const float WallDebrisShotImpulseMax = 5f;
        private const float WallDebrisDislodgeKick = 2.2f;
        private const float DebrisShatterMinImpactSpeed = 4f;
        private const int DebrisShatterMinFragments = 10;
        private const int DebrisShatterMaxFragments = 30;
        private const float DebrisShatterFragmentLifetime = 1.6f;
        private const float DebrisImpactGlowLifetime = 0.9f;
        private static readonly Color DebrisImpactGlowColor = new(1f, 0.42f, 0.08f, 1f);
        private const float PillarBraceProbeDepth = 0.55f;
        private const int MaxPillarRouterColliders = 96;
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
        private static readonly int WallDamageStampShearsId = Shader.PropertyToID("_WallDamageStampShears");
        private static readonly int WallDamageNId = Shader.PropertyToID("_WallDamageN");
        private static readonly int WallDamageU2Id = Shader.PropertyToID("_WallDamageU2");
        private static readonly int WallDamageV2Id = Shader.PropertyToID("_WallDamageV2");
        private static readonly int WallDamageN2Id = Shader.PropertyToID("_WallDamageN2");
        private static readonly Vector3[] PillarBraceProbeDirections =
        {
            Vector3.right,
            Vector3.left,
            Vector3.forward,
            Vector3.back
        };
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
        private ComputeBuffer wallDamageStampShearsBuffer;
        private MaterialPropertyBlock wallDamagePropertyBlock;
        private int wallDamageStampBoundsCapacity;
        private int wallDamageStampPointsCapacity;
        private int wallDamageStampPointOffsetsCapacity;
        private int wallDamageStampPointCountsCapacity;
        private int wallDamageStampShearsCapacity;
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
        private Vector3 forcedWallNormalLocal = Vector3.zero;
        private Vector3 lastDamageHitPointWorld;
        private Vector3 lastDamageShotDirectionWorld;
        private bool hasLastDamageImpulse;
        private bool scanStampsHaveShear;
        private UnsupportedIslandScanGrid cachedScanGrid;
        private int cachedScanGridStampCount = -1;
        private int lastPocketAbsorptionStampCount = -1;
        private int lastSplinterAbsorptionStampCount = -1;
        private DestructibleArenaPiece axisSlabX;
        private DestructibleArenaPiece axisSlabZ;
        private DestructibleArenaPiece structuralFallSibling;
        private DestructibleArenaPiece pillarRouterPiece;
        private bool isPillarRouter;
        private bool pillarAxisSlabMode;
        private bool suppressSurvivingColliders;
        private bool applyingMirroredStructuralFall;
        private GameObject survivingColliderRoot;
        private GameObject pillarColliderRoot;
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
            // Points always describe the hole on the front (+Normal) face. ShearPerDepth slides
            // that cross-section per unit of depth so angled shots carve angled tunnels; UvOffset
            // is the accumulated slide for this stamp's own plane (zero on front-face stamps,
            // shear * thickness on the opposite-face stamp); MidDepthOffset samples the tunnel
            // at half depth for solidity/support tests.
            public Vector2 ShearPerDepth = Vector2.zero;
            public Vector2 UvOffset = Vector2.zero;
            public Vector2 MidDepthOffset = Vector2.zero;
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
            private float duration;
            private float elapsed;
            private Vector3 startScale;

            public void Initialize(float lifetimeSeconds)
            {
                duration = Mathf.Max(0.1f, lifetimeSeconds);
                startScale = transform.localScale;
                elapsed = 0f;
            }

            private void Update()
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var shrink = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, t));
                transform.localScale = startScale * Mathf.Max(0.001f, shrink);
                if (elapsed >= duration)
                {
                    Destroy(gameObject);
                }
            }
        }

        // Shatters a falling slab into small edge-rendered fragments when it lands hard on a
        // floor surface, leaving a brief reddish-orange glow on the floor at the impact point.
        private sealed class FallingSlabImpactShatter : MonoBehaviour
        {
            private Material bodyMaterial;
            private Material rimMaterial;
            private StylizedOutlineCategory outlineCategory;
            private int seed;
            private bool shattered;

            public void Initialize(Material material, Material edgeRimMaterial, StylizedOutlineCategory category, int randomSeed)
            {
                bodyMaterial = material;
                rimMaterial = edgeRimMaterial;
                outlineCategory = category;
                seed = randomSeed;
            }

            private void OnCollisionEnter(Collision collision)
            {
                if (shattered || collision.rigidbody != null || collision.contactCount == 0)
                {
                    return;
                }

                var contact = collision.GetContact(0);
                if (contact.normal.y < 0.65f || !IsFloorCollider(collision.collider))
                {
                    return;
                }

                var impactSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
                if (impactSpeed < DebrisShatterMinImpactSpeed)
                {
                    return;
                }

                shattered = true;
                SpawnShatterFragments(contact.point);
                SpawnFloorImpactGlow(contact.point, collision.collider);
                Destroy(gameObject);
            }

            private static bool IsFloorCollider(Collider collider)
            {
                var piece = collider.GetComponentInParent<DestructibleArenaPiece>();
                if (piece != null)
                {
                    return piece.IsFloorSurface();
                }

                // Static, upward-facing geometry without a destructible piece is ground
                // (terrain, thresholds); walls and pillars are excluded by their parent piece.
                return true;
            }

            private void SpawnShatterFragments(Vector3 impactPoint)
            {
                var slabRenderer = GetComponent<Renderer>();
                var bounds = slabRenderer != null ? slabRenderer.bounds : new Bounds(impactPoint, Vector3.one * 0.4f);
                var size = bounds.size;
                var faceArea = Mathf.Max(
                    0.05f,
                    Mathf.Max(size.x * size.y, Mathf.Max(size.y * size.z, size.x * size.z)));
                var fragmentCount = Mathf.Clamp(
                    Mathf.RoundToInt(faceArea * 26f),
                    DebrisShatterMinFragments,
                    DebrisShatterMaxFragments);
                var body = GetComponent<Rigidbody>();
                var carriedVelocity = body != null ? body.linearVelocity : Vector3.zero;
                var sizeScale = Mathf.Clamp(Mathf.Max(size.x, Mathf.Max(size.y, size.z)), 0.5f, 1.6f);
                for (var i = 0; i < fragmentCount; i++)
                {
                    var fragmentSeed = seed ^ (i * 374761393);

                    // Glass-like shards: small irregular polygons extruded into thin
                    // tapered plates, sizes biased toward tiny slivers.
                    var shardRadius = sizeScale * Mathf.Lerp(
                        0.05f,
                        0.17f,
                        Hash01(fragmentSeed ^ 0x1a3) * Hash01(fragmentSeed ^ 0x2b7));
                    var shardThickness = shardRadius * Mathf.Lerp(0.26f, 0.8f, Hash01(fragmentSeed ^ 0x4d7));
                    var shardMesh = CreateShatterShardMesh(fragmentSeed, shardRadius, shardThickness, out var shardRimMesh);

                    var fragment = new GameObject("Wall Debris Shatter Fragment");
                    var horizontalJitter = new Vector3(
                        Mathf.Lerp(-bounds.extents.x, bounds.extents.x, Hash01(fragmentSeed ^ 0x25b)) * 0.85f,
                        0f,
                        Mathf.Lerp(-bounds.extents.z, bounds.extents.z, Hash01(fragmentSeed ^ 0x69d)) * 0.85f);
                    fragment.transform.position = impactPoint + horizontalJitter +
                        Vector3.up * Mathf.Lerp(0.06f, Mathf.Max(0.16f, size.y * 0.55f), Hash01(fragmentSeed ^ 0x3c5));
                    fragment.transform.rotation = Quaternion.Euler(
                        Mathf.Lerp(0f, 360f, Hash01(fragmentSeed ^ 0x111)),
                        Mathf.Lerp(0f, 360f, Hash01(fragmentSeed ^ 0x222)),
                        Mathf.Lerp(0f, 360f, Hash01(fragmentSeed ^ 0x333)));

                    var fragmentFilter = fragment.AddComponent<MeshFilter>();
                    fragmentFilter.sharedMesh = shardMesh;
                    var fragmentRenderer = fragment.AddComponent<MeshRenderer>();
                    fragmentRenderer.sharedMaterial = bodyMaterial;
                    DroidRenderSetup.ApplyRenderer(fragmentRenderer, outlineCategory);

                    // Every shard carries its own neon edge lines, matching the parent
                    // slab's contour rim at miniature scale.
                    if (shardRimMesh != null && rimMaterial != null)
                    {
                        var rim = new GameObject("Wall Debris Shatter Fragment Rim");
                        rim.transform.SetParent(fragment.transform, false);
                        var rimFilter = rim.AddComponent<MeshFilter>();
                        rimFilter.sharedMesh = shardRimMesh;
                        var rimRenderer = rim.AddComponent<MeshRenderer>();
                        rimRenderer.sharedMaterial = rimMaterial;
                        DroidRenderSetup.ApplyRenderer(rimRenderer, StylizedOutlineCategory.None);
                    }

                    var fragmentCollider = fragment.AddComponent<MeshCollider>();
                    fragmentCollider.sharedMesh = shardMesh;
                    fragmentCollider.convex = true;
                    fragmentCollider.material = GetFallingDebrisPhysicsMaterial();

                    var radial = new Vector3(horizontalJitter.x, 0f, horizontalJitter.z);
                    radial = radial.sqrMagnitude > 0.0001f
                        ? radial.normalized
                        : new Vector3(Mathf.Lerp(-1f, 1f, Hash01(fragmentSeed ^ 0x77)), 0f, Mathf.Lerp(-1f, 1f, Hash01(fragmentSeed ^ 0x99))).normalized;
                    var fragmentBody = fragment.AddComponent<Rigidbody>();
                    fragmentBody.mass = 0.15f;
                    fragmentBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    fragmentBody.linearVelocity = carriedVelocity * 0.2f +
                        radial * Mathf.Lerp(1f, 3.2f, Hash01(fragmentSeed ^ 0x5af)) +
                        Vector3.up * Mathf.Lerp(0.9f, 2.6f, Hash01(fragmentSeed ^ 0x6b1));
                    fragmentBody.angularVelocity = new Vector3(
                        Mathf.Lerp(-12f, 12f, Hash01(fragmentSeed ^ 0x741)),
                        Mathf.Lerp(-12f, 12f, Hash01(fragmentSeed ^ 0x852)),
                        Mathf.Lerp(-12f, 12f, Hash01(fragmentSeed ^ 0x963)));
                    fragment.AddComponent<FallingWallSlabAnimation>().Initialize(
                        DebrisShatterFragmentLifetime * Mathf.Lerp(0.8f, 1.2f, Hash01(fragmentSeed ^ 0xa75)));
                    fragment.AddComponent<ShatterShardMeshCleanup>().Initialize(shardMesh, shardRimMesh);
                }
            }

            private void SpawnFloorImpactGlow(Vector3 impactPoint, Collider floorCollider)
            {
                var slabRenderer = GetComponent<Renderer>();
                var size = slabRenderer != null ? slabRenderer.bounds.size : Vector3.one * 0.4f;
                var fallbackRadius = Mathf.Clamp(Mathf.Max(size.x, size.z) * 0.8f, 0.3f, 1f);
                var floorHexMaterial = ResolveFloorHexMaterial(floorCollider);

                // One continuous layer: a debris-shaped bright core whose baked falloff
                // mask dissolves to nothing at the rim, revealing the floor's red hex
                // pattern inside the lit area.
                var meshFilter = GetComponent<MeshFilter>();
                var footprint = CreateImpactGlowFootprintMesh(
                    meshFilter != null ? meshFilter.sharedMesh : null,
                    transform,
                    impactPoint,
                    out var footprintRadius);
                if (footprint != null)
                {
                    SpawnFloorImpactGlowLayer(impactPoint, 0.02f, footprint, Vector3.one, footprintRadius, floorHexMaterial, true);
                    return;
                }

                SpawnFloorImpactGlowLayer(
                    impactPoint,
                    0.02f,
                    GetImpactGlowMesh(),
                    new Vector3(fallbackRadius, 1f, fallbackRadius),
                    fallbackRadius,
                    floorHexMaterial,
                    false);
            }

            // The glow shader redraws the floor's hex pattern; pulling the size, line
            // width, and origin off the actual floor material keeps the cells aligned.
            private static Material ResolveFloorHexMaterial(Collider floorCollider)
            {
                if (floorCollider == null)
                {
                    return null;
                }

                var renderer = floorCollider.GetComponent<Renderer>();
                if (renderer == null)
                {
                    renderer = floorCollider.GetComponentInParent<Renderer>();
                }

                if (renderer == null)
                {
                    return null;
                }

                var material = renderer.sharedMaterial;
                return material != null && material.HasProperty("_HexSize") && material.HasProperty("_PatternOrigin")
                    ? material
                    : null;
            }

            private static void SpawnFloorImpactGlowLayer(
                Vector3 impactPoint,
                float heightOffset,
                Mesh mesh,
                Vector3 scale,
                float footprintRadius,
                Material floorHexMaterial,
                bool ownsMesh)
            {
                var glow = new GameObject("Wall Debris Impact Glow");
                glow.transform.position = impactPoint + Vector3.up * heightOffset;
                glow.transform.localScale = scale;
                var glowMeshFilter = glow.AddComponent<MeshFilter>();
                glowMeshFilter.sharedMesh = mesh;
                var glowRenderer = glow.AddComponent<MeshRenderer>();
                var glowMaterial = CreateImpactGlowMaterial(floorHexMaterial);
                if (glowMaterial.HasProperty("_ImpactCenter"))
                {
                    glowMaterial.SetVector("_ImpactCenter", new Vector4(impactPoint.x, 0f, impactPoint.z, 0f));
                }

                if (glowMaterial.HasProperty("_FootprintRadius"))
                {
                    glowMaterial.SetFloat("_FootprintRadius", Mathf.Max(0.2f, footprintRadius * Mathf.Max(scale.x, scale.z)));
                }

                glowRenderer.sharedMaterial = glowMaterial;
                DroidRenderSetup.ApplyRenderer(glowRenderer, StylizedOutlineCategory.None);
                glow.AddComponent<FloorImpactGlowAnimation>().Initialize(
                    glowMaterial,
                    DebrisImpactGlowColor,
                    DebrisImpactGlowLifetime,
                    ownsMesh ? mesh : null);
            }
        }

        // Frees the per-shard procedural meshes when a shatter fragment despawns.
        private sealed class ShatterShardMeshCleanup : MonoBehaviour
        {
            private Mesh bodyMesh;
            private Mesh rimMesh;

            public void Initialize(Mesh body, Mesh rim)
            {
                bodyMesh = body;
                rimMesh = rim;
            }

            private void OnDestroy()
            {
                if (bodyMesh != null)
                {
                    Destroy(bodyMesh);
                }

                if (rimMesh != null)
                {
                    Destroy(rimMesh);
                }
            }
        }

        private sealed class FloorImpactGlowAnimation : MonoBehaviour
        {
            private Material material;
            private Mesh ownedMesh;
            private Color baseColor;
            private Vector3 startScale;
            private float duration;
            private float elapsed;
            private bool usesGlowShader;

            public void Initialize(Material glowMaterial, Color color, float lifetimeSeconds, Mesh meshToDestroy)
            {
                material = glowMaterial;
                ownedMesh = meshToDestroy;
                baseColor = color;
                duration = Mathf.Max(0.1f, lifetimeSeconds);
                startScale = transform.localScale;
                elapsed = 0f;
                usesGlowShader = glowMaterial != null && glowMaterial.HasProperty("_Intensity");
            }

            private void Update()
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                // Flash on fast, spread slightly, then fade out — additive blending means
                // zero intensity dissolves completely into the floor.
                var pop = t < 0.1f ? Mathf.SmoothStep(0f, 1f, t / 0.1f) : 1f;
                var fade = Mathf.Pow(1f - t, 1.7f);
                var spread = Mathf.Lerp(1f, 1.16f, Mathf.SmoothStep(0f, 1f, t));
                transform.localScale = new Vector3(startScale.x * pop * spread, startScale.y, startScale.z * pop * spread);
                if (material != null)
                {
                    if (usesGlowShader)
                    {
                        material.SetFloat("_Intensity", pop * fade);
                        material.SetFloat(
                            "_RippleProgress",
                            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.6f, t)));
                    }
                    else
                    {
                        SetMaterialColor(material, baseColor * (pop * fade));
                    }
                }

                if (elapsed >= duration)
                {
                    Destroy(gameObject);
                }
            }

            private void OnDestroy()
            {
                if (material != null)
                {
                    Destroy(material);
                }

                if (ownedMesh != null)
                {
                    Destroy(ownedMesh);
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
            cachedScanGrid = null;
            cachedScanGridStampCount = -1;
            lastPocketAbsorptionStampCount = -1;
            lastSplinterAbsorptionStampCount = -1;
            if (!initialized)
            {
                chunks.Clear();
                chunksByIndex.Clear();
                chunkGridBuilt = false;
            }

            configuredBiteDirectionLocal = biteDirectionWorld.sqrMagnitude > 0.0001f
                ? transform.InverseTransformDirection(biteDirectionWorld.normalized)
                : Vector3.zero;

            if (profile == DestructibleDamageProfile.CornerPillar && UsesStructuralWallOutlineSource())
            {
                InitializePillarRouter();
                return;
            }

            if (UsesContourOwnedWallDamage() || ShouldInitializeStartupStructuralWallBody())
            {
                InitializeChunks();
            }
        }

        private void InitializePillarRouter()
        {
            if (isPillarRouter)
            {
                return;
            }

            isPillarRouter = true;
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

            axisSlabX = CreatePillarAxisSlab("Pillar Axis X Damage", sourceSize, Vector3.right);
            axisSlabZ = CreatePillarAxisSlab("Pillar Axis Z Damage", sourceSize, Vector3.forward);
            axisSlabX.structuralFallSibling = axisSlabZ;
            axisSlabZ.structuralFallSibling = axisSlabX;
        }

        private DestructibleArenaPiece CreatePillarAxisSlab(string slabName, Vector3 sourceSize, Vector3 wallNormalLocal)
        {
            var slab = new GameObject(slabName);
            slab.transform.SetParent(transform, false);
            var piece = slab.AddComponent<DestructibleArenaPiece>();
            piece.forcedWallNormalLocal = wallNormalLocal;
            piece.suppressSurvivingColliders = true;
            piece.pillarAxisSlabMode = true;
            piece.pillarRouterPiece = this;
            piece.Configure(maxHealth, sourceSize, intactMaterial, configuredOutlineCategory, DestructibleDamageProfile.Wall, Vector3.zero);
            var slabCollider = slab.GetComponent<BoxCollider>();
            if (slabCollider != null)
            {
                slabCollider.enabled = false;
            }

            return piece;
        }

        private DestructibleArenaPiece ResolvePillarAxisSlab(Vector3 hitNormalWorld)
        {
            var localNormal = transform.InverseTransformDirection(
                hitNormalWorld.sqrMagnitude > 0.001f ? hitNormalWorld.normalized : Vector3.forward);
            return Mathf.Abs(localNormal.x) >= Mathf.Abs(localNormal.z) ? axisSlabX : axisSlabZ;
        }

        private void MirrorStructuralFallToSibling(UnsupportedWallIsland island, Vector3 u, Vector3 v)
        {
            if (applyingMirroredStructuralFall ||
                structuralFallSibling == null ||
                island == null ||
                island.Points == null ||
                island.Points.Count < 3)
            {
                return;
            }

            // The sibling models the perpendicular axis; a mirrored band wipes its full width at
            // these heights. That is only physical when the falling section spans this slab's
            // whole horizontal extent (a severed cross-section). Partial chips keep the sibling
            // axis intact.
            if (TryGetContourOwnedWallBasis(0f, out _, out _, out _, out _, out var bounds) &&
                TryGetWallUvUpAxis(u, v, out var upIsU, out _))
            {
                var horizontalExtent = upIsU ? island.Max.y - island.Min.y : island.Max.x - island.Min.x;
                var horizontalSpan = upIsU ? bounds.Height : bounds.Width;
                if (horizontalExtent < horizontalSpan - Mathf.Max(PillarSeveranceSliverWidth, horizontalSpan * 0.18f))
                {
                    return;
                }
            }

            var minY = float.PositiveInfinity;
            var maxY = float.NegativeInfinity;
            for (var i = 0; i < island.Points.Count; i++)
            {
                var local = u * island.Points[i].x + v * island.Points[i].y;
                minY = Mathf.Min(minY, local.y);
                maxY = Mathf.Max(maxY, local.y);
            }

            if (maxY - minY <= 0.01f)
            {
                return;
            }

            structuralFallSibling.RemoveFullWidthBand(minY, maxY);
        }

        private void RemoveFullWidthBand(float minLocalY, float maxLocalY)
        {
            if (!UsesContourOwnedWallDamage() ||
                !TryGetContourOwnedWallBasis(0f, out var normal, out var u, out var v, out var halfN, out var bounds))
            {
                return;
            }

            var uIsVertical = Mathf.Abs(Vector3.Dot(u, Vector3.up)) > 0.5f;
            var minU = uIsVertical ? minLocalY : bounds.MinU - 0.05f;
            var maxU = uIsVertical ? maxLocalY : bounds.MaxU + 0.05f;
            var minV = uIsVertical ? bounds.MinV - 0.05f : minLocalY;
            var maxV = uIsVertical ? bounds.MaxV + 0.05f : maxLocalY;
            var points = new List<Vector2>
            {
                new(minU, minV),
                new(maxU, minV),
                new(maxU, maxV),
                new(minU, maxV)
            };
            wallDamageStamps.Add(CreateUnsupportedWallIslandCleanupStamp(normal, u, v, halfN, points));
            applyingMirroredStructuralFall = true;
            RemoveUnsupportedContourOwnedWallIslands();
            applyingMirroredStructuralFall = false;
            RebuildCombinedMesh();
        }

        private Vector3 ResolvePillarSlabToppleDirectionWorld()
        {
            if (pillarRouterPiece != null)
            {
                return pillarRouterPiece.ResolvePillarToppleDirectionWorld();
            }

            var fallback = transform.TransformDirection(GetWallNormalLocal());
            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
        }

        private Vector3 ResolvePillarToppleDirectionWorld()
        {
            // Walls braced against a face keep the pillar from falling that way; probe each
            // horizontal face for adjacent static geometry and topple toward the open sides.
            var size = GetSourceSize();
            var free = Vector3.zero;
            foreach (var localDirection in PillarBraceProbeDirections)
            {
                var worldDirection = transform.TransformDirection(localDirection);
                var faceHalfExtent = Vector3.Dot(AbsVector(size), AbsVector(localDirection)) * 0.5f;
                var probeCenter = transform.position + worldDirection * (faceHalfExtent + PillarBraceProbeDepth * 0.5f);
                var probeHalfExtents = new Vector3(0.24f, Mathf.Max(0.3f, size.y * 0.25f), 0.24f);
                var braced = false;
                foreach (var candidate in Physics.OverlapBox(probeCenter, probeHalfExtents, transform.rotation))
                {
                    if (candidate == null ||
                        candidate.isTrigger ||
                        candidate.attachedRigidbody != null ||
                        candidate.transform.IsChildOf(transform))
                    {
                        continue;
                    }

                    braced = true;
                    break;
                }

                if (!braced)
                {
                    free += worldDirection;
                }
            }

            free.y = 0f;
            if (free.sqrMagnitude > 0.0001f)
            {
                return free.normalized;
            }

            var bite = configuredBiteDirectionLocal.sqrMagnitude > 0.0001f
                ? transform.TransformDirection(configuredBiteDirectionLocal)
                : transform.forward;
            bite.y = 0f;
            return bite.sqrMagnitude > 0.0001f ? bite.normalized : Vector3.forward;
        }

        private void RebuildPillarRouterColliders()
        {
            if (!isPillarRouter || axisSlabX == null || axisSlabZ == null || surfaceCollider == null)
            {
                return;
            }

            var rectsX = axisSlabX.CollectSurvivingColliderRects();
            var rectsZ = axisSlabZ.CollectSurvivingColliderRects();
            if (rectsX == null && rectsZ == null)
            {
                return;
            }

            // Slab X rects are (minY, minZ, maxY, maxZ); slab Z rects are (minX, minY, maxX, maxY).
            // Surviving pillar material is the intersection of both prisms.
            var size = GetSourceSize();
            rectsX ??= new List<Vector4> { new(-size.y * 0.5f, -size.z * 0.5f, size.y * 0.5f, size.z * 0.5f) };
            rectsZ ??= new List<Vector4> { new(-size.x * 0.5f, -size.y * 0.5f, size.x * 0.5f, size.y * 0.5f) };

            if (pillarColliderRoot == null)
            {
                pillarColliderRoot = new GameObject("Destructible Pillar Colliders");
                pillarColliderRoot.transform.SetParent(transform, false);
            }

            var previousColliders = pillarColliderRoot.GetComponents<BoxCollider>();
            for (var i = 0; i < previousColliders.Length; i++)
            {
                if (Application.isPlaying)
                {
                    Destroy(previousColliders[i]);
                }
                else
                {
                    DestroyImmediate(previousColliders[i]);
                }
            }

            surfaceCollider.enabled = false;
            var emitted = 0;
            foreach (var rectX in rectsX)
            {
                foreach (var rectZ in rectsZ)
                {
                    var minY = Mathf.Max(rectX.x, rectZ.y);
                    var maxY = Mathf.Min(rectX.z, rectZ.w);
                    if (maxY - minY <= 0.005f)
                    {
                        continue;
                    }

                    if (emitted >= MaxPillarRouterColliders)
                    {
                        return;
                    }

                    var box = pillarColliderRoot.AddComponent<BoxCollider>();
                    box.center = new Vector3((rectZ.x + rectZ.z) * 0.5f, (minY + maxY) * 0.5f, (rectX.y + rectX.w) * 0.5f);
                    box.size = new Vector3(rectZ.z - rectZ.x, maxY - minY, rectX.w - rectX.y);
                    emitted++;
                }
            }
        }

        private void RemoveSeveredPillarSections(Vector3 hitNormalWorld)
        {
            if (!UsesContourOwnedWallDamage() ||
                wallDamageStamps.Count == 0 ||
                !TryGetContourOwnedWallBasis(0f, out var normal, out var u, out var v, out var halfN, out var bounds) ||
                !TryGetWallUvUpAxis(u, v, out var upIsU, out var upSign))
            {
                return;
            }

            var grid = BuildUnsupportedIslandScanGrid(bounds);
            if (grid.Count == 0)
            {
                return;
            }

            // A pillar section only falls when a height band is cut through the slab's full
            // width (slivers thinner than the limit cannot carry the load). Everything above
            // the lowest severed band topples away from the bracing walls as one piece.
            var horizontalCount = CantileverHorizontalCount(grid, upIsU);
            var verticalCount = CantileverVerticalCount(grid, upIsU);
            var horizontalStep = CantileverHorizontalStep(grid, upIsU);
            var sliverLimit = Mathf.Max(PillarSeveranceSliverWidth, horizontalStep * 2f);
            var severedStep = -1;
            for (var step = 0; step < verticalCount; step++)
            {
                var w = upSign > 0f ? step : verticalCount - 1 - step;
                var solidWidth = 0f;
                for (var h = 0; h < horizontalCount; h++)
                {
                    if (grid.Solid[CantileverCellIndex(grid, upIsU, h, w)])
                    {
                        solidWidth += horizontalStep;
                    }
                }

                if (solidWidth <= sliverLimit)
                {
                    severedStep = step;
                    break;
                }
            }

            if (severedStep < 0)
            {
                return;
            }

            var cells = new List<int>();
            var mask = new bool[grid.Count];
            for (var step = severedStep; step < verticalCount; step++)
            {
                var w = upSign > 0f ? step : verticalCount - 1 - step;
                for (var h = 0; h < horizontalCount; h++)
                {
                    var index = CantileverCellIndex(grid, upIsU, h, w);
                    if (grid.Solid[index])
                    {
                        cells.Add(index);
                        mask[index] = true;
                    }
                }
            }

            if (cells.Count == 0 || !TryCreateUnsupportedIslandFromCells(grid, cells, mask, out var island))
            {
                return;
            }

            AddUnsupportedWallIslandCleanupStamp(island, normal, u, v, halfN);
            var slabBudget = applyingMirroredStructuralFall ? 0 : 1;
            SpawnFallingWallSlab(island, normal, u, v, halfN, ref slabBudget, ResolvePillarSlabToppleDirectionWorld());
            MirrorStructuralFallToSibling(island, u, v);
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
            TakeDamage(amount, hitPoint, hitNormal, hitCollider, Vector3.zero);
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, Collider hitCollider, Vector3 shotDirectionWorld)
        {
            if (amount <= 0f)
            {
                return;
            }

            if (isPillarRouter && axisSlabX != null && axisSlabZ != null)
            {
                ResolvePillarAxisSlab(hitNormal).TakeDamage(amount, hitPoint, hitNormal, hitCollider, shotDirectionWorld);
                return;
            }

            InitializeChunks();
            if (UsesContourOwnedWallDamage())
            {
                // Remember the shot so debris freed by this hit inherits its energy as a real
                // impulse instead of a synthetic kick.
                lastDamageHitPointWorld = hitPoint;
                lastDamageShotDirectionWorld = shotDirectionWorld.sqrMagnitude > 0.0001f
                    ? shotDirectionWorld.normalized
                    : Vector3.zero;
                hasLastDamageImpulse = lastDamageShotDirectionWorld.sqrMagnitude > 0.5f;
                var stamp = AddContourOwnedWallDamage(hitPoint, shotDirectionWorld);
                SpawnContourOwnedWallHitSpray(stamp, hitNormal, shotDirectionWorld);
                RemoveUnsupportedContourOwnedWallIslands(hitNormal);
                RebuildCombinedMesh();
                hasLastDamageImpulse = false;
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

            if (isPillarRouter && axisSlabX != null && axisSlabZ != null)
            {
                // Material is gone wherever either axis slab carved its prism through the box.
                return axisSlabX.AllowsProjectilePassThrough(hitPoint, hitNormal) ||
                    axisSlabZ.AllowsProjectilePassThrough(hitPoint, hitNormal);
            }

            if (UsesContourOwnedWallDamage())
            {
                return IsLocalPointInsideWallDamageUnion(transform.InverseTransformPoint(hitPoint));
            }

            var chunk = FindHitChunk(hitPoint);
            return chunk == null || chunk.Destroyed;
        }

        public bool IsFloorSurface()
        {
            return damageProfile == DestructibleDamageProfile.Floor || configuredOutlineCategory == StylizedOutlineCategory.Floor;
        }

        // A shot that enters through an existing hole can still strike surviving material
        // deeper inside the piece (the wall of an angled tunnel, the far side of a corner
        // bite). Colliders are full-thickness approximations, so march the exact stamp union
        // along the ray to find the first real material point.
        public bool TryGetMaterialHitAlongRay(Vector3 origin, Vector3 direction, out Vector3 materialPoint, out Vector3 materialNormal)
        {
            materialPoint = default;
            materialNormal = default;
            if (!initialized || direction.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            if (!isPillarRouter && !UsesContourOwnedWallDamage())
            {
                return false;
            }

            var localOrigin = transform.InverseTransformPoint(origin);
            var localDirection = transform.InverseTransformDirection(direction.normalized);
            var half = GetSourceSize() * 0.5f;
            if (!TryIntersectLocalBox(localOrigin, localDirection, half, out var entryT, out var exitT))
            {
                return false;
            }

            const float stepSize = 0.025f;
            var t = Mathf.Max(entryT, 0f) + stepSize * 0.5f;
            for (var step = 0; step < 96 && t < exitT; step++, t += stepSize)
            {
                var sample = localOrigin + localDirection * t;
                if (HasMaterialAtLocalPoint(sample))
                {
                    materialPoint = transform.TransformPoint(sample);
                    materialNormal = -direction.normalized;
                    return true;
                }
            }

            return false;
        }

        private bool HasMaterialAtLocalPoint(Vector3 localPoint)
        {
            if (isPillarRouter)
            {
                return axisSlabX != null &&
                    axisSlabZ != null &&
                    !axisSlabX.IsLocalPointInsideWallDamageUnion(localPoint) &&
                    !axisSlabZ.IsLocalPointInsideWallDamageUnion(localPoint);
            }

            return !IsLocalPointInsideWallDamageUnion(localPoint);
        }

        private static bool TryIntersectLocalBox(Vector3 origin, Vector3 direction, Vector3 half, out float entryT, out float exitT)
        {
            entryT = float.NegativeInfinity;
            exitT = float.PositiveInfinity;
            for (var axis = 0; axis < 3; axis++)
            {
                var component = direction[axis];
                if (Mathf.Abs(component) <= 0.000001f)
                {
                    if (Mathf.Abs(origin[axis]) > half[axis])
                    {
                        return false;
                    }

                    continue;
                }

                var inverse = 1f / component;
                var tA = (-half[axis] - origin[axis]) * inverse;
                var tB = (half[axis] - origin[axis]) * inverse;
                entryT = Mathf.Max(entryT, Mathf.Min(tA, tB));
                exitT = Mathf.Min(exitT, Mathf.Max(tA, tB));
            }

            return exitT > entryT && exitT > 0f;
        }

        public bool TryResolvePlayerVault(Vector3 playerPosition, Vector3 playerForward, float playerRadius, float playerHeight, out PlayerVaultSolution solution)
        {
            solution = default;
            if (isPillarRouter)
            {
                if (axisSlabX != null && axisSlabX.TryResolvePlayerVault(playerPosition, playerForward, playerRadius, playerHeight, out solution))
                {
                    return true;
                }

                return axisSlabZ != null && axisSlabZ.TryResolvePlayerVault(playerPosition, playerForward, playerRadius, playerHeight, out solution);
            }

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
            if (forcedWallNormalLocal.sqrMagnitude > 0.5f)
            {
                return forcedWallNormalLocal;
            }

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

        private DamageStamp AddContourOwnedWallDamage(Vector3 hitPoint, Vector3 shotDirectionWorld)
        {
            if (!TryGetContourOwnedWallBasis(0f, out var normal, out var u, out var v, out var halfN, out var bounds))
            {
                return null;
            }

            var localHit = transform.InverseTransformPoint(hitPoint);
            var shearPerDepth = CalculateWallDamageShear(shotDirectionWorld, normal, u, v);
            // Project the hit back along the shot direction onto the front (+normal) face so the
            // tunnel passes through the actual impact point regardless of which face was struck.
            var depthAtHit = halfN - Vector3.Dot(localHit, normal);
            var center = new Vector2(Vector3.Dot(localHit, u), Vector3.Dot(localHit, v)) - shearPerDepth * depthAtHit;
            var halfU = Mathf.Min(
                Mathf.Max(0.12f, bounds.Width / Mathf.Max(1, CalculateCellCount(bounds.Width)) * 0.5f),
                bounds.Width * 0.21f);
            var halfV = Mathf.Min(
                Mathf.Max(0.12f, bounds.Height / Mathf.Max(1, CalculateCellCount(bounds.Height)) * 0.5f),
                bounds.Height * 0.21f);
            var stampRadius = Mathf.Min(halfU, halfV);
            halfU = stampRadius;
            halfV = stampRadius;
            // A grazing shot back-projected across the thickness can land the center past the
            // face bounds, leaving only a sliver of the polygon on the piece; pull it back so a
            // real hole is carved. Pillar slabs keep a small inset so most of the hole stays on
            // the struck face (overflow is handled by the sibling via dual-basis clipping).
            var centerInset = pillarAxisSlabMode ? stampRadius * 0.6f : 0f;
            center.x = ClampToCenteredRange(center.x, bounds.MinU + centerInset, bounds.MaxU - centerInset);
            center.y = ClampToCenteredRange(center.y, bounds.MinV + centerInset, bounds.MaxV - centerInset);
            var stamp = CreateContourOwnedWallDamageStamp(
                center,
                normal,
                u,
                v,
                halfN,
                halfU * 1.08f,
                halfV * 1.08f,
                CalculateContourOwnedWallDamageSeed(center),
                shearPerDepth,
                bounds);
            wallDamageStamps.Add(stamp);
            return stamp;
        }

        private static float ClampToCenteredRange(float value, float min, float max)
        {
            return min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
        }

        private Vector2 CalculateWallDamageShear(Vector3 shotDirectionWorld, Vector3 normal, Vector3 u, Vector3 v)
        {
            if (shotDirectionWorld.sqrMagnitude <= 0.0001f)
            {
                return Vector2.zero;
            }

            var local = transform.InverseTransformDirection(shotDirectionWorld.normalized);
            var along = Vector3.Dot(local, normal);
            if (Mathf.Abs(along) <= 0.05f)
            {
                return Vector2.zero;
            }

            if (along > 0f)
            {
                // Normalize the travel direction to run front (+normal) face to back face.
                local = -local;
                along = -along;
            }

            var shear = new Vector2(Vector3.Dot(local, u), Vector3.Dot(local, v)) / -along;
            shear.x = Mathf.Clamp(shear.x, -MaxWallDamageShearPerDepth, MaxWallDamageShearPerDepth);
            shear.y = Mathf.Clamp(shear.y, -MaxWallDamageShearPerDepth, MaxWallDamageShearPerDepth);
            return shear;
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

            var sprayDirectionWorld = hasLastDamageImpulse
                ? lastDamageShotDirectionWorld
                : hitNormalWorld.sqrMagnitude > 0.0001f
                    ? -hitNormalWorld.normalized
                    : transform.TransformDirection(-normal);
            AbsorbEnclosedDamagePockets(normal, u, v, halfN);
            AbsorbDetachedDepthSplinters(normal, u, v, halfN, bounds);
            // A mirrored structural fall only consumes material the originating slab already
            // turned into debris — spawning again here would duplicate it.
            var sprayBudget = applyingMirroredStructuralFall ? 0 : MaxUnsupportedIslandSpraysPerDamage;
            var slabBudget = applyingMirroredStructuralFall ? 0 : MaxFallingSlabsPerDamage;
            RunUnsupportedIslandCleanupPasses(normal, u, v, halfN, bounds, sprayDirectionWorld, ref sprayBudget, ref slabBudget);
            if (pillarAxisSlabMode)
            {
                // The cantilever/pedestal heuristics are tuned for wide walls and misfire on a
                // slender braced pillar; pillars only shed their top once a band is cut through.
                RemoveSeveredPillarSections(hitNormalWorld);
            }
            else
            {
                RemoveCantileverCollapsedWallSections(hitNormalWorld);
                RemovePedestalCrushedWallSections(hitNormalWorld);
            }

            RunUnsupportedIslandCleanupPasses(normal, u, v, halfN, bounds, sprayDirectionWorld, ref sprayBudget, ref slabBudget);
        }

        // Two overlapping jagged hole polygons can enclose slivers of real material that are
        // thinner than the island scan resolution: the shader keeps drawing them but no
        // structural pass can ever remove them. Detect those pockets exactly on the contour
        // union and stamp them out so the material truly disappears everywhere (rendering,
        // colliders, pass-through).
        private void AbsorbEnclosedDamagePockets(Vector3 normal, Vector3 u, Vector3 v, float halfN)
        {
            if (wallDamageStamps.Count < 2 || wallDamageStamps.Count == lastPocketAbsorptionStampCount)
            {
                return;
            }

            // Sheared tunnels can enclose pockets that exist only deeper in the material, so
            // the contour analysis runs at the front, middle and back depth when needed.
            var depthSteps = CalculateWallDamageStampsHaveShear() ? 2 : 0;
            List<DamageStamp> absorbed = null;
            for (var step = 0; step <= depthSteps; step++)
            {
                AbsorbEnclosedDamagePocketsAtDepth(normal, u, v, halfN, step, ref absorbed);
            }

            if (absorbed != null)
            {
                wallDamageStamps.AddRange(absorbed);
            }

            lastPocketAbsorptionStampCount = wallDamageStamps.Count;
        }

        private void AbsorbEnclosedDamagePocketsAtDepth(
            Vector3 normal,
            Vector3 u,
            Vector3 v,
            float halfN,
            int halfDepthSteps,
            ref List<DamageStamp> absorbed)
        {
            var thickness = GetContourOwnedWallContourThickness();
            var segments = BuildStampUnionDamageSegments(wallDamageStamps, thickness, true, false, halfDepthSteps * halfN);
            if (segments.Count < 4)
            {
                return;
            }

            var groups = BuildContourSegmentGroups(segments, Mathf.Max(0.002f, thickness * 0.35f));
            var spanLimit = Mathf.Min(PocketAbsorptionMaxSpan, CalculateSmallInteriorIslandSpanLimit(wallDamageStamps));
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                var span = group.Span;
                if (Mathf.Max(span.x, span.y) > spanLimit)
                {
                    continue;
                }

                // A pocket is a small contour cluster whose interior is material while its
                // immediate surroundings are hole. Clip-fragmented loops rarely register as
                // "closed", so probe geometry directly instead of relying on loop topology.
                var centroid = group.Centroid;
                if (IsPointInsideWallDamageUnionAtHalfDepthSteps(centroid, halfDepthSteps))
                {
                    continue;
                }

                var probeMin = group.Min - Vector2.one * PocketProbePadding;
                var probeMax = group.Max + Vector2.one * PocketProbePadding;
                if (!IsPointInsideWallDamageUnionAtHalfDepthSteps(new Vector2(probeMin.x, probeMin.y), halfDepthSteps) ||
                    !IsPointInsideWallDamageUnionAtHalfDepthSteps(new Vector2(probeMax.x, probeMin.y), halfDepthSteps) ||
                    !IsPointInsideWallDamageUnionAtHalfDepthSteps(new Vector2(probeMax.x, probeMax.y), halfDepthSteps) ||
                    !IsPointInsideWallDamageUnionAtHalfDepthSteps(new Vector2(probeMin.x, probeMax.y), halfDepthSteps))
                {
                    continue;
                }

                absorbed ??= new List<DamageStamp>();
                absorbed.Add(CreateDamagePocketAbsorptionStamp(group, segments, normal, u, v, halfN, halfDepthSteps * halfN));
            }
        }

        // Two tunnels carved at different angles can leave thin partial-depth wedges floating
        // inside the merged volume. Their (u,v) columns hold material at *some* depth, so the
        // 2D support model thinks they are attached. A column that is nowhere full-thickness
        // and whose cluster touches no full-thickness column is a detached shaving — remove it.
        private void AbsorbDetachedDepthSplinters(Vector3 normal, Vector3 u, Vector3 v, float halfN, DamageComponentPlaneBounds bounds)
        {
            if (wallDamageStamps.Count < 2 ||
                wallDamageStamps.Count == lastSplinterAbsorptionStampCount ||
                !CalculateWallDamageStampsHaveShear())
            {
                return;
            }

            lastSplinterAbsorptionStampCount = wallDamageStamps.Count;
            var grid = BuildUnsupportedIslandScanGrid(bounds);
            if (grid.Count == 0)
            {
                return;
            }

            var fullThickness = new bool[grid.Count];
            for (var y = 0; y < grid.Rows; y++)
            {
                for (var x = 0; x < grid.Columns; x++)
                {
                    var index = grid.Index(x, y);
                    if (!grid.Solid[index])
                    {
                        continue;
                    }

                    var center = grid.CellCenter(x, y);
                    fullThickness[index] =
                        !IsPointInsideWallDamageUnionAtHalfDepthSteps(center, 0) &&
                        !IsPointInsideWallDamageUnionAtHalfDepthSteps(center, 1) &&
                        !IsPointInsideWallDamageUnionAtHalfDepthSteps(center, 2);
                }
            }

            var visited = new bool[grid.Count];
            var cluster = new List<int>();
            var queue = new Queue<int>();
            List<DamageStamp> absorbed = null;
            for (var index = 0; index < grid.Count; index++)
            {
                if (!grid.Solid[index] || fullThickness[index] || visited[index])
                {
                    continue;
                }

                cluster.Clear();
                var touchesFullThickness = false;
                visited[index] = true;
                queue.Enqueue(index);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    cluster.Add(current);
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
                        if (!grid.Solid[neighbor])
                        {
                            continue;
                        }

                        if (fullThickness[neighbor])
                        {
                            touchesFullThickness = true;
                            continue;
                        }

                        if (!visited[neighbor])
                        {
                            visited[neighbor] = true;
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                if (touchesFullThickness || cluster.Count == 0)
                {
                    continue;
                }

                var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                for (var i = 0; i < cluster.Count; i++)
                {
                    var center = grid.CellCenter(grid.CellX(cluster[i]), grid.CellY(cluster[i]));
                    min = Vector2.Min(min, center);
                    max = Vector2.Max(max, center);
                }

                min -= new Vector2(grid.StepU, grid.StepV) * 0.5f + Vector2.one * UnsupportedIslandCleanupPadding;
                max += new Vector2(grid.StepU, grid.StepV) * 0.5f + Vector2.one * UnsupportedIslandCleanupPadding;
                var splinterStamp = new DamageStamp
                {
                    Normal = normal,
                    U = u,
                    V = v,
                    Plane = halfN + DamageContourInset,
                    Min = min,
                    Max = max,
                    Points = new[]
                    {
                        new Vector2(min.x, min.y),
                        new Vector2(max.x, min.y),
                        new Vector2(max.x, max.y),
                        new Vector2(min.x, max.y)
                    },
                    RenderContour = false
                };
                splinterStamp.Opposite = CreateOppositeContourOwnedWallStamp(splinterStamp, normal, halfN);
                splinterStamp.Opposite.RenderContour = false;
                absorbed ??= new List<DamageStamp>();
                absorbed.Add(splinterStamp);
            }

            if (absorbed != null)
            {
                wallDamageStamps.AddRange(absorbed);
                lastSplinterAbsorptionStampCount = wallDamageStamps.Count;
            }
        }

        private static DamageStamp CreateDamagePocketAbsorptionStamp(
            ContourSegmentGroup group,
            List<ContourSegment2D> segments,
            Vector3 normal,
            Vector3 u,
            Vector3 v,
            float halfN,
            float groupDepth)
        {
            // Track the local tunnel direction so the absorbed pocket follows its neighbors
            // through the thickness instead of punching a stray straight hole.
            var shear = Vector2.zero;
            var shearSamples = 0;
            foreach (var segmentIndex in group.SegmentIndexes)
            {
                var stamp = segments[segmentIndex].Stamp;
                if (stamp == null)
                {
                    continue;
                }

                shear += stamp.ShearPerDepth;
                shearSamples++;
            }

            if (shearSamples > 0)
            {
                shear /= shearSamples;
            }

            // Group bounds were measured in the world UV of their depth; shift them back to
            // the front plane where stamp polygons live.
            var frontOffset = shear * groupDepth;
            var min = group.Min - frontOffset - Vector2.one * UnsupportedIslandCleanupPadding;
            var max = group.Max - frontOffset + Vector2.one * UnsupportedIslandCleanupPadding;
            var pocketStamp = new DamageStamp
            {
                Normal = normal,
                U = u,
                V = v,
                Plane = halfN + DamageContourInset,
                Min = min,
                Max = max,
                Points = new[]
                {
                    new Vector2(min.x, min.y),
                    new Vector2(max.x, min.y),
                    new Vector2(max.x, max.y),
                    new Vector2(min.x, max.y)
                },
                RenderContour = false,
                ShearPerDepth = shear,
                MidDepthOffset = shear * halfN
            };
            pocketStamp.Opposite = CreateOppositeContourOwnedWallStamp(pocketStamp, normal, halfN);
            pocketStamp.Opposite.RenderContour = false;
            return pocketStamp;
        }

        private void RunUnsupportedIslandCleanupPasses(
            Vector3 normal,
            Vector3 u,
            Vector3 v,
            float halfN,
            DamageComponentPlaneBounds bounds,
            Vector3 sprayDirectionWorld,
            ref int sprayBudget,
            ref int slabBudget)
        {
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
                        MirrorStructuralFallToSibling(island, u, v);
                        removedAnyIsland = true;
                        continue;
                    }

                    if (applyingMirroredStructuralFall)
                    {
                        // Mirrored cleanup suppresses effects, but the material must still be
                        // stamped out — skipping left orphan shards that no later pass could
                        // reach until this exact piece was shot again.
                        AddUnsupportedWallIslandCleanupStamp(island, normal, u, v, halfN);
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
                        MirrorStructuralFallToSibling(island, u, v);
                        removedAnyIsland = true;
                    }
                }

                if (!removedAnyIsland)
                {
                    break;
                }
            }
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
                    MirrorStructuralFallToSibling(island, u, v);
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
                        MirrorStructuralFallToSibling(island, u, v);
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

            var coreLabels = LabelSupportCoreComponents(grid, CalculateTopPerimeterSupportExclusionMask(), out var coreSupported);
            var ownerLabels = AssignSolidCellsToSupportCores(grid, coreLabels);
            var unsupportedCells = BuildUnsupportedSolidMask(grid, ownerLabels, coreSupported);
            AddUnsupportedIslandComponents(grid, unsupportedCells, islands);
            AddOpenContourShardIslands(bounds, grid, ownerLabels, coreSupported, islands);

            islands.Sort((a, b) => b.Area.CompareTo(a.Area));
            return islands;
        }

        private UnsupportedIslandScanGrid BuildUnsupportedIslandScanGrid(DamageComponentPlaneBounds bounds)
        {
            // Stamps are append-only, so the count identifies the damage state exactly. A
            // single shot rebuilds this grid roughly a dozen times (island passes, structural
            // passes, colliders, outline quads); reusing it removes most of the per-shot hitch.
            if (cachedScanGrid != null && cachedScanGridStampCount == wallDamageStamps.Count)
            {
                return cachedScanGrid;
            }

            scanStampsHaveShear = CalculateWallDamageStampsHaveShear();
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

            cachedScanGrid = grid;
            cachedScanGridStampCount = wallDamageStamps.Count;
            return grid;
        }

        private bool IsWallMaterialPresentInScanCell(UnsupportedIslandScanGrid grid, int x, int y)
        {
            if (!IsPointInsideWallDamageUnionThroughDepth(grid.CellCenter(x, y)))
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
                !IsPointInsideWallDamageUnionThroughDepth(sample);
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

        // Nothing rests on a wall's top edge, so contact there must not count as support;
        // returns the perimeter side-mask bits of edges that provide no real support. A
        // wall's side edges abut neighboring structure and stay supportive, but a pillar
        // slab's side edges are its open faces — only the ground holds a pillar up.
        private int CalculateTopPerimeterSupportExclusionMask()
        {
            if (!TryGetContourOwnedWallBasis(0f, out _, out var u, out var v, out _, out _) ||
                !TryGetWallUvUpAxis(u, v, out var upIsU, out var upSign))
            {
                return 0;
            }

            if (pillarAxisSlabMode)
            {
                var bottomBit = upIsU ? (upSign > 0f ? 1 : 2) : (upSign > 0f ? 4 : 8);
                return 15 & ~bottomBit;
            }

            if (upIsU)
            {
                return upSign > 0f ? 2 : 1;
            }

            return upSign > 0f ? 8 : 4;
        }

        private static int[] LabelSupportCoreComponents(UnsupportedIslandScanGrid grid, int excludedSideMask, out bool[] coreSupported)
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
                    AccumulatePerimeterSupportContact(grid, x, y, excludedSideMask, ref perimeterContactLength, ref perimeterSideMask);
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
            int excludedSideMask,
            ref float contactLength,
            ref int sideMask)
        {
            if (x == 0 && (excludedSideMask & 1) == 0)
            {
                contactLength += grid.StepV;
                sideMask |= 1;
            }

            if (x == grid.Columns - 1 && (excludedSideMask & 2) == 0)
            {
                contactLength += grid.StepV;
                sideMask |= 2;
            }

            if (y == 0 && (excludedSideMask & 4) == 0)
            {
                contactLength += grid.StepU;
                sideMask |= 4;
            }

            if (y == grid.Rows - 1 && (excludedSideMask & 8) == 0)
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
            var rawSegments = BuildClippedVisibleContourSegments(bounds, thickness, false, false, 0f);
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
            const float boundaryEpsilon = 0.003f;
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

        private static void JaggedizePolygonLoop(List<Vector2> points, float amplitude)
        {
            if (points == null || points.Count < 3 || amplitude <= 0f)
            {
                return;
            }

            var seed = 0x3a99;
            for (var i = 0; i < points.Count; i++)
            {
                unchecked
                {
                    seed = seed * 31 + Mathf.RoundToInt(points[i].x * 73f) + Mathf.RoundToInt(points[i].y * 131f);
                }
            }

            var result = new List<Vector2>(points.Count * 3);
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                result.Add(a);
                var edge = b - a;
                var length = edge.magnitude;
                var pieces = Mathf.Min(6, Mathf.FloorToInt(length / (amplitude * 2.5f)));
                if (pieces < 2)
                {
                    continue;
                }

                var direction = edge / length;
                var perpendicular = new Vector2(-direction.y, direction.x);
                for (var piece = 1; piece < pieces; piece++)
                {
                    var offset = Mathf.Lerp(-amplitude, amplitude, Hash01(seed ^ (i * 387) ^ (piece * 5197)));
                    result.Add(a + edge * (piece / (float)pieces) + perpendicular * offset);
                }
            }

            points.Clear();
            points.AddRange(result);
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

        private void SpawnContourOwnedWallHitSpray(DamageStamp stamp, Vector3 hitNormalWorld, Vector3 shotDirectionWorld)
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
            // Impact shards punch out along the projectile's actual path through the wall;
            // the surface normal is only a fallback when no shot direction is known.
            var sprayDirectionWorld = shotDirectionWorld.sqrMagnitude > 0.0001f
                ? shotDirectionWorld.normalized
                : hitNormalWorld.sqrMagnitude > 0.0001f
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
            ref int slabBudget,
            Vector3? toppleDirectionWorld = null)
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

            var centroid = island.Centroid;
            var shape = new List<Vector2>(island.Points);
            if (CalculateSignedPolygonArea(shape) < 0f)
            {
                shape.Reverse();
            }

            var capTriangles = new List<int>();
            if (!TryTriangulatePolygon(shape, capTriangles))
            {
                shape = hull;
                capTriangles.Clear();
                for (var i = 1; i < hull.Count - 1; i++)
                {
                    capTriangles.Add(0);
                    capTriangles.Add(i);
                    capTriangles.Add(i + 1);
                }
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var i = 0; i < capTriangles.Count; i += 3)
            {
                AddTriangleOriented(
                    vertices,
                    triangles,
                    SlabPointToMeshLocal(shape[capTriangles[i]], centroid, u, v, normal, halfN),
                    SlabPointToMeshLocal(shape[capTriangles[i + 1]], centroid, u, v, normal, halfN),
                    SlabPointToMeshLocal(shape[capTriangles[i + 2]], centroid, u, v, normal, halfN),
                    normal);
                AddTriangleOriented(
                    vertices,
                    triangles,
                    SlabPointToMeshLocal(shape[capTriangles[i]], centroid, u, v, normal, -halfN),
                    SlabPointToMeshLocal(shape[capTriangles[i + 1]], centroid, u, v, normal, -halfN),
                    SlabPointToMeshLocal(shape[capTriangles[i + 2]], centroid, u, v, normal, -halfN),
                    -normal);
            }

            for (var i = 0; i < shape.Count; i++)
            {
                var a = shape[i];
                var b = shape[(i + 1) % shape.Count];
                var edge = b - a;
                if (edge.sqrMagnitude <= 0.0000001f)
                {
                    continue;
                }

                var sideOutward2D = new Vector2(edge.y, -edge.x);
                var sideOutward = u * sideOutward2D.x + v * sideOutward2D.y;
                AddQuadOriented(
                    vertices,
                    triangles,
                    SlabPointToMeshLocal(a, centroid, u, v, normal, halfN),
                    SlabPointToMeshLocal(b, centroid, u, v, normal, halfN),
                    SlabPointToMeshLocal(b, centroid, u, v, normal, -halfN),
                    SlabPointToMeshLocal(a, centroid, u, v, normal, -halfN),
                    sideOutward);
            }

            var colliderVertices = new List<Vector3>();
            var colliderTriangles = new List<int>();
            for (var i = 1; i < hull.Count - 1; i++)
            {
                AddTriangleOriented(
                    colliderVertices,
                    colliderTriangles,
                    SlabPointToMeshLocal(hull[0], centroid, u, v, normal, halfN),
                    SlabPointToMeshLocal(hull[i], centroid, u, v, normal, halfN),
                    SlabPointToMeshLocal(hull[i + 1], centroid, u, v, normal, halfN),
                    normal);
                AddTriangleOriented(
                    colliderVertices,
                    colliderTriangles,
                    SlabPointToMeshLocal(hull[0], centroid, u, v, normal, -halfN),
                    SlabPointToMeshLocal(hull[i], centroid, u, v, normal, -halfN),
                    SlabPointToMeshLocal(hull[i + 1], centroid, u, v, normal, -halfN),
                    -normal);
            }

            var colliderMesh = CreateMesh("Falling Wall Slab Collider Mesh", colliderVertices, new[] { colliderTriangles });
            var mesh = CreateMesh("Falling Wall Slab Mesh", vertices, new[] { triangles });
            var rimVertices = new List<Vector3>();
            var rimTriangles = new List<int>();
            var rimThickness = Mathf.Clamp(halfN * 0.16f, 0.012f, 0.03f);
            for (var i = 0; i < shape.Count; i++)
            {
                var a = shape[i];
                var b = shape[(i + 1) % shape.Count];
                if ((b - a).sqrMagnitude <= 0.0000001f)
                {
                    continue;
                }

                AddContourSegment(
                    rimVertices,
                    rimTriangles,
                    SlabPointToMeshLocal(a, centroid, u, v, normal, halfN),
                    SlabPointToMeshLocal(b, centroid, u, v, normal, halfN),
                    normal,
                    rimThickness);
                AddContourSegment(
                    rimVertices,
                    rimTriangles,
                    SlabPointToMeshLocal(a, centroid, u, v, normal, -halfN),
                    SlabPointToMeshLocal(b, centroid, u, v, normal, -halfN),
                    -normal,
                    rimThickness);
            }

            var rimMesh = rimVertices.Count == 0
                ? null
                : CreateMesh("Falling Wall Slab Rim Mesh", rimVertices, new[] { rimTriangles });
            var seed = CalculateUnsupportedIslandSpraySeed(island, transform.TransformDirection(normal));
            var centerLocal = u * centroid.x + v * centroid.y;
            Vector3 drift;
            Vector3 spawnOffset;
            if (toppleDirectionWorld.HasValue)
            {
                drift = toppleDirectionWorld.Value * 0.55f;
                spawnOffset = toppleDirectionWorld.Value * 0.04f;
            }
            else if (pillarAxisSlabMode)
            {
                // Pillar pieces are a full pillar thickness deep; a random sideways shove can
                // bury them inside an adjacent wall, so always fall toward the open side.
                var awayFromWalls = ResolvePillarSlabToppleDirectionWorld();
                drift = awayFromWalls * 0.55f;
                spawnOffset = awayFromWalls * WallDebrisSpawnClearance;
            }
            else
            {
                // The piece's volume was freed before spawning, so it can detach in place
                // instead of teleporting a full wall thickness sideways.
                drift = transform.TransformDirection(normal) *
                    ((Hash01(seed ^ 0x9c7) > 0.5f ? 1f : -1f) * 0.55f);
                spawnOffset = drift.sqrMagnitude > 0.0001f
                    ? drift.normalized * WallDebrisSpawnClearance
                    : Vector3.zero;
            }

            SpawnFallingDebrisObject(mesh, colliderMesh, rimMesh, transform.TransformPoint(centerLocal), transform.rotation, seed, drift, spawnOffset, toppleDirectionWorld);
            slabBudget--;
        }


        private static Vector3 SlabPointToMeshLocal(Vector2 point, Vector2 centroid, Vector3 u, Vector3 v, Vector3 normal, float depth)
        {
            return u * (point.x - centroid.x) + v * (point.y - centroid.y) + normal * depth;
        }

        private void SpawnFallingDebrisObject(
            Mesh mesh,
            Mesh colliderMesh,
            Mesh rimMesh,
            Vector3 worldPosition,
            Quaternion worldRotation,
            int seed,
            Vector3 driftVelocityWorld,
            Vector3 spawnOffsetWorld,
            Vector3? toppleDirectionWorld = null)
        {
            var slab = new GameObject("Falling Wall Slab");
            slab.transform.position = worldPosition + spawnOffsetWorld;
            slab.transform.rotation = worldRotation;
            var meshFilter = slab.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            var renderer = slab.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetUnclippedDestructibleBodyMaterial();
            var outlineCategory = configuredOutlineCategory == StylizedOutlineCategory.None
                ? StylizedOutlineCategory.Wall
                : configuredOutlineCategory;
            DroidRenderSetup.ApplyRenderer(renderer, outlineCategory);

            if (rimMesh != null)
            {
                var rim = new GameObject("Falling Wall Slab Rim");
                rim.transform.SetParent(slab.transform, false);
                var rimFilter = rim.AddComponent<MeshFilter>();
                rimFilter.sharedMesh = rimMesh;
                var rimRenderer = rim.AddComponent<MeshRenderer>();
                rimRenderer.sharedMaterial = GetDamageContourMaterial();
                DroidRenderSetup.ApplyRenderer(rimRenderer, StylizedOutlineCategory.None);
            }

            var slabCollider = slab.AddComponent<MeshCollider>();
            slabCollider.sharedMesh = colliderMesh != null ? colliderMesh : mesh;
            slabCollider.convex = true;
            slabCollider.material = GetFallingDebrisPhysicsMaterial();
            var body = slab.AddComponent<Rigidbody>();
            var debrisSize = mesh.bounds.size;
            var debrisFaceArea = Mathf.Max(
                debrisSize.x * debrisSize.y,
                Mathf.Max(debrisSize.y * debrisSize.z, debrisSize.x * debrisSize.z));
            var kickScale = Mathf.Clamp(0.4f / Mathf.Max(0.1f, debrisFaceArea), 0.2f, 1.5f);
            body.mass = Mathf.Clamp(debrisFaceArea * 4f, 1f, 40f);
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (toppleDirectionWorld.HasValue && toppleDirectionWorld.Value.sqrMagnitude > 0.0001f)
            {
                // A severed pillar section pivots over its base: spin about up x direction so the
                // top leans toward the open side, with enough kick to clear the stump.
                var topple = toppleDirectionWorld.Value.normalized;
                var toppleAxis = Vector3.Cross(Vector3.up, topple);
                var toppleSpeed = Mathf.Lerp(
                    PillarToppleTipMinDegreesPerSecond,
                    PillarToppleTipMaxDegreesPerSecond,
                    Hash01(seed ^ 0x215));
                body.linearVelocity = topple * PillarToppleKickSpeed;
                body.angularVelocity = toppleAxis.sqrMagnitude > 0.0001f
                    ? toppleAxis.normalized * (toppleSpeed * Mathf.Deg2Rad)
                    : Vector3.zero;
            }
            else if (hasLastDamageImpulse)
            {
                // The piece detaches as dead weight; the only added motion is the shot's energy
                // delivered as an impulse at the attachment point nearest the hit. Rigidbody
                // mass and inertia then do the realistic part for free: heavy slabs barely
                // react and drop straight down, light shards get visibly kicked and spun.
                // The mass-scaled dislodge term pops small fragments clear of the wall.
                body.linearVelocity = lastDamageShotDirectionWorld *
                    Mathf.Min(WallDebrisDislodgeKick / body.mass, WallDebrisDislodgeKick);
                body.angularVelocity = Vector3.zero;
                if (Application.isPlaying)
                {
                    var impulse = lastDamageShotDirectionWorld *
                        Mathf.Lerp(WallDebrisShotImpulseMin, WallDebrisShotImpulseMax, Hash01(seed ^ 0x5b3));
                    body.AddForceAtPosition(impulse, slabCollider.ClosestPoint(lastDamageHitPointWorld), ForceMode.Impulse);
                }
            }
            else
            {
                var lateral = new Vector3(
                    Mathf.Lerp(-0.35f, 0.35f, Hash01(seed ^ 0x3d1)),
                    0f,
                    Mathf.Lerp(-0.35f, 0.35f, Hash01(seed ^ 0x77b)));
                var tipAxis = Vector3.Cross(Vector3.up, lateral.sqrMagnitude > 0.0001f ? lateral.normalized : Vector3.forward);
                var tipSpeed = Mathf.Lerp(18f, FallingSlabMaxTipDegreesPerSecond, Hash01(seed ^ 0x215)) *
                    (Hash01(seed ^ 0x6c2) > 0.5f ? 1f : -1f);
                body.linearVelocity = (lateral + driftVelocityWorld) * kickScale;
                body.angularVelocity = tipAxis * (tipSpeed * Mathf.Deg2Rad * kickScale);
            }

            slab.AddComponent<FallingWallSlabAnimation>().Initialize(FallingSlabLifetimeSeconds);
            slab.AddComponent<FallingSlabImpactShatter>().Initialize(renderer.sharedMaterial, GetDamageContourMaterial(), outlineCategory, seed);
        }

        private static Mesh impactGlowMesh;

        // Unit-radius horizontal disc with the footprint falloff mask baked into UV0.x
        // (1 at the core, 0 at the rim); the spawner bakes the actual radius into the
        // transform.
        private static Mesh GetImpactGlowMesh()
        {
            if (impactGlowMesh != null)
            {
                return impactGlowMesh;
            }

            const int segments = 16;
            var ringRadii = new[] { 0.45f, 0.75f, 1f };
            var ringMasks = new[] { 1f, 0.45f, 0f };
            BuildImpactGlowRingFan(
                segments,
                ringRadii,
                ringMasks,
                ringIndex => angleIndex => new Vector2(
                    Mathf.Cos(angleIndex / (float)segments * Mathf.PI * 2f) * ringRadii[ringIndex],
                    Mathf.Sin(angleIndex / (float)segments * Mathf.PI * 2f) * ringRadii[ringIndex]),
                out var vertices,
                out var normals,
                out var uvs,
                out var triangles);

            impactGlowMesh = new Mesh
            {
                name = "Wall Debris Impact Glow Mesh",
                vertices = vertices,
                normals = normals,
                uv = uvs,
                triangles = triangles
            };
            return impactGlowMesh;
        }

        // Builds a center vertex plus concentric rings sharing one falloff mask in UV0.x,
        // fanned/stitched into a watertight horizontal sheet.
        private static void BuildImpactGlowRingFan(
            int segments,
            float[] ringRadii,
            float[] ringMasks,
            System.Func<int, System.Func<int, Vector2>> ringPointResolver,
            out Vector3[] vertices,
            out Vector3[] normals,
            out Vector2[] uvs,
            out int[] triangles)
        {
            var ringCount = ringRadii.Length;
            vertices = new Vector3[1 + ringCount * segments];
            normals = new Vector3[vertices.Length];
            uvs = new Vector2[vertices.Length];
            triangles = new int[segments * 3 + (ringCount - 1) * segments * 6];
            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uvs[0] = new Vector2(1f, 0f);
            for (var ring = 0; ring < ringCount; ring++)
            {
                var resolvePoint = ringPointResolver(ring);
                for (var i = 0; i < segments; i++)
                {
                    var point = resolvePoint(i);
                    var index = 1 + ring * segments + i;
                    vertices[index] = new Vector3(point.x, 0f, point.y);
                    normals[index] = Vector3.up;
                    uvs[index] = new Vector2(ringMasks[ring], 0f);
                }
            }

            var triangleIndex = 0;
            for (var i = 0; i < segments; i++)
            {
                triangles[triangleIndex++] = 0;
                triangles[triangleIndex++] = 1 + (i + 1) % segments;
                triangles[triangleIndex++] = 1 + i;
            }

            for (var ring = 0; ring < ringCount - 1; ring++)
            {
                var inner = 1 + ring * segments;
                var outer = inner + segments;
                for (var i = 0; i < segments; i++)
                {
                    var next = (i + 1) % segments;
                    triangles[triangleIndex++] = inner + i;
                    triangles[triangleIndex++] = outer + next;
                    triangles[triangleIndex++] = outer + i;
                    triangles[triangleIndex++] = inner + i;
                    triangles[triangleIndex++] = inner + next;
                    triangles[triangleIndex++] = outer + next;
                }
            }
        }

        private static Material CreateImpactGlowMaterial(Material floorHexMaterial)
        {
            var glowShader = Shader.Find("ArenaShooter/DebrisImpactGlow");
            if (glowShader != null)
            {
                // The dedicated shader draws a translucent orange wash, the floor's red
                // hex pattern (aligned via the floor material's own parameters), and a
                // brief shockwave ripple — all dissolving through the falloff mask.
                var glowMaterial = new Material(glowShader) { name = "Wall Debris Impact Glow Material" };
                glowMaterial.SetColor("_GlowColor", DebrisImpactGlowColor);
                var hexSize = 0.45f;
                var hexLineWidth = 0.0035f;
                var patternOrigin = Vector4.zero;
                var hexLineColor = new Color(1f, 0.12f, 0.04f, 1f);
                if (floorHexMaterial != null)
                {
                    hexSize = floorHexMaterial.GetFloat("_HexSize");
                    patternOrigin = floorHexMaterial.GetVector("_PatternOrigin");
                    if (floorHexMaterial.HasProperty("_LineWidth"))
                    {
                        hexLineWidth = floorHexMaterial.GetFloat("_LineWidth");
                    }

                    if (floorHexMaterial.HasProperty("_LineColor"))
                    {
                        hexLineColor = floorHexMaterial.GetColor("_LineColor");
                    }
                }

                glowMaterial.SetFloat("_HexSize", hexSize);
                // The floor's idle hairline is too thin to read inside a brief flash;
                // beef the revealed lines up while keeping them clearly line-like.
                glowMaterial.SetFloat("_LineWidth", Mathf.Clamp(hexLineWidth * 3f, 0.01f, 0.022f));
                glowMaterial.SetVector("_PatternOrigin", patternOrigin);
                glowMaterial.SetColor("_LineColor", hexLineColor);
                return glowMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            // Fallback when the glow shader is missing: additive unlit color, animated
            // toward black so it dissolves into the floor.
            var material = new Material(shader) { name = "Wall Debris Impact Glow Material" };
            SetMaterialColor(material, DebrisImpactGlowColor);
            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", 0);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            }

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return material;
        }

        // Projects the debris mesh onto the floor plane and builds its convex footprint
        // as concentric rings around the impact point: a bright debris-shaped core that
        // softly dissolves to nothing past the hull, via the falloff mask in UV0.x.
        private static Mesh CreateImpactGlowFootprintMesh(
            Mesh sourceMesh,
            Transform sourceTransform,
            Vector3 impactPoint,
            out float footprintRadius)
        {
            footprintRadius = 0.45f;
            if (sourceMesh == null || sourceTransform == null)
            {
                return null;
            }

            var sourceVertices = sourceMesh.vertices;
            if (sourceVertices == null || sourceVertices.Length < 3)
            {
                return null;
            }

            var stride = Mathf.Max(1, sourceVertices.Length / 48);
            var points = new List<Vector2>(sourceVertices.Length / stride + 1);
            for (var i = 0; i < sourceVertices.Length; i += stride)
            {
                var world = sourceTransform.TransformPoint(sourceVertices[i]);
                points.Add(new Vector2(world.x - impactPoint.x, world.z - impactPoint.z));
            }

            if (!TryBuildConvexHull(points, out var hull) || hull.Count < 3)
            {
                return null;
            }

            foreach (var point in hull)
            {
                footprintRadius = Mathf.Max(footprintRadius, point.magnitude);
            }

            var falloffMargin = Mathf.Clamp(footprintRadius * 0.85f, 0.3f, 1f);
            var capturedRadius = footprintRadius;
            var ringScales = new[] { 0.6f, 1f, -1f };
            var ringMasks = new[] { 1f, 0.45f, 0f };
            BuildImpactGlowRingFan(
                hull.Count,
                ringScales,
                ringMasks,
                ringIndex => angleIndex =>
                {
                    var point = hull[angleIndex];
                    if (ringScales[ringIndex] >= 0f)
                    {
                        return point * ringScales[ringIndex];
                    }

                    // Outermost ring: pushed outward by the falloff margin so the fade
                    // happens past the debris hull instead of eating into the core.
                    var magnitude = point.magnitude;
                    var direction = magnitude > 0.0001f
                        ? point / magnitude
                        : new Vector2(
                            Mathf.Cos(angleIndex / (float)hull.Count * Mathf.PI * 2f),
                            Mathf.Sin(angleIndex / (float)hull.Count * Mathf.PI * 2f));
                    return point + direction * Mathf.Min(falloffMargin, capturedRadius);
                },
                out var vertices,
                out var normals,
                out var uvs,
                out var triangles);

            return new Mesh
            {
                name = "Wall Debris Impact Glow Footprint Mesh",
                vertices = vertices,
                normals = normals,
                uv = uvs,
                triangles = triangles
            };
        }

        // Builds an irregular convex polygon extruded into a thin tapered plate — a
        // glass-like shard — plus a matching neon edge-rim mesh outlining both faces.
        private static Mesh CreateShatterShardMesh(int shardSeed, float radius, float thickness, out Mesh rimMesh)
        {
            var cornerCount = 4 + Mathf.FloorToInt(Hash01(shardSeed ^ 0x3f1) * 2.999f);
            var halfThickness = Mathf.Max(0.004f, thickness * 0.5f);
            var bottomScale = Mathf.Lerp(0.5f, 0.85f, Hash01(shardSeed ^ 0x5c3));
            var top = new Vector2[cornerCount];
            var bottom = new Vector2[cornerCount];
            for (var i = 0; i < cornerCount; i++)
            {
                var angle = (i + Hash01(shardSeed ^ (i * 0x68b ^ 0x1cd)) * 0.55f) / cornerCount * Mathf.PI * 2f;
                var cornerRadius = radius * Mathf.Lerp(0.55f, 1f, Hash01(shardSeed ^ (i * 0x2e5 ^ 0x7a9)));
                top[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * cornerRadius;
                bottom[i] = top[i] * bottomScale;
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var i = 1; i < cornerCount - 1; i++)
            {
                AddShardTriangle(
                    vertices,
                    triangles,
                    ShardPoint(top[0], halfThickness),
                    ShardPoint(top[i], halfThickness),
                    ShardPoint(top[i + 1], halfThickness),
                    Vector3.up);
                AddShardTriangle(
                    vertices,
                    triangles,
                    ShardPoint(bottom[0], -halfThickness),
                    ShardPoint(bottom[i], -halfThickness),
                    ShardPoint(bottom[i + 1], -halfThickness),
                    Vector3.down);
            }

            for (var i = 0; i < cornerCount; i++)
            {
                var next = (i + 1) % cornerCount;
                var outward2D = (top[i] + top[next]) * 0.5f;
                var outward = new Vector3(outward2D.x, 0f, outward2D.y);
                if (outward.sqrMagnitude < 0.000001f)
                {
                    outward = Vector3.forward;
                }

                AddShardTriangle(
                    vertices,
                    triangles,
                    ShardPoint(top[i], halfThickness),
                    ShardPoint(top[next], halfThickness),
                    ShardPoint(bottom[next], -halfThickness),
                    outward);
                AddShardTriangle(
                    vertices,
                    triangles,
                    ShardPoint(top[i], halfThickness),
                    ShardPoint(bottom[next], -halfThickness),
                    ShardPoint(bottom[i], -halfThickness),
                    outward);
            }

            var rimVertices = new List<Vector3>();
            var rimTriangles = new List<int>();
            var rimThickness = Mathf.Clamp(radius * 0.16f, 0.005f, 0.014f);
            for (var i = 0; i < cornerCount; i++)
            {
                var next = (i + 1) % cornerCount;
                AddShardRimSegment(
                    rimVertices,
                    rimTriangles,
                    ShardPoint(top[i], halfThickness),
                    ShardPoint(top[next], halfThickness),
                    Vector3.up,
                    rimThickness);
                AddShardRimSegment(
                    rimVertices,
                    rimTriangles,
                    ShardPoint(bottom[i], -halfThickness),
                    ShardPoint(bottom[next], -halfThickness),
                    Vector3.down,
                    rimThickness);
            }

            rimMesh = rimVertices.Count == 0
                ? null
                : BuildShardMesh("Wall Debris Shatter Fragment Rim Mesh", rimVertices, rimTriangles);
            return BuildShardMesh("Wall Debris Shatter Fragment Mesh", vertices, triangles);
        }

        private static Vector3 ShardPoint(Vector2 planar, float height)
        {
            return new Vector3(planar.x, height, planar.y);
        }

        private static Mesh BuildShardMesh(string meshName, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddShardTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
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

        // Double-sided thin quad centered on a shard edge, mirroring the parent slab's
        // contour rim segments at fragment scale.
        private static void AddShardRimSegment(List<Vector3> vertices, List<int> triangles, Vector3 start, Vector3 end, Vector3 normal, float thickness)
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
            AddShardTriangle(vertices, triangles, start - halfWidth, end - halfWidth, end + halfWidth, normal);
            AddShardTriangle(vertices, triangles, start - halfWidth, end + halfWidth, start + halfWidth, normal);
            AddShardTriangle(vertices, triangles, start - halfWidth, end + halfWidth, end - halfWidth, -normal);
            AddShardTriangle(vertices, triangles, start - halfWidth, start + halfWidth, end + halfWidth, -normal);
        }

        private static PhysicsMaterial fallingDebrisPhysicsMaterial;

        private static PhysicsMaterial GetFallingDebrisPhysicsMaterial()
        {
            if (fallingDebrisPhysicsMaterial == null)
            {
                fallingDebrisPhysicsMaterial = new PhysicsMaterial("Falling Wall Debris")
                {
                    bounciness = 0.35f,
                    dynamicFriction = 0.7f,
                    staticFriction = 0.7f,
                    bounceCombine = PhysicsMaterialCombine.Maximum
                };
            }

            return fallingDebrisPhysicsMaterial;
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
            int seed,
            Vector2 shearPerDepth,
            DamageComponentPlaneBounds clampBounds)
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

            // Pillar slab polygons may overflow the box edge: the sibling slab clips against
            // them (dual-basis shader), so a corner bite consumes both faces. The exit offset
            // stays clamped to the piece bounds for everything, otherwise sheared rim and
            // tunnel geometry trails up to a meter past the visible faces.
            var thickness = halfN * 2f;
            var exitOffset = ClampStampExitOffsetToBounds(shearPerDepth * thickness, min, max, clampBounds);
            var effectiveShear = thickness > 0.0001f ? exitOffset / thickness : Vector2.zero;
            var stamp = new DamageStamp
            {
                Normal = normal,
                U = u,
                V = v,
                Plane = halfN + DamageContourInset,
                Min = min,
                Max = max,
                Points = points,
                ShearPerDepth = effectiveShear,
                MidDepthOffset = exitOffset * 0.5f
            };
            stamp.Opposite = CreateOppositeContourOwnedWallStamp(stamp, normal, halfN);
            return stamp;
        }

        private static Vector2 ClampStampExitOffsetToBounds(Vector2 offset, Vector2 min, Vector2 max, DamageComponentPlaneBounds bounds)
        {
            // Keep the exit polygon from sliding past the piece bounds while always allowing a
            // straight (zero offset) tunnel even when the entry polygon already crosses an edge.
            var lowX = Mathf.Min(0f, bounds.MinU - min.x);
            var highX = Mathf.Max(0f, bounds.MaxU - max.x);
            var lowY = Mathf.Min(0f, bounds.MinV - min.y);
            var highY = Mathf.Max(0f, bounds.MaxV - max.y);
            return new Vector2(Mathf.Clamp(offset.x, lowX, highX), Mathf.Clamp(offset.y, lowY, highY));
        }

        private static DamageStamp CreateOppositeContourOwnedWallStamp(DamageStamp source, Vector3 normal, float halfN)
        {
            var exitOffset = source.ShearPerDepth * (halfN * 2f);
            var opposite = new DamageStamp
            {
                Normal = -normal,
                U = source.U,
                V = source.V,
                Plane = halfN + DamageContourInset,
                Min = source.Min + exitOffset,
                Max = source.Max + exitOffset,
                Points = source.Points,
                RenderClosed = source.RenderClosed,
                ShearPerDepth = source.ShearPerDepth,
                UvOffset = exitOffset,
                MidDepthOffset = source.MidDepthOffset
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

        // A (u,v) column only counts as empty when the hole union covers it at the front, the
        // middle, and the back of the material. Sheared tunnels leave partial-depth wedges (a
        // face plus interior wall with no opposite face); sampling a single depth made those
        // invisible to every structural pass, so they lingered as phantom shards.
        private bool IsPointInsideWallDamageUnionThroughDepth(Vector2 point)
        {
            if (!scanStampsHaveShear)
            {
                // Straight tunnels are identical at every depth; skip the two extra passes.
                return IsPointInsideWallDamageUnionAtHalfDepthSteps(point, 0);
            }

            return IsPointInsideWallDamageUnionAtHalfDepthSteps(point, 0) &&
                IsPointInsideWallDamageUnionAtHalfDepthSteps(point, 1) &&
                IsPointInsideWallDamageUnionAtHalfDepthSteps(point, 2);
        }

        private bool CalculateWallDamageStampsHaveShear()
        {
            for (var i = 0; i < wallDamageStamps.Count; i++)
            {
                var stamp = wallDamageStamps[i];
                if (stamp != null && stamp.MidDepthOffset.sqrMagnitude > 0.00000001f)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPointInsideWallDamageUnionAtHalfDepthSteps(Vector2 point, int halfDepthSteps)
        {
            for (var i = 0; i < wallDamageStamps.Count; i++)
            {
                var stamp = wallDamageStamps[i];
                if (stamp == null)
                {
                    continue;
                }

                var corrected = point - stamp.MidDepthOffset * halfDepthSteps;
                if (corrected.x >= stamp.Min.x &&
                    corrected.x <= stamp.Max.x &&
                    corrected.y >= stamp.Min.y &&
                    corrected.y <= stamp.Max.y &&
                    IsPointInPolygon(corrected, stamp.Points))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsLocalPointInsideWallDamageUnion(Vector3 localPoint)
        {
            if (!TryGetContourOwnedWallBasis(0f, out var normal, out var u, out var v, out var halfN, out _))
            {
                return false;
            }

            var uv = new Vector2(Vector3.Dot(localPoint, u), Vector3.Dot(localPoint, v));
            var depth = halfN - Vector3.Dot(localPoint, normal);
            for (var i = 0; i < wallDamageStamps.Count; i++)
            {
                var stamp = wallDamageStamps[i];
                if (stamp == null)
                {
                    continue;
                }

                var corrected = uv - stamp.ShearPerDepth * depth;
                if (corrected.x >= stamp.Min.x &&
                    corrected.x <= stamp.Max.x &&
                    corrected.y >= stamp.Min.y &&
                    corrected.y <= stamp.Max.y &&
                    IsPointInPolygon(corrected, stamp.Points))
                {
                    return true;
                }
            }

            return false;
        }

        private void UploadWallDamageShaderData()
        {
            if (!UsesContourOwnedWallDamage() ||
                !TryGetContourOwnedWallBasis(0f, out var normal, out var u, out var v, out var halfN, out _))
            {
                return;
            }

            // A pillar axis slab also clips against its sibling's stamps (flagged into the
            // secondary basis) so a hole reaching the shared box corner bites both faces.
            List<DamageStamp> siblingStamps = null;
            var siblingNormal = Vector3.zero;
            var siblingU = Vector3.zero;
            var siblingV = Vector3.zero;
            var siblingHalfN = 0f;
            if (pillarAxisSlabMode &&
                structuralFallSibling != null &&
                structuralFallSibling.wallDamageStamps.Count > 0 &&
                structuralFallSibling.TryGetContourOwnedWallBasis(0f, out siblingNormal, out siblingU, out siblingV, out siblingHalfN, out _))
            {
                siblingStamps = structuralFallSibling.wallDamageStamps;
            }

            var ownCount = wallDamageStamps.Count;
            var siblingCount = siblingStamps?.Count ?? 0;
            var stampCount = ownCount + siblingCount;
            if (stampCount == 0)
            {
                // Renderers must still get a property block with bound (dummy) buffers — the
                // clip shaders drop their draws when the declared buffers are left unbound.
                // One shared static set serves every undamaged piece, so arena generation
                // stays free of per-piece GPU allocations.
                EnsureSharedEmptyWallDamageBuffers();
                ApplyWallDamagePropertyBlock(
                    combinedRenderer, 0, u, v, normal, halfN, Vector4.zero, Vector4.zero, Vector4.zero,
                    sharedEmptyStampBoundsBuffer, sharedEmptyStampPointOffsetsBuffer, sharedEmptyStampPointCountsBuffer, sharedEmptyStampPointsBuffer, sharedEmptyStampShearsBuffer);
                ApplyWallDamagePropertyBlock(
                    outlineSourceRenderer, 0, u, v, normal, halfN, Vector4.zero, Vector4.zero, Vector4.zero,
                    sharedEmptyStampBoundsBuffer, sharedEmptyStampPointOffsetsBuffer, sharedEmptyStampPointCountsBuffer, sharedEmptyStampPointsBuffer, sharedEmptyStampShearsBuffer);
                return;
            }

            var pointCount = 0;
            for (var i = 0; i < ownCount; i++)
            {
                pointCount += wallDamageStamps[i]?.Points?.Length ?? 0;
            }

            for (var i = 0; i < siblingCount; i++)
            {
                pointCount += siblingStamps[i]?.Points?.Length ?? 0;
            }

            EnsureWallDamageShaderBufferCapacity(
                Mathf.Max(1, stampCount),
                Mathf.Max(1, pointCount));

            var stampBounds = new Vector4[Mathf.Max(1, stampCount)];
            var stampPointOffsets = new int[Mathf.Max(1, stampCount)];
            var stampPointCounts = new int[Mathf.Max(1, stampCount)];
            var stampShears = new Vector4[Mathf.Max(1, stampCount)];
            var stampPoints = new Vector4[Mathf.Max(1, pointCount)];
            var pointOffset = 0;
            for (var stampIndex = 0; stampIndex < stampCount; stampIndex++)
            {
                var fromSibling = stampIndex >= ownCount;
                var stamp = fromSibling ? siblingStamps[stampIndex - ownCount] : wallDamageStamps[stampIndex];
                stampBounds[stampIndex] = new Vector4(stamp.Min.x, stamp.Min.y, stamp.Max.x, stamp.Max.y);
                stampPointOffsets[stampIndex] = pointOffset;
                stampShears[stampIndex] = new Vector4(stamp.ShearPerDepth.x, stamp.ShearPerDepth.y, fromSibling ? 1f : 0f, 0f);
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
            wallDamageStampShearsBuffer.SetData(stampShears);
            wallDamageStampPointsBuffer.SetData(stampPoints);
            var secondaryU = new Vector4(siblingU.x, siblingU.y, siblingU.z, 0f);
            var secondaryV = new Vector4(siblingV.x, siblingV.y, siblingV.z, 0f);
            var secondaryN = new Vector4(siblingNormal.x, siblingNormal.y, siblingNormal.z, siblingHalfN);
            ApplyWallDamagePropertyBlock(
                combinedRenderer, stampCount, u, v, normal, halfN, secondaryU, secondaryV, secondaryN,
                wallDamageStampBoundsBuffer, wallDamageStampPointOffsetsBuffer, wallDamageStampPointCountsBuffer, wallDamageStampPointsBuffer, wallDamageStampShearsBuffer);
            ApplyWallDamagePropertyBlock(
                outlineSourceRenderer, stampCount, u, v, normal, halfN, secondaryU, secondaryV, secondaryN,
                wallDamageStampBoundsBuffer, wallDamageStampPointOffsetsBuffer, wallDamageStampPointCountsBuffer, wallDamageStampPointsBuffer, wallDamageStampShearsBuffer);
        }

        private static ComputeBuffer sharedEmptyStampBoundsBuffer;
        private static ComputeBuffer sharedEmptyStampPointsBuffer;
        private static ComputeBuffer sharedEmptyStampPointOffsetsBuffer;
        private static ComputeBuffer sharedEmptyStampPointCountsBuffer;
        private static ComputeBuffer sharedEmptyStampShearsBuffer;

        private static void EnsureSharedEmptyWallDamageBuffers()
        {
            if (sharedEmptyStampBoundsBuffer != null)
            {
                return;
            }

            sharedEmptyStampBoundsBuffer = new ComputeBuffer(1, sizeof(float) * 4);
            sharedEmptyStampPointsBuffer = new ComputeBuffer(1, sizeof(float) * 4);
            sharedEmptyStampPointOffsetsBuffer = new ComputeBuffer(1, sizeof(int));
            sharedEmptyStampPointCountsBuffer = new ComputeBuffer(1, sizeof(int));
            sharedEmptyStampShearsBuffer = new ComputeBuffer(1, sizeof(float) * 4);
            Application.quitting += ReleaseSharedEmptyWallDamageBuffers;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseSharedEmptyWallDamageBuffers;
#endif
        }

        private static void ReleaseSharedEmptyWallDamageBuffers()
        {
            Application.quitting -= ReleaseSharedEmptyWallDamageBuffers;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseSharedEmptyWallDamageBuffers;
#endif
            sharedEmptyStampBoundsBuffer?.Release();
            sharedEmptyStampBoundsBuffer = null;
            sharedEmptyStampPointsBuffer?.Release();
            sharedEmptyStampPointsBuffer = null;
            sharedEmptyStampPointOffsetsBuffer?.Release();
            sharedEmptyStampPointOffsetsBuffer = null;
            sharedEmptyStampPointCountsBuffer?.Release();
            sharedEmptyStampPointCountsBuffer = null;
            sharedEmptyStampShearsBuffer?.Release();
            sharedEmptyStampShearsBuffer = null;
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

            if (wallDamageStampShearsBuffer == null || wallDamageStampShearsCapacity < stampCapacity)
            {
                wallDamageStampShearsBuffer?.Release();
                wallDamageStampShearsCapacity = Mathf.Max(1, stampCapacity);
                wallDamageStampShearsBuffer = new ComputeBuffer(wallDamageStampShearsCapacity, sizeof(float) * 4);
            }
        }

        private void ApplyWallDamagePropertyBlock(
            Renderer renderer,
            int stampCount,
            Vector3 u,
            Vector3 v,
            Vector3 normal,
            float halfN,
            Vector4 secondaryU,
            Vector4 secondaryV,
            Vector4 secondaryN,
            ComputeBuffer boundsBuffer,
            ComputeBuffer pointOffsetsBuffer,
            ComputeBuffer pointCountsBuffer,
            ComputeBuffer pointsBuffer,
            ComputeBuffer shearsBuffer)
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
            wallDamagePropertyBlock.SetVector(WallDamageNId, new Vector4(normal.x, normal.y, normal.z, halfN));
            wallDamagePropertyBlock.SetVector(WallDamageU2Id, secondaryU);
            wallDamagePropertyBlock.SetVector(WallDamageV2Id, secondaryV);
            wallDamagePropertyBlock.SetVector(WallDamageN2Id, secondaryN);
            wallDamagePropertyBlock.SetBuffer(WallDamageStampBoundsId, boundsBuffer);
            wallDamagePropertyBlock.SetBuffer(WallDamageStampPointOffsetsId, pointOffsetsBuffer);
            wallDamagePropertyBlock.SetBuffer(WallDamageStampPointCountsId, pointCountsBuffer);
            wallDamagePropertyBlock.SetBuffer(WallDamageStampPointsId, pointsBuffer);
            wallDamagePropertyBlock.SetBuffer(WallDamageStampShearsId, shearsBuffer);
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

            wallDamageStampShearsBuffer?.Release();
            wallDamageStampShearsBuffer = null;
            wallDamageStampShearsCapacity = 0;
        }

        private void RebuildContourOwnedWallMesh()
        {
            var vertices = new List<Vector3>();
            var slabTriangles = new List<int>();
            AddContourOwnedWallBodyGeometry(vertices, slabTriangles, 0f);

            var thickness = GetContourOwnedWallContourThickness();
            var visibleSegments = GetVisibleContourOwnedWallSegments(thickness);
            var backSegments = GetVisibleContourOwnedWallBackSegments(thickness);
            var bridgeVertices = new List<Vector3>();
            var bridgeTriangles = new List<int>();
            AddSlicedContourInteriorBridge(bridgeVertices, bridgeTriangles, thickness);

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
            RebuildContourOwnedWallDamageContourMesh(visibleSegments, backSegments);
            RebuildSurvivingColliders();
            UploadWallDamageShaderData();
            if (pillarAxisSlabMode)
            {
                if (pillarRouterPiece != null)
                {
                    pillarRouterPiece.RebuildPillarRouterColliders();
                }

                // The sibling clips against this slab's stamps too, so refresh its shader data.
                structuralFallSibling?.UploadWallDamageShaderData();
            }
        }

        private sealed class SurvivingColliderRun
        {
            public int StartColumn;
            public int EndColumn;
            public int RowStart;
            public int RowEnd;
        }

        private void RebuildSurvivingColliders()
        {
            if (suppressSurvivingColliders ||
                surfaceCollider == null ||
                wallDamageStamps.Count == 0 ||
                !TryGetContourOwnedWallBasis(0f, out var normal, out var u, out var v, out var halfN, out _))
            {
                return;
            }

            var rects = CollectSurvivingColliderRects();
            if (rects == null)
            {
                return;
            }

            if (survivingColliderRoot == null)
            {
                survivingColliderRoot = new GameObject("Destructible Wall Colliders");
                survivingColliderRoot.transform.SetParent(transform, false);
            }

            var previousColliders = survivingColliderRoot.GetComponents<BoxCollider>();
            for (var i = 0; i < previousColliders.Length; i++)
            {
                if (Application.isPlaying)
                {
                    Destroy(previousColliders[i]);
                }
                else
                {
                    DestroyImmediate(previousColliders[i]);
                }
            }

            surfaceCollider.enabled = false;
            for (var i = 0; i < rects.Count; i++)
            {
                var rect = rects[i];
                var box = survivingColliderRoot.AddComponent<BoxCollider>();
                box.center = u * ((rect.x + rect.z) * 0.5f) + v * ((rect.y + rect.w) * 0.5f);
                box.size = AbsVector(u) * (rect.z - rect.x) + AbsVector(v) * (rect.w - rect.y) + AbsVector(normal) * (halfN * 2f);
            }
        }

        // Returns surviving material rectangles as (minU, minV, maxU, maxV) in the wall plane,
        // or null while the piece is still undamaged (callers treat null as the full box).
        private List<Vector4> CollectSurvivingColliderRects()
        {
            if (wallDamageStamps.Count == 0 ||
                !TryGetContourOwnedWallBasis(0f, out _, out _, out _, out _, out var bounds))
            {
                return null;
            }

            var grid = BuildUnsupportedIslandScanGrid(bounds);
            if (grid.Count == 0)
            {
                return null;
            }

            var rects = new List<Vector4>();
            var activeBoxes = new List<SurvivingColliderRun>();
            var columnRuns = new List<Vector2Int>();
            for (var x = 0; x <= grid.Columns; x++)
            {
                columnRuns.Clear();
                if (x < grid.Columns)
                {
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
                            columnRuns.Add(new Vector2Int(runStart, y - 1));
                            runStart = -1;
                        }
                    }
                }

                for (var i = activeBoxes.Count - 1; i >= 0; i--)
                {
                    var box = activeBoxes[i];
                    var continued = false;
                    for (var r = 0; r < columnRuns.Count; r++)
                    {
                        if (columnRuns[r].x == box.RowStart && columnRuns[r].y == box.RowEnd)
                        {
                            box.EndColumn = x;
                            columnRuns.RemoveAt(r);
                            continued = true;
                            break;
                        }
                    }

                    if (!continued)
                    {
                        rects.Add(CreateSurvivingColliderRect(box, grid, bounds));
                        activeBoxes.RemoveAt(i);
                    }
                }

                for (var r = 0; r < columnRuns.Count; r++)
                {
                    activeBoxes.Add(new SurvivingColliderRun
                    {
                        StartColumn = x,
                        EndColumn = x,
                        RowStart = columnRuns[r].x,
                        RowEnd = columnRuns[r].y
                    });
                }
            }

            return rects;
        }

        private static Vector4 CreateSurvivingColliderRect(
            SurvivingColliderRun run,
            UnsupportedIslandScanGrid grid,
            DamageComponentPlaneBounds bounds)
        {
            return new Vector4(
                bounds.MinU + run.StartColumn * grid.StepU,
                bounds.MinV + run.RowStart * grid.StepV,
                bounds.MinU + (run.EndColumn + 1) * grid.StepU,
                bounds.MinV + (run.RowEnd + 1) * grid.StepV);
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

        private void RebuildContourOwnedWallDamageContourMesh(List<ContourSegment2D> visibleSegments, List<ContourSegment2D> backSegments)
        {
            if (damageContourMeshFilter == null)
            {
                return;
            }

            var hasFront = visibleSegments != null && visibleSegments.Count > 0;
            var hasBack = backSegments != null && backSegments.Count > 0;
            if (!UsesContourOwnedWallDamage() || (!hasFront && !hasBack))
            {
                damageContourMeshFilter.sharedMesh = null;
                return;
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            AddContourOwnedWallDamageRimSegments(vertices, triangles, visibleSegments, backSegments);
            damageContourMeshFilter.sharedMesh = vertices.Count == 0
                ? null
                : CreateMesh("Destructible Wall Damage Contour Mesh", vertices, new[] { triangles });
        }

        private void AddContourOwnedWallDamageRimSegments(
            List<Vector3> vertices,
            List<int> triangles,
            List<ContourSegment2D> visibleSegments,
            List<ContourSegment2D> backSegments)
        {
            if (!TryGetContourOwnedWallBasis(0f, out _, out _, out _, out _, out var bounds))
            {
                return;
            }

            var sliceCount = CalculateTunnelBridgeSliceCount();
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

                AddThroughThicknessDamageRimSegment(vertices, triangles, segment, segment.Start, segment.End, bounds, sliceCount);
                AddThroughThicknessDamageRimSegment(vertices, triangles, segment, segment.End, segment.Start, bounds, sliceCount);
            }

            // The back-face ring uses its own union: segment points are already in the back
            // face's world UV (front polygon plus each tunnel's exit offset).
            for (var i = 0; i < backSegments.Count; i++)
            {
                var segment = backSegments[i];
                var opposite = segment.Stamp != null ? segment.Stamp.Opposite : null;
                if (opposite == null)
                {
                    continue;
                }

                AddSingleSidedContourSegment(
                    vertices,
                    triangles,
                    DamageStampWorldPointToLocal(opposite, segment.Start, WallDamageRimDepthBias),
                    DamageStampWorldPointToLocal(opposite, segment.End, WallDamageRimDepthBias),
                    opposite.Normal,
                    WallDamageRimThickness);
            }
        }

        private void AddThroughThicknessDamageRimSegment(
            List<Vector3> vertices,
            List<int> triangles,
            ContourSegment2D segment,
            Vector2 point,
            Vector2 tangentPoint,
            DamageComponentPlaneBounds bounds,
            int sliceCount)
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
            var wallDepth = (stamp.Plane - DamageContourInset) * 2f;
            if (wallDepth <= 0.0001f)
            {
                return;
            }

            // Draw the strip in depth slices and drop the parts that run through another
            // tunnel or slide past the piece bounds, so merged holes do not get stray rim
            // lines crossing their openings or trailing outside the visible faces.
            for (var slice = 0; slice < sliceCount; slice++)
            {
                var depthNear = wallDepth * slice / sliceCount;
                var depthFar = wallDepth * (slice + 1) / sliceCount;
                var depthMid = (depthNear + depthFar) * 0.5f;
                var midUv = point + stamp.ShearPerDepth * depthMid;
                if (midUv.x < bounds.MinU - 0.01f ||
                    midUv.x > bounds.MaxU + 0.01f ||
                    midUv.y < bounds.MinV - 0.01f ||
                    midUv.y > bounds.MaxV + 0.01f)
                {
                    continue;
                }

                if (IsWorldUvInsideOtherStampAtDepth(midUv, stamp, depthMid))
                {
                    continue;
                }

                var near = TunnelWorldUvToLocal(stamp, point + stamp.ShearPerDepth * depthNear, depthNear, wallDepth);
                var far = TunnelWorldUvToLocal(stamp, point + stamp.ShearPerDepth * depthFar, depthFar, wallDepth);
                var through = far - near;
                if (through.sqrMagnitude <= 0.000001f)
                {
                    continue;
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

                AddQuadOriented(vertices, triangles, near - halfWidth, far - halfWidth, far + halfWidth, near + halfWidth, outward);
                AddQuadOriented(vertices, triangles, near - halfWidth, near + halfWidth, far + halfWidth, far - halfWidth, -outward);
            }
        }

        private bool IsWorldUvInsideOtherStampAtDepth(Vector2 worldUv, DamageStamp owner, float depth)
        {
            for (var i = 0; i < wallDamageStamps.Count; i++)
            {
                var stamp = wallDamageStamps[i];
                if (stamp == null || stamp == owner)
                {
                    continue;
                }

                var corrected = worldUv - stamp.ShearPerDepth * depth;
                if (corrected.x >= stamp.Min.x &&
                    corrected.x <= stamp.Max.x &&
                    corrected.y >= stamp.Min.y &&
                    corrected.y <= stamp.Max.y &&
                    IsPointInPolygon(corrected, stamp.Points))
                {
                    return true;
                }
            }

            return false;
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

            // The two pillar axis slabs share one box; each face must be rendered by exactly one
            // slab or an untouched sibling face hides the other slab's holes. Lateral sides are
            // the sibling's main faces, and the slab whose U axis is vertical owns top/bottom.
            var addUSides = true;
            var addVSides = true;
            if (pillarAxisSlabMode)
            {
                addUSides = Mathf.Abs(Vector3.Dot(u, Vector3.up)) > 0.5f;
                addVSides = false;
            }

            if (addUSides)
            {
                AddContourOwnedWallFullOuterSide(vertices, triangles, normal, u, v, halfN, bounds.MinU, bounds.MinV, bounds.MaxV, true, -u);
                AddContourOwnedWallFullOuterSide(vertices, triangles, normal, u, v, halfN, bounds.MaxU, bounds.MinV, bounds.MaxV, true, u);
            }

            if (addVSides)
            {
                AddContourOwnedWallFullOuterSide(vertices, triangles, normal, u, v, halfN, bounds.MinV, bounds.MinU, bounds.MaxU, false, -v);
                AddContourOwnedWallFullOuterSide(vertices, triangles, normal, u, v, halfN, bounds.MaxV, bounds.MinU, bounds.MaxU, false, v);
            }
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

            return BuildClippedVisibleContourSegments(bounds, thickness, true, false, 0f);
        }

        // Sheared tunnels exit the back face offset from where they entered, so the back-face
        // hole union must be computed at full material depth rather than reusing the front one.
        private List<ContourSegment2D> GetVisibleContourOwnedWallBackSegments(float thickness)
        {
            if (wallDamageStamps.Count == 0 ||
                !TryGetContourOwnedWallBasis(0f, out _, out _, out _, out var halfN, out var bounds))
            {
                return new List<ContourSegment2D>();
            }

            return BuildClippedVisibleContourSegments(bounds, thickness, true, false, halfN * 2f);
        }

        private List<ContourSegment2D> GetContourOwnedWallBridgeSegments(float thickness)
        {
            if (wallDamageStamps.Count == 0 ||
                !TryGetContourOwnedWallBasis(0f, out _, out _, out _, out _, out var bounds))
            {
                return new List<ContourSegment2D>();
            }

            return BuildClippedVisibleContourSegments(bounds, thickness, true, true, 0f);
        }

        private List<ContourSegment2D> BuildClippedVisibleContourSegments(
            DamageComponentPlaneBounds bounds,
            float thickness,
            bool removeSmallInteriorIslands,
            bool includeNonContourOwners,
            float depth)
        {
            var result = new List<ContourSegment2D>();
            var rawSegments = BuildStampUnionDamageSegments(wallDamageStamps, thickness, includeNonContourOwners, removeSmallInteriorIslands, depth);
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
            var segmentOffset = transform.TransformDirection(Vector3.right) * (chunk.BaseScale.x + 0.05f);
            SpawnFallingDebrisObject(mesh, null, null, transform.TransformPoint(centerLocal), transform.rotation, CalculatePillarBiteSeed(chunk, 0), Vector3.zero, segmentOffset);
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
            return BuildStampUnionDamageSegments(stamps, thickness, includeNonContourOwners, removeSmallInteriorIslands, 0f);
        }

        // depth measures into the material from the front (+normal) face; sheared tunnels slide
        // their cross-sections per unit depth, so the union is evaluated in the world UV of that
        // depth and the returned segments live in those coordinates.
        private List<ContourSegment2D> BuildStampUnionDamageSegments(
            List<DamageStamp> stamps,
            float thickness,
            bool includeNonContourOwners,
            bool removeSmallInteriorIslands,
            float depth)
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

                var ownerOffset = stamp.ShearPerDepth * depth;
                var edgeCount = stamp.RenderClosed ? stamp.Points.Length : stamp.Points.Length - 2;
                for (var i = 0; i < edgeCount; i++)
                {
                    var next = stamp.RenderClosed ? (i + 1) % stamp.Points.Length : i + 1;
                    AddClippedStampEdge(
                        segments,
                        stamps,
                        stampIndex,
                        stamp,
                        stamp.Points[i] + ownerOffset,
                        stamp.Points[next] + ownerOffset,
                        depth);
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

        // Builds the through-material tunnel walls (including severance cut faces) as depth
        // slices, each clipped against the hole union evaluated at that depth. This keeps walls
        // of merged tunnels with different shot directions from cutting through one another and
        // lets a structural cut face inherit holes where slanted tunnels cross it.
        private void AddSlicedContourInteriorBridge(List<Vector3> vertices, List<int> triangles, float thickness)
        {
            if (wallDamageStamps.Count == 0 ||
                !TryGetContourOwnedWallBasis(0f, out _, out _, out _, out var halfN, out var bounds))
            {
                return;
            }

            var wallDepth = halfN * 2f;
            if (wallDepth <= 0.0001f)
            {
                return;
            }

            var sliceCount = CalculateTunnelBridgeSliceCount();
            for (var slice = 0; slice < sliceCount; slice++)
            {
                var depthNear = wallDepth * slice / sliceCount;
                var depthFar = wallDepth * (slice + 1) / sliceCount;
                var depthMid = (depthNear + depthFar) * 0.5f;
                // Keep regular union segments (per-slice "small island" filtering deletes
                // whole wall bands, leaving see-through gaps), but cull tiny isolated rings:
                // those are merge tangency artifacts that would render as floating matte
                // specks — anything that small and real has already been absorbed.
                var segments = BuildStampUnionDamageSegments(wallDamageStamps, thickness, true, false, depthMid);
                RemoveTinyBridgeSegmentGroups(segments, thickness);
                for (var i = 0; i < segments.Count; i++)
                {
                    var segment = segments[i];
                    if (segment.Stamp == null ||
                        !TryClipSegmentToBounds(segment.Start, segment.End, bounds, out var start, out var end))
                    {
                        continue;
                    }

                    var shear = segment.Stamp.ShearPerDepth;
                    AddInteriorBridgeSegment(
                        vertices,
                        triangles,
                        TunnelWorldUvToLocal(segment.Stamp, start + shear * (depthNear - depthMid), depthNear, wallDepth),
                        TunnelWorldUvToLocal(segment.Stamp, end + shear * (depthNear - depthMid), depthNear, wallDepth),
                        TunnelWorldUvToLocal(segment.Stamp, start + shear * (depthFar - depthMid), depthFar, wallDepth),
                        TunnelWorldUvToLocal(segment.Stamp, end + shear * (depthFar - depthMid), depthFar, wallDepth));
                }
            }
        }

        private void RemoveTinyBridgeSegmentGroups(List<ContourSegment2D> segments, float thickness)
        {
            if (segments.Count == 0)
            {
                return;
            }

            var groups = BuildContourSegmentGroups(segments, Mathf.Max(0.002f, thickness * 0.35f));
            List<int> doomedSegments = null;
            for (var i = 0; i < groups.Count; i++)
            {
                var span = groups[i].Span;
                if (Mathf.Max(span.x, span.y) > BridgeSliceArtifactMaxSpan)
                {
                    continue;
                }

                doomedSegments ??= new List<int>();
                doomedSegments.AddRange(groups[i].SegmentIndexes);
            }

            if (doomedSegments == null)
            {
                return;
            }

            doomedSegments.Sort();
            for (var i = doomedSegments.Count - 1; i >= 0; i--)
            {
                segments.RemoveAt(doomedSegments[i]);
            }
        }

        private int CalculateTunnelBridgeSliceCount()
        {
            for (var i = 0; i < wallDamageStamps.Count; i++)
            {
                var stamp = wallDamageStamps[i];
                if (stamp != null && stamp.ShearPerDepth.sqrMagnitude > 0.000001f)
                {
                    return ShearedTunnelBridgeSlices;
                }
            }

            return 1;
        }

        private static Vector3 TunnelWorldUvToLocal(DamageStamp stamp, Vector2 worldUv, float depth, float wallDepth)
        {
            var planeCoordinate = Mathf.Lerp(stamp.Plane, -stamp.Plane, depth / wallDepth);
            return stamp.Normal * planeCoordinate + stamp.U * worldUv.x + stamp.V * worldUv.y;
        }

        private static Vector3 DamageStampWorldPointToLocal(DamageStamp stamp, Vector2 worldPoint, float planeOffset)
        {
            return stamp.Normal * (stamp.Plane + planeOffset) + stamp.U * worldPoint.x + stamp.V * worldPoint.y;
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

        private void AddClippedStampEdge(List<ContourSegment2D> segments, List<DamageStamp> stamps, int ownerIndex, DamageStamp ownerStamp, Vector2 start, Vector2 end, float depth)
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
                    other.Points.Length < 3)
                {
                    continue;
                }

                // Shift the edge into the other stamp's front-polygon frame at this depth; the
                // parametric cuts are unchanged by the translation.
                var otherOffset = other.ShearPerDepth * depth;
                if (!BoundsOverlap(edgeMin, edgeMax, other.Min + otherOffset, other.Max + otherOffset))
                {
                    continue;
                }

                AddSegmentPolygonIntersectionCuts(start - otherOffset, end - otherOffset, other.Points, cuts);
            }

            cuts.Sort();
            var edgeLength = (end - start).magnitude;
            for (var i = 0; i < cuts.Count - 1; i++)
            {
                var t0 = cuts[i];
                var t1 = cuts[i + 1];
                if (t1 - t0 <= 0.0001f)
                {
                    continue;
                }

                // Slivers where jagged closed polygons graze each other render as floating
                // dust — drop them outright. Open stamps are exempt: their deliberately tiny
                // "line shard" fragments are managed by the open-contour shard pipeline.
                if (ownerStamp.RenderClosed && edgeLength * (t1 - t0) <= MinContourSegmentLength)
                {
                    continue;
                }

                var midpoint = Vector2.Lerp(start, end, (t0 + t1) * 0.5f);
                if (IsPointInsideOtherDamageStamp(midpoint, stamps, ownerIndex, depth))
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

        private bool IsPointInsideOtherDamageStamp(Vector2 point, List<DamageStamp> stamps, int ownerIndex, float depth)
        {
            for (var i = 0; i < stamps.Count; i++)
            {
                if (i == ownerIndex)
                {
                    continue;
                }

                var stamp = stamps[i];
                if (stamp == null)
                {
                    continue;
                }

                var corrected = point - stamp.ShearPerDepth * depth;
                if (corrected.x >= stamp.Min.x &&
                    corrected.x <= stamp.Max.x &&
                    corrected.y >= stamp.Min.y &&
                    corrected.y <= stamp.Max.y &&
                    IsPointInPolygon(corrected, stamp.Points))
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
            return stamp.Normal * stamp.Plane + stamp.U * (point.x + stamp.UvOffset.x) + stamp.V * (point.y + stamp.UvOffset.y);
        }

        private static Vector3 DamageStampPointToLocal(DamageStamp stamp, Vector2 point, float planeOffset)
        {
            return stamp.Normal * (stamp.Plane + planeOffset) + stamp.U * (point.x + stamp.UvOffset.x) + stamp.V * (point.y + stamp.UvOffset.y);
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
