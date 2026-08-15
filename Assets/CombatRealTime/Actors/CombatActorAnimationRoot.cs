using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatActorAnimationRoot : MonoBehaviour
{
    [SerializeField] private Transform animationRoot;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform lockPoint;

    private CombatActorRootMotionRelay rootMotionRelay;
    private int cinematicSessionToken = -1;
    private bool firstCinematicDeltaLogged;

    public Transform ActorRoot => transform;
    public Transform AnimationRoot => animationRoot;
    public Animator Animator => animator;
    public Transform LockPoint => lockPoint;
    public bool IsCinematicMotionActive => cinematicSessionToken >= 0;

    private void Reset()
    {
        animationRoot = transform.Find("AnimationRoot");
        animator = animationRoot != null ? animationRoot.GetComponentInChildren<Animator>(true) : null;
        lockPoint = transform.Find("EnemyLockPoint");
    }

    private void Awake()
    {
        ResolveReferences();
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

        if (animationRoot == transform || animationRoot.parent != transform)
        {
            error = name + ": AnimationRoot doit etre un enfant direct distinct du ActorRoot.";
            return false;
        }

        if (animationRoot.localPosition.sqrMagnitude > 0.000001f ||
            Quaternion.Angle(animationRoot.localRotation, Quaternion.identity) > 0.01f ||
            (animationRoot.localScale - Vector3.one).sqrMagnitude > 0.000001f)
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

        Animator[] gameplayAnimators = GetComponentsInChildren<Animator>(true);
        int controllerCount = 0;
        for (int i = 0; i < gameplayAnimators.Length; i++)
        {
            if (gameplayAnimators[i].runtimeAnimatorController != null)
            {
                controllerCount++;
            }
        }

        if (controllerCount != 1)
        {
            error = name + ": un seul Animator de gameplay est autorise (trouves: " + controllerCount + ").";
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
        ResolveReferences();
        ResetAnimationRootPose();
        cinematicSessionToken = sessionToken;
        firstCinematicDeltaLogged = false;
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
        Debug.Log("[Combat Actor Motion] End | actor='" + name + "' | token=" + sessionToken +
                  " | actorPos=" + transform.position + " | animationRootLocal=" +
                  (animationRoot != null ? animationRoot.localPosition.ToString() : "None") + ".", this);
    }

    public void ApplyAnimationDelta(Vector3 worldDeltaPosition, Quaternion deltaRotation)
    {
        if (!IsCinematicMotionActive)
        {
            return;
        }

        if (!firstCinematicDeltaLogged &&
            (worldDeltaPosition.sqrMagnitude > 0.00000025f ||
             Quaternion.Angle(deltaRotation, Quaternion.identity) > 0.01f))
        {
            firstCinematicDeltaLogged = true;
            Debug.Log("[Combat Actor Motion] First delta | actor='" + name + "' | token=" + cinematicSessionToken +
                      " | animator='" + (animator != null ? animator.name : "None") + "' | deltaPos=" +
                      worldDeltaPosition + " | deltaRot=" + deltaRotation.eulerAngles + " | animationRootLocal=" +
                      (animationRoot != null ? animationRoot.localPosition.ToString() : "None") + ".", this);
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
            animationRoot = transform.Find("AnimationRoot");
        }

        if (animator == null)
        {
            animator = animationRoot != null ? animationRoot.GetComponentInChildren<Animator>(true) : null;
        }

        if (lockPoint == null)
        {
            lockPoint = transform.Find("EnemyLockPoint");
        }

        if (rootMotionRelay == null && animator != null)
        {
            rootMotionRelay = animator.GetComponent<CombatActorRootMotionRelay>();
        }
    }
}
