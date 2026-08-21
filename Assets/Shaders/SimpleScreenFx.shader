Shader "Hidden/OrangePC/SimpleScreenFx"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_BloomTex ("Bloom", 2D) = "black" {}
		_PrevTex ("Prev", 2D) = "black" {}
	}
	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		// 0: extract bright
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			sampler2D _MainTex;
			float _Threshold;
			struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
			v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
			fixed4 frag(v2f i) : SV_Target
			{
				float3 c = tex2D(_MainTex, i.uv).rgb;
				float lum = dot(c, float3(0.2126, 0.7152, 0.0722));
				float w = saturate((lum - _Threshold) / max(1e-4, 1.0 - _Threshold));
				return float4(c * w, 1);
			}
			ENDCG
		}

		// 1: blur H
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			sampler2D _MainTex;
			float4 _MainTex_TexelSize;
			struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
			v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
			fixed4 frag(v2f i) : SV_Target
			{
				float2 t = float2(_MainTex_TexelSize.x, 0);
				float3 c = tex2D(_MainTex, i.uv).rgb * 0.227;
				c += tex2D(_MainTex, i.uv + t * 1.4).rgb * 0.316;
				c += tex2D(_MainTex, i.uv - t * 1.4).rgb * 0.316;
				c += tex2D(_MainTex, i.uv + t * 3.2).rgb * 0.070;
				c += tex2D(_MainTex, i.uv - t * 3.2).rgb * 0.070;
				return float4(c, 1);
			}
			ENDCG
		}

		// 2: blur V
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			sampler2D _MainTex;
			float4 _MainTex_TexelSize;
			struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
			v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
			fixed4 frag(v2f i) : SV_Target
			{
				float2 t = float2(0, _MainTex_TexelSize.y);
				float3 c = tex2D(_MainTex, i.uv).rgb * 0.227;
				c += tex2D(_MainTex, i.uv + t * 1.4).rgb * 0.316;
				c += tex2D(_MainTex, i.uv - t * 1.4).rgb * 0.316;
				c += tex2D(_MainTex, i.uv + t * 3.2).rgb * 0.070;
				c += tex2D(_MainTex, i.uv - t * 3.2).rgb * 0.070;
				return float4(c, 1);
			}
			ENDCG
		}

		// 3: composite + optional CA / vignette / camera motion blur
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			sampler2D _MainTex;
			sampler2D _BloomTex;
			sampler2D _PrevTex;
			float _Bloom;
			float _Vignette;
			float _Chromatic;
			float _Motion;
			struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
			v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
			fixed4 frag(v2f i) : SV_Target
			{
				float2 uv = i.uv;
				float2 n = uv * 2.0 - 1.0;

				float3 col = tex2D(_MainTex, uv).rgb;

				if (_Chromatic > 0.001)
				{
					float2 off = n * _Chromatic * 0.004;
					col.r = tex2D(_MainTex, uv + off).r;
					col.b = tex2D(_MainTex, uv - off).b;
				}

				if (_Bloom > 0.001)
					col += tex2D(_BloomTex, uv).rgb * _Bloom;

				if (_Vignette > 0.001)
				{
					float vig = saturate(1.0 - dot(n, n) * _Vignette);
					col *= vig;
				}

				if (_Motion > 0.001)
				{
					float3 prev = tex2D(_PrevTex, uv).rgb;
					col = lerp(col, prev, 0.45 * _Motion);
				}

				return float4(col, 1);
			}
			ENDCG
		}
	}
}
