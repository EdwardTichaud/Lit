using System;
using System.Collections;
using System.Collections.Generic;
using Lit.Timeline;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Playables;

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
    private readonly NetworkVariable<int> replicatedState = new NetworkVariable<int>();
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
    private bool ownsCinematicPriority;
    private readonly HashSet<ulong> viewers = new HashSet<ulong>();
    private readonly List<EnemyController> suspendedEnemies = new List<EnemyController>();
    private bool Online => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool Authority => !Online || IsSpawned && IsServer;
    private int State => rules != null && definition != null && rules.TryGetInt(definition.StateKey, out int value) ? value : 0;
    private bool Knows(KnowledgeSO knowledge) => knowledge != null && KnowledgeManager.Instance != null && KnowledgeManager.Instance.HasKnowledge(knowledge);
    public bool Evaluate(CycleCondition condition) => condition != null && condition.Matches(State, Knows);
    private bool HasFlags(int flags) => flags != 0 && (State & flags) == flags;

    public override void OnNetworkSpawn()
    {
        replicatedState.OnValueChanged += OnStateChanged;
        ResolveRules();
        if (IsServer) replicatedState.Value = State;
        else ApplyState(replicatedState.Value);
    }
    public override void OnNetworkDespawn()
    {
        replicatedState.OnValueChanged -= OnStateChanged;
        OnDisable();
    }
    private void ResolveRules()
    {
        if (rules == null) rules = FindAnyObjectByType<WorldRulesStateManager>();
    }
    private void Update()
    {
        ResolveRules();
        if (rules == null || definition == null || string.IsNullOrWhiteSpace(definition.cycleId)) return;
        if (Online && !IsSpawned) return;
        if (!Authority && State != replicatedState.Value) ApplyState(replicatedState.Value);
        if (Authority)
        {
            if (IsSpawned && replicatedState.Value != State) replicatedState.Value = State;
            BindEncounter();
            RevealKnowledgeAfterDefeat();
            if (definition.playCinematicAfterDefeat && HasFlags(definition.enemyDefeatedFlags) && !HasFlags(definition.cinematicCompletedFlags) && !attemptedCinematic)
            {
                attemptedCinematic = true;
                StartCoroutine(DeathSequence());
            }
        }
        ApplyPresentation();
    }
    private void ApplyPresentation()
    {
        if (activations != null) foreach (var binding in activations)
        {
            if (binding == null || binding.target == null || binding.target == gameObject) continue;
            bool visible = Evaluate(binding.condition);
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
        if (HasFlags(definition.enemyDefeatedFlags))
        {
            if (!health.IsDead) health.ForceDefeat();
        }
        else if (health.IsDead) Commit(definition.enemyDefeatedFlags);
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
        if (Authority && definition != null && changed.IsDead) Commit(definition.enemyDefeatedFlags);
    }
    private void Commit(int flag)
    {
        ResolveRules();
        if (!Authority || rules == null || definition == null || string.IsNullOrWhiteSpace(definition.cycleId)) return;
        if (flag == 0 || (State & flag) == flag) return;
        ApplyState(State | flag);
        if (IsSpawned) replicatedState.Value = State;
        RevealKnowledgeAfterDefeat();
    }
    private void RevealKnowledgeAfterDefeat()
    {
        if (!Authority || definition == null || !HasFlags(definition.enemyDefeatedFlags) || definition.knowledgeOnEnemyDefeat == null) return;
        foreach (var knowledge in definition.knowledgeOnEnemyDefeat)
            if (knowledge != null && !Knows(knowledge)) KnowledgeReveal.Reveal(knowledge, "Le groupe", definition.cycleId);
    }
    private void OnStateChanged(int before, int after) => ApplyState(after);
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
        if (!HasCinematic())
        {
            Debug.LogWarning("[Cycle] Cinématique à assigner : progression conservée en attente.", this);
            yield break;
        }
        cinematicRunning = true;
        foreach (EnemyController enemyState in FindObjectsByType<EnemyController>())
            if (!enemyState.IsSuspended)
            {
                suspendedEnemies.Add(enemyState);
                enemyState.SetSuspended(true);
            }
        cinematicEarliestFinish = Time.realtimeSinceStartupAsDouble + director.playableAsset.duration;
        completedViewers = 0;
        int token = ++cinematicToken;
        viewers.Clear();
        if (Online)
        {
            foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds) viewers.Add(id);
            PlayCinematicClientRpc(token);
        }
        else StartCoroutine(LocalCinematic(token));
        double timeout = Time.realtimeSinceStartupAsDouble + director.playableAsset.duration + 30d;
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
    private bool HasCinematic() => director != null && director.playableAsset != null &&
        director.playableAsset.duration > 0 && !double.IsInfinity(director.playableAsset.duration) &&
        bindingProfile != null && bindingProfile.Matches(director.playableAsset);

    [ClientRpc] private void PlayCinematicClientRpc(int token) => StartCoroutine(LocalCinematic(token));
    private IEnumerator LocalCinematic(int token)
    {
        bool success = false;
        try
        {
            var root = LocalPlayerUtils.GetControlledCharacter();
            lockedPlayer = root != null ? root.GetComponent<SquadCharacterController>() : null;
            if (lockedPlayer != null) ownsLock = lockedPlayer.TryBeginUccExternalLock();
            if (!HasCinematic() || TimelineManager.Instance == null || lockedPlayer != null && !ownsLock) yield break;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            var combat = RealTimeCombatManager.Instance;
            if (combat != null && !combat.IsCinematicSequenceActive)
            {
                combat.SetCinematicSequenceActive(true);
                ownsCinematicPriority = true;
            }
            playback = TimelineManager.Instance.Play(director, bindingProfile);
            while (!playback.IsDone) yield return null;
            success = playback.State == TimelinePlaybackState.Completed;
        }
        finally
        {
            ReleaseLock();
            playback = null;
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
        Commit(definition.cinematicCompletedFlags);
    }
    [ClientRpc] private void StopCinematicClientRpc() => CancelPlayback();
    private void ReleaseLock()
    {
        if (ownsCinematicPriority && RealTimeCombatManager.Instance != null) RealTimeCombatManager.Instance.SetCinematicSequenceActive(false);
        ownsCinematicPriority = false;
        if (ownsLock && lockedPlayer != null) lockedPlayer.EndUccExternalLock();
        ownsLock = false;
        lockedPlayer = null;
    }
    private void CancelPlayback()
    {
        if (playback != null && !playback.IsDone) playback.Stop();
        ReleaseLock();
    }
    public bool Interact(CycleInteraction interaction)
    {
        ResolveRules();
        if (!isActiveAndEnabled || definition == null || rules == null || localDialogue || cinematicRunning ||
            interaction == null || FindInteraction(interaction.dialogueId) != interaction) return false;
        string id = interaction.dialogueId;
        var dialogue = definition.FindDialogue(id);
        if (dialogue == null) return false;
        bool available = Evaluate(dialogue.condition);
        string text = !available ? dialogue.unavailableLine : dialogue.HasReward(State) && !string.IsNullOrWhiteSpace(dialogue.repeatLine) ? dialogue.repeatLine : dialogue.line;
        if (string.IsNullOrWhiteSpace(text)) return false;
        bool startedOnline = Online;
        var manager = NetworkManager.Singleton;
        if (startedOnline && !IsSpawned) return false;
        int token = ++dialogueToken;
        localDialogue = true;
        bool shown = DialoguePanelUI.TryShowTimedConversation(text, definition.dialogueSeconds, completed =>
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
        var ghost = interaction != null ? interaction.Ghost : null;
        var controller = player != null ? player.GetComponentInParent<SquadCharacterController>() : null;
        if (controller == null && player != null) controller = player.GetComponentInChildren<SquadCharacterController>();
        return interaction != null && interaction.isActiveAndEnabled && dialogue != null && Evaluate(dialogue.condition) &&
            ghost != null && ghost.isActiveAndEnabled && controller != null && controller.CurrentHp > 0 &&
            CharacterInteractionDetection.IsCharacterWithinRange(controller.transform,
                ghost.GetInteractionDetectionCollider(), ghost.GetInteractionAnchor(), ghost.GetInteractionMaxDistance(controller) + .5f);
    }
    private void BeginDialogue(ulong client, string id, int token, GameObject player)
    {
        ResolveRules();
        pendingDialogues.Remove(client);
        if (!Authority || rules == null || definition == null || !Eligible(id, player)) return;
        pendingDialogues[client] = new PendingDialogue { id = id, token = token,
            earliest = Time.realtimeSinceStartupAsDouble + Math.Max(0f, definition.dialogueSeconds) - .1d };
        ApplyDialogueEffects(definition.FindDialogue(id), false);
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
        if (!completed) { Commit(dialogue.openedFlags); return; }
        bool newReward = dialogue.rewardSkill != null && dialogue.rewardFlag > 0 && !dialogue.HasReward(State);
        Commit(dialogue.completedFlags | (newReward ? dialogue.rewardFlag : 0));
        if (!newReward || !dialogue.HasReward(State)) return;
        if (Online) RewardClientRpc(dialogue.id);
        else SkillUnlockPanel.TryShow(dialogue.rewardSkill);
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
        pendingDialogues.Clear();
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
