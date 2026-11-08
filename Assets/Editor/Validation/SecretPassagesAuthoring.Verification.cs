using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class SecretPassagesAuthoring
{
    public static void RefineEndpointSpacing()
    {
        var setup = Setup();
        try
        {
            var scene = EditorSceneManager.OpenScene(CourtsScene, OpenSceneMode.Single);
            foreach (var discovery in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<KnowledgePassageDiscovery>(true)))
            {
                int index = int.Parse(discovery.definition.passageId.Split('.').Last()) - 1;
                Vector3 a = CourtsAuthoring.Origin + CourtsStarts()[index], b = CourtsAuthoring.Origin + CourtsEnds()[index];
                var narrow = discovery.GetComponent<TrouEtroit>(); narrow.transform.position = a;
                narrow.teleportPointA.position = a; narrow.teleportPointB.position = b;
                var frames = narrow.transform.Cast<Transform>().Where(t => t.name == "Ouverture_etroite").ToArray();
                frames[0].position = a + Vector3.up * .8f; frames[1].position = b + Vector3.up * .8f;
                if (index < 2)
                {
                    string text = index == 0 ? "Cours intérieures : au coin extérieur de la maison Mirage, une ouverture étroite rejoint la galerie haute du même côté." :
                        "Cours intérieures : au coin extérieur de l'atelier, une ouverture rejoint sa galerie haute. Elle évite la montée ordinaire.";
                    var definition = discovery.definition; definition.knowledge.description = text;
                    definition.note.description = definition.note.parchmentText = text + "\n\nCes annotations ne recensent pas tous les passages du château.";
                    EditorUtility.SetDirty(definition.knowledge); EditorUtility.SetDirty(definition.note);
                }
            }
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        Verify();
    }
    [MenuItem("Tools/Lit/Zone 2/Audit saved secret content")]
    public static void Verify()
    {
        var setup = Setup(); var checks = new List<string>();
        void Check(bool success, string message) { if (!success) throw new InvalidOperationException(message); checks.Add(message); }
        try
        {
            var catalog = Load<SecretPassageCatalog>(CatalogPath);
            Check(catalog.entries.Length == 20 && catalog.entries.Select(e => e.noteId).Distinct().Count() == 20, "Twenty unique planned note identities");
            Check(catalog.entries.Count(e => e.reserved) == 10 && catalog.entries.Where(e => e.reserved).All(e => e.definition == null), "Ten future reservations without invented content");
            Check(catalog.entries[1].definition != null && !catalog.entries[1].reserved, "Second District passage authored after user approval");
            Check(catalog.notesCycle.steps.Length == 20 && catalog.notesCycle.steps.All(s => s.kind == CycleStepKind.Read && s.terminal), "Global cycle counts twenty Read facts, never knowledge");
            Check(catalog.notesCycle.ValidateConfiguration().Count == 0, "Valid independent global cycle");
            var paths = new[] { CourtsAuthoring.CorePath, CourtsAuthoring.Folder + "/District_2_Courts_Environment.unity", CourtsScene,
                "Assets/Scenes/Reve/Reve_Limbes.unity", "Assets/Scenes/Reve/Reve_Limbes_Environment.unity", "Assets/Scenes/Reve/Reve_Limbes_Progression.unity", LimbesScene, DistrictNotesScene, D1Scene,
                "Assets/Scenes/District_1/District_1_Rooms_Environment.unity", "Assets/Scenes/District_1/District_1_Corridor_Environment.unity" }
                .Concat(Load<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_District_1.asset").loadingScenes.Select(AssetDatabase.GetAssetPath)).Distinct().ToArray();
            var scenes = new List<Scene>();
            foreach (string path in paths) scenes.Add(EditorSceneManager.OpenScene(path, scenes.Count == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive));
            var roots = scenes.SelectMany(s => s.GetRootGameObjects()).ToArray();
            var discoveries = roots.SelectMany(r => r.GetComponentsInChildren<KnowledgePassageDiscovery>(true)).ToArray();
            Check(discoveries.Length == 10, "Ten physical passages: two District, six Courts, two Limbes");
            Physics.SyncTransforms();
            foreach (var discovery in discoveries)
            {
                var entry = catalog.entries.Single(e => e.definition == discovery.definition);
                var narrow = discovery.GetComponent<TrouEtroit>();
                Check(discovery.definition.HasValidIdentity && discovery.definition.HasReadLinkedNote, "Read-only note grants passage knowledge " + entry.number);
                Check(discovery.firstRecognition != null && discovery.firstRecognition.Sequence.playOnce && discovery.firstRecognition.Sequence.TryValidate(out _), "Authored first-recognition dialogue " + entry.number);
                Check(narrow.teleportPointA != null && narrow.teleportPointB != null, "Two authored endpoints " + entry.number);
                var note = roots.SelectMany(r => r.GetComponentsInChildren<InteractableItem>(true)).Single(n => n.representedItem == entry.definition.note);
                Check(note.gameObject.scene.name == entry.noteScene, "Correct cross-zone note placement " + entry.number);
                Check(!narrow.IsInsideInteractionVolume(note.transform.position), "Note outside its own passage " + entry.number);
                if (entry.number > 1)
                    foreach (var point in new[] { narrow.teleportPointA, narrow.teleportPointB })
                        Check(Physics.RaycastAll(point.position + Vector3.up * .5f, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore)
                            .Any(h => h.normal.y > .6f && h.collider.GetComponentInParent<TrouEtroit>() == null), "Solid landing " + entry.number + " " + point.name);
                if (entry.noteScene != "District_1_SecretNotes")
                    Check(Physics.RaycastAll(note.transform.position + Vector3.up * .3f, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore).Any(h => h.normal.y > .6f), "Solid floor under note " + entry.number);
            }
            var legacy = discoveries.Single(d => d.definition == catalog.entries[0].definition);
            Check(NetcodeSceneIdUtility.GetStableId(legacy.transform) == 1734355439, "Legacy network identity preserved exactly");
            Check(catalog.entries.Count(e => e.definition != null && e.noteScene == "District_2_Courts_Secrets") == 6, "Six notes in Courts including District and Limbes leads");
            Check(catalog.entries.Count(e => e.definition != null && e.noteScene == "Reve_Limbes_Secrets") == 3, "Three Limbes notes including return lead to District 1");
            var onward = roots.SelectMany(r => r.GetComponentsInChildren<PortalController>(true)).Single(p => p.name == "Acces_libre_aux_Cours_interieures");
            var onwardData = new SerializedObject(onward);
            Check(onward.isActiveAndEnabled && onward.IsSceneTransition && onward.DestinationPoints.Count == 4 &&
                (ZoneManifest)onwardData.FindProperty("destinationZone").objectReferenceValue == Load<ZoneManifest>(CourtsAuthoring.Folder + "/ZoneManifest_District_2_Courts.asset"), "Free District-to-Courts portal and four authored arrival points");
            var bootstrap = EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity", OpenSceneMode.Additive);
            var flow = bootstrap.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameFlowService>(true)).Single();
            var zones = new SerializedObject(flow).FindProperty("zoneManifests");
            var district2 = Load<ZoneManifest>(CourtsAuthoring.Folder + "/ZoneManifest_District_2_Courts.asset");
            Check(Enumerable.Range(0, zones.arraySize).Count(i => zones.GetArrayElementAtIndex(i).objectReferenceValue == district2) == 1,
                "District_2 registered exactly once in Bootstrap for save resume");
            Check(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))
                .Intersect(district2.LoadingSceneNames.Concat(new[] { district2.PrimarySceneName })).Count() == district2.LoadingSceneNames.Count + 1,
                "All District_2 required scenes enabled in Build Settings");
            Check(MainMenuSaveCatalog.SceneLabel("District_1_Core") == "District 1 — Moonrise" &&
                MainMenuSaveCatalog.SceneLabel("District_2_Courts") == "District 2 — Stillhaven" &&
                MainMenuSaveCatalog.SceneLabel("District_20_Core") == "District 20", "District names and unrelated district labels");
            Directory.CreateDirectory("Library/Zone2Secrets");
            File.WriteAllLines("Library/Zone2Secrets/AuthoringVerification.txt", checks.Concat(new[] { "PENDING: complete District runtime due pre-existing missing prefabs, multiplayer, artistic finish." }));
            Debug.Log("[SecretPassagesVerification] PASS " + checks.Count + " checks on ten authored passages and free campaign entrance.");
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }
}
