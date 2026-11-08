using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Avatar-qualified local presentation geometry. Never moves or locks an actor in world space.</summary>
public sealed class NativeFootSupportSet : ScriptableObject
{
    [Serializable] public sealed class Cycle
    {
        public AnimationClip clip;
        public float cycleOffset;
        [HideInInspector] public Vector3[] left,right;
        [HideInInspector] public Vector2[] heights;
    }
    public Avatar avatar;
    public RuntimeAnimatorController baseController;
    public string controllerSignature,sourcePrefabPath;
    public int steps;
    public string[] leftPointIds,rightPointIds,rendererPaths;
    public Mesh[] meshes;
    public Quaternion leftGoalToBone,rightGoalToBone;
    public string inputSignature,rigSignature,dataSignature;
    public string runtimeValidationSignature;
    public Cycle[] cycles;
    public struct Input
    {
        public Cycle geometry;
        public NativeFootGoalCurves goals;
        public int index;
        public float fraction,phase,weight;
    }
    public bool Matches(Animator animator)
    {
        if(animator==null||animator.avatar!=avatar||rendererPaths==null||meshes==null||rendererPaths.Length!=meshes.Length)return false;
        RuntimeAnimatorController graph=animator.runtimeAnimatorController;
        while(graph is AnimatorOverrideController overrides)graph=overrides.runtimeAnimatorController;
        if(graph!=baseController)return false;
        for(int i=0;i<meshes.Length;i++)
        {
            var node=string.IsNullOrEmpty(rendererPaths[i])?animator.transform:animator.transform.Find(rendererPaths[i]);
            var skin=node!=null?node.GetComponent<SkinnedMeshRenderer>():null;
            if(skin==null||skin.sharedMesh!=meshes[i])return false;
        }
        return true;
    }
    Cycle Find(AnimationClip clip)
    {
        if(cycles!=null)foreach(var cycle in cycles)if(cycle!=null&&cycle.clip==clip)return cycle;
        return null;
    }
    bool Complete(Cycle cycle)=>steps>1&&cycle!=null&&leftPointIds!=null&&leftPointIds.Length>0&&rightPointIds!=null&&rightPointIds.Length>0&&
        cycle.left!=null&&cycle.left.Length==(steps+1)*leftPointIds.Length&&cycle.right!=null&&cycle.right.Length==(steps+1)*rightPointIds.Length&&cycle.heights!=null&&cycle.heights.Length==steps+1;

    public bool TryBlend(LocomotionPresentationProfile profile,List<AnimatorClipInfo> clips,float phase,
        Quaternion leftBoneReference,Quaternion rightBoneReference,List<Input> inputs,out NativeFootGoalCurves.Pose pose)
    {
        pose=default;inputs.Clear();float total=0;
        foreach(var info in clips)
        {
            if(info.weight<.001f)continue;
            var cycle=profile.FindCycle(info.clip);var geometry=Find(info.clip);
            if(cycle?.footGoals==null||!cycle.footGoals.Complete||!Complete(geometry)||Mathf.Abs(cycle.cycleOffset-geometry.cycleOffset)>.000001f)return false;
            float p=Mathf.Repeat(phase+cycle.cycleOffset,1);float t=p*steps;
            inputs.Add(new Input {geometry=geometry,goals=cycle.footGoals,phase=p,index=Mathf.Min(steps-1,Mathf.FloorToInt(t)),fraction=t-Mathf.Floor(t),weight=info.weight});
            total+=info.weight;
        }
        if(total<.001f)return false;
        Vector2 height=default;
        foreach(var input in inputs)
        {
            input.goals.Evaluate(input.phase,out var l,out var r,out _,out _);
            pose.left+=l*input.weight/total;pose.right+=r*input.weight/total;
            height+=Vector2.LerpUnclamped(input.geometry.heights[input.index],input.geometry.heights[input.index+1],input.fraction)*input.weight/total;
        }
        pose.leftRotation=Mean(inputs,total,true,leftBoneReference*Quaternion.Inverse(leftGoalToBone));
        pose.rightRotation=Mean(inputs,total,false,rightBoneReference*Quaternion.Inverse(rightGoalToBone));
        pose.left.y=height.x-Lowest(inputs,total,true,pose.leftRotation*leftGoalToBone);
        pose.right.y=height.y-Lowest(inputs,total,false,pose.rightRotation*rightGoalToBone);
        return true;
    }
    static Quaternion Mean(List<Input> inputs,float total,bool left,Quaternion reference)
    {
        Vector3 sum=default;
        foreach(var input in inputs)
        {
            input.goals.Evaluate(input.phase,out _,out _,out var l,out var r);
            var q=Quaternion.Inverse(reference)*(left?l:r);
            if(q.w<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
            var v=new Vector3(q.x,q.y,q.z);float length=v.magnitude;
            if(length>.000001f)sum+=v*(2*Mathf.Atan2(length,q.w)*input.weight/length/total);
        }
        float angle=sum.magnitude;
        return angle<.000001f?reference:reference*Quaternion.AngleAxis(angle*Mathf.Rad2Deg,sum/angle);
    }
    float Lowest(List<Input> inputs,float total,bool left,Quaternion rotation)
    {
        int count=left?leftPointIds.Length:rightPointIds.Length;float lowest=float.PositiveInfinity;
        for(int vertex=0;vertex<count;vertex++)
        {
            Vector3 point=default;
            foreach(var input in inputs)
            {
                var shape=left?input.geometry.left:input.geometry.right;
                point+=Vector3.LerpUnclamped(shape[input.index*count+vertex],shape[(input.index+1)*count+vertex],input.fraction)*input.weight/total;
            }
            lowest=Mathf.Min(lowest,(rotation*point).y);
        }
        return lowest;
    }
}
