using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static partial class ActorFacingCalibration
{
    [Serializable] public sealed class ContactCase
    {
        public string actor, clip, reference, status;
        public bool nativeIK, controllerFootIK;
        public float groundReferenceY, curveOffset, contactOffset, contactDice, alternativeMargin;
        public float referenceLeftDuty, referenceRightDuty, leftDuty, rightDuty;
        public float[] referenceLeftTouchdowns, referenceRightTouchdowns, leftTouchdowns, rightTouchdowns;
    }
    [Serializable] public sealed class ContactReport
    {
        public string scope="Read-only rendered-sole contact hypotheses. No physics, controller offsets or animation curves are changed.";
        public float soleHeightTolerance=.02f, risingVelocityTolerance=.15f;
        public List<ContactCase> cases=new List<ContactCase>();
    }
    sealed class Contacts
    {
        public bool[] left, right;
    }
    static Contacts ContactMasks(ActorPoseSampler.Frame[] frames,float ground,float duration)
    {
        const int count=120;var result=new Contacts {left=new bool[count],right=new bool[count]};
        for(int i=0;i<count;i++)
        {
            int index=Mathf.Min(Mathf.FloorToInt((float)i/count*(frames.Length-1)),frames.Length-2);
            int previous=index==0?frames.Length-2:index-1;
            float dt=duration/(frames.Length-1);
            foreach(var left in new[]{true,false})
            {
                float height=left?frames[index].leftSole.y:frames[index].rightSole.y;
                float old=left?frames[previous].leftSole.y:frames[previous].rightSole.y;
                if(float.IsNaN(height) || float.IsInfinity(height) || float.IsNaN(old) || float.IsInfinity(old))throw new InvalidOperationException("No finite skinned sole measurement");
                (left?result.left:result.right)[i]=height<=ground+.02f && (height-old)/dt<=.15f;
            }
        }
        return result;
    }
    static float[] Touchdowns(bool[] mask)=>Enumerable.Range(0,mask.Length).Where(i=>mask[i]&&!mask[(i+mask.Length-1)%mask.Length]).Select(i=>(float)i/mask.Length).ToArray();
    static float Dice(Contacts a,Contacts b,int offset)
    {
        int intersection=0,total=0;
        for(int i=0;i<a.left.Length;i++)foreach(bool left in new[]{true,false})
        {
            bool x=(left?a.left:a.right)[i],y=(left?b.left:b.right)[(i+offset)%a.left.Length];
            if(x)total++;if(y)total++;if(x&&y)intersection++;
        }
        return total>0?2f*intersection/total:0;
    }
    [MenuItem("Lit/Animation Presentation/Facing Calibration/9 Audit Uncertain Sole Contacts (Read Only)")]
    public static void AuditUncertainContacts()
    {
        var phases=JsonUtility.FromJson<ActorCyclePhaseAlignment.Report>(File.ReadAllText(ReportFolder+"/directional-phases.json"));
        var actors=Resolve().GroupBy(i=>i.entry.actor).Select(g=>g.First()).ToArray();var report=new ContactReport();
        foreach(var weak in phases.items.Where(i=>i.correlation<.75f))foreach(bool nativeIK in new[]{false,true})
        {
            var actor=actors.Single(i=>i.actor.name==weak.actor);var view=new ControllerView(actor.actor.runtimeAnimatorController);
            var state=view.controller.layers[0].stateMachine.states.Single(s=>s.state.name==(actor.entry.actor=="Lucian"?"CombatLocomotion":"Locomotion")).state;
            var row=new ContactCase {actor=actor.entry.actor,clip=weak.clip,reference=weak.reference,curveOffset=weak.offset,nativeIK=nativeIK,controllerFootIK=state.iKOnFeet};report.cases.Add(row);
            try
            {
                using(var sampler=new ActorPoseSampler(actor.actor))
                {
                    var idle=sampler.Sample(actor.source,measureSoles:true,applyFootIK:nativeIK);
                    var floor=idle.Select(f=>Mathf.Min(f.leftSole.y,f.rightSole.y)).OrderBy(y=>y).ToArray();row.groundReferenceY=floor[floor.Length/2];
                    var reference=ActorAnimationAudit.Source(weak.reference);var clip=ActorAnimationAudit.Source(weak.clip);
                    var a=sampler.Sample(reference,sampleCount:Mathf.Max(120,Mathf.CeilToInt(reference.length*60)),measureSoles:true,applyFootIK:nativeIK);
                    var b=sampler.Sample(clip,sampleCount:Mathf.Max(120,Mathf.CeilToInt(clip.length*60)),measureSoles:true,applyFootIK:nativeIK);
                    var am=ContactMasks(a,row.groundReferenceY,reference.length);var bm=ContactMasks(b,row.groundReferenceY,clip.length);
                    row.referenceLeftDuty=am.left.Count(x=>x)/120f;row.referenceRightDuty=am.right.Count(x=>x)/120f;row.leftDuty=bm.left.Count(x=>x)/120f;row.rightDuty=bm.right.Count(x=>x)/120f;
                    row.referenceLeftTouchdowns=Touchdowns(am.left);row.referenceRightTouchdowns=Touchdowns(am.right);row.leftTouchdowns=Touchdowns(bm.left);row.rightTouchdowns=Touchdowns(bm.right);
                    var scores=Enumerable.Range(0,120).Select(offset=>new {offset,score=Dice(am,bm,offset)}).OrderByDescending(s=>s.score).ToArray();
                    var best=scores[0];row.contactOffset=best.offset/120f;row.contactDice=best.score;
                    float alternative=scores.Where(s=>Mathf.Abs(Mathf.DeltaAngle(s.offset*3,best.offset*3))>=36).Max(s=>s.score);row.alternativeMargin=best.score-alternative;
                    bool distinct=new[]{row.referenceLeftDuty,row.referenceRightDuty,row.leftDuty,row.rightDuty}.All(d=>d>.1f&&d<.9f);
                    row.status=distinct && row.contactDice>=.8f && row.alternativeMargin>=.05f?
                        "Contact-based phase candidate; verify rendered contacts before applying":
                        "Unresolved: contact duration/height or phase ambiguity requires rendered review; no offset applied";
                    var csv=new StringBuilder("phase,reference_left,reference_right,candidate_left,candidate_right\n");
                    for(int i=0;i<120;i++)csv.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0:F6},{1},{2},{3},{4}\n",i/120f,am.left[i]?1:0,am.right[i]?1:0,bm.left[i]?1:0,bm.right[i]?1:0);
                    File.WriteAllText(ReportFolder+"/contacts-"+row.actor+"-"+Path.GetFileNameWithoutExtension(weak.clip)+(nativeIK?"-ik":"-raw")+".csv",csv.ToString());
                }
            }
            catch(Exception e){row.status="Unresolved measurement: "+e.Message;}
        }
        File.WriteAllText(ReportFolder+"/contacts.json",JsonUtility.ToJson(report,true));
    }
}
