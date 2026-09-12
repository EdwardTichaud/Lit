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
            throw new InvalidOperationException("La scÃ¨ne Nina existe dÃ©jÃ . Elle ne sera pas Ã©crasÃ©e.");
        EnsureFolder(DataPath);
        EnsureFolder("Assets/Resources/Narrative");
        var definition = Asset<CycleDefinition>("Assets/Resources/Narrative/NinaCycle.asset");
        var existence = Knowledge("ExistenceDesChimeres", "Existence des chimÃ¨res", "Des Ãªtres vivants ont Ã©tÃ© fusionnÃ©s artificiellement par le Scientifique fou.");
        var dilemma = Knowledge("DilemmeEdouard", "Dilemme Ã‰douard", "Ã‰douard a dÃ©couvert que Nina Ã©tait issue de la fusion d'une enfant et d'un chien. Comment lui venir en aide sans lui infliger davantage de souffrance ?");
        definition.cycleId = "district1.nina";
        definition.completionFlags = 8;
        definition.cycleSceneName = "District_1_Enigme_Ghost_Nina";
        definition.knowledgeOnEnemyDefeat = new[] { existence };
        definition.playCinematicAfterDefeat = true;
        definition.dialogues = new[]
        {
            new CycleDialogue { id = "nina", condition = new CycleCondition { knowledge = new[] { existence, dilemma } },
                line = "Nina ne bouge plus.", unavailableLine = "Edouard ? Tu reviendras ?", openedFlags = 20 },
            new CycleDialogue { id = "scar", condition = new CycleCondition { anyFlags = 20, knowledge = new[] { existence, dilemma } },
                line = "Prends cette force, et souviens-toi d'elle.", repeatLine = "Souviens-toi de Nina.", rewardFlag = 8,
                durationSeconds = 2f, disappearAfterCompletion = true, disappearanceDelay = 0f,
                rewardSkill = AssetDatabase.LoadAssetAtPath<SkillSO>("Assets/CombatRealTime/Skills/Skill_3_Cicatrice.asset") }
        };
        CycleMigration.ConfigureNina(definition);
        var letter = Asset<Item>(DataPath + "/Item_Edward.asset");
        letter.itemId = "item_edward";
        letter.itemName = "Lettre manuscrite d'Ã‰douard";
        letter.readableKind = Item.ReadableKind.Parchment;
        letter.parchmentText = "Je croyais avoir trouvÃ© un animal Ã©garÃ©. Nina comprenait mes mots. Puis elle m'a rÃ©pondu.\n\nJ'ai retrouvÃ© les notes du scientifique. Il avait fusionnÃ© sa petite-fille et son chien. Il avait Ã©crit : Â« Pour la science. Â»\n\nSous cette forme, Nina est encore lÃ . Je l'entends chercher une voix familiÃ¨re. Je voudrais lui promettre que tout peut Ãªtre rÃ©parÃ©, mais je n'en sais rien. La laisser ainsi me paraÃ®t cruel. DÃ©cider pour elle me terrifie tout autant.\n\nJe ne sais pas comment la sauver. Je sais seulement que je ne veux plus qu'elle soit seule.\n\nÃ‰douard";
        letter.knowledgeUnlockedOnRead.Add(dilemma);
        dilemma.readableItem = letter;
        var ninaData = Asset<GhostData>(DataPath + "/GhostData_Nina.asset");
        ninaData.ghostId = "ghost_nina"; ninaData.displayName = "Nina"; ninaData.question = definition.FindDialogue("nina").unavailableLine;
        var scarData = Asset<GhostData>(DataPath + "/GhostData_Scar.asset");
        scarData.ghostId = "ghost_scar"; scarData.displayName = "Scar"; scarData.question = definition.FindDialogue("scar").line;
        var scientist = Asset<CharacterData>(DataPath + "/ScientifiqueFou.asset");
        scientist.characterName = "Scientifique fou";
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var root = new GameObject("NinaCycle_A_CONFIGURER");
            var cycle = root.AddComponent<CycleController>();
            cycle.definition = definition;
            var scientistObject = Child(root, "SceneMarker_ScientifiqueFou_A_PLACER");
            cycle.encounterMarker = scientistObject.AddComponent<SceneMarker>();
            cycle.encounterMarker.SetCharacterData(scientist);
            var nina = Ghost(root, "Ghost_Nina", ninaData, cycle, false);
            var scar = Ghost(root, "Ghost_Scar", scarData, cycle, true);
            var blood = Child(root, "Nina's blood_A_ASSIGNER");
            blood.SetActive(false);
            scar.gameObject.SetActive(false);
            cycle.interactions = new[] { nina.GetComponent<CycleInteraction>(), scar.GetComponent<CycleInteraction>() };
            cycle.activations = new[]
            {
                new CycleActivationBinding { target = blood, condition = definition.FindDialogue("scar").condition },
                new CycleActivationBinding { target = scar.gameObject, condition = definition.FindDialogue("scar").condition }
            };
            cycle.poses = new[] { new CyclePoseBinding { condition = definition.FindDialogue("nina").condition, conditionBoolParameter = "isDead" } };
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
        foreach (var asset in new UnityEngine.Object[] { definition, letter, ninaData, scarData, scientist, existence, dilemma }) EditorUtility.SetDirty(asset);
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
        Debug.Log("[NinaCycle] ScÃ¨ne crÃ©Ã©e. SÃ©lectionner NinaCycle_A_CONFIGURER pour voir les ressources manquantes.");
    }
    private static GhostController Ghost(GameObject root, string name, GhostData data, CycleController cycle, bool scar)
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
        config.FindProperty("enableProximityDissolve").boolValue = true;
        config.FindProperty("enableProximityPresentation").boolValue = true;
        config.ApplyModifiedPropertiesWithoutUndo();
        var adapter = actor.AddComponent<CycleInteraction>();
        adapter.cycle = cycle; adapter.dialogueId = scar ? "scar" : "nina";
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
