using UnityEngine;

/// <summary>
/// Rend une inscription de decor consultable sans la ramasser. Son texte est
/// affiche uniquement dans la boite de dialogue du joueur qui interagit.
/// </summary>
[DisallowMultipleComponent]
public sealed class StabReadableInteractable : MonoBehaviour, ICharacterDetectedInteractable, ILocalInteractHandler
{
    [SerializeField, Tooltip("Item configure avec Readable Kind = Stab.")]
    private Item readableItem;
    [SerializeField, Tooltip("Collider utilise pour la detection. Laisse vide pour chercher dans les enfants.")]
    private Collider interactionCollider;
    [SerializeField, Min(0.1f)] private float interactionMaxDistance = 2f;
    [SerializeField] private int interactionPriority = 105;

    private GameObject detectedCharacter;

    private void Awake()
    {
        ResolveInteractionCollider();
        RuntimeOutlineUtility.EnsureOutlineTargets(gameObject);
    }

    private void OnValidate()
    {
        interactionMaxDistance = Mathf.Max(0.1f, interactionMaxDistance);
        ResolveInteractionCollider();
    }

    public bool CanBeDetectedBy(SquadCharacterController controller)
    {
        return controller != null && isActiveAndEnabled && readableItem != null &&
            readableItem.IsReadableStab() && GetInteractionDetectionCollider() != null;
    }

    public Collider GetInteractionDetectionCollider() => ResolveInteractionCollider();
    public Transform GetInteractionAnchor() => transform;
    public float GetInteractionMaxDistance(SquadCharacterController controller) => interactionMaxDistance;
    public int GetInteractionPriority(SquadCharacterController controller) => interactionPriority;

    public void SetDetectedCharacter(GameObject character)
    {
        detectedCharacter = character;
    }

    public bool TryHandleLocalInteract()
    {
        if (!isActiveAndEnabled || detectedCharacter == null || readableItem == null ||
            !readableItem.IsReadableStab() || InputFocusStack.HasAnyFocus())
        {
            return false;
        }

        GameObject localCharacter = LocalPlayerUtils.GetControlledCharacter();
        if (localCharacter == null ||
            (localCharacter.transform.root != detectedCharacter.transform.root &&
             !localCharacter.transform.IsChildOf(detectedCharacter.transform) &&
             !detectedCharacter.transform.IsChildOf(localCharacter.transform)))
        {
            return false;
        }

        SquadCharacterController controller = detectedCharacter.GetComponentInParent<SquadCharacterController>();
        if (controller == null || !CharacterInteractionDetection.IsCharacterWithinRange(
                controller.transform, GetInteractionDetectionCollider(), GetInteractionAnchor(), interactionMaxDistance))
        {
            return false;
        }

        KnowledgeReveal.Reveal(readableItem.knowledgeUnlockedOnRead, detectedCharacter, "stab");
        string text = readableItem.GetParchmentText().Trim();
        return !string.IsNullOrWhiteSpace(text) && DialoguePanelUI.TryShow($"« {text} »");
    }

    private Collider ResolveInteractionCollider()
    {
        if (interactionCollider != null)
        {
            return interactionCollider;
        }

        interactionCollider = GetComponentInChildren<Collider>(true);
        if (interactionCollider == null && Application.isPlaying)
        {
            SphereCollider fallback = gameObject.AddComponent<SphereCollider>();
            fallback.isTrigger = true;
            fallback.center = new Vector3(0f, 0.8f, 0f);
            fallback.radius = 1f;
            interactionCollider = fallback;
        }

        return interactionCollider;
    }
}
