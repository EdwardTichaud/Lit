using Unity.Netcode;
using UnityEngine;

public sealed partial class RealTimeCombatManager
{
    private bool TryUseGroundImpact(SkillSO skill)
    {
        ResolvePlayerReferences();
        // Network movement/area impact need a dedicated server command; never accept client damage.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("[GroundImpact] Prototype de laboratoire solo : lancement reseau non encore disponible.", this);
            return false;
        }
        if (IsPlayerDead() || IsCinematicSequenceActive || GetSkillCooldownRemaining(skill) > 0 ||
            playerActionPresentation == null || !playerActionPresentation.StartGroundImpact(skill)) return false;
        StartSkillCooldown(skill);
        PlayerSkillStarted?.Invoke(skill, lockedEnemy);
        return true;
    }

    public void ResolveGroundImpact(SkillSO skill, bool airborne, Vector3 point)
    {
        if (!CombatProtectionDamage.HasAuthority || skill == null || !skill.IsGroundImpactSkill || IsPlayerDead() ||
            playerActionPresentation == null || !playerActionPresentation.ConsumeGroundImpact(skill)) return;
        float radius = skill.groundImpact.radius;
        int damage = Mathf.RoundToInt(skill.Damages * (airborne ? skill.groundImpact.airborneDamageMultiplier : 1));
        float push = airborne ? skill.groundImpact.airbornePushDistance : skill.groundImpact.groundPushDistance;
        var action = CurrentAttackContext;
        if (AllowsFreeAttacks && (action == null || action.Skill != skill || !action.TryConsume(playerActionPresentation.ActionGeneration))) return;
        foreach (var enemy in FindObjectsByType<LitBrainsEnemy>(FindObjectsInactive.Exclude))
        {
            if (enemy.IsDead || !InGroundImpact(enemy.transform, point, radius)) continue;
            if (AllowsFreeAttacks && (enemy.IsCinematicSuspended || !FreeAttackLineClear(point, enemy))) continue;
            int applied = AllowsFreeAttacks ? CompleteFreeAttackImpact(skill, enemy, false, action: action, damageOverride: damage, evaluateOutcome: false) :
                enemy.ReceiveImpact(new CombatImpact(damage, skill.interruptionForce, CombatImpactOrigin.Basic, source: playerController));
            if (applied <= 0) continue;
            if (!AllowsFreeAttacks) GroundImpactFeedback(skill, enemy.transform, applied);
            else CombatImpactFeedbackController.EnsureInstance()?.PlayImpact(skill, enemy.LockPoint, playerRoot);
            enemy.StartGroundImpactPush(GroundImpactPushDirection(enemy.transform, point), push);
        }
        foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude))
        {
            if (enemy.Health == null || enemy.Health.IsDead || !InGroundImpact(enemy.transform, point, radius)) continue;
            var target = new LegacyCombatTarget(enemy);
            if (AllowsFreeAttacks && (enemy.IsCinematicSuspended || !FreeAttackLineClear(point, target))) continue;
            int applied = AllowsFreeAttacks ? CompleteFreeAttackImpact(skill, target, false, action: action, damageOverride: damage, evaluateOutcome: false) : enemy.ReceiveLightDamage(damage, playerController);
            if (applied <= 0) continue;
            if (!AllowsFreeAttacks) GroundImpactFeedback(skill, enemy.transform, applied);
            else CombatImpactFeedbackController.EnsureInstance()?.PlayImpact(skill, enemy.LockPoint, playerRoot);
            enemy.StartGroundImpactPush(GroundImpactPushDirection(enemy.transform, point), push);
        }
        GroundImpactWave.Show(point, radius);
        EvaluateCombatOutcome();
    }

    private static bool InGroundImpact(Transform target, Vector3 point, float radius) =>
        Vector3.ProjectOnPlane(target.position - point, Vector3.up).sqrMagnitude <= radius * radius &&
        Mathf.Abs(target.position.y - point.y) <= 2f;

    private static Vector3 GroundImpactPushDirection(Transform target, Vector3 point)
    {
        var radial = Vector3.ProjectOnPlane(target.position - point, Vector3.up);
        return radial.sqrMagnitude > .0001f ? radial.normalized : target.forward;
    }

    private void GroundImpactFeedback(SkillSO skill, Transform target, int applied)
    {
        CombatDamageWorldFeedback.Show(target, applied, Color.white, 2);
        CombatImpactFeedbackController.EnsureInstance()?.PlayImpact(skill, target, playerRoot);
        AddClarity(skill.ClarityGainOnHit);
        PlayerLightDamageApplied?.Invoke(applied);
        PlayerSkillImpactApplied?.Invoke(skill, applied);
    }
}
