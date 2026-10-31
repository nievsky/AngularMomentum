Shader "OrbitalPanopticon/ParticleAdditiveGlow"
{
    Properties
    {
        [HDR] _BaseColor ("Color Tint", Color) = (1.0, 1.0, 1.0, 1.0)
        _CoreSharpness ("Core Sharpness", Range(1.0, 10.0)) = 4.0
        _GlowFalloff ("Glow Falloff", Range(0.5, 5.0)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One // Pure Additive HDR
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            float4 _BaseColor;
            float _CoreSharpness;
            float _GlowFalloff;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _BaseColor;
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv - 0.5;
                float dist = length(centered) * 2.0;
                float circle = saturate(1.0 - dist);

                float glow = pow(circle, _GlowFalloff);
                float core = pow(circle, _CoreSharpness) * 2.2;

                float3 col = input.color.rgb * (glow + core);
                float alpha = input.color.a;

                return float4(col * alpha, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
