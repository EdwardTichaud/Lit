using UnityEngine;
using UnityEngine.AI;

public partial class LitBrainsEnemy
{
    private float authoredAgentBaseOffset;
    private void UpdateNavigationGrounding()
    {
        bool profileGrounding = presentationProfile != null && presentationProfile.IsUsable(anim.avatar) && presentationProfile.alignNavMeshGround;
        if ((!reconcileNavigationGroundHeight && !profileGrounding) || cinematicSuspended || !agent.enabled || !agent.isOnNavMesh) return;
        float tolerance = profileGrounding ? presentationProfile.maximumNavMeshGroundCorrection : .25f;
        var filter = new NavMeshQueryFilter {agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
        if (!NavMesh.SamplePosition(transform.position,out var surface,tolerance + .05f,filter) ||
            Vector3.ProjectOnPlane(surface.position - transform.position,Vector3.up).sqrMagnitude > .0025f) return;
        float nearest = float.PositiveInfinity; bool found = false; float ground = 0;
        int mask = LayerMask.GetMask("Ground","Default");
        foreach (var hit in Physics.RaycastAll(surface.position + Vector3.up * tolerance,Vector3.down,tolerance*2,mask,QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.normal.y < .5f || hit.distance >= nearest) continue;
            ground = hit.point.y; nearest = hit.distance; found = true;
        }
        if (!found) return; // Never snap to a missing or unrelated lower floor.
        float correction = ground - surface.position.y;
        if (Mathf.Abs(correction) > tolerance) return;
        float desired = authoredAgentBaseOffset + correction;
        if (Mathf.Abs(agent.baseOffset-desired) > .0001f) agent.baseOffset = desired;
    }
}
