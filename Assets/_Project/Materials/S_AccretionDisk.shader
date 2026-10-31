Shader "OrbitalPanopticon/AccretionDisk"
{
    Properties
    {
        [HDR] _InnerHotColor ("Inner Hot Color (HDR)", Color) = (2.6, 1.9, 0.75, 1.0)
        [HDR] _OuterCoolColor ("Outer Cool Color (HDR)", Color) = (0.85, 0.22, 0.04, 1.0)
        _InnerRadius ("Inner Edge Radius", Range(0.05, 0.7)) = 0.32
        _InnerSoftness ("Inner Edge Softness", Range(0.01, 0.3)) = 0.08
        _OuterRadius ("Outer Edge Radius", Range(0.5, 1.0)) = 0.95
        _OuterSoftness ("Outer Edge Softness", Range(0.05, 0.5)) = 0.22
        _SwirlSpeed ("Swirl Speed", Range(0.1, 5.0)) = 1.3
        _NoiseTiling ("Noise Tiling (Angle, Radius)", Vector) = (4.0, 7.0, 0, 0)
        _DiskIntensity ("Disk Intensity", Range(0.1, 5.0)) = 1.8
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "AccretionDiskForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _InnerHotColor;
                float4 _OuterCoolColor;
                float _InnerRadius;
                float _InnerSoftness;
                float _OuterRadius;
                float _OuterSoftness;
                float _SwirlSpeed;
                float4 _NoiseTiling;
                float _DiskIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                float2 shift = float2(100.0, 100.0);
                float2x2 rot = float2x2(cos(0.5), sin(0.5), -sin(0.5), cos(0.5));
                for (int i = 0; i < 4; ++i)
                {
                    v += a * noise(p);
                    p = mul(rot, p * 2.0) + shift;
                    a *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float r = length(centered);

                if (r > _OuterRadius || r < (_InnerRadius - 0.05))
                {
                    discard;
                }

                float theta = atan2(centered.y, centered.x);
                float u = (theta / 6.2831853) + 0.5;
                float v = r;

                // Keplerian differential rotation: inner orbits rotate faster
                float time = _Time.y * _SwirlSpeed;
                float keplerian = time * (0.8 / max(r, 0.15));

                float n1 = fbm(float2(u * _NoiseTiling.x + keplerian, v * _NoiseTiling.y));
                float n2 = fbm(float2(u * _NoiseTiling.x * 1.6 + keplerian * 0.7, v * _NoiseTiling.y * 1.4));
                float swirl = saturate(n1 * 0.65 + n2 * 0.35);

                // Radial falloff: inner hole and outer edge
                float innerFade = smoothstep(_InnerRadius, _InnerRadius + _InnerSoftness, r);
                float outerFade = smoothstep(_OuterRadius, _OuterRadius - _OuterSoftness, r);
                float diskAlpha = innerFade * outerFade * swirl * saturate(_DiskIntensity);

                // Temperature gradient (Hot white/yellow core -> Cool ember red exterior)
                float temp = saturate(1.0 - (r - _InnerRadius) / max(_OuterRadius - _InnerRadius, 0.001));
                float3 diskColor = lerp(_OuterCoolColor.rgb, _InnerHotColor.rgb, pow(temp, 1.4));

                return float4(diskColor * _DiskIntensity, diskAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
