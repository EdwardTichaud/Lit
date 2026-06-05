using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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
    [SerializeField, Tooltip("Bonus de score applique a la cible deja selectionnee pour limiter le clignotement.")]
    private float interactionCurrentTargetBonus = 0.15f;
    [SerializeField, Tooltip("Exige qu'au moins une partie de l'objet soit visible par la camera et non cachee par un collider.")]
    private bool requireInteractionTargetVisibleByCamera = true;

    [Header("Munin Reaction")]
    [SerializeField, Tooltip("Fait reagir Munin quand une cible allumable/eteignable est detectee.")]
    private bool enableMuninInteractionReaction = true;
    [SerializeField, Range(0f, 1f), Tooltip("Reaction de proximite quand une torche est a portee.")]
    private float muninTorchProximityReactionIntensity = 0.45f;
    [SerializeField, Range(0f, 1f), Tooltip("Reaction de proximite quand un brasero est a portee.")]
    private float muninBraseroProximityReactionIntensity = 1f;

    private const int InteractionDetectionHitCapacity = 128;

    private readonly Collider[] interactionDetectionHits = new Collider[InteractionDetectionHitCapacity];
    private readonly List<ICharacterDetectedInteractable> interactionDetectionCandidates = new List<ICharacterDetectedInteractable>(16);
    private readonly List<ICharacterDetectedInteractable> switchTargetCandidates = new List<ICharacterDetectedInteractable>(8);
    private readonly HashSet<Object> interactionDetectionUniqueTargets = new HashSet<Object>();
    private ICharacterDetectedInteractable currentDetectedInteractable;
    private ICharacterDetectedInteractable manualSwitchedInteractable;

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

    private void UpdateLocalInteractionDetection()
    {
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
            Mathf.Max(0.1f, interactionDetectionRadius),
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
            manualSwitchedInteractable = null;
            return null;
        }

        Vector3 forward = ResolveInteractionForward();
        Camera interactionCamera = requireInteractionTargetVisibleByCamera
            ? CharacterInteractionDetection.ResolveInteractionCamera()
            : null;

        if (IsSwitchableInteractionTarget(manualSwitchedInteractable) &&
            TryEvaluateInteractionCandidate(manualSwitchedInteractable, origin, forward, interactionCamera, out _, out _))
        {
            return manualSwitchedInteractable;
        }

        manualSwitchedInteractable = null;

        ICharacterDetectedInteractable bestDirect = null;
        ICharacterDetectedInteractable bestTriggerZone = null;
        float bestDirectScore = float.NegativeInfinity;
        float bestTriggerZoneScore = float.NegativeInfinity;

        for (int i = 0; i < interactionDetectionCandidates.Count; i++)
        {
            ICharacterDetectedInteractable candidate = interactionDetectionCandidates[i];
            if (!TryEvaluateInteractionCandidate(candidate, origin, forward, interactionCamera, out bool usesTriggerZone, out float score))
            {
                continue;
            }

            if (ReferenceEquals(candidate, currentDetectedInteractable))
            {
                score += interactionCurrentTargetBonus * 100f;
            }

            if (usesTriggerZone)
            {
                if (score > bestTriggerZoneScore)
                {
                    bestTriggerZoneScore = score;
                    bestTriggerZone = candidate;
                }
            }
            else if (score > bestDirectScore)
            {
                bestDirectScore = score;
                bestDirect = candidate;
            }
        }

        return bestDirect != null ? bestDirect : bestTriggerZone;
    }

    private void OnSwitchTargetPerformed(InputAction.CallbackContext context)
    {
        if (!enableCharacterInteractionDetection ||
            !isActiveAndEnabled ||
            !IsLocalControlledCharacter() ||
            InputFocusStack.HasAnyFocus())
        {
            return;
        }

        if (SquadManager.Instance != null && SquadManager.Instance.IsInputLocked())
        {
            return;
        }

        if (!CanUseLitInteractionsWithUcc())
        {
            ClearLocalInteractionTarget();
            return;
        }

        Vector3 origin = GetInteractionOriginWorldPosition();
        RefreshInteractionDetectionCandidates(origin);

        if (!TrySelectNextSwitchTarget(origin, out ICharacterDetectedInteractable nextTarget))
        {
            return;
        }

        manualSwitchedInteractable = nextTarget;
        ApplyLocalInteractionTarget(nextTarget);
    }

    private bool TrySelectNextSwitchTarget(Vector3 origin, out ICharacterDetectedInteractable nextTarget)
    {
        nextTarget = null;
        switchTargetCandidates.Clear();

        Vector3 forward = ResolveInteractionForward();
        Camera interactionCamera = requireInteractionTargetVisibleByCamera
            ? CharacterInteractionDetection.ResolveInteractionCamera()
            : null;
        for (int i = 0; i < interactionDetectionCandidates.Count; i++)
        {
            ICharacterDetectedInteractable candidate = interactionDetectionCandidates[i];
            if (!IsSwitchableInteractionTarget(candidate))
            {
                continue;
            }

            if (!TryEvaluateInteractionCandidate(candidate, origin, forward, interactionCamera, out _, out _))
            {
                continue;
            }

            switchTargetCandidates.Add(candidate);
        }

        if (switchTargetCandidates.Count == 0)
        {
            manualSwitchedInteractable = null;
            return false;
        }

        switchTargetCandidates.Sort((left, right) => CompareSwitchTargetCandidates(left, right, origin, forward, interactionCamera));

        int currentIndex = FindSwitchTargetCandidateIndex(currentDetectedInteractable);
        if (currentIndex < 0)
        {
            currentIndex = FindSwitchTargetCandidateIndex(manualSwitchedInteractable);
        }

        nextTarget = currentIndex >= 0
            ? switchTargetCandidates[(currentIndex + 1) % switchTargetCandidates.Count]
            : switchTargetCandidates[0];

        return nextTarget != null;
    }

    private int CompareSwitchTargetCandidates(
        ICharacterDetectedInteractable left,
        ICharacterDetectedInteractable right,
        Vector3 origin,
        Vector3 forward,
        Camera interactionCamera)
    {
        TryEvaluateInteractionCandidate(left, origin, forward, interactionCamera, out _, out float leftScore);
        TryEvaluateInteractionCandidate(right, origin, forward, interactionCamera, out _, out float rightScore);

        int scoreComparison = rightScore.CompareTo(leftScore);
        if (scoreComparison != 0)
        {
            return scoreComparison;
        }

        int leftId = left is Object leftObject && leftObject != null ? leftObject.GetInstanceID() : 0;
        int rightId = right is Object rightObject && rightObject != null ? rightObject.GetInstanceID() : 0;
        return leftId.CompareTo(rightId);
    }

    private int FindSwitchTargetCandidateIndex(ICharacterDetectedInteractable target)
    {
        if (target == null)
        {
            return -1;
        }

        for (int i = 0; i < switchTargetCandidates.Count; i++)
        {
            if (ReferenceEquals(switchTargetCandidates[i], target))
            {
                return i;
            }
        }

        return -1;
    }

    private bool TryEvaluateInteractionCandidate(
        ICharacterDetectedInteractable candidate,
        Vector3 origin,
        Vector3 forward,
        Camera interactionCamera,
        out bool usesTriggerZone,
        out float score)
    {
        usesTriggerZone = false;
        score = float.NegativeInfinity;

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
        if (usesTriggerZone &&
            (collider == null ||
            !collider.isTrigger ||
            !CharacterInteractionDetection.IsCharacterInsideInteractionCollider(transform, collider)))
        {
            return false;
        }

        Vector3 point = CharacterInteractionDetection.GetInteractionPoint(collider, anchor, origin);
        Vector3 toPoint = point - origin;
        float distance = toPoint.magnitude;
        float maxDistance = Mathf.Max(0.05f, candidate.GetInteractionMaxDistance(this));
        if (!usesTriggerZone && distance > maxDistance)
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
            !CharacterInteractionDetection.IsInteractionTargetVisibleFromCamera(
                candidate,
                collider,
                anchor,
                interactionCamera,
                transform))
        {
            return false;
        }

        float normalizedDistance = distance / maxDistance;
        score = candidate.GetInteractionPriority(this) * 1000f
            - normalizedDistance * 100f
            + forwardDot * 10f;
        return true;
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

    private static bool IsSwitchableInteractionTarget(ICharacterDetectedInteractable target)
    {
        return target is Torch || target is Brasero;
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

        if (currentDetectedInteractable != null)
        {
            currentDetectedInteractable.SetDetectedCharacter(gameObject);
        }

        UpdateMuninInteractionReaction(currentDetectedInteractable);
        RuntimeOutlineSelectionManager.SetActiveInteractable(this, currentDetectedInteractable);
    }

    private void ClearLocalInteractionTarget()
    {
        manualSwitchedInteractable = null;

        if (currentDetectedInteractable == null)
        {
            UpdateMuninInteractionReaction(null);
            RuntimeOutlineSelectionManager.Clear(this);
            return;
        }

        currentDetectedInteractable.SetDetectedCharacter(null);
        currentDetectedInteractable = null;
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

        if (target is Torch)
        {
            return muninTorchProximityReactionIntensity;
        }

        if (target is Brasero)
        {
            return muninBraseroProximityReactionIntensity;
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
