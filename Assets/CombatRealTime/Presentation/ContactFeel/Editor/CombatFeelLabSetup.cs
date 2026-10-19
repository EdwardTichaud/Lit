using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public static class CombatFeelLabSetup
{
    public const string CameraPath = "Assets/Characters/3_Enemy/Iluvilirae/CombatFeelCamera.asset";

    [MenuItem("Lit/Combat Lab/Configure Action Camera")]
    public static void InstallActionCamera()
    {
        Install();
        var camera = AssetDatabase.LoadAssetAtPath<LitTacticalCameraProfile>(CameraPath);
        if (camera.actionFraming == null || !camera.actionFraming.enabled)
        {
            var exploration = AssetDatabase.LoadAssetAtPath<LitTacticalCameraProfile>("Assets/CombatRealTime/Camera/TacticalDefault.asset");
            camera.initialDistance = exploration.initialDistance;
            camera.initialPitch = exploration.initialPitch;
            camera.fieldOfView = exploration.fieldOfView;
            camera.actionFraming = new LitTacticalActionFraming { enabled = true };
            EditorUtility.SetDirty(camera);
            AssetDatabase.SaveAssets();
        }
        VerifyInstalled();
    }

    public static void VerifyInstalled()
    {
        var paths = new[] { IluviliraeSetup.ScenePath, ChargedSkillSetup.EnemyPath, GroundImpactSkillSetup.PrefabPath, CameraPath };
        var before = paths.Select(System.IO.File.ReadAllBytes).ToArray();
        Install();
        for (int i = 0; i < paths.Length; i++)
            if (!before[i].SequenceEqual(System.IO.File.ReadAllBytes(paths[i])))
                throw new InvalidOperationException("Repeated contact installation rewrote " + paths[i]);
        Debug.Log("[CombatFeel] PASS | repeated installation leaves scene, enemy and camera bytes unchanged.");
    }

    [MenuItem("Lit/Combat Lab/Configure Contact Feel")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring.");
        if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
            .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Save open scenes before configuring the laboratory.");
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
            var arena = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LucianBrainsCombatArena>(true)).Single();
            string path = AssetDatabase.GetAssetPath(arena.enemyPrefab);
            if (!path.StartsWith(ChargedSkillSetup.Folder + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("Contact presentation must remain on a laboratory prefab: " + path);
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var brain = prefab.GetComponent<LitBrainsEnemy>();
                if (brain == null) throw new InvalidOperationException("Lab enemy has no Brains actor.");
                if (prefab.GetComponent<CombatContactPose>() == null)
                {
                    prefab.AddComponent<CombatContactPose>();
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                var animator = prefab.GetComponent<Animator>();
                Debug.Log("[CombatFeel] Enemy=" + path + " | bone colliders=" +
                    string.Join(", ", animator.GetComponentsInChildren<Collider>(true)
                        .Where(c => c.transform != prefab.transform).Select(c => c.name)));
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            string playerPath = AssetDatabase.GetAssetPath(arena.lucianPrefab);
            if (playerPath != GroundImpactSkillSetup.PrefabPath)
                throw new InvalidOperationException("Unexpected laboratory Lucian prefab: " + playerPath);
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                if (player.GetComponent<CombatContactAudio>() == null)
                {
                    player.AddComponent<CombatContactAudio>();
                    PrefabUtility.SaveAsPrefabAsset(player, playerPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            if (!scene.GetRootGameObjects().Any(r => r.name == "CombatFeelFill"))
            {
                var fill = new GameObject("CombatFeelFill", typeof(Light));
                var light = fill.GetComponent<Light>();
                light.type = LightType.Directional;
                light.shadows = LightShadows.None;
                fill.AddComponent<HDAdditionalLightData>();
                light.color = new Color(.9f, .94f, 1);
                light.intensity = 4000;
                fill.transform.rotation = Quaternion.Euler(35, 150, 0);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            var cameraMode = arena.arenaCamera.GetComponent<LitGameplayCameraModeController>();
            if (cameraMode == null) throw new InvalidOperationException("Lab camera mode missing.");
            var serialized = new SerializedObject(cameraMode);
            var profile = AssetDatabase.LoadAssetAtPath<LitTacticalCameraProfile>(CameraPath);
            if (profile == null)
            {
                var source = serialized.FindProperty("profile").objectReferenceValue as LitTacticalCameraProfile;
                if (source == null) throw new InvalidOperationException("Existing tactical profile missing.");
                profile = UnityEngine.Object.Instantiate(source);
                profile.name = "CombatFeelCamera";
                profile.initialDistance = 7.5f;
                profile.initialPitch = 38;
                profile.fieldOfView = 58;
                AssetDatabase.CreateAsset(profile, CameraPath);
            }
            if (serialized.FindProperty("profile").objectReferenceValue != profile)
            {
                serialized.FindProperty("profile").objectReferenceValue = profile;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
            }
            if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[CombatFeel] Installed lab-only torso response and camera profile. ChargedSkill unchanged.");
        }
        finally
        {
            if (previous.Any(s => s.isLoaded) && previous.Count(s => s.isActive) == 1)
                EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }
}
