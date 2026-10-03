using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    [Tooltip("Objets à cacher lorsque leur condition est satisfaite. L'état est réappliqué après chargement ou synchronisation réseau.")]
    public CycleDeactivationBinding[] deactivations = Array.Empty<CycleDeactivationBinding>();
    [Tooltip("Objets qui se dissolvent avant d'etre desactives lorsque leur condition est satisfaite.")]
    public CycleDissolveBinding[] disappearances = Array.Empty<CycleDissolveBinding>();
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
    [Tooltip("Flames suivies par ce cycle. Une Flame deja allumee apres chargement valide immediatement son jalon.")]
    public CycleFlameBinding[] flames = Array.Empty<CycleFlameBinding>();
    [Tooltip("Repliques automatiques de l'histoire. Elles ne demandent pas une nouvelle interaction au joueur.")]
    public CycleAutoDialogueBinding[] autoDialogues = Array.Empty<CycleAutoDialogueBinding>();
    public CycleSequenceBinding[] sequences = Array.Empty<CycleSequenceBinding>();

    [Header("Dev - démarrage de cycle")]
    [SerializeField, Tooltip("Simule les jalons antérieurs à l'étape choisie. Cet état est local, temporaire et n'écrit jamais la sauvegarde.")]
    private bool devStartEnabled;
    [SerializeField, Tooltip("Étape qui reste à accomplir au lancement du test. Les étapes placées avant elle dans la définition sont simulées comme terminées.")]
    private string devStartStepId;
    private readonly HashSet<string> attemptedSequences = new HashSet<string>();
    private CycleSequenceBinding activeSequence;
    private string activeSequenceId = "cinematic";
    private PlayableDirector ActiveDirector => activeSequence != null ? activeSequence.director : director;
    private TimelineBindingProfile ActiveProfile => activeSequence != null ? activeSequence.profile : bindingProfile;
    private WorldRulesStateManager rules;
    private CharacterInfo health;
    private TimelinePlaybackHandle playback;
    private SquadCharacterController lockedPlayer;
    private LitOpsiveLocomotionBridge.ExternalLockHandle playerLock;
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
    private readonly HashSet<CycleDissolveBinding> presentedDisappearances = new HashSet<CycleDissolveBinding>();
    private readonly Dictionary<CycleDissolveBinding, Coroutine> activeDissolves = new Dictionary<CycleDissolveBinding, Coroutine>();
    private readonly HashSet<GhostController> observedPuzzleGhosts = new HashSet<GhostController>();
    private readonly HashSet<CycleAutoDialogueBinding> playedAutoDialogues = new HashSet<CycleAutoDialogueBinding>();
    private readonly HashSet<string> devCompletedSteps = new HashSet<string>();
    private readonly HashSet<string> devFacts = new HashSet<string>();
    private readonly HashSet<KnowledgeSO> devKnowledge = new HashSet<KnowledgeSO>();
    private bool devSimulationInitialized;
    private bool Online => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool Authority => !Online || IsSpawned && IsServer;
    private bool DevSimulationActive
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return devStartEnabled && definition != null && definition.FindStep(devStartStepId) != null;
#else
            return false;
#endif
        }
    }
    public bool IsDevSimulationActive => DevSimulationActive;
    public string DevSimulationStartStepId => devStartStepId;

    /// <summary>Extends knowledge checks while a cycle start simulation is running, without mutating KnowledgeManager.</summary>
    public static bool IsVirtuallyKnownForDevSimulation(KnowledgeSO knowledge)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (knowledge == null) return false;
        CycleController[] controllers = FindObjectsByType<CycleController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CycleController controller in controllers)
            if (controller != null && controller.DevSimulationActive && controller.devKnowledge.Contains(knowledge)) return true;
#endif
        return false;
    }

    public static bool HasVirtualKnowledgeForDevSimulation(KnowledgeManager manager, KnowledgeCategory category)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CycleController[] controllers = FindObjectsByType<CycleController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CycleController controller in controllers)
            if (controller != null && controller.DevSimulationActive && controller.devKnowledge.Any(knowledge =>
                knowledge != null && knowledge.category == category && (manager == null || !manager.HasKnowledge(knowledge)))) return true;
#endif
        return false;
    }

    public static int CountVirtualKnowledgeForDevSimulation(KnowledgeManager manager, KnowledgeCategory category, string tag = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int count = 0;
        CycleController[] controllers = FindObjectsByType<CycleController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        HashSet<KnowledgeSO> counted = new HashSet<KnowledgeSO>();
        foreach (CycleController controller in controllers)
            if (controller != null && controller.DevSimulationActive)
                foreach (KnowledgeSO knowledge in controller.devKnowledge)
                    if (knowledge != null && counted.Add(knowledge) && knowledge.category == category &&
                        (string.IsNullOrWhiteSpace(tag) || knowledge.HasTag(tag)) && (manager == null || !manager.HasKnowledge(knowledge))) count++;
        return count;
#else
        return 0;
#endif
    }

    public static bool HasVirtualKnowledgeWithTagForDevSimulation(KnowledgeManager manager, string tag) =>
        HasVirtualKnowledgeWithTagIgnoringCategory(manager, tag);

    private static bool HasVirtualKnowledgeWithTagIgnoringCategory(KnowledgeManager manager, string tag)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CycleController[] controllers = FindObjectsByType<CycleController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CycleController controller in controllers)
            if (controller != null && controller.DevSimulationActive && controller.devKnowledge.Any(knowledge =>
                knowledge != null && knowledge.HasTag(tag) && (manager == null || !manager.HasKnowledge(knowledge)))) return true;
#endif
        return false;
    }

    public static int CountVirtualKnowledgeWithTagForDevSimulation(KnowledgeManager manager, string tag)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int count = 0;
        HashSet<KnowledgeSO> counted = new HashSet<KnowledgeSO>();
        CycleController[] controllers = FindObjectsByType<CycleController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CycleController controller in controllers)
            if (controller != null && controller.DevSimulationActive)
                foreach (KnowledgeSO knowledge in controller.devKnowledge)
                    if (knowledge != null && counted.Add(knowledge) && knowledge.HasTag(tag) && (manager == null || !manager.HasKnowledge(knowledge))) count++;
        return count;
#else
        return 0;
#endif
    }

    /// <summary>True when this Flame is temporarily driven by a cycle-start simulation.
    /// Persistence providers use this to retain its real saved state while Play Mode is testing a cycle.</summary>
    public static bool IsDevSimulationFlame(Flame flame)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (flame == null) return false;
        CycleController[] controllers = FindObjectsByType<CycleController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CycleController controller in controllers)
        {
            if (controller == null || !controller.DevSimulationActive) continue;
            foreach (CycleFlameBinding binding in controller.flames ?? Array.Empty<CycleFlameBinding>())
                if (binding != null && binding.flame == flame) return true;
        }
#endif
        return false;
    }
    public string ExplainStepForInspector(CycleStep step)
    {
        if (step == null) return string.Empty;
        if (DevSimulationActive)
            return DevIsStepCompleted(definition, step.id) ? "Simulée comme validée" :
                IsStepActive(step) ? "Étape de test active" : "Verrouillée dans la simulation";
        return progression != null ? progression.ExplainBlocked(definition, step) : "État indisponible";
    }
    public CycleStatus Status => DevSimulationActive ? CycleStatus.InProgress : progression != null ? progression.GetStatus(definition) : CycleStatus.Unavailable;
    private bool Completed => !DevSimulationActive && (progression != null ? progression.IsCompleted(definition) : definition != null && definition.IsCompleted(State));
    private int State => DevSimulationActive ? 0 : rules != null && definition != null && rules.TryGetInt(definition.StateKey, out int value) ? value : 0;
    private bool Knows(KnowledgeSO knowledge) => knowledge != null && (DevSimulationActive && devKnowledge.Contains(knowledge) || KnowledgeManager.Instance != null && KnowledgeManager.Instance.HasKnowledge(knowledge));
    public bool Evaluate(CycleCondition condition) => condition != null && condition.Matches(State, Knows) &&
        (DevSimulationActive ? DevMatches(definition, condition.requirements) : progression == null || progression.Matches(definition, condition.requirements));
    private bool HasFlags(int flags) => flags != 0 && (State & flags) == flags;

    private void OnEnable() { devSimulationInitialized = false; presentationDirty = true; ResolveRules(); BindGhostPuzzleEvents(); }
    private void OnValidate() { devSimulationInitialized = false; }
    public override void OnDestroy() { UnbindGhostPuzzleEvents(); base.OnDestroy(); }
    public override void OnNetworkSpawn() { ResolveRules(); presentationDirty = true; }
    public override void OnNetworkDespawn() => OnDisable();
    private void ResolveRules()
    {
        if (rules == null) rules = FindAnyObjectByType<WorldRulesStateManager>();
        if (DevSimulationActive)
        {
            InitializeDevSimulation();
            return;
        }
        if (progression == null && rules != null && Application.isPlaying)
        {
            progression = CycleProgressionService.Ensure(rules);
            progression.Register(this);
            progression.Changed += OnProgressionChanged;
            presentationDirty = true;
        }
    }
    private void OnProgressionChanged() => presentationDirty = true;

    private void InitializeDevSimulation()
    {
        if (devSimulationInitialized || !DevSimulationActive || definition == null) return;
        devSimulationInitialized = true;
        devCompletedSteps.Clear();
        devFacts.Clear();
        devKnowledge.Clear();

        CycleStep[] steps = definition.steps ?? Array.Empty<CycleStep>();
        int startIndex = Array.FindIndex(steps, step => step != null && step.id == devStartStepId);
        for (int index = 0; index < startIndex; index++)
            DevCompleteStep(steps[index]);

        presentationDirty = true;
    }

    private bool DevIsStepCompleted(CycleDefinition cycle, string stepId) =>
        cycle == definition && !string.IsNullOrWhiteSpace(stepId) && devCompletedSteps.Contains(stepId);

    private bool DevHasFact(CycleDefinition cycle, CycleStepKind kind, string sourceId) =>
        cycle == definition && !string.IsNullOrWhiteSpace(sourceId) && devFacts.Contains(DevFactKey(kind, sourceId));

    private static string DevFactKey(CycleStepKind kind, string sourceId) => kind + ":" + sourceId;

    private bool DevMatches(CycleDefinition owner, CycleRequirements requirements)
    {
        if (requirements == null || requirements.conditions == null || requirements.conditions.Length == 0) return true;
        bool any = requirements.mode == CycleRequirementMode.Any;
        foreach (CycleRequirement requirement in requirements.conditions)
        {
            bool matches = requirement != null && (requirement.kind == CycleRequirementKind.Step
                ? DevIsStepCompleted(owner, requirement.stepId)
                : requirement.kind == CycleRequirementKind.Knowledge ? Knows(requirement.knowledge)
                : requirement.cycle != null && (requirement.cycle == definition
                    ? false
                    : progression != null && progression.IsCompleted(requirement.cycle)));
            if (any && matches) return true;
            if (!any && !matches) return false;
        }
        return !any;
    }

    private bool IsStepActive(CycleStep step)
    {
        if (!DevSimulationActive)
            return progression != null && progression.IsStepActive(definition, step);
        // The test state simulates only the past. The real definition remains authoritative
        // for parallel branches such as the three Broken Anchor torches.
        return step != null && !DevIsStepCompleted(definition, step.id) &&
            DevMatches(definition, step.prerequisites);
    }

    private bool HasFact(CycleStepKind kind, string sourceId) => DevSimulationActive
        ? DevHasFact(definition, kind, sourceId)
        : progression != null && progression.HasFact(definition, kind, sourceId);

    private void DevReport(CycleStepKind kind, string sourceId)
    {
        if (definition == null || string.IsNullOrWhiteSpace(sourceId)) return;
        devFacts.Add(DevFactKey(kind, sourceId));
        foreach (CycleStep step in definition.steps ?? Array.Empty<CycleStep>())
            if (step != null && step.kind == kind && step.sourceId == sourceId && IsStepActive(step))
                DevCompleteStep(step);
        presentationDirty = true;
    }

    private void DevCompleteStep(CycleStep step)
    {
        if (step == null || string.IsNullOrWhiteSpace(step.id) || !devCompletedSteps.Add(step.id)) return;
        if (step.kind != CycleStepKind.Knowledge && !string.IsNullOrWhiteSpace(step.sourceId))
            devFacts.Add(DevFactKey(step.kind, step.sourceId));
        if (step.kind == CycleStepKind.Knowledge && step.knowledge != null) devKnowledge.Add(step.knowledge);
        foreach (CycleReward reward in step.rewards ?? Array.Empty<CycleReward>())
            if (reward != null && reward.kind == CycleRewardKind.Knowledge && reward.knowledge != null)
                devKnowledge.Add(reward.knowledge);
    }

    private void Update()
    {
        BindGhostPuzzleEvents();
        if (Time.unscaledTime >= nextBindingCheck)
        {
            nextBindingCheck = Time.unscaledTime + .5f;
            ResolveRules();
            if (Authority && definition != null) { BindEncounter(); BindAdditionalEncounters(); BindFlames(); }
        }
        if (rules == null || definition == null || string.IsNullOrWhiteSpace(definition.cycleId)) return;
        if (Online && !DevSimulationActive && (!IsSpawned || !Authority && (progression == null || !progression.IsReady))) return;
        if (presentationDirty)
        {
            presentationDirty = false;
            if (Authority)
            {
                TryStartAutoDialogue();
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
            if (next != null && !next.IsDead && HasFact(CycleStepKind.EnemyDefeated, binding.id)) next.ForceDefeat();
            if (next != null && next.IsDead) Report(CycleStepKind.EnemyDefeated, binding.id);
        }
    }

    private void BindFlames()
    {
        foreach (var binding in flames ?? Array.Empty<CycleFlameBinding>())
        {
            if (binding == null) continue;
            if (binding.flame != null)
            {
                // The test start point owns the visible state of regular Flames too (notably the Ancient Flame).
                // The persistence layer deliberately ignores these temporary changes.
                if (DevSimulationActive && Authority)
                {
                    bool shouldBeLit = HasFact(CycleStepKind.Interaction, binding.id);
                    if (binding.flame.IsLit != shouldBeLit) binding.flame.SetLit(shouldBeLit);
                }
                if (binding.callback == null)
                {
                    binding.callback = (flame, lit) => { if (Authority && lit) Report(CycleStepKind.Interaction, binding.id); };
                    binding.flame.StateChanged += binding.callback;
                }
                if (binding.flame.IsEffectivelyLit) Report(CycleStepKind.Interaction, binding.id);
                continue;
            }

            BrokenAnchorTorch torch = binding.bossTorch;
            if (torch == null) continue;
            if (binding.bossTorchCallback == null)
            {
                binding.bossTorchCallback = (_, lit) =>
                {
                    if (!Authority || !lit) return;
                    Debug.Log($"[Belmont] Torche '{binding.id}' allumée : rapport du jalon.", this);
                    Report(CycleStepKind.Interaction, binding.id);
                };
                torch.StateChanged += binding.bossTorchCallback;
            }
            if (Authority && HasFact(CycleStepKind.Interaction, binding.id)) torch.RestoreLitFromCycle();
            if (torch.IsLit) Report(CycleStepKind.Interaction, binding.id);
        }
    }

    private void TryStartAutoDialogue()
    {
        if (localDialogue || cinematicRunning || !DevSimulationActive && progression == null) return;
        foreach (var binding in autoDialogues ?? Array.Empty<CycleAutoDialogueBinding>())
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.id) || playedAutoDialogues.Contains(binding) ||
                !Evaluate(binding.condition)) continue;
            var step = definition.FindStep(binding.id);
            if (step == null || step.kind != CycleStepKind.DialogueCompleted || !IsStepActive(step)) continue;
            playedAutoDialogues.Add(binding);
            StartCoroutine(CompleteAutoDialogue(binding));
            return;
        }
    }

    private IEnumerator CompleteAutoDialogue(CycleAutoDialogueBinding binding)
    {
        string line = binding.line;
        if (Online && IsSpawned) PlayAutoDialogueClientRpc(binding.id);
        else DialoguePanelUI.TryShowTimedConversation(line, binding.durationSeconds, _ => { }, this);
        yield return new WaitForSecondsRealtime(Mathf.Max(.1f, binding.durationSeconds));
        if (Authority && binding != null) Report(CycleStepKind.DialogueCompleted, binding.id);
    }

    [ClientRpc]
    private void PlayAutoDialogueClientRpc(string id)
    {
        var binding = Array.Find(autoDialogues ?? Array.Empty<CycleAutoDialogueBinding>(), item => item != null && item.id == id);
        if (binding != null && !string.IsNullOrWhiteSpace(binding.line))
            DialoguePanelUI.TryShowTimedConversation(binding.line, binding.durationSeconds, _ => { }, this);
    }

    private void BindGhostPuzzleEvents()
    {
        foreach (var interaction in interactions ?? Array.Empty<CycleInteraction>())
        {
            if (interaction == null || interaction.cycle != this || !interaction.useGhostPuzzleResolution) continue;
            GhostController ghost = interaction.Ghost;
            if (ghost == null || !observedPuzzleGhosts.Add(ghost)) continue;
            ghost.Understood += OnPuzzleGhostUnderstood;
        }
    }

    private void UnbindGhostPuzzleEvents()
    {
        foreach (GhostController ghost in observedPuzzleGhosts)
            if (ghost != null) ghost.Understood -= OnPuzzleGhostUnderstood;
        observedPuzzleGhosts.Clear();
    }

    private void OnPuzzleGhostUnderstood(GhostController ghost)
    {
        if (!Authority || definition == null || ghost == null) return;
        foreach (var interaction in interactions ?? Array.Empty<CycleInteraction>())
        {
            if (interaction == null || !interaction.useGhostPuzzleResolution || interaction.Ghost != ghost) continue;
            CycleDialogue dialogue = definition.FindDialogue(interaction.dialogueId);
            if (dialogue != null && Evaluate(dialogue.condition))
                Report(CycleStepKind.DialogueCompleted, interaction.dialogueId);
        }
    }
    private void Report(CycleStepKind kind, string id)
    {
        if (DevSimulationActive) DevReport(kind, id);
        else if (definition != null && definition.HasSteps && progression != null) progression.Report(this, kind, id);
        presentationDirty = true;
    }

    /// <summary>Read-only bridge for scene mechanics, including the memory-only Dev state.</summary>
    public bool IsSceneStepCompleted(string stepId)
    {
        ResolveRules();
        return definition != null && (DevSimulationActive ? DevIsStepCompleted(definition, stepId) :
            progression != null && progression.IsStepCompleted(definition, stepId));
    }

    public bool IsSceneEventActive(CycleStepKind kind, string sourceId)
    {
        ResolveRules();
        return isActiveAndEnabled && definition != null && !Completed &&
            Array.Exists(definition.steps ?? Array.Empty<CycleStep>(), step =>
                step != null && step.kind == kind && step.sourceId == sourceId && IsStepActive(step));
    }

    /// <summary>Trusted server-side mechanics only. Remote callers must first validate their player and range.</summary>
    public bool TryReportSceneEvent(CycleStepKind kind, string sourceId)
    {
        if (!Authority || (kind != CycleStepKind.ZoneEntered && kind != CycleStepKind.NamedFact &&
            kind != CycleStepKind.Interaction) || !IsSceneEventActive(kind, sourceId)) return false;
        Report(kind, sourceId);
        return true;
    }

    /// <summary>A failed sequence may be retried explicitly; no success or reward is synthesized.</summary>
    public bool TryRetrySceneSequence(string sourceId)
    {
        if (!Authority || cinematicRunning || sequencePending ||
            !IsSceneEventActive(CycleStepKind.SequenceCompleted, sourceId) ||
            !attemptedSequences.Remove(sourceId)) return false;
        presentationDirty = true;
        return true;
    }
    /// <summary>Explicit retry/start request from an optional rendezvous, with normal progression checks.</summary>
    public bool RequestSceneSequence(string sourceId)
    {
        if (!Authority || cinematicRunning || sequencePending || !IsSceneEventActive(CycleStepKind.SequenceCompleted, sourceId)) return false;
        attemptedSequences.Remove(sourceId);
        presentationDirty = true;
        return true;
    }
    private void TryStartSequence()
    {
        if (Completed || cinematicRunning || sequencePending) return;
        if (definition.HasSteps)
        {
            foreach (var step in definition.steps)
            {
                if (step == null || step.kind != CycleStepKind.SequenceCompleted || !IsStepActive(step) || attemptedSequences.Contains(step.sourceId)) continue;
                activeSequence = Array.Find(sequences, item => item != null && item.id == step.sourceId);
                activeSequenceId = step.sourceId;
                if (activeSequence == null && step.sourceId != "cinematic")
                { Debug.LogWarning("[Cycle] Liaison de sequence absente : " + step.sourceId, this); attemptedSequences.Add(step.sourceId); continue; }
                if (activeSequence != null && activeSequence.startGate != null && !activeSequence.startGate.Ready) continue;
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
        if ((!DevSimulationActive && progression == null) || definition == null || !definition.HasSteps) return false;
        foreach (var step in definition.steps)
            if (step != null && step.kind == CycleStepKind.DialogueCompleted && step.sourceId == dialogue.id &&
                (DevSimulationActive ? DevIsStepCompleted(definition, step.id) : progression != null && progression.IsStepCompleted(definition, step.id))) return true;
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
            bool visible = Evaluate(binding.condition) && (!binding.useHideCondition || !Evaluate(binding.hideWhen));
            if (visible && interactions != null) foreach (var interaction in interactions)
                if (interaction != null && interaction.gameObject == binding.target && interaction.Ghost != null &&
                    interaction.Ghost.IsDialogueDisappearanceComplete) { visible = false; break; }
            if (binding.target.activeSelf != visible) binding.target.SetActive(visible);
        }
        if (deactivations != null) foreach (var binding in deactivations)
        {
            if (binding == null || binding.target == null || binding.target == gameObject) continue;
            bool visible = !Evaluate(binding.condition);
            if (binding.target.activeSelf != visible) binding.target.SetActive(visible);
        }
        ApplyDisappearances();
        if (poses != null) foreach (var binding in poses)
        {
            if (binding == null || binding.animator == null || !binding.animator.isActiveAndEnabled) continue;
            binding.Apply(Evaluate(binding.condition));
        }
    }

    private void ApplyDisappearances()
    {
        foreach (var binding in disappearances ?? Array.Empty<CycleDissolveBinding>())
        {
            if (binding == null || binding.target == null || binding.target == gameObject) continue;
            bool shouldDisappear = Evaluate(binding.condition);
            bool firstPresentation = presentedDisappearances.Add(binding);
            if (!shouldDisappear)
            {
                if (activeDissolves.TryGetValue(binding, out Coroutine running) && running != null) StopCoroutine(running);
                activeDissolves.Remove(binding);
                binding.ApplyStrength(0f);
                if (!binding.target.activeSelf) binding.target.SetActive(true);
                continue;
            }
            if (!binding.target.activeSelf || activeDissolves.ContainsKey(binding)) continue;
            // Une sauvegarde deja resolue ne rejoue pas l'effet devant un joueur qui arrive tard.
            if (firstPresentation) { binding.ApplyStrength(1f); binding.target.SetActive(false); continue; }
            activeDissolves[binding] = StartCoroutine(DissolveAndHide(binding));
        }
    }

    private IEnumerator DissolveAndHide(CycleDissolveBinding binding)
    {
        binding.ApplyStrength(0f);
        float elapsed = 0f;
        float duration = Mathf.Max(.01f, binding.durationSeconds);
        while (elapsed < duration && binding != null && binding.target != null)
        {
            elapsed += Time.unscaledDeltaTime;
            binding.ApplyStrength(Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        if (binding != null && binding.target != null)
        {
            binding.ApplyStrength(1f);
            binding.target.SetActive(false);
        }
        activeDissolves.Remove(binding);
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
        if (definition.HasSteps ? HasFact(CycleStepKind.EnemyDefeated, "encounter") : HasFlags(definition.enemyDefeatedFlags))
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
        if (DevSimulationActive) return;
        ResolveRules();
        if (!Authority || rules == null || definition == null || string.IsNullOrWhiteSpace(definition.cycleId)) return;
        if (flag == 0 || (State & flag) == flag) return;
        ApplyState(State | flag);
        presentationDirty = true;
    }
    private bool HasRecordedEncounterDefeat()
    {
        return HasFlags(definition.enemyDefeatedFlags) ||
            definition.HasSteps && HasFact(CycleStepKind.EnemyDefeated, "encounter");
    }
    private void ApplyState(int state)
    {
        if (DevSimulationActive) return;
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
        ulong[] selectedAudience = null;
        if (activeSequence != null && activeSequence.startGate != null && !activeSequence.startGate.TryConsume(out selectedAudience))
        {
            attemptedSequences.Remove(activeSequenceId);
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
            foreach (ulong id in selectedAudience ?? NetworkManager.Singleton.ConnectedClientsIds.ToArray()) viewers.Add(id);
            PlayCinematicClientRpc(token, activeSequenceId, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = viewers.ToArray() } });
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

    [ClientRpc] private void PlayCinematicClientRpc(int token, string id, ClientRpcParams targets = default)
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
            if (lockedPlayer != null) ownsLock = lockedPlayer.TryBeginUccExternalLock(this, out playerLock);
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
        playerLock?.Dispose();
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
        string text = !available ? dialogue.unavailableLine : IsDialogueCompleted(dialogue) && !string.IsNullOrWhiteSpace(dialogue.repeatLine) ? dialogue.repeatLine : dialogue.line;
        if (string.IsNullOrWhiteSpace(text)) return false;
        bool startedOnline = Online;
        var manager = NetworkManager.Singleton;
        if (startedOnline && !IsSpawned) return false;
        if (startedOnline)
        {
            RequestDialogueServerRpc(id);
            return true;
        }
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
        BeginDialogue(0, id, token, LocalPlayerUtils.GetControlledCharacter());
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
    private void RequestDialogueServerRpc(string id, RpcParams rpc = default)
    {
        var root = NetcodePlayerUtils.GetPlayerTransform(rpc.Receive.SenderClientId);
        int token = ++dialogueToken;
        BeginDialogue(rpc.Receive.SenderClientId, id, token, root != null ? root.gameObject : null);
        if (pendingDialogues.ContainsKey(rpc.Receive.SenderClientId)) OpenDialogueClientRpc(id, token, BuildClientRpcParams(rpc.Receive.SenderClientId));
    }
    [ClientRpc] private void OpenDialogueClientRpc(string id, int token, ClientRpcParams rpcParams = default)
    {
        var dialogue = definition != null ? definition.FindDialogue(id) : null;
        if (dialogue == null || localDialogue) return;
        string text = IsDialogueCompleted(dialogue) && !string.IsNullOrWhiteSpace(dialogue.repeatLine) ? dialogue.repeatLine : dialogue.line;
        if (string.IsNullOrWhiteSpace(text)) return;
        localDialogue = true;
        bool shown = DialoguePanelUI.TryShowTimedConversation(text, definition.ResolveDialogueSeconds(dialogue), completed =>
        {
            localDialogue = false;
            if (this != null && isActiveAndEnabled && IsSpawned) CompleteDialogueServerRpc(id, token, completed);
        }, this);
        if (!shown) localDialogue = false;
    }
    private static ClientRpcParams BuildClientRpcParams(ulong clientId) => new ClientRpcParams
    {
        Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
    };
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
        UnbindGhostPuzzleEvents();
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
        activeDissolves.Clear();
        localDialogue = false;
        if (health != null) health.HealthChanged -= OnHealthChanged;
        health = null;
        foreach (var binding in flames ?? Array.Empty<CycleFlameBinding>())
        {
            if (binding != null && binding.flame != null && binding.callback != null)
            {
                binding.flame.StateChanged -= binding.callback;
                binding.callback = null;
            }
            if (binding != null && binding.bossTorch != null && binding.bossTorchCallback != null)
            {
                binding.bossTorch.StateChanged -= binding.bossTorchCallback;
                binding.bossTorchCallback = null;
            }
        }
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
    [Tooltip("Activer une condition de fin optionnelle. Désactivé préserve les liaisons existantes.")] public bool useHideCondition;
    public CycleCondition hideWhen = new CycleCondition();
}

/// <summary>Inverse presentation binding: hides its target when the condition becomes true.</summary>
[Serializable]
public sealed class CycleDeactivationBinding
{
    public GameObject target;
    public CycleCondition condition = new CycleCondition();
}

/// <summary>Disparition visuelle avant desactivation. Les materiaux qui exposent
/// _DissolveStrength sont pilotes par MaterialPropertyBlock, sans etre modifies globalement.</summary>
[Serializable]
public sealed class CycleDissolveBinding
{
    private static readonly int DissolveStrengthId = Shader.PropertyToID("_DissolveStrength");
    public GameObject target;
    public CycleCondition condition = new CycleCondition();
    [Min(.01f)] public float durationSeconds = 2f;
    [NonSerialized] private Renderer[] renderers;
    [NonSerialized] private MaterialPropertyBlock block;

    public void ApplyStrength(float value)
    {
        if (target == null) return;
        renderers ??= target.GetComponentsInChildren<Renderer>(true);
        block ??= new MaterialPropertyBlock();
        float clamped = Mathf.Clamp01(value);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                Material material = materials[index];
                if (material == null || !material.HasProperty(DissolveStrengthId)) continue;
                renderer.GetPropertyBlock(block, index);
                block.SetFloat(DissolveStrengthId, clamped);
                renderer.SetPropertyBlock(block, index);
            }
        }
    }
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
