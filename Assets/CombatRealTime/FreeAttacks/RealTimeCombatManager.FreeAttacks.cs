using System.Collections.Generic;
using UnityEngine;

public sealed partial class RealTimeCombatManager
{
    private FreeAttackProfile freeAttackProfile;
    private Camera freeAttackCamera;
    public bool AllowsFreeAttacks => freeAttackProfile != null && freeAttackProfile.enabled;
    public bool UsesBurstContactDash => AllowsFreeAttacks && freeAttackProfile.contactDashMotion == ContactDashMotion.Burst;
    public float RetreatLandingDeceleration => AllowsFreeAttacks ? freeAttackProfile.retreatLandingDeceleration : 0;
    private bool HasFreeAttackControl => playerRoot != null && (playerLocomotionBridge == null ||
        !playerLocomotionBridge.IsScriptedTraversalActive && !playerLocomotionBridge.IsCinematicMotionSessionActive &&
        playerLocomotionBridge.HasOnlyExternalLocksOwnedBy(playerActionPresentation, playerRoot.GetComponent<PlayerStateMotionController>()));
    public bool CanReceiveFreeAttackInput => Time.timeScale > 0 && AllowsFreeAttacks && HasFreeAttackControl && !IsPlayerDead() && !IsCinematicSequenceActive &&
        InputModeCoordinator.CurrentMode == InputMode.Combat;
    private bool CanUseFreeSkill => CanReceiveFreeAttackInput || Time.timeScale > 0 && AllowsFreeAttacks && HasFreeAttackControl && !IsPlayerDead() &&
        !IsCinematicSequenceActive && InputModeCoordinator.CurrentMode == InputMode.CombatWheel;
    public bool HasAttackInputContext => InputModeCoordinator.CurrentMode == InputMode.Combat &&
        (CanReceiveFreeAttackInput || combatActive && HasLockedCombatTarget);
    public PlayerAttackContext CurrentAttackContext => AllowsFreeAttacks ? playerActionPresentation?.AttackContext : null;
    public Transform ActionTargetPoint => CurrentAttackContext != null ? CurrentAttackContext.TargetPoint : LockedTargetPoint;
    public float FreeAttackTurnRate => freeAttackProfile != null ? freeAttackProfile.turnDegreesPerSecond : 1440f;
    private bool FinishingFreeDashAfterDefeat => CurrentAttackContext != null &&
        CurrentAttackContext.Skill.TargetLunge.resolveImpactAtContact && playerActionPresentation.IsTargetLungeActive &&
        externalTarget != null && !IsPlayerDead();

    public void ConfigureFreeAttacks(FreeAttackProfile profile, Camera camera)
    {
        freeAttackProfile = profile;
        freeAttackCamera = camera;
    }

    public PlayerAttackContext CreateAttackContext(SkillSO skill, int generation)
    {
        return TryResolveSkillTargeting(skill, generation, out var context, out _) ? context : null;
    }

    public static ICombatTarget TargetFromCollider(Collider collider)
    {
        var brain = collider.GetComponentInParent<LitBrainsEnemy>();
        if (brain != null) return !brain.IsCinematicSuspended ? brain : null;
        var enemy = collider.GetComponentInParent<EnemyController>();
        return enemy != null && enemy.CombatEnabled && !enemy.IsFlameDormant && !enemy.IsCinematicSuspended ? new LegacyCombatTarget(enemy) : null;
    }

    public bool FreeAttackLineClear(Vector3 origin, ICombatTarget target)
    {
        if (target == null || target.Root == null) return false;
        Vector3 from = origin + Vector3.up, to = target.Root.position + Vector3.up;
        foreach (var hit in Physics.RaycastAll(from, to - from, Vector3.Distance(from, to),
            freeAttackProfile.obstructionMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(playerRoot) || hit.collider.transform.IsChildOf(target.Root) ||
                hit.collider.GetComponentInParent<SquadCharacterController>() != null || TargetFromCollider(hit.collider) != null) continue;
            return false;
        }
        return true;
    }

    private ICombatTarget FindFreeAttackTarget(SkillSO skill, Vector3 direction, bool contact, ICombatTarget preferred)
    {
        SkillTargetingContract.EnemyRange(skill, out float minimumRange, out float maximumRange);
        float radius = contact ? freeAttackProfile.contactRadius : maximumRange;
        Vector3 center = contact ? playerRoot.TransformPoint(freeAttackProfile.contactCenter) : playerRoot.position + Vector3.up;
        // Ranged targeted skills retain their authored reach, with a narrow
        // forward contact corridor rather than the melee sphere.
        bool ranged = contact && (skill.targetingMode == SkillTargetingMode.RequiredTarget || skill.targetingMode == SkillTargetingMode.LockedTarget) && skill.MaximumHitDistance > 4;
        var contacts = ranged ? Physics.OverlapCapsule(playerRoot.position + Vector3.up,
            playerRoot.position + Vector3.up + direction * skill.MaximumHitDistance, .35f, ~0, QueryTriggerInteraction.Ignore) :
            Physics.OverlapSphere(center, Mathf.Max(.01f, radius), ~0, QueryTriggerInteraction.Ignore);
        ICombatTarget best = null;
        float bestAngle = float.PositiveInfinity, bestDistance = float.PositiveInfinity;
        var seen = new HashSet<Transform>();
        foreach (var collider in contacts)
        {
            var target = TargetFromCollider(collider);
            if (target == null || target.IsDead || !seen.Add(target.Root)) continue;
            Vector3 delta = target.Root.position - playerRoot.position;
            float distance = Vector3.ProjectOnPlane(delta, Vector3.up).magnitude;
            if (Mathf.Abs(delta.y) > freeAttackProfile.heightTolerance ||
                !(contact ? skill.IsWithinHitRange(distance) : distance >= minimumRange && distance <= maximumRange)) continue;
            float angle = Vector3.Angle(direction, Vector3.ProjectOnPlane(delta, Vector3.up));
            if (contact ? angle > skill.enemyImpact.arcDegrees * .5f :
                !FreeAttackProfile.InCone(direction, delta, freeAttackProfile.acquisitionCone, freeAttackProfile.maximumCorrection)) continue;
            if (!FreeAttackLineClear(playerRoot.position, target)) continue;
            if (preferred != null && target.Root == preferred.Root) return target;
            bool tie = Mathf.Approximately(angle, bestAngle) && Mathf.Approximately(distance, bestDistance);
            if (angle < bestAngle || Mathf.Approximately(angle, bestAngle) && distance < bestDistance ||
                tie && best != null && string.CompareOrdinal(target.Root.GetEntityId().ToString(), best.Root.GetEntityId().ToString()) < 0)
            { best = target; bestAngle = angle; bestDistance = distance; }
        }
        return best;
    }

    private bool TryUseFreeSkill(SkillSO skill, PlayerAttackContext resolved = null)
    {
        ResolvePlayerReferences();
        if (!CanUseFreeSkill || playerRoot == null || playerActionPresentation == null ||
            GetSkillCooldownRemaining(skill) > 0 || skill.AnimationClip == null ||
            !TryResolveSkillAnimatorState(skill, out int hash, out _)) return false;
        var context = resolved ?? CreateAttackContext(skill, 0);
        if (!ValidateFreeAttackContext(context)) return false;
        if (skill.TargetLunge.enabled && skill.TargetLunge.resolveImpactAtContact)
        {
            if (playerLocomotionBridge == null || !playerLocomotionBridge.IsDriving ||
                playerLocomotionBridge.IsExternalLockActive && !playerActionPresentation.OwnsOnlyActionMotionLock(playerLocomotionBridge)) return false;
            if (playerActionPresentation.GetSafeAttackTravel(skill, context) <= .05f) return false;
        }
        if (!playerActionPresentation.TryPlaySkill(skill, hash, context)) return false;
        if (playerActionPresentation.LastRequestWasBuffered) return true;
        NotifyBufferedSkillStarted(skill);
        return true;
    }

    public float FreeDashDistance => freeAttackProfile != null ? freeAttackProfile.freeDashDistance : 0;
    public bool ValidateFreeAttackContext(PlayerAttackContext context)
    {
        if (context == null) return false;
        var skill = context.Skill;
        if (!SkillTargetingContract.Validate(skill, out _)) return false;
        var mode = context.Mode;
        if (mode == SkillTargetingMode.LockedTarget && (!HasLockedCombatTarget || LockedCombatTarget.Root != context.TargetRoot)) return false;
        if ((mode == SkillTargetingMode.RequiredTarget || mode == SkillTargetingMode.LockedTarget) && !context.TargetAlive) return false;
        if (context.TargetAlive && (mode == SkillTargetingMode.RequiredTarget || mode == SkillTargetingMode.LockedTarget || mode == SkillTargetingMode.OptionalTarget))
        {
            if (!EnemyWithinLaunchRange(skill, context.Target)) return false;
        }
        return true;
    }
    public ICombatTarget FindFreeContactTarget(SkillSO skill) => AllowsFreeAttacks && playerRoot != null
        ? FindFreeAttackTarget(skill, playerRoot.forward, true, CurrentAttackContext?.TargetAlive == true ? CurrentAttackContext.Target : null) : null;

    public int ResolveFreeAttackImpact(SkillSO skill, out Transform victim, ICombatTarget travelledTarget = null)
    {
        victim = null;
        var context = CurrentAttackContext;
        if (freeAttackProfile != null && freeAttackProfile.diagnostics)
            Debug.Log($"[FreeAttack] Contact event skill={skill?.name} generation={context?.Generation}/{playerActionPresentation?.ActionGeneration} consumed={context?.ImpactConsumed} authority={CombatProtectionDamage.HasAuthority} cinematic={IsCinematicSequenceActive}", this);
        if (!AllowsFreeAttacks || !CombatProtectionDamage.HasAuthority || IsPlayerDead() || IsCinematicSequenceActive ||
            context == null || context.Skill != skill || !context.TryConsume(playerActionPresentation.ActionGeneration)) return 0;
        var target = travelledTarget ?? FindFreeAttackTarget(skill, playerRoot.forward, true, context.TargetAlive ? context.Target : null);
        if (freeAttackProfile.diagnostics)
            Debug.Log($"[FreeAttack] Contact sample caster={playerRoot.position} facing={playerRoot.forward} captured={context.TargetRoot?.position} victim={target?.Root?.name} victimPosition={target?.Root?.position}", this);
        if (target == null || target.IsDead || target.Root == null) return 0;
        if (context.Mode != SkillTargetingMode.FreeAttack && context.Target != null &&
            (!context.TargetAlive || target.Root != context.TargetRoot)) return 0;
        victim = target.LockPoint;
        int generation = context.Generation;
        if (ExternalImpactArbitrator != null && ExternalImpactArbitrator(skill, () =>
        {
            if (playerActionPresentation.IsCurrentSession(generation) && CurrentAttackContext == context)
                CompleteFreeAttackImpact(skill, target, true, action: context);
        })) return 0;
        return CompleteFreeAttackImpact(skill, target, false, action: context);
    }

    private int CompleteFreeAttackImpact(SkillSO skill, ICombatTarget target, bool deferred, bool cinematic = false, PlayerAttackContext action = null, int? damageOverride = null, bool evaluateOutcome = true)
    {
        if (!CombatProtectionDamage.HasAuthority || !SkillTargetingContract.Alive(target) || IsCinematicSequenceActive && !cinematic ||
            action == null || action.Skill != skill || !action.ImpactConsumed ||
            (cinematic ? combatSkillCinematicController?.Targeting != action : CurrentAttackContext != action || !playerActionPresentation.IsCurrentSession(action.Generation))) return 0;
        int applied;
        bool accepted;
        var legacy = target.Root.GetComponent<EnemyController>();
        if (legacy != null)
        {
            if (legacy.IsCinematicSuspended && !cinematic || legacy.IsFlameDormant) return 0;
            int damage = EnemyRevelation.ModifyDamage(target.Root, damageOverride ?? Mathf.RoundToInt(skill.Damages));
            var defence = legacy.GetComponent<EnemyTacticalResponseController>();
            if (defence != null && defence.IsDodging) return 0;
            if (defence != null) damage = defence.ResolveIncomingPlayerDamage(skill, damage);
            applied = legacy.ReceivePlayerContactDamage(damage, playerController, out accepted);
        }
        else
        {
            var brain = target.Root.GetComponent<LitBrainsEnemy>();
            if (brain != null && brain.IsCinematicSuspended && !cinematic) return 0;
            var impact = new CombatImpact(damageOverride ?? Mathf.RoundToInt(skill.Damages), skill.interruptionForce,
                CombatImpactOrigin.Basic, source: playerController);
            if (brain != null) applied = brain.ReceiveImpact(impact, out accepted);
            else
            {
                applied = DeliverExternalImpact(target, impact);
                accepted = applied > 0;
            }
        }
        if (accepted)
        {
            if (cinematic) BeginContactEncounter(playerRoot, target);
            else TryEngageFromPlayerContact(playerRoot, target, action);
        }
        if (applied <= 0) return 0;
        if (!cinematic && playerRoot != null && target.Root.TryGetComponent<CombatContactPose>(out var contactPose))
            contactPose.PlayContact(playerRoot.position, Mathf.InverseLerp(10, 35, skill.Damages));
        if (freeAttackProfile.diagnostics) Debug.Log($"[FreeAttack] Delivered skill={skill.name} victim={target.Root.name} damage={applied} deferred={deferred}", this);
        if (skill.stunSeconds > 0 && target is ICombatStunReceiver stun) stun.TryApplySkillStun(skill.stunSeconds);
        CombatDamageWorldFeedback.Show(target.Root, applied, Color.white, 2);
        AddClarity(skill.ClarityGainOnHit);
        PlayerLightDamageApplied?.Invoke(applied); PlayerSkillImpactApplied?.Invoke(skill, applied);
        if (deferred) playerActionPresentation.HandleConfirmedExternalImpact(skill, target.LockPoint);
        if (!cinematic && evaluateOutcome) EvaluateCombatOutcome();
        return applied;
    }

    public int ApplyTargetedCinematicSkillImpact(SkillSO skill, PlayerAttackContext context)
    {
        if (!AllowsFreeAttacks || !CombatProtectionDamage.HasAuthority || context == null || !context.TargetAlive || IsPlayerDead() ||
            combatSkillCinematicController == null || combatSkillCinematicController.Targeting != context ||
            !context.TryConsume(context.Generation)) return 0;
        var delta = context.TargetRoot.position - playerRoot.position;
        if (!skill.IsWithinHitRange(Vector3.ProjectOnPlane(delta, Vector3.up).magnitude) ||
            Mathf.Abs(delta.y) > freeAttackProfile.heightTolerance || !FreeAttackLineClear(playerRoot.position, context.Target)) return 0;
        return CompleteFreeAttackImpact(skill, context.Target, false, true, context);
    }

    public int ResolveReservedPlayerContact(SkillSO skill, ICombatTarget target, int damage)
    {
        var context = CurrentAttackContext;
        if (context == null || context.Skill != skill || context.TargetRoot != target?.Root ||
            !FreeAttackLineClear(playerRoot.position, target) || !context.TryConsume(playerActionPresentation.ActionGeneration)) return 0;
        return CompleteFreeAttackImpact(skill, target, false, action: context, damageOverride: damage);
    }
}
