using UnityEngine;

public partial class LitBrainsEnemy
{
    private float requestedNavigationSpeed, lastAppliedNavigationSpeed = -1;
    public new void MoveTo(Vector3 position, float speed = 1, float stoppingDistance = 0)
    {
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
