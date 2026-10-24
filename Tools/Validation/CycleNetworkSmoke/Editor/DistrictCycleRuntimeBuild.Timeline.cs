using System.Linq;
using Lit.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public static partial class DistrictCycleRuntimeBuild
{
    private static void CreateTimelineFixture()
    {
        const string timelinePath = "Assets/Resources/CycleTimelineFixture.playable";
        var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
        if (timeline == null) { timeline = ScriptableObject.CreateInstance<TimelineAsset>(); AssetDatabase.CreateAsset(timeline, timelinePath); }
        foreach (var previous in timeline.GetRootTracks().ToArray()) timeline.DeleteTrack(previous);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resources/CycleTimelineFixture.anim");
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, "Assets/Resources/CycleTimelineFixture.anim"); }
        clip.ClearCurves();
        clip.SetCurve("", typeof(TimelineDissolveTarget), "strength", AnimationCurve.Linear(0, 0, 1, 1));
        var track = timeline.CreateTrack<AnimationTrack>(null, "Real frame dissolution");
        var timelineClip = track.CreateClip<AnimationPlayableAsset>(); timelineClip.start = 0; timelineClip.duration = 1;
        ((AnimationPlayableAsset)timelineClip.asset).clip = clip;
        var profile = AssetDatabase.LoadAssetAtPath<TimelineBindingProfile>("Assets/Resources/CycleTimelineFixtureProfile.asset");
        if (profile == null) { profile = ScriptableObject.CreateInstance<TimelineBindingProfile>(); AssetDatabase.CreateAsset(profile, "Assets/Resources/CycleTimelineFixtureProfile.asset"); }
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("timeline").objectReferenceValue = timeline;
        serialized.FindProperty("bindings").arraySize = 0; serialized.ApplyModifiedPropertiesWithoutUndo();
        var root = new GameObject("Standalone native Timeline fixture");
        var director = root.AddComponent<PlayableDirector>(); director.playableAsset = timeline;
        director.playOnAwake = false; director.extrapolationMode = DirectorWrapMode.None;
        director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube); visual.transform.SetParent(root.transform, false);
        var animationRoot = new GameObject("Presentation animation"); animationRoot.transform.SetParent(root.transform, false);
        var animator = animationRoot.AddComponent<Animator>();
        var effect = animationRoot.AddComponent<TimelineDissolveTarget>();
        var effectData = new SerializedObject(effect);
        effectData.FindProperty("visualRoot").objectReferenceValue = visual.transform; effectData.ApplyModifiedPropertiesWithoutUndo();
        director.SetGenericBinding(track, animator);
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/CycleTimelineFixture.prefab");
        Object.DestroyImmediate(root);
        EditorUtility.SetDirty(timeline); EditorUtility.SetDirty(clip); EditorUtility.SetDirty(profile);
    }
}
