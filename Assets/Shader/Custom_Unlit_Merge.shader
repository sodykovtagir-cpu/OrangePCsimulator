Shader "Custom/Unlit/Merge"
{
    Properties
    {
        _MainTex   ("Main",   2D) = "white" {}
        _SecondTex ("Second", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // ST/texel sizes Unity provides
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4 _SecondTex_TexelSize;

            struct Vertex_Stage_Input
            {
                float4 pos : POSITION;
                float2 uv  : TEXCOORD0;
            };

            struct Vertex_Stage_Output
            {
                float2 uv  : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            Vertex_Stage_Output vert (Vertex_Stage_Input v)
            {
                Vertex_Stage_Output o;
                o.uv  = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                o.pos = mul(UNITY_MATRIX_VP, mul(UNITY_MATRIX_M, v.pos));
                return o;
            }

            // HLSL objects (work on DX, Metal, GLES3; Unity cross-compiles for GLES2)
            Texture2D<float4> _MainTex;
            SamplerState      sampler_MainTex;
            Texture2D<float4> _SecondTex;
            SamplerState      sampler_SecondTex;

            // SampleLevel compatibility: on GLES2, fall back to regular Sample (no explicit LOD)
            #if defined(SHADER_API_GLES) && !defined(SHADER_API_GLES3)
                #define SAMPLE_L0(tex, samp, uv) tex.Sample(samp, uv)
            #else
                #define SAMPLE_L0(tex, samp, uv) tex.SampleLevel(samp, uv, 0)
            #endif

            // Snap an arbitrary UV to the nearest texel center given a texel size in UV units
            float2 NearestUV(float2 uv, float2 texelSize)
            {
                float2 t = max(texelSize, float2(1e-6, 1e-6));
                return (floor(uv / t + 0.5) + 0.5) * t;
            }

            float isNotWhite(float4 c)
            {
                float m = max(max(abs(c.r - 1.0), abs(c.g - 1.0)), abs(c.b - 1.0));
                return step(0.002, m);
            }

            float4 frag (Vertex_Stage_Output i) : SV_TARGET
            {
                // Size of one main-tex texel in UV space (after tiling)
                float2 cellSize = max(_MainTex_TexelSize.xy * _MainTex_ST.xy, float2(1e-6, 1e-6));

                // Robust cell index: bias to avoid boundary flips
                float2 cellIndex = floor(i.uv / cellSize + 1e-6);

                // Local UV inside cell [0,1)
                float2 cellUV = frac(i.uv / cellSize);

                // Center of the source texel in UV space (already a texel center)
                float2 uvCenter = (cellIndex + 0.5) * cellSize;

                // MAIN: sample exactly at center; on most APIs lock to LOD 0
                float4 tint = SAMPLE_L0(_MainTex, sampler_MainTex, uvCenter);

                // Anti-bleed at cell borders
                float2 fw = fwidth(cellUV);
                float  eps = max(fw.x, fw.y) * 1.5;
                float2 uvClamped = clamp(cellUV, eps, 1.0 - eps);

                float edgeDist   = min(min(uvClamped.x, uvClamped.y), min(1.0 - uvClamped.x, 1.0 - uvClamped.y));
                float borderMask = smoothstep(0.0, eps, edgeDist);

                // SECOND: force nearest by snapping to texel centers in 0..1 UV space
                float2 secondNearest = NearestUV(uvClamped, _SecondTex_TexelSize.xy);
                float4 second = SAMPLE_L0(_SecondTex, sampler_SecondTex, secondNearest);

                return tint * second * borderMask;
            }

            ENDHLSL
        }
    }
}
