using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Production fixed-flame gate; the server samples the same contacts as the legacy actor.</summary>
[DefaultExecutionOrder(-75)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CombatTimeDomain))]
public sealed class ProductionBrainsFlamePolicy : NetworkBehaviour, ILitBrainsEncounterPolicy
{
    public bool requiresFlameInfluence = true;
    private readonly NetworkVariable<bool> activeInfluence = new NetworkVariable<bool>(false);
    private bool offlineInfluence, agentCaptured, savedStopped;
    private float nextSample;
    private Collider[] contacts;
    private CharacterInfo health;
    private CombatTimeDomain clock;
    private NavMeshAgent agent;
    public bool AllowsDecisions => isActiveAndEnabled && !IsFlameDormant;
    public bool AllowsDamage => isActiveAndEnabled && !IsFlameDormant;
    public bool IsReturning => false;
    private bool Online => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    public bool IsFlameDormant => requiresFlameInfluence && !(Online ? IsSpawned && activeInfluence.Value : offlineInfluence) &&
        !(health != null && health.IsDead);

    private void Awake() => Resolve();
    private void Resolve()
    {
        health = GetComponent<CharacterInfo>();
        clock = GetComponent<CombatTimeDomain>();
        agent = GetComponent<NavMeshAgent>();
        contacts = GetComponentsInChildren<Collider>(true);
    }
    private void OnEnable()
    {
        Resolve();
        if (health != null) health.HealthChanged += OnHealthChanged;
        Refresh();
    }
    private void OnHealthChanged(CharacterInfo changed) => ApplyPause();
    public override void OnNetworkSpawn()
    {
        activeInfluence.OnValueChanged += OnInfluenceChanged;
        Refresh();
    }
    public override void OnNetworkDespawn()
    {
        activeInfluence.OnValueChanged -= OnInfluenceChanged;
        ReleasePause();
        offlineInfluence = false;
    }
    private void OnInfluenceChanged(bool previous, bool current) => ApplyPause();
    private void Update()
    {
        if (Time.unscaledTime >= nextSample) Refresh();
        ApplyPause();
    }
    public void Refresh()
    {
        if (clock == null) Resolve();
        nextSample = Time.unscaledTime + .25f;
        if (!Online || IsSpawned && IsServer)
        {
            bool sampled = !requiresFlameInfluence || SampleContacts();
            if (Online) activeInfluence.Value = sampled; else offlineInfluence = sampled;
        }
        ApplyPause();
    }
    private bool SampleContacts()
    {
        foreach (var flame in Flame.ActiveInfluenceFlames)
            if (flame != null)
                foreach (var contact in contacts)
                    if (contact != null && flame.ProvidesEnemyActivationTo(contact)) return true;
        return false;
    }
    private void ApplyPause()
    {
        bool paused = IsFlameDormant;
        clock?.SetIntrinsicPause(this, paused);
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        if (paused)
        {
            if (!agentCaptured) { savedStopped = agent.isStopped; agentCaptured = true; }
            agent.isStopped = true;
        }
        else if (agentCaptured)
        {
            if (GetComponent<LitBrainsEnemy>()?.IsCinematicSuspended != true) agent.isStopped = savedStopped;
            agentCaptured = false;
        }
    }
    private void ReleasePause()
    {
        clock?.SetIntrinsicPause(this, false);
        if (agentCaptured && agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh &&
            GetComponent<LitBrainsEnemy>()?.IsCinematicSuspended != true) agent.isStopped = savedStopped;
        agentCaptured = false;
    }
    private void OnDisable()
    {
        if (health != null) health.HealthChanged -= OnHealthChanged;
        ReleasePause();
        offlineInfluence = false;
    }
    public bool FilterIncomingDamage(int requested, SquadCharacterController source, out int permitted)
    {
        permitted = AllowsDamage ? Mathf.Max(0, requested) : 0;
        return AllowsDamage;
    }
}
