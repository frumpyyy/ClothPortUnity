#ifndef CLOTH_COMMON_INCLUDED
#define CLOTH_COMMON_INCLUDED

struct Spring
{
    uint particleA;
    uint particleB;

    float restingLength;
    float compliance;
    float lambda;
    float pad;
    uint isBroken;
    uint springType;
};

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

struct Sphere
{
    float3 Center;
    float radius;
    float3 CenterPrevious;
    float pad;
    float3 velocity;
    float pad1;
};

struct Capsule
{
    float3 hemisphereA;
    float radius;

    float3 hemisphereB;
    float pad;

    float3 velocity;
    float pad1;
};


uint AddressOf(uint index, uint component)
{
    return (index * 3 + component) * 4;
}

#endif