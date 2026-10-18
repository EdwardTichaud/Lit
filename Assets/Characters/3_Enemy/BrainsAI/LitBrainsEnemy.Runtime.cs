using Unity.Netcode;
using UnityEngine;
using Ultrabolt.BrainsAI;

public partial class LitBrainsEnemy : ICombatHealthThresholdActor
{
    public CharacterInfo Health { get; private set; }
    public CharacterData RuntimeCharacterData => Health != null ? Health.CharacterData : characterData;
    private NavMeshWorldService navigationWorld;
    private Collider[] deathColliders;
    private bool perceptionResetPending;
    private bool thresholdFailureRetaliation;
    private System.Action thresholdRetaliationCompleted;
    private bool ThresholdStageBlocksActions => CombatHealthThresholdController.Instance != null &&
        CombatHealthThresholdController.Instance.BlocksEnemyActions(this);
    private bool ThresholdStageSuspendsActions => CombatHealthThresholdController.Instance != null &&
        CombatHealthThresholdController.Instance.ShouldSuspendEnemy(this);
    public bool IsFlameDormant => EncounterIsFlameDormant;

    public bool ForceDefeatFromThreshold()
    {
        if (!HasSimulationAuthority || !isActiveAndEnabled || IsFlameDormant || !EncounterAllowsDamage) return false;
        if (Health == null) ConfigureCharacterData(characterData);
        Health.ForceDefeat();
        return Health.IsDead;
    }

    public bool TryStartThresholdFailureRetaliation(SkillSO skill)
    {
        if (!HasSimulationAuthority || !isActiveAndEnabled || IsDead || cinematicSuspended ||
            skill == null || combatProfile == null || combatProfile.attacks == null ||
            anim == null || !anim.isActiveAndEnabled || !canAttack || chargedAirborne || jumping ||
            ThresholdStageBlocksActions || !EncounterAllowsDecisions || !TryPrepareNavigation()) return false;
        int index = System.Array.FindIndex(combatProfile.attacks, attack => attack != null &&
            attack.skill == skill && attack.clip != null && !string.IsNullOrEmpty(attack.stateName) &&
            anim.HasState(0, Animator.StringToHash(attack.stateName)));
        if (index < 0) return false;
        var target = fov.CurrentTarget;
        if (!IsLivingTarget(target)) target = ResolvePerceptionTarget();
        if (!IsLivingTarget(target)) return false;
        CancelFluidAttack();
        CancelClashRecovery();
        hurtRemaining = 0;
        counterStunPending = counterStunActive = false;
        ClearCounterStunVfx();
        fov.SetExternalTarget(target);
        CommitFluidAttack(index, target, thresholdRetaliation: true);
        thresholdFailureRetaliation = true;
        return true;
    }

    public void CompleteThresholdFailureRetaliation(System.Action onCompleted)
    {
        thresholdFailureRetaliation = false;
        CancelFluidAttack();
        ReleaseChargedHold();
        thresholdRetaliationCompleted += onCompleted;
        TryFinishThresholdRetaliation();
    }

    private void TryFinishThresholdRetaliation()
    {
        if (thresholdRetaliationCompleted == null) return;
        if (isActiveAndEnabled && !IsDead &&
            (cinematicSuspended || chargedAirborne || jumping || !TryPrepareNavigation())) return;
        var completed = thresholdRetaliationCompleted;
        thresholdRetaliationCompleted = null;
        if (isActiveAndEnabled && !IsDead && anim != null && anim.isActiveAndEnabled)
        {
            StopFluidMovement();
            anim.CrossFadeInFixedTime(CombatLocomotionState, .08f, 0);
        }
        completed.Invoke();
    }

    public void ConfigureCharacterData(CharacterData data)
    {
        if (Health == null) Health = GetComponent<CharacterInfo>();
        if (Health == null) Health = gameObject.AddComponent<CharacterInfo>();
        Health.HealthChanged -= OnRuntimeHealthChanged;
        Health.DataChanged -= OnRuntimeDataChanged;
        var resolvedData = data != null ? data : Health.SourceData;
        if (characterData != resolvedData) ResetProductionEngagementDamage();
        characterData = resolvedData;
        if (characterData != null) Health.SetCharacterData(characterData);
        Health.HealthChanged += OnRuntimeHealthChanged;
        Health.DataChanged += OnRuntimeDataChanged;
        OnRuntimeHealthChanged(Health);
    }

    private void OnEnable()
    {
        RefreshEncounterPolicies();
        if (!brains.Contains(this)) brains.Add(this);
        if (Health != null) OnRuntimeHealthChanged(Health);
        BindNavigationWorld();
        nextDetection = 0;
    }

    private void OnDestroy()
    {
        if (Health != null) Health.HealthChanged -= OnRuntimeHealthChanged;
        if (Health != null) Health.DataChanged -= OnRuntimeDataChanged;
        BindNavigationWorld(null);
    }

    private void OnRuntimeDataChanged(CharacterInfo health)
    {
        if (characterData != health.SourceData) ResetProductionEngagementDamage();
        characterData = health.SourceData;
        OnRuntimeHealthChanged(health);
        nextDetection = 0;
    }

    private int ApplyHealthDamage(int amount, out bool thresholdReached, SquadCharacterController source = null)
    {
        thresholdReached = false;
        if (ThresholdStageBlocksActions) return 0;
        if (Health == null || Health.SourceData != characterData) ConfigureCharacterData(characterData);
        var thresholds = CombatHealthThresholdController.Instance;
        int allowedDamage = amount;
        thresholdReached = thresholds != null && thresholds.TryPrepareDamage(this, amount, out allowedDamage, source);
        int applied = Health.ApplyDamage(thresholdReached ? allowedDamage : amount);
        currentHealth = Health.CurrentHp;
        if (thresholdReached) thresholds.NotifyThresholdDamageApplied(this, source);
        return applied;
    }

    private void OnRuntimeHealthChanged(CharacterInfo health)
    {
        currentHealth = health.CurrentHp;
        if (currentHealth <= 0) ResetProductionEngagementDamage();
        if (anim == null || agent == null || fov == null) return;
        if (currentHealth > 0)
        {
            if (!dead) return;
            dead = false;
            productionDeathPoseStarted = false;
            if (deathColliders != null)
                foreach (var collider in deathColliders) if (collider != null) collider.enabled = true;
            deathColliders = null;
            anim.SetBool("Dead", false);
            anim.CrossFadeInFixedTime(CombatLocomotionState, .08f, 0);
            ResetFluidCombat();
            nextDetection = 0;
            return;
        }
        if (dead) return;
        dead = true;
        ReleaseChargedHold();
        CancelClashRecovery();
        counterStunPending = counterStunActive = false;
        ClearCounterStunVfx();
        CancelFluidAttack();
        StopAllCoroutines();
        ResetPerception();
        SuspendNavigation();
        anim.ResetTrigger("Hurt");
        BeginDeathPresentation();
        // Only restore colliders that this death actually disabled.
        var enabledColliders = new System.Collections.Generic.List<Collider>();
        foreach (var collider in GetComponentsInChildren<Collider>())
            if (collider.enabled) { enabledColliders.Add(collider); collider.enabled = false; }
        deathColliders = enabledColliders.ToArray();
    }

    public bool IsEngagedWith(Transform actor)
    {
        return isActiveAndEnabled && !IsDead && EncounterAllowsDecisions && !EncounterIsReturning && !perceptionResetPending && IsLivingTarget(actor) && fov != null &&
            fov.status == FOV.Status.Alerted && fov.CurrentTarget == actor;
    }

    public bool ValidateRuntimeContract(out string report)
    {
        if (GetComponent<EnemyController>() != null || GetComponents<Brain>().Length != 1)
        { report = "multiple enemy brains/controllers"; return false; }
        if (GetComponent<UnityEngine.AI.NavMeshAgent>() == null || GetComponent<Animator>() == null || fov == null)
        { report = "missing navigation, animator or FOV"; return false; }
        if (combatProfile == null)
        { report = "missing authored Brains combat profile; no attacks synthesized"; return false; }
        if (combatProfile.attacks == null || combatProfile.attacks.Length == 0)
        { report = "no authored Brains attacks"; return false; }
        foreach (var attack in combatProfile.attacks)
            if (attack == null || attack.clip == null || attack.skill == null)
            { report = "incomplete authored Brains attack"; return false; }
        report = "LitBrainsEnemy";
        return true;
    }

    private bool CanPerceiveCandidate(Transform candidate)
    {
        if (!IsLivingTarget(candidate) || characterData == null || characterData.vision == null) return false;
        if (UsesProductionPatterns && productionReturning)
            return IsWithinPursuitBounds(candidate) && RuntimeCharacterData.vision.TryEvaluate(transform, candidate,
                ProductionProfile.returnReengageDistance, 360, out _, out _, out _);
        if (fov.CurrentTarget == candidate && UsesProductionPatterns) return IsWithinPursuitBounds(candidate);
        if (fov.CurrentTarget == candidate)
            return Vector3.Distance(transform.position + Vector3.up * characterData.vision.eyeHeight,
                candidate.position + Vector3.up * characterData.vision.targetHeight) <= characterData.vision.maximumDistance;
        return characterData.TryEvaluateVision(transform, candidate, out _, out _, out _);
    }

    private Transform ResolvePerceptionTarget()
    {
        if (detectionTargetOverride != null) return detectionTargetOverride;
        Transform best = null;
        float bestDistance = float.PositiveInfinity;
        var network = NetworkManager.Singleton;
        var actors = SquadCharacterController.ActiveCharacters;
        for (int i = 0; i < actors.Count; i++)
        {
            var actor = actors[i];
            if (actor == null || !actor.isActiveAndEnabled || actor.CharacterData != null && actor.CharacterData.isEnemy) continue;
            if (network != null && network.IsListening)
            {
                bool assigned = false;
                foreach (var client in network.ConnectedClientsList)
                    if (NetcodePlayerUtils.GetPlayerTransform(client.ClientId) == actor.transform) { assigned = true; break; }
                if (!assigned) continue;
            }
            if (!CanPerceiveCandidate(actor.transform)) continue;
            if (fov.CurrentTarget == actor.transform) return actor.transform;
            float distance = (actor.transform.position - transform.position).sqrMagnitude;
            if (distance < bestDistance || distance == bestDistance && best != null && string.CompareOrdinal(actor.GetEntityId().ToString(), best.GetEntityId().ToString()) < 0)
            { best = actor.transform; bestDistance = distance; }
        }
        // Standalone fixtures may provide only a local transform, without a squad actor.
        if (best == null && (network == null || !network.IsListening)) best = LocalPlayerContext.LocalCharacterRoot;
        return best;
    }

    private void BindNavigationWorld() => BindNavigationWorld(NavMeshWorldService.Instance);

    private void BindNavigationWorld(NavMeshWorldService world)
    {
        if (navigationWorld == world) return;
        if (navigationWorld != null) navigationWorld.StateChanged -= OnNavigationWorldStateChanged;
        navigationWorld = world;
        if (navigationWorld != null)
        {
            navigationWorld.StateChanged += OnNavigationWorldStateChanged;
            if (!navigationWorld.IsReady) OnNavigationWorldStateChanged(navigationWorld.State);
        }
    }

    private void OnNavigationWorldStateChanged(NavMeshWorldState state)
    {
        if (state == NavMeshWorldState.Ready) { nextDetection = 0; return; }
        ResetPerception();
        CancelFluidAttack();
        StopAllCoroutines();
        jumping = false;
        SuspendNavigation();
    }

    private void SuspendNavigation()
    {
        if (agent != null) agent.enabled = false;
        if (fov != null) fov.enabled = false;
        navigationReady = false;
    }
}
