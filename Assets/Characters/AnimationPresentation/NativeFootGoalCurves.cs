using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Editor-sampled native IK effectors in actor-root space. Presentation data, never physical displacement.</summary>
[Serializable]
public sealed class NativeFootGoalCurves
{
    public AnimationCurve[] leftPosition,rightPosition,leftRotation,rightRotation;
    public bool Complete=>Valid(leftPosition,3)&&Valid(rightPosition,3)&&Valid(leftRotation,4)&&Valid(rightRotation,4);
    private static bool Valid(AnimationCurve[] curves,int count)
    {
        if(curves==null||curves.Length!=count)return false;
        foreach(var curve in curves)if(curve==null||curve.length<2)return false;
        return true;
    }
    private static Vector3 Position(AnimationCurve[] c,float phase)=>new Vector3(c[0].Evaluate(phase),c[1].Evaluate(phase),c[2].Evaluate(phase));
    private static Quaternion Rotation(AnimationCurve[] c,float phase)=>new Quaternion(c[0].Evaluate(phase),c[1].Evaluate(phase),c[2].Evaluate(phase),c[3].Evaluate(phase)).normalized;
    public void Evaluate(float phase,out Vector3 left,out Vector3 right,out Quaternion lr,out Quaternion rr)
    {
        phase=Mathf.Repeat(phase,1);left=Position(leftPosition,phase);right=Position(rightPosition,phase);lr=Rotation(leftRotation,phase);rr=Rotation(rightRotation,phase);
    }
    public struct Pose {public Vector3 left,right;public Quaternion leftRotation,rightRotation;}
    public static bool Blend(LocomotionPresentationProfile profile,List<AnimatorClipInfo> clips,float phase,out Pose pose)
    {
        pose=default;float total=0;Vector4 lq=default,rq=default;Quaternion lr=Quaternion.identity,rr=Quaternion.identity;
        foreach(var clip in clips)
        {
            if(clip.weight<.001f)continue;
            var cycle=profile.FindCycle(clip.clip);
            if(cycle==null||cycle.footGoals==null||!cycle.footGoals.Complete)return false;
            cycle.footGoals.Evaluate(phase+cycle.cycleOffset,out var left,out var right,out var ql,out var qr);
            if(total==0){lr=ql;rr=qr;}
            if(Quaternion.Dot(lr,ql)<0)ql=new Quaternion(-ql.x,-ql.y,-ql.z,-ql.w);
            if(Quaternion.Dot(rr,qr)<0)qr=new Quaternion(-qr.x,-qr.y,-qr.z,-qr.w);
            pose.left+=left*clip.weight;pose.right+=right*clip.weight;
            lq+=new Vector4(ql.x,ql.y,ql.z,ql.w)*clip.weight;rq+=new Vector4(qr.x,qr.y,qr.z,qr.w)*clip.weight;total+=clip.weight;
        }
        if(total<.001f)return false;
        pose.left/=total;pose.right/=total;pose.leftRotation=new Quaternion(lq.x,lq.y,lq.z,lq.w).normalized;pose.rightRotation=new Quaternion(rq.x,rq.y,rq.z,rq.w).normalized;
        return true;
    }
    public static void Apply(Animator animator,Pose pose)
    {
        animator.SetIKPosition(AvatarIKGoal.LeftFoot,animator.transform.TransformPoint(pose.left));animator.SetIKPosition(AvatarIKGoal.RightFoot,animator.transform.TransformPoint(pose.right));
        animator.SetIKRotation(AvatarIKGoal.LeftFoot,animator.transform.rotation*pose.leftRotation);animator.SetIKRotation(AvatarIKGoal.RightFoot,animator.transform.rotation*pose.rightRotation);
    }
}
