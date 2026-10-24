using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class ActorFacingCalibration
{
    [Serializable] public sealed class GroundingProbe
    {
        public string actor, clip, status;
        public float floor, bodyYResponse, maximumVerticalCorrection, beforeMaximumSupportHeight, afterMaximumSupportError;
        public float maximumJointRotationDifference, maximumPhysicalRootDisplacement, maximumRelativeFootDifference;
        public float beforeBodyYRange, afterBodyYRange, additionalLoopSeam, maximumCorrectionSpeed;
    }
    [Serializable] public sealed class GroundingProbeReport
    {
        public string scope="In-memory feasibility probe on the four uncertain WALK clips only. RootT.y is body pose, not physical actor movement. No asset or physics change.";
        public bool acceptedForGameplay;
        public string reviewDecision="NOT accepted as automatic grounding: the constructed sole constraint changes vertical hip oscillation and does not prove natural contact or horizontal support.";
        public List<GroundingProbe> cases=new List<GroundingProbe>();
    }
    [Serializable] public sealed class ProjectionCase
    {
        public string actor,clip,settings;
        public float maximumSupportError,bodyYRange,maximumBodyChange,physicalRootDisplacement;
    }
    [Serializable] public sealed class ProjectionReport
    {
        public string scope="In-memory Feet/Center-of-Mass extraction settings comparison; no asset write or automatic adoption";
        public List<ProjectionCase> cases=new List<ProjectionCase>();
    }
    [MenuItem("Lit/Animation Presentation/Facing Calibration/Probe Native Vertical Extraction (No Asset Writes)")]
    public static void ProbeNativeVerticalExtraction()
    {
        var contacts=JsonUtility.FromJson<ContactReport>(File.ReadAllText(ReportFolder+"/contacts.json"));
        var actors=Resolve().GroupBy(i=>i.entry.actor).Select(g=>g.First()).ToArray();var report=new ProjectionReport();
        foreach(var contact in contacts.cases.Where(c=>!c.nativeIK))
        {
            var actor=actors.Single(a=>a.entry.actor==contact.actor);var source=ActorAnimationAudit.Source(contact.clip);
            using(var sampler=new ActorPoseSampler(actor.actor))
            {
                var before=sampler.Sample(source,measureSoles:true);
                foreach(bool bake in new[]{false,true})foreach(bool feet in new[]{false,true})
                {
                    var probe=new AnimationClip();
                    try
                    {
                        EditorUtility.CopySerialized(source,probe);var settings=AnimationUtility.GetAnimationClipSettings(probe);
                        settings.keepOriginalPositionY=false;settings.heightFromFeet=feet;settings.loopBlendPositionY=bake;
                        AnimationUtility.SetAnimationClipSettings(probe,settings);
                        var frames=sampler.Sample(probe,sampleCount:before.Length-1,measureSoles:true);
                        report.cases.Add(new ProjectionCase {actor=contact.actor,clip=contact.clip,settings="BakeY="+bake+" Feet="+feet,
                            maximumSupportError=frames.Max(f=>Mathf.Abs(Mathf.Min(f.leftSole.y,f.rightSole.y)-contact.groundReferenceY)),
                            bodyYRange=frames.Max(f=>f.body.y)-frames.Min(f=>f.body.y),
                            maximumBodyChange=Enumerable.Range(0,frames.Length).Max(i=>Vector3.Distance(frames[i].body,before[i].body)),
                            physicalRootDisplacement=frames.Max(f=>f.root.magnitude)});
                    }
                    finally {Object.DestroyImmediate(probe);}
                }
            }
        }
        File.WriteAllText(ReportFolder+"/vertical-extraction-probe.json",JsonUtility.ToJson(report,true));
    }
    static AnimationCurve ShiftCurve(AnimationCurve source,float offset)
    {
        var keys=source.keys;
        for(int i=0;i<keys.Length;i++){var key=keys[i];key.value+=offset;keys[i]=key;}
        return new AnimationCurve(keys){preWrapMode=source.preWrapMode,postWrapMode=source.postWrapMode};
    }
    [MenuItem("Lit/Animation Presentation/Facing Calibration/Probe Walk Body Grounding (No Asset Writes)")]
    public static void ProbeWalkGrounding() => ProbeWalkGrounding(false);
    [MenuItem("Lit/Animation Presentation/Facing Calibration/Capture Walk Grounding Feasibility")]
    public static void CaptureWalkGrounding() => ProbeWalkGrounding(true);
    static void ProbeWalkGrounding(bool capture)
    {
        var contacts=JsonUtility.FromJson<ContactReport>(File.ReadAllText(ReportFolder+"/contacts.json"));
        var actors=Resolve().GroupBy(i=>i.entry.actor).Select(g=>g.First()).ToArray();var report=new GroundingProbeReport();
        var binding=EditorCurveBinding.FloatCurve("",typeof(Animator),"RootT.y");
        foreach(var contact in contacts.cases.Where(c=>!c.nativeIK))
        {
            var actor=actors.Single(a=>a.entry.actor==contact.actor);var source=ActorAnimationAudit.Source(contact.clip);
            var row=new GroundingProbe {actor=contact.actor,clip=contact.clip,floor=contact.groundReferenceY};report.cases.Add(row);
            AnimationClip shifted=null,candidate=null;
            try
            {
                var curve=AnimationUtility.GetEditorCurve(source,binding);
                if(curve==null)throw new InvalidOperationException("Missing body RootT.y; no approximation allowed");
                using(var sampler=new ActorPoseSampler(actor.actor))
                {
                    int count=Mathf.Max(120,Mathf.CeilToInt(source.length*60));
                    var before=sampler.Sample(source,sampleCount:count,measureSoles:true);
                    shifted=new AnimationClip();EditorUtility.CopySerialized(source,shifted);AnimationUtility.SetEditorCurve(shifted,binding,ShiftCurve(curve,.01f));
                    var perturbed=sampler.Sample(shifted,sampleCount:count,measureSoles:true);
                    row.bodyYResponse=Enumerable.Range(0,count).Average(i=>(perturbed[i].body.y-before[i].body.y)/.01f);
                    if(!float.IsFinite(row.bodyYResponse) || Mathf.Abs(row.bodyYResponse)<.1f)throw new InvalidOperationException("Body-height response unavailable");
                    var values=new AnimationCurve();
                    for(int i=0;i<=count;i++)
                    {
                        float sole=Mathf.Min(before[i].leftSole.y,before[i].rightSole.y);
                        float correction=row.floor-sole;
                        if(!float.IsFinite(correction))throw new InvalidOperationException("Non-finite sole height");
                        row.maximumVerticalCorrection=Mathf.Max(row.maximumVerticalCorrection,Mathf.Abs(correction));
                        row.beforeMaximumSupportHeight=Mathf.Max(row.beforeMaximumSupportHeight,sole-row.floor);
                        values.AddKey(before[i].time,curve.Evaluate(before[i].time)+correction/row.bodyYResponse);
                    }
                    if(row.maximumVerticalCorrection>.1f)throw new InvalidOperationException("More than 10 cm required: animation retouch, not a small calibration");
                    for(int i=0;i<values.length;i++){AnimationUtility.SetKeyLeftTangentMode(values,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(values,i,AnimationUtility.TangentMode.Linear);}
                    candidate=new AnimationClip();EditorUtility.CopySerialized(source,candidate);AnimationUtility.SetEditorCurve(candidate,binding,values);
                    var after=sampler.Sample(candidate,sampleCount:count,measureSoles:true);
                    row.beforeBodyYRange=before.Max(f=>f.body.y)-before.Min(f=>f.body.y);row.afterBodyYRange=after.Max(f=>f.body.y)-after.Min(f=>f.body.y);
                    row.additionalLoopSeam=Vector3.Distance(after[0].body,after[count].body)-Vector3.Distance(before[0].body,before[count].body);
                    for(int i=0;i<=count;i++)
                    {
                        row.afterMaximumSupportError=Mathf.Max(row.afterMaximumSupportError,Mathf.Abs(Mathf.Min(after[i].leftSole.y,after[i].rightSole.y)-row.floor));
                        row.maximumPhysicalRootDisplacement=Mathf.Max(row.maximumPhysicalRootDisplacement,after[i].root.magnitude);
                        if(i>0)row.maximumCorrectionSpeed=Mathf.Max(row.maximumCorrectionSpeed,Mathf.Abs((after[i].body.y-before[i].body.y)-(after[i-1].body.y-before[i-1].body.y))/(source.length/count));
                        row.maximumRelativeFootDifference=Mathf.Max(row.maximumRelativeFootDifference,
                            Vector3.Distance(before[i].leftFoot-before[i].body,after[i].leftFoot-after[i].body),Vector3.Distance(before[i].rightFoot-before[i].body,after[i].rightFoot-after[i].body));
                        for(int bone=0;bone<after[i].boneRotations.Length;bone++)
                            row.maximumJointRotationDifference=Mathf.Max(row.maximumJointRotationDifference,Quaternion.Angle(before[i].boneRotations[bone],after[i].boneRotations[bone]));
                    }
                    row.status=row.afterMaximumSupportError<=.002f && row.maximumPhysicalRootDisplacement<=.001f && row.maximumRelativeFootDifference<=.001f && row.maximumJointRotationDifference<=.1f?
                        "Feasible pose-only candidate. Rendered stance, loop seam and horizontal slip NOT validated; no asset written":
                        "Rejected: protected pose or support residual outside tolerance";
                    if(capture && row.status.StartsWith("Feasible"))foreach(float phase in new[]{0,.25f,.5f,.75f})
                    {
                        var entry=new Entry {actor=row.actor,cycle=Path.GetFileNameWithoutExtension(row.clip)+"_p"+Mathf.RoundToInt(phase*100),target="In-memory grounding probe"};
                        CaptureInputs(new List<Input> {new Input {actor=actor.actor,source=source,candidate=candidate,entry=entry}},"GroundingCaptures",phase,row.floor,true);
                    }
                }
            }
            catch(Exception e){row.status="Rejected: "+e.Message;}
            finally {if(shifted!=null)Object.DestroyImmediate(shifted);if(candidate!=null)Object.DestroyImmediate(candidate);}
        }
        File.WriteAllText(ReportFolder+"/grounding-probe.json",JsonUtility.ToJson(report,true));
    }
}
