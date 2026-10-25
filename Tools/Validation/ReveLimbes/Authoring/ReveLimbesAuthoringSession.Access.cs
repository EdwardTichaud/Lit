using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static partial class ReveLimbesAuthoringSession
{
    private const string AccessPath = "Assets/Scenes/Maison/Maison_AccesReve.unity";
    private static void PollAccessCreation()
    {
        string request = Output + "/CreateAccess.request";
        if (running || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        File.Delete(request); running = true;
        try { CreateAccess(); }
        catch (Exception error) { File.WriteAllText(Output + "/CreateAccess.result", "FAILED " + error); }
        finally { running = false; }
    }
    private static void CreateAccess()
    {
        if (File.Exists(AccessPath)) throw new InvalidOperationException("Do not regenerate an existing Maison access scene.");
        var limbes = AssetDatabase.LoadAssetAtPath<ZoneManifest>(ZoneFolder + "/ZoneManifest_Reve_Limbes.asset");
        var house = AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_Maison.asset");
        if (limbes == null || house == null || EditorUtility.IsDirty(house)) throw new InvalidOperationException("Saved manifests required; preserve unsaved asset edits.");
        var original = SceneManager.GetActiveScene();
        var opened = new List<Scene>(); Scene access = default, interactive = default;
        bool openedInteractive = false; NavMeshDataInstance inspectionNavigation = default;
        try
        {
            var paths = EditorBuildSettings.scenes.ToDictionary(scene => Path.GetFileNameWithoutExtension(scene.path), scene => scene.path);
            var houseNames = new[] { house.PrimarySceneName }.Concat(house.LoadingSceneNames).Concat(house.PostLoadingSceneNames).Distinct().ToArray();
            foreach (string name in houseNames)
            {
                Scene scene = SceneManager.GetSceneByName(name);
                if (scene.IsValid() && scene.isLoaded)
                {
                    if (scene.isDirty) throw new InvalidOperationException("Preserve unsaved Maison scene: " + scene.path);
                }
                else opened.Add(EditorSceneManager.OpenScene(paths[name], OpenSceneMode.Additive));
            }
            Physics.SyncTransforms();
            var spawn = ZoneSpawnPoint.FindFirstInScene(house.PrimarySceneName);
            if (spawn == null) throw new InvalidOperationException("Maison has no authored arrival.");
            if (house.BakedNavMeshData != null) inspectionNavigation = NavMesh.AddNavMeshData(house.BakedNavMeshData);
            Vector3 portalPoint = FindHousePortalPoint(spawn.transform.position, houseNames, inspectionNavigation.valid);
            var arrivals = ZoneSpawnPoint.FindAll("Default").Where(point => point.gameObject.scene.name == house.PrimarySceneName).OrderBy(point => point.PartySlot).Select(point => new Pose(point.transform.position, point.transform.rotation)).ToArray();
            if (arrivals.Length == 0) arrivals = new[] { new Pose(spawn.transform.position, spawn.transform.rotation) };
            access = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); access.name = "Maison_AccesReve";
            if (!EditorSceneManager.SaveScene(access, AccessPath)) throw new IOException("Could not establish the owned access scene path.");
            var entryRoot = Root(access, "Acces_aux_Limbes", portalPoint);
            var limbesArrivals = Enumerable.Range(0, 4).Select(index => new Pose(Origin + new Vector3((index - 1.5f) * 1.1f, .1f, -2), Quaternion.identity)).ToArray();
            CreatePortal(entryRoot.transform, "Portail_des_Limbes", Vector3.zero, limbes, limbesArrivals);
            if (!EditorSceneManager.SaveScene(access, AccessPath)) throw new IOException("Could not save the new Maison portal.");
            AssetDatabase.ImportAsset(AccessPath, ImportAssetOptions.ForceSynchronousImport);

            string interactivePath = ZoneFolder + "/Reve_Limbes_Interactives.unity";
            interactive = SceneManager.GetSceneByPath(interactivePath);
            if (interactive.IsValid() && interactive.isLoaded && interactive.isDirty) throw new InvalidOperationException("Preserve unsaved Limbes interactions.");
            if (!interactive.IsValid() || !interactive.isLoaded) { interactive = EditorSceneManager.OpenScene(interactivePath, OpenSceneMode.Additive); openedInteractive = true; }
            var root = interactive.GetRootGameObjects().Single(go => go.name == "Limbes_Interactions");
            CreatePortal(root.transform, "Retour_Maison_Seuil", new Vector3(0, 0, -4.3f), house, arrivals);
            CreatePortal(root.transform, "Retour_Maison_Rive", Islands[6] + new Vector3(0, 0, 2), house, arrivals);
            Save(interactive);
            house.loadingScenes.Add(AssetDatabase.LoadAssetAtPath<SceneAsset>(AccessPath));
            Set(house, "loadingSceneNames", house.loadingScenes.Select(scene => scene.name).ToArray());
            EditorUtility.SetDirty(house); AssetDatabase.SaveAssetIfDirty(house);
            if (!EditorBuildSettings.scenes.Any(scene => scene.path == AccessPath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(AccessPath, true) }).ToArray();
            bool registered = RegisterLimbesManifest(limbes);
            File.WriteAllText(Output + "/CreateAccess.result", "FINISHED portal=" + portalPoint + "; returns=2; existing Maison placements preserved; Bootstrap registration=" + registered +
                (registered ? "" : "; register the manifest after voluntarily saving Bootstrap; do not test save continuation before registration."));
        }
        finally
        {
            if (inspectionNavigation.valid) inspectionNavigation.Remove();
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (access.IsValid() && access.isLoaded) EditorSceneManager.CloseScene(access, true);
            if (openedInteractive && interactive.IsValid() && interactive.isLoaded) EditorSceneManager.CloseScene(interactive, true);
            foreach (var scene in opened.AsEnumerable().Reverse()) if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }
    private static Vector3 FindHousePortalPoint(Vector3 spawn, string[] houseScenes, bool hasNavigation)
    {
        foreach (var offset in new[] { new Vector3(3.5f,0,0), new Vector3(-3.5f,0,0), new Vector3(0,0,-4), new Vector3(4,0,3), new Vector3(-4,0,3) })
        foreach (var hit in Physics.RaycastAll(spawn + offset + Vector3.up * 3, Vector3.down, 7, ~0, QueryTriggerInteraction.Ignore).OrderBy(hit => hit.distance))
        {
            if (hit.normal.y < .8f || !houseScenes.Contains(hit.collider.gameObject.scene.name)) continue;
            Vector3 p = hit.point;
            if (Physics.OverlapCapsule(p + Vector3.up * .5f, p + Vector3.up * 1.8f, .4f, ~0, QueryTriggerInteraction.Ignore).Length != 0) continue;
            if (hasNavigation)
            {
                var path = new NavMeshPath();
                if (!NavMesh.SamplePosition(spawn, out var a, 3, NavMesh.AllAreas) || !NavMesh.SamplePosition(p, out var b, 1, NavMesh.AllAreas) ||
                    !NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            }
            return p;
        }
        throw new InvalidOperationException("No clear, physically grounded and reachable portal point near Maison arrival. Place it manually instead of moving existing decor.");
    }
    private static void CreatePortal(Transform parent, string name, Vector3 position, ZoneManifest zone, Pose[] arrivals)
    {
        if (parent.Find(name) != null) throw new InvalidOperationException("Existing portal must not be duplicated: " + name);
        var go = Child(parent, name, position);
        Module("Doorway_Smooth_01 Variant", "Encadrement_du_songe", go.transform, new Vector3(0, 3.5f, 0), new Vector3(0, 90, 0), 1.2f, false);
        var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.center = Vector3.up; box.size = new Vector3(2.2f, 2.8f, 1);
        var portal = go.AddComponent<PortalController>();
        var data = new SerializedObject(portal); data.FindProperty("destinationMode").enumValueIndex = 1;
        data.FindProperty("destinationZone").objectReferenceValue = zone; data.FindProperty("interactionCollider").objectReferenceValue = box;
        data.FindProperty("interactionMaxDistance").floatValue = 2;
        var destinations = data.FindProperty("destinationPoints"); destinations.arraySize = arrivals.Length;
        for (int i = 0; i < arrivals.Length; i++)
        {
            var point = Child(go.transform, "Arrivee_" + i, Vector3.zero); point.transform.SetPositionAndRotation(arrivals[i].position, arrivals[i].rotation);
            destinations.GetArrayElementAtIndex(i).objectReferenceValue = point.transform;
        }
        data.ApplyModifiedPropertiesWithoutUndo(); go.AddComponent<RuntimeOutlineTarget>();
        var veil = GameObject.CreatePrimitive(PrimitiveType.Sphere); veil.name = "Lueur_du_passage"; veil.transform.SetParent(go.transform, false);
        veil.transform.localPosition = new Vector3(0, 1.1f, 0); veil.transform.localScale = new Vector3(1.4f, 2.2f, .08f);
        UnityEngine.Object.DestroyImmediate(veil.GetComponent<Collider>());
        veil.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ZoneFolder + "/Lighting/Limbes_Lueur.mat");
    }
    private static bool RegisterLimbesManifest(ZoneManifest manifest)
    {
        const string path = "Assets/Scenes/Bootstrap.unity";
        Scene scene = SceneManager.GetSceneByPath(path); bool opened = false;
        if (scene.IsValid() && scene.isLoaded && scene.isDirty) return false;
        if (!scene.IsValid() || !scene.isLoaded) { scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); opened = true; }
        try
        {
            if (scene.isDirty) return false;
            var flow = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameFlowService>(true)).Single();
            var data = new SerializedObject(flow); var zones = data.FindProperty("zoneManifests");
            for (int i = 0; i < zones.arraySize; i++) if (zones.GetArrayElementAtIndex(i).objectReferenceValue == manifest) return true;
            zones.InsertArrayElementAtIndex(zones.arraySize); zones.GetArrayElementAtIndex(zones.arraySize - 1).objectReferenceValue = manifest;
            data.ApplyModifiedPropertiesWithoutUndo();
            // Only our registration in a previously clean Bootstrap is saved.
            // A dirty user's scene is refused above, never auto-saved.
            return EditorSceneManager.SaveScene(scene, path);
        }
        finally { if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
    }
}
