using UnityEngine;

[CreateAssetMenu(fileName = "ClothConfig", menuName = "Scriptable Objects/ClothConfig")]
public class ClothConfig : ScriptableObject
{
    public int Width = 30;
    public int Height = 30;

    public float Spacing = 1.0f;

    public float Friction = 0.7f;

    public float Damping = 0.7f;

    public float ClothComplianceMultiplier = 1.0f;

    public Vector3 gravity = new Vector3(0, -9.81f, 0);

    public int Iterations = 8;
    public int Substeps = 2;

    public float Clothcompilance = 0.0005f;
}
