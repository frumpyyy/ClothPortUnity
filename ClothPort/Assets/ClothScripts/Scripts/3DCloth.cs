using NUnit.Framework;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor.ShaderGraph;
using UnityEngine;

public class GPUCloth : MonoBehaviour
{
    public ComputeShader integrateCompute;
    public ComputeShader springCompute;
    public ComputeShader deltasCompute;
    public ComputeShader resetLamdaCompute;
    public ComputeShader velocityCompute;
    public ComputeShader normalsCompute;


    public int Width = 30;
    public int Height = 30;

    public float Spacing = 1.0f;

    public float Friction = 0.7f;

    public float Damping = 0.7f;

    public float ClothComplianceMultiplier = 1.0f;

    public Vector3 gravity = new Vector3(0, -9.81f, 0);

    ComputeBuffer particleBuffer;

    ComputeBuffer springBuffer;

    ComputeBuffer deltaBuffer;

    ComputeBuffer normalBuffer;

    public int Iterations = 8;
    public int Substeps = 2;

    public float Clothcompilance = 0.0005f;

    GraphicsBuffer indexBuffer;
    ComputeBuffer drawArgsBuffer;

    [SerializeField]
    private Material clothMaterial;

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
    }

    void FixedUpdate()
    {
        Simulate(Time.fixedDeltaTime);


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
        particleBuffer?.Release();
        springBuffer?.Release();
        deltaBuffer?.Release();
        indexBuffer?.Release();
        drawArgsBuffer?.Release();
        normalBuffer?.Release();
    }

    void CreateRenderBuffers()
    {
        List<uint> indices = new();

        for (int y = 0; y < Height - 1; y++)
        {
            for (int x = 0; x < Width - 1; x++)
            {
                uint i0 = (uint)(y * Width + x);
                uint i1 = i0 + 1;
                uint i2 = i0 + (uint)Width;
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
        Particle[] particles = new Particle[Width * Height];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int i = y * Width + x;

                particles[i].position = new Vector3(x * Spacing, 0, y * Spacing);

                particles[i].prevPosition = particles[i].position;

                particles[i].invMass = 1.0f;
            }
        }

        particles[0].invMass = 0;
        particles[Width - 1].invMass = 0;

        int stride = Marshal.SizeOf<Particle>();

        particleBuffer = new ComputeBuffer(particles.Length, stride);

        particleBuffer.SetData(particles);

        deltaBuffer = new ComputeBuffer(Width * Height * 3, sizeof(float), ComputeBufferType.Raw);

        float[] zeros = new float[Width * Height * 3];

        deltaBuffer.SetData(zeros);

        normalBuffer = new ComputeBuffer(Width * Height, sizeof(float) * 3);

    }

    void CreateSprings()
    {
        List<Spring> springs = new List<Spring>();

        #region structural
        //horizontal structure springs
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width - 1; x++)
            {
                int a = y * Width + x;
                int b = a + 1;

                Spring s = new Spring();

                s.particleA = a;
                s.particleB = b;

                s.restingLength = Spacing;

                s.compliance = Clothcompilance;

                s.lambda = 0.0f;

                s.springType = (int)SpringType.Horizontal;

                springs.Add(s);
            }
        }

        //vertical structured springs
        for (int y = 0; y < Height - 1; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int a = y * Width + x;
                int b = a + Width;

                Spring s = new Spring();

                s.particleA = a;
                s.particleB = b;

                s.restingLength = Spacing;

                s.compliance = Clothcompilance;

                s.lambda = 0.0f;

                s.springType =
                    (int)SpringType.Vertical;

                springs.Add(s);
            }
        }
        #endregion structural

        #region shearing
        for (int y = 0; y < Height - 1; y++)
        {
            for (int x = 0; x < Width - 1; x++)
            {
                {
                    int a = y * Width + x;
                    int b = (y + 1) * Width + (x + 1);

                    Spring s = new Spring();

                    s.particleA = a;
                    s.particleB = b;

                    s.restingLength = Spacing * Mathf.Sqrt(2.0f);

                    s.compliance = Clothcompilance * 0.7f;
                    s.lambda = 0.0f;

                    s.springType = (int)SpringType.Shearing;

                    springs.Add(s);
                }

                {
                    int a = y * Width + (x + 1);
                    int b = (y + 1) * Width + x;

                    Spring s = new Spring();

                    s.particleA = a;
                    s.particleB = b;

                    s.restingLength = Spacing * Mathf.Sqrt(2.0f);

                    s.compliance = Clothcompilance * 0.7f;
                    s.lambda = 0.0f;

                    s.springType = (int)SpringType.Shearing;

                    springs.Add(s);
                }
            }
        }
        #endregion shearing 

        #region bending
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width - 2; x++)
            {
                int a = y * Width + x;
                int b = a + 2;

                Spring s = new Spring();

                s.particleA = a;
                s.particleB = b;

                s.restingLength = Spacing * 2.0f;

                s.compliance = Clothcompilance * 0.5f;
                s.lambda = 0.0f;

                s.springType = (int)SpringType.Bending;

                springs.Add(s);
            }
        }
        for (int y = 0; y < Height - 2; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int a = y * Width + x;
                int b = a + Width * 2;

                Spring s = new Spring();

                s.particleA = a;
                s.particleB = b;

                s.restingLength = Spacing * 2.0f;

                s.compliance = Clothcompilance * 0.5f;
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

    void Simulate(float dt)
    {
        for (int x = 0; x < Substeps; x++)
        {
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

            normalsCompute.SetInt("numParticles", Width * Height);

            normalsCompute.SetInt("clothWidth", Width);

            normalsCompute.SetInt("clothHeight", Height);

            //shader dispatch group sizing
            int particleGroups = Mathf.CeilToInt(Width * Height / 256.0f);

            normalsCompute.Dispatch(normalKernel, particleGroups, 1, 1);

            #endregion calculateNormals

            #region Integrate
            int kernel = integrateCompute.FindKernel("Integrate");

            //buffer setting
            integrateCompute.SetBuffer(kernel, "Particles", particleBuffer);

            //compute shader var setting
            integrateCompute.SetFloat("dt", dt / Substeps);

            integrateCompute.SetInt("numParticles", Width * Height);

            integrateCompute.SetVector("gravity", gravity);

            //diatpch integration shader
            integrateCompute.Dispatch(kernel, particleGroups, 1, 1);

            #endregion Integrate

            for (int i = 0; i < Iterations; i++)
            {
                #region SolveSprings

                int springKernel = springCompute.FindKernel("SolveSprings");

                //buffer setting
                springCompute.SetBuffer(springKernel, "Particles", particleBuffer);

                springCompute.SetBuffer(springKernel, "Springs", springBuffer);

                springCompute.SetBuffer(springKernel, "PositionDeltas", deltaBuffer);

                //compute shader var setting
                springCompute.SetInt("numSprings", springBuffer.count);

                springCompute.SetFloat("dt", dt / (Substeps * Iterations));

                springCompute.SetFloat("compliance", ClothComplianceMultiplier);

                springCompute.SetFloat("damping", Damping);

                //dispatch solve springs shaders
                springCompute.Dispatch(springKernel, solveSpringsGroup, 1, 1);

                #endregion SolveSprings

                #region ApplyDeltas

                int deltasKernal = deltasCompute.FindKernel("ApplyDeltas");

                //buffer setting
                deltasCompute.SetBuffer(deltasKernal, "Particles", particleBuffer);

                deltasCompute.SetBuffer(deltasKernal, "PositionDeltas", deltaBuffer);

                //compute shader var setting
                deltasCompute.SetInt("numParticles", Width * Height);

                //dispatch apply deltas shader
                deltasCompute.Dispatch(deltasKernal, particleGroups, 1, 1);

                #endregion ApplyDeltas

            }


            #region RecalculateVelocity

            int velocityKernal = velocityCompute.FindKernel("UpdateVelocity");

            //buffer setting
            velocityCompute.SetBuffer(velocityKernal, "Particles", particleBuffer);

            //compute shader var setting
            velocityCompute.SetInt("numParticles", Width * Height);

            velocityCompute.SetInt("numSubSteps", Substeps);

            velocityCompute.SetFloat("dt", dt);

            velocityCompute.SetFloat("friction", Friction);

            velocityCompute.SetFloat("damping", Damping);

            int particleGroups1 = Mathf.CeilToInt(Width * Height / 256.0f);

            //dispatch apply deltas shader
            velocityCompute.Dispatch(velocityKernal, particleGroups1, 1, 1);


            #endregion RecalculateVelocity
        }


    }


}

[StructLayout(LayoutKind.Sequential)]
public struct Particle
{
    public Vector3 position;
    float padding1;
    public Vector3 prevPosition;
    float padding2;
    public Vector3 velocity;
    public float invMass;
    public Vector3 accumulatedForce;
    float padding3;
    public Vector3 prevCollisionNormal;
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

    public int isBroken;
    public int springType;

    public float pad;
}
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