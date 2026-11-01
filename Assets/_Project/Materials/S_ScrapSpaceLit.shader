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

        [Header(Virtual Studio Lighting)]
        _MinLighting ("Min Lighting (Shadow Faces)", Range(0.0, 0.5)) = 0.12
        _MaxLighting ("Max Lighting (Lit Faces)", Range(0.5, 1.5)) = 0.85

        [Header(Subtle Pulsing Silhouette Outline)]
        [HDR] _SilhouetteColor ("Silhouette Outline Color (HDR)", Color) = (0.65, 0.82, 0.95, 1.0)
        [HDR] _RimColor ("Legacy Rim Color (HDR)", Color) = (0.65, 0.82, 0.95, 1.0)
        _RimPower ("Silhouette Rim Power", Range(1.0, 12.0)) = 5.0
        _SilhouetteIntensity ("Silhouette Intensity", Range(0.0, 2.0)) = 0.35
        _RimIntensity ("Legacy Rim Intensity", Range(0.0, 2.0)) = 0.35
        _PulseSpeed ("Breathing Pulse Speed", Range(0.1, 10.0)) = 2.2

        [Header(Transparency and Fading)]
        _Alpha ("Global Transparency Alpha", Range(0.0, 1.0)) = 1.0
        [HideInInspector] [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1.0
        [HideInInspector] [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0.0
        [HideInInspector] [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1.0
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

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull Back

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
                float4 _SilhouetteColor;
                float4 _RimColor;
                float _BumpScale;
                float _Metallic;
                float _Smoothness;
                float _MinLighting;
                float _MaxLighting;
                float _RimPower;
                float _SilhouetteIntensity;
                float _RimIntensity;
                float _PulseSpeed;
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

                // 3. Virtual Three-Point Studio Rig (Key & Fill)
                // Key Light: from upper-right-front
                half3 keyLightDir = normalize(half3(0.4, 0.7, -0.5));
                // Back/Fill Light: from lower-left-back
                half3 fillLightDir = normalize(half3(-0.6, -0.3, 0.5));

                // Wrapped diffuse shading for smooth, rich 3D shading
                half NdotKey = dot(normalWS, keyLightDir);
                half wrapKey = saturate((NdotKey + 0.3) / 1.3);

                half NdotFill = dot(normalWS, fillLightDir);
                half wrapFill = saturate((NdotFill + 0.2) / 1.2);

                half totalLight = wrapKey * 0.75 + wrapFill * 0.35;
                // Clamp lighting strictly between minLighting (0.12 on shadow faces) and maxLighting (0.85 on lit faces)
                half lighting = lerp(_MinLighting, _MaxLighting, saturate(totalLight));

                // Specular roughness highlights
                half3 halfKey = normalize(keyLightDir + viewDirWS);
                half NdotH = saturate(dot(normalWS, halfKey));
                half specPower = max(2.0, _Smoothness * 64.0);
                half specular = pow(NdotH, specPower) * max(0.04, _Metallic);
                half3 specColor = half3(1.0, 1.0, 1.0) * (specular * 0.45);

                half3 diffuseColor = albedo.rgb * lighting;

                // 4. Subtle Pulsing Silhouette Outline (Rim)
                // Grazing view angle with high exponent (_RimPower = 5.0) hugs ONLY razor-thin silhouette
                half NdotV = saturate(dot(normalWS, viewDirWS));
                half fresnel = pow(1.0 - NdotV, _RimPower);

                // Unique phase offset per object based on world position
                float objectPhaseOffset = frac(UNITY_MATRIX_M._m03 * 1.73 + UNITY_MATRIX_M._m13 * 3.17 + UNITY_MATRIX_M._m23 * 2.41) * 6.2831853;
                float pulse = 0.55 + 0.45 * sin(_Time.y * _PulseSpeed + objectPhaseOffset);

                half silIntensity = max(_SilhouetteIntensity, _RimIntensity);
                half3 silColor = (_SilhouetteColor.a > 0.01) ? _SilhouetteColor.rgb : _RimColor.rgb;
                half3 silhouetteRim = silColor * (fresnel * pulse * silIntensity);

                // 5. Emission Map
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;

                // 6. Final Composite
                half3 finalColor = diffuseColor + specColor + silhouetteRim + emission;
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
