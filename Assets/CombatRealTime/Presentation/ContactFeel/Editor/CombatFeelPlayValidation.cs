using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Short rendered contact recipe on real lab actors. Not a manual feel acceptance.</summary>
[InitializeOnLoad]
public static class CombatFeelPlayValidation
{
    private const string Key = "Lit.ContactFeel.Validation";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static double started;
    private static float at, maximumAngle;
    private static float minimumCameraRatio;
    private static int stage, index, hp, audioBefore, audioGeneration;
    private static Vector3 root;
    private static bool captured;
    private static float orbitBefore;
    private static readonly string DirectoryPath = Path.GetFullPath("Library/CombatFeel/ContactVisual");
    static CombatFeelPlayValidation() => EditorApplication.update += Tick;
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch only.");
        CombatFeelLabSetup.VerifyInstalled();
        Directory.CreateDirectory(DirectoryPath);
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        EditorApplication.isPlaying = true;
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Capture(string name)
    {
        var camera = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>().arenaCamera;
        CaptureCamera(camera, Path.Combine(DirectoryPath, name + ".png"));
    }

    public static void CaptureCamera(Camera camera, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var texture = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = texture });
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            Debug.Log("[ContactFeel] Render=" + path);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(texture);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            Check(EditorApplication.timeSinceStartup - started < 45, "Contact recipe timeout stage=" + stage);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena?.Player == null || arena.Enemy == null) return;
            var enemy = arena.Enemy;
            var pose = enemy.GetComponent<CombatContactPose>();
            var bridge = arena.Player.GetComponent<LitOpsiveLocomotionBridge>();
            var action = arena.Player.GetComponent<PlayerActionPresentationController>();
            var audio = arena.Player.GetComponent<CombatContactAudio>();
            var view = arena.arenaCamera.GetComponent<Opsive.UltimateCharacterController.Camera.CameraController>().ActiveViewType as LitTacticalUccViewType;
            var manager = arena.GetComponent<RealTimeCombatManager>();
            typeof(LitBrainsEnemy).GetField("hurtRemaining", Fields).SetValue(enemy, 1000f);
            typeof(LitBrainsEnemy).GetField("nextDetection", Fields).SetValue(enemy, float.PositiveInfinity);
            if (stage == 0)
            {
                typeof(SquadCharacterController).GetField("enableIdleSitting", Fields).SetValue(arena.Player, false);
                if (arena.Player.IsInSittingCycle) { arena.Player.TrySetSitting(false); return; }
                if (!enemy.NavigationReady || !bridge.Grounded) return;
                Check(pose != null, "Authored enemy contact component missing.");
                Check(audio != null, "Authored player contact audio component missing.");
                Check(view != null && Mathf.Abs(view.ActionDistanceRatio - 1) < .001f, "Exploration camera is not wide at rest.");
                enemy.Health.SetHealth(1000, 1000);
                enemy.combatProfile.resistance = 10000;
                manager.BeginExternalEncounter(arena.Player.transform, enemy, startEngaged: false);
                at = Time.time; stage = 1;
            }
            if (stage == 1 && Time.time - at > .3f && bridge.Grounded && !action.IsActionActive)
            {
                var direction = Vector3.ProjectOnPlane(arena.Player.transform.forward, Vector3.up).normalized;
                bridge.TeleportForSceneTransition(enemy.transform.position - direction * 1.8f + Vector3.up * .02f, Quaternion.LookRotation(direction));
                bridge.SetActionFacingDirection(direction);
                Physics.SyncTransforms();
                hp = enemy.CurrentHealth; root = enemy.transform.position;
                audioBefore = audio.ContactSoundsPlayed;
                maximumAngle = 0; captured = false;
                minimumCameraRatio = 1;
                if (index == 0) Capture("00_framing");
                var skill = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(ChargedSkillSetup.Folder + "/Clash/ClashBasic_" + index + ".asset");
                UnityEngine.Object.FindAnyObjectByType<SkillsManager>().SetAnimationEventSkill(skill);
                Check(manager.TryUseSkill(skill), "Basic refused index=" + index);
                audioGeneration = action.ActionGeneration;
                at = Time.time; stage = 2;
            }
            if (stage == 2)
            {
                maximumAngle = Mathf.Max(maximumAngle, pose.AppliedAngle);
                minimumCameraRatio = Mathf.Min(minimumCameraRatio, view.ActionDistanceRatio);
                Check(Mathf.Abs(view.ManualDistance - 10) < .001f, "Action framing changed the user's base zoom.");
                Check(Vector3.Distance(enemy.transform.position, root) < .05f, "Cosmetic contact moved the enemy root.");
                if (!captured && pose.AppliedAngle > 1)
                {
                    Capture("0" + (index + 1) + "_contact"); captured = true;
                    Debug.Log("[ContactFeel] contact=" + index + " angle=" + pose.AppliedAngle + " damage=" + (hp - enemy.CurrentHealth));
                }
                if (!action.IsActionActive && Time.time - at > .5f)
                {
                    var skill = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(ChargedSkillSetup.Folder + "/Clash/ClashBasic_" + index + ".asset");
                    Check(hp - enemy.CurrentHealth == Mathf.RoundToInt(skill.Damages), "Missing or duplicate contact damage.");
                    Check(captured && maximumAngle > 1, "No visible additive response to confirmed contact.");
                    Check(minimumCameraRatio < .99f, "No action-specific camera approach occurred.");
                    Check(audio.ContactSoundsPlayed == audioBefore + 1, "Missing or doubled confirmed contact sound.");
                    Check(audio.LastBasePitch == audio.ResolvePitch(skill.Damages, audioGeneration), "Contact sound used another action's weight.");
                    Debug.Log("[ContactFeel] sound=" + index + " basePitch=" + audio.LastBasePitch + " count=" + audio.ContactSoundsPlayed);
                    at = Time.time; stage = 3;
                }
            }
            if (stage == 3 && Time.time - at > .35f)
            {
                Check(pose.AppliedAngle == 0, "Contact failed to settle.");
                if (++index < 3) { stage = 1; at = Time.time; return; }
                Capture("04_settled"); at = Time.time; stage = 4;
            }
            if (stage == 4 && Time.time - at > 1.8f)
            {
                Check(view.ActionDistanceRatio > .995f, "Camera did not return to the user's wide base framing.");
                var mode = arena.arenaCamera.GetComponent<LitGameplayCameraModeController>();
                var field = typeof(LitGameplayCameraModeController).GetField("pendingInput", Fields);
                var buffer = (LitTacticalInputBuffer)field.GetValue(mode);
                buffer.Add(Vector2.zero, new Vector2(12, 0), -2);
                field.SetValue(mode, buffer);
                orbitBefore = (float)typeof(LitTacticalUccViewType).GetField("yaw", Fields).GetValue(view);
                typeof(LitTacticalUccViewType).GetMethod("ConsumePendingInput", Fields).Invoke(view, null);
                var skill = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(ChargedSkillSetup.Folder + "/Clash/ClashBasic_0.asset");
                UnityEngine.Object.FindAnyObjectByType<SkillsManager>().SetAnimationEventSkill(skill);
                Check(manager.TryUseSkill(skill), "Manual camera override probe could not start a Basic.");
                at = Time.time; stage = 5;
            }
            if (stage == 5 && Time.time - at > .3f)
            {
                Check(Mathf.Abs(view.ManualDistance - 12) < .001f && view.ActionDistanceRatio > .995f,
                    "Action framing fought the manual zoom-out command: base=" + view.ManualDistance + " ratio=" + view.ActionDistanceRatio);
                float yaw = (float)typeof(LitTacticalUccViewType).GetField("yaw", Fields).GetValue(view);
                Check(Mathf.Abs(Mathf.DeltaAngle(orbitBefore, yaw) - 12) < .01f, "Action framing overrode or doubled manual orbit.");
                if (action.IsActionActive) return;
                Capture("05_manual_zoom");
                Debug.Log("[ContactFeel] PASS | real contacts and unique sounds; additive recoil; root unchanged; action camera approaches then restores wide framing; manual zoom/orbit win. Manual feel/30s exchange not qualified.");
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception); SessionState.SetBool(Key, false); EditorApplication.Exit(1);
        }
    }
}
