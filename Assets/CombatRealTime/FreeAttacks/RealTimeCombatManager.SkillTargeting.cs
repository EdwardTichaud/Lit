using UnityEngine;

public sealed partial class RealTimeCombatManager
{
    public string LastSkillTargetingFailure { get; private set; }

    public bool TryResolveSkillTargeting(SkillSO skill, int generation, out PlayerAttackContext context, out string reason)
    {
        context = null;
        if (!AllowsFreeAttacks) { reason = "Ciblage du laboratoire indisponible."; return false; }
        return ResolveSkillTargeting(skill, generation, out context, out reason);
    }

    private bool ResolveSkillTargeting(SkillSO skill, int generation, out PlayerAttackContext context, out string reason)
    {
        context = null;
        reason = null;
        if (playerRoot == null) { reason = "Lanceur indisponible."; return false; }
        if (!SkillTargetingContract.Validate(skill, out reason)) return false;
        var mode = skill.targetingMode;
        var forward = freeAttackCamera != null ? freeAttackCamera.transform.forward : playerRoot.forward;
        var direction = FreeAttackProfile.ResolveDirection(LocalInputRouter.MoveValue, forward, playerRoot.forward);
        ICombatTarget target = null;
        SquadCharacterController ally = null;
        bool locked = false;
        Vector3? zone = null;
        if (mode == SkillTargetingMode.SelectedAlly)
        {
            bool link = skill.IsProtectionSkill && skill.protection.kind == SkillProtectionKind.Intercession;
            ally = supportTarget;
            // Preserve the existing optional solo/self use, never replace an explicit selection.
            if (ally == null && (link || PlayerRegenerationEffect.IsValidRegenerationRecipient(playerController, playerController))) ally = playerController;
            bool valid = link ? PlayerProtectionEffects.Alive(ally) : PlayerRegenerationEffect.IsValidRegenerationRecipient(playerController, ally);
            float range = link ? skill.protection.radius : skill.healingRadius;
            if (!valid) { reason = "Allie selectionne absent, mort ou incompatible."; return false; }
            if (Vector3.Distance(playerRoot.position, ally.transform.position) > range) { reason = "Allie hors portee."; return false; }
        }
        else if (mode == SkillTargetingMode.Self) ally = playerController;
        else if (mode != SkillTargetingMode.AllyArea && mode != SkillTargetingMode.CasterArea &&
            !(skill.IsControlSkill && skill.control.kind == SkillControlKind.PasDeRecul))
        {
            bool selected = HasLockedCombatTarget;
            if (mode == SkillTargetingMode.LockedTarget && !HasLockedCombatTarget)
            { reason = "Verrouillez un ennemi pour utiliser cette competence."; return false; }
            target = selected ? LockedCombatTarget : null;
            locked = selected;
            if (selected && !CompatibleSkillEnemy(target))
            {
                if (mode != SkillTargetingMode.FreeAttack) { reason = "Ennemi verrouille absent, mort ou incompatible."; return false; }
                target = null;
            }
            if (!selected) target = FindFreeAttackTarget(skill, direction, false, null);
            if (target != null)
            {
                bool required = mode != SkillTargetingMode.FreeAttack;
                if (required && !EnemyWithinLaunchRange(skill, target)) { reason = "Ennemi hors portee de lancement."; return false; }
                if (required && !FreeAttackLineClear(playerRoot.position, target)) { reason = "Ennemi masque par un obstacle."; return false; }
                var delta = Vector3.ProjectOnPlane(target.Root.position - playerRoot.position, Vector3.up);
                if (delta.sqrMagnitude > .0001f) direction = delta.normalized;
            }
            else if (mode == SkillTargetingMode.RequiredTarget) { reason = "Aucun ennemi compatible devant le lanceur."; return false; }
        }
        if (skill.IsControlSkill && skill.control.kind == SkillControlKind.PasDeRecul)
            direction = PlayerControlSkills.ResolveDirection(playerController, playerLocomotionBridge,
                SkillTargetingContract.Alive(LockedCombatTarget) ? LockedCombatTarget.Root : null);
        if (skill.IsControlSkill && skill.control.kind == SkillControlKind.Entrave)
        {
            if (!EnemyEntraveZone.TryFindGround(target.Root, out var point)) { reason = "Sol local invalide sous la cible."; return false; }
            zone = point;
        }
        context = new PlayerAttackContext(generation, skill, playerRoot.position, direction, target, locked, ally, zone);
        return true;
    }

    private bool CompatibleSkillEnemy(ICombatTarget target)
    {
        if (!SkillTargetingContract.Alive(target)) return false;
        var encounter = EnemyEncounterActors.Resolve(target.Root);
        return (encounter == null || EnemyEncounterActors.IsAvailable(encounter)) &&
            target.Root.GetComponent<LitBrainsEnemy>()?.IsCinematicSuspended != true &&
            target.Root.GetComponent<EnemyController>()?.IsCinematicSuspended != true &&
            target.Root.GetComponent<EnemyController>()?.IsFlameDormant != true;
    }

    private bool EnemyWithinLaunchRange(SkillSO skill, ICombatTarget target)
    {
        SkillTargetingContract.EnemyRange(skill, out float minimum, out float maximum);
        Vector3 delta = target.Root.position - playerRoot.position;
        float distance = skill.IsHealingSkill ? delta.magnitude : Vector3.ProjectOnPlane(delta, Vector3.up).magnitude;
        return distance >= minimum && distance <= maximum && Mathf.Abs(delta.y) <= freeAttackProfile.heightTolerance;
    }

    private bool RefuseSkillTargeting(string reason)
    {
        LastSkillTargetingFailure = reason;
        if (freeAttackProfile != null && freeAttackProfile.diagnostics) Debug.LogWarning("[SkillTargeting] " + reason, this);
        return false;
    }
}
