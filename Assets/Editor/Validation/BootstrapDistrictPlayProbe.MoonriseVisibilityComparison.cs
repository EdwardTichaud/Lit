using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class BootstrapDistrictPlayProbe
{
    private static readonly System.Collections.Generic.Dictionary<Light, LightShadows> moonriseLightShadows = new System.Collections.Generic.Dictionary<Light, LightShadows>();
    private static readonly System.Collections.Generic.Dictionary<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData, float> moonriseShadowDimmers = new System.Collections.Generic.Dictionary<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData, float>();
    private static int moonriseLightingPhase, moonriseLightingFrame;
    private static float moonriseLightingStarted;

    private static void TickMoonriseLightingProbe(SquadCharacterController actor)
    {
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(moonrisePad, new UnityEngine.InputSystem.LowLevel.GamepadState());
        string path = Path.Combine(report.run, "lighting-comparison.txt");
        if (moonriseLightingPhase == 0)
        {
            report.scope = "Focused actual Moonrise counter followed by temporary render-only shadow A/B. No asset, exposure, light intensity or gameplay damage changes. Not full route or pacing proof.";
            File.WriteAllText(path, "Render-only shadow comparison; actors and exposure continue naturally.\n" + MoonriseComparisonPose() + "\n");
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.isActiveAndEnabled))
            {
                moonriseLightShadows[light] = light.shadows;
                var hd = light.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
                if (hd != null && light.type == LightType.Point && Vector3.Distance(light.transform.position, actor.transform.position) < 6f)
                    moonriseShadowDimmers[hd] = hd.shadowDimmer;
                File.AppendAllText(path, $"light={light.gameObject.scene.name}/{light.name} position={light.transform.position} type={light.type} intensity={light.intensity} range={light.range} color={light.color} shadows={light.shadows} mask={light.cullingMask} renderingLayers={light.renderingLayerMask} dimmer={hd?.lightDimmer} shadowDimmer={hd?.shadowDimmer} playerDistance={Vector3.Distance(light.transform.position, actor.transform.position):F2} enemyDistance={Vector3.Distance(light.transform.position, moonriseEnemy.Root.position):F2}\n");
            }
            foreach (var root in new[] { actor.transform, moonriseEnemy.Root })
                foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    File.AppendAllText(path, $"body={root.name}/{renderer.name} enabled={renderer.enabled} layer={renderer.gameObject.layer} renderingLayers={renderer.renderingLayerMask} materials={string.Join(";", renderer.sharedMaterials.Select(m => m == null ? "null" : m.name + ":" + m.shader.name))}\n");
            ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_Lighting_Baseline.png"));
            moonriseLightingFrame = Time.frameCount;
            moonriseLightingPhase = 1;
            return;
        }
        if (Time.frameCount <= moonriseLightingFrame + 1) return;
        if (moonriseLightingPhase == 1)
        {
            foreach (var pair in moonriseShadowDimmers) if (pair.Key != null) pair.Key.shadowDimmer = .75f;
            moonriseLightingStarted = Time.realtimeSinceStartup;
            moonriseLightingPhase = 2;
            return;
        }
        if (moonriseLightingPhase == 2)
        {
            if (Time.realtimeSinceStartup - moonriseLightingStarted < .4f) return;
            Check(moonriseShadowDimmers.Count > 0 && moonriseShadowDimmers.All(p => p.Key == null || Mathf.Approximately(p.Key.shadowDimmer, .75f)), "Local practical shadow dimmer .75 applied for comparison, without changing intensity, moon or ambient fill.");
            ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_Lighting_PracticalShadow075.png"));
            File.AppendAllText(path, "Local shadow .75 " + MoonriseComparisonPose() + "\n");
            moonriseLightingFrame = Time.frameCount;
            moonriseLightingPhase = 3;
            return;
        }
        if (moonriseLightingPhase == 3)
        {
            foreach (var pair in moonriseLightShadows) if (pair.Key != null) pair.Key.shadows = LightShadows.None;
            moonriseLightingStarted = Time.realtimeSinceStartup;
            moonriseLightingPhase = 4;
            return;
        }
        if (moonriseLightingPhase == 4)
        {
            if (Time.realtimeSinceStartup - moonriseLightingStarted < .4f) return;
            Check(moonriseLightShadows.Keys.All(l => l == null || l.shadows == LightShadows.None), "Temporary shadowless comparison actually disabled all sampled active light shadows.");
            ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_Lighting_NoShadows.png"));
            File.AppendAllText(path, "Shadowless " + MoonriseComparisonPose() + "\n");
            moonriseLightingFrame = Time.frameCount;
            moonriseLightingPhase = 5;
            return;
        }
        if (moonriseLightingPhase == 5)
        {
            foreach (var pair in moonriseLightShadows) if (pair.Key != null) pair.Key.shadows = pair.Value;
            foreach (var pair in moonriseShadowDimmers) if (pair.Key != null) pair.Key.shadowDimmer = pair.Value;
            Check(moonriseLightShadows.All(p => p.Key == null || p.Key.shadows == p.Value), "Original light shadow settings restored before exiting the diagnostic.");
            moonriseLightingStarted = Time.realtimeSinceStartup;
            moonriseLightingPhase = 6;
            return;
        }
        if (Time.realtimeSinceStartup - moonriseLightingStarted < .4f) return;
        ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_Lighting_Restored.png"));
        File.AppendAllText(path, "Restored " + MoonriseComparisonPose() + "\n");
        Finish(true, "Native counter and render-only shadow comparison captured, original shadow settings restored. Inspect images to diagnose lighting; no visual success or full route claim.");
    }

    private static void CleanupMoonriseLightingProbe()
    {
        foreach (var pair in moonriseLightShadows) if (pair.Key != null) pair.Key.shadows = pair.Value;
        foreach (var pair in moonriseShadowDimmers) if (pair.Key != null) pair.Key.shadowDimmer = pair.Value;
        moonriseShadowDimmers.Clear();
        moonriseLightShadows.Clear();
        moonriseLightingPhase = moonriseLightingFrame = 0;
    }

    private static LitGameplayCameraModeController moonriseComparisonCamera;
    private static LitTacticalCameraProfile moonriseOriginalProfile, moonriseComparisonProfile;
    private static int moonriseComparisonPhase, moonriseComparisonFrame;
    private static float moonriseComparisonStarted;

    private static void BeginMoonriseMaskComparison()
    {
        if (!Environment.GetCommandLineArgs().Contains("-moonriseMaskComparison")) return;
        moonriseComparisonCamera = Camera.main.GetComponent<LitGameplayCameraModeController>();
        if (moonriseComparisonCamera == null || !moonriseComparisonCamera.MaskActive)
            throw new InvalidOperationException("The visual comparison requires the actual active tactical mask.");
        moonriseOriginalProfile = (LitTacticalCameraProfile)typeof(LitGameplayCameraModeController).GetField("profile", Private).GetValue(moonriseComparisonCamera);
        File.WriteAllText(Path.Combine(report.run, "mask-comparison.txt"),
            $"Original radius={moonriseOriginalProfile.maskRadius} {MoonriseComparisonPose()}\n" +
            "Comparison only: a temporary profile clone, no asset write, no pause and no input suppression. Actors may move between frames.\n");
        moonriseComparisonPhase = 1;
        moonriseComparisonFrame = Time.frameCount;
    }

    private static void TickMoonriseMaskComparison()
    {
        if (moonriseComparisonPhase == 0 || Time.frameCount <= moonriseComparisonFrame) return;
        if (moonriseComparisonPhase == 1)
        {
            // CaptureScreenshot is deferred until the end of its frame; the
            // baseline must render before replacing the profile reference.
            moonriseComparisonProfile = Object.Instantiate(moonriseOriginalProfile);
            moonriseComparisonProfile.name = "Moonrise temporary mask comparison";
            moonriseComparisonProfile.hideFlags = HideFlags.DontSave;
            moonriseComparisonProfile.maskRadius = .26f;
            typeof(LitGameplayCameraModeController).GetField("profile", Private).SetValue(moonriseComparisonCamera, moonriseComparisonProfile);
            moonriseComparisonStarted = Time.realtimeSinceStartup;
            moonriseComparisonPhase = 2;
            return;
        }
        if (moonriseComparisonPhase == 2)
        {
            if (Time.realtimeSinceStartup - moonriseComparisonStarted < .3f) return;
            ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_GameView_Mask026.png"));
            File.AppendAllText(Path.Combine(report.run, "mask-comparison.txt"),
                $"Wide radius={moonriseComparisonProfile.maskRadius} {MoonriseComparisonPose()}\n");
            moonriseComparisonFrame = Time.frameCount;
            moonriseComparisonPhase = 3;
            return;
        }
        if (moonriseComparisonPhase == 3)
        {
            moonriseComparisonProfile.adaptiveNearClip = true;
            moonriseComparisonStarted = Time.realtimeSinceStartup;
            moonriseComparisonPhase = 4;
            return;
        }
        if (moonriseComparisonPhase == 4)
        {
            if (Time.realtimeSinceStartup - moonriseComparisonStarted < .5f) return;
            var mask = typeof(LitGameplayCameraModeController).GetField("visibilityMask", Private).GetValue(moonriseComparisonCamera);
            Check((Transform)typeof(LitTacticalVisibilityMask).GetField("enemyTransform", Private).GetValue(mask) == moonriseEnemy.Root,
                "The tactical opening follows the locked Brains body through the common combat target.");
            Check((Transform)typeof(LitGameplayCameraModeController).GetField("clipEnemy", Private).GetValue(moonriseComparisonCamera) == moonriseEnemy.Root &&
                (bool)typeof(LitGameplayCameraModeController).GetField("hasClipEnemyBody", Private).GetValue(moonriseComparisonCamera) &&
                Camera.main.nearClipPlane > moonriseComparisonCamera.InitialNearClip,
                "Adaptive near clip captures and protects the locked Brains physical body while removing close scenery.");
            ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_GameView_Mask026_NearClip.png"));
            File.AppendAllText(Path.Combine(report.run, "mask-comparison.txt"),
                $"Wide and adaptive clip={Camera.main.nearClipPlane} initial={moonriseComparisonCamera.InitialNearClip} {MoonriseComparisonPose()}\n");
            moonriseComparisonFrame = Time.frameCount;
            moonriseComparisonPhase = 5;
            return;
        }
        CleanupMoonriseMaskComparison();
        Check(true, "Visual A/B uses a temporary mask profile and restores the original before the rest of the combat recipe.");
        if (Environment.GetCommandLineArgs().Contains("-moonriseMaskComparisonOnly"))
            Finish(true, "Focused camera A/B capture and profile restoration verified. Death, exploration continuity and 15-20 minute pacing are outside this focused run.");
    }

    private static string MoonriseComparisonPose() =>
        $"frame={Time.frameCount} time={Time.time} camera={Camera.main.transform.position} rotation={Camera.main.transform.eulerAngles} " +
        $"player={LocalPlayerContext.LocalCharacterRoot.position} enemy={moonriseEnemy.Root.position}";

    private static void CleanupMoonriseMaskComparison()
    {
        if (moonriseComparisonCamera != null && moonriseOriginalProfile != null)
            typeof(LitGameplayCameraModeController).GetField("profile", Private).SetValue(moonriseComparisonCamera, moonriseOriginalProfile);
        if (moonriseComparisonProfile != null)
        {
            if (Application.isPlaying) Object.Destroy(moonriseComparisonProfile);
            else Object.DestroyImmediate(moonriseComparisonProfile);
        }
        moonriseComparisonCamera = null;
        moonriseOriginalProfile = moonriseComparisonProfile = null;
        moonriseComparisonPhase = 0;
    }
}
