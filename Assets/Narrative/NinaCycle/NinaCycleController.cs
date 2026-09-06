using System.Collections;
using System.Collections.Generic;
using Lit.Timeline;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>Server commits milestones; world variables own save/load, NGO owns live replication.</summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class NinaCycleController : NetworkBehaviour
{
    public const int ScientistDefeated = 1, CinematicCompleted = 2, NinaVisited = 4, RewardGranted = 8;
    public NinaCycleDefinition definition;
    public SceneMarker scientistMarker;
    public GhostController nina;
    public GhostController scar;
    public Animator ninaAnimator;
    public GameObject ninaBlood;
    public PlayableDirector director;
    public TimelineBindingProfile bindingProfile;
    public string idleState = "Idle", deadState = "Dead";
    private readonly NetworkVariable<int> replicatedState = new NetworkVariable<int>();
    private WorldRulesStateManager rules;
    private CombatHealth health;
    private TimelinePlaybackHandle playback;
    private SquadCharacterController lockedPlayer;
    private bool ownsLock, attemptedCinematic, cinematicRunning, localDialogue;
    private int previousPose = -1, cinematicToken;
    private double cinematicEarliestFinish;
    private int completedViewers;
    private bool ownsCinematicPriority;
    private readonly HashSet<ulong> viewers = new HashSet<ulong>();
    private readonly List<EnemyCinematicState> suspendedEnemies = new List<EnemyCinematicState>();
    private readonly Dictionary<ulong, PendingInteraction> pending = new Dictionary<ulong, PendingInteraction>();
    private struct PendingInteraction { public bool scar; public double earliest; }
    private bool Online => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool Authority => !Online || IsSpawned && IsServer;
    private int State => rules != null && definition != null && rules.TryGetInt(definition.StateKey, out int value) ? value : 0;
    private bool Knows(KnowledgeSO knowledge) => knowledge != null && KnowledgeManager.Instance != null && KnowledgeManager.Instance.HasKnowledge(knowledge);

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
        CancelPlayback();
    }
    private void ResolveRules()
    {
        if (rules == null) rules = FindAnyObjectByType<WorldRulesStateManager>();
    }
    private void Update()
    {
        ResolveRules();
        if (rules == null || definition == null) return;
        if (Online && !IsSpawned) return;
        if (Authority)
        {
            if (IsSpawned && replicatedState.Value != State) replicatedState.Value = State;
            BindScientist();
            if ((State & CinematicCompleted) != 0 && definition.existence != null && !Knows(definition.existence))
                KnowledgeReveal.Reveal(definition.existence, "Le groupe", definition.cycleId);
            if ((State & ScientistDefeated) != 0 && (State & CinematicCompleted) == 0 && !attemptedCinematic)
            {
                attemptedCinematic = true;
                StartCoroutine(DeathSequence());
            }
        }
        bool dead = Knows(definition.dilemma);
        if (ninaBlood != null && ninaBlood.activeSelf != dead) ninaBlood.SetActive(dead);
        if (ninaAnimator != null && ninaAnimator.isActiveAndEnabled && previousPose != (dead ? 1 : 0))
        {
            string pose = dead ? deadState : idleState;
            if (ninaAnimator.HasState(0, Animator.StringToHash(pose)))
            {
                ninaAnimator.CrossFade(pose, .15f, 0);
                previousPose = dead ? 1 : 0;
            }
        }
        if (scar != null && scar.gameObject.activeSelf != ((State & NinaVisited) != 0))
            scar.gameObject.SetActive((State & NinaVisited) != 0);
    }
    private void BindScientist()
    {
        var actor = scientistMarker != null ? scientistMarker.BakedCharacterInstance : null;
        var next = actor != null ? actor.GetComponentInChildren<CombatHealth>(true) : null;
        if (next != health)
        {
            if (health != null) health.HealthChanged -= OnHealthChanged;
            health = next;
            if (health != null) health.HealthChanged += OnHealthChanged;
        }
        if (health == null) return;
        if ((State & ScientistDefeated) != 0)
        {
            if (!health.IsDead) health.ForceDefeat();
        }
        else if (health.IsDead) Commit(ScientistDefeated);
    }
    private void OnHealthChanged(CombatHealth changed)
    {
        if (Authority && changed.IsDead) Commit(ScientistDefeated);
    }
    private void Commit(int flag)
    {
        if (!Authority || rules == null || definition == null) return;
        ApplyState(State | flag);
        if (IsSpawned) replicatedState.Value = State;
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
        if (!HasCinematic())
        {
            Debug.LogWarning("[NinaCycle] Cinématique à assigner : progression conservée en attente.", this);
            yield break;
        }
        cinematicRunning = true;
        foreach (var enemyState in FindObjectsByType<EnemyCinematicState>())
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
    [ServerRpc(RequireOwnership = false)]
    private void CinematicResultServerRpc(int token, bool success, ServerRpcParams rpc = default)
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
        Commit(CinematicCompleted);
        KnowledgeReveal.Reveal(definition.existence, "Le groupe", definition.cycleId);
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
    public bool Interact(GhostController ghost, bool isScar)
    {
        if (definition == null || ghost == null || localDialogue || cinematicRunning) return false;
        bool dead = Knows(definition.dilemma);
        string text = isScar ? ((State & RewardGranted) != 0 ? "Souviens-toi de Nina." : definition.scarLine) : dead ? definition.deadLine : definition.idleLine;
        if (isScar && (State & NinaVisited) == 0) return false;
        double started = Time.realtimeSinceStartupAsDouble;
        if (Online)
        {
            if (!IsSpawned) return false;
            BeginInteractionServerRpc(isScar);
        }
        else BeginInteraction(0, isScar, LocalPlayerUtils.GetControlledCharacter());
        localDialogue = true;
        bool shown = DialoguePanelUI.TryShowTimedConversation(text, definition.dialogueSeconds, completed =>
        {
            localDialogue = false;
            if (!completed || this == null || !isActiveAndEnabled || Time.realtimeSinceStartupAsDouble - started < definition.dialogueSeconds - .05f) return;
            if (Online && IsSpawned) CompleteInteractionServerRpc(isScar);
            else CompleteInteraction(0, isScar, LocalPlayerUtils.GetControlledCharacter());
        }, this);
        if (!shown) localDialogue = false;
        return shown;
    }
    private bool Eligible(bool isScar, GameObject player)
    {
        GhostController ghost = isScar ? scar : nina;
        var controller = player != null ? player.GetComponentInParent<SquadCharacterController>() : null;
        return ghost != null && ghost.isActiveAndEnabled && controller != null && controller.CurrentHp > 0 &&
            Vector3.Distance(controller.transform.position, ghost.transform.position) <= ghost.GetInteractionMaxDistance(controller) + .5f &&
            (isScar ? (State & NinaVisited) != 0 : CanVisitNina(State, Knows(definition.dilemma), Knows(definition.existence)));
    }
    private void BeginInteraction(ulong id, bool isScar, GameObject player)
    {
        pending.Remove(id);
        if (Eligible(isScar, player)) pending[id] = new PendingInteraction { scar = isScar, earliest = Time.realtimeSinceStartupAsDouble + definition.dialogueSeconds - .1f };
    }
    private void CompleteInteraction(ulong id, bool isScar, GameObject player)
    {
        if (!pending.TryGetValue(id, out var request)) return;
        pending.Remove(id);
        if (request.scar != isScar || Time.realtimeSinceStartupAsDouble < request.earliest ||
            Time.realtimeSinceStartupAsDouble > request.earliest + 10d || !Eligible(isScar, player)) return;
        if (isScar && definition.cicatrice == null) { Debug.LogWarning("[NinaCycle] Skill Cicatrice à assigner.", this); return; }
        bool first = isScar && (State & RewardGranted) == 0;
        Commit(isScar ? RewardGranted : NinaVisited);
        if (first)
        {
            if (Online) RewardClientRpc();
            else InfoBoxUI.TryShow("Compétence débloquée : Cicatrice", 2f);
        }
    }
    [ServerRpc(RequireOwnership = false)] private void BeginInteractionServerRpc(bool isScar, ServerRpcParams rpc = default)
    {
        var root = NetcodePlayerUtils.GetPlayerTransform(rpc.Receive.SenderClientId);
        BeginInteraction(rpc.Receive.SenderClientId, isScar, root != null ? root.gameObject : null);
    }
    [ServerRpc(RequireOwnership = false)] private void CompleteInteractionServerRpc(bool isScar, ServerRpcParams rpc = default)
    {
        var root = NetcodePlayerUtils.GetPlayerTransform(rpc.Receive.SenderClientId);
        CompleteInteraction(rpc.Receive.SenderClientId, isScar, root != null ? root.gameObject : null);
    }
    [ClientRpc] private void RewardClientRpc() => InfoBoxUI.TryShow("Compétence débloquée : Cicatrice", 2f);
    private void OnDisable()
    {
        cinematicRunning = false;
        DialoguePanelUI.CancelTimedConversation(this);
        ReleaseEnemies();
        CancelPlayback();
        StopAllCoroutines();
        pending.Clear();
        localDialogue = false;
        if (health != null) health.HealthChanged -= OnHealthChanged;
        health = null;
    }

    private void ReleaseEnemies()
    {
        foreach (var enemyState in suspendedEnemies) if (enemyState != null) enemyState.SetSuspended(false);
        suspendedEnemies.Clear();
    }

    public static bool CanVisitNina(int state, bool dilemmaKnown, bool existenceKnown) =>
        (state & CinematicCompleted) != 0 && dilemmaKnown && existenceKnown;
}
