using UnityEngine;
using UnityEngine.AI;

public enum LitBrainsAttackPhase { None, Preparation, Strike, Recovery }

public partial class LitBrainsEnemy
{
    public LitBrainsAttackPhase AttackPhase { get; private set; }
    public float AttackCooldownRemaining => Mathf.Max(0, fluidCooldown);
    public float ResistanceDamage => resistanceDamage;
    public bool IsCounterStunned => counterStunPending || counterStunActive && hurtRemaining > 0;
    private bool counterStunPending, counterStunActive;
    private const float CounterStunSeconds = 2f;
    [Tooltip("Effect attached to the character's head while Knocked Out is active.")]
    public GameObject VFX_KnockedOut;
    private GameObject counterStunVfx;
    public LitBrainsAttackProfile ActiveAttack => activeAttack;
    public float ActualSpeed => agent != null && agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0;
    private LitBrainsAttackProfile activeAttack;
    private float fluidCooldown, resistanceDamage, sinceImpact, protectionRemaining;
    private float hurtRemaining, attackElapsed, advanceRemaining, blockedSeconds;
    private float recoveryElapsed;
    private float advanceElapsed, advanceInitial;
    private bool fluidImpactUsed, fluidAttackObserved;
    private Vector3 strikeForward;

    private void ResetFluidCombat()
    {
        if (combatProfile == null) return;
        fluidCooldown = combatProfile.initialAttackDelay;
        resistanceDamage = sinceImpact = protectionRemaining = hurtRemaining = blockedSeconds = 0;
        counterStunPending = counterStunActive = false;
        ClearCounterStunVfx();
        CancelFluidAttack();
    }

    private void TickFluidCombat()
    {
        float dt = Time.deltaTime;
        fluidCooldown = Mathf.Max(0, fluidCooldown - dt);
        protectionRemaining = Mathf.Max(0, protectionRemaining - dt);
        sinceImpact += dt;
        if (sinceImpact >= combatProfile.resistanceRecoveryDelay)
            resistanceDamage = Mathf.Max(0, resistanceDamage - combatProfile.resistanceRecoveryPerSecond * dt);
        bool hurtWasActive = hurtRemaining > 0;
        hurtRemaining = Mathf.Max(0, hurtRemaining - dt);
        StopControlConditions["LitBrainsEnemy.Hurt"] = hurtRemaining > 0 || AttackPhase != LitBrainsAttackPhase.None;
        if (hurtWasActive && hurtRemaining <= 0)
        {
            counterStunActive = false;
            ClearCounterStunVfx();
            anim.ResetTrigger("Hurt");
            anim.CrossFadeInFixedTime("Locomotion", .06f, 0);
        }
        if (hurtRemaining > 0) { StopFluidMovement(); return; }
        if (AttackPhase == LitBrainsAttackPhase.None) return;
        attackElapsed += dt;
        bool inAttack = IsAttackCommitted;
        fluidAttackObserved |= inAttack;
        if ((fluidAttackObserved && !inAttack) || (!fluidAttackObserved && attackElapsed > .35f))
        {
            CancelFluidAttack();
            return;
        }
        StopFluidMovement();
        if (AttackPhase == LitBrainsAttackPhase.Recovery)
        {
            recoveryElapsed += dt;
            if (recoveryElapsed >= activeAttack.recoveryDurationSeconds)
            {
                var handoff = activeAttack.visualHandoff;
                float phase = handoff != null && handoff.validated ? handoff.destinationPhase : 0;
                float blend = handoff != null && handoff.validated ? handoff.blendSeconds : .08f;
                CancelFluidAttack();
                // Fixed blend duration; destination phase is expressed in cycle seconds.
                float cycle = handoff != null && handoff.validated ? handoff.destinationCycleSeconds : 0;
                anim.CrossFadeInFixedTime("Locomotion", blend, 0, phase * cycle);
                return;
            }
        }
        if (AttackPhase == LitBrainsAttackPhase.Preparation)
        {
            TurnToFluidTarget();
            if (fov.CurrentTarget != null && advanceRemaining > 0)
            {
                float gap = Vector3.ProjectOnPlane(fov.CurrentTarget.position - transform.position, Vector3.up).magnitude;
                float distance = Mathf.Min(advanceRemaining, Mathf.Max(0, gap - combatProfile.approachStopDistance),
                    activeAttack.preparationAdvanceDistance * dt / Mathf.Max(.01f, activeAttack.strikeSeconds));
                MoveFluidAdvance(transform.forward, distance);
            }
        }
        else if (AttackPhase == LitBrainsAttackPhase.Strike && advanceRemaining > 0)
        {
            float before = Mathf.Clamp01(advanceElapsed / activeAttack.advanceSeconds);
            advanceElapsed += dt;
            float after = Mathf.Clamp01(advanceElapsed / activeAttack.advanceSeconds);
            // A quick launch easing into the contact; direction is fixed at Strike.
            float distance = Mathf.Min(advanceRemaining, advanceInitial * ((1 - before) * (1 - before) - (1 - after) * (1 - after)));
            MoveFluidAdvance(strikeForward, distance);
        }
        // Safety exit for missing/end events. No stale committed attack survives its clip.
        if (activeAttack?.clip != null && attackElapsed > activeAttack.clip.length + .4f)
        {
            CancelFluidAttack();
            anim.CrossFadeInFixedTime("Locomotion", .08f, 0);
        }
    }

    protected override void Alerted()
    {
        if (combatProfile == null) { base.Alerted(); return; }
        if (fov.CurrentTarget == null) return;
        if (AttackPhase != LitBrainsAttackPhase.None || hurtRemaining > 0) return;
        TurnToFluidTarget();
        // Commit directly from pursuit. Never brake to Idle just to ask whether an attack is ready.
        HandleAttack();
        if (AttackPhase != LitBrainsAttackPhase.None) { StopFluidMovement(); return; }
        float distance = Vector3.ProjectOnPlane(fov.CurrentTarget.position - transform.position, Vector3.up).magnitude;
        canChase = distance > combatProfile.approachStopDistance + .05f;
        if (canChase)
        {
            agent.isStopped = false;
            MoveTo(fov.CurrentTarget.position, runSpeed, combatProfile.approachStopDistance);
            if (!agent.pathPending && (agent.pathStatus != NavMeshPathStatus.PathComplete || agent.velocity.sqrMagnitude < .01f))
                blockedSeconds += Time.deltaTime;
            else blockedSeconds = 0;
            if (blockedSeconds >= 1f)
            {
                agent.ResetPath();
                agent.SetDestination(fov.CurrentTarget.position);
                blockedSeconds = 0;
            }
        }
        else
        {
            StopFluidMovement();
        }
    }

    protected override void HandleAttack()
    {
        if (!HasAttackLineOfSight()) return;
        if (combatProfile == null) { base.HandleAttack(); return; }
        if (!canAttack || fluidCooldown > 0 || AttackPhase != LitBrainsAttackPhase.None || hurtRemaining > 0 ||
            fov.CurrentTarget == null || combatProfile.attacks.Length == 0) return;
        Vector3 delta = Vector3.ProjectOnPlane(fov.CurrentTarget.position - transform.position, Vector3.up);
        if (delta.magnitude > combatProfile.attackStartDistance || Vector3.Angle(transform.forward, delta) > combatProfile.attackFacingDegrees ||
            Physics.Linecast(transform.position + Vector3.up, fov.CurrentTarget.position + Vector3.up,
                LayerMask.GetMask("Default", "Ground", "Obstacle", "CameraObstruction"), QueryTriggerInteraction.Ignore)) return;
        selectedAttack = Random.Range(0, combatProfile.attacks.Length);
        activeAttack = combatProfile.attacks[selectedAttack];
        if (activeAttack == null || activeAttack.clip == null) return;
        ActionSequenceId++;
        if (activeAttack.visualHandoff != null && activeAttack.visualHandoff.validated)
            SetActionRecoveryRate(1);
        attackElapsed = 0;
        fluidImpactUsed = fluidAttackObserved = false;
        AttackPhase = LitBrainsAttackPhase.Preparation;
        advanceRemaining = activeAttack.preparationAdvanceDistance;
        fluidCooldown = Random.Range(combatProfile.attackInterval.x, combatProfile.attackInterval.y);
        anim.SetInteger("Attack ID", selectedAttack + 1);
        anim.SetTrigger("Attack");
    }

    private void TurnToFluidTarget()
    {
        if (fov.CurrentTarget == null) return;
        Vector3 delta = Vector3.ProjectOnPlane(fov.CurrentTarget.position - transform.position, Vector3.up);
        agent.updateRotation = false;
        if (delta.sqrMagnitude > .001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(delta), combatProfile.turnDegreesPerSecond * Time.deltaTime);
    }

    private void StopFluidMovement()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = true;
        if (agent.hasPath) agent.ResetPath();
        agent.velocity = Vector3.zero;
    }

    private void MoveFluidAdvance(Vector3 direction, float distance)
    {
        if (distance <= 0) return;
        Vector3 destination = transform.position + direction * distance;
        Vector3 origin = transform.position + Vector3.up * agent.height * .5f;
        int mask = LayerMask.GetMask("Default", "Ground", "Obstacle", "CameraObstruction");
        bool blocked = Physics.SphereCast(origin, agent.radius, direction, out _, distance + .04f, mask, QueryTriggerInteraction.Ignore);
        if (!blocked && !agent.Raycast(destination, out _)) agent.Move(direction * distance);
        else advanceRemaining = 0;
        advanceRemaining = Mathf.Max(0, advanceRemaining - distance);
    }

    public void HandleBeginBrainsStrike(AnimationEvent source)
    {
        if (!AcceptFluidEvent(source) || AttackPhase != LitBrainsAttackPhase.Preparation) return;
        AttackPhase = LitBrainsAttackPhase.Strike;
        strikeForward = transform.forward;
        advanceRemaining = activeAttack.advanceDistance;
        if (fov.CurrentTarget != null)
            advanceRemaining = Mathf.Min(advanceRemaining, Mathf.Max(0, Vector3.Dot(fov.CurrentTarget.position - transform.position, strikeForward) - combatProfile.approachStopDistance));
        advanceInitial = advanceRemaining;
        advanceElapsed = 0;
    }

    public void HandleBeginBrainsRecovery(AnimationEvent source)
    {
        if (!AcceptFluidEvent(source)) return;
        AttackPhase = LitBrainsAttackPhase.Recovery;
        recoveryElapsed = 0;
        advanceRemaining = 0;
        if (activeAttack.visualHandoff != null && activeAttack.visualHandoff.validated)
            SetActionRecoveryRate(activeAttack.visualHandoff.RecoveryRate(activeAttack.clip, activeAttack.recoveryDurationSeconds));
    }

    private bool AcceptFluidEvent(AnimationEvent source) => combatProfile != null && !dead && !cinematicSuspended &&
        source != null && activeAttack != null && source.animatorStateInfo.shortNameHash == Animator.StringToHash(activeAttack.stateName);

    private bool TryCommitFluidImpact()
    {
        if (AttackPhase != LitBrainsAttackPhase.Strike || fluidImpactUsed) return false;
        fluidImpactUsed = true;
        return true;
    }

    private void CancelFluidAttack()
    {
        AttackPhase = LitBrainsAttackPhase.None;
        activeAttack = null;
        advanceRemaining = 0;
        StopControlConditions["LitBrainsEnemy.Hurt"] = hurtRemaining > 0;
        if (anim != null) anim.ResetTrigger("Attack");
        SetActionRecoveryRate(1);
    }

    private void SetActionRecoveryRate(float rate)
    {
        if (anim == null) return;
        foreach (var parameter in anim.parameters)
            if (parameter.name == "ActionRecoveryPlaybackRate")
            { anim.SetFloat(parameter.nameHash, rate); return; }
    }

    private void OnDisable()
    {
        ResetPerception();
        if (IsCounterStunned) hurtRemaining = 0;
        counterStunPending = counterStunActive = false;
        ClearCounterStunVfx();
        CancelFluidAttack();
        StopFluidMovement();
    }

    public int ReceiveImpact(CombatImpact impact)
    {
        if (dead || impact.Damage <= 0) return 0;
        if (combatProfile == null) return ReceiveDamage(impact.Damage);
        int before = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - impact.Damage);
        sinceImpact = 0;
        if (currentHealth == 0)
        {
            counterStunPending = counterStunActive = false;
            ClearCounterStunVfx();
            CancelFluidAttack();
            dead = true;
            StopAllCoroutines();
            fov.canInvestigate = false;
            fov.SetExternalTarget(null);
            fov.enabled = false;
            anim.ResetTrigger("Hurt");
            BeginDeathPresentation();
            StopFluidMovement();
            agent.enabled = false;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        }
        else if (!IsCounterStunned && (impact.Origin == CombatImpactOrigin.Counter || protectionRemaining <= 0))
        {
            resistanceDamage += impact.Interruption;
            if (impact.Origin == CombatImpactOrigin.Counter || resistanceDamage >= combatProfile.resistance)
            {
                resistanceDamage = 0;
                protectionRemaining = combatProfile.interruptionProtectionSeconds;
                hurtRemaining = cinematicSuspended ? 0 : combatProfile.hurtSeconds;
                CancelFluidAttack();
                StopFluidMovement();
                anim.ResetTrigger("Hurt");
                if (!cinematicSuspended) anim.CrossFadeInFixedTime("Hurt", .04f, 0);
            }
        }
        return before - currentHealth;
    }

    private void BeginCounterStun()
    {
        counterStunPending = false;
        counterStunActive = true;
        hurtRemaining = CounterStunSeconds;
        protectionRemaining = Mathf.Max(protectionRemaining, CounterStunSeconds);
        CancelFluidAttack();
        StopFluidMovement();
        anim.ResetTrigger("Hurt");
        anim.CrossFadeInFixedTime("Knocked Out", .04f, 0);
        CreateCounterStunVfx();
    }

    /// <summary>Timeline signal entry point. Damage alone never enters this state.</summary>
    void ICombatKnockoutReceiver.KnockedOut() => ApplyKnockedOut();

    public void ApplyKnockedOut()
    {
        if (dead || IsCounterStunned) return;
        resistanceDamage = 0;
        counterStunPending = true;
        CancelFluidAttack();
        StopFluidMovement();
        CreateCounterStunVfx();
        // Timeline still owns the Animator until its graph is released.
        // Keep the full vulnerability window for the return to gameplay.
        if (!cinematicSuspended) BeginCounterStun();
    }

    private void CreateCounterStunVfx()
    {
        if (counterStunVfx == null && VFX_KnockedOut != null)
        {
            Transform anchor = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            if (anchor == null) anchor = transform;
            counterStunVfx = Instantiate(VFX_KnockedOut, anchor);
            counterStunVfx.transform.localPosition = Vector3.zero;
            counterStunVfx.transform.localRotation = Quaternion.identity;
        }
    }

    private void ClearCounterStunVfx()
    {
        if (counterStunVfx == null) return;
        counterStunVfx.SetActive(false);
        Destroy(counterStunVfx);
        counterStunVfx = null;
    }

    private void OnDrawGizmos()
    {
        if (combatProfile == null || activeAttack == null) return;
        Gizmos.color = AttackPhase == LitBrainsAttackPhase.Strike ? Color.red : Color.yellow;
        Vector3 center = transform.position + Vector3.up;
        Vector3 left = Quaternion.AngleAxis(-activeAttack.arcDegrees * .5f, Vector3.up) * transform.forward;
        Vector3 right = Quaternion.AngleAxis(activeAttack.arcDegrees * .5f, Vector3.up) * transform.forward;
        Gizmos.DrawLine(center, center + left * activeAttack.reach);
        Gizmos.DrawLine(center, center + right * activeAttack.reach);
        DrawHitArc(center, activeAttack);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        if (combatProfile == null || combatProfile.attacks == null) return;
        Gizmos.color = new Color(1, .4f, .1f, .8f);
        foreach (var attack in combatProfile.attacks)
            if (attack != null) DrawHitArc(transform.position + Vector3.up, attack);
    }

    private void DrawHitArc(Vector3 center, LitBrainsAttackProfile attack)
    {
        Vector3 previous = center + Quaternion.AngleAxis(-attack.arcDegrees * .5f, Vector3.up) * transform.forward * attack.reach;
        Gizmos.DrawLine(center, previous);
        for (int i = 1; i <= 20; i++)
        {
            Vector3 point = center + Quaternion.AngleAxis(Mathf.Lerp(-attack.arcDegrees * .5f, attack.arcDegrees * .5f, i / 20f), Vector3.up) * transform.forward * attack.reach;
            Gizmos.DrawLine(previous, point); previous = point;
        }
        Gizmos.DrawLine(previous, center);
    }
}
