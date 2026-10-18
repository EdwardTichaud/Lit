using System.Collections;
using UnityEngine;

public partial class LitBrainsEnemy
{
    private Coroutine groundImpactPush;
    public void StartGroundImpactPush(Vector3 direction, float distance)
    {
        if (!HasSimulationAuthority || !EncounterAllowsDamage || dead || cinematicSuspended || !navigationReady || agent == null || !agent.enabled || !agent.isOnNavMesh || agent.isOnOffMeshLink) return;
        if (groundImpactPush != null) StopCoroutine(groundImpactPush);
        groundImpactPush = StartCoroutine(GroundImpactPush(Vector3.ProjectOnPlane(direction, Vector3.up).normalized, distance));
    }
    private IEnumerator GroundImpactPush(Vector3 direction, float distance)
    {
        yield return null;
        float elapsed = 0;
        while (elapsed < .25f && HasSimulationAuthority && !IsFlameDormant && !dead && !cinematicSuspended && agent.enabled && agent.isOnNavMesh && !agent.isOnOffMeshLink)
        {
            float dt = Mathf.Min(Time.deltaTime, .25f - elapsed);
            float before = 1 - elapsed / .25f;
            float after = 1 - (elapsed + dt) / .25f;
            var step = direction * distance * (before * before - after * after);
            int mask = LayerMask.GetMask("Default", "Ground", "Obstacle", "CameraObstruction");
            if (agent.Raycast(transform.position + step, out _) || Physics.SphereCast(transform.position + Vector3.up * agent.height * .5f,
                agent.radius, direction, out _, step.magnitude + .04f, mask, QueryTriggerInteraction.Ignore)) break;
            agent.Move(step); elapsed += dt; yield return null;
        }
        groundImpactPush = null;
    }
}
