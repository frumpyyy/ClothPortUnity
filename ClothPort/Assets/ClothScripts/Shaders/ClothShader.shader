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
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                uint particleIndex = Indices[IN.vertexID];

                float3 worldPos = Particles[particleIndex].position;

                OUT.positionHCS = TransformWorldToHClip(worldPos);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _BaseColor;
            }

            ENDHLSL
        }
    }
}