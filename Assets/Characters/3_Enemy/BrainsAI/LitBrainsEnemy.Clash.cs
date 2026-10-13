using UnityEngine;

public partial class LitBrainsEnemy
{
    private float clashRemaining, clashMotionRemaining, clashDistance, clashDuration;
    private Vector3 clashDirection;
    public bool IsClashGrounded
    {
        get
        {
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
        if (dead || cinematicSuspended || !navigationReady || !agent.enabled || !agent.isOnNavMesh) return false;
        CancelFluidAttack();
        clashDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        clashDistance = distance;
        clashDuration = Mathf.Max(.01f, duration);
        clashMotionRemaining = clashDuration;
        clashRemaining = clashDuration + recovery;
        StopControlConditions["LitBrainsEnemy.Clash"] = true;
        StopFluidMovement();
        anim.CrossFadeInFixedTime("Hurt", .08f, 0);
        return true;
    }

    private bool TickClashRecovery()
    {
        if (clashRemaining <= 0) return false;
        float dt = Time.deltaTime;
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
