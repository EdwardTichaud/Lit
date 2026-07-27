using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AncientFlameEnemyBlocker : MonoBehaviour
{
    [SerializeField] private Flame flame;
    [SerializeField, Min(0.1f)] private float enemyProximityRange = 8f;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.2f;
    [SerializeField] private Color blockedFlameColor = new Color(0.2f, 0.65f, 1f, 1f);

    private readonly Dictionary<Light, LightPresentation> lightPresentations = new Dictionary<Light, LightPresentation>();
    private readonly Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient> particleColors = new Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient>();
    private float nextRefreshTime;
    private bool isBlocked;

    private void Awake()
    {
        if (flame == null)
        {
            flame = GetComponent<Flame>();
        }

        CachePresentations();
    }

    private void OnEnable()
    {
        EvaluateBlockState();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        EvaluateBlockState();
    }

    private void OnDisable()
    {
        ApplyBlockState(false);
    }

    private void EvaluateBlockState()
    {
        nextRefreshTime = Time.unscaledTime + refreshInterval;
        ApplyBlockState(HasNearbyLivingEnemy());
    }

    private bool HasNearbyLivingEnemy()
    {
        RealTimeCombatEnemy[] enemies = FindObjectsOfType<RealTimeCombatEnemy>();
        float rangeSqr = enemyProximityRange * enemyProximityRange;
        for (int i = 0; i < enemies.Length; i++)
        {
            RealTimeCombatEnemy enemy = enemies[i];
            if (enemy == null || !enemy.gameObject.activeInHierarchy || (enemy.Health != null && enemy.Health.IsDead))
            {
                continue;
            }

            if ((enemy.transform.position - transform.position).sqrMagnitude <= rangeSqr)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyBlockState(bool blocked)
    {
        if (isBlocked == blocked)
        {
            return;
        }

        isBlocked = blocked;
        CachePresentations();
        flame?.SetExternalSuppression(blocked);

        foreach (KeyValuePair<Light, LightPresentation> pair in lightPresentations)
        {
            if (pair.Key == null)
            {
                continue;
            }

            pair.Key.color = blocked ? blockedFlameColor : pair.Value.color;
            pair.Key.useColorTemperature = blocked ? false : pair.Value.useColorTemperature;
        }

        foreach (KeyValuePair<ParticleSystem, ParticleSystem.MinMaxGradient> pair in particleColors)
        {
            if (pair.Key == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = pair.Key.main;
            main.startColor = blocked ? new ParticleSystem.MinMaxGradient(blockedFlameColor) : pair.Value;
        }
    }

    private void CachePresentations()
    {
        Light[] lights = GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light != null && !lightPresentations.ContainsKey(light))
            {
                lightPresentations.Add(light, new LightPresentation(light.color, light.useColorTemperature));
            }
        }

        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle != null && !particleColors.ContainsKey(particle))
            {
                particleColors.Add(particle, particle.main.startColor);
            }
        }
    }

    private readonly struct LightPresentation
    {
        public readonly Color color;
        public readonly bool useColorTemperature;

        public LightPresentation(Color color, bool useColorTemperature)
        {
            this.color = color;
            this.useColorTemperature = useColorTemperature;
        }
    }
}
