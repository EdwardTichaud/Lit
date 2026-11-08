using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;

/// <summary>Read-only feasibility of continuous contact-height blending on the actual avatar.</summary>
public static class LucianContactBlendAudit
{
    const string Output="Library/LucianCoherence/contact-blends";
    const int SourceSteps=120;
    internal const float Floor=.10000038f;
    sealed class Geometry
    {
        public Vector3[][] left=new Vector3[SourceSteps+1][],right=new Vector3[SourceSteps+1][];
        public Vector2[] height=new Vector2[SourceSteps+1];
    }
    struct Input {public Geometry geometry;public int index;public float fraction,weight;}
    [Serializable] sealed class SourceEntry {public string source,candidate;public long fileId;}
    [Serializable] sealed class SourceManifest {public SourceEntry[] entries;}
    [Serializable] sealed class RotationRow {public string clip;public bool nativeFootIK;public float maximumStep,maximumDifferenceFromSource;}
    [Serializable] sealed class RotationReport {public string scope="Direct original/copy comparison on Lucian, 480 intervals over two cycles. No controller, scripts or asset writes.";public List<RotationRow> cases=new List<RotationRow>();}
    public static void CompareRunRSource()
    {
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(LucianCadencePreview.Folder+"/LocomotionMeasurements.asset");
        var candidate=profile.run.Single(c=>Vector2.Dot(c.direction.normalized,Vector2.right)>.999f).clip;
        var manifest=JsonUtility.FromJson<SourceManifest>(File.ReadAllText("Library/LucianCoherence/stride-cycles/manifest.json"));
        var entry=manifest.entries.Single(e=>e.candidate==AssetDatabase.GetAssetPath(candidate));
        var source=ActorAnimationAudit.Source(entry.source,entry.fileId);var report=new RotationReport();
        using(var sampler=new ActorPoseSampler(ActorAnimationAudit.Actor(false)))foreach(bool ik in new[]{false,true})
        {
            var reference=sampler.Sample(source,sampleCount:480,cycles:2,applyFootIK:ik);
            foreach(var clip in new[]{source,candidate})
            {
                var frames=clip==source?reference:sampler.Sample(clip,sampleCount:480,cycles:2,applyFootIK:ik);
                var row=new RotationRow {clip=AssetDatabase.GetAssetPath(clip),nativeFootIK=ik};
                for(int i=0;i<frames.Length;i++)
                {
                    if(i>0)row.maximumStep=Mathf.Max(row.maximumStep,Quaternion.Angle(frames[i].leftRotation,frames[i-1].leftRotation),Quaternion.Angle(frames[i].rightRotation,frames[i-1].rightRotation));
                    row.maximumDifferenceFromSource=Mathf.Max(row.maximumDifferenceFromSource,Quaternion.Angle(frames[i].leftRotation,reference[i].leftRotation),Quaternion.Angle(frames[i].rightRotation,reference[i].rightRotation));
                }
                report.cases.Add(row);
            }
        }
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/run-r-source-rotations.json",JsonUtility.ToJson(report,true));
    }
    [Serializable] sealed class Row
    {
        public float gait,angle,maxHeightError,minSole,maxLowerSole,maxCorrection,maxCorrectionStep,maxPelvisDifference,maxRootDifference,maxFootXZDifference,maxFootRotationStep;
        public string status;public int samples;public string[] clips;
        public float maxNativeToFKAngle;
        public float maxAnatomicalSourceAngle;
        public int peakCorrectionSample,peakRotationSample;
        public float maximumRuntimePositionError,maximumRuntimeRotationError;
    }
    [Serializable] sealed class Report
    {
        public string status="PREPARING",scope="Editor-only continuous geometry prototype; static actor, complete candidate controller and actual mesh. No asset writes, runtime implementation, transitions, terrain, torch, UCC or visual acceptance.";
        public int sourceSteps=SourceSteps;
        public List<Row> cases=new List<Row>();
        public string[] leftFeatures,rightFeatures;
        public Quaternion leftGoalToBone,rightGoalToBone;
        public float maximumGoalToBoneAngleDeviation;
        public bool coherentRotations;
        public bool nativeRotations;
        public bool fkRotations;
        public bool muscleController;
        public bool warmControllerPose;
        public bool anatomicalRotations;
        public float maximumFKCallbackReadDeviation;
        public float minimumQuaternionLoopDot=1;
    }
    public static void Run()=>RunAudit(false);
    public static void RunRotationComparison(){RunAudit(false);RunAudit(true);}
    public static void RunNativeRotations()=>RunAudit(false,true);
    public static void RunFKRotations()=>RunAudit(false,false,true);
    public static void RunMuscleRotations()=>RunAudit(false,false,true,true);
    public static void RunWarmMuscleRotations()=>RunAudit(false,false,true,true,true);
    public static void RunAnatomicalRotations()=>RunAudit(false,false,true,true,true,true);
    public static void PrepareSupport()=>RunAudit(false,false,true,true,true,true,true);
    public static void VerifyPreparedSupport()=>RunAudit(false,false,true,true,true,true,false,true);
    public static void PrepareAndVerifySupport(){PrepareSupport();VerifyPreparedSupport();}
    public static void PrepareSupportForControllerChange(string previousSignature)=>RunAudit(false,false,true,true,true,true,true,false,previousSignature);
    static void RunAudit(bool coherentRotations,bool nativeRotations=false,bool fkRotations=false,bool muscleController=false,bool warmControllerPose=false,bool anatomicalRotations=false,bool prepareSupport=false,bool verifyRuntime=false,string previousControllerSignature=null)
    {
        string output=verifyRuntime?Output+"-runtime-verification":prepareSupport?Output+"-prepared":anatomicalRotations?Output+"-anatomical-rotations":warmControllerPose?Output+"-warm-muscle-rotations":muscleController?Output+"-muscle-rotations":fkRotations?Output+"-fk-rotations":nativeRotations?Output+"-native-rotations":coherentRotations?Output+"-coherent":Output;
        Directory.CreateDirectory(output);
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(LucianCadencePreview.Folder+"/LocomotionMeasurements.asset");
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder+"/LucianCadence.overrideController");
        var cycles=profile.walk.Concat(profile.run).Concat(new[]{profile.explorationWalk,profile.explorationRun}).ToArray();
        var runtimeData=verifyRuntime?AssetDatabase.LoadAssetAtPath<NativeFootSupportSet>(NativeFootSupportPreparation.DataPath):null;
        if(verifyRuntime)NativeFootSupportPreparation.Validate(profile,runtimeData,false);
        var data=new Dictionary<AnimationClip,Geometry>();var report=new Report {coherentRotations=coherentRotations,nativeRotations=nativeRotations,fkRotations=fkRotations,muscleController=muscleController,warmControllerPose=warmControllerPose,anatomicalRotations=anatomicalRotations};
        string[] leftIds=null,rightIds=null;
        var leftUsed=new HashSet<int>();var rightUsed=new HashSet<int>();
        var transient=new List<UnityEngine.Object>();
        RuntimeAnimatorController activeController=controller;
        try
        {
        if(muscleController)activeController=CreateMuscleController(controller,transient);
        using(var sampler=new ActorPoseSampler(ActorAnimationAudit.Actor(false)))
        {
            foreach(var cycle in cycles)
            {
                cycle.footGoals.Evaluate(0,out _,out _,out var startL,out var startR);
                cycle.footGoals.Evaluate(.999999f,out _,out _,out var endL,out var endR);
                report.minimumQuaternionLoopDot=Mathf.Min(report.minimumQuaternionLoopDot,Quaternion.Dot(startL,endL),Quaternion.Dot(startR,endR));
                var geometry=new Geometry();ActorSoleSampler.ContactPoint[] left=null,right=null;
                var frames=sampler.Sample(cycle.clip,sampleCount:SourceSteps,measureSoles:true,applyFootIK:true,captureIKGoals:true,geometrySample:(a,soles,index)=>
                {
                    if(index==0)
                    {
                        left=soles.FootPoints(true).ToArray();right=soles.FootPoints(false).ToArray();
                        var li=left.Select(p=>p.Id).ToArray();var ri=right.Select(p=>p.Id).ToArray();
                        if(leftIds==null){leftIds=li;rightIds=ri;}
                        if(!leftIds.SequenceEqual(li)||!rightIds.SequenceEqual(ri))throw new InvalidOperationException("Mesh identities differ between cycles");
                    }
                    Vector3[] Capture(ActorSoleSampler.ContactPoint[] points,HumanBodyBones bone)
                    {
                        var foot=a.GetBoneTransform(bone);var result=new Vector3[points.Length];
                        for(int i=0;i<points.Length;i++)
                        {
                            if(!soles.TryPosition(points[i],out var position))throw new InvalidOperationException("Source geometry unresolved");
                            result[i]=Quaternion.Inverse(foot.rotation)*(position-foot.position);
                        }
                        return result;
                    }
                    geometry.left[index]=Capture(left,HumanBodyBones.LeftFoot);geometry.right[index]=Capture(right,HumanBodyBones.RightFoot);
                });
                for(int i=0;i<frames.Length;i++)
                {
                    geometry.height[i]=new Vector2(frames[i].leftSole.y,frames[i].rightSole.y);
                    var l=Quaternion.Inverse(frames[i].leftGoalRotation)*frames[i].leftRotation;
                    var r=Quaternion.Inverse(frames[i].rightGoalRotation)*frames[i].rightRotation;
                    if(data.Count==0&&i==0){report.leftGoalToBone=l;report.rightGoalToBone=r;}
                    report.maximumGoalToBoneAngleDeviation=Mathf.Max(report.maximumGoalToBoneAngleDeviation,Quaternion.Angle(report.leftGoalToBone,l),Quaternion.Angle(report.rightGoalToBone,r));
                }
                data.Add(cycle.clip,geometry);
            }
            if(report.maximumGoalToBoneAngleDeviation>.5f){report.status="GOAL_FRAME_REVIEW";Save();throw new InvalidOperationException("IK goal/bone orientation frame is not constant on this avatar");}
            report.status="MEASURING";Save();
            for(int gait=0;gait<=4;gait++)for(int direction=0;direction<16;direction++)
            {
                var row=new Row {gait=gait/4f,angle=direction*22.5f,minSole=float.PositiveInfinity};
                AnimatorControllerPlayable active=default;
                var clipInfo=new List<AnimatorClipInfo>();var inputs=new List<Input>();
                var desired=new List<Vector2>();var corrections=new List<Vector2>();
                var runtimeInputs=new List<NativeFootSupportSet.Input>(4);
                ActorPoseSampler.Frame[] fk=null;int beforeEvaluation=0;
                void Configure(AnimatorControllerPlayable p)
                {
                    active=p;float tier=Mathf.Lerp(profile.blendThresholds.x,profile.blendThresholds.y,row.gait),angle=row.angle*Mathf.Deg2Rad;
                    p.SetFloat("LitSpeed",tier);p.SetFloat("LocomotionTier",tier);p.SetFloat("LocomotionPlaybackRate",1);
                    p.SetFloat("HorizontalMovement",Mathf.Sin(angle));p.SetFloat("ForwardMovement",Mathf.Cos(angle));p.SetFloat("CombatMoveMagnitude",1);
                    p.SetBool("CombatStrafeActive",true);p.SetBool("IsMoving",true);p.SetLayerWeight(1,0);p.SetLayerWeight(2,0);
                    p.Play("Base Layer.CombatLocomotion",0,0);
                }
                void Goals(Animator a,bool correct)
                {
                    clipInfo.Clear();clipInfo.AddRange(active.GetCurrentAnimatorClipInfo(0));
                    float phase=active.GetCurrentAnimatorStateInfo(0).normalizedTime;
                    if(!DiagnosticBlend(profile,clipInfo,phase,out var pose,coherentRotations))throw new InvalidOperationException("Unqualified native goals");
                    if(nativeRotations)
                    {
                        pose.leftRotation=a.GetIKRotation(AvatarIKGoal.LeftFoot);
                        pose.rightRotation=a.GetIKRotation(AvatarIKGoal.RightFoot);
                    }
                    if(fkRotations)
                    {
                        var f=fk[correct?desired.Count:beforeEvaluation++];
                        var l=f.leftRotation*Quaternion.Inverse(report.leftGoalToBone);var r=f.rightRotation*Quaternion.Inverse(report.rightGoalToBone);
                        row.maxNativeToFKAngle=Mathf.Max(row.maxNativeToFKAngle,Quaternion.Angle(l,pose.leftRotation),Quaternion.Angle(r,pose.rightRotation));
                        if(anatomicalRotations)
                        {
                            Quaternion Mean(bool left,Quaternion reference)
                            {
                                Vector3 sum=default;float total=0;
                                foreach(var info in clipInfo)
                                {
                                    if(info.weight<.001f)continue;
                                    var cycle=profile.FindCycle(info.clip);cycle.footGoals.Evaluate(phase+cycle.cycleOffset,out _,out _,out var ql,out var qr);
                                    var q=Quaternion.Inverse(reference)*(left?ql:qr);
                                    if(q.w<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
                                    var v=new Vector3(q.x,q.y,q.z);float length=v.magnitude;float angle=2*Mathf.Atan2(length,q.w);
                                    row.maxAnatomicalSourceAngle=Mathf.Max(row.maxAnatomicalSourceAngle,angle*Mathf.Rad2Deg);
                                    if(length>.000001f)sum+=v*(angle*info.weight/length);
                                    total+=info.weight;
                                }
                                sum/=total;float a=sum.magnitude;
                                return a<.000001f?reference:reference*Quaternion.AngleAxis(a*Mathf.Rad2Deg,sum/a);
                            }
                            l=Mean(true,l);r=Mean(false,r);
                        }
                        pose.leftRotation=l;pose.rightRotation=r;
                    }
                    if(correct)
                    {
                        inputs.Clear();Vector2 height=default;float total=0;
                        foreach(var info in clipInfo)
                        {
                            if(info.weight<.001f)continue;
                            var cycle=profile.FindCycle(info.clip);float t=Mathf.Repeat(phase+cycle.cycleOffset,1)*SourceSteps;
                            var input=new Input {geometry=data[info.clip],index=Mathf.Min(SourceSteps-1,Mathf.FloorToInt(t)),fraction=t-Mathf.Floor(t),weight=info.weight};
                            inputs.Add(input);total+=info.weight;
                            height+=Vector2.LerpUnclamped(input.geometry.height[input.index],input.geometry.height[input.index+1],input.fraction)*info.weight;
                        }
                        height/=total;
                        float Predict(bool left,Quaternion rotation,HashSet<int> used)
                        {
                            float low=float.PositiveInfinity;int selected=-1;
                            for(int vertex=0;vertex<(left?leftIds.Length:rightIds.Length);vertex++)
                            {
                                Vector3 point=default;
                                foreach(var input in inputs)
                                {
                                    var shape=left?input.geometry.left:input.geometry.right;
                                    point+=Vector3.LerpUnclamped(shape[input.index][vertex],shape[input.index+1][vertex],input.fraction)*input.weight/total;
                                }
                                float y=(rotation*point).y;
                                if(y<low){low=y;selected=vertex;}
                            }
                            used.Add(selected);return low;
                        }
                        var delta=new Vector2(height.x-pose.left.y-Predict(true,pose.leftRotation*report.leftGoalToBone,leftUsed),height.y-pose.right.y-Predict(false,pose.rightRotation*report.rightGoalToBone,rightUsed));
                        pose.left.y+=delta.x;pose.right.y+=delta.y;
                        desired.Add(height);corrections.Add(delta);
                        if(runtimeData!=null)
                        {
                            var reference=fk[desired.Count-1];
                            if(!runtimeData.TryBlend(profile,clipInfo,phase,reference.leftRotation,reference.rightRotation,runtimeInputs,out var runtimePose))throw new InvalidOperationException("Runtime support data incomplete");
                            row.maximumRuntimePositionError=Mathf.Max(row.maximumRuntimePositionError,Vector3.Distance(runtimePose.left,pose.left),Vector3.Distance(runtimePose.right,pose.right));
                            row.maximumRuntimeRotationError=Mathf.Max(row.maximumRuntimeRotationError,Quaternion.Angle(runtimePose.leftRotation,pose.leftRotation),Quaternion.Angle(runtimePose.rightRotation,pose.rightRotation));
                            pose=runtimePose;
                        }
                    }
                    NativeFootGoalCurves.Apply(a,pose);
                    a.SetIKPositionWeight(AvatarIKGoal.LeftFoot,1);a.SetIKPositionWeight(AvatarIKGoal.RightFoot,1);a.SetIKRotationWeight(AvatarIKGoal.LeftFoot,1);a.SetIKRotationWeight(AvatarIKGoal.RightFoot,1);
                }
                if(fkRotations)
                {
                    var readL=new List<Quaternion>();var readR=new List<Quaternion>();
                    fk=sampler.Sample(profile.walk[0].clip,sampleCount:240,cycles:2,controller:activeController,configureController:Configure,warmControllerPose:warmControllerPose,modifyIKGoals:a=>
                    {
                        readL.Add(a.GetBoneTransform(HumanBodyBones.LeftFoot).rotation);readR.Add(a.GetBoneTransform(HumanBodyBones.RightFoot).rotation);
                        a.SetIKPositionWeight(AvatarIKGoal.LeftFoot,0);a.SetIKPositionWeight(AvatarIKGoal.RightFoot,0);a.SetIKRotationWeight(AvatarIKGoal.LeftFoot,0);a.SetIKRotationWeight(AvatarIKGoal.RightFoot,0);
                    });
                    for(int i=0;i<fk.Length;i++)report.maximumFKCallbackReadDeviation=Mathf.Max(report.maximumFKCallbackReadDeviation,Quaternion.Angle(fk[i].leftRotation,readL[i]),Quaternion.Angle(fk[i].rightRotation,readR[i]));
                }
                var before=sampler.Sample(profile.walk[0].clip,sampleCount:240,cycles:2,measureSoles:true,controller:activeController,configureController:Configure,warmControllerPose:warmControllerPose,modifyIKGoals:a=>Goals(a,false));
                var after=sampler.Sample(profile.walk[0].clip,sampleCount:240,cycles:2,measureSoles:true,controller:activeController,configureController:Configure,warmControllerPose:warmControllerPose,modifyIKGoals:a=>Goals(a,true));
                if(after.Length!=desired.Count)throw new InvalidOperationException("IK callback/sample mismatch");
                for(int i=0;i<after.Length;i++)
                {
                    var a=after[i];var b=before[i];
                    row.maxHeightError=Mathf.Max(row.maxHeightError,Mathf.Abs(a.leftSole.y-desired[i].x),Mathf.Abs(a.rightSole.y-desired[i].y));
                    row.minSole=Mathf.Min(row.minSole,a.leftSole.y-Floor,a.rightSole.y-Floor);row.maxLowerSole=Mathf.Max(row.maxLowerSole,Mathf.Min(a.leftSole.y,a.rightSole.y)-Floor);
                    row.maxCorrection=Mathf.Max(row.maxCorrection,Mathf.Abs(corrections[i].x),Mathf.Abs(corrections[i].y));
                    if(i>0)
                    {
                        float ds=Mathf.Max(Mathf.Abs(corrections[i].x-corrections[i-1].x),Mathf.Abs(corrections[i].y-corrections[i-1].y));
                        float da=Mathf.Max(Quaternion.Angle(a.leftRotation,after[i-1].leftRotation),Quaternion.Angle(a.rightRotation,after[i-1].rightRotation));
                        if(ds>row.maxCorrectionStep)row.peakCorrectionSample=i;
                        if(da>row.maxFootRotationStep)row.peakRotationSample=i;
                        row.maxCorrectionStep=Mathf.Max(row.maxCorrectionStep,Mathf.Abs(corrections[i].x-corrections[i-1].x),Mathf.Abs(corrections[i].y-corrections[i-1].y));
                        row.maxFootRotationStep=Mathf.Max(row.maxFootRotationStep,Quaternion.Angle(a.leftRotation,after[i-1].leftRotation),Quaternion.Angle(a.rightRotation,after[i-1].rightRotation));
                    }
                    row.maxPelvisDifference=Mathf.Max(row.maxPelvisDifference,Vector3.Distance(a.body,b.body));row.maxRootDifference=Mathf.Max(row.maxRootDifference,Vector3.Distance(a.root,b.root));
                    row.maxFootXZDifference=Mathf.Max(row.maxFootXZDifference,Vector3.ProjectOnPlane(a.leftFoot-b.leftFoot,Vector3.up).magnitude,Vector3.ProjectOnPlane(a.rightFoot-b.rightFoot,Vector3.up).magnitude);
                }
                row.samples=after.Length;row.clips=clipInfo.Where(c=>c.weight>.001f).Select(c=>c.clip.name+":"+c.weight).ToArray();
                row.status=row.maxHeightError<=.02f&&row.maxPelvisDifference<=.001f&&row.maxRootDifference<=.0001f&&row.maxFootXZDifference<=.003f&&row.maxCorrectionStep<=.03f&&row.maxFootRotationStep<=30&&row.maximumRuntimePositionError<=.00001f&&row.maximumRuntimeRotationError<=.1f?"PASS_NUMERIC_PROTOTYPE":"REVIEW";
                report.cases.Add(row);Save();Debug.Log("[LucianContactBlendAudit] "+report.cases.Count+"/80: "+row.status+" height="+row.maxHeightError);
            }
        }}
        finally {for(int i=transient.Count-1;i>=0;i--)if(transient[i]!=null)UnityEngine.Object.DestroyImmediate(transient[i]);}
        report.leftFeatures=leftUsed.OrderBy(i=>i).Select(i=>leftIds[i]).ToArray();report.rightFeatures=rightUsed.OrderBy(i=>i).Select(i=>rightIds[i]).ToArray();
        report.status=report.minimumQuaternionLoopDot>0&&report.cases.All(r=>r.status=="PASS_NUMERIC_PROTOTYPE")?"PASS_NUMERIC_PROTOTYPE":"REVIEW";Save();
        if(verifyRuntime)
        {
            if(report.status!="PASS_NUMERIC_PROTOTYPE"||report.cases.Count!=80)throw new InvalidOperationException("Runtime support verification failed");
            NativeFootSupportPreparation.Validate(profile,runtimeData,false);
            if(runtimeData.runtimeValidationSignature!=runtimeData.dataSignature)
            {
                runtimeData.runtimeValidationSignature=runtimeData.dataSignature;EditorUtility.SetDirty(runtimeData);AssetDatabase.SaveAssetIfDirty(runtimeData);
            }
        }
        if(prepareSupport)
        {
            if(report.status!="PASS_NUMERIC_PROTOTYPE")throw new InvalidOperationException("Complete support prevalidation failed; no dataset written");
            var prepared=ScriptableObject.CreateInstance<NativeFootSupportSet>();prepared.name="LucianFootSupports";
            try
            {
                prepared.avatar=profile.measuredAvatar;prepared.baseController=controller.runtimeAnimatorController;prepared.steps=SourceSteps;
                prepared.leftPointIds=report.leftFeatures;prepared.rightPointIds=report.rightFeatures;prepared.leftGoalToBone=report.leftGoalToBone;prepared.rightGoalToBone=report.rightGoalToBone;
                using(var soles=new ActorSoleSampler(ActorAnimationAudit.Actor(false),true))
                {
                    var skins=soles.FootRenderers();prepared.meshes=skins.Select(s=>s.sharedMesh).ToArray();
                    var actor=ActorAnimationAudit.Actor(false);prepared.rendererPaths=skins.Select(s=>AnimationUtility.CalculateTransformPath(s.transform,actor.transform)).ToArray();
                }
                var li=leftUsed.OrderBy(i=>i).ToArray();var ri=rightUsed.OrderBy(i=>i).ToArray();
                prepared.cycles=cycles.Select(c=>new NativeFootSupportSet.Cycle {clip=c.clip,cycleOffset=c.cycleOffset,
                    left=data[c.clip].left.SelectMany(frame=>li.Select(i=>frame[i])).ToArray(),right=data[c.clip].right.SelectMany(frame=>ri.Select(i=>frame[i])).ToArray(),heights=data[c.clip].height.ToArray()}).ToArray();
                NativeFootSupportPreparation.StorePrepared(prepared,previousControllerSignature);
            }
            finally{UnityEngine.Object.DestroyImmediate(prepared);}
        }
        void Save()=>File.WriteAllText(output+"/report.json",JsonUtility.ToJson(report,true));
    }
    internal static RuntimeAnimatorController CreateMuscleController(AnimatorOverrideController source,List<UnityEngine.Object> transient,string stateName="CombatLocomotion",bool builtInFootIK=false,
        string previousStateName=null, bool previousFootIK=false)
    {
        var graph=(AnimatorController)source.runtimeAnimatorController;
        var original=LocomotionPresentationCalibration.States(graph.layers[0].stateMachine).Single(s=>s.name==stateName);
        var copy=new AnimatorController {name="Lucian temporary muscle pose",hideFlags=HideFlags.HideAndDontSave};transient.Add(copy);
        copy.parameters=graph.parameters;
        var layers=new AnimatorControllerLayer[3];
        for(int i=0;i<layers.Length;i++)
        {
            var machine=new AnimatorStateMachine {name=graph.layers[i].name,hideFlags=HideFlags.HideAndDontSave};transient.Add(machine);
            var state=machine.AddState(i==0?stateName:"Unused");state.hideFlags=HideFlags.HideAndDontSave;transient.Add(state);machine.defaultState=state;
            if(i==0)
            {
                state.motion=original.motion;state.speed=original.speed;state.speedParameter=original.speedParameter;state.speedParameterActive=original.speedParameterActive;
                state.iKOnFeet=builtInFootIK;
                if (previousStateName != null)
                {
                    var previous = LocomotionPresentationCalibration.States(graph.layers[0].stateMachine).Single(s => s.name == previousStateName);
                    var link = previous.transitions.Single(t => t.destinationState == original && t.conditions.Length == 0);
                    var entry = machine.AddState(previousStateName);
                    transient.Add(entry);
                    entry.hideFlags = HideFlags.HideAndDontSave;
                    entry.motion = previous.motion;
                    entry.speed = previous.speed;
                    entry.speedParameter = previous.speedParameter;
                    entry.speedParameterActive = previous.speedParameterActive;
                    entry.iKOnFeet = previousFootIK;
                    var transition = entry.AddTransition(state);
                    transient.Add(transition);
                    transition.hideFlags = HideFlags.HideAndDontSave;
                    transition.hasExitTime = link.hasExitTime;
                    transition.exitTime = link.exitTime;
                    transition.hasFixedDuration = link.hasFixedDuration;
                    transition.duration = link.duration;
                    transition.offset = link.offset;
                }
            }
            layers[i]=new AnimatorControllerLayer {name=graph.layers[i].name,stateMachine=machine,defaultWeight=i==0?1:0,iKPass=i==0};
        }
        copy.layers=layers;
        var result=new AnimatorOverrideController(copy) {hideFlags=HideFlags.HideAndDontSave};transient.Add(result);
        var replacements=new List<KeyValuePair<AnimationClip,AnimationClip>>();source.GetOverrides(replacements);
        var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();result.GetOverrides(pairs);
        for(int i=0;i<pairs.Count;i++)pairs[i]=new KeyValuePair<AnimationClip,AnimationClip>(pairs[i].Key,replacements.Single(p=>p.Key==pairs[i].Key).Value);
        result.ApplyOverrides(pairs);return result;
    }
    // Rejected fixed-hemisphere experiment retained only for reproducible Editor diagnostics.
    static bool DiagnosticBlend(LocomotionPresentationProfile profile,List<AnimatorClipInfo> clips,float phase,out NativeFootGoalCurves.Pose pose,bool fixedHemisphere)
    {
        if(!fixedHemisphere)return NativeFootGoalCurves.Blend(profile,clips,phase,out pose);
        pose=default;var reference=profile.explorationWalk?.footGoals;
        if(reference==null||!reference.Complete)return false;
        reference.Evaluate(0,out _,out _,out var lr,out var rr);
        float total=0;Vector4 lq=default,rq=default;
        foreach(var info in clips)
        {
            if(info.weight<.001f)continue;
            var cycle=profile.FindCycle(info.clip);if(cycle?.footGoals==null||!cycle.footGoals.Complete)return false;
            cycle.footGoals.Evaluate(phase+cycle.cycleOffset,out var l,out var r,out var ql,out var qr);
            cycle.footGoals.Evaluate(0,out _,out _,out var startL,out var startR);
            if(Quaternion.Dot(lr,startL)<0)ql=new Quaternion(-ql.x,-ql.y,-ql.z,-ql.w);
            if(Quaternion.Dot(rr,startR)<0)qr=new Quaternion(-qr.x,-qr.y,-qr.z,-qr.w);
            pose.left+=l*info.weight;pose.right+=r*info.weight;
            lq+=new Vector4(ql.x,ql.y,ql.z,ql.w)*info.weight;rq+=new Vector4(qr.x,qr.y,qr.z,qr.w)*info.weight;total+=info.weight;
        }
        if(total<.001f)return false;
        pose.left/=total;pose.right/=total;pose.leftRotation=new Quaternion(lq.x,lq.y,lq.z,lq.w).normalized;pose.rightRotation=new Quaternion(rq.x,rq.y,rq.z,rq.w).normalized;return true;
    }
}
