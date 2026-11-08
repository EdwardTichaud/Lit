using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Offline CC5-specific pilot. Encodes the same corrected pose in FK muscles, body and IK goals.</summary>
public static class LucianSupportPilot
{
    const string Output="Library/LucianCoherence/support-pilot";
    const string Folder="Assets/Characters/1_Squad/Lucian/Animation/CoherencePilot";
    [Serializable] sealed class Report
    {
        public string source,candidate,status;
        public float floor,manualBakeDifference,maximumDesiredPoseError,maximumUpperBodyPositionError,maximumIKPoseError,minimumSoleFK,minimumSoleIK,maximumRootMotion;
        public bool eventsPreserved;
        public int maximumProjectionIterations;
        public float minimumSubframeSoleFK,minimumSubframeSoleIK,maximumAnkleHorizontalChange,maximumFootRotationChange;
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        var actor=ActorAnimationAudit.Actor(false);
        var source=ActorAnimationAudit.Source(ActorAnimationAudit.Sources+"Twinblades_Common_Run_Loop_Inplace.FBX");
        var report=new Report {source=AssetDatabase.GetAssetPath(source),candidate=Folder+"/Lucian_Common_Run_Support.anim",status="Prevalidation"};
        // Use the obtained physical support plane, independently of an animation's lowest point.
        var lines=File.ReadAllLines("Docs/Animation/Lucian_Coherence_Cadence/soles.csv");var header=lines[0].Split(',');
        int floorIndex=Array.IndexOf(header,"floor_y"),rootIndex=Array.IndexOf(header,"root_y"),timeIndex=Array.IndexOf(header,"time");
        var floors=lines.Skip(1).Select(l=>l.Split(',')).Where(v=>float.Parse(v[timeIndex],CultureInfo.InvariantCulture)>1)
            .Select(v=>float.Parse(v[floorIndex],CultureInfo.InvariantCulture)-float.Parse(v[rootIndex],CultureInfo.InvariantCulture)).Where(v=>!float.IsNaN(v)&&!float.IsInfinity(v)).OrderBy(v=>v).ToArray();
        if(floors.Length<60 || floors.Last()-floors.First()>.005f)throw new InvalidOperationException("No stable physical support plane in baseline");
        report.floor=floors[floors.Length/2];
        var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        AnimationClip candidate=null;
        try
        {
            using(var sampler=new ActorPoseSampler(actor))
            {
                var manual=sampler.Sample(source,sampleCount:60,measureSoles:true);
                var baked=sampler.Sample(source,sampleCount:60,measureSoles:true,manualSoles:false);
                for(int i=0;i<manual.Length;i++)report.manualBakeDifference=Mathf.Max(report.manualBakeDifference,
                    Vector3.Distance(manual[i].leftSole,baked[i].leftSole),Vector3.Distance(manual[i].rightSole,baked[i].rightSole));
                if(report.manualBakeDifference>.002f)throw new InvalidOperationException("Manual soles disagree with Unity BakeMesh: "+report.manualBakeDifference);
                int count=Mathf.CeilToInt(source.length*240);
                var before=sampler.Sample(source,sampleCount:count,measureSoles:true);
                var muscles=Enumerable.Range(0,HumanTrait.MuscleCount).Where(i=>ActorFacingCalibration.IsLegMuscle(HumanTrait.MuscleName[i])).ToArray();
                var limbTwists=muscles.Where(i=>HumanTrait.MuscleName[i].Contains("Leg Twist")).ToArray();
                var values=new Dictionary<string,List<float>>();
                foreach(var muscle in muscles)values[HumanTrait.MuscleName[muscle]]=new List<float>();
                foreach(string prefix in new[]{"RootT","LeftFootT","RightFootT","LeftHandT","RightHandT"})foreach(char axis in "xyz")values[prefix+"."+axis]=new List<float>();
                foreach(string prefix in new[]{"RootQ","LeftFootQ","RightFootQ","LeftHandQ","RightHandQ"})foreach(char axis in "xyzw")values[prefix+"."+axis]=new List<float>();
                int frame=0;HumanPoseHandler handler=null;ActorSoleSampler soles=null;
                ActorPoseSampler.Frame[] desired;
                try
                {
                    desired=sampler.Sample(source,sampleCount:count,measureSoles:true,poseCorrection:a=>
                    {
                        if(handler==null) {handler=new HumanPoseHandler(a.avatar,a.transform);soles=new ActorSoleSampler(a,true);}
                        // Correct only mesh penetration. Authored airborne portions remain airborne.
                        var leftFoot=a.GetBoneTransform(HumanBodyBones.LeftFoot);var rightFoot=a.GetBoneTransform(HumanBodyBones.RightFoot);
                        var pose=new HumanPose();
                        handler.GetHumanPose(ref pose);
                        var authoredTwists=limbTwists.Select(i=>pose.muscles[i]).ToArray();
                        for(int pass=0;pass<12;pass++)
                        {
                            soles.Sample(out var left,out var right);
                            float l=Mathf.Max(0,report.floor-left.y),r=Mathf.Max(0,report.floor-right.y);
                            var leftGoal=before[frame].leftFoot;leftGoal.y=leftFoot.position.y+l;
                            var rightGoal=before[frame].rightFoot;rightGoal.y=rightFoot.position.y+r;
                            if(pass>0 && l<.0002f && r<.0002f && Vector3.Distance(leftFoot.position,leftGoal)<.0002f && Vector3.Distance(rightFoot.position,rightGoal)<.0002f)break;
                            if(Vector3.Distance(leftFoot.position,leftGoal)>.00001f)ActorFacingCalibration.SolveLeg(a,true,leftGoal,before[frame].bonePositions[(int)HumanBodyBones.LeftLowerLeg]);
                            if(Vector3.Distance(rightFoot.position,rightGoal)>.00001f)ActorFacingCalibration.SolveLeg(a,false,rightGoal,before[frame].bonePositions[(int)HumanBodyBones.RightLowerLeg]);
                            // Keep the authored ankle orientation; otherwise each Humanoid round trip
                            // feeds its small rotation error into the next solve and accumulates drift.
                            leftFoot.rotation=before[frame].leftRotation;
                            rightFoot.rotation=before[frame].rightRotation;
                            // Project into the actual Humanoid representation before measuring the next correction.
                            // A raw bone-space two-bone solve is not necessarily an exact muscle-space round trip.
                            handler.GetHumanPose(ref pose);
                            // A near-straight knee has an ambiguous axial decomposition. Retain
                            // the source limb twists instead of keying discontinuous inverse-solve branches.
                            for(int i=0;i<limbTwists.Length;i++)pose.muscles[limbTwists[i]]=authoredTwists[i];
                            handler.SetHumanPose(ref pose);
                            report.maximumProjectionIterations=Mathf.Max(report.maximumProjectionIterations,pass+1);
                        }
                        handler.GetHumanPose(ref pose);
                        foreach(var muscle in muscles)values[HumanTrait.MuscleName[muscle]].Add(pose.muscles[muscle]);
                        Vector("RootT",pose.bodyPosition);Rotation("RootQ",pose.bodyRotation);
                        Vector("LeftFootT",pose.ikGoalPositions[0]);Vector("RightFootT",pose.ikGoalPositions[1]);
                        Vector("LeftHandT",pose.ikGoalPositions[2]);Vector("RightHandT",pose.ikGoalPositions[3]);
                        // Clip curves require the internal convention, not the public, orientation-offset IK quaternions.
                        Rotation("LeftFootQ",pose.internalIkGoalRotations[0]);Rotation("RightFootQ",pose.internalIkGoalRotations[1]);
                        Rotation("LeftHandQ",pose.internalIkGoalRotations[2]);Rotation("RightHandQ",pose.internalIkGoalRotations[3]);
                        frame++;
                    });
                }
                finally {handler?.Dispose();soles?.Dispose();}
                candidate=new AnimationClip();EditorUtility.CopySerialized(source,candidate);candidate.name="Lucian_Common_Run_Support";
                foreach(var pair in values)
                {
                    var curve=new AnimationCurve(pair.Value.Select((v,i)=>new Keyframe(source.length*i/count,v)).ToArray());
                    for(int i=0;i<curve.length;i++) {AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);}
                    AnimationUtility.SetEditorCurve(candidate,EditorCurveBinding.FloatCurve("",typeof(Animator),pair.Key),curve);
                }
                candidate.EnsureQuaternionContinuity();
                File.WriteAllLines(Output+"/authored-poses.csv",new[]{"time,"+string.Join(",",values.Keys)}.Concat(Enumerable.Range(0,count+1).Select(i=>(source.length*i/count).ToString("R",CultureInfo.InvariantCulture)+","+string.Join(",",values.Values.Select(v=>v[i].ToString("R",CultureInfo.InvariantCulture))))));
                var after=sampler.Sample(candidate,sampleCount:count,measureSoles:true);
                var ik=sampler.Sample(candidate,sampleCount:count,measureSoles:true,applyFootIK:true);
                report.minimumSoleFK=after.Min(f=>Mathf.Min(f.leftSole.y,f.rightSole.y))-report.floor;
                report.minimumSoleIK=ik.Min(f=>Mathf.Min(f.leftSole.y,f.rightSole.y))-report.floor;
                for(int i=0;i<after.Length;i++)
                {
                    foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.Spine,HumanBodyBones.Chest})
                        report.maximumDesiredPoseError=Mathf.Max(report.maximumDesiredPoseError,Vector3.Distance(desired[i].bonePositions[(int)bone],after[i].bonePositions[(int)bone]));
                    foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.Head,HumanBodyBones.LeftHand,HumanBodyBones.RightHand})
                        report.maximumUpperBodyPositionError=Mathf.Max(report.maximumUpperBodyPositionError,Vector3.Distance(before[i].bonePositions[(int)bone],after[i].bonePositions[(int)bone]));
                    report.maximumIKPoseError=Mathf.Max(report.maximumIKPoseError,Vector3.Distance(after[i].leftFoot,ik[i].leftFoot),Vector3.Distance(after[i].rightFoot,ik[i].rightFoot));
                    report.maximumAnkleHorizontalChange=Mathf.Max(report.maximumAnkleHorizontalChange,Vector3.ProjectOnPlane(after[i].leftFoot-before[i].leftFoot,Vector3.up).magnitude,Vector3.ProjectOnPlane(after[i].rightFoot-before[i].rightFoot,Vector3.up).magnitude);
                    report.maximumFootRotationChange=Mathf.Max(report.maximumFootRotationChange,Quaternion.Angle(after[i].leftRotation,before[i].leftRotation),Quaternion.Angle(after[i].rightRotation,before[i].rightRotation));
                }
                var sub=sampler.Sample(candidate,sampleCount:count*2+1,measureSoles:true);
                var subIK=sampler.Sample(candidate,sampleCount:count*2+1,measureSoles:true,applyFootIK:true);
                File.WriteAllLines(Output+"/subframes.csv",new[]{"time,left_sole,right_sole,left_ik,right_ik,left_ankle_y,right_ankle_y"}.Concat(sub.Select((f,i)=>string.Join(",",new[]{f.time,f.leftSole.y-report.floor,f.rightSole.y-report.floor,subIK[i].leftSole.y-report.floor,subIK[i].rightSole.y-report.floor,f.leftFoot.y,f.rightFoot.y}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))))));
                report.minimumSubframeSoleFK=sub.Min(f=>Mathf.Min(f.leftSole.y,f.rightSole.y))-report.floor;
                report.minimumSubframeSoleIK=subIK.Min(f=>Mathf.Min(f.leftSole.y,f.rightSole.y))-report.floor;
                report.eventsPreserved=JsonUtility.ToJson(new ActorFacingCalibration.Entry {events=ActorAnimationAudit.EventDetails(source)})==JsonUtility.ToJson(new ActorFacingCalibration.Entry {events=ActorAnimationAudit.EventDetails(candidate)});
                using(var motion=new PlayerInPlaceSampling(actor))report.maximumRootMotion=motion.Sample(candidate).MaxDisplacement;
                File.WriteAllText(Output+"/report.json",JsonUtility.ToJson(report,true));
                if(report.maximumDesiredPoseError>.005f || report.maximumUpperBodyPositionError>.005f || report.maximumIKPoseError>.005f || report.minimumSoleFK<-.003f || report.minimumSoleIK<-.003f || report.minimumSubframeSoleFK<-.003f || report.minimumSubframeSoleIK<-.003f || report.maximumAnkleHorizontalChange>.005f || report.maximumFootRotationChange>3f || !report.eventsPreserved || report.maximumRootMotion>.005f)
                    throw new InvalidOperationException("Pilot pose/IK/support contract failed; inspect report, no asset installed");
                Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
                var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(report.candidate);
                if(existing==null) {AssetDatabase.CreateAsset(candidate,report.candidate);candidate=null;}
                else if(!ActorFacingCalibration.SameAuthoring(existing,candidate))throw new InvalidOperationException("Existing pilot differs; authoring preserved");
                report.status="Pilot FK/IK/pose gates passed. Runtime UCC and visual qualification still required.";
                void Vector(string name,Vector3 v) {for(int axis=0;axis<3;axis++)values[name+"."+"xyz"[axis]].Add(v[axis]);}
                void Rotation(string name,Quaternion q) {for(int axis=0;axis<4;axis++)values[name+"."+"xyzw"[axis]].Add(q[axis]);}
            }
        }
        catch(Exception e) {report.status=e.Message;throw;}
        finally {if(candidate!=null)Object.DestroyImmediate(candidate);if(previous.IsValid())SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);File.WriteAllText(Output+"/report.json",JsonUtility.ToJson(report,true));}
    }
    public static void BindPreview()
    {
        throw new InvalidOperationException("Muscle-space pilot rejected by inter-key pose tests. It cannot be bound. Use the separately validated native-support candidate.");
    }
}
