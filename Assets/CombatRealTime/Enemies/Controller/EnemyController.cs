using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI;

/// <summary>One lifecycle and one authority for enemy decisions, movement and actions.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterInfo), typeof(Animator), typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed partial class EnemyController : CharacterAnimationController
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
        get => combatEnabled;
        set
        {
            if (combatEnabled == value) return;
            combatEnabled = value;
            if (!initialized) return;
            if (!value) { CancelAction("desengagement"); StopNavigation(); NavigationOnDisable(); }
            else if (isActiveAndEnabled) NavigationOnEnable();
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
        ContractAwake();
        RecoveryAwake();
        EncounterAwake();
        initialized = true;
    }
    private void OnAnimatorMove()
    {
        if (BrainAuthority && animationRelayEnabled && Animator != null && ShouldConsumeAnimatorRootMotion) ApplyAnimationDelta(Animator.deltaPosition, Animator.deltaRotation);
    }
    private void Start() => BrainStart();
    private void OnEnable()
    {
        if (!initialized) return;
        EncounterOnEnable();
        if (combatEnabled) NavigationOnEnable();
        RecoveryOnEnable();
    }
    private void Update()
    {
        if (!initialized || !combatEnabled || !BrainAuthority) return;
        BrainUpdate();
        LocomotionUpdate();
    }
    private void FixedUpdate() { if (initialized && BrainAuthority) PhysicsFixedUpdate(); }
    private void LateUpdate()
    {
        if (!initialized) return;
        if (combatEnabled && BrainAuthority) LocomotionLateUpdate();
        PhysicsLateUpdate();
        AnimationLateUpdate();
    }
    private void OnDisable()
    {
        if (!initialized) return;
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
        if (initialized && isActiveAndEnabled) OnEnable();
    }
    public override void OnNetworkDespawn()
    {
        OnDisable();
        EncounterOnNetworkDespawn();
        base.OnNetworkDespawn();
    }
    public void SetCinematicSuspended(bool value) => SetSuspended(value);
    public bool PlaceForCinematic(Vector3 position, Quaternion rotation) => Place(position, rotation);
    public void ApplyCinematicRootMotion(Vector3 delta, Quaternion rotation)
    {
        if (!IsSuspended) return;
        transform.SetPositionAndRotation(transform.position + delta, rotation * transform.rotation);
        if (PhysicsBody != null) { PhysicsBody.position = transform.position; PhysicsBody.rotation = transform.rotation; }
        Physics.SyncTransforms();
    }
    public void NotifyAttackCompleted() => ResolveAnimationAttackEnded();
}
