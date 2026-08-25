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
			float _Grain;
		float _Motion;
		float2 _MotionDir;
		float _AO;
		float4 _MainTex_TexelSize;
			UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
			struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
			v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
			fixed4 frag(v2f i) : SV_Target
			{
				float2 uv = i.uv;
				float2 ndc = uv * 2.0 - 1.0;

				float3 col = tex2D(_MainTex, uv).rgb;

				// Сначала размытие по сырому кадру, потом виньетка/зерно —
				// иначе края смешиваются с невиньетированными сэмплами и мигают.
				if (_Motion > 0.001)
				{
					// Мягкий гаусс вдоль вектора камеры — без отдельных «копий» кадра.
					float2 d = _MotionDir;
					float3 acc = 0;
					float wsum = 0;
					[unroll]
					for (int s = -5; s <= 5; s++)
					{
						float t = s / 5.0;
						float w = exp(-t * t * 2.8);
						acc += tex2D(_MainTex, saturate(uv + d * t)).rgb * w;
						wsum += w;
					}
					col = lerp(col, acc / max(wsum, 1e-4), saturate(_Motion));
				}

				if (_AO > 0.001)
				{
					float d = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
					float2 t = _MainTex_TexelSize.xy * 4.0;
					float occ = 0;
					occ += saturate((d - LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv + float2(t.x, 0)))) * 0.6);
					occ += saturate((d - LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv + float2(-t.x, 0)))) * 0.6);
					occ += saturate((d - LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv + float2(0, t.y)))) * 0.6);
					occ += saturate((d - LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv + float2(0, -t.y)))) * 0.6);
					occ += saturate((d - LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv + t))) * 0.6);
					occ += saturate((d - LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv - t))) * 0.6);
					occ += saturate((d - LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv + float2(t.x, -t.y)))) * 0.6);
					occ += saturate((d - LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv + float2(-t.x, t.y)))) * 0.6);
					occ = saturate(occ / 8.0);
					occ = occ * occ;
					col *= 1.0 - occ * _AO;
				}

				if (_Bloom > 0.001)
					col += tex2D(_BloomTex, uv).rgb * _Bloom;

				if (_Vignette > 0.001)
				{
					float vig = saturate(1.0 - dot(ndc, ndc) * _Vignette);
					col *= vig;
				}

				if (_Grain > 0.001)
				{
					float gn = frac(sin(dot(uv * float2(1280, 720) + _Time.y * 32.0, float2(12.9898, 78.233))) * 43758.5453);
					col += (gn - 0.5) * _Grain;
				}

				return float4(col, 1);
			}
			ENDCG
		}
	}
}
