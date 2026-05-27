Shader "ArenaShooter/SoftPickupLiftAura"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.45, 1, 0.9, 0.08)
        _EmissionStrength ("Emission Strength", Float) = 0.35
        _Alpha ("Alpha", Range(0, 1)) = 0.08
        _Speed ("Speed", Float) = 0.45
        _WispScale ("Wisp Scale", Float) = 8.0
        _Cylindrical ("Cylindrical", Float) = 0.0
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
            Name "Soft Aura"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
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
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _EmissionStrength;
                half _Alpha;
                half _Speed;
                half _WispScale;
                half _Cylindrical;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float softNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float centered = abs(uv.x - 0.5) * 2.0;
                float core = pow(saturate(1.0 - centered), 1.75);
                float shoulder = pow(saturate(1.0 - centered), 0.82) * 0.58;
                float horizontalFade = lerp(saturate(core + shoulder), 1.0, saturate(_Cylindrical));
                float bottomStrength = pow(saturate(1.0 - uv.y), 1.35);
                float verticalFade = smoothstep(0.0, 0.10, uv.y) * (1.0 - smoothstep(0.88, 1.0, uv.y));

                float t = _Time.y * _Speed;
                float bend = sin((uv.y * 1.8 - t * 0.32) * 6.28318) * 0.055;
                float streamX = abs((uv.x + bend) - 0.5) * 2.0;
                float streamCore = lerp(pow(saturate(1.0 - streamX), 2.0), 0.72, saturate(_Cylindrical));
                float bandA = sin((uv.y * _WispScale - t) * 6.28318) * 0.5 + 0.5;
                float bandB = sin((uv.y * (_WispScale * 1.42) + uv.x * lerp(0.65, 3.0, saturate(_Cylindrical)) - t * 1.28) * 6.28318) * 0.5 + 0.5;
                float noise = softNoise(float2(uv.x * 1.2 + t * 0.04, uv.y * 13.0 - t * 0.85));
                float wisps = smoothstep(0.42, 0.86, streamCore * 0.46 + bandA * 0.30 + bandB * 0.18 + noise * 0.06);

                float alpha = _Alpha * horizontalFade * verticalFade * lerp(0.26, 1.0, bottomStrength) * lerp(0.22, 1.0, wisps);
                half3 color = _BaseColor.rgb * (1.0 + _EmissionStrength * wisps * bottomStrength);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
