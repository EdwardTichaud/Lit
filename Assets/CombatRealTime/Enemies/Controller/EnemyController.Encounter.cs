using System.Collections;
using Unity.Netcode;
using UnityEngine;

public sealed partial class EnemyController : IGhostInteractionHandler, ICycleCinematicBlocker
{
    private enum EncounterState : byte { Dormant, Dialogue, Active }

    private EnemyEncounterOptions EncounterOptions => Health != null && Health.CharacterData != null
        ? Health.CharacterData.enemyEncounterOptions : null;
    private EnemyDeathOptions DeathOptions => Health != null && Health.CharacterData != null
        ? Health.CharacterData.enemyDeathOptions : null;
    public bool StartsAsGhost => EncounterOptions != null && EncounterOptions.startAsGhost;
    public bool HasDeathPresentation => DeathOptions != null && DeathOptions.enabled;
    private Collider interactionCollider;
    private AudioSource deathVoiceSource;
    public bool IsDeathPresentationPlaying { get; private set; }
    public bool IsCyclePresentationBlocking => IsDeathPresentationPlaying;

    private readonly NetworkVariable<EncounterState> state = new NetworkVariable<EncounterState>();
    private GhostController ghost;
    private Coroutine activationRoutine;
    private bool stateBound;
    private EncounterState localState;

    private bool Online => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool Authority => !Online || IsSpawned && IsServer;
    private EncounterState CurrentState => Online ? state.Value : localState;
    private bool IsDormant => CurrentState == EncounterState.Dormant;

    private void EncounterAwake()
    {
        ghost = GetComponent<GhostController>();
        if (interactionCollider == null) interactionCollider = GetComponent<Collider>();
        ApplyState(CurrentState);
    }

    private void EncounterOnEnable()
    {
        DeathOptionsOnEnable();
        if (IsSpawned) BindState();
        ApplyState(CurrentState);
    }

    private void EncounterOnDisable()
    {
        if (Health != null) Health.HealthChanged -= OnDeathOptionsHealthChanged;
        DialoguePanelUI.CancelTimedConversation(this);
        CancelDeathPresentation();
        if (activationRoutine != null) StopCoroutine(activationRoutine);
        activationRoutine = null;
        UnbindState();
        // An interrupted introduction may be started again on reactivation.
        if (Authority && CurrentState == EncounterState.Dialogue)
            SetState(EncounterState.Dormant);
    }

    private void EncounterOnNetworkSpawn()
    {
        if (isActiveAndEnabled) BindState();
        ApplyState(state.Value);
    }

    private void EncounterOnNetworkDespawn()
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
        if (!StartsAsGhost || !isActiveAndEnabled || source == null || source != ghost || !source.isActiveAndEnabled ||
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
        if (!StartsAsGhost || !Authority || !IsDormant || !IsPlayerInRange(player)) return;
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
        yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, EncounterOptions.introductionSeconds));
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
        DialoguePanelUI.TryShowTimedConversation(EncounterOptions.introductionLine, Mathf.Max(0.5f, EncounterOptions.introductionSeconds), null, this);
    }

    // Health starts one local presentation; the combat UI only waits for it.
    // Combat outcome/health authority remains with the existing combat system.
    private Coroutine deathPresentationRoutine;
    private bool deathPresentationStarted;

    private void DeathOptionsOnEnable()
    {
        if (Health == null) return;
        Health.HealthChanged -= OnDeathOptionsHealthChanged;
        Health.HealthChanged += OnDeathOptionsHealthChanged;
        // A restored corpse must not replay its last words on scene load.
        deathPresentationStarted = Health.IsDead;
    }

    private void OnDeathOptionsHealthChanged(CharacterInfo health)
    {
        if (!health.IsDead) { CancelDeathPresentation(); deathPresentationStarted = false; return; }
        EnsureDeathPresentation();
    }

    private void EnsureDeathPresentation()
    {
        if (!isActiveAndEnabled || Health == null || !Health.IsDead || !HasDeathPresentation || deathPresentationStarted) return;
        deathPresentationStarted = true;
        deathPresentationRoutine = StartCoroutine(RunDeathPresentation());
    }

    public IEnumerator PlayDeathPresentation()
    {
        EnsureDeathPresentation();
        while (IsDeathPresentationPlaying) yield return null;
    }

    private IEnumerator RunDeathPresentation()
    {
        IsDeathPresentationPlaying = true;
        try
        {
            PlayDeathAnimation();
            float duration = Mathf.Max(0.5f, DeathOptions.dialogueSeconds);
            if (DeathOptions.voiceLine != null && DeathOptions.voiceLine.audioClip != null)
            {
                duration = Mathf.Max(duration, DeathOptions.voiceLine.audioClip.length);
                deathVoiceSource = AudioManager.Instance?.PlayUiOneShotClip(DeathOptions.voiceLine);
            }
            bool closed = false;
            bool shown = DialoguePanelUI.TryShowTimedConversation(DeathOptions.dialogueLine, duration, _ => closed = true, this);
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
            FinishDeathPresentation();
        }
    }

    public void CancelDeathPresentation()
    {
        var routine = deathPresentationRoutine;
        deathPresentationRoutine = null;
        if (routine != null) StopCoroutine(routine);
        FinishDeathPresentation();
    }

    private void FinishDeathPresentation()
    {
        deathPresentationRoutine = null;
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
        if (player != null) RealTimeCombatManager.Instance?.BeginCombat(player, this);
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
        if (!StartsAsGhost) return;
        bool active = next == EncounterState.Active;
        if (ghost == null) ghost = GetComponent<GhostController>();
        if (ghost != null) ghost.SetGhostMode(next == EncounterState.Dormant);
        CombatEnabled = active;
    }

    private bool IsPlayerInRange(Transform player)
    {
        if (player == null || interactionCollider == null) return false;
        return CharacterInteractionDetection.IsCharacterWithinRange(player, interactionCollider, transform, EncounterOptions.interactionDistance);
    }
}
