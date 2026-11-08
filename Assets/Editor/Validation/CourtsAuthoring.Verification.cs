using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lit.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static partial class CourtsAuthoring
{
    [MenuItem("Tools/Lit/Zone 2/Finalize Courts safety and navigation")]
    public static void FinalizeSafety()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene.");
        var setup = SavedSetup();
        try
        {
            var core = EditorSceneManager.OpenScene(CorePath, OpenSceneMode.Single);
            var world = EditorSceneManager.OpenScene(Folder + "/District_2_Courts_Environment.unity", OpenSceneMode.Additive);
            var manifest = Load<ZoneManifest>(Folder + "/ZoneManifest_District_2_Courts.asset");
            manifest.primaryScene = Load<SceneAsset>(CorePath);
            manifest.loadingScenes = new List<SceneAsset> { Load<SceneAsset>(world.path) };
            Set(manifest, "editorSceneReferencesMigrated", true);
            typeof(ZoneManifest).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(manifest, null);
            EditorUtility.SetDirty(manifest);
            foreach (var portal in core.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PortalController>(true))) BindPortalSpawns(portal);
            var progression = core.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CourtsProgression>(true)).Single();
            foreach (string name in new[] { "Acces_commun_a_retablir", "Retour_des_visites_a_ouvrir" })
            {
                var candidate = core.GetRootGameObjects().Concat(world.GetRootGameObjects()).SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Single(t => t.name == name);
                if (candidate.gameObject.scene != core)
                {
                    candidate.SetParent(null, true); SceneManager.MoveGameObjectToScene(candidate.gameObject, core); candidate.SetParent(progression.transform, true);
                }
                if (name.StartsWith("Acces")) progression.commonAccessBarrier = candidate.gameObject; else progression.visitsBarrier = candidate.gameObject;
            }
            // Static navigation includes the route that opens later; barriers retain their runtime collisions.
            var navPath = Folder + "/Data/CourtsNavigation.asset";
            progression.commonAccessBarrier.SetActive(false); progression.visitsBarrier.SetActive(false);
            var host = new GameObject("__Courts_NavigationBake"); SceneManager.MoveGameObjectToScene(host, world);
            try
            {
                var surface = host.AddComponent<Unity.AI.Navigation.NavMeshSurface>(); surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.collectObjects = Unity.AI.Navigation.CollectObjects.Volume; surface.center = Origin + new Vector3(0, 5, 44); surface.size = new Vector3(80, 40, 120);
                surface.BuildNavMesh(); if (surface.navMeshData == null) throw new InvalidOperationException("No navigation.");
                var existing = Load<NavMeshData>(navPath); EditorUtility.CopySerialized(surface.navMeshData, existing); EditorUtility.SetDirty(existing);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); progression.commonAccessBarrier.SetActive(true); progression.visitsBarrier.SetActive(true); }
            if (progression.GetComponent<LimbesFallRecovery>() == null)
            {
                var recovery = progression.gameObject.AddComponent<LimbesFallRecovery>(); recovery.voidHeight = -8;
                var volume = Child(progression.transform, "Volume_de_participation", new Vector3(0, 2, 44)).AddComponent<BoxCollider>(); volume.isTrigger = true; volume.size = new Vector3(80, 30, 120); recovery.participationVolume = volume;
                recovery.landings = new[] {
                    Landing(progression.transform, "arrivee", new Vector3(0, .1f, 3), new Vector3(22, 4, 22)),
                    Landing(progression.transform, "galerie", new Vector3(0, 6.1f, 49), new Vector3(42, 4, 10)),
                    Landing(progression.transform, "cour_finale", new Vector3(0, .1f, 74), new Vector3(26, 4, 22))
                };
            }
            SceneManager.SetActiveScene(world); RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.35f, .38f, .45f);
            EditorSceneManager.SaveScene(core); EditorSceneManager.SaveScene(world); AssetDatabase.SaveAssets();
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        Verify();
    }
    private static LimbesFallRecovery.SafeLanding Landing(Transform root, string id, Vector3 point, Vector3 size)
    {
        var go = Child(root, "Sol_sur_" + id, point); var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.center = Vector3.up; box.size = size;
        return new LimbesFallRecovery.SafeLanding { id = id, arrival = go.transform, volume = box };
    }
    private static void BindPortalSpawns(PortalController portal)
    {
        if (portal.DestinationPoints.Count > 0) return;
        var so = new SerializedObject(portal); var destination = (ZoneManifest)so.FindProperty("destinationZone").objectReferenceValue;
        string path = AssetDatabase.GetAssetPath(destination.primaryScene);
        var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var points = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ZoneSpawnPoint>(true)).OrderBy(p => p.PartySlot).ToArray();
            if (points.Length == 0) throw new InvalidOperationException("No authored spawns in " + path);
            var targets = so.FindProperty("destinationPoints"); targets.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++)
            {
                var anchor = Child(portal.transform, "Destination_" + i, Vector3.zero).transform;
                anchor.SetPositionAndRotation(points[i].transform.position, points[i].transform.rotation);
                targets.GetArrayElementAtIndex(i).objectReferenceValue = anchor;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    [MenuItem("Tools/Lit/Zone 2/Verify saved Courts")]
    public static void Verify()
    {
        var setup = SavedSetup(); var checks = new List<string>();
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks.Add(message); }
        try
        {
            var manifest = Load<ZoneManifest>(Folder + "/ZoneManifest_District_2_Courts.asset");
            Check(manifest.PrimarySceneName == "District_2_Courts" && manifest.LoadingSceneNames.Contains("District_2_Courts_Environment"), "Core and saved environment in manifest");
            var core = EditorSceneManager.OpenScene(CorePath, OpenSceneMode.Single);
            var world = EditorSceneManager.OpenScene(Folder + "/District_2_Courts_Environment.unity", OpenSceneMode.Additive);
            var roots = core.GetRootGameObjects().Concat(world.GetRootGameObjects()).ToArray();
            T[] All<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
            Check(All<Transform>().All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "No missing scripts");
            var controllers = All<CycleController>(); Check(controllers.Length == 5, "Five local cycles");
            foreach (var controller in controllers)
            {
                Check(controller.definition != null && controller.definition.ValidateConfiguration().Count == 0, "Valid cycle " + controller.name);
                Check(controller.definition.prerequisites.conditions.Length == 0 && controller.definition.steps.All(s => s.prerequisites.conditions.All(c => c.kind != CycleRequirementKind.CycleCompleted)), "Independent local definition " + controller.name);
            }
            var runners = All<StorySequenceRunner>(); Check(runners.Length == 6, "Six authored presentations");
            foreach (var runner in runners) Check(runner.Sequence != null && runner.Sequence.TryValidate(out _) && runner.Sequence.playOnce, "Valid playOnce presentation " + runner.name);
            var notes = All<InteractableItem>(); Check(notes.Length == 9, "Nine family and local investigation documents");
            Physics.SyncTransforms();
            foreach (var note in notes)
            {
                var item = note.representedItem;
                Check(item != null && item.IsReadable() && item.knowledgeUnlockedOnRead.Count == 1 && item.knowledgeUnlockedOnPickup.Count == 0, "Read-only knowledge " + note.name);
                Check(Physics.RaycastAll(note.transform.position + Vector3.up, Vector3.down, 2, ~0, QueryTriggerInteraction.Ignore).Any(h => h.collider.gameObject.scene == world && h.normal.y > .6f), "Physical floor below " + note.name);
                Check(ItemRegistryItemExists(item), "Session catalog contains " + item.itemId);
            }
            var progression = All<CourtsProgression>().Single();
            Check(progression.commonAccessBarrier != null && progression.visitsBarrier != null && progression.nextZonePortal != null, "Access references survive serialization");
            Check(progression.commonAccessBarrier.scene == core && progression.visitsBarrier.scene == core, "No cross-scene serialized references");
            Check(All<ZoneSpawnPoint>().Length == 4 && All<LimbesFallRecovery>().Single().landings.Length == 3, "Party spawns and fall recovery");
            Check(All<PortalController>().All(p => p.DestinationPoints.Count > 0), "Portals have explicit authored destination poses");
            Check(All<SceneMarker>().Any(m => m.BakedCharacterInstance != null && m.BakedCharacterInstance.GetComponent<CourtsGargoyleGate>() != null), "Gargoyle uses standard persistent marker");
            Check(All<CourtsGargoyleGate>().Single().GetComponent<LitBrainsEnemy>() != null && All<CourtsGargoyleGate>().Single().GetComponent<EnemyController>() == null, "Single Brains combat owner");
            Check(All<Flame>().Length == 6, "Six reused ordinary Flames");
            Check(manifest.BakedNavMeshData != null, "Baked navigation present");
            Check(new[] { CorePath, world.path }.All(p => EditorBuildSettings.scenes.Any(s => s.enabled && s.path == p)), "Scenes enabled in build");
            Directory.CreateDirectory("Library/Zone2Courts"); File.WriteAllLines("Library/Zone2Courts/AuthoringVerification.txt", checks);
            Debug.Log("[CourtsVerification] PASS: " + checks.Count + " authoring checks. Does not prove runtime traversal.");
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }
    private static bool ItemRegistryItemExists(Item item)
    {
        var prefab = Load<GameObject>("Assets/Core/System/GameplaySessionRoot.prefab");
        return prefab.GetComponentsInChildren<ItemRegistry>(true).Any(r => r.items.Contains(item)) && prefab.GetComponentsInChildren<CharacterStateStore>(true).Any(r => r.allItems.Contains(item));
    }
    private static SceneSetup[] SavedSetup()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve unsaved scene: " + SceneManager.GetSceneAt(i).name);
        if (SceneManager.sceneCount == 0 || string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            EditorSceneManager.OpenScene(CorePath, OpenSceneMode.Single);
        return EditorSceneManager.GetSceneManagerSetup();
    }
}
