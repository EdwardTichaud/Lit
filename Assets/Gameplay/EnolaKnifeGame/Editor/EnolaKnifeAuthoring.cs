using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class EnolaKnifeAuthoring
{
    public const string ScenePath = "Assets/Scenes/Fracture_Cycle_Enola.unity";
    public const string ManifestPath = "Assets/Gameplay/EnolaKnifeGame/ZoneManifest_Enola.asset";
    public const string Root = "Library/EnolaKnifeValidation";
    public const string EntranceName = "Entree_Fracture_Enola";
    public const string ExitName = "Sortie_Fracture_Enola";
    static EnolaKnifeAuthoring() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string request = Root + "/Install.request";
        if (!File.Exists(request)) return;
        File.Delete(request);
        try { Install(); File.WriteAllText(Root + "/Install.result", "PASS " + DateTime.UtcNow.ToString("O")); }
        catch (Exception e) { File.WriteAllText(Root + "/Install.result", e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Lit/Enola/Install knife game and access")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save unsaved scenes voluntarily first: " + SceneManager.GetSceneAt(i).name);
        var previous = SceneManager.GetActiveScene(); var opened = new List<Scene>();
        Scene Open(string path)
        {
            var scene = SceneManager.GetSceneByPath(path);
            if (scene.isLoaded) return scene;
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); opened.Add(scene); return scene;
        }
        try
        {
            var home = Open("Assets/Scenes/Maison/Maison_Core.unity");
            var hub = AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_Maison.asset");
            foreach (var asset in hub.loadingScenes) Open(AssetDatabase.GetAssetPath(asset));
            var scene = Open(ScenePath);
            var bootstrap = Open("Assets/Scenes/Bootstrap.unity");
            var stool = scene.GetRootGameObjects().Single(go => go.name == "Stool_Interactive");
            var existingEntrance = home.GetRootGameObjects().FirstOrDefault(go => go.name == EntranceName);
            var homeSpawn = home.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ZoneSpawnPoint>()).First(s => s.SpawnId == "Default");
            Physics.SyncTransforms();
            Vector3 entryFloor = existingEntrance != null ? existingEntrance.transform.position : FindEntryFloor(homeSpawn.transform);
            var reward = LoadReward();
            var manifest = LoadManifest();
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Reve/Completion/Data/LimbesExpansionStone.mat");
            if (stool.GetComponent<EnolaKnifeGame>() == null)
            {
                var game = stool.AddComponent<EnolaKnifeGame>(); game.stoolPoint = stool.transform.Find("StoolPoint"); game.reward = reward;
                game.knifePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ORPH_Tools/Prefab/HDRP_ORPH_ToolsPrefab/HDRP_SM_ORPH_Knife.prefab");
                var action = ConfigureAction(stool, "Jouer avec Enola", .48f);
                UnityEventTools.AddPersistentListener(action.onInteract, game.Begin);
            }
            stool.GetComponent<EnolaKnifeGame>().activityAnimator = LoadActivityAnimator();
            stool.GetComponent<EnolaKnifeGame>().knifeMaterial = wood;
            Vector3 basePoint = stool.transform.position;
            Vector3 arrival = basePoint + new Vector3(-1.1f, .04f, -2.5f);
            if (scene.GetRootGameObjects().All(go => go.name != "Enola_Platform"))
            {
                Primitive(scene, "Enola_Platform", PrimitiveType.Cube, basePoint + new Vector3(0, -.25f, 1), new Vector3(10, .5f, 10), wood);
                // A low rail defines the authored footprint without changing the table or the ghost.
                for (int i = 0; i < 4; i++)
                {
                    var axis = Quaternion.Euler(0, i * 90, 0);
                    var rail = Primitive(scene, "Enola_Rail_" + i, PrimitiveType.Cube, basePoint + new Vector3(0, .45f, 1) + axis * Vector3.forward * 4.94f,
                        new Vector3(10, .9f, .12f), wood); rail.transform.rotation = axis;
                }
                var spawn = New(scene, "Enola_Arrival", arrival); spawn.AddComponent<ZoneSpawnPoint>();
                AddLight(scene, basePoint + new Vector3(-2, 3.4f, -1), new Color(1, .83f, .66f), 1100);
                AddLight(scene, basePoint + new Vector3(2, 3.2f, 3), new Color(.72f, .84f, 1), 850);
            }
            if (scene.GetRootGameObjects().All(go => go.name != ExitName))
            {
                var exit = MakeMarker(scene, ExitName, basePoint + new Vector3(-3, 0, -2), wood);
                var travel = exit.AddComponent<EnolaFractureTravel>();
                UnityEventTools.AddPersistentListener(ConfigureAction(exit, "Retourner à la Maison", .85f).onInteract, travel.Leave);
            }
            if (existingEntrance == null)
            {
                var entry = MakeMarker(home, EntranceName, entryFloor, wood);
                var travel = entry.AddComponent<EnolaFractureTravel>(); travel.destination = manifest; travel.arrivalPosition = arrival; travel.arrivalYaw = 20;
                UnityEventTools.AddPersistentListener(ConfigureAction(entry, "Entrer dans la fracture d'Enola", .85f).onInteract, travel.Enter);
                var returnPoint = New(home, "Enola_ReturnSpawn", homeSpawn.transform.position + Vector3.up * .03f);
                returnPoint.transform.rotation = homeSpawn.transform.rotation;
                var spawn = new SerializedObject(returnPoint.AddComponent<ZoneSpawnPoint>());
                spawn.FindProperty("spawnId").stringValue = travel.returnSpawnId; spawn.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(home); EditorSceneManager.SaveScene(home);
            }
            var flow = bootstrap.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameFlowService>(true)).Single();
            var data = new SerializedObject(flow); var zones = data.FindProperty("zoneManifests");
            if (!Enumerable.Range(0, zones.arraySize).Any(i => zones.GetArrayElementAtIndex(i).objectReferenceValue == manifest))
            {
                int i = zones.arraySize; zones.arraySize++; zones.GetArrayElementAtIndex(i).objectReferenceValue = manifest;
                data.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(bootstrap); EditorSceneManager.SaveScene(bootstrap);
            }
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath && s.enabled))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        finally
        {
            for (int i = opened.Count - 1; i >= 0; i--) if (opened[i].isLoaded) EditorSceneManager.CloseScene(opened[i], true);
            if (previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }
    private static Vector3 FindEntryFloor(Transform spawn)
    {
        var portals = UnityEngine.Object.FindObjectsByType<PortalController>();
        foreach (float distance in new[] { 2.5f, 3.5f, 4.5f })
        foreach (int angle in new[] { 55, -55, 90, -90, 0, 135, -135, 180 })
        {
            Vector3 sample = spawn.position + Quaternion.Euler(0, angle, 0) * spawn.forward * distance;
            if (!Physics.Raycast(sample + Vector3.up * .7f, Vector3.down, out var floor, 1.5f, ~0, QueryTriggerInteraction.Ignore) ||
                floor.normal.y < .85f || Mathf.Abs(floor.point.y - spawn.position.y) > .45f ||
                portals.Any(p => Vector3.Distance(p.GetInteractionAnchor().position, floor.point) < 2.3f) ||
                Physics.CheckCapsule(floor.point + Vector3.up * .35f, floor.point + Vector3.up * 1.7f, .31f, ~0, QueryTriggerInteraction.Ignore)) continue;
            Vector3 delta = floor.point - spawn.position;
            if (Physics.Raycast(spawn.position + Vector3.up, delta.normalized, delta.magnitude - .3f, ~0, QueryTriggerInteraction.Ignore)) continue;
            return floor.point;
        }
        throw new InvalidOperationException("No open physical floor near the Maison spawn for Enola's entry.");
    }
    private static WorldActionInteractable ConfigureAction(GameObject go, string label, float height)
    {
        var collider = go.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.center = Vector3.up * height; collider.size = new Vector3(.9f, 1f, .9f);
        var action = go.AddComponent<WorldActionInteractable>(); action.detectionCollider = collider; action.requiresFlame = false;
        var anchor = new GameObject("InteractionAnchor"); anchor.transform.SetParent(go.transform, false); anchor.transform.localPosition = Vector3.up * height;
        action.interactionAnchor = anchor.transform;
        foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>(true))
            if (renderer.GetComponent<RuntimeOutlineTarget>() == null) renderer.gameObject.AddComponent<RuntimeOutlineTarget>();
        var prompt = go.AddComponent<WorldInteractionPrompt>(); prompt.target = action; prompt.label = label; prompt.worldOffset = Vector3.up * .8f;
        return action;
    }
    private static GameObject MakeMarker(Scene scene, string name, Vector3 position, Material material)
    {
        var root = New(scene, name, position);
        var pedestal = Primitive(scene, "Borne", PrimitiveType.Cylinder, position + Vector3.up * .4f, new Vector3(.48f, .4f, .48f), material);
        pedestal.transform.SetParent(root.transform, true);
        var glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Reve/Lighting/Limbes_Lueur.mat");
        var orb = Primitive(scene, "Signe", PrimitiveType.Sphere, position + Vector3.up * 1.04f, Vector3.one * .22f, glow);
        UnityEngine.Object.DestroyImmediate(orb.GetComponent<Collider>()); orb.transform.SetParent(root.transform, true);
        return root;
    }
    private static GameObject New(Scene scene, string name, Vector3 position)
    {
        var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); go.transform.position = position; return go;
    }
    private static GameObject Primitive(Scene scene, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(type); SceneManager.MoveGameObjectToScene(go, scene); go.name = name;
        go.transform.position = position; go.transform.localScale = scale;
        if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }
    private static void AddLight(Scene scene, Vector3 point, Color color, float intensity)
    {
        var light = New(scene, "Enola_RoomLight", point).AddComponent<Light>(); light.type = LightType.Point;
        light.color = color; light.range = 9; light.intensity = intensity; light.shadows = LightShadows.Soft;
    }
    private static KnowledgeSO LoadReward()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Knowledges")) AssetDatabase.CreateFolder("Assets/Resources", "Knowledges");
        string path = "Assets/Resources/Knowledges/" + EnolaKnifeGame.RewardId + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<KnowledgeSO>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<KnowledgeSO>(); asset.knowledgeId = EnolaKnifeGame.RewardId;
        asset.title = "La cadence d'Enola";
        asset.description = "Enola a appris le jeu du couteau enfant. Elle privilégie une cadence régulière : observer, attendre le bon temps, puis agir sans précipitation.";
        asset.personId = "ghost_enola_vivejoie"; asset.tags.Add("enola.knife");
        AssetDatabase.CreateAsset(asset, path); return asset;
    }
    private static ZoneManifest LoadManifest()
    {
        var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>(ManifestPath);
        if (manifest != null) return manifest;
        manifest = ScriptableObject.CreateInstance<ZoneManifest>();
        var data = new SerializedObject(manifest);
        data.FindProperty("editorSceneReferencesMigrated").boolValue = true;
        data.FindProperty("primaryScene").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        data.FindProperty("primarySceneName").stringValue = "Fracture_Cycle_Enola";
        data.FindProperty("loadingMessage").stringValue = "Rejoindre la fracture d'Enola…";
        data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(manifest, ManifestPath); return manifest;
    }
    private static RuntimeAnimatorController LoadActivityAnimator()
    {
        const string path = "Assets/Gameplay/EnolaKnifeGame/EnolaKnife.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller != null) return controller;
        var source = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Characters/4_Animations/Player_Model.controller");
        // Clone the parameter contract used by UCC/locomotion, but only the five activity states.
        controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        foreach (var parameter in source.parameters) controller.AddParameter(parameter);
        var machine = controller.layers[0].stateMachine;
        foreach (var name in new[] { EnolaKnifeGame.Idle, "AS_Left_HitBetweenThumbIndex", "AS_Left_HitBetweenIndexMiddle",
                     "AS_Left_HitBetweenMiddleRing", "AS_Left_HitBetweenRingPinky" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/TricksUp/FFFAnimPack/Animations/" + name + ".anim");
            if (clip == null) throw new InvalidOperationException("Missing knife animation " + name);
            var state = machine.AddState(name); state.motion = clip;
            if (name == EnolaKnifeGame.Idle) machine.defaultState = state;
        }
        return controller;
    }
}
