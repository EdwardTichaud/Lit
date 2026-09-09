using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Network-authoritative narrative gate for the Mad Scientist. Before the
/// encounter GhostController owns appearance and interaction; after his line completes he
/// becomes a normal combat actor.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject), typeof(Collider), typeof(GhostController))]
public sealed class ScientistEncounterController : NetworkBehaviour, IGhostInteractionHandler, ICycleCinematicBlocker
{
    private enum EncounterState : byte { Dormant, Dialogue, Active }

    [SerializeField, TextArea] private string introductionLine = "Vous n'auriez jamais dû venir ici...";
    [SerializeField, Min(0.5f)] private float introductionSeconds = 2.5f;
    [SerializeField, Min(0.5f)] private float interactionDistance = 2.5f;
    [SerializeField] private Collider interactionCollider;

    [Header("Defeat")]
    [SerializeField, TextArea] private string deathLine = "Qu'est ce que... j'ai fait...";
    [SerializeField, Min(0.5f)] private float deathDialogueSeconds = 4f;
    [SerializeField] private AudioClipSO deathVoiceLine;
    private AudioSource deathVoiceSource;
    public bool IsDeathPresentationPlaying { get; private set; }
    public bool IsCyclePresentationBlocking => IsDeathPresentationPlaying;

    private readonly NetworkVariable<EncounterState> state = new NetworkVariable<EncounterState>();
    private GhostController ghost;
    private EnemyController brain;
    private EnemyController enemy;
    private EnemyController navigation;
    private Coroutine activationRoutine;
    private bool stateBound;
    private EncounterState localState;

    private bool Online => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool Authority => !Online || IsSpawned && IsServer;
    private EncounterState CurrentState => Online ? state.Value : localState;
    private bool IsDormant => CurrentState == EncounterState.Dormant;

    private void Awake()
    {
        ghost = GetComponent<GhostController>();
        brain = GetComponent<EnemyController>();
        enemy = GetComponent<EnemyController>();
        navigation = GetComponent<EnemyController>();
        if (interactionCollider == null) interactionCollider = GetComponent<Collider>();
        ApplyState(CurrentState);
    }

    private void OnEnable()
    {
        if (IsSpawned) BindState();
        ApplyState(CurrentState);
    }

    private void OnDisable()
    {
        CancelDeathPresentation();
        if (activationRoutine != null) StopCoroutine(activationRoutine);
        activationRoutine = null;
        UnbindState();
        // An interrupted introduction may be started again on reactivation.
        if (Authority && CurrentState == EncounterState.Dialogue)
            SetState(EncounterState.Dormant);
    }

    public override void OnNetworkSpawn()
    {
        if (isActiveAndEnabled) BindState();
        ApplyState(state.Value);
    }

    public override void OnNetworkDespawn()
    {
        CancelDeathPresentation();
        state.OnValueChanged -= OnStateChanged;
        stateBound = false;
        if (activationRoutine != null) StopCoroutine(activationRoutine);
        activationRoutine = null;
        localState = EncounterState.Dormant;
    }

    public bool Interact(GhostController source)
    {
        if (!isActiveAndEnabled || source == null || source != ghost || !source.isActiveAndEnabled ||
            (Online && !IsSpawned) || !IsDormant) return false;
        var player = LocalPlayerUtils.GetControlledCharacter();
        if (player == null || !IsPlayerInRange(player.transform)) return false;
        if (Online) StartEncounterServerRpc();
        else StartEncounter(player.transform);
        return true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void StartEncounterServerRpc(RpcParams rpc = default)
    {
        StartEncounter(NetcodePlayerUtils.GetPlayerTransform(rpc.Receive.SenderClientId));
    }

    private void StartEncounter(Transform player)
    {
        if (!Authority || !IsDormant || !IsPlayerInRange(player)) return;
        var controller = player != null ? player.GetComponentInParent<SquadCharacterController>() : null;
        if (controller == null && player != null) controller = player.GetComponentInChildren<SquadCharacterController>();
        if (controller == null || controller.CurrentHp <= 0 || ghost == null || !ghost.CanBeDetectedBy(controller)) return;
        SetState(EncounterState.Dialogue);
        if (Online && IsSpawned) ShowIntroductionClientRpc();
        else ShowIntroduction();
        activationRoutine = StartCoroutine(ActivateAfterDialogue());
    }

    private IEnumerator ActivateAfterDialogue()
    {
        yield return new WaitForSecondsRealtime(introductionSeconds);
        activationRoutine = null;
        if (!Authority || !isActiveAndEnabled || CurrentState != EncounterState.Dialogue) yield break;
        SetState(EncounterState.Active);
        if (Online && IsSpawned) BeginCombatClientRpc();
        else BeginLocalCombat();
    }

    [ClientRpc] private void ShowIntroductionClientRpc() => ShowIntroduction();
    [ClientRpc] private void BeginCombatClientRpc() => BeginLocalCombat();

    private void ShowIntroduction()
    {
        DialoguePanelUI.TryShowTimedConversation(introductionLine, introductionSeconds, null, this);
    }

    // Each local combat UI plays this presentation before showing its result.
    // Combat outcome/health authority remains with the existing combat system.
    public IEnumerator PlayDeathPresentation()
    {
        if (IsDeathPresentationPlaying) yield break;
        IsDeathPresentationPlaying = true;
        try
        {
            enemy?.PlayDeathAnimation();
            float duration = Mathf.Max(0.5f, deathDialogueSeconds);
            if (deathVoiceLine != null && deathVoiceLine.audioClip != null)
            {
                duration = Mathf.Max(duration, deathVoiceLine.audioClip.length);
                deathVoiceSource = AudioManager.Instance?.PlayUiOneShotClip(deathVoiceLine);
            }
            bool closed = false;
            bool shown = DialoguePanelUI.TryShowTimedConversation(deathLine, duration, _ => closed = true, this);
            if (shown)
            {
                while (this != null && isActiveAndEnabled && !closed) yield return null;
            }
            else
            {
                // Missing UI must not suppress the death animation/voice or block victory.
                yield return new WaitForSecondsRealtime(duration);
            }
        }
        finally
        {
            CancelDeathPresentation();
        }
    }

    public void CancelDeathPresentation()
    {
        if (!IsDeathPresentationPlaying) return;
        IsDeathPresentationPlaying = false;
        DialoguePanelUI.CancelTimedConversation(this);
        if (deathVoiceSource != null) deathVoiceSource.Stop();
        deathVoiceSource = null;
    }

    private void BeginLocalCombat()
    {
        Transform player = LocalPlayerUtils.GetControlledCharacter() != null
            ? LocalPlayerUtils.GetControlledCharacter().transform : null;
        if (player != null && enemy != null) RealTimeCombatManager.Instance?.BeginCombat(player, enemy);
    }

    private void OnStateChanged(EncounterState before, EncounterState after) => ApplyState(after);

    private void BindState()
    {
        if (stateBound) return;
        state.OnValueChanged += OnStateChanged;
        stateBound = true;
    }

    private void UnbindState()
    {
        if (!stateBound) return;
        state.OnValueChanged -= OnStateChanged;
        stateBound = false;
    }

    private void SetState(EncounterState next)
    {
        if (!Authority) return;
        if (Online) state.Value = next;
        else localState = next;
        ApplyState(CurrentState);
    }

    private void ApplyState(EncounterState next)
    {
        bool active = next == EncounterState.Active;
        if (ghost == null) ghost = GetComponent<GhostController>();
        if (ghost != null) ghost.SetGhostMode(next == EncounterState.Dormant);
        if (enemy != null) enemy.CombatEnabled = active;
    }

    private bool IsPlayerInRange(Transform player)
    {
        if (player == null || interactionCollider == null) return false;
        return CharacterInteractionDetection.IsCharacterWithinRange(player, interactionCollider, transform, interactionDistance);
    }
}
