using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

/// <summary>Independent final-bone comparison, native Humanoid retarget versus qualified scripted goals.</summary>
public static class LucianBoneFidelityAudit
{
    [Serializable] sealed class Row
    {
        public string clip,worstBone;
        public float feet,knees,hips,torso,allBones,footRotation,soleHeight;
        public int samples;
    }
    [Serializable] sealed class Report
    {
        public string scope="Actual Lucian rig and skin. Pure native points, same phase/controller motion. Native Foot IK true versus scripted support with Foot IK false. No UCC scripts, transitions, physics or visual acceptance.";
        public List<Row> cycles=new List<Row>();
    }

    [Serializable] sealed class IdleBlendRow
    {
        public float walkWeight, peakFootDisplacement, peakFootRotation, peakSoleHeight;
        public float peakHipsDisplacement, phaseAtPeakFootDisplacement;
        public float lowestNativeSole, highestNativeLowerSole;
    }

    [Serializable] sealed class IdleBlendReport
    {
        public string scope = "Same Lucian exploration tree, avatar, weights and phase with Unity Foot IK on and off. No UCC, trajectory, transitions or production writes.";
        public List<IdleBlendRow> blends = new List<IdleBlendRow>();
        public float scriptedFootDisplacementWithNativeFlag, scriptedFootRotationWithNativeFlag;
    }

    public static void RunIdleBlend()
    {
        var profile = AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(NativeFootSupportPreparation.ProfilePath);
        NativeFootSupportPreparation.Validate(profile, profile.footSupports, true);
        var source = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder + "/LucianCadence.overrideController");
        var transient = new List<UnityEngine.Object>();
        var report = new IdleBlendReport();
        var walk = profile.explorationWalk.clip;
        try
        {
            var native = LucianContactBlendAudit.CreateMuscleController(source, transient, "Locomotion", true);
            var muscle = LucianContactBlendAudit.CreateMuscleController(source, transient, "Locomotion", false);
            using var sampler = new ActorPoseSampler(ActorAnimationAudit.Actor(false));
            foreach (float walkWeight in new[] { 0f, .22f, .5f, .78f, .98f, 1f })
            {
                AnimatorControllerPlayable active = default;
                void Configure(AnimatorControllerPlayable p)
                {
                    active = p;
                    p.SetFloat("LitSpeed", profile.explorationBlendThresholds.x * walkWeight);
                    p.SetFloat("LocomotionPlaybackRate", 1);
                    p.Play("Base Layer.Locomotion", 0, 0);
                }

                float[] phases = new float[121];
                var withIk = sampler.Sample(walk, sampleCount: 120, measureSoles: true, controller: native,
                    configureController: Configure, warmControllerPose: true, geometrySample: (_, _, index) =>
                    {
                        float measuredWeight = active.GetCurrentAnimatorClipInfo(0).Where(c => c.clip == walk).Sum(c => c.weight);
                        if (Mathf.Abs(measuredWeight - walkWeight) > .002f)
                            throw new InvalidOperationException($"Walk weight {measuredWeight} differs from {walkWeight}.");
                        phases[index] = active.GetCurrentAnimatorStateInfo(0).normalizedTime;
                    });
                var withoutIk = sampler.Sample(walk, sampleCount: 120, measureSoles: true, controller: muscle,
                    configureController: Configure, warmControllerPose: true, geometrySample: (_, _, index) =>
                    {
                        if (Mathf.Abs(active.GetCurrentAnimatorStateInfo(0).normalizedTime - phases[index]) > .0001f)
                            throw new InvalidOperationException("The two pose graphs are out of phase.");
                    });
                var row = new IdleBlendRow
                {
                    walkWeight = walkWeight,
                    lowestNativeSole = float.PositiveInfinity,
                    highestNativeLowerSole = float.NegativeInfinity
                };
                for (int i = 0; i < withIk.Length; ++i)
                {
                    var a = withIk[i];
                    var b = withoutIk[i];
                    float displacement = Mathf.Max(Vector3.Distance(a.leftFoot, b.leftFoot), Vector3.Distance(a.rightFoot, b.rightFoot));
                    if (displacement > row.peakFootDisplacement)
                    {
                        row.peakFootDisplacement = displacement;
                        row.phaseAtPeakFootDisplacement = phases[i];
                    }
                    row.peakFootRotation = Mathf.Max(row.peakFootRotation, Quaternion.Angle(a.leftRotation, b.leftRotation), Quaternion.Angle(a.rightRotation, b.rightRotation));
                    row.peakSoleHeight = Mathf.Max(row.peakSoleHeight, Mathf.Abs(a.leftSole.y - b.leftSole.y), Mathf.Abs(a.rightSole.y - b.rightSole.y));
                    row.peakHipsDisplacement = Mathf.Max(row.peakHipsDisplacement, Vector3.Distance(a.body, b.body));
                    float lower = Mathf.Min(a.leftSole.y, a.rightSole.y);
                    row.lowestNativeSole = Mathf.Min(row.lowestNativeSole, lower);
                    row.highestNativeLowerSole = Mathf.Max(row.highestNativeLowerSole, lower);
                }
                report.blends.Add(row);
                if (walkWeight != 1f) continue;

                // Probe whether the flag can supply goals for a partial Idle blend
                // while still allowing the qualified solver to replace them at full walk.
                var clips = new List<AnimatorClipInfo>();
                var inputs = new List<NativeFootSupportSet.Input>();
                ActorPoseSampler.Frame[] OverrideGoals(RuntimeAnimatorController controller)
                {
                    return sampler.Sample(walk, sampleCount: 120, controller: controller,
                        configureController: Configure, warmControllerPose: true, modifyIKGoals: animator =>
                        {
                            clips.Clear();
                            clips.AddRange(active.GetCurrentAnimatorClipInfo(0));
                            var inverse = Quaternion.Inverse(animator.transform.rotation);
                            if (!profile.footSupports.TryBlend(profile, clips, active.GetCurrentAnimatorStateInfo(0).normalizedTime,
                                inverse * animator.GetBoneTransform(HumanBodyBones.LeftFoot).rotation,
                                inverse * animator.GetBoneTransform(HumanBodyBones.RightFoot).rotation, inputs, out var pose))
                                throw new InvalidOperationException("Full walk did not resolve qualified IK goals.");
                            NativeFootGoalCurves.Apply(animator, pose);
                            foreach (var foot in new[] { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot })
                            {
                                animator.SetIKPositionWeight(foot, 1);
                                animator.SetIKRotationWeight(foot, 1);
                            }
                        });
                }

                var scripted = OverrideGoals(muscle);
                var scriptedWithNativeFlag = OverrideGoals(native);
                for (int i = 0; i < scripted.Length; ++i)
                {
                    var a = scripted[i];
                    var b = scriptedWithNativeFlag[i];
                    report.scriptedFootDisplacementWithNativeFlag = Mathf.Max(report.scriptedFootDisplacementWithNativeFlag,
                        Vector3.Distance(a.leftFoot, b.leftFoot), Vector3.Distance(a.rightFoot, b.rightFoot));
                    report.scriptedFootRotationWithNativeFlag = Mathf.Max(report.scriptedFootRotationWithNativeFlag,
                        Quaternion.Angle(a.leftRotation, b.leftRotation), Quaternion.Angle(a.rightRotation, b.rightRotation));
                }
            }
            File.WriteAllText("Library/LucianCoherence/idle-blend-fidelity.json", JsonUtility.ToJson(report, true));
        }
        finally
        {
            for (int i = transient.Count - 1; i >= 0; --i)
                if (transient[i] != null) UnityEngine.Object.DestroyImmediate(transient[i]);
        }
    }

    public static void Run()
    {
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(NativeFootSupportPreparation.ProfilePath);
        NativeFootSupportPreparation.Validate(profile,profile.footSupports,true);
        var source=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder+"/LucianCadence.overrideController");
        var transient=new List<UnityEngine.Object>();var report=new Report();
        var clips=new List<AnimatorClipInfo>();var inputs=new List<NativeFootSupportSet.Input>();
        try
        {
            var references=new[]{LucianContactBlendAudit.CreateMuscleController(source,transient,"CombatLocomotion",true),LucianContactBlendAudit.CreateMuscleController(source,transient,"Locomotion",true)};
            var candidates=new[]{LucianContactBlendAudit.CreateMuscleController(source,transient,"CombatLocomotion",false),LucianContactBlendAudit.CreateMuscleController(source,transient,"Locomotion",false)};
            using(var sampler=new ActorPoseSampler(ActorAnimationAudit.Actor(false)))
            foreach(var cycle in profile.walk.Concat(profile.run).Concat(new[]{profile.explorationWalk,profile.explorationRun}))
            {
                bool exploration=cycle==profile.explorationWalk||cycle==profile.explorationRun;
                bool running=cycle==profile.explorationRun||profile.run.Contains(cycle);
                int graph=exploration?1:0;var thresholds=exploration?profile.explorationBlendThresholds:profile.blendThresholds;
                float tier=running?thresholds.y:thresholds.x;var direction=cycle.direction.normalized;
                AnimatorControllerPlayable active=default;
                void Configure(AnimatorControllerPlayable p)
                {
                    active=p;p.SetFloat("LitSpeed",tier);p.SetFloat("LocomotionTier",tier);p.SetFloat("LocomotionPlaybackRate",1);
                    p.SetFloat("HorizontalMovement",direction.x);p.SetFloat("ForwardMovement",direction.y);p.SetFloat("CombatMoveMagnitude",1);
                    p.SetBool("CombatStrafeActive",!exploration);p.SetBool("IsMoving",true);p.SetLayerWeight(1,0);p.SetLayerWeight(2,0);
                    p.Play(exploration?"Base Layer.Locomotion":"Base Layer.CombatLocomotion",0,0);
                }
                var native=sampler.Sample(cycle.clip,sampleCount:120,measureSoles:true,controller:references[graph],configureController:Configure,warmControllerPose:true);
                var scripted=sampler.Sample(cycle.clip,sampleCount:120,measureSoles:true,controller:candidates[graph],configureController:Configure,warmControllerPose:true,modifyIKGoals:a=>
                {
                    clips.Clear();clips.AddRange(active.GetCurrentAnimatorClipInfo(0));
                    if(clips.Where(c=>c.clip==cycle.clip).Sum(c=>c.weight)<.999f)throw new InvalidOperationException("Native point must remain pure");
                    var inverse=Quaternion.Inverse(a.transform.rotation);
                    if(!profile.footSupports.TryBlend(profile,clips,active.GetCurrentAnimatorStateInfo(0).normalizedTime,
                        inverse*a.GetBoneTransform(HumanBodyBones.LeftFoot).rotation,inverse*a.GetBoneTransform(HumanBodyBones.RightFoot).rotation,inputs,out var pose))throw new InvalidOperationException("Unqualified support");
                    NativeFootGoalCurves.Apply(a,pose);
                    a.SetIKPositionWeight(AvatarIKGoal.LeftFoot,1);a.SetIKPositionWeight(AvatarIKGoal.RightFoot,1);
                    a.SetIKRotationWeight(AvatarIKGoal.LeftFoot,1);a.SetIKRotationWeight(AvatarIKGoal.RightFoot,1);
                });
                var row=new Row {clip=cycle.clip.name,samples=native.Length};
                for(int i=0;i<native.Length;i++)
                {
                    var a=native[i];var b=scripted[i];
                    row.feet=Mathf.Max(row.feet,Vector3.Distance(a.leftFoot,b.leftFoot),Vector3.Distance(a.rightFoot,b.rightFoot));
                    row.footRotation=Mathf.Max(row.footRotation,Quaternion.Angle(a.leftRotation,b.leftRotation),Quaternion.Angle(a.rightRotation,b.rightRotation));
                    row.soleHeight=Mathf.Max(row.soleHeight,Mathf.Abs(a.leftSole.y-b.leftSole.y),Mathf.Abs(a.rightSole.y-b.rightSole.y));
                    for(int bone=0;bone<(int)HumanBodyBones.LastBone;bone++)
                    {
                        float error=Vector3.Distance(a.bonePositions[bone],b.bonePositions[bone]);
                        if(error>row.allBones){row.allBones=error;row.worstBone=((HumanBodyBones)bone).ToString();}
                        if(bone==(int)HumanBodyBones.LeftLowerLeg||bone==(int)HumanBodyBones.RightLowerLeg)row.knees=Mathf.Max(row.knees,error);
                        if(bone==(int)HumanBodyBones.Hips)row.hips=Mathf.Max(row.hips,error);
                        if(bone==(int)HumanBodyBones.Spine||bone==(int)HumanBodyBones.Chest||bone==(int)HumanBodyBones.Head)row.torso=Mathf.Max(row.torso,error);
                    }
                }
                report.cycles.Add(row);
            }
            File.WriteAllText("Library/LucianCoherence/native-bone-fidelity.json",JsonUtility.ToJson(report,true));
        }
        finally{for(int i=transient.Count-1;i>=0;i--)if(transient[i]!=null)UnityEngine.Object.DestroyImmediate(transient[i]);}
    }
}
