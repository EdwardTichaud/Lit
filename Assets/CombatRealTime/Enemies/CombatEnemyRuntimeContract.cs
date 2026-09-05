using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Verifies that an instantiated enemy is the full gameplay prefab, rather than
/// a visual child. It deliberately never repairs a clone at runtime: missing
/// physics or navigation must be fixed on the authored prefab.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RealTimeCombatEnemy), typeof(EnemySkills), typeof(CombatEnemyPhysicsMotor))]
[RequireComponent(typeof(EnemyNavigationController))]
[RequireComponent(typeof(CombatActorAnimationRoot), typeof(Rigidbody), typeof(CapsuleCollider))]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class CombatEnemyRuntimeContract : MonoBehaviour
{
    [SerializeField] private RealTimeCombatEnemy enemy;
    [SerializeField] private EnemySkills enemySkills;
    [SerializeField] private CombatEnemyPhysicsMotor physicsMotor;
    [SerializeField] private CombatActorAnimationRoot animationRoot;
    [SerializeField] private Rigidbody rigidbodyComponent;
    [SerializeField] private CapsuleCollider capsuleCollider;
    [SerializeField] private NavMeshAgent navigationAgent;
    [SerializeField] private EnemyNavigationController navigation;
    [SerializeField] private Animator animator;
    [SerializeField] private bool logDiagnostics = true;

    private bool loggedFailure;

    public bool IsValid { get; private set; }
    public bool CanRunCombat => IsValid && physicsMotor != null && physicsMotor.IsOperational;
    public CombatEnemyPhysicsMotor PhysicsMotor => physicsMotor;

    public static bool HasRequiredComponents(GameObject actor)
    {
        Rigidbody body = actor != null ? actor.GetComponent<Rigidbody>() : null;
        CapsuleCollider capsule = actor != null ? actor.GetComponent<CapsuleCollider>() : null;
        return actor != null &&
               actor.GetComponent<RealTimeCombatEnemy>() != null &&
               actor.GetComponent<EnemySkills>() != null &&
               actor.GetComponent<CombatEnemyPhysicsMotor>() != null &&
               actor.GetComponent<CombatActorAnimationRoot>() != null &&
               body != null && body.isKinematic &&
               capsule != null && capsule.enabled && !capsule.isTrigger &&
               actor.GetComponent<NavMeshAgent>() != null &&
               actor.GetComponent<EnemyNavigationController>() != null &&
               ResolveAnimator(actor) != null && ResolveAnimator(actor).runtimeAnimatorController != null;
    }

    public static string DescribeRequiredComponents(GameObject actor)
    {
        Animator resolvedAnimator = ResolveAnimator(actor);
        Rigidbody body = actor != null ? actor.GetComponent<Rigidbody>() : null;
        CapsuleCollider capsule = actor != null ? actor.GetComponent<CapsuleCollider>() : null;
        NavMeshAgent agent = actor != null ? actor.GetComponent<NavMeshAgent>() : null;
        EnemyNavigationController navigation = actor != null ? actor.GetComponent<EnemyNavigationController>() : null;
        return "enemy=" + Present(actor != null ? actor.GetComponent<RealTimeCombatEnemy>() : null) +
               ", skills=" + Present(actor != null ? actor.GetComponent<EnemySkills>() : null) +
               ", physics=" + Present(actor != null ? actor.GetComponent<CombatEnemyPhysicsMotor>() : null) +
               ", actorAnimation=" + Present(actor != null ? actor.GetComponent<CombatActorAnimationRoot>() : null) +
               ", rigidbody=" + (body != null ? "ok/kinematic=" + body.isKinematic : "absent") +
               ", capsule=" + (capsule != null
                   ? "ok/enabled=" + capsule.enabled + "/trigger=" + capsule.isTrigger
                   : "absent") +
               ", navMeshAgent=" + (agent != null ? "ok/enabled=" + agent.enabled : "absent") +
               ", enemyNavigation=" + Present(navigation) +
               ", animator=" + (resolvedAnimator != null && resolvedAnimator.runtimeAnimatorController != null
                   ? resolvedAnimator.name
                   : "absent/controller absent");
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ValidateContract(out _);
    }

    public bool ValidateContract(out string report)
    {
        ResolveReferences();
        IsValid = HasRequiredComponents(gameObject);
        report = DescribeRequiredComponents(gameObject);

        if (!IsValid && logDiagnostics && !loggedFailure)
        {
            loggedFailure = true;
            Debug.LogError("[CombatEnemyRuntimeContract] Clone invalide '" + name + "' : " + report +
                           ". IA et combat doivent rester inactifs. Verifier CharacterData.worldPrefab.", this);
        }

        return IsValid;
    }

    public void DisableCombatSystems()
    {
        EnemyCombatBrain brain = GetComponent<EnemyCombatBrain>();
        if (brain != null) brain.enabled = false;
        if (enemySkills != null) enemySkills.enabled = false;
        if (physicsMotor != null) physicsMotor.enabled = false;
        RealTimeCombatEnemyBehaviour behaviour = GetComponent<RealTimeCombatEnemyBehaviour>();
        if (behaviour != null) behaviour.enabled = false;
        CombatEnemyLocomotionController locomotion = GetComponent<CombatEnemyLocomotionController>();
        if (locomotion != null) locomotion.enabled = false;
    }

    public void TraceAnimationEvent(string eventName)
    {
        if (!logDiagnostics)
        {
            return;
        }

        Debug.Log("[CombatEnemyEvent] actor='" + name + "' | event=" + eventName +
                  " | contract=" + IsValid + " | physics=" +
                  (physicsMotor != null ? physicsMotor.State.ToString() : "absent") +
                  " | position=" + transform.position + ".", this);
    }

    private void ResolveReferences()
    {
        enemy = GetComponent<RealTimeCombatEnemy>();
        enemySkills = GetComponent<EnemySkills>();
        physicsMotor = GetComponent<CombatEnemyPhysicsMotor>();
        animationRoot = GetComponent<CombatActorAnimationRoot>();
        rigidbodyComponent = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        navigationAgent = GetComponent<NavMeshAgent>();
        navigation = GetComponent<EnemyNavigationController>();
        animator = animationRoot != null ? animationRoot.Animator : GetComponent<Animator>();
    }

    private static Animator ResolveAnimator(GameObject actor)
    {
        if (actor == null)
        {
            return null;
        }

        CombatActorAnimationRoot contract = actor.GetComponent<CombatActorAnimationRoot>();
        return contract != null ? contract.Animator : actor.GetComponent<Animator>();
    }

    private static string Present(Object value)
    {
        return value != null ? "ok" : "absent";
    }
}
