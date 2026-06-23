Shader "Wireframe/Cull" {
    Properties {
        _LineColor ("Line color", Vector) = (0,0,0,1)
        _LineSize  ("Line size (Pixels)", Float) = 1.0
    }

    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass {
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex   vert
            #pragma geometry geom
            #pragma fragment frag

            float4x4 unity_ObjectToWorld;
            float4x4 unity_MatrixVP;

            float4 _LineColor;
            float  _LineSize;

            struct Vertex_Stage_Input {
                float4 pos : POSITION;
                uint   vid : SV_VertexID;
            };

            struct Vertex_Stage_Output {
                float4 pos  : SV_POSITION;
                float3 bary : TEXCOORD0; 
            };

            Vertex_Stage_Output vert (Vertex_Stage_Input IN) {
                Vertex_Stage_Output OUT;
                OUT.pos  = mul(unity_MatrixVP, mul(unity_ObjectToWorld, IN.pos));
                OUT.bary = 0;
                return OUT;
            }

            [maxvertexcount(3)]
            void geom(triangle Vertex_Stage_Output IN[3],
                      inout TriangleStream<Vertex_Stage_Output> triStream)
            {
                Vertex_Stage_Output V;
                V = IN[0]; V.bary = float3(1,0,0); triStream.Append(V);
                V = IN[1]; V.bary = float3(0,1,0); triStream.Append(V);
                V = IN[2]; V.bary = float3(0,0,1); triStream.Append(V);
            }

            float4 frag (Vertex_Stage_Output IN) : SV_TARGET {
                // 1. Distance to edge
                float d = min(min(IN.bary.x, IN.bary.y), IN.bary.z);

                // 2. Rate of change (derivative)
                float ddx = fwidth(d);

                // 3. Convert to Screen Pixels
                float pixelDist = d / (ddx + 1e-4);

                // 4. Cutout
                // If the pixel distance is greater than our line size, discard the pixel.
                // We use 'clip' which discards if the value is negative.
                clip(_LineSize - pixelDist);

                return _LineColor;
            }
            ENDHLSL
        }
    }
}