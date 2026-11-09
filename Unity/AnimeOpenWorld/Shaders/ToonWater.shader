// アニメ調の水面 (URP 用・半透明)
// 波で揺れるくっきりした光の反射と、視線角度による色の変化だけのシンプルな水
Shader "AnimeOpenWorld/ToonWater"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.35, 0.82, 0.92, 0.55)
        _DeepColor ("Deep Color", Color) = (0.08, 0.35, 0.65, 0.9)
        _HighlightColor ("Highlight Color", Color) = (1, 1, 1, 1)
        _HighlightSize ("Highlight Size", Range(0, 0.2)) = 0.025
        _WaveScale ("Wave Scale", Float) = 0.12
        _WaveSpeed ("Wave Speed", Float) = 1.0
        _WaveStrength ("Wave Strength", Range(0, 1)) = 0.18
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _HighlightColor;
                half _HighlightSize;
                float _WaveScale;
                float _WaveSpeed;
                half _WaveStrength;
            CBUFFER_END

            half4 _ToonAmbientColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half   fogFactor  : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.fogFactor = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 p = IN.positionWS.xz * _WaveScale;
                float t = _Time.y * _WaveSpeed;
                float2 d = float2(
                    sin(p.x * 1.0 + t) + 0.5 * sin(p.y * 1.7 + t * 1.3),
                    cos(p.y * 1.2 + t * 0.8) + 0.5 * cos((p.x + p.y) * 0.9 - t));
                half3 N = normalize(half3(d.x * _WaveStrength, 1, d.y * _WaveStrength));
                half3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                half fresnel = pow(1.0h - saturate(V.y), 3.0h);
                half4 baseColor = lerp(_ShallowColor, _DeepColor, fresnel);

                Light light = GetMainLight();
                half3 lighting = light.color * (0.5h + 0.5h * saturate(light.direction.y)) + _ToonAmbientColor.rgb;

                half3 H = normalize(light.direction + V);
                half spec = step(1.0h - _HighlightSize, dot(N, H));

                half3 color = baseColor.rgb * lighting + spec * _HighlightColor.rgb * light.color;
                color = MixFog(color, IN.fogFactor);
                return half4(color, saturate(baseColor.a + spec));
            }
            ENDHLSL
        }
    }
}
