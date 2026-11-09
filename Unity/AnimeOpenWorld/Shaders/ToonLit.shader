// アニメ調トゥーンシェーダー (URP 用)
// - 2 階調のセル影 + 影色
// - くっきりしたハイライト / リムライト
// - 背面法 (Inverted Hull) のアウトライン
// - 頂点カラー対応 (地形・小物用)
Shader "AnimeOpenWorld/ToonLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Toggle] _UseVertexColor ("Use Vertex Color", Float) = 0

        [Header(Shade)]
        _ShadeColor ("Shade Color (multiply)", Color) = (0.68, 0.62, 0.82, 1)
        _ShadeThreshold ("Shade Threshold", Range(-1, 1)) = 0.0
        _ShadeSoftness ("Shade Softness", Range(0.001, 0.5)) = 0.03

        [Header(Highlight)]
        _HighlightColor ("Highlight Color", Color) = (1, 1, 1, 1)
        _HighlightSize ("Highlight Size", Range(0, 0.2)) = 0.02

        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 16)) = 5
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.3

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.15, 0.1, 0.15, 1)
        _OutlineWidth ("Outline Width (px)", Range(0, 8)) = 1.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _UseVertexColor;
            half4 _ShadeColor;
            half _ShadeThreshold;
            half _ShadeSoftness;
            half4 _HighlightColor;
            half _HighlightSize;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half4 _OutlineColor;
            half _OutlineWidth;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // DayNightCycle から毎フレーム設定される環境光
        half4 _ToonAmbientColor;
        ENDHLSL

        // ---------------------------------------------------------------
        // メインの描画
        // ---------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half4  color      : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb * lerp(half3(1, 1, 1), IN.color.rgb, _UseVertexColor);

                half3 N = normalize(IN.normalWS);
                half3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light light = GetMainLight(shadowCoord);

                // セル影: ランバートを 2 階調に。落ち影も同じ階調に揃える
                half NdotL = dot(N, light.direction);
                half lambert = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, NdotL);
                half shadow = smoothstep(0.45h, 0.55h, light.shadowAttenuation);
                half lit = lambert * shadow;

                half3 diffuse = lerp(albedo * _ShadeColor.rgb, albedo, lit) * light.color;
                half3 ambient = albedo * max(SampleSH(N), _ToonAmbientColor.rgb);

                // くっきりしたハイライト
                half3 H = normalize(light.direction + V);
                half spec = step(1.0h - _HighlightSize, dot(N, H)) * lit * step(0.0001h, _HighlightSize);
                half3 specular = spec * _HighlightColor.rgb * light.color;

                // リムライト
                half rim = pow(1.0h - saturate(dot(N, V)), _RimPower) * _RimStrength;
                half3 rimColor = rim * _RimColor.rgb * (0.35h + 0.65h * lit) * light.color;

                half3 color = diffuse + ambient + specular + rimColor;
                color = MixFog(color, IN.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // アウトライン (背面を法線方向に押し出す)
        // ---------------------------------------------------------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half   fogFactor  : TEXCOORD0;
            };

            Varyings vertOutline(Attributes IN)
            {
                Varyings OUT;
                float4 positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float2 normalCS = mul((float3x3)UNITY_MATRIX_VP, normalWS).xy;
                float len = length(normalCS);
                normalCS = len > 1e-5 ? normalCS / len : float2(0, 0);
                // 画面上でほぼ一定のピクセル幅になるように押し出す
                positionCS.xy += normalCS * (_OutlineWidth * 2.0 / _ScreenParams.xy) * positionCS.w;
                OUT.positionCS = positionCS;
                OUT.fogFactor = ComputeFogFactor(positionCS.z);
                return OUT;
            }

            half4 fragOutline(Varyings IN) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, IN.fogFactor), 1);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // 影を落とす
        // ---------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            float4 vertShadow(Attributes IN) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 fragShadow() : SV_Target { return 0; }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // 深度 / 法線 (SSAO や被写界深度などのポストエフェクト用)
        // ---------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth

            float4 vertDepth(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half fragDepth() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDN
            #pragma fragment fragDN

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3  normalWS   : TEXCOORD0;
            };

            Varyings vertDN(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 fragDN(Varyings IN) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(IN.normalWS), 0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
