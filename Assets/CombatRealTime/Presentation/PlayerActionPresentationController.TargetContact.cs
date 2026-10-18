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
    private System.Func<Transform, Vector3, Vector3, bool> externalContactResolver;
    private float externalFreeDashDistance;

    public bool TryPrepareExternalContactSkill(int generation, SkillSO skill)
    {
        if (!externallyDriven || !IsCurrentSession(generation) || contactAnimationOverride != null || skill == null || !skill.TargetLunge.resolveImpactAtContact ||
            !PrepareContactSkillAnimation(skill, out var replacement, out var original)) return false;
        contactPreviousController = original;
        contactAnimationOverride = replacement;
        animator.runtimeAnimatorController = replacement;
        animator.Update(0f);
        return true;
    }

    public bool TryBeginExternalContactLunge(int generation, SkillSO skill,
        System.Func<Transform, Vector3, Vector3, bool> resolveContact, float freeDashDistance)
    {
        if (!externallyDriven || !IsCurrentSession(generation) || AttackContext?.Skill != skill ||
            resolveContact == null || float.IsNaN(freeDashDistance) || float.IsInfinity(freeDashDistance) || freeDashDistance <= 0) return false;
        BeginTargetLunge(skill, AttackContext.TargetRoot);
        if (!targetLungeOwnsPlanarMotion) return false;
        externalContactResolver = resolveContact;
        externalFreeDashDistance = freeDashDistance;
        return true;
    }

    private float ResolveFreeContactDistance() => externalContactResolver != null ? externalFreeDashDistance :
        RealTimeCombatManager.Instance != null ? RealTimeCombatManager.Instance.FreeDashDistance : 0;

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
        var context = AttackContext;
        bool freeDash = context != null && context.Target == null;
        Vector3 launchOrigin = transform.position;
        Vector3 targetOrigin = context != null ? context.TargetOrigin : targetRoot != null ? targetRoot.position : launchOrigin;
        LitOpsiveLocomotionBridge.TargetCollisionHandle collisionScope = null;
        try
        {
            float wait = 0;
            while (!contactWindowArmed && IsCurrentSession(generation) && lungeToken == targetLungeToken && wait < .35f)
            {
                yield return new WaitForFixedUpdate(); wait += clock != null ? clock.FixedDeltaTime : Time.fixedDeltaTime;
            }
            if (!contactWindowArmed || !freeDash && targetRoot == null || !IsCurrentSession(generation))
            {
                if (debugContactMotion) Debug.LogWarning($"[TargetMobility] Motion rejected | armed={contactWindowArmed} session={IsCurrentSession(generation)} wait={wait}", this);
                yield break;
            }
            Vector3 start = transform.position;
            contactTravelDirection = context != null ? context.Direction : Vector3.ProjectOnPlane(targetOrigin - launchOrigin, Vector3.up).normalized;
            if (contactTravelDirection.sqrMagnitude < .0001f) contactTravelDirection = transform.forward;
            float targetDistance = freeDash ? ResolveFreeContactDistance() : Vector3.ProjectOnPlane(targetOrigin - start, Vector3.up).magnitude;
            Vector3 launchDestination = targetOrigin + contactTravelDirection * profile.passThroughDistance;
            float requested = freeDash ? targetDistance : profile.passThroughTarget ? Mathf.Max(0, Vector3.Dot(launchDestination - start, contactTravelDirection)) : Mathf.Max(0, targetDistance - profile.stoppingDistance);
            float clear = ClearContactRoute(targetRoot, contactTravelDirection, requested, profile.passThroughTarget && !freeDash);
            bool traversing = !freeDash && profile.passThroughTarget && clear >= TargetExitDistance(targetRoot, start, contactTravelDirection);
            if (!freeDash && profile.passThroughTarget && !traversing)
                clear = ClearContactRoute(targetRoot, contactTravelDirection, Mathf.Max(0, targetDistance - profile.stoppingDistance), false);
            Vector3 destination = start + contactTravelDirection * clear;
            if (!FiniteContact(destination) || !FiniteContact(clear)) yield break;
            if (debugContactMotion) Debug.Log($"[TargetMobility] Route | start={start} destination={destination} clear={clear} requested={requested} traversing={traversing} colliders={locomotionBridge.ScriptedMotionColliders.Length}", this);
            if (traversing && locomotionBridge.TryAcquireTargetCollisionBypass(this, targetRoot, out collisionScope))
                contactCollisionHandle = collisionScope;
            else if (traversing) yield break;

            float elapsed = 0;
            float duration = ContactApproachDuration(clear, profile.maximumApproachSpeed, profile.approachDurationSeconds);
            if (!FiniteContact(duration) || duration <= 0) yield break;
            float nextTrace = 0;
            Vector3 previous = transform.position;
            while (IsCurrentSession(generation) && lungeToken == targetLungeToken &&
                (context != null ? freeDash || targetRoot != null : targetRoot != null && RealTimeCombatManager.Instance?.LockedCombatTarget?.Root == targetRoot) && elapsed < duration)
            {
                Vector3 current = transform.position;
                if (!FiniteContact(current)) yield break;
                ResolveTravelledContact(skill, targetRoot, previous, current, generation);
                if (!IsCurrentSession(generation)) yield break;
                previous = current;
                float scale = clock != null ? clock.Scale : 1f;
                if (!FiniteContact(scale)) yield break;
                if (scale <= 0) { yield return new WaitForFixedUpdate(); continue; }
                Vector3 direction = context != null || traversing ? contactTravelDirection : Vector3.ProjectOnPlane(targetRoot.position - current, Vector3.up).normalized;
                if (context == null && !traversing) contactTravelDirection = direction;
                float remaining = context != null || traversing ? Vector3.Dot(destination - current, direction) :
                    Mathf.Max(0, Vector3.ProjectOnPlane(targetRoot.position - current, Vector3.up).magnitude - profile.stoppingDistance);
                if (!FiniteContact(remaining) || !FiniteContact(direction)) yield break;
                if (remaining <= .04f) break;
                float stepTime = Mathf.Max(.001f, Time.fixedDeltaTime);
                if (!FiniteContact(stepTime)) yield break;
                float speed = Mathf.Min(profile.maximumApproachSpeed, remaining / Mathf.Max(stepTime, duration - elapsed - .1f));
                float safeStep = ClearContactRoute(targetRoot, direction, Mathf.Min(remaining, speed * stepTime * scale), traversing);
                if (debugContactMotion && elapsed >= nextTrace)
                {
                    nextTrace += .1f;
                    var targetCollider = targetRoot != null ? targetRoot.GetComponentInChildren<Collider>() : null;
                    bool ignored = targetCollider != null && locomotionBridge.ScriptedMotionColliders.Length > 0 && Physics.GetIgnoreCollision(locomotionBridge.ScriptedMotionColliders[0], targetCollider);
                    Debug.Log($"[TargetMobility] Step | elapsed={elapsed:F3} pos={current} velocity={locomotionBridge.Velocity} remaining={remaining:F3} speed={speed:F2} safe={safeStep:F3} scope={locomotionBridge.TargetCollisionBypassCount} pairIgnored={ignored}", this);
                }
                if (context != null) FaceActionTarget();
                else locomotionBridge.SetActionFacingDirection(direction);
                if (!locomotionBridge.DriveScriptedPlanarMotion(this, direction * (safeStep / stepTime))) break;
                yield return new WaitForFixedUpdate(); elapsed += clock != null ? clock.FixedDeltaTime : Time.fixedDeltaTime;
                if (safeStep <= .002f)
                {
                    if (debugContactMotion) Debug.Log($"[TargetMobility] Blocked | position={transform.position} elapsed={elapsed}", this);
                    break;
                }
            }
            if (IsCurrentSession(generation) && (targetRoot != null || freeDash))
                ResolveTravelledContact(skill, targetRoot, previous, transform.position, generation);
        }
        finally
        {
            collisionScope?.Dispose();
            if (ReferenceEquals(contactCollisionHandle, collisionScope)) contactCollisionHandle = null;
            if (lungeToken == targetLungeToken && contactGeneration == generation)
            {
                locomotionBridge.DriveScriptedPlanarMotion(this, Vector3.zero);
                // The contact collision bypass ends with the travelled route,
                // but an authored committed strike retains its control/facing
                // lock until TerminateAction (completion, interruption or death).
                if (!profile.holdControlsUntilAnimationEnd || !IsCurrentSession(generation))
                {
                    locomotionBridge.EndScriptedPlanarMotion(this);
                    targetLungeOwnsPlanarMotion = false;
                }
                targetLungeRoutine = null;
            }
        }
    }

    private void ResolveTravelledContact(SkillSO skill, Transform targetRoot, Vector3 previous, Vector3 current, int generation)
    {
        if (contactImpactUsed || !contactWindowArmed || generation != activeToken || !actionActive || !FiniteContact(previous) || !FiniteContact(current)) return;
        Vector3 travel = Vector3.ProjectOnPlane(current - previous, Vector3.up);
        float distance = travel.magnitude;
        Vector3 direction = distance > .0001f ? travel / distance : contactTravelDirection;
        foreach (var body in locomotionBridge.ScriptedMotionColliders)
        {
            if (!(body is CapsuleCollider capsule) || !body.enabled) continue;
            GetCapsuleWorldPoints(capsule, out var a, out var b, out float radius);
            Vector3 offset = previous - current;
            bool touching = false; float contactDistance = distance;
            var contactTarget = targetRoot;
            foreach (var collider in Physics.OverlapCapsule(a, b, radius + .12f, ~0, QueryTriggerInteraction.Ignore))
            {
                var candidate = targetRoot == null && AttackContext != null ? RealTimeCombatManager.TargetFromCollider(collider)?.Root : targetRoot;
                if (candidate != null && collider.transform.IsChildOf(candidate)) { touching = true; contactTarget = candidate; }
            }
            if (distance > .0001f)
                foreach (var hit in Physics.CapsuleCastAll(a + offset, b + offset, radius + .03f, direction, distance, ~0, QueryTriggerInteraction.Ignore))
                {
                    var candidate = targetRoot == null && AttackContext != null ? RealTimeCombatManager.TargetFromCollider(hit.collider)?.Root : targetRoot;
                    if (candidate != null && hit.collider.transform.IsChildOf(candidate) && (!touching || hit.distance < contactDistance))
                    { touching = true; contactDistance = hit.distance; contactTarget = candidate; }
                }
            if (!touching) continue;
            contactImpactUsed = true;
            if (debugContactMotion) Debug.Log($"[TargetMobility] Contact | position={previous + direction * contactDistance} target={contactTarget.position}", this);
            if (externalContactResolver != null)
            {
                externalContactResolver(contactTarget, previous + direction * contactDistance, contactTravelDirection);
                return;
            }
            var manager = RealTimeCombatManager.Instance;
            int applied = manager != null ? manager.ApplySkillDamageAtContact(skill, contactTarget,
                previous + direction * contactDistance, contactTravelDirection) : 0;
            if (applied > 0) HandleConfirmedExternalImpact(skill, contactTarget);
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

    public float GetSafeAttackTravel(SkillSO skill, PlayerAttackContext context, float freeDashDistance = -1)
    {
        if (context == null || locomotionBridge == null) return 0;
        if (!context.TargetAlive) return GetSafePlanarTravel(context.Direction, freeDashDistance >= 0 ? freeDashDistance : ResolveFreeContactDistance());
        float distance = Vector3.ProjectOnPlane(context.TargetOrigin - transform.position, Vector3.up).magnitude;
        if (skill.TargetLunge.passThroughTarget)
        {
            float requested = Mathf.Max(0, Vector3.Dot(context.TargetOrigin - transform.position, context.Direction) + skill.TargetLunge.passThroughDistance);
            float clear = ClearContactRoute(context.TargetRoot, context.Direction, requested, true);
            if (clear >= TargetExitDistance(context.TargetRoot, transform.position, context.Direction)) return clear;
        }
        return ClearContactRoute(context.TargetRoot, context.Direction, Mathf.Max(0, distance - skill.TargetLunge.stoppingDistance), false);
    }

    private float ClearContactRoute(Transform target, Vector3 direction, float requested, bool ignoreTarget)
    {
        if (!FiniteContact(direction) || !FiniteContact(transform.position) || !FiniteContact(requested) || requested <= 0 || locomotionBridge == null) return 0;
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

    private static bool FiniteContact(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool FiniteContact(Vector3 value) => FiniteContact(value.x) && FiniteContact(value.y) && FiniteContact(value.z);
}
