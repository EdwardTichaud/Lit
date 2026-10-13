using System.Reflection;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>Native Timeline playback uses the same render-only handoff as gameplay.</summary>
[InitializeOnLoad]
public static class CombatCameraHandoffPreview
{
    private static readonly PropertyInfo TimelineState = typeof(TimelineEditor).GetProperty("state", BindingFlags.Static | BindingFlags.NonPublic);
    private static LitCameraDirector output;
    private static PlayableDirector preview;
    private static bool ownsOutput, wasPlaying;
    private static Vector3 restingPosition;
    private static Quaternion restingRotation;
    private static float restingFov;
    private static double finishingAt;
    private static CombatCinematicRig rig;

    static CombatCameraHandoffPreview()
    {
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += Clear;
        EditorApplication.quitting += Clear;
    }

    private static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Clear(); return; }
        var director = TimelineEditor.inspectedDirector;
        bool relevant = director != null && AssetDatabase.GetAssetPath(director.playableAsset) == CounterKnockoutSignalSetup.TimelinePath;
        if (!relevant || preview != null && preview != director) { Clear(); if (!relevant) return; }
        var state = TimelineState?.GetValue(null);
        bool playing = state != null && (bool)(state.GetType().GetProperty("playing")?.GetValue(state) ?? false);
        if (output == null)
        {
            var lab = Object.FindAnyObjectByType<CombatAnimationLab>();
            if (lab == null || lab.cameraBrain == null) return;
            output = lab.cameraBrain.GetComponent<LitCameraDirector>();
            ownsOutput = output == null;
            if (ownsOutput)
            {
                output = lab.cameraBrain.gameObject.AddComponent<LitCameraDirector>();
                output.hideFlags = HideFlags.HideAndDontSave;
            }
            output.PrepareOutputPreview();
            var restingCamera = output.ControlledCamera;
            if (restingCamera != null)
            {
                restingPosition = restingCamera.transform.position;
                restingRotation = restingCamera.transform.rotation;
                restingFov = restingCamera.fieldOfView;
            }
            preview = director;
            rig = AssetDatabase.LoadAssetAtPath<GameObject>(CounterKnockoutSignalSetup.RigPath)?.GetComponent<CombatCinematicRig>();
        }
        var camera = output.ControlledCamera;
        if (camera == null) { Clear(); return; }
        if (playing && !wasPlaying)
        {
            finishingAt = 0;
            output.StartOutputBlend(rig != null ? rig.CameraEntryBlendSeconds : 0f);
        }
        else if (!playing && wasPlaying)
        {
            float duration = rig != null ? rig.CameraExitBlendSeconds : 0f;
            output.StartOutputBlend(duration);
            finishingAt = EditorApplication.timeSinceStartup + duration;
        }
        if (!playing && finishingAt > 0)
        {
            // Timeline has released its graph. Restore the preview's gameplay
            // pose as the destination, never the output of the interpolation.
            camera.transform.SetPositionAndRotation(restingPosition, restingRotation);
            camera.fieldOfView = restingFov;
            if (EditorApplication.timeSinceStartup >= finishingAt) finishingAt = 0;
        }
        else if (!playing)
        {
            restingPosition = camera.transform.position;
            restingRotation = camera.transform.rotation;
            restingFov = camera.fieldOfView;
        }
        wasPlaying = playing;
    }

    private static void Clear()
    {
        if (output != null)
        {
            output.EndOutputPreview();
            if (ownsOutput) Object.DestroyImmediate(output);
        }
        output = null;
        preview = null;
        ownsOutput = wasPlaying = false;
        finishingAt = 0;
    }
}
