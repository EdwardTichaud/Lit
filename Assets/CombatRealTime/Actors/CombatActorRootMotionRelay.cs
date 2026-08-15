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

#if UNITY_EDITOR
    private void OnValidate()
    {
        animator = GetComponent<Animator>();
        if (actor == null)
        {
            actor = GetComponentInParent<CombatActorAnimationRoot>();
        }
    }
#endif

    private void OnAnimatorMove()
    {
        if (!enabled || actor == null || animator == null || !actor.IsCinematicMotionActive)
        {
            return;
        }

        actor.ApplyAnimationDelta(animator.deltaPosition, animator.deltaRotation);
    }
}
