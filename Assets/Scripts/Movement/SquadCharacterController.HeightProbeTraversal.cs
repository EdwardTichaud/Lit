using UnityEngine;

public partial class SquadCharacterController
{
    [Header("Height Probe Traversal")]
    [SerializeField, Tooltip("Autorise le franchissement explicite des petites marches et le snap descendant du sol.")]
    private bool enableHeightProbeTraversal = true;
    [SerializeField, Tooltip("Distance maximale scannee devant le joueur pour trouver le dessus d'une marche.")]
    private float heightProbeForwardDistance = 1.2f;
    [SerializeField, Tooltip("Nombre d'echantillons courts derriere la contremarche detectee.")]
    private int heightProbeSampleCount = 8;
    [SerializeField, Tooltip("Hausse maximale autorisee pour une marche marchable.")]
    private float heightProbeMaxRise = 0.35f;
    [SerializeField, Tooltip("Descente maximale autorisee pour rester colle aux marches descendantes.")]
    private float heightProbeMaxDrop = 0.45f;
    [SerializeField, Tooltip("Hausse maximale appliquee en une seule correction de marche.")]
    private float heightProbeMaxStepRise = 0.32f;
    [SerializeField, Tooltip("Descente maximale appliquee en une seule correction de marche.")]
    private float heightProbeMaxStepDrop = 0.45f;
    [SerializeField, Tooltip("Distance supplementaire scannee derriere la contremarche detectee.")]
    private float heightProbeStepSearchExtraDistance = 0.22f;
    [SerializeField, Tooltip("Recolle le personnage au support apres un deplacement horizontal pour descendre proprement les marches. Ne sert plus a monter.")]
    private bool enableHeightProbeGroundSnap = true;
    [SerializeField, Tooltip("Marge verticale ajoutee au raycast de comparaison de hauteur.")]
    private float heightProbeVerticalPadding = 0.25f;
    [SerializeField, Tooltip("Offset conserve au-dessus du support trouve.")]
    private float heightProbeContactOffset = 0.03f;
    [SerializeField, Tooltip("Hauteur minimale a detecter avant de tenter une correction de marche.")]
    private float heightProbeMinRise = 0.03f;
    [SerializeField, Tooltip("Courte grace apres perte du sol pour eviter un blocage sur une bordure.")]
    private float heightProbeGroundGraceTime = 0.12f;
    [SerializeField, Tooltip("Plafond dur applique aux anciens prefabs pour eviter qu'un obstacle haut soit traite comme une marche.")]
    private float heightProbeObstacleStepLimit = 0.35f;
    [SerializeField, Tooltip("Distance ajoutee derriere la contremarche pour chercher un dessus marchable.")]
    private float heightProbeStepSurfaceInset = 0.08f;
    [SerializeField, Tooltip("Plafond dur de scan pour eviter de chercher un support loin derriere un obstacle.")]
    private float heightProbeForwardScanLimit = 0.9f;

    private void ValidateHeightProbeTraversalSettings()
    {
        heightProbeForwardDistance = Mathf.Max(0.05f, heightProbeForwardDistance);
        heightProbeSampleCount = Mathf.Clamp(heightProbeSampleCount, 1, 64);
        heightProbeMaxRise = Mathf.Max(0f, heightProbeMaxRise);
        heightProbeMaxDrop = Mathf.Max(0f, heightProbeMaxDrop);
        heightProbeMaxStepRise = Mathf.Max(0f, heightProbeMaxStepRise);
        heightProbeMaxStepDrop = Mathf.Max(0f, heightProbeMaxStepDrop);
        heightProbeStepSearchExtraDistance = Mathf.Max(0f, heightProbeStepSearchExtraDistance);
        heightProbeVerticalPadding = Mathf.Max(0.02f, heightProbeVerticalPadding);
        heightProbeContactOffset = Mathf.Max(0f, heightProbeContactOffset);
        heightProbeMinRise = Mathf.Max(0f, heightProbeMinRise);
        heightProbeGroundGraceTime = Mathf.Max(0f, heightProbeGroundGraceTime);
        heightProbeObstacleStepLimit = Mathf.Max(0f, heightProbeObstacleStepLimit);
        heightProbeStepSurfaceInset = Mathf.Max(0.01f, heightProbeStepSurfaceInset);
        heightProbeForwardScanLimit = Mathf.Max(0.05f, heightProbeForwardScanLimit);
    }

    private bool CanUseHeightProbeTraversal()
    {
        if (!enableHeightProbeTraversal || Time.time < groundIgnoreUntilTime)
        {
            return false;
        }

        return isGrounded || Time.time <= lastGroundedTime + heightProbeGroundGraceTime;
    }

    private void ApplyHeightProbeTraversalOffsetToRigidbody(Vector3 resolvedDisplacement)
    {
        Vector3 up = transform.up;
        float verticalOffset = Vector3.Dot(resolvedDisplacement, up);
        if (Mathf.Abs(verticalOffset) <= 0.0001f)
        {
            return;
        }

        heightProbeVerticalOffsetThisStep = verticalOffset;

        if (rigidbodyTarget == null)
        {
            Transform target = motionRoot != null ? motionRoot : transform;
            target.position += up * verticalOffset;
            return;
        }

        // MovePosition is consumed during the physics step; setting the position here
        // makes the next horizontal velocity solve start above the stair riser.
        rigidbodyTarget.position += up * verticalOffset;
        Physics.SyncTransforms();
    }

    private bool TryResolveHeightProbeTraversal(
        Vector3 basePoint1,
        Vector3 basePoint2,
        float radius,
        Vector3 attemptedDisplacement,
        int blockingMask,
        RaycastHit blockingHit,
        out Vector3 traversalDisplacement)
    {
        traversalDisplacement = Vector3.zero;
        if (!CanUseHeightProbeTraversal())
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
        Vector3 currentFootPoint = GetCapsuleFootPoint(basePoint1, basePoint2, radius, up);
        if (!TrySampleGround(
                currentFootPoint,
                up,
                heightProbeVerticalPadding,
                heightProbeVerticalPadding + heightProbeContactOffset + 0.05f,
                GetGroundSupportMask(),
                out GroundProbeSample currentSupport))
        {
            return false;
        }

        if (!TryFindHeightProbeSupportAhead(
                currentFootPoint,
                currentSupport.point,
                direction,
                blockingHit.distance,
                radius,
                horizontalDistance,
                basePoint1,
                basePoint2,
                blockingMask,
                out _,
                out float rise))
        {
            return false;
        }

        float finalVerticalOffset = rise + heightProbeContactOffset;
        float castRadius = Mathf.Max(0.01f, radius - movementCollisionSkin);
        Vector3 finalPoint1 = basePoint1 + horizontalDisplacement + up * finalVerticalOffset;
        Vector3 finalPoint2 = basePoint2 + horizontalDisplacement + up * finalVerticalOffset;
        if (!IsCapsulePlacementClear(finalPoint1, finalPoint2, castRadius, blockingMask))
        {
            return false;
        }

        traversalDisplacement = horizontalDisplacement + up * finalVerticalOffset;
        return true;
    }

    private bool TryFindHeightProbeSupportAhead(
        Vector3 currentFootPoint,
        Vector3 currentSupportPoint,
        Vector3 direction,
        float blockingHitDistance,
        float capsuleRadius,
        float horizontalDistance,
        Vector3 basePoint1,
        Vector3 basePoint2,
        int blockingMask,
        out GroundProbeSample bestSupport,
        out float bestRise)
    {
        bestSupport = default;
        bestRise = 0f;

        Vector3 up = transform.up;
        float maxStepRise = GetHeightProbeStepRiseLimit();
        if (maxStepRise <= 0f)
        {
            return false;
        }

        float startDistance = Mathf.Clamp(
            blockingHitDistance + Mathf.Max(0f, capsuleRadius) + movementCollisionSkin + heightProbeStepSurfaceInset,
            0.05f,
            GetHeightProbeForwardScanDistance());
        float scanDistance = Mathf.Min(
            GetHeightProbeForwardScanDistance(),
            startDistance + heightProbeStepSearchExtraDistance);
        int sampleCount = Mathf.Max(1, heightProbeSampleCount);
        int mask = GetGroundSupportMask();
        float castRadius = Mathf.Max(0.01f, capsuleRadius - movementCollisionSkin);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount == 1
                ? startDistance
                : Mathf.Lerp(startDistance, scanDistance, i / (sampleCount - 1f));
            Vector3 probeFootPoint = currentFootPoint + direction * t;
            if (!TrySampleGround(
                    probeFootPoint,
                    up,
                    heightProbeMaxRise + heightProbeVerticalPadding,
                    heightProbeVerticalPadding + heightProbeContactOffset + 0.05f,
                    mask,
                    out GroundProbeSample sample))
            {
                continue;
            }

            float rise = Vector3.Dot(sample.point - currentSupportPoint, up);
            if (rise < heightProbeMinRise || rise > maxStepRise)
            {
                continue;
            }

            float verticalOffset = rise + heightProbeContactOffset;
            Vector3 raisedPoint1 = basePoint1 + up * verticalOffset;
            Vector3 raisedPoint2 = basePoint2 + up * verticalOffset;
            if (TryGetHorizontalBlockingHit(
                    raisedPoint1,
                    raisedPoint2,
                    castRadius,
                    direction,
                    horizontalDistance + movementCollisionSkin,
                    blockingMask,
                    out _))
            {
                continue;
            }

            bestSupport = sample;
            bestRise = rise;
            return true;
        }

        return false;
    }

    private bool TryResolveHeightProbeGroundSnap(
        Vector3 basePoint1,
        Vector3 basePoint2,
        float radius,
        Vector3 resolvedDisplacement,
        int blockingMask,
        out Vector3 snappedDisplacement)
    {
        snappedDisplacement = resolvedDisplacement;
        if (!enableHeightProbeGroundSnap || !CanUseHeightProbeTraversal())
        {
            return false;
        }

        Vector3 up = transform.up;
        Vector3 horizontalDisplacement = Vector3.ProjectOnPlane(resolvedDisplacement, up);
        if (horizontalDisplacement.sqrMagnitude <= 0.00000001f)
        {
            return false;
        }

        float existingVertical = Vector3.Dot(resolvedDisplacement, up);
        if (Mathf.Abs(existingVertical) > 0.0001f)
        {
            return false;
        }

        Vector3 currentFootPoint = GetCapsuleFootPoint(basePoint1, basePoint2, radius, up);
        if (!TrySampleGround(
                currentFootPoint,
                up,
                heightProbeVerticalPadding,
                heightProbeVerticalPadding + heightProbeContactOffset + 0.05f,
                GetGroundSupportMask(),
                out GroundProbeSample currentSupport))
        {
            return false;
        }

        Vector3 targetFootPoint = currentFootPoint + horizontalDisplacement;
        if (!TrySampleGround(
                targetFootPoint,
                up,
                GetHeightProbeStepRiseLimit() + heightProbeVerticalPadding,
                GetHeightProbeStepDropLimit() + heightProbeVerticalPadding,
                GetGroundSupportMask(),
                out GroundProbeSample targetSupport))
        {
            return false;
        }

        float supportDelta = Vector3.Dot(targetSupport.point - currentSupport.point, up);
        if (supportDelta >= -heightProbeMinRise)
        {
            return false;
        }

        if (supportDelta < -GetHeightProbeStepDropLimit())
        {
            return false;
        }

        float verticalOffset = Vector3.Dot(
            (targetSupport.point + up * heightProbeContactOffset) - targetFootPoint,
            up);
        if (verticalOffset >= -heightProbeMinRise)
        {
            return false;
        }

        if (Mathf.Abs(verticalOffset) <= 0.0001f)
        {
            return false;
        }

        float castRadius = Mathf.Max(0.01f, radius - movementCollisionSkin);
        Vector3 finalPoint1 = basePoint1 + horizontalDisplacement + up * verticalOffset;
        Vector3 finalPoint2 = basePoint2 + horizontalDisplacement + up * verticalOffset;
        if (!IsCapsulePlacementClear(finalPoint1, finalPoint2, castRadius, blockingMask))
        {
            return false;
        }

        snappedDisplacement = horizontalDisplacement + up * verticalOffset;
        return true;
    }

    private float GetHeightProbeStepRiseLimit()
    {
        if (heightProbeMaxRise <= 0f)
        {
            return 0f;
        }

        return heightProbeMaxStepRise > 0f
            ? Mathf.Min(heightProbeMaxRise, heightProbeMaxStepRise, heightProbeObstacleStepLimit)
            : Mathf.Min(heightProbeMaxRise, heightProbeObstacleStepLimit);
    }

    private float GetHeightProbeStepDropLimit()
    {
        if (heightProbeMaxDrop <= 0f)
        {
            return 0f;
        }

        return heightProbeMaxStepDrop > 0f
            ? Mathf.Min(heightProbeMaxDrop, heightProbeMaxStepDrop)
            : heightProbeMaxDrop;
    }

    private float GetHeightProbeForwardScanDistance()
    {
        return Mathf.Min(heightProbeForwardDistance, heightProbeForwardScanLimit);
    }

    private bool IsCapsulePlacementClear(
        Vector3 point1,
        Vector3 point2,
        float radius,
        int mask)
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
            if (col == null || IsSelfCollider(col))
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
        return bottomCenter - up * radius;
    }
}
