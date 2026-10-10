using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Opt-in actual-model capture in Iluvilirae_Test; never saves scene or runtime edits.</summary>
[InitializeOnLoad]
public static class ActorAnimationLabValidation
{
    private const string Key = "Lit.ActorAnimationLabValidation";
    private static LucianBrainsCombatArena arena;
    private static double started;
    private static int shot;
    private static AnimatorOverrideController preview;
    private static Camera capture;
    private static readonly string[] Actions = { "Strike", "Sweep", "Followup" };
    static ActorAnimationLabValidation() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated validation project.");
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        SessionState.SetBool(Key,true); EditorApplication.isPlaying = true;
    }
    public static void RunWithCandidates()
    {
        LocomotionPresentationCalibration.PrepareLaboratoryCandidates();
        Run();
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (arena == null) { arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>(); started = EditorApplication.timeSinceStartup; }
            if (arena == null || arena.Player == null || arena.Enemy == null || !arena.Enemy.NavigationReady) return;
            if (capture == null)
            {
                Directory.CreateDirectory(ActorAnimationAudit.ReportFolder + "/frames");
                arena.Enemy.enabled = false;
                var agent = arena.Enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                agent.isStopped = true; agent.velocity = Vector3.zero;
                var animator = arena.Enemy.Animator; animator.fireEvents = false;
                arena.Enemy.GetComponent<CombatTimeDomain>()?.SetIntrinsicPause(Key,true);
                preview = new AnimatorOverrideController(animator.runtimeAnimatorController);
                animator.runtimeAnimatorController = preview;
                capture = new GameObject("Actor inspection",typeof(Camera)).GetComponent<Camera>();
                capture.CopyFrom(arena.arenaCamera);
                foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    if (camera != capture) camera.enabled = false;
                capture.targetTexture = null; capture.fieldOfView = 38;
                capture.transform.position = arena.Enemy.transform.position + new Vector3(2.8f,1.7f,3.3f);
                capture.transform.LookAt(arena.Enemy.transform.position + Vector3.up * .9f);
                ConfigureInspectionCamera(capture);
                started = EditorApplication.timeSinceStartup;
                SetupPose(); return;
            }
            if (EditorApplication.timeSinceStartup - started < .4) return;
            if (shot >= 18)
            {
                SessionState.SetBool(Key,false);
                File.WriteAllText(ActorAnimationAudit.ReportFolder + "/visual-result.txt", "Captured actual Juggernaut in Iluvilirae_Test with HDRP/materials. Human visual review required.");
                EditorApplication.Exit(0); return;
            }
            string action = Actions[(shot % 9) / 3];
            string variant = shot < 9 ? "before" : "repaired";
            string path = ActorAnimationAudit.ReportFolder + "/frames/" + variant + "_" + action + "_" + (shot % 3) + ".png";
            Capture(path);
            shot++;
            started = EditorApplication.timeSinceStartup;
            if (shot < 18) SetupPose();
        }
        catch (Exception error)
        {
            SessionState.SetBool(Key,false); Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
    private static void Capture(string path)
        => RenderSnapshot(capture,path);
    public static void ConfigureInspectionCamera(Camera camera)
    {
        var data = camera.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>() ??
            camera.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
        data.customRenderingSettings = true;
        data.antialiasing = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.AntialiasingMode.None;
        var field = UnityEngine.Rendering.HighDefinition.FrameSettingsField.MotionBlur;
        data.renderingPathCustomFrameSettings.SetEnabled(field,false);
        data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
    }
    public static void RenderSnapshot(Camera camera,string path)
    {
        var texture = new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);
        var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = texture };
        try
        {
            if (!UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera,request))
                throw new InvalidOperationException("The active pipeline cannot render the inspection camera.");
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
            try { image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG()); }
            finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(image); }
        }
        finally { UnityEngine.Object.Destroy(texture); }
    }
    private static void SetupPose()
    {
        string action = Actions[(shot % 9) / 3];
        var current = AssetDatabase.LoadAssetAtPath<AnimationClip>(ActorAnimationAudit.EnemyFolder + "Animations/Juggernaut_v2_" + action + ".anim");
        var clip = shot < 9 ? AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/AnimationValidationBaselines/Juggernaut_v2_" + action + ".anim") : current;
        if (clip == null) throw new InvalidOperationException("Baseline clip missing: " + action);
        preview[current] = clip;
        var animator = arena.Enemy.Animator;
        string state = arena.Enemy.combatProfile.attacks.Single(a => a.clip == current).stateName;
        int hash = Animator.StringToHash("Base Layer." + state);
        if (!animator.HasState(0,hash)) throw new InvalidOperationException("Inspection state unresolved: " + state);
        animator.speed = 1; animator.Play(hash,0,new[] { .3f,.6f,.9f }[shot % 3] / clip.length);
        animator.Update(0); animator.speed = 0; // Freeze for capture only, never a gameplay cadence policy.
    }
}
