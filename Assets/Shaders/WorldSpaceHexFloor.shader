Shader "ArenaShooter/WorldSpaceHexFloor"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.0015, 0.001, 0.004, 1)
        _LineColor ("Line Color", Color) = (0.85, 0.018, 0.012, 1)
        _HexSize ("Hex Size", Float) = 0.7
        _LineWidth ("Line Width", Float) = 0.007
        _EmissionStrength ("Emission Strength", Float) = 0.45
        _PulseSpeed ("Pulse Speed", Float) = 0
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0
        _PatternOrigin ("Pattern Origin", Vector) = (0, 0, 0, 0)
        _NormalFadeStart ("Normal Fade Start", Range(0, 1)) = 0.18
        _NormalFadeEnd ("Normal Fade End", Range(0, 1)) = 0.42
        _WaveDirection ("Wave Direction", Vector) = (0.85, 0.48, 0, 0)
        _WaveSpeed ("Wave Speed", Float) = 16
        _WavePeriod ("Wave Period", Float) = 240
        _WaveTravelSpan ("Wave Travel Span", Float) = 192
        _WaveRestartGap ("Wave Restart Gap", Float) = 48
        _WaveWidth ("Wave Width", Float) = 6
        _WaveSoftness ("Wave Softness", Float) = 9
        _IdleLineStrength ("Idle Line Strength", Range(0, 1)) = 0
        _WaveLineStrength ("Wave Line Strength", Range(0, 2)) = 0.78
        _HillIdleLineStrength ("Hill Idle Line Strength", Range(0, 1)) = 0.16
        _HillIdleHeightStart ("Hill Idle Height Start", Float) = 0.12
        _HillIdleHeightEnd ("Hill Idle Height End", Float) = 1.15
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            #define _SPECULARHIGHLIGHTS_OFF 1
            #define _ENVIRONMENTREFLECTIONS_OFF 1

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                half4 _WaveDirection;
                half _WaveSpeed;
                half _WavePeriod;
                half _WaveTravelSpan;
                half _WaveRestartGap;
                half _WaveWidth;
                half _WaveSoftness;
                half _IdleLineStrength;
                half _WaveLineStrength;
                half _HillIdleLineStrength;
                half _HillIdleHeightStart;
                half _HillIdleHeightEnd;
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

                float2 waveDirection = _WaveDirection.xy;
                waveDirection *= rsqrt(max(dot(waveDirection, waveDirection), 0.0001));
                float waveWidth = max((float)_WaveWidth, 0.001);
                float waveSoftness = max((float)_WaveSoftness, 0.001);
                float waveTravelSpan = max((float)_WaveTravelSpan, waveWidth * 2.0 + waveSoftness * 2.0 + 0.001);
                float waveRestartGap = max((float)_WaveRestartGap, 0.0);
                float wavePeriod = max((float)_WavePeriod, waveTravelSpan + waveRestartGap + 0.001);
                float cyclePosition = frac(_Time.y * (float)_WaveSpeed / wavePeriod) * wavePeriod;
                float activeWave = 1.0 - step(waveTravelSpan, cyclePosition);
                float waveCenter = cyclePosition - waveTravelSpan * 0.5;
                float waveDistance = abs(dot(input.positionWS.xz - _PatternOrigin.xz, waveDirection) - waveCenter);
                float waveCore = 1.0 - smoothstep(0.0, waveWidth, waveDistance);
                float waveShoulder = 1.0 - smoothstep(0.0, waveWidth + waveSoftness, waveDistance);
                float waveMask = saturate(max(waveCore, waveShoulder * 0.42) * activeWave);
                float hillIdleMask = smoothstep((float)_HillIdleHeightStart, max((float)_HillIdleHeightEnd, (float)_HillIdleHeightStart + 0.001), input.positionWS.y);
                float idleLineIntensity = max((float)_IdleLineStrength, hillIdleMask * (float)_HillIdleLineStrength);
                float lineIntensity = saturate(max(idleLineIntensity, waveMask * _WaveLineStrength));
                half3 lineColor = _LineColor.rgb * lineIntensity * (1.0h + _EmissionStrength * lineIntensity);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionHCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = ComputeFogFactor(input.positionHCS.z);
                inputData.vertexLighting = half3(0.0h, 0.0h, 0.0h);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionHCS);
                inputData.shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = _BaseColor.rgb;
                surfaceData.specular = half3(0.0h, 0.0h, 0.0h);
                surfaceData.metallic = 0.0h;
                surfaceData.smoothness = 0.0h;
                surfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);
                surfaceData.emission = lineColor * saturate(lineMask);
                surfaceData.occlusion = 1.0h;
                surfaceData.alpha = 1.0h;
                surfaceData.clearCoatMask = 0.0h;
                surfaceData.clearCoatSmoothness = 0.0h;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }
    }
}
