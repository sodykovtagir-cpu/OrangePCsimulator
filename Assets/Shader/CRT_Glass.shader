Shader "OrangePC/CRT Glass"
{
    Properties
    {
        _ScreenUVRect ("Screen Mesh UV Rectangle (XY origin, ZW size)", Vector) = (0, 0, 1, 1)
        _ScanlineCount ("Scanlines", Range(32, 512)) = 160
        _ScanlineStrength ("Scanline Strength", Range(0, 0.6)) = 0.24
        _PhosphorCount ("RGB Phosphor Triads", Range(32, 640)) = 240
        _PhosphorStrength ("Phosphor Strength", Range(0, 0.4)) = 0.1
        _VignetteStrength ("Glass Edge Shading", Range(0, 0.6)) = 0.18
        _Brightness ("Brightness Compensation", Range(0.5, 1.5)) = 1.12
        _Tint ("Glass Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        // This is a world-space glass mesh attached to the CRT hardware, NOT a
        // desktop UI material or camera post-effect. Draw after world-space UI.
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" "ForceNoShadowCasting"="True" }
        Pass
        {
            Name "CRT_GLASS"
            Cull Back
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Blend DstColor Zero
            ColorMask RGB

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _ScreenUVRect;
            float _ScanlineCount;
            float _ScanlineStrength;
            float _PhosphorCount;
            float _PhosphorStrength;
            float _VignetteStrength;
            float _Brightness;
            float4 _Tint;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // The existing CRT screen uses a sub-rectangle of a texture atlas.
                // Normalize that rectangle without modifying the original mesh/UVs.
                o.uv = (v.uv - _ScreenUVRect.xy) / max(_ScreenUVRect.zw, float2(0.00001, 0.00001));
                return o;
            }

            float PatternVisibility(float phase)
            {
                // Average out subpixel patterns when viewed from a distance/angle
                // instead of allowing scanlines and RGB stripes to shimmer or moire.
                return 1.0 - smoothstep(0.25, 0.75, fwidth(phase));
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = saturate(i.uv);
                float linePhase = uv.y * _ScanlineCount;
                float lineWave = 0.5 + 0.5 * cos(linePhase * 6.28318530718) * PatternVisibility(linePhase);
                float scanlines = 1.0 - _ScanlineStrength * lineWave;

                float phosphorPhase = uv.x * _PhosphorCount;
                float3 channelPhase = phosphorPhase * 6.28318530718 + float3(0.0, -2.09439510239, 2.09439510239);
                float3 stripes = 0.5 - 0.5 * cos(channelPhase) * PatternVisibility(phosphorPhase);
                float3 phosphors = 1.0 - _PhosphorStrength * stripes;

                float2 centered = uv * 2.0 - 1.0;
                float edge = smoothstep(0.2, 1.0, dot(centered, centered) * 0.5);
                float vignette = 1.0 - _VignetteStrength * edge;
                float3 glass = scanlines * phosphors * vignette * _Brightness * _Tint.rgb;

                // Multiply the already-rendered screen in place. Black stays black,
                // alpha is untouched, and no desktop texture/material is ever changed.
                return float4(glass, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
