Shader "Custom/GUI/TextShader_WithDepthCulling"
{
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _Color   ("Text Color",    Color) = (1,1,1,1)
    }

    // ------------------------------------------------------------------------
    // SubShader – the only pass we need for UI text
    // ------------------------------------------------------------------------
    SubShader
    {
        // --------------------------------------------------------------
        // Render‑state block – this is the part that makes the text
        // participate in Z‑culling while staying transparent.
        // --------------------------------------------------------------
        Tags
        {
            "RenderType" = "Transparent"   // tells Unity it’s a transparent material
            "Queue"      = "Transparent"   // draws after opaque geometry
            "IgnoreProjector" = "True"     // (optional, same as Unity’s default UI shader)
        }

        // Blend for alpha‑transparent fonts → premultiplied or straight‑alpha.
        // Most UI fonts use straight alpha, so we use SrcAlpha / OneMinusSrcAlpha.
        Blend SrcAlpha OneMinusSrcAlpha

        // Depth handling – this is what gives you “culling”.
        ZWrite Off                 // we don’t want to overwrite the depth buffer with UI
        ZTest LEqual               // discard fragments that are behind something already rendered
        Cull Off                   // UI quads are usually double‑sided (can be changed to Back)

        LOD 200

        Pass
        {
            // --------------------------------------------------------------
            // HLSL program
            // --------------------------------------------------------------
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   2.0   // keep it compatible with all platforms

            // --------------------------------------------------------------
            // Uniforms & built‑in matrices
            // --------------------------------------------------------------
            float4x4 unity_ObjectToWorld;
            float4x4 unity_MatrixVP;          // = UNITY_MATRIX_MVP
            float4   _MainTex_ST;             // tiling/offset

            // --------------------------------------------------------------
            // Vertex input / output structs
            // --------------------------------------------------------------
            struct Vertex_Stage_Input
            {
                float4 pos : POSITION;   // vertex position (object space)
                float2 uv  : TEXCOORD0;  // UVs
            };

            struct Vertex_Stage_Output
            {
                float2 uv  : TEXCOORD0;  // passed to fragment shader
                float4 pos : SV_POSITION; // clip‑space position
            };

            // --------------------------------------------------------------
            // Vertex shader – same as your original
            // --------------------------------------------------------------
            Vertex_Stage_Output vert (Vertex_Stage_Input v)
            {
                Vertex_Stage_Output o;
                o.uv = (v.uv * _MainTex_ST.xy) + _MainTex_ST.zw;
                // Transform from object → world → clip space
                o.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, v.pos));
                return o;
            }

            // --------------------------------------------------------------
            // Fragment (pixel) shader
            // --------------------------------------------------------------
            Texture2D<float4> _MainTex;
            SamplerState       sampler_MainTex;
            float4             _Color;

            struct Fragment_Stage_Input
            {
                float2 uv : TEXCOORD0;
            };

            float4 frag (Fragment_Stage_Input i) : SV_TARGET
            {
                // Sample the font texture and apply the tint colour.
                // Alpha from the texture will be multiplied by _Color.a.
                return _MainTex.Sample(sampler_MainTex, i.uv) * _Color;
            }
            ENDHLSL
        }
    }

    // ------------------------------------------------------------------------
    // Fallback – use Unity’s built‑in UI/default shader if something goes wrong.
    // ------------------------------------------------------------------------
    Fallback "UI/Default"
}
