using UnityEngine;
using Opsive.UltimateCharacterController.Character.Abilities;

public partial class LitOpsiveLocomotionBridge
{
    public bool IsChargedAscentActive => actionTakeoff != null && actionTakeoff.IsActive && actionTakeoff.HasPendingChargedRise;
    public bool StartChargedAscent(PlayerActionPresentationController owner, ChargedSkillSO skill)
    {
        PrepareActionTakeoff(owner, 2f * skill.launchHeight / Mathf.Max(.1f, skill.ascentSeconds));
        if (actionTakeoff == null || !actionTakeoff.IsActive) return false;
        actionTakeoff.ConfigureChargedRise(skill);
        return true;
    }
    public void StopChargedAscent() { if (actionTakeoff != null && !actionTakeoff.IsAirborneHold) RestoreActionTakeoff(); }
    private void MaintainUccAirborneHold()
    {
        if (locomotion == null) return;
        EnsureActionTakeoff();
        if (actionTakeoff.IsActive && actionTakeoff.IsAirborneHold) return;
        RestoreActionTakeoff();
        actionTakeoff.ConfigureHold();
        locomotion.TryStartAbility(actionTakeoff);
    }
    private void RestoreUccAirborneHold()
    { if (actionTakeoff != null && actionTakeoff.IsAirborneHold) RestoreActionTakeoff(); }
    private LitUccActionTakeoffAbility actionTakeoff;
    public void CancelActionImpulse(PlayerActionPresentationController owner)
    {
        if (owner == null || impulseLock == null || !ReferenceEquals(impulseLock.Owner, owner)) return;
        if (externalImpulseLockRoutine != null) StopCoroutine(externalImpulseLockRoutine);
        externalImpulseLockRoutine = null;
        impulseLock.Dispose(); impulseLock = null;
        RestoreActionTakeoff();
    }
    private void PrepareActionTakeoff(PlayerActionPresentationController owner, float speed)
    {
        if (locomotion == null || !locomotion.Grounded || IsScriptedTraversalActive) return;
        EnsureActionTakeoff();
        RestoreActionTakeoff();
        actionTakeoff.Configure(speed);
        if (!locomotion.TryStartAbility(actionTakeoff)) return;
        owner.RegisterActionCleanup(owner.ActionGeneration, StopChargedAscent);
    }
    private void EnsureActionTakeoff()
    {
        actionTakeoff = locomotion.GetAbility<LitUccActionTakeoffAbility>();
        if (actionTakeoff == null)
        {
            var abilities = locomotion.Abilities ?? System.Array.Empty<Ability>();
            var expanded = new Ability[abilities.Length + 1];
            System.Array.Copy(abilities, expanded, abilities.Length);
            actionTakeoff = new LitUccActionTakeoffAbility(); expanded[abilities.Length] = actionTakeoff;
            locomotion.Abilities = expanded;
        }
    }
    private void RestoreActionTakeoff()
    {
        if (locomotion != null && actionTakeoff != null && actionTakeoff.IsActive)
            locomotion.TryStopAbility(actionTakeoff, true);
    }
}
