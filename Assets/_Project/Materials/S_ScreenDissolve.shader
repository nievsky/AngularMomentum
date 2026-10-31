Shader "UI/ScreenDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0,0,0,1)
        _DissolveProgress ("Dissolve Progress", Range(0, 1)) = 0.0
        _DissolveEdgeWidth ("Edge Width", Range(0.001, 0.25)) = 0.09
        _SoftGlowWidth ("Soft Glow Width", Range(0.01, 0.4)) = 0.16
        [HDR] _EdgeCoreColor ("Edge Core Color (Scorching Core)", Color) = (8.0, 4.5, 1.0, 1.0)
        [HDR] _EdgeOuterColor ("Edge Outer Color (Fiery Orange)", Color) = (4.5, 0.9, 0.05, 1.0)
        [HDR] _DissolveEdgeColor ("Legacy Edge Color", Color) = (4.5, 0.9, 0.05, 1.0)
        _NoiseScale ("Noise Scale", Float) = 6.0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent+100"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float4 _ClipRect;
            float _DissolveProgress;
            float _DissolveEdgeWidth;
            float _SoftGlowWidth;
            float4 _EdgeCoreColor;
            float4 _EdgeOuterColor;
            float4 _DissolveEdgeColor;
            float _NoiseScale;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            float hash21(float2 p)
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
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    v += a * noise(p);
                    p = p * 2.05 + float2(1.7, 3.2);
                    a *= 0.5;
                }
                return v;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float progress = saturate(_DissolveProgress);

                if (progress <= 0.0001)
                {
                    return fixed4(0, 0, 0, 0);
                }

                if (progress >= 0.9999)
                {
                    return fixed4(0, 0, 0, 1);
                }

                float aspect = 1.0;
                if (_ScreenParams.y > 0.0)
                {
                    aspect = _ScreenParams.x / _ScreenParams.y;
                }
                float2 noiseUV = IN.texcoord;
                noiseUV.x *= aspect;

                float n = fbm(noiseUV * max(1.0, _NoiseScale));
                float edge = max(0.005, _DissolveEdgeWidth);

                // 1. Dissolved region: 100% solid pitch-black
                if (n < progress)
                {
                    #ifdef UNITY_UI_CLIP_RECT
                    float clipVal = UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                    return fixed4(0, 0, 0, clipVal);
                    #else
                    return fixed4(0, 0, 0, 1);
                    #endif
                }

                // 2. Undissolved region: 100% transparent
                if (n > progress + edge)
                {
                    return fixed4(0, 0, 0, 0);
                }

                // 3. Fiery Orange Border: strictly along [progress, progress + edge]
                float t = saturate((n - progress) / edge); // 0 at inner edge (next to black), 1 at outer edge

                // Bright white-yellow inner core (#FFF0A0)
                float3 coreColor = _EdgeCoreColor.rgb;
                // Blazing orange-red outer flame (#FF4500)
                float3 outerColor = _EdgeOuterColor.rgb;
                // Deep ember red for outer fringe
                float3 emberRed = float3(0.75, 0.1, 0.02);

                float3 fireRgb;
                if (t < 0.28)
                {
                    fireRgb = lerp(coreColor, outerColor, t / 0.28);
                }
                else
                {
                    fireRgb = lerp(outerColor, emberRed, (t - 0.28) / 0.72);
                }

                // Soft alpha falloff as it meets the undissolved world
                // At t = 0 (meeting black fill), alpha is solid 1.0
                // At t = 1 (outer edge), alpha falls smoothly to 0.0
                float fireAlpha = saturate(1.0 - pow(t, 1.4));

                #ifdef UNITY_UI_CLIP_RECT
                fireAlpha *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(fireAlpha - 0.001);
                #endif

                return fixed4(fireRgb, fireAlpha);
            }
            ENDCG
        }
    }
}
