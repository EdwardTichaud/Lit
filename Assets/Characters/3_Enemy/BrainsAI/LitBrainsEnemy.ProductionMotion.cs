using UnityEngine;
using UnityEngine.AI;

public partial class LitBrainsEnemy
{
    private bool productionMotionActive, productionAirborne, productionAdvance, productionRush, productionEndRequested, productionAbort;
    private SkillSO productionMotionSkill;
    private Rigidbody productionBody;
    private CapsuleCollider productionCapsule;
    private float productionVerticalSpeed, productionAirTime, productionAdvanceTime, productionAdvanceProgress, productionRushTime, productionRushDistance;
    private Vector3 productionAdvanceDirection, productionRushDirection;
    private bool ProductionMotionActive => productionMotionActive;

    private void BeginProductionMotion(SkillSO skill)
    {
        productionBody = GetComponent<Rigidbody>();
        productionCapsule = GetComponent<CapsuleCollider>();
        productionMotionSkill = skill;
        productionMotionActive = true;
        productionAirborne = productionAdvance = productionRush = productionEndRequested = productionAbort = false;
        productionVerticalSpeed = productionAirTime = 0;
        ResetProductionLandingRecovery();
        productionLandingOrigin = agent != null && agent.enabled && agent.isOnNavMesh ? agent.nextPosition : transform.position;
        StopFluidMovement();
        SuspendNavigation();
    }

    public void HandleProductionBrainsBeginAdvance(AnimationEvent source)
    {
        if (!AcceptFluidEvent(source) || !UsesProductionPatterns || productionAirborne || !productionMotionSkill.EnemyActionMotion.enableAdvance) return;
        productionAdvance = true;
        productionAdvanceTime = productionAdvanceProgress = 0;
        productionAdvanceDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
    }
    public void HandleProductionBrainsEndAdvance(AnimationEvent source) { if (AcceptFluidEvent(source)) productionAdvance = false; }
    public void HandleProductionBrainsBeginAirborne(AnimationEvent source)
    {
        if (!AcceptFluidEvent(source) || !UsesProductionPatterns || !productionMotionSkill.EnemyActionMotion.IsAirborne || productionAirborne) return;
        productionAirborne = true;
        productionAdvance = false;
        productionVerticalSpeed = productionMotionSkill.EnemyActionMotion.initialUpwardSpeed;
        productionAirTime = 0;
    }
    public void HandleProductionBrainsRequestLanding(AnimationEvent source)
    {
        if (!AcceptFluidEvent(source) || !productionAirborne) return;
        if (!productionRush) productionVerticalSpeed = Mathf.Min(productionVerticalSpeed, -productionMotionSkill.EnemyActionMotion.minimumLandingSpeed);
    }
    public void HandleProductionBrainsBeginRush(AnimationEvent source)
    {
        if (!AcceptFluidEvent(source) || !productionAirborne || productionRush || CommittedAttackTarget == null) return;
        var motion = productionMotionSkill.EnemyActionMotion;
        if (!motion.HasHomingRush) return;
        Vector3 delta = CommittedAttackTarget.position - productionBody.position;
        productionRushDirection = delta.normalized;
        productionRushDistance = Mathf.Max(0, delta.magnitude - motion.rushStoppingDistance);
        productionRushTime = motion.rushImpulseDuration;
        productionRush = productionRushDistance > 0;
        productionVerticalSpeed = 0;
    }
    public void HandleProductionBrainsEndRush(AnimationEvent source) { if (AcceptFluidEvent(source)) productionRush = false; }

    private void RequestProductionMotionEnd(bool abort)
    {
        productionEndRequested = true;
        productionAbort |= abort;
        productionAdvance = productionRush = false;
        if (productionAirborne && productionMotionSkill != null)
            productionVerticalSpeed = Mathf.Min(productionVerticalSpeed, -productionMotionSkill.EnemyActionMotion.minimumLandingSpeed);
    }

    private bool TickProductionMotion()
    {
        if (!productionMotionActive) return false;
        if (!HasSimulationAuthority || cinematicSuspended || IsFlameDormant || productionBody == null || productionCapsule == null ||
            NavMeshWorldService.Instance != null && !NavMeshWorldService.Instance.IsReady) return true;
        var clock = GetComponent<CombatTimeDomain>();
        float dt = clock != null ? clock.FixedDeltaTime : Time.fixedDeltaTime;
        if (dt <= 0) return true;
        var motion = productionMotionSkill.EnemyActionMotion;
        Vector3 position = productionBody.position;
        Vector3 step = Vector3.zero;
        if (productionLandingRecovery)
        {
            if (!TickProductionLandingRecovery(position, dt, motion, out step)) return true;
        }
        if (productionAdvance)
        {
            productionAdvanceTime += dt;
            float t = Mathf.Clamp01(productionAdvanceTime / Mathf.Max(.01f, motion.advanceDuration));
            float progress = Mathf.Max(productionAdvanceProgress, t >= 1 ? 1 : Mathf.Clamp01(motion.advanceProgress?.Evaluate(t) ?? t));
            step += productionAdvanceDirection * ((progress - productionAdvanceProgress) * motion.advanceDistance);
            productionAdvanceProgress = progress;
            if (t >= 1) productionAdvance = false;
        }
        if (productionAirborne)
        {
            productionAirTime += dt;
            if (productionAirTime >= motion.maximumAirborneSeconds) RequestProductionMotionEnd(true);
            if (productionRush)
            {
                float distance = Mathf.Min(productionRushDistance, motion.rushMaximumSpeed * Mathf.Min(dt, productionRushTime));
                step += productionRushDirection * distance;
                productionRushDistance -= distance;
                productionRushTime -= dt;
                if (productionRushDistance <= 0 || productionRushTime <= 0) productionRush = false;
            }
            else
            {
                productionVerticalSpeed = Mathf.Max(-motion.maximumFallSpeed, productionVerticalSpeed - motion.gravity * dt);
                step.y += productionVerticalSpeed * dt;
            }
        }
        bool capsuleLanding = productionLandingRecovery || productionAirborne && step.y <= 0;
        if (capsuleLanding)
        {
            step = ClampProductionLandingStep(position, step, motion, out productionCapsuleSupported);
            if (productionCapsuleSupported) { productionAirborne = false; productionRush = false; }
        }
        else step = ClampProductionMotion(position, step, motion);
        Vector3 probe = productionLandingRecovery ? position + Vector3.ProjectOnPlane(step, Vector3.up) : productionAirborne ? position : position + step;
        if (!capsuleLanding && !productionCapsuleSupported && (!productionAirborne || step.y <= 0) && TryProductionGround(probe, out var ground))
        {
            float feet = position.y + step.y;
            float skin = RuntimeCharacterData.enemySettings.PhysicsGroundSkin;
            float snap = Mathf.Min(navMeshAttachmentTolerance, RuntimeCharacterData.enemySettings.PhysicsMaximumGroundSnapDistance);
            if (!productionAirborne && !productionLandingRecovery && Mathf.Abs(ground.point.y + skin - position.y) <= snap ||
                (productionAirborne || productionLandingRecovery) && feet <= ground.point.y + skin && ground.point.y <= position.y + skin)
            {
                step.y = ground.point.y + skin - position.y;
                productionAirborne = false;
                productionVerticalSpeed = 0;
                productionRush = false;
            }
        }
        productionBody.MovePosition(position + step);
        if (dead && !productionAirborne)
        {
            productionMotionActive = false;
            ResetProductionLandingRecovery();
            productionMotionSkill = null;
        }
        return true;
    }

    private bool TryProductionGround(Vector3 position, out RaycastHit nearest)
    {
        nearest = default;
        var settings = RuntimeCharacterData.enemySettings;
        bool found = false;
        foreach (var hit in Physics.RaycastAll(position + Vector3.up * settings.PhysicsGroundProbeStartHeight, Vector3.down,
            settings.PhysicsGroundProbeStartHeight + settings.PhysicsGroundProbeDistance, settings.PhysicsGroundMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.normal.y < settings.PhysicsMinimumGroundNormal ||
                hit.collider.GetComponentInParent<SquadCharacterController>() != null || hit.collider.GetComponentInParent<LitBrainsEnemy>() != null) continue;
            if (!found || hit.distance < nearest.distance) { nearest = hit; found = true; }
        }
        return found;
    }

    private Vector3 ClampProductionMotion(Vector3 position, Vector3 step, EnemyActionMotionProfile motion)
    {
        if (step.sqrMagnitude <= .0000001f) return step;
        ProductionCapsuleEndpoints(position, out var top, out var bottom, out float radius);
        float distance = step.magnitude;
        float allowed = distance;
        foreach (var hit in Physics.CapsuleCastAll(top, bottom,
            radius, step.normalized, distance + motion.rushCollisionSkin, motion.rushBlockingMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.normal.y >= RuntimeCharacterData.enemySettings.PhysicsMinimumGroundNormal) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0, hit.distance - motion.rushCollisionSkin));
        }
        if (!productionAirborne && !productionLandingRecovery)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(position, out var start, navMeshAttachmentTolerance, filter)) return Vector3.zero;
            if (NavMesh.Raycast(start.position, start.position + step, out var wall, filter))
                allowed = Mathf.Min(allowed, Mathf.Max(0, wall.distance - motion.rushCollisionSkin));
        }
        if (allowed < distance) { productionAdvance = productionRush = false; }
        return step.normalized * allowed;
    }

    private void TickProductionAction(float dt)
    {
        if (AttackPhase == LitBrainsAttackPhase.Preparation && !productionEndRequested) TurnToFluidTarget();
        attackElapsed += dt;
        if (productionMotionSkill != null && attackElapsed > productionMotionSkill.AnimationClip.length + RuntimeCharacterData.enemySettings.RecoveryExtraRecoverySeconds)
            RequestProductionMotionEnd(true);
        if (!productionEndRequested || productionAirborne) return;
        if (!productionCapsuleSupported && (!TryProductionGround(transform.position, out var ground) ||
            Mathf.Abs(ground.point.y - transform.position.y) > navMeshAttachmentTolerance)) return;
        if (dead)
        {
            productionMotionActive = false;
            ResetProductionLandingRecovery();
            productionMotionSkill = null;
            return;
        }
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        var world = NavMeshWorldService.Instance;
        if (!NavMesh.SamplePosition(transform.position, out _, .15f, filter) ||
            world != null && !world.TryValidatePosition(transform.position, agent.areaMask, out _))
        {
            if (!productionLandingRecovery && !TryBeginProductionLandingRecovery(transform.position))
                FailProductionLandingRecovery("no nearby clear connected landing surface");
            return;
        }
        productionMotionActive = false;
        if (!TryPrepareNavigation()) { productionMotionActive = true; return; }
        ResetProductionLandingRecovery();
        productionMotionSkill = null;
        CompleteProductionStep(productionAbort);
    }
}
