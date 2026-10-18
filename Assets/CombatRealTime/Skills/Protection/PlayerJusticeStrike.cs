using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerJusticeStrike : MonoBehaviour
{
    private SkillSO skill;
    private ICombatTarget target;
    private PlayerActionPresentationController presentation;
    private int generation, damage;
    private bool used;
    public bool OwnsAction => skill != null && presentation != null && presentation.IsCurrentSession(generation);
    public bool TryStart(SkillSO definition, ICombatTarget victim, bool cosmeticOnly = false, PlayerAttackContext targeting = null)
    {
        var caster = GetComponent<SquadCharacterController>();
        presentation = GetComponent<PlayerActionPresentationController>();
        if (!SkillHealing.CanCast(caster) || definition == null || victim == null || victim.IsDead || victim.Root == null ||
            presentation == null || !definition.IsWithinLaunchRange(Vector3.ProjectOnPlane(victim.Root.position - transform.position, Vector3.up).magnitude)) return false;
        var effects = PlayerProtectionEffects.Get(caster);
        if (!cosmeticOnly && (!CombatProtectionDamage.HasAuthority || Mathf.RoundToInt(effects.Reserve) <= 0)) return false;
        if (targeting == null) presentation.SetActionFacingTarget(victim.LockPoint);
        if (!presentation.TryPlaySkill(definition, Animator.StringToHash(definition.AnimatorState), targeting) || presentation.LastRequestWasBuffered) return false;
        skill = definition; target = victim; generation = presentation.ActionGeneration; used = false;
        damage = cosmeticOnly ? 0 : effects.ConsumeReserve();
        int session = generation;
        presentation.RegisterActionCleanup(session, () =>
        {
            if (generation != session) return;
            skill = null; target = null; damage = 0;
        });
        return true;
    }
    public void ResolveImpact()
    {
        if (!OwnsAction || used) return;
        used = true;
        if (!CombatProtectionDamage.HasAuthority || damage <= 0 || target == null || target.Root == null || target.IsDead ||
            !CombatContactGeometry.HasContact(transform, target.Root, skill.maximumHitDistance, skill.enemyImpact.arcDegrees, 1.8f)) return;
        float distance = Vector3.ProjectOnPlane(target.Root.position - transform.position, Vector3.up).magnitude;
        if (!skill.IsWithinHitRange(distance)) return;
        var manager = RealTimeCombatManager.Instance;
        if (manager != null && manager.AllowsFreeAttacks && manager.PlayerRoot == transform)
        {
            int resolved = manager.ResolveReservedPlayerContact(skill, target, damage);
            if (resolved > 0) CombatImpactFeedbackController.EnsureInstance()?.PlayImpact(skill, target.LockPoint, transform);
            return;
        }
        var legacy = target.Root.GetComponent<EnemyController>();
        int applied = legacy != null ? legacy.ReceiveLightDamage(damage, GetComponent<SquadCharacterController>()) :
            target is ICombatImpactReceiver receiver ? receiver.ReceiveImpact(new CombatImpact(damage, skill.interruptionForce, CombatImpactOrigin.Basic,
                source: GetComponent<SquadCharacterController>())) : target.ReceiveDamage(damage);
        if (applied <= 0) return;
        if (legacy == null) CombatDamageWorldFeedback.Show(target.Root, applied, Color.white, 2);
        CombatImpactFeedbackController.EnsureInstance()?.PlayImpact(skill, target.LockPoint, transform);
    }
}
