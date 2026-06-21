using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class Sablier : MonoBehaviour
{
    private struct ParticleEmissionSnapshot
    {
        public float rateOverTimeMultiplier;
        public float rateOverDistanceMultiplier;
    }

    private struct ParticleFadeState
    {
        public ParticleSystem system;
        public float elapsed;
        public float duration;
    }

    private enum RotationAxis
    {
        X = 0,
        Y = 1,
        Z = 2,
    }

    [Header("Target")]
    [SerializeField, Tooltip("Objet a faire tourner. Si vide, utilise ce GameObject.")]
    private Transform target;
    [Header("Control")]
    [SerializeField, Tooltip("Si faux, le script n'agit pas.")]
    private bool isRunning = true;

    [Header("Rotation")]
    [SerializeField, Tooltip("Axe de rotation du tour complet.")]
    private RotationAxis axis = RotationAxis.Y;
    [SerializeField, Tooltip("Utilise l'axe local de la cible au lieu de l'axe monde.")]
    private bool useLocalSpace = true;
    [SerializeField, Min(0.01f), Tooltip("Duree d'un tour complet de 360 degres (s).")]
    private float rotationDuration = 1f;
    [SerializeField, Tooltip("Lance la boucle automatiquement a l'activation.")]
    private bool playOnEnable = true;

    [Header("Loop")]
    [SerializeField, Min(0f), Tooltip("Temps d'attente entre deux rotations completes (s).")]
    private float waitDuration = 1f;
    [Header("Phase Particles")]
    [SerializeField, Tooltip("Particle systems lances pendant la phase de rotation.")]
    private ParticleSystem[] playDuringRotation;
    [SerializeField, Tooltip("Particle systems lances pendant la phase d'attente.")]
    private ParticleSystem[] playDuringWait;
    [SerializeField, Min(0f), Tooltip("Duree de reduction progressive de l'emission avant l'arret des particles (s).")]
    private float stopEmissionFadeDuration = 0.35f;

    private Transform TargetTransform => target != null ? target : transform;

    private Quaternion cycleStartRotation;
    private float phaseTimer;
    private bool isWaiting;
    private bool wasRunningLastFrame;
    private readonly List<ParticleFadeState> activeParticleFades = new List<ParticleFadeState>();
    private readonly Dictionary<ParticleSystem, ParticleEmissionSnapshot> particleEmissionSnapshots = new Dictionary<ParticleSystem, ParticleEmissionSnapshot>();

    private void OnEnable()
    {
        ResetCycle();
        wasRunningLastFrame = isRunning;

        if (!playOnEnable)
        {
            isWaiting = true;
        }

        if (!isRunning)
        {
            return;
        }

        ApplyPhaseParticleStates();
    }

    private void OnDisable()
    {
        RestoreTrackedParticleEmission();
        activeParticleFades.Clear();
    }

    private void Update()
    {
        float deltaTime = Application.isPlaying ? Time.deltaTime : 0.016f;
        HandleRunningStateChange();
        UpdateParticleFades(deltaTime);

        if (!isRunning)
        {
            return;
        }

        Transform currentTarget = TargetTransform;
        if (currentTarget == null)
        {
            return;
        }

        phaseTimer += deltaTime;

        if (isWaiting)
        {
            if (phaseTimer >= waitDuration)
            {
                BeginRotationCycle();
            }

            return;
        }

        float normalized = Mathf.Clamp01(phaseTimer / Mathf.Max(0.01f, rotationDuration));
        ApplyRotation(normalized);

        if (normalized < 1f)
        {
            return;
        }

        ApplyRotation(1f);
        BeginWaitCycle();
    }

    private void ResetCycle()
    {
        cycleStartRotation = GetCurrentRotation();
        phaseTimer = 0f;
        isWaiting = false;
    }

    private void BeginRotationCycle()
    {
        cycleStartRotation = GetCurrentRotation();
        phaseTimer = 0f;
        isWaiting = false;
        ApplyPhaseParticleStates();
    }

    private void BeginWaitCycle()
    {
        SetCurrentRotation(cycleStartRotation);
        phaseTimer = 0f;
        isWaiting = true;
        ApplyPhaseParticleStates();
    }

    private void ApplyRotation(float normalized)
    {
        Quaternion rotated = cycleStartRotation * Quaternion.AngleAxis(normalized * 360f, GetAxisVector());
        SetCurrentRotation(rotated);
    }

    private Quaternion GetCurrentRotation()
    {
        Transform currentTarget = TargetTransform;
        return useLocalSpace ? currentTarget.localRotation : currentTarget.rotation;
    }

    private void SetCurrentRotation(Quaternion rotation)
    {
        Transform currentTarget = TargetTransform;
        if (useLocalSpace)
        {
            currentTarget.localRotation = rotation;
            return;
        }

        currentTarget.rotation = rotation;
    }

    private Vector3 GetAxisVector()
    {
        return axis switch
        {
            RotationAxis.X => Vector3.right,
            RotationAxis.Y => Vector3.up,
            _ => Vector3.forward,
        };
    }

    private void ApplyPhaseParticleStates()
    {
        if (isWaiting)
        {
            FadeOutParticleSystems(playDuringRotation);
            PlayParticleSystems(playDuringWait);
            return;
        }

        FadeOutParticleSystems(playDuringWait);
        PlayParticleSystems(playDuringRotation);
    }

    private void HandleRunningStateChange()
    {
        if (wasRunningLastFrame == isRunning)
        {
            return;
        }

        if (!isRunning)
        {
            RestoreTrackedParticleEmission();
            activeParticleFades.Clear();
        }
        else
        {
            ApplyPhaseParticleStates();
        }

        wasRunningLastFrame = isRunning;
    }

    private void PlayParticleSystems(ParticleSystem[] systems)
    {
        List<ParticleSystem> targets = CollectParticleSystems(systems);
        for (int i = 0; i < targets.Count; i++)
        {
            ParticleSystem current = targets[i];
            CancelParticleFade(current, restoreEmission: true);
            CaptureParticleEmission(current);
            current.Play(withChildren: false);
        }
    }

    private void FadeOutParticleSystems(ParticleSystem[] systems)
    {
        List<ParticleSystem> targets = CollectParticleSystems(systems);
        for (int i = 0; i < targets.Count; i++)
        {
            ParticleSystem current = targets[i];
            CancelParticleFade(current, restoreEmission: true);
            CaptureParticleEmission(current);

            if (stopEmissionFadeDuration <= 0f)
            {
                current.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmitting);
                continue;
            }

            activeParticleFades.Add(new ParticleFadeState
            {
                system = current,
                elapsed = 0f,
                duration = stopEmissionFadeDuration,
            });
        }
    }

    private void UpdateParticleFades(float deltaTime)
    {
        for (int i = activeParticleFades.Count - 1; i >= 0; i--)
        {
            ParticleFadeState fade = activeParticleFades[i];
            if (fade.system == null || !particleEmissionSnapshots.TryGetValue(fade.system, out ParticleEmissionSnapshot snapshot))
            {
                activeParticleFades.RemoveAt(i);
                continue;
            }

            fade.elapsed += deltaTime;
            float normalized = fade.duration <= 0f ? 1f : Mathf.Clamp01(fade.elapsed / fade.duration);
            SetParticleEmission(fade.system, snapshot, 1f - normalized);

            if (normalized >= 1f)
            {
                fade.system.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmitting);
                RestoreParticleEmission(fade.system);
                activeParticleFades.RemoveAt(i);
                continue;
            }

            activeParticleFades[i] = fade;
        }
    }

    private List<ParticleSystem> CollectParticleSystems(ParticleSystem[] roots)
    {
        List<ParticleSystem> results = new List<ParticleSystem>();
        if (roots == null)
        {
            return results;
        }

        HashSet<ParticleSystem> uniqueSystems = new HashSet<ParticleSystem>();
        for (int i = 0; i < roots.Length; i++)
        {
            ParticleSystem root = roots[i];
            if (root == null)
            {
                continue;
            }

            ParticleSystem[] nestedSystems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int j = 0; j < nestedSystems.Length; j++)
            {
                ParticleSystem current = nestedSystems[j];
                if (current != null && uniqueSystems.Add(current))
                {
                    results.Add(current);
                }
            }
        }

        return results;
    }

    private void CancelParticleFade(ParticleSystem system, bool restoreEmission)
    {
        for (int i = activeParticleFades.Count - 1; i >= 0; i--)
        {
            if (activeParticleFades[i].system != system)
            {
                continue;
            }

            activeParticleFades.RemoveAt(i);
        }

        if (restoreEmission)
        {
            RestoreParticleEmission(system);
        }
    }

    private void CaptureParticleEmission(ParticleSystem system)
    {
        if (system == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = system.emission;
        particleEmissionSnapshots[system] = new ParticleEmissionSnapshot
        {
            rateOverTimeMultiplier = emission.rateOverTimeMultiplier,
            rateOverDistanceMultiplier = emission.rateOverDistanceMultiplier,
        };
    }

    private void RestoreTrackedParticleEmission()
    {
        foreach (KeyValuePair<ParticleSystem, ParticleEmissionSnapshot> pair in particleEmissionSnapshots)
        {
            RestoreParticleEmission(pair.Key);
        }
    }

    private void RestoreParticleEmission(ParticleSystem system)
    {
        if (system == null || !particleEmissionSnapshots.TryGetValue(system, out ParticleEmissionSnapshot snapshot))
        {
            return;
        }

        SetParticleEmission(system, snapshot, 1f);
    }

    private static void SetParticleEmission(ParticleSystem system, ParticleEmissionSnapshot snapshot, float multiplier)
    {
        if (system == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = system.emission;
        float clampedMultiplier = Mathf.Clamp01(multiplier);
        emission.rateOverTimeMultiplier = snapshot.rateOverTimeMultiplier * clampedMultiplier;
        emission.rateOverDistanceMultiplier = snapshot.rateOverDistanceMultiplier * clampedMultiplier;
    }
}
