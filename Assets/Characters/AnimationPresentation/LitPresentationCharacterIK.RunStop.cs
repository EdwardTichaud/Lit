using System.Collections.Generic;
using UnityEngine;

public sealed partial class LitPresentationCharacterIK
{
    public const string RunStopFootParameter = "MeasuredRunStopFoot";
    private static readonly int RunStopFootHash = Animator.StringToHash(RunStopFootParameter);
    private static readonly int MeasuredRunStopHash = Animator.StringToHash("Run_Stop");
    private readonly List<AnimatorClipInfo> runStopNextClips = new List<AnimatorClipInfo>(4);
    private LocomotionPresentationProfile.RunStop runStopSession;
    private int runStopFoot;
    private int runStopRequestFrame;
    private bool runStopPlacementCaptured;
    private float runStopPlacement;

    private bool RunStopEligible(LocomotionPresentationProfile profile) =>
        profile != null && profile.runStop?.IsConfigured == true && profile.footSupports != null &&
        bridge.PreserveWholeBodyLocomotion && actions?.IsActionActive != true &&
        Vector3.Dot(physicalLocomotion.GroundedRaycastHit.normal, physicalLocomotion.Up) >= .999f &&
        profile.footSupports.Matches(presentationAnimator);

    public void PrepareMeasuredRunStop(LocomotionPresentationProfile profile, AnimatorStateInfo source, float blend)
    {
        runStopSession = null;
        runStopPlacementCaptured = false;
        runStopPlacement = 0;
        runStopFoot = 0;
        if (RunStopEligible(profile) && source.shortNameHash == Locomotion &&
            !presentationAnimator.IsInTransition(BaseLayerIndex) &&
            Mathf.Abs(blend - profile.explorationBlendThresholds.y) < .001f)
        {
            var supports = profile.footSupports;
            foreach (var cycle in supports.cycles)
            {
                if (cycle.clip != profile.explorationRun.clip) continue;
                float index = Mathf.Repeat(source.normalizedTime + cycle.cycleOffset, 1) * supports.steps;
                int lower = Mathf.Min(supports.steps - 1, Mathf.FloorToInt(index));
                var height = Vector2.Lerp(cycle.heights[lower], cycle.heights[lower + 1], index - lower);
                bool left = Mathf.Abs(height.x - profile.runStop.solePlane) <= .02f;
                bool right = Mathf.Abs(height.y - profile.runStop.solePlane) <= .02f;
                if (left != right)
                {
                    runStopSession = profile.runStop;
                    runStopFoot = left ? -1 : 1;
                    runStopRequestFrame = Time.frameCount;
                }
                break;
            }
        }
        presentationAnimator.SetFloat(RunStopFootHash, runStopFoot);
    }

    private bool TryMeasuredRunStop(out NativeFootGoalCurves.Pose pose)
    {
        pose = default;
        if (runStopSession == null && runStopFoot == 0) return false;
        var profile = bridge.ActiveMeasuredPresentationProfile;
        bool valid = runStopSession != null && profile?.runStop == runStopSession && RunStopEligible(profile);
        var current = presentationAnimator.GetCurrentAnimatorStateInfo(BaseLayerIndex);
        bool transitioning = presentationAnimator.IsInTransition(BaseLayerIndex);
        var next = transitioning ? presentationAnimator.GetNextAnimatorStateInfo(BaseLayerIndex) : default;
        bool participates = current.shortNameHash == MeasuredRunStopHash || next.shortNameHash == MeasuredRunStopHash;
        if (!valid || !participates)
        {
            if (!valid || Time.frameCount > runStopRequestFrame + 2) runStopSession = null;
            // Do not reweight Run_Stop while it still contributes to an interrupted pose.
            if (!participates && runStopSession == null)
            {
                runStopFoot = 0;
                presentationAnimator.SetFloat(RunStopFootHash, 0);
            }
            return false;
        }

        clips.Clear();
        presentationAnimator.GetCurrentAnimatorClipInfo(BaseLayerIndex, clips);
        if (!TryRunStopPose(profile,current,clips,out var from,out float fromWeight)) return false;
        if (!transitioning)
        {
            pose = from;
            return true;
        }

        if (!runStopPlacementCaptured && current.shortNameHash == Locomotion && next.shortNameHash == MeasuredRunStopHash &&
            next.normalizedTime <= .03f && clips.Count == 1 && clips[0].clip == profile.explorationRun.clip)
        {
            var run = profile.explorationRun;
            float phase = runStopFoot < 0 ? runStopSession.leftReferencePhase : runStopSession.rightReferencePhase;
            run.footGoals.Evaluate(phase + run.cycleOffset,out var left,out var right,out _,out _);
            runStopPlacement = Mathf.Clamp(runStopFoot < 0 ? from.left.z-left.z : from.right.z-right.z,-.32f,.32f);
            runStopPlacementCaptured = true;
        }

        runStopNextClips.Clear();
        presentationAnimator.GetNextAnimatorClipInfo(BaseLayerIndex,runStopNextClips);
        if (!TryRunStopPose(profile,next,runStopNextClips,out var to,out float toWeight) ||
            Mathf.Abs(fromWeight + toWeight - 1f) > .005f) return false;
        float weight = toWeight / (fromWeight + toWeight);
        pose.left = Vector3.Lerp(from.left,to.left,weight);
        pose.right = Vector3.Lerp(from.right,to.right,weight);
        pose.leftRotation = Quaternion.Slerp(from.leftRotation,to.leftRotation,weight);
        pose.rightRotation = Quaternion.Slerp(from.rightRotation,to.rightRotation,weight);
        return true;
    }

    private bool TryRunStopPose(LocomotionPresentationProfile profile, AnimatorStateInfo state, List<AnimatorClipInfo> leaves,
        out NativeFootGoalCurves.Pose pose, out float weight)
    {
        pose = default;
        weight = 0;
        if (state.shortNameHash != Locomotion && state.shortNameHash != MeasuredRunStopHash) return false;
        foreach (var leaf in leaves) weight += leaf.weight;
        if (weight <= .00001f)
        {
            pose.leftRotation = pose.rightRotation = Quaternion.identity;
            return true;
        }
        if (state.shortNameHash == MeasuredRunStopHash || (leaves.Count == 1 && leaves[0].clip == runStopSession.idle.clip))
        {
            var cycle = state.shortNameHash == MeasuredRunStopHash
                ? (runStopFoot < 0 ? runStopSession.left : runStopSession.right) : runStopSession.idle;
            if (leaves.Count != 1 || leaves[0].clip != cycle.clip) return false;
            float phase = state.shortNameHash == MeasuredRunStopHash
                ? Mathf.Clamp(state.normalizedTime,0,.999999f) : state.normalizedTime + cycle.cycleOffset;
            cycle.footGoals.Evaluate(phase,out pose.left,out pose.right,out pose.leftRotation,out pose.rightRotation);
            if (state.shortNameHash == MeasuredRunStopHash)
            {
                float offset = runStopPlacement * (1f - Mathf.SmoothStep(0,1,Mathf.InverseLerp(.51f,.92f,phase)));
                if (runStopFoot < 0) pose.left.z += offset;
                else pose.right.z += offset;
            }
            return true;
        }

        var inverse = Quaternion.Inverse(presentationAnimator.transform.rotation);
        return profile.footSupports.TryBlend(profile,leaves,state.normalizedTime,
            inverse * presentationAnimator.GetBoneTransform(HumanBodyBones.LeftFoot).rotation,
            inverse * presentationAnimator.GetBoneTransform(HumanBodyBones.RightFoot).rotation,supportInputs,out pose);
    }
}
