Shader "OrbitalPanopticon/OuterWildsBlackHole"
{
    Properties
    {
        _CoreRadius ("Event Horizon Core Radius", Range(0.05, 0.8)) = 0.35
        _MaxLensingRadius ("Max Lensing Radius", Range(0.5, 1.5)) = 1.0
        _LensingStrength ("Lensing Strength", Range(0.0, 0.4)) = 0.08
        _LensingFalloff ("Lensing Falloff Power", Range(0.5, 5.0)) = 1.8
        [HDR] _PhotonRingColor ("Photon Ring Color", Color) = (1.0, 0.92, 0.78, 1.0)
        _PhotonRingSharpness ("Photon Ring Sharpness", Range(10.0, 250.0)) = 90.0
        _PhotonRingIntensity ("Photon Ring Intensity", Range(0.0, 5.0)) = 1.6
        _EdgeFadeSoftness ("Outer Edge Fade Softness", Range(0.01, 0.5)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "OuterWildsLensing"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _CoreRadius;
                float _MaxLensingRadius;
                float _LensingStrength;
                float _LensingFalloff;
                float4 _PhotonRingColor;
                float _PhotonRingSharpness;
                float _PhotonRingIntensity;
                float _EdgeFadeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS      : SV_POSITION;
                float4 screenPos       : TEXCOORD0;
                float4 centerClip      : TEXCOORD1;
                float screenMeshRadius : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);

                // Black Hole world position from object model matrix
                float3 centerWS = UNITY_MATRIX_M._m03_m13_m23;
                output.centerClip = TransformWorldToHClip(centerWS);

                // Calculate projected screen-space mesh radius
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float3 cameraRightWS = UNITY_MATRIX_V[0].xyz;
                float meshScale = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20)) * 0.5;
                float4 rimClip = TransformWorldToHClip(centerWS + cameraRightWS * meshScale);

                float2 centerScreenUV = ComputeScreenPos(output.centerClip).xy / max(output.centerClip.w, 0.0001);
                float2 rimScreenUV = ComputeScreenPos(rimClip).xy / max(rimClip.w, 0.0001);

                centerScreenUV.x *= aspect;
                rimScreenUV.x *= aspect;
                output.screenMeshRadius = max(0.001, length(rimScreenUV - centerScreenUV));

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Discard if Black Hole is completely behind the camera view frustum
                if (input.centerClip.w <= 0.01)
                {
                    discard;
                }

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float4 centerScreen = ComputeScreenPos(input.centerClip);
                float2 centerUV = centerScreen.xy / centerScreen.w;

                // Screen aspect correction for isotropic circular metric
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 delta = screenUV - centerUV;
                delta.x *= aspect;

                float rScreen = length(delta);
                float normR = rScreen / input.screenMeshRadius;

                // 1) Singularity Core (normR < _CoreRadius): Output pure pitch-black
                if (normR < _CoreRadius)
                {
                    return float4(0.0, 0.0, 0.0, 1.0);
                }

                // 2) Photon Ring / Einstein Ring Edge (normR right around _CoreRadius)
                float ringDelta = abs(normR - _CoreRadius);
                float photonRing = exp(-ringDelta * _PhotonRingSharpness);
                float3 photonGlow = _PhotonRingColor.rgb * (photonRing * _PhotonRingIntensity);

                // 3) Relativistic Gravitational Lensing (normR >= _CoreRadius)
                float falloffTerm = saturate(1.0 - normR / _MaxLensingRadius);
                float deflection = _LensingStrength * pow(falloffTerm, _LensingFalloff) / max(normR - _CoreRadius + 0.02, 0.001);

                // Offset the background sampling UV toward the center
                float2 normDir = delta / max(rScreen, 0.0001);
                float2 uvDir = normalize(float2(normDir.x / aspect, normDir.y));
                float2 warpedUV = screenUV - uvDir * deflection;
                warpedUV = clamp(warpedUV, 0.001, 0.999);

                float3 warpedBg = SampleSceneColor(warpedUV);

                // Smooth outer edge alpha fade
                float outerFade = smoothstep(_MaxLensingRadius, _MaxLensingRadius - _EdgeFadeSoftness, normR);

                float3 finalColor = warpedBg + photonGlow;

                return float4(finalColor, outerFade);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
