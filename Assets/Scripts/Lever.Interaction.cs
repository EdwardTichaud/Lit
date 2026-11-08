using UnityEngine;

public partial class Lever : ICharacterDetectedInteractable, ILocalInteractHandler
{
    [SerializeField, Tooltip("Utilise la selection/outline communs plutot que l'ecoute directe de A. Migrer les instances explicitement.")]
    private bool useSharedInteractionSelection;
    private GameObject detectedCharacter;

    public bool UsesSharedInteractionSelection => useSharedInteractionSelection;

    public bool CanBeDetectedBy(SquadCharacterController controller) =>
        useSharedInteractionSelection && useInteractInput && isActiveAndEnabled &&
        controller != null && controller.CurrentHp > 0 && !(singleUse && wasActivatedOnce) &&
        IsCharacterInRange(controller.transform);

    public Collider GetInteractionDetectionCollider() => interactionTrigger != null ? interactionTrigger : leverCollider;
    public Transform GetInteractionAnchor() => transform;
    public float GetInteractionMaxDistance(SquadCharacterController controller)
    {
        ResolveInteractionCenterAndRadius(out _, out float radius);
        return radius;
    }
    public int GetInteractionPriority(SquadCharacterController controller) => 80;
    public void SetDetectedCharacter(GameObject character) => detectedCharacter = character;

    public bool TryHandleLocalInteract()
    {
        if (!useSharedInteractionSelection || !useInteractInput || !isActiveAndEnabled ||
            detectedCharacter == null || !RuntimeOutlineSelectionManager.IsActiveInteractable(this)) return false;
        // The same cooldown, input gates, range checks and authoritative RPC
        // serve both paths. Only the shared router calls this migrated path.
        ProcessLocalInteraction();
        return true;
    }

    private void ClearSharedInteractionSelection()
    {
        detectedCharacter = null;
        if (useSharedInteractionSelection && RuntimeOutlineSelectionManager.IsActiveInteractable(this))
            RuntimeOutlineSelectionManager.Clear();
    }
}
