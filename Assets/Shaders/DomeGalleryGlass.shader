Shader "ArenaShooter/DomeGalleryGlass"
{
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
            Name "Dome Gallery Glass"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 paneUV : TEXCOORD0;   // pane-local (u across, v up), 0..1
                float2 paneData : TEXCOORD1; // x unused, y = tier + ring phi01 packed as tier.frac
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 paneUV : TEXCOORD0;
                float2 paneData : TEXCOORD1;
            };

            // Per-column equalizer state streamed in by AllOutWarDomeScoreboard:
            // x = active tile count, y = brightness multiplier on the colour ramp.
            // Never set (menu, legacy modes) the array is all zeros and no bars draw.
            float4 _EqualizerBars[120];

            // Approximate world-space size of one pane; converts metres to pane UV.
            // Kept constant across tiers so the seam inset and corner radius stay a
            // constant FRACTION of the pane — and since every pane in a row subtends
            // the same angle from mid-arena, the seams look identical per row.
            static const float PaneWidth = 31.6;
            static const float PaneHeight = 61.0;

            // One window count shared by every tier (ShieldDomeBackdrop
            // GalleryTierWindowCounts) and the tier latitude spans (mirror of
            // GalleryTierBounds): six tiers from the dome base to the apex iris.
            static const float PaneCount = 36.0;
            static const float TierBounds[7] = { 0.0, 0.2495, 0.4548, 0.6081, 0.7222, 0.8110, 0.884 };

            // Dome geometry (mirror of AllOutWarStadiumVisuals): theta = lat * PI * 0.485,
            // flat radius = R * cos(theta) with R = 340.
            static const float DomeRadius = 340.0;
            static const float ThetaMax = 1.5236725;

            // Equalizer bar geometry, derived from the retired mesh bars that used
            // to sit on the dome shell (AllOutWarDomeScoreboard): 120 columns
            // around the ring, hollow tiles 8.2 m wide, rising from unit y -44
            // around centre latitude 0.22 at 226 dome units per latitude. Tiles
            // run 5.1 units tall with a 1.6-unit gap (the originals were 3.4/1.05)
            // and a 24-tile ceiling, so a max-height column climbs to latitude
            // ~0.74 — well up the bowl wall. Lines are 0.78 units for distance
            // legibility, and tile width tapers near the top where the columns
            // converge (fixed 8.2 m tiles would fuse into a solid band there).
            static const float ColumnCount = 120.0;
            static const float BarBaseLatitude = 0.22 - 44.0 / 226.0;
            static const float BarTilePitch = (5.1 + 1.6) / 226.0;
            static const float BarTileLatHeight = 5.1 / 226.0;
            static const float BarLineLat = 0.78 / 226.0;
            static const float BarHalfWidth = 4.1;
            static const float BarLineMeters = 0.78;

            float3 EqualizerRamp(float t)
            {
                // Mirror of AllOutWarDomeScoreboard.GetEqualizerColor: blue band,
                // then violet-magenta, red-orange, and amber-yellow.
                if (t < 0.24)
                {
                    return lerp(float3(0.02, 0.46, 1.65), float3(0.02, 0.9, 2.4), t / 0.24);
                }

                if (t < 0.52)
                {
                    return lerp(float3(0.42, 0.12, 2.2), float3(1.55, 0.06, 2.05), (t - 0.24) / 0.28);
                }

                if (t < 0.78)
                {
                    return lerp(float3(1.9, 0.03, 0.26), float3(2.0, 0.42, 0.04), (t - 0.52) / 0.26);
                }

                return lerp(float3(2.0, 0.62, 0.02), float3(2.45, 1.92, 0.02), (t - 0.78) / 0.22);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS.xyz));
                output.paneUV = input.paneUV;
                output.paneData = input.paneData;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // The mirror array IS the show: every pane displays the sunset
                // gradient (the old dome shell tint, at the pane's true latitude so
                // the gradient runs continuously across the whole bowl) plus the
                // music equalizer bars, clipped by the pane seams so the content
                // looks rendered by the mirrors. Between the tiles: black.
                float2 uv = input.paneUV;
                int tier = (int)floor(input.paneData.y);
                float paneCenter01 = frac(input.paneData.y) / 0.999;
                float phi01 = paneCenter01 + (uv.x - 0.5) / PaneCount;
                float latitude = lerp(TierBounds[tier], TierBounds[tier + 1], uv.y);

                // Rounded glass tile embedded in the dome: outside the tile the
                // fragment is bare black dome, so the dividers ARE the dome.
                float2 uvMeters = float2(uv.x * PaneWidth, uv.y * PaneHeight);
                float2 halfSize = float2(PaneWidth, PaneHeight) * 0.5;
                float2 fromCenter = abs(uvMeters - halfSize);
                const float seamInset = 0.5;
                const float cornerRadius = 2.6;
                float2 cornerQ = fromCenter - (halfSize - seamInset - cornerRadius);
                float tileDistance = length(max(cornerQ, 0.0))
                                   + min(max(cornerQ.x, cornerQ.y), 0.0) - cornerRadius;
                float aa = max(max(fwidth(uvMeters.x), fwidth(uvMeters.y)) * 0.7, 0.015);
                float glassMask = 1.0 - smoothstep(-aa, aa, tileDistance);

                // Sunset gradient (mirror of the retired ShieldDomeBackdrop
                // SampleDomeTint): hot orange at the horizon melting through
                // magenta into a deep violet zenith — same style and position as
                // when the shell itself carried it.
                float warmth = exp(-latitude * 3.1);
                float domeBrightness = 0.010 + 0.065 * exp(-latitude * 2.8);
                float3 color = lerp(float3(0.55, 0.16, 1.0), float3(1.0, 0.38, 0.06), warmth) * domeBrightness;

                // Equalizer bars, drawn in dome space so they land exactly where
                // the old mesh skyline stood and get sliced by the pane seams.
                float column = clamp(floor(phi01 * ColumnCount), 0.0, ColumnCount - 1.0);
                float4 bar = _EqualizerBars[(int)column];
                float rel = latitude - BarBaseLatitude;
                float tileIndex = floor(rel / BarTilePitch);
                if (tileIndex >= 0.0 && tileIndex < bar.x)
                {
                    float v = rel - tileIndex * BarTilePitch;
                    float theta = latitude * ThetaMax;
                    float columnSpacing = 6.2831853 * DomeRadius * cos(theta) / ColumnCount;
                    float halfWidth = min(BarHalfWidth, columnSpacing * 0.41);
                    float columnCenter01 = (column + 0.5) / ColumnCount;
                    float dx = abs(phi01 - columnCenter01) * columnSpacing * ColumnCount;

                    float aaLat = max(fwidth(latitude), 1e-5);
                    float aaX = max(fwidth(dx), 1e-4);
                    float outerCover = (1.0 - smoothstep(halfWidth - aaX, halfWidth + aaX, dx))
                                     * (1.0 - smoothstep(BarTileLatHeight - aaLat, BarTileLatHeight + aaLat, v));
                    float innerCover = (1.0 - smoothstep(halfWidth - BarLineMeters - aaX, halfWidth - BarLineMeters + aaX, dx))
                                     * smoothstep(BarLineLat - aaLat, BarLineLat + aaLat, v)
                                     * (1.0 - smoothstep(BarTileLatHeight - BarLineLat - aaLat, BarTileLatHeight - BarLineLat + aaLat, v));
                    float barMask = saturate(outerCover - innerCover);
                    float colorT = column / (ColumnCount - 1.0);
                    color += EqualizerRamp(colorT) * bar.y * barMask;
                }

                return half4(color * glassMask, 1.0h);
            }
            ENDHLSL
        }
    }
}
