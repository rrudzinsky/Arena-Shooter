Shader "Hidden/ArenaShooter/DroidOutlineWallDamageMask"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "DroidOutlineWallDamageMask"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Shaders/WallDamageClip.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalVS : TEXCOORD0;
                float viewDepth : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            float4 _OutlineDistanceParams;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 positionVS = TransformWorldToView(positionWS);
                output.positionOS = input.positionOS.xyz;
                output.positionHCS = TransformWorldToHClip(positionWS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.normalVS = TransformWorldToViewDir(normalWS, true);
                output.viewDepth = max(0.0, -positionVS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                ArenaClipWallDamage(input.positionOS);
                half3 encodedNormal = normalize(input.normalVS) * 0.5h + 0.5h;
                half distanceWeight = (half)saturate(1.0 - (input.viewDepth - _OutlineDistanceParams.x) * _OutlineDistanceParams.y);
                distanceWeight = lerp(0.25h, 1.0h, distanceWeight);
                return half4(encodedNormal, distanceWeight);
            }
            ENDHLSL
        }
    }
}
