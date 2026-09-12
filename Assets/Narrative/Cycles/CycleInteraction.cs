using UnityEngine;

/// <summary>Interaction adapter for Ghost, NPC or object. All rewards are validated by the cycle authority.</summary>
[DisallowMultipleComponent]
public sealed class CycleInteraction : MonoBehaviour, IGhostInteractionHandler, ICharacterDetectedInteractable, ILocalInteractHandler
{
    [Tooltip("Cycle proprietaire de cette interaction.")] public CycleController cycle;
    [Tooltip("ID du dialogue, ou source d'une etape Interaction sans dialogue.")] public string dialogueId;
    [SerializeField, Tooltip("Point utilise pour un PNJ ou objet. Vide utilise ce Transform ; un Ghost utilise ses propres reglages.")]
    private Transform interactionAnchor;
    [SerializeField, Min(0), Tooltip("Distance maximale en metres pour un PNJ ou objet ; zero exige le contact au point.")]
    private float interactionDistance = 3f;
    [SerializeField, Tooltip("Collider de detection pour un PNJ ou objet. Vide cherche le collider sur cet objet.")]
    private Collider interactionCollider;
    private GhostController ghost;
    public GhostController Ghost => ghost != null ? ghost : ghost = GetComponent<GhostController>();
    public bool Interact(GhostController actor) => actor == Ghost && TryInteract();
    // Callable by the existing NPC/object interaction system; never grants a milestone directly.
    public bool TryInteract() => isActiveAndEnabled && cycle != null && cycle.Interact(this);
    internal bool IsWithinRange(GameObject player)
    {
        if (!isActiveAndEnabled || player == null) return false;
        var character = player.GetComponentInParent<SquadCharacterController>();
        if (character == null) character = player.GetComponentInChildren<SquadCharacterController>();
        if (character == null || character.CurrentHp <= 0) return false;
        if (Ghost != null)
            return Ghost.isActiveAndEnabled && CharacterInteractionDetection.IsCharacterWithinRange(character.transform,
                Ghost.GetInteractionDetectionCollider(), Ghost.GetInteractionAnchor(), Ghost.GetInteractionMaxDistance(character) + .5f);
        return CharacterInteractionDetection.IsCharacterWithinRange(character.transform, GetInteractionDetectionCollider(), GetInteractionAnchor(), interactionDistance);
    }
    public bool TryHandleLocalInteract() => Ghost == null && TryInteract();
    public bool CanBeDetectedBy(SquadCharacterController character) => Ghost == null && isActiveAndEnabled && character != null &&
        cycle != null && cycle.Status != CycleStatus.Completed && cycle.Status != CycleStatus.Unavailable;
    public Collider GetInteractionDetectionCollider() => interactionCollider != null ? interactionCollider : interactionCollider = GetComponent<Collider>();
    public Transform GetInteractionAnchor() => interactionAnchor != null ? interactionAnchor : transform;
    public float GetInteractionMaxDistance(SquadCharacterController character) => interactionDistance;
    public int GetInteractionPriority(SquadCharacterController character) => 5;
    public void SetDetectedCharacter(GameObject character) { }
}
