Shader "ArenaShooter/DebrisImpactGlow"
{
    Properties
    {
        _GlowColor ("Glow Color", Color) = (1, 0.42, 0.08, 1)
        _LineColor ("Hex Line Color", Color) = (1, 0.12, 0.04, 1)
        _HexSize ("Hex Size", Float) = 0.45
        _LineWidth ("Line Width", Float) = 0.012
        _PatternOrigin ("Pattern Origin", Vector) = (0, 0, 0, 0)
        _Intensity ("Intensity", Range(0, 2)) = 1
        _FillStrength ("Fill Strength", Range(0, 2)) = 0.34
        _LineStrength ("Line Strength", Range(0, 4)) = 1.6
        _RippleStrength ("Ripple Strength", Range(0, 2)) = 0.5
        _RippleProgress ("Ripple Progress", Range(0, 1)) = 0
        _ImpactCenter ("Impact Center", Vector) = (0, 0, 0, 0)
        _FootprintRadius ("Footprint Radius", Float) = 0.6
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Debris Impact Glow"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float mask : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                half4 _LineColor;
                half _HexSize;
                half _LineWidth;
                half4 _PatternOrigin;
                half _Intensity;
                half _FillStrength;
                half _LineStrength;
                half _RippleStrength;
                half _RippleProgress;
                half4 _ImpactCenter;
                half _FootprintRadius;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.mask = input.uv.x;
                return output;
            }

            // Hex math mirrors ArenaShooter/WorldSpaceHexFloor exactly so the revealed
            // cells line up with the floor's own (mostly invisible) red hex pattern.
            float3 RoundHexCube(float3 cube)
            {
                float3 rounded = round(cube);
                float3 diff = abs(rounded - cube);
                if (diff.x > diff.y && diff.x > diff.z)
                {
                    rounded.x = -rounded.y - rounded.z;
                }
                else if (diff.y > diff.z)
                {
                    rounded.y = -rounded.x - rounded.z;
                }
                else
                {
                    rounded.z = -rounded.x - rounded.y;
                }

                return rounded;
            }

            float HexEdgeDistance(float2 worldPoint)
            {
                const float sqrt3 = 1.732050808;
                const float invSqrt3 = 0.577350269;
                const float inradiusFactor = 0.866025404;

                float size = max((float)_HexSize, 0.001);
                float2 patternPoint = worldPoint - _PatternOrigin.xz;
                float q = (invSqrt3 * patternPoint.x - patternPoint.y * 0.333333333) / size;
                float r = (patternPoint.y * 0.666666667) / size;
                float3 cube = float3(q, -q - r, r);
                float3 hex = RoundHexCube(cube);
                float2 center = float2(size * sqrt3 * (hex.x + hex.z * 0.5), size * 1.5 * hex.z);
                float2 local = patternPoint - center;
                float inradius = size * inradiusFactor;

                float edgeA = inradius - abs(local.x);
                float edgeB = inradius - abs(dot(local, float2(0.5, inradiusFactor)));
                float edgeC = inradius - abs(dot(local, float2(-0.5, inradiusFactor)));
                return min(edgeA, min(edgeB, edgeC));
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Vertex-baked footprint mask: 1 at the debris core, 0 at the soft rim.
                float falloff = smoothstep(0.0, 1.0, saturate(input.mask));

                float edgeDistance = HexEdgeDistance(input.positionWS.xz);
                float antiAlias = max(fwidth(edgeDistance), 0.0015);
                float lineMask = 1.0 - smoothstep(_LineWidth, _LineWidth + antiAlias * 1.25, edgeDistance);

                // Brief expanding shockwave ring, gone by the time the glow fades.
                float dist = length(input.positionWS.xz - _ImpactCenter.xz);
                float radius = max((float)_FootprintRadius, 0.001);
                float ringCenter = _RippleProgress * radius * 1.45;
                float ringWidth = radius * 0.24;
                float ring = (1.0 - smoothstep(0.0, ringWidth, abs(dist - ringCenter))) * (1.0 - _RippleProgress);

                // Additive: a faint translucent orange wash, bright red hex lines on top,
                // and the ripple. The floor itself stays untouched underneath.
                half3 color =
                    _GlowColor.rgb * (_FillStrength * pow(max(falloff, 0.0001), 1.5)) +
                    _LineColor.rgb * (_LineStrength * lineMask * pow(max(falloff, 0.0001), 0.6)) +
                    _GlowColor.rgb * (_RippleStrength * ring * falloff);
                return half4(color * _Intensity, 0.0h);
            }
            ENDHLSL
        }
    }
}
