Shader "OrbitalPanopticon/ScrapSpaceLit"
{
    Properties
    {
        [Header(Albedo and Color Tint)]
        _BaseMap ("Base Map (Albedo)", 2D) = "white" {}
        _MainTex ("Legacy Albedo", 2D) = "white" {}
        _BaseColor ("Base Color Tint", Color) = (1, 1, 1, 1)
        _Color ("Legacy Color Tint", Color) = (1, 1, 1, 1)

        [Header(Normal Map)]
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1.0

        [Header(PBR Metallic and Roughness)]
        _SpecGlossMap ("Metallic / Roughness Map", 2D) = "white" {}
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.0
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.5

        [Header(Emission)]
        _EmissionMap ("Emission Map", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission Color (HDR)", Color) = (0, 0, 0, 1)

        [Header(Space Visibility Floor and Rim)]
        _AmbientFloor ("Self-Illumination Floor (Unlit Base)", Range(0.0, 1.0)) = 0.38
        [HDR] _RimColor ("Silhouette Fresnel Color (HDR)", Color) = (0.7, 0.88, 1.0, 1.0)
        _RimPower ("Fresnel Rim Power", Range(0.5, 8.0)) = 3.0
        _RimIntensity ("Fresnel Rim Intensity", Range(0.0, 4.0)) = 0.45

        [Header(Transparency and Fading)]
        _Alpha ("Global Transparency Alpha", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_SpecGlossMap);
            SAMPLER(sampler_SpecGlossMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                float4 _EmissionMap_ST;
                float4 _SpecGlossMap_ST;
                float4 _BaseColor;
                float4 _Color;
                float4 _EmissionColor;
                float4 _RimColor;
                float _BumpScale;
                float _Metallic;
                float _Smoothness;
                float _AmbientFloor;
                float _RimPower;
                float _RimIntensity;
                float _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                half3  normalWS     : TEXCOORD1;
                half3  tangentWS    : TEXCOORD2;
                half3  bitangentWS  : TEXCOORD3;
                float2 uv           : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = normalInputs.tangentWS;
                output.bitangentWS = normalInputs.bitangentWS;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Albedo & Color
                half4 albedoTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 albedo = albedoTex * _BaseColor * _Color;

                // 2. Normal Mapping
                half3 normalWS = normalize(input.normalWS);
                #if defined(_BUMP_MAP) || 1
                    half4 bumpSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                    half3 normalTS = UnpackNormalScale(bumpSample, _BumpScale);
                    half3x3 tbn = half3x3(normalize(input.tangentWS), normalize(input.bitangentWS), normalWS);
                    normalWS = normalize(mul(normalTS, tbn));
                #endif

                half3 viewDirWS = normalize(_WorldSpaceCameraPos - input.positionWS);

                // 3. Main Light & Additional Lighting
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 lightColor = mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation);
                half3 litDiffuse = albedo.rgb * lightColor * NdotL;

                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < pixelLightCount; ++i)
                {
                    Light light = GetAdditionalLight(i, input.positionWS);
                    half addNdotL = saturate(dot(normalWS, light.direction));
                    litDiffuse += albedo.rgb * light.color * (light.distanceAttenuation * light.shadowAttenuation) * addNdotL;
                }
                #endif

                // 4. Minimum Ambient Floor: Guarantee model details are visible in total darkness
                half3 ambient = albedo.rgb * _AmbientFloor;
                half3 finalDiffuse = ambient + litDiffuse;

                // 5. Specular Highlight (Metallic / Smoothness)
                half3 halfDir = normalize(mainLight.direction + viewDirWS);
                half NdotH = saturate(dot(normalWS, halfDir));
                half specPower = max(1.0, _Smoothness * 64.0);
                half specular = pow(NdotH, specPower) * max(0.04, _Metallic);
                half3 specColor = lightColor * specular;

                // 6. Silhouette Fresnel Rim: Pops 3D shape against deep space background
                half NdotV = saturate(dot(normalWS, viewDirWS));
                half fresnel = pow(1.0 - NdotV, _RimPower) * _RimIntensity;
                half3 rimLight = _RimColor.rgb * fresnel;

                // 7. Emission Map
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;

                // 8. Final Composite
                half3 finalColor = finalDiffuse + specColor + rimLight + emission;
                half finalAlpha = saturate(albedo.a * _Alpha);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS.xyz));
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
