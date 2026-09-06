using Opsive.UltimateCharacterController.Character.Abilities;
using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
#if UNITY_EDITOR
    [Header("Locomotion Diagnostics")]
    [SerializeField, Tooltip("Editor-only: logs the complete locomotion ownership chain at a fixed sampling rate.")]
    private bool debugLocomotionDiagnostics;
    [SerializeField, Min(0.1f), Tooltip("Minimum time between locomotion diagnostic samples.")]
    private float locomotionDiagnosticInterval = 0.1f;

    private float nextLocomotionDiagnosticTime;
    private AnimationPhase lastDiagnosticAnimationPhase = AnimationPhase.Other;
    private bool lastDiagnosticInputSuppressed;
    private bool lastDiagnosticSpeedChangeActive;
    private int lastDiagnosticExternalLockCount = -1;
    private int lastDiagnosticTraversalLockCount = -1;

    public void SetLocomotionDiagnosticsEnabled(bool enabled)
    {
        debugLocomotionDiagnostics = enabled;
        nextLocomotionDiagnosticTime = 0f;
        lastDiagnosticAnimationPhase = AnimationPhase.Other;
        lastDiagnosticExternalLockCount = -1;
        lastDiagnosticTraversalLockCount = -1;

        Camera gameplayCamera = Camera.main;
        LitSmoothUccCameraViewAdapter cameraDiagnostics = gameplayCamera != null
            ? gameplayCamera.GetComponent<LitSmoothUccCameraViewAdapter>()
            : null;
        cameraDiagnostics?.SetMotionDiagnosticsEnabled(enabled);
    }
#endif

    private void TickLocomotionDiagnostics()
    {
#if UNITY_EDITOR
        if (!debugLocomotionDiagnostics || !Application.isPlaying)
        {
            return;
        }

        AnimationPhase phase = ResolveCurrentAnimationPhase();
        SpeedChange speedChange = locomotion != null ? locomotion.GetAbility<SpeedChange>() : null;
        bool speedChangeActive = speedChange != null && speedChange.IsActive;
        bool inputSuppressed = IsInputSuppressedByUcc;
        bool stateChanged = phase != lastDiagnosticAnimationPhase ||
            inputSuppressed != lastDiagnosticInputSuppressed ||
            speedChangeActive != lastDiagnosticSpeedChangeActive ||
            externalLockCount != lastDiagnosticExternalLockCount ||
            scriptedTraversalLockCount != lastDiagnosticTraversalLockCount;

        if (stateChanged)
        {
            Debug.Log(
                $"[Lit/UCC LocomotionChange] phase={lastDiagnosticAnimationPhase}->{phase} " +
                $"suppressed={lastDiagnosticInputSuppressed}->{inputSuppressed} " +
                $"speedChange={lastDiagnosticSpeedChangeActive}->{speedChangeActive} " +
                $"externalLocks={externalLockCount} traversalLocks={scriptedTraversalLockCount}",
                this);
            lastDiagnosticAnimationPhase = phase;
            lastDiagnosticInputSuppressed = inputSuppressed;
            lastDiagnosticSpeedChangeActive = speedChangeActive;
            lastDiagnosticExternalLockCount = externalLockCount;
            lastDiagnosticTraversalLockCount = scriptedTraversalLockCount;
        }

        if (Time.unscaledTime < nextLocomotionDiagnosticTime)
        {
            return;
        }

        nextLocomotionDiagnosticTime = Time.unscaledTime + Mathf.Max(0.1f, locomotionDiagnosticInterval);
        LogLocomotionDiagnostic(phase, speedChangeActive);
#endif
    }

#if UNITY_EDITOR
    private void LogLocomotionDiagnostic(AnimationPhase phase, bool speedChangeActive)
    {
        AnimatorStateInfo current = animator != null ? animator.GetCurrentAnimatorStateInfo(0) : default;
        AnimatorStateInfo next = animator != null && animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : default;
        Vector3 velocity = locomotion != null ? locomotion.Velocity : Vector3.zero;
        Vector3 rootDelta = animator != null ? animator.deltaPosition : Vector3.zero;
        bool grounded = locomotion != null && locomotion.Grounded;
        float gravity = locomotion != null ? locomotion.GravityAmount : 0f;
        string activeAbilities = ResolveActiveAbilityLabel();
        string currentClip = ResolveAnimatorClipName(current);
        string nextClip = animator != null && animator.IsInTransition(0) ? ResolveNextAnimatorClipName(next) : "-";
        float litSpeed = ReadAnimatorFloat(speedParam);
        float legacySpeed = ReadAnimatorFloat("Speed");
        float forward = ReadAnimatorFloat(forwardMovementParam);
        bool moving = ReadAnimatorBool(isMovingParam);
        CombatActorAnimationRoot combatActor = GetComponent<CombatActorAnimationRoot>();
        bool cinematicMotion = combatActor != null && combatActor.IsCinematicMotionActive;

        Debug.Log(
            $"[Lit/UCC Locomotion] input=({currentWorldMoveInput.x:F2},{currentWorldMoveInput.y:F2}) " +
            $"state='{currentClip}' t={current.normalizedTime:F2} next='{nextClip}' nt={next.normalizedTime:F2} " +
            $"params LitSpeed={litSpeed:F2} Speed={legacySpeed:F2} Forward={forward:F2} IsMoving={moving} " +
            $"rootDelta=({rootDelta.x:F3},{rootDelta.y:F3},{rootDelta.z:F3}) velocity=({velocity.x:F2},{velocity.y:F2},{velocity.z:F2}) " +
            $"grounded={grounded} gravity={gravity:F3} abilities={activeAbilities} " +
            $"phase={phase} rootPos={(locomotion != null && locomotion.UseRootMotionPosition)} rootScale={(locomotion != null ? locomotion.RootMotionSpeedMultiplier : 0f):F2} " +
            $"driving={IsDriving} suppressed={IsInputSuppressedByUcc} speedChange={speedChangeActive} " +
            $"externalLocks={externalLockCount} traversalLocks={scriptedTraversalLockCount} cinematic={cinematicMotion}",
            this);
    }

    private string ResolveAnimatorClipName(AnimatorStateInfo state)
    {
        if (animator == null)
        {
            return "none";
        }

        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
        return clips != null && clips.Length > 0 && clips[0].clip != null
            ? clips[0].clip.name
            : state.fullPathHash.ToString();
    }

    private string ResolveNextAnimatorClipName(AnimatorStateInfo state)
    {
        AnimatorClipInfo[] clips = animator.GetNextAnimatorClipInfo(0);
        return clips != null && clips.Length > 0 && clips[0].clip != null
            ? clips[0].clip.name
            : state.fullPathHash.ToString();
    }

    private float ReadAnimatorFloat(string parameter)
    {
        return HasAnimatorParameter(parameter, AnimatorControllerParameterType.Float)
            ? animator.GetFloat(parameter)
            : 0f;
    }

    private bool ReadAnimatorBool(string parameter)
    {
        return HasAnimatorParameter(parameter, AnimatorControllerParameterType.Bool) && animator.GetBool(parameter);
    }
#endif
}
