using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Lightweight, data-driven defensive choices for a real-time enemy. Navigation
/// remains the only owner of ordinary movement; this component only requests a
/// short authored presentation and a NavMesh displacement for a dodge.
/// </summary>
[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
[RequireComponent(typeof(RealTimeCombatEnemy))]
public sealed class EnemyTacticalResponseController : MonoBehaviour
{
    [Serializable]
    public sealed class TacticalProfile
    {
        [Header("Decision")]
        [Range(0f, 1f)] public float guardChance = .12f;
        [Range(0f, 1f)] public float dodgeChance = .08f;
        [Min(.1f)] public float reactionMaximumDistance = 3.5f;
        [Range(0f, 180f)] public float reactionMaximumAngle = 110f;
        [Min(0f)] public float guardCooldownSeconds = 4f;
        [Min(0f)] public float dodgeCooldownSeconds = 5f;
        [Min(.05f)] public float guardDurationSeconds = .58f;
        [Min(.05f)] public float dodgeDurationSeconds = .38f;
        [Range(0f, 1f)] public float guardedDamageMultiplier = .45f;
        [Min(.05f)] public float dodgeDistance = 1.35f;

        [Header("Animator")]
        public string guardAnimatorState = "Guard";
        public string dodgeAnimatorState = "Dodge";
        [Range(0f, .25f)] public float transitionSeconds = .08f;
    }

    private enum TacticalState { None, Guarding, Dodging }

    [SerializeField] private RealTimeCombatEnemy enemy;
    [SerializeField] private CombatEnemyPhysicsMotor physicsMotor;
    [SerializeField] private CombatEnemyLocomotionController locomotion;
    [SerializeField] private NavMeshAgent navigationAgent;
    [SerializeField] private CombatActorAnimationRoot animationContract;
    [SerializeField] private RealTimeCombatEnemyBehaviour combatBehaviour;
    [SerializeField] private TacticalProfile profile = new TacticalProfile();
    [SerializeField] private bool logDiagnostics;

    private TacticalState state;
    private float stateEndsAt;
    private float nextGuardAt;
    private float nextDodgeAt;
    private Vector3 dodgeDirection;
    private RealTimeCombatManager manager;

    public TacticalProfile Profile => profile;
    public bool IsReacting => state != TacticalState.None;
    public bool IsGuarding => state == TacticalState.Guarding;
    public bool IsDodging => state == TacticalState.Dodging;

    private void Reset()
    {
        enemy = GetComponent<RealTimeCombatEnemy>();
        physicsMotor = GetComponent<CombatEnemyPhysicsMotor>();
        locomotion = GetComponent<CombatEnemyLocomotionController>();
        navigationAgent = GetComponent<NavMeshAgent>();
        animationContract = GetComponent<CombatActorAnimationRoot>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        BindManager(RealTimeCombatManager.Instance);
    }

    private void OnDisable()
    {
        BindManager(null);
        ClearReaction();
    }

    private void Update()
    {
        if (manager == null && RealTimeCombatManager.Instance != null)
        {
            BindManager(RealTimeCombatManager.Instance);
        }

        if (!IsRuntimeReady())
        {
            ClearReaction();
            return;
        }

        if (state == TacticalState.None)
        {
            return;
        }

        if (state == TacticalState.Dodging)
        {
            ApplyDodgeMovement();
        }

        KeepReactionPresentation();

        if (Time.time >= stateEndsAt || enemy == null || enemy.ActiveSkill != null ||
            (enemy.Health != null && enemy.Health.IsDead))
        {
            ClearReaction();
        }
    }

    public int ResolveIncomingPlayerDamage(SkillSO skill, int requestedDamage)
    {
        if (requestedDamage <= 0)
        {
            return 0;
        }

        if (state == TacticalState.Dodging)
        {
            Trace("impact evite");
            return 0;
        }

        if (state == TacticalState.Guarding)
        {
            int reduced = Mathf.CeilToInt(requestedDamage * Mathf.Clamp01(profile.guardedDamageMultiplier));
            Trace("impact garde " + requestedDamage + " -> " + reduced);
            return reduced;
        }

        return requestedDamage;
    }

    private void OnPlayerSkillStarted(SkillSO skill, RealTimeCombatEnemy target)
    {
        if (target != enemy || skill == null || !CanReact())
        {
            return;
        }

        float roll = UnityEngine.Random.value;
        if (roll < profile.guardChance && Time.time >= nextGuardAt && TryStartGuard())
        {
            nextGuardAt = Time.time + profile.guardCooldownSeconds;
            return;
        }

        if (roll < profile.guardChance + profile.dodgeChance && Time.time >= nextDodgeAt && TryStartDodge())
        {
            nextDodgeAt = Time.time + profile.dodgeCooldownSeconds;
        }
    }

    private bool CanReact()
    {
        ResolveReferences();
        if (!IsRuntimeReady() || enemy == null || manager == null || !manager.IsCombatActive || manager.EngagedEnemy != enemy ||
            state != TacticalState.None || (enemy.Health != null && enemy.Health.IsDead) ||
            enemy.ActiveSkill != null || (physicsMotor != null && physicsMotor.IsDrivingActionRootMotion))
        {
            return false;
        }

        Transform player = manager.PlayerRoot;
        if (player == null)
        {
            return false;
        }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude > profile.reactionMaximumDistance || toPlayer.sqrMagnitude <= .0001f)
        {
            return false;
        }

        return Vector3.Angle(transform.forward, toPlayer.normalized) <= profile.reactionMaximumAngle;
    }

    private bool TryStartGuard()
    {
        if (!TryCrossFade(profile.guardAnimatorState))
        {
            return false;
        }

        state = TacticalState.Guarding;
        stateEndsAt = Time.time + profile.guardDurationSeconds;
        locomotion?.StopNavigation();
        Trace("garde");
        return true;
    }

    private bool TryStartDodge()
    {
        if (!TryCrossFade(profile.dodgeAnimatorState))
        {
            return false;
        }

        Transform player = manager != null ? manager.PlayerRoot : null;
        Vector3 side = player != null
            ? Vector3.Cross(Vector3.up, player.position - transform.position).normalized
            : transform.right;
        dodgeDirection = (UnityEngine.Random.value < .5f ? side : -side);
        dodgeDirection.y = 0f;
        state = TacticalState.Dodging;
        stateEndsAt = Time.time + profile.dodgeDurationSeconds;
        locomotion?.StopNavigation();
        Trace("esquive");
        return true;
    }

    private void ApplyDodgeMovement()
    {
        if (navigationAgent == null || !navigationAgent.isActiveAndEnabled || !navigationAgent.isOnNavMesh)
        {
            return;
        }

        navigationAgent.isStopped = false;
        float speed = profile.dodgeDistance / Mathf.Max(.01f, profile.dodgeDurationSeconds);
        navigationAgent.Move(dodgeDirection * speed * Time.deltaTime);
    }

    private void KeepReactionPresentation()
    {
        string requestedState = state == TacticalState.Guarding ? profile.guardAnimatorState : profile.dodgeAnimatorState;
        Animator animator = animationContract != null ? animationContract.Animator : null;
        if (animator == null || string.IsNullOrWhiteSpace(requestedState))
        {
            return;
        }

        int hash = Animator.StringToHash(requestedState);
        if (!animator.HasState(0, hash))
        {
            hash = Animator.StringToHash("Base Layer." + requestedState);
        }

        if (animator.HasState(0, hash) && animator.GetCurrentAnimatorStateInfo(0).fullPathHash != hash)
        {
            animator.CrossFade(hash, profile.transitionSeconds, 0);
        }
    }

    private bool TryCrossFade(string stateName)
    {
        Animator animator = animationContract != null ? animationContract.Animator : null;
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(stateName))
        {
            return false;
        }

        int hash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, hash))
        {
            hash = Animator.StringToHash("Base Layer." + stateName);
            if (!animator.HasState(0, hash))
            {
                Debug.LogWarning("[EnemyTacticalResponse] Etat Animator introuvable : " + stateName + " sur " + name + ".", this);
                return false;
            }
        }

        animator.CrossFade(hash, profile.transitionSeconds, 0);
        return true;
    }

    private void ClearReaction()
    {
        if (state == TacticalState.None)
        {
            return;
        }

        state = TacticalState.None;
        stateEndsAt = 0f;
        Trace("reaction terminee");
    }

    private void BindManager(RealTimeCombatManager nextManager)
    {
        if (manager == nextManager)
        {
            return;
        }

        if (manager != null)
        {
            manager.PlayerSkillStarted -= OnPlayerSkillStarted;
        }

        manager = nextManager;
        if (manager != null)
        {
            manager.PlayerSkillStarted += OnPlayerSkillStarted;
        }
    }

    private void ResolveReferences()
    {
        enemy ??= GetComponent<RealTimeCombatEnemy>();
        physicsMotor ??= GetComponent<CombatEnemyPhysicsMotor>();
        locomotion ??= GetComponent<CombatEnemyLocomotionController>();
        navigationAgent ??= GetComponent<NavMeshAgent>();
        animationContract ??= GetComponent<CombatActorAnimationRoot>();
        combatBehaviour ??= GetComponent<RealTimeCombatEnemyBehaviour>();
    }

    private bool IsRuntimeReady()
    {
        ResolveReferences();
        return combatBehaviour != null && combatBehaviour.IsRuntimeReady;
    }

    private void Trace(string message)
    {
        if (logDiagnostics)
        {
            Debug.Log("[EnemyTacticalResponse] " + name + " " + message + ".", this);
        }
    }
}
