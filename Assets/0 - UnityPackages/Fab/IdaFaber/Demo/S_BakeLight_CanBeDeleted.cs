// This script automatically bakes lighting once, preventing the scene from
// appearing too dark on first load in Unity 6.
//
// You don't need this script if you prefer to bake lighting manually:
// Window > Rendering > Lighting > Generate Lighting
//
// It can safely be removed; it's included only to improve the first-time
// experience for users opening the asset in Unity 6.



using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EzBake.Editor {
    #if UNITY_6000_0_OR_NEWER
    [InitializeOnLoad]
    public class AutoBakeLightingOnce
    {
        static AutoBakeLightingOnce()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            EditorApplication.delayCall += () => CheckAndBakeOnce(scene);
        }

        private static void CheckAndBakeOnce(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            
            string scenePath = scene.path;
            if (string.IsNullOrEmpty(scenePath)) return; 

            bool hasBakedData = LightmapSettings.lightmaps.Length > 0 || Lightmapping.lightingDataAsset != null;

            if (hasBakedData)
            {
                return;
            }

            if (!Lightmapping.isRunning)
            {
                Debug.Log($"[AutoBake] Detected unbaked scene: '{scene.name}'. Triggering 'Generate Lighting'.");
                Lightmapping.Bake(); 
            }
        }
    }
    #endif
}