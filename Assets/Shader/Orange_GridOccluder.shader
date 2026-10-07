Shader "OrangePC/GridOccluder"
{
    // Невидимый окклюдер перетаскиваемого предмета для сетки размещения.
    //
    // Рисуется на очереди ПЕРЕД сеткой (Transparent+4, сетка — Transparent+5),
    // ничего не закрашивает (ColorMask 0) и не пишет глубину (ZWrite Off), а
    // только помечает стенсилом (Ref 57) область предмета. Шейдер сетки
    // OrangePC/GridOverlay пропускает пиксели только там, где стенсил не равен
    // 57 — поэтому сетка не накладывается на предмет, который тащат:
    // ни на прозрачные части (стекло корпуса), ни там, где сетка на полу
    // геометрически ближе к камере, чем сам предмет.
    //
    // Box-объект создаёт и двигает PlacementGridVisual по габаритам предмета.
    // Совместимость: встроенный конвейер (built-in), HLSL.

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+4"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "ForceNoShadowCasting" = "True"
            "DisableBatching" = "True"
        }

        Pass
        {
            ZWrite Off
            ZTest LEqual
            Cull Off
            ColorMask 0
            Lighting Off
            Fog { Mode Off }
            Stencil
            {
                Ref 57
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Vertex_Stage_Input
            {
                float4 pos : POSITION;
            };

            struct Vertex_Stage_Output
            {
                float4 clip : SV_POSITION;
            };

            Vertex_Stage_Output vert (Vertex_Stage_Input v)
            {
                Vertex_Stage_Output o;
                float4 world = mul(unity_ObjectToWorld, v.pos);
                o.clip = mul(UNITY_MATRIX_VP, world);
                return o;
            }

            float4 frag (Vertex_Stage_Output i) : SV_Target
            {
                // Цвет неважен — пиксель всё равно не пишется (ColorMask 0).
                return float4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
