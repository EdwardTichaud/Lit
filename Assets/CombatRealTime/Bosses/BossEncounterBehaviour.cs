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

    private readonly NetworkVariable<BossEncounterState> replicatedState = new(BossEncounterState.Dormant);
    private readonly NetworkVariable<int> replicatedSegments = new(0);
    private BossEncounterState offlineState;
    private int offlineSegments;
    private bool localCombatOpen;

    public event Action PresentationChanged;
    public BossDefinitionSO Definition => definition;
    public EnemyController Enemy => enemy != null ? enemy : GetComponent<EnemyController>();
    public bool SuppressDefaultEnemyBrain => true;
    public bool IsBossEngaged => State == BossEncounterState.Engaged;
    public bool IsBossResolved => State == BossEncounterState.Resolved;
    public int CurrentSegments => IsSpawned ? replicatedSegments.Value : offlineSegments;
    public int MaximumSegments => definition != null ? definition.SegmentCount : 1;
    protected bool Authority => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsServer;
    protected BossEncounterState State => IsSpawned ? replicatedState.Value : offlineState;

    protected virtual void Awake()
    {
        if (enemy == null) enemy = GetComponent<EnemyController>();
        if (enemy != null && definition != null) enemy.CreateWall = definition.CreateBattleWall;
        SetSegmentsLocal(MaximumSegments);
    }

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
        base.OnNetworkDespawn();
    }

    protected virtual void Update()
    {
        if (Authority && State == BossEncounterState.Dormant && CanEngage(ResolveAuthoritativePlayer()))
            EngageAuthoritatively();
        RefreshLocalCombatPresentation();
    }

    protected virtual bool CanEngage(Transform player)
    {
        return player != null && Enemy != null && Enemy.gameObject.activeInHierarchy &&
               (player.position - Enemy.transform.position).sqrMagnitude <= Mathf.Pow(definition != null ? definition.EngagementDistance : 8f, 2f);
    }

    protected void EngageAuthoritatively()
    {
        if (!Authority || State != BossEncounterState.Dormant || Enemy == null) return;
        SetState(BossEncounterState.Engaged);
        OnBossEngagedAuthoritatively();
    }

    protected void RemoveSegmentAuthoritatively()
    {
        if (!Authority || State != BossEncounterState.Engaged) return;
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

    public bool FilterIncomingDamage(int incomingDamage, SquadCharacterController source, out int permittedDamage)
    {
        permittedDamage = Mathf.Max(0, incomingDamage);
        if (definition == null || definition.DamagePolicy == BossDamagePolicy.Normal) return true;
        permittedDamage = 0;
        if (definition.ShowImmuneImpact && Enemy != null)
            CombatDamageWorldFeedback.ShowMessage(Enemy.transform, "Immúnisé", new Color(.72f, .88f, 1f), 2.2f);
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
        if (player == null || Enemy == null || localCombatOpen) return;
        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        if (manager == null) return;
        localCombatOpen = manager.BeginCombat(player, Enemy);
        if (localCombatOpen) manager.BeginEnemyAggro(player, Enemy);
    }

    private void EndLocalCombatPresentation()
    {
        if (!localCombatOpen) return;
        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        if (manager != null && manager.IsCombatActive && manager.EngagedEnemy == Enemy) manager.EndCombat();
        localCombatOpen = false;
    }

    private void SetState(BossEncounterState value)
    {
        if (IsSpawned) replicatedState.Value = value;
        else offlineState = value;
        PresentationChanged?.Invoke();
    }
    private void SetSegments(int value)
    {
        if (IsSpawned) replicatedSegments.Value = value;
        else offlineSegments = value;
        PresentationChanged?.Invoke();
    }
    private void SetSegmentsLocal(int value) { if (!IsSpawned) offlineSegments = value; }
    private void OnStateChanged(BossEncounterState _, BossEncounterState __) => PresentationChanged?.Invoke();
    private void OnSegmentsChanged(int _, int __) => PresentationChanged?.Invoke();

    private static Transform ResolveAuthoritativePlayer()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening && manager.IsServer)
        {
            foreach (ulong id in manager.ConnectedClientsIds)
            {
                Transform player = NetcodePlayerUtils.GetPlayerTransform(id);
                if (player != null) return player;
            }
        }
        return LocalPlayerContext.LocalCharacterRoot;
    }
}
