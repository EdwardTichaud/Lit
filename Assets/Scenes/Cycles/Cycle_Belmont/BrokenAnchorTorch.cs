using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>Cible lumineuse propre au puzzle de L'Ancre brisée. Ce n'est pas une Flame.</summary>
[RequireComponent(typeof(SphereCollider), typeof(NetworkObject))]
[DisallowMultipleComponent]
public sealed class BrokenAnchorTorch : NetworkBehaviour, ICharacterDetectedInteractable, ILocalInteractHandler
{
    [SerializeField] private Light torchLight;
    [SerializeField] private GameObject[] activateWhenLit = Array.Empty<GameObject>();
    [SerializeField] private SphereCollider interactionCollider;
    [SerializeField, Min(.1f)] private float interactionDistance = 1.5f;
    [SerializeField] private int interactionPriority = 80;
    [SerializeField, TextArea] private string inspectionLine = "Lucian : Ces flammes semblent différentes des autres.";
    [SerializeField] private bool startsLit;

    private readonly NetworkVariable<bool> netIsLit = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private GameObject detectedCharacter;
    private bool isLit;
    public event Action<BrokenAnchorTorch, bool> StateChanged;
    public bool IsLit => isLit;

    private void Awake()
    {
        if (torchLight == null) torchLight = GetComponentInChildren<Light>(true);
        if (interactionCollider == null) interactionCollider = GetComponent<SphereCollider>();
        ApplyState(startsLit, false);
    }

    public override void OnNetworkSpawn()
    {
        netIsLit.OnValueChanged += OnNetworkLitChanged;
        if (IsServer) { netIsLit.Value = startsLit; ApplyState(startsLit, false); }
        else ApplyState(netIsLit.Value, false);
    }

    public override void OnNetworkDespawn() => netIsLit.OnValueChanged -= OnNetworkLitChanged;

    /// <summary>Appelé uniquement par l'impact autoritaire d'une boule de l'Ancre.</summary>
    public bool TryLight()
    {
        if (isLit || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer)) return false;
        SetLitAuthoritatively(true);
        return true;
    }

    /// <summary>Réapplique l'état déjà validé par la progression après chargement.</summary>
    public void RestoreLitFromCycle()
    {
        if (isLit || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer)) return;
        SetLitAuthoritatively(true);
    }

    private void SetLitAuthoritatively(bool value)
    {
        startsLit = value;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned) netIsLit.Value = value;
        ApplyState(value, true);
    }

    private void OnNetworkLitChanged(bool _, bool value) => ApplyState(value, true);
    private void ApplyState(bool value, bool notify)
    {
        bool changed = isLit != value;
        isLit = value;
        if (torchLight != null) torchLight.enabled = value;
        foreach (GameObject target in activateWhenLit) if (target != null) target.SetActive(value);
        if (notify && changed) StateChanged?.Invoke(this, value);
    }

    public bool CanBeDetectedBy(SquadCharacterController controller) => controller != null && isActiveAndEnabled && interactionCollider != null && interactionCollider.enabled;
    public Collider GetInteractionDetectionCollider() => interactionCollider != null ? interactionCollider : GetComponent<Collider>();
    public Transform GetInteractionAnchor() => torchLight != null ? torchLight.transform : transform;
    public float GetInteractionMaxDistance(SquadCharacterController controller) => interactionDistance;
    public int GetInteractionPriority(SquadCharacterController controller) => interactionPriority;
    public void SetDetectedCharacter(GameObject character) => detectedCharacter = character;

    public bool TryHandleLocalInteract()
    {
        if (!isActiveAndEnabled || detectedCharacter == null || InputFocusStack.HasAnyFocus()) return false;
        if (!CharacterInteractionDetection.IsCharacterWithinRange(detectedCharacter.transform, GetInteractionDetectionCollider(), GetInteractionAnchor(), interactionDistance)) return false;
        return DialoguePanelUI.TryShowNonBlocking(inspectionLine, 3f);
    }
}
