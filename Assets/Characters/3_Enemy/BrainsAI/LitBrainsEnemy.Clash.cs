using UnityEngine;

public partial class LitBrainsEnemy
{
    private float clashRemaining, clashMotionRemaining, clashDistance, clashDuration;
    private Vector3 clashDirection;
    public bool IsClashRecovering => clashRemaining > 0;
    public bool IsClashGrounded
    {
        get
        {
            if (chargedAirborne || cinematicSuspended || dead) return false;
            if (ProductionMotionActive) return CanLaunchGroundedProductionAction();
            if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.isOnOffMeshLink) return false;
            // Package rays start exactly at the feet and can miss a coplanar collider.
            foreach (var hit in Physics.RaycastAll(transform.position + Vector3.up * .3f,
                Vector3.down, .65f, LayerMask.GetMask("Default", "Ground"), QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform) && hit.normal.y >= .5f) return true;
            return false;
        }
    }
    public bool AcceptClashEvent(AnimationEvent source) => AcceptFluidEvent(source);

    public bool BeginClashRecovery(Vector3 direction, float distance, float duration, float recovery)
    {
        if (ProductionMotionActive)
        {
            if (!HasSimulationAuthority || !EncounterAllowsDecisions || !IsClashGrounded || agent == null) return false;
            var filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!UnityEngine.AI.NavMesh.SamplePosition(transform.position, out var local, .15f, filter) ||
                Vector3.Distance(local.position, transform.position) > .15f) return false;
            // Grounded action hands back to the same validated navigation adapter; never warp.
            productionMotionActive = productionAirborne = productionAdvance = productionRush = productionEndRequested = productionAbort = false;
            productionMotionSkill = null;
            productionPattern = null;
            CancelFluidAttack();
            if (!TryPrepareNavigation()) return false;
        }
        if (!HasSimulationAuthority || !EncounterAllowsDecisions || dead || cinematicSuspended || !navigationReady || !agent.enabled || !agent.isOnNavMesh) return false;
        ActionSequenceId++;
        CancelFluidAttack();
        clashDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        clashDistance = distance;
        clashDuration = Mathf.Max(.01f, duration);
        clashMotionRemaining = clashDuration;
        clashRemaining = clashDuration + recovery;
        StopControlConditions["LitBrainsEnemy.Clash"] = true;
        StopFluidMovement();
        PlayCombatHurt();
        return true;
    }

    private bool TickClashRecovery()
    {
        if (clashRemaining <= 0) return false;
        var clock = GetComponent<CombatTimeDomain>();
        float dt = clock != null ? clock.DeltaTime : Time.deltaTime;
        StopFluidMovement();
        float before = clashMotionRemaining / clashDuration;
        clashMotionRemaining = Mathf.Max(0, clashMotionRemaining - dt);
        float after = clashMotionRemaining / clashDuration;
        MoveFluidAdvance(clashDirection, clashDistance * (before * before - after * after));
        clashRemaining = Mathf.Max(0, clashRemaining - dt);
        if (clashRemaining <= 0) CancelClashRecovery();
        return true;
    }

    public void CancelClashRecovery()
    {
        bool wasActive = clashRemaining > 0 || StopControlConditions.ContainsKey("LitBrainsEnemy.Clash") && StopControlConditions["LitBrainsEnemy.Clash"];
        clashRemaining = clashMotionRemaining = 0;
        StopControlConditions["LitBrainsEnemy.Clash"] = false;
        if (wasActive && !dead && !cinematicSuspended) anim.CrossFadeInFixedTime("Locomotion", .08f, 0);
    }
}
