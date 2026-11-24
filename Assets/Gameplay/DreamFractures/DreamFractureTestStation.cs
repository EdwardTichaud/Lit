using System.Linq;
using UnityEngine;

namespace Lit.DreamFractures
{
    [RequireComponent(typeof(Collider))]
    public sealed class DreamFractureTestStation : MonoBehaviour, ICharacterDetectedInteractable, ILocalInteractHandler
    {
        public string stationId;
        public bool InRange(Transform player) => player != null &&
            CharacterInteractionDetection.IsInActiveFlameInfluence(this) &&
            CharacterInteractionDetection.IsCharacterWithinRange(player, GetComponent<Collider>(), transform, 2.2f);
        public bool CanBeDetectedBy(SquadCharacterController character) => character != null &&
            character.CurrentHp > 0 && DreamFractureRunController.Instance != null &&
            DreamFractureRunController.Instance.State.phase == DreamFracturePhase.Running &&
            !DreamFractureRunController.Instance.State.scoredEvents.Contains(stationId) &&
            CharacterInteractionDetection.IsInActiveFlameInfluence(this);
        private MaterialPropertyBlock block;
        private Renderer presentation;
        private void Update()
        {
            var run = DreamFractureRunController.Instance;
            if (run == null) return;
            if (presentation == null) presentation = GetComponent<Renderer>();
            if (presentation == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            presentation.GetPropertyBlock(block);
            block.SetColor("_BaseColor", run.State.scoredEvents.Contains(stationId)
                ? new Color(.15f, .65f, .45f) : new Color(.4f, .55f, .85f));
            presentation.SetPropertyBlock(block);
        }
        public Collider GetInteractionDetectionCollider() => GetComponent<Collider>();
        public Transform GetInteractionAnchor() => transform;
        public float GetInteractionMaxDistance(SquadCharacterController _) => 2;
        public int GetInteractionPriority(SquadCharacterController _) => 10;
        public void SetDetectedCharacter(GameObject _) { }
        public bool TryHandleLocalInteract()
        {
            var controller = DreamFractureRunController.Instance;
            if (controller == null || !InRange(LocalPlayerContext.LocalCharacterRoot)) return false;
            controller.Request("objective", stationId);
            return true;
        }
    }
}
