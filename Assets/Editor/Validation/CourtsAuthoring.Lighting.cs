using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class CourtsAuthoring
{
    [MenuItem("Tools/Lit/Zone 2/Install reused dream lighting")]
    public static void InstallLighting()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        var setup = SavedSetup();
        try
        {
            var world = EditorSceneManager.OpenScene(Folder + "/District_2_Courts_Environment.unity", OpenSceneMode.Single);
            if (world.GetRootGameObjects().Any(r => r.name == "Eclairage_HDRP_des_cours")) return;
            var previous = world.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)).Single(l => l.name == "Lumiere_des_cours");
            Object.DestroyImmediate(previous.gameObject);
            var source = EditorSceneManager.OpenScene("Assets/Scenes/Reve/Reve_Limbes_Lighting.unity", OpenSceneMode.Additive);
            try
            {
                var original = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Volume>(true)).Single();
                var host = new GameObject("Eclairage_HDRP_des_cours"); SceneManager.MoveGameObjectToScene(host, world); host.transform.position = Origin;
                var box = host.AddComponent<BoxCollider>(); EditorUtility.CopySerialized(original.GetComponent<BoxCollider>(), box);
                var volume = host.AddComponent<Volume>(); EditorUtility.CopySerialized(original, volume);
                var light = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LitContrastLight>(true)).Single(l => l.name == "Lumiere_du_vide");
                var clone = Object.Instantiate(light.gameObject); SceneManager.MoveGameObjectToScene(clone, world);
                clone.name = "Lumiere_diffuse_reemployee"; clone.transform.SetParent(host.transform, false);
                clone.transform.localPosition = light.transform.localPosition; clone.transform.localRotation = light.transform.localRotation;
                clone.GetComponent<LitContrastLight>().ApplySettings();
                // Only the existing local Volume and its directional light are reused: no vortex or Limbes props.
                EditorSceneManager.SaveScene(world);
            }
            finally { EditorSceneManager.CloseScene(source, true); }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }
}
