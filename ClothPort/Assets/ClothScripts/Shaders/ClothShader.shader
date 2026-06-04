Shader "Custom/ClothShader"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {

            Name "ForwardLit"

            Cull Off
            
            HLSLPROGRAM

            

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Particle
            {
                float3 position;
                float padding1;

                float3 prevPosition;
                float padding2;

                float3 velocity;
                float invMass;

                float3 accumulatedForce;
                float padding3;

                float3 prevCollisionNormal;
                float padding4;
            };

            StructuredBuffer<Particle> Particles;
            StructuredBuffer<uint> Indices;
            StructuredBuffer<float3> Normals;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                uint particleIndex = Indices[IN.vertexID];

                float3 worldPos = Particles[particleIndex].position;

                float3 normal = Normals[particleIndex];

                OUT.positionHCS = TransformWorldToHClip(worldPos);

                OUT.normalWS = normalize(normal);

                return OUT;
            }

            half4 frag(Varyings IN, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                Light mainLight = GetMainLight();

                float3 N = normalize(IN.normalWS);

                if(!isFrontFace) N = -N;

                float NdotL = saturate(dot(N, mainLight.direction));

                return _BaseColor * NdotL;
            }

            ENDHLSL
        }
    }
}