// Enigma 顔専用トゥーンシェーダー（URP）
// 顔は法線を視線方向へ平坦化し凹凸由来の陰を消す。落ち影も弱めて受け、
// 木・髪などのソフトシャドウが顔に汚く落ちるのを防ぐ
Shader "Enigma/ToonFace"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _ShadeColor("Shade Color", Color) = (0.62, 0.58, 0.72, 1)
        // 明側に寄ったソフトランプ: 顔のシェード境界を曖昧にして面で受ける
        _RampThreshold("Ramp Threshold", Range(0, 1)) = 0.30
        _RampSmoothing("Ramp Smoothing", Range(0.001, 0.5)) = 0.22
        _RimColor("Rim Color", Color) = (1, 1, 1, 0.35)
        _RimPower("Rim Power", Range(0.5, 8)) = 3.5
        _OutlineColor("Outline Color", Color) = (0.12, 0.1, 0.16, 1)
        _OutlineWidth("Outline Width", Range(0, 0.02)) = 0.0035
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0
        // 受け取る落ち影の効き（0=影を無視、1=通常）
        _SelfShadowStrength("Self Shadow Strength", Range(0, 1)) = 0.3
        // 法線を視線方向へ寄せる率（鼻・頬の陰を消す）
        _FlattenNormal("Flatten Normal", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        // ── メイン（顔向けセルシェーディング） ──────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _ShadeColor;
                half   _RampThreshold;
                half   _RampSmoothing;
                half4  _RimColor;
                half   _RimPower;
                half4  _OutlineColor;
                half   _OutlineWidth;
                half   _Cutoff;
                half   _SelfShadowStrength;
                half   _FlattenNormal;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                // _Cutoff > 0 のマテリアルのみカットアウト（まつ毛・前髪など）
                clip(baseTex.a - _Cutoff);
                half3 albedo  = baseTex.rgb * _BaseColor.rgb;

                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                // 法線を視線方向へ平坦化し、鼻・頬の凹凸による細かい陰を面で受ける
                float3 normalWS = normalize(lerp(normalize(input.normalWS), viewDirWS, _FlattenNormal));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // 落ち影は _SelfShadowStrength の分だけ効かせる（木・髪の影を弱める）
                half atten = lerp(1.0h, mainLight.shadowAttenuation, _SelfShadowStrength);
                half ndl   = dot(normalWS, mainLight.direction) * 0.5h + 0.5h;
                half lit   = ndl * atten;
                half band  = smoothstep(_RampThreshold - _RampSmoothing,
                                        _RampThreshold + _RampSmoothing, lit);

                half3 shade = albedo * _ShadeColor.rgb;
                half3 color = lerp(shade, albedo, band) * mainLight.color;

                // 追加ライト（簡易: ランプを通さず加算）
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; i++)
                {
                    Light l = GetAdditionalLight(i, input.positionWS);
                    half a = saturate(dot(normalWS, l.direction)) * l.distanceAttenuation * l.shadowAttenuation;
                    color += albedo * l.color * a * 0.5h;
                }

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // ── 輪郭線（背面押し出し） ────────────────────────
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _ShadeColor;
                half   _RampThreshold;
                half   _RampSmoothing;
                half4  _RimColor;
                half   _RimPower;
                half4  _OutlineColor;
                half   _OutlineWidth;
                half   _Cutoff;
                half   _SelfShadowStrength;
                half   _FlattenNormal;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);

                // クリップ空間で法線方向に押し出し、距離に関わらず一定の線幅にする
                float3 normalCS = normalize(mul((float3x3)GetWorldToHClipMatrix(), normalWS).xyz);
                output.positionCS.xy += normalCS.xy * _OutlineWidth * output.positionCS.w;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // ── 影キャスト ───────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif

                output.positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDir));
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ── 深度プリパス ─────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToWorldHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
