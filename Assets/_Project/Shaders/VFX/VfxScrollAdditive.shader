// Enigma VFX: 2層UVスクロール加算シェーダー
// ビーム/オーラ用。ベース層 + ディテール層のスクロールを重ねて加算発光させる。
Shader "Enigma/VFX/ScrollAdditive"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _DetailMap("Detail Map", 2D) = "white" {}
        _BaseColor("Base Color (HDR)", Color) = (1, 1, 1, 1)
        _ScrollMain("Scroll Main (xy)", Vector) = (0.1, 0, 0, 0)
        _ScrollDetail("Scroll Detail (xy)", Vector) = (-0.15, 0.05, 0, 0)
        _DetailStrength("Detail Strength", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DetailMap);
            SAMPLER(sampler_DetailMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _DetailMap_ST;
                half4  _BaseColor;
                half4  _ScrollMain;
                half4  _ScrollDetail;
                half   _DetailStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 mainUV = IN.uv + _Time.y * _ScrollMain.xy;
                float2 detailUV = IN.uv * 2.0 + _Time.y * _ScrollDetail.xy;

                half4 main = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, mainUV);
                half4 detail = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, detailUV);

                half3 rgb = main.rgb * (1 + detail.r * _DetailStrength) * _BaseColor.rgb * main.a * IN.color.rgb;
                half alpha = main.a * IN.color.a;

                return half4(rgb * IN.color.a, alpha);
            }
            ENDHLSL
        }
    }
}
