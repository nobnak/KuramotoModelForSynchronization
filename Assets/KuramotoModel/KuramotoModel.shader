Shader "Unlit/KuramotoModel"{
	Properties{
		_MainTex ("Texture", 2D) = "white" {}
		_Highlight ("Highlight", Color) = (1,1,1,1)
		_Shadow ("Shadow", Color) = (0,0,0,1)

		_CohColor ("Coherence", Color) = (1,1,1,1)
	}
	SubShader{
		Tags { "RenderType"="Opaque" }

		Pass{
			CGPROGRAM
			#define CIRCLE_IN_RADIAN 6.28318530718
			#pragma target 4.0
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			
			static const float3 VERTICES[4] = {
				float3(-0.5, -0.5, 0),
				float3( 0.5, -0.5, 0),
				float3(-0.5,  0.5, 0),
				float3( 0.5,  0.5, 0)
			};
			static const float2 UVS[4] = {
				float2(0, 0),
				float2(1, 0),
				float2(0, 1),
				float2(1, 1)
			};
			static const int INDICES[6] = {
				0,3,1,	0,2,3
			};
						 
			struct appdata {
				uint vid : SV_VertexID;
				uint iid : SV_InstanceID;
			};
			struct v2f	{
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _Highlight;
			float4 _Shadow;
			float4 _CohColor;
			StructuredBuffer<float4x4> _ParticleModelMatrices;
			StructuredBuffer<float> _Phases;
			StructuredBuffer<float> _CohPhi;
			
			v2f vert (appdata v){
				uint index = INDICES[v.vid];
				float3 vertex = VERTICES[index];
				float2 uv = UVS[index];
				float4x4 particleModel = _ParticleModelMatrices[v.iid];
				float phase = _Phases[v.iid];
				float cohPhi = _CohPhi[v.iid];

				v2f o;
				o.vertex = UnityObjectToClipPos(mul(particleModel, float4(vertex, 1)));
				o.uv = TRANSFORM_TEX(uv, _MainTex);
				o.color = lerp(_Shadow, _Highlight, 0.5 * (cos(phase * CIRCLE_IN_RADIAN) + 1.0))
					* lerp(_CohColor, 1, sin((cohPhi - phase) * CIRCLE_IN_RADIAN));
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target {
				fixed4 col = tex2D(_MainTex, i.uv);
				return col * i.color;
			}
			ENDCG
		}
	}
}
