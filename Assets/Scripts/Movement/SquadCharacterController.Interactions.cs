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
    [SerializeField, Tooltip("Bonus de score applique a la cible deja selectionnee pour limiter le clignotement.")]
    private float interactionCurrentTargetBonus = 0.15f;

    private readonly Collider[] interactionDetectionHits = new Collider[32];
    private readonly List<ICharacterDetectedInteractable> interactionDetectionCandidates = new List<ICharacterDetectedInteractable>(16);
    private readonly HashSet<Object> interactionDetectionUniqueTargets = new HashSet<Object>();
    private ICharacterDetectedInteractable currentDetectedInteractable;

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

        Vector3 origin = GetInteractionOriginWorldPosition();
        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            Mathf.Max(0.1f, interactionDetectionRadius),
            interactionDetectionHits,
            interactionDetectionMask,
            QueryTriggerInteraction.Ignore);

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

        ICharacterDetectedInteractable bestTarget = SelectBestInteractionTarget(origin);
        ApplyLocalInteractionTarget(bestTarget);
    }

    private ICharacterDetectedInteractable SelectBestInteractionTarget(Vector3 origin)
    {
        if (interactionDetectionCandidates.Count == 0)
        {
            return null;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        else
        {
            forward.Normalize();
        }

        ICharacterDetectedInteractable best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < interactionDetectionCandidates.Count; i++)
        {
            ICharacterDetectedInteractable candidate = interactionDetectionCandidates[i];
            if (!(candidate is Object unityCandidate) || unityCandidate == null)
            {
                continue;
            }

            Collider collider = candidate.GetInteractionDetectionCollider();
            Transform anchor = candidate.GetInteractionAnchor();
            Vector3 point = CharacterInteractionDetection.GetInteractionPoint(collider, anchor, origin);
            Vector3 toPoint = point - origin;
            float distance = toPoint.magnitude;
            float maxDistance = Mathf.Max(0.05f, candidate.GetInteractionMaxDistance(this));
            if (distance > maxDistance)
            {
                continue;
            }

            Vector3 flatDirection = new Vector3(toPoint.x, 0f, toPoint.z);
            float forwardDot = 1f;
            if (flatDirection.sqrMagnitude > 0.0001f)
            {
                forwardDot = Vector3.Dot(flatDirection.normalized, forward);
                if (forwardDot < interactionMinimumForwardDot)
                {
                    continue;
                }
            }

            float normalizedDistance = distance / maxDistance;
            float score = candidate.GetInteractionPriority(this) * 1000f
                - normalizedDistance * 100f
                + forwardDot * 10f;

            if (candidate == currentDetectedInteractable)
            {
                score += interactionCurrentTargetBonus * 100f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
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
    }

    private void ClearLocalInteractionTarget()
    {
        if (currentDetectedInteractable == null)
        {
            return;
        }

        currentDetectedInteractable.SetDetectedCharacter(null);
        currentDetectedInteractable = null;
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
