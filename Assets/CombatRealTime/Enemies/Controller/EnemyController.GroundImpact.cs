using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public sealed partial class EnemyController
{
    private Vector3 groundImpactActionDirection;
    private float groundImpactActionRemaining, groundImpactActionDistance;
    public void StartGroundImpactPush(Vector3 direction, float distance)
    {
        direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        if (distance <= 0 || Health == null || Health.IsDead || State == CombatEnemyPhysicsState.Cinematic) return;
        if (State == CombatEnemyPhysicsState.Navigation) StartCoroutine(GroundImpactPush(direction, distance));
        else
        {
            groundImpactActionDirection = direction;
            groundImpactActionRemaining = .25f;
            groundImpactActionDistance = distance;
        }
    }
    private Vector3 GroundImpactActionDelta(Vector3 position)
    {
        if (groundImpactActionRemaining <= 0) return Vector3.zero;
        float dt = Mathf.Min(PhysicsLocalFixedDeltaTime, groundImpactActionRemaining);
        Vector3 delta = groundImpactActionDirection * (groundImpactActionDistance / .25f * dt);
        groundImpactActionRemaining -= dt;
        if (!PhysicsTryGetGroundY(position + delta, out _))
        { groundImpactActionRemaining = 0; return Vector3.zero; }
        return delta;
    }
    private IEnumerator GroundImpactPush(Vector3 direction, float distance)
    {
        var agent = GetComponent<NavMeshAgent>();
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.isOnOffMeshLink) yield break;
        float elapsed = 0;
        while (elapsed < .25f && !Health.IsDead && State == CombatEnemyPhysicsState.Navigation && agent.enabled && agent.isOnNavMesh && !agent.isOnOffMeshLink)
        {
            float dt = Mathf.Min(Time.deltaTime, .25f - elapsed);
            var step = direction * (distance / .25f * dt);
            if (agent.Raycast(transform.position + step, out _) || Physics.Raycast(transform.position + Vector3.up,
                direction, step.magnitude + .4f, PhysicsGroundMask, QueryTriggerInteraction.Ignore)) yield break;
            agent.Move(step);
            elapsed += dt; yield return null;
        }
    }
}
