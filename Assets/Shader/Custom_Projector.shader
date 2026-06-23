Shader "Custom/Projector"
{
    Properties
    {
        _ShadowTex ("Cookie", 2D) = "white" {}
        _FalloffTex ("FallOff", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" }

        Pass
        {
            ZWrite Off
            ZTest LEqual
            Cull Back                 // don't hit the back faces
            Offset -2, -2             // stronger bias to avoid z-fight
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct v2f {
                float4 pos       : SV_POSITION;
                float4 uvShadow  : TEXCOORD0;   // projector UVW
                float4 uvFalloff : TEXCOORD1;   // projector clip UVW
                UNITY_FOG_COORDS(2)
            };

            float4x4 unity_Projector;
            float4x4 unity_ProjectorClip;

            v2f vert (float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.uvShadow  = mul(unity_Projector,     vertex);
                o.uvFalloff = mul(unity_ProjectorClip, vertex);
                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }

            sampler2D _ShadowTex;
            sampler2D _FalloffTex;

            fixed4 frag (v2f i) : SV_Target
            {
                // clip to projector frustum in XY and Z to stop backside/overspray
                float2 uv = i.uvShadow.xy / i.uvShadow.w;
                clip(uv.x); clip(1.0 - uv.x);
                clip(uv.y); clip(1.0 - uv.y);

                float z = i.uvFalloff.z / i.uvFalloff.w; // 0..1 inside frustum
                clip(z); clip(1.0 - z);

                float3 rgb = tex2Dproj(_ShadowTex, UNITY_PROJ_COORD(i.uvShadow)).rgb;
                clip(dot(rgb, fixed3(1, 1, 1)) - .01);
              
                fixed4 res = fixed4(rgb, (.6 / (z * 2)));

                UNITY_APPLY_FOG_COLOR(i.fogCoord, res, fixed4(1, 1, 1, 1));
                return res;
            }
            ENDCG
        }
    }
}
