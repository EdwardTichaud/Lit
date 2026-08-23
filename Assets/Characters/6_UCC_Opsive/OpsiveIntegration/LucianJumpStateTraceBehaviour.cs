using UnityEngine;

public sealed class LucianJumpStateTraceBehaviour : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponentInParent<LucianJumpPresentationController>()?.TraceAnimatorState("Enter", stateInfo);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponentInParent<LucianJumpPresentationController>()?.TraceAnimatorState("Exit", stateInfo);
    }
}
