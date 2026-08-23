using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatActorAnimationRoot : MonoBehaviour
{
    [SerializeField] private Transform animationRoot;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform lockPoint;

    private CombatActorRootMotionRelay rootMotionRelay;
    private CombatEnemyPhysicsMotor enemyPhysicsMotor;
    private int cinematicSessionToken = -1;

    public Transform ActorRoot => transform;
    public Transform AnimationRoot => animationRoot;
    public Animator Animator => animator;
    public Transform LockPoint => lockPoint;
    public bool IsCinematicMotionActive => cinematicSessionToken >= 0;
    public bool ShouldConsumeAnimatorRootMotion => IsCinematicMotionActive ||
                                                   (enemyPhysicsMotor != null && enemyPhysicsMotor.IsDrivingActionRootMotion);

    private void Reset()
    {
        animationRoot = transform;
        animator = GetComponent<Animator>();
        lockPoint = transform.Find("EnemyLockPoint");
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void LateUpdate()
    {
        // AnimationRoot is purely visual. Some imported clips animate this
        // transform even while applyRootMotion is disabled (notably Hit), which
        // moves the whole mesh away from ActorRoot. World movement must always
        // be handled by the relay and its explicit receiver instead.
        if (animationRoot != null && animationRoot != transform &&
            (animationRoot.localPosition.sqrMagnitude > 0.000001f ||
             Quaternion.Angle(animationRoot.localRotation, Quaternion.identity) > 0.01f))
        {
            ResetAnimationRootPose();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
    }
#endif

    public void Configure(Transform configuredAnimationRoot, Animator configuredAnimator, Transform configuredLockPoint)
    {
        animationRoot = configuredAnimationRoot;
        animator = configuredAnimator;
        lockPoint = configuredLockPoint;
        ResolveReferences();
    }

    public bool ValidateContract(out string error)
    {
        ResolveReferences();
        if (animationRoot == null)
        {
            error = name + ": AnimationRoot manquant.";
            return false;
        }

        if (animationRoot != transform && animationRoot.parent != transform)
        {
            error = name + ": AnimationRoot doit etre le root acteur ou son enfant direct.";
            return false;
        }

        if (animationRoot != transform &&
            (animationRoot.localPosition.sqrMagnitude > 0.000001f ||
             Quaternion.Angle(animationRoot.localRotation, Quaternion.identity) > 0.01f ||
             (animationRoot.localScale - Vector3.one).sqrMagnitude > 0.000001f))
        {
            error = name + ": AnimationRoot doit conserver une pose locale identite.";
            return false;
        }

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            error = name + ": Animator de gameplay valide manquant.";
            return false;
        }

        if (animator.transform != animationRoot && !animator.transform.IsChildOf(animationRoot))
        {
            error = name + ": Animator doit etre porte par AnimationRoot ou sa hierarchie.";
            return false;
        }

        error = null;
        return true;
    }

    public bool SetActorPose(Vector3 position, Quaternion rotation)
    {
        if (TryGetComponent(out LitOpsiveLocomotionBridge bridge))
        {
            return bridge.SetCinematicPositionAndRotation(position, rotation, true, false);
        }

        if (TryGetComponent(out RealTimeCombatEnemyBehaviour enemyBehaviour))
        {
            return enemyBehaviour.PlaceForCinematic(position, rotation);
        }

        transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
        return true;
    }

    public void ResetAnimationRootPose()
    {
        if (animationRoot == null || animationRoot == transform)
        {
            return;
        }

        animationRoot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        animationRoot.localScale = Vector3.one;
    }

    public void BeginCinematicMotion(int sessionToken)
    {
        cinematicSessionToken = sessionToken;
        ResolveReferences();
        if (rootMotionRelay != null)
        {
            rootMotionRelay.enabled = true;
        }
    }

    /// <summary>
    /// A Timeline using scene offsets already owns the actor transform. In that
    /// case its Animator deltas must not be applied a second time by the relay.
    /// </summary>
    public void SetCinematicRootMotionRelayEnabled(bool enabled)
    {
        if (rootMotionRelay != null)
        {
            rootMotionRelay.enabled = enabled;
        }
    }

    public void EnableRootMotionRelay()
    {
        if (rootMotionRelay != null)
        {
            rootMotionRelay.enabled = true;
        }
    }

    public void EndCinematicMotion(int sessionToken)
    {
        if (cinematicSessionToken != sessionToken)
        {
            return;
        }

        cinematicSessionToken = -1;
        if (rootMotionRelay != null)
        {
            rootMotionRelay.enabled = false;
        }
        ResetAnimationRootPose();
    }

    public void ApplyAnimationDelta(Vector3 worldDeltaPosition, Quaternion deltaRotation)
    {
        if (enemyPhysicsMotor != null && enemyPhysicsMotor.IsDrivingActionRootMotion)
        {
            enemyPhysicsMotor.ApplyActionRootMotion(worldDeltaPosition, deltaRotation);
            return;
        }

        if (!IsCinematicMotionActive)
        {
            return;
        }

        if (TryGetComponent(out LitOpsiveLocomotionBridge bridge))
        {
            bridge.ApplyCinematicRootMotion(worldDeltaPosition, deltaRotation);
            return;
        }

        if (TryGetComponent(out RealTimeCombatEnemyBehaviour enemyBehaviour))
        {
            enemyBehaviour.ApplyCinematicRootMotion(worldDeltaPosition, deltaRotation);
            return;
        }

        transform.SetPositionAndRotation(transform.position + worldDeltaPosition, deltaRotation * transform.rotation);
        Physics.SyncTransforms();
    }

    private void ResolveReferences()
    {
        if (animationRoot == null)
        {
            animationRoot = transform;
        }

        if (animator == null)
        {
            animator = animationRoot.GetComponentInChildren<Animator>(true);
        }

        if (lockPoint == null)
        {
            lockPoint = transform.Find("EnemyLockPoint");
        }

        if (rootMotionRelay == null && animator != null)
        {
            rootMotionRelay = animator.GetComponent<CombatActorRootMotionRelay>();
        }

        enemyPhysicsMotor ??= GetComponent<CombatEnemyPhysicsMotor>();
    }
}
