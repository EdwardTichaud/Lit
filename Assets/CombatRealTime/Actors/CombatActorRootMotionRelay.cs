using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class CombatActorRootMotionRelay : MonoBehaviour
{
    [SerializeField] private CombatActorAnimationRoot actor;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (actor == null)
        {
            actor = GetComponentInParent<CombatActorAnimationRoot>();
        }

        LogDevelopmentContractDiagnostic();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnValidate()
    {
        animator = GetComponent<Animator>();
        actor ??= GetComponentInParent<CombatActorAnimationRoot>();
    }
#endif

    private void OnAnimatorMove()
    {
        if (!enabled || actor == null || animator == null || !actor.ShouldConsumeAnimatorRootMotion)
        {
            return;
        }

        actor.ApplyAnimationDelta(animator.deltaPosition, animator.deltaRotation);
    }

    private void LogDevelopmentContractDiagnostic()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (actor == null)
        {
            Debug.LogError("[CombatAnimatorContract] Root motion relay without CombatActorAnimationRoot.", this);
            return;
        }

        if (actor.Animator != animator)
        {
            Debug.LogError("[CombatAnimatorContract] Root motion relay must be on the resolved gameplay Animator.", this);
        }
#endif
    }
}
