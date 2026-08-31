using System.Collections.Generic;
using UnityEngine;

public partial class SquadCharacterController
{
    [Header("Interaction Detection")]
    [SerializeField, Tooltip("Active la detection locale des objets interactifs par le personnage controle.")]
    private bool enableCharacterInteractionDetection = true;
    [SerializeField, Tooltip("LayerMask large utilise pour la detection des candidats interactifs.")]
    private LayerMask interactionDetectionMask = ~0;
    [SerializeField, Tooltip("Distance maximale de recherche des interactions autour du personnage.")]
    private float interactionDetectionRadius = 2.25f;
    [SerializeField, Range(-1f, 1f), Tooltip("Dot minimal devant le personnage pour privilegier les cibles visibles. Plus bas = plus permissif.")]
    private float interactionMinimumForwardDot = -0.35f;
    [SerializeField, Tooltip("Hauteur supplementaire ajoutee au point d'origine de la detection si aucune capsule n'est disponible.")]
    private float interactionDetectionHeightOffset = 0.9f;
    [SerializeField, Tooltip("Exige qu'au moins une partie de l'objet soit visible par la camera et non cachee par un collider.")]
    private bool requireInteractionTargetVisibleByCamera = true;
    [SerializeField, Min(0f), Tooltip("Temps de maintien de la cible courante quand le test camera perd brievement la visibilite.")]
    private float interactionCameraVisibilityGraceSeconds = 0.15f;

    [Header("Munin Reaction")]
    [SerializeField, Tooltip("Fait reagir Munin quand une cible allumable/eteignable est detectee.")]
    private bool enableMuninInteractionReaction = true;
    [SerializeField, Range(0f, 1f), Tooltip("Reaction de proximite quand une flamme est a portee.")]
    private float muninFlameProximityReactionIntensity = 1f;

    private const int InteractionDetectionHitCapacity = 128;

    private readonly Collider[] interactionDetectionHits = new Collider[InteractionDetectionHitCapacity];
    private readonly List<ICharacterDetectedInteractable> interactionDetectionCandidates = new List<ICharacterDetectedInteractable>(16);
    private readonly HashSet<Object> interactionDetectionUniqueTargets = new HashSet<Object>();
    private ICharacterDetectedInteractable currentDetectedInteractable;
    private float currentTargetLastCameraVisibleTime = float.NegativeInfinity;

    public Vector3 GetInteractionOriginWorldPosition()
    {
        if (TryGetLocomotionCapsule(out Vector3 center, out _, out _))
        {
            return center;
        }

        return transform.position + transform.up * Mathf.Max(0f, interactionDetectionHeightOffset);
    }

    public void RefreshLocalInteractionDetectionForExternalLocomotion()
    {
        UpdateLocalInteractionDetection();
    }

    /// <summary>
    /// Removes the current world-interaction selection immediately. Combat uses
    /// this on entry so a nearby ladder, item, or other prompt cannot remain
    /// visible for a frame while the combat HUD takes over.
    /// </summary>
    public void HideLocalInteractionPresentation()
    {
        ClearLocalInteractionTarget();
    }

    private void UpdateLocalInteractionDetection()
    {
        // World interactions must never compete visually with the combat HUD,
        // reaction prompts, or lock-on presentation. Clearing the current
        // target also asks each interactable to hide its own world-space UI.
        if (RealTimeCombatManager.Instance != null && RealTimeCombatManager.Instance.IsCombatActive)
        {
            ClearLocalInteractionTarget();
            return;
        }

        if (!enableCharacterInteractionDetection)
        {
            ClearLocalInteractionTarget();
            return;
        }

        if (!IsLocalControlledCharacter())
        {
            ClearLocalInteractionTarget();
            return;
        }

        if (!isActiveAndEnabled)
        {
            ClearLocalInteractionTarget();
            return;
        }

        if (!CanUseLitInteractionsWithUcc())
        {
            ClearLocalInteractionTarget();
            return;
        }

        Vector3 origin = GetInteractionOriginWorldPosition();
        RefreshInteractionDetectionCandidates(origin);

        ICharacterDetectedInteractable bestTarget = SelectBestInteractionTarget(origin);
        ApplyLocalInteractionTarget(bestTarget);
    }

    private void RefreshInteractionDetectionCandidates(Vector3 origin)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            ResolveInteractionDetectionRadius(),
            interactionDetectionHits,
            interactionDetectionMask,
            QueryTriggerInteraction.Collide);

        interactionDetectionCandidates.Clear();
        interactionDetectionUniqueTargets.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = interactionDetectionHits[i];
            if (hit == null || IsSelfCollider(hit))
            {
                continue;
            }

            ICharacterDetectedInteractable target = CharacterInteractionDetection.ResolveTarget(hit);
            if (!(target is Object unityTarget) || unityTarget == null)
            {
                continue;
            }

            if (!interactionDetectionUniqueTargets.Add(unityTarget))
            {
                continue;
            }

            if (!target.CanBeDetectedBy(this))
            {
                continue;
            }

            interactionDetectionCandidates.Add(target);
        }
    }

    private ICharacterDetectedInteractable SelectBestInteractionTarget(Vector3 origin)
    {
        if (interactionDetectionCandidates.Count == 0)
        {
            return null;
        }

        Vector3 forward = ResolveInteractionForward();
        Camera interactionCamera = requireInteractionTargetVisibleByCamera
            ? CharacterInteractionDetection.ResolveInteractionCamera()
            : null;

        ICharacterDetectedInteractable bestTarget = null;
        float bestDistanceSqr = float.PositiveInfinity;

        for (int i = 0; i < interactionDetectionCandidates.Count; i++)
        {
            ICharacterDetectedInteractable candidate = interactionDetectionCandidates[i];
            if (!TryEvaluateInteractionCandidate(
                    candidate,
                    origin,
                    forward,
                    interactionCamera,
                    out _,
                    out float distanceSqr))
            {
                continue;
            }

            if (bestTarget == null ||
                distanceSqr < bestDistanceSqr ||
                (distanceSqr == bestDistanceSqr &&
                 GetInteractionCandidateTieBreaker(candidate) < GetInteractionCandidateTieBreaker(bestTarget)))
            {
                bestTarget = candidate;
                bestDistanceSqr = distanceSqr;
            }
        }

        return bestTarget;
    }

    private bool TryEvaluateInteractionCandidate(
        ICharacterDetectedInteractable candidate,
        Vector3 origin,
        Vector3 forward,
        Camera interactionCamera,
        out bool usesTriggerZone,
        out float distanceSqr)
    {
        usesTriggerZone = false;
        distanceSqr = float.PositiveInfinity;

        if (!(candidate is Object unityCandidate) || unityCandidate == null)
        {
            return false;
        }

        if (!candidate.CanBeDetectedBy(this))
        {
            return false;
        }

        if (!CanUseLitInteractableWithUcc(candidate))
        {
            return false;
        }

        Collider collider = candidate.GetInteractionDetectionCollider();
        Transform anchor = candidate.GetInteractionAnchor();
        usesTriggerZone = CharacterInteractionDetection.UsesTriggerInteractionZone(candidate);
        // A Flame uses a large trigger volume to grant interaction access. Once
        // the player is inside it, Collider.ClosestPoint returns the player
        // position, which used to make its selection distance exactly zero and
        // let it permanently beat a nearer item. Keep the trigger for range
        // validation, but rank it from its actual visual/interaction anchor.
        Vector3 rangePoint = CharacterInteractionDetection.GetInteractionPoint(
            collider, anchor, origin);
        Vector3 selectionPoint = usesTriggerZone && anchor != null
            ? anchor.position
            : rangePoint;
        Vector3 toPoint = selectionPoint - origin;
        distanceSqr = toPoint.sqrMagnitude;
        float rangeDistance = Vector3.Distance(rangePoint, origin);
        float maxDistance = ResolveInteractionMaxDistance(candidate);
        if (usesTriggerZone)
        {
            bool inTrigger = collider != null &&
                             collider.isTrigger &&
                             CharacterInteractionDetection.IsCharacterInsideInteractionCollider(transform, collider);
            bool inMuninDistance = IsMuninLightInteractionTarget(candidate) &&
                                   CharacterInteractionDetection.IsCharacterWithinRange(transform, collider, anchor, maxDistance);
            if (!inTrigger && !inMuninDistance)
            {
                return false;
            }
        }

        if (!usesTriggerZone && rangeDistance > maxDistance)
        {
            return false;
        }

        Vector3 flatDirection = new Vector3(toPoint.x, 0f, toPoint.z);
        float forwardDot = 1f;
        if (!usesTriggerZone && flatDirection.sqrMagnitude > 0.0001f)
        {
            forwardDot = Vector3.Dot(flatDirection.normalized, forward);
            if (forwardDot < interactionMinimumForwardDot)
            {
                return false;
            }
        }

        if (requireInteractionTargetVisibleByCamera &&
            !IsInteractionCandidateVisibleFromCamera(
                candidate,
                collider,
                anchor,
                interactionCamera))
        {
            return false;
        }

        return true;
    }

    private bool IsInteractionCandidateVisibleFromCamera(
        ICharacterDetectedInteractable candidate,
        Collider collider,
        Transform anchor,
        Camera interactionCamera)
    {
        bool visible = CharacterInteractionDetection.IsInteractionTargetVisibleFromCamera(
            candidate,
            collider,
            anchor,
            interactionCamera,
            transform);
        if (visible)
        {
            if (ReferenceEquals(candidate, currentDetectedInteractable))
            {
                currentTargetLastCameraVisibleTime = Time.unscaledTime;
            }

            return true;
        }

        if (!ReferenceEquals(candidate, currentDetectedInteractable) ||
            interactionCameraVisibilityGraceSeconds <= 0f)
        {
            return false;
        }

        return Time.unscaledTime - currentTargetLastCameraVisibleTime <= interactionCameraVisibilityGraceSeconds;
    }

    private Vector3 ResolveInteractionForward()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return Vector3.forward;
        }

        return forward.normalized;
    }

    private float ResolveInteractionDetectionRadius()
    {
        float radius = Mathf.Max(0.1f, interactionDetectionRadius);
        MuninController munin = ResolveMuninReactionController();
        if (munin != null && munin.OverridesLightSourceDetectionDistance)
        {
            radius = Mathf.Max(radius, munin.MaxLightSourceDetectionDistance);
        }

        return radius;
    }

    private float ResolveInteractionMaxDistance(ICharacterDetectedInteractable candidate)
    {
        float maxDistance = Mathf.Max(0.05f, candidate.GetInteractionMaxDistance(this));
        MuninController munin = ResolveMuninReactionController();
        if (munin != null && munin.TryGetLightSourceDetectionDistance(candidate, out float muninDistance))
        {
            maxDistance = Mathf.Max(maxDistance, muninDistance);
        }

        return maxDistance;
    }

    private static bool IsMuninLightInteractionTarget(ICharacterDetectedInteractable target)
    {
        return target is Flame;
    }

    private static int GetInteractionCandidateTieBreaker(ICharacterDetectedInteractable target)
    {
        return target is Object unityTarget && unityTarget != null
            ? unityTarget.GetHashCode()
            : int.MaxValue;
    }

    private void ApplyLocalInteractionTarget(ICharacterDetectedInteractable target)
    {
        if (ReferenceEquals(currentDetectedInteractable, target))
        {
            return;
        }

        if (currentDetectedInteractable != null)
        {
            currentDetectedInteractable.SetDetectedCharacter(null);
        }

        currentDetectedInteractable = target;
        currentTargetLastCameraVisibleTime = currentDetectedInteractable != null
            ? Time.unscaledTime
            : float.NegativeInfinity;

        if (currentDetectedInteractable != null)
        {
            currentDetectedInteractable.SetDetectedCharacter(gameObject);
        }

        UpdateMuninInteractionReaction(currentDetectedInteractable);
        RuntimeOutlineSelectionManager.SetActiveInteractable(this, currentDetectedInteractable);
    }

    private void ClearLocalInteractionTarget()
    {
        if (currentDetectedInteractable == null)
        {
            currentTargetLastCameraVisibleTime = float.NegativeInfinity;
            UpdateMuninInteractionReaction(null);
            RuntimeOutlineSelectionManager.Clear(this);
            return;
        }

        currentDetectedInteractable.SetDetectedCharacter(null);
        currentDetectedInteractable = null;
        currentTargetLastCameraVisibleTime = float.NegativeInfinity;
        UpdateMuninInteractionReaction(null);
        RuntimeOutlineSelectionManager.Clear(this);
    }

    private void UpdateMuninInteractionReaction(ICharacterDetectedInteractable target)
    {
        MuninController munin = ResolveMuninReactionController();
        if (munin == null)
        {
            return;
        }

        if (!enableMuninInteractionReaction)
        {
            munin.ClearProximityReaction();
            return;
        }

        float intensity = ResolveMuninReactionIntensity(target);
        if (intensity <= 0f)
        {
            munin.ClearProximityReaction();
            return;
        }

        munin.SetProximityReaction(intensity);
    }

    private float ResolveMuninReactionIntensity(ICharacterDetectedInteractable target)
    {
        if (target == null)
        {
            return 0f;
        }

        if (target is Flame)
        {
            return muninFlameProximityReactionIntensity;
        }

        return 0f;
    }

    private MuninController ResolveMuninReactionController()
    {
        return GetComponentInChildren<MuninController>(true);
    }

    private bool IsLocalControlledCharacter()
    {
        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        if (controlled == null)
        {
            return false;
        }

        Transform controlledTransform = controlled.transform;
        return controlledTransform == transform
            || controlledTransform.IsChildOf(transform)
            || transform.IsChildOf(controlledTransform);
    }
}
