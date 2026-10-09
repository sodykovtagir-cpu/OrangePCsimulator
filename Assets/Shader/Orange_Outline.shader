// Обводка выбранного предмета (первое нажатие — выбор, второе — перенос).
// Inverted hull: рисуем вывернутую наружу оболочку, раздутую по нормалям.
Shader "Orange/Outline"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (0.188, 0.835, 0.784, 0.9)
        _OutlineWidth("Width", Range(0.001, 0.2)) = 0.015
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "OutlineShell"
            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            fixed4 _OutlineColor;
            float _OutlineWidth;

            v2f vert(appdata v)
            {
                v2f o;
                v.vertex.xyz += normalize(v.normal) * _OutlineWidth;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
}
