using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Measures effective CC5 cycles with/without Unity's existing Humanoid foot IK. Never edits a clip or certifies estimated contacts.</summary>
public static class LucianCycleSupportAudit
{
    const string Folder="Library/LucianCoherence/cycles";
    [Serializable] public sealed class Measurement
    {
        public string clip,guid,csv,rootSource;public long fileId;public bool footIK;
        public float length,referenceFloor,minimumSole,maximumLowestSole,maximumBodyExcursion,estimatedNativeSpeed,maximumSupportError;
        public float rootSpeedOnAvatar,rootDuration,rootHumanScale;
        public int supportIntervals;
    }
    [Serializable] sealed class Report {public string avatar,scope="Read-only measured soles; speed is an estimated stance fit, not a support-validation gate.";public List<Measurement> cycles=new List<Measurement>();}
    [Serializable] sealed class Fidelity
    {
        public string clip;public float rootDistance,rootYRange,maximumPelvisYDifference,maximumSoleDifferenceAfterHorizontalRemoval,rootMinimumSole,inplaceMinimumSole;
    }
    [Serializable] sealed class Fidelities {public List<Fidelity> clips=new List<Fidelity>();}
    [Serializable] sealed class CurveContract
    {
        public string[] humanPoseMembers,footBindings;
        public float humanScale;
        public Vector3 footT,bodyPosition,fkFoot,ikFoot,rootSpacePrediction,bodySpacePrediction;
        public Quaternion bodyRotation;
        public Vector3[] handlerGoalPositions;
        public Quaternion[] handlerGoalRotations;
        public float footBottomHeight;
    }
    public static void InspectCurveContract()
    {
        var clip=ActorAnimationAudit.Source(ActorAnimationAudit.Sources+"Twinblades_Common_Walk_Loop_Inplace.FBX");
        var report=new CurveContract {humanPoseMembers=typeof(HumanPose).GetMembers().Select(m=>m.Name).Distinct().ToArray(),
            footBindings=AnimationUtility.GetCurveBindings(clip).Where(b=>b.propertyName.Contains("Foot")).Select(b=>b.propertyName).ToArray()};
        var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        try
        {
            using(var sampler=new ActorPoseSampler(ActorAnimationAudit.Actor(false)))
            {
                bool captured=false;
                report.fkFoot=sampler.Sample(clip,sampleCount:2,poseCorrection:a=>
                {
                    if(captured)return;captured=true;report.humanScale=a.humanScale;report.footBottomHeight=a.leftFeetBottomHeight;
                    using(var handler=new HumanPoseHandler(a.avatar,a.transform))
                    {var pose=new HumanPose();handler.GetHumanPose(ref pose);report.bodyPosition=pose.bodyPosition;report.bodyRotation=pose.bodyRotation;
                        report.handlerGoalPositions=pose.ikGoalPositions.ToArray();report.handlerGoalRotations=pose.ikGoalRotations.ToArray();}
                })[0].leftFoot;
                report.ikFoot=sampler.Sample(clip,sampleCount:2,applyFootIK:true)[0].leftFoot;
            }
            var values=new float[3];
            for(int i=0;i<3;i++)
            {
                var curve=AnimationUtility.GetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),"LeftFootT."+"xyz"[i]));
                if(curve==null)throw new InvalidOperationException("Foot goal curve unavailable: "+"xyz"[i]);values[i]=curve.Evaluate(0);
            }
            report.footT=new Vector3(values[0],values[1],values[2]);report.rootSpacePrediction=report.footT*report.humanScale;
            report.bodySpacePrediction=(report.bodyPosition+report.bodyRotation*report.footT)*report.humanScale;
        }
        finally {if(previous.IsValid())SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        File.WriteAllText(Folder+"/curve-contract.json",JsonUtility.ToJson(report,true));
    }
    public static void CompareRootPoses()
    {
        var measured=JsonUtility.FromJson<Report>(File.ReadAllText(Folder+"/report.json"));
        var report=new Fidelities();var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        try
        {
            using(var sampler=new ActorPoseSampler(ActorAnimationAudit.Actor(false)))
            foreach(var item in measured.cycles.Where(c=>!c.footIK && c.clip.Contains("Common_")))
            {
                var source=sampler.Sample(ActorAnimationAudit.Source(item.rootSource),rootMotion:true,sampleCount:120,measureSoles:true);
                var target=sampler.Sample(ActorAnimationAudit.Source(item.clip,item.fileId),sampleCount:120,measureSoles:true);
                var row=new Fidelity {clip=item.clip,rootDistance=Vector3.ProjectOnPlane(source.Last().root-source.First().root,Vector3.up).magnitude,
                    rootYRange=source.Max(f=>f.root.y)-source.Min(f=>f.root.y),rootMinimumSole=source.Min(f=>Mathf.Min(f.leftSole.y,f.rightSole.y)),
                    inplaceMinimumSole=target.Min(f=>Mathf.Min(f.leftSole.y,f.rightSole.y))};
                for(int i=0;i<source.Length;i++)
                {
                    var horizontal=Vector3.ProjectOnPlane(source[i].root,Vector3.up);
                    row.maximumPelvisYDifference=Mathf.Max(row.maximumPelvisYDifference,Mathf.Abs(source[i].body.y-target[i].body.y));
                    row.maximumSoleDifferenceAfterHorizontalRemoval=Mathf.Max(row.maximumSoleDifferenceAfterHorizontalRemoval,
                        Vector3.Distance(source[i].leftSole-horizontal,target[i].leftSole),Vector3.Distance(source[i].rightSole-horizontal,target[i].rightSole));
                }
                report.clips.Add(row);
            }
        }
        finally {if(previous.IsValid())SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        File.WriteAllText(Folder+"/root-pose-fidelity.json",JsonUtility.ToJson(report,true));
    }
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        var actor=ActorAnimationAudit.Actor(false);
        var graph=JsonUtility.FromJson<LucianCoherenceAudit.Report>(File.ReadAllText("Library/LucianCoherence/controller-baseline.json"));
        var clips=graph.states.Where(s=>!string.IsNullOrEmpty(s.effective) &&
            (s.effective.Contains("Common_Walk_Loop") || s.effective.Contains("Common_Run_Loop") || s.effective.Contains("Strafe_Walk_") || s.effective.Contains("Strafe_Run_")))
            .GroupBy(s=>s.guid+":"+s.fileId).Select(g=>g.First()).ToArray();
        var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        var report=new Report {avatar=AssetDatabase.GetAssetPath(actor.avatar)};
        var assetPaths=AssetDatabase.GetAllAssetPaths();
        try
        {
            using(var sampler=new ActorPoseSampler(actor))
            {
                var idle=ActorAnimationAudit.Source(ActorAnimationAudit.Sources+"Common_Idle_Inplace.FBX");
                var idleFrames=sampler.Sample(idle,sampleCount:30,measureSoles:true);
                float floor=idleFrames.Average(f=>(f.leftSole.y+f.rightSole.y)*.5f);
                foreach(var item in clips)
                {
                    var clip=ActorAnimationAudit.Source(item.effective,item.fileId);
                    string rootName=Path.GetFileNameWithoutExtension(item.effective).Replace("_Inplace","_Root");
                    var roots=assetPaths.Where(p=>p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase) && Path.GetFileNameWithoutExtension(p).Equals(rootName,StringComparison.OrdinalIgnoreCase)).ToArray();
                    float rootSpeed=0,rootDuration=0,humanScale=0;
                    if(roots.Length==1)using(var trajectory=new PlayerInPlaceSampling(actor))
                    {
                        var root=ActorAnimationAudit.Source(roots[0]);var samples=trajectory.Sample(root);
                        rootSpeed=samples.Distance/clip.length;rootDuration=root.length;humanScale=samples.humanScale;
                    }
                    foreach(bool ik in new[]{false,true})
                    {
                        var frames=sampler.Sample(clip,measureSoles:true,applyFootIK:ik);
                        var row=new Measurement {clip=item.effective,guid=item.guid,fileId=item.fileId,footIK=ik,length=clip.length,referenceFloor=floor,
                            rootSource=roots.Length==1?roots[0]:"Unresolved or ambiguous",rootSpeedOnAvatar=rootSpeed,rootDuration=rootDuration,rootHumanScale=humanScale,
                            minimumSole=frames.Min(f=>Mathf.Min(f.leftSole.y,f.rightSole.y))-floor,
                            maximumLowestSole=frames.Max(f=>Mathf.Min(f.leftSole.y,f.rightSole.y))-floor,
                            maximumBodyExcursion=frames.Max(f=>f.body.y)-frames.Min(f=>f.body.y)};
                        var direction=Direction(clip.name);
                        var fits=new List<float>();var errors=new List<float>();
                        for(int i=1;i<frames.Length;i++)foreach(bool left in new[]{false,true})
                        {
                            var p=left?frames[i].leftSole:frames[i].rightSole;
                            var before=left?frames[i-1].leftSole:frames[i-1].rightSole;
                            var foot=left?frames[i].leftFoot:frames[i].rightFoot;
                            var footBefore=left?frames[i-1].leftFoot:frames[i-1].rightFoot;
                            if(p.y-floor>.025f || before.y-floor>.025f)continue;
                            float dt=frames[i].time-frames[i-1].time;
                            float speed=-Vector3.Dot(foot-footBefore,direction)/dt;
                            if(speed>.1f)fits.Add(speed);
                            errors.Add(Mathf.Abs(p.y-floor));
                        }
                        row.supportIntervals=fits.Count;
                        if(fits.Count>0) {fits.Sort();row.estimatedNativeSpeed=fits[fits.Count/2];}
                        if(errors.Count>0)row.maximumSupportError=errors.Max();
                        row.csv=Folder+"/"+clip.name+"-ik"+(ik?1:0)+".csv";
                        var lines=new List<string>{"time,hips_y,left_x,left_y,left_z,right_x,right_y,right_z,leftsole_x,leftsole_y,leftsole_z,rightsole_x,rightsole_y,rightsole_z"};
                        foreach(var f in frames)lines.Add(string.Join(",",new[]{f.time,f.body.y,f.leftFoot.x,f.leftFoot.y,f.leftFoot.z,f.rightFoot.x,f.rightFoot.y,f.rightFoot.z,
                            f.leftSole.x,f.leftSole.y,f.leftSole.z,f.rightSole.x,f.rightSole.y,f.rightSole.z}.Select(F)));
                        File.WriteAllLines(row.csv,lines);report.cycles.Add(row);
                    }
                }
            }
        }
        finally {if(previous.IsValid())SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        File.WriteAllText(Folder+"/report.json",JsonUtility.ToJson(report,true));
    }
    static string F(float v)=>v.ToString("R",CultureInfo.InvariantCulture);
    internal static Vector3 Direction(string name)
    {
        var names=new[]{"F","FR","R","BR","B","BL","L","FL"};
        for(int i=0;i<names.Length;i++)if(name.Contains("_"+names[i]+"_")) {float angle=i*Mathf.PI/4;return new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));}
        return Vector3.forward;
    }
}
