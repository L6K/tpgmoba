// Enigma VFX: ノイズ侵食による加算合成シェーダー
// 炎・煙・爆発など「ノイズで溶けて消える」質感の基盤。パーティクル頂点カラー対応。
Shader "Enigma/VFX/ErosionAdditive"
{
    Properties
    {
        _BaseMap("Base Map (Shape/Glow)", 2D) = "white" {}
        _NoiseMap("Noise Map (Tileable)", 2D) = "white" {}
        _BaseColor("Base Color (HDR)", Color) = (1, 1, 1, 1)
        _Erosion("Erosion", Range(0, 1)) = 0
        _ErosionSoftness("Erosion Softness", Range(0.01, 0.5)) = 0.15
        _NoiseScrollX("Noise Scroll X", Float) = 0.1
        _NoiseScrollY("Noise Scroll Y", Float) = 0.05
        _NoiseTiling("Noise Tiling", Float) = 1
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
            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NoiseMap_ST;
                half4  _BaseColor;
                half   _Erosion;
                half   _ErosionSoftness;
                half   _NoiseScrollX;
                half   _NoiseScrollY;
                half   _NoiseTiling;
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
                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                float2 noiseUV = IN.uv * _NoiseTiling + _Time.y * float2(_NoiseScrollX, _NoiseScrollY);
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUV).r;

                half erosionMask = smoothstep(_Erosion, _Erosion + _ErosionSoftness, noise);
                half alpha = base.a * erosionMask * IN.color.a;

                half3 color = base.rgb * _BaseColor.rgb * IN.color.rgb * alpha;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
