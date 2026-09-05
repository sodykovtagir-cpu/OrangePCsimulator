Shader "Hidden/UiScreenBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            // Направление размытия в текселях: (spread,0) — горизонталь,
            // (0,spread) — вертикаль. Сепарабельный гаусс (H/V проходы).
            float4 _BlurDir;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 dir = _BlurDir.xy * _MainTex_TexelSize.xy;

                // 9-tap Gaussian weights (гладко, без «квадратов» box blur).
                static const half w0 = 0.2270270270;
                static const half w1 = 0.1945945946;
                static const half w2 = 0.1216216216;
                static const half w3 = 0.0540540541;
                static const half w4 = 0.0162162162;

                fixed4 col = tex2D(_MainTex, i.uv) * w0;
                col += tex2D(_MainTex, i.uv + dir * 1.0) * w1;
                col += tex2D(_MainTex, i.uv - dir * 1.0) * w1;
                col += tex2D(_MainTex, i.uv + dir * 2.0) * w2;
                col += tex2D(_MainTex, i.uv - dir * 2.0) * w2;
                col += tex2D(_MainTex, i.uv + dir * 3.0) * w3;
                col += tex2D(_MainTex, i.uv - dir * 3.0) * w3;
                col += tex2D(_MainTex, i.uv + dir * 4.0) * w4;
                col += tex2D(_MainTex, i.uv - dir * 4.0) * w4;
                return col;
            }
            ENDCG
        }
    }
}
