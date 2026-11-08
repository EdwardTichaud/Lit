using UnityEngine;
using UnityEngine.AI;

public partial class LitBrainsEnemy
{
    private float requestedNavigationSpeed, lastAppliedNavigationSpeed = -1;
    public new void MoveTo(Vector3 position, float speed = 1, float stoppingDistance = 0)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        if (encounterPolicies == null) RefreshEncounterPolicies();
        bool constrained = false;
        foreach (var component in encounterPolicies)
            if (component != null && component.isActiveAndEnabled && component is ILitBrainsMovementPolicy) { constrained = true; break; }
        if (constrained)
        {
            bool clipped = false;
            var path = new NavMeshPath();
            if (!agent.CalculatePath(position, path) || path.corners.Length < 2) { StopFluidMovement(); return; }
            var corners = path.corners;
            Vector3 from = transform.position;
            foreach (var corner in corners)
            {
                Vector3 step = corner - from;
                Vector3 allowed = ConstrainEncounterStep(from, step);
                position = from + allowed;
                if ((allowed - step).sqrMagnitude > .000001f) { clipped = true; break; }
                from = corner;
            }
            // Recalculation to the clipped endpoint must not find a different
            // shortcut outside the light (for example around a wall).
            if (!agent.CalculatePath(position, path) || path.corners.Length < 2) { StopFluidMovement(); return; }
            from = transform.position;
            foreach (var corner in path.corners)
            {
                Vector3 step = corner - from;
                if ((ConstrainEncounterStep(from, step) - step).sqrMagnitude > .000001f) { StopFluidMovement(); return; }
                from = corner;
            }
            if (clipped) stoppingDistance = Mathf.Min(stoppingDistance, .05f);
        }
        requestedNavigationSpeed = speed;
        base.MoveTo(position, speed * EnemyEntraveZone.NavigationMultiplier(transform.position), stoppingDistance);
        lastAppliedNavigationSpeed = agent.speed;
    }
    private void ApplyNavigationControl()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.isStopped || IsAttackCommitted) return;
        if (!Mathf.Approximately(agent.speed, lastAppliedNavigationSpeed)) requestedNavigationSpeed = agent.speed;
        lastAppliedNavigationSpeed = requestedNavigationSpeed * EnemyEntraveZone.NavigationMultiplier(transform.position);
        agent.speed = lastAppliedNavigationSpeed;
    }
}
