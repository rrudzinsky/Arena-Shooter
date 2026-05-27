Shader "ArenaShooter/SmoothPickupLiftAura"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.45, 1, 0.9, 0.045)
        _Alpha ("Alpha", Range(0, 1)) = 0.045
        _EmissionStrength ("Emission Strength", Float) = 0.65
        _FlowSpeed ("Flow Speed", Float) = 0.42
        _FlowStrength ("Flow Strength", Range(0, 1)) = 0.28
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
            Name "Smooth Lift Volume"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
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
                half _Alpha;
                half _EmissionStrength;
                half _FlowSpeed;
                half _FlowStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float y = saturate(input.uv.y);
                float archFade = 1.0 - smoothstep(0.76, 1.0, y);
                float bottomFade = pow(saturate(1.0 - y), 0.58);
                float floorFade = smoothstep(0.0, 0.08, y);
                float flowA = sin((y * 2.2 - _Time.y * _FlowSpeed) * 6.28318) * 0.5 + 0.5;
                float flowB = sin((y * 3.4 - _Time.y * (_FlowSpeed * 1.37) + 0.31) * 6.28318) * 0.5 + 0.5;
                float flow = lerp(1.0, 0.72 + max(flowA, flowB) * 0.42, _FlowStrength);
                float alpha = _Alpha * archFade * bottomFade * floorFade * flow;
                half3 color = _BaseColor.rgb * (1.0 + _EmissionStrength * bottomFade * flow);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
