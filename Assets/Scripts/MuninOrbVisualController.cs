using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Presentation-only controller for Munin's orb. It preserves the authored HDRP
/// materials and only drives particle visibility, sort order and lightweight
/// state pulses on the instantiated prefab.
/// </summary>
[DisallowMultipleComponent]
public sealed class MuninOrbVisualController : MonoBehaviour
{
    public enum VisualState
    {
        Rest,
        Attention,
        Action
    }

    [Header("State")]
    [SerializeField] private VisualState initialState = VisualState.Rest;
    [SerializeField, Min(0.01f)] private float stateBlendSpeed = 6f;
    [SerializeField, Min(0f)] private float attentionPulseScale = 0.035f;
    [SerializeField, Min(0.01f)] private float attentionDuration = 0.7f;
    [SerializeField, Min(0f)] private float actionPulseScale = 0.12f;
    [SerializeField, Min(0.01f)] private float actionPulseDuration = 0.28f;
    [SerializeField, Min(0.01f)] private float actionDuration = 0.45f;

    [Header("Layers")]
    [SerializeField, Tooltip("The permanent distortion layer is disabled because it is costly and causes the most visible transparency artifacts near the camera.")]
    private bool disableContinuousDistortion = true;
    [SerializeField, Tooltip("Particle systems whose names contain one of these tokens are treated as transient action layers.")]
    private string[] actionLayerTokens = { "burst", "impact" };
    [SerializeField, Tooltip("Particle systems whose names contain one of these tokens are treated as additive halo layers.")]
    private string[] haloLayerTokens = { "glow", "lens" };
    [SerializeField, Tooltip("Particle systems whose names contain one of these tokens are treated as the stable core.")]
    private string[] coreLayerTokens = { "sphere" };
    [SerializeField, Tooltip("Particle systems whose names contain one of these tokens are disabled outside an explicit action.")]
    private string[] distortionLayerTokens = { "distort" };
    [SerializeField, Range(0.01f, 0.2f)] private float maxParticleScreenSize = 0.07f;

    private readonly List<ParticleSystem> actionSystems = new List<ParticleSystem>();
    private readonly List<ParticleSystem> distortionSystems = new List<ParticleSystem>();
    private ParticleSystemRenderer[] particleRenderers = Array.Empty<ParticleSystemRenderer>();
    private MuninController munin;
    private Vector3 baseLocalScale;
    private VisualState state;
    private float stateIntensity;
    private float transientStateRemaining;
    private float actionPulseRemaining;
    private bool initialized;

    public VisualState State => state;

    private void Awake()
    {
        CachePresentation();
        state = initialState;
        baseLocalScale = transform.localScale;
        initialized = true;
        ApplyState(true);
    }

    private void OnEnable()
    {
        CachePresentation();
        if (!initialized)
        {
            state = initialState;
            baseLocalScale = transform.localScale;
            initialized = true;
        }

        BindMunin();
        ApplyState(true);
    }

    private void OnDisable()
    {
        UnbindMunin();
        if (initialized)
        {
            transform.localScale = baseLocalScale;
        }
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        float targetIntensity = state == VisualState.Rest ? 0f : 1f;
        stateIntensity = Mathf.MoveTowards(stateIntensity, targetIntensity, stateBlendSpeed * deltaTime);

        if (actionPulseRemaining > 0f)
        {
            actionPulseRemaining = Mathf.Max(0f, actionPulseRemaining - deltaTime);
        }

        if (transientStateRemaining > 0f)
        {
            transientStateRemaining = Mathf.Max(0f, transientStateRemaining - deltaTime);
            if (transientStateRemaining <= 0f && state != VisualState.Rest)
            {
                state = VisualState.Rest;
                ApplyState(false);
            }
        }

        float actionPulse = actionPulseDuration > 0f
            ? Mathf.Sin((actionPulseRemaining / actionPulseDuration) * Mathf.PI)
            : 0f;
        float attentionPulse = state == VisualState.Attention
            ? (0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 1.4f)) * stateIntensity
            : 0f;
        float scaleOffset = attentionPulse * attentionPulseScale + actionPulse * actionPulseScale;
        transform.localScale = baseLocalScale * (1f + scaleOffset);
    }

    public void SetState(VisualState nextState)
    {
        if (state == nextState)
        {
            return;
        }

        state = nextState;
        transientStateRemaining = state == VisualState.Attention
            ? attentionDuration
            : state == VisualState.Action ? actionDuration : 0f;
        if (state == VisualState.Action)
        {
            TriggerActionPulse();
        }

        ApplyState(false);
    }

    public void TriggerActionPulse()
    {
        actionPulseRemaining = actionPulseDuration;
        for (int i = 0; i < actionSystems.Count; i++)
        {
            ParticleSystem system = actionSystems[i];
            if (system != null)
            {
                system.Play(true);
            }
        }
    }

    /// <summary>Returns the orb to its quiet presentation immediately.</summary>
    public void SetRestState()
    {
        state = VisualState.Rest;
        transientStateRemaining = 0f;
        ApplyState(false);
    }

    private void BindMunin()
    {
        MuninController resolved = GetComponentInParent<MuninController>();
        if (munin == resolved)
        {
            return;
        }

        UnbindMunin();
        munin = resolved;
        if (munin == null)
        {
            return;
        }

        munin.ChargeUseRejected += OnChargeUseRejected;
        munin.ChargesSpent += OnChargesSpent;
        munin.ChargeRewardReceived += OnChargeRewardReceived;
    }

    private void UnbindMunin()
    {
        if (munin == null)
        {
            return;
        }

        munin.ChargeUseRejected -= OnChargeUseRejected;
        munin.ChargesSpent -= OnChargesSpent;
        munin.ChargeRewardReceived -= OnChargeRewardReceived;
        munin = null;
    }

    private void OnChargeUseRejected(MuninController _)
    {
        SetState(VisualState.Attention);
        TriggerActionPulse();
    }

    private void OnChargesSpent(MuninController _, int __)
    {
        SetState(VisualState.Action);
    }

    private void OnChargeRewardReceived(MuninController _, int __, string ___)
    {
        SetState(VisualState.Attention);
        TriggerActionPulse();
    }

    private void CachePresentation()
    {
        actionSystems.Clear();
        distortionSystems.Clear();

        ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem system = systems[i];
            string name = system.name;
            if (ContainsAny(name, actionLayerTokens))
            {
                actionSystems.Add(system);
            }

            if (ContainsAny(name, distortionLayerTokens))
            {
                distortionSystems.Add(system);
            }
        }

        particleRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < particleRenderers.Length; i++)
        {
            ParticleSystemRenderer renderer = particleRenderers[i];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.maxParticleSize = maxParticleScreenSize;

            string name = renderer.name;
            renderer.sortingFudge = ContainsAny(name, coreLayerTokens) ? -0.1f
                : ContainsAny(name, haloLayerTokens) ? 0.1f
                : ContainsAny(name, actionLayerTokens) ? 0.15f
                : 0f;
        }
    }

    private void ApplyState(bool immediate)
    {
        stateIntensity = immediate && state != VisualState.Rest ? 1f : stateIntensity;
        bool actionActive = state == VisualState.Action;
        for (int i = 0; i < distortionSystems.Count; i++)
        {
            ParticleSystem system = distortionSystems[i];
            if (system == null)
            {
                continue;
            }

            if (disableContinuousDistortion && !actionActive)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            else if (actionActive)
            {
                system.Play(true);
            }
        }
    }

    private static bool ContainsAny(string value, string[] tokens)
    {
        if (string.IsNullOrEmpty(value) || tokens == null)
        {
            return false;
        }

        for (int i = 0; i < tokens.Length; i++)
        {
            if (!string.IsNullOrEmpty(tokens[i]) && value.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
