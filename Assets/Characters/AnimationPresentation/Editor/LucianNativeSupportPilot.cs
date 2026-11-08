using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Exact-avatar offline IK-goal authoring. Source muscles and body curves stay intact.</summary>
public static class LucianNativeSupportPilot
{
    const string Output="Library/LucianCoherence/support-native";
    const string ClipPath="Assets/Characters/1_Squad/Lucian/Animation/CoherencePilot/Lucian_Common_Run_SupportIK.anim";
    [Serializable] internal sealed class Report
    {
        public string source,candidate=ClipPath,status="Prevalidation",sourceGuid,sourceHash,avatarHash,candidateHash;
        public long sourceFileId;
        public bool requiresNativeFootIK=true,eventsPreserved,musclesPreserved,horizontalTrajectoryRemoved;
        public float sourceRootMotion;
        public bool nativeReference,swingPreserved;
        public float contactNativeSpeed;
        public float leftContactPhase,rightContactPhase;
        public Vector3 travelDirection;
        public float floor,minimumSole,subframeMinimumSole,persistedMinimumSole,maximumAnkleError,maximumUpperBodyError,maximumFootRotationError,maximumRootMotion;
    }
    public static void Run()
        => Prepare(ActorAnimationAudit.Source(ActorAnimationAudit.Sources+"Twinblades_Common_Run_Loop_Inplace.FBX"),ClipPath,Output);

    internal static Report Prepare(AnimationClip source,string clipPath,string output,bool nativeReference=false,bool preserveSwing=false,Vector3? travelDirection=null)
    {
        Directory.CreateDirectory(output);
        var actor=ActorAnimationAudit.Actor(false);
        var direction=(travelDirection??Vector3.forward).normalized;
        var report=new Report {source=AssetDatabase.GetAssetPath(source),candidate=clipPath,nativeReference=nativeReference,swingPreserved=preserveSwing,travelDirection=direction};
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out report.sourceGuid,out report.sourceFileId);
        report.sourceHash=AssetDatabase.GetAssetDependencyHash(report.source).ToString();
        report.avatarHash=AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(actor.avatar)).ToString();
        var lines=File.ReadAllLines("Docs/Animation/Lucian_Coherence_Cadence/soles.csv");var headers=lines[0].Split(',');
        int fi=Array.IndexOf(headers,"floor_y"),ri=Array.IndexOf(headers,"root_y");
        var levels=lines.Skip(1).Select(l=>l.Split(',')).Select(v=>float.Parse(v[fi],CultureInfo.InvariantCulture)-float.Parse(v[ri],CultureInfo.InvariantCulture)).Where(v=>!float.IsNaN(v)&&!float.IsInfinity(v)).OrderBy(v=>v).ToArray();
        report.floor=levels[levels.Length/2];
        var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        AnimationClip candidate=null,working=source;
        try
        {
            using(var sampler=new ActorPoseSampler(actor))
            {
                int count=Mathf.CeilToInt(source.length*240),index=0;
                var before=sampler.Sample(source,sampleCount:count,measureSoles:true,applyFootIK:nativeReference);
                var lifts=preserveSwing?SwingLifts(before,source.length,report.floor,output,direction,out report.contactNativeSpeed,out report.leftContactPhase,out report.rightContactPhase):null;
                using(var motion=new PlayerInPlaceSampling(actor))
                {
                    var path=motion.Sample(source);report.sourceRootMotion=path.MaxDisplacement;
                    if(path.MaxDisplacement>.005f)
                    {
                        if(path.MaxYaw>.1f||path.positions.Any(p=>Mathf.Abs(p.y)>.001f))throw new InvalidOperationException("Unexpected vertical/yaw trajectory; explicit source repair required");
                        working=new AnimationClip();EditorUtility.CopySerialized(source,working);
                        foreach(int axis in new[]{0,2})
                        {
                            var binding=EditorCurveBinding.FloatCurve("",typeof(Animator),"RootT."+"xyz"[axis]);
                            var curve=AnimationUtility.GetEditorCurve(working,binding);
                            if(curve==null)throw new InvalidOperationException("No body trajectory curve for measured drift");
                            var corrected=new AnimationCurve(path.positions.Select((p,i)=>new Keyframe(source.length*i/(path.positions.Length-1),curve.Evaluate(source.length*i/(path.positions.Length-1))-p[axis]/path.humanScale)).ToArray());
                            for(int i=0;i<corrected.length;i++){AnimationUtility.SetKeyLeftTangentMode(corrected,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(corrected,i,AnimationUtility.TangentMode.Linear);}
                            AnimationUtility.SetEditorCurve(working,binding,corrected);
                        }
                        var settings=AnimationUtility.GetAnimationClipSettings(working);settings.loopBlendPositionXZ=true;AnimationUtility.SetAnimationClipSettings(working,settings);
                        report.horizontalTrajectoryRemoved=true;
                    }
                }
                var targets=new Vector3[count+1,2];var bodyRotations=new Quaternion[count+1];
                var values=new Dictionary<string,List<float>>();
                foreach(string side in new[]{"Left","Right"})
                {
                    foreach(char axis in "xyz")values[side+"FootT."+axis]=new List<float>();
                    foreach(char axis in "xyzw")values[side+"FootQ."+axis]=new List<float>();
                }
                HumanPoseHandler handler=null;
                try
                {
                    sampler.Sample(working,sampleCount:count,applyFootIK:nativeReference,poseCorrection:a=>
                    {
                        if(handler==null)handler=new HumanPoseHandler(a.avatar,a.transform);
                        var pose=new HumanPose();handler.GetHumanPose(ref pose);
                        bodyRotations[index]=pose.bodyRotation;
                        var original=before[index];
                        for(int side=0;side<2;side++)
                        {
                            float lift=lifts!=null?lifts[index,side]:Mathf.Max(0,report.floor-(side==0?original.leftSole.y:original.rightSole.y));
                            targets[index,side]=(side==0?original.leftFoot:original.rightFoot)+Vector3.up*lift;
                            var goal=pose.ikGoalPositions[side]+Quaternion.Inverse(pose.bodyRotation)*(Vector3.up*lift/a.humanScale);
                            string prefix=side==0?"LeftFoot":"RightFoot";
                            for(int axis=0;axis<3;axis++)values[prefix+"T."+"xyz"[axis]].Add(goal[axis]);
                            var rotation=pose.internalIkGoalRotations[side];
                            for(int axis=0;axis<4;axis++)values[prefix+"Q."+"xyzw"[axis]].Add(rotation[axis]);
                        }
                        index++;
                    });
                }
                finally {handler?.Dispose();}
                candidate=new AnimationClip();EditorUtility.CopySerialized(working,candidate);candidate.name=Path.GetFileNameWithoutExtension(clipPath);
                foreach(var pair in values)
                {
                    var curve=new AnimationCurve(pair.Value.Select((v,i)=>new Keyframe(source.length*i/count,v)).ToArray());
                    for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);}
                    AnimationUtility.SetEditorCurve(candidate,EditorCurveBinding.FloatCurve("",typeof(Animator),pair.Key),curve);
                }
                candidate.EnsureQuaternionContinuity();
                // Correct the native solver's small reconstruction residual in goal space,
                // without inverse-solving or rewriting any source muscle curve.
                for(int pass=0;pass<3;pass++)
                {
                    var solved=sampler.Sample(candidate,sampleCount:count,applyFootIK:true);
                    float error=0;
                    for(int i=0;i<solved.Length;i++)for(int side=0;side<2;side++)
                    {
                        var delta=targets[i,side]-(side==0?solved[i].leftFoot:solved[i].rightFoot);
                        error=Mathf.Max(error,delta.magnitude);
                        delta=Quaternion.Inverse(bodyRotations[i])*delta/actor.humanScale;
                        string prefix=side==0?"LeftFootT.":"RightFootT.";
                        for(int axis=0;axis<3;axis++)values[prefix+"xyz"[axis]][i]+=delta[axis];
                    }
                    if(error<.0002f)break;
                    foreach(var pair in values.Where(p=>p.Key.Contains("FootT.")))
                    {
                        var curve=new AnimationCurve(pair.Value.Select((v,i)=>new Keyframe(source.length*i/count,v)).ToArray());
                        for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);}
                        AnimationUtility.SetEditorCurve(candidate,EditorCurveBinding.FloatCurve("",typeof(Animator),pair.Key),curve);
                    }
                }
                var after=sampler.Sample(candidate,sampleCount:count,measureSoles:true,applyFootIK:true);
                var sub=sampler.Sample(candidate,sampleCount:count*2+1,measureSoles:true,applyFootIK:true);
                report.minimumSole=after.Min(f=>Mathf.Min(f.leftSole.y,f.rightSole.y))-report.floor;
                report.subframeMinimumSole=sub.Min(f=>Mathf.Min(f.leftSole.y,f.rightSole.y))-report.floor;
                for(int i=0;i<after.Length;i++)
                {
                    report.maximumAnkleError=Mathf.Max(report.maximumAnkleError,Vector3.Distance(after[i].leftFoot,targets[i,0]),Vector3.Distance(after[i].rightFoot,targets[i,1]));
                    report.maximumFootRotationError=Mathf.Max(report.maximumFootRotationError,Quaternion.Angle(before[i].leftRotation,after[i].leftRotation),Quaternion.Angle(before[i].rightRotation,after[i].rightRotation));
                    foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.Head,HumanBodyBones.LeftHand,HumanBodyBones.RightHand})
                        report.maximumUpperBodyError=Mathf.Max(report.maximumUpperBodyError,Vector3.Distance(before[i].bonePositions[(int)bone],after[i].bonePositions[(int)bone]));
                }
                report.eventsPreserved=ActorClipReconstruction.Events(source)==ActorClipReconstruction.Events(candidate);
                report.musclesPreserved=AnimationUtility.GetCurveBindings(source).Where(b=>b.type==typeof(Animator)&&HumanTrait.MuscleName.Contains(b.propertyName)).All(b=>
                {
                    var a=AnimationUtility.GetEditorCurve(source,b);var c=AnimationUtility.GetEditorCurve(candidate,b);
                    return c!=null&&a.preWrapMode==c.preWrapMode&&a.postWrapMode==c.postWrapMode&&a.keys.SequenceEqual(c.keys);
                });
                using(var motion=new PlayerInPlaceSampling(actor))report.maximumRootMotion=motion.Sample(candidate).MaxDisplacement;
                File.WriteAllLines(output+"/subframes.csv",new[]{"time,left_sole,right_sole"}.Concat(sub.Select(f=>string.Join(",",new[]{f.time,f.leftSole.y-report.floor,f.rightSole.y-report.floor}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))))));
                if(report.minimumSole<-.003f||report.subframeMinimumSole<-.003f||report.maximumAnkleError>.005f||report.maximumFootRotationError>3||report.maximumUpperBodyError>.005f||report.maximumRootMotion>.005f||!report.eventsPreserved||!report.musclesPreserved)
                    throw new InvalidOperationException("Native IK pilot fails pose/support gates; no candidate installed");
                var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if(existing==null){AssetDatabase.CreateAsset(candidate,clipPath);candidate=null;}
                else if(!ActorFacingCalibration.SameAuthoring(existing,candidate))throw new InvalidOperationException("Existing native IK pilot differs; preserved");
                var persisted=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                var repeated=sampler.Sample(persisted,sampleCount:count*2+1,measureSoles:true,applyFootIK:true,cycles:2);
                report.persistedMinimumSole=repeated.Min(f=>Mathf.Min(f.leftSole.y,f.rightSole.y))-report.floor;
                if(report.persistedMinimumSole<-.003f)throw new InvalidOperationException("Persisted two-cycle support fails; no binding authorized");
                report.candidateHash=AssetDatabase.GetAssetDependencyHash(clipPath).ToString();
                report.status="PASS offline native Foot IK. Runtime and visual qualification required.";
            }
        }
        catch(Exception e){report.status=e.Message;throw;}
        finally {if(candidate!=null)Object.DestroyImmediate(candidate);if(working!=source)Object.DestroyImmediate(working);if(previous.IsValid())SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);File.WriteAllText(output+"/report.json",JsonUtility.ToJson(report,true));}
        return report;
    }
    public static void BindPreview()
    {
        throw new InvalidOperationException("The first SupportIK pilot flattens part of the foot return. Use complete Stride preparation/prevalidation and LucianNativeCycleBatch.Apply; this old binding is retired.");
    }
    public static void CheckPersisted()
    {
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if(clip==null)throw new InvalidOperationException("Pilot missing");
        var report=JsonUtility.FromJson<Report>(File.ReadAllText(Output+"/report.json"));
        using(var sampler=new ActorPoseSampler(ActorAnimationAudit.Actor(false)))
        {
            var frames=sampler.Sample(clip,sampleCount:1920,measureSoles:true,applyFootIK:true,cycles:4);
            File.WriteAllLines(Output+"/persisted-cycles.csv",new[]{"time,left_sole,right_sole"}.Concat(frames.Select(f=>string.Join(",",new[]{f.time,f.leftSole.y-report.floor,f.rightSole.y-report.floor}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))))));
            if(frames.Any(f=>Mathf.Min(f.leftSole.y,f.rightSole.y)-report.floor<-.003f))throw new InvalidOperationException("Persisted multi-cycle support fails; inspect persisted-cycles.csv");
        }
    }
    public static void IsolateFlame()
    {
        string path=LucianCadencePreview.Folder+"/LucianTorchArms.mask";
        var actor=ActorAnimationAudit.Actor(false);
        var left=actor.GetBoneTransform(HumanBodyBones.LeftShoulder)??actor.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        var right=actor.GetBoneTransform(HumanBodyBones.RightShoulder)??actor.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if(left==null||right==null)throw new InvalidOperationException("Humanoid arm paths missing");
        var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
        if(mask==null)
        {
            mask=new AvatarMask {name="LucianTorchArms"};
            for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
            foreach(var part in new[]{AvatarMaskBodyPart.LeftArm,AvatarMaskBodyPart.RightArm,AvatarMaskBodyPart.LeftFingers,AvatarMaskBodyPart.RightFingers})mask.SetHumanoidBodyPartActive(part,true);
            var transforms=actor.GetComponentsInChildren<Transform>(true);mask.transformCount=transforms.Length;
            var lp=AnimationUtility.CalculateTransformPath(left,actor.transform);var rp=AnimationUtility.CalculateTransformPath(right,actor.transform);
            for(int i=0;i<mask.transformCount;i++)
            {
                var p=AnimationUtility.CalculateTransformPath(transforms[i],actor.transform);mask.SetTransformPath(i,p);
                mask.SetTransformActive(i,p==lp||p.StartsWith(lp+"/",StringComparison.Ordinal)||p==rp||p.StartsWith(rp+"/",StringComparison.Ordinal));
            }
            AssetDatabase.CreateAsset(mask,path);
        }
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(LucianCadencePreview.Folder+"/LucianCadence.controller");
        var layers=controller.layers;var layer=layers.Single(l=>l.name=="Upper Body Flame");
        if(layer.avatarMask!=mask){layer.avatarMask=mask;controller.layers=layers;EditorUtility.SetDirty(controller);AssetDatabase.SaveAssetIfDirty(controller);}
    }
    public static void CheckController()
    {
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder+"/LucianCadence.overrideController");
        var report=JsonUtility.FromJson<Report>(File.ReadAllText(Output+"/report.json"));
        using(var sampler=new ActorPoseSampler(ActorAnimationAudit.Actor(false)))
        {
            ActorPoseSampler.Frame[] baseline=null;
            foreach(bool torch in new[]{false,true})
            {
                var frames=sampler.Sample(clip,sampleCount:960,measureSoles:true,cycles:4,controller:controller,configureController:p=>
                {
                    p.SetFloat("LitSpeed",3.25f);p.SetFloat("LocomotionTier",1);p.SetFloat("LocomotionPlaybackRate",1);
                    p.SetFloat("ForwardMovement",1);p.SetBool("Flame",torch);p.SetLayerWeight(1,torch?1:0);p.SetLayerWeight(2,0);
                    p.Play("Base Layer.Locomotion",0,0);
                    if(torch)p.Play("Upper Body Flame.Flame_Locomotion",1,0);
                });
                string name=torch?"torch":"plain";
                File.WriteAllLines(Output+"/controller-"+name+".csv",new[]{"time,left_sole,right_sole,pelvis_y,foot_difference,hip_difference"}.Concat(frames.Select((f,i)=>string.Join(",",new[]{f.time,f.leftSole.y-report.floor,f.rightSole.y-report.floor,f.body.y,baseline==null?0:Mathf.Max(Vector3.Distance(baseline[i].leftFoot,f.leftFoot),Vector3.Distance(baseline[i].rightFoot,f.rightFoot)),baseline==null?0:Vector3.Distance(baseline[i].body,f.body)}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))))));
                if(!torch)baseline=frames;
            }
        }
    }
    public static void CheckContactSampler()
    {
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        ActorSoleSampler manual=null,baked=null;
        ActorSoleSampler.ContactPoint? ml=null,mr=null,bl=null,br=null;
        float minimumError=0,anchorError=0;int sampled=0;
        try
        {
            using(var sampler=new ActorPoseSampler(ActorAnimationAudit.Actor(false)))
                sampler.Sample(clip,sampleCount:120,measureSoles:true,applyFootIK:true,poseCorrection:a=>
                {
                    if(manual==null){manual=new ActorSoleSampler(a,true);baked=new ActorSoleSampler(a);}
                    if(!manual.Sample(out var lm,out var rm)||!baked.Sample(out var lb,out var rb))throw new InvalidOperationException("Sole mesh unresolved");
                    minimumError=Mathf.Max(minimumError,Vector3.Distance(lm,lb),Vector3.Distance(rm,rb));
                    if(!ml.HasValue){ml=manual.CaptureLowest(true);mr=manual.CaptureLowest(false);bl=baked.CaptureLowest(true);br=baked.CaptureLowest(false);}
                    if(!ml.HasValue||!mr.HasValue||!bl.HasValue||!br.HasValue)throw new InvalidOperationException("Contact identity unresolved");
                    if(!manual.TryPosition(ml.Value,out var pml)||!manual.TryPosition(mr.Value,out var pmr)||!baked.TryPosition(bl.Value,out var pbl)||!baked.TryPosition(br.Value,out var pbr))throw new InvalidOperationException("Held contact identity lost");
                    if(manual.TryPosition(bl.Value,out _))throw new InvalidOperationException("Foreign sampler identity accepted");
                    anchorError=Mathf.Max(anchorError,Vector3.Distance(pml,pbl),Vector3.Distance(pmr,pbr));sampled++;
                });
            File.WriteAllText(Output+"/contact-sampler.txt","samples="+sampled+"\nminimum_error_m="+minimumError.ToString("R",CultureInfo.InvariantCulture)+"\nheld_vertex_error_m="+anchorError.ToString("R",CultureInfo.InvariantCulture));
            if(minimumError>.002f||anchorError>.002f)throw new InvalidOperationException("Held mesh contact disagrees with BakeMesh");
        }
        finally {manual?.Dispose();baked?.Dispose();}
    }
    private static float[,] SwingLifts(ActorPoseSampler.Frame[] frames,float duration,float floor,string output,Vector3 direction,out float nativeSpeed,out float leftPhase,out float rightPhase)
    {
        int count=frames.Length-1;float dt=duration/count;
        var lifts=new float[count+1,2];var contacts=new bool[count+1,2];var speeds=new List<float>();var phases=new float[2];
        for(int side=0;side<2;side++)
        {
            float Height(int i)=>side==0?frames[i].leftSole.y:frames[i].rightSole.y;
            Vector3 Foot(int i)=>side==0?frames[i].leftFoot:frames[i].rightFoot;
            float minimum=Enumerable.Range(0,count).Min(Height);
            float Velocity(int i)=>Vector3.Dot(Foot((i+1)%count)-Foot((i+count-1)%count),direction)/(2*dt);
            var lowSpeeds=Enumerable.Range(0,count).Where(i=>Height(i)<minimum+.07f&&Velocity(i)<-.2f).Select(i=>-Velocity(i)).OrderBy(v=>v).ToArray();
            if(lowSpeeds.Length<5)throw new InvalidOperationException("No reliable backward stance samples");
            float centre=lowSpeeds[lowSpeeds.Length/2];
            var planted=Enumerable.Range(0,count).Where(i=>Height(i)<minimum+.07f&&Mathf.Abs(Velocity(i)+centre)<centre*.25f).ToArray();
            if(planted.Length<5)throw new InvalidOperationException("No reliable stance interval");
            phases[side]=Mathf.Repeat(Mathf.Atan2(planted.Sum(i=>Mathf.Sin(2*Mathf.PI*i/count)),planted.Sum(i=>Mathf.Cos(2*Mathf.PI*i/count)))/(2*Mathf.PI),1);
            var keys=new List<Keyframe>();
            foreach(int i in planted)
            {
                contacts[i,side]=true;speeds.Add(-Velocity(i));
                foreach(int cycle in new[]{-1,0,1})keys.Add(new Keyframe(duration*i/count+duration*cycle,floor-Height(i)));
            }
            var correction=new AnimationCurve(keys.OrderBy(k=>k.time).ToArray());
            for(int i=0;i<correction.length;i++){AnimationUtility.SetKeyLeftTangentMode(correction,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(correction,i,AnimationUtility.TangentMode.ClampedAuto);}
            for(int i=0;i<=count;i++)lifts[i,side]=Mathf.Max(0,correction.Evaluate(duration*i/count),floor-Height(i));
            contacts[count,side]=contacts[0,side];
        }
        var ordered=speeds.OrderBy(v=>v).ToArray();nativeSpeed=ordered[ordered.Length/2];leftPhase=phases[0];rightPhase=phases[1];
        File.WriteAllLines(output+"/contact-phases.csv",new[]{"time,left_stance,right_stance,left_source_sole,right_source_sole,left_target_sole,right_target_sole"}.Concat(Enumerable.Range(0,count+1).Select(i=>string.Join(",",new[]{frames[i].time,contacts[i,0]?1f:0f,contacts[i,1]?1f:0f,frames[i].leftSole.y-floor,frames[i].rightSole.y-floor,frames[i].leftSole.y+lifts[i,0]-floor,frames[i].rightSole.y+lifts[i,1]-floor}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))))));
        return lifts;
    }
    public static void PrepareStridePilot()
    {
        var source=ActorAnimationAudit.Source(ActorAnimationAudit.Sources+"Twinblades_Common_Run_Loop_Inplace.FBX");
        Prepare(source,"Assets/Characters/1_Squad/Lucian/Animation/CoherencePilot/Lucian_Common_Run_Stride02.anim","Library/LucianCoherence/stride-pilot",true,true);
    }
    public static void BindStridePilot()
    {
        var r=JsonUtility.FromJson<Report>(File.ReadAllText("Library/LucianCoherence/stride-pilot/report.json"));
        if(!r.status.StartsWith("PASS")||r.contactNativeSpeed<1||r.sourceHash!=AssetDatabase.GetAssetDependencyHash(r.source).ToString()||r.candidateHash!=AssetDatabase.GetAssetDependencyHash(r.candidate).ToString())throw new InvalidOperationException("Stride pilot prevalidation missing or stale");
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(r.candidate);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder+"/LucianCadence.overrideController");
        var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();controller.GetOverrides(pairs);
        if(pairs.Count(p=>p.Key.name=="Twinblades_Common_Run_Loop_Inplace")!=1)throw new InvalidOperationException("Ambiguous common run consumer");
        bool changed=false;for(int i=0;i<pairs.Count;i++)if(pairs[i].Key.name=="Twinblades_Common_Run_Loop_Inplace"&&pairs[i].Value!=clip){pairs[i]=new KeyValuePair<AnimationClip,AnimationClip>(pairs[i].Key,clip);changed=true;}
        if(changed){controller.ApplyOverrides(pairs);AssetDatabase.SaveAssetIfDirty(controller);}
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(LucianCadencePreview.Folder+"/LocomotionMeasurements.asset");
        bool rebind=profile.explorationRun.clip!=clip;
        if(rebind||!profile.RequiresNativeFootIK(clip))
        {
            if(rebind){profile.explorationRun.clip=clip;profile.explorationRun.nativeSpeed=r.contactNativeSpeed;}
            if(!profile.RequiresNativeFootIK(clip))profile.nativeFootIKClips=profile.nativeFootIKClips.Concat(new[]{clip}).ToArray();
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
        }
    }
    [Serializable] sealed class ContactFitWindow {public string scenario,side;public float factor,duration;}
    [Serializable] sealed class ContactFit {public string run,candidateHash,avatarHash;public float previousNativeSpeed;public ContactFitWindow[] windows;}
    [Serializable] sealed class ProbeStatus {public string status;public int samples;public string[] errors;}
    public static void CalibrateStrideCadence()
    {
        var fit=JsonUtility.FromJson<ContactFit>(File.ReadAllText("Library/LucianCoherence/stride-pilot/cadence-fit.json"));
        var source=JsonUtility.FromJson<Report>(File.ReadAllText("Library/LucianCoherence/stride-pilot/report.json"));
        var status=JsonUtility.FromJson<ProbeStatus>(File.ReadAllText(fit.run+"/report.json"));
        var actor=ActorAnimationAudit.Actor(false);
        if(!status.status.StartsWith("RECORDED")||status.errors.Length!=0||status.samples<1000||fit.candidateHash!=source.candidateHash||fit.avatarHash!=source.avatarHash||fit.candidateHash!=AssetDatabase.GetAssetDependencyHash(source.candidate).ToString()||fit.avatarHash!=AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(actor.avatar)).ToString())
            throw new InvalidOperationException("Measured cadence inputs missing, failed or stale");
        foreach(string scenario in new[]{"walk","run","torch_walk","torch_run"})
            foreach(string side in new[]{"left","right"})
                if(fit.windows.Count(w=>w.scenario==scenario&&w.side==side&&w.duration>=.04f)<3)throw new InvalidOperationException("Insufficient contacts: "+scenario+" "+side);
        var factors=fit.windows.Select(w=>w.factor).OrderBy(v=>v).ToArray();
        if(factors.Length<24||factors.Any(f=>float.IsNaN(f)||float.IsInfinity(f)||f<.8f||f>1.2f))throw new InvalidOperationException("Ambiguous cadence correction; no automatic approximation");
        float factor=factors.Length%2==1?factors[factors.Length/2]:(factors[factors.Length/2-1]+factors[factors.Length/2])*.5f;
        float speed=fit.previousNativeSpeed/factor;
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(LucianCadencePreview.Folder+"/LocomotionMeasurements.asset");
        if(profile.explorationRun.clip!=AssetDatabase.LoadAssetAtPath<AnimationClip>(source.candidate)||Mathf.Abs(profile.explorationRun.nativeSpeed-fit.previousNativeSpeed)>.0001f&&Mathf.Abs(profile.explorationRun.nativeSpeed-speed)>.0001f)
            throw new InvalidOperationException("Existing cadence authoring changed; preserved");
        if(Mathf.Abs(profile.explorationRun.nativeSpeed-speed)>.00001f){profile.explorationRun.nativeSpeed=speed;EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);}
        File.WriteAllText("Library/LucianCoherence/stride-pilot/cadence-result.txt","source="+fit.run+"\ncontacts="+factors.Length+"\nfactor="+factor.ToString("R",CultureInfo.InvariantCulture)+"\nnative_speed="+speed.ToString("R",CultureInfo.InvariantCulture));
    }
}
