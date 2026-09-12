using System;
using System.Collections;
using System.Collections.Generic;
using Lit.Timeline;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>Server commits milestones; world variables own save/load, NGO owns live replication.</summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class CycleController : NetworkBehaviour
{
    public CycleDefinition definition;
    public SceneMarker encounterMarker;
    [Tooltip("Ennemi de la rencontre lorsque la scene utilise directement un EnemyController sans SceneMarker.")]
    public EnemyController encounterEnemy;
    public CycleInteraction[] interactions = Array.Empty<CycleInteraction>();
    public CycleActivationBinding[] activations = Array.Empty<CycleActivationBinding>();
    public CyclePoseBinding[] poses = Array.Empty<CyclePoseBinding>();
    public PlayableDirector director;
    public TimelineBindingProfile bindingProfile;
    private CycleProgressionService progression;
    private bool presentationDirty = true;
    private bool sequencePending;
    private float nextBindingCheck;
    [Tooltip("Ennemis de cette scene a suspendre pendant ses sequences ; vide utilise seulement la rencontre liee.")]
    public EnemyController[] cinematicParticipants = Array.Empty<EnemyController>();
    public CycleEncounterBinding[] encounters = Array.Empty<CycleEncounterBinding>();
    public CycleSequenceBinding[] sequences = Array.Empty<CycleSequenceBinding>();
    private readonly HashSet<string> attemptedSequences = new HashSet<string>();
    private CycleSequenceBinding activeSequence;
    private string activeSequenceId = "cinematic";
    private PlayableDirector ActiveDirector => activeSequence != null ? activeSequence.director : director;
    private TimelineBindingProfile ActiveProfile => activeSequence != null ? activeSequence.profile : bindingProfile;
    public CycleStatus Status => progression != null ? progression.GetStatus(definition) : CycleStatus.Unavailable;
    private bool Completed => progression != null ? progression.IsCompleted(definition) : definition != null && definition.IsCompleted(State);
    private WorldRulesStateManager rules;
    private CharacterInfo health;
    private TimelinePlaybackHandle playback;
    private SquadCharacterController lockedPlayer;
    private bool ownsLock, attemptedCinematic, cinematicRunning, localDialogue;
    private int cinematicToken, dialogueToken;
    private readonly Dictionary<ulong, PendingDialogue> pendingDialogues = new Dictionary<ulong, PendingDialogue>();
    private struct PendingDialogue { public string id; public int token; public double earliest; }
    private double cinematicEarliestFinish;
    private int completedViewers;
    private int localPlaybackGeneration;
    private readonly HashSet<ulong> completionReadyClients = new HashSet<ulong>();
    private bool completionReadySent, unloadStarted;
    private double completionStartedAt = -1;
    private bool completionCancelled;
    private readonly HashSet<ulong> viewers = new HashSet<ulong>();
    private readonly List<EnemyController> suspendedEnemies = new List<EnemyController>();
    private readonly HashSet<CycleInteraction> presentedInteractions = new HashSet<CycleInteraction>();
    private bool Online => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool Authority => !Online || IsSpawned && IsServer;
    private int State => rules != null && definition != null && rules.TryGetInt(definition.StateKey, out int value) ? value : 0;
    private bool Knows(KnowledgeSO knowledge) => knowledge != null && KnowledgeManager.Instance != null && KnowledgeManager.Instance.HasKnowledge(knowledge);
    public bool Evaluate(CycleCondition condition) => condition != null && condition.Matches(State, Knows) &&
        (progression == null || progression.Matches(definition, condition.requirements));
    private bool HasFlags(int flags) => flags != 0 && (State & flags) == flags;

    private void OnEnable() { presentationDirty = true; ResolveRules(); }
    public override void OnNetworkSpawn() { ResolveRules(); presentationDirty = true; }
    public override void OnNetworkDespawn() => OnDisable();
    private void ResolveRules()
    {
        if (rules == null) rules = FindAnyObjectByType<WorldRulesStateManager>();
        if (progression == null && rules != null && Application.isPlaying)
        {
            progression = CycleProgressionService.Ensure(rules);
            progression.Register(this);
            progression.Changed += OnProgressionChanged;
            presentationDirty = true;
        }
    }
    private void OnProgressionChanged() => presentationDirty = true;
    private void Update()
    {
        if (Time.unscaledTime >= nextBindingCheck)
        {
            nextBindingCheck = Time.unscaledTime + .5f;
            ResolveRules();
            if (Authority && definition != null) { BindEncounter(); BindAdditionalEncounters(); }
        }
        if (rules == null || definition == null || string.IsNullOrWhiteSpace(definition.cycleId)) return;
        if (Online && (!IsSpawned || !Authority && (progression == null || !progression.IsReady))) return;
        if (presentationDirty)
        {
            presentationDirty = false;
            if (Authority)
            {
                RevealKnowledgeAfterDefeat();
                TryStartSequence();
            }
            ApplyPresentation();
        }
        UpdateCompletedScene();
    }

    private void BindAdditionalEncounters()
    {
        foreach (var binding in encounters ?? Array.Empty<CycleEncounterBinding>())
        {
            if (binding == null) continue;
            var next = binding.ResolveHealth();
            if (next != binding.health)
            {
                if (binding.health != null && binding.callback != null) binding.health.HealthChanged -= binding.callback;
                binding.health = next;
                binding.callback = changed => { if (Authority && changed.IsDead) Report(CycleStepKind.EnemyDefeated, binding.id); };
                if (next != null) next.HealthChanged += binding.callback;
            }
            if (next != null && !next.IsDead && progression != null && progression.HasDefeatFact(definition, binding.id)) next.ForceDefeat();
            if (next != null && next.IsDead) Report(CycleStepKind.EnemyDefeated, binding.id);
        }
    }
    private void Report(CycleStepKind kind, string id)
    {
        if (definition != null && definition.HasSteps && progression != null) progression.Report(this, kind, id);
        presentationDirty = true;
    }
    private void TryStartSequence()
    {
        if (Completed || cinematicRunning || sequencePending) return;
        if (definition.HasSteps)
        {
            foreach (var step in definition.steps)
            {
                if (step == null || step.kind != CycleStepKind.SequenceCompleted || progression == null ||
                    !progression.IsStepActive(definition, step) || attemptedSequences.Contains(step.sourceId)) continue;
                activeSequence = Array.Find(sequences, item => item != null && item.id == step.sourceId);
                activeSequenceId = step.sourceId;
                if (activeSequence == null && step.sourceId != "cinematic")
                { Debug.LogWarning("[Cycle] Liaison de sequence absente : " + step.sourceId, this); attemptedSequences.Add(step.sourceId); continue; }
                attemptedSequences.Add(step.sourceId);
                sequencePending = true;
                StartCoroutine(DeathSequence());
                return;
            }
        }
        else if (definition.playCinematicAfterDefeat && HasFlags(definition.enemyDefeatedFlags) &&
                 !HasFlags(definition.cinematicCompletedFlags) && !attemptedCinematic)
        {
            attemptedCinematic = true;
            sequencePending = true;
            StartCoroutine(DeathSequence());
        }
    }

    private bool CompletionPresentationReady()
    {
        if (localDialogue || cinematicRunning || (playback != null && !playback.IsDone) || EncounterPresentationIsBlocking()) return false;
        if (interactions != null) foreach (var interaction in interactions)
        {
            if (interaction == null || interaction.cycle != this || interaction.Ghost == null) continue;
            var dialogue = definition.FindDialogue(interaction.dialogueId);
            if (dialogue != null && dialogue.disappearAfterCompletion && IsDialogueCompleted(dialogue) &&
                !interaction.Ghost.IsDialogueDisappearanceComplete) return false;
        }
        return true;
    }

    private void UpdateCompletedScene()
    {
        if (!Completed)
        {
            completionReadyClients.Clear(); completionReadySent = false;
            completionStartedAt = -1; completionCancelled = false;
            return;
        }
        if (completionStartedAt < 0) completionStartedAt = Time.realtimeSinceStartupAsDouble;
        if (unloadStarted) return;
        bool ready = CompletionPresentationReady();
        if (Online && !IsServer)
        {
            if (ready && !completionReadySent) { completionReadySent = true; CompletionReadyServerRpc(); }
            return;
        }
        if (Online) foreach (ulong client in NetworkManager.ConnectedClientsIds)
            if (client != NetworkManager.LocalClientId && !completionReadyClients.Contains(client)) ready = false;
        bool timedOut = Time.realtimeSinceStartupAsDouble - completionStartedAt >= Math.Max(1f, definition.completionPresentationTimeout);
        if (!ready && !timedOut) return;
        if (!ready && !completionCancelled)
        {
            completionCancelled = true;
            Debug.LogWarning("[Cycle] Delai de presentation depasse : annulation des presentations de " + definition.cycleId, this);
            if (Online) CancelCompletionClientRpc();
            CancelOwnedPresentation();
            return; // Send cancellation before the scene unload request.
        }
        if (GameFlowService.Instance != null)
            unloadStarted = GameFlowService.Instance.TryUnloadCompletedCycleScene(definition, gameObject.scene);
    }
    private void CancelOwnedPresentation()
    {
        cinematicRunning = false;
        pendingDialogues.Clear();
        DialoguePanelUI.CancelTimedConversation(this);
        CancelPlayback();
        ReleaseEnemies();
        StopAllCoroutines();
        localDialogue = false;
    }
    [ClientRpc] private void CancelCompletionClientRpc() => CancelOwnedPresentation();

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void CompletionReadyServerRpc(RpcParams rpc = default)
    {
        if (Authority && definition != null && Completed)
            completionReadyClients.Add(rpc.Receive.SenderClientId);
    }
    private bool IsDialogueCompleted(CycleDialogue dialogue)
    {
        if (dialogue.HasCompleted(State)) return true;
        if (progression == null || definition == null || !definition.HasSteps) return false;
        foreach (var step in definition.steps)
            if (step != null && step.kind == CycleStepKind.DialogueCompleted && step.sourceId == dialogue.id &&
                progression.IsStepCompleted(definition, step.id)) return true;
        return false;
    }
    private void ApplyPresentation()
    {
        if (interactions != null) foreach (var interaction in interactions)
        {
            if (interaction == null || interaction.cycle != this) continue;
            var dialogue = definition != null ? definition.FindDialogue(interaction.dialogueId) : null;
            if (dialogue == null || !dialogue.disappearAfterCompletion || interaction.Ghost == null) continue;
            bool firstPresentation = presentedInteractions.Add(interaction);
            // Loaded/late-join milestones hide immediately; a live completion plays its delay once.
            interaction.Ghost.SetDialogueCompletion(IsDialogueCompleted(dialogue), dialogue.disappearanceDelay, restoreImmediately: firstPresentation);
        }
        if (activations != null) foreach (var binding in activations)
        {
            if (binding == null || binding.target == null || binding.target == gameObject) continue;
            bool visible = Evaluate(binding.condition);
            if (visible && interactions != null) foreach (var interaction in interactions)
                if (interaction != null && interaction.gameObject == binding.target && interaction.Ghost != null &&
                    interaction.Ghost.IsDialogueDisappearanceComplete) { visible = false; break; }
            if (binding.target.activeSelf != visible) binding.target.SetActive(visible);
        }
        if (poses != null) foreach (var binding in poses)
        {
            if (binding == null || binding.animator == null || !binding.animator.isActiveAndEnabled) continue;
            binding.Apply(Evaluate(binding.condition));
        }
    }
    private void BindEncounter()
    {
        var next = ResolveEncounterHealth();
        if (next != health)
        {
            if (health != null) health.HealthChanged -= OnHealthChanged;
            health = next;
            if (health != null) health.HealthChanged += OnHealthChanged;
        }
        if (health == null) return;
        if (definition.HasSteps && progression != null ? progression.HasDefeatFact(definition, "encounter") : HasFlags(definition.enemyDefeatedFlags))
        {
            if (!health.IsDead) health.ForceDefeat();
        }
        else if (health.IsDead)
        {
            if (definition.HasSteps) Report(CycleStepKind.EnemyDefeated, "encounter");
            else Commit(definition.enemyDefeatedFlags);
        }
    }
    private CharacterInfo ResolveEncounterHealth()
    {
        // A spawned/replaced instance is authoritative, not the hidden baked copy.
        var actor = encounterMarker != null
            ? encounterMarker.RuntimeInstance != null ? encounterMarker.RuntimeInstance : encounterMarker.BakedCharacterInstance
            : null;
        if (actor != null) return actor.GetComponentInChildren<CharacterInfo>(true);
        return encounterEnemy != null ? encounterEnemy.Health : null;
    }
    private void OnHealthChanged(CharacterInfo changed)
    {
        if (Authority && definition != null && changed.IsDead)
        {
            if (definition.HasSteps) Report(CycleStepKind.EnemyDefeated, "encounter");
            else Commit(definition.enemyDefeatedFlags);
        }
    }
    private void Commit(int flag)
    {
        ResolveRules();
        if (!Authority || rules == null || definition == null || string.IsNullOrWhiteSpace(definition.cycleId)) return;
        if (flag == 0 || (State & flag) == flag) return;
        ApplyState(State | flag);
        presentationDirty = true;
        RevealKnowledgeAfterDefeat();
    }
    private void RevealKnowledgeAfterDefeat()
    {
        if (!Authority || definition == null || !HasFlags(definition.enemyDefeatedFlags) || definition.knowledgeOnEnemyDefeat == null) return;
        foreach (var knowledge in definition.knowledgeOnEnemyDefeat)
            if (knowledge != null && !Knows(knowledge)) KnowledgeReveal.Reveal(knowledge, "Le groupe", definition.cycleId);
    }
    private void ApplyState(int state)
    {
        ResolveRules();
        if (rules != null && definition != null) rules.SetInt(definition.StateKey, state);
    }
    private IEnumerator DeathSequence()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, definition.deathDelay));
        // Wait for encounter dialogue/results before taking cinematic focus.
        while (EncounterPresentationIsBlocking() ||
               (RealTimeCombatManager.Instance != null && RealTimeCombatManager.Instance.IsCinematicSequenceActive) ||
               (DialoguePanelUI.Instance != null && DialoguePanelUI.Instance.IsShowing) ||
               (RealTimeCombatSceneUiController.Instance != null && RealTimeCombatSceneUiController.Instance.IsResultVisible))
            yield return null;
        sequencePending = false;
        if (Completed) yield break;
        if (!HasCinematic())
        {
            Debug.LogWarning("[Cycle] CinÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©matique ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â  assigner : progression conservÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©e en attente.", this);
            yield break;
        }
        cinematicRunning = true;
        var participants = cinematicParticipants != null && cinematicParticipants.Length > 0
            ? cinematicParticipants : new[] { encounterEnemy };
        foreach (EnemyController enemyState in participants)
            if (enemyState != null && !enemyState.IsSuspended)
            {
                suspendedEnemies.Add(enemyState);
                enemyState.SetSuspended(true);
            }
        cinematicEarliestFinish = Time.realtimeSinceStartupAsDouble + ActiveDirector.playableAsset.duration;
        completedViewers = 0;
        int token = ++cinematicToken;
        viewers.Clear();
        if (Online)
        {
            foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds) viewers.Add(id);
            PlayCinematicClientRpc(token, activeSequenceId);
        }
        else StartCoroutine(LocalCinematic(token));
        double timeout = Time.realtimeSinceStartupAsDouble + ActiveDirector.playableAsset.duration + 30d;
        while (cinematicRunning && Time.realtimeSinceStartupAsDouble < timeout)
        {
            if (Online)
            {
                viewers.RemoveWhere(id => !NetworkManager.Singleton.ConnectedClients.ContainsKey(id));
                if (viewers.Count == 0) FinishCinematic(completedViewers > 0);
            }
            yield return null;
        }
        if (cinematicRunning) FinishCinematic(false);
    }
    private bool HasCinematic() => ActiveDirector != null && ActiveDirector.playableAsset != null &&
        ActiveDirector.playableAsset.duration > 0 && !double.IsInfinity(ActiveDirector.playableAsset.duration) &&
        ActiveProfile != null && ActiveProfile.Matches(ActiveDirector.playableAsset);

    [ClientRpc] private void PlayCinematicClientRpc(int token, string id)
    {
        activeSequenceId = id;
        activeSequence = Array.Find(sequences, item => item != null && item.id == id);
        StartCoroutine(LocalCinematic(token));
    }
    private IEnumerator LocalCinematic(int token)
    {
        bool success = false;
        int generation = ++localPlaybackGeneration;
        try
        {
            var root = LocalPlayerUtils.GetControlledCharacter();
            lockedPlayer = root != null ? root.GetComponent<SquadCharacterController>() : null;
            if (lockedPlayer != null) ownsLock = lockedPlayer.TryBeginUccExternalLock();
            if (!HasCinematic() || TimelineManager.Instance == null || lockedPlayer != null && !ownsLock) yield break;
            ActiveDirector.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            playback = TimelineManager.Instance.Play(ActiveDirector, ActiveProfile);
            while (!playback.IsDone) yield return null;
            success = playback.State == TimelinePlaybackState.Completed;
        }
        finally
        {
            if (generation == localPlaybackGeneration) { ReleaseLock(); playback = null; }
            if (Online && IsSpawned) CinematicResultServerRpc(token, success);
            else if (Authority) FinishCinematic(success);
        }
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void CinematicResultServerRpc(int token, bool success, RpcParams rpc = default)
    {
        if (!cinematicRunning || token != cinematicToken || !viewers.Remove(rpc.Receive.SenderClientId)) return;
        success &= Time.realtimeSinceStartupAsDouble >= cinematicEarliestFinish - .1d;
        if (!success) FinishCinematic(false);
        else { completedViewers++; if (viewers.Count == 0) FinishCinematic(true); }
    }
    private void FinishCinematic(bool success)
    {
        if (!Authority || !cinematicRunning) return;
        cinematicRunning = false;
        ReleaseEnemies();
        if (Online && IsSpawned) StopCinematicClientRpc();
        else CancelPlayback();
        if (!success) return;
        if (definition.HasSteps) Report(CycleStepKind.SequenceCompleted, activeSequenceId);
        else Commit(definition.cinematicCompletedFlags);
        presentationDirty = true;
    }
    [ClientRpc] private void StopCinematicClientRpc() => CancelPlayback();
    private void ReleaseLock()
    {
        if (ownsLock && lockedPlayer != null) lockedPlayer.EndUccExternalLock();
        ownsLock = false;
        lockedPlayer = null;
    }
    private void CancelPlayback()
    {
        localPlaybackGeneration++;
        if (playback != null && !playback.IsDone) playback.Stop();
        playback = null;
        ReleaseLock();
    }
    public bool Interact(CycleInteraction interaction)
    {
        ResolveRules();
        if (!isActiveAndEnabled || definition == null || rules == null || localDialogue || cinematicRunning ||
            interaction == null || FindInteraction(interaction.dialogueId) != interaction) return false;
        string id = interaction.dialogueId;
        var dialogue = definition.FindDialogue(id);
        if (dialogue == null)
        {
            if (Completed || !interaction.IsWithinRange(LocalPlayerUtils.GetControlledCharacter())) return false;
            if (Online) { if (!IsSpawned) return false; InteractionServerRpc(id); }
            else Report(CycleStepKind.Interaction, id);
            return true;
        }
        if (Completed || dialogue.disappearAfterCompletion && IsDialogueCompleted(dialogue)) return false;
        bool available = Evaluate(dialogue.condition);
        string text = !available ? dialogue.unavailableLine : dialogue.HasReward(State) && !string.IsNullOrWhiteSpace(dialogue.repeatLine) ? dialogue.repeatLine : dialogue.line;
        if (string.IsNullOrWhiteSpace(text)) return false;
        bool startedOnline = Online;
        var manager = NetworkManager.Singleton;
        if (startedOnline && !IsSpawned) return false;
        int token = ++dialogueToken;
        localDialogue = true;
        bool shown = DialoguePanelUI.TryShowTimedConversation(text, definition.ResolveDialogueSeconds(dialogue), completed =>
        {
            localDialogue = false;
            if (this == null || !isActiveAndEnabled || startedOnline != Online || manager != NetworkManager.Singleton) return;
            if (startedOnline)
            {
                if (IsSpawned) CompleteDialogueServerRpc(id, token, completed);
            }
            else CompleteDialogue(0, id, token, completed, LocalPlayerUtils.GetControlledCharacter());
        }, this);
        if (!shown) { localDialogue = false; return false; }
        if (startedOnline) BeginDialogueServerRpc(id, token);
        else BeginDialogue(0, id, token, LocalPlayerUtils.GetControlledCharacter());
        return true;
    }
    private CycleInteraction FindInteraction(string id)
    {
        CycleInteraction found = null;
        if (interactions != null) foreach (var interaction in interactions)
            if (interaction != null && interaction.cycle == this && interaction.dialogueId == id)
            {
                if (found != null) return null;
                found = interaction;
            }
        return found;
    }
    private bool Eligible(string id, GameObject player)
    {
        var interaction = FindInteraction(id);
        var dialogue = definition != null ? definition.FindDialogue(id) : null;
        return interaction != null && interaction.isActiveAndEnabled && dialogue != null &&
            !Completed && !(dialogue.disappearAfterCompletion && IsDialogueCompleted(dialogue)) && Evaluate(dialogue.condition) &&
            interaction.IsWithinRange(player);
    }
    private void BeginDialogue(ulong client, string id, int token, GameObject player)
    {
        ResolveRules();
        pendingDialogues.Remove(client);
        if (!Authority || rules == null || definition == null || !Eligible(id, player)) return;
        var dialogue = definition.FindDialogue(id);
        pendingDialogues[client] = new PendingDialogue { id = id, token = token,
            earliest = Time.realtimeSinceStartupAsDouble + definition.ResolveDialogueSeconds(dialogue) - .1d };
        ApplyDialogueEffects(dialogue, false);
        ApplyPresentation();
    }
    private void CompleteDialogue(ulong client, string id, int token, bool completed, GameObject player)
    {
        if (!Authority || !pendingDialogues.TryGetValue(client, out var pending) || pending.id != id || pending.token != token) return;
        pendingDialogues.Remove(client);
        if (!completed || Time.realtimeSinceStartupAsDouble < pending.earliest || !Eligible(id, player)) return;
        ApplyDialogueEffects(definition.FindDialogue(id), true);
        ApplyPresentation();
    }
    private void ApplyDialogueEffects(CycleDialogue dialogue, bool completed)
    {
        ResolveRules();
        if (!Authority || rules == null || definition == null || dialogue == null) return;
        if (definition.HasSteps && progression != null)
        {
            Report(completed ? CycleStepKind.DialogueCompleted : CycleStepKind.Interaction, dialogue.id);
            return;
        }
        if (!completed) { Commit(dialogue.openedFlags); return; }
        bool newReward = dialogue.rewardSkill != null && dialogue.rewardFlag > 0 && !dialogue.HasReward(State);
        Commit(dialogue.completedFlags | (newReward ? dialogue.rewardFlag : 0));
        if (!newReward || !dialogue.HasReward(State)) return;
        if (Online) RewardClientRpc(dialogue.id);
        else SkillUnlockPanel.TryShow(dialogue.rewardSkill);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void InteractionServerRpc(string id, RpcParams rpc = default)
    {
        var interaction = FindInteraction(id);
        var player = NetcodePlayerUtils.GetPlayerTransform(rpc.Receive.SenderClientId);
        if (!Authority || Completed || interaction == null || definition.FindDialogue(id) != null ||
            !interaction.IsWithinRange(player != null ? player.gameObject : null)) return;
        Report(CycleStepKind.Interaction, id);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void BeginDialogueServerRpc(string id, int token, RpcParams rpc = default)
    {
        var root = NetcodePlayerUtils.GetPlayerTransform(rpc.Receive.SenderClientId);
        BeginDialogue(rpc.Receive.SenderClientId, id, token, root != null ? root.gameObject : null);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void CompleteDialogueServerRpc(string id, int token, bool completed, RpcParams rpc = default)
    {
        var root = NetcodePlayerUtils.GetPlayerTransform(rpc.Receive.SenderClientId);
        CompleteDialogue(rpc.Receive.SenderClientId, id, token, completed, root != null ? root.gameObject : null);
    }
    [ClientRpc] private void RewardClientRpc(string id)
    {
        var dialogue = definition != null ? definition.FindDialogue(id) : null;
        if (dialogue != null) SkillUnlockPanel.TryShow(dialogue.rewardSkill);
    }
    private bool EncounterPresentationIsBlocking()
    {
        if (health == null) return false;
        foreach (var behaviour in health.GetComponents<MonoBehaviour>())
            if (behaviour is ICycleCinematicBlocker blocker && blocker.IsCyclePresentationBlocking) return true;
        return false;
    }
    private void OnDisable()
    {
        cinematicRunning = false;
        attemptedCinematic = false;
        sequencePending = false;
        pendingDialogues.Clear();
        completionReadyClients.Clear();
        completionReadySent = unloadStarted = false;
        completionStartedAt = -1;
        completionCancelled = false;
        attemptedSequences.Clear();
        if (progression != null) { progression.Changed -= OnProgressionChanged; progression.Unregister(this); }
        progression = null;
        foreach (var binding in encounters ?? Array.Empty<CycleEncounterBinding>())
            if (binding != null)
            {
                if (binding.health != null && binding.callback != null) binding.health.HealthChanged -= binding.callback;
                binding.health = null;
            }
        presentedInteractions.Clear();
        if (poses != null) foreach (var binding in poses) if (binding != null) binding.previousState = null;
        DialoguePanelUI.CancelTimedConversation(this);
        ReleaseEnemies();
        CancelPlayback();
        StopAllCoroutines();
        localDialogue = false;
        if (health != null) health.HealthChanged -= OnHealthChanged;
        health = null;
    }

    private void ReleaseEnemies()
    {
        foreach (EnemyController enemyState in suspendedEnemies) if (enemyState != null) enemyState.SetSuspended(false);
        suspendedEnemies.Clear();
    }

}

public interface ICycleCinematicBlocker
{
    bool IsCyclePresentationBlocking { get; }
}

[Serializable]
public sealed class CycleActivationBinding
{
    public GameObject target;
    public CycleCondition condition = new CycleCondition();
}

[Serializable]
public sealed class CyclePoseBinding
{
    public Animator animator;
    public CycleCondition condition = new CycleCondition();
    public string defaultState = "Idle", matchedState = "Dead";
    [Tooltip("Booleen Animator optionnel qui suit la condition, par exemple isDead. Laisser vide pour un changement d'etat uniquement.")]
    public string conditionBoolParameter;
    [Min(0)] public int layer;
    [Min(0)] public float crossFade = .15f;
    [NonSerialized] public string previousState;

    public void Apply(bool matched)
    {
        if (animator == null || !animator.isActiveAndEnabled) return;
        if (!string.IsNullOrWhiteSpace(conditionBoolParameter))
            foreach (var parameter in animator.parameters)
                if (parameter.name == conditionBoolParameter && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    // Reassert after a Ghost appearance or an Animator rebind.
                    if (animator.GetBool(parameter.nameHash) != matched) animator.SetBool(parameter.nameHash, matched);
                    break;
                }
        string pose = matched ? matchedState : defaultState;
        if (previousState == pose || string.IsNullOrWhiteSpace(pose) || layer < 0 || layer >= animator.layerCount) return;
        string path = pose.Contains(".") ? pose : animator.GetLayerName(layer) + "." + pose;
        int hash = Animator.StringToHash(path);
        if (!animator.HasState(layer, hash))
        {
            hash = Animator.StringToHash(pose);
            if (!animator.HasState(layer, hash)) return;
        }
        animator.CrossFade(hash, crossFade, layer);
        previousState = pose;
    }
}
