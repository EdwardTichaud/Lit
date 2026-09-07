using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Network-authoritative narrative gate for the Mad Scientist. Before the
/// encounter he is an ordinary world interaction; after his line completes he
/// becomes a normal combat actor.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject), typeof(Collider))]
public sealed class ScientistEncounterController : NetworkBehaviour, ICharacterDetectedInteractable
{
    private enum EncounterState : byte { Dormant, Dialogue, Active }

    [SerializeField, TextArea] private string introductionLine = "Vous n'auriez jamais dû venir ici...";
    [SerializeField, Min(0.5f)] private float introductionSeconds = 2.5f;
    [SerializeField, Min(0.5f)] private float interactionDistance = 2.5f;
    [SerializeField] private int interactionPriority = 95;
    [SerializeField] private Collider interactionCollider;

    private readonly NetworkVariable<EncounterState> state = new NetworkVariable<EncounterState>();
    private EnemyCombatBrain brain;
    private RealTimeCombatEnemy enemy;
    private EnemyNavigationController navigation;
    private Coroutine activationRoutine;
    private bool inputBound;

    private bool Online => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool Authority => !Online || IsSpawned && IsServer;
    private bool IsDormant => state.Value == EncounterState.Dormant;

    private void Awake()
    {
        brain = GetComponent<EnemyCombatBrain>();
        enemy = GetComponent<RealTimeCombatEnemy>();
        navigation = GetComponent<EnemyNavigationController>();
        if (interactionCollider == null) interactionCollider = GetComponent<Collider>();
        ApplyState(state.Value);
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteract;
        inputBound = true;
    }

    private void OnDisable()
    {
        if (inputBound) LocalInputRouter.Interact -= OnInteract;
        inputBound = false;
        if (activationRoutine != null) StopCoroutine(activationRoutine);
        activationRoutine = null;
    }

    public override void OnNetworkSpawn()
    {
        state.OnValueChanged += OnStateChanged;
        ApplyState(state.Value);
    }

    public override void OnNetworkDespawn()
    {
        state.OnValueChanged -= OnStateChanged;
    }

    public bool CanBeDetectedBy(SquadCharacterController controller) =>
        IsDormant && controller != null && controller.CurrentHp > 0 && isActiveAndEnabled;

    public Collider GetInteractionDetectionCollider() => interactionCollider;
    public Transform GetInteractionAnchor() => transform;
    public float GetInteractionMaxDistance(SquadCharacterController controller) => interactionDistance;
    public int GetInteractionPriority(SquadCharacterController controller) => interactionPriority;
    public void SetDetectedCharacter(GameObject character) { }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (!IsDormant || LocalInputRouter.IsInteractConsumed || InputFocusStack.HasAnyFocus() ||
            !RuntimeOutlineSelectionManager.IsActiveInteractable(this))
        {
            return;
        }

        if (!LocalInputRouter.TryConsumeInteract()) return;
        if (Online && IsSpawned) StartEncounterServerRpc();
        else StartEncounter(LocalPlayerUtils.GetControlledCharacter() != null
            ? LocalPlayerUtils.GetControlledCharacter().transform : null);
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartEncounterServerRpc(ServerRpcParams rpc = default)
    {
        StartEncounter(NetcodePlayerUtils.GetPlayerTransform(rpc.Receive.SenderClientId));
    }

    private void StartEncounter(Transform player)
    {
        if (!Authority || !IsDormant || !IsPlayerInRange(player)) return;
        state.Value = EncounterState.Dialogue;
        ApplyState(state.Value);
        if (Online && IsSpawned) ShowIntroductionClientRpc();
        else ShowIntroduction();
        activationRoutine = StartCoroutine(ActivateAfterDialogue());
    }

    private IEnumerator ActivateAfterDialogue()
    {
        yield return new WaitForSecondsRealtime(introductionSeconds);
        if (!Authority || state.Value != EncounterState.Dialogue) yield break;
        state.Value = EncounterState.Active;
        ApplyState(state.Value);
        if (Online && IsSpawned) BeginCombatClientRpc();
        else BeginLocalCombat();
    }

    [ClientRpc] private void ShowIntroductionClientRpc() => ShowIntroduction();
    [ClientRpc] private void BeginCombatClientRpc() => BeginLocalCombat();

    private void ShowIntroduction()
    {
        DialoguePanelUI.TryShowTimedConversation(introductionLine, introductionSeconds, null, this);
    }

    private void BeginLocalCombat()
    {
        Transform player = LocalPlayerUtils.GetControlledCharacter() != null
            ? LocalPlayerUtils.GetControlledCharacter().transform : null;
        if (player != null && enemy != null) RealTimeCombatManager.Instance?.BeginCombat(player, enemy);
    }

    private void OnStateChanged(EncounterState before, EncounterState after) => ApplyState(after);

    private void ApplyState(EncounterState next)
    {
        bool active = next == EncounterState.Active;
        if (brain != null) brain.enabled = active;
        if (navigation != null) navigation.enabled = active;
        if (enemy != null) enemy.enabled = active;
        if (!active && RuntimeOutlineSelectionManager.IsActiveInteractable(this))
            RuntimeOutlineSelectionManager.Clear();
    }

    private bool IsPlayerInRange(Transform player)
    {
        if (player == null || interactionCollider == null) return false;
        return CharacterInteractionDetection.IsCharacterWithinRange(player, interactionCollider, transform, interactionDistance);
    }
}
