Shader "OrbitalPanopticon/ProceduralSpaceSkybox"
{
    Properties
    {
        [Header(1. Deep Space Gradient)]
        _SpaceZenithColor ("Space Zenith Color", Color) = (0.012, 0.018, 0.042, 1.0)
        _SpaceHorizonColor ("Space Horizon Color", Color) = (0.004, 0.007, 0.016, 1.0)
        _SpaceNadirColor ("Space Nadir Color", Color) = (0.001, 0.002, 0.006, 1.0)
        _GradientExponent ("Gradient Exponent", Range(0.2, 5.0)) = 1.35

        [Header(2. Multi Layered Starfield)]
        _StarDensity ("Star Density", Range(20.0, 300.0)) = 110.0
        _StarSize ("Star Size", Range(0.002, 0.06)) = 0.016
        _StarBrightness ("Star Brightness", Range(0.1, 5.0)) = 2.2
        _StarColorVariation ("Star Color Variation", Range(0.0, 1.0)) = 0.85
        _StarTwinkleSpeed ("Star Twinkle Speed", Range(0.0, 10.0)) = 2.2
        _StarTwinkleAmount ("Star Twinkle Amount", Range(0.0, 1.0)) = 0.55

        [Header(3. Milky Way Band)]
        _MilkyWayWidth ("Milky Way Width", Range(0.05, 0.8)) = 0.32
        _MilkyWayFalloff ("Milky Way Falloff", Range(0.5, 6.0)) = 2.2
        [HDR] _MilkyWayCoreHDRColor ("Milky Way Core Color", Color) = (0.65, 0.52, 0.95, 1.0)
        _MilkyWayOuterColor ("Milky Way Outer Color", Color) = (0.12, 0.18, 0.38, 1.0)
        _DustLaneDarkness ("Dust Lane Darkness", Range(0.0, 1.0)) = 0.18
        _MilkyWayStarBoost ("Milky Way Star Boost", Range(1.0, 6.0)) = 3.5
        _MilkyWayNoiseScale ("Milky Way Noise Scale", Range(0.5, 10.0)) = 2.8

        [Header(4. Cosmic Nebulae Clouds)]
        _NebulaIntensity ("Nebula Intensity", Range(0.0, 3.0)) = 0.95
        _NebulaScale ("Nebula Scale", Range(0.5, 8.0)) = 2.0
        [HDR] _NebulaColorA ("Nebula Color A (Teal/Cyan)", Color) = (0.08, 0.48, 0.85, 1.0)
        [HDR] _NebulaColorB ("Nebula Color B (Violet/Magenta)", Color) = (0.75, 0.15, 0.65, 1.0)
        _NebulaOffset ("Nebula Offset", Vector) = (1.5, 2.3, 3.7, 0.0)

        [Header(5. Milky Way Rotation)]
        _MilkyWayRotationEuler ("Milky Way Rotation Euler", Vector) = (32.0, 45.0, 18.0, 0.0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "SpaceSkyboxPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SpaceZenithColor;
                float4 _SpaceHorizonColor;
                float4 _SpaceNadirColor;
                float _GradientExponent;

                float _StarDensity;
                float _StarSize;
                float _StarBrightness;
                float _StarColorVariation;
                float _StarTwinkleSpeed;
                float _StarTwinkleAmount;

                float _MilkyWayWidth;
                float _MilkyWayFalloff;
                float4 _MilkyWayCoreHDRColor;
                float4 _MilkyWayOuterColor;
                float _DustLaneDarkness;
                float _MilkyWayStarBoost;
                float _MilkyWayNoiseScale;

                float _NebulaIntensity;
                float _NebulaScale;
                float4 _NebulaColorA;
                float4 _NebulaColorB;
                float4 _NebulaOffset;

                float4 _MilkyWayRotationEuler;
                float4x4 _MilkyWayRotationMatrix;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldDir   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.worldDir = input.positionOS.xyz;
                return output;
            }

            // --- Procedural Math & Noise Utilities ---
            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(443.897, 441.423, 437.195));
                p += dot(p, p.yxz + 19.19);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            float Noise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(i + float3(0, 0, 0));
                float n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0));
                float n110 = Hash31(i + float3(1, 1, 0));
                float n001 = Hash31(i + float3(0, 0, 1));
                float n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1));
                float n111 = Hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, u.x);
                float nx10 = lerp(n010, n110, u.x);
                float nx01 = lerp(n001, n101, u.x);
                float nx11 = lerp(n011, n111, u.x);

                float nxy0 = lerp(nx00, nx10, u.y);
                float nxy1 = lerp(nx01, nx11, u.y);

                return lerp(nxy0, nxy1, u.z);
            }

            float FBM3D(float3 p)
            {
                float val = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    val += amp * Noise3D(p);
                    p *= 2.04;
                    amp *= 0.5;
                }
                return val;
            }

            float3x3 EulerToRotationMatrix(float3 eulerDeg)
            {
                float3 rad = eulerDeg * (3.14159265 / 180.0);
                float cx = cos(rad.x); float sx = sin(rad.x);
                float cy = cos(rad.y); float sy = sin(rad.y);
                float cz = cos(rad.z); float sz = sin(rad.z);

                float3x3 mx = float3x3(1, 0, 0,  0, cx, -sx,  0, sx, cx);
                float3x3 my = float3x3(cy, 0, sy,  0, 1, 0,  -sy, 0, cy);
                float3x3 mz = float3x3(cz, -sz, 0,  sz, cz, 0,  0, 0, 1);

                return mul(mz, mul(my, mx));
            }

            float3 GetStarColor(float seed, float variation)
            {
                // Stellar temperature categories
                float3 colBlueGiant = float3(0.65, 0.85, 1.45);
                float3 colWhiteSun  = float3(1.00, 0.98, 0.94);
                float3 colWarmAmber = float3(1.25, 0.85, 0.52);
                float3 colRedDwarf  = float3(1.40, 0.48, 0.32);

                float3 targetCol;
                if (seed < 0.30)
                {
                    targetCol = lerp(colWhiteSun, colBlueGiant, seed / 0.30);
                }
                else if (seed < 0.70)
                {
                    targetCol = colWhiteSun;
                }
                else if (seed < 0.88)
                {
                    targetCol = lerp(colWhiteSun, colWarmAmber, (seed - 0.70) / 0.18);
                }
                else
                {
                    targetCol = lerp(colWarmAmber, colRedDwarf, (seed - 0.88) / 0.12);
                }

                return lerp(float3(1, 1, 1), targetCol, variation);
            }

            float3 EvaluateStarfield(float3 dir, float bandFactor, float timeVal)
            {
                float3 starAcc = float3(0, 0, 0);

                // Layer 1: Dense background stars (sharp cellular hash)
                float baseScale1 = _StarDensity;
                float3 p1 = dir * baseScale1;
                int3 baseCell1 = floor(p1);
                float3 f1 = frac(p1);
                int3 step1 = (f1 > 0.5) ? int3(1, 1, 1) : int3(0, 0, 0);

                // Check 8 nearest neighboring voxel corners
                for (int x = 0; x <= 1; x++)
                {
                    for (int y = 0; y <= 1; y++)
                    {
                        for (int z = 0; z <= 1; z++)
                        {
                            int3 offset = int3(x == 1 ? (step1.x * 2 - 1) : 0,
                                               y == 1 ? (step1.y * 2 - 1) : 0,
                                               z == 1 ? (step1.z * 2 - 1) : 0);
                            float3 cell = baseCell1 + offset;
                            float3 rnd = Hash33(cell);

                            // Denser stars inside Milky Way band
                            float threshold = lerp(0.86, 0.55, saturate(bandFactor * _MilkyWayStarBoost * 0.28));
                            if (rnd.x > threshold)
                            {
                                float3 starPos = cell + rnd * 0.75 + 0.12;
                                float dist = length(p1 - starPos);
                                float starRadius = _StarSize * (0.6 + 0.8 * rnd.y);

                                if (dist < starRadius)
                                {
                                    float normD = dist / starRadius;
                                    float core = saturate(1.0 - normD);
                                    float starIntensity = pow(core, 3.5) + pow(core, 12.0) * 3.0;

                                    // Twinkle
                                    float tw = sin(timeVal * _StarTwinkleSpeed * (rnd.y * 2.0 + 1.0) + rnd.z * 12.0);
                                    tw = lerp(1.0, saturate(tw * 0.5 + 0.5), _StarTwinkleAmount);

                                    float3 starCol = GetStarColor(rnd.z, _StarColorVariation);
                                    starAcc += starCol * (starIntensity * _StarBrightness * tw);
                                }
                            }
                        }
                    }
                }

                // Layer 2: Bright Major Constellation Stars & Diffraction Spikes
                float baseScale2 = _StarDensity * 0.28;
                float3 p2 = dir * baseScale2 + float3(19.3, 41.7, 73.1);
                int3 baseCell2 = floor(p2);
                float3 f2 = frac(p2);
                int3 step2 = (f2 > 0.5) ? int3(1, 1, 1) : int3(0, 0, 0);

                for (int x2 = 0; x2 <= 1; x2++)
                {
                    for (int y2 = 0; y2 <= 1; y2++)
                    {
                        for (int z2 = 0; z2 <= 1; z2++)
                        {
                            int3 offset = int3(x2 == 1 ? (step2.x * 2 - 1) : 0,
                                               y2 == 1 ? (step2.y * 2 - 1) : 0,
                                               z2 == 1 ? (step2.z * 2 - 1) : 0);
                            float3 cell = baseCell2 + offset;
                            float3 rnd = Hash33(cell);

                            if (rnd.x > 0.93)
                            {
                                float3 starPos = cell + rnd * 0.75 + 0.12;
                                float dist = length(p2 - starPos);
                                float starRadius = _StarSize * 2.6 * (0.8 + 0.5 * rnd.y);

                                if (dist < starRadius)
                                {
                                    float normD = dist / starRadius;
                                    float core = saturate(1.0 - normD);
                                    float starIntensity = pow(core, 2.5) * 2.0 + pow(core, 9.0) * 4.5;

                                    float tw = sin(timeVal * (_StarTwinkleSpeed * 1.4) + rnd.z * 6.28);
                                    tw = lerp(1.0, saturate(tw * 0.5 + 0.5), _StarTwinkleAmount * 0.45);

                                    float3 starCol = GetStarColor(rnd.y, _StarColorVariation);
                                    starAcc += starCol * (starIntensity * _StarBrightness * 1.6 * tw);
                                }
                            }
                        }
                    }
                }

                return starAcc;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.worldDir);
                float timeVal = _Time.y;

                // 1. Deep Space Directional Gradient
                float up = dir.y;
                float3 spaceColor;
                if (up >= 0.0)
                {
                    float t = pow(up, _GradientExponent);
                    spaceColor = lerp(_SpaceHorizonColor.rgb, _SpaceZenithColor.rgb, t);
                }
                else
                {
                    float t = pow(saturate(-up), _GradientExponent);
                    spaceColor = lerp(_SpaceHorizonColor.rgb, _SpaceNadirColor.rgb, t);
                }

                // 2. Movable & Rotatable Milky Way Band
                float3x3 rotMat;
                if (abs(_MilkyWayRotationMatrix._m00) > 0.001 || abs(_MilkyWayRotationMatrix._m11) > 0.001)
                {
                    rotMat = (float3x3)_MilkyWayRotationMatrix;
                }
                else
                {
                    rotMat = EulerToRotationMatrix(_MilkyWayRotationEuler.xyz);
                }

                float3 bandDir = mul(rotMat, dir);
                float bandDist = abs(bandDir.y);
                float widthSafe = max(0.01, _MilkyWayWidth);
                float bandFactor = pow(saturate(1.0 - bandDist / widthSafe), _MilkyWayFalloff);

                // Cosmic Dust Lanes inside Milky Way
                float dustNoise = FBM3D(bandDir * _MilkyWayNoiseScale + float3(1.2, 3.4, 5.6));
                float laneOffset = abs(bandDir.y + (dustNoise - 0.5) * widthSafe * 0.65);
                float darkLane = saturate(laneOffset / (widthSafe * 0.35));
                darkLane = lerp(_DustLaneDarkness, 1.0, darkLane);

                float3 bandBaseCol = lerp(_MilkyWayOuterColor.rgb, _MilkyWayCoreHDRColor.rgb, pow(bandFactor, 1.4));
                float3 milkyWayGlow = bandBaseCol * (bandFactor * darkLane);

                // 3. Cosmic Nebulae Clouds (Domain-Warped 3D FBM)
                float3 pNeb = dir * _NebulaScale + _NebulaOffset.xyz;
                float3 warp = float3(
                    FBM3D(pNeb + float3(0.0, 0.0, 0.0)),
                    FBM3D(pNeb + float3(4.8, 1.7, 2.3)),
                    FBM3D(pNeb + float3(2.1, 7.9, 3.8))
                );
                float nebulaNoise = FBM3D(pNeb + 1.25 * warp);
                nebulaNoise = saturate((nebulaNoise - 0.32) * 2.4);

                float3 nebulaCol = lerp(_NebulaColorA.rgb, _NebulaColorB.rgb, saturate(warp.x * 1.2));
                float3 nebulaGlow = nebulaCol * (pow(nebulaNoise, 2.2) * _NebulaIntensity);

                // 4. Multi-Layered Starfield
                float3 starLight = EvaluateStarfield(dir, bandFactor, timeVal);

                // 5. Final Composite (Deep Space + Milky Way + Nebulae + Starfield)
                float3 finalColor = spaceColor + milkyWayGlow + nebulaGlow + starLight;
                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
