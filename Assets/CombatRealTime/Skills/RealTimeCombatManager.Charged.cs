using Unity.Netcode;
using UnityEngine;

public sealed partial class RealTimeCombatManager
{
    private bool TryUseChargedSkill(ChargedSkillSO skill)
    {
        ResolvePlayerReferences();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("[ChargedSkill] Prototype solo du laboratoire : commande serveur de projection non encore disponible.", this);
            return false;
        }
        var character = playerRoot != null ? playerRoot.GetComponentInChildren<SquadCharacterController>() : null;
        var candidate = AllowsFreeAttacks ? CreateAttackContext(skill, 0)?.TargetRoot : LockedCombatTarget?.Root;
        var target = candidate != null ? candidate.GetComponent<ICombatChargedTarget>() : null;
        if ((!combatActive && !CanReceiveFreeAttackInput) || IsPlayerDead() || IsCinematicSequenceActive || character?.CharacterData?.chargedSkill != skill ||
            (!AllowsFreeAttacks && (target == null || !target.CanReceiveChargedLaunch)) || playerLocomotionBridge == null || !playerLocomotionBridge.Grounded ||
            GetSkillCooldownRemaining(skill) > 0 || target != null && !skill.IsWithinLaunchRange(Vector3.ProjectOnPlane(target.ChargedRoot.position - playerRoot.position, Vector3.up).magnitude) ||
            playerActionPresentation == null) return false;
        if (!AllowsFreeAttacks) FaceLockedEnemyForAction();
        if (!playerActionPresentation.StartChargedSkill(skill, target)) return false;
        StartSkillCooldown(skill);
        PlayerSkillStarted?.Invoke(skill, lockedEnemy);
        return true;
    }

    public int ResolveChargedContact(ChargedSkillSO skill, ICombatChargedTarget chargedTarget)
    {
        var target = chargedTarget?.ChargedRoot != null ? EnemyEncounterActors.Resolve(chargedTarget.ChargedRoot)?.EncounterTarget : null;
        if (AllowsFreeAttacks)
        {
            int appliedFree = ResolveFreeAttackImpact(skill, out var victim, target);
            if (appliedFree > 0) playerActionPresentation?.HandleConfirmedExternalImpact(skill, victim);
            return appliedFree;
        }
        if (!combatActive || !CombatProtectionDamage.HasAuthority || target == null || target.IsDead || chargedTarget.ChargedProtected || playerRoot == null ||
            IsPlayerDead() || IsCinematicSequenceActive) return 0;
        Vector3 delta = target.Root.position - playerRoot.position;
        if (!skill.IsWithinHitRange(Vector3.ProjectOnPlane(delta, Vector3.up).magnitude) || Mathf.Abs(delta.y) > skill.maximumVerticalHitDistance) return 0;
        int mask = LayerMask.GetMask("Default", "Ground", "Obstacle", "CameraObstruction");
        foreach (var hit in Physics.RaycastAll(playerRoot.position + Vector3.up, delta.normalized, delta.magnitude, mask, QueryTriggerInteraction.Ignore))
            if (!hit.collider.transform.IsChildOf(playerRoot) && !hit.collider.transform.IsChildOf(target.Root) &&
                hit.collider.GetComponentInParent<SquadCharacterController>() == null) return 0;
        int applied = DeliverExternalImpact(target, new CombatImpact(Mathf.RoundToInt(skill.Damages), 0, CombatImpactOrigin.Basic));
        if (applied > 0) GroundImpactFeedback(skill, target.Root, applied);
        EvaluateCombatOutcome();
        return applied;
    }
}
