Shader "UI/FrostedGlass"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlurAmount ("Blur Mix", Range(0,1)) = 0.85
        _NoiseAmount ("Noise Amount", Range(0,1)) = 0.025
        _NoiseFreq ("Noise Density", Range(0.01, 6)) = 2.0
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            // Глобальная размытая копия экрана (заполняется DisplayManager).
            // Если её нет — используем белый (будет просто тинт).
            sampler2D _UIScreenBlur;
            fixed4 _Color;
            float _BlurAmount;
            float _NoiseAmount;
            float _NoiseFreq;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 base = tex2D(_MainTex, i.texcoord) * _Color * i.color;

                // Размытый фон под панелью (экранные UV). Размытие снимается кадром
                // ранее — окна, открытые ДО появления панели, уже попадают в блюр.
                float2 suv = i.screenPos.xy / i.screenPos.w;
                fixed4 blur = tex2D(_UIScreenBlur, suv);
                // Если блюр чёрный/пустой (нет провайдера) — не подмешиваем его.
                float hasBlur = (blur.a > 0.001) ? 1.0 : 0.0;
                fixed3 bg = blur.rgb;

                // Смешиваем тинт панели с размытым фоном.
                fixed3 rgb = lerp(base.rgb, bg, _BlurAmount * hasBlur);

                // Очень мелкое зерно.
                float2 np = i.worldPosition.xy * max(_NoiseFreq, 0.0001);
                float n = vnoise(np) * 0.7 + hash21(floor(np)) * 0.3;
                float grain = (n - 0.5) * _NoiseAmount;
                rgb += grain;

                fixed4 outCol = fixed4(rgb, base.a);

                #ifdef UNITY_UI_CLIP_RECT
                outCol.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(outCol.a - 0.001);
                #endif

                return outCol;
            }
            ENDCG
        }
    }
}
