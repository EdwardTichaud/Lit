using UnityEngine;

public enum ChargedLaunchTrajectory { ConstantVelocity, Impulse }
public enum ChargedPreparationMode { HeldPose, BasicTail }

[CreateAssetMenu(fileName = "ChargedSkill", menuName = "Scriptable Objects/Combat/Charged Skill")]
public sealed class ChargedSkillSO : SkillSO
{
    public ChargedPreparationMode preparationMode;
    [Range(0f, .98f)] public float chargeTailStartNormalizedTime = .7f;
    [Range(.01f, 1f)] public float chargeTailPlaybackRate = .1f;
    [Min(.01f)] public float chargeTailBlendSeconds = .08f;
    public AnimationClip preparationClip;
    public string preparationState = "Base Layer.ChargedPreparation";
    [Min(.1f)] public float chargeSeconds = 2f;
    [Header("Charge Camera")]
    [Range(0f, 30f)] public float chargeCameraFovReduction = 10f;
    [Min(0f)] public float chargeCameraBehindOffset = .6f;
    [Min(.01f)] public float chargeCameraBlendSeconds = .25f;
    [Header("Airborne Presentation")]
    public AnimationClip playerFloatClip;
    public string playerFloatState = "Base Layer.ChargedFloat";
    public AnimationClip enemyFloatClip;
    public string enemyFloatState = "Base Layer.ChargedFloatHit";
    [Min(.1f)] public float launchHeight = 3f;
    [Min(.1f)] public float ascentSeconds = .55f;
    [Min(0)] public float airborneHoldSeconds = 1.2f;
    [Min(.1f)] public float maximumVerticalHitDistance = 1.5f;
    [Header("Launch Dynamics")]
    public ChargedLaunchTrajectory launchTrajectory;
    [Min(0)] public float descentInitialSpeed;
    [Min(.1f)] public float descentAcceleration = 20f;
    [Min(.1f)] public float maximumDescentSpeed = 28f;

    public float EvaluateAscentHeight(float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / Mathf.Max(.1f, ascentSeconds));
        return Mathf.Max(0, launchHeight) * (launchTrajectory == ChargedLaunchTrajectory.Impulse ? t * (2f - t) : t);
    }

    public float EvaluateDescentDistance(float elapsed)
    {
        float initial = Mathf.Min(Mathf.Max(0, descentInitialSpeed), Mathf.Max(.1f, maximumDescentSpeed));
        float acceleration = Mathf.Max(.1f, descentAcceleration);
        float capTime = (Mathf.Max(.1f, maximumDescentSpeed) - initial) / acceleration;
        float accelerated = Mathf.Min(Mathf.Max(0, elapsed), capTime);
        return initial * accelerated + .5f * acceleration * accelerated * accelerated +
            Mathf.Max(0, elapsed - capTime) * Mathf.Max(.1f, maximumDescentSpeed);
    }
}
