using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Actual-avatar blend probes; source-native reference only, without gameplay behaviours.</summary>
public static class LucianStrideBlendAudit
{
    public static void RunRootGoals()=>Run("root-goals",true);
    const string Folder="Library/LucianCoherence/stride-blends";
    [Serializable] sealed class PoseDetail
    {
        public string clip;public float phase,leftSole,rightSole,leftKnee,rightKnee;
        public Vector3 pelvis,leftFoot,rightFoot,leftGoal,rightGoal,leftSolePoint,rightSolePoint,leftSupportOffset,rightSupportOffset;
        public Quaternion leftRotation,rightRotation;
    }
    [Serializable] sealed class DetailReport
    {
        public string scope="Static actual-avatar worst walking blend, no UCC. Source samples matched within 1/4800 cycle.";
        public List<PoseDetail> poses=new List<PoseDetail>();
        public int leftVertices,rightVertices;
        public float predictedLeftSole,predictedRightSole,desiredLeftSole,desiredRightSole;
        public Vector2 testGoalCorrection;
        public float testPelvisDifference,testRootDifference;
    }
    public static void InspectWorstWalk()
    {
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(LucianCadencePreview.Folder+"/LocomotionMeasurements.asset");
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder+"/LucianCadence.overrideController");
        UnityEngine.Animations.AnimatorControllerPlayable active=default;
        var phases=new List<float>();var weights=new List<AnimatorClipInfo>();
        using(var sampler=new ActorPoseSampler(ActorAnimationAudit.Actor(false)))
        {
            void Configure(UnityEngine.Animations.AnimatorControllerPlayable p)
            {
                active=p;float angle=112.5f*Mathf.Deg2Rad;
                p.SetFloat("LitSpeed",profile.blendThresholds.x);p.SetFloat("LocomotionTier",profile.blendThresholds.x);p.SetFloat("LocomotionPlaybackRate",1);
                p.SetFloat("HorizontalMovement",Mathf.Sin(angle));p.SetFloat("ForwardMovement",Mathf.Cos(angle));p.SetFloat("CombatMoveMagnitude",1);
                p.SetBool("CombatStrafeActive",true);p.SetBool("IsMoving",true);p.SetLayerWeight(1,0);p.SetLayerWeight(2,0);p.Play("Base Layer.CombatLocomotion",0,0);
            }
            void Goals(Animator a,Vector2 correction)
            {
                weights=active.GetCurrentAnimatorClipInfo(0).Where(c=>c.weight>.001f).ToList();
                if(!NativeFootGoalCurves.Blend(profile,weights,active.GetCurrentAnimatorStateInfo(0).normalizedTime,out var goals))throw new InvalidOperationException("Missing root goal data");
                goals.left.y+=correction.x;goals.right.y+=correction.y;
                NativeFootGoalCurves.Apply(a,goals);a.SetIKPositionWeight(AvatarIKGoal.LeftFoot,1);a.SetIKPositionWeight(AvatarIKGoal.RightFoot,1);a.SetIKRotationWeight(AvatarIKGoal.LeftFoot,1);a.SetIKRotationWeight(AvatarIKGoal.RightFoot,1);
            }
            var frames=sampler.Sample(profile.walk[0].clip,sampleCount:480,measureSoles:true,controller:controller,configureController:Configure,
                poseCorrection:a=>phases.Add(active.GetCurrentAnimatorStateInfo(0).normalizedTime),modifyIKGoals:a=>Goals(a,Vector2.zero));
            int worst=Enumerable.Range(1,frames.Length-1).OrderByDescending(i=>Mathf.Min(frames[i].leftSole.y,frames[i].rightSole.y)).First();
            var report=new DetailReport();report.poses.Add(Describe("mixed",phases[worst],frames[worst]));
            var leftShapes=new List<Dictionary<string,Vector3>>();var rightShapes=new List<Dictionary<string,Vector3>>();
            foreach(var weight in weights)
            {
                var cycle=profile.FindCycle(weight.clip);float phase=Mathf.Repeat(phases[worst]+cycle.cycleOffset,1);
                var l=new Dictionary<string,Vector3>();var r=new Dictionary<string,Vector3>();
                var source=sampler.Sample(weight.clip,sampleCount:2400,measureSoles:true,applyFootIK:true,captureIKGoals:true,geometrySample:(a,soles,index)=>
                {
                    if(index!=Mathf.RoundToInt(phase*2400))return;
                    void Capture(bool left,Dictionary<string,Vector3> result)
                    {
                        var bone=a.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                        foreach(var point in soles.FootPoints(left))
                        {
                            if(!soles.TryPosition(point,out var position))throw new InvalidOperationException("Foot geometry unavailable");
                            result.Add(point.Id,Quaternion.Inverse(bone.rotation)*(position-bone.position));
                        }
                    }
                    Capture(true,l);Capture(false,r);
                });
                leftShapes.Add(l);rightShapes.Add(r);
                report.poses.Add(Describe(weight.clip.name,phase,source[Mathf.RoundToInt(phase*2400)]));
            }
            float Predict(List<Dictionary<string,Vector3>> shapes,Vector3 foot,Quaternion rotation)
            {
                if(shapes.Any(s=>!s.Keys.SequenceEqual(shapes[0].Keys)))throw new InvalidOperationException("Source mesh identities differ");
                float low=float.PositiveInfinity;
                foreach(string key in shapes[0].Keys)
                {
                    Vector3 p=default;for(int i=0;i<shapes.Count;i++)p+=shapes[i][key]*weights[i].weight;
                    low=Mathf.Min(low,(foot+rotation*p).y-.10000038f);
                }
                return low;
            }
            report.leftVertices=leftShapes[0].Count;report.rightVertices=rightShapes[0].Count;
            report.predictedLeftSole=Predict(leftShapes,frames[worst].leftFoot,frames[worst].leftRotation);
            report.predictedRightSole=Predict(rightShapes,frames[worst].rightFoot,frames[worst].rightRotation);
            for(int i=0;i<weights.Count;i++){report.desiredLeftSole+=report.poses[i+1].leftSole*weights[i].weight;report.desiredRightSole+=report.poses[i+1].rightSole*weights[i].weight;}
            report.testGoalCorrection=new Vector2(report.desiredLeftSole-report.predictedLeftSole,report.desiredRightSole-report.predictedRightSole);
            // A single-pose feasibility test, deliberately NOT a runtime policy or
            // validation of these constant deltas elsewhere in the cycle.
            var corrected=sampler.Sample(profile.walk[0].clip,sampleCount:480,measureSoles:true,controller:controller,configureController:Configure,
                modifyIKGoals:a=>Goals(a,report.testGoalCorrection));
            report.poses.Add(Describe("single-pose contact-height test (not a complete-cycle correction)",phases[worst],corrected[worst]));
            report.testPelvisDifference=Vector3.Distance(corrected[worst].body,frames[worst].body);
            report.testRootDifference=Vector3.Distance(corrected[worst].root,frames[worst].root);
            Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/worst-walk-detail.json",JsonUtility.ToJson(report,true));
        }
    }
    static PoseDetail Describe(string name,float phase,ActorPoseSampler.Frame f)
    {
        float Knee(HumanBodyBones thigh,HumanBodyBones knee,HumanBodyBones foot)=>Vector3.Angle(f.bonePositions[(int)thigh]-f.bonePositions[(int)knee],f.bonePositions[(int)foot]-f.bonePositions[(int)knee]);
        return new PoseDetail {clip=name,phase=phase,leftSole=f.leftSole.y-.10000038f,rightSole=f.rightSole.y-.10000038f,pelvis=f.body,leftFoot=f.leftFoot,rightFoot=f.rightFoot,
            leftGoal=f.leftGoal,rightGoal=f.rightGoal,leftRotation=f.leftRotation,rightRotation=f.rightRotation,
            leftSolePoint=f.leftSole,rightSolePoint=f.rightSole,leftSupportOffset=Quaternion.Inverse(f.leftRotation)*(f.leftSole-f.leftFoot),rightSupportOffset=Quaternion.Inverse(f.rightRotation)*(f.rightSole-f.rightFoot),
            leftKnee=Knee(HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot),rightKnee=Knee(HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot)};
    }
    [Serializable] sealed class Row {public string gait;public float angle,minSole,maxLowerSole,bothAirFraction;public string[] clips;}
    [Serializable] sealed class Report {public string scope="Static root, actual avatar and complete controller. No UCC/terrain/action acceptance.";public List<Row> cases=new List<Row>();}
    public static void Run(string label,bool rootGoals=false)
    {
        Directory.CreateDirectory(Folder+"/"+label);
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(LucianCadencePreview.Folder+"/LocomotionMeasurements.asset");
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder+"/LucianCadence.overrideController");
        var report=new Report();
        using(var sampler=new ActorPoseSampler(ActorAnimationAudit.Actor(false)))
        foreach(bool run in new[]{false,true})for(int direction=0;direction<16;direction++)
        {
            float angle=direction*22.5f;var vector=new Vector2(Mathf.Sin(angle*Mathf.Deg2Rad),Mathf.Cos(angle*Mathf.Deg2Rad));
            UnityEngine.Animations.AnimatorControllerPlayable active=default;string[] observed=null;
            var frames=sampler.Sample(run?profile.run[0].clip:profile.walk[0].clip,sampleCount:240,measureSoles:true,cycles:2,controller:controller,configureController:p=>
            {
                active=p;p.SetFloat("LitSpeed",run?profile.blendThresholds.y:profile.blendThresholds.x);
                p.SetFloat("LocomotionTier",run?profile.blendThresholds.y:profile.blendThresholds.x);p.SetFloat("LocomotionPlaybackRate",1);
                p.SetFloat("HorizontalMovement",vector.x);p.SetFloat("ForwardMovement",vector.y);p.SetFloat("CombatMoveMagnitude",1);
                p.SetBool("CombatStrafeActive",true);p.SetBool("IsMoving",true);p.SetLayerWeight(1,0);p.SetLayerWeight(2,0);
                p.Play("Base Layer.CombatLocomotion",0,0);
            },poseCorrection:a=>
            {
                if(active.GetCurrentAnimatorStateInfo(0).shortNameHash!=Animator.StringToHash("CombatLocomotion"))throw new InvalidOperationException("Blend probe left combat locomotion");
                if(observed==null)observed=active.GetCurrentAnimatorClipInfo(0).Where(c=>c.weight>.001f).Select(c=>c.clip.name).ToArray();
            },modifyIKGoals:rootGoals?a=>
            {
                var clips=active.GetCurrentAnimatorClipInfo(0).ToList();
                if(!NativeFootGoalCurves.Blend(profile,clips,active.GetCurrentAnimatorStateInfo(0).normalizedTime,out var goals))throw new InvalidOperationException("Root goal metadata missing");
                NativeFootGoalCurves.Apply(a,goals);a.SetIKPositionWeight(AvatarIKGoal.LeftFoot,1);a.SetIKPositionWeight(AvatarIKGoal.RightFoot,1);a.SetIKRotationWeight(AvatarIKGoal.LeftFoot,1);a.SetIKRotationWeight(AvatarIKGoal.RightFoot,1);
            }:null);
            var row=new Row {gait=run?"run":"walk",angle=angle,minSole=frames.Min(f=>Mathf.Min(f.leftSole.y,f.rightSole.y))-.10000038f,maxLowerSole=frames.Max(f=>Mathf.Min(f.leftSole.y,f.rightSole.y))-.10000038f,
                bothAirFraction=(float)frames.Count(f=>Mathf.Min(f.leftSole.y,f.rightSole.y)>.13000038f)/frames.Length,clips=observed};
            report.cases.Add(row);
            File.WriteAllLines(Folder+"/"+label+"/"+row.gait+"-"+direction+".csv",new[]{"time,left_sole,right_sole,pelvis_y"}.Concat(frames.Select(f=>string.Join(",",new[]{f.time,f.leftSole.y-.10000038f,f.rightSole.y-.10000038f,f.body.y}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))))));
        }
        File.WriteAllText(Folder+"/"+label+"/report.json",JsonUtility.ToJson(report,true));
    }
    public static void AlignPreviewPhases()
    {
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(LucianCadencePreview.Folder+"/LocomotionMeasurements.asset");
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder+"/LucianCadence.overrideController");
        var graph=(AnimatorController)controller.runtimeAnimatorController;
        var overrides=new List<KeyValuePair<AnimationClip,AnimationClip>>();controller.GetOverrides(overrides);
        var phases=profile.walk.Concat(profile.run).Concat(new[]{profile.explorationWalk,profile.explorationRun}).ToDictionary(c=>c.clip,c=>c);
        bool profileChanged=false;
        foreach(var state in LocomotionPresentationCalibration.States(graph.layers[0].stateMachine))
        {
            if(state.name!="Locomotion"&&state.name!="CombatLocomotion")continue;
            Align(state.motion);
        }
        if(profileChanged){EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);}
        void Align(Motion motion)
        {
            if(motion is not BlendTree tree)return;
            if(AssetDatabase.GetAssetPath(tree)!=AssetDatabase.GetAssetPath(graph))throw new InvalidOperationException("Shared tree cannot be changed by the preview");
            var children=tree.children;bool changed=false;
            for(int i=0;i<children.Length;i++)
            {
                if(children[i].motion is BlendTree){Align(children[i].motion);continue;}
                if(children[i].motion is not AnimationClip key)continue;
                var effective=overrides.Single(p=>p.Key==key).Value??key;
                if(!phases.TryGetValue(effective,out var cycle))continue;
                // A common phase: left support centred at 0.5, right support at 0.
                float left=2*Mathf.PI*(cycle.leftContactPhase-.5f),right=2*Mathf.PI*cycle.rightContactPhase;
                float offset=Mathf.Repeat(Mathf.Atan2(Mathf.Sin(left)+Mathf.Sin(right),Mathf.Cos(left)+Mathf.Cos(right))/(2*Mathf.PI),1);
                if(cycle.cycleOffset!=offset){cycle.cycleOffset=offset;profileChanged=true;}
                if(Mathf.Abs(Mathf.DeltaAngle(children[i].cycleOffset*360,offset*360))<.0001f)continue;
                children[i].cycleOffset=offset;changed=true;
            }
            if(changed){tree.children=children;EditorUtility.SetDirty(tree);AssetDatabase.SaveAssetIfDirty(tree);}
        }
    }
}
