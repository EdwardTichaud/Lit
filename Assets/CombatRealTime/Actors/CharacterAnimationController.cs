using UnityEngine;
using Unity.Netcode;

public enum CombatActorAnimatorContractMode { RootAnimator, LegacyChildAnimator }

/// <summary>Typed animation contract shared by player presentation and the enemy controller.</summary>
public abstract class CharacterAnimationController : NetworkBehaviour
{
    public abstract Transform ActorRoot { get; }
    public abstract Transform AnimationRoot { get; }
    public abstract Animator Animator { get; }
    public abstract Transform LockPoint { get; }
    public abstract CombatTimeDomain TimeDomain { get; }
    public abstract CombatActorAnimatorContractMode AnimatorContractMode { get; }
    public abstract bool UsesRootAnimator { get; }
    public abstract bool IsCinematicMotionActive { get; }
    public abstract bool ShouldConsumeAnimatorRootMotion { get; }
    public abstract void Configure(Transform configuredAnimationRoot, Animator configuredAnimator, Transform configuredLockPoint);
    public abstract bool ValidateContract(out string error);
    public abstract bool SetActorPose(Vector3 position, Quaternion rotation);
    public abstract void ResetAnimationRootPose();
    public abstract void BeginCinematicMotion(int sessionToken);
    public abstract void SetCinematicRootMotionRelayEnabled(bool enabled);
    public abstract void EnableRootMotionRelay();
    public abstract void EndCinematicMotion(int sessionToken);
    public abstract void ApplyAnimationDelta(Vector3 worldDeltaPosition, Quaternion deltaRotation);
}
