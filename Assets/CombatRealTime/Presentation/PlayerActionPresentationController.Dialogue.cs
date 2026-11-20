using UnityEngine;

public sealed partial class PlayerActionPresentationController
{
    private Object dialoguePresentationOwner;
    private int dialoguePresentationState;
    public bool HasDialoguePresentation => dialoguePresentationOwner != null && animator != null && animator.isActiveAndEnabled &&
        (animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0).fullPathHash == dialoguePresentationState
            : animator.GetCurrentAnimatorStateInfo(0).fullPathHash == dialoguePresentationState);

    public bool PresentDialogue(Object owner, int stateHash, float blendSeconds, float offsetSeconds)
    {
        if (owner == null || !isActiveAndEnabled || IsActionActive || deathAnimationLocked ||
            animator == null || !animator.isActiveAndEnabled || !animator.HasState(0, stateHash) ||
            GetComponent<CharacterAnimationController>()?.IsCinematicMotionActive == true) return false;
        dialoguePresentationOwner = owner;
        dialoguePresentationState = stateHash;
        animator.CrossFadeInFixedTime(stateHash, Mathf.Max(.05f, blendSeconds), 0, offsetSeconds);
        return true;
    }

    public void ReleaseDialogue(Object owner, float blendSeconds = .2f, bool returnToLocomotion = true)
    {
        if (!ReferenceEquals(owner, dialoguePresentationOwner)) return;
        dialoguePresentationOwner = null;
        if (!returnToLocomotion || IsActionActive || deathAnimationLocked || animator == null ||
            !animator.isActiveAndEnabled || GetComponent<CharacterAnimationController>()?.IsCinematicMotionActive == true) return;
        if (animator.GetCurrentAnimatorStateInfo(0).fullPathHash != dialoguePresentationState &&
            (!animator.IsInTransition(0) || animator.GetNextAnimatorStateInfo(0).fullPathHash != dialoguePresentationState)) return;
        var squad = GetComponent<SquadCharacterController>();
        int destination = Animator.StringToHash(squad != null && squad.IsSittingRequested ? "Base Layer.Sitting_Idle" : ResolveCurrentLocomotionDestination());
        if (animator.HasState(0, destination)) animator.CrossFadeInFixedTime(destination, Mathf.Max(.05f, blendSeconds), 0);
    }
}
