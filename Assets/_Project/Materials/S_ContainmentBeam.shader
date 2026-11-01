Shader "OrbitalPanopticon/ContainmentBeam"
{
    Properties
    {
        [Header(Beam Colors)]
        [HDR] _CoreColor ("Core Color (HDR White-Cyan)", Color) = (2.0, 7.5, 9.5, 1.0)
        [HDR] _GlowColor ("Glow Color (HDR Deep Cyan)", Color) = (0.2, 3.5, 6.0, 1.0)

        [Header(Animation)]
        _ScrollSpeed ("Plasma Scroll Speed", Range(0.5, 20.0)) = 5.0
        _CoreSharpness ("Core Sharpness", Range(1.0, 8.0)) = 2.5
        _BeamAlpha ("Beam Alpha Multiplier", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Geometry+450" // Queue 2450: right before _CameraOpaqueTexture at 2500!
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ContainmentBeamForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _GlowColor;
                float _ScrollSpeed;
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

            half4 frag(Varyings input) : SV_Target
            {
                float d = abs(input.uv.y - 0.5) * 2.0;
                float core = pow(saturate(1.0 - d), _CoreSharpness);
                float glow = pow(saturate(1.0 - d), 1.2);

                float ripple = 0.85 + 0.15 * sin(input.uv.x * 25.0 - _Time.y * _ScrollSpeed);

                float3 finalColor = (_CoreColor.rgb * core + _GlowColor.rgb * glow) * ripple * _BeamAlpha * input.color.rgb;
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
