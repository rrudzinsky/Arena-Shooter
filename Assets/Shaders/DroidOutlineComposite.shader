Shader "Hidden/ArenaShooter/DroidOutlineComposite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "DroidOutlineComposite"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_DroidOutlineMaskTex);
            float4 _DroidOutlineMaskTexelSize;
            float4 _OutlineColor;
            float4 _OutlineParams;
            float4 _OutlineDistanceParams;
            int _DiagnosticMode;
            int _ApplyMatteScene;

            half Occupancy(half maskAlpha)
            {
                return step(0.01h, maskAlpha);
            }

            half DistanceWeight(half maskAlpha)
            {
                return saturate((maskAlpha - 0.25h) / 0.75h);
            }

            half DistanceScaledEdge(float2 uv, float2 offset, half minRadiusScale, half minIntensity)
            {
                half4 center = SAMPLE_TEXTURE2D_X(_DroidOutlineMaskTex, sampler_PointClamp, uv);
                half4 radiusProbe = SAMPLE_TEXTURE2D_X(_DroidOutlineMaskTex, sampler_PointClamp, uv + offset);
                half radiusWeight = max(DistanceWeight(center.a), DistanceWeight(radiusProbe.a));
                float2 scaledOffset = offset * lerp((float)minRadiusScale, 1.0, (float)radiusWeight);
                half4 sampleValue = SAMPLE_TEXTURE2D_X(_DroidOutlineMaskTex, sampler_PointClamp, uv + scaledOffset);

                half centerOccupancy = Occupancy(center.a);
                half sampleOccupancy = Occupancy(sampleValue.a);
                half alphaEdge = abs(centerOccupancy - sampleOccupancy);

                half3 centerNormal = center.rgb * 2.0h - 1.0h;
                half3 sampleNormal = sampleValue.rgb * 2.0h - 1.0h;
                half normalEdge = step((half)_OutlineParams.z, distance(centerNormal, sampleNormal)) * centerOccupancy * sampleOccupancy;
                half edgeWeight = lerp(minIntensity, 1.0h, max(DistanceWeight(center.a), DistanceWeight(sampleValue.a)));

                return saturate(alphaEdge + normalEdge) * edgeWeight;
            }

            half EdgeAtRadius(float2 uv, float radius, half minRadiusScale, half minIntensity)
            {
                float2 texel = _DroidOutlineMaskTexelSize.xy * radius;
                half edge = 0.0h;
                edge = max(edge, DistanceScaledEdge(uv, float2(texel.x, 0.0), minRadiusScale, minIntensity));
                edge = max(edge, DistanceScaledEdge(uv, float2(-texel.x, 0.0), minRadiusScale, minIntensity));
                edge = max(edge, DistanceScaledEdge(uv, float2(0.0, texel.y), minRadiusScale, minIntensity));
                edge = max(edge, DistanceScaledEdge(uv, float2(0.0, -texel.y), minRadiusScale, minIntensity));
                edge = max(edge, DistanceScaledEdge(uv, texel, minRadiusScale, minIntensity));
                edge = max(edge, DistanceScaledEdge(uv, -texel, minRadiusScale, minIntensity));
                edge = max(edge, DistanceScaledEdge(uv, float2(texel.x, -texel.y), minRadiusScale, minIntensity));
                edge = max(edge, DistanceScaledEdge(uv, float2(-texel.x, texel.y), minRadiusScale, minIntensity));
                return edge;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord.xy;
                half4 sceneColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);

                half hardEdge = EdgeAtRadius(uv, _OutlineParams.x, (half)_OutlineDistanceParams.z, 0.58h);
                half glowEdge = max(
                    EdgeAtRadius(uv, _OutlineParams.x + 1.0, (half)_OutlineDistanceParams.w, 0.22h),
                    EdgeAtRadius(uv, _OutlineParams.y, (half)_OutlineDistanceParams.w, 0.22h));
                half glow = saturate(hardEdge + glowEdge * 0.32h);

                if (_DiagnosticMode == 2)
                {
                    return half4(1.0h, 0.0h, 1.0h, 1.0h);
                }

                if (_DiagnosticMode == 1)
                {
                    half4 mask = SAMPLE_TEXTURE2D_X(_DroidOutlineMaskTex, sampler_PointClamp, uv);
                    return half4(hardEdge, mask.a, glow, 1.0h);
                }

                half3 outline = _OutlineColor.rgb * glow * _OutlineParams.w;
                return half4(sceneColor.rgb + outline, 1.0h);
            }
            ENDHLSL
        }
    }
}
