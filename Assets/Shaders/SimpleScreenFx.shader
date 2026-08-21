Shader "Hidden/OrangePC/SimpleScreenFx"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_PrevTex ("Prev", 2D) = "black" {}
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

			sampler2D _MainTex;
			sampler2D _PrevTex;
			float _Vignette;
			float _Chromatic;
			float _Bloom;
			float _Motion;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float2 uv = i.uv;
				float2 n = uv * 2.0 - 1.0;

				float3 col;
				if (_Chromatic > 0.001)
				{
					float2 off = n * _Chromatic * 0.012;
					col.r = tex2D(_MainTex, uv + off).r;
					col.g = tex2D(_MainTex, uv).g;
					col.b = tex2D(_MainTex, uv - off).b;
				}
				else
				{
					col = tex2D(_MainTex, uv).rgb;
				}

				if (_Bloom > 0.001)
				{
					float3 acc = 0;
					acc += tex2D(_MainTex, uv + float2(0.003, 0)).rgb;
					acc += tex2D(_MainTex, uv + float2(-0.003, 0)).rgb;
					acc += tex2D(_MainTex, uv + float2(0, 0.003)).rgb;
					acc += tex2D(_MainTex, uv + float2(0, -0.003)).rgb;
					acc += tex2D(_MainTex, uv + float2(0.006, 0.004)).rgb;
					acc += tex2D(_MainTex, uv + float2(-0.006, -0.004)).rgb;
					acc *= 0.1667;
					float3 highlight = saturate(acc - 0.45);
					col += highlight * _Bloom;
				}

				if (_Motion > 0.001)
				{
					float3 prev = tex2D(_PrevTex, uv).rgb;
					col = lerp(col, prev, 0.45 * _Motion);
				}

				float vig = saturate(1.0 - dot(n, n) * _Vignette);
				col *= vig;
				return float4(col, 1);
			}
			ENDCG
		}
	}
}
