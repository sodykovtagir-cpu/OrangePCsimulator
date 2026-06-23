Shader "Custom/Diffuse/Color" {
    Properties {
        _Color ("Color", Vector) = (0.5,0.5,0.5,1)
        _MainTex ("Base (RGB)", 2D) = "white" {}
    }
    //DummyShaderTextExporter
    SubShader{
        Tags { "RenderType"="Opaque" "LightMode"="ForwardBase" }
        LOD 200

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            float4 _MainTex_ST;

            Texture2D<float4> _MainTex;
            SamplerState sampler_MainTex;
            float4 _Color;

            struct Vertex_Stage_Input
            {
                float4 pos : POSITION;
                float2 uv  : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct Vertex_Stage_Output
            {
                float2 uv  : TEXCOORD0;
                float4 pos : SV_POSITION;
                float3 wN  : TEXCOORD1;
                float3 wP  : TEXCOORD2;
            };

            Vertex_Stage_Output vert(Vertex_Stage_Input input)
            {
                Vertex_Stage_Output output;
                float4 worldPos = mul(UNITY_MATRIX_M, input.pos);
                output.pos = mul(UNITY_MATRIX_VP, worldPos);
                output.uv  = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.wP  = worldPos.xyz;
                output.wN  = UnityObjectToWorldNormal(input.normal);
                return output;
            }

            float4 frag(Vertex_Stage_Output input) : SV_TARGET
            {
                float4 tex = _MainTex.Sample(sampler_MainTex, input.uv);
                float3 N = normalize(input.wN);
                float3 L = normalize(UnityWorldSpaceLightDir(input.wP));
                float  NdotL = saturate(dot(N, L));

                float3 ambient = ShadeSH9(float4(N,1.0));
                float3 albedo  = tex.rgb * _Color.rgb;
                float3 lit     = albedo * (ambient + _LightColor0.rgb * NdotL);

                return float4(lit, tex.a * _Color.a);
            }

            ENDHLSL
        }
    }
    Fallback "Mobile/VertexLit"
}
