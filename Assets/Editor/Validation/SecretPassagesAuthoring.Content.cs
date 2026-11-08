using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lit.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class SecretPassagesAuthoring
{
    internal const string CatalogPath = "Assets/Resources/Narrative/SecretPassageCatalog.asset";
    internal const string CourtsScene = "Assets/Scenes/District_2/Courts/District_2_Courts_Secrets.unity";
    internal const string LimbesScene = "Assets/Scenes/Reve/Reve_Limbes_Secrets.unity";
    internal const string DistrictNotesScene = "Assets/Scenes/District_1/District_1_SecretNotes.unity";
    private static readonly List<Item> newItems = new();
    private static Material stone;

    [MenuItem("Tools/Lit/Zone 2/Install approved secret passages")]
    public static void Install()
    {
        var setup = Setup();
        if (File.Exists(CatalogPath) || File.Exists(CourtsScene)) throw new InvalidOperationException("Content already exists; edit it, do not regenerate.");
        EnsureFolder(Folder + "/Data"); EnsureFolder(Folder + "/Prefabs"); EnsureFolder(Folder + "/Sequences");
        newItems.Clear(); stone = Load<Material>("Assets/Scenes/Reve/Completion/Data/LimbesExpansionStone.mat");
        try
        {
            var cycle = ScriptableObject.CreateInstance<CycleDefinition>(); cycle.cycleId = "global.secret_notes";
            cycle.title = "Les vingt notes des passages"; cycle.category = CycleCategory.Side;
            cycle.description = "Retrouver et lire vingt notes dispersées. Les annotations connues ne prétendent pas recenser tous les chemins du château.";
            cycle.playCinematicAfterDefeat = false;
            cycle.steps = Enumerable.Range(1, 20).Select(n => new CycleStep { id = "note_" + n.ToString("00"), title = "Lire la note " + n,
                kind = CycleStepKind.Read, sourceId = NoteId(n), terminal = true }).ToArray();
            AssetDatabase.CreateAsset(cycle, "Assets/Resources/Narrative/SecretNotesCycle.asset");
            var catalog = ScriptableObject.CreateInstance<SecretPassageCatalog>(); catalog.notesCycle = cycle;
            catalog.entries = Enumerable.Range(1, 20).Select(n => new SecretPassageCatalog.Entry { number = n, noteId = NoteId(n), reserved = n > 10 }).ToArray();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            var courts = NewScene(CourtsScene); var courtsRoot = Root(courts, "Passages_et_notes_des_Cours");
            var limbes = NewScene(LimbesScene); var limbesRoot = Root(limbes, "Passages_et_notes_des_Limbes");
            var district = NewScene(DistrictNotesScene); var districtRoot = Root(district, "Notes_croisees_District_1");
            var d1 = EditorSceneManager.OpenScene(D1Scene, OpenSceneMode.Additive);
            var old = d1.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TrouEtroit>(true)).ToArray();
            if (old.Length != 1) throw new InvalidOperationException("District inspection changed; review allocation before migrating.");
            uint originalId = NetcodeSceneIdUtility.GetStableId(old[0].transform);
            var d1Definition = Define(catalog, 1, "legacy." + originalId, "District 1 — le couloir ouest",
                "Dans le couloir ouest du District 1, une grille étroite relie deux portions du passage de maintenance.",
                old[0].gameObject.scene.name, courts.name);
            PlaceNote(courtsRoot, d1Definition.note, CourtsAuthoring.Origin + new Vector3(6, .5f, 7));
            Bind(old[0], d1Definition, Recognition(old[0].transform, 1, "Une ouverture dans la grille. Elle relie les deux portions du couloir ouest."));
            if (NetcodeSceneIdUtility.GetStableId(old[0].transform) != originalId) throw new InvalidOperationException("Legacy passage identity changed.");
            EditorSceneManager.SaveScene(d1);
            // Slot 2 remains planned (not reserved for a future zone) until the one-versus-two District discrepancy is resolved.
            catalog.entries[1].passageScene = d1.name; catalog.entries[1].noteScene = limbes.name;
            var origin = CourtsAuthoring.Origin;
            var starts = CourtsStarts(); var ends = CourtsEnds();
            string[] titles = { "Maison et galerie", "Atelier et galerie", "D'une maison à l'autre", "La cour et la maison", "La cour et l'atelier", "Le recoin de la réserve" };
            string[] texts = {
                "Cours intérieures : au coin extérieur de la maison Mirage, une ouverture étroite rejoint la galerie haute du même côté.",
                "Cours intérieures : au coin extérieur de l'atelier, une ouverture rejoint sa galerie haute. Elle évite la montée ordinaire.",
                "Cours intérieures : deux ouvertures au fond des maisons se font face. Le passage étroit relie la maison Mirage à l'atelier.",
                "Cours intérieures : au bord gauche de la cour d'arrivée, une ouverture mène au fond de la maison Mirage.",
                "Cours intérieures : au bord droit de la cour d'arrivée, une ouverture rejoint le fond de l'atelier.",
                "Cours intérieures : près du mur extérieur de la réserve, un passage étroit donne sur un recoin à l'écart."
            };
            Vector3[] localNotes = { new(-5, .5f, 8), new(-18, .5f, 20), new(-10, 6.5f, 49), new(21, .5f, 24) };
            for (int i = 0; i < 6; i++)
            {
                int n = i + 3;
                var definition = Define(catalog, n, "courts." + (i + 1), "Cours — " + titles[i], texts[i], courts.name,
                    i < 4 ? courts.name : i == 4 ? district.name : limbes.name);
                Passage(courtsRoot, definition, origin + starts[i], origin + ends[i], "Ce chemin relie " + titles[i].ToLowerInvariant() + ".");
                if (i < 4) PlaceNote(courtsRoot, definition.note, origin + localNotes[i]);
            }
            var room = Root(courts, "Recoin_facultatif_de_la_reserve");
            Block(room, "Sol", origin + new Vector3(38, -.25f, 27), new Vector3(8, .5f, 8), true);
            Block(room, "Mur_du_fond", origin + new Vector3(42, 1.5f, 27), new Vector3(.4f, 3, 8), true);
            var episode = Readable("courts.optional.visit", "Une visite sans panier",
                "Je ne suis pas passée pour porter quelque chose aujourd'hui. Je voulais simplement rester un moment avec Abel. — Alix", null);
            var episodeKnowledge = Knowledge("courts.optional.visit", episode.itemName, episode.description,
                Load<CycleDefinition>("Assets/Resources/Narrative/Courts_familyCycle.asset"));
            episodeKnowledge.readableItem = episode; episode.knowledgeUnlockedOnRead.Add(episodeKnowledge);
            PlaceNote(room, episode, origin + new Vector3(39, .5f, 29)); EditorUtility.SetDirty(episodeKnowledge); EditorUtility.SetDirty(episode);
            var baseOrigin = new Vector3(600, 120, 0);
            var a = Define(catalog, 9, "limbes.1", "Limbes — vers la terrasse gauche",
                "Limbes : près du seuil d'arrivée, une ouverture étroite rejoint la terrasse haute de gauche, au bout de la première branche.", limbes.name, courts.name);
            Passage(limbesRoot, a, baseOrigin + new Vector3(-1.2f, .1f, -4), baseOrigin + new Vector3(-43, 10.1f, 36), "Cette ouverture rejoint la terrasse de gauche.");
            PlaceNote(courtsRoot, a.note, origin + new Vector3(12, 6.5f, 49));
            var b = Define(catalog, 10, "limbes.2", "Limbes — vers la terrasse droite",
                "Limbes : près du seuil d'arrivée, une seconde ouverture étroite rejoint la terrasse haute de droite, au bout de la deuxième branche.", limbes.name, limbes.name);
            Passage(limbesRoot, b, baseOrigin + new Vector3(1.2f, .1f, -4), baseOrigin + new Vector3(45, 16.1f, 57), "Cette ouverture rejoint la terrasse de droite.");
            PlaceNote(limbesRoot, b.note, baseOrigin + new Vector3(1, .5f, 1));
            PlaceNote(limbesRoot, catalog.entries[7].definition.note, baseOrigin + new Vector3(-1, .5f, 2));
            var core = EditorSceneManager.OpenScene("Assets/Scenes/District_1/District_1_Core.unity", OpenSceneMode.Additive);
            var spawn = core.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ZoneSpawnPoint>(true)).OrderBy(p => p.PartySlot).First();
            PlaceNote(districtRoot, catalog.entries[6].definition.note, spawn.transform.position + new Vector3(1.3f, .4f, 0));
            EditorSceneManager.CloseScene(core, true);
            foreach (var scene in new[] { courts, limbes, district }) EditorSceneManager.SaveScene(scene);
            RegisterScene(courts, Load<ZoneManifest>(CourtsAuthoring.Folder + "/ZoneManifest_District_2_Courts.asset"));
            RegisterScene(limbes, Load<ZoneManifest>("Assets/Scenes/Reve/ZoneManifest_Reve_Limbes.asset"));
            RegisterScene(district, Load<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_District_1.asset"));
            RegisterItems(); EditorUtility.SetDirty(catalog); EditorUtility.SetDirty(cycle); AssetDatabase.SaveAssets();
            Debug.Log("[SecretPassages] Installed six Courts, two Limbes, one migrated District passage; District slot 2 unresolved; ten future reservations untouched.");
        }
        finally { newItems.Clear(); EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }

    private static string NoteId(int number) => "secret.note." + number.ToString("00");
    private static Vector3[] CourtsStarts() => new[] { new Vector3(-26, .05f, 19), new Vector3(26, .05f, 19), new Vector3(-14, .05f, 35),
        new Vector3(-9, .05f, 7), new Vector3(9, .05f, 11), new Vector3(26, .05f, 27) };
    private static Vector3[] CourtsEnds() => new[] { new Vector3(-20, 6.05f, 49), new Vector3(20, 6.05f, 49), new Vector3(14, .05f, 35),
        new Vector3(-24, .05f, 31), new Vector3(24, .05f, 35), new Vector3(37, .05f, 25) };
    private static SecretPassageDefinition Define(SecretPassageCatalog catalog, int n, string id, string title, string text, string destination, string source)
    {
        var knowledge = Knowledge("secret.passage." + n.ToString("00"), title, text, catalog.notesCycle);
        var definition = ScriptableObject.CreateInstance<SecretPassageDefinition>(); definition.passageId = id; definition.knowledge = knowledge;
        definition.note = Readable(NoteId(n), "Note — " + title, text + "\n\nCes annotations ne recensent pas tous les passages du château.", knowledge);
        AssetDatabase.CreateAsset(definition, Folder + "/Data/Passage_" + n.ToString("00") + ".asset");
        knowledge.readableItem = definition.note; EditorUtility.SetDirty(knowledge);
        var entry = catalog.entries[n - 1]; entry.definition = definition; entry.noteScene = source; entry.passageScene = destination;
        return definition;
    }
    private static KnowledgeSO Knowledge(string id, string title, string text, CycleDefinition cycle)
    {
        var knowledge = ScriptableObject.CreateInstance<KnowledgeSO>(); knowledge.knowledgeId = id; knowledge.title = title; knowledge.description = text;
        knowledge.cycles.Add(cycle); AssetDatabase.CreateAsset(knowledge, Folder + "/Data/Knowledge_" + id + ".asset"); Set(knowledge, "uniqueId", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(knowledge))); return knowledge;
    }
    private static Item Readable(string id, string title, string text, KnowledgeSO knowledge)
    {
        var note = Object.Instantiate(Load<Item>("Assets/Scenes/Reve/Cycles/Seuil/Data/Item_0.asset"));
        note.itemId = id; note.readableContentId = id; note.itemName = title; note.description = note.parchmentText = text;
        note.knowledgeUnlockedOnPickup = new List<KnowledgeSO>(); note.knowledgeUnlockedOnRead = knowledge != null ? new List<KnowledgeSO> { knowledge } : new List<KnowledgeSO>(); note.worldPrefab = null;
        AssetDatabase.CreateAsset(note, Folder + "/Data/Item_" + id + ".asset"); newItems.Add(note); return note;
    }
    private static void PlaceNote(Transform root, Item item, Vector3 point)
    {
        var go = Child(root, "Note_" + item.itemId, point);
        var visual = Block(go.transform, "Papier", point + Vector3.up * .02f, new Vector3(.6f, .08f, .4f), false);
        visual.GetComponent<Renderer>().sharedMaterial = Load<Material>("Assets/Scenes/Reve/Lighting/Limbes_Lueur.mat");
        var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(1, 1, 1);
        var readable = go.AddComponent<InteractableItem>(); readable.interactableCategory = InteractableItem.InteractableCategory.RecoverableItem;
        readable.representedItem = item; readable.storedItems = new List<InteractableItem.LootItemEntry> { new() { item = item, quantity = 1 } };
        readable.allowTake = true; readable.interactionMaxDistance = 2; readable.interactionTrigger = box;
        // Secrets' paper can be consulted in ambient light; only enemy activation is universally Flame-gated.
        Set(readable, "requireLitInfluenceForInteraction", false); Set(readable, "reactToFlameInfluence", false); go.AddComponent<RuntimeOutlineTarget>();
        string path = Folder + "/Prefabs/Note_" + item.itemId + ".prefab";
        item.worldPrefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        var contents = PrefabUtility.LoadPrefabContents(path);
        try { contents.transform.position = Vector3.zero; PrefabUtility.SaveAsPrefabAsset(contents, path); }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        EditorUtility.SetDirty(item);
    }
    private static void Passage(Transform root, SecretPassageDefinition definition, Vector3 a, Vector3 b, string line)
    {
        var go = Child(root, "Passage_" + definition.passageId, a);
        var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.center = Vector3.up; box.size = new Vector3(2.4f, 2, 2.4f);
        var narrow = go.AddComponent<TrouEtroit>(); narrow.keepOriginalHeight = false;
        narrow.observateurAbility = Load<AbilitySO>("Assets/Resources/Abilities/Ability_Observateur.asset");
        narrow.saufConduitAbility = Load<AbilitySO>("Assets/Resources/Abilities/Ability_SaufConduit.asset");
        narrow.teleportPointA = Child(go.transform, "Endpoint_A", a).transform;
        narrow.teleportPointB = Child(go.transform, "Endpoint_B", b).transform;
        var other = narrow.teleportPointB.gameObject.AddComponent<BoxCollider>(); other.isTrigger = true; other.center = Vector3.up; other.size = box.size;
        narrow.additionalTriggers = new Collider[] { other };
        foreach (Vector3 point in new[] { a, b })
        {
            var frame = Block(go.transform, "Ouverture_etroite", point + Vector3.up * .8f, new Vector3(.8f, 1.6f, .25f), false);
            frame.AddComponent<RuntimeOutlineTarget>();
        }
        Bind(narrow, definition, Recognition(go.transform, int.Parse(definition.knowledge.knowledgeId.Split('.').Last()), line));
    }
    private static void Bind(TrouEtroit narrow, SecretPassageDefinition definition, StorySequenceRunner recognition)
    {
        var discovery = narrow.gameObject.AddComponent<KnowledgePassageDiscovery>(); discovery.definition = definition; discovery.firstRecognition = recognition;
    }
    private static StorySequenceRunner Recognition(Transform root, int number, string line)
    {
        string id = "secret.recognition." + number.ToString("00");
        var asset = ScriptableObject.CreateInstance<StorySequenceAsset>(); asset.sequenceId = id; asset.displayName = "Reconnaître le passage " + number; asset.playOnce = true;
        asset.startFromBlack = false; asset.fadeToBlackBeforeGameplay = false; asset.fadeFromBlackAfterGameplayRestore = false;
        var voice = ScriptableObject.CreateInstance<VoiceLineData>(); voice.voiceLineText = line;
        AssetDatabase.CreateAsset(voice, Folder + "/Sequences/" + id + "_Line.asset");
        asset.steps.Add(new StorySequenceStep { type = StorySequenceStepType.Dialogue, actorId = "Lucian", speakerNameOverride = "Lucian", voiceLine = voice, skippable = true, focusCameraOnSpeaker = false });
        AssetDatabase.CreateAsset(asset, Folder + "/Sequences/" + id + ".asset");
        if (!asset.TryValidate(out string issue)) throw new InvalidOperationException(issue);
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/Narrative/StorySequence/Prefabs/StorySequenceRig.prefab"), root.gameObject.scene);
        PrefabUtility.UnpackPrefabInstance(rig, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        rig.name = "Reconnaissance_" + number; rig.transform.SetParent(root, false);
        var runner = rig.GetComponent<StorySequenceRunner>(); Set(runner, "sequence", asset); Set(runner, "playOnStart", false); return runner;
    }
    private static void RegisterScene(Scene scene, ZoneManifest manifest)
    {
        if (!manifest.loadingScenes.Any(s => AssetDatabase.GetAssetPath(s) == scene.path)) manifest.loadingScenes.Add(Load<SceneAsset>(scene.path));
        typeof(ZoneManifest).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(manifest, null); EditorUtility.SetDirty(manifest);
        if (!EditorBuildSettings.scenes.Any(s => s.path == scene.path)) EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(scene.path, true) }).ToArray();
    }
    private static void RegisterItems()
    {
        const string path = "Assets/Core/System/GameplaySessionRoot.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var registry in root.GetComponentsInChildren<ItemRegistry>(true)) foreach (var item in newItems) if (!registry.items.Contains(item)) registry.items.Add(item);
            foreach (var store in root.GetComponentsInChildren<CharacterStateStore>(true)) foreach (var item in newItems) if (!store.allItems.Contains(item)) store.allItems.Add(item);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static Scene NewScene(string path)
    {
        if (File.Exists(path)) throw new InvalidOperationException("Refusing to overwrite " + path);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); EditorSceneManager.SaveScene(scene, path); return scene;
    }
    private static Transform Root(Scene scene, string name)
    {
        var root = new GameObject(name); SceneManager.MoveGameObjectToScene(root, scene); return root.transform;
    }
    private static GameObject Child(Transform root, string name, Vector3 worldPoint)
    {
        var go = new GameObject(name); go.transform.SetParent(root, false); go.transform.position = worldPoint; return go;
    }
    private static GameObject Block(Transform root, string name, Vector3 point, Vector3 size, bool collision)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(root, false); go.transform.position = point; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = stone; if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }
    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing source " + path);
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    private static void Set(Object target, string field, object value)
    {
        var so = new SerializedObject(target); var p = so.FindProperty(field) ?? throw new InvalidOperationException(field);
        if (value is bool b) p.boolValue = b; else if (value is string text) p.stringValue = text; else if (value is Object asset) p.objectReferenceValue = asset; else throw new InvalidOperationException(field);
        so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
    }
}
