Shader "OrbitalPanopticon/RedSweepingLaser"
{
    Properties
    {
        [Header(Laser Colors)]
        [HDR] _CoreColor ("Core Color (HDR White-Hot)", Color) = (8.0, 2.5, 2.0, 1.0)
        [HDR] _GlowColor ("Glow Color (HDR Fiery Red)", Color) = (6.0, 0.1, 0.15, 1.0)

        [Header(Plasma Animation)]
        _ScrollSpeed ("Plasma Scroll Speed", Range(1.0, 40.0)) = 18.0
        _NoiseFrequency ("Noise Frequency", Range(5.0, 80.0)) = 32.0
        _CoreSharpness ("Core Sharpness", Range(1.0, 8.0)) = 3.5
        _BeamAlpha ("Beam Alpha Multiplier", Range(0.0, 1.0)) = 1.0
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
            Name "RedSweepingLaserForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _GlowColor;
                float _ScrollSpeed;
                float _NoiseFrequency;
                float _CoreSharpness;
                float _BeamAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Distance from center across the beam [0.0 at center, 1.0 at edges]
                float distFromCenter = abs(input.uv.y - 0.5) * 2.0;

                // High frequency plasma wave scrolling along UV.x
                float wave1 = sin(input.uv.x * _NoiseFrequency - _Time.y * _ScrollSpeed) * 0.5 + 0.5;
                float wave2 = sin(input.uv.x * (_NoiseFrequency * 2.3) + _Time.y * (_ScrollSpeed * 1.4)) * 0.5 + 0.5;
                float plasma = lerp(0.75, 1.25, (wave1 + wave2) * 0.5);

                // Beam core vs outer glow
                float coreMask = pow(saturate(1.0 - distFromCenter), _CoreSharpness);
                float glowMask = pow(saturate(1.0 - distFromCenter), 1.2);

                float3 coreRGB = _CoreColor.rgb * (coreMask * plasma);
                float3 glowRGB = _GlowColor.rgb * (glowMask * plasma * 1.5);

                float3 finalRGB = (coreRGB + glowRGB) * input.color.rgb * _BeamAlpha * input.color.a;
                return float4(finalRGB, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}