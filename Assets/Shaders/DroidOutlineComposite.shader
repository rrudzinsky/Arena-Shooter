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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D_X(_DroidOutlineMaskTex);
            TEXTURE2D_X(_DroidOutlineWeaponOccluderTex);
            float4 _DroidOutlineMaskTexelSize;
            float4 _OutlineColor;
            float4 _OutlineParams;
            float4 _OutlineStyleParams;
            float4 _OutlineDistanceParams;
            float4 _OutlineFlowParams;
            float4 _OutlineFlowParams2;
            int _FlowEnabled;
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

            // Smooth, slowly drifting bend field. Nudges where the mask is sampled so the
            // lines themselves undulate (the "alive" wiggle). Pure nested sines: no hash
            // noise, no per-pixel randomness, so thin lines can never sparkle or speckle.
            float2 LifeWiggleOffset(float2 pixel, float t, float wiggleSpeed)
            {
                float2 bend;
                bend.x = sin(pixel.y * 0.057 + t * wiggleSpeed * 5.3 + sin(pixel.x * 0.041 + t * 1.9));
                bend.y = sin(pixel.x * 0.049 - t * wiggleSpeed * 4.1 + sin(pixel.y * 0.045 - t * 1.4) + 2.13);
                return bend;
            }

            // Organic stream of brightness travelling along the line. Two incommensurate
            // octaves with a nested phase warp read as liquid rather than a stripe train.
            float LiquidPattern(float ph, float drift)
            {
                float liquid = sin(ph - drift + 1.9 * sin(ph * 0.317 + drift * 0.43));
                liquid += 0.55 * sin(ph * 2.137 + drift * 0.61 + 1.3);
                return saturate(liquid * 0.3226 + 0.5);
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

                float2 maskUv = uv;
                float radiusBreath = 1.0;
                half flowBrightness = 1.0h;
                half flowHot = 0.0h;

                if (_FlowEnabled != 0)
                {
                    float2 texel = _DroidOutlineMaskTexelSize.xy;
                    float2 pixel = uv * _DroidOutlineMaskTexelSize.zw;
                    float t = _Time.y;
                    float gradRadius = max(1.5, _OutlineParams.x + 0.5);

                    half4 maskR = SAMPLE_TEXTURE2D_X(_DroidOutlineMaskTex, sampler_PointClamp, uv + float2(texel.x, 0.0) * gradRadius);
                    half4 maskL = SAMPLE_TEXTURE2D_X(_DroidOutlineMaskTex, sampler_PointClamp, uv - float2(texel.x, 0.0) * gradRadius);
                    half4 maskU = SAMPLE_TEXTURE2D_X(_DroidOutlineMaskTex, sampler_PointClamp, uv + float2(0.0, texel.y) * gradRadius);
                    half4 maskD = SAMPLE_TEXTURE2D_X(_DroidOutlineMaskTex, sampler_PointClamp, uv - float2(0.0, texel.y) * gradRadius);

                    // Silhouette pixels: tangent from the occupancy gradient. Interior crease
                    // pixels have uniform occupancy, so fall back to the normal-field gradient.
                    float2 occupancyGrad = float2(
                        Occupancy(maskR.a) - Occupancy(maskL.a),
                        Occupancy(maskU.a) - Occupancy(maskD.a));
                    float2 normalGrad = float2(
                        distance(maskR.rgb * 2.0h - 1.0h, maskL.rgb * 2.0h - 1.0h),
                        distance(maskU.rgb * 2.0h - 1.0h, maskD.rgb * 2.0h - 1.0h));
                    bool hasOccupancyGrad = dot(occupancyGrad, occupancyGrad) > 0.05;
                    float2 grad = hasOccupancyGrad ? occupancyGrad : normalGrad;
                    float2 tangent = normalize(float2(-grad.y, grad.x) + float2(1e-4, 2e-4));

                    // Close-range gate from scene depth: droid depth is the nearer of the
                    // pixel itself and its occupied side, so silhouette halo pixels (whose
                    // own depth is the background) still gate off the droid's distance.
                    float2 towardBody = hasOccupancyGrad ? normalize(occupancyGrad) : float2(0.0, 0.0);
                    float2 depthUv = uv + towardBody * texel * gradRadius;
                    float eyeDepth = min(
                        LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams),
                        LinearEyeDepth(SampleSceneDepth(depthUv), _ZBufferParams));
                    half closeWeight = (half)(1.0 - smoothstep(_OutlineFlowParams2.z, _OutlineFlowParams2.w, eyeDepth));

                    if (closeWeight > 0.001h)
                    {
                        float wavelength = max(8.0, _OutlineFlowParams.z);
                        float k = 6.2831853 / wavelength;
                        float ph = dot(pixel, tangent) * k;
                        float drift = t * _OutlineFlowParams.y * k;
                        float pattern = LiquidPattern(ph, drift);

                        half flowStrength = (half)_OutlineFlowParams.x * closeWeight;
                        flowBrightness = 1.0h + flowStrength * (half)(pattern * 2.0 - 1.0);
                        flowHot = (half)smoothstep(0.78, 0.97, pattern) * (half)_OutlineFlowParams2.y * closeWeight;
                        radiusBreath = 1.0 + 0.14 * closeWeight * sin(ph * 0.53 - drift * 0.8 + 2.3);
                        maskUv = uv + LifeWiggleOffset(pixel, t, _OutlineFlowParams2.x) *
                            (_OutlineFlowParams.w * closeWeight) * texel;
                    }
                }

                half2 hardEdgeComponents = EdgeAtRadius(maskUv, _OutlineParams.x * radiusBreath, (half)_OutlineDistanceParams.z, 0.58h);
                half hardEdge = saturate(
                    hardEdgeComponents.x * (half)_OutlineStyleParams.w +
                    hardEdgeComponents.y * (half)_OutlineStyleParams.x);
                half2 glowEdgeComponents = max(
                    EdgeAtRadius(maskUv, (_OutlineParams.x + 1.0) * radiusBreath, (half)_OutlineDistanceParams.w, 0.22h),
                    EdgeAtRadius(maskUv, _OutlineParams.y * radiusBreath, (half)_OutlineDistanceParams.w, 0.22h));
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
                if (_FlowEnabled != 0)
                {
                    outline *= flowBrightness;
                    outline += lerp(_OutlineColor.rgb, half3(1.55h, 1.42h, 1.05h), 0.5h) * (flowHot * glow);
                }

                return half4(sceneColor.rgb + outline, 1.0h);
            }
            ENDHLSL
        }
    }
}
