Shader "OrangePC/GridOverlay"
{
    // Визуальная сетка размещения Orange PC.
    // Ришется на плоском кваде, который ложится на поверхность под прицелом
    // (пол / стена / стол). Линии строятся по МИРОВЫМ координатам, поэтому
    // всегда совпадают с точками снапа PlacementGrid — предмет встаёт ровно
    // в центр подсвеченной клетки.
    //
    // Цвет — берёзовый (кремовые линии, чуть светлее подсветка клетки).
    //
    // Сетка не накладывается на перетаскиваемый предмет: OrangePC/GridOccluder
    // рисует вокруг предмета невидимый box и помечает его область стенсилом
    // (Ref 57). Этот шейдер пропускает пиксели только там, где стенсил НЕ
    // равен 57, поэтому сетка не просвечивает ни через стекло предмета, ни
    // там, где она геометрически ближе к камере, чем сам предмет.
    //
    // Совместимость: встроенный конвейер (built-in), GLES2/GLES3/Vulkan/GL.
    // Никаких производных (fwidth) и текстур — чтобы не ломалось на ГЛЕС2.

    Properties
    {
        _Color        ("Линия",                 Color)  = (0.95, 0.90, 0.78, 1)
        _Accent       ("Клетка под курсором",   Color)  = (1.00, 0.97, 0.86, 1)
        _CellSize     ("Размер клетки (м)",     Float)  = 0.5
        _LineHalfWidth("Полутолщина линии (м)", Float)  = 0.008
        _PixelWidth   ("Ширина в пикселях",     Float)  = 0.9
        _PixelScale   ("Мир/пиксель на 1 м",    Float)  = 0.001
        _MajorEvery   ("Жирная каждые N",       Float)  = 4
        _AxisU        ("Ось U",                 Vector) = (1,0,0,0)
        _AxisV        ("Ось V",                 Vector) = (0,0,1,0)
        _HighlightUV  ("Клетка (u,v,вкл,пол)",  Vector) = (0,0,0,0.5)
        _Opacity      ("Прозрачность",          Float)  = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+5"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "ForceNoShadowCasting" = "True"
            "DisableBatching" = "True"
        }

        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Lighting Off
            Fog { Mode Off }
            // Не рисуемся там, где OrangePC/GridOccluder пометил перетаскиваемый предмет.
            Stencil
            {
                Ref 57
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            float4 _Accent;
            float4 _AxisU;
            float4 _AxisV;
            float4 _HighlightUV;
            float  _CellSize;
            float  _LineHalfWidth;
            float  _PixelWidth;
            float  _PixelScale;
            float  _MajorEvery;
            float  _Opacity;

            struct Vertex_Stage_Input
            {
                float4 pos : POSITION;
                float2 uv  : TEXCOORD0;
            };

            struct Vertex_Stage_Output
            {
                float4 clip  : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float3 world : TEXCOORD1;
            };

            Vertex_Stage_Output vert (Vertex_Stage_Input v)
            {
                Vertex_Stage_Output o;
                float4 world = mul(unity_ObjectToWorld, v.pos);
                o.world = world.xyz;
                o.uv    = v.uv;
                o.clip  = mul(UNITY_MATRIX_VP, world);
                return o;
            }

            float4 frag (Vertex_Stage_Output i) : SV_Target
            {
                float cell = max(_CellSize, 0.0001);

                // Координаты в клетках. +0.5 сдвигает линии на полклетки,
                // чтобы точки снапа (кратные cell) были ЦЕНТРАМИ клеток.
                float u = dot(i.world, _AxisU.xyz);
                float v = dot(i.world, _AxisV.xyz);
                float2 g = float2(u, v) / cell + 0.5;

                // Расстояние до ближайшей линии сетки (в клетках -> в метрах)
                float2 dcell = abs(frac(g + 0.5) - 0.5);
                float  nearest = min(dcell.x, dcell.y);
                float  dist = nearest * cell;

                // Толщина: не меньше ~пикселя на экране, иначе сетка «исчезает»
                float camDist = distance(i.world, _WorldSpaceCameraPos);
                float w = max(_LineHalfWidth, camDist * _PixelScale * _PixelWidth);

                // Внимание: line — зарезервированное слово HLSL (примитив
                // отрисовки), поэтому переменная названа gridLine.
                float gridLine = 1.0 - smoothstep(w, w * 2.2, dist);

                // Каждая N-я линия — жирнее
                float2 gi = floor(g + 0.5);
                float majorEvery = max(_MajorEvery, 1.0);
                float mx = abs(gi.x) - floor(abs(gi.x) / majorEvery) * majorEvery;
                float my = abs(gi.y) - floor(abs(gi.y) / majorEvery) * majorEvery;
                float majorX = step(mx, 0.5);
                float majorY = step(my, 0.5);
                float major = lerp(majorY, majorX, step(dcell.x, dcell.y));

                float strength = gridLine * (1.0 + major * 0.85);

                // Подсветка клетки, куда встанет предмет
                float2 hd = abs(g - _HighlightUV.xy);
                float inCell = step(hd.x, _HighlightUV.w) * step(hd.y, _HighlightUV.w) * _HighlightUV.z;

                // Мягкое затухание к краям квада
                float r = length(i.uv - 0.5) * 2.0;
                float fade = 1.0 - smoothstep(0.45, 1.0, r);

                float a = saturate((strength * 0.9 + inCell * 0.30) * fade * _Opacity);
                float3 rgb = lerp(_Color.rgb, _Accent.rgb, inCell * 0.65);

                return float4(rgb, a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
