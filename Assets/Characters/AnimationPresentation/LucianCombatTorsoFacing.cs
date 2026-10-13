using UnityEngine;

/// <summary>Presentation only: keeps the authored feet/hips while aiming the combat torso along UCC's forward.</summary>
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class LucianCombatTorsoFacing : MonoBehaviour
{
    [Range(0, 75)] public float maximumCorrection = 65;
    [Min(.01f)] public float entryBlendSeconds = .08f;
    private Animator animator;
    private LitOpsiveLocomotionBridge bridge;
    private CombatTimeDomain timeDomain;
    private PlayerActionPresentationController presentation;
    private Transform spine, chest;
    private Quaternion spineBefore, chestBefore, spineApplied, chestApplied;
    private bool applied;
    private float weight;
    private static readonly int Idle = Animator.StringToHash("Base Layer.CombatIdle");
    private static readonly int Move = Animator.StringToHash("Base Layer.CombatLocomotion");
    public float AppliedYaw { get; private set; }
    public float BlendWeight => weight;

    private void Awake()
    {
        animator = GetComponent<CharacterAnimationController>()?.Animator ?? GetComponent<Animator>();
        bridge = GetComponent<LitOpsiveLocomotionBridge>();
        timeDomain = GetComponent<CombatTimeDomain>();
        presentation = GetComponent<PlayerActionPresentationController>();
    }
    private static bool IsLocomotion(AnimatorStateInfo state) => state.fullPathHash == Idle || state.fullPathHash == Move;
    private void LateUpdate()
    {
        // If the Animator did not evaluate (pause/culling), undo our previous pose before measuring again.
        // Never restore an old pose over a fresh animation or a new action owner.
        ReleasePose();
        bool eligible = animator != null && animator.isActiveAndEnabled && animator.isInitialized && animator.isHuman &&
            bridge != null && bridge.IsCombatLockActive && !bridge.IsCombatFreeSprint &&
            !bridge.IsCombatDirectionalEvasionFacing && !bridge.IsCinematicMotionSessionActive &&
            (presentation == null || !presentation.IsActionActive) &&
            IsLocomotion(animator.GetCurrentAnimatorStateInfo(0)) &&
            (!animator.IsInTransition(0) || IsLocomotion(animator.GetNextAnimatorStateInfo(0)));
        if (!eligible) { weight=0; AppliedYaw=0; return; }
        if (spine == null)
        {
            spine=animator.GetBoneTransform(HumanBodyBones.Spine);
            chest=animator.GetBoneTransform(HumanBodyBones.Chest);
        }
        if (spine == null || chest == null) return;
        float dt=timeDomain != null ? timeDomain.DeltaTime : Time.deltaTime;
        weight=Mathf.MoveTowards(weight,1,dt/Mathf.Max(.01f,entryBlendSeconds));
        spineBefore=spine.localRotation; chestBefore=chest.localRotation;
        AppliedYaw=AlignUpperBody(animator,transform.forward,transform.up,maximumCorrection,weight);
        spineApplied=spine.localRotation; chestApplied=chest.localRotation; applied=true;
    }

    /// <summary>Changes only spine/chest rotations; physical root and all lower-body transforms remain untouched.</summary>
    public static float AlignUpperBody(Animator rig, Vector3 forward, Vector3 up, float limit, float strength)
    {
        var left=rig.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        var right=rig.GetBoneTransform(HumanBodyBones.RightUpperArm);
        var spine=rig.GetBoneTransform(HumanBodyBones.Spine);
        var chest=rig.GetBoneTransform(HumanBodyBones.Chest);
        if (left == null || right == null || spine == null || chest == null) return 0;
        var bodyForward=Vector3.Cross(right.position-left.position,up);
        forward=Vector3.ProjectOnPlane(forward,up);
        if (bodyForward.sqrMagnitude < .0001f || forward.sqrMagnitude < .0001f) return 0;
        float yaw=Mathf.Clamp(Vector3.SignedAngle(bodyForward,forward,up),-limit,limit)*Mathf.Clamp01(strength);
        spine.rotation=Quaternion.AngleAxis(yaw*.4f,up)*spine.rotation;
        chest.rotation=Quaternion.AngleAxis(yaw*.6f,up)*chest.rotation;
        return yaw;
    }
    private void ReleasePose()
    {
        if (!applied) return;
        if (chest != null && Quaternion.Angle(chest.localRotation,chestApplied) < .001f) chest.localRotation=chestBefore;
        if (spine != null && Quaternion.Angle(spine.localRotation,spineApplied) < .001f) spine.localRotation=spineBefore;
        applied=false;
    }
    private void OnDisable() { ReleasePose(); weight=0; AppliedYaw=0; }
}
