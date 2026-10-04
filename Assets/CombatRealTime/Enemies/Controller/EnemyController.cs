using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI;

/// <summary>One lifecycle and one authority for enemy decisions, movement and actions.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterInfo), typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed partial class EnemyController : CharacterAnimationController, ILitInfluenceReceiver
{
    private CharacterInfo info;
    private EnemySettings fallbackSettings;
    private EnemySettings Configuration
    {
        get
        {
            info ??= GetComponent<CharacterInfo>();
            CharacterData data = info != null ? info.CharacterData : null;
            if (data != null) return data.enemySettings ??= new EnemySettings();
            return fallbackSettings ??= new EnemySettings();
        }
    }
    private bool initialized;
    [SerializeField, Tooltip("Activer pour un ennemi hostile des son apparition. Le scientifique est active par son interaction Ghost.")]
    private bool combatEnabled = true;
    public bool CombatEnabled
    {
        get => combatEnabled && !IsFlameDormant && (!StartsAsGhost || CurrentState == EncounterState.Active);
        set
        {
            if (combatEnabled == value) return;
            combatEnabled = value;
            if (!initialized) return;
            if (!value) { BrainOnDisable(); StopNavigation(); NavigationOnDisable(); }
            else if (CombatEnabled && isActiveAndEnabled) NavigationOnEnable();
        }
    }
    public bool IsRuntimeReady => CanRunCombat && IsReady;
    public bool IsInAttackMode => BrainTarget != null && !BrainReturning && CombatEnabled;
    public bool IsAlerted => BrainTarget != null;
    public bool IsCinematicSuspended => IsSuspended;
    public float PursuitRadius => BrainProfile != null ? BrainProfile.pursuitRadius : 20f;
    public bool ShouldEndCombatForPursuit => BrainReturning;
    public bool IsPlayerOutsidePursuitZone => BrainTarget != null && Vector3.ProjectOnPlane(BrainTarget.transform.position - BrainHome, Vector3.up).sqrMagnitude > PursuitRadius * PursuitRadius;

    private void Awake()
    {
        AnimationAwake();
        ActorAwake();
        SkillsAwake();
        PhysicsAwake();
        NavigationAwake();
        LocomotionAwake();
        BrainAwake();
        BossAwake();
        ContractAwake();
        RecoveryAwake();
        EncounterAwake();
        initialized = true;
        FlameInfluenceRefresh(true);
    }
    private void OnAnimatorMove()
    {
        if (CombatEnabled && BrainAuthority && animationRelayEnabled && Animator != null && ShouldConsumeAnimatorRootMotion) ApplyAnimationDelta(Animator.deltaPosition, Animator.deltaRotation);
    }
    private void Start() => BrainStart();
    private void OnEnable()
    {
        if (!initialized) return;
        FlameInfluenceRefresh(true);
        EncounterOnEnable();
        if (CombatEnabled && !IsBossBrainSuppressed) NavigationOnEnable();
        RecoveryOnEnable();
    }
    private void Update()
    {
        FlameInfluenceRefresh();
        if (Authority && CurrentState == EncounterState.Dialogue &&
            (Health.IsDead || Online && !NetworkManager.Singleton.ConnectedClients.ContainsKey(introductionClient)))
            CompleteIntroduction(introductionToken, introductionClient, false);
        if (!initialized || !CombatEnabled || !BrainAuthority) return;
        BrainUpdate();
        LocomotionUpdate();
    }
    private void FixedUpdate() { if (initialized && BrainAuthority && !IsFlameDormant) PhysicsFixedUpdate(); }
    private void LateUpdate()
    {
        if (!initialized) return;
        if (IsFlameDormant) return;
        if (CombatEnabled && BrainAuthority) LocomotionLateUpdate();
        PhysicsLateUpdate();
        AnimationLateUpdate();
    }
    private void OnDisable()
    {
        FlameInfluenceRelease();
        if (!initialized) return;
        BattleWallOnDisable();
        EncounterOnDisable();
        HideInput();
        RecoveryOnDisable();
        BrainOnDisable();
        ActorOnDisable();
        AnimationOnDisable();
        LocomotionOnDisable();
        PhysicsOnDisable();
        NavigationOnDisable();
        StopAllCoroutines();
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        EncounterOnNetworkSpawn();
        FlameInfluenceNetworkSpawn();
        if (initialized && isActiveAndEnabled) OnEnable();
    }
    public override void OnNetworkDespawn()
    {
        FlameInfluenceNetworkDespawn();
        OnDisable();
        EncounterOnNetworkDespawn();
        base.OnNetworkDespawn();
    }
    public void SetCinematicSuspended(bool value) => SetSuspended(value);
    public bool PlaceForCinematic(Vector3 position, Quaternion rotation) => Place(position, rotation);
    public void ApplyCinematicRootMotion(Vector3 delta, Quaternion rotation)
    {
        if (IsFlameDormant) return;
        if (!IsSuspended) return;
        transform.SetPositionAndRotation(transform.position + delta, rotation * transform.rotation);
        if (PhysicsBody != null) { PhysicsBody.position = transform.position; PhysicsBody.rotation = transform.rotation; }
        Physics.SyncTransforms();
    }

    /// <summary>Keeps an enemy inside its active combat arena without interrupting the action owner.</summary>
    public void ConstrainToBattleWall(Vector3 position)
    {
        if ((transform.position - position).sqrMagnitude <= 0.000001f)
        {
            return;
        }

        transform.position = position;
        if (PhysicsBody != null)
        {
            PhysicsBody.position = position;
        }

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.Warp(position);
            agent.nextPosition = position;
        }

        Physics.SyncTransforms();
    }
    public void NotifyAttackCompleted() => ResolveAnimationAttackEnded();
}
