Shader "Hidden/UiScreenBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Spread ("Spread", Float) = 3.0
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
            float _Spread;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 ts = _MainTex_TexelSize.xy * _Spread;
                fixed4 col = 0;

                // 9-tap box blur (сильный даунсэмпл + несколько проходов дают
                // интенсивное «матовое» размытие).
                static const float W = 1.0 / 9.0;
                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 o = float2(x, y) * ts;
                        col += tex2D(_MainTex, i.uv + o) * W;
                    }
                }
                return col;
            }
            ENDCG
        }
    }
}
