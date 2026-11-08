using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Opsive.UltimateCharacterController.Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Explicit two-step preparation and binding of the eighteen measured Lucian cycles. Candidate assets only.</summary>
[InitializeOnLoad]
public static class LucianNativeCycleBatch
{
    const string Key="Lit.Lucian.NativeCycleBatch";
    static string Folder=>SessionState.GetString(Key+".Folder","Library/LucianCoherence/native-cycles");
    [Serializable] sealed class Measurements {public List<LucianCycleSupportAudit.Measurement> cycles;}
    [Serializable] sealed class Entry
    {
        public string source,candidate,status="Pending";
        public long fileId;
        public string previous,previousHash;
        public Vector3 direction;
        public LucianNativeSupportPilot.Report validation;
    }
    [Serializable] sealed class Manifest
    {
        public string status="Preparing",scope="Candidate only; offline pose gates are not visual/runtime acceptance.";
        public bool stride;
        public List<Entry> entries=new List<Entry>();
    }
    static LucianNativeCycleBatch()=>EditorApplication.update+=Tick;
    public static void Start(bool stride=true)
    {
        if(SessionState.GetBool(Key,false))throw new InvalidOperationException("Native cycle preparation already running");
        var measurements=JsonUtility.FromJson<Measurements>(File.ReadAllText("Library/LucianCoherence/cycles/report.json"));
        var sources=measurements.cycles.Where(c=>!c.footIK).ToArray();
        if(sources.Length!=18)throw new InvalidOperationException("Expected eighteen measured source cycles");
        var manifest=new Manifest {stride=stride};
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder+"/LucianCadence.overrideController");
        var overrides=new List<KeyValuePair<AnimationClip,AnimationClip>>();controller.GetOverrides(overrides);
        // Resolve every GUID/sub-asset before creating any new candidate.
        foreach(var item in sources)
        {
            var source=ActorAnimationAudit.Source(item.clip,item.fileId);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long _);
            if(guid!=item.guid)throw new InvalidOperationException("Source changed since audit: "+item.clip);
            string name=Path.GetFileNameWithoutExtension(item.clip).Replace("Twinblades_","Lucian_").Replace("_Inplace","_SupportIK");
            if(item.clip.EndsWith("Twinblades_Common_Run_Loop_Inplace.FBX"))name="Lucian_Common_Run_SupportIK";
            string target="Assets/Characters/1_Squad/Lucian/Animation/CoherencePilot/"+name+".anim";
            if(stride)target=target.Replace("_SupportIK.anim","_Stride03.anim");
            if(stride&&item.clip.EndsWith("Twinblades_Common_Run_Loop_Inplace.FBX"))target="Assets/Characters/1_Squad/Lucian/Animation/CoherencePilot/Lucian_Common_Run_Stride02.anim";
            var consumers=overrides.Where(p=>p.Key==source).ToArray();
            if(consumers.Length!=1)throw new InvalidOperationException("Source override consumer unresolved: "+item.clip);
            string previous=AssetDatabase.GetAssetPath(consumers[0].Value??consumers[0].Key);
            if(previous!=item.clip&&!previous.StartsWith("Assets/Characters/1_Squad/Lucian/Animation/CoherencePilot/",StringComparison.Ordinal))throw new InvalidOperationException("Unrecognized authoring preserved: "+previous);
            manifest.entries.Add(new Entry {source=item.clip,fileId=item.fileId,candidate=target,previous=previous,previousHash=AssetDatabase.GetAssetDependencyHash(previous).ToString(),direction=LucianCycleSupportAudit.Direction(source.name)});
        }
        if(manifest.entries.Select(e=>e.candidate).Distinct().Count()!=18)throw new InvalidOperationException("Ambiguous candidate paths");
        SessionState.SetString(Key+".Folder",stride?"Library/LucianCoherence/stride-cycles":"Library/LucianCoherence/native-cycles");
        Directory.CreateDirectory(Folder);Save(manifest);SessionState.SetBool(Key,true);
    }
    static void Save(Manifest manifest)=>File.WriteAllText(Folder+"/manifest.json",JsonUtility.ToJson(manifest,true));
    public static void RetryFailed()
    {
        if(SessionState.GetBool(Key,false))throw new InvalidOperationException("Preparation running");
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder+"/manifest.json"));
        foreach(var entry in manifest.entries.Where(e=>e.status!="PASS"))entry.status="Pending";
        manifest.status="Preparing";Save(manifest);SessionState.SetBool(Key,true);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder+"/manifest.json"));
        var entry=manifest.entries.FirstOrDefault(e=>e.status=="Pending");
        if(entry==null)
        {
            manifest.status=manifest.entries.All(e=>e.status=="PASS")?"PREVALIDATED":"FAILED_PREVALIDATION";
            Save(manifest);SessionState.SetBool(Key,false);return;
        }
        try
        {
            var source=ActorAnimationAudit.Source(entry.source,entry.fileId);
            entry.validation=LucianNativeSupportPilot.Prepare(source,entry.candidate,Folder+"/"+Path.GetFileNameWithoutExtension(entry.candidate),manifest.stride,manifest.stride,entry.direction);
            entry.status="PASS";
        }
        catch(Exception e){entry.status=e.Message;}
        Save(manifest);
    }
    public static void Apply()
    {
        if(SessionState.GetBool(Key,false))throw new InvalidOperationException("Preparation still running");
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder+"/manifest.json"));
        if(manifest.status!="PREVALIDATED"||manifest.entries.Count!=18||manifest.entries.Any(e=>e.status!="PASS"))throw new InvalidOperationException("Complete cycle prevalidation required");
        var actor=ActorAnimationAudit.Actor(false);string avatarHash=AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(actor.avatar)).ToString();
        var replacements=new Dictionary<AnimationClip,AnimationClip>();
        foreach(var entry in manifest.entries)
        {
            var r=entry.validation;
            if(r==null||r.sourceHash!=AssetDatabase.GetAssetDependencyHash(entry.source).ToString()||r.avatarHash!=avatarHash||r.candidateHash!=AssetDatabase.GetAssetDependencyHash(entry.candidate).ToString())
                throw new InvalidOperationException("Revalidate changed input/candidate: "+entry.source);
            var source=ActorAnimationAudit.Source(entry.source,entry.fileId);var target=AssetDatabase.LoadAssetAtPath<AnimationClip>(entry.candidate);
            if(target==null)throw new InvalidOperationException("Candidate missing: "+entry.candidate);
            replacements.Add(source,target);
        }
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder+"/LucianCadence.overrideController");
        var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();controller.GetOverrides(pairs);
        var covered=new HashSet<AnimationClip>();bool changed=false;
        for(int i=0;i<pairs.Count;i++)
        {
            var effective=pairs[i].Value??pairs[i].Key;
            var source=replacements.Keys.FirstOrDefault(c=>c==effective||replacements[c]==effective||c==pairs[i].Key);
            if(source==null)continue;
            var input=manifest.entries.Single(e=>e.source==AssetDatabase.GetAssetPath(source));
            if(effective!=source&&effective!=replacements[source]&&(AssetDatabase.GetAssetPath(effective)!=input.previous||AssetDatabase.GetAssetDependencyHash(input.previous).ToString()!=input.previousHash))throw new InvalidOperationException("Override authoring changed: "+pairs[i].Key.name);
            covered.Add(source);
            if(effective!=replacements[source]){pairs[i]=new KeyValuePair<AnimationClip,AnimationClip>(pairs[i].Key,replacements[source]);changed=true;}
        }
        if(covered.Count!=18)throw new InvalidOperationException("Not all cycle consumers resolved");
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(LucianCadencePreview.Folder+"/LocomotionMeasurements.asset");
        var cycles=profile.walk.Concat(profile.run).Concat(new[]{profile.explorationWalk,profile.explorationRun}).ToArray();
        if(cycles.Length!=18||cycles.Any(c=>c==null||manifest.entries.Count(e=>AssetDatabase.GetAssetPath(c.clip)==e.source||AssetDatabase.GetAssetPath(c.clip)==e.previous||AssetDatabase.GetAssetPath(c.clip)==e.candidate)!=1))throw new InvalidOperationException("Profile consumers changed");
        if(profile.footSupports!=null)
        {
            NativeFootSupportPreparation.Validate(profile,profile.footSupports,true);
            if(changed||cycles.Any(c=>manifest.entries.Single(e=>e.candidate==AssetDatabase.GetAssetPath(c.clip)).validation.leftContactPhase!=c.leftContactPhase||
                manifest.entries.Single(e=>e.candidate==AssetDatabase.GetAssetPath(c.clip)).validation.rightContactPhase!=c.rightContactPhase))
                throw new InvalidOperationException("Prepare the support-aware contract as a whole before changing qualified cycles");
        }
        var prefab=PrefabUtility.LoadPrefabContents(LucianCadencePreview.Folder+"/LucianCadence.prefab");
        try
        {
            var ik=prefab.GetComponentInChildren<CharacterIK>(true);
            if(ik==null)throw new InvalidOperationException("UCC IK component missing");
            // All input and consumer checks above precede the application stage.
            if(changed){controller.ApplyOverrides(pairs);AssetDatabase.SaveAssetIfDirty(controller);}
            var graph=(AnimatorController)controller.runtimeAnimatorController;
            foreach(var state in LocomotionPresentationCalibration.States(graph.layers[0].stateMachine))
                if((state.name=="Locomotion"||state.name=="CombatLocomotion")&&state.iKOnFeet!=(profile.footSupports==null))
                {state.iKOnFeet=profile.footSupports==null;EditorUtility.SetDirty(state);AssetDatabase.SaveAssetIfDirty(state);}
            bool profileChanged=false;
            foreach(var cycle in cycles)
            {
                string current=AssetDatabase.GetAssetPath(cycle.clip);
                var entry=manifest.entries.Single(e=>current==e.source||current==e.previous||current==e.candidate);
                var target=AssetDatabase.LoadAssetAtPath<AnimationClip>(entry.candidate);
                if(cycle.clip!=target)
                {
                    cycle.clip=target;
                    // Exported effectors and phase offsets belong to the old clip.
                    // Re-export/align explicitly after the complete new binding.
                    cycle.footGoals=null;cycle.cycleOffset=0;
                    if(manifest.stride)cycle.nativeSpeed=entry.validation.contactNativeSpeed;
                    profileChanged=true;
                }
                if(manifest.stride&&(cycle.leftContactPhase!=entry.validation.leftContactPhase||cycle.rightContactPhase!=entry.validation.rightContactPhase))
                {cycle.leftContactPhase=entry.validation.leftContactPhase;cycle.rightContactPhase=entry.validation.rightContactPhase;profileChanged=true;}
            }
            var native=replacements.Values.OrderBy(c=>c.name,StringComparer.Ordinal).ToArray();
            if(!profile.nativeFootIKClips.SequenceEqual(native)){profile.nativeFootIKClips=native;profileChanged=true;}
            if(profileChanged){EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);}
            if(ik is not LitPresentationCharacterIK)
            {
                var serialized=new SerializedObject(ik);serialized.FindProperty("m_Script").objectReferenceValue=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Characters/AnimationPresentation/LitPresentationCharacterIK.cs");serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefab,LucianCadencePreview.Folder+"/LucianCadence.prefab");
            }
        }
        finally{PrefabUtility.UnloadPrefabContents(prefab);}
        if(AssetDatabase.LoadAssetAtPath<GameObject>(LucianCadencePreview.Folder+"/LucianCadence.prefab").GetComponentInChildren<LitPresentationCharacterIK>(true)==null)
            throw new InvalidOperationException("Candidate IK adapter did not serialize");
    }
}
