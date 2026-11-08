using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Explicit preparation/application of the isolated candidate's qualified support data.</summary>
public static class NativeFootSupportPreparation
{
    public const string ProfilePath=LucianCadencePreview.Folder+"/LocomotionMeasurements.asset";
    public const string DataPath=LucianCadencePreview.Folder+"/LucianFootSupports.asset";
    static LocomotionPresentationProfile.Cycle[] Cycles(LocomotionPresentationProfile p)=>p.walk.Concat(p.run).Concat(new[]{p.explorationWalk,p.explorationRun}).ToArray();
    static string Hash(Action<BinaryWriter> write)
    {
        using(var memory=new MemoryStream())
        {
            using(var writer=new BinaryWriter(memory,System.Text.Encoding.UTF8,true))write(writer);
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(memory.ToArray())).Replace("-","").ToLowerInvariant();
        }
    }
    static void Reference(BinaryWriter w,UnityEngine.Object value,bool dependency=false)
    {
        if(value==null)throw new InvalidOperationException("Required source reference missing");
        if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value,out string guid,out long fileId))throw new InvalidOperationException("Unresolved source reference: "+value.name);
        w.Write(guid);w.Write(fileId);
        if(dependency)w.Write(AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(value)).ToString());
    }
    static void Vector(BinaryWriter w,Vector3 v){w.Write(v.x);w.Write(v.y);w.Write(v.z);}
    static void Rotation(BinaryWriter w,Quaternion q){w.Write(q.x);w.Write(q.y);w.Write(q.z);w.Write(q.w);}
    static void RigRotation(BinaryWriter w,Quaternion q)
    {
        q=q.normalized;
        var values=new[]{(long)Math.Round(q.x*1000000d),(long)Math.Round(q.y*1000000d),(long)Math.Round(q.z*1000000d),(long)Math.Round(q.w*1000000d)};
        long sign=1;foreach(long value in values)if(value!=0){sign=value<0?-1:1;break;}
        foreach(long value in values)w.Write(value*sign);
    }
    static void Curves(BinaryWriter w,AnimationCurve[] curves)
    {
        w.Write(curves.Length);
        foreach(var curve in curves)
        {
            w.Write((int)curve.preWrapMode);w.Write((int)curve.postWrapMode);w.Write(curve.length);
            foreach(var k in curve.keys){w.Write(k.time);w.Write(k.value);w.Write(k.inTangent);w.Write(k.outTangent);w.Write(k.inWeight);w.Write(k.outWeight);w.Write((int)k.weightedMode);}
        }
    }
    public static string InputSignature(LocomotionPresentationProfile profile)=>Hash(w=>
    {
        w.Write(1);w.Write(Application.unityVersion);Reference(w,profile.measuredAvatar,true);
        foreach(string path in new[]{"Assets/Characters/AnimationPresentation/NativeFootSupportSet.cs","Assets/Characters/AnimationPresentation/NativeFootGoalCurves.cs"})
            w.Write(Hash(code=>code.Write(File.ReadAllBytes(path))));
        foreach(var c in Cycles(profile))
        {
            Reference(w,c.clip,true);w.Write(c.cycleOffset);
            if(c.footGoals==null||!c.footGoals.Complete)throw new InvalidOperationException("Native goals incomplete: "+c.clip.name);
            Curves(w,c.footGoals.leftPosition);Curves(w,c.footGoals.rightPosition);Curves(w,c.footGoals.leftRotation);Curves(w,c.footGoals.rightRotation);
        }
    });
    public static string RigSignature(Animator actor,bool canonicalRotations=true)=>Hash(w=>
    {
        Reference(w,actor.avatar,true);Vector(w,actor.transform.lossyScale);
        for(int i=0;i<(int)HumanBodyBones.LastBone;i++)
        {
            var bone=actor.GetBoneTransform((HumanBodyBones)i);w.Write(bone!=null);
            if(bone==null)continue;
            w.Write(AnimationUtility.CalculateTransformPath(bone,actor.transform));Vector(w,bone.localPosition);
            if(canonicalRotations)RigRotation(w,bone.localRotation);else Rotation(w,bone.localRotation);
            Vector(w,bone.localScale);
        }
        using(var soles=new ActorSoleSampler(actor,true))foreach(var skin in soles.FootRenderers())
        {
            Reference(w,skin.sharedMesh,true);w.Write(AnimationUtility.CalculateTransformPath(skin.transform,actor.transform));
            foreach(var bone in skin.bones)w.Write(bone!=null?AnimationUtility.CalculateTransformPath(bone,actor.transform):"<null>");
            for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)w.Write(skin.GetBlendShapeWeight(i));
        }
    });
    public static string DataSignature(NativeFootSupportSet data)=>Hash(w=>
    {
        w.Write(1);Reference(w,data.avatar);Reference(w,data.baseController);w.Write(data.controllerSignature);w.Write(data.sourcePrefabPath);w.Write(data.inputSignature);w.Write(data.rigSignature);w.Write(data.steps);
        Rotation(w,data.leftGoalToBone);Rotation(w,data.rightGoalToBone);
        foreach(string s in data.leftPointIds)w.Write(s);foreach(string s in data.rightPointIds)w.Write(s);
        for(int i=0;i<data.meshes.Length;i++){Reference(w,data.meshes[i]);w.Write(data.rendererPaths[i]);}
        foreach(var c in data.cycles)
        {
            Reference(w,c.clip);w.Write(c.cycleOffset);
            foreach(var p in c.left)Vector(w,p);foreach(var p in c.right)Vector(w,p);
            foreach(var h in c.heights){w.Write(h.x);w.Write(h.y);}
        }
    });
    public static void StorePrepared(NativeFootSupportSet prepared,string expectedPreviousControllerSignature=null)
    {
        prepared.inputSignature=InputSignature(AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(ProfilePath));
        prepared.rigSignature=RigSignature(ActorAnimationAudit.Actor(false));
        prepared.sourcePrefabPath=LucianCadencePreview.Folder+"/LucianCadence.prefab";
        prepared.controllerSignature=AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(prepared.baseController)).ToString();
        prepared.dataSignature=DataSignature(prepared);
        var existing=AssetDatabase.LoadAssetAtPath<NativeFootSupportSet>(DataPath);
        if(existing!=null)
        {
            if(existing.dataSignature==prepared.dataSignature&&DataSignature(existing)==prepared.dataSignature)return;
            bool sameSources=existing.inputSignature==prepared.inputSignature&&existing.rigSignature==prepared.rigSignature&&DataSignature(existing)==existing.dataSignature;
            bool authorizedDomainChange=sameSources&&expectedPreviousControllerSignature!=null&&existing.controllerSignature==expectedPreviousControllerSignature&&
                existing.controllerSignature!=prepared.controllerSignature&&existing.runtimeValidationSignature==existing.dataSignature;
            if(expectedPreviousControllerSignature!=null&&!authorizedDomainChange)throw new InvalidOperationException("Controller requalification does not match the previously verified dataset.");
            if(sameSources&&!authorizedDomainChange)
                throw new InvalidOperationException("Existing prepared data differs with unchanged inputs; review rather than overwrite");
            EditorUtility.CopySerialized(prepared,existing);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing);
        }
        else {prepared.hideFlags=HideFlags.None;var stored=UnityEngine.Object.Instantiate(prepared);stored.name=prepared.name;AssetDatabase.CreateAsset(stored,DataPath);}
    }
    public static void Validate(LocomotionPresentationProfile profile,NativeFootSupportSet data,bool applied)
    {
        if(data==null||data.cycles==null||data.cycles.Length!=18)throw new InvalidOperationException("Complete prepared support data required");
        if(data.inputSignature!=InputSignature(profile)||data.rigSignature!=RigSignature(ActorAnimationAudit.Actor(false))||data.dataSignature!=DataSignature(data))
            throw new InvalidOperationException("Support inputs/rig/data changed: revalidate before applying");
        if(data.controllerSignature!=AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(data.baseController)).ToString())throw new InvalidOperationException("Candidate controller changed since preparation/application");
        var graph=data.baseController as AnimatorController;
        if(graph==null)throw new InvalidOperationException("Expected candidate AnimatorController");
        var states=LocomotionPresentationCalibration.States(graph.layers[0].stateMachine).Where(s=>s.name=="Locomotion"||s.name=="CombatLocomotion").ToArray();
        if(states.Length!=2||applied&&states.Any(s=>s.iKOnFeet))throw new InvalidOperationException("Candidate must use muscle pose with one scripted IK pass");
        if(profile.matchSimpleDirectionalBlend)
        {
            var visited=new HashSet<Motion>();
            void Timing(Motion motion)
            {
                if(motion==null||!visited.Add(motion))return;
                if(motion is BlendTree tree)foreach(var child in tree.children)
                {
                    if(float.IsNaN(child.timeScale)||float.IsInfinity(child.timeScale)||Mathf.Abs(child.timeScale-1)>.00001f)throw new InvalidOperationException("Measured cycle timing requires unit motion time scales: "+tree.name);
                    Timing(child.motion);
                }
            }
            foreach(var state in states)
            {
                if(Mathf.Abs(state.speed-1)>.00001f||!state.speedParameterActive||state.speedParameter!=LocomotionPresentationProfile.PlaybackParameter)throw new InvalidOperationException("Measured cycle timing requires the qualified locomotion playback parameter: "+state.name);
                Timing(state.motion);
            }
        }
        if(applied&&profile.footSupports!=data)throw new InvalidOperationException("Prepared dataset not bound to profile");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(data.sourcePrefabPath);
        var contract=prefab!=null?prefab.GetComponent<CharacterAnimationController>():null;
        var actor=contract!=null?contract.Animator:prefab!=null?prefab.GetComponentInChildren<Animator>(true):null;
        if(actor==null)throw new InvalidOperationException("Candidate presentation Animator missing");
        if(!data.Matches(actor))throw new InvalidOperationException("Candidate presentation avatar/mesh/controller mismatch: "+actor.name+" avatar="+AssetDatabase.GetAssetPath(actor.avatar)+" controller="+AssetDatabase.GetAssetPath(actor.runtimeAnimatorController));
        string actualRig=RigSignature(actor);
        if(actualRig!=data.rigSignature)throw new InvalidOperationException("Candidate rig signature differs: "+actualRig+" expected "+data.rigSignature);
    }
    public static void ApplyCandidate()
    {
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(ProfilePath);var data=AssetDatabase.LoadAssetAtPath<NativeFootSupportSet>(DataPath);
        Validate(profile,data,false);
        if(data.runtimeValidationSignature!=data.dataSignature)throw new InvalidOperationException("Runtime implementation must pass complete verification before binding");
        var graph=(AnimatorController)data.baseController;var states=LocomotionPresentationCalibration.States(graph.layers[0].stateMachine).Where(s=>s.name=="Locomotion"||s.name=="CombatLocomotion").ToArray();
        if(profile.footSupports==data&&states.All(s=>!s.iKOnFeet)){Validate(profile,data,true);return;}
        var previous=profile.footSupports;bool previousValidated=profile.supportValidated;var flags=states.Select(s=>s.iKOnFeet).ToArray();
        string previousController=data.controllerSignature,previousSignature=data.dataSignature,previousValidation=data.runtimeValidationSignature;
        try
        {
            foreach(var state in states){state.iKOnFeet=false;EditorUtility.SetDirty(state);}
            EditorUtility.SetDirty(graph);AssetDatabase.SaveAssetIfDirty(graph);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph),ImportAssetOptions.ForceUpdate);
            profile.footSupports=data;profile.supportValidated=false;EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
            data.controllerSignature=AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(graph)).ToString();data.dataSignature=DataSignature(data);data.runtimeValidationSignature=data.dataSignature;EditorUtility.SetDirty(data);AssetDatabase.SaveAssetIfDirty(data);
            Validate(profile,data,true);
        }
        catch
        {
            var restored=LocomotionPresentationCalibration.States(((AnimatorController)data.baseController).layers[0].stateMachine).Where(s=>s.name=="Locomotion"||s.name=="CombatLocomotion").ToArray();
            for(int i=0;i<restored.Length;i++){restored[i].iKOnFeet=flags[i];EditorUtility.SetDirty(restored[i]);}
            EditorUtility.SetDirty(data.baseController);AssetDatabase.SaveAssetIfDirty(data.baseController);
            profile.footSupports=previous;profile.supportValidated=previousValidated;EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
            data.controllerSignature=previousController;data.dataSignature=previousSignature;data.runtimeValidationSignature=previousValidation;EditorUtility.SetDirty(data);AssetDatabase.SaveAssetIfDirty(data);throw;
        }
    }
    public static void ApplyCandidateChecked()
    {
        ApplyCandidate();
        var data=AssetDatabase.LoadAssetAtPath<NativeFootSupportSet>(DataPath);
        var paths=new[]{ProfilePath,DataPath,AssetDatabase.GetAssetPath(data.baseController)};
        var hashes=paths.Select(p=>Hash(w=>w.Write(File.ReadAllBytes(p)))).ToArray();var timestamps=paths.Select(File.GetLastWriteTimeUtc).ToArray();
        ApplyCandidate();
        for(int i=0;i<paths.Length;i++)if(hashes[i]!=Hash(w=>w.Write(File.ReadAllBytes(paths[i])))||timestamps[i]!=File.GetLastWriteTimeUtc(paths[i]))throw new InvalidOperationException("Second application wrote an asset: "+paths[i]);
        Directory.CreateDirectory("Library/LucianCoherence");
        File.WriteAllText("Library/LucianCoherence/support-application.txt","APPLIED_AND_IDEMPOTENT\n"+data.dataSignature+"\n"+string.Join("\n",paths));
    }
    public static void DiagnoseCandidateRig()
    {
        var data=AssetDatabase.LoadAssetAtPath<NativeFootSupportSet>(DataPath);var source=ActorAnimationAudit.Actor(false);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(data.sourcePrefabPath);var target=prefab.GetComponent<CharacterAnimationController>().Animator;
        var text=new System.Text.StringBuilder();text.AppendLine("Source="+source.name+" target="+target.name+" matches="+data.Matches(target));
        text.AppendLine("Scale "+source.transform.lossyScale.ToString("R")+" / "+target.transform.lossyScale.ToString("R"));
        for(int i=0;i<(int)HumanBodyBones.LastBone;i++)
        {
            var a=source.GetBoneTransform((HumanBodyBones)i);var b=target.GetBoneTransform((HumanBodyBones)i);
            if(a==null||b==null){if(a!=b)text.AppendLine(((HumanBodyBones)i)+" missing");continue;}
            string pa=AnimationUtility.CalculateTransformPath(a,source.transform),pb=AnimationUtility.CalculateTransformPath(b,target.transform);
            var qa=new Vector4(a.localRotation.x,a.localRotation.y,a.localRotation.z,a.localRotation.w);var qb=new Vector4(b.localRotation.x,b.localRotation.y,b.localRotation.z,b.localRotation.w);
            float dp=Vector3.Distance(a.localPosition,b.localPosition),dq=Mathf.Min(Vector4.Distance(qa,qb),Vector4.Distance(qa,-qb)),ds=Vector3.Distance(a.localScale,b.localScale);
            if(dp>0||dq>0||ds>0||pa!=pb)text.AppendLine(((HumanBodyBones)i)+" position="+dp.ToString("R")+" quat="+dq.ToString("R")+" scale="+ds.ToString("R")+" paths="+pa+" / "+pb);
        }
        using(var sa=new ActorSoleSampler(source,true))using(var sb=new ActorSoleSampler(target,true))
        {
            var a=sa.FootRenderers();var b=sb.FootRenderers();text.AppendLine("Foot renderers "+a.Length+" / "+b.Length);
            for(int i=0;i<Mathf.Min(a.Length,b.Length);i++)
            {
                text.AppendLine(a[i].name+" / "+b[i].name+" mesh="+(a[i].sharedMesh==b[i].sharedMesh)+" bones="+a[i].bones.Length+"/"+b[i].bones.Length);
                for(int j=0;j<Mathf.Min(a[i].bones.Length,b[i].bones.Length);j++)
                {
                    string pa=a[i].bones[j]!=null?AnimationUtility.CalculateTransformPath(a[i].bones[j],source.transform):"<null>",pb=b[i].bones[j]!=null?AnimationUtility.CalculateTransformPath(b[i].bones[j],target.transform):"<null>";
                    if(pa!=pb)text.AppendLine("Bone binding "+j+" "+pa+" / "+pb);
                }
                for(int j=0;j<a[i].sharedMesh.blendShapeCount;j++)if(a[i].GetBlendShapeWeight(j)!=b[i].GetBlendShapeWeight(j))text.AppendLine("Blend shape "+j+" "+a[i].GetBlendShapeWeight(j)+" / "+b[i].GetBlendShapeWeight(j));
            }
        }
        File.WriteAllText("Library/LucianCoherence/support-rig-comparison.txt",text.ToString());
    }
    public static void RequalifyPreparedRigAndVerify()
    {
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(ProfilePath);var data=AssetDatabase.LoadAssetAtPath<NativeFootSupportSet>(DataPath);
        if(profile.footSupports!=null)throw new InvalidOperationException("Only unbound prepared data may be requalified");
        var source=ActorAnimationAudit.Actor(false);
        if(data.inputSignature!=InputSignature(profile)||data.dataSignature!=DataSignature(data)||data.rigSignature!=RigSignature(source,false))throw new InvalidOperationException("Original prepared inputs no longer match");
        var actor=AssetDatabase.LoadAssetAtPath<GameObject>(data.sourcePrefabPath).GetComponent<CharacterAnimationController>().Animator;
        string signature=RigSignature(source);
        if(!data.Matches(actor)||RigSignature(actor)!=signature)throw new InvalidOperationException("Candidate rig differs beyond quaternion representation precision");
        data.rigSignature=signature;data.runtimeValidationSignature=null;data.dataSignature=DataSignature(data);EditorUtility.SetDirty(data);AssetDatabase.SaveAssetIfDirty(data);
        LucianContactBlendAudit.VerifyPreparedSupport();
    }
}
