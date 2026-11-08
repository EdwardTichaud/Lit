using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Lit.Fractures;
using Lit.Fractures.Editor;
using Opsive.UltimateCharacterController.Character;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

/// <summary>Opt-in real-UCC evidence on a temporary flat platform. No production scene or save is written.</summary>
[InitializeOnLoad]
public static class LucianCoherencePlayProbe
{
    const string Key="Lit.LucianCoherence.PlayProbe", Folder="Library/LucianCoherence";
    [Serializable] sealed class Setup { public SceneSetup[] scenes; }
    [Serializable] sealed class Result
    {
        public string run, status, scope="Real prefab, native UCC and simulated gamepad on a temporary flat platform. Initial placement only; no corrective position writes during trials. Not action, slope, collision, cinematic or network acceptance.";
        public string poseTrace;
        public int samples;
        public int jumpFreeAirborneSamples,jumpTorchAirborneSamples,nativeIKAirborneSamples;
        public int rootSpaceGoalSamples;
        public int supportBlendSamples;
        public string supportDataset;
        public bool wholeBodyPolicyEnabled;
        public string existingIKVariant;
        public float maximumObtainedSpeed;
        public List<TrialEvidence> trials=new List<TrialEvidence>();
        public List<ContactEvent> contactEvents=new List<ContactEvent>();
        public List<string> errors=new List<string>();
    }
    [Serializable] sealed class TrialEvidence {public string name;public int frames,movingFrames;public float simulatedSeconds;}
    [Serializable] sealed class ContactEvent
    {
        public string kind,trial,source;public float time,height,phase;public int jumpPhase;
        public bool grounded,jumpActive,impulseApplied,leftGround;
    }
    sealed class Trial
    {
        public string name;public Vector2 input;public bool sprint,torch,combat,jump;
        public Trial(string n,Vector2 i,bool s=false,bool t=false,bool c=false,bool j=false) {name=n;input=i;sprint=s;torch=t;combat=c;jump=j;}
    }
    static readonly Trial[] Trials = new[] {
        new Trial("idle",Vector2.zero),new Trial("walk",Vector2.up),new Trial("run",Vector2.up,true),new Trial("stop",Vector2.zero),
        new Trial("torch_equip",Vector2.zero,false,true),new Trial("torch_walk",Vector2.up,false,true),new Trial("torch_run",Vector2.up,true,true),new Trial("torch_stop",Vector2.zero,false,true),
        new Trial("combat_F",Vector2.up,false,false,true),new Trial("combat_FR",new Vector2(1,1).normalized,false,false,true),new Trial("combat_R",Vector2.right,false,false,true),
        new Trial("combat_BR",new Vector2(1,-1).normalized,false,false,true),new Trial("combat_B",Vector2.down,false,false,true),new Trial("combat_BL",new Vector2(-1,-1).normalized,false,true,true),
        new Trial("combat_L",Vector2.left,false,true,true),new Trial("combat_FL",new Vector2(-1,1).normalized,false,true,true),new Trial("combat_stop",Vector2.zero,false,true,true)
    };
    static Trial[] activeTrials=Trials;
    static Result report;
    static FractureGraybox lab;
    static LitOpsiveLocomotionBridge bridge;
    static Animator animator;
    static Transform target;
    static Gamepad pad;
    static Camera camera;
    static Light inspectionLight;
    static Material inspectionGround;
    static Texture2D inspectionGrid;
    static StreamWriter csv;
    static int trial=-1,lastFrame=-1;
    static float trialStart,started;
    static TrialEvidence trialEvidence;
    static Vector3 previous;
    static string runPath;
    static bool hasPlayback;
    static bool jumpSawAscent,jumpCaptured;
    static GameObject observedActor;
    static readonly List<Object> stopProbeData = new List<Object>();
    static float? handoffCaptureDelta;
    static LucianCoherencePlayProbe() {EditorApplication.update+=Tick;Application.logMessageReceived+=Observe;}
    // Explicit CLI entry points: the asynchronous probe must finish and restore its
    // scene before the batch editor exits. Never use these in an interactive editor.
    public static void RunContactsBatch()=>BeginBatch("cadence-contacts");
    public static void RunDirectionOwnershipBatch()=>BeginBatch("cadence-direction-contacts",false);
    public static void RunDirectionResponseBatch()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Response probe requires a dedicated batch editor.");
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(NativeFootSupportPreparation.ProfilePath);
        NativeFootSupportPreparation.Validate(profile,profile.footSupports,true);
        if(profile.directionDegreesPerSecond!=360||profile.gaitBlendSeconds!=.18f)
        {
            profile.directionDegreesPerSecond=360;profile.gaitBlendSeconds=.18f;profile.supportValidated=false;
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
        }
        BeginBatch("cadence-response-contacts",false);
    }
    public static void RunVisualComparisonBatch()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Visual comparison requires a dedicated batch editor.");
        SessionState.SetString(Key+".NextBatchLabel","cadence-contract");
        SessionState.SetBool(Key+".InspectionGround",true);
        BeginBatch("baseline-contract",true);
    }
    public static void RunSurfaceCalibrationBatch()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Calibration requires a dedicated batch editor.");
        LucianSurfaceVelocityAudit.ApplyMeasuredDirections();
        BeginBatch("cadence-surface-calibrated-contacts",false);
    }
    public static void RunDirectionalGeometryBatch()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Directional geometry probe requires a dedicated batch editor.");
        LucianSurfaceVelocityAudit.VerifyRepeatApplication();
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(NativeFootSupportPreparation.ProfilePath);
        NativeFootSupportPreparation.Validate(profile,profile.footSupports,true);
        if(!profile.matchSimpleDirectionalBlend)
        {
            profile.matchSimpleDirectionalBlend=true;profile.supportValidated=false;
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
        }
        BeginBatch("cadence-directional-geometry-contacts",false);
    }
    public static void RunMixedGaitBatch()=>BeginBatch("cadence-mixed-gaits-contacts",false);
    public static void RunStopSupportBatch()=>BeginBatch("cadence-mixed-gaits-stop-support-contacts",false);
    public static void RunCombatIdleSupportBatch()=>BeginBatch("cadence-stop-support-combat-idle-handoffs-contacts",false);
    public static void RunLockedBrakeSupportBatch()=>BeginBatch("cadence-stop-support-combat-idle-keep-gait-handoffs-contacts",false);
    public static void RunLockedBrakeFadeBatch()=>BeginBatch("cadence-stop-support-combat-idle-keep-gait-fixed-fade-handoffs-contacts",false);
    public static void RunExplorationBrakeBlendBatch()=>BeginBatch("cadence-stop-support-combat-idle-keep-gait-fixed-fade-smooth-exploration-handoffs-contacts",false);
    public static void RunHandoffComparisonBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("The handoff comparison requires a dedicated batch editor.");
        SessionState.SetString(Key + ".NextBatchLabel", "cadence-stop-support-handoffs-contacts");
        BeginBatch("cadence-reference-handoffs-contacts", false);
    }
    public static void RunGaitTimingContractBatch()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Timing contract probe requires a dedicated batch editor.");
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(NativeFootSupportPreparation.ProfilePath);
        NativeFootSupportPreparation.Validate(profile,profile.footSupports,true);
        LucianGaitTimingAudit.Run();
        BeginBatch("cadence-contract",false);
    }
    public static void RunContractBatch()=>BeginBatch("cadence-contract");
    public static void RunBaselineContractBatch()=>BeginBatch("baseline-contract");
    public static void RunJumpAuditBatch()=>BeginBatch("cadence-contract",false);
    public static void RunSupportContactsAndContractBatch()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Support probe requires a dedicated batch editor.");
        NativeFootSupportPreparation.ApplyCandidateChecked();
        SessionState.SetString(Key+".NextBatchLabel","cadence-contract");
        BeginBatch("cadence-support-contacts",false);
    }
    public static void RunJumpReloadAuditBatch()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Domain reload probe requires a dedicated batch editor.");
        SessionState.SetBool(Key+".RestorePlayOptions",true);
        SessionState.SetBool(Key+".PreviousPlayOptionsEnabled",EditorSettings.enterPlayModeOptionsEnabled);
        SessionState.SetInt(Key+".PreviousPlayOptions",(int)EditorSettings.enterPlayModeOptions);
        EditorSettings.enterPlayModeOptionsEnabled=false; // Standard Play: domain and scene reload.
        BeginBatch("cadence-contract",false);
    }
    static void RestorePlayOptions()
    {
        if(!SessionState.GetBool(Key+".RestorePlayOptions",false))return;
        EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Key+".PreviousPlayOptions",0);
        EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Key+".PreviousPlayOptionsEnabled",false);
        SessionState.EraseBool(Key+".RestorePlayOptions");
    }
    static void BeginBatch(string label,bool captureRenders=true)
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Batch probe requires a dedicated batch editor.");
        SessionState.SetBool(Key+".Batch",true);
        SessionState.SetInt(Key+".BatchExitCode",1);
        SessionState.SetBool(Key+".CaptureRenders",captureRenders);
        Application.targetFrameRate=60;
        try
        {
            EditorSceneManager.OpenScene(LucianCadencePreview.ScenePath,OpenSceneMode.Single);
            Begin(label);
        }
        catch(Exception e){RestorePlayOptions();Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Observe(string message,string trace,LogType type)
    {
        if(report!=null && EditorApplication.isPlaying && (type==LogType.Error || type==LogType.Exception))report.errors.Add(message);
    }
    static void Begin(string label)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before this probe.");
        if(EditorSceneManager.GetSceneManagerSetup().Any(s=>s.isLoaded && (string.IsNullOrEmpty(s.path) || SceneManager.GetSceneByPath(s.path).isDirty)))
            throw new InvalidOperationException("Save the open scene before the probe; no user work is saved automatically.");
        Directory.CreateDirectory(Folder);
        StopObservingContacts();
        report=null;lab=null;bridge=null;animator=null;pad=null;camera=null;target=null;csv=null;trial=-1;lastFrame=-1;started=0;
        SessionState.SetString(Key+".Setup",JsonUtility.ToJson(new Setup {scenes=EditorSceneManager.GetSceneManagerSetup()}));
        SessionState.SetString(Key+".Run",Folder+"/play-"+label+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        SessionState.SetInt(Key,1);
        EditorSceneManager.OpenScene(label.StartsWith("cadence")?LucianCadencePreview.ScenePath:FractureGrayboxAuthoring.ScenePath,OpenSceneMode.Single);
        EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating)return;
        const string request=Folder+"/Play.request";
        if(SessionState.GetInt(Key,0)==0)
        {
            if(!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode)return;
            var label=File.ReadAllText(request).Trim();File.Delete(request);
            if(label!="baseline" && label!="candidate" && label!="cadence" && label!="cadence-ik" && label!="cadence-noik" && label!="cadence-support" && label!="cadence-contract" && label!="baseline-contract" && label!="cadence-contacts" && label!="baseline-contacts") {File.WriteAllText(Folder+"/play-result.txt","Invalid probe label");return;}
            try {Begin(label);} catch(Exception e) {SessionState.SetInt(Key,0);File.WriteAllText(Folder+"/play-result.txt",e.ToString());}
            return;
        }
        if(!EditorApplication.isPlaying)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(SessionState.GetInt(Key,0)==1)
            {
                File.WriteAllText(Folder+"/play-result.txt","Interrupted before probe completion: "+SessionState.GetString(Key+".Run",""));
                SessionState.SetInt(Key,2);
            }
            if(SessionState.GetInt(Key,0)==2)
            {
                StopObservingContacts();
                ReleaseInspectionGround();
                var candidateName=SessionState.GetString(Key+".CandidateDataName","");
                if(!string.IsNullOrEmpty(candidateName))foreach(var candidate in Resources.FindObjectsOfTypeAll<CharacterData>())
                    if(candidate.name==candidateName && !EditorUtility.IsPersistent(candidate))Object.DestroyImmediate(candidate);
                SessionState.EraseString(Key+".CandidateDataName");
                EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Setup>(SessionState.GetString(Key+".Setup","")).scenes);
                SessionState.SetInt(Key,0);
                RestorePlayOptions();
                if(Application.isBatchMode&&SessionState.GetBool(Key+".Batch",false))
                {
                    string next=SessionState.GetString(Key+".NextBatchLabel","");
                    SessionState.EraseString(Key+".NextBatchLabel");
                    if(SessionState.GetInt(Key+".BatchExitCode",1)==0&&!string.IsNullOrEmpty(next))
                    {
                        SessionState.SetInt(Key+".BatchExitCode",1);Begin(next);return;
                    }
                    SessionState.EraseBool(Key+".Batch");
                    EditorApplication.Exit(SessionState.GetInt(Key+".BatchExitCode",1));
                }
            }
            return;
        }
        EditorApplication.QueuePlayerLoopUpdate();
        if(lastFrame==Time.frameCount)return;
        lastFrame=Time.frameCount;
        try
        {
            if(report==null)
            {
                if(started==0)started=Time.realtimeSinceStartup;
                lab=Object.FindAnyObjectByType<FractureGraybox>();
                if(lab==null || lab.Player==null) {if(Time.realtimeSinceStartup-started>20)throw new InvalidOperationException("Lucian fixture did not initialize");return;}
                Initialize();return;
            }
            if(Time.realtimeSinceStartup-started>(Application.isBatchMode?180:90))throw new InvalidOperationException("UCC evidence probe timeout");
            if(trial<0) {if(Time.unscaledTime-trialStart<.8f)return;Next();return;}
            Record();
            trialEvidence.frames++;
            trialEvidence.simulatedSeconds+=Time.deltaTime;
            if(bridge.PlanarVelocity.magnitude>.25f)trialEvidence.movingFrames++;
            var item=activeTrials[trial];
            var inputState=new GamepadState {leftStick=item.input,rightTrigger=item.sprint?1:0};
            if(item.jump&&trialEvidence.simulatedSeconds<.2f)inputState=inputState.WithButton(GamepadButton.South);
            InputSystem.QueueStateEvent(pad,inputState);InputSystem.Update();
            if(trialEvidence.simulatedSeconds<(item.jump?1.8f:runPath.Contains("-handoffs-")?1.8f:runPath.Contains("-contacts-")?3f:1.2f))return;
            Capture(item.name);
            Next();
        }
        catch(Exception e) {if(report!=null)report.errors.Add(e.ToString());Finish("FAILED: "+e.Message);}
    }
    static void Initialize()
    {
        runPath=SessionState.GetString(Key+".Run","");Directory.CreateDirectory(runPath);
        activeTrials=runPath.Contains("-contract-")?Trials.Concat(new[]{new Trial("jump_free",Vector2.up,j:true),new Trial("landing_free",Vector2.up),new Trial("jump_torch",Vector2.up,t:true,j:true),new Trial("landing_torch",Vector2.up,t:true)}).ToArray():Trials;
        if(runPath.Contains("-mixed-gaits-"))activeTrials=new[] {
            new Trial("idle",Vector2.zero),
            new Trial("explore_035",Vector2.up*.35f),new Trial("explore_05",Vector2.up*.5f),new Trial("explore_065",Vector2.up*.65f),new Trial("explore_08",Vector2.up*.8f),
            new Trial("combat_035",Vector2.up*.35f,c:true),new Trial("combat_05",Vector2.up*.5f,c:true),new Trial("combat_065",Vector2.up*.65f,c:true),new Trial("combat_08",Vector2.up*.8f,c:true),
            new Trial("combat_FR_05",new Vector2(1,1).normalized*.5f,c:true),new Trial("combat_FL_05",new Vector2(-1,1).normalized*.5f,t:true,c:true),new Trial("stop",Vector2.zero,t:true)};
        if (runPath.Contains("-handoffs-"))
        {
            handoffCaptureDelta = Time.captureDeltaTime;
            Time.captureFramerate = 60;
            var diagonal = new Vector2(-1, 1).normalized * .5f;
            activeTrials = new[]
            {
                new Trial("exploration_approach", Vector2.up * .5f, t:true),
                new Trial("exploration_stop", Vector2.zero, t:true),
                new Trial("combat_approach", diagonal, t:true, c:true),
                new Trial("combat_stop_locked", Vector2.zero, t:true, c:true),
                new Trial("combat_resume", diagonal, t:true, c:true),
                new Trial("unlock_moving", diagonal, t:true),
                new Trial("exploration_brake_after_unlock", Vector2.zero, t:true)
            };
        }
        report=new Result {run=runPath,status="Recording"};
        if(runPath.Contains("/play-cadence-"))report.supportDataset=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(NativeFootSupportPreparation.ProfilePath)?.footSupports?.name;
        bridge=lab.Player.GetComponent<LitOpsiveLocomotionBridge>();animator=lab.Player.GetComponent<CharacterAnimationController>().Animator;
        if(!animator.isHuman || !animator.avatar.isValid)throw new InvalidOperationException("Lucian avatar invalid");
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Lucian probe temporary flat floor";floor.transform.position=new Vector3(1000,9,1000);floor.transform.localScale=new Vector3(1000,1,1000);
        if(SessionState.GetBool(Key+".InspectionGround",false))
        {
            inspectionGrid=new Texture2D(64,64,TextureFormat.RGB24,false);
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)inspectionGrid.SetPixel(x,y,((x/16+y/16)%2)==0?new Color(.22f,.24f,.25f):new Color(.34f,.36f,.37f));
            inspectionGrid.filterMode=FilterMode.Point;inspectionGrid.wrapMode=TextureWrapMode.Repeat;inspectionGrid.Apply(false,true);
            inspectionGround=new Material(Shader.Find("HDRP/Lit"));inspectionGround.SetTexture("_BaseColorMap",inspectionGrid);
            inspectionGround.SetTextureScale("_BaseColorMap",new Vector2(500,500));inspectionGround.SetFloat("_Smoothness",0);
            floor.GetComponent<Renderer>().sharedMaterial=inspectionGround;
        }
        // This is fixture placement before recording, never a distance/collision correction.
        lab.Player.GetComponent<UltimateCharacterLocomotion>().SetPositionAndRotation(new Vector3(1000,10,1000),Quaternion.identity,false,true);
        Physics.SyncTransforms();
        var existingIK=lab.Player.GetComponent<CharacterIK>();
        if(runPath.Contains("/play-cadence-ik-"))
        {
            if(existingIK==null)throw new InvalidOperationException("Existing UCC IK missing");
            existingIK.OverrideFootIKWeight=1;report.existingIKVariant="Existing UCC IK, preweighted feet (test only, no jumping in this probe)";
        }
        else if(runPath.Contains("/play-cadence-noik-"))
        {
            if(existingIK==null)throw new InvalidOperationException("Existing UCC IK missing");
            existingIK.enabled=false;report.existingIKVariant="Existing UCC IK disabled in test copy";
        }
        else report.existingIKVariant="Existing UCC IK unchanged";
        if(runPath.Contains("/play-candidate-"))
        {
            var candidate=Object.Instantiate(lab.Player.CharacterData);candidate.name="LucianCoherenceCandidateData-"+Path.GetFileName(runPath);candidate.hideFlags=HideFlags.HideAndDontSave;
            candidate.playerSettings.locomotion.preserveWholeBodyLocomotion=true;
            SessionState.SetString(Key+".CandidateDataName",candidate.name);
            lab.Player.BindCharacterData(candidate,initializeInventory:false);
        }
        if (runPath.Contains("-stop-support-"))
        {
            var solver = lab.Player.GetComponent<LitPresentationCharacterIK>();
            if (solver == null) throw new InvalidOperationException("The Lucian test solver is missing.");
            LucianIdleSupportAudit.ConfigurePlayProbe(solver, stopProbeData, runPath.Contains("-combat-idle-"),
                runPath.Contains("-keep-gait-"), runPath.Contains("-fixed-fade-") ? .14f : 0f);
            solver.SmoothExplorationForProbe = runPath.Contains("-smooth-exploration-");
        }
        lab.Player.ResetFlameToMax(1000,ensureFlameItem:true);
        report.wholeBodyPolicyEnabled=bridge.PreserveWholeBodyLocomotion;
        if(runPath.Contains("/play-candidate-") && !report.wholeBodyPolicyEnabled)throw new InvalidOperationException("Candidate movement settings did not bind");
        target=new GameObject("Lucian probe static lock target").transform;target.position=new Vector3(1000,10,10000);
        camera=new GameObject("Lucian probe capture").AddComponent<Camera>();camera.CopyFrom(lab.previewCamera);camera.enabled=false;camera.targetTexture=null;
        ActorAnimationLabValidation.ConfigureInspectionCamera(camera);
        var sourceCameraData=lab.previewCamera.GetComponent<HDAdditionalCameraData>();
        if(sourceCameraData!=null)EditorUtility.CopySerialized(sourceCameraData,camera.GetComponent<HDAdditionalCameraData>());
        ActorAnimationLabValidation.ConfigureInspectionCamera(camera);
        inspectionLight=new GameObject("Lucian probe inspection light").AddComponent<Light>();inspectionLight.type=LightType.Directional;
        inspectionLight.gameObject.AddComponent<HDAdditionalLightData>();inspectionLight.intensity=6000;inspectionLight.shadows=SessionState.GetBool(Key+".InspectionGround",false)?LightShadows.Soft:LightShadows.None;
        pad=InputSystem.AddDevice<Gamepad>();InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();
        csv=new StreamWriter(runPath+"/samples.csv");
        csv.WriteLine("time,trial,actual_speed,ucc_speed,grounded,input_x,input_y,lit_speed,tier,overlay_speed,overlay_tier,overlay_weight,base_phase,overlay_phase,base_state,overlay_state,torch,actor_clock,root_motion,base_clips,overlay_clips,frame,playback_rate,input_suppressed,action_active,ik_enabled,flame_weight,animator_scale,human_scale,native_ik_preserved,override_foot_ik,root_space_goals,support_blend,support_lqx,support_lqy,support_lqz,support_lqw,support_rqx,support_rqy,support_rqz,support_rqw,final_lqx,final_lqy,final_lqz,final_lqw,final_rqx,final_rqy,final_rqz,final_rqw,left_ground_weight,right_ground_weight");
        hasPlayback=animator.parameters.Any(p=>p.name==LocomotionPresentationProfile.PlaybackParameter);
        previous=lab.Player.transform.position;trial=-1;trialStart=Time.unscaledTime;started=Time.realtimeSinceStartup;
        var recorder=lab.Player.gameObject.AddComponent<ActorPresentationRecorder>();recorder.authority="UCC";recorder.recordFlatContactPatches=runPath.Contains("-mixed-gaits-")||runPath.Contains("-handoffs-");report.poseTrace=recorder.OutputPath;
        observedActor=lab.Player.gameObject;
        Opsive.Shared.Events.EventHandler.RegisterEvent<bool>(observedActor,"OnCharacterGrounded",ObserveGrounded);
        Opsive.Shared.Events.EventHandler.RegisterEvent<float>(observedActor,"OnCharacterLand",ObserveLand);
        Save();
    }
    static void ObserveGrounded(bool value)=>ObserveContact(value?"grounded":"airborne",0);
    static void ObserveLand(float height)=>ObserveContact("land",height);
    static void ObserveContact(string kind,float height)
    {
        if(report==null||observedActor==null)return;
        var jump=observedActor.GetComponent<PlayerScriptedJumpController>();
        bool Flag(string name)=>jump!=null&&(bool)typeof(PlayerScriptedJumpController).GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(jump);
        report.contactEvents.Add(new ContactEvent {kind=kind,height=height,time=Time.unscaledTime,trial=trial>=0&&trial<activeTrials.Length?activeTrials[trial].name:"initialization",
            grounded=bridge.Grounded,jumpActive=jump!=null&&jump.IsActive,impulseApplied=Flag("takeoffImpulseApplied"),leftGround=Flag("leftGround"),
            phase=animator.GetCurrentAnimatorStateInfo(0).normalizedTime,jumpPhase=animator.GetInteger("JumpPhase"),
            source=kind=="land"?new System.Diagnostics.StackTrace(1,true).ToString():null});
    }
    static void StopObservingContacts()
    {
        if(observedActor!=null)
        {
            Opsive.Shared.Events.EventHandler.UnregisterEvent<bool>(observedActor,"OnCharacterGrounded",ObserveGrounded);
            Opsive.Shared.Events.EventHandler.UnregisterEvent<float>(observedActor,"OnCharacterLand",ObserveLand);
        }
        observedActor=null;
    }
    static void Next()
    {
        if(trial>=0)
        {
            if(trialEvidence.frames<20||activeTrials[trial].input.sqrMagnitude>.1f&&trialEvidence.movingFrames<10)
                report.errors.Add("Trial insufficiently exercised: "+trialEvidence.name+" frames="+trialEvidence.frames+" moving="+trialEvidence.movingFrames);
        }
        trial++;
        if(trial>=activeTrials.Length)
        {
            if(!string.IsNullOrEmpty(report.supportDataset)&&report.supportBlendSamples==0)report.errors.Add("Bound support dataset was not exercised");
            if(runPath.Contains("-contract-")&&(report.jumpFreeAirborneSamples==0||report.jumpTorchAirborneSamples==0))report.errors.Add("Requested jump was not exercised in both torch modes");
            Finish(report.errors.Count==0?"RECORDED: review measured clips and renders; no acceptance claimed":"RECORDED WITH ERRORS");return;
        }
        var item=activeTrials[trial];lab.Player.ApplyFlameState(lab.Player.FlameSecondsRemaining,item.torch);
        trialEvidence=new TrialEvidence {name=item.name};report.trials.Add(trialEvidence);
        if(lab.Player.IsFlameEquipped!=item.torch)throw new InvalidOperationException("Torch fixture did not reach requested state: "+item.name);
        if(item.combat)bridge.SetCombatLockTarget(target);else bridge.ClearCombatLockTarget();
        // Look-source retries re-enable disabled UCC IK. For the control only, remove
        // its initialized runtime instance and subscriptions; no prefab is changed.
        if(runPath.Contains("/play-cadence-noik-")&&trial==0)
        {
            var ik=lab.Player.GetComponent<CharacterIK>();
            if(ik!=null){ik.enabled=false;Object.Destroy(ik);}
            report.existingIKVariant="UCC CharacterIK removed from initialized runtime fixture; native Humanoid Foot IK retained";
        }
        jumpSawAscent=false;jumpCaptured=false;trialStart=Time.unscaledTime;Save();
    }
    static string F(float v)=>v.ToString("R",CultureInfo.InvariantCulture);
    static string Clips(int layer)=>"\""+string.Join("|",animator.GetCurrentAnimatorClipInfo(layer).Select(c=>c.clip.name.Replace("\"","\"\"")+":"+F(c.weight)))+"\"";
    static void Capture(string name)
    {
        if(Application.isBatchMode&&!SessionState.GetBool(Key+".CaptureRenders",true))return;
        camera.transform.position=lab.Player.transform.position+lab.Player.transform.forward*3.2f+lab.Player.transform.right*1.8f+Vector3.up*1.25f;
        camera.transform.LookAt(lab.Player.transform.position+Vector3.up*.9f);
        inspectionLight.transform.rotation=camera.transform.rotation*Quaternion.Euler(15,-20,0);
        ActorAnimationLabValidation.RenderSnapshot(camera,runPath+"/"+name+".png");
    }
    static void Record()
    {
        if(Time.deltaTime<=0)return;
        var existingIK=lab.Player.GetComponent<CharacterIK>();
        bool rootGoals=existingIK is LitPresentationCharacterIK goalsIK&&goalsIK.RootSpaceGoalsApplied;
        if(rootGoals)report.rootSpaceGoalSamples++;
        bool support=existingIK is LitPresentationCharacterIK supportIK&&supportIK.SupportBlendApplied;
        if(support)report.supportBlendSamples++;
        if(!bridge.Grounded)
        {
            if(activeTrials[trial].name=="jump_free")report.jumpFreeAirborneSamples++;
            if(activeTrials[trial].name=="jump_torch")report.jumpTorchAirborneSamples++;
            if(existingIK is LitPresentationCharacterIK retained&&retained.PreservingNativeFootIK)report.nativeIKAirborneSamples++;
        }
        if(runPath.Contains("/play-cadence-noik-")&&existingIK!=null&&existingIK.enabled)
            throw new InvalidOperationException("No-IK control invalidated: UCC IK was re-enabled after trial setup");
        var displacement=lab.Player.transform.position-previous;
        if(activeTrials[trial].jump&&!bridge.Grounded)
        {
            if(displacement.y>.0001f)jumpSawAscent=true;
            if(jumpSawAscent&&!jumpCaptured&&displacement.y<-.0001f){Capture(activeTrials[trial].name+"-apex");jumpCaptured=true;}
        }
        float speed=Vector3.ProjectOnPlane(displacement,Vector3.up).magnitude/Time.deltaTime;previous=lab.Player.transform.position;
        report.samples++;report.maximumObtainedSpeed=Mathf.Max(report.maximumObtainedSpeed,speed);
        int layer=animator.GetLayerIndex(PlayerMovingLegsOverlay.LayerName);
        if(bridge.PreserveWholeBodyLocomotion && bridge.Grounded && Time.unscaledTime-trialStart>.1f &&
            PlayerMovingLegsOverlay.IsWholeBodyLocomotion(animator.GetCurrentAnimatorStateInfo(0).shortNameHash) && animator.GetLayerWeight(layer)>.001f)
            throw new InvalidOperationException("A second leg gait still overrides Lucian's full-body locomotion");
        if(runPath.Contains("/play-cadence-") && bridge.Grounded && Time.unscaledTime-trialStart>.1f && !bridge.IsMeasuredLocomotionPresentationActive && !bridge.IsInputSuppressedByUcc)
            throw new InvalidOperationException("Measured cadence candidate is not active");
        var input=bridge.CurrentWorldMoveInput;
        csv.WriteLine(string.Join(",",F(Time.unscaledTime),activeTrials[trial].name,F(speed),F(bridge.PlanarVelocity.magnitude),bridge.Grounded,F(input.x),F(input.y),
            F(animator.GetFloat("LitSpeed")),F(animator.GetFloat("LocomotionTier")),F(animator.GetFloat("MovingLegsSpeed")),F(animator.GetFloat("MovingLegsTier")),F(animator.GetLayerWeight(layer)),
            F(animator.GetCurrentAnimatorStateInfo(0).normalizedTime),F(animator.GetCurrentAnimatorStateInfo(layer).normalizedTime),animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
            animator.GetCurrentAnimatorStateInfo(layer).fullPathHash,lab.Player.IsFlameEquipped,F(animator.speed),animator.applyRootMotion,Clips(0),Clips(layer),Time.frameCount,
            F(hasPlayback?animator.GetFloat(LocomotionPresentationProfile.PlaybackParameter):1),bridge.IsInputSuppressedByUcc,lab.Player.GetComponent<PlayerActionPresentationController>()?.IsActionActive==true,
            existingIK!=null&&existingIK.enabled,F(animator.GetLayerWeight(1)),F(animator.transform.lossyScale.y),F(animator.humanScale),
            existingIK is LitPresentationCharacterIK nativeIK&&nativeIK.PreservingNativeFootIK,F(existingIK!=null?existingIK.OverrideFootIKWeight:-1),rootGoals,support)+RotationEvidence(existingIK as LitPresentationCharacterIK));
    }
    static string RotationEvidence(LitPresentationCharacterIK ik)
    {
        var rotations=ik!=null?new[]{ik.LeftSupportRotation,ik.RightSupportRotation,ik.LeftFinalRotation,ik.RightFinalRotation}:new Quaternion[4];
        return ","+string.Join(",",rotations.SelectMany(q=>new[]{q.x,q.y,q.z,q.w}).Select(F))+","+F(ik!=null?ik.LeftGroundWeight:0)+","+F(ik!=null?ik.RightGroundWeight:0);
    }
    static void Save() {if(report!=null) {csv?.Flush();File.WriteAllText(runPath+"/report.json",JsonUtility.ToJson(report,true));}}
    static void ReleaseInspectionGround()
    {
        if (handoffCaptureDelta.HasValue)
        {
            Time.captureDeltaTime = handoffCaptureDelta.Value;
            handoffCaptureDelta = null;
        }
        foreach (var data in stopProbeData)
            if (data != null) Object.DestroyImmediate(data);
        stopProbeData.Clear();
        if(inspectionGround!=null)Object.DestroyImmediate(inspectionGround);
        if(inspectionGrid!=null)Object.DestroyImmediate(inspectionGrid);
        inspectionGround=null;inspectionGrid=null;
    }
    static void Finish(string status)
    {
        StopObservingContacts();
        SessionState.SetInt(Key+".BatchExitCode",report!=null&&report.errors.Count==0&&status.StartsWith("RECORDED")?0:1);
        if(report!=null) {report.status=status;Save();File.WriteAllText(Folder+"/play-result.txt",runPath+"\n"+status);}
        else File.WriteAllText(Folder+"/play-result.txt",status);
        csv?.Dispose();csv=null;
        if(pad!=null)InputSystem.RemoveDevice(pad);
        SessionState.SetInt(Key,2);EditorApplication.isPlaying=false;
    }
}
