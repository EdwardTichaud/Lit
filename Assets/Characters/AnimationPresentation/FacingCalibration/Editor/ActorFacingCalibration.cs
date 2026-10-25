using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit, lab-only calibration. No provider imports or production bindings are rewritten.</summary>
[InitializeOnLoad]
public static partial class ActorFacingCalibration
{
    public const string Folder = "Assets/Characters/AnimationPresentation/FacingCalibration";
    public const string ReportFolder = "Library/ActorFacingCalibration";
    const string Request = "Library/ActorFacingCalibration.request";
    static readonly string[] Turns = { "Neck Turn Left-Right", "Head Turn Left-Right" };
    [Serializable] public sealed class Entry
    {
        public string actor, cycle, source, sourceGuid, sourceHash, metadataHash, avatarGuid, target;
        public long sourceFileId;
        public float muscleOffset, originalYaw, candidateYaw, maximumCandidateYaw;
        public float maximumFootDifference, maximumHipDifference, maximumRootDisplacement;
        public string status;
        public ActorAnimationAudit.EventData[] events;
        public string[] consumers;
    }
    [Serializable] public sealed class Manifest
    {
        public int version = 1;
        public string scope = "LAB ONLY: source/candidate visual comparison; no production migration";
        public string werewolf = "Generic, native animations; no native Run";
        public List<Entry> entries = new List<Entry>();
    }
    sealed class Input
    {
        public Animator actor;
        public AnimationClip source, candidate;
        public Entry entry;
    }
    sealed class Frame
    {
        public Vector3 leftFoot, rightFoot, hip, root;
        public float yaw;
    }

    static ActorFacingCalibration() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
        try
        {
            if (command == "audit") Audit();
            else if (command == "prepare") Prepare();
            else if (command == "capture") Capture();
            else if (command == "scene") RebuildScene();
            else if (command == "transitions") AuditTransitions();
            else if (command == "inventory") AuditConsumers();
            else if (command == "directions") PrepareDirections();
            else if (command == "actions") ActorActionExitAudit.Audit();
            else if (command == "actions-ik") ActorActionExitAudit.AuditWithNativeIK();
            else if (command == "capture-directions") CaptureDirections();
            else if (command == "direction-transitions") AuditDirectionTransitions();
            else if (command == "contacts") AuditUncertainContacts();
            else if (command == "grounding-probe") ProbeWalkGrounding();
            else if (command == "grounding-capture") CaptureWalkGrounding();
            else if (command == "vertical-extraction-probe") ProbeNativeVerticalExtraction();
            else if (command == "bindings") PreparePreviewBindings();
            else if (command == "retouch-walk") PrepareWalkRetouch();
            else if (command == "retouch-walks") PrepareWalkRetouches();
            else throw new InvalidOperationException("Unknown facing calibration request: " + command);
            Directory.CreateDirectory(ReportFolder); File.WriteAllText(ReportFolder + "/result.txt", "PASS: " + command);
        }
        catch (Exception e)
        {
            Directory.CreateDirectory(ReportFolder); File.WriteAllText(ReportFolder + "/result.txt", "FAILED: " + e);
            Debug.LogException(e);
        }
    }

    static string Hash(string path)
    {
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }
    static List<Input> Resolve()
    {
        var result = new List<Input>();
        string sources = ActorAnimationAudit.Sources;
        var actors = new[] { ActorAnimationAudit.Actor(false), ActorAnimationAudit.Actor(true),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Werewolf/Prefab/Basemesh1.prefab")?.GetComponentInChildren<Animator>(true),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StoneGolem/Prefab/StoneGolem_Skin1.prefab")?.GetComponentInChildren<Animator>(true) };
        string[] names = { "Lucian", "Juggernaut", "Werewolf", "StoneGolem" };
        string[][] paths = {
            new[] {sources+"Twinblades_Idle_Inplace.FBX", sources+"Twinblades_Strafe_Walk_F_Inplace.FBX", sources+"Twinblades_Strafe_Run_F_Inplace.FBX"},
            new[] {sources+"Twinblades_Idle_Inplace.FBX", sources+"Twinblades_Strafe_Walk_F_Inplace.FBX", sources+"Twinblades_Strafe_Run_F_Inplace.FBX"},
            new[] {"Assets/Werewolf/Animations/Werewolf@idle1.fbx", "Assets/Werewolf/Animations/Werewolf@walk.fbx"},
            new[] {"Assets/StoneGolem/Animations/StoneGolem@idle1.fbx", "Assets/StoneGolem/Animations/StoneGolem@walkF.fbx", "Assets/StoneGolem/Animations/StoneGolem@jogF.fbx"} };
        // Measured on the actual avatars. Offsets affect ONLY two upper-body muscles, not Root Rotation.
        float[][] offsets = { new[] {-.7440892f, -.4956805f, -.2226866f}, new[] {-.7468941f, -.4911797f, -.2017483f}, new float[2], new float[3] };
        for (int actor = 0; actor < actors.Length; actor++)
        {
            var animator = actors[actor];
            if (animator == null || actor < 2 && (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman))
                throw new InvalidOperationException("Missing or invalid actor rig: " + names[actor]);
            for (int cycle = 0; cycle < paths[actor].Length; cycle++)
            {
                var source = ActorAnimationAudit.Source(paths[actor][cycle]);
                if ((actor < 2) != source.isHumanMotion) throw new InvalidOperationException("Unexpected clip rig: " + paths[actor][cycle]);
                var entry = new Entry {actor=names[actor], cycle=new[]{"Idle","Walk","Run"}[cycle], source=paths[actor][cycle],
                    muscleOffset=offsets[actor][cycle], sourceHash=Hash(paths[actor][cycle]), metadataHash=Hash(paths[actor][cycle]+".meta"),
                    events=ActorAnimationAudit.EventDetails(source)};
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out entry.sourceGuid, out entry.sourceFileId);
                if (animator.avatar != null) AssetDatabase.TryGetGUIDAndLocalFileIdentifier(animator.avatar, out entry.avatarGuid, out long _);
                entry.target=Folder+"/Clips/"+entry.actor+"_"+entry.cycle+".anim";
                result.Add(new Input {actor=animator,source=source,entry=entry});
            }
        }
        return result;
    }

    static Transform Clone(Transform original, Dictionary<Transform,Transform> map)
    {
        var t = new GameObject(original.name).transform;
        t.SetLocalPositionAndRotation(original.localPosition, original.localRotation); t.localScale=original.localScale;
        map.Add(original,t);
        foreach (Transform child in original) Clone(child,map).SetParent(t,false);
        t.gameObject.SetActive(original.gameObject.activeSelf);
        return t;
    }
    static Transform Named(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t=>t.name.Equals(name,StringComparison.OrdinalIgnoreCase));
    static Frame[] Sample(Animator source, AnimationClip clip)
    {
        var root=Clone(source.transform,new Dictionary<Transform,Transform>()).gameObject;
        root.hideFlags=HideFlags.HideAndDontSave; root.SetActive(true);
        root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity); root.transform.localScale=source.transform.lossyScale;
        var animator=root.AddComponent<Animator>(); animator.avatar=source.avatar; animator.applyRootMotion=false; animator.fireEvents=false;
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; animator.Rebind();
        var head=animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Head):Named(root.transform,"head");
        var hip=animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Hips):Named(root.transform,"pelvis");
        var left=animator.isHuman?animator.GetBoneTransform(HumanBodyBones.LeftFoot):Named(root.transform,"foot_l");
        var right=animator.isHuman?animator.GetBoneTransform(HumanBodyBones.RightFoot):Named(root.transform,"foot_r");
        var basis=Quaternion.Inverse(head.rotation)*Vector3.forward;
        var graph=PlayableGraph.Create("Facing calibration measurement");
        try
        {
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var p=AnimationClipPlayable.Create(graph,clip);p.SetApplyFootIK(false);p.SetApplyPlayableIK(false);
            AnimationPlayableOutput.Create(graph,"Pose",animator).SetSourcePlayable(p);graph.Play();graph.Evaluate(0);
            int samples=Mathf.Max(2,Mathf.CeilToInt(clip.length*60));var frames=new Frame[samples+1];
            for(int i=0;i<=samples;i++)
            {
                if(i>0)graph.Evaluate(clip.length/samples);
                frames[i]=new Frame {leftFoot=left.position,rightFoot=right.position,hip=hip.position,root=root.transform.position,
                    yaw=Vector3.SignedAngle(Vector3.forward,Vector3.ProjectOnPlane(head.rotation*basis,Vector3.up),Vector3.up)};
            }
            return frames;
        }
        finally {graph.Destroy();Object.DestroyImmediate(root);}
    }
    static void Measure(Input input, float maximumYaw=3f)
    {
        if(input.candidate!=null)
        {
            if(Mathf.Abs(input.source.length-input.candidate.length)>1e-5f || input.source.frameRate!=input.candidate.frameRate ||
                JsonUtility.ToJson(AnimationUtility.GetAnimationClipSettings(input.source))!=JsonUtility.ToJson(AnimationUtility.GetAnimationClipSettings(input.candidate)))
                throw new InvalidOperationException("Timing or extraction settings changed: "+input.entry.target);
            var expected=ActorAnimationAudit.EventDetails(input.source);var actual=ActorAnimationAudit.EventDetails(input.candidate);
            if(expected.Length!=actual.Length || expected.Where((e,i)=>JsonUtility.ToJson(e)!=JsonUtility.ToJson(actual[i])).Any())
                throw new InvalidOperationException("Animation events changed: "+input.entry.target);
        }
        var before=Sample(input.actor,input.source);var after=input.candidate!=null?Sample(input.actor,input.candidate):before;
        var e=input.entry;e.originalYaw=before.Average(f=>f.yaw);e.candidateYaw=after.Average(f=>f.yaw);
        e.maximumCandidateYaw=after.Max(f=>Mathf.Abs(f.yaw));
        if(before.Length!=after.Length)throw new InvalidOperationException("Duration changed: "+e.target);
        for(int i=0;i<before.Length;i++)
        {
            e.maximumFootDifference=Mathf.Max(e.maximumFootDifference,Vector3.Distance(before[i].leftFoot,after[i].leftFoot),Vector3.Distance(before[i].rightFoot,after[i].rightFoot));
            e.maximumHipDifference=Mathf.Max(e.maximumHipDifference,Vector3.Distance(before[i].hip,after[i].hip));
            e.maximumRootDisplacement=Mathf.Max(e.maximumRootDisplacement,after[i].root.magnitude);
        }
        e.status=input.candidate==null?"Read-only source audit":"Lab candidate; rendered review and gameplay transitions pending";
        if(input.candidate!=null && (e.maximumFootDifference>.001f || e.maximumHipDifference>.001f || e.maximumRootDisplacement>.001f ||
            input.actor.isHuman && e.maximumCandidateYaw>maximumYaw)) throw new InvalidOperationException("Candidate outside preservation/facing tolerances: "+e.target+" "+JsonUtility.ToJson(e));
    }
    static AnimationClip Candidate(Input input)
    {
        if(input.source.isHumanMotion)
        {
            var settings=AnimationUtility.GetAnimationClipSettings(input.source);
            if(!settings.keepOriginalOrientation || !settings.loopBlendOrientation)
                throw new InvalidOperationException("Original/baked rotation policy must be reviewed before calibration: "+input.entry.source);
        }
        var copy=new AnimationClip(); EditorUtility.CopySerialized(input.source,copy);
        copy.name=input.entry.actor+"_"+input.entry.cycle;
        if(input.entry.muscleOffset!=0)
        foreach(string muscle in Turns)
        {
            var binding=EditorCurveBinding.FloatCurve("",typeof(Animator),muscle);
            var curve=AnimationUtility.GetEditorCurve(input.source,binding);
            if(curve==null)throw new InvalidOperationException("Missing muscle "+muscle+" in "+input.entry.source);
            var keys=curve.keys;
            for(int i=0;i<keys.Length;i++){var k=keys[i];k.value+=input.entry.muscleOffset;if(Mathf.Abs(k.value)>1)throw new InvalidOperationException("Muscle limit exceeded");keys[i]=k;}
            curve.keys=keys;AnimationUtility.SetEditorCurve(copy,binding,curve);
        }
        return copy;
    }
    internal static bool SameAuthoring(AnimationClip a, AnimationClip b)
    {
        if(a.frameRate!=b.frameRate || a.legacy!=b.legacy || a.wrapMode!=b.wrapMode || a.isHumanMotion!=b.isHumanMotion ||
            JsonUtility.ToJson(AnimationUtility.GetAnimationClipSettings(a))!=JsonUtility.ToJson(AnimationUtility.GetAnimationClipSettings(b)))return false;
        var ae=ActorAnimationAudit.EventDetails(a);var be=ActorAnimationAudit.EventDetails(b);
        if(ae.Length!=be.Length || ae.Where((e,i)=>JsonUtility.ToJson(e)!=JsonUtility.ToJson(be[i])).Any())return false;
        var bindings=AnimationUtility.GetCurveBindings(a);var other=AnimationUtility.GetCurveBindings(b);
        if(bindings.Length!=other.Length)return false;
        bool Equal(float x,float y)=>x.Equals(y) || Mathf.Abs(x-y)<=1e-6f;
        foreach(var binding in bindings)
        {
            if(!other.Contains(binding))return false;
            var ac=AnimationUtility.GetEditorCurve(a,binding);var bc=AnimationUtility.GetEditorCurve(b,binding);
            if(ac.length!=bc.length || ac.preWrapMode!=bc.preWrapMode || ac.postWrapMode!=bc.postWrapMode)return false;
            var ak=ac.keys;var bk=bc.keys;
            for(int i=0;i<ak.Length;i++)
                if(!Equal(ak[i].time,bk[i].time) || !Equal(ak[i].value,bk[i].value) || !Equal(ak[i].inTangent,bk[i].inTangent) || !Equal(ak[i].outTangent,bk[i].outTangent) ||
                    ak[i].weightedMode!=bk[i].weightedMode || !Equal(ak[i].inWeight,bk[i].inWeight) || !Equal(ak[i].outWeight,bk[i].outWeight))return false;
        }
        var objectBindings=AnimationUtility.GetObjectReferenceCurveBindings(a);var otherObjects=AnimationUtility.GetObjectReferenceCurveBindings(b);
        if(objectBindings.Length!=otherObjects.Length)return false;
        foreach(var binding in objectBindings)
        {
            if(!otherObjects.Contains(binding))return false;
            var ac=AnimationUtility.GetObjectReferenceCurve(a,binding);var bc=AnimationUtility.GetObjectReferenceCurve(b,binding);
            if(ac.Length!=bc.Length || ac.Where((key,i)=>!Equal(key.time,bc[i].time) || key.value!=bc[i].value).Any())return false;
        }
        return true; // Native runtime caches/instance IDs are not authoring changes. Pose checks still run after import.
    }
    static void SaveReport(List<Input> inputs, string name)
    {
        Directory.CreateDirectory(ReportFolder);File.WriteAllText(ReportFolder+"/"+name+".json",JsonUtility.ToJson(new Manifest {entries=inputs.Select(i=>i.entry).ToList()},true));
    }
    [MenuItem("Lit/Animation Presentation/Facing Calibration/1 Audit Native Rigs (Read Only)")]
    public static void Audit()
    {
        var inputs=Resolve();foreach(var input in inputs)Measure(input);SaveReport(inputs,"audit");
    }
    [MenuItem("Lit/Animation Presentation/Facing Calibration/2 Prepare Comparison Lab")]
    public static void Prepare()
    {
        var inputs=Resolve(); // Resolve all sources and avatars before writing any asset.
        for(int i=0;i<SceneManager.sceneCount;i++)
            if(string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                throw new InvalidOperationException("Save the untitled scene before preparing the comparison lab. No asset has been modified.");
        try
        {
            foreach(var input in inputs){input.candidate=Candidate(input);Measure(input);}
            Directory.CreateDirectory(Folder+"/Clips");AssetDatabase.Refresh();
            foreach(var input in inputs)
            {
                var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(input.entry.target);
                if(existing!=null)
                {
                    if(!SameAuthoring(existing,input.candidate)){EditorUtility.CopySerialized(input.candidate,existing);EditorUtility.SetDirty(existing);}
                    Object.DestroyImmediate(input.candidate);input.candidate=existing;
                }
                else AssetDatabase.CreateAsset(input.candidate,input.entry.target);
            }
            foreach(var input in inputs)AssetDatabase.SaveAssetIfDirty(input.candidate);
            foreach(var input in inputs){AssetDatabase.ImportAsset(input.entry.target,ImportAssetOptions.ForceUpdate);input.candidate=AssetDatabase.LoadAssetAtPath<AnimationClip>(input.entry.target);Measure(input);
                if(Hash(input.entry.source)!=input.entry.sourceHash || Hash(input.entry.source+".meta")!=input.entry.metadataHash)throw new InvalidOperationException("Source changed during calibration");}
            SaveReport(inputs,"candidates");BuildScene(inputs);
        }
        finally {foreach(var input in inputs)if(input.candidate!=null && !AssetDatabase.Contains(input.candidate))Object.DestroyImmediate(input.candidate);}
    }

    static GameObject VisualCopy(Animator source)
    {
        var map=new Dictionary<Transform,Transform>();var root=Clone(source.transform,map).gameObject;root.SetActive(true);
        root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=source.transform.lossyScale;
        foreach(var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var copy=map[skin.transform].gameObject.AddComponent<SkinnedMeshRenderer>();copy.sharedMesh=skin.sharedMesh;copy.sharedMaterials=skin.sharedMaterials;
            copy.bones=skin.bones.Select(b=>b!=null && map.TryGetValue(b,out var mapped)?mapped:null).ToArray();
            copy.rootBone=skin.rootBone!=null && map.TryGetValue(skin.rootBone,out var rb)?rb:null;
            copy.localBounds=skin.localBounds;copy.enabled=skin.enabled;copy.updateWhenOffscreen=true;
            if(skin.sharedMesh!=null)for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)copy.SetBlendShapeWeight(i,skin.GetBlendShapeWeight(i));
        }
        foreach(var renderer in source.GetComponentsInChildren<MeshRenderer>(true))
        {
            var filter=renderer.GetComponent<MeshFilter>();if(filter==null)continue;
            map[renderer.transform].gameObject.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
            var copy=map[renderer.transform].gameObject.AddComponent<MeshRenderer>();copy.sharedMaterials=renderer.sharedMaterials;copy.enabled=renderer.enabled;
        }
        var animator=root.AddComponent<Animator>();animator.avatar=source.avatar;animator.applyRootMotion=false;
        return root; // No gameplay scripts, torso compensation, physics or VFX are instantiated.
    }

    [MenuItem("Lit/Animation Presentation/Facing Calibration/3 Capture Actual Models")]
    public static void Capture() => CaptureInputs(Resolve(),"Captures");
    static void CaptureInputs(List<Input> inputs,string captureFolder,float normalizedTime=.35f,float? groundY=null,bool bodyVerticalCorrection=false,bool legRetouch=false,float pelvisPoseOffset=0)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before capturing the isolated comparison.");
        foreach(var input in inputs)
        {
            if(input.candidate==null)input.candidate=AssetDatabase.LoadAssetAtPath<AnimationClip>(input.entry.target);
            if(input.candidate==null)throw new InvalidOperationException("Prepare the lab before capturing: "+input.entry.target);
        }
        var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        var renderScene=EditorSceneManager.NewPreviewScene();
        Mesh floorMesh=null;Material floorMaterial=null;
        try
        {
            SceneManager.SetActiveScene(scene);
            if(groundY.HasValue)
            {
                var shader=Shader.Find("HDRP/Lit");if(shader==null)throw new InvalidOperationException("HDRP floor shader unavailable");
                floorMaterial=new Material(shader);floorMaterial.SetColor("_BaseColor",new Color(.23f,.23f,.25f));floorMaterial.SetFloat("_Smoothness",0);
                floorMesh=new Mesh {vertices=new[]{new Vector3(-8,groundY.Value,-8),new Vector3(8,groundY.Value,-8),new Vector3(-8,groundY.Value,8),new Vector3(8,groundY.Value,8)},triangles=new[]{0,2,1,1,2,3}};
                floorMesh.RecalculateNormals();floorMesh.RecalculateBounds();
                var floor=new GameObject("Measured reference floor");floor.layer=31;SceneManager.MoveGameObjectToScene(floor,renderScene);
                floor.AddComponent<MeshFilter>().sharedMesh=floorMesh;floor.AddComponent<MeshRenderer>().sharedMaterial=floorMaterial;
            }
            var camera=new GameObject("Facing capture").AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;
            SceneManager.MoveGameObjectToScene(camera.gameObject,renderScene);camera.scene=renderScene;camera.cameraType=CameraType.Game;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.17f,.2f);
            ActorAnimationLabValidation.ConfigureInspectionCamera(camera);
            var cameraSettings=camera.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
            cameraSettings.clearColorMode=UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.ClearColorMode.Color;
            cameraSettings.backgroundColorHDR=new Color(.15f,.17f,.2f);
            cameraSettings.volumeLayerMask=1<<31;
            foreach(var field in new[]{UnityEngine.Rendering.HighDefinition.FrameSettingsField.ExposureControl,UnityEngine.Rendering.HighDefinition.FrameSettingsField.Postprocess})
            {
                cameraSettings.renderingPathCustomFrameSettings.SetEnabled(field,true);
                cameraSettings.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field]=true;
            }
            var light=new GameObject("Facing capture light").AddComponent<Light>();light.type=LightType.Directional;light.cullingMask=1<<31;
            SceneManager.MoveGameObjectToScene(light.gameObject,renderScene);
            light.transform.rotation=Quaternion.Euler(35,155,0);
            light.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
            light.intensity=12000;
            var fill=new GameObject("Facing capture fill").AddComponent<Light>();fill.type=LightType.Directional;fill.cullingMask=1<<31;
            SceneManager.MoveGameObjectToScene(fill.gameObject,renderScene);fill.transform.rotation=Quaternion.Euler(20,225,0);
            fill.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();fill.intensity=2500;
            var volume=new GameObject("Facing capture exposure").AddComponent<UnityEngine.Rendering.Volume>();volume.gameObject.layer=31;volume.isGlobal=true;volume.priority=1000;
            SceneManager.MoveGameObjectToScene(volume.gameObject,renderScene);
            volume.sharedProfile=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Characters/3_Enemy/Iluvilirae/Iluvilirae_Test_Volume.asset");
            Directory.CreateDirectory(ReportFolder+"/"+captureFolder);
            foreach(var input in inputs)
            {
                var original=VisualCopy(input.actor);var candidate=VisualCopy(input.actor);
                SceneManager.MoveGameObjectToScene(original,renderScene);SceneManager.MoveGameObjectToScene(candidate,renderScene);
                var graph=PlayableGraph.Create("Isolated facing capture");
                try
                {
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    foreach(var pair in new[]{(original,input.source),(candidate,input.candidate)})
                    {
                        foreach(var t in pair.Item1.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                        var animator=pair.Item1.GetComponent<Animator>();animator.fireEvents=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();
                        var playable=AnimationClipPlayable.Create(graph,pair.Item2);playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);
                        AnimationPlayableOutput.Create(graph,"Pose",animator).SetSourcePlayable(playable);
                    }
                    graph.Play();graph.Evaluate(0);
                    // A positive evaluation initializes the skin even for a requested first-frame capture.
                    graph.Evaluate(Mathf.Max(.00001f,input.source.length*normalizedTime));
                    var sourceAnimator=original.GetComponent<Animator>();var targetAnimator=candidate.GetComponent<Animator>();
                    foreach(var side in new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot})
                    {
                        var a=sourceAnimator.isHuman?sourceAnimator.GetBoneTransform(side):Named(original.transform,side==HumanBodyBones.LeftFoot?"foot_l":"foot_r");
                        var b=targetAnimator.isHuman?targetAnimator.GetBoneTransform(side):Named(candidate.transform,side==HumanBodyBones.LeftFoot?"foot_l":"foot_r");
                        var delta=b.position-a.position;
                        if(legRetouch)
                        {
                            if(Vector3.ProjectOnPlane(delta,Vector3.up).magnitude>.01f || Mathf.Abs(delta.y)>.101f ||
                                Vector3.Distance(sourceAnimator.GetBoneTransform(HumanBodyBones.Hips).position+Vector3.up*pelvisPoseOffset,targetAnimator.GetBoneTransform(HumanBodyBones.Hips).position)>.001f)
                                throw new InvalidOperationException("Leg retouch capture changed protected hip/horizontal pose");
                        }
                        else if(bodyVerticalCorrection)
                        {
                            var ah=sourceAnimator.GetBoneTransform(HumanBodyBones.Hips);var bh=targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
                            if(Vector3.ProjectOnPlane(delta,Vector3.up).magnitude>.001f || Mathf.Abs(delta.y)>.101f ||
                                Vector3.Distance(a.position-ah.position,b.position-bh.position)>.001f)
                                throw new InvalidOperationException("Grounding capture changed more than body height");
                        }
                        else if(delta.magnitude>.001f)throw new InvalidOperationException("Capture phase mismatch: "+input.entry.target);
                    }
                    var renderers=original.GetComponentsInChildren<Renderer>().Where(r=>r.enabled && r.gameObject.activeInHierarchy).ToArray();
                    if(renderers.Length==0)throw new InvalidOperationException("No visible renderers: "+input.entry.actor);
                    var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                    float spacing=Mathf.Max(bounds.size.x*1.25f,bounds.size.y*.8f);
                    original.transform.position+=Vector3.right*spacing*.5f;candidate.transform.position-=Vector3.right*spacing*.5f;
                    var center=bounds.center;float distance=Mathf.Max(bounds.size.y*1.7f,(bounds.size.x+spacing)*.9f);
                    camera.orthographic=true;camera.orthographicSize=Mathf.Max(bounds.size.y*.65f,(bounds.size.x+spacing)*.35f);
                    camera.transform.position=center+new Vector3(0,bounds.size.y*.08f,distance);camera.transform.LookAt(center);
                    ActorAnimationLabValidation.RenderSnapshot(camera,ReportFolder+"/"+captureFolder+"/"+input.entry.actor+"_"+input.entry.cycle+".png");
                }
                finally {graph.Destroy();Object.DestroyImmediate(original);Object.DestroyImmediate(candidate);}
            }
        }
        finally
        {
            if(previous.IsValid())SceneManager.SetActiveScene(previous);EditorSceneManager.ClosePreviewScene(renderScene);EditorSceneManager.CloseScene(scene,true);
            if(floorMesh!=null)Object.DestroyImmediate(floorMesh);if(floorMaterial!=null)Object.DestroyImmediate(floorMaterial);
        }
    }
    [MenuItem("Lit/Animation Presentation/Facing Calibration/Rebuild Comparison Scene")]
    public static void RebuildScene()
    {
        var inputs=Resolve();
        foreach(var input in inputs)
        {
            input.candidate=AssetDatabase.LoadAssetAtPath<AnimationClip>(input.entry.target);
            if(input.candidate==null)throw new InvalidOperationException("Prepare clips first: "+input.entry.target);
        }
        BuildScene(inputs,true);
    }
    static void BuildScene(List<Input> inputs, bool rebuild=false)
    {
        const string scenePath=Folder+"/FacingCalibrationLab.unity";
        if(SceneManager.GetSceneByPath(scenePath).IsValid())throw new InvalidOperationException("Close the comparison scene before rebuilding it.");
        if(File.Exists(scenePath) && !rebuild) {Debug.Log("Facing clips prepared. Existing comparison scene kept intact: "+scenePath);return;}
        var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var manager=new GameObject("Facing comparison — visual only").AddComponent<FacingCalibrationLab>();
            var entries=new List<FacingCalibrationLab.Actor>();var groups=inputs.GroupBy(i=>i.entry.actor).ToArray();
            for(int actor=0;actor<groups.Length;actor++)for(int variant=0;variant<2;variant++)
            {
                var group=groups[actor].ToArray();var visual=VisualCopy(group[0].actor);
                var phase=group.Length==3?MeasurePhase(group[1],group[2]):null;
                visual.name=group[0].entry.actor+(variant==0?" SOURCE":" CALIBRATED");visual.transform.position=new Vector3((actor-1.5f)*5,0,variant*6);
                entries.Add(new FacingCalibrationLab.Actor {label=visual.name,animator=visual.GetComponent<Animator>(),
                    idle=variant==0?group[0].source:group[0].candidate,walk=variant==0?group[1].source:group[1].candidate,
                    run=group.Length<3?null:variant==0?group[2].source:group[2].candidate,
                    walkToRunPhaseOffset=phase!=null && phase.correlation>=.75f?phase.walkToRunOffset:0});
            }
            manager.actors=entries.ToArray();
            var camera=new GameObject("Comparison Camera").AddComponent<Camera>();camera.tag="MainCamera";
            camera.transform.position=new Vector3(0,9,22);camera.transform.LookAt(new Vector3(0,1.8f,3));camera.fieldOfView=65;
            var light=new GameObject("Comparison Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=12000;light.transform.rotation=Quaternion.Euler(45,-30,0);
            // Optional HDRP presentation; the measurement code has no rendering-pipeline dependency.
            var lightData=Type.GetType("UnityEngine.Rendering.HighDefinition.HDAdditionalLightData, Unity.RenderPipelines.HighDefinition.Runtime");
            if(lightData!=null)light.gameObject.AddComponent(lightData);
            light.intensity=12000;light.transform.rotation=Quaternion.Euler(35,155,0);
            var cameraData=Type.GetType("UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData, Unity.RenderPipelines.HighDefinition.Runtime");
            if(cameraData!=null)camera.gameObject.AddComponent(cameraData);
            var volumeType=Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime");
            var exposure=AssetDatabase.LoadMainAssetAtPath("Assets/Characters/3_Enemy/Iluvilirae/Iluvilirae_Test_Volume.asset");
            if(volumeType!=null && exposure!=null)
            {
                var volume=new GameObject("Comparison Exposure").AddComponent(volumeType);
                volumeType.GetProperty("isGlobal")?.SetValue(volume,true);
                volumeType.GetProperty("sharedProfile")?.SetValue(volume,exposure);
            }
            EditorSceneManager.SaveScene(scene,scenePath);
        }
        finally {if(previous.IsValid())SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
    }
}
