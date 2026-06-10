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
            TEXTURE2D_X(_DroidOutlineWeaponOccluderTex);
            float4 _DroidOutlineMaskTexelSize;
            float4 _OutlineColor;
            float4 _OutlineParams;
            float4 _OutlineStyleParams;
            float4 _OutlineDistanceParams;
            int _DiagnosticMode;
            int _ApplyMatteScene;
            int _SuppressByWeaponOccluder;
            int _OutsideOnlyAlphaEdge;

            half Occupancy(half maskAlpha)
            {
                return step(0.01h, maskAlpha);
            }

            half DistanceWeight(half maskAlpha)
            {
                return saturate((maskAlpha - 0.25h) / 0.75h);
            }

            half WeaponOccupancyAt(float2 uv)
            {
                return Occupancy(SAMPLE_TEXTURE2D_X(_DroidOutlineWeaponOccluderTex, sampler_PointClamp, uv).a);
            }

            half WeaponOccluderTouchesRadius(float2 uv, float radius)
            {
                float2 texel = _DroidOutlineMaskTexelSize.xy * radius;
                half occupancy = WeaponOccupancyAt(uv);
                occupancy = max(occupancy, WeaponOccupancyAt(uv + float2(texel.x, 0.0)));
                occupancy = max(occupancy, WeaponOccupancyAt(uv + float2(-texel.x, 0.0)));
                occupancy = max(occupancy, WeaponOccupancyAt(uv + float2(0.0, texel.y)));
                occupancy = max(occupancy, WeaponOccupancyAt(uv + float2(0.0, -texel.y)));
                occupancy = max(occupancy, WeaponOccupancyAt(uv + texel));
                occupancy = max(occupancy, WeaponOccupancyAt(uv - texel));
                occupancy = max(occupancy, WeaponOccupancyAt(uv + float2(texel.x, -texel.y)));
                occupancy = max(occupancy, WeaponOccupancyAt(uv + float2(-texel.x, texel.y)));
                return occupancy;
            }

            half WeaponOccluderTouchesOutlineNeighborhood(float2 uv)
            {
                half occupancy = WeaponOccluderTouchesRadius(uv, _OutlineParams.x);
                occupancy = max(occupancy, WeaponOccluderTouchesRadius(uv, _OutlineParams.x + 1.0));
                occupancy = max(occupancy, WeaponOccluderTouchesRadius(uv, _OutlineParams.y));
                return occupancy;
            }

            half2 DistanceScaledEdge(float2 uv, float2 offset, half minRadiusScale, half minIntensity)
            {
                half4 center = SAMPLE_TEXTURE2D_X(_DroidOutlineMaskTex, sampler_PointClamp, uv);
                half4 radiusProbe = SAMPLE_TEXTURE2D_X(_DroidOutlineMaskTex, sampler_PointClamp, uv + offset);
                half radiusWeight = max(DistanceWeight(center.a), DistanceWeight(radiusProbe.a));
                float2 scaledOffset = offset * lerp((float)minRadiusScale, 1.0, (float)radiusWeight);
                half4 sampleValue = SAMPLE_TEXTURE2D_X(_DroidOutlineMaskTex, sampler_PointClamp, uv + scaledOffset);

                half centerOccupancy = Occupancy(center.a);
                half sampleOccupancy = Occupancy(sampleValue.a);
                half twoSidedAlphaEdge = abs(centerOccupancy - sampleOccupancy);
                half outsideOnlyAlphaEdge = (1.0h - centerOccupancy) * sampleOccupancy;
                half alphaEdge = _OutsideOnlyAlphaEdge != 0 ? outsideOnlyAlphaEdge : twoSidedAlphaEdge;

                half3 centerNormal = center.rgb * 2.0h - 1.0h;
                half3 sampleNormal = sampleValue.rgb * 2.0h - 1.0h;
                half normalEdge = step((half)_OutlineParams.z, distance(centerNormal, sampleNormal)) *
                    centerOccupancy * sampleOccupancy;
                half edgeWeight = lerp(minIntensity, 1.0h, max(DistanceWeight(center.a), DistanceWeight(sampleValue.a)));

                return half2(alphaEdge, normalEdge) * edgeWeight;
            }

            half2 EdgeAtRadius(float2 uv, float radius, half minRadiusScale, half minIntensity)
            {
                float2 texel = _DroidOutlineMaskTexelSize.xy * radius;
                half2 edge = half2(0.0h, 0.0h);
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

                if (_SuppressByWeaponOccluder != 0)
                {
                    half weaponOccupancy = WeaponOccluderTouchesOutlineNeighborhood(uv);
                    if (weaponOccupancy > 0.0h)
                    {
                        return sceneColor;
                    }
                }

                half2 hardEdgeComponents = EdgeAtRadius(uv, _OutlineParams.x, (half)_OutlineDistanceParams.z, 0.58h);
                half hardEdge = saturate(
                    hardEdgeComponents.x * (half)_OutlineStyleParams.w +
                    hardEdgeComponents.y * (half)_OutlineStyleParams.x);
                half2 glowEdgeComponents = max(
                    EdgeAtRadius(uv, _OutlineParams.x + 1.0, (half)_OutlineDistanceParams.w, 0.22h),
                    EdgeAtRadius(uv, _OutlineParams.y, (half)_OutlineDistanceParams.w, 0.22h));
                half glowEdge = saturate(
                    glowEdgeComponents.x * (half)_OutlineStyleParams.w +
                    glowEdgeComponents.y * (half)_OutlineStyleParams.x);
                half glow = saturate(hardEdge * (half)_OutlineStyleParams.z + glowEdge * (half)_OutlineStyleParams.y);

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
