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
    }

    private void OnAnimatorMove()
    {
        if (!enabled || actor == null || animator == null || !actor.ShouldConsumeAnimatorRootMotion)
        {
            return;
        }

        actor.ApplyAnimationDelta(animator.deltaPosition, animator.deltaRotation);
    }
}
