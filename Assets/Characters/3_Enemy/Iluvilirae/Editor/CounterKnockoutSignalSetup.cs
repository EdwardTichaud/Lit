using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Timeline;

public static class CounterKnockoutSignalSetup
{
    public const string TimelinePath = "Assets/Characters/3_Enemy/Iluvilirae/CombatLab_CounterSkill_2.playable";
    public const string SignalPath = "Assets/Characters/3_Enemy/Iluvilirae/CombatLab_KnockedOut.signal";
    public const string AnimationPath = "Assets/Characters/4_Animations/Mixamo_KnockedOut.anim";
    public const string RigPath = "Assets/Characters/3_Enemy/Iluvilirae/CombatLab_CounterRig.prefab";
    public const string CameraShakeSignalPath = "Assets/Characters/3_Enemy/Iluvilirae/CombatLab_CameraShake.signal";

    [MenuItem("Lit/Combat Lab/Configure Counter CameraShake Signals")]
    public static void ConfigureCameraShake()
    {
        var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
        var signal = AssetDatabase.LoadAssetAtPath<SignalAsset>(CameraShakeSignalPath);
        var markers = timeline.GetRootTracks().OfType<SignalTrack>().SelectMany(t => t.GetMarkers())
            .OfType<SignalEmitter>().Where(m => m.asset == null || signal != null && m.asset == signal).ToArray();
        if (markers.Length != 4) throw new InvalidOperationException("Expected four authored camera shake signals. No markers changed.");
        if (signal == null)
        {
            signal = ScriptableObject.CreateInstance<SignalAsset>();
            AssetDatabase.CreateAsset(signal, CameraShakeSignalPath);
        }
        foreach (var marker in markers)
        {
            marker.asset = signal;
            marker.emitOnce = true;
            marker.retroactive = false;
            EditorUtility.SetDirty(marker);
        }
        EditorUtility.SetDirty(timeline);
        var root = PrefabUtility.LoadPrefabContents(RigPath);
        try
        {
            var rig = root.GetComponent<CombatCinematicRig>();
            var events = root.GetComponent<AnimationEvents>() ?? root.AddComponent<AnimationEvents>();
            var receiver = root.GetComponent<SignalReceiver>();
            var reaction = receiver.GetReaction(signal);
            if (reaction == null) { reaction = new UnityEvent(); receiver.AddReaction(signal, reaction); }
            if (!Enumerable.Range(0, reaction.GetPersistentEventCount()).Any(i =>
                reaction.GetPersistentTarget(i) == events && reaction.GetPersistentMethodName(i) == nameof(AnimationEvents.CameraShake)))
                UnityEventTools.AddPersistentListener(reaction, events.CameraShake);
            foreach (var camera in root.GetComponentsInChildren<Unity.Cinemachine.CinemachineCamera>(true))
                if (camera.GetComponent<CombatCinematicCameraShake>() == null) camera.gameObject.AddComponent<CombatCinematicCameraShake>();
            // The lab has one camera: duplicated Timeline shots need explicit aliases to that camera.
            var cameras = root.GetComponentsInChildren<Unity.Cinemachine.CinemachineCamera>(true);
            if (cameras.Length == 1)
            {
                var data = new SerializedObject(rig);
                var bindings = data.FindProperty("cameraBindings");
                foreach (var shot in timeline.GetRootTracks().OfType<Unity.Cinemachine.CinemachineTrack>()
                    .SelectMany(t => t.GetClips()).Select(c => c.asset).OfType<Unity.Cinemachine.CinemachineShot>())
                {
                    string key = shot.VirtualCamera.exposedName.ToString();
                    if (rig.HasCameraBinding(key)) continue;
                    int index = bindings.arraySize++;
                    bindings.GetArrayElementAtIndex(index).FindPropertyRelative("timelineCameraKey").stringValue = key;
                    bindings.GetArrayElementAtIndex(index).FindPropertyRelative("camera").objectReferenceValue = cameras[0];
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, RigPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("Four CameraShake signals bound at their authored times; camera settings preserved.");
    }

    [MenuItem("Lit/Combat Lab/Configure Counter KnockedOut Signal")]
    public static void Configure()
    {
        var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
        var animation = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationPath);
        if (timeline == null || animation == null) throw new InvalidOperationException("Counter Timeline or Mixamo_KnockedOut missing.");
        var signal = AssetDatabase.LoadAssetAtPath<SignalAsset>(SignalPath);
        if (signal == null)
        {
            signal = ScriptableObject.CreateInstance<SignalAsset>();
            signal.name = "KnockedOut";
            AssetDatabase.CreateAsset(signal, SignalPath);
        }
        var track = timeline.GetRootTracks().OfType<SignalTrack>().FirstOrDefault()
            ?? timeline.CreateTrack<SignalTrack>(null, "Signals");
        var marker = track.GetMarkers().OfType<SignalEmitter>().FirstOrDefault(m => m.asset == signal)
            ?? track.CreateMarker<SignalEmitter>(3.5);
        marker.asset = signal;
        marker.time = 3.5;
        marker.emitOnce = true;
        marker.retroactive = false;
        double end = Math.Max(timeline.duration, 3.5 + 1.0 / timeline.editorSettings.frameRate);
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = end;
        // Keep the counter camera active through the final signal and graph release.
        foreach (var cameraTrack in timeline.GetRootTracks().Where(t => t.GetType().Name == "CinemachineTrack"))
        {
            var shot = cameraTrack.GetClips().LastOrDefault();
            if (shot != null && shot.end < end) shot.duration = end - shot.start;
        }
        EditorUtility.SetDirty(track);
        EditorUtility.SetDirty(timeline);
        string path = RigPath;
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var receiver = root.GetComponent<SignalReceiver>();
            var rig = root.GetComponent<CombatCinematicRig>();
            var events = root.GetComponent<AnimationEvents>() ?? root.AddComponent<AnimationEvents>();
            var reaction = receiver.GetReaction(signal);
            if (reaction == null) { reaction = new UnityEvent(); receiver.AddReaction(signal, reaction); }
            bool bound = Enumerable.Range(0, reaction.GetPersistentEventCount()).Any(i =>
                reaction.GetPersistentTarget(i) == events && reaction.GetPersistentMethodName(i) == nameof(AnimationEvents.KnockedOut));
            if (!bound) UnityEventTools.AddPersistentListener(reaction, events.KnockedOut);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(JuggernautV2Setup.ControllerPath);
        var state = controller.layers[0].stateMachine.states.Single(s => s.state.name == "Knocked Out").state;
        state.motion = animation;
        state.speed = animation.length / 2f;
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("KnockedOut signal configured at 3.50s; Mixamo_KnockedOut bound for a two-second vulnerability window.");
    }
}
