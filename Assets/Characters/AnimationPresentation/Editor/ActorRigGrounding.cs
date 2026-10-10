using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>A constant avatar-to-ground pose offset. Does not change the capsule, Agent or authored body oscillations.</summary>
public static class ActorRigGrounding
{
    [Serializable] public sealed class Measurement
    {
        public string actor, clip; public bool nativeIK;
        public float leftMin,rightMin,leftMax,rightMax,maximumBothFeetHeight;
    }
    [Serializable] public sealed class Report { public System.Collections.Generic.List<Measurement> measurements = new System.Collections.Generic.List<Measurement>(); }
    [MenuItem("Lit/Animation Presentation/Audit Existing Native Foot IK")]
    public static void AuditNativeIK()
    {
        var report = new Report();
        foreach (bool enemy in new[] {false,true})
        {
            var actor = ActorAnimationAudit.Actor(enemy);
            using (var sampler = new ActorPoseSampler(actor))
            foreach (string name in new[] {"Idle","Walk","Run","Strike","Sweep","Followup","Hurt"})
            {
                var clip = ActorAnimationAudit.Source(ActorAnimationAudit.EnemySource(name));
                foreach (bool ik in new[] {false,true})
                {
                    var frames = sampler.Sample(clip,measureSoles:true,applyFootIK:ik);
                    report.measurements.Add(new Measurement {actor=actor.name,clip=clip.name,nativeIK=ik,
                        leftMin=frames.Min(f=>f.leftSole.y),rightMin=frames.Min(f=>f.rightSole.y),
                        leftMax=frames.Max(f=>f.leftSole.y),rightMax=frames.Max(f=>f.rightSole.y),
                        maximumBothFeetHeight=frames.Max(f=>Mathf.Min(f.leftSole.y,f.rightSole.y))});
                }
            }
        }
        System.IO.Directory.CreateDirectory(ActorAnimationAudit.ReportFolder);
        System.IO.File.WriteAllText(ActorAnimationAudit.ReportFolder + "/native-ik.json",JsonUtility.ToJson(report,true));
    }
    public static void AuditAttackRetargeting()
    {
        AuditNativeIK();
        ActorActionExitAudit.AuditWithNativeIK();
    }
    public static float EnemyOffset()
    {
        var actor = ActorAnimationAudit.Actor(true);
        var idle = ActorAnimationAudit.Source(ActorAnimationAudit.EnemySource("Idle"));
        using (var sampler = new ActorPoseSampler(actor))
        {
            var frames = sampler.Sample(idle,false,30,true);
            if (frames.Any(f => float.IsNaN(f.leftSole.y) || float.IsInfinity(f.leftSole.y) ||
                float.IsNaN(f.rightSole.y) || float.IsInfinity(f.rightSole.y)))
                throw new InvalidOperationException("Rendered sole bindings unresolved on the reference Idle.");
            float left = frames.Average(f => f.leftSole.y), right = frames.Average(f => f.rightSole.y);
            if (Mathf.Abs(left-right) > .01f || frames.Max(f => f.leftSole.y) - frames.Min(f => f.leftSole.y) > .01f ||
                frames.Max(f => f.rightSole.y) - frames.Min(f => f.rightSole.y) > .01f)
                throw new InvalidOperationException("Reference Idle has no stable two-foot support; grounding needs author review.");
            var agent = actor.GetComponent<NavMeshAgent>();
            if (agent == null) throw new InvalidOperationException("NavMesh grounding authority missing.");
            return -agent.baseOffset - (left+right)*.5f;
        }
    }
    public static void ApplyPoseOffset(AnimationClip clip,float humanScale,float offset)
    {
        if (Mathf.Abs(offset) < .00001f) return;
        var binding = EditorCurveBinding.FloatCurve("",typeof(Animator),"RootT.y");
        var curve = AnimationUtility.GetEditorCurve(clip,binding);
        if (curve == null) throw new InvalidOperationException("Body height curve unresolved: " + clip.name);
        var keys = curve.keys;
        if (humanScale <= .001f) throw new InvalidOperationException("Invalid avatar scale for pose grounding.");
        for (int i = 0; i < keys.Length; i++) keys[i].value += offset / humanScale;
        // Keep the authored tangents and every oscillation; only its rig-dependent baseline changes.
        AnimationUtility.SetEditorCurve(clip,binding,new AnimationCurve(keys) {preWrapMode=curve.preWrapMode,postWrapMode=curve.postWrapMode});
    }
}

/// <summary>Explicit, reversible laboratory preview. Original controllers, profiles and prefabs are never overwritten.</summary>
[InitializeOnLoad]
public static class ActorRecoveryPreview
{
    private const string Key = "Lit.ActorRecoveryPreview";
    private static bool installed;
    private static LucianBrainsCombatArena arena;
    private static LitBrainsCombatProfile profile;
    private static double started;
    private static readonly System.Collections.Generic.HashSet<string> observed = new System.Collections.Generic.HashSet<string>();
    private static Camera camera;
    private static int capturedSequence = -1;
    private static string originalControllerHash;
    private static LitBrainsEnemy previewEnemy;
    private static RuntimeAnimatorController originalController;
    private static LitBrainsCombatProfile originalProfile;
    static ActorRecoveryPreview()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode) return;
        if (previewEnemy != null)
        {
            previewEnemy.combatProfile = originalProfile;
            if (previewEnemy.Animator != null) previewEnemy.Animator.runtimeAnimatorController = originalController;
        }
        if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
        if (camera != null) UnityEngine.Object.DestroyImmediate(camera.gameObject);
        profile=null; camera=null; previewEnemy=null; originalProfile=null; originalController=null;
        installed=false; arena=null; observed.Clear(); capturedSequence=-1;
        SessionState.SetBool(Key,false); SessionState.SetBool(Key + ".Test",false);
    }

    [MenuItem("Lit/Animation Presentation/Try Enemy Recovery Preview in Iluvilirae")]
    public static void Start()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != IluviliraeSetup.ScenePath)
            throw new InvalidOperationException("Open Iluvilirae_Test first. This preview is restricted to the laboratory.");
        SessionState.SetBool(Key,true);
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
    }

    public static void RunBatchValidation()
    {
        if (!Application.isBatchMode || !Application.dataPath.Replace('\\','/').Contains("/Library/ActorAnimationSandbox/"))
            throw new InvalidOperationException("Validation requires the isolated laboratory project.");
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        SessionState.SetBool(Key + ".Test",true); Start();
    }

    private static void Install(LitBrainsEnemy enemy)
    {
        observed.Clear(); capturedSequence=-1;
        ActorActionExitAudit.AuditWithNativeIK();
        var report = JsonUtility.FromJson<ActorActionExitAudit.Report>(System.IO.File.ReadAllText(ActorAnimationAudit.ReportFolder + "/action-exits-native-ik.json"));
        originalControllerHash = AssetDatabase.GetAssetDependencyHash(JuggernautV2Setup.ControllerPath).ToString();
        string folder = "Assets/Characters/3_Enemy/Iluvilirae/PresentationPreview";
        System.IO.Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        string path = folder + "/Juggernaut_Recovery_" + originalControllerHash.Substring(0,8) + ".controller";
        if (!AssetDatabase.LoadAssetAtPath<AnimatorController>(path) && !AssetDatabase.CopyAsset(JuggernautV2Setup.ControllerPath,path))
            throw new InvalidOperationException("Could not create isolated preview controller.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        profile = UnityEngine.Object.Instantiate(enemy.combatProfile);
        profile.name += " (recovery preview)";
        var states = controller.layers.SelectMany(l => LocomotionPresentationCalibration.States(l.stateMachine)).ToArray();
        states.Single(s => s.name == "Locomotion").iKOnFeet = true;
        if (!controller.parameters.Any(p => p.name == "ActionRecoveryPlaybackRate"))
            controller.AddParameter(new AnimatorControllerParameter {name="ActionRecoveryPlaybackRate",type=AnimatorControllerParameterType.Float,defaultFloat=1});
        int enabled = 0;
        foreach (var action in profile.attacks)
        {
            var measured = report.exits.Single(e => e.state == action.stateName);
            bool candidate = measured.recoveredExitSoleDistance <= .03f && measured.recoveredExitPelvisHeightDifference <= .02f;
            action.visualHandoff = new VisualActionHandoff {validated=candidate,
                sourceExitWindow=new Vector2(action.recoverySeconds/action.clip.length,1),
                destinationPhase=measured.suggestedDestinationPhase,
                destinationCycleSeconds=ActorAnimationAudit.Source(ActorAnimationAudit.EnemySource("Idle")).length};
            if (!candidate) continue;
            enabled++;
            var state = states.Single(s => s.name == action.stateName);
            state.iKOnFeet = true;
            state.speedParameter = "ActionRecoveryPlaybackRate"; state.speedParameterActive = true;
            foreach (var transition in state.transitions.Where(t => t.hasExitTime && t.conditions.Length == 0).ToArray()) state.RemoveTransition(transition);
            EditorUtility.SetDirty(state);
        }
        if (enabled == 0) throw new InvalidOperationException("No measured recovery candidates; originals remain unchanged.");
        EditorUtility.SetDirty(states.Single(s => s.name == "Locomotion"));
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
        previewEnemy=enemy; originalController=enemy.Animator.runtimeAnimatorController; originalProfile=enemy.combatProfile;
        enemy.Animator.runtimeAnimatorController = controller;
        enemy.combatProfile = profile;
        Debug.Log("[ActorRecoveryPreview] " + enabled + " laboratory candidates. Native IK also affects laboratory locomotion; support acceptance pending. Exit Play Mode to restore original actor assets.");
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key,false) || EditorApplication.isCompiling) return;
        if (!EditorApplication.isPlaying)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            { installed=false; arena=null; observed.Clear(); SessionState.SetBool(Key,false); SessionState.SetBool(Key + ".Test",false); }
            return;
        }
        try
        {
            if (arena == null) arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena == null || arena.Enemy == null || arena.Player == null || !arena.Enemy.NavigationReady) return;
            var enemy = arena.Enemy;
            if (!installed)
            {
                if (enemy.AttackPhase != LitBrainsAttackPhase.None) return; // Never replace the presentation owner during an action.
                Install(enemy); installed=true; started=EditorApplication.timeSinceStartup;
                if (SessionState.GetBool(Key + ".Test",false))
                {
                    enemy.gameObject.AddComponent<ActorPresentationRecorder>().authority="NavMeshAgent";
                    camera = new GameObject("Recovery inspection",typeof(Camera)).GetComponent<Camera>();
                    camera.CopyFrom(arena.arenaCamera); camera.enabled=false; camera.targetTexture=null;
                    camera.fieldOfView=38; ActorAnimationLabValidation.ConfigureInspectionCamera(camera);
                    System.IO.Directory.CreateDirectory(ActorAnimationAudit.ReportFolder + "/recovery-preview");
                }
            }
            if (!SessionState.GetBool(Key + ".Test",false)) return;
            if (enemy.Animator.applyRootMotion) throw new InvalidOperationException("Preview activated physical Root.");
            var action = enemy.ActiveAttack;
            float rate = enemy.Animator.GetFloat("ActionRecoveryPlaybackRate");
            if (action != null && enemy.AttackPhase != LitBrainsAttackPhase.Recovery && Mathf.Abs(rate-1) > .001f)
                throw new InvalidOperationException("Preview retimed the preparation or impact.");
            if (action != null && action.visualHandoff.validated && enemy.AttackPhase == LitBrainsAttackPhase.Recovery)
            {
                if (Mathf.Abs(rate-action.visualHandoff.RecoveryRate(action.clip,action.recoveryDurationSeconds)) > .001f)
                    throw new InvalidOperationException("Recovery cadence differs from the original gameplay window.");
                observed.Add(action.stateName);
                if (capturedSequence != enemy.ActionSequenceId)
                {
                    capturedSequence=enemy.ActionSequenceId;
                    camera.transform.position=enemy.transform.position+new Vector3(2.8f,1.7f,3.3f);
                    camera.transform.LookAt(enemy.transform.position+Vector3.up*.9f);
                    ActorAnimationLabValidation.RenderSnapshot(camera,ActorAnimationAudit.ReportFolder + "/recovery-preview/"+action.stateName+".png");
                }
            }
            if (observed.Count >= profile.attacks.Count(a => a.visualHandoff.validated))
            {
                if (originalControllerHash != AssetDatabase.GetAssetDependencyHash(JuggernautV2Setup.ControllerPath).ToString())
                    throw new InvalidOperationException("Original controller changed during preview.");
                Finish("PASS: native recovery candidates run on the real actor; preparation/impact cadence unchanged, Root disabled, original controller untouched. Visual/support acceptance pending.",0);
            }
            else if (EditorApplication.timeSinceStartup-started > 40) throw new InvalidOperationException("Recovery preview observation timed out.");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            if (Application.isBatchMode) Finish(error.ToString(),1);
            else { SessionState.SetBool(Key,false); EditorApplication.isPlaying=false; }
        }
    }
    private static void Finish(string result,int code)
    {
        System.IO.File.WriteAllText(ActorAnimationAudit.ReportFolder + "/recovery-preview-result.txt",result);
        SessionState.SetBool(Key,false); SessionState.SetBool(Key + ".Test",false);
        Debug.Log(result); EditorApplication.Exit(code);
    }
}
