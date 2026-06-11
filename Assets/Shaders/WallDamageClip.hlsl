#ifndef ARENA_WALL_DAMAGE_CLIP_INCLUDED
#define ARENA_WALL_DAMAGE_CLIP_INCLUDED

StructuredBuffer<float4> _WallDamageStampBounds;
StructuredBuffer<float4> _WallDamageStampPoints;
StructuredBuffer<int> _WallDamageStampPointOffsets;
StructuredBuffer<int> _WallDamageStampPointCounts;
StructuredBuffer<float4> _WallDamageStampShears;
int _WallDamageStampCount;
int _WallDamageClipEnabled;
float4 _WallDamageU;
float4 _WallDamageV;
float4 _WallDamageN;
// Secondary stamp basis: pillar axis slabs share one box, so each slab also clips against
// its sibling's stamps (flagged via _WallDamageStampShears.z) expressed in this basis.
float4 _WallDamageU2;
float4 _WallDamageV2;
float4 _WallDamageN2;

float2 ArenaWallDamageUV(float3 positionOS)
{
    return float2(dot(positionOS, _WallDamageU.xyz), dot(positionOS, _WallDamageV.xyz));
}

bool ArenaPointInsideWallDamageStamp(float2 sampleUv, int stampIndex)
{
    float4 bounds = _WallDamageStampBounds[stampIndex];
    if (sampleUv.x < bounds.x || sampleUv.x > bounds.z || sampleUv.y < bounds.y || sampleUv.y > bounds.w)
    {
        return false;
    }

    bool inside = false;
    int pointCount = _WallDamageStampPointCounts[stampIndex];
    if (pointCount < 3)
    {
        return false;
    }

    int pointBase = _WallDamageStampPointOffsets[stampIndex];
    float2 previous = _WallDamageStampPoints[pointBase + pointCount - 1].xy;
    for (int i = 0; i < pointCount; i++)
    {
        float2 current = _WallDamageStampPoints[pointBase + i].xy;
        float denominator = previous.y - current.y;
        if (((current.y > sampleUv.y) != (previous.y > sampleUv.y)) &&
            abs(denominator) > 0.000001 &&
            sampleUv.x < (previous.x - current.x) * (sampleUv.y - current.y) / denominator + current.x)
        {
            inside = !inside;
        }

        previous = current;
    }

    return inside;
}

bool ArenaPointInsideWallDamage(float2 sampleUv)
{
    for (int i = 0; i < _WallDamageStampCount; i++)
    {
        if (ArenaPointInsideWallDamageStamp(sampleUv, i))
        {
            return true;
        }
    }

    return false;
}

void ArenaClipWallDamage(float3 positionOS)
{
    if (_WallDamageClipEnabled == 0 || _WallDamageStampCount <= 0)
    {
        return;
    }

    // Stamp polygons live on the front (+normal) face of their basis; each stamp's shear
    // slides the hole cross-section across the thickness so angled shots carve angled tunnels.
    float2 uvPrimary = ArenaWallDamageUV(positionOS);
    float depthPrimary = _WallDamageN.w - dot(positionOS, _WallDamageN.xyz);
    float2 uvSecondary = float2(dot(positionOS, _WallDamageU2.xyz), dot(positionOS, _WallDamageV2.xyz));
    float depthSecondary = _WallDamageN2.w - dot(positionOS, _WallDamageN2.xyz);
    for (int i = 0; i < _WallDamageStampCount; i++)
    {
        float4 shear = _WallDamageStampShears[i];
        float2 uv = shear.z > 0.5 ? uvSecondary : uvPrimary;
        float depth = shear.z > 0.5 ? depthSecondary : depthPrimary;
        if (ArenaPointInsideWallDamageStamp(uv - shear.xy * depth, i))
        {
            clip(-1.0);
            return;
        }
    }
}

#endif
