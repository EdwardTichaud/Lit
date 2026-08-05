using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Recupere les penetrations verticales introduites par le root motion,
/// sans modifier le deplacement horizontal.
/// </summary>
[DisallowMultipleComponent]
public sealed class AnimationGroundRecovery : MonoBehaviour
{
    private const int HitCapacity = 12;

    [SerializeField] private Animator animator;
    [SerializeField, Tooltip("Maintient la racine animee a sa hauteur de sol initiale. A activer pour les clips dont le root motion vertical ne doit pas deplacer l'acteur.")]
    private bool lockVerticalRootMotionToGround;
    [SerializeField, Min(0.1f), Tooltip("Hauteur du point de sondage au-dessus de la racine animee.")]
    private float probeHeight = 3f;
    [SerializeField, Min(0.1f), Tooltip("Distance maximale de recherche d'un support sous la racine animee.")]
    private float probeDistance = 8f;
    [SerializeField, Range(0f, 1f), Tooltip("Composante verticale minimale de la normale d'un support valide.")]
    private float minimumGroundNormal = 0.45f;
    [SerializeField, Min(0f), Tooltip("Penetration verticale ignoree afin d'eviter les micro-corrections.")]
    private float penetrationTolerance = 0.03f;
    [SerializeField, Min(0f), Tooltip("Vitesse de rattrapage des petites penetrations.")]
    private float recoverySpeed = 18f;
    [SerializeField, Min(0f), Tooltip("Au-dela de cette penetration, le retour au sol est immediat.")]
    private float immediateRecoveryDistance = 0.5f;
    [SerializeField] private LayerMask groundMask = ~0;

    private readonly RaycastHit[] hits = new RaycastHit[HitCapacity];
    private LitOpsiveLocomotionBridge locomotionBridge;
    private NavMeshAgent navigationAgent;
    private bool hasGroundOffset;
    private bool groundSnapRequested;
    private float groundOffset;

    private Transform AnimatedTransform => animator != null ? animator.transform : transform;

    private void Awake()
    {
        animator = animator != null ? animator : ResolveRootMotionAnimator();
        locomotionBridge = GetComponent<LitOpsiveLocomotionBridge>();
        navigationAgent = GetComponent<NavMeshAgent>();
    }

    /// <summary>
    /// Requests a one-shot vertical snap after the current animation frame.
    /// Intended for the landing event of a root-motion attack.
    /// </summary>
    public void RequestGroundSnap()
    {
        groundSnapRequested = true;
    }

    private void LateUpdate()
    {
        // UCC owns the root transform, collision response, and grounded state.
        // Correcting the same transform here creates a feedback loop with root motion
        // and can cancel abilities such as Jump through an external pose update.
        if (locomotionBridge != null && locomotionBridge.IsDriving)
        {
            return;
        }

        Transform animatedTransform = AnimatedTransform;
        if (!TryFindGround(animatedTransform, out RaycastHit groundHit))
        {
            return;
        }

        if (!hasGroundOffset)
        {
            groundOffset = animatedTransform.position.y - groundHit.point.y;
            hasGroundOffset = true;
            return;
        }

        float targetY = groundHit.point.y + groundOffset;
        if (groundSnapRequested)
        {
            groundSnapRequested = false;
            Vector3 snappedPosition = new Vector3(animatedTransform.position.x, targetY, animatedTransform.position.z);
            ApplyRecoveredPosition(animatedTransform, snappedPosition);
            return;
        }

        float verticalOffset = targetY - animatedTransform.position.y;
        bool requiresCorrection = lockVerticalRootMotionToGround
            ? Mathf.Abs(verticalOffset) > penetrationTolerance
            : verticalOffset > penetrationTolerance;
        if (!requiresCorrection)
        {
            return;
        }

        float recoveredY = lockVerticalRootMotionToGround || Mathf.Abs(verticalOffset) >= immediateRecoveryDistance
            ? targetY
            : Mathf.MoveTowards(animatedTransform.position.y, targetY, recoverySpeed * Time.deltaTime);
        Vector3 recoveredPosition = new Vector3(animatedTransform.position.x, recoveredY, animatedTransform.position.z);
        ApplyRecoveredPosition(animatedTransform, recoveredPosition);
    }

    private bool TryFindGround(Transform animatedTransform, out RaycastHit closestGround)
    {
        Vector3 origin = animatedTransform.position + Vector3.up * probeHeight;
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            hits,
            probeDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        closestGround = default;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];
            Collider collider = hit.collider;
            if (collider == null || collider.transform == transform ||
                collider.transform.IsChildOf(transform) || hit.normal.y < minimumGroundNormal)
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestGround = hit;
            }
        }

        return closestGround.collider != null;
    }

    private Animator ResolveRootMotionAnimator()
    {
        Animator[] animators = GetComponentsInChildren<Animator>(true);
        Animator controllerFallback = null;
        for (int i = 0; i < animators.Length; i++)
        {
            Animator candidate = animators[i];
            if (candidate == null || candidate.runtimeAnimatorController == null)
            {
                continue;
            }

            if (candidate.applyRootMotion)
            {
                return candidate;
            }

            controllerFallback ??= candidate;
        }

        return controllerFallback;
    }

    private void ApplyRecoveredPosition(Transform animatedTransform, Vector3 recoveredPosition)
    {
        if (animatedTransform != transform)
        {
            animatedTransform.position = recoveredPosition;
            return;
        }

        if (locomotionBridge != null && locomotionBridge.SetExternalPositionAndRotation(recoveredPosition, transform.rotation, true))
        {
            return;
        }

        if (navigationAgent != null && navigationAgent.isActiveAndEnabled && navigationAgent.isOnNavMesh)
        {
            navigationAgent.Warp(recoveredPosition);
            return;
        }

        transform.position = recoveredPosition;
    }
}
