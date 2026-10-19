using UnityEngine;
using UnityEngine.AI;

public partial class LitBrainsEnemy
{
    private bool productionLandingRecovery, productionCapsuleSupported;
    private Vector3 productionLandingOrigin, productionLandingDestination;
    private float productionLandingElapsed, productionLandingDuration;
    private int productionLandingGeneration;

    private void ResetProductionLandingRecovery()
    {
        productionLandingRecovery = false;
        productionCapsuleSupported = false;
        productionLandingElapsed = productionLandingDuration = 0;
    }

    private bool TryBeginProductionLandingRecovery(Vector3 position)
    {
        var settings = RuntimeCharacterData.enemySettings;
        float radius = settings.LocomotionClearanceSearchRadius;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        var world = NavMeshWorldService.Instance;
        if (world != null && !world.IsReady) return false;
        float bestDistance = float.PositiveInfinity;
        Vector3 best = default;
        int count = Mathf.Clamp(settings.LocomotionClearanceSearchSamples, 4, 24);
        for (int ring = 1; ring <= 2; ring++)
        for (int index = 0; index < count; index++)
        {
            float angle = index * Mathf.PI * 2 / count;
            Vector3 expected = position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (radius * ring * .5f);
            if (!NavMesh.SamplePosition(expected, out var nav, settings.PhysicsMaximumGroundSnapDistance, filter) ||
                Vector3.ProjectOnPlane(nav.position - position, Vector3.up).magnitude > radius ||
                Mathf.Abs(nav.position.y - productionLandingOrigin.y) > settings.PhysicsMaximumGroundSnapDistance ||
                position.y - nav.position.y > settings.PhysicsMaximumGroundSnapDistance || nav.position.y > position.y + .15f ||
                world != null && !world.TryValidatePosition(nav.position, agent.areaMask, out _) ||
                !TryProductionGround(nav.position, out var ground)) continue;
            Vector3 feet = ground.point + Vector3.up * settings.PhysicsGroundSkin;
            if (Mathf.Abs(feet.y - nav.position.y) > .15f || !ProductionLandingCapsuleClear(feet)) continue;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(productionLandingOrigin, nav.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            float distance = Vector3.Distance(position, feet);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = feet;
        }
        if (float.IsPositiveInfinity(bestDistance)) return false;
        productionLandingDestination = best;
        productionLandingGeneration = ActionSequenceId;
        productionLandingElapsed = 0;
        productionLandingDuration = bestDistance / Mathf.Max(.1f, walkSpeed) + settings.RecoveryExtraRecoverySeconds;
        productionLandingRecovery = true;
        productionVerticalSpeed = 0;
        productionAdvance = productionRush = false;
        return true;
    }

    private bool ProductionLandingCapsuleClear(Vector3 feet)
    {
        ProductionCapsuleEndpoints(feet, out var top, out var bottom, out float radius);
        foreach (var collider in Physics.OverlapCapsule(top, bottom,
            Mathf.Max(.01f, radius - RuntimeCharacterData.enemySettings.PhysicsGroundSkin),
            productionMotionSkill.EnemyActionMotion.rushBlockingMask, QueryTriggerInteraction.Ignore))
            if (!collider.transform.IsChildOf(transform)) return false;
        return true;
    }

    private void ProductionCapsuleEndpoints(Vector3 feet, out Vector3 top, out Vector3 bottom, out float radius)
    {
        Vector3 center = feet + transform.rotation * Vector3.Scale(productionCapsule.center, transform.lossyScale);
        radius = productionCapsule.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        float half = Mathf.Max(radius, productionCapsule.height * Mathf.Abs(transform.lossyScale.y) * .5f);
        top = center + Vector3.up * (half - radius);
        bottom = center - Vector3.up * (half - radius);
    }

    private Vector3 ClampProductionLandingStep(Vector3 position, Vector3 step, EnemyActionMotionProfile motion, out bool supported)
    {
        supported = false;
        // Keep tangential exit motion independent of the support below the capsule.
        Vector3 planar = ClampProductionMotion(position, Vector3.ProjectOnPlane(step, Vector3.up), motion);
        ProductionCapsuleEndpoints(position + planar, out var top, out var bottom, out float radius);
        Vector3 direction = step.y > 0 ? Vector3.up : Vector3.down;
        float allowed = Mathf.Abs(step.y);
        foreach (var hit in Physics.CapsuleCastAll(top, bottom, radius, direction, allowed + motion.rushCollisionSkin,
            motion.rushBlockingMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || Vector3.Dot(direction, hit.normal) >= -.001f) continue;
            float limit = Mathf.Max(0, hit.distance - motion.rushCollisionSkin);
            if (limit >= allowed) continue;
            allowed = limit;
            // A steep prop can stop this capsule without being a walkable floor.
            // Physical support permits egress, never admission to navigation.
            if (step.y <= 0 && hit.normal.y > .001f)
            {
                supported = true;
                productionVerticalSpeed = 0;
            }
        }
        return planar + direction * allowed;
    }

    private bool TickProductionLandingRecovery(Vector3 position, float dt, EnemyActionMotionProfile motion, out Vector3 step)
    {
        step = Vector3.zero;
        if (productionLandingGeneration != ActionSequenceId)
        {
            ResetProductionLandingRecovery();
            productionMotionActive = false;
            productionMotionSkill = null;
            return false;
        }
        if (dead)
        {
            ResetProductionLandingRecovery();
            productionAirborne = true;
            return true;
        }
        productionLandingElapsed += dt;
        if (productionLandingElapsed > productionLandingDuration)
        {
            FailProductionLandingRecovery("bounded physical egress timed out");
            return false;
        }
        Vector3 remaining = Vector3.ProjectOnPlane(productionLandingDestination - position, Vector3.up);
        step = Vector3.ClampMagnitude(remaining, Mathf.Max(.1f, walkSpeed) * dt);
        productionVerticalSpeed = Mathf.Max(-motion.maximumFallSpeed, productionVerticalSpeed - motion.gravity * dt);
        step.y = productionVerticalSpeed * dt;
        return true;
    }

    private void FailProductionLandingRecovery(string reason)
    {
        ResetProductionLandingRecovery();
        productionMotionActive = productionAirborne = false;
        productionMotionSkill = null;
        productionPattern = null;
        CancelFluidAttack();
        if (!dead && !cinematicSuspended && HasCombatState(CombatLocomotionState))
            anim.CrossFadeInFixedTime(CombatLocomotionState, .08f, 0);
        Debug.LogWarning("[LitBrainsLanding] " + name + " | " + reason + " | position=" + transform.position +
            " destination=" + productionLandingDestination + " | " + DescribeProductionLandingContacts() +
            ". Navigation remains disabled; no warp or fallback impact.", this);
    }

    private string DescribeProductionLandingContacts()
    {
        if (productionCapsule == null) return "capsule absent";
        ProductionCapsuleEndpoints(transform.position, out var top, out var bottom, out float radius);
        string report = "capsule radius=" + radius + " bottom=" + bottom;
        foreach (var collider in Physics.OverlapCapsule(top, bottom, radius + .05f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (collider.transform.IsChildOf(transform)) continue;
            bool overlap = Physics.ComputePenetration(productionCapsule, transform.position, transform.rotation,
                collider, collider.transform.position, collider.transform.rotation, out var separation, out float depth);
            report += " contact=" + collider.name + "/" + collider.GetType().Name +
                " bounds=" + collider.bounds + " overlap=" + overlap + " depth=" + depth + " separation=" + separation;
        }
        return report;
    }
}
