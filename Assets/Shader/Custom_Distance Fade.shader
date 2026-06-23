Shader "Custom/Unlit/Merge_DistanceMask"
{
    Properties
    {
        // Properties from Merge
        _MainTex ("Main (Cell Color)", 2D) = "white" {}

        // Properties from DistanceMask
        _NearText ("Near Mask (Cell Pattern)", 2D) = "white" {}
        _FarText  ("Far Mask (Cell Pattern)", 2D) = "white" {}
        _FadeDistanceNear ("Near fade dist", Float) = 0.002
        _FadeDistanceFar  ("Far fade dist", Float) = 0.005
    }

    SubShader
    {
        // Tags from DistanceMask (for transparency)
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200
        // Use standard alpha blending
        Blend SrcAlpha OneMinusSrcAlpha 
        ZWrite Off
        Cull Off // double-sided

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // ST/texel sizes
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4 _NearText_TexelSize; 
            float4 _FarText_TexelSize;  

            // Distance fade floats
            float _FadeDistanceNear;
            float _FadeDistanceFar;

            struct Vertex_Stage_Input
            {
                float4 pos : POSITION;
                float2 uv  : TEXCOORD0;
            };

            struct Vertex_Stage_Output
            {
                float2 uv    : TEXCOORD0;
                float4 pos   : SV_POSITION;
                float  edepth : TEXCOORD1; // View-space depth
            };

            // --- VERTEX SHADER (Combined) ---
            Vertex_Stage_Output vert (Vertex_Stage_Input v)
            {
                Vertex_Stage_Output o;
                // UV from Merge
                o.uv  = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                
                // Position and Depth from DistanceMask
                float4 worldPos = mul(UNITY_MATRIX_M, v.pos);
                o.pos = mul(UNITY_MATRIX_VP, worldPos);
                float3 viewPos = mul(UNITY_MATRIX_MV, v.pos).xyz;
                o.edepth = -viewPos.z; // Pass view-space depth to frag
                
                return o;
            }

            // Texture/Sampler definitions
            UNITY_DECLARE_TEX2D(_MainTex);
            UNITY_DECLARE_TEX2D(_NearText);
            UNITY_DECLARE_TEX2D(_FarText);

            // SampleLevel compatibility (from Merge)
            #if defined(SHADER_API_GLES) && !defined(SHADER_API_GLES3)
                #define SAMPLE_L0(tex, samp, uv) tex.Sample(samp, uv)
            #else
                #define SAMPLE_L0(tex, samp, uv) tex.SampleLevel(samp, uv, 0)
            #endif

            // NearestUV function (from Merge)
            float2 NearestUV(float2 uv, float2 texelSize)
            {
                float2 t = max(texelSize, float2(1e-6, 1e-6));
                return (floor(uv / t + 0.5) + 0.5) * t;
            }

            // --- FRAGMENT SHADER (Combined) ---
            float4 frag (Vertex_Stage_Output i) : SV_TARGET
            {
                // --- Start of Merge UV logic ---
                float2 cellSize = max(_MainTex_TexelSize.xy * _MainTex_ST.xy, float2(1e-6, 1e-6));
                float2 cellIndex = floor(i.uv / cellSize + 1e-6);
                float2 cellUV = frac(i.uv / cellSize);
                float2 uvCenter = (cellIndex + 0.5) * cellSize;

                // MAIN: sample exactly at center (cell color)
                float4 tint = SAMPLE_L0(_MainTex, sampler_MainTex, uvCenter);

                // Anti-bleed at cell borders (The Anti-aliasing requested)
                float2 fw = fwidth(cellUV);
                float  eps = max(fw.x, fw.y) * 1.5;
                float2 uvClamped = clamp(cellUV, eps, 1.0 - eps);
                float  edgeDist = min(min(uvClamped.x, uvClamped.y), min(1.0 - uvClamped.x, 1.0 - uvClamped.y));
                float  borderMask = smoothstep(0.0, eps, edgeDist); 
                // --- End of Merge UV logic ---


                // --- Start of DistanceMask logic ---
                float2 nearNearest = NearestUV(uvClamped, _NearText_TexelSize.xy);
                float4 nearMaskTex = SAMPLE_L0(_NearText, sampler_NearText, nearNearest);
                float3 nearMask_rgb = nearMaskTex.rgb;
                float  nearMask_a   = nearMaskTex.a;   
                
                float2 farNearest = NearestUV(uvClamped, _FarText_TexelSize.xy);
                float4 farMaskTex  = SAMPLE_L0(_FarText,  sampler_FarText,  farNearest);
                // Far Mask Luminosity (used for far RGB and far Alpha)
                float farMask_lum = dot(farMaskTex.rgb, float3(0.299, 0.587, 0.114));
                float3 farMask_rgb = float3(farMask_lum, farMask_lum, farMask_lum);
                float  farMask_a   = farMask_lum;

                // Near state (modulating/multiplying the Far mask)
                float3 maskNear_rgb = nearMask_rgb * farMask_rgb;
                float  maskNear_a   = nearMask_a   * farMask_a;
                
                // Far state (just the Far mask)
                float3 maskFar_rgb = farMask_rgb;
                float  maskFar_a   = farMask_a;

                float3 final_rgb_mask;
                float  final_a_mask;

                // Distance Lerp logic
                if (i.edepth <= _FadeDistanceNear)
                {
                    final_rgb_mask = maskNear_rgb;
                    final_a_mask   = maskNear_a;
                }
                else if (i.edepth >= _FadeDistanceFar)
                {
                    final_rgb_mask = maskFar_rgb;
                    final_a_mask   = maskFar_a;
                }
                else
                {
                    float t = (i.edepth - _FadeDistanceNear) / (_FadeDistanceFar - _FadeDistanceNear);
                    final_rgb_mask = lerp(maskNear_rgb, maskFar_rgb, t);
                    final_a_mask   = lerp(maskNear_a,   maskFar_a,   t);
                }
                
                final_rgb_mask = saturate(final_rgb_mask);
                final_a_mask   = saturate(final_a_mask);
                // --- End of DistanceMask logic ---


                // --- Final Combination ---
                float4 outCol;
                
                // 1. Calculate the final RGB color
                outCol.rgb = tint.rgb * final_rgb_mask * borderMask;
                
                // 2. Use the BLACKNESS of the final RGB color as the final alpha
                float finalLuminosity = dot(outCol.rgb, float3(0.299, 0.587, 0.114));
                
                // CORRECTED: Blackness is 1.0 - Luminosity
                outCol.a = finalLuminosity; 

                return outCol;
            }

            ENDHLSL
        }
    }
}