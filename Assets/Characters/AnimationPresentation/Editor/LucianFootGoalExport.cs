using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Prepare and verify all root-space native effectors before publishing presentation data.</summary>
public static class LucianFootGoalExport
{
    [Serializable] sealed class Row {public string clip;public float maximumFootPositionError,maximumFootAngleError;public int callbackSamples;}
    [Serializable] sealed class Report {public List<Row> clips=new List<Row>();}
    public static void Run()
    {
        const string output="Library/LucianCoherence/root-goals";Directory.CreateDirectory(output);
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(LucianCadencePreview.Folder+"/LocomotionMeasurements.asset");
        if(profile.footSupports!=null){NativeFootSupportPreparation.Validate(profile,profile.footSupports,true);return;}
        var cycles=profile.walk.Concat(profile.run).Concat(new[]{profile.explorationWalk,profile.explorationRun}).ToArray();
        var prepared=new Dictionary<LocomotionPresentationProfile.Cycle,NativeFootGoalCurves>();var report=new Report();
        using(var sampler=new ActorPoseSampler(ActorAnimationAudit.Actor(false)))
        foreach(var cycle in cycles)
        {
            var frames=sampler.Sample(cycle.clip,sampleCount:240,applyFootIK:true,captureIKGoals:true);
            var data=new NativeFootGoalCurves {leftPosition=Positions(frames.Select(f=>f.leftGoal).ToArray()),rightPosition=Positions(frames.Select(f=>f.rightGoal).ToArray()),leftRotation=Rotations(frames.Select(f=>f.leftGoalRotation).ToArray()),rightRotation=Rotations(frames.Select(f=>f.rightGoalRotation).ToArray())};
            const int count=479;int index=0;
            var reference=sampler.Sample(cycle.clip,sampleCount:count,applyFootIK:true);
            var reconstructed=sampler.Sample(cycle.clip,sampleCount:count,applyFootIK:true,modifyIKGoals:a=>
            {
                data.Evaluate((float)index/count,out var l,out var r,out var lr,out var rr);index++;
                NativeFootGoalCurves.Apply(a,new NativeFootGoalCurves.Pose {left=l,right=r,leftRotation=lr,rightRotation=rr});
                a.SetIKPositionWeight(AvatarIKGoal.LeftFoot,1);a.SetIKPositionWeight(AvatarIKGoal.RightFoot,1);a.SetIKRotationWeight(AvatarIKGoal.LeftFoot,1);a.SetIKRotationWeight(AvatarIKGoal.RightFoot,1);
            });
            var row=new Row {clip=AssetDatabase.GetAssetPath(cycle.clip),callbackSamples=index};
            for(int i=0;i<reference.Length;i++)
            {
                row.maximumFootPositionError=Mathf.Max(row.maximumFootPositionError,Vector3.Distance(reference[i].leftFoot,reconstructed[i].leftFoot),Vector3.Distance(reference[i].rightFoot,reconstructed[i].rightFoot));
                row.maximumFootAngleError=Mathf.Max(row.maximumFootAngleError,Quaternion.Angle(reference[i].leftRotation,reconstructed[i].leftRotation),Quaternion.Angle(reference[i].rightRotation,reconstructed[i].rightRotation));
            }
            report.clips.Add(row);File.WriteAllText(output+"/report.json",JsonUtility.ToJson(report,true));
            if(index!=count+1||row.maximumFootPositionError>.003f||row.maximumFootAngleError>.5f)throw new InvalidOperationException("Native effector roundtrip failed: "+cycle.clip.name);
            prepared.Add(cycle,data);
        }
        foreach(var pair in prepared)pair.Key.footGoals=pair.Value;
        EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
    }
    internal static AnimationCurve[] Positions(Vector3[] values)
    {
        return Enumerable.Range(0,3).Select(axis=>Curve(values.Select(v=>v[axis]).ToArray())).ToArray();
    }
    internal static AnimationCurve[] Rotations(Quaternion[] values)
    {
        for(int i=1;i<values.Length;i++)if(Quaternion.Dot(values[i-1],values[i])<0)values[i]=new Quaternion(-values[i].x,-values[i].y,-values[i].z,-values[i].w);
        return Enumerable.Range(0,4).Select(axis=>Curve(values.Select(v=>v[axis]).ToArray())).ToArray();
    }
    private static AnimationCurve Curve(float[] values)
    {
        var c=new AnimationCurve(values.Select((v,i)=>new Keyframe((float)i/(values.Length-1),v)).ToArray());
        for(int i=0;i<c.length;i++){AnimationUtility.SetKeyLeftTangentMode(c,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(c,i,AnimationUtility.TangentMode.ClampedAuto);}return c;
    }
}
