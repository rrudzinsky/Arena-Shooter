Shader "ArenaShooter/WorldSpaceHexFloor"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.0015, 0.001, 0.004, 1)
        _LineColor ("Line Color", Color) = (1.65, 0.025, 0.015, 1)
        _HexSize ("Hex Size", Float) = 0.7
        _LineWidth ("Line Width", Float) = 0.007
        _EmissionStrength ("Emission Strength", Float) = 1.35
        _PulseSpeed ("Pulse Speed", Float) = 0
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0
        _PatternOrigin ("Pattern Origin", Vector) = (0, 0, 0, 0)
        _NormalFadeStart ("Normal Fade Start", Range(0, 1)) = 0.18
        _NormalFadeEnd ("Normal Fade End", Range(0, 1)) = 0.42
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "World Space Hex Floor"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _LineColor;
                half _HexSize;
                half _LineWidth;
                half _EmissionStrength;
                half _PulseSpeed;
                half _PulseStrength;
                half4 _PatternOrigin;
                half _NormalFadeStart;
                half _NormalFadeEnd;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

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
                float3 normalWS = normalize(input.normalWS);
                float upward = saturate(dot(normalWS, float3(0.0, 1.0, 0.0)));
                float surfaceMask = smoothstep(_NormalFadeStart, _NormalFadeEnd, upward);

                float edgeDistance = HexEdgeDistance(input.positionWS.xz);
                float antiAlias = max(fwidth(edgeDistance), 0.0015);
                float lineMask = 1.0 - smoothstep(_LineWidth, _LineWidth + antiAlias * 1.25, edgeDistance);
                lineMask *= surfaceMask;

                float pulsePhase = sin(_Time.y * _PulseSpeed + dot(input.positionWS.xz, float2(0.071, 0.043))) * 0.5 + 0.5;
                float pulse = 1.0 + pulsePhase * _PulseStrength;
                half3 lineColor = _LineColor.rgb * (1.0h + _EmissionStrength * pulse);
                half3 color = lerp(_BaseColor.rgb, lineColor, saturate(lineMask));
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
