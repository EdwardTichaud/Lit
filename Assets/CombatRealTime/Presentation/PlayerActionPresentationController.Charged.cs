using System.Collections;
using UnityEngine;

public sealed partial class PlayerActionPresentationController
{
    private ICombatChargedTarget chargedTarget;
    private bool chargedFirstContact, chargedHoldHandoff;
    private ChargedSkillSO chargedFloatSkill;
    private System.Action chargedHoldRelease;
    private bool chargedDescentRequired;
    private float chargedHoldDeadline;
    private int chargedPreparationToken;
    private RealTimeCombatInput chargedHoldInput;
    public ChargedSkillSO PassiveChargedFloatSkill => chargedHoldRelease != null ? chargedFloatSkill : null;
    public bool IsPreparingChargedSkill => IsPreparingBasicTail || chargedPreparationToken != 0 && IsCurrentSession(chargedPreparationToken) &&
        externallyDriven && sessionOwner == this;
    public bool IsChargingSkill => IsPreparingChargedSkill ||
        chargedHoldInput != null && chargedHoldInput.IsChargingSkillFor(this);
    public void BindChargedHoldInput(RealTimeCombatInput input) => chargedHoldInput = input;
    public void ReleaseChargedHoldInput(RealTimeCombatInput input)
    { if (chargedHoldInput == input) chargedHoldInput = null; }

    private float ResolveAirborneHoldDuration(float authoredDuration)
    {
        if (locomotionBridge == null || locomotionBridge.Grounded || chargedHoldRelease == null) return authoredDuration;
        var clock = GetComponent<CombatTimeDomain>();
        float remaining = chargedHoldDeadline - (clock != null ? clock.LocalTime : Time.time);
        if (remaining <= 0f) chargedDescentRequired = true;
        return Mathf.Min(authoredDuration, Mathf.Max(0f, remaining));
    }
    private void ReleaseChargedPlayerHold()
    { var release = chargedHoldRelease; chargedHoldRelease = null; release?.Invoke(); ExitChargedFloat(); }
    public void ReleaseChargedFloatForWorldExit()
    {
        ReleaseChargedPlayerHold();
        locomotionBridge?.StopChargedAscent();
    }

    public int BeginChargedPreparation(ChargedSkillSO skill, bool frameCamera = true)
    {
        if (actionActive || locomotionBridge == null || !locomotionBridge.Grounded ||
            !CanPlayState(skill.preparationState) || !locomotionBridge.HasOnlyExternalLocksOwnedBy(this, GetComponent<PlayerStateMotionController>())) return 0;
        int token = BeginExternalPresentation(this);
        if (token == 0) return 0;
        chargedPreparationToken = token;
        RegisterActionCleanup(token, () => { if (chargedPreparationToken == token) chargedPreparationToken = 0; });
        if (!locomotionBridge.TryAcquireExternalLock(this, out var handle, disableGameplayInput: false))
        { TerminateAction(token, ActionEndReason.Failed); return 0; }
        RegisterActionCleanup(token, handle.Dispose);
        var camera = frameCamera ? LitCameraDirector.EnsureInstance() : null;
        if (camera != null)
        {
            int cameraHandle = camera.BeginChargeFraming(transform, skill.chargeCameraFovReduction, skill.chargeCameraBehindOffset, skill.chargeCameraBlendSeconds);
            RegisterActionCleanup(token, () => { if (camera != null) camera.EndChargeFraming(cameraHandle); });
        }
        locomotionBridge.GetComponent<PlayerStateMotionController>()?.SetActionPolicy(Animator.StringToHash(skill.preparationState), PlayerActionMovementPolicy.VisualOnly);
        animator.CrossFadeInFixedTime(skill.preparationState, .08f, 0, 0);
        return token;
    }

    public void EndChargedPreparation(int token)
    {
        if (token != 0 && IsCurrentSession(token) && externallyDriven && sessionOwner == this)
            TerminateAction(token, ActionEndReason.Completed);
    }

    public bool StartChargedSkill(ChargedSkillSO skill, ICombatChargedTarget target)
    {
        bool replaceTail = CanLaunchFromChargedBasicTail(skill);
        if (actionActive && !replaceTail || target == null && RealTimeCombatManager.Instance?.AllowsFreeAttacks != true || !CanPlayState(skill.AnimatorState) || locomotionBridge == null ||
            !locomotionBridge.Grounded || !locomotionBridge.HasOnlyExternalLocksOwnedBy(this, GetComponent<PlayerStateMotionController>())) return false;
        if (replaceTail ? !StartAction(Animator.StringToHash(skill.AnimatorState), skill.Presentation, skill.SkillName, false, null, skill)
            : !TryPlaySkill(skill, Animator.StringToHash(skill.AnimatorState))) return false;
        chargedTarget = target;
        chargedFirstContact = chargedHoldHandoff = false;
        int token = activeToken;
        RegisterActionTermination(token, reason =>
        {
            if (reason != ActionEndReason.Completed || !chargedHoldHandoff) chargedTarget?.ReleaseChargedHold();
            locomotionBridge.StopChargedAscent();
            chargedTarget = null;
        });
        return true;
    }

    public bool BeginExternalChargedLaunch(int generation, ChargedSkillSO skill, ICombatChargedTarget target)
    {
        if (!externallyDriven || !IsCurrentSession(generation) || activePresentedSkill != skill || target == null ||
            !target.CanReceiveChargedLaunch || !target.BeginChargedLaunch(skill)) return false;
        chargedTarget = target;
        chargedFirstContact = true;
        chargedHoldHandoff = false;
        RegisterActionTermination(generation, reason =>
        {
            if (reason != ActionEndReason.Completed || !chargedHoldHandoff) target?.ReleaseChargedHold();
            locomotionBridge.StopChargedAscent();
            if (chargedTarget == target) chargedTarget = null;
        });
        if (!locomotionBridge.StartChargedAscent(this, skill))
        { target.ReleaseChargedHold(); return false; }
        StartCoroutine(ChargedAirborneHold(generation, target, skill));
        return true;
    }

    public void HandleChargedSkillContact(int contact)
    {
        if (!(activePresentedSkill is ChargedSkillSO skill) || !actionActive || contact != 0) return;
        var manager = RealTimeCombatManager.Instance;
        if (manager == null || !manager.IsCombatActive && !manager.AllowsFreeAttacks || manager.IsCinematicSequenceActive) return;
        if (contact == 0)
        {
            if (chargedFirstContact) return;
            chargedFirstContact = true;
            if (manager.AllowsFreeAttacks) chargedTarget = CombatChargedTargets.Resolve(manager.FindFreeContactTarget(skill));
            if (chargedTarget == null || !chargedTarget.CanReceiveChargedLaunch ||
                manager.ResolveChargedContact(skill, chargedTarget) <= 0 || !chargedTarget.ChargedAlive) return;
            if (!chargedTarget.BeginChargedLaunch(skill)) return;
            if (!locomotionBridge.StartChargedAscent(this, skill))
            { chargedTarget.ReleaseChargedHold(); return; }
            StartCoroutine(ChargedAirborneHold(activeToken, chargedTarget, skill));
        }
    }

    private IEnumerator ChargedAirborneHold(int token, ICombatChargedTarget target, ChargedSkillSO skill)
    {
        float elapsed = 0;
        var clock = GetComponent<CombatTimeDomain>();
        while (IsCurrentSession(token) && locomotionBridge.IsChargedAscentActive && elapsed < skill.ascentSeconds + .75f)
        { yield return new WaitForFixedUpdate(); elapsed += clock != null ? clock.FixedDeltaTime : Time.fixedDeltaTime; }
        if (!IsCurrentSession(token)) yield break;
        locomotionBridge.StopChargedAscent();
        int holdHandle = locomotionBridge.AcquireCombatAirborneHold(this, skill.airborneHoldSeconds);
        if (holdHandle == 0)
        { TerminateAction(token, ActionEndReason.Completed); yield break; }
        bool holding = true;
        chargedDescentRequired = false;
        chargedHoldDeadline = (clock != null ? clock.LocalTime : Time.time) + skill.airborneHoldSeconds;
        int targetGeneration = target != null ? target.ChargedGeneration : -1;
        chargedHoldRelease = () =>
        {
            if (!holding) return;
            holding = false;
            if (locomotionBridge != null) locomotionBridge.ReleaseCombatAirborneHold(holdHandle);
            // An aerial Basic must not extend this launch's suspension after its
            // deadline. Its animation may continue, but UCC owns the descent.
            chargedDescentRequired = locomotionBridge != null && !locomotionBridge.Grounded;
            if (chargedDescentRequired) ReleaseAirborneHold();
            if (chargedDescentRequired && !deathAnimationLocked && !locomotionBridge.IsCinematicMotionSessionActive &&
                (!actionActive || activeActionIsBasic) && (clock != null ? clock.LocalTime : Time.time) >= chargedHoldDeadline)
                locomotionBridge.RequestCombatSkillLanding(skill.descentInitialSpeed);
            if (target != null && target.ChargedRoot != null && target.IsChargedAirborne && target.ChargedGeneration == targetGeneration) target.ReleaseChargedHold();
        };
        var ownRelease = chargedHoldRelease;
        RegisterActionTermination(token, reason =>
        {
            if (reason == ActionEndReason.Completed && chargedHoldHandoff) return;
            ownRelease();
        });
        chargedHoldHandoff = true;
        chargedFloatSkill = skill;
        target?.BeginChargedFloat(skill.airborneHoldSeconds);
        TerminateAction(token, ActionEndReason.Completed);
        TryShowChargedFloat();
        elapsed = 0;
        int observedGeneration = activeToken;
        while (holding && isActiveAndEnabled && !deathAnimationLocked && target != null && target.ChargedAlive && !target.ChargedProtected &&
            !locomotionBridge.IsCinematicMotionSessionActive)
        {
            if (!IsCurrentSession(token))
            {
                if (!chargedHoldHandoff || (actionActive && !activeActionIsBasic)) break;
                // Completed aerial Basics may hand back the passive pose. Failed
                // or interrupted sessions must instead release the suspension.
                if (activeToken != observedGeneration && !actionActive && LastEndReason != ActionEndReason.Completed) break;
                observedGeneration = activeToken;
                elapsed += clock != null ? clock.DeltaTime : Time.deltaTime;
                if (elapsed >= skill.airborneHoldSeconds) break;
            }
            TryShowChargedFloat();
            yield return null;
        }
        ownRelease();
        if (chargedHoldRelease == ownRelease)
        {
            chargedHoldRelease = null;
            ExitChargedFloat();
        }
    }

    private bool TryShowChargedFloat()
    {
        if (chargedFloatSkill == null || actionActive || chargedHoldRelease == null || deathAnimationLocked ||
            locomotionBridge == null || locomotionBridge.Grounded || locomotionBridge.IsCinematicMotionSessionActive ||
            Lit.Combat.Migration.Network.CombatCinematicReservations.ContainsParticipant(transform) || !CanPlayState(chargedFloatSkill.playerFloatState)) return false;
        string state = chargedFloatSkill.playerFloatState;
        if (!animator.GetCurrentAnimatorStateInfo(0).IsName(state) &&
            (!animator.IsInTransition(0) || !animator.GetNextAnimatorStateInfo(0).IsName(state)))
        {
            locomotionBridge.GetComponent<PlayerStateMotionController>()?.SetActionPolicy(Animator.StringToHash(state), PlayerActionMovementPolicy.VisualOnly);
            animator.CrossFadeInFixedTime(state, .08f, 0, 0);
        }
        return true;
    }

    private void ExitChargedFloat()
    {
        var skill = chargedFloatSkill;
        chargedFloatSkill = null;
        if (skill == null || actionActive || deathAnimationLocked || animator == null || !animator.isActiveAndEnabled ||
            locomotionBridge == null || locomotionBridge.IsCinematicMotionSessionActive ||
            Lit.Combat.Migration.Network.CombatCinematicReservations.ContainsParticipant(transform)) return;
        bool ownPose = animator.GetCurrentAnimatorStateInfo(0).IsName(skill.playerFloatState) ||
            animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(skill.playerFloatState);
        if (!ownPose) return;
        locomotionBridge.GetComponent<PlayerStateMotionController>()?.Cancel();
        string destination = locomotionBridge.Grounded ? ResolveCurrentLocomotionDestination() : "Base Layer.Falling";
        if (CanPlayState(destination)) animator.CrossFadeInFixedTime(destination, .08f, 0, 0);
    }
}
