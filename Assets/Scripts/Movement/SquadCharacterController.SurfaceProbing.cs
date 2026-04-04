using UnityEngine;

public partial class SquadCharacterController
{
    private struct GroundProbeContext
    {
        public Vector3 up;
        public Vector3 center;
        public float radius;
        public float height;
        public Vector3 footPoint;
    }

    [Header("Ground Probing")]
    [SerializeField, Range(0f, 89f), Tooltip("Angle max d'une surface consideree comme marchable.")]
    private float maxWalkableSlopeAngle = 55f;

    private float GetWalkableGroundNormalDot()
    {
        return Mathf.Cos(Mathf.Clamp(maxWalkableSlopeAngle, 0f, 89f) * Mathf.Deg2Rad);
    }

    private int GetGroundSupportMask()
    {
        return GetVoidGroundMask();
    }

    private bool TryBuildGroundProbeContext(out GroundProbeContext context)
    {
        context = default;
        Vector3 center;
        float radius;
        float height;
        if (TryGetLocomotionCapsule(out center, out radius, out height))
        {
            Vector3 up = transform.up;
            float halfHeight = height * 0.5f;
            float bottomOffset = Mathf.Max(0f, halfHeight - radius);
            Vector3 bottomCenter = center - up * bottomOffset;

            context.up = up;
            context.center = center;
            context.radius = radius;
            context.height = height;
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
        context.footPoint = fallbackBottomCenter - fallbackUp * radius;
        return true;
    }

    private bool TryProbeGroundedSupport(float probeDistance, float probeRadius, out GroundProbeSample sample, out float bottomGap)
    {
        sample = default;
        bottomGap = float.PositiveInfinity;

        if (!TryBuildGroundProbeContext(out GroundProbeContext context))
        {
            return false;
        }

        float clampedProbeDistance = Mathf.Max(0.005f, probeDistance);
        float clampedProbeRadius = Mathf.Max(0.02f, probeRadius);
        int supportMask = GetGroundSupportMask();
        if (!TrySampleGround(
                context.footPoint,
                context.up,
                clampedProbeRadius,
                clampedProbeDistance + clampedProbeRadius,
                supportMask,
                out sample))
        {
            Vector3 overlapCenter = context.footPoint + context.up * (clampedProbeRadius - 0.002f);
            int overlapCount = Physics.OverlapSphereNonAlloc(
                overlapCenter,
                clampedProbeRadius,
                movementOverlapHits,
                supportMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider col = movementOverlapHits[i];
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
                movementCastHits,
                clampedProbeDistance + 0.004f,
                supportMask,
                QueryTriggerInteraction.Ignore);
            float bestDistance = float.PositiveInfinity;
            int bestIndex = -1;
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = movementCastHits[i].collider;
                if (col == null || IsSelfCollider(col))
                {
                    continue;
                }

                if (Vector3.Dot(movementCastHits[i].normal, context.up) < GetWalkableGroundNormalDot())
                {
                    continue;
                }

                float hitDistance = movementCastHits[i].distance;
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

            RaycastHit bestHit = movementCastHits[bestIndex];
            sample.point = bestHit.point;
            sample.normal = bestHit.normal;
            sample.collider = bestHit.collider;
        }

        bottomGap = Vector3.Dot(context.footPoint - sample.point, context.up);
        return bottomGap <= clampedProbeDistance &&
               bottomGap >= -(clampedProbeRadius + 0.01f);
    }

    private bool TrySampleGround(Vector3 origin, Vector3 up, float maxUp, float maxDown, int mask, out GroundProbeSample sample)
    {
        sample = default;
        float upRange = Mathf.Max(0.02f, maxUp);
        float downRange = Mathf.Max(0.02f, maxDown);
        float rayStart = upRange + 0.05f;
        float rayDistance = upRange + downRange + 0.1f;
        Vector3 rayOrigin = origin + up * rayStart;

        int hitCount = Physics.RaycastNonAlloc(rayOrigin, -up, movementCastHits, rayDistance, mask, QueryTriggerInteraction.Ignore);
        float bestDistance = float.PositiveInfinity;
        int bestIndex = -1;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = movementCastHits[i].collider;
            if (col == null || IsSelfCollider(col))
            {
                continue;
            }

            float heightOffset = Vector3.Dot(movementCastHits[i].point - origin, up);
            if (heightOffset > upRange || heightOffset < -downRange)
            {
                continue;
            }

            if (Vector3.Dot(movementCastHits[i].normal, up) < GetWalkableGroundNormalDot())
            {
                continue;
            }

            float distance = movementCastHits[i].distance;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        RaycastHit bestHit = movementCastHits[bestIndex];
        sample.point = bestHit.point;
        sample.normal = bestHit.normal;
        sample.collider = bestHit.collider;
        return true;
    }

    private bool HasGroundSupportAhead(Vector3 moveDirection, float lookAheadDistance = -1f)
    {
        if (moveDirection.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        if (!TryBuildGroundProbeContext(out GroundProbeContext context))
        {
            return true;
        }

        Vector3 direction = moveDirection.normalized;
        float configuredLookAhead = Mathf.Max(0.05f, voidCheckDistance);
        float requestedLookAhead = lookAheadDistance > 0f
            ? Mathf.Max(0.05f, lookAheadDistance)
            : configuredLookAhead;
        float sampleDistance = Mathf.Min(configuredLookAhead, requestedLookAhead);
        float sampleRadius = Mathf.Max(0.05f, context.radius * 0.35f);
        float sampleDepth = Mathf.Max(0.05f, voidCheckDepth);
        int mask = GetGroundSupportMask();
        Vector3 castOrigin = context.footPoint + direction * (context.radius + sampleDistance) + context.up * (sampleRadius + 0.02f);

        int hitCount = Physics.SphereCastNonAlloc(
            castOrigin,
            sampleRadius,
            -context.up,
            movementCastHits,
            sampleDepth + sampleRadius,
            mask,
            QueryTriggerInteraction.Ignore);
        float bestDistance = float.PositiveInfinity;
        int bestIndex = -1;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = movementCastHits[i].collider;
            if (col == null || IsSelfCollider(col))
            {
                continue;
            }

            if (Vector3.Dot(movementCastHits[i].normal, context.up) < GetWalkableGroundNormalDot())
            {
                continue;
            }

            float distance = movementCastHits[i].distance;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex >= 0;
    }
}
