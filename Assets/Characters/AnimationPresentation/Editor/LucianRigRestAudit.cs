using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only rest hierarchy, Avatar definition and renderer bind-pose correspondence.</summary>
public static class LucianRigRestAudit
{
    [Serializable] sealed class Bone
    {
        public string human,path,referencePath;
        public Vector3 actualPosition,referencePosition,avatarPosition,actualScale,referenceScale;
        public float referencePositionError,avatarPositionError,referenceRotationError,referenceSegmentLength,actualSegmentLength;
    }
    [Serializable] sealed class Skin
    {
        public string renderer,mesh,referenceMesh;public int bones;
        public float maximumBindTranslationError,maximumBindRotationError,referenceBindTranslationError,referenceBindRotationError,maximumSourceMatrixPositionDifference,maximumSourceMatrixRotationDifference;
        public bool referenceCompared;
    }
    [Serializable] sealed class Report {public string avatar;public List<Bone> bones=new List<Bone>();public List<Skin> skins=new List<Skin>();}
    public static void Run()
    {
        var actor=ActorAnimationAudit.Actor(false);var report=new Report {avatar=AssetDatabase.GetAssetPath(actor.avatar)};
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(report.avatar);
        var references=model.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.ToArray());
        var description=actor.avatar.humanDescription;
        for(int i=0;i<(int)HumanBodyBones.LastBone;i++)
        {
            var actual=actor.GetBoneTransform((HumanBodyBones)i);if(actual==null)continue;
            var human=description.human.FirstOrDefault(h=>h.humanName.Replace(" ","")==((HumanBodyBones)i).ToString());
            if(string.IsNullOrEmpty(human.boneName)||!references.TryGetValue(human.boneName,out var matches)||matches.Length!=1)throw new InvalidOperationException("Reference bone ambiguous: "+actual.name);
            var reference=matches[0];var skeleton=description.skeleton.Single(s=>s.name==human.boneName);
            report.bones.Add(new Bone {human=((HumanBodyBones)i).ToString(),path=AnimationUtility.CalculateTransformPath(actual,actor.transform),referencePath=AnimationUtility.CalculateTransformPath(reference,model.transform),
                actualPosition=actual.localPosition,referencePosition=reference.localPosition,avatarPosition=skeleton.position,actualScale=actual.localScale,referenceScale=reference.localScale,
                referencePositionError=Vector3.Distance(actual.localPosition,reference.localPosition),avatarPositionError=Vector3.Distance(actual.localPosition,skeleton.position),referenceRotationError=Quaternion.Angle(actual.localRotation,reference.localRotation),
                actualSegmentLength=actual.parent!=null?Vector3.Distance(actual.position,actual.parent.position):0,referenceSegmentLength=reference.parent!=null?Vector3.Distance(reference.position,reference.parent.position):0});
        }
        foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if(renderer.sharedMesh==null||!renderer.enabled)continue;
            var skin=new Skin {renderer=renderer.name,bones=renderer.bones.Length,mesh=AssetDatabase.GetAssetPath(renderer.sharedMesh)};var bindings=renderer.sharedMesh.bindposes;
            var matches=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name==renderer.name).ToArray();
            var reference=matches.Length==1?matches[0]:null;
            if(reference!=null){skin.referenceMesh=AssetDatabase.GetAssetPath(reference.sharedMesh);skin.referenceCompared=reference.sharedMesh==renderer.sharedMesh&&reference.bones.Length==renderer.bones.Length;}
            for(int i=0;i<Mathf.Min(bindings.Length,renderer.bones.Length);i++)
            {
                if(renderer.bones[i]==null)continue;
                var matrix=renderer.transform.worldToLocalMatrix*renderer.bones[i].localToWorldMatrix*bindings[i];
                skin.maximumBindTranslationError=Mathf.Max(skin.maximumBindTranslationError,((Vector3)matrix.GetColumn(3)).magnitude);
                skin.maximumBindRotationError=Mathf.Max(skin.maximumBindRotationError,Quaternion.Angle(matrix.rotation,Quaternion.identity));
                if(skin.referenceCompared)
                {
                    if(reference.bones[i]==null||reference.bones[i].name!=renderer.bones[i].name)throw new InvalidOperationException("Skin bone remapping differs: "+renderer.name+" "+i);
                    var baseline=reference.transform.worldToLocalMatrix*reference.bones[i].localToWorldMatrix*bindings[i];
                    skin.referenceBindTranslationError=Mathf.Max(skin.referenceBindTranslationError,((Vector3)baseline.GetColumn(3)).magnitude);
                    skin.referenceBindRotationError=Mathf.Max(skin.referenceBindRotationError,Quaternion.Angle(baseline.rotation,Quaternion.identity));
                    // Renderer transforms cancel out of world-space skinning. Compare the
                    // actual bone/bind deformation in each Animator root's coordinates.
                    var actualDeformation=actor.transform.worldToLocalMatrix*renderer.bones[i].localToWorldMatrix*bindings[i];
                    var referenceDeformation=model.transform.worldToLocalMatrix*reference.bones[i].localToWorldMatrix*bindings[i];
                    var difference=actualDeformation*referenceDeformation.inverse;
                    skin.maximumSourceMatrixPositionDifference=Mathf.Max(skin.maximumSourceMatrixPositionDifference,((Vector3)difference.GetColumn(3)).magnitude);
                    skin.maximumSourceMatrixRotationDifference=Mathf.Max(skin.maximumSourceMatrixRotationDifference,Quaternion.Angle(difference.rotation,Quaternion.identity));
                }
            }
            report.skins.Add(skin);
        }
        Directory.CreateDirectory("Library/LucianCoherence/rig-rest");File.WriteAllText("Library/LucianCoherence/rig-rest/report.json",JsonUtility.ToJson(report,true));
    }
}
