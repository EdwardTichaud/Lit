using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class NinaCycleSetup
{
    public const string ScenePath = "Assets/Scenes/District_1/District_1_Enigme_Ghost_Nina.unity";
    private const string DataPath = "Assets/Narrative/NinaCycle/Data";
    static NinaCycleSetup() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Library/NinaCycle.request")) return;
        File.Delete("Library/NinaCycle.request");
        try { Create(); File.WriteAllText("Library/NinaCycle.result", "Created and validated " + ScenePath); }
        catch (Exception e) { File.WriteAllText("Library/NinaCycle.result", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Lit/Narrative/Create Nina Cycle Scene")]
    public static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("La scène Nina existe déjà. Elle ne sera pas écrasée.");
        EnsureFolder(DataPath);
        EnsureFolder("Assets/Resources/Narrative");
        var definition = Asset<NinaCycleDefinition>("Assets/Resources/Narrative/NinaCycle.asset");
        definition.existence = Knowledge("ExistenceDesChimeres", "Existence des chimères", "Des êtres vivants ont été fusionnés artificiellement par le Scientifique fou.");
        definition.dilemma = Knowledge("DilemmeEdouard", "Dilemme Édouard", "Édouard a découvert que Nina était issue de la fusion d'une enfant et d'un chien. Comment lui venir en aide sans lui infliger davantage de souffrance ?");
        var letter = Asset<Item>(DataPath + "/Item_Edward.asset");
        letter.itemId = "item_edward";
        letter.itemName = "Lettre manuscrite d'Édouard";
        letter.readableKind = Item.ReadableKind.Parchment;
        letter.parchmentText = "Je croyais avoir trouvé un animal égaré. Nina comprenait mes mots. Puis elle m'a répondu.\n\nJ'ai retrouvé les notes du scientifique. Il avait fusionné sa petite-fille et son chien. Il avait écrit : « Pour la science. »\n\nSous cette forme, Nina est encore là. Je l'entends chercher une voix familière. Je voudrais lui promettre que tout peut être réparé, mais je n'en sais rien. La laisser ainsi me paraît cruel. Décider pour elle me terrifie tout autant.\n\nJe ne sais pas comment la sauver. Je sais seulement que je ne veux plus qu'elle soit seule.\n\nÉdouard";
        letter.knowledgeUnlockedOnRead.Add(definition.dilemma);
        definition.dilemma.readableItem = letter;
        var ninaData = Asset<GhostData>(DataPath + "/GhostData_Nina.asset");
        ninaData.ghostId = "ghost_nina"; ninaData.displayName = "Nina"; ninaData.question = definition.idleLine;
        var scarData = Asset<GhostData>(DataPath + "/GhostData_Scar.asset");
        scarData.ghostId = "ghost_scar"; scarData.displayName = "Scar"; scarData.question = definition.scarLine;
        var scientist = Asset<CharacterData>(DataPath + "/ScientifiqueFou.asset");
        scientist.characterName = "Scientifique fou";
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var root = new GameObject("NinaCycle_A_CONFIGURER");
            var cycle = root.AddComponent<NinaCycleController>();
            cycle.definition = definition;
            var scientistObject = Child(root, "SceneMarker_ScientifiqueFou_A_PLACER");
            cycle.scientistMarker = scientistObject.AddComponent<SceneMarker>();
            cycle.scientistMarker.SetCharacterData(scientist);
            cycle.nina = Ghost(root, "Ghost_Nina", ninaData, cycle, false);
            cycle.scar = Ghost(root, "Ghost_Scar", scarData, cycle, true);
            cycle.ninaBlood = Child(root, "Nina's blood_A_ASSIGNER");
            cycle.ninaBlood.SetActive(false);
            cycle.scar.gameObject.SetActive(false);
            cycle.director = Child(root, "Cinematique_ScientifiqueFou_A_ASSIGNER").AddComponent<PlayableDirector>();
            cycle.director.playOnAwake = false;
            cycle.director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            cycle.director.extrapolationMode = DirectorWrapMode.None;
            var marker = Child(root, "SceneMarker_Item_Edward_A_BAKER").AddComponent<SceneMarker>();
            marker.SetItem(letter);
            // Content placement and all visual resources deliberately remain authored by the designer.
            root.transform.position = new Vector3(0, -94, 0);
            EditorSceneManager.SaveScene(scene, ScenePath);
            root.GetComponent<Unity.Netcode.NetworkObject>().SendMessage("OnValidate", SendMessageOptions.DontRequireReceiver);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
        foreach (var asset in new UnityEngine.Object[] { definition, letter, ninaData, scarData, scientist, definition.existence, definition.dilemma }) EditorUtility.SetDirty(asset);
        var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_District_1.asset");
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (!manifest.loadingScenes.Contains(sceneAsset)) manifest.loadingScenes.Add(sceneAsset);
        var serialized = new SerializedObject(manifest);
        var names = serialized.FindProperty("loadingSceneNames");
        names.InsertArrayElementAtIndex(names.arraySize);
        names.GetArrayElementAtIndex(names.arraySize - 1).stringValue = "District_1_Enigme_Ghost_Nina";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manifest);
        if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("[NinaCycle] Scène créée. Sélectionner NinaCycle_A_CONFIGURER pour voir les ressources manquantes.");
    }
    private static GhostController Ghost(GameObject root, string name, GhostData data, NinaCycleController cycle, bool scar)
    {
        var marker = Child(root, "SceneMarker_" + name).AddComponent<SceneMarker>();
        marker.SetGhost(data);
        var actor = Child(marker.gameObject, name);
        var collider = actor.AddComponent<SphereCollider>();
        collider.radius = 1; collider.isTrigger = true;
        var ghost = actor.AddComponent<GhostController>();
        ghost.SetGhostData(data);
        var config = new SerializedObject(ghost);
        config.FindProperty("playOnce").boolValue = false;
        config.FindProperty("enableProximityDissolve").boolValue = false;
        config.FindProperty("enableProximityPresentation").boolValue = false;
        config.ApplyModifiedPropertiesWithoutUndo();
        var adapter = actor.AddComponent<NinaGhostInteraction>();
        adapter.cycle = cycle; adapter.isScar = scar;
        return ghost;
    }
    private static KnowledgeSO Knowledge(string id, string title, string description)
    {
        var value = Asset<KnowledgeSO>(DataPath + "/Knowledge_" + id + ".asset");
        value.knowledgeId = "nina." + id; value.title = title; value.description = description;
        value.category = KnowledgeCategory.Truth; value.districtId = "district_1";
        return value;
    }
    private static T Asset<T>(string path) where T : ScriptableObject
    {
        var value = AssetDatabase.LoadAssetAtPath<T>(path);
        if (value != null) return value;
        value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); return value;
    }
    private static GameObject Child(GameObject parent, string name)
    {
        var child = new GameObject(name); child.transform.SetParent(parent.transform, false); return child;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
}
