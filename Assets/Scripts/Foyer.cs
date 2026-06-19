using UnityEngine;

[DisallowMultipleComponent]
public sealed class Foyer : MonoBehaviour, ICharacterDetectedInteractable, ILocalInteractHandler
{
    [Header("Interaction")]
    [SerializeField, Tooltip("Collider utilise pour detecter le foyer. Le premier collider enfant est utilise s'il est vide.")]
    private Collider interactionCollider;
    [SerializeField, Tooltip("Point utilise pour classer le foyer parmi les interactions proches.")]
    private Transform interactionAnchor;
    [SerializeField, Min(0.1f), Tooltip("Distance maximale pour s'asseoir ou se relever devant le foyer.")]
    private float interactionMaxDistance = 2.5f;
    [SerializeField, Tooltip("Priorite de selection si plusieurs objets interactifs sont proches.")]
    private int interactionPriority = 80;

    private GameObject detectedCharacter;
    private Collider resolvedInteractionCollider;

    private void Reset()
    {
        ResolveReferences();
        RuntimeOutlineUtility.EnsureOutlineTargets(gameObject);
    }

    private void Awake()
    {
        ResolveReferences();
        RuntimeOutlineUtility.EnsureOutlineTargets(gameObject);
    }

    private void OnDisable()
    {
        detectedCharacter = null;
        if (RuntimeOutlineSelectionManager.IsActiveInteractable(this))
        {
            RuntimeOutlineSelectionManager.Clear();
        }
    }

    private void OnValidate()
    {
        interactionMaxDistance = Mathf.Max(0.1f, interactionMaxDistance);
        ResolveReferences();
    }

    public bool CanBeDetectedBy(SquadCharacterController controller)
    {
        return isActiveAndEnabled &&
               controller != null &&
               controller.CanToggleSitting &&
               GetInteractionDetectionCollider() != null;
    }

    public Collider GetInteractionDetectionCollider()
    {
        if (resolvedInteractionCollider == null)
        {
            ResolveReferences();
        }

        return resolvedInteractionCollider;
    }

    public Transform GetInteractionAnchor()
    {
        return interactionAnchor != null ? interactionAnchor : transform;
    }

    public float GetInteractionMaxDistance(SquadCharacterController controller)
    {
        return interactionMaxDistance;
    }

    public int GetInteractionPriority(SquadCharacterController controller)
    {
        return interactionPriority;
    }

    public void SetDetectedCharacter(GameObject character)
    {
        detectedCharacter = character;
    }

    public bool TryHandleLocalInteract()
    {
        if (!isActiveAndEnabled || detectedCharacter == null)
        {
            return false;
        }

        if (InputFocusStack.HasAnyFocus() ||
            (SquadManager.Instance != null && SquadManager.Instance.IsInputLocked()))
        {
            return true;
        }

        SquadCharacterController controller = detectedCharacter.GetComponentInParent<SquadCharacterController>();
        if (controller == null)
        {
            controller = detectedCharacter.GetComponentInChildren<SquadCharacterController>(true);
        }

        if (controller == null || !controller.CanToggleSitting)
        {
            return true;
        }

        controller.TryToggleSitting();
        return true;
    }

    private void ResolveReferences()
    {
        resolvedInteractionCollider = CharacterInteractionDetection.ResolveInteractionCollider(
            this,
            interactionCollider,
            allowRuntimeFallback: false);

        if (interactionAnchor == null)
        {
            interactionAnchor = transform;
        }
    }
}
