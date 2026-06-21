using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class CameraBreathingEffect : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Transform a animer. Laisser vide pour utiliser ce GameObject.")]
    [SerializeField] private Transform target;

    [Header("Breathing")]
    [SerializeField] private bool effectEnabled = true;
    [SerializeField, Min(0f), Tooltip("Multiplicateur global de l'effet.")]
    private float intensity = 1f;
    [SerializeField, Min(0.01f), Tooltip("Cycles de respiration par seconde.")]
    private float frequency = 0.85f;
    [SerializeField, Range(0f, 1f), Tooltip("Decalage de phase de la respiration.")]
    private float phaseOffset;
    [SerializeField, Tooltip("Utilise le temps non ralenti par Time.timeScale.")]
    private bool useUnscaledTime = true;

    [Header("Motion")]
    [SerializeField, Tooltip("Amplitude locale de position en metres.")]
    private Vector3 positionAmplitude = new Vector3(0.015f, 0.035f, 0.02f);
    [SerializeField, Tooltip("Amplitude locale de rotation en degres.")]
    private Vector3 rotationAmplitude = new Vector3(0.2f, 0.08f, 0.12f);
    [SerializeField, Tooltip("Retire le dernier offset quand le composant est desactive.")]
    private bool resetOffsetOnDisable = true;

    private Transform lastTarget;
    private Vector3 lastPositionOffset;
    private Quaternion lastRotationOffset = Quaternion.identity;
    private Vector3 lastAppliedPosition;
    private Quaternion lastAppliedRotation = Quaternion.identity;
    private bool hasAppliedOffset;

    private void LateUpdate()
    {
        Transform resolvedTarget = ResolveTarget();
        if (resolvedTarget == null)
        {
            ClearRuntimeState();
            return;
        }

        if (resolvedTarget != lastTarget)
        {
            ClearRuntimeState();
            lastTarget = resolvedTarget;
        }

        Vector3 basePosition = resolvedTarget.localPosition;
        Quaternion baseRotation = resolvedTarget.localRotation;
        if (TryRemoveLastOffset(resolvedTarget, ref basePosition, ref baseRotation))
        {
            resolvedTarget.localPosition = basePosition;
            resolvedTarget.localRotation = baseRotation;
        }

        if (!effectEnabled || intensity <= 0f)
        {
            ClearRuntimeState(keepTarget: true);
            return;
        }

        float phase = EvaluatePhase();
        Vector3 positionOffset = EvaluatePositionOffset(phase);
        Quaternion rotationOffset = EvaluateRotationOffset(phase);

        resolvedTarget.localPosition = basePosition + positionOffset;
        resolvedTarget.localRotation = baseRotation * rotationOffset;

        lastPositionOffset = positionOffset;
        lastRotationOffset = rotationOffset;
        lastAppliedPosition = resolvedTarget.localPosition;
        lastAppliedRotation = resolvedTarget.localRotation;
        hasAppliedOffset = true;
    }

    private void OnDisable()
    {
        if (!resetOffsetOnDisable || lastTarget == null)
        {
            ClearRuntimeState();
            return;
        }

        Vector3 basePosition = lastTarget.localPosition;
        Quaternion baseRotation = lastTarget.localRotation;
        if (TryRemoveLastOffset(lastTarget, ref basePosition, ref baseRotation))
        {
            lastTarget.localPosition = basePosition;
            lastTarget.localRotation = baseRotation;
        }

        ClearRuntimeState();
    }

    private void OnValidate()
    {
        intensity = Mathf.Max(0f, intensity);
        frequency = Mathf.Max(0.01f, frequency);
        phaseOffset = Mathf.Repeat(phaseOffset, 1f);
    }

    private Transform ResolveTarget()
    {
        return target != null ? target : transform;
    }

    private bool TryRemoveLastOffset(Transform resolvedTarget, ref Vector3 basePosition, ref Quaternion baseRotation)
    {
        if (!hasAppliedOffset || resolvedTarget != lastTarget)
        {
            return false;
        }

        if (!Approximately(resolvedTarget.localPosition, lastAppliedPosition) ||
            !Approximately(resolvedTarget.localRotation, lastAppliedRotation))
        {
            return false;
        }

        basePosition -= lastPositionOffset;
        baseRotation *= Quaternion.Inverse(lastRotationOffset);
        return true;
    }

    private float EvaluatePhase()
    {
        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        return (time * frequency + phaseOffset) * Mathf.PI * 2f;
    }

    private Vector3 EvaluatePositionOffset(float phase)
    {
        float scaledIntensity = Mathf.Max(0f, intensity);
        return new Vector3(
            Mathf.Sin(phase * 0.5f) * positionAmplitude.x,
            Mathf.Sin(phase) * positionAmplitude.y,
            Mathf.Cos(phase * 0.75f) * positionAmplitude.z) * scaledIntensity;
    }

    private Quaternion EvaluateRotationOffset(float phase)
    {
        float scaledIntensity = Mathf.Max(0f, intensity);
        Vector3 eulerOffset = new Vector3(
            Mathf.Sin(phase) * rotationAmplitude.x,
            Mathf.Sin(phase * 0.5f) * rotationAmplitude.y,
            Mathf.Cos(phase * 0.75f) * rotationAmplitude.z) * scaledIntensity;
        return Quaternion.Euler(eulerOffset);
    }

    private void ClearRuntimeState(bool keepTarget = false)
    {
        lastPositionOffset = Vector3.zero;
        lastRotationOffset = Quaternion.identity;
        lastAppliedPosition = Vector3.zero;
        lastAppliedRotation = Quaternion.identity;
        hasAppliedOffset = false;

        if (!keepTarget)
        {
            lastTarget = null;
        }
    }

    private static bool Approximately(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude <= 0.000001f;
    }

    private static bool Approximately(Quaternion a, Quaternion b)
    {
        return Mathf.Abs(Quaternion.Dot(a, b)) >= 0.99999f;
    }
}
