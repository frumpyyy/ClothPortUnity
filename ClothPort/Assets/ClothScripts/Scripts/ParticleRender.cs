using UnityEngine;

public class ParticleRender : MonoBehaviour
{
    public GPUCloth cloth;

    private Particle[] particles;

    void OnDrawGizmos()
    {
        if (cloth == null)
            return;

        //particles = cloth.GetParticles();

        if (particles == null)
            return;

        Gizmos.color = Color.red;

        foreach (var p in particles)
        {
            Gizmos.DrawSphere(
                p.position,
                0.025f
            );
        }
    }
}