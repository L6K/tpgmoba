// Enigma VFX: 放射UVリング（ショックウェーブ）シェーダー
// Quad 1枚を極座標展開し、半径帯のみを発光させる。縁は角度ノイズで揺らぐ。
Shader "Enigma/VFX/RadialRing"
{
    Properties
    {
        _BaseColor("Base Color (HDR)", Color) = (1, 1, 1, 1)
        _RingRadius("Ring Radius", Range(0, 1)) = 0.4
        _RingWidth("Ring Width", Range(0.01, 0.5)) = 0.08
        _Softness("Softness", Range(0.01, 0.3)) = 0.05
        _NoiseMap("Noise Map (Edge Wobble)", 2D) = "white" {}
        _NoiseStrength("Noise Strength", Range(0, 0.3)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _RingRadius;
                half  _RingWidth;
                half  _Softness;
                half  _NoiseStrength;
            CBUFFER_END

            // TWO_PI は URP コア(Macros.hlsl)定義を使用する(再定義は警告になる)

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
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 centered = IN.uv - 0.5;
                half r = length(centered) * 2.0;
                half angle01 = (atan2(centered.y, centered.x) + PI) / TWO_PI;

                half edgeNoise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, float2(angle01, 0.5)).r;
                r += (edgeNoise - 0.5) * _NoiseStrength;

                half dist = abs(r - _RingRadius);
                half band = 1.0 - smoothstep(_RingWidth - _Softness, _RingWidth + _Softness, dist);

                half alpha = band * _BaseColor.a * IN.color.a;
                half3 rgb = _BaseColor.rgb * band * IN.color.rgb;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
