using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class ReveLimbesAuthoringSession
{
    private static void PollPreview()
    {
        string request=Output+"/Preview.request";
        if(running || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        File.Delete(request); running=true;
        var opened=new List<Scene>(); Scene original=SceneManager.GetActiveScene(); GameObject cameraObject=null; RenderTexture render=null; Texture2D pixels=null;
        try
        {
            Directory.CreateDirectory(Output);
            var manifest=AssetDatabase.LoadAssetAtPath<ZoneManifest>(ZoneFolder+"/ZoneManifest_Reve_Limbes.asset");
            if(manifest == null) throw new InvalidOperationException("Import manifest first.");
            foreach(string name in new[]{manifest.PrimarySceneName}.Concat(manifest.LoadingSceneNames))
            {
                string path=ZoneFolder+"/"+name+".unity";
                var scene=SceneManager.GetSceneByPath(path);
                if(scene.IsValid() && scene.isLoaded) { if(scene.isDirty) throw new InvalidOperationException("Preserve dirty scene: "+path); }
                else opened.Add(EditorSceneManager.OpenScene(path,OpenSceneMode.Additive));
            }
            var report=new List<string>{"project="+Application.dataPath,"pid="+System.Diagnostics.Process.GetCurrentProcess().Id,"workingDirectory="+Environment.CurrentDirectory,
                "pipeline="+GraphicsSettings.currentRenderPipeline?.name+"; quality="+QualitySettings.GetQualityLevel()+"; type="+GraphicsSettings.currentRenderPipeline?.GetType().FullName,
                "enabledBuildScenes="+string.Join(",",EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path))};
            var roots=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s=>s.name.StartsWith("Reve_Limbes",StringComparison.Ordinal)).SelectMany(s=>s.GetRootGameObjects()).ToArray();
            int missing=roots.Sum(root=>root.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
            report.Add("missingScripts="+missing); if(missing!=0) throw new InvalidOperationException("Missing scripts in imported production scenes.");
            report.Add("packPrefabInstances="+roots.SelectMany(root=>root.GetComponentsInChildren<Transform>(true)).Count(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).StartsWith(Pack,StringComparison.Ordinal)));
            report.AddRange(roots.SelectMany(root=>root.GetComponentsInChildren<Renderer>(true)).SelectMany(renderer=>renderer.sharedMaterials)
                .Select(material=>material==null?"material=NULL":"material="+material.name+" | "+material.shader?.name+" | "+AssetDatabase.GetAssetPath(material)).Distinct());
            cameraObject=new GameObject("Owned_Limbes_Render_Preview"); cameraObject.hideFlags=HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(cameraObject,SceneManager.GetSceneByName("Reve_Limbes"));
            var camera=cameraObject.AddComponent<Camera>(); camera.enabled=false; camera.fieldOfView=65; camera.nearClipPlane=.1f; camera.farClipPlane=700; camera.clearFlags=CameraClearFlags.Skybox;
            var hd=cameraObject.AddComponent<HDAdditionalCameraData>(); hd.volumeLayerMask=~0; hd.volumeAnchorOverride=camera.transform; hd.clearColorMode=HDAdditionalCameraData.ClearColorMode.Sky;
            render=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32); render.Create();
            pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);
            var positions=new[]{new Vector3(0,3,-8),new Vector3(38,38,-18),new Vector3(-43,23,35)};
            var targets=new[]{new Vector3(0,7,48),new Vector3(0,10,45),new Vector3(8,12,57)};
            Physics.SyncTransforms();
            for(int i=0;i<positions.Length;i++)
            {
                camera.transform.position=Origin+positions[i]; camera.transform.LookAt(Origin+targets[i]);
                VolumeManager.instance.Update(camera.transform,~0);
                if(i==0) report.Add("skyType="+VolumeManager.instance.stack.GetComponent<VisualEnvironment>().skyType.value+"; hdri="+VolumeManager.instance.stack.GetComponent<HDRISky>().hdriSky.value?.name);
                var renderRequest=new RenderPipeline.StandardRequest{destination=render};
                if(!RenderPipeline.SupportsRenderRequest(camera,renderRequest)) throw new InvalidOperationException("Current pipeline cannot render preview.");
                for(int warm=0;warm<3;warm++) RenderPipeline.SubmitRenderRequest(camera,renderRequest);
                var previous=RenderTexture.active;
                try { RenderTexture.active=render; pixels.ReadPixels(new Rect(0,0,1440,900),0,0); pixels.Apply(); File.WriteAllBytes(Output+"/Preview_"+i+".png",pixels.EncodeToPNG()); }
                finally { RenderTexture.active=previous; }
            }
            File.WriteAllLines(Output+"/Preview.result",new[]{"FINISHED native production materials preview; not a gameplay test."}.Concat(report));
        }
        catch(Exception error) { File.WriteAllText(Output+"/Preview.result","FAILED "+error); }
        finally
        {
            if(cameraObject!=null) Object.DestroyImmediate(cameraObject);
            if(pixels!=null) Object.DestroyImmediate(pixels);
            if(render!=null) {render.Release(); Object.DestroyImmediate(render);}
            if(original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            foreach(var scene in opened.AsEnumerable().Reverse()) if(scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene,true);
            running=false;
        }
    }
}
