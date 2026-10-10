using Unity.Cinemachine;
using UnityEngine;

/// <summary>Short camera-only correction, including when combat time is paused by Timeline.</summary>
[DisallowMultipleComponent]
public sealed class CombatCinematicCameraShake : CinemachineExtension
{
    [SerializeField, Min(0f)] private float amplitude = .06f;
    [SerializeField, Min(.01f)] private float duration = .14f;
    [SerializeField, Min(.1f)] private float frequency = 28f;
    private float startedAt = float.NegativeInfinity;

    public void PlayShake() => startedAt = Time.unscaledTime;
    public void ClearShake() => startedAt = float.NegativeInfinity;

    protected override void OnEnable()
    {
        base.OnEnable();
        ClearShake();
    }

    public static Vector3 EvaluateOffset(float elapsed, float amplitude, float duration, float frequency)
    {
        if (elapsed < 0 || duration <= 0 || elapsed >= duration) return Vector3.zero;
        float envelope = 1f - elapsed / duration;
        float phase = elapsed * frequency * Mathf.PI * 2f;
        return new Vector3(Mathf.Sin(phase), Mathf.Cos(phase * 1.27f) * .65f, 0) * (amplitude * envelope * envelope);
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Noise || !Application.isPlaying) return;
        state.PositionCorrection += state.RawOrientation * EvaluateOffset(Time.unscaledTime - startedAt, amplitude, duration, frequency);
    }
}
