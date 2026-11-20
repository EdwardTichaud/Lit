using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Explicit Lucian-only migration. Preflight resolves and samples every new source before any asset is changed.</summary>
[InitializeOnLoad]
public static class SimpleLucianAnimationSetup
{
    public const string Folder = "Assets/Animations/Autres/Locomotion/Lucian/SimpleWholeBody";
    public const string ControllerPath = "Assets/Characters/3_Enemy/Iluvilirae/Lucian_ImpactLab.controller";
    public const string PrefabPath = "Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab";
    public const string DataPath = "Assets/Characters/1_Squad/Lucian/Lucian.asset";
    public const string Report = "Library/SimpleLucianAnimation";
    public static readonly string[] ControllerPaths = {ControllerPath,
        "Assets/Characters/AnimationPresentation/LucianCoherencePreview/Cadence01/LucianCadence.controller"};
    public static readonly string[] PrefabPaths = {PrefabPath,
        "Assets/Characters/3_Enemy/Iluvilirae/Lucian_ImpactLab.prefab",
        "Assets/Characters/AnimationPresentation/LucianCoherencePreview/Cadence01/LucianCadence.prefab"};
    [Serializable] public class Entry
    {
        public string id, source, guid, target, targetGuid;
        public long sourceId;
        public float nativeSpeed, leftPhase, rootDistance, rootYaw;
        public string status;
        [NonSerialized] public AnimationClip clip;
        [NonSerialized] public Vector2 direction;
    }
    [Serializable] public class Manifest { public List<Entry> entries = new(); public string status; }
    static SimpleLucianAnimationSetup() => EditorApplication.update += Poll;
    private static void Poll()
    {
        string request = Report + "/migration.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || AssetDatabase.IsAssetImportWorkerProcess()) return;
        string mode = File.ReadAllText(request).Trim(); File.Delete(request);
        File.WriteAllText(Report + "/error.txt", "");
        try
        {
            if (mode == "audit") Audit(); else if (mode == "apply") Install(); else if (mode == "validate") Validate(); else if (mode == "cleanup") RetireUnusedDialogueCopies(); else if(mode == "manual-speed") RemoveAutomaticLocomotionCadence();
            else throw new InvalidOperationException("Unknown simple animation request: " + mode);
            File.WriteAllText(Report + "/result.txt", mode + " PASS " + DateTime.UtcNow.ToString("O"));
        }
        catch (Exception e) { File.WriteAllText(Report + "/error.txt", e.ToString()); Debug.LogException(e); }
    }
    private static Entry Source(string id, string path, Vector2 direction, bool cycle)
    {
        var clip = ActorAnimationAudit.Source(path);
        if (!clip.isHumanMotion || clip.length <= 0 || AnimationUtility.GetAnimationEvents(clip).Length != 0)
            throw new InvalidOperationException("Invalid Humanoid presentation source or unexpected events: " + path);
        var actor = ActorAnimationAudit.Actor(false);
        var entry = new Entry {id=id, source=path, clip=clip, direction=direction,
            target=Folder + "/" + id + ".anim", status="Prevalidated on Lucian; rendered review pending."};
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out entry.guid, out entry.sourceId);
        using (var sampler = new PlayerInPlaceSampling(actor))
        { var motion=sampler.Sample(clip); entry.rootDistance=motion.MaxDisplacement; entry.rootYaw=motion.MaxYaw; }
        if (cycle)
        { var measured=LocomotionPresentationCalibration.Measure(actor,clip,direction); entry.nativeSpeed=measured.nativeSpeed; entry.leftPhase=measured.leftPhase; }
        return entry;
    }
    private static Manifest Preflight()
    {
        Directory.CreateDirectory(Report);
        var data=AssetDatabase.LoadAssetAtPath<CharacterData>(DataPath);
        if(data==null || data.playerSettings?.dialoguePresentation==null) throw new InvalidOperationException("Missing Lucian authoring data.");
        foreach(var path in ControllerPaths) if(AssetDatabase.LoadAssetAtPath<AnimatorController>(path)==null) throw new InvalidOperationException("Missing controller: "+path);
        var m=new Manifest();
        const string mobility="Assets/Animations/Autres/Locomotion/Mobility_Starter/IPC/";
        m.entries.Add(Source("Idle",mobility+"MOB1_Stand_Relaxed_Idle_v2_IPC.fbx",Vector2.up,false));
        m.entries.Add(Source("Walk",mobility+"MOB1_Walk_F_Loop_IPC.fbx",Vector2.up,true));
        m.entries.Add(Source("Run",mobility+"MOB1_Jog_F_Loop_IPC.fbx",Vector2.up,true));
        m.entries.Add(Source("Walk_Start",mobility+"MOB1_Stand_Relaxed_To_Walk_F_IPC.fbx",Vector2.up,false));
        m.entries.Add(Source("Run_Start",mobility+"MOB1_Stand_Relaxed_To_Jog_F_IPC.fbx",Vector2.up,false));
        m.entries.Add(Source("Walk_Stop",mobility+"MOB1_Walk_F_To_Stand_Relaxed_RU_IPC.fbx",Vector2.up,false));
        m.entries.Add(Source("Turn_L90",mobility+"MOB1_Stand_Relaxed_L_90_IPC.fbx",Vector2.up,false));
        m.entries.Add(Source("Turn_R90",mobility+"MOB1_Stand_Relaxed_R_90_IPC.fbx",Vector2.up,false));
        string[] directions={"front","frontR45","right","backR45","back","backL45","left","frontL45"};
        for(int i=0;i<8;i++) foreach(string gait in new[]{"walk","run"})
        {
            float a=i*Mathf.PI/4;
            m.entries.Add(Source("Combat_"+gait+"_"+directions[i],
                "Assets/Animations/Autres/Locomotion/Dynamic_Archer_Set/Humanoid/inplace/move_"+gait+"_"+directions[i]+"_inplace.fbx",
                new Vector2(Mathf.Sin(a),Mathf.Cos(a)),true));
        }
        foreach(var g in data.playerSettings.dialoguePresentation.gestures.Where(g=>g.validated && g.standing && g.role==DialogueRole.Speaking))
        {
            if(g.source==null || !g.source.isHumanMotion || AnimationUtility.GetAnimationEvents(g.source).Length!=0)
                throw new InvalidOperationException("Unresolved full-body dialogue source: "+g.id);
        }
        foreach(var prefix in new[]{"", "Combat_"})
        {
            float walk=m.entries.First(e=>e.id==(prefix==""?"Walk":"Combat_walk_front")).nativeSpeed;
            float run=m.entries.First(e=>e.id==(prefix==""?"Run":"Combat_run_front")).nativeSpeed;
            if(run<=walk)throw new InvalidOperationException("Measured run must exceed walk before migration.");
        }
        m.status="Preflight complete; no assets modified.";
        return m;
    }
    [MenuItem("Lit/Animation/Simple Lucian/1 Audit Without Changes")]
    public static void Audit() => File.WriteAllText(Report+"/manifest.json",JsonUtility.ToJson(Preflight(),true));
    public static bool IsInstalled => AssetDatabase.LoadAssetAtPath<CharacterData>(DataPath)?.playerSettings?.locomotion?.presentationProfile!=null;
    [MenuItem("Lit/Animation/Simple Lucian/2 Apply Migration")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var manifest=Preflight(); // complete preflight, including every directional clip
        var data=AssetDatabase.LoadAssetAtPath<CharacterData>(DataPath);
        var avatar=ActorAnimationAudit.Actor(false);
        Backup(); EnsureFolder(Folder);
        foreach(var e in manifest.entries)
        {
            File.WriteAllText(Report+"/progress.txt","Preparing "+e.id);
            if(AssetDatabase.LoadAssetAtPath<AnimationClip>(e.target)!=null)continue;
            var copy=Object.Instantiate(e.clip);copy.name="Lucian_"+e.id;
            using(var sampler=new PlayerInPlaceSampling(avatar)) PlayerInPlaceSampling.Neutralize(copy,sampler.Sample(e.clip));
            foreach(var b in AnimationUtility.GetCurveBindings(copy))
                if(b.type==typeof(SkinnedMeshRenderer) || DialoguePresentationSetup.IsFace(b.propertyName)) AnimationUtility.SetEditorCurve(copy,b,null);
            AssetDatabase.CreateAsset(copy,e.target);
        }
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(Folder+"/Lucian_WholeBody.asset");
        if(profile==null)
        {
            profile=ScriptableObject.CreateInstance<LocomotionPresentationProfile>();
            profile.validated=true;profile.measuredAvatar=avatar.avatar;
            profile.stopSpeed=.05f;
            profile.explorationWalk=Cycle(manifest.entries.Single(e=>e.id=="Walk"));
            profile.explorationRun=Cycle(manifest.entries.Single(e=>e.id=="Run"));
            profile.explorationRun.cycleOffset=profile.explorationRun.leftContactPhase-profile.explorationWalk.leftContactPhase;
            profile.walk=manifest.entries.Where(e=>e.id.StartsWith("Combat_walk_")).Select(Cycle).ToArray();
            profile.run=manifest.entries.Where(e=>e.id.StartsWith("Combat_run_")).Select(Cycle).ToArray();
            float reference=profile.walk[0].leftContactPhase;
            foreach(var c in profile.walk.Concat(profile.run)) c.cycleOffset=c.leftContactPhase-reference;
            AssetDatabase.CreateAsset(profile,Folder+"/Lucian_WholeBody.asset");
        }
        var dialogue=data.playerSettings.dialoguePresentation;
        PrepareDialogue(dialogue,avatar,manifest.entries.Single(e=>e.id=="Idle"));
        foreach(string path in ControllerPaths) PrepareController(AssetDatabase.LoadAssetAtPath<AnimatorController>(path),manifest,profile,dialogue);
        foreach(string dataPath in new[]{DataPath,
            "Assets/Characters/3_Enemy/Iluvilirae/Lucian_ImpactLab.asset",
            "Assets/Characters/AnimationPresentation/LucianCoherencePreview/Cadence01/LucianCadence.asset"})
        {
            var target=AssetDatabase.LoadAssetAtPath<CharacterData>(dataPath);
            if(target.playerSettings.locomotion.presentationProfile==profile &&
                target.playerSettings.dialoguePresentation==dialogue)continue;
            target.playerSettings.locomotion.presentationProfile=profile;
            target.playerSettings.dialoguePresentation=dialogue;
            EditorUtility.SetDirty(target);AssetDatabase.SaveAssetIfDirty(target);
        }
        foreach(string path in PrefabPaths)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed=false;
                var monitor=root.GetComponentInChildren<Opsive.UltimateCharacterController.Character.AnimatorMonitor>(true);
                if(monitor!=null && !(monitor is LitPresentationAnimatorMonitor))
                {
                    var so=new SerializedObject(monitor);
                    var script=AssetDatabase.FindAssets("LitPresentationAnimatorMonitor t:MonoScript").Select(AssetDatabase.GUIDToAssetPath)
                        .Select(AssetDatabase.LoadAssetAtPath<MonoScript>).Single(s=>s.GetClass()==typeof(LitPresentationAnimatorMonitor));
                    so.FindProperty("m_Script").objectReferenceValue=script;so.ApplyModifiedPropertiesWithoutUndo();changed=true;
                }
                if(changed)PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        manifest.status="Applied. Runtime pose compensation removed. Full visual acceptance remains a separate check.";
        foreach(var e in manifest.entries)e.targetGuid=AssetDatabase.AssetPathToGUID(e.target);
        if(AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/Sources.asset")==null)
            AssetDatabase.CreateAsset(new TextAsset(JsonUtility.ToJson(manifest,true)){name="Lucian_Locomotion_Sources"},Folder+"/Sources.asset");
        File.WriteAllText(Report+"/manifest.json",JsonUtility.ToJson(manifest,true));
        Validate();
    }
    private static void Backup()
    {
        string folder=Report+"/before";Directory.CreateDirectory(folder);
        foreach(string path in ControllerPaths.Concat(PrefabPaths).Concat(new[]{DataPath,DialoguePresentationSetup.ProfilePath}))
        {string target=folder+"/"+Path.GetFileName(path);if(!File.Exists(target))File.Copy(path,target);}
    }
    private static LocomotionPresentationProfile.Cycle Cycle(Entry e) => new(){clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(e.target),
        direction=e.direction,nativeSpeed=e.nativeSpeed,leftContactPhase=e.leftPhase};
    private static void EnsureFolder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;EnsureFolder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
    private static void PrepareDialogue(DialoguePresentationProfile p,Animator avatar,Entry idle)
    {
        string before=EditorJsonUtility.ToJson(p);
        foreach(var g in p.gestures)
        {
            if(g.id=="Speak_Explain_A" || g.id=="Speak_Explain_B") {g.validated=false;g.body=null;continue;}
            // An unsupported seated gesture keeps the authored sitting idle, never a standing fallback.
            if(g.id=="Speak_Seated") {g.body=null;g.validated=false;g.validation="Complete seated gesture not qualified; keep Sit Idle with voice/expression.";continue;}
            if(g.role!=DialogueRole.Speaking) {g.seated=false;g.standing=true;}
            string path=g.role==DialogueRole.Speaking?DialoguePresentationSetup.Folder+"/"+g.id+".anim":idle.target;
            var body=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(body==null)throw new InvalidOperationException("Missing complete dialogue copy: "+path);
            if(g.role!=DialogueRole.Speaking)
            {g.source=idle.clip;AssetDatabase.TryGetGUIDAndLocalFileIdentifier(g.source,out string _,out g.sourceId);}
            g.body=body;g.validated=true;
            g.validation="Complete original body pose; root trajectory removed. In-game review pending.";
        }
        if(EditorJsonUtility.ToJson(p)!=before){EditorUtility.SetDirty(p);AssetDatabase.SaveAssetIfDirty(p);}
    }
    [MenuItem("Lit/Animation/Simple Lucian/Use Manual Locomotion Speeds")]
    public static void RemoveAutomaticLocomotionCadence()
    {
        foreach(string path in ControllerPaths)
            RemoveAutomaticLocomotionCadence(AssetDatabase.LoadAssetAtPath<AnimatorController>(path));
    }
    private static void RemoveAutomaticLocomotionCadence(AnimatorController controller)
    {
        if(controller==null)throw new InvalidOperationException("Missing Lucian controller.");
        const string retiredParameter="LocomotionPlaybackRate";
        bool changed=false;
        foreach(var state in controller.layers.SelectMany(l=>LocomotionPresentationCalibration.States(l.stateMachine)))
        {
            if(state.speedParameter!=retiredParameter)continue;
            state.speedParameterActive=false;
            state.speedParameter=string.Empty;
            EditorUtility.SetDirty(state);changed=true;
        }
        for(int index=controller.parameters.Length-1;index>=0;index--)
            if(controller.parameters[index].name==retiredParameter)
            {controller.RemoveParameter(index);changed=true;}
        if(changed){EditorUtility.SetDirty(controller);AssetDatabase.SaveAssetIfDirty(controller);}
    }
    private static void PrepareController(AnimatorController c,Manifest m,LocomotionPresentationProfile p,DialoguePresentationProfile d)
    {
        RemoveAutomaticLocomotionCadence(c);
        if(c.layers.Length==2 && c.layers[1].avatarMask!=null && c.layers[0].stateMachine.states.Any(s=>s.state.name=="Dialogue_Speak_Neutral"))
        {
            foreach(var state in LocomotionPresentationCalibration.States(c.layers[0].stateMachine))
                if((state.name=="Turn_L180" || state.name=="Turn_R180" || state.name=="Run_Stop") && state.motion!=null)
                {state.motion=null;state.speed=1;EditorUtility.SetDirty(state);}
            foreach(var gesture in d.gestures.Where(g=>g.validated && g.body!=null))
            {
                var state=c.layers[0].stateMachine.states.Select(s=>s.state).Single(s=>s.name=="Dialogue_"+gesture.id);
                if(state.motion==gesture.body)continue;
                state.motion=gesture.body;EditorUtility.SetDirty(state);EditorUtility.SetDirty(c);
            }
            AssetDatabase.SaveAssetIfDirty(c);return;
        }
        c.layers=c.layers.Where(l=>l.name=="Base Layer" || l.name=="Upper Body Flame").ToArray();
        if(c.layers.Length!=2)throw new InvalidOperationException("Unexpected Lucian controller layers.");
        var layers=c.layers;var main=layers[0].stateMachine;
        var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>(Folder+"/TorchArm.mask");
        if(mask==null)
        {
            mask=new AvatarMask();
            for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm,true);mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers,true);
            AssetDatabase.CreateAsset(mask,Folder+"/TorchArm.mask");
        }
        layers[1].avatarMask=mask;layers[1].defaultWeight=0;c.layers=layers;
        AnimationClip Clip(string id)=>AssetDatabase.LoadAssetAtPath<AnimationClip>(m.entries.Single(e=>e.id==id).target);
        var explore=Tree(c,"WholeBody_Exploration",BlendTreeType.Simple1D,"LitSpeed");
        explore.useAutomaticThresholds=false;
        explore.children=new[]{Child(Clip("Idle"),0),Child(p.explorationWalk.clip,p.explorationBlendThresholds.x,p.explorationWalk.cycleOffset),Child(p.explorationRun.clip,p.explorationBlendThresholds.y,p.explorationRun.cycleOffset)};
        var walk=Directional(c,"WholeBody_CombatWalk",p.walk);
        var run=Directional(c,"WholeBody_CombatRun",p.run);
        var combat=Tree(c,"WholeBody_Combat",BlendTreeType.Simple1D,"LitSpeed");combat.useAutomaticThresholds=false;
        combat.children=new[]{Child(Clip("Idle"),0),Child(walk,p.blendThresholds.x),Child(run,p.blendThresholds.y)};
        foreach(var state in LocomotionPresentationCalibration.States(main))
        {
            string name=state.name;
            if(name=="Locomotion")state.motion=explore;
            else if(name=="CombatLocomotion")state.motion=combat;
            else if(name=="CombatIdle" || name.IndexOf("Idle",StringComparison.OrdinalIgnoreCase)>=0 && name.StartsWith("Twin"))state.motion=Clip("Idle");
            else if(name=="Walk")state.motion=p.explorationWalk.clip;
            else if(name=="Walk_Start" || name=="Run_Start" || name=="Walk_Stop" || name=="Turn_L90" || name=="Turn_R90")state.motion=Clip(name);
            else if(name=="Run_Stop")state.motion=null; // Not selected by the simple bridge: no compatible authored run stop.
            else if(name=="WalkStartToRun")state.motion=explore;
            else if(name=="Turn_L180" || name=="Turn_R180") {state.motion=null;state.speed=1;} // No authored 180-degree clip: never synthesize one from a 90-degree turn.
            EditorUtility.SetDirty(state);
        }
        foreach(var g in d.gestures.Where(g=>g.validated && g.body!=null))
        {
            string name="Dialogue_"+g.id;
            var state=main.states.Select(s=>s.state).SingleOrDefault(s=>s.name==name)??main.AddState(name);
            state.motion=g.body;state.writeDefaultValues=false;state.iKOnFeet=false;
        }
        // Ground-state transitions are owned by the bridge. Keep action/traversal transitions intact.
        var ground=new HashSet<string>{"Locomotion","CombatLocomotion","CombatIdle","Walk_Start","Run_Start","Walk_Stop","Run_Stop","WalkStartToRun","Turn_L90","Turn_R90","Turn_L180","Turn_R180"};
        foreach(var state in LocomotionPresentationCalibration.States(main).Where(s=>ground.Contains(s.name)))
            foreach(var t in state.transitions.ToArray()) if(t.destinationState!=null && ground.Contains(t.destinationState.name))state.RemoveTransition(t);
        foreach(var t in main.anyStateTransitions.ToArray())if(t.destinationState!=null && ground.Contains(t.destinationState.name))main.RemoveAnyStateTransition(t);
        // Unreferenced old blend trees and removed layer graphs must not retain hidden locomotion sources.
        PruneOrphans(c);
        EditorUtility.SetDirty(c);AssetDatabase.SaveAssetIfDirty(c);
    }
    private static ChildMotion Child(Motion motion,float threshold,float offset=0)=>new(){motion=motion,threshold=threshold,timeScale=1,cycleOffset=offset};
    private static BlendTree Tree(AnimatorController c,string name,BlendTreeType type,string parameter)
    {var tree=new BlendTree{name=name,blendType=type,blendParameter=parameter};AssetDatabase.AddObjectToAsset(tree,c);return tree;}
    private static BlendTree Directional(AnimatorController c,string name,LocomotionPresentationProfile.Cycle[] cycles)
    {
        var t=Tree(c,name,BlendTreeType.SimpleDirectional2D,"HorizontalMovement");t.blendParameterY="ForwardMovement";
        t.children=cycles.Select(c=>new ChildMotion{motion=c.clip,position=c.direction,timeScale=1,cycleOffset=c.cycleOffset}).ToArray();return t;
    }
    private static void PruneOrphans(AnimatorController c)
    {
        var live=new HashSet<Object>{c};
        void Motion(Motion m){if(m==null||!live.Add(m))return;if(m is BlendTree b)foreach(var x in b.children)Motion(x.motion);}
        void Machine(AnimatorStateMachine m)
        {
            live.Add(m);foreach(var b in m.behaviours)live.Add(b);
            foreach(var t in m.anyStateTransitions)live.Add(t);foreach(var t in m.entryTransitions)live.Add(t);
            foreach(var child in m.stateMachines){foreach(var t in m.GetStateMachineTransitions(child.stateMachine))live.Add(t);Machine(child.stateMachine);}
            foreach(var state in m.states){live.Add(state.state);Motion(state.state.motion);foreach(var t in state.state.transitions)live.Add(t);foreach(var b in state.state.behaviours)live.Add(b);}
        }
        foreach(var layer in c.layers)Machine(layer.stateMachine);
        foreach(var obj in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(c)))
            if(!live.Contains(obj) && (obj is BlendTree || obj is AnimatorStateMachine || obj is AnimatorState || obj is AnimatorTransitionBase || obj is StateMachineBehaviour))Object.DestroyImmediate(obj,true);
    }
    [MenuItem("Lit/Animation/Simple Lucian/5 Remove Unreferenced Dialogue Copies")]
    public static void RetireUnusedDialogueCopies()
    {
        if(!IsInstalled || EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Install the simple contract and exit Play Mode first.");
        var profile=AssetDatabase.LoadAssetAtPath<DialoguePresentationProfile>(DialoguePresentationSetup.ProfilePath);
        var candidates=AssetDatabase.FindAssets("",new[]{DialoguePresentationSetup.Folder}).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p=>p.EndsWith("_UpperBody.anim",StringComparison.Ordinal) || p.EndsWith(".mask",StringComparison.Ordinal) ||
                p.EndsWith("/Listen_Attentive.anim",StringComparison.Ordinal) || p.EndsWith("/Wait_Attentive.anim",StringComparison.Ordinal) || p.EndsWith("/Speak_Explain_A.anim",StringComparison.Ordinal) || p.EndsWith("/Speak_Explain_B.anim",StringComparison.Ordinal) || p.EndsWith("/Speak_Seated.anim",StringComparison.Ordinal)).ToArray();
        var extensions=new HashSet<string>{".prefab",".unity",".asset",".controller",".overrideController",".playable",".anim",".vfx"};
        var consumers=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal) &&
            extensions.Contains(Path.GetExtension(p)) && !candidates.Contains(p)).ToArray();
        File.WriteAllText(Report+"/progress.txt","Checking asset references before retiring obsolete dialogue copies.");
        var referenced=new HashSet<string>(AssetDatabase.GetDependencies(consumers,false));
        var log=new List<string>();
        foreach(string path in candidates)
        {
            if(referenced.Contains(path)) {log.Add("RETAINED referenced: "+path);continue;}
            AssetDatabase.ReleaseCachedFileHandles();
            if(!AssetDatabase.DeleteAsset(path))throw new IOException("Could not retire owned unreferenced copy: "+path);
            log.Add("REMOVED unreferenced: "+path);
        }
        File.WriteAllLines(Report+"/retired-copies.txt",log);
    }
    [MenuItem("Lit/Animation/Simple Lucian/3 Validate Contract")]
    public static void Validate()
    {
        if(!IsInstalled)throw new BuildFailedException("Lucian whole-body profile is missing.");
        var data=AssetDatabase.LoadAssetAtPath<CharacterData>(DataPath);
        var dialogue=data.playerSettings.dialoguePresentation;
        if(dialogue==null || dialogue.gestures.Where(g=>g.validated).Any(g=>g.body==null))
            throw new BuildFailedException("Lucian dialogue must use qualified complete body clips.");
        var provenance=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/Sources.asset");
        if(provenance==null)throw new BuildFailedException("Missing immutable locomotion source provenance.");
        var approved=JsonUtility.FromJson<Manifest>(provenance.text);
        foreach(var e in approved.entries)
        {
            string source=AssetDatabase.GUIDToAssetPath(e.guid);
            if(string.IsNullOrEmpty(source) || source.IndexOf("Twinblades",StringComparison.OrdinalIgnoreCase)>=0 ||
                AssetDatabase.AssetPathToGUID(e.target)!=e.targetGuid)throw new BuildFailedException("Unresolved or forbidden locomotion provenance: "+e.id);
        }
        foreach(var path in ControllerPaths)
        {
            var c=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(c.parameters.Any(p=>p.name=="LocomotionPlaybackRate") ||
                c.layers.SelectMany(l=>LocomotionPresentationCalibration.States(l.stateMachine)).Any(s=>s.speedParameterActive && s.speedParameter=="LocomotionPlaybackRate"))
                throw new BuildFailedException("Automatic locomotion cadence remains: "+path);
            if(c.layers.Length!=2 || c.layers[0].avatarMask!=null || c.layers[1].avatarMask==null)throw new BuildFailedException("Lucian must have exactly base + torch arm layers.");
            var mask=c.layers[1].avatarMask;
            for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)
                if(mask.GetHumanoidBodyPartActive((AvatarMaskBodyPart)i)!=(i==(int)AvatarMaskBodyPart.RightArm || i==(int)AvatarMaskBodyPart.RightFingers))throw new BuildFailedException("Invalid torch-only arm mask.");
            foreach(var s in LocomotionPresentationCalibration.States(c.layers[0].stateMachine))
                if(s.name.Contains("Locomotion") || s.name.Contains("Idle") || s.name.StartsWith("Dialogue_") || s.name.StartsWith("Turn_") || s.name.StartsWith("Walk") || s.name.StartsWith("Run"))
                    foreach(var clip in Clips(s.motion)) if(AssetDatabase.GetAssetPath(clip).IndexOf("Twinblades",StringComparison.OrdinalIgnoreCase)>=0)
                        throw new BuildFailedException("Forbidden Twinblades presentation source: "+s.name+" / "+clip.name);
        }
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if(prefab.GetComponent<CharacterAnimationController>().Animator.applyRootMotion)throw new BuildFailedException("Legacy Lucian pose compensation or automatic Root remains.");
        foreach(var path in PrefabPaths)
        {
            var actor=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var contract=actor.GetComponent<CharacterAnimationController>();
            if(actor.GetComponentInChildren<LitPresentationAnimatorMonitor>(true)==null ||
                !data.playerSettings.locomotion.presentationProfile.IsUsable(contract.Animator.avatar))
                throw new BuildFailedException("Missing exclusive parameter ownership or incompatible avatar: "+path);
            var motor=actor.GetComponent<Opsive.UltimateCharacterController.Character.UltimateCharacterLocomotion>();
            if(motor!=null && (motor.UseRootMotionPosition || motor.UseRootMotionRotation))throw new BuildFailedException("UCC Root gameplay enabled: "+path);
        }
        Debug.Log("SIMPLE_LUCIAN_CONTRACT_PASS");
    }
    private static IEnumerable<AnimationClip> Clips(Motion m)
    {if(m is AnimationClip c)yield return c;else if(m is BlendTree b)foreach(var x in b.children)foreach(var c2 in Clips(x.motion))yield return c2;}
}

public sealed class SimpleLucianAnimationBuildValidator : IPreprocessBuildWithReport
{
    public int callbackOrder=>10;
    public void OnPreprocessBuild(BuildReport report){if(SimpleLucianAnimationSetup.IsInstalled)SimpleLucianAnimationSetup.Validate();}
}
