using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public interface IInteractionLightPolicy { bool RequiresFlame { get; } }

[DisallowMultipleComponent, RequireComponent(typeof(Collider))]
public sealed class WorldActionInteractable : MonoBehaviour, ICharacterDetectedInteractable, ILocalInteractHandler, IInteractionLightPolicy
{
    [Serializable] public sealed class PlayerAction : UnityEvent<SquadCharacterController> { }
    public Collider detectionCollider;
    public Transform interactionAnchor;
    [Min(.1f)] public float reach = 2f;
    public bool requiresFlame = true;
    public bool soloOnly = true;
    public PlayerAction onInteract = new PlayerAction();
    private GameObject detected;
    private int activationFrame = -1;
    public bool RequiresFlame => requiresFlame;
    public bool CanBeDetectedBy(SquadCharacterController actor) => isActiveAndEnabled && actor != null && actor.CurrentHp > 0 &&
        actor.IsGrounded && !actor.IsMovementInputSuppressed && !actor.IsFlightActive &&
        (!soloOnly || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening);
    public Collider GetInteractionDetectionCollider() => detectionCollider != null ? detectionCollider : GetComponent<Collider>();
    public Transform GetInteractionAnchor() => interactionAnchor != null ? interactionAnchor : transform;
    public float GetInteractionMaxDistance(SquadCharacterController actor) => reach;
    public int GetInteractionPriority(SquadCharacterController actor) => 115;
    public void SetDetectedCharacter(GameObject actor) => detected = actor;
    public bool TryHandleLocalInteract()
    {
        var actor = detected != null ? detected.GetComponent<SquadCharacterController>() : null;
        var collider = GetInteractionDetectionCollider();
        if (activationFrame == Time.frameCount || actor == null || actor.gameObject != LocalPlayerUtils.GetControlledCharacter() ||
            InputFocusStack.HasAnyFocus() || InputModeCoordinator.CurrentMode != InputMode.Exploration ||
            GameFlowService.Instance?.IsTransitioning == true || !CanBeDetectedBy(actor) ||
            !RuntimeOutlineSelectionManager.IsActiveInteractable(this) ||
            !CharacterInteractionDetection.IsInActiveFlameInfluence(this) ||
            !CharacterInteractionDetection.IsCharacterWithinRange(actor.transform, collider, GetInteractionAnchor(), reach) ||
            !CharacterInteractionDetection.IsInteractionTargetVisibleFromCharacter(this, collider, GetInteractionAnchor(),
                actor.transform.position + actor.transform.up, actor.transform)) return false;
        activationFrame = Time.frameCount;
        onInteract.Invoke(actor);
        return true;
    }
    private void OnDisable() { detected = null; activationFrame = -1; }
}
