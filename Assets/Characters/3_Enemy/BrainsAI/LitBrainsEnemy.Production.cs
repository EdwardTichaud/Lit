using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public partial class LitBrainsEnemy
{
    private bool productionHomeSet, productionEngaged, productionReturning;
    private Vector3 productionHome;
    private Quaternion productionHomeRotation;
    private EnemyCombatPattern productionPattern, productionPreviousPattern, productionAirborneChoice;
    private int productionStep, productionConsecutive;
    private float productionObserveUntil, productionReadyAt, productionGuardUntil, productionNextGuardAt, productionReturnAt;
    private bool productionAirborneChosen;
    private int productionEngagementMaximumLightDamage, productionCommittedDamage;
    private int productionDamageSequence = -1;
    public int EngagementMaximumLightDamage => productionEngagementMaximumLightDamage;
    private readonly Dictionary<EnemyCombatPattern, float> productionCooldowns = new Dictionary<EnemyCombatPattern, float>();
    private bool UsesProductionPatterns => combatProfile != null && combatProfile.executeProductionPatterns && combatProfile.productionSourceProfile != null;
    public bool UsesAuthoredProductionCombat => UsesProductionPatterns;
    private EnemyCombatProfileSO ProductionProfile => UsesProductionPatterns ?
        RuntimeCharacterData != null && RuntimeCharacterData.enemyCombatProfile != null ? RuntimeCharacterData.enemyCombatProfile : combatProfile.productionSourceProfile : null;
    private float ProductionNow => GetComponent<CombatTimeDomain>() is CombatTimeDomain clock ? clock.LocalTime : Time.time;
    private float ProductionDelta => GetComponent<CombatTimeDomain>() is CombatTimeDomain clock ? clock.DeltaTime : Time.deltaTime;
    public bool IsEncounterEngaged => isActiveAndEnabled && !IsDead && EncounterAllowsDecisions &&
        (NavMeshWorldService.Instance == null || NavMeshWorldService.Instance.IsReady) &&
        !productionReturning && !EncounterIsReturning && (UsesProductionPatterns && productionEngaged || fov != null && fov.CurrentTarget != null);
    public bool IsWithinPursuitBounds(Transform actor)
    {
        if (actor == null) return false;
        EnsureProductionHome();
        float radius = ProductionProfile != null ? ProductionProfile.pursuitRadius :
            RuntimeCharacterData != null && RuntimeCharacterData.vision != null ? RuntimeCharacterData.vision.maximumDistance : 0;
        return Vector3.ProjectOnPlane(actor.position - productionHome, Vector3.up).sqrMagnitude <= radius * radius;
    }
    public bool IsThreatFor(Transform actor) => IsEncounterEngaged && IsLivingTarget(actor) && IsWithinPursuitBounds(actor);

    private void EnsureProductionHome()
    {
        if (productionHomeSet) return;
        productionHomeSet = true;
        productionHome = transform.position;
        productionHomeRotation = transform.rotation;
    }

    private bool ProductionCanStart(EnemyCombatPattern pattern, SkillSO skill)
    {
        var target = fov.CurrentTarget;
        if (!IsLivingTarget(target) || !IsWithinPursuitBounds(target) || !RuntimeCharacterData.combatSkills.Contains(skill)) return false;
        Vector3 delta = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
        var path = new NavMeshPath();
        return delta.magnitude >= pattern.minimumStartDistance && skill.IsWithinHitRange(delta.magnitude) &&
            Vector3.Angle(transform.forward, delta) <= pattern.maximumStartAngle && HasAttackLineOfSight() &&
            agent.CalculatePath(target.position, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    private bool ProductionEquipped(EnemyCombatPattern pattern) => pattern != null && pattern.IsConfigured && pattern.weight > 0 &&
        RuntimeCharacterData != null && RuntimeCharacterData.combatSkills != null && pattern.skills.All(skill => RuntimeCharacterData.combatSkills.Contains(skill)) &&
        pattern.skills.All(skill => combatProfile.attacks.Any(attack => attack.skill == skill));

    private bool ProductionAvailable(EnemyCombatPattern pattern)
    {
        if (!ProductionEquipped(pattern) || productionCooldowns.TryGetValue(pattern, out float until) && ProductionNow < until) return false;
        return pattern != productionPreviousPattern || productionConsecutive < pattern.maximumConsecutiveUses ||
            !ProductionProfile.patterns.Any(other => other != pattern && ProductionEquipped(other));
    }

    private bool TickProductionEncounter()
    {
        if (!UsesProductionPatterns) return false;
        EnsureProductionHome();
        var source = ProductionProfile;
        var target = fov.CurrentTarget;
        // Leaving the leash ends pursuit, not an already committed living-target
        // attack. Its normal completion rejects the next combo step outside bounds.
        if (IsLivingTarget(target) && !IsWithinPursuitBounds(target) && AttackPhase != LitBrainsAttackPhase.None)
            return false;
        if (target != null && !IsWithinPursuitBounds(target) || productionEngaged && !IsLivingTarget(target))
        {
            productionReturning = true;
            productionEngaged = false;
            ResetProductionEngagementDamage();
            productionReturnAt = ProductionNow + source.disengagePauseSeconds;
            productionPattern = null;
            productionPreviousPattern = null;
            productionConsecutive = 0;
            productionAirborneChosen = false;
            CancelFluidAttack();
            ResetPerception();
        }
        if (!productionReturning) return false;
        if (hurtRemaining > 0) { TickFluidCombat(); StopFluidMovement(); UpdateAnimator(); return true; }
        target = ResolvePerceptionTarget();
        if (IsLivingTarget(target) && IsWithinPursuitBounds(target) &&
            RuntimeCharacterData.vision.TryEvaluate(transform, target, source.returnReengageDistance, 360, out _, out _, out _))
        {
            productionReturning = false;
            productionEngaged = true;
            fov.SetExternalTarget(target);
            nextDetection = 0;
            return false;
        }
        if (ProductionNow < productionReturnAt) { StopFluidMovement(); return true; }
        Vector3 homeDirection = Vector3.ProjectOnPlane(productionHome - transform.position, Vector3.up);
        if (homeDirection.magnitude > .25f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(homeDirection),
                source.trackingDegreesPerSecond * ProductionDelta);
            agent.isStopped = false;
            MoveTo(productionHome, runSpeed, .15f);
        }
        else
        {
            StopFluidMovement();
            transform.rotation = Quaternion.RotateTowards(transform.rotation, productionHomeRotation, source.trackingDegreesPerSecond * ProductionDelta);
            if (Quaternion.Angle(transform.rotation, productionHomeRotation) <= 1) productionReturning = false;
        }
        UpdateAnimator();
        return true;
    }

    private void HandleProductionApproach()
    {
        var source = ProductionProfile;
        if (fov.CurrentTarget == null || productionPattern != null || AttackPhase != LitBrainsAttackPhase.None || hurtRemaining > 0) return;
        if (ProductionNow < productionGuardUntil || ProductionNow < productionReadyAt) { StopFluidMovement(); return; }
        if (productionGuardUntil > 0)
        {
            productionGuardUntil = 0;
            anim.CrossFadeInFixedTime(combatProfile.idleState, .08f, 0);
        }
        TurnToFluidTarget();
        float distance = Vector3.ProjectOnPlane(fov.CurrentTarget.position - transform.position, Vector3.up).magnitude;
        var equipped = source.patterns.Where(ProductionEquipped).ToArray();
        bool hasMelee = equipped.Any(pattern => !pattern.skills[0].EnemyActionMotion.IsAirborne);
        if (!productionAirborneChosen && source.preferMeleeApproach && hasMelee)
        {
            var airborne = equipped.FirstOrDefault(pattern => pattern.skills[0].EnemyActionMotion.IsAirborne && ProductionAvailable(pattern) && ProductionCanStart(pattern, pattern.skills[0]));
            if (airborne != null)
            {
                productionAirborneChosen = true;
                if (Random.value < source.airborneAlternativeChance) productionAirborneChoice = airborne;
            }
        }
        var candidates = FilterProductionApproachCandidates(equipped);
        if (candidates.Length == 0) return;
        float desired = candidates.Select(ProductionApproachDistance)
            .OrderBy(value => Mathf.Abs(distance - value)).First();
        if (!candidates.Any(pattern => ProductionCanStart(pattern, pattern.skills[0])))
        {
            productionObserveUntil = 0;
            if (ProductionNeedsRetreat(candidates, distance))
            {
                Vector3 away = Vector3.ProjectOnPlane(transform.position - fov.CurrentTarget.position, Vector3.up);
                if (away.sqrMagnitude < .001f) away = -transform.forward;
                Vector3 destination = fov.CurrentTarget.position + away.normalized * desired;
                destination.y = transform.position.y;
                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                var world = NavMeshWorldService.Instance;
                var path = new NavMeshPath();
                if (!NavMesh.SamplePosition(destination, out var retreat, .15f, filter) ||
                    world != null && !world.TryValidatePosition(destination, agent.areaMask, out _) ||
                    !agent.CalculatePath(retreat.position, path) || path.status != NavMeshPathStatus.PathComplete)
                { StopFluidMovement(); return; }
                agent.isStopped = false;
                MoveTo(retreat.position, walkSpeed, .05f);
                return;
            }
            agent.isStopped = false;
            MoveTo(fov.CurrentTarget.position, runSpeed, desired);
            return;
        }
        StopFluidMovement();
        if (productionObserveUntil <= 0)
        { productionObserveUntil = ProductionNow + Random.Range(source.observationSeconds.x, source.observationSeconds.y); return; }
        if (ProductionNow < productionObserveUntil) return;
        EnemyCombatPattern choice = null;
        int total = 0;
        foreach (var pattern in candidates)
            if (ProductionAvailable(pattern) && ProductionCanStart(pattern, pattern.skills[0]))
            { total += pattern.weight; if (Random.Range(0, total) < pattern.weight) choice = pattern; }
        if (choice == null) return;
        if (ProductionNow >= productionNextGuardAt && distance < 3.5f && Random.value < source.guardChance && HasCombatState(combatProfile.guardState))
        {
            productionGuardUntil = ProductionNow + source.guardDurationSeconds;
            productionNextGuardAt = ProductionNow + source.guardCooldownSeconds;
            anim.CrossFadeInFixedTime(combatProfile.guardState, .08f, 0);
            productionObserveUntil = 0;
            return;
        }
        productionConsecutive = choice == productionPreviousPattern ? productionConsecutive + 1 : 1;
        productionPreviousPattern = productionPattern = choice;
        productionStep = 0;
        CommitProductionStep();
    }

    private static bool ProductionNeedsRetreat(EnemyCombatPattern[] candidates, float distance) =>
        candidates.Length > 0 && candidates.All(pattern => distance < Mathf.Max(pattern.minimumStartDistance, pattern.skills[0].MinimumHitDistance));

    private float ProductionApproachDistance(EnemyCombatPattern pattern)
    {
        float minimum = Mathf.Max(pattern.minimumStartDistance, pattern.skills[0].MinimumHitDistance);
        float maximum = pattern.skills[0].MaximumHitDistance;
        float margin = Mathf.Min(.2f, Mathf.Max(0, (maximum - minimum) * .5f));
        return Mathf.Clamp(ProductionProfile.preferredCombatDistance, minimum + margin, maximum - margin);
    }

    private EnemyCombatPattern[] FilterProductionApproachCandidates(EnemyCombatPattern[] equipped)
    {
        // A preference cannot exclude the actor's entire authored loadout.
        if (!ProductionProfile.preferMeleeApproach || !equipped.Any(pattern => !pattern.skills[0].EnemyActionMotion.IsAirborne))
            return equipped;
        return equipped.Where(pattern => !pattern.skills[0].EnemyActionMotion.IsAirborne || pattern == productionAirborneChoice).ToArray();
    }

    private void CommitProductionStep()
    {
        SkillSO skill = productionPattern.skills[productionStep];
        int index = System.Array.FindIndex(combatProfile.attacks, attack => attack.skill == skill);
        CommitFluidAttack(index, fov.CurrentTarget);
    }

    private void CompleteProductionStep(bool abort)
    {
        if (productionPattern == null) { CancelFluidAttack(); return; }
        var pattern = productionPattern;
        if (!abort && CombatHealthThresholdController.Instance?.HasPendingStage(this) != true &&
            productionStep + 1 < pattern.skills.Count && ProductionCanStart(pattern, pattern.skills[productionStep + 1]))
        { productionStep++; CancelFluidAttack(); productionPattern = pattern; CommitProductionStep(); return; }
        productionCooldowns[pattern] = ProductionNow + pattern.cooldownSeconds;
        productionPattern = null;
        productionReadyAt = ProductionNow + pattern.recoverySeconds;
        productionObserveUntil = 0;
        productionAirborneChosen = false;
        productionAirborneChoice = null;
        CancelFluidAttack();
        anim.CrossFadeInFixedTime(combatProfile.idleState, .08f, 0);
    }

    private int ResolveProductionGuard(int amount, SquadCharacterController source)
    {
        if (!UsesProductionPatterns || ProductionNow >= productionGuardUntil || source == null || productionPattern != null) return amount;
        return Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(source.transform.position - transform.position, Vector3.up)) <= 70
            ? Mathf.CeilToInt(amount * ProductionProfile.guardedDamageMultiplier) : amount;
    }

    private void UpdateProductionAnimator()
    {
        Vector3 velocity = agent != null && agent.enabled && agent.isOnNavMesh ? agent.velocity : Vector3.zero;
        bool moving = velocity.sqrMagnitude > .0025f && AttackPhase == LitBrainsAttackPhase.None && !ProductionMotionActive && ProductionNow >= productionGuardUntil;
        Vector3 direction = transform.InverseTransformDirection(velocity.normalized);
        anim.SetFloat("HorizontalMovement", moving ? direction.x : 0, .08f, Time.deltaTime);
        anim.SetFloat("ForwardMovement", moving ? direction.z : 0, .08f, Time.deltaTime);
        anim.SetFloat("CombatMoveMagnitude", moving ? 1 : 0, .08f, Time.deltaTime);
        anim.SetFloat("CombatLocomotionPlaybackRate", moving ? velocity.magnitude / Mathf.Max(.01f, RuntimeCharacterData.enemySettings.LocomotionRunCycleSpeed) : 0);
        anim.SetFloat("LocomotionTier", 3.25f);
        anim.SetBool("CombatStrafeActive", true);
        if (moving && !anim.GetCurrentAnimatorStateInfo(0).IsName(CombatLocomotionState) &&
            (!anim.IsInTransition(0) || !anim.GetNextAnimatorStateInfo(0).IsName(CombatLocomotionState)))
            anim.CrossFadeInFixedTime(CombatLocomotionState, .08f, 0);
    }

    public bool HasAuthoredImpactContact(SquadCharacterController victim)
    {
        if (victim == null || victim.CurrentHp <= 0 || activeAttack?.skill == null || activeAttack.skill.enemyImpact == null) return false;
        var skill = activeAttack.skill;
        var shape = skill.enemyImpact;
        Vector3 delta = Vector3.ProjectOnPlane(victim.transform.position - transform.position, Vector3.up);
        if (!skill.IsWithinHitRange(delta.magnitude) || Vector3.Angle(transform.forward, delta) > shape.arcDegrees * .5f ||
            shape.requireGroundedTarget && !victim.IsGrounded) return false;
        foreach (var collider in Physics.OverlapSphere(transform.TransformPoint(shape.offset), shape.radius, shape.targetMask, QueryTriggerInteraction.Ignore))
            if (collider.GetComponentInParent<SquadCharacterController>() == victim) return true;
        return false;
    }

    public void CollectAuthoredImpactVictims(List<SquadCharacterController> victims)
    {
        victims.Clear();
        if (activeAttack?.skill == null || activeAttack.skill.enemyImpact == null) return;
        var shape = activeAttack.skill.enemyImpact;
        foreach (var collider in Physics.OverlapSphere(transform.TransformPoint(shape.offset), shape.radius, shape.targetMask, QueryTriggerInteraction.Ignore))
        {
            var victim = collider.GetComponentInParent<SquadCharacterController>();
            if (victim != null && !victims.Contains(victim) && HasAuthoredImpactContact(victim)) victims.Add(victim);
        }
    }
    public int AuthoredAttackDamage => UsesProductionPatterns && activeAttack != null && productionDamageSequence == ActionSequenceId
        ? productionCommittedDamage : 0;

    private void RecordProductionAppliedDamage(int applied)
    {
        // Legacy ReceiveLightDamage, ReceivePlayerContactDamage and ReceivePassiveDamage
        // all prepare the engagement ledger, including hits during an active attack/QTE.
        if (UsesProductionPatterns && !IsDead && applied > 0)
            productionEngagementMaximumLightDamage = Mathf.Max(productionEngagementMaximumLightDamage, applied);
    }

    private void CaptureProductionAttackDamage(SkillSO skill, bool thresholdRetaliation)
    {
        ClearProductionCommittedDamage();
        if (!UsesProductionPatterns || skill == null) return;
        float basis = productionEngagementMaximumLightDamage > 0 ? productionEngagementMaximumLightDamage : Mathf.Max(0f, skill.Damages);
        productionCommittedDamage = thresholdRetaliation ? Mathf.Max(0, Mathf.RoundToInt(skill.Damages)) :
            Mathf.Max(1, Mathf.CeilToInt(basis * Mathf.Max(0f, skill.EnemyDamageMultiplier)));
        productionDamageSequence = ActionSequenceId;
    }

    private void ClearProductionCommittedDamage()
    {
        productionCommittedDamage = 0;
        productionDamageSequence = -1;
    }

    private void ResetProductionEngagementDamage()
    {
        productionEngagementMaximumLightDamage = 0;
        ClearProductionCommittedDamage();
    }
}
