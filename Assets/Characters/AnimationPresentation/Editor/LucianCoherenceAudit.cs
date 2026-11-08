using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

/// <summary>Read-only controller comparison on Lucian's actual avatar and meshes. No gameplay behaviours are cloned.</summary>
[InitializeOnLoad]
public static class LucianCoherenceAudit
{
    const string Folder = "Library/LucianCoherence";
    [Serializable] public sealed class Report
    {
        public string avatar, controller, limitations = "Controlled Animator parameter probes, not measured UCC speeds or a gameplay acceptance test. LEFT: base controller, RIGHT: current Moving Legs overlay. Identical phases isolate masking/parameter differences; runtime phase drift is a separate test.";
        public bool validHumanoid;
        public List<Row> cases = new List<Row>();
        public string[] layers;
        public string[] skinBindings;
        public List<StateMotion> states = new List<StateMotion>();
    }
    [Serializable] public sealed class StateMotion
    {
        public string state, tree, source, effective, guid, cadenceParameter;
        public long fileId;
        public float speed, cycleOffset, threshold;
        public bool footIK;
    }
    [Serializable] public sealed class Row
    {
        public string name, baseState, overlayState;
        public float probePhysicalSpeed, baseBlendSpeed, maximumFootDifference, maximumPelvisDifference, maximumKneeAngleDifference;
        public string[] baseClips, overlayClips;
        public string capture;
        public float captureFootDifferenceBeforeRender, captureFootDifferenceAfterRender;
        public float capturePhase;
    }
    static LucianCoherenceAudit() => EditorApplication.update += Poll;
    static void Poll()
    {
        const string request = "Library/LucianCoherence.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var command=File.ReadAllText(request).Trim();File.Delete(request);
        try
        {
            if(command=="audit")Audit();
            else if(command=="cycles")LucianCycleSupportAudit.Run();
            else if(command=="root-pose")LucianCycleSupportAudit.CompareRootPoses();
            else if(command=="curve-contract")LucianCycleSupportAudit.InspectCurveContract();
            else if(command=="pilot-support")LucianSupportPilot.Run();
            else if(command=="bind-support-pilot")LucianSupportPilot.BindPreview();
            else if(command=="pilot-native-support")LucianNativeSupportPilot.Run();
            else if(command=="bind-native-support")LucianNativeSupportPilot.BindPreview();
            else if(command=="check-native-support")LucianNativeSupportPilot.CheckPersisted();
            else if(command=="isolate-preview-flame")LucianNativeSupportPilot.IsolateFlame();
            else if(command=="check-controller-support")LucianNativeSupportPilot.CheckController();
            else if(command=="check-contact-sampler")LucianNativeSupportPilot.CheckContactSampler();
            else if(command=="audit-stance-reference")LucianStanceReferenceAudit.Run();
            else if(command=="audit-rig-rest")LucianRigRestAudit.Run();
            else if(command=="prepare-stride-pilot")LucianNativeSupportPilot.PrepareStridePilot();
            else if(command=="bind-stride-pilot")LucianNativeSupportPilot.BindStridePilot();
            else if(command=="calibrate-stride-cadence")LucianNativeSupportPilot.CalibrateStrideCadence();
            else if(command=="audit-stride-blends-before")LucianStrideBlendAudit.Run("before");
            else if(command=="align-stride-phases")LucianStrideBlendAudit.AlignPreviewPhases();
            else if(command=="audit-stride-blends-after")LucianStrideBlendAudit.Run("after");
            else if(command=="export-native-root-goals")LucianFootGoalExport.Run();
            else if(command=="audit-root-goal-blends")LucianStrideBlendAudit.Run("root-goals",true);
            else if(command=="prepare-native-cycles")LucianNativeCycleBatch.Start();
            else if(command=="retry-native-cycles")LucianNativeCycleBatch.RetryFailed();
            else if(command=="apply-native-cycles")LucianNativeCycleBatch.Apply();
            else if(command=="prepare-cadence-preview")LucianCadencePreview.Prepare();
            else if(command=="normalize-cadence-directions")LucianCadencePreview.NormalizeDirectionalPreview();
            else if(command=="apply-locomotion-authority")ApplyLocomotionAuthority();
            else throw new InvalidOperationException("Unknown Lucian coherence command: "+command);
            File.WriteAllText(Folder + "/result.txt", "PASS: "+command);
        }
        catch (Exception e) { Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/result.txt", e.ToString()); Debug.LogException(e); }
    }
    static void ApplyLocomotionAuthority()
    {
        const string path="Assets/Characters/1_Squad/Lucian/Lucian.asset";
        var asset=AssetDatabase.LoadAssetAtPath<CharacterData>(path);
        if(asset==null)throw new InvalidOperationException("Lucian CharacterData unresolved");
        var data=new SerializedObject(asset);
        var field=data.FindProperty("playerSettings.locomotion.preserveWholeBodyLocomotion");
        if(field==null)throw new InvalidOperationException("Compile the new locomotion setting before applying");
        if(field.boolValue)return;
        Directory.CreateDirectory(Folder);
        var backup=Folder+"/Lucian-before-authority.asset";
        if(!File.Exists(backup))File.Copy(path,backup);
        field.boolValue=true;data.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(asset);
    }
    [MenuItem("Lit/Animation Presentation/Audit Lucian Controller Coherence (Read Only)")]
    public static void Audit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before this isolated read-only audit.");
        Directory.CreateDirectory(Folder);
        var source = ActorAnimationAudit.Actor(false);
        if (source == null || source.runtimeAnimatorController == null) throw new InvalidOperationException("Lucian presentation Animator unresolved");
        var report = new Report {avatar=AssetDatabase.GetAssetPath(source.avatar), controller=AssetDatabase.GetAssetPath(source.runtimeAnimatorController),
            validHumanoid=source.avatar != null && source.avatar.isHuman && source.avatar.isValid};
        Inventory(source.runtimeAnimatorController,report);
        var previous = SceneManager.GetActiveScene();
        // New transient objects must not dirty the user's open scene before moving to the preview.
        var staging = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(staging);
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject left = null, right = null;
        try
        {
            left=ActorFacingCalibration.VisualCopy(source); right=ActorFacingCalibration.VisualCopy(source);
            SceneManager.MoveGameObjectToScene(left,scene); SceneManager.MoveGameObjectToScene(right,scene);
            foreach (var root in new[]{left,right}) foreach(var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=31;
            var a=left.GetComponent<Animator>(); var b=right.GetComponent<Animator>();
            foreach (var animator in new[]{a,b}) { animator.runtimeAnimatorController=source.runtimeAnimatorController; animator.fireEvents=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; animator.Rebind(); animator.Update(0); }
            int layer=b.GetLayerIndex(PlayerMovingLegsOverlay.LayerName);
            if (layer<0) throw new InvalidOperationException("Current controller has no Moving Legs layer");
            report.layers=Enumerable.Range(0,b.layerCount).Select(i=>b.GetLayerName(i)+" weight="+b.GetLayerWeight(i)).ToArray();
            report.skinBindings=left.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s=>s.name+" active="+s.gameObject.activeInHierarchy+" enabled="+s.enabled+" bones="+s.bones.Length+" humanoidLegBound="+s.bones.Contains(a.GetBoneTransform(HumanBodyBones.LeftLowerLeg))).ToArray();
            var camera=CreateCamera(scene);
            Probe("Exploration_Walk_1.036", "Locomotion", false, 1.35f, 1.036f, Vector2.up);
            Probe("Exploration_Walk_6.5_probe", "Locomotion", false, 1.35f, 6.5f, Vector2.up);
            Probe("Exploration_Run_6.5_probe", "Locomotion", false, 3.25f, 6.5f, Vector2.up);
            foreach (var direction in new[]{Vector2.up,Vector2.right,Vector2.down,Vector2.left})
                Probe("Combat_"+direction, "CombatLocomotion", true, 1.35f, 1.036f, direction);
            Probe("CombatIdle_WithResidualMotion", "CombatIdle", true, 1.35f, 1.036f, Vector2.right);

            void Probe(string name,string state,bool combat,float blend,float speed,Vector2 direction)
            {
                var row=new Row {name=name,baseState=state,overlayState=combat?"CombatLocomotion":"Locomotion",probePhysicalSpeed=speed,baseBlendSpeed=blend};
                foreach(var animator in new[]{a,b})
                {
                    animator.Rebind(); animator.Update(0);
                    for(int i=1;i<animator.layerCount;i++)animator.SetLayerWeight(i,0);
                    animator.SetBool("CombatStrafeActive",combat);
                    animator.SetBool("IsMoving",true);
                    foreach(var pair in new[]{("LitSpeed",blend),("LocomotionTier",blend),("CombatMoveMagnitude",1f),("HorizontalMovement",direction.x),("ForwardMovement",direction.y),
                        ("MovingLegsSpeed",speed),("MovingLegsTier",blend),("MovingLegsMagnitude",1f),("MovingLegsX",direction.x),("MovingLegsZ",direction.y),("LocomotionPlaybackRate",1f)})
                        if(animator.parameters.Any(p=>p.name==pair.Item1))animator.SetFloat(pair.Item1,pair.Item2);
                }
                b.SetLayerWeight(layer,1);
                for(int frame=0;frame<=60;frame++)
                {
                    float phase=frame/60f;
                    foreach(var animator in new[]{a,b}) { animator.Play("Base Layer."+state,0,phase); animator.Play("Moving Legs."+row.overlayState,layer,phase); animator.Update(.00001f); }
                    foreach(var bone in new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot})
                    {
                        float difference=Vector3.Distance(Local(a,bone),Local(b,bone));
                        if(difference>row.maximumFootDifference) {row.maximumFootDifference=difference;row.capturePhase=phase;}
                    }
                    row.maximumPelvisDifference=Mathf.Max(row.maximumPelvisDifference,Vector3.Distance(Local(a,HumanBodyBones.Hips),Local(b,HumanBodyBones.Hips)));
                    foreach(bool rightSide in new[]{false,true})row.maximumKneeAngleDifference=Mathf.Max(row.maximumKneeAngleDifference,Mathf.Abs(Knee(a,rightSide)-Knee(b,rightSide)));
                }
                row.baseClips=Clips(a,0);row.overlayClips=Clips(b,layer);
                foreach(var animator in new[]{a,b}) {animator.Play("Base Layer."+state,0,row.capturePhase);animator.Play("Moving Legs."+row.overlayState,layer,row.capturePhase);animator.Update(.00001f);}
                left.transform.position=new Vector3(-.8f,0,0);right.transform.position=new Vector3(.8f,0,0);
                camera.transform.position=new Vector3(0,1.1f,4.5f);camera.transform.LookAt(new Vector3(0,.95f,0));
                row.captureFootDifferenceBeforeRender=Vector3.Distance(Local(a,HumanBodyBones.LeftFoot),Local(b,HumanBodyBones.LeftFoot));
                row.capture=Folder+"/"+report.cases.Count+".png";ActorAnimationLabValidation.RenderSnapshot(camera,row.capture);
                row.captureFootDifferenceAfterRender=Vector3.Distance(Local(a,HumanBodyBones.LeftFoot),Local(b,HumanBodyBones.LeftFoot));
                left.transform.position=right.transform.position=Vector3.zero;
                report.cases.Add(row);
            }
        }
        finally {if(left!=null)Object.DestroyImmediate(left);if(right!=null)Object.DestroyImmediate(right);EditorSceneManager.ClosePreviewScene(scene);if(previous.IsValid())SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(staging,true);}
        File.WriteAllText(Folder+"/controller-baseline.json",JsonUtility.ToJson(report,true));
    }
    static void Inventory(RuntimeAnimatorController controller,Report report)
    {
        var overrides=new List<Dictionary<AnimationClip,AnimationClip>>();
        while(controller is AnimatorOverrideController overlay)
        {
            var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();overlay.GetOverrides(pairs);
            overrides.Insert(0,pairs.ToDictionary(p=>p.Key,p=>p.Value));controller=overlay.runtimeAnimatorController;
        }
        if(controller is not AnimatorController graph)throw new InvalidOperationException("Animator graph unresolved");
        foreach(var layer in graph.layers)Visit(layer.stateMachine,layer.name);
        void Visit(AnimatorStateMachine machine,string path)
        {
            foreach(var item in machine.states)Motion(item.state.motion,path+"."+item.state.name,item.state,"",0);
            foreach(var child in machine.stateMachines)Visit(child.stateMachine,path+"."+child.stateMachine.name);
        }
        void Motion(Motion motion,string path,AnimatorState state,string tree,float threshold)
        {
            if(motion is BlendTree blend) {foreach(var child in blend.children)Motion(child.motion,path,state,tree+"/"+blend.name+"["+blend.blendParameter+","+blend.blendParameterY+"]",child.threshold);return;}
            var original=motion as AnimationClip;var effective=original;
            foreach(var map in overrides)if(effective!=null && map.TryGetValue(effective,out var replacement) && replacement!=null)effective=replacement;
            var row=new StateMotion {state=path,tree=tree,source=AssetDatabase.GetAssetPath(original),effective=AssetDatabase.GetAssetPath(effective),
                footIK=state.iKOnFeet,speed=state.speed,cycleOffset=state.cycleOffset,cadenceParameter=state.speedParameterActive?state.speedParameter:"",threshold=threshold};
            if(effective!=null)AssetDatabase.TryGetGUIDAndLocalFileIdentifier(effective,out row.guid,out row.fileId);
            report.states.Add(row);
        }
    }
    static string[] Clips(Animator a,int layer)=>a.GetCurrentAnimatorClipInfo(layer).Select(c=>AssetDatabase.GetAssetPath(c.clip)+" | "+c.clip.name+" | weight="+c.weight.ToString("F3")).ToArray();
    static Vector3 Local(Animator a,HumanBodyBones bone)=>a.transform.InverseTransformPoint(a.GetBoneTransform(bone).position);
    static float Knee(Animator a,bool right)
    {
        var hip=Local(a,right?HumanBodyBones.RightUpperLeg:HumanBodyBones.LeftUpperLeg);
        var knee=Local(a,right?HumanBodyBones.RightLowerLeg:HumanBodyBones.LeftLowerLeg);
        var ankle=Local(a,right?HumanBodyBones.RightFoot:HumanBodyBones.LeftFoot);
        return Vector3.Angle(hip-knee,ankle-knee);
    }
    static Camera CreateCamera(Scene scene)
    {
        var camera=new GameObject("Lucian audit camera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(camera.gameObject,scene);
        camera.enabled=false;camera.scene=scene;camera.cullingMask=1<<31;camera.orthographic=true;camera.orthographicSize=1.45f;
        ActorAnimationLabValidation.ConfigureInspectionCamera(camera);
        var hd=camera.GetComponent<HDAdditionalCameraData>();hd.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;hd.backgroundColorHDR=new Color(.12f,.14f,.17f);hd.volumeLayerMask=1<<31;
        foreach(var field in new[]{FrameSettingsField.ExposureControl,FrameSettingsField.Postprocess}) {hd.renderingPathCustomFrameSettings.SetEnabled(field,true);hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field]=true;}
        var volume=new GameObject("Lucian audit exposure").AddComponent<Volume>();SceneManager.MoveGameObjectToScene(volume.gameObject,scene);volume.gameObject.layer=31;volume.isGlobal=true;
        volume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Characters/3_Enemy/Iluvilirae/Iluvilirae_Test_Volume.asset");
        foreach(float yaw in new[]{155f,225f}) {var light=new GameObject("Lucian audit light").AddComponent<Light>();SceneManager.MoveGameObjectToScene(light.gameObject,scene);light.type=LightType.Directional;light.cullingMask=1<<31;light.transform.rotation=Quaternion.Euler(35,yaw,0);light.gameObject.AddComponent<HDAdditionalLightData>();light.intensity=yaw==155?12000:2500;}
        return camera;
    }
}
