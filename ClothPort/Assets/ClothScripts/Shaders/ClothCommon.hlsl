#ifndef CLOTH_COMMON_INCLUDED
#define CLOTH_COMMON_INCLUDED

struct Spring
{
    uint particleA;
    uint particleB;

    float restingLength;
    float compliance;

    float lambda;

    uint isBroken;
    uint springType;

    float pad;
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

uint AddressOf(uint index, uint component)
{
    return (index * 3 + component) * 4;
}

#endif