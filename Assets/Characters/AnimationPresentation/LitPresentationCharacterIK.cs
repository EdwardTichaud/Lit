using System.Collections.Generic;
using Opsive.UltimateCharacterController.Character;
using UnityEngine;

/// <summary>One UCC solver, retaining native Humanoid goals only for explicitly qualified ground cycles.</summary>
public sealed partial class LitPresentationCharacterIK : CharacterIK
{
    private Animator presentationAnimator;
    private LitOpsiveLocomotionBridge bridge;
    private PlayerActionPresentationController actions;
    private UltimateCharacterLocomotion physicalLocomotion;
    private readonly List<AnimatorClipInfo> clips=new List<AnimatorClipInfo>(20);
    private readonly List<NativeFootSupportSet.Input> supportInputs=new List<NativeFootSupportSet.Input>(4);
    public bool PreservingNativeFootIK {get;private set;}
    public bool RootSpaceGoalsApplied {get;private set;}
    public bool SupportBlendApplied {get;private set;}
    public int SupportEvaluationFrame {get;private set;}=-1;
    public int CompletedLateFrame {get;private set;}=-1;
    public Vector3 LeftSupportPositionLocal {get;private set;}
    public Vector3 RightSupportPositionLocal {get;private set;}
    public Vector3 LeftFinalPositionLocal {get;private set;}
    public Vector3 RightFinalPositionLocal {get;private set;}
    public float LeftGroundWeight {get;private set;}
    public float RightGroundWeight {get;private set;}
    public Quaternion LeftSupportRotation {get;private set;}
    public Quaternion RightSupportRotation {get;private set;}
    public Quaternion LeftFinalRotation {get;private set;}
    public Quaternion RightFinalRotation {get;private set;}
    private static readonly int Locomotion=Animator.StringToHash("Locomotion"),CombatLocomotion=Animator.StringToHash("CombatLocomotion");
#if UNITY_EDITOR
    private static readonly int WalkStop = Animator.StringToHash("Walk_Stop");
    private static readonly int CombatIdle = Animator.StringToHash("CombatIdle");
    private LocomotionPresentationProfile stopProbeProfile;
    private AnimationClip stopProbeClip;
    private NativeFootGoalCurves stopProbeGoals;
    private LocomotionPresentationProfile.Cycle combatIdleProbe;
    public bool KeepCombatDirectionForProbe { get; private set; }
    public float CombatBrakeFadeForProbe { get; private set; }
    public bool SmoothExplorationForProbe { get; set; }
    private readonly List<AnimatorClipInfo> nextProbeClips = new List<AnimatorClipInfo>(8);

    public void ConfigureStopProbe(LocomotionPresentationProfile profile, AnimationClip stop, NativeFootGoalCurves goals,
        LocomotionPresentationProfile.Cycle combatIdle, bool keepCombatDirection, float combatBrakeFade)
    {
        stopProbeProfile = profile;
        stopProbeClip = stop;
        stopProbeGoals = goals;
        combatIdleProbe = combatIdle;
        KeepCombatDirectionForProbe = keepCombatDirection;
        CombatBrakeFadeForProbe = combatBrakeFade;
    }

    private bool TryStopProbe(out NativeFootGoalCurves.Pose pose)
    {
        pose = default;
        if (stopProbeProfile == null || bridge.ActiveMeasuredPresentationProfile == null ||
            !bridge.PreserveWholeBodyLocomotion || actions?.IsActionActive == true ||
            Vector3.Dot(physicalLocomotion.GroundedRaycastHit.normal, physicalLocomotion.Up) < .999f ||
            !stopProbeProfile.footSupports.Matches(presentationAnimator)) return false;

        var current = presentationAnimator.GetCurrentAnimatorStateInfo(BaseLayerIndex);
        clips.Clear();
        presentationAnimator.GetCurrentAnimatorClipInfo(BaseLayerIndex, clips);
        if (!TryProbeState(current, clips, out var from, out float fromWeight)) return false;
        if (!presentationAnimator.IsInTransition(BaseLayerIndex))
        {
            pose = from;
            return true;
        }

        nextProbeClips.Clear();
        presentationAnimator.GetNextAnimatorClipInfo(BaseLayerIndex, nextProbeClips);
        if (!TryProbeState(presentationAnimator.GetNextAnimatorStateInfo(BaseLayerIndex), nextProbeClips,
            out var to, out float toWeight) || Mathf.Abs(fromWeight + toWeight - 1) > .005f) return false;
        float blend = toWeight / (fromWeight + toWeight);
        pose.left = Vector3.Lerp(from.left, to.left, blend);
        pose.right = Vector3.Lerp(from.right, to.right, blend);
        pose.leftRotation = Quaternion.Slerp(from.leftRotation, to.leftRotation, blend);
        pose.rightRotation = Quaternion.Slerp(from.rightRotation, to.rightRotation, blend);
        return true;
    }

    private bool TryProbeState(AnimatorStateInfo state, List<AnimatorClipInfo> leaves,
        out NativeFootGoalCurves.Pose pose, out float weight)
    {
        pose = default;
        weight = 0;
        foreach (var leaf in leaves) weight += leaf.weight;
        if (weight <= .00001f)
        {
            pose.leftRotation = pose.rightRotation = Quaternion.identity;
            return state.shortNameHash == Locomotion || state.shortNameHash == CombatLocomotion || state.shortNameHash == WalkStop ||
                state.shortNameHash == CombatIdle && combatIdleProbe != null;
        }
        if (state.shortNameHash == WalkStop)
        {
            if (leaves.Count != 1 || leaves[0].clip != stopProbeClip) return false;
            stopProbeGoals.Evaluate(Mathf.Clamp(state.normalizedTime, 0, .999999f),
                out pose.left, out pose.right, out pose.leftRotation, out pose.rightRotation);
            return true;
        }
        if (state.shortNameHash == CombatIdle)
        {
            if (combatIdleProbe == null || leaves.Count != 1 || leaves[0].clip != combatIdleProbe.clip) return false;
            combatIdleProbe.footGoals.Evaluate(state.normalizedTime + combatIdleProbe.cycleOffset,
                out pose.left, out pose.right, out pose.leftRotation, out pose.rightRotation);
            return true;
        }
        if (state.shortNameHash != Locomotion && state.shortNameHash != CombatLocomotion) return false;
        var inverse = Quaternion.Inverse(presentationAnimator.transform.rotation);
        return stopProbeProfile.footSupports.TryBlend(stopProbeProfile, leaves, state.normalizedTime,
            inverse * presentationAnimator.GetBoneTransform(HumanBodyBones.LeftFoot).rotation,
            inverse * presentationAnimator.GetBoneTransform(HumanBodyBones.RightFoot).rotation, supportInputs, out pose);
    }
#endif
    protected override void Awake()
    {
        base.Awake();
        presentationAnimator=GetComponent<Animator>();bridge=GetComponentInParent<LitOpsiveLocomotionBridge>();
        actions=GetComponentInParent<PlayerActionPresentationController>();
        physicalLocomotion=GetComponentInParent<UltimateCharacterLocomotion>();
    }
    public override void UpdateSolvers(int layerIndex)
    {
        RootSpaceGoalsApplied=false;
        SupportBlendApplied=false;
        NativeFootGoalCurves.Pose rootGoals = default;
        bool probeGoals = layerIndex == BaseLayerIndex && TryMeasuredRunStop(out rootGoals);
#if UNITY_EDITOR
        if (!probeGoals && layerIndex == BaseLayerIndex) probeGoals = TryStopProbe(out rootGoals);
#endif
        PreservingNativeFootIK=layerIndex==BaseLayerIndex&&(probeGoals||ShouldPreserve());
        Vector3 leftPosition=default,rightPosition=default;
        Quaternion leftRotation=Quaternion.identity,rightRotation=Quaternion.identity;
        if(PreservingNativeFootIK)
        {
            var profile=bridge.ActiveMeasuredPresentationProfile;
            bool ready=probeGoals;
            SupportBlendApplied=probeGoals;
            if(!ready&&!presentationAnimator.IsInTransition(BaseLayerIndex))
            {
                float phase=presentationAnimator.GetCurrentAnimatorStateInfo(BaseLayerIndex).normalizedTime;
                if(profile.footSupports!=null)
                {
                    if(profile.footSupports.Matches(presentationAnimator))
                    {
                        var inverseRoot=Quaternion.Inverse(presentationAnimator.transform.rotation);
                        ready=profile.footSupports.TryBlend(profile,clips,phase,inverseRoot*presentationAnimator.GetBoneTransform(HumanBodyBones.LeftFoot).rotation,
                            inverseRoot*presentationAnimator.GetBoneTransform(HumanBodyBones.RightFoot).rotation,supportInputs,out rootGoals);
                        SupportBlendApplied=ready;
                    }
                }
                else ready=NativeFootGoalCurves.Blend(profile,clips,phase,out rootGoals);
            }
            if(ready)
            {
                NativeFootGoalCurves.Apply(presentationAnimator,rootGoals);
                RootSpaceGoalsApplied=true;
            }
            // A state with built-in Foot IK disabled has no qualified native fallback
            // when an unexported Idle participates. Do not force undefined goals to weight one.
            else if(profile.footSupports!=null) PreservingNativeFootIK=false;
            if(PreservingNativeFootIK)
            {
                leftPosition=presentationAnimator.GetIKPosition(AvatarIKGoal.LeftFoot);rightPosition=presentationAnimator.GetIKPosition(AvatarIKGoal.RightFoot);
                leftRotation=presentationAnimator.GetIKRotation(AvatarIKGoal.LeftFoot);rightRotation=presentationAnimator.GetIKRotation(AvatarIKGoal.RightFoot);
                LeftSupportRotation=leftRotation;RightSupportRotation=rightRotation;
                SupportEvaluationFrame=Time.frameCount;
                LeftSupportPositionLocal=presentationAnimator.transform.InverseTransformPoint(leftPosition);
                RightSupportPositionLocal=presentationAnimator.transform.InverseTransformPoint(rightPosition);
                if(SupportBlendApplied&&physicalLocomotion!=null)
                {
                    // UCC subtracts its baseline ColliderSpacing from the cached hips
                    // after IK. Native targets have not included that offset yet.
                    // Precompensate only these goals, just as UCC's own ground goals do.
                    var baseline=physicalLocomotion.transform.TransformVector(Vector3.up*physicalLocomotion.ColliderSpacing);
                    leftPosition+=baseline;rightPosition+=baseline;
                    presentationAnimator.SetIKPosition(AvatarIKGoal.LeftFoot,leftPosition);
                    presentationAnimator.SetIKPosition(AvatarIKGoal.RightFoot,rightPosition);
                }
            }
        }
        // Leave UCC's ground correction, smoothing state and serialized override untouched.
        // Only the final weights of this eligible Animator evaluation are retained.
        base.UpdateSolvers(layerIndex);
        LeftGroundWeight=RightGroundWeight=0;
        if(!PreservingNativeFootIK)return;
        LeftGroundWeight=presentationAnimator.GetIKPositionWeight(AvatarIKGoal.LeftFoot);
        RightGroundWeight=presentationAnimator.GetIKPositionWeight(AvatarIKGoal.RightFoot);
        // Keep UCC's correction blend, rather than turning a partial ground correction
        // into a full, abruptly flattened foot merely because native retarget IK is required.
        presentationAnimator.SetIKPosition(AvatarIKGoal.LeftFoot,Vector3.Lerp(leftPosition,presentationAnimator.GetIKPosition(AvatarIKGoal.LeftFoot),LeftGroundWeight));
        presentationAnimator.SetIKPosition(AvatarIKGoal.RightFoot,Vector3.Lerp(rightPosition,presentationAnimator.GetIKPosition(AvatarIKGoal.RightFoot),RightGroundWeight));
        var leftGroundRotation=presentationAnimator.GetIKRotation(AvatarIKGoal.LeftFoot);
        var rightGroundRotation=presentationAnimator.GetIKRotation(AvatarIKGoal.RightFoot);
        if(SupportBlendApplied&&physicalLocomotion!=null)
        {
            // Qualified goals already include the native flat-ground foot pitch/roll.
            // UCC's upright LookRotation discards it and can flip near a vertical foot.
            // Add only the physical surface inclination; retain UCC's contact weights.
            var normal=physicalLocomotion.GroundedRaycastHit.normal;
            var tilt=normal.sqrMagnitude>.5f?Quaternion.FromToRotation(physicalLocomotion.Up,normal):Quaternion.identity;
            leftGroundRotation=tilt*leftRotation;rightGroundRotation=tilt*rightRotation;
        }
        presentationAnimator.SetIKRotation(AvatarIKGoal.LeftFoot,Quaternion.Slerp(leftRotation,leftGroundRotation,presentationAnimator.GetIKRotationWeight(AvatarIKGoal.LeftFoot)));
        presentationAnimator.SetIKRotation(AvatarIKGoal.RightFoot,Quaternion.Slerp(rightRotation,rightGroundRotation,presentationAnimator.GetIKRotationWeight(AvatarIKGoal.RightFoot)));
        presentationAnimator.SetIKPositionWeight(AvatarIKGoal.LeftFoot,1);
        presentationAnimator.SetIKPositionWeight(AvatarIKGoal.RightFoot,1);
        presentationAnimator.SetIKRotationWeight(AvatarIKGoal.LeftFoot,1);
        presentationAnimator.SetIKRotationWeight(AvatarIKGoal.RightFoot,1);
        LeftFinalRotation=presentationAnimator.GetIKRotation(AvatarIKGoal.LeftFoot);
        RightFinalRotation=presentationAnimator.GetIKRotation(AvatarIKGoal.RightFoot);
        LeftFinalPositionLocal=presentationAnimator.transform.InverseTransformPoint(presentationAnimator.GetIKPosition(AvatarIKGoal.LeftFoot));
        RightFinalPositionLocal=presentationAnimator.transform.InverseTransformPoint(presentationAnimator.GetIKPosition(AvatarIKGoal.RightFoot));
    }
    // Preserve the vendor pass exactly once; expose its completion to final-pose diagnostics.
    public new void LateUpdate()
    {
        base.LateUpdate();
        CompletedLateFrame=Time.frameCount;
    }
    private bool ShouldPreserve()
    {
        if(!isActiveAndEnabled||presentationAnimator==null||bridge==null||!bridge.PreserveWholeBodyLocomotion||actions!=null&&actions.IsActionActive)return false;
        var profile=bridge.ActiveMeasuredPresentationProfile;
        if(profile==null)return false;
        var state=presentationAnimator.GetCurrentAnimatorStateInfo(BaseLayerIndex);
        if(state.shortNameHash!=Locomotion&&state.shortNameHash!=CombatLocomotion)return false;
        if(presentationAnimator.IsInTransition(BaseLayerIndex))
        {
            var next=presentationAnimator.GetNextAnimatorStateInfo(BaseLayerIndex);
            if(next.shortNameHash!=Locomotion&&next.shortNameHash!=CombatLocomotion)return false;
        }
        clips.Clear();presentationAnimator.GetCurrentAnimatorClipInfo(BaseLayerIndex,clips);
        bool any=false;
        foreach(var clip in clips)
        {
            if(clip.weight<.001f)continue;
            if(profile.RequiresNativeFootIK(clip.clip))any=true;
            else if(clip.clip!=null&&clip.clip.name.IndexOf("Idle",System.StringComparison.OrdinalIgnoreCase)<0)return false;
        }
        return any;
    }
}
