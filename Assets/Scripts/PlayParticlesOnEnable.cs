using UnityEngine;

public class PlayParticlesOnEnable : MonoBehaviour
{
    private ParticleSystem[] particleSystems;

    private void Awake()
    {
        CacheParticleSystems();
    }

    private void OnEnable()
    {
        if (particleSystems == null || particleSystems.Length == 0)
        {
            CacheParticleSystems();
        }

        if (LitInfluenceParticleSystemController.TryRegisterAndApply(particleSystems))
        {
            return;
        }

        foreach (var ps in particleSystems)
        {
            if (ps == null)
            {
                continue;
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            ps.Play(true);
        }
    }

    private void CacheParticleSystems()
    {
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
    }
}
