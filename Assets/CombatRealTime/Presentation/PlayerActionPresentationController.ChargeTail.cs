using UnityEngine;

public sealed partial class PlayerActionPresentationController
{
    public const string ChargeTailSpeedParameter = "ChargedBasicPlaybackRate";
    private static readonly int ChargeTailSpeedHash = Animator.StringToHash(ChargeTailSpeedParameter);
    private ChargedSkillSO chargeTailSkill;
    private int chargeTailGeneration;
    private float chargeTailStart, chargeTailRate = 1f, chargeTailBlend = .08f;
    private bool chargeTailEntered, chargeTailReleased;
    private bool chargeTailFrameCamera = true;
    private System.Action chargeTailCameraRelease;
    private System.Action chargeTailMovementRelease;
    public BasicSkillsSO ActiveBasicForCharge => actionActive && activeActionIsBasic ? activePresentedSkill as BasicSkillsSO : null;
    public BasicSkillsSO PendingBasicForCharge => hasBufferedAction ? bufferedBasicSkill : null;
    public bool IsPreparingBasicTail => chargeTailSkill != null && IsCurrentSession(chargeTailGeneration);

    public static bool TryResolveChargeTailStart(BasicSkillsSO basic, ChargedSkillSO charged, out float start)
    {
        start = charged != null ? charged.chargeTailStartNormalizedTime : 1f;
        if (basic == null || basic.Context != BasicSkillContext.Grounded || basic.AnimationClip == null ||
            basic.AnimationClip.length <= 0 || charged == null || !float.IsFinite(start) || start < 0 || start > .98f ||
            !float.IsFinite(charged.chargeTailPlaybackRate) || charged.chargeTailPlaybackRate <= 0 || charged.chargeTailPlaybackRate > 1 ||
            !float.IsFinite(charged.chargeTailBlendSeconds) || charged.chargeTailBlendSeconds <= 0) return false;
        // Conservatively finish every authored operation except the cosmetic sword visibility.
        foreach (var e in basic.AnimationClip.events)
            if (e.functionName != "ShowSword" && e.functionName != "HideSwordWhenComboEnds")
                start = Mathf.Max(start, (e.time + 1f / Mathf.Max(1f, basic.AnimationClip.frameRate)) / basic.AnimationClip.length);
        return start <= .98f && !basic.AnimationClip.isLooping;
    }

    public bool TryBeginChargedBasicTail(ChargedSkillSO skill, int generation, bool frameCamera = true)
    {
        if (skill == null || skill.preparationMode != ChargedPreparationMode.BasicTail || !IsCurrentSession(generation) ||
            !TryResolveChargeTailStart(ActiveBasicForCharge, skill, out float start) || locomotionBridge == null || !locomotionBridge.Grounded) return false;
        if (chargeTailGeneration == generation) return chargeTailSkill == skill && !chargeTailReleased;
        bool hasParameter = false;
        foreach (var parameter in animator.parameters)
            if (parameter.nameHash == ChargeTailSpeedHash && parameter.type == AnimatorControllerParameterType.Float) hasParameter = true;
        if (!hasParameter) return false;
        chargeTailSkill = skill; chargeTailGeneration = generation; chargeTailStart = start;
        chargeTailFrameCamera = frameCamera;
        chargeTailEntered = chargeTailReleased = false;
        chargeTailRate = 1f; chargeTailBlend = skill.chargeTailBlendSeconds;
        RegisterActionCleanup(generation, () => ResetChargedBasicTail(generation));
        return true;
    }

    public bool CanLaunchFromChargedBasicTail(ChargedSkillSO skill) =>
        skill != null && skill == chargeTailSkill && IsCurrentSession(chargeTailGeneration) &&
        ActiveBasicForCharge != null && TryGetChargedBasicState(out var state) && state.normalizedTime >= chargeTailStart;

    private bool TryGetChargedBasicState(out AnimatorStateInfo state)
    {
        state = default;
        if (!externallyDriven) return TryGetActiveAnimatorState(out state);
        if (animator == null || !animator.isActiveAndEnabled || ActiveBasicForCharge == null) return false;
        state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        return state.fullPathHash == Animator.StringToHash(ActiveBasicForCharge.AnimatorState);
    }

    public bool UpdateExternalChargedBasicTail(int generation)
    {
        if (!externallyDriven || !IsCurrentSession(generation) || chargeTailGeneration != generation ||
            !TryGetChargedBasicState(out var state)) return false;
        TickChargedBasicTail(generation, state);
        return IsPreparingBasicTail;
    }

    public void EndExternalChargedBasicTail(int generation)
    {
        if (externallyDriven && chargeTailGeneration == generation && IsCurrentSession(generation))
            TerminateAction(generation, ActionEndReason.Completed, false);
    }

    private bool TickChargedBasicTail(int generation, AnimatorStateInfo state)
    {
        if (chargeTailGeneration != generation || !IsCurrentSession(generation)) return false;
        bool held = chargeTailSkill != null;
        if (held && !chargeTailEntered && state.normalizedTime >= chargeTailStart)
        {
            if (!locomotionBridge.TryAcquireExternalLock(this, out var movementLock, disableGameplayInput: false))
            { ReleaseChargedBasicTail(generation); return false; }
            RegisterActionCleanup(generation, movementLock.Dispose);
            chargeTailMovementRelease = movementLock.Dispose;
            locomotionBridge.GetComponent<PlayerStateMotionController>()?.SetActionPolicy(
                Animator.StringToHash(ActiveBasicForCharge.AnimatorState), PlayerActionMovementPolicy.VisualOnly);
            chargeTailEntered = true;
            var camera = chargeTailFrameCamera ? LitCameraDirector.EnsureInstance() : null;
            if (camera != null)
            {
                int handle = camera.BeginChargeFraming(transform, chargeTailSkill.chargeCameraFovReduction,
                    chargeTailSkill.chargeCameraBehindOffset, chargeTailSkill.chargeCameraBlendSeconds);
                chargeTailCameraRelease = () => { if (camera != null) camera.EndChargeFraming(handle); };
            }
        }
        float target = held && chargeTailEntered ? (state.normalizedTime >= 1f ? 0f : chargeTailSkill.chargeTailPlaybackRate) : 1f;
        // Clamping the non-looping clip holds its final pose; never restart it to freeze it.
        chargeTailRate = target == 0 ? 0 : Mathf.MoveTowards(chargeTailRate, target, Time.unscaledDeltaTime / chargeTailBlend);
        animator.SetFloat(ChargeTailSpeedHash, chargeTailRate);
        return held && chargeTailEntered;
    }

    public void ReleaseChargedBasicTail(int generation)
    {
        if (chargeTailGeneration != generation || !IsCurrentSession(generation)) return;
        chargeTailSkill = null;
        chargeTailReleased = true;
        var movement = chargeTailMovementRelease; chargeTailMovementRelease = null; movement?.Invoke();
        var release = chargeTailCameraRelease; chargeTailCameraRelease = null; release?.Invoke();
    }

    private void ResetChargedBasicTail(int generation)
    {
        if (chargeTailGeneration != generation) return;
        chargeTailSkill = null; chargeTailGeneration = 0;
        chargeTailEntered = chargeTailReleased = false; chargeTailRate = 1f;
        var movement = chargeTailMovementRelease; chargeTailMovementRelease = null; movement?.Invoke();
        if (animator != null) animator.SetFloat(ChargeTailSpeedHash, 1f);
        var release = chargeTailCameraRelease; chargeTailCameraRelease = null; release?.Invoke();
    }
}
