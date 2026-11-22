using UnityEngine;

/// <summary>Move this authored root to the chosen refuge, then confirm placement in the Inspector.</summary>
[RequireComponent(typeof(SphereCollider))]
public sealed class AurellReunionAnchor : MonoBehaviour, ICharacterDetectedInteractable, ILocalInteractHandler, IInteractionLightPolicy
{
    public AurellReunionCycle owner;
    [Tooltip("Activer apres avoir place Aurell au milieu du parcours, sur un sol et hors combat.")]
    public bool placementConfirmed;
    [Min(.1f)] public float interactionDistance = 3f;
    public bool RequiresFlame => false;
    public bool CanBeDetectedBy(SquadCharacterController character) => character != null &&
        character.transform == LocalPlayerContext.LocalCharacterRoot && owner != null && owner.CanMeet;
    public bool TryHandleLocalInteract()
    {
        var root = LocalPlayerContext.LocalCharacterRoot;
        return isActiveAndEnabled && root != null && owner != null &&
            RuntimeOutlineSelectionManager.IsActiveInteractable(this) &&
            CharacterInteractionDetection.IsInteractionTargetVisibleFromCharacter(this, GetInteractionDetectionCollider(), transform,
                root.position + root.up, root) && owner.TryMeet();
    }
    public Collider GetInteractionDetectionCollider() => GetComponent<SphereCollider>();
    public Transform GetInteractionAnchor() => transform;
    public float GetInteractionMaxDistance(SquadCharacterController character) => interactionDistance;
    public int GetInteractionPriority(SquadCharacterController character) => 5;
    public void SetDetectedCharacter(GameObject character) { }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = placementConfirmed ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
