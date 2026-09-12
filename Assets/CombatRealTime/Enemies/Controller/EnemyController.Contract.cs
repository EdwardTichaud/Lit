using UnityEngine;
using UnityEngine.AI;

public sealed partial class EnemyController
{
    private EnemyController ContractEnemy;
    private EnemyController ContractEnemySkills;
    private EnemyController ContractPhysicsMotor;
    private CharacterAnimationController ContractAnimationRoot;
    [SerializeField]
    private Rigidbody ContractRigidbodyComponent;
    [SerializeField]
    private CapsuleCollider ContractCapsuleCollider;
    [SerializeField]
    private NavMeshAgent ContractNavigationAgent;
    private EnemyController ContractNavigation;
    [SerializeField]
    private Animator ContractAnimator;
    private bool ContractLogDiagnostics { get => Configuration.ContractLogDiagnostics; set => Configuration.ContractLogDiagnostics = value; }
    private bool ContractLoggedFailure;
    public bool IsValid { get; private set; }

    public bool CanRunCombat => CombatEnabled && IsValid && ContractPhysicsMotor != null && ContractPhysicsMotor.IsOperational;
    public EnemyController PhysicsMotor => ContractPhysicsMotor;
    public static bool HasRequiredComponents(GameObject actor)
    {
        Rigidbody body = actor != null ? actor.GetComponent<Rigidbody>() : null;
        CapsuleCollider capsule = actor != null ? actor.GetComponent<CapsuleCollider>() : null;
        return actor != null && actor.GetComponent<EnemyController>() != null && actor.GetComponent<CharacterAnimationController>() != null && body != null && body.isKinematic && capsule != null && capsule.enabled && !capsule.isTrigger && actor.GetComponent<NavMeshAgent>() != null && actor.GetComponent<EnemyController>() != null && ContractResolveAnimator(actor) != null && ContractResolveAnimator(actor).runtimeAnimatorController != null;
    }

    public static string DescribeRequiredComponents(GameObject actor)
    {
        Animator resolvedAnimator = ContractResolveAnimator(actor);
        Rigidbody body = actor != null ? actor.GetComponent<Rigidbody>() : null;
        CapsuleCollider capsule = actor != null ? actor.GetComponent<CapsuleCollider>() : null;
        NavMeshAgent agent = actor != null ? actor.GetComponent<NavMeshAgent>() : null;
        EnemyController navigation = actor != null ? actor.GetComponent<EnemyController>() : null;
        return "enemy=" + ContractPresent(actor != null ? actor.GetComponent<EnemyController>() : null) + ", skills=" + ContractPresent(actor != null ? actor.GetComponent<EnemyController>() : null) + ", physics=" + ContractPresent(actor != null ? actor.GetComponent<EnemyController>() : null) + ", actorAnimation=" + ContractPresent(actor != null ? actor.GetComponent<CharacterAnimationController>() : null) + ", rigidbody=" + (body != null ? "ok/kinematic=" + body.isKinematic : "absent") + ", capsule=" + (capsule != null ? "ok/enabled=" + capsule.enabled + "/trigger=" + capsule.isTrigger : "absent") + ", navMeshAgent=" + (agent != null ? "ok/enabled=" + agent.enabled : "absent") + ", enemyNavigation=" + ContractPresent(navigation) + ", animator=" + (resolvedAnimator != null && resolvedAnimator.runtimeAnimatorController != null ? resolvedAnimator.name : "absent/controller absent");
    }



    private void ContractAwake()
    {
        ValidateContract(out _);
    }

    public override bool ValidateContract(out string report)
    {
        ContractResolveReferences();
        IsValid = HasRequiredComponents(gameObject) && ValidateAnimationContract(out _);
        report = DescribeRequiredComponents(gameObject);
        if (!IsValid && ContractLogDiagnostics && !ContractLoggedFailure)
        {
            ContractLoggedFailure = true;
            Debug.LogError("[CombatEnemyRuntimeContract] Clone invalide '" + name + "' : " + report + ". IA et combat doivent rester inactifs. Verifier CharacterData.worldPrefab.", this);
        }

        return IsValid;
    }

    public void DisableCombatSystems() => CombatEnabled = false;

    public void TraceAnimationEvent(string eventName)
    {
        if (!ContractLogDiagnostics)
        {
            return;
        }

        Debug.Log("[CombatEnemyEvent] actor='" + name + "' | event=" + eventName + " | contract=" + IsValid + " | physics=" + (ContractPhysicsMotor != null ? ContractPhysicsMotor.State.ToString() : "absent") + " | position=" + transform.position + ".", this);
    }

    private void ContractResolveReferences()
    {
        ContractEnemy = GetComponent<EnemyController>();
        ContractEnemySkills = GetComponent<EnemyController>();
        ContractPhysicsMotor = GetComponent<EnemyController>();
        ContractAnimationRoot = GetComponent<CharacterAnimationController>();
        ContractRigidbodyComponent = GetComponent<Rigidbody>();
        ContractCapsuleCollider = GetComponent<CapsuleCollider>();
        ContractNavigationAgent = GetComponent<NavMeshAgent>();
        ContractNavigation = GetComponent<EnemyController>();
        ContractAnimator = ContractAnimationRoot != null ? ContractAnimationRoot.Animator : GetComponent<Animator>();
    }

    private static Animator ContractResolveAnimator(GameObject actor)
    {
        if (actor == null)
        {
            return null;
        }

        CharacterAnimationController contract = actor.GetComponent<CharacterAnimationController>();
        return contract != null ? contract.Animator : actor.GetComponent<Animator>();
    }

    private static string ContractPresent(Object value)
    {
        return value != null ? "ok" : "absent";
    }
}
