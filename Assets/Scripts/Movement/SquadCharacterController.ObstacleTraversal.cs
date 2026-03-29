using UnityEngine;

public partial class SquadCharacterController
{
    private bool CanUseObstacleTraversal()
    {
        if (!enableObstacleTraversal)
        {
            return false;
        }

        if (Time.time < groundIgnoreUntilTime)
        {
            return false;
        }

        return isGrounded || Time.time <= lastGroundedTime + obstacleTraversalGroundGraceTime;
    }

    private void ApplyObstacleTraversalOffsetToRigidbody(Vector3 resolvedDisplacement)
    {
        if (rigidbodyTarget == null)
        {
            return;
        }

        Vector3 up = transform.up;
        float verticalOffset = Vector3.Dot(resolvedDisplacement, up);
        if (Mathf.Abs(verticalOffset) <= 0.0001f)
        {
            return;
        }

        if (verticalOffset > 0f)
        {
            QueueObstacleTraversalVisualLag(verticalOffset);
        }

        Vector3 correctedPosition = rigidbodyTarget.position + (up * verticalOffset);
        rigidbodyTarget.MovePosition(correctedPosition);
    }

    private void CacheObstacleTraversalVisualTargets()
    {
        ResetObstacleTraversalVisualTargetsImmediate();
        obstacleTraversalVisualTargets.Clear();
        obstacleTraversalVisualBaseLocalPositions.Clear();

        if (!smoothObstacleTraversalVisuals)
        {
            return;
        }

        Transform explicitRoot = obstacleTraversalVisualRoot;
        if (explicitRoot != null && explicitRoot != transform)
        {
            AddObstacleTraversalVisualTarget(explicitRoot);
            return;
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (string.Equals(child.name, "root", System.StringComparison.OrdinalIgnoreCase))
            {
                AddObstacleTraversalVisualTarget(child);
                return;
            }
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.GetComponentInChildren<Renderer>(true) != null)
            {
                AddObstacleTraversalVisualTarget(child);
            }
        }
    }

    private void AddObstacleTraversalVisualTarget(Transform target)
    {
        if (target == null || target == transform)
        {
            return;
        }

        if (obstacleTraversalVisualTargets.Contains(target))
        {
            return;
        }

        obstacleTraversalVisualTargets.Add(target);
        obstacleTraversalVisualBaseLocalPositions.Add(target.localPosition);
    }

    private void QueueObstacleTraversalVisualLag(float stepHeight)
    {
        if (!smoothObstacleTraversalVisuals || obstacleTraversalVisualTargets.Count == 0)
        {
            return;
        }

        float filteredStepHeight = Mathf.Max(0f, stepHeight - obstacleTraversalVisualDeadZone);
        if (filteredStepHeight <= 0.0001f)
        {
            return;
        }

        obstacleTraversalVisualLagTarget = Mathf.Clamp(
            obstacleTraversalVisualLagTarget + filteredStepHeight,
            0f,
            obstacleTraversalVisualMaxLag);
    }

    private void UpdateObstacleTraversalVisualSmoothing(float deltaTime)
    {
        if (!smoothObstacleTraversalVisuals || obstacleTraversalVisualTargets.Count == 0)
        {
            return;
        }

        if (deltaTime > 0f)
        {
            obstacleTraversalVisualLagTarget = Mathf.MoveTowards(
                obstacleTraversalVisualLagTarget,
                0f,
                obstacleTraversalVisualCatchUpSpeed * deltaTime);
            float blend = 1f - Mathf.Exp(-obstacleTraversalVisualResponsiveness * deltaTime);
            obstacleTraversalVisualLag = Mathf.Lerp(
                obstacleTraversalVisualLag,
                obstacleTraversalVisualLagTarget,
                blend);
        }
        else
        {
            obstacleTraversalVisualLag = obstacleTraversalVisualLagTarget;
        }

        for (int i = 0; i < obstacleTraversalVisualTargets.Count; i++)
        {
            Transform target = obstacleTraversalVisualTargets[i];
            if (target == null)
            {
                continue;
            }

            Transform parent = target.parent;
            Vector3 localUp = parent != null
                ? parent.InverseTransformDirection(transform.up).normalized
                : Vector3.up;
            target.localPosition = obstacleTraversalVisualBaseLocalPositions[i] - (localUp * obstacleTraversalVisualLag);
        }
    }

    private void ResetObstacleTraversalVisualTargetsImmediate()
    {
        obstacleTraversalVisualLag = 0f;
        obstacleTraversalVisualLagTarget = 0f;
        int count = Mathf.Min(obstacleTraversalVisualTargets.Count, obstacleTraversalVisualBaseLocalPositions.Count);
        for (int i = 0; i < count; i++)
        {
            Transform target = obstacleTraversalVisualTargets[i];
            if (target == null)
            {
                continue;
            }

            target.localPosition = obstacleTraversalVisualBaseLocalPositions[i];
        }
    }

    private bool TryResolveForwardSupportTraversal(
        Vector3 basePoint1,
        Vector3 basePoint2,
        float radius,
        Vector3 attemptedDisplacement,
        int blockingMask,
        out Vector3 traversalDisplacement)
    {
        traversalDisplacement = Vector3.zero;
        if (!CanUseObstacleTraversal())
        {
            return false;
        }

        Vector3 up = transform.up;
        Vector3 horizontalDisplacement = Vector3.ProjectOnPlane(attemptedDisplacement, up);
        float horizontalDistance = horizontalDisplacement.magnitude;
        if (horizontalDistance <= 0.0001f)
        {
            return false;
        }

        float castRadius = Mathf.Max(0.01f, radius - movementCollisionSkin);
        Vector3 currentFootPoint = GetCapsuleFootPoint(basePoint1, basePoint2, radius, up);
        if (!TrySampleGround(
                currentFootPoint,
                up,
                obstacleTraversalContactOffset + 0.05f,
                Mathf.Max(0.05f, obstacleTraversalProbeDistance),
                GetGroundSupportMask(),
                out GroundProbeSample currentSupport))
        {
            return false;
        }

        Vector3 direction = horizontalDisplacement / horizontalDistance;
        float lookAheadDistance = horizontalDistance + Mathf.Max(radius * 0.6f, 0.1f);
        Vector3 aheadFootPoint = currentFootPoint + direction * lookAheadDistance;
        if (!TrySampleGround(
                aheadFootPoint,
                up,
                obstacleTraversalMaxStepHeight + obstacleTraversalClearance,
                obstacleTraversalProbeDistance + obstacleTraversalMaxStepHeight,
                GetGroundSupportMask(),
                out GroundProbeSample aheadSupport))
        {
            return false;
        }

        float rise = Vector3.Dot(aheadSupport.point - currentSupport.point, up);
        if (rise <= 0.03f)
        {
            return false;
        }

        float finalVerticalOffset = rise + obstacleTraversalContactOffset;
        float maxAllowedVerticalOffset = obstacleTraversalMaxStepHeight + obstacleTraversalClearance + obstacleTraversalContactOffset;
        if (finalVerticalOffset > maxAllowedVerticalOffset)
        {
            return false;
        }

        Vector3 finalPoint1 = basePoint1 + horizontalDisplacement + (up * finalVerticalOffset);
        Vector3 finalPoint2 = basePoint2 + horizontalDisplacement + (up * finalVerticalOffset);
        if (!IsCapsulePlacementClear(finalPoint1, finalPoint2, castRadius, blockingMask, aheadSupport.collider))
        {
            return false;
        }

        traversalDisplacement = horizontalDisplacement + (up * finalVerticalOffset);
        return true;
    }

    private bool TryResolveProactiveObstacleTraversal(
        Vector3 basePoint1,
        Vector3 basePoint2,
        float radius,
        Vector3 attemptedDisplacement,
        int blockingMask,
        out Vector3 traversalDisplacement)
    {
        traversalDisplacement = Vector3.zero;
        if (!TryGetLowObstacleAheadHit(basePoint1, basePoint2, radius, attemptedDisplacement, blockingMask, out RaycastHit lowObstacleHit))
        {
            return false;
        }

        return TryResolveObstacleTraversal(
            basePoint1,
            basePoint2,
            radius,
            attemptedDisplacement,
            blockingMask,
            lowObstacleHit,
            out traversalDisplacement);
    }

    private bool TryResolveObstacleTraversal(
        Vector3 basePoint1,
        Vector3 basePoint2,
        float radius,
        Vector3 attemptedDisplacement,
        int blockingMask,
        RaycastHit blockingHit,
        out Vector3 traversalDisplacement)
    {
        traversalDisplacement = Vector3.zero;
        if (!CanUseObstacleTraversal())
        {
            return false;
        }

        Vector3 up = transform.up;
        Vector3 horizontalDisplacement = Vector3.ProjectOnPlane(attemptedDisplacement, up);
        float horizontalDistance = horizontalDisplacement.magnitude;
        if (horizontalDistance <= 0.0001f)
        {
            return false;
        }

        float maxStepHeight = Mathf.Max(0.02f, obstacleTraversalMaxStepHeight);
        float clearance = Mathf.Max(0.01f, obstacleTraversalClearance);
        float liftAmount = maxStepHeight + clearance;
        float castRadius = Mathf.Max(0.01f, radius - movementCollisionSkin);
        Vector3 direction = horizontalDisplacement / horizontalDistance;
        Vector3 liftOffset = up * liftAmount;
        Vector3 currentFootPoint = GetCapsuleFootPoint(basePoint1, basePoint2, radius, up);

        Vector3 liftedPoint1 = basePoint1 + liftOffset;
        Vector3 liftedPoint2 = basePoint2 + liftOffset;
        if (!IsCapsulePlacementClear(liftedPoint1, liftedPoint2, castRadius, blockingMask))
        {
            return false;
        }

        if (TryGetHorizontalBlockingHit(
                liftedPoint1,
                liftedPoint2,
                castRadius,
                direction,
                horizontalDistance + movementCollisionSkin,
                blockingMask,
                out _))
        {
            return false;
        }

        Vector3 liftedTargetPoint1 = liftedPoint1 + horizontalDisplacement;
        Vector3 liftedTargetPoint2 = liftedPoint2 + horizontalDisplacement;
        Vector3 liftedTargetFootPoint = GetCapsuleFootPoint(liftedTargetPoint1, liftedTargetPoint2, radius, up);
        float maxProbeDown = liftAmount + Mathf.Max(0.02f, obstacleTraversalProbeDistance);

        if (!TrySampleGround(
                liftedTargetFootPoint,
                up,
                clearance,
                maxProbeDown,
                GetGroundSupportMask(),
                out GroundProbeSample support))
        {
            return false;
        }

        float supportOffsetFromLiftedFoot = Vector3.Dot(support.point - liftedTargetFootPoint, up);
        float finalVerticalOffset = liftAmount + supportOffsetFromLiftedFoot + obstacleTraversalContactOffset;
        if (finalVerticalOffset <= 0.0001f)
        {
            return false;
        }

        if (blockingHit.collider != null)
        {
            float obstacleHeight = Mathf.Max(0f, Vector3.Dot(blockingHit.point - currentFootPoint, up));
            float minimumLiftFromObstacle = obstacleHeight + obstacleTraversalContactOffset;
            finalVerticalOffset = Mathf.Max(finalVerticalOffset, minimumLiftFromObstacle);
        }

        float maxAllowedVerticalOffset = maxStepHeight + clearance + obstacleTraversalContactOffset;
        if (finalVerticalOffset > maxAllowedVerticalOffset)
        {
            return false;
        }

        Vector3 finalPoint1 = basePoint1 + horizontalDisplacement + (up * finalVerticalOffset);
        Vector3 finalPoint2 = basePoint2 + horizontalDisplacement + (up * finalVerticalOffset);
        if (!IsCapsulePlacementClear(finalPoint1, finalPoint2, castRadius, blockingMask, support.collider))
        {
            return false;
        }

        traversalDisplacement = horizontalDisplacement + (up * finalVerticalOffset);
        return true;
    }

    private bool TryGetLowObstacleAheadHit(
        Vector3 basePoint1,
        Vector3 basePoint2,
        float radius,
        Vector3 attemptedDisplacement,
        int mask,
        out RaycastHit hit)
    {
        hit = default;
        if (!CanUseObstacleTraversal())
        {
            return false;
        }

        Vector3 up = transform.up;
        Vector3 horizontalDisplacement = Vector3.ProjectOnPlane(attemptedDisplacement, up);
        float horizontalDistance = horizontalDisplacement.magnitude;
        if (horizontalDistance <= 0.0001f)
        {
            return false;
        }

        Vector3 direction = horizontalDisplacement / horizontalDistance;
        float maxStepHeight = Mathf.Max(0.02f, obstacleTraversalMaxStepHeight);
        float castRadius = Mathf.Max(0.01f, Mathf.Min(radius * 0.92f, radius - movementCollisionSkin * 0.5f));
        Vector3 footPoint = GetCapsuleFootPoint(basePoint1, basePoint2, radius, up);
        Vector3 lowerBandBottom = footPoint + up * (castRadius + 0.01f);
        float lowerBandHeight = Mathf.Max(0.02f, maxStepHeight - castRadius);
        Vector3 lowerBandTop = lowerBandBottom + up * lowerBandHeight;

        int hitCount = Physics.CapsuleCastNonAlloc(
            lowerBandTop,
            lowerBandBottom,
            castRadius,
            direction,
            movementCastHits,
            horizontalDistance + movementCollisionSkin,
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

        hit = movementCastHits[bestIndex];
        return true;
    }

    private bool IsCapsulePlacementClear(
        Vector3 point1,
        Vector3 point2,
        float radius,
        int mask,
        Collider ignoredCollider = null)
    {
        int hitCount = Physics.OverlapCapsuleNonAlloc(
            point1,
            point2,
            radius,
            movementOverlapHits,
            mask,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = movementOverlapHits[i];
            if (col == null || col == ignoredCollider || IsSelfCollider(col))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static Vector3 GetCapsuleFootPoint(Vector3 point1, Vector3 point2, float radius, Vector3 up)
    {
        Vector3 bottomCenter = Vector3.Dot(point1 - point2, up) >= 0f ? point2 : point1;
        return bottomCenter - (up * radius);
    }
}
