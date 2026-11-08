using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lit.Fractures;
using Lit.Fractures.Editor;
using Opsive.UltimateCharacterController.Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Separate, explicitly staged candidate. Production GUIDs, clips, CharacterData and physics are untouched.</summary>
public static class LucianCadencePreview
{
    public const string Folder="Assets/Characters/AnimationPresentation/LucianCoherencePreview/Cadence01";
    public const string ScenePath=Folder+"/LucianCadence.unity";
    [Serializable] sealed class Measurements {public List<LucianCycleSupportAudit.Measurement> cycles;}
    public static void NormalizeDirectionalPreview()
    {
        string path=Folder+"/LucianCadence.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if(controller==null)throw new InvalidOperationException("Stage cadence candidate first");
        var root=(BlendTree)controller.layers[0].stateMachine.states.Single(s=>s.state.name=="CombatLocomotion").state.motion;
        foreach(var child in root.children)
        {
            if(child.motion is not BlendTree tree)continue;
            if(AssetDatabase.GetAssetPath(tree)!=path || tree.blendType!=BlendTreeType.SimpleDirectional2D)throw new InvalidOperationException("Unexpected or shared directional tree");
            var children=tree.children;bool changed=false;
            for(int i=0;i<children.Length;i++)
            {
                var position=children[i].position;
                if(position.sqrMagnitude<.001f)continue;
                var normalized=position.normalized;
                if((position-normalized).sqrMagnitude<1e-8f)continue;
                children[i].position=normalized;changed=true;
            }
            if(changed) {tree.children=children;EditorUtility.SetDirty(tree);AssetDatabase.SaveAssetIfDirty(tree);}
        }
    }
    public static void Prepare()
    {
        if(File.Exists(ScenePath))return; // Authoring of an existing candidate is never overwritten.
        var measurements=JsonUtility.FromJson<Measurements>(File.ReadAllText("Library/LucianCoherence/cycles/report.json")).cycles.Where(m=>!m.footIK).ToArray();
        if(measurements.Length!=18 || measurements.Any(m=>m.rootSpeedOnAvatar<=0 || Mathf.Abs(m.rootDuration-m.length)>.001f))
            throw new InvalidOperationException("All eighteen resolved, duration-matched root references must be measured first");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var sourceAnimator=ActorAnimationAudit.Actor(false);
        var sourceOverride=sourceAnimator.runtimeAnimatorController as AnimatorOverrideController;
        if(sourceOverride==null || sourceOverride.runtimeAnimatorController is not AnimatorController originalGraph)throw new InvalidOperationException("Lucian graph contract changed");
        string controllerPath=Folder+"/LucianCadence.controller";
        if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(originalGraph),controllerPath))throw new InvalidOperationException("Cannot create independent controller candidate");
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        var originalOverrides=new List<KeyValuePair<AnimationClip,AnimationClip>>();sourceOverride.GetOverrides(originalOverrides);
        var overrides=new AnimatorOverrideController(controller){name="LucianCadence"};
        var copiedPairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();overrides.GetOverrides(copiedPairs);
        for(int i=0;i<copiedPairs.Count;i++)
        {
            var key=copiedPairs[i].Key;
            var matches=originalOverrides.Where(p=>p.Key==key || AssetDatabase.GetAssetPath(p.Key)==AssetDatabase.GetAssetPath(originalGraph) && p.Key.name==key.name).ToArray();
            if(matches.Length!=1)throw new InvalidOperationException("Ambiguous override key: "+key.name);
            copiedPairs[i]=new KeyValuePair<AnimationClip,AnimationClip>(key,matches[0].Value);
        }
        overrides.ApplyOverrides(copiedPairs);AssetDatabase.CreateAsset(overrides,Folder+"/LucianCadence.overrideController");
        var profile=ScriptableObject.CreateInstance<LocomotionPresentationProfile>();profile.name="LucianCadenceMeasurements";profile.measuredAvatar=sourceAnimator.avatar;
        profile.chooseGaitFromObtainedSpeed=true;profile.validated=true;profile.supportValidated=false;
        var free=(BlendTree)controller.layers[0].stateMachine.states.Single(s=>s.state.name=="Locomotion").state.motion;
        var combat=(BlendTree)controller.layers[0].stateMachine.states.Single(s=>s.state.name=="CombatLocomotion").state.motion;
        if(free.blendParameter!="LitSpeed" || combat.blendParameter!="LocomotionTier")throw new InvalidOperationException("Locomotion tree contract changed");
        var freeCycles=free.children.Where(c=>c.motion is AnimationClip && c.threshold>0).OrderBy(c=>c.threshold).ToArray();
        var combatCycles=combat.children.Where(c=>c.motion is BlendTree).OrderBy(c=>c.threshold).ToArray();
        if(freeCycles.Length!=2 || combatCycles.Length!=2)throw new InvalidOperationException("Walk/run tree contract unresolved");
        profile.explorationBlendThresholds=new Vector2(freeCycles[0].threshold,freeCycles[1].threshold);
        profile.blendThresholds=new Vector2(combatCycles[0].threshold,combatCycles[1].threshold);
        profile.explorationWalk=Cycle((AnimationClip)freeCycles[0].motion,Vector2.up,freeCycles[0].timeScale);
        profile.explorationRun=Cycle((AnimationClip)freeCycles[1].motion,Vector2.up,freeCycles[1].timeScale);
        profile.walk=Cycles((BlendTree)combatCycles[0].motion);profile.run=Cycles((BlendTree)combatCycles[1].motion);
        if(profile.walk.Length!=8 || profile.run.Length!=8 || !profile.IsUsable(sourceAnimator.avatar))throw new InvalidOperationException("Incomplete candidate measurements");
        // Concrete intent-independence checks before staging the prefab.
        float nativeRun=profile.explorationRun.nativeSpeed;
        profile.Evaluate(nativeRun,false,Vector2.up,out var blend,out var rate,true);
        if(Mathf.Abs(blend-profile.explorationBlendThresholds.y)>.001f || Mathf.Abs(rate-1)>.001f)throw new InvalidOperationException("Native run must select a full run cycle even without sprint input");
        profile.Evaluate(nativeRun*1.65f,true,Vector2.up,out blend,out rate,true);
        if(Mathf.Abs(rate-1.65f)>.001f)throw new InvalidOperationException("Obtained distance/cadence ratio mismatch");
        profile.Evaluate(0,true,Vector2.up,out blend,out rate,true);
        if(blend!=0 || rate!=1)throw new InvalidOperationException("Requested sprint must not animate movement at a standstill");
        AssetDatabase.CreateAsset(profile,Folder+"/LocomotionMeasurements.asset");
        LocomotionPresentationCalibration.BindCadence(controller,"Locomotion","CombatLocomotion");AssetDatabase.SaveAssetIfDirty(controller);
        var originalData=AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/1_Squad/Lucian/Lucian.asset");
        var data=Object.Instantiate(originalData);data.name="LucianCadence";
        data.playerSettings.locomotion.preserveWholeBodyLocomotion=true;data.playerSettings.locomotion.presentationProfile=profile;
        AssetDatabase.CreateAsset(data,Folder+"/LucianCadence.asset");
        var prefab=PrefabUtility.LoadPrefabContents(ActorAnimationAudit.PlayerPrefab);
        try
        {
            foreach(var component in prefab.GetComponentsInChildren<Component>(true))
            {
                if(component==null)continue;
                var serialized=new SerializedObject(component);var p=serialized.GetIterator();bool changed=false;
                while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference && p.objectReferenceValue==originalData) {p.objectReferenceValue=data;changed=true;}
                if(changed)serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            prefab.GetComponent<CharacterAnimationController>().Animator.runtimeAnimatorController=overrides;
            var monitor=prefab.GetComponentInChildren<AnimatorMonitor>(true);
            if(monitor==null)throw new InvalidOperationException("UCC monitor unresolved");
            if(monitor is not LitPresentationAnimatorMonitor)
            {
                var serialized=new SerializedObject(monitor);serialized.FindProperty("m_Script").objectReferenceValue=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Characters/AnimationPresentation/LitPresentationAnimatorMonitor.cs");serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(prefab,Folder+"/LucianCadence.prefab");
        }
        finally {PrefabUtility.UnloadPrefabContents(prefab);}
        var preview=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/LucianCadence.prefab");
        if(preview.GetComponentInChildren<LitPresentationAnimatorMonitor>(true)==null)throw new InvalidOperationException("Candidate monitor conversion failed");
        if(!AssetDatabase.CopyAsset(FractureGrayboxAuthoring.ScenePath,ScenePath))throw new InvalidOperationException("Cannot stage independent scene");
        var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        try
        {
            var lab=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FractureGraybox>()).Single();lab.lucianPrefab=preview;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        finally {if(previous.IsValid())SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        LocomotionPresentationProfile.Cycle[] Cycles(BlendTree tree)=>tree.children.Where(c=>c.motion is AnimationClip && c.position.sqrMagnitude>.001f).Select(c=>Cycle((AnimationClip)c.motion,c.position,c.timeScale)).ToArray();
        LocomotionPresentationProfile.Cycle Cycle(AnimationClip clip,Vector2 direction,float timeScale)
        {
            var effective=originalOverrides.SingleOrDefault(p=>p.Key==clip).Value ?? clip;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(effective,out string guid,out long id);
            var measurement=measurements.Single(m=>m.guid==guid && m.fileId==id);
            return new LocomotionPresentationProfile.Cycle {clip=effective,direction=direction,nativeSpeed=measurement.rootSpeedOnAvatar*timeScale};
        }
    }
}
