using UnityEngine;

public sealed partial class RealTimeCombatInput
{
    private ChargedSkillSO heldChargedSkill;
    private PlayerActionPresentationController chargePresentation;
    private float chargedPressedAt;
    private int basicChargeGeneration;
    private int preparationGeneration;
    private BasicSkillsSO pendingChargeBasic;
    private Lit.Combat.Migration.Network.CombatNetworkActorRuntime networkChargeOwner;
    public bool RequiresFullBasicPlayback => basicAttackHeld && heldChargedSkill != null;
    public bool IsChargingSkillFor(PlayerActionPresentationController owner) => owner != null && chargePresentation == owner &&
        heldChargedSkill != null && basicAttackHeld && !basicAttackRequiresRelease && owner.isActiveAndEnabled &&
        !owner.IsDeathAnimationLocked &&
        (pendingChargeBasic != null ? owner.PendingBasicForCharge == pendingChargeBasic ||
            owner.ActiveBasicForCharge == pendingChargeBasic && owner.ActionGeneration != basicChargeGeneration :
        preparationGeneration != 0 ? owner.IsCurrentSession(preparationGeneration) :
            !owner.IsActionActive && owner.LastEndReason == PlayerActionPresentationController.ActionEndReason.Completed &&
                owner.ActionGeneration == basicChargeGeneration + 1 ||
            owner.IsBasicSkillAction && owner.ActionGeneration == basicChargeGeneration);

    private void BeginChargedHold(bool basicAccepted)
    {
        var root = RealTimeCombatManager.Instance?.PlayerRoot;
        var character = root != null ? root.GetComponentInChildren<SquadCharacterController>() : null;
        var skill = character != null ? character.CharacterData?.chargedSkill : null;
        if (!basicAccepted || skill == null || ResolveBasicSkillContext(RealTimeCombatManager.Instance) != BasicSkillContext.Grounded) return;
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            networkChargeOwner = root.GetComponent<Lit.Combat.Migration.Network.CombatNetworkActorRuntime>();
            if (networkChargeOwner != null && !networkChargeOwner.RequestChargeHold(true)) networkChargeOwner = null;
            return;
        }
        chargePresentation = root.GetComponentInChildren<PlayerActionPresentationController>();
        if (chargePresentation == null) return;
        heldChargedSkill = skill;
        chargedPressedAt = Time.unscaledTime;
        basicChargeGeneration = chargePresentation.ActionGeneration;
        pendingChargeBasic = skill.preparationMode == ChargedPreparationMode.BasicTail ? chargePresentation.PendingBasicForCharge : null;
        preparationGeneration = 0;
        basicAttackHeld = true;
        chargePresentation.BindChargedHoldInput(this);
    }

    public void ContinueChargedHoldAfterBasic(PlayerActionPresentationController owner)
    {
        var manager = RealTimeCombatManager.Instance;
        if (owner == chargePresentation && IsChargingSkillFor(owner) && manager != null)
            TickChargedHold(manager);
    }

    private void TickChargedHold(RealTimeCombatManager manager)
    {
        if (networkChargeOwner != null) return; // The server observes the hold and confirms preparation/launch.
        var owner = chargePresentation;
        var bridge = owner != null ? owner.GetComponent<LitOpsiveLocomotionBridge>() : null;
        if (heldChargedSkill == null || owner == null || manager.PlayerRoot == null || !owner.transform.IsChildOf(manager.PlayerRoot) || bridge == null || !bridge.Grounded ||
            !owner.isActiveAndEnabled || manager.GetSkillCooldownRemaining(heldChargedSkill) > 0 ||
            basicComboRoutine != null || basicSkillQueue.Count > 0)
        { CancelChargedHold("acteur, sol, cooldown ou buffer indisponible"); return; }
        if (heldChargedSkill.preparationMode == ChargedPreparationMode.BasicTail)
        {
            if (Time.timeScale <= 0) return;
            if (pendingChargeBasic != null)
            {
                if (owner.PendingBasicForCharge == pendingChargeBasic) return;
                if (owner.ActiveBasicForCharge != pendingChargeBasic || owner.ActionGeneration == basicChargeGeneration)
                { CancelChargedHold("Basic bufferisee remplacee"); return; }
                pendingChargeBasic = null;
                basicChargeGeneration = owner.ActionGeneration;
            }
            if (!owner.TryBeginChargedBasicTail(heldChargedSkill, basicChargeGeneration))
            { CancelChargedHold("fin de Basic incompatible avec la charge"); return; }
            if (Time.unscaledTime - chargedPressedAt >= heldChargedSkill.chargeSeconds && owner.CanLaunchFromChargedBasicTail(heldChargedSkill))
            {
                var skill = heldChargedSkill;
                bool started = manager.TryUseSkill(skill);
                CancelChargedHold(started ? "lancement depuis la fin du coup" : "lancement refuse, reprise du geste");
            }
            return;
        }
        if (preparationGeneration != 0 && !owner.IsCurrentSession(preparationGeneration))
        { CancelChargedHold("session de preparation remplacee"); return; }
        if (preparationGeneration == 0 && owner.IsActionActive)
        {
            if (!owner.IsBasicSkillAction || owner.ActionGeneration != basicChargeGeneration) CancelChargedHold("Basic initiale remplacee");
            return;
        }
        if (preparationGeneration == 0 && owner.LastEndReason != PlayerActionPresentationController.ActionEndReason.Completed)
        { CancelChargedHold("Basic initiale terminee anormalement"); return; }
        if (Time.unscaledTime - chargedPressedAt > heldChargedSkill.chargeSeconds)
        {
            var skill = heldChargedSkill;
            owner.ReleaseChargedHoldInput(this);
            owner.EndChargedPreparation(preparationGeneration);
            preparationGeneration = 0;
            basicAttackHeld = false;
            heldChargedSkill = null;
            basicAttackRequiresRelease = true;
            if (!manager.TryUseSkill(skill))
                Trace("ChargedSkill refuse: " + skill.SkillName + " | action=" + owner.IsActionActive + " | " + InputDiagnostics);
            return;
        }
        if (preparationGeneration == 0)
        {
            preparationGeneration = owner.BeginChargedPreparation(heldChargedSkill);
            if (preparationGeneration == 0) CancelChargedHold();
        }
    }

    private void CancelChargedHold(string reason = "relachement ou changement de contexte")
    {
        if (networkChargeOwner != null) networkChargeOwner.RequestChargeHold(false);
        networkChargeOwner = null;
        bool armed = heldChargedSkill != null;
        var owner = chargePresentation;
        int token = preparationGeneration;
        if (armed && reason != "relachement ou changement de contexte")
            Trace("ChargedSkill annule: " + reason + " | basic=" + basicChargeGeneration +
                " preparation=" + token + " active=" + (owner != null ? owner.ActionGeneration : 0) +
                " grounded=" + (owner != null ? owner.GetComponent<LitOpsiveLocomotionBridge>()?.Grounded : null) +
                " queued=" + basicSkillQueue.Count);
        heldChargedSkill = null;
        chargePresentation = null;
        preparationGeneration = 0;
        basicAttackHeld = false;
        if (armed) basicAttackRequiresRelease = true;
        if (owner != null)
        {
            owner.ReleaseChargedBasicTail(basicChargeGeneration);
            pendingChargeBasic = null;
            owner.ReleaseChargedHoldInput(this);
            owner.EndChargedPreparation(token);
        }
    }
}
