using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static partial class ReveLimbesAuthoringSession
{
    private static void PollExistingIntegration()
    {
        string request = Output + "/IntegrateExisting.request";
        if (running || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        File.Delete(request); running = true;
        try
        {
            var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>(ZoneFolder + "/ZoneManifest_Reve_Limbes.asset");
            var house = AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_Maison.asset");
            var access = AssetDatabase.LoadAssetAtPath<SceneAsset>(AccessPath);
            if (manifest == null || house == null || access == null || EditorUtility.IsDirty(house))
                throw new InvalidOperationException("Imported assets and saved Maison manifest are required; preserve unsaved changes.");
            var paths = NewScenes.Concat(new[] { "Reve_Limbes_Cycle_Seuil", "Reve_Limbes_Cycle_Chambre" })
                .Select(name => ZoneFolder + "/" + name + ".unity").Concat(new[] { AccessPath }).ToArray();
            foreach (string path in paths)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) throw new InvalidOperationException("Missing imported scene: " + path);
            if (!RegisterLimbesManifest(manifest))
                throw new InvalidOperationException("Bootstrap has unsaved changes; registration postponed without saving them.");
            if (!house.loadingScenes.Contains(access)) house.loadingScenes.Add(access);
            Set(house,"loadingSceneNames",house.loadingScenes.Select(scene => scene.name).ToArray());
            EditorUtility.SetDirty(house); AssetDatabase.SaveAssetIfDirty(house);
            var builds = EditorBuildSettings.scenes.ToList();
            foreach(string path in paths)
                if (!builds.Any(scene=>scene.path == path)) builds.Add(new EditorBuildSettingsScene(path,true));
            EditorBuildSettings.scenes = builds.ToArray();
            File.WriteAllText(Output+"/IntegrateExisting.result","FINISHED manifest registered in clean Bootstrap; Maison additive portal and seven build entries integrated; no authored environment rebuilt.");
        }
        catch(Exception error) { File.WriteAllText(Output+"/IntegrateExisting.result","FAILED "+error); }
        finally { running = false; }
    }
}
