using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>Session-owned narrative state. Scene adapters submit validated facts; clients only receive state.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WorldRulesStateManager))]
public sealed class CycleProgressionService : MonoBehaviour
{
    private const string Message = "lit.cycles.state.v1";
    public static CycleProgressionService Instance { get; private set; }
    public event Action Changed;
    private WorldRulesStateManager rules;
    private KnowledgeManager knowledge;
    private NetworkManager network;
    private JoinSyncSystem join;
    private readonly List<CycleDefinition> definitions = new List<CycleDefinition>();
    private readonly HashSet<CycleController> sources = new HashSet<CycleController>();
    private bool reconciling, dirty = true, messagesRegistered, receivedServerState;
    public bool IsReady => Authority || receivedServerState && !JoinSyncSystem.IsGameplayBlocked;
    private string previousState;
    private float nextDependencyCheck;
    public bool Authority => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
    public IReadOnlyList<CycleDefinition> Definitions => definitions;
    [Serializable] private sealed class Packet { public List<WorldVariableSnapshot> values; public string rewardCycle; public string rewardStep; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;
    public static CycleProgressionService Ensure(WorldRulesStateManager state)
    {
        if (state == null) return null;
        var service = state.GetComponent<CycleProgressionService>();
        return service != null ? service : state.gameObject.AddComponent<CycleProgressionService>();
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        rules = GetComponent<WorldRulesStateManager>();
        foreach (var definition in Resources.LoadAll<CycleDefinition>("Narrative")) RegisterDefinition(definition);
    }
    private void OnEnable()
    {
        if (rules == null) rules = GetComponent<WorldRulesStateManager>();
        rules.VariablesChanged += OnVariablesChanged;
        HookDependencies();
        dirty = true;
    }
    private void OnDisable()
    {
        if (rules != null) rules.VariablesChanged -= OnVariablesChanged;
        UnhookKnowledge();
        UnhookNetwork();
        if (join != null) join.ClientMarkedReady -= SendCurrentState;
        join = null;
        if (Instance == this) Instance = null;
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void Update()
    {
        if (Time.unscaledTime >= nextDependencyCheck)
        {
            nextDependencyCheck = Time.unscaledTime + .5f;
            HookDependencies();
        }
        if (dirty) Refresh();
    }
    public void RegisterDefinition(CycleDefinition definition)
    {
        if (definition == null || definitions.Contains(definition)) return;
        if (definitions.Exists(other => other.cycleId == definition.cycleId))
        { Debug.LogError("[Cycle] ID de cycle duplique : " + definition.cycleId, definition); return; }
        definitions.Add(definition);
        dirty = true;
    }
    public void Register(CycleController source)
    {
        if (source == null) return;
        RegisterDefinition(source.definition);
        sources.Add(source);
    }
    public void Unregister(CycleController source) => sources.Remove(source);
    private int Read(string key) => rules != null && rules.TryGetInt(key, out int value) ? value : 0;
    public bool IsStepCompleted(CycleDefinition cycle, string id) => cycle != null && Read(cycle.StepKey(id)) != 0;
    public bool IsCompleted(CycleDefinition cycle)
    {
        if (cycle == null) return false;
        if (Read(cycle.StateKey + ".completed") != 0 || cycle.IsCompleted(Read(cycle.StateKey))) return true;
        if (!cycle.HasSteps) return false;
        bool terminal = false;
        foreach (var step in cycle.steps)
            if (step != null && step.terminal) { terminal = true; if (!IsStepCompleted(cycle, step.id)) return false; }
        return terminal;
    }
    public bool HasDefeatFact(CycleDefinition cycle, string sourceId)
    {
        if (cycle == null) return false;
        if (Read(cycle.FactKey(sourceId)) != 0) return true;
        foreach (var step in cycle.steps ?? Array.Empty<CycleStep>())
            if (step != null && step.kind == CycleStepKind.EnemyDefeated && step.sourceId == sourceId && IsStepCompleted(cycle, step.id)) return true;
        return false;
    }
    public bool IsCompletedScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return false;
        foreach (var definition in definitions)
            if (definition.cycleSceneName == sceneName && IsCompleted(definition)) return true;
        return false;
    }
    public static bool ShouldSkipScene(string sceneName)
    {
        if (Instance != null) return Instance.IsCompletedScene(sceneName);
        var state = UnityEngine.Object.FindAnyObjectByType<WorldRulesStateManager>();
        if (state == null || string.IsNullOrWhiteSpace(sceneName)) return false;
        foreach (var definition in Resources.LoadAll<CycleDefinition>("Narrative"))
            if (definition.cycleSceneName == sceneName &&
                (state.TryGetInt(definition.StateKey + ".completed", out int completed) && completed != 0 ||
                 state.TryGetInt(definition.StateKey, out int flags) && definition.IsCompleted(flags))) return true;
        return false;
    }
    public CycleStatus GetStatus(CycleDefinition cycle)
    {
        if (!IsReady) return CycleStatus.Unavailable;
        if (IsCompleted(cycle)) return CycleStatus.Completed;
        if (cycle == null) return CycleStatus.Unavailable;
        if (Read(cycle.StateKey) != 0) return CycleStatus.InProgress;
        if (cycle.steps != null) foreach (var step in cycle.steps)
            if (step != null && IsStepCompleted(cycle, step.id)) return CycleStatus.InProgress;
        return Matches(cycle, cycle.prerequisites) ? CycleStatus.Available : CycleStatus.Unavailable;
    }
    public bool Matches(CycleDefinition owner, CycleRequirements requirements)
    {
        if (requirements == null || requirements.conditions == null || requirements.conditions.Length == 0) return true;
        bool any = requirements.mode == CycleRequirementMode.Any;
        foreach (var condition in requirements.conditions)
        {
            bool value = condition != null && (condition.kind == CycleRequirementKind.Step
                ? IsStepCompleted(owner, condition.stepId)
                : condition.kind == CycleRequirementKind.CycleCompleted ? IsCompleted(condition.cycle)
                : condition.knowledge != null && KnowledgeManager.Instance != null && KnowledgeManager.Instance.HasKnowledge(condition.knowledge));
            if (any && value) return true;
            if (!any && !value) return false;
        }
        return !any;
    }
    public bool IsStepActive(CycleDefinition cycle, CycleStep step) => cycle != null && step != null &&
        !IsCompleted(cycle) && !IsStepCompleted(cycle, step.id) &&
        Matches(cycle, cycle.prerequisites) && Matches(cycle, step.prerequisites);
    public string ExplainBlocked(CycleDefinition cycle, CycleStep step)
    {
        if (IsStepCompleted(cycle, step.id)) return "Validee";
        if (IsCompleted(cycle)) return "Cycle termine";
        if (!Matches(cycle, cycle.prerequisites)) return "Prerequis du cycle non remplis";
        if (!Matches(cycle, step.prerequisites)) return "Prerequis de l'etape non remplis";
        return "En attente : " + step.kind + " / " + step.sourceId;
    }
    // No RPC accepts a milestone from a client. Only a registered, authoritative scene adapter may report it.
    internal bool Report(CycleController source, CycleStepKind kind, string sourceId)
    {
        if (!Authority || source == null || !source.isActiveAndEnabled || !sources.Contains(source) ||
            source.definition == null || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !source.IsSpawned)) return false;
        return ApplyEvent(source.definition, kind, sourceId);
    }
    internal bool ApplyEvent(CycleDefinition cycle, CycleStepKind kind, string sourceId)
    {
        if (!Authority || cycle == null || string.IsNullOrWhiteSpace(sourceId)) return false;
        RegisterDefinition(cycle);
        Refresh();
        if (IsCompleted(cycle)) return false;
        if (kind == CycleStepKind.EnemyDefeated && Read(cycle.FactKey(sourceId)) == 0) rules.SetInt(cycle.FactKey(sourceId), 1);
        // Capture eligible steps before applying this event. One click cannot traverse an entire chain.
        var eligible = new List<CycleStep>();
        foreach (var step in cycle.steps)
            if (step != null && step.kind == kind && step.sourceId == sourceId && IsStepActive(cycle, step)) eligible.Add(step);
        foreach (var step in eligible) Complete(cycle, step, true);
        Refresh();
        return eligible.Count > 0;
    }
    private void Complete(CycleDefinition cycle, CycleStep step, bool notify)
    {
        if (IsStepCompleted(cycle, step.id)) return;
        rules.SetInt(cycle.StepKey(step.id), 1);
        if (notify && step.legacyWriteFlags != 0) rules.SetInt(cycle.StateKey, Read(cycle.StateKey) | step.legacyWriteFlags);
        if (notify && step.rewardSkill != null)
        {
            SkillUnlockPanel.TryShow(step.rewardSkill);
            Broadcast(cycle.cycleId, step.id);
        }
    }
    public void Refresh()
    {
        if (rules == null || reconciling) return;
        reconciling = true;
        dirty = false;
        try
        {
            if (Authority)
            {
                foreach (var cycle in definitions)
                    foreach (var step in cycle.steps ?? Array.Empty<CycleStep>())
                        if (step != null && step.legacyAnyFlags != 0 && (Read(cycle.StateKey) & step.legacyAnyFlags) != 0)
                            Complete(cycle, step, false);
                // Bounded fixed point: persistent facts may unlock several ordered steps or another cycle.
                int limit = 1;
                foreach (var cycle in definitions) limit += cycle.steps?.Length ?? 0;
                for (int pass = 0; pass < limit; pass++)
                {
                    bool changed = false;
                    foreach (var cycle in definitions)
                    {
                        foreach (var step in cycle.steps ?? Array.Empty<CycleStep>())
                        {
                            if (!IsStepActive(cycle, step)) continue;
                            bool fact = step.kind == CycleStepKind.Knowledge && step.knowledge != null && KnowledgeManager.Instance != null && KnowledgeManager.Instance.HasKnowledge(step.knowledge) ||
                                step.kind == CycleStepKind.EnemyDefeated && Read(cycle.FactKey(step.sourceId)) != 0;
                            if (fact) { Complete(cycle, step, true); changed = true; }
                        }
                        if (IsCompleted(cycle) && Read(cycle.StateKey + ".completed") == 0)
                        { rules.SetInt(cycle.StateKey + ".completed", 1); changed = true; }
                    }
                    if (!changed) break;
                }
            }
            string current = JsonUtility.ToJson(Capture());
            if (current != previousState)
            {
                previousState = current;
                if (Authority) Broadcast();
                Changed?.Invoke();
            }
        }
        finally { reconciling = false; }
    }
    private void OnVariablesChanged() { dirty = true; }
    private void OnKnowledge(KnowledgeSO _) { dirty = true; Changed?.Invoke(); }
    private void HookDependencies()
    {
        if (Instance == null) Instance = this;
        if (knowledge != KnowledgeManager.Instance)
        {
            UnhookKnowledge(); knowledge = KnowledgeManager.Instance;
            if (knowledge != null) { knowledge.KnowledgeUnlocked += OnKnowledge; knowledge.KnowledgeRemoved += OnKnowledge; dirty = true; }
        }
        var manager = NetworkManager.Singleton;
        if (network != manager || network != null && !network.IsListening)
        { UnhookNetwork(); network = manager; dirty = true; }
        if (!messagesRegistered && network != null && network.IsListening && network.CustomMessagingManager != null)
        {
            network.CustomMessagingManager.RegisterNamedMessageHandler(Message, Receive);
            messagesRegistered = true;
        }
        if (join != JoinSyncSystem.Instance)
        {
            if (join != null) join.ClientMarkedReady -= SendCurrentState;
            join = JoinSyncSystem.Instance;
            if (join != null) join.ClientMarkedReady += SendCurrentState;
        }
    }
    private void UnhookKnowledge()
    {
        if (knowledge != null) { knowledge.KnowledgeUnlocked -= OnKnowledge; knowledge.KnowledgeRemoved -= OnKnowledge; }
        knowledge = null;
    }
    private void UnhookNetwork()
    {
        if (messagesRegistered && network != null && network.CustomMessagingManager != null)
            network.CustomMessagingManager.UnregisterNamedMessageHandler(Message);
        messagesRegistered = false; network = null; receivedServerState = false;
    }
    private Packet Capture(string rewardCycle = null, string rewardStep = null)
    {
        var values = rules.CaptureVariables();
        values.RemoveAll(value => value == null || !value.Key.StartsWith("narrative.", StringComparison.Ordinal));
        values.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        return new Packet { values = values, rewardCycle = rewardCycle, rewardStep = rewardStep };
    }
    private void Broadcast(string rewardCycle = null, string rewardStep = null)
    {
        if (network == null || !network.IsListening || !network.IsServer || !messagesRegistered) return;
        string json = JsonUtility.ToJson(Capture(rewardCycle, rewardStep));
        foreach (ulong client in network.ConnectedClientsIds)
            if (client != network.LocalClientId) Send(client, json);
    }
    private void SendCurrentState(ulong client)
    {
        if (Authority && network != null && network.IsListening && network.IsServer)
            Send(client, JsonUtility.ToJson(Capture()));
    }
    private void Send(ulong client, string json)
    {
        using (var writer = new FastBufferWriter(sizeof(int) + json.Length * 4 + 16, Allocator.Temp))
        {
            writer.WriteValueSafe(json);
            network.CustomMessagingManager.SendNamedMessage(Message, client, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }
    }
    private void Receive(ulong sender, FastBufferReader reader)
    {
        if (network == null || network.IsServer || sender != NetworkManager.ServerClientId) return;
        reader.ReadValueSafe(out string json);
        var packet = JsonUtility.FromJson<Packet>(json);
        if (packet?.values == null) return;
        receivedServerState = true;
        var cycle = definitions.Find(item => item.cycleId == packet.rewardCycle);
        var reward = cycle != null ? cycle.FindStep(packet.rewardStep) : null;
        bool show = reward != null && !IsStepCompleted(cycle, reward.id);
        var combined = rules.CaptureVariables();
        combined.RemoveAll(value => value != null && value.Key.StartsWith("narrative.", StringComparison.Ordinal));
        foreach (var value in packet.values)
            if (value != null && value.Key.StartsWith("narrative.", StringComparison.Ordinal)) combined.Add(value);
        rules.ApplyVariables(combined);
        if (show && reward.rewardSkill != null) SkillUnlockPanel.TryShow(reward.rewardSkill);
        Refresh();
    }
}
