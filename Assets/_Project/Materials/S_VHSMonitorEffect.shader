Shader "OrbitalPanopticon/VHSMonitorEffect"
{
    Properties
    {
        _MasterBlend ("Master Blend", Range(0.0, 1.0)) = 0.65

        [Header(CRT Scanlines)]
        _ScanlineIntensity ("Scanline Intensity", Range(0.0, 1.0)) = 0.28
        _ScanlineCount ("Scanline Count", Range(100.0, 1080.0)) = 640.0
        _ScanlineScrollSpeed ("Scanline Scroll Speed", Range(-10.0, 10.0)) = 0.8

        [Header(VHS Tracking Glitch)]
        _TrackingBarIntensity ("Tracking Bar Intensity", Range(0.0, 1.0)) = 0.45
        _TrackingBarSpeed ("Tracking Bar Speed", Range(0.0, 2.0)) = 0.18
        _TrackingBarThickness ("Tracking Bar Thickness", Range(0.01, 0.35)) = 0.08
        _HorizontalGlitchStrength ("Horizontal Glitch Strength", Range(0.0, 0.15)) = 0.035

        [Header(RGB Chromatic Shift)]
        _RGBShiftAmount ("RGB Shift Amount", Range(0.0, 0.05)) = 0.0055

        [Header(Tape Noise and Static)]
        _TapeNoiseIntensity ("Tape Noise Intensity", Range(0.0, 1.0)) = 0.18

        [Header(CRT Curvature and Vignette)]
        _ScreenCurvature ("Screen Curvature", Range(0.0, 0.5)) = 0.12
        _CornerVignette ("Corner Vignette", Range(0.0, 1.0)) = 0.45

        [Header(Signal Jitter)]
        _VerticalJitter ("Vertical Jitter", Range(0.0, 0.02)) = 0.003

        [Header(Surveillance CCTV Tint)]
        _CCTVTint ("CCTV Tint Color", Color) = (0.95, 1.02, 0.98, 1.0)
        _CCTVTintIntensity ("CCTV Tint Intensity", Range(0.0, 1.0)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100
        ZWrite Off
        Cull Off

        Pass
        {
            Name "VHSMonitorPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _MasterBlend;
                float _ScanlineIntensity;
                float _ScanlineCount;
                float _ScanlineScrollSpeed;
                float _TrackingBarIntensity;
                float _TrackingBarSpeed;
                float _TrackingBarThickness;
                float _HorizontalGlitchStrength;
                float _RGBShiftAmount;
                float _TapeNoiseIntensity;
                float _ScreenCurvature;
                float _CornerVignette;
                float _VerticalJitter;
                float4 _CCTVTint;
                float _CCTVTintIntensity;
            CBUFFER_END

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 rawUV = input.texcoord;
                float4 origColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, rawUV);

                if (_MasterBlend <= 0.0001)
                {
                    return origColor;
                }

                float timeVal = _Time.y;

                // 1. CRT Screen Curvature (Barrel Distortion)
                float2 centered = rawUV * 2.0 - 1.0;
                float r2 = dot(centered, centered);
                float2 curvedCentered = centered * (1.0 + r2 * _ScreenCurvature);
                float2 uv = curvedCentered * 0.5 + 0.5;

                bool isOutside = (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0);

                // 2. Signal Interlacing & Vertical Jitter
                float jitterSeed = floor(timeVal * 14.0);
                float randVal = frac(sin(jitterSeed * 12.9898) * 43758.5453);
                float jitterActive = step(0.91, randVal);
                float jitterY = (frac(randVal * 19.37) - 0.5) * _VerticalJitter * 2.0 * jitterActive;
                uv.y += jitterY;

                float scanLineIdx = floor(uv.y * _ScanlineCount);
                float lineEven = (fmod(scanLineIdx, 2.0) < 1.0) ? 1.0 : -1.0;
                uv.x += lineEven * (_VerticalJitter * 0.25);

                // 3. VHS Tracking Glitch Bar
                float barPos = frac(timeVal * _TrackingBarSpeed);
                float distToBar = abs(uv.y - barPos);
                distToBar = min(distToBar, 1.0 - distToBar);
                float barFactor = smoothstep(_TrackingBarThickness, 0.0, distToBar) * _TrackingBarIntensity;

                float barNoise = sin(uv.y * 140.0 + timeVal * 55.0) * cos(uv.y * 310.0 - timeVal * 35.0);
                float horizDisplace = barNoise * _HorizontalGlitchStrength * barFactor;
                uv.x += horizDisplace;

                // 4. RGB Chromatic Shift / Color Bleed
                float2 uvClamped = saturate(uv);
                float shiftMultiplier = 1.0 + barFactor * 2.5;
                float2 shiftR = float2(_RGBShiftAmount, _RGBShiftAmount * 0.2) * shiftMultiplier;
                float2 shiftB = float2(-_RGBShiftAmount, -_RGBShiftAmount * 0.2) * shiftMultiplier;

                float colR = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uvClamped + shiftR)).r;
                float colG = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvClamped).g;
                float colB = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uvClamped + shiftB)).b;
                float3 vhsColor = float3(colR, colG, colB);

                // 5. Tape Noise & Static Snow
                float2 pixelCoord = rawUV * _ScreenParams.xy;
                float staticNoise = frac(sin(dot(pixelCoord + float2(timeVal * 117.0, timeVal * 319.0), float2(12.9898, 78.233))) * 43758.5453);
                float noiseMod = (staticNoise - 0.5) * _TapeNoiseIntensity;
                noiseMod += (staticNoise - 0.5) * barFactor * 0.5;
                vhsColor += noiseMod;

                // 6. CRT Scanlines
                float scan = sin((uv.y * _ScanlineCount + timeVal * _ScanlineScrollSpeed) * 6.2831853);
                scan = scan * 0.5 + 0.5;
                scan = pow(scan, 1.35);
                vhsColor *= lerp(1.0, scan, _ScanlineIntensity);

                // 7. Surveillance CCTV Phosphor Tint
                float luma = dot(vhsColor, float3(0.2126, 0.7152, 0.0722));
                float3 tintedColor = lerp(vhsColor, luma * _CCTVTint.rgb, _CCTVTintIntensity);
                vhsColor = tintedColor;

                // 8. Corner Vignette & Bezel
                float corner = (1.0 - curvedCentered.x * curvedCentered.x) * (1.0 - curvedCentered.y * curvedCentered.y);
                corner = saturate(corner);
                corner = pow(corner, _CornerVignette * 3.5);
                vhsColor *= corner;

                if (isOutside)
                {
                    vhsColor = float3(0.015, 0.015, 0.015);
                }

                // 9. Master Blend
                float3 finalRgb = lerp(origColor.rgb, vhsColor, _MasterBlend);
                return float4(finalRgb, origColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
