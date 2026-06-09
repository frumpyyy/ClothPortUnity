using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor.ShaderGraph;
using UnityEngine;
using UnityEngine.Animations;

public class GPUCloth : MonoBehaviour
{
    public ComputeShader integrateCompute;
    public ComputeShader springCompute;
    public ComputeShader deltasCompute;
    public ComputeShader resetLamdaCompute;
    public ComputeShader velocityCompute;
    public ComputeShader normalsCompute;
    public ComputeShader sphereCollisionCompute;
    public ComputeShader capsuleCollisionCompute;

    [SerializeField] private ClothConfig config;

    ComputeBuffer particleBuffer;

    ComputeBuffer springBuffer;

    ComputeBuffer deltaBuffer;

    ComputeBuffer normalBuffer;

    GraphicsBuffer indexBuffer;

    ComputeBuffer drawArgsBuffer;

    ComputeBuffer sphereBuffer;

    ComputeBuffer capsuleBuffer;

    [SerializeField]
    private Material clothMaterial;

    private int SphereCounter = 0;

    private Dictionary<SphereCollider, Vector3> _prevSpherePositions = new();

    private int CapsuleCounter = 0;

    private Dictionary<CapsuleCollider, Vector3> _prevCapsulePositions = new();

    void Start()
    {
        CreateParticles();

        CreateSprings();

        CreateRenderBuffers();

        clothMaterial.SetBuffer("Particles", particleBuffer);
        clothMaterial.SetBuffer("Indices", indexBuffer);
        clothMaterial.SetBuffer("Normals", normalBuffer);

        Debug.Log(Marshal.SizeOf<Spring>());
        Debug.Log(Marshal.SizeOf<Particle>());

        Debug.Log("Particle Count: " + particleBuffer.count);
        Debug.Log("Index Count: " + indexBuffer.count);

        if (ClothManager.instance != null)
            ClothManager.instance.RegisterCloth(this);

    }


    void Update()
    {

        Graphics.DrawProceduralIndirect(
            clothMaterial,
            new Bounds(
                transform.position,
                Vector3.one * 1000),
            MeshTopology.Triangles,
            drawArgsBuffer);
    }

    void OnDestroy()
    {
        if (ClothManager.instance != null)
            ClothManager.instance.UnregisterCloth(this);

        particleBuffer?.Release();
        springBuffer?.Release();
        deltaBuffer?.Release();
        indexBuffer?.Release();
        drawArgsBuffer?.Release();
        normalBuffer?.Release();
        sphereBuffer?.Release();
        capsuleBuffer?.Release();
    }

    void CreateRenderBuffers()
    {
        List<uint> indices = new();

        for (int y = 0; y < config.Height - 1; y++)
        {
            for (int x = 0; x < config.Width - 1; x++)
            {
                uint i0 = (uint)(y * config.Width + x);
                uint i1 = i0 + 1;
                uint i2 = i0 + (uint)config.Width;
                uint i3 = i2 + 1;

                indices.Add(i0);
                indices.Add(i2);
                indices.Add(i1);

                indices.Add(i1);
                indices.Add(i2);
                indices.Add(i3);
            }
        }

        indexBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Structured,
            indices.Count,
            sizeof(uint));

        indexBuffer.SetData(indices);

        uint[] args =
        {
        (uint)indices.Count,
        1,
        0,
        0
    };

        drawArgsBuffer = new ComputeBuffer(
            1,
            sizeof(uint) * 4,
            ComputeBufferType.IndirectArguments);

        drawArgsBuffer.SetData(args);
    }

    void CreateParticles()
    {
        Particle[] particles = new Particle[config.Width * config.Height];
        for (int y = 0; y < config.Height; y++)
        {
            for (int x = 0; x < config.Width; x++)
            {
                int i = y * config.Width + x;

                particles[i].position = new Vector3(x * config.Spacing, 0, y * config.Spacing);

                particles[i].prevPosition = particles[i].position;

                particles[i].invMass = 1.0f;
            }
        }

        particles[0].invMass = 0;
        particles[config.Width - 1].invMass = 0;

        int stride = Marshal.SizeOf<Particle>();

        particleBuffer = new ComputeBuffer(particles.Length, stride);

        particleBuffer.SetData(particles);

        deltaBuffer = new ComputeBuffer(config.Width * config.Height * 3, sizeof(float), ComputeBufferType.Raw);

        float[] zeros = new float[config.Width * config.Height * 3];

        deltaBuffer.SetData(zeros);

        normalBuffer = new ComputeBuffer(config.Width * config.Height, sizeof(float) * 3);

    }

    void CreateSprings()
    {
        List<Spring> springs = new List<Spring>();

        #region structural
        //horizontal structure springs
        for (int y = 0; y < config.Height; y++)
        {
            for (int x = 0; x < config.Width - 1; x++)
            {
                int a = y * config.Width + x;
                int b = a + 1;

                Spring s = new Spring();

                s.particleA = a;
                s.particleB = b;

                s.restingLength = config.Spacing;

                s.compliance = config.Clothcompilance;

                s.lambda = 0.0f;

                s.springType = (int)SpringType.Horizontal;

                springs.Add(s);
            }
        }

        //vertical structured springs
        for (int y = 0; y < config.Height - 1; y++)
        {
            for (int x = 0; x < config.Width; x++)
            {
                int a = y * config.Width + x;
                int b = a + config.Width;

                Spring s = new Spring();

                s.particleA = a;
                s.particleB = b;

                s.restingLength = config.Spacing;

                s.compliance = config.Clothcompilance;

                s.lambda = 0.0f;

                s.springType =
                    (int)SpringType.Vertical;

                springs.Add(s);
            }
        }
        #endregion structural

        #region shearing
        for (int y = 0; y < config.Height - 1; y++)
        {
            for (int x = 0; x < config.Width - 1; x++)
            {
                {
                    int a = y * config.Width + x;
                    int b = (y + 1) * config.Width + (x + 1);

                    Spring s = new Spring();

                    s.particleA = a;
                    s.particleB = b;

                    s.restingLength = config.Spacing * Mathf.Sqrt(2.0f);

                    s.compliance = config.Clothcompilance * 0.7f;
                    s.lambda = 0.0f;

                    s.springType = (int)SpringType.Shearing;

                    springs.Add(s);
                }

                {
                    int a = y * config.Width + (x + 1);
                    int b = (y + 1) * config.Width + x;

                    Spring s = new Spring();

                    s.particleA = a;
                    s.particleB = b;

                    s.restingLength = config.Spacing * Mathf.Sqrt(2.0f);

                    s.compliance = config.Clothcompilance * 0.7f;
                    s.lambda = 0.0f;

                    s.springType = (int)SpringType.Shearing;

                    springs.Add(s);
                }
            }
        }
        #endregion shearing 

        #region bending
        for (int y = 0; y < config.Height; y++)
        {
            for (int x = 0; x < config.Width - 2; x++)
            {
                int a = y * config.Width + x;
                int b = a + 2;

                Spring s = new Spring();

                s.particleA = a;
                s.particleB = b;

                s.restingLength = config.Spacing * 2.0f;

                s.compliance = config.Clothcompilance * 0.5f;
                s.lambda = 0.0f;

                s.springType = (int)SpringType.Bending;

                springs.Add(s);
            }
        }
        for (int y = 0; y < config.Height - 2; y++)
        {
            for (int x = 0; x < config.Width; x++)
            {
                int a = y * config.Width + x;
                int b = a + config.Width * 2;

                Spring s = new Spring();

                s.particleA = a;
                s.particleB = b;

                s.restingLength = config.Spacing * 2.0f;

                s.compliance = config.Clothcompilance * 0.5f;
                s.lambda = 0.0f;

                s.springType = (int)SpringType.Bending;

                springs.Add(s);
            }
        }
        #endregion bending

        int stride = Marshal.SizeOf<Spring>();

        springBuffer = new ComputeBuffer(springs.Count, stride);

        springBuffer.SetData(springs.ToArray());

        Debug.Log($"Created {springs.Count} springs");
    }

    void updateSphereBuffer()
    {
        SphereCollider[] colliders = FindObjectsByType<SphereCollider>();

        Sphere[] sphereData = new Sphere[colliders.Length];

        for (int i = 0; i < colliders.Length; i++)
        {
            SphereCollider sphereColl = colliders[i];

            Vector3 center = sphereColl.transform.TransformPoint(sphereColl.center);

            Vector3 sphereVelocity = Vector3.zero;

            if (_prevSpherePositions.TryGetValue(sphereColl, out Vector3 prevPos))
                sphereVelocity = (center - prevPos) / Time.deltaTime;

            _prevSpherePositions[sphereColl] = center;

            float sphereScale = Mathf.Max(sphereColl.transform.lossyScale.x,
                sphereColl.transform.lossyScale.y, sphereColl.transform.lossyScale.z);

            sphereData[i].Center = center;

            sphereData[i].CenterPrevious = sphereData[i].Center;

            sphereData[i].radius = sphereColl.radius * sphereScale;

            sphereData[i].velocity = sphereVelocity;
        }

        if (sphereBuffer == null || sphereBuffer.count != Mathf.Max(1, sphereData.Length))
        {
            sphereBuffer?.Release();

            sphereBuffer = new ComputeBuffer(Mathf.Max(1, sphereData.Length), Marshal.SizeOf<Sphere>());
        }

        if (sphereData.Length > 0)
        {
            sphereBuffer.SetData(sphereData);
            SphereCounter = sphereData.Length;
        }
    }
    void updateCapsuleBuffer()
    {
        CapsuleCollider[] colliders = FindObjectsByType<CapsuleCollider>();

        Capsule[] capsuleData = new Capsule[colliders.Length];

        for (int i = 0; i < colliders.Length; i++)
        {
            CapsuleCollider capsuleColl = colliders[i];

            Vector3 capsuleCentre = capsuleColl.transform.TransformPoint(capsuleColl.center);

            Vector3 capsuleVelocity = Vector3.zero;

            if (_prevCapsulePositions.TryGetValue(capsuleColl, out Vector3 prevPos))
                capsuleVelocity = (capsuleCentre - prevPos) / Time.deltaTime;

            _prevCapsulePositions[capsuleColl] = capsuleCentre;

            Vector3 capsuleAxis = capsuleColl.direction switch
            {
                0 => capsuleColl.transform.right,
                1 => capsuleColl.transform.up,
                2 => capsuleColl.transform.forward,
                _ => capsuleColl.transform.up //defaulting to up in the case of degenerate collision con
            };

            Vector3 lossyScale = capsuleColl.transform.lossyScale;

            float capsuleRadius = capsuleColl.direction switch
            {
                0 => capsuleColl.radius * Mathf.Max(lossyScale.y, lossyScale.z),
                1 => capsuleColl.radius * Mathf.Max(lossyScale.x, lossyScale.z),
                2 => capsuleColl.radius * Mathf.Max(lossyScale.x, lossyScale.y),
                _ => capsuleColl.radius * Mathf.Max(lossyScale.x, lossyScale.z)
            };

            float heightScalar = capsuleColl.direction switch
            {
                0 => lossyScale.x,
                1 => lossyScale.y,
                2 => lossyScale.z,
                _ => lossyScale.y
            };

            float capsuleHalfHeight = Mathf.Max(0, capsuleColl.height * heightScalar * 0.5f - capsuleRadius);

            capsuleData[i].hemisphereA = capsuleCentre + capsuleAxis * capsuleHalfHeight;

            capsuleData[i].hemisphereB = capsuleCentre - capsuleAxis * capsuleHalfHeight;

            capsuleData[i].radius = capsuleRadius;

            capsuleData[i].velocity = capsuleVelocity;

        }

        if (capsuleBuffer == null || capsuleBuffer.count != Mathf.Max(1, capsuleData.Length))
        {
            capsuleBuffer?.Release();

            capsuleBuffer = new ComputeBuffer(Mathf.Max(1, capsuleData.Length), Marshal.SizeOf<Capsule>());
        }

        if (capsuleData.Length > 0)
        {
            capsuleBuffer.SetData(capsuleData);
            CapsuleCounter = capsuleData.Length;
        }
    }

    public void Simulate(float dt)
    {
        for (int x = 0; x < config.Substeps; x++)
        {
            #region CollisionPrimitiveUpdating

            updateSphereBuffer();
            updateCapsuleBuffer();

            #endregion CollisionPrimitiveUpdating

            #region ResetLambda
            int resetLamdaKernel = resetLamdaCompute.FindKernel("ResetKernel");

            resetLamdaCompute.SetBuffer(resetLamdaKernel, "Springs", springBuffer);

            resetLamdaCompute.SetInt("numSprings", springBuffer.count);

            //shader dispatch group sizing
            int solveSpringsGroup = Mathf.CeilToInt(springBuffer.count / 256.0f);

            resetLamdaCompute.Dispatch(resetLamdaKernel, solveSpringsGroup, 1, 1);

            #endregion ResetLamda

            #region calculateNormals

            int normalKernel = normalsCompute.FindKernel("NormalCalc");

            normalsCompute.SetBuffer(normalKernel, "Particles", particleBuffer);

            normalsCompute.SetBuffer(normalKernel, "OutNormals", normalBuffer);

            normalsCompute.SetInt("numParticles", config.Width * config.Height);

            normalsCompute.SetInt("clothWidth", config.Width);

            normalsCompute.SetInt("clothHeight", config.Height);

            //shader dispatch group sizing
            int particleGroups = Mathf.CeilToInt(config.Width * config.Height / 256.0f);

            normalsCompute.Dispatch(normalKernel, particleGroups, 1, 1);

            #endregion calculateNormals

            #region Integrate
            int kernel = integrateCompute.FindKernel("Integrate");

            //buffer setting
            integrateCompute.SetBuffer(kernel, "Particles", particleBuffer);

            //compute shader var setting
            integrateCompute.SetFloat("dt", dt / config.Substeps);

            integrateCompute.SetInt("numParticles", config.Width * config.Height);

            integrateCompute.SetVector("gravity", config.gravity);

            //diatpch integration shader
            integrateCompute.Dispatch(kernel, particleGroups, 1, 1);

            #endregion Integrate

            for (int i = 0; i < config.Iterations; i++)
            {
                #region SolveSprings

                int springKernel = springCompute.FindKernel("SolveSprings");

                //buffer setting
                springCompute.SetBuffer(springKernel, "Particles", particleBuffer);

                springCompute.SetBuffer(springKernel, "Springs", springBuffer);

                springCompute.SetBuffer(springKernel, "PositionDeltas", deltaBuffer);

                //compute shader var setting
                springCompute.SetInt("numSprings", springBuffer.count);

                springCompute.SetInt("numSubsteps", config.Substeps);

                springCompute.SetFloat("dt", dt);

                springCompute.SetFloat("compliance", config.ClothComplianceMultiplier);

                springCompute.SetFloat("damping", config.Damping);

                //dispatch solve springs shaders
                springCompute.Dispatch(springKernel, solveSpringsGroup, 1, 1);

                #endregion SolveSprings

                #region SphereCollision

                int sphereKernel = sphereCollisionCompute.FindKernel("SphereCloth");

                sphereCollisionCompute.SetBuffer(sphereKernel, "Particles", particleBuffer);

                sphereCollisionCompute.SetBuffer(sphereKernel, "PositionDeltas", deltaBuffer);

                sphereCollisionCompute.SetBuffer(sphereKernel, "Spheres", sphereBuffer);

                sphereCollisionCompute.SetInt("numParticles", config.Width * config.Height);

                sphereCollisionCompute.SetInt("numIterations", config.Iterations);

                sphereCollisionCompute.SetInt("numSubSteps", config.Substeps);

                sphereCollisionCompute.SetInt("numSpheres", SphereCounter);

                sphereCollisionCompute.SetFloat("dt", dt);

                sphereCollisionCompute.SetFloat("Spacing", config.Spacing);

                //dispatch solve springs shaders
                sphereCollisionCompute.Dispatch(sphereKernel, particleGroups, 1, 1);

                #endregion SphereCollision

                #region CapsuleCollision

                int capsuleKernel = capsuleCollisionCompute.FindKernel("CapsuleCloth");

                capsuleCollisionCompute.SetBuffer(capsuleKernel, "Particles", particleBuffer);

                capsuleCollisionCompute.SetBuffer(capsuleKernel, "PositionDeltas", deltaBuffer);

                capsuleCollisionCompute.SetBuffer(capsuleKernel, "Capsules", capsuleBuffer);

                capsuleCollisionCompute.SetInt("numParticles", config.Width * config.Height);

                capsuleCollisionCompute.SetInt("numIterations", config.Iterations);

                capsuleCollisionCompute.SetInt("numSubSteps", config.Substeps);

                capsuleCollisionCompute.SetInt("numCapsules", CapsuleCounter);

                capsuleCollisionCompute.SetFloat("dt", dt);

                capsuleCollisionCompute.SetFloat("Spacing", config.Spacing);

                capsuleCollisionCompute.Dispatch(capsuleKernel, particleGroups, 1, 1);

                #endregion CapsuleCollision

                #region ApplyDeltas

                int deltasKernal = deltasCompute.FindKernel("ApplyDeltas");

                //buffer setting
                deltasCompute.SetBuffer(deltasKernal, "Particles", particleBuffer);

                deltasCompute.SetBuffer(deltasKernal, "PositionDeltas", deltaBuffer);

                //compute shader var setting
                deltasCompute.SetInt("numParticles", config.Width * config.Height);

                //dispatch apply deltas shader
                deltasCompute.Dispatch(deltasKernal, particleGroups, 1, 1);

                #endregion ApplyDeltas

            }


            #region RecalculateVelocity

            int velocityKernal = velocityCompute.FindKernel("UpdateVelocity");

            //buffer setting
            velocityCompute.SetBuffer(velocityKernal, "Particles", particleBuffer);

            //compute shader var setting
            velocityCompute.SetInt("numParticles", config.Width * config.Height);

            velocityCompute.SetInt("numSubSteps", config.Substeps);

            velocityCompute.SetFloat("dt", dt);

            velocityCompute.SetFloat("friction", config.Friction);

            velocityCompute.SetFloat("damping", config.Damping);

            int particleGroups1 = Mathf.CeilToInt(config.Width * config.Height / 256.0f);

            //dispatch apply deltas shader
            velocityCompute.Dispatch(velocityKernal, particleGroups1, 1, 1);


            #endregion RecalculateVelocity
        }


    }


}

[StructLayout(LayoutKind.Sequential)]
public struct Particle
{
    public float3 position;
    float padding1;
    public float3 prevPosition;
    float padding2;
    public float3 velocity;
    public float invMass;
    public float3 accumulatedForce;
    float padding3;
    public float3 prevCollisionNormal;
    float padding4;

}

[StructLayout(LayoutKind.Sequential)]
public struct Spring
{
    public int particleA;
    public int particleB;

    public float restingLength;
    public float compliance;
    public float lambda;
    public float pad;

    public int isBroken;
    public int springType;
}

[StructLayout(LayoutKind.Sequential)]
public struct Sphere
{
    public float3 Center;
    public float radius;

    public float3 CenterPrevious;
    public float pad;

    public float3 velocity;
    public float pad1;
}

[StructLayout(LayoutKind.Sequential)]
public struct Capsule
{
    public float3 hemisphereA;
    public float radius;

    public float3 hemisphereB;
    public float pad;

    public float3 velocity;
    public float pad1;
};

public enum SpringType
{
    Vertical = 0,
    Horizontal = 1,
    Shearing = 2,
    Bending = 3
}

[StructLayout(LayoutKind.Sequential)]
public struct Delta
{
    public Vector3 value;
}