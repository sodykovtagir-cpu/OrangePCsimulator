Shader "Unlit/DepthOutline_Toned"
{
    Properties{
        _RimFactor("RimFactor", Range(0.0,5.0)) = 1.0
        _RimColor("RimColor", Color) = (1,0,0,1)
        _DistanceFactor("DistanceFactor", Range(0.0,10.0)) = 1.0
    }
    SubShader{
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector"="true" }
        Pass{
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #include "UnityCG.cginc"
            #pragma vertex vert
            #pragma fragment frag

            float _RimFactor;
            float4 _RimColor;
            float _DistanceFactor;
            sampler2D _CameraDepthTexture;

            struct a2v {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldViewDir : TEXCOORD2;
            };

            v2f vert(a2v v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.screenPos.z);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldViewDir = WorldSpaceViewDir(v.vertex).xyz;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // scene depth in eye space
                float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)));

                // replicate original distance calculation but tone it down
                float distance = 1.0 - saturate(sceneZ - i.screenPos.z);

                // remove tiny internal changes like original
                if (distance > 0.999999)
                    distance = 0.0;

                // gentler synthesis replacing removed params
                // smaller bias and exponent to soften curve
                const float synthBias = 0.5;
                const float synthExp = 0.8;

                // guard log argument
                float safeD = max(distance, 1e-6);
                distance = pow(saturate(_DistanceFactor * log(safeD) + synthBias), synthExp);

                // reduce overall depth influence
                distance = saturate(distance * 0.6);

                // rim by normal vs view dir, softened by multiplier
                float rim = 1.0 - abs(dot(normalize(i.worldNormal), normalize(i.worldViewDir)));
                rim = pow(saturate(rim), _RimFactor) * 0.6;

                // depth-derived main factor substitutes original main tex influence
                float mainFactor = saturate(1.0 - distance * 0.8);

                // build color with softer alphas
                float4 col = float4(0,0,0,0);
                col = lerp(col, float4(_RimColor.rgb, 0.2), mainFactor);
                col = float4(_RimColor.rgb, lerp(col.a, _RimColor.a * 0.6, distance));
                col = lerp(col, _RimColor, rim);

                return col;
            }

            ENDCG
        }
    }
    FallBack Off
}
