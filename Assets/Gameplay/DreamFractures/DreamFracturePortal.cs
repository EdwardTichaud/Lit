using UnityEngine;

namespace Lit.DreamFractures
{
    [RequireComponent(typeof(Collider))]
    public sealed class DreamFracturePortal : MonoBehaviour, ICharacterDetectedInteractable, ILocalInteractHandler
    {
        [Min(.1f)] public float interactionDistance = 2;
        [Min(.1f)] public float gatheringRadius = 8;
        public bool IsAvailable(Transform player, float range) => player != null &&
            Vector3.Distance(player.position, transform.position) <= range &&
            CharacterInteractionDetection.IsInActiveFlameInfluence(this);
        public bool CanBeDetectedBy(SquadCharacterController character) =>
            character != null && character.CurrentHp > 0 && IsAvailable(character.transform, gatheringRadius);
        public Collider GetInteractionDetectionCollider() => GetComponent<Collider>();
        public Transform GetInteractionAnchor() => transform;
        public float GetInteractionMaxDistance(SquadCharacterController _) => interactionDistance;
        public int GetInteractionPriority(SquadCharacterController _) => 10;
        public void SetDetectedCharacter(GameObject _) { }
        public bool TryHandleLocalInteract()
        {
            var service = DreamFractureRunController.Instance;
            if (service == null || !IsAvailable(LocalPlayerContext.LocalCharacterRoot, interactionDistance + .3f))
                return false;
            service.OpenCatalog();
            return true;
        }
    }
}
