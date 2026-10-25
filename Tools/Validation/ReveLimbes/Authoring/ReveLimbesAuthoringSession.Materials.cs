using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public static partial class ReveLimbesAuthoringSession
{
    private static void PollMaterialAdaptation()
    {
        string request=Output+"/AdaptMaterials.request";
        if(running || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        File.Delete(request); running=true; Scene original=SceneManager.GetActiveScene(); var opened=new List<Scene>();
        try
        {
            EnsureFolder(ZoneFolder+"/Materials"); var copies=new Dictionary<Material,Material>(); int changed=0;
            foreach(string path in Directory.GetFiles(ZoneFolder,"*.unity"))
            {
                var scene=SceneManager.GetSceneByPath(path); bool close=false;
                if(scene.IsValid() && scene.isLoaded) { if(scene.isDirty) throw new InvalidOperationException("Preserve dirty scene: "+path); }
                else {scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive); opened.Add(scene); close=true;}
                bool sceneChanged=false;
                foreach(var renderer in scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Renderer>(true)))
                {
                    var materials=renderer.sharedMaterials; bool rendererChanged=false;
                    bool packInstance=false;
                    for(var parent=renderer.transform;parent!=null;parent=parent.parent)
                        if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(parent.gameObject).StartsWith(Pack,StringComparison.Ordinal)) {packInstance=true;break;}
                    for(int i=0;i<materials.Length;i++)
                    {
                        Material source=materials[i];
                        if(source==null || source.shader==null || source.shader.name=="Hidden/InternalErrorShader" ||
                           packInstance && !AssetDatabase.GetAssetPath(source).StartsWith("Assets/",StringComparison.Ordinal))
                        {
                            // Some pack variants refer to material GUIDs that
                            // are absent from the supplied pack. Fill only
                            // our instance's missing slot, never the source.
                            var fallback=AssetDatabase.LoadAssetAtPath<Material>(ZoneFolder+"/Materials/Limbes_8b7dd6368a526594488b05eb05723a5e.mat");
                            if(packInstance && fallback!=null) {materials[i]=fallback;rendererChanged=true;}
                            continue;
                        }
                        string sourcePath=AssetDatabase.GetAssetPath(source);
                        if(!sourcePath.StartsWith("Assets/0 - UnityPackages/Fab/ModularDungeonCollection/",StringComparison.Ordinal)) continue;
                        if(!copies.TryGetValue(source,out Material local))
                        {
                            string target=ZoneFolder+"/Materials/Limbes_"+AssetDatabase.AssetPathToGUID(sourcePath)+".mat";
                            local=AssetDatabase.LoadAssetAtPath<Material>(target);
                            if(local==null)
                            {
                                local=new Material(Shader.Find("HDRP/Lit")); local.name="Limbes_"+source.name;
                                Texture texture(params string[] names)
                                {
                                    var data=new SerializedObject(source); var entries=data.FindProperty("m_SavedProperties.m_TexEnvs");
                                    foreach(string name in names) for(int entry=0;entry<entries.arraySize;entry++)
                                    {
                                        var e=entries.GetArrayElementAtIndex(entry);
                                        if(e.FindPropertyRelative("first").stringValue==name && e.FindPropertyRelative("second.m_Texture").objectReferenceValue is Texture value) return value;
                                    }
                                    return null;
                                }
                                local.SetColor("_BaseColor",Color.white); local.SetFloat("_Metallic",0); local.SetFloat("_Smoothness",.3f);
                                local.SetTexture("_BaseColorMap",texture("_BaseColorMap","_MainTex","_BaseMap","_AlbedoMap"));
                                local.SetTexture("_NormalMap",texture("_NormalMap","_BumpMap","_Normals"));
                                local.SetFloat("_DoubleSidedEnable",1); local.SetFloat("_DoubleSidedNormalMode",2);
                                HDMaterial.ValidateMaterial(local); AssetDatabase.CreateAsset(local,target);
                            }
                            copies.Add(source,local);
                        }
                        materials[i]=local; rendererChanged=true;
                    }
                    if(rendererChanged) {renderer.sharedMaterials=materials; PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); sceneChanged=true; changed++;}
                }
                if(sceneChanged) {EditorSceneManager.MarkSceneDirty(scene); Save(scene);}
            }
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{ZoneFolder+"/Materials"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid); string filename=Path.GetFileNameWithoutExtension(path);
                if(!filename.StartsWith("Limbes_",StringComparison.Ordinal)) continue;
                var source=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(filename.Substring(7)));
                if(source!=null) copies[source]=AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{ZoneFolder+"/Cycles"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid); var contents=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool modified=false;
                    foreach(var renderer in contents.GetComponentsInChildren<Renderer>(true))
                    {
                        var materials=renderer.sharedMaterials; bool different=false;
                        for(int i=0;i<materials.Length;i++) if(materials[i]!=null && copies.TryGetValue(materials[i],out var local)) {materials[i]=local; different=true;}
                        if(different) {renderer.sharedMaterials=materials; PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); modified=true;}
                    }
                    if(modified) PrefabUtility.SaveAsPrefabAsset(contents,path);
                }
                finally {PrefabUtility.UnloadPrefabContents(contents);}
            }
            File.WriteAllText(Output+"/AdaptMaterials.result","FINISHED local HDRP materials="+copies.Count+"; renderers="+changed+"; owned collectible prefabs adapted; source pack assets untouched.");
        }
        catch(Exception error) {File.WriteAllText(Output+"/AdaptMaterials.result","FAILED "+error);}
        finally
        {
            if(original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            foreach(var scene in opened.AsEnumerable().Reverse()) if(scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene,true);
            running=false;
        }
    }
}
