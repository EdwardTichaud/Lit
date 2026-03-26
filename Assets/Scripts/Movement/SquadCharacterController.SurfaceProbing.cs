using UnityEngine;

public partial class SquadCharacterController
{
    private enum SurfaceTraversalType
    {
        None = 0,
        Walkable = 1,
        StepUp = 2,
        StepDown = 3,
        Blocked = 4,
        Ledge = 5,
    }

    private struct SurfaceProbeContext
    {
        public Vector3 up;
        public Vector3 center;
        public float radius;
        public float height;
        public Vector3 bottomCenter;
        public Vector3 footPoint;
    }

    private struct SurfaceTraversalResult
    {
        public SurfaceTraversalType type;
        public StepGroundSample currentGround;
        public StepGroundSample targetGround;
        public float heightDelta;
        public float lookAheadDistance;
        public bool hasCurrentGround;
        public bool hasTargetGround;
    }

    [Header("Surface Probing")]
    [SerializeField, Range(0f, 89f), Tooltip("Angle max d'une surface consideree comme marchable.")]
    private float maxWalkableSlopeAngle = 55f;
    [SerializeField, Tooltip("Affiche les derniers probes de surface dans la scene pour debug.")]
    private bool debugSurfaceProbeGizmos;

    private Vector3 debugSurfaceProbeOrigin;
    private Vector3 debugSurfaceProbeDirection;
    private Vector3 debugSurfaceCurrentPoint;
    private Vector3 debugSurfaceTargetPoint;
    private SurfaceTraversalType debugSurfaceTraversalType;
    private bool debugSurfaceHasCurrentPoint;
    private bool debugSurfaceHasTargetPoint;

    private float GetWalkableGroundNormalDot()
    {
        return Mathf.Cos(Mathf.Clamp(maxWalkableSlopeAngle, 0f, 89f) * Mathf.Deg2Rad);
    }

    private int GetSurfaceSupportMask()
    {
        return GetVoidGroundMask() | GetStepMask();
    }

    private bool TryBuildSurfaceProbeContext(out SurfaceProbeContext context)
    {
        context = default;
        Vector3 center;
        float radius;
        float height;
        if (TryGetStepCapsule(out center, out radius, out height))
        {
            Vector3 up = transform.up;
            float halfHeight = height * 0.5f;
            float bottomOffset = Mathf.Max(0f, halfHeight - radius);
            Vector3 bottomCenter = center - up * bottomOffset;

            context.up = up;
            context.center = center;
            context.radius = radius;
            context.height = height;
            context.bottomCenter = bottomCenter;
            context.footPoint = bottomCenter - up * radius;
            return true;
        }

        if (characterController == null)
        {
            return false;
        }

        Bounds bounds = characterController.bounds;
        Vector3 fallbackUp = transform.up;
        radius = Mathf.Max(0.01f, Mathf.Max(bounds.extents.x, bounds.extents.z));
        height = Mathf.Max(bounds.size.y, radius * 2f);
        center = bounds.center;
        float fallbackHalfHeight = height * 0.5f;
        float fallbackBottomOffset = Mathf.Max(0f, fallbackHalfHeight - radius);
        Vector3 fallbackBottomCenter = center - fallbackUp * fallbackBottomOffset;

        context.up = fallbackUp;
        context.center = center;
        context.radius = radius;
        context.height = height;
        context.bottomCenter = fallbackBottomCenter;
        context.footPoint = fallbackBottomCenter - fallbackUp * radius;
        return true;
    }

    private bool TryProbeGroundedSupport(float probeDistance, float probeRadius, out StepGroundSample sample, out float bottomGap)
    {
        sample = default;
        bottomGap = float.PositiveInfinity;

        if (!TryBuildSurfaceProbeContext(out SurfaceProbeContext context))
        {
            return false;
        }

        float clampedProbeDistance = Mathf.Max(0.005f, probeDistance);
        float clampedProbeRadius = Mathf.Max(0.02f, probeRadius);
        int supportMask = GetSurfaceSupportMask();
        if (!TrySampleGround(
                context.footPoint,
                context.up,
                clampedProbeRadius,
                clampedProbeDistance + clampedProbeRadius,
                supportMask,
                requireStepSurface: false,
                out sample))
        {
            Vector3 overlapCenter = context.footPoint + context.up * (clampedProbeRadius - 0.002f);
            int overlapCount = Physics.OverlapSphereNonAlloc(
                overlapCenter,
                clampedProbeRadius,
                stepOverlapHits,
                supportMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider col = stepOverlapHits[i];
                if (col == null || IsSelfCollider(col))
                {
                    continue;
                }

                sample.collider = col;
                sample.point = context.footPoint;
                sample.normal = context.up;
                bottomGap = 0f;
                return true;
            }

            Vector3 sphereCastOrigin = context.footPoint + context.up * (clampedProbeRadius + 0.002f);
            int hitCount = Physics.SphereCastNonAlloc(
                sphereCastOrigin,
                clampedProbeRadius,
                -context.up,
                stepCastHits,
                clampedProbeDistance + 0.004f,
                supportMask,
                QueryTriggerInteraction.Ignore);
            float bestDistance = float.PositiveInfinity;
            int bestIndex = -1;
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = stepCastHits[i].collider;
                if (col == null || IsSelfCollider(col))
                {
                    continue;
                }

                if (Vector3.Dot(stepCastHits[i].normal, context.up) < GetWalkableGroundNormalDot())
                {
                    continue;
                }

                float hitDistance = stepCastHits[i].distance;
                if (hitDistance < bestDistance)
                {
                    bestDistance = hitDistance;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                return false;
            }

            RaycastHit bestHit = stepCastHits[bestIndex];
            sample.point = bestHit.point;
            sample.normal = bestHit.normal;
            sample.collider = bestHit.collider;
        }

        bottomGap = Vector3.Dot(context.footPoint - sample.point, context.up);
        return bottomGap <= clampedProbeDistance &&
               bottomGap >= -(clampedProbeRadius + 0.01f);
    }

    private bool TryProbeTraversalGround(SurfaceProbeContext context, out StepGroundSample sample)
    {
        float maxDown = Mathf.Max(stepHeight + stepHeightTolerance, jumpGroundCheckDistance) + stepGroundCheckDistance;
        float maxUp = Mathf.Max(0.02f, stepGroundCheckDistance);
        return TrySampleGround(
            context.footPoint,
            context.up,
            maxUp,
            maxDown,
            GetSurfaceSupportMask(),
            requireStepSurface: false,
            out sample);
    }

    private SurfaceTraversalResult EvaluateForwardTraversal(Vector3 moveDirection)
    {
        SurfaceTraversalResult result = default;
        if (moveDirection.sqrMagnitude < 0.0001f)
        {
            result.type = SurfaceTraversalType.None;
            UpdateSurfaceProbeDebug(default, Vector3.zero, result);
            return result;
        }

        if (!TryBuildSurfaceProbeContext(out SurfaceProbeContext context))
        {
            result.type = SurfaceTraversalType.None;
            UpdateSurfaceProbeDebug(default, moveDirection, result);
            return result;
        }

        Vector3 direction = moveDirection.normalized;
        if (!TryProbeTraversalGround(context, out result.currentGround))
        {
            result.type = SurfaceTraversalType.None;
            UpdateSurfaceProbeDebug(context, direction, result);
            return result;
        }

        result.hasCurrentGround = true;

        float maxUp = stepHeight + stepHeightTolerance;
        float maxDown = (stepDownHeight > 0f ? stepDownHeight : stepHeight) + stepHeightTolerance;
        float probeUp = maxUp + stepGroundCheckDistance;
        float probeDown = maxDown + stepGroundCheckDistance;
        float lookAheadDistance = Mathf.Max(stepCheckDistance, voidCheckDistance);
        float startDistance = context.radius + Mathf.Max(0.02f, lookAheadDistance * 0.35f);
        float endDistance = context.radius + Mathf.Max(0.02f, lookAheadDistance);
        result.lookAheadDistance = endDistance;

        bool currentOnStairs = IsStepSurfaceCollider(result.currentGround.collider);
        float defaultDeltaThreshold = currentOnStairs
            ? StepAssistSurfaceDeadZone
            : Mathf.Max(0.001f, stepMinHeight);

        bool lowerBlocked = TryGetMovementCapsule(out Vector3 movementPoint1, out Vector3 movementPoint2, out float movementRadius) &&
                            TryGetHorizontalBlockingHit(
                                movementPoint1,
                                movementPoint2,
                                Mathf.Max(0.01f, movementRadius - stepRadiusPadding),
                                direction,
                                startDistance,
                                GetMovementBlockingMask(),
                                out _);

        bool anySupportFound = false;
        bool supportGapEncountered = false;
        bool hasWalkableCandidate = false;
        StepGroundSample walkableCandidate = default;

        for (int i = 0; i < StepAssistLookAheadSampleCount; i++)
        {
            float t = StepAssistLookAheadSampleCount == 1 ? 1f : (float)i / (StepAssistLookAheadSampleCount - 1);
            float distance = Mathf.Lerp(startDistance, endDistance, t);
            Vector3 sampleOrigin = context.footPoint + direction * distance;
            if (!TrySampleGround(
                    sampleOrigin,
                    context.up,
                    probeUp,
                    probeDown,
                    GetSurfaceSupportMask(),
                    requireStepSurface: false,
                    out StepGroundSample candidateGround))
            {
                if (anySupportFound)
                {
                    supportGapEncountered = true;
                }

                continue;
            }

            anySupportFound = true;
            float delta = Vector3.Dot(candidateGround.point - result.currentGround.point, context.up);
            if (delta > maxUp + 0.01f || delta < -maxDown - 0.01f)
            {
                continue;
            }

            bool targetOnStairs = IsStepSurfaceCollider(candidateGround.collider);
            float sampleThreshold = (currentOnStairs || targetOnStairs)
                ? StepAssistSurfaceDeadZone
                : defaultDeltaThreshold;

            result.targetGround = candidateGround;
            result.hasTargetGround = true;
            result.heightDelta = delta;

            if (Mathf.Abs(delta) <= sampleThreshold)
            {
                walkableCandidate = candidateGround;
                hasWalkableCandidate = true;
                continue;
            }

            if (delta > sampleThreshold)
            {
                if (!lowerBlocked && !currentOnStairs && !targetOnStairs)
                {
                    walkableCandidate = candidateGround;
                    hasWalkableCandidate = true;
                    continue;
                }

                result.type = SurfaceTraversalType.StepUp;
                UpdateSurfaceProbeDebug(context, direction, result);
                return result;
            }

            bool allowStepDown = currentOnStairs || targetOnStairs || supportGapEncountered;
            if (allowStepDown)
            {
                result.type = SurfaceTraversalType.StepDown;
                UpdateSurfaceProbeDebug(context, direction, result);
                return result;
            }

            walkableCandidate = candidateGround;
            hasWalkableCandidate = true;
        }

        if (hasWalkableCandidate)
        {
            result.targetGround = walkableCandidate;
            result.hasTargetGround = true;
            result.heightDelta = Vector3.Dot(walkableCandidate.point - result.currentGround.point, context.up);
            result.type = SurfaceTraversalType.Walkable;
            UpdateSurfaceProbeDebug(context, direction, result);
            return result;
        }

        if (lowerBlocked)
        {
            result.type = SurfaceTraversalType.Blocked;
            UpdateSurfaceProbeDebug(context, direction, result);
            return result;
        }

        result.type = anySupportFound ? SurfaceTraversalType.Walkable : SurfaceTraversalType.Ledge;
        UpdateSurfaceProbeDebug(context, direction, result);
        return result;
    }

    private void UpdateSurfaceProbeDebug(SurfaceProbeContext context, Vector3 direction, SurfaceTraversalResult result)
    {
        if (!debugSurfaceProbeGizmos)
        {
            return;
        }

        debugSurfaceProbeOrigin = context.footPoint;
        debugSurfaceProbeDirection = direction;
        debugSurfaceCurrentPoint = result.currentGround.point;
        debugSurfaceTargetPoint = result.targetGround.point;
        debugSurfaceTraversalType = result.type;
        debugSurfaceHasCurrentPoint = result.hasCurrentGround;
        debugSurfaceHasTargetPoint = result.hasTargetGround;
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugSurfaceProbeGizmos)
        {
            return;
        }

        Color traversalColor = debugSurfaceTraversalType switch
        {
            SurfaceTraversalType.StepUp => Color.cyan,
            SurfaceTraversalType.StepDown => new Color(0.3f, 0.7f, 1f),
            SurfaceTraversalType.Blocked => Color.yellow,
            SurfaceTraversalType.Ledge => Color.red,
            _ => Color.green,
        };

        if (debugSurfaceProbeOrigin != Vector3.zero)
        {
            Gizmos.color = traversalColor;
            Gizmos.DrawLine(debugSurfaceProbeOrigin, debugSurfaceProbeOrigin + (debugSurfaceProbeDirection * Mathf.Max(stepCheckDistance, voidCheckDistance)));
        }

        if (debugSurfaceHasCurrentPoint)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(debugSurfaceCurrentPoint, 0.04f);
        }

        if (debugSurfaceHasTargetPoint)
        {
            Gizmos.color = traversalColor;
            Gizmos.DrawSphere(debugSurfaceTargetPoint, 0.05f);
        }
    }
}
