Shader "Custom/MobileDiffuseLightmap" {
    Properties {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _Lightmap ("Lightmap", 2D) = "white" {}
    }
    SubShader {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "LightMode"="ForwardBase" }
        LOD 200

        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float3 normalDir : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float4 pos : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _Lightmap;

            v2f vert(appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(UNITY_MATRIX_M, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 lm  = tex2D(_Lightmap, i.uv);

                float3 N = normalize(i.normalDir);
                float3 L = normalize(UnityWorldSpaceLightDir(i.worldPos));
                float NdotL = saturate(dot(N, L));

                float3 ambient = ShadeSH9(float4(N, 1.0));
                float3 direct  = _LightColor0.rgb * NdotL;

                float3 lit = (ambient + direct);
                float3 final = col.rgb * lm.rgb * lit;

                return fixed4(final, col.a);
            }

            ENDHLSL
        }
    }

    Fallback "Mobile/Diffuse"
}
