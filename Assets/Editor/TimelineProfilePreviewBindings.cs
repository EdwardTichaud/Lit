#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Lit.Timeline;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

/// <summary>Temporary editor bindings only; never serializes cross-scene or runtime camera references.</summary>
[InitializeOnLoad]
internal static class TimelineProfilePreviewBindings
{
    private static PlayableDirector director;
    private static GameObject cameraObject;
    private static readonly Dictionary<Object,Object> previous = new();
    private static readonly List<(Transform target,Vector3 position,Quaternion rotation,Vector3 scale,bool active)> poses = new();
    public static bool Active => director != null;
    static TimelineProfilePreviewBindings()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Restore;
        EditorApplication.quitting += Restore;
        EditorApplication.playModeStateChanged += state => { if(state == PlayModeStateChange.ExitingEditMode) Restore(); };
    }
    public static void Prepare(TimelineBindingProfile profile)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        Restore();
        var directors=Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include).Where(d=>d.playableAsset==profile.Timeline).ToArray();
        if(directors.Length!=1){Debug.LogError("Ouvrez la scène du cycle : un unique Director de cette Timeline est requis.");return;}
        var entries=Object.FindObjectsByType<TimelineBindingTarget>(FindObjectsInactive.Include).SelectMany(t=>t.Targets).Where(e=>e!=null&&e.target!=null).ToArray();
        foreach(var b in profile.Bindings)
            if(b.required&&b.bindingId!="camera.cinemachine_brain"&&entries.Count(e=>e.bindingId==b.bindingId)!=1){Debug.LogError("Ouvrez les scènes nécessaires : cible manquante ou dupliquée "+b.bindingId);return;}
        director=directors[0];
        foreach(var b in profile.Bindings){
            previous[b.track]=director.GetGenericBinding(b.track);
            Object target=entries.FirstOrDefault(e=>e.bindingId==b.bindingId)?.target;
            if(b.bindingId=="camera.cinemachine_brain"){
                cameraObject=new GameObject("Timeline_EditorPreviewCamera"){hideFlags=HideFlags.HideAndDontSave};
                cameraObject.AddComponent<Camera>();cameraObject.AddComponent<HDAdditionalCameraData>();target=cameraObject.AddComponent<CinemachineBrain>();
            }
            if(target==null)continue;
            var transform=target is Component c?c.transform:target is GameObject g?g.transform:null;
            if(transform!=null&&transform.gameObject!=cameraObject&&!poses.Any(p=>p.target==transform))poses.Add((transform,transform.localPosition,transform.localRotation,transform.localScale,transform.gameObject.activeSelf));
            director.SetGenericBinding(b.track,target);
        }
        Selection.activeGameObject=director.gameObject;
        UnityEditor.Timeline.TimelineEditor.GetOrCreateWindow().SetTimeline(director);
        Debug.Log("Bindings temporaires prêts : activez Preview dans Timeline et déplacez la tête de lecture. 'Terminer l’aperçu' restaure la scène ; l'aperçu est aussi nettoyé avant sauvegarde et Play Mode.");
    }
    public static void Restore()
    {
        if(director!=null){director.Stop();foreach(var pair in previous)if(pair.Value!=null)director.SetGenericBinding(pair.Key,pair.Value);else director.ClearGenericBinding(pair.Key);}
        foreach(var pose in poses)if(pose.target!=null){pose.target.localPosition=pose.position;pose.target.localRotation=pose.rotation;pose.target.localScale=pose.scale;pose.target.gameObject.SetActive(pose.active);}
        previous.Clear();poses.Clear();director=null;
        if(cameraObject!=null)Object.DestroyImmediate(cameraObject);cameraObject=null;
    }
}
internal sealed class TimelinePreviewSaveGuard : AssetModificationProcessor
{
    private static string[] OnWillSaveAssets(string[] paths){TimelineProfilePreviewBindings.Restore();return paths;}
}
#endif
