using System.Collections;
using UnityEngine;

public sealed partial class PlayerActionPresentationController
{
    [SerializeField] private bool debugContactMotion;
    public bool DebugContactMotion => debugContactMotion;
    private SkillSO contactSkill;
    private int contactGeneration;
    private bool contactWindowArmed, contactImpactUsed;
    private Vector3 contactTravelDirection;
    private LitOpsiveLocomotionBridge.TargetCollisionHandle contactCollisionHandle;
    private RuntimeAnimatorController contactPreviousController;
    private AnimatorOverrideController contactAnimationOverride;
    public bool IsTargetLungeActive => targetLungeOwnsPlanarMotion;

    private bool PrepareContactSkillAnimation(SkillSO skill, out AnimatorOverrideController replacement, out RuntimeAnimatorController original)
    {
        replacement = null; original = null;
        bool justice = skill != null && skill.IsProtectionSkill && skill.protection.kind == SkillProtectionKind.JusteRetour;
        if (skill == null || !skill.TargetLunge.resolveImpactAtContact && !justice) return true;
        var slot = justice ? skill.protection.animationSlot : skill.TargetLunge.contactAnimationSlot;
        if (skill.AnimationClip == null || slot == null) return false;
        original = contactAnimationOverride != null && animator.runtimeAnimatorController == contactAnimationOverride
            ? contactPreviousController : animator.runtimeAnimatorController;
        replacement = new AnimatorOverrideController(original);
        var mappings = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
        replacement.GetOverrides(mappings);
        bool found = false;
        for (int i = 0; i < mappings.Count; i++)
            if (mappings[i].Key == slot)
            {
                mappings[i] = new System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>(mappings[i].Key, skill.AnimationClip);
                found = true;
            }
        if (!found) { Destroy(replacement); replacement = null; return false; }
        replacement.ApplyOverrides(mappings); return true;
    }
    private void ReleaseContactSkillAnimation()
    {
        if (contactAnimationOverride == null) return;
        if (animator != null && animator.runtimeAnimatorController == contactAnimationOverride)
            animator.runtimeAnimatorController = contactPreviousController;
        Destroy(contactAnimationOverride);
        contactAnimationOverride = null; contactPreviousController = null;
    }

    public void HandleOpenSkillContactWindow()
    {
        if (debugContactMotion) Debug.Log($"[TargetMobility] Arm | skill={contactSkill?.SkillName} generation={contactGeneration}/{activeToken} active={actionActive}", this);
        if (contactSkill != null && contactGeneration == activeToken && actionActive) contactWindowArmed = true;
    }
    public static float ContactApproachDuration(float distance, float speed, float authoredDuration) =>
        Mathf.Max(authoredDuration, Mathf.Max(0, distance) / Mathf.Max(.1f, speed)) + .12f;

    private IEnumerator RunContactTargetLunge(SkillSO skill, Transform targetRoot, int lungeToken, int generation)
    {
        if (!locomotionBridge.BeginScriptedPlanarMotion(this)) yield break;
        targetLungeOwnsPlanarMotion = true;
        var profile = skill.TargetLunge;
        var clock = GetComponent<CombatTimeDomain>();
        Vector3 launchOrigin = transform.position;
        Vector3 targetOrigin = targetRoot != null ? targetRoot.position : launchOrigin;
        LitOpsiveLocomotionBridge.TargetCollisionHandle collisionScope = null;
        try
        {
            float wait = 0;
            while (!contactWindowArmed && IsCurrentSession(generation) && lungeToken == targetLungeToken && wait < .35f)
            {
                yield return new WaitForFixedUpdate(); wait += clock != null ? clock.FixedDeltaTime : Time.fixedDeltaTime;
            }
            if (!contactWindowArmed || targetRoot == null || !IsCurrentSession(generation))
            {
                if (debugContactMotion) Debug.LogWarning($"[TargetMobility] Motion rejected | armed={contactWindowArmed} session={IsCurrentSession(generation)} wait={wait}", this);
                yield break;
            }
            Vector3 start = transform.position;
            contactTravelDirection = Vector3.ProjectOnPlane(targetOrigin - launchOrigin, Vector3.up).normalized;
            if (contactTravelDirection.sqrMagnitude < .0001f) contactTravelDirection = transform.forward;
            float targetDistance = Vector3.ProjectOnPlane(targetRoot.position - start, Vector3.up).magnitude;
            Vector3 launchDestination = targetOrigin + contactTravelDirection * profile.passThroughDistance;
            float requested = profile.passThroughTarget ? Mathf.Max(0, Vector3.Dot(launchDestination - start, contactTravelDirection)) : Mathf.Max(0, targetDistance - profile.stoppingDistance);
            float clear = ClearContactRoute(targetRoot, contactTravelDirection, requested, profile.passThroughTarget);
            bool traversing = profile.passThroughTarget && clear >= TargetExitDistance(targetRoot, start, contactTravelDirection);
            if (profile.passThroughTarget && !traversing)
                clear = ClearContactRoute(targetRoot, contactTravelDirection, Mathf.Max(0, targetDistance - profile.stoppingDistance), false);
            Vector3 destination = start + contactTravelDirection * clear;
            if (debugContactMotion) Debug.Log($"[TargetMobility] Route | start={start} destination={destination} clear={clear} requested={requested} traversing={traversing} colliders={locomotionBridge.ScriptedMotionColliders.Length}", this);
            if (traversing && locomotionBridge.TryAcquireTargetCollisionBypass(this, targetRoot, out collisionScope))
                contactCollisionHandle = collisionScope;
            else if (traversing) yield break;

            float elapsed = 0;
            float duration = ContactApproachDuration(clear, profile.maximumApproachSpeed, profile.approachDurationSeconds);
            float nextTrace = 0;
            Vector3 previous = transform.position;
            while (IsCurrentSession(generation) && lungeToken == targetLungeToken && targetRoot != null &&
                RealTimeCombatManager.Instance?.LockedCombatTarget?.Root == targetRoot && elapsed < duration)
            {
                Vector3 current = transform.position;
                ResolveTravelledContact(skill, targetRoot, previous, current, generation);
                if (!IsCurrentSession(generation)) yield break;
                previous = current;
                float scale = clock != null ? clock.Scale : 1f;
                if (scale <= 0) { yield return new WaitForFixedUpdate(); continue; }
                Vector3 direction = traversing ? contactTravelDirection : Vector3.ProjectOnPlane(targetRoot.position - current, Vector3.up).normalized;
                if (!traversing) contactTravelDirection = direction;
                float remaining = traversing ? Vector3.Dot(destination - current, direction) :
                    Mathf.Max(0, Vector3.ProjectOnPlane(targetRoot.position - current, Vector3.up).magnitude - profile.stoppingDistance);
                if (remaining <= .04f) break;
                float stepTime = Mathf.Max(.001f, Time.fixedDeltaTime);
                float speed = Mathf.Min(profile.maximumApproachSpeed, remaining / Mathf.Max(stepTime, duration - elapsed - .1f));
                float safeStep = ClearContactRoute(targetRoot, direction, Mathf.Min(remaining, speed * stepTime * scale), traversing);
                if (debugContactMotion && elapsed >= nextTrace)
                {
                    nextTrace += .1f;
                    var targetCollider = targetRoot.GetComponentInChildren<Collider>();
                    bool ignored = targetCollider != null && locomotionBridge.ScriptedMotionColliders.Length > 0 && Physics.GetIgnoreCollision(locomotionBridge.ScriptedMotionColliders[0], targetCollider);
                    Debug.Log($"[TargetMobility] Step | elapsed={elapsed:F3} pos={current} velocity={locomotionBridge.Velocity} remaining={remaining:F3} speed={speed:F2} safe={safeStep:F3} scope={locomotionBridge.TargetCollisionBypassCount} pairIgnored={ignored}", this);
                }
                locomotionBridge.SetActionFacingDirection(direction);
                if (!locomotionBridge.DriveScriptedPlanarMotion(this, direction * (safeStep / stepTime))) break;
                yield return new WaitForFixedUpdate(); elapsed += clock != null ? clock.FixedDeltaTime : Time.fixedDeltaTime;
                if (safeStep <= .002f)
                {
                    if (debugContactMotion) Debug.Log($"[TargetMobility] Blocked | position={transform.position} elapsed={elapsed}", this);
                    break;
                }
            }
            if (IsCurrentSession(generation) && targetRoot != null)
                ResolveTravelledContact(skill, targetRoot, previous, transform.position, generation);
        }
        finally
        {
            collisionScope?.Dispose();
            if (ReferenceEquals(contactCollisionHandle, collisionScope)) contactCollisionHandle = null;
            if (lungeToken == targetLungeToken && contactGeneration == generation)
            {
                locomotionBridge.DriveScriptedPlanarMotion(this, Vector3.zero);
                locomotionBridge.EndScriptedPlanarMotion(this);
                targetLungeOwnsPlanarMotion = false;
                targetLungeRoutine = null;
            }
        }
    }

    private void ResolveTravelledContact(SkillSO skill, Transform targetRoot, Vector3 previous, Vector3 current, int generation)
    {
        if (contactImpactUsed || !contactWindowArmed || generation != activeToken || !actionActive) return;
        Vector3 travel = Vector3.ProjectOnPlane(current - previous, Vector3.up);
        float distance = travel.magnitude;
        Vector3 direction = distance > .0001f ? travel / distance : contactTravelDirection;
        foreach (var body in locomotionBridge.ScriptedMotionColliders)
        {
            if (!(body is CapsuleCollider capsule) || !body.enabled) continue;
            GetCapsuleWorldPoints(capsule, out var a, out var b, out float radius);
            Vector3 offset = previous - current;
            bool touching = false; float contactDistance = distance;
            foreach (var collider in Physics.OverlapCapsule(a, b, radius + .12f, ~0, QueryTriggerInteraction.Ignore))
                touching |= collider.transform.IsChildOf(targetRoot);
            if (distance > .0001f)
                foreach (var hit in Physics.CapsuleCastAll(a + offset, b + offset, radius + .03f, direction, distance, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.collider.transform.IsChildOf(targetRoot)) { touching = true; contactDistance = Mathf.Min(contactDistance, hit.distance); }
            if (!touching) continue;
            contactImpactUsed = true;
            if (debugContactMotion) Debug.Log($"[TargetMobility] Contact | position={previous + direction * contactDistance} target={targetRoot.position}", this);
            var manager = RealTimeCombatManager.Instance;
            int applied = manager != null ? manager.ApplySkillDamageAtContact(skill, targetRoot,
                previous + direction * contactDistance, contactTravelDirection) : 0;
            if (applied > 0) HandleConfirmedExternalImpact(skill, manager.LockedTargetPoint ?? targetRoot);
            return;
        }
    }

    private float TargetExitDistance(Transform target, Vector3 start, Vector3 direction)
    {
        float exit = Vector3.Dot(target.position - start, direction);
        foreach (var collider in target.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger) continue;
            var bounds = collider.bounds;
            float support = Mathf.Abs(direction.x) * bounds.extents.x + Mathf.Abs(direction.z) * bounds.extents.z;
            exit = Mathf.Max(exit, Vector3.Dot(bounds.center - start, direction) + support);
        }
        float radius = .4f;
        foreach (var collider in locomotionBridge.ScriptedMotionColliders)
            if (collider is CapsuleCollider capsule) radius = Mathf.Max(radius, capsule.radius * Mathf.Abs(capsule.transform.lossyScale.x));
        return exit + radius + .1f;
    }

    public float GetSafePlanarTravel(Vector3 direction, float requested) => ClearContactRoute(null, direction.normalized, requested, false);

    private float ClearContactRoute(Transform target, Vector3 direction, float requested, bool ignoreTarget)
    {
        float clear = BattleWallContainment.LimitPlanarTravel(transform, direction, requested);
        float groundMargin = .35f;
        foreach (var body in locomotionBridge.ScriptedMotionColliders)
        {
            if (!(body is CapsuleCollider capsule) || !body.enabled) continue;
            GetCapsuleWorldPoints(capsule, out var a, out var b, out float radius);
            groundMargin = Mathf.Max(groundMargin, radius);
            foreach (var hit in Physics.CapsuleCastAll(a, b, radius, direction, requested, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || ignoreTarget && target != null && hit.collider.transform.IsChildOf(target) || hit.normal.y >= .45f) continue;
                // A capsule touching the floor may return a zero-distance cast with a fake horizontal normal.
                if (hit.distance <= .001f && hit.collider.Raycast(new Ray(transform.position + Vector3.up * .45f, Vector3.down), out var floor, 1f) &&
                    floor.normal.y >= .45f && floor.point.y <= transform.position.y + .2f) continue;
                if (debugContactMotion) Debug.Log($"[TargetMobility] Obstacle | name={hit.collider.name} distance={hit.distance} normal={hit.normal} target={target?.name}", this);
                clear = Mathf.Min(clear, Mathf.Max(0, hit.distance - .08f));
            }
        }
        float safe = 0, height = transform.position.y;
        for (float distance = Mathf.Min(.2f, clear); distance > 0; distance = Mathf.Min(clear, distance + .2f))
        {
            Vector3 point = transform.position + direction * (distance + groundMargin); point.y = height;
            bool found = false; float nearest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(point + Vector3.up * .45f, Vector3.down, 1f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || target != null && hit.collider.transform.IsChildOf(target) || hit.normal.y < .45f ||
                    hit.collider.GetComponentInParent<SquadCharacterController>() != null ||
                    hit.collider.GetComponentInParent<LitBrainsEnemy>() != null || hit.collider.GetComponentInParent<EnemyController>() != null || hit.distance >= nearest) continue;
                found = true; nearest = hit.distance; height = hit.point.y;
            }
            if (!found)
            {
                if (debugContactMotion) Debug.Log($"[TargetMobility] No ground | probe={point} safe={safe}", this);
                return safe;
            }
            safe = distance;
            if (distance >= clear) break;
        }
        return safe;
    }
}
