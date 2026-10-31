Shader "OrbitalPanopticon/ValuableLavaLoot"
{
    Properties
    {
        [Header(Molten Magma Core)]
        [HDR] _MagmaCoreColor ("Magma Core Color (HDR)", Color) = (8.5, 6.5, 0.8, 1.0)
        [HDR] _MagmaMidColor ("Magma Mid Color (HDR)", Color) = (5.5, 1.2, 0.08, 1.0)
        _LavaScale ("Lava 3D Noise Scale", Range(1.0, 20.0)) = 5.5
        _FlowSpeed ("Lava Flow Speed", Range(0.05, 3.0)) = 0.55
        _MagmaContrast ("Magma Contrast", Range(0.5, 4.0)) = 1.8
        _MagmaIntensity ("Magma Emission Intensity", Range(1.0, 15.0)) = 7.5

        [Header(Dark Basalt Crust)]
        _CrustColor ("Dark Basalt Crust Color", Color) = (0.07, 0.05, 0.06, 1.0)
        _CrustScale ("Crust Voronoi Scale", Range(1.0, 15.0)) = 4.2
        _CrackWidth ("Crack Width", Range(0.1, 0.8)) = 0.42
        _CrackSoftness ("Crack Edge Softness", Range(0.01, 0.3)) = 0.08
        _EdgeGlowIntensity ("Crack Edge Glow", Range(0.0, 12.0)) = 6.0

        [Header(Vertex Displacement Pulsing)]
        _DisplacementAmount ("Vertex Displacement Amount", Range(0.0, 0.2)) = 0.045
        _DisplacementSpeed ("Vertex Displacement Speed", Range(0.1, 5.0)) = 2.2
        _DisplacementScale ("Vertex Displacement Frequency", Range(1.0, 15.0)) = 4.5

        [Header(Pulsing Heat Rim)]
        [HDR] _RimColor ("Fresnel Rim Color (HDR)", Color) = (6.0, 2.8, 0.3, 1.0)
        _RimPower ("Fresnel Rim Power", Range(0.5, 6.0)) = 2.2
        _RimIntensity ("Fresnel Rim Intensity", Range(0.0, 8.0)) = 4.0
        _PulseSpeed ("Heat Pulse Speed", Range(0.5, 10.0)) = 4.5

        [Header(Transparency and Fading)]
        _Alpha ("Global Transparency Alpha", Range(0.0, 1.0)) = 1.0
        _BaseColor ("Base Color Tint", Color) = (1, 1, 1, 1)
        _Color ("Legacy Color Tint", Color) = (1, 1, 1, 1)
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
            Name "ValuableLavaForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MagmaCoreColor;
                float4 _MagmaMidColor;
                float _LavaScale;
                float _FlowSpeed;
                float _MagmaContrast;
                float _MagmaIntensity;

                float4 _CrustColor;
                float _CrustScale;
                float _CrackWidth;
                float _CrackSoftness;
                float _EdgeGlowIntensity;

                float _DisplacementAmount;
                float _DisplacementSpeed;
                float _DisplacementScale;

                float4 _RimColor;
                float _RimPower;
                float _RimIntensity;
                float _PulseSpeed;

                float _Alpha;
                float4 _BaseColor;
                float4 _Color;
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
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
            };

            // ---------------- 3D Procedural Hash and Noise ----------------
            float3 hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            float voronoi3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float minDist = 1.0;

                for (int z = -1; z <= 1; z++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        for (int x = -1; x <= 1; x++)
                        {
                            float3 neighbor = float3(x, y, z);
                            float3 pointPos = hash33(i + neighbor);
                            float3 diff = neighbor + pointPos - f;
                            float dist = length(diff);
                            minDist = min(minDist, dist);
                        }
                    }
                }
                return minDist;
            }

            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float noise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash13(i + float3(0,0,0));
                float n100 = hash13(i + float3(1,0,0));
                float n010 = hash13(i + float3(0,1,0));
                float n110 = hash13(i + float3(1,1,0));
                float n001 = hash13(i + float3(0,0,1));
                float n101 = hash13(i + float3(1,0,1));
                float n011 = hash13(i + float3(0,1,1));
                float n111 = hash13(i + float3(1,1,1));

                return lerp(
                    lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                    lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y),
                    f.z
                );
            }

            float fbm3D(float3 p)
            {
                float v = 0.0;
                float a = 0.55;
                for (int i = 0; i < 3; i++)
                {
                    v += a * noise3D(p);
                    p = p * 2.1 + float3(1.7, 9.2, 4.3);
                    a *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Organic 3D Noise Vertex Displacement Pulsing
                float3 dispCoord = input.positionOS.xyz * _DisplacementScale + float3(0.0, _Time.y * _DisplacementSpeed, 0.0);
                float noiseDisp = (fbm3D(dispCoord) - 0.5) * 2.0 * _DisplacementAmount;
                float breathing = sin(_Time.y * _PulseSpeed) * 0.012;
                float totalDisp = noiseDisp + breathing;

                float3 displacedOS = input.positionOS.xyz + input.normalOS * totalDisp;

                output.positionOS = input.positionOS.xyz;
                output.positionWS = TransformObjectToWorld(displacedOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Layer 2 — Dark Basalt Crust (3D Voronoi Cellular Plates)
                float cell = voronoi3D(input.positionOS * _CrustScale);
                float cellPlate = 1.0 - cell;
                float crustMask = smoothstep(_CrackWidth, _CrackWidth + _CrackSoftness, cellPlate);
                float crackEdge = smoothstep(0.0, 0.45, 1.0 - abs(crustMask - 0.5) * 2.0);

                // Layer 1 — Molten Magma Core (Flowing 3D Noise inside cracks)
                float3 flowOffset = float3(
                    _Time.y * _FlowSpeed * 0.4,
                    _Time.y * _FlowSpeed * 0.8,
                    _Time.y * _FlowSpeed * 0.3
                );
                float3 flowPos = input.positionOS * _LavaScale + flowOffset;
                float lavaFbm = fbm3D(flowPos);
                float3 magmaColor = lerp(_MagmaMidColor.rgb, _MagmaCoreColor.rgb, pow(lavaFbm, _MagmaContrast));
                float3 crackGlow = _MagmaCoreColor.rgb * crackEdge * _EdgeGlowIntensity;

                // Combine Volcanic Rock and Molten Rivers
                float3 surfaceColor = lerp(magmaColor * _MagmaIntensity + crackGlow, _CrustColor.rgb, crustMask);

                // Layer 3 — Pulsing Fresnel Heat Rim
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(_WorldSpaceCameraPos - input.positionWS);
                float NdotV = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - NdotV, _RimPower);

                float pulse = (sin(_Time.y * _PulseSpeed) * 0.5 + 0.5);
                float pulseMult = lerp(0.85, 1.35, pulse);
                float3 rimEmission = _RimColor.rgb * (fresnel * _RimIntensity * pulseMult);

                // Final Combined HDR Color
                float3 finalColor = surfaceColor + rimEmission;

                // Support _Alpha and tint via MaterialPropertyBlock (for holding & dissolve fade)
                float alpha = saturate(min(_Alpha, min(_BaseColor.a, _Color.a)));

                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
