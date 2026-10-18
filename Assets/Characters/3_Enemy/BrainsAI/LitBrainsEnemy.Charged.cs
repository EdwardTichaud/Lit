using UnityEngine;
using UnityEngine.AI;

public partial class LitBrainsEnemy : ICombatChargedTarget
{
    public Transform ChargedRoot => this != null ? transform : null;
    public bool ChargedAlive => this != null && !IsDead && isActiveAndEnabled;
    public bool ChargedProtected => this == null || IsCinematicSuspended;
    public int ChargedGeneration => ActionSequenceId;
    private bool chargedAirborne;
    private bool chargedLandingAttachment;
    private Rigidbody chargedBody;
    private CapsuleCollider chargedCapsule;
    private float chargedRiseRemaining, chargedRiseSpeed, chargedHoldRemaining, chargedFallSpeed;
    private float chargedConfirmedGroundY;
    private bool chargedLandingReported;
    private string chargedFloatState;
    public bool IsChargedAirborne => chargedAirborne;
    public bool CanReceiveChargedLaunch => HasSimulationAuthority && EncounterAllowsDamage && isActiveAndEnabled && !dead && !cinematicSuspended && !chargedAirborne &&
        agent != null && (ProductionMotionActive ? CanLaunchGroundedProductionAction() :
            navigationReady && agent.enabled && agent.isOnNavMesh && !agent.isOnOffMeshLink) &&
        GetComponent<Rigidbody>() != null && GetComponent<Rigidbody>().isKinematic && GetComponent<CapsuleCollider>() != null;

    private bool CanLaunchGroundedProductionAction() => !productionAirborne &&
        (NavMeshWorldService.Instance == null || NavMeshWorldService.Instance.IsReady) &&
        TryProductionGround(transform.position, out var ground) && Mathf.Abs(ground.point.y - transform.position.y) <= .15f;

    public bool BeginChargedLaunch(ChargedSkillSO skill)
    {
        if (skill == null || !CanReceiveChargedLaunch || !ProductionMotionActive && !IsClashGrounded) return false;
        chargedBody = GetComponent<Rigidbody>();
        chargedCapsule = GetComponent<CapsuleCollider>();
        CancelClashRecovery();
        CancelFluidAttack();
        productionMotionActive = productionAirborne = productionAdvance = productionRush = productionEndRequested = productionAbort = false;
        productionMotionSkill = null;
        productionPattern = null;
        StopFluidMovement();
        ActionSequenceId++;
        agent.enabled = false;
        navigationReady = false;
        chargedAirborne = true;
        chargedRiseRemaining = skill.ascentSeconds;
        chargedRiseSpeed = skill.launchHeight / Mathf.Max(.1f, skill.ascentSeconds);
        // Missing second event cannot leave a target suspended indefinitely.
        chargedHoldRemaining = Mathf.Min(8f, Mathf.Max(skill.AnimationClip.length, skill.ascentSeconds) + skill.airborneHoldSeconds);
        chargedFallSpeed = 0;
        chargedConfirmedGroundY = transform.position.y;
        chargedLandingReported = false;
        chargedFloatState = skill.enemyFloatState;
        StopControlConditions["LitBrainsEnemy.Charged"] = true;
        anim.SetBool("Grounded", false);
        anim.ResetTrigger("Hurt");
        PlayChargedFloatHit();
        return true;
    }

    public void BeginChargedFloat(float holdSeconds)
    {
        if (chargedAirborne) chargedHoldRemaining = Mathf.Max(0, holdSeconds);
    }

    public void ReleaseChargedHold()
    {
        chargedRiseRemaining = chargedHoldRemaining = 0;
    }

    private void FixedUpdate()
    {
        if (TickProductionMotion()) return;
        if (!HasSimulationAuthority || IsFlameDormant || !chargedAirborne || cinematicSuspended || chargedBody == null || chargedCapsule == null) return;
        var domain = GetComponent<CombatTimeDomain>();
        float dt = domain != null ? domain.FixedDeltaTime : Time.fixedDeltaTime;
        if (dt <= 0) return;
        Vector3 position = chargedBody.position;
        Vector3 center = position + transform.rotation * Vector3.Scale(chargedCapsule.center, transform.lossyScale);
        float radius = chargedCapsule.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        float half = Mathf.Max(radius, chargedCapsule.height * Mathf.Abs(transform.lossyScale.y) * .5f);
        bool rising = chargedRiseRemaining > 0;
        float step;
        if (rising)
        {
            step = chargedRiseSpeed * Mathf.Min(dt, chargedRiseRemaining);
            chargedRiseRemaining = Mathf.Max(0, chargedRiseRemaining - dt);
        }
        else if (!dead && chargedHoldRemaining > 0)
        { chargedHoldRemaining = Mathf.Max(0, chargedHoldRemaining - dt); return; }
        else
        { chargedFallSpeed += 20f * dt; step = -chargedFallSpeed * dt; }
        Vector3 direction = step >= 0 ? Vector3.up : Vector3.down;
        float allowed = Mathf.Abs(step);
        bool collided = false;
        int mask = LayerMask.GetMask("Default", "Ground", "Obstacle", "CameraObstruction");
        foreach (var hit in Physics.CapsuleCastAll(center + Vector3.up * (half - radius), center - Vector3.up * (half - radius),
            radius * .95f, direction, allowed + .04f, mask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<SquadCharacterController>() != null ||
                hit.collider.GetComponentInParent<LitBrainsEnemy>() != null) continue;
            if (!rising && hit.normal.y < .5f) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0, hit.distance - .025f));
            collided = true;
        }
        if (!rising && position.y - allowed < chargedConfirmedGroundY)
        {
            // A removed floor cannot cause an infinite fall. Descend physically
            // to the previously confirmed height; navigation still requires a real floor.
            allowed = Mathf.Max(0, position.y - chargedConfirmedGroundY);
            collided = true;
        }
        chargedBody.MovePosition(position + direction * allowed);
        if (collided && rising) chargedRiseRemaining = 0;
        if (collided && !rising) FinishChargedLanding();
    }

    private void FinishChargedLanding()
    {
        chargedLandingAttachment = true;
        chargedAirborne = false;
        chargedRiseRemaining = chargedHoldRemaining = chargedFallSpeed = 0;
        StopControlConditions["LitBrainsEnemy.Charged"] = false;
        anim.SetBool("Grounded", true);
        if (dead || cinematicSuspended) return;
        // Validate locally; do not warp or reuse an airborne agent position.
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        Vector3 expected = chargedBody.position - Vector3.up * agent.baseOffset;
        if (HasChargedLandingGround() && NavMesh.SamplePosition(expected, out var hit, .15f, filter) &&
            Mathf.Abs(hit.position.y - expected.y) <= .15f && Vector3.ProjectOnPlane(hit.position - expected, Vector3.up).magnitude <= .15f)
        {
            agent.enabled = true;
            navigationReady = agent.isOnNavMesh;
        }
        hurtRemaining = .15f;
        anim.CrossFadeInFixedTime(CombatLocomotionState, .08f, 0);
    }

    private void ClearChargedReaction()
    {
        chargedAirborne = false;
        chargedRiseRemaining = chargedHoldRemaining = chargedFallSpeed = 0;
        StopControlConditions["LitBrainsEnemy.Charged"] = false;
    }

    private void PlayChargedFloatHit()
    {
        if (dead || cinematicSuspended || IsCounterStunned || anim == null || !anim.isActiveAndEnabled) return;
        string state = !string.IsNullOrWhiteSpace(chargedFloatState) && anim.HasState(0, Animator.StringToHash(chargedFloatState))
            ? chargedFloatState : "Hurt";
        if (!anim.GetCurrentAnimatorStateInfo(0).IsName(state) &&
            (!anim.IsInTransition(0) || !anim.GetNextAnimatorStateInfo(0).IsName(state)))
            anim.CrossFadeInFixedTime(state, .08f, 0, 0);
    }

    private bool HasChargedLandingGround()
    {
        foreach (var hit in Physics.RaycastAll(transform.position + Vector3.up * .35f, Vector3.down, .7f,
            LayerMask.GetMask("Default", "Ground"), QueryTriggerInteraction.Ignore))
            if (!hit.collider.transform.IsChildOf(transform) && hit.normal.y >= .5f &&
                hit.collider.GetComponentInParent<SquadCharacterController>() == null &&
                hit.collider.GetComponentInParent<LitBrainsEnemy>() == null) return true;
        return false;
    }

    private void ReportChargedLandingFailure(Vector3 expected)
    {
        if (chargedLandingReported) return;
        chargedLandingReported = true;
        Debug.LogWarning("[ChargedSkill] Reprise NavMesh refusee apres atterrissage : sol ou projection locale invalide | actor=" + name + " expected=" + expected, this);
    }
}
