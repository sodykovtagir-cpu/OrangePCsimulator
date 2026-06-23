Shader "Custom/Shine" {
	Properties {
		_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
		_ShineTex ("Shine", 2D) = "white" {}
		_AnimateSped ("Animate Speed", Float) = 1
		_Brightness ("Brightness", Float) = 1
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			float4 _MainTex_ST;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Vertex_Stage_Output
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.uv = (input.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw;
				output.pos = mul(UNITY_MATRIX_VP, mul(UNITY_MATRIX_M, input.pos));
				return output;
			}

			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;

			Texture2D<float4> _ShineTex;
			SamplerState sampler_ShineTex;

			float _AnimateSped;
			float _Brightness;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				float4 baseCol = _MainTex.Sample(sampler_MainTex, input.uv.xy);

				// Vertical stretch (top-to-bottom) with horizontal motion
				float2 scroll = float2(frac(input.uv.x + _Time.y * _AnimateSped), 0.5);
				float4 shineCol = _ShineTex.Sample(sampler_ShineTex, scroll) * _Brightness;

				// Multiply shine with base
				float3 finalRGB = baseCol.rgb * shineCol.rgb;
				return float4(finalRGB, baseCol.a);
			}

			ENDHLSL
		}
	}
}
