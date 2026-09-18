// Тонкая IPS-панель.
//
// Зачем отдельный шейдер. У матричных дисплеев (LedDisplay 2x/4x) картинка
// растянута с крошечной текстуры вроде 32x16 на всю поверхность, поэтому между
// пикселями видна грубая сетка -- это читается как светодиодное табло, а не как
// экран. Здесь картинка выводится в полном разрешении, а «пиксельность»
// добавляется по-настоящему: каждый пиксель разбит на три вертикальные
// субполосы R/G/B, как в реальной IPS-матрице. С расстояния они сливаются в
// ровное изображение, а вблизи дают характерную структуру, а не чёрные щели.
//
// Сила эффекта настраивается: _SubpixelStrength = 0 полностью гладкий экран.
Shader "OrangePC/IPS Panel"
{
    Properties
    {
        _MainTex ("Screen", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(IPS Matrix)]
        // Ноль -- гладкая картинка без структуры. Единица -- максимально
        // выраженные субпиксели.
        _SubpixelStrength ("Subpixel Strength", Range(0,1)) = 0.35
        // Сколько пикселей матрицы приходится на экран по горизонтали.
        // Больше значение -- мельче структура.
        _PixelDensity ("Pixel Density", Float) = 320
        // Лёгкое затемнение между строками: без него матрица выглядит плоско.
        _RowGap ("Row Gap", Range(0,1)) = 0.12

        [Header(Panel)]
        // Подсветка: IPS всегда немного светится даже на чёрном.
        _Backlight ("Backlight", Range(0,1)) = 0.04
        _Brightness ("Brightness", Range(0,4)) = 1.15
        // Свечение стекла по углу обзора -- IPS почти не теряет цвет вбок,
        // поэтому эффект очень слабый.
        _Glare ("Glare", Range(0,1)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;

            float _SubpixelStrength;
            float _PixelDensity;
            float _RowGap;
            float _Backlight;
            float _Brightness;
            float _Glare;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 src = tex2D(_MainTex, i.uv) * _Color;
                float3 col = src.rgb * _Brightness;

                // ---- субпиксельная структура ----
                // Каждый пиксель матрицы делится на три вертикальные полосы:
                // красную, зелёную и синюю. Гасим в каждой полосе два чужих
                // канала, поэтому суммарная яркость сохраняется, а вблизи
                // видна настоящая структура IPS, а не чёрная сетка.
                float density = max(1.0, _PixelDensity);
                float sub = frac(i.uv.x * density) * 3.0;

                float3 mask = float3(
                    saturate(1.0 - abs(sub - 0.5)),
                    saturate(1.0 - abs(sub - 1.5)),
                    saturate(1.0 - abs(sub - 2.5)));

                // Нормируем: иначе экран темнеет там, где маска слабая.
                float peak = max(mask.r, max(mask.g, mask.b));
                mask = peak > 0.0001 ? mask / peak : float3(1, 1, 1);

                float3 tinted = col * mask;
                col = lerp(col, tinted, _SubpixelStrength);

                // ---- зазор между строками ----
                // Очень слабый: у IPS строки практически не видны, в отличие
                // от светодиодной матрицы.
                float row = frac(i.uv.y * density * 0.5);
                float gap = 1.0 - _RowGap * smoothstep(0.85, 1.0, row);
                col *= gap;

                // ---- подсветка ----
                // Чёрный на IPS не абсолютный: матрица подсвечена сзади.
                col += _Backlight * (0.6 + 0.4 * src.rgb);

                // ---- блик стекла ----
                float ndv = saturate(dot(normalize(i.worldNormal), normalize(i.viewDir)));
                col += _Glare * pow(1.0 - ndv, 4.0);

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }

    FallBack "Unlit/Texture"
}
