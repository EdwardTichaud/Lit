using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class CounterCameraHandoffValidation
{
    private const string Key = "Lit.CounterCameraHandoffValidation";
    private static LucianBrainsCombatArena arena;
    private static int stage;
    private static double started;
    private static int oldGeneration;
    private static LitCameraDirector cameraDirector;
    private static readonly System.Collections.Generic.HashSet<ParticleSystem> hitVfxSeen = new System.Collections.Generic.HashSet<ParticleSystem>();
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static CounterCameraHandoffValidation()
    {
        EditorApplication.update += Tick;
        if (SessionState.GetBool(Key, false)) SuppressSceneRepair();
    }

    private static void SuppressSceneRepair()
    {
        var type = typeof(CounterCameraHandoffValidation).Assembly.GetType("MainMenuMissingPrefabRepair");
        if (type == null) return;
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
        var method = type.GetMethod("RepairMissingPrefab", BindingFlags.Static | BindingFlags.NonPublic);
        if (method != null) EditorApplication.delayCall -= (EditorApplication.CallbackFunction)Delegate.CreateDelegate(typeof(EditorApplication.CallbackFunction), method);
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project.");
        SessionState.SetBool(Key, true);
        SuppressSceneRepair();
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            Require(EditorApplication.timeSinceStartup - started < 40, "Timeout at stage " + stage);
            arena ??= UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena == null || arena.Player == null || arena.Enemy == null || !arena.Enemy.NavigationReady) return;
            var manager = arena.GetComponent<RealTimeCombatManager>();
            if (stage == 0)
            {
                // Isolate the counter from normal attack/hurt inputs, but retain
                // the real actors, camera, bindings, staged rig and Timeline.
                arena.Player.SetHealth(1000, 1000);
                arena.Player.GetComponent<LitOpsiveLocomotionBridge>().SetExternalPositionAndRotation(
                    arena.Enemy.transform.position + arena.Enemy.transform.forward * 2.5f, Quaternion.identity, true);
                stage = 1;
            }
            if (stage == 1)
            {
                if (!arena.Enemy.IsAttackCommitted) return;
                Require(arena.GetComponent<CounterSkillCombatController>().TryStartFromSuccessfulQte(manager.CombatTarget,
                    arena.enemyAttackSkills[0]), "Real cinematic counter refused");
                cameraDirector = LitCameraDirector.Instance;
                Require(cameraDirector != null, "Missing gameplay camera director");
                var rigs = UnityEngine.Object.FindObjectsByType<CombatCinematicRig>(FindObjectsSortMode.None);
                Require(Array.Exists(rigs, r => r.CameraEntryBlendSeconds == .25f && r.CameraExitBlendSeconds == .25f), "Baked handoff settings missing");
                VerifyOutputSamples();
                started = EditorApplication.timeSinceStartup;
                stage = 2;
                return;
            }
            if (stage == 2)
            {
                foreach (var particles in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                {
                    if (particles.name != "CombatLab_CounterHitVfx(Clone)") continue;
                    if (hitVfxSeen.Add(particles))
                    {
                        Require(Mathf.Abs(particles.transform.position.y - arena.Player.transform.position.y - 1.5f) < .02f,
                            "Hit VFX height did not match the current player");
                        Require(particles.GetComponent<ParticleSystemRenderer>().sharedMaterial != null, "Hit material missing");
                    }
                }
                if (manager.IsCinematicSequenceActive) return;
                Require(hitVfxSeen.Count == 4, "Expected four runtime hit bursts, observed " + hitVfxSeen.Count);
                Require(Time.timeScale > 0f, "Counter left global pause active");
                Require(!(bool)Get("timelineCinemachineControlActive"), "Camera authority not restored");
                Require((float)Get("outputBlendSeconds") > 0f, "Exit did not schedule a blend");
                VerifyOutputSamples();
                // A stale end must not cancel a subsequent camera session.
                cameraDirector.BeginTimelineCinemachineControl(.25f, .25f, out oldGeneration);
                cameraDirector.BeginTimelineCinemachineControl(.25f, .25f, out int next);
                cameraDirector.EndTimelineCinemachineControl(oldGeneration);
                Require((bool)Get("timelineCinemachineControlActive"), "Stale end cancelled newer session");
                cameraDirector.EndTimelineCinemachineControl(next);
                stage = 3;
                started = EditorApplication.timeSinceStartup;
            }
            if (stage == 3 && EditorApplication.timeSinceStartup - started > .35)
            {
                Finish("PASS real counter in Iluvilirae_Test; four hit VFX at player height, entry/exit render interpolation, raw pose restoration, realtime clock during counter pause, stale generation protection");
            }
        }
        catch (Exception exception) { Finish("FAIL " + exception); }
    }

    private static object Get(string name) => typeof(LitCameraDirector).GetField(name, Private).GetValue(cameraDirector);
    private static void VerifyOutputSamples()
    {
        var camera = cameraDirector.ControlledCamera;
        var source = Get("blendSource");
        var sourcePosition = (Vector3)source.GetType().GetField("position").GetValue(source);
        var sourceRotation = (Quaternion)source.GetType().GetField("rotation").GetValue(source);
        float sourceFov = (float)source.GetType().GetField("fov").GetValue(source);
        var targetPosition = camera.transform.position + new Vector3(2, 1, -1);
        var targetRotation = Quaternion.Euler(10, 80, 0) * camera.transform.rotation;
        float targetFov = 65f;
        foreach (float elapsed in new[] { 0f, .125f, .25f })
        {
            camera.transform.SetPositionAndRotation(targetPosition, targetRotation);
            camera.fieldOfView = targetFov;
            typeof(LitCameraDirector).GetField("blendStartedAt", Private).SetValue(cameraDirector, Time.realtimeSinceStartupAsDouble - elapsed);
            typeof(LitCameraDirector).GetField("outputBlendSeconds", Private).SetValue(cameraDirector, .25f);
            typeof(LitCameraDirector).GetMethod("BeginRender", Private).Invoke(cameraDirector, new object[] { default(ScriptableRenderContext), camera });
            float weight = Mathf.SmoothStep(0, 1, elapsed / .25f);
            Require(Vector3.Distance(camera.transform.position, Vector3.Lerp(sourcePosition, targetPosition, weight)) < .03f, "Position interpolation mismatch");
            Require(Quaternion.Angle(camera.transform.rotation, Quaternion.Slerp(sourceRotation, targetRotation, weight)) < 1f, "Rotation interpolation mismatch");
            Require(Mathf.Abs(camera.fieldOfView - Mathf.Lerp(sourceFov, targetFov, weight)) < .3f, "FOV interpolation mismatch");
            typeof(LitCameraDirector).GetMethod("EndRender", Private).Invoke(cameraDirector, new object[] { default(ScriptableRenderContext), camera });
            Require(Vector3.Distance(camera.transform.position, targetPosition) < .001f && Quaternion.Angle(camera.transform.rotation, targetRotation) < .01f && camera.fieldOfView == targetFov,
                "Blended output fed back into source camera");
        }
        cameraDirector.StartOutputBlend(.25f);
    }

    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Finish(string result)
    {
        SessionState.SetBool(Key, false);
        File.WriteAllText("Library/CounterCameraHandoff.result", result);
        Debug.Log("[CounterCameraHandoffValidation] " + result);
        EditorApplication.Exit(result.StartsWith("PASS") ? 0 : 1);
    }
}
