using System;
using Unity.Netcode;
using UnityEngine;

public enum BossEncounterState : byte { Dormant, Engaged, Resolved }

/// <summary>Networked generic shell for a lockable boss. Concrete bosses only implement their puzzle and resolution.</summary>
[DisallowMultipleComponent]
public abstract class BossEncounterBehaviour : NetworkBehaviour, IBossEncounterBehaviour
{
    [SerializeField] private BossDefinitionSO definition;
    [SerializeField] private EnemyController enemy;
    private IEnemyEncounterActor boundActor;
    private RealTimeCombatManager presentationManager;
    private Transform presentationPlayer;
    private readonly System.Collections.Generic.List<Transform> encounterParticipants = new System.Collections.Generic.List<Transform>();
    protected virtual bool UsesWholePartyEngagementGate => BossEncounterParticipants.UsesWholePartyGate(this);

    // Existing bespoke bosses still use Enemy. A generic implementation opts in explicitly.
    protected virtual bool SupportsNonLegacyEncounterActor => false;
    public IEnemyEncounterActor EncounterActor
    {
        get
        {
            if (boundActor != null) return EnemyEncounterActors.IsAlive(boundActor) ? boundActor : null;
            var actor = Enemy != null ? (IEnemyEncounterActor)Enemy : EnemyEncounterActors.Resolve(this);
            return actor is EnemyController || SupportsNonLegacyEncounterActor ? actor : null;
        }
    }

    public bool TryBindEncounterActor(IEnemyEncounterActor actor)
    {
        if (!EnemyEncounterActors.IsAlive(actor) || actor.EncounterRoot == null ||
            actor.EncounterTarget?.Root != actor.EncounterRoot ||
            !(actor is EnemyController) && (!SupportsNonLegacyEncounterActor || !ReferenceEquals(actor.BossEncounter, this))) return false;
        if (ReferenceEquals(boundActor, actor)) return true;
        if (State != BossEncounterState.Dormant || localCombatOpen) return false;
        boundActor = actor;
        if (actor is EnemyController legacy) enemy = legacy;
        return true;
    }

    private readonly NetworkVariable<BossEncounterState> replicatedState = new(BossEncounterState.Dormant);
    private readonly NetworkVariable<int> replicatedSegments = new(0);
    private BossEncounterState offlineState;
    private int offlineSegments;
    private bool localCombatOpen;
    private readonly NetworkVariable<double> replicatedEncounterClock = new(0);
    private double offlineEncounterClock;
    protected double EncounterTime => IsSpawned ? replicatedEncounterClock.Value : offlineEncounterClock;
    protected System.Collections.IEnumerator WaitForEncounterSeconds(float seconds)
    {
        double until = EncounterTime + Mathf.Max(0, seconds);
        while (EncounterTime < until && !IsBossResolved) yield return null;
        while (EncounterActor != null && EncounterActor.IsFlameDormant && !IsBossResolved) yield return null;
    }

    public event Action PresentationChanged;
    public BossDefinitionSO Definition => definition;
    public EnemyController Enemy => enemy != null ? enemy : GetComponent<EnemyController>();
    public virtual bool SuppressDefaultEnemyBrain => definition == null || definition.SuppressDefaultEnemyBrain;
    public bool IsBossEngaged => State == BossEncounterState.Engaged;
    public bool IsBossResolved => State == BossEncounterState.Resolved;
    public int CurrentSegments => IsSpawned ? replicatedSegments.Value : offlineSegments;
    public int MaximumSegments => definition != null ? definition.SegmentCount : 1;
    protected bool Authority => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsServer;
    protected BossEncounterState State => IsSpawned ? replicatedState.Value : offlineState;

    protected virtual void Awake()
    {
        if (enemy == null) enemy = GetComponent<EnemyController>();
        SetSegmentsLocal(MaximumSegments);
    }

    protected virtual void OnDisable() => EndLocalCombatPresentation();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        replicatedState.OnValueChanged += OnStateChanged;
        replicatedSegments.OnValueChanged += OnSegmentsChanged;
        if (Authority && replicatedSegments.Value <= 0 && State != BossEncounterState.Resolved)
            replicatedSegments.Value = MaximumSegments;
        RefreshLocalCombatPresentation();
    }

    public override void OnNetworkDespawn()
    {
        replicatedState.OnValueChanged -= OnStateChanged;
        replicatedSegments.OnValueChanged -= OnSegmentsChanged;
        EndLocalCombatPresentation();
        encounterParticipants.Clear();
        base.OnNetworkDespawn();
    }

    protected virtual void Update()
    {
        var actor = EncounterActor;
        if (Authority && EnemyEncounterActors.IsAvailable(actor))
        {
            double delta = actor.TimeDomain != null ? actor.TimeDomain.DeltaTime : Time.deltaTime;
            if (IsSpawned) replicatedEncounterClock.Value += delta; else offlineEncounterClock += delta;
        }
        if (Authority && State == BossEncounterState.Dormant)
            foreach (var player in EnumerateEncounterPlayers())
                if (CanEngage(player)) { EngageAuthoritatively(); break; }
        if (Authority && State == BossEncounterState.Engaged) RegisterEncounterParticipants();
        RefreshLocalCombatPresentation();
    }

    protected virtual bool CanEngage(Transform player)
    {
        var actor = EncounterActor;
        return player != null && EnemyEncounterActors.IsAvailable(actor) &&
               (player.position - actor.EncounterRoot.position).sqrMagnitude <= Mathf.Pow(definition != null ? definition.EngagementDistance : 8f, 2f);
    }

    private bool IsWithinEngagementDistance(Transform player)
    {
        var actor = EncounterActor;
        return player != null && actor?.EncounterRoot != null &&
            (player.position - actor.EncounterRoot.position).sqrMagnitude <= Mathf.Pow(definition != null ? definition.EngagementDistance : 8f, 2f);
    }

    protected void EngageAuthoritatively()
    {
        if (!Authority || State != BossEncounterState.Dormant || !EnemyEncounterActors.IsAvailable(EncounterActor)) return;
        encounterParticipants.Clear();
        encounterParticipants.AddRange(BossEncounterParticipants.Collect(EnumerateEncounterPlayers(), CanEngage,
            IsWithinEngagementDistance, UsesWholePartyEngagementGate));
        SetState(BossEncounterState.Engaged);
        OnBossEngagedAuthoritatively();
        RegisterEncounterParticipants();
    }

    protected void RemoveSegmentAuthoritatively()
    {
        if (!Authority || State != BossEncounterState.Engaged || !EnemyEncounterActors.IsAvailable(EncounterActor)) return;
        int next = Mathf.Max(0, CurrentSegments - 1);
        SetSegments(next);
        OnSegmentChangedAuthoritatively(next);
        if (next == 0) ResolveAuthoritatively();
    }

    protected void ResolveAuthoritatively()
    {
        if (!Authority || State == BossEncounterState.Resolved) return;
        SetSegments(0);
        SetState(BossEncounterState.Resolved);
        OnBossResolvedAuthoritatively();
    }

    public virtual bool FilterIncomingDamage(int incomingDamage, SquadCharacterController source, out int permittedDamage)
    {
        permittedDamage = Mathf.Max(0, incomingDamage);
        if (definition == null || definition.DamagePolicy == BossDamagePolicy.Normal) return true;
        permittedDamage = 0;
        if (definition.ShowImmuneImpact && EncounterActor?.EncounterRoot != null)
            CombatDamageWorldFeedback.ShowMessage(EncounterActor.EncounterRoot, "Immúnisé", new Color(.72f, .88f, 1f), 2.2f);
        return false;
    }

    protected virtual void OnBossEngagedAuthoritatively() { }
    protected virtual void OnSegmentChangedAuthoritatively(int remaining) { }
    protected virtual void OnBossResolvedAuthoritatively() { }

    private void RefreshLocalCombatPresentation()
    {
        if (State != BossEncounterState.Engaged)
        {
            EndLocalCombatPresentation();
            return;
        }

        Transform player = LocalPlayerContext.LocalCharacterRoot;
        var actor = EncounterActor;
        if (localCombatOpen && (!EnemyEncounterActors.IsAvailable(actor) ||
            !EnemyEncounterActors.IsPresenting(actor, presentationManager, presentationPlayer))) EndLocalCombatPresentation();
        if (player == null || !EnemyEncounterActors.IsAvailable(actor) || localCombatOpen) return;
        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        if (manager == null) return;
        if (!manager.AllowsFreeAttacks && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) &&
            !encounterParticipants.Contains(player)) return;
        localCombatOpen = EnemyEncounterActors.BeginLocalCombat(actor, manager, player);
        if (localCombatOpen) { presentationManager = manager; presentationPlayer = player; }
    }

    protected void EndLocalCombatPresentation()
    {
        if (!localCombatOpen) return;
        localCombatOpen = false;
        var manager = presentationManager;
        var player = presentationPlayer;
        presentationManager = null; presentationPlayer = null;
        // Production engagement belongs to the world ledger, including its other threats.
        if (manager != null && manager.AllowsFreeAttacks && manager.GetComponent<CombatWorldRuntime>() != null) return;
        if (EnemyEncounterActors.IsPresenting(EncounterActor, manager, player) && manager.ActiveThreatCount <= 1) manager.EndCombat();
    }

    private void SetState(BossEncounterState value)
    {
        if (IsSpawned) replicatedState.Value = value;
        else offlineState = value;
        PresentationChanged?.Invoke();
    }
    protected void SetSegments(int value)
    {
        if (IsSpawned) replicatedSegments.Value = value;
        else offlineSegments = value;
        PresentationChanged?.Invoke();
    }
    private void SetSegmentsLocal(int value) { if (!IsSpawned) offlineSegments = value; }
    private void OnStateChanged(BossEncounterState _, BossEncounterState __) => PresentationChanged?.Invoke();
    private void OnSegmentsChanged(int _, int __) => PresentationChanged?.Invoke();

    private void RegisterEncounterParticipants()
    {
        if (!Authority || State != BossEncounterState.Engaged || !EnemyEncounterActors.IsAvailable(EncounterActor)) return;
        CombatWorldRuntime world = null;
        foreach (var candidate in FindObjectsByType<CombatWorldRuntime>())
        {
            if (!candidate.isActiveAndEnabled) continue;
            if (world != null) return;
            world = candidate;
        }
        if (world == null) return;
        foreach (var root in encounterParticipants)
        {
            if (root == null) continue;
            var player = root.GetComponent<SquadCharacterController>();
            if (player == null) player = root.GetComponentInChildren<SquadCharacterController>();
            if (player == null) player = root.GetComponentInParent<SquadCharacterController>();
            if (player == null || !player.isActiveAndEnabled || player.CurrentHp <= 0) continue;
            bool registered = false;
            foreach (var threat in world.GetThreats(player))
                if (threat?.Root == EncounterActor.EncounterRoot) { registered = true; break; }
            if (!registered) world.TryEngageActor(player, EncounterActor.EncounterTarget);
        }
    }

    private static System.Collections.Generic.IEnumerable<Transform> EnumerateEncounterPlayers()
    {
        var network = NetworkManager.Singleton;
        if (network != null && network.IsListening && !network.IsServer) yield break;
        foreach (var player in CycleGroupSequenceGate.LivingPlayers()) yield return player.player;
    }
}
