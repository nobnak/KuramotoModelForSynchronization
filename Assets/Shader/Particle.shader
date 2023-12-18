Shader "Unlit/Particle" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Size ("Size", float) = 1

		_Highlight ("Highlight", Color) = (1,1,1,1)
		_Shadow ("Shadow", Color) = (0,0,0,1)
		_CohColor ("Coherence", Color) = (1,1,1,1)
    }
    SubShader {
        Tags { "RenderType"="Transparent" "Queue" = "Transparent" }

        HLSLINCLUDE
        
            #include "UnityCG.cginc"

            struct appdata {
                uint instanceID : SV_InstanceID;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            #include "Assets/ShaderLibrary/Particle.hlslinclude"
            #include "Packages/jp.nobnak.kuramoto_model/Runtime/Data/Particle3.cs.hlsl"
            #define FADE 0.5
			#define CIRCLE_IN_RADIAN 6.28318530718

            sampler2D _MainTex;

            StructuredBuffer<Particle3> _Particles;
			StructuredBuffer<float> _Phases;
			StructuredBuffer<float> _CohPhi;

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Color;
            float _Size;
            uint _ParticlesCount;

			float4 _Highlight;
			float4 _Shadow;
			float4 _CohColor;
            CBUFFER_END

            appdata vert(appdata v) {
                return v;
            }

            [maxvertexcount(4)]
            void geom (point appdata v[1], inout TriangleStream<v2f> stream) {
                uint instanceID = v[0].instanceID;
                if (instanceID >= _ParticlesCount) {
                    return;
                }

                Particle3 p = _Particles[instanceID];
                if (p.activity == 0) {
					return;
				}
				float phase = _Phases[instanceID];
				float cohPhi = _CohPhi[instanceID];

                float3 center_wc = mul(unity_ObjectToWorld, float4(p.pos, 1)).xyz;
                float size = _Size;
                float4 color = _Color;
                color *= lerp(_Shadow, _Highlight, 0.5 * (cos(phase * CIRCLE_IN_RADIAN) + 1.0));
					//* lerp(_CohColor, 1, sin((cohPhi - phase) * CIRCLE_IN_RADIAN));

                make_quad(stream, center_wc, size, color);
            }

            float4 frag (v2f i) : SV_Target {
                float4 cmain = tex2D(_MainTex, i.uv);
                float4 cout = cmain * i.color * _Color;
                return cout;
            }
        ENDHLSL

        Pass {
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            ENDHLSL
        }
    }
}
