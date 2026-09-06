#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Timeline;
using UnityEngine.UI;

public static class CounterSkillPrototypeBuilder
{
    private const string Root = "Assets/CombatRealTime/Counters";
    private const string TimelinePath = Root + "/CounterSkill_TemporalRiposte.playable";
    private const string SkillPath = Root + "/CounterSkill_TemporalRiposte.asset";
    private const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
    private const string SessionRootPath = "Assets/Core/System/GameplaySessionRoot.prefab";
    private const string CounterSwordGuid = "2622150291d55c442964e5d6fc048a04";
    private const string JuggernautCounteredGuid = "37a89e362d7404e4b99804c51c57ed3b";
    private const string SouthButtonPath = "Assets/UI/Inputs/XBox GamePad SouthButton.png";
    private const string AssomoirPath = "Assets/Characters/1_Squad/Lucian/Animation/Juggernaut_Assomoir.anim";
    private const string AssomoirSkillPath = "Assets/Characters/3_Enemy/Juggernaut/Skill_Juggernaut_Assomoir.asset";
    private const string AttackAlertPath = "Assets/CombatRealTime/Presentation/AttackLightAlert.prefab";
    private const string EastButtonPath = "Assets/UI/Inputs/XBox GamePad EastButton.png";
    private const string NorthButtonPath = "Assets/UI/Inputs/XBox GamePad NorthButton.png";
    private const string ThreatAudioPath = "Assets/Audio/AudioClips/AudioClip_SFX_LightCharge.asset";
    private const string PerfectAudioPath = "Assets/Audio/AudioClips/AudioClip_SFX_LightImpact.asset";
    private const string WarningMaterialPath = "Assets/CombatRealTime/Presentation/MAT_CombatWarning.mat";

    [MenuItem("Lit/Combat/Build CounterSkill Prototype")]
    public static void Build()
    {
        AnimationClip playerClip = LoadClip(CounterSwordGuid);
        AnimationClip enemyClip = LoadClip(JuggernautCounteredGuid);
        if (playerClip == null || enemyClip == null)
        {
            Debug.LogError("[CounterSkill] Clips Counter_Sword ou Countered introuvables.");
            return;
        }

        TimelineAsset timeline = BuildTimeline(playerClip, enemyClip);
        CounterSkillSO skill = BuildSkill(timeline);
        ConfigureCounterAnimationEvent(playerClip);
        ConfigureAssomoirEvents();
        ConfigureSessionRoot(skill);
        RemoveBootstrapCounterWheel();
        RemoveBootstrapReactionPrompt();
        RemoveBootstrapCounterWheel();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CounterSkill] Prototype configure : Timeline, CounterSkillSO, roue Bootstrap et BattleManager.");
    }

    [MenuItem("Lit/Combat/Configure Reaction Telegraph")]
    public static void ConfigureReactionTelegraph()
    {
        ConfigureAssomoirEvents();
        ConfigureSessionRoot(AssetDatabase.LoadAssetAtPath<CounterSkillSO>(SkillPath));
        RemoveBootstrapReactionPrompt();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Combat Telegraph] Assomoir, BattleManager et prompt world-space configures.");
    }

    private static TimelineAsset BuildTimeline(AnimationClip playerClip, AnimationClip enemyClip)
    {
        TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
        if (timeline == null)
        {
            timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, TimelinePath);
        }

        ClearTracks(timeline);
        AnimationTrack playerTrack = timeline.CreateTrack<AnimationTrack>(null, "Player.Animator");
        TimelineClip playerTimelineClip = playerTrack.CreateClip<AnimationPlayableAsset>();
        ((AnimationPlayableAsset)playerTimelineClip.asset).clip = playerClip;
        playerTimelineClip.start = 0d;
        playerTimelineClip.duration = playerClip.length;

        AnimationTrack enemyTrack = timeline.CreateTrack<AnimationTrack>(null, "Enemy.Animator");
        TimelineClip enemyTimelineClip = enemyTrack.CreateClip<AnimationPlayableAsset>();
        ((AnimationPlayableAsset)enemyTimelineClip.asset).clip = enemyClip;
        enemyTimelineClip.start = 0d;
        enemyTimelineClip.duration = enemyClip.length;

        CinemachineTrack cameraTrack = timeline.CreateTrack<CinemachineTrack>(null, "Cinemachine");
        TimelineClip cameraClip = cameraTrack.CreateClip<CinemachineShot>();
        cameraClip.start = 0d;
        cameraClip.duration = System.Math.Max(playerClip.length, enemyClip.length);
        timeline.CreateTrack<SignalTrack>(null, "Signals");
        EditorUtility.SetDirty(timeline);
        return timeline;
    }

    private static CounterSkillSO BuildSkill(TimelineAsset timeline)
    {
        CounterSkillSO skill = AssetDatabase.LoadAssetAtPath<CounterSkillSO>(SkillPath);
        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<CounterSkillSO>();
            AssetDatabase.CreateAsset(skill, SkillPath);
        }

        SerializedObject serialized = new SerializedObject(skill);
        serialized.FindProperty("displayName").stringValue = "Riposte temporelle";
        serialized.FindProperty("timeline").objectReferenceValue = timeline;
        serialized.FindProperty("damage").intValue = 25;
        serialized.FindProperty("clarityGain").floatValue = 10f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static void ConfigureSessionRoot(CounterSkillSO skill)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SessionRootPath);
        try
        {
            Transform battleManager = FindChild(root.transform, "BattleManager");
            PlayableDirector director = battleManager != null ? battleManager.GetComponent<PlayableDirector>() : null;
            if (battleManager == null || director == null)
            {
                Debug.LogError("[CounterSkill] BattleManager ou PlayableDirector introuvable dans GameplaySessionRoot.");
                return;
            }

            Transform cameraTransform = battleManager.Find("CounterSkill_VirtualCamera");
            if (cameraTransform == null)
            {
                GameObject cameraObject = new GameObject("CounterSkill_VirtualCamera");
                cameraTransform = cameraObject.transform;
                cameraTransform.SetParent(battleManager, false);
                cameraObject.AddComponent<CinemachineCamera>();
                cameraObject.AddComponent<CounterSkillCameraRig>();
            }

            CounterSkillCombatController controller = battleManager.GetComponent<CounterSkillCombatController>();
            if (controller == null) controller = battleManager.gameObject.AddComponent<CounterSkillCombatController>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("combatManager").objectReferenceValue = battleManager.GetComponent<RealTimeCombatManager>();
            serialized.FindProperty("director").objectReferenceValue = director;
            serialized.FindProperty("counterVirtualCamera").objectReferenceValue = cameraTransform.GetComponent<CinemachineCamera>();
            serialized.FindProperty("cameraRig").objectReferenceValue = cameraTransform.GetComponent<CounterSkillCameraRig>();
            serialized.FindProperty("impactFeedback").objectReferenceValue = battleManager.GetComponent<CombatImpactFeedbackController>();
            SerializedProperty skills = serialized.FindProperty("availableSkills");
            skills.arraySize = 1;
            skills.GetArrayElementAtIndex(0).objectReferenceValue = skill;
            serialized.FindProperty("defaultCounterSkill").objectReferenceValue = skill;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ConfigureCombatWarning(battleManager);

            CombatReactionTelegraphController telegraph = battleManager.GetComponent<CombatReactionTelegraphController>();
            if (telegraph == null) telegraph = battleManager.gameObject.AddComponent<CombatReactionTelegraphController>();
            RealTimeCombatReactionPrompt prompt = CreateReactionPrompt(battleManager);
            SerializedObject telegraphSerialized = new SerializedObject(telegraph);
            telegraphSerialized.FindProperty("combatManager").objectReferenceValue = battleManager.GetComponent<RealTimeCombatManager>();
            telegraphSerialized.FindProperty("impactFeedback").objectReferenceValue = battleManager.GetComponent<CombatImpactFeedbackController>();
            telegraphSerialized.FindProperty("prompt").objectReferenceValue = prompt;
            telegraphSerialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, SessionRootPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RemoveBootstrapCounterWheel()
    {
        var scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
        CounterSkillWheel existing = Object.FindAnyObjectByType<CounterSkillWheel>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureCounterAnimationEvent(AnimationClip clip)
    {
        List<AnimationEvent> events = new List<AnimationEvent>(AnimationUtility.GetAnimationEvents(clip));
        AnimationEvent impact = events.Find(e => e.functionName == "ResolveCounterSkillImpact");
        for (int i = events.Count - 1; i >= 0; i--)
        {
            string name = events[i].functionName;
            if (name == "Take" || name == "Release" || name == "SlowCombatTimeTo" ||
                name == "RestoreCombatTime" || name == "CounterHit" || name == "ResolveCounterSkillImpact")
                events.RemoveAt(i);
        }
        events.Add(impact ?? new AnimationEvent
        {
            functionName = "ResolveCounterSkillImpact",
            time = clip.length * 0.58f
        });
        events.Sort((a, b) => a.time.CompareTo(b.time));
        AnimationUtility.SetAnimationEvents(clip, events.ToArray());
        EditorUtility.SetDirty(clip);
    }

    private static void ConfigureAssomoirEvents()
    {
        var skill = AssetDatabase.LoadAssetAtPath<SkillSO>("Assets/Characters/3_Enemy/Juggernaut/Skill_Juggernaut_Assomoir.asset");
        if (skill != null) JuggernautEssentialEvents.ConfigureSkill(skill, .837f);
    }

    private static RealTimeCombatReactionPrompt CreateReactionPrompt(Transform battleManager)
    {
        Transform existing = battleManager.Find("RealTimeCombatReactionPrompt");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject root = new GameObject("RealTimeCombatReactionPrompt", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(RealTimeCombatReactionPrompt));
        root.transform.SetParent(battleManager, false);
        root.transform.localScale = Vector3.one * 0.01f;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(360f, 110f);
        RealTimeCombatReactionPrompt prompt = root.GetComponent<RealTimeCombatReactionPrompt>();

        Image[] icons = new Image[3];
        Sprite[] sprites =
        {
            AssetDatabase.LoadAssetAtPath<Sprite>(NorthButtonPath),
            AssetDatabase.LoadAssetAtPath<Sprite>(EastButtonPath),
            AssetDatabase.LoadAssetAtPath<Sprite>(SouthButtonPath)
        };
        for (int i = 0; i < icons.Length; i++)
        {
            GameObject iconObject = new GameObject("ReactionIcon_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(root.transform, false);
            RectTransform rect = iconObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(72f, 72f);
            rect.anchoredPosition = new Vector2((i - 1) * 92f, 0f);
            icons[i] = iconObject.GetComponent<Image>();
            icons[i].sprite = sprites[i];
            icons[i].preserveAspect = true;
        }

        SerializedObject serialized = new SerializedObject(prompt);
        serialized.FindProperty("visualRoot").objectReferenceValue = root;
        serialized.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
        SerializedProperty iconArray = serialized.FindProperty("reactionIcons");
        iconArray.arraySize = icons.Length;
        for (int i = 0; i < icons.Length; i++) iconArray.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];
        serialized.FindProperty("counterIcon").objectReferenceValue = sprites[0];
        serialized.FindProperty("dodgeIcon").objectReferenceValue = sprites[1];
        serialized.FindProperty("jumpIcon").objectReferenceValue = sprites[2];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return prompt;
    }

    private static void RemoveBootstrapReactionPrompt()
    {
        var scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
        RealTimeCombatReactionPrompt prompt = Object.FindAnyObjectByType<RealTimeCombatReactionPrompt>(FindObjectsInactive.Include);
        if (prompt != null)
        {
            Object.DestroyImmediate(prompt.gameObject);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }


    private static void ConfigureCombatWarning(Transform battleManager)
    {
        Shader shader = Shader.Find("Hidden/Lit/CombatWarning");
        if (shader == null)
        {
            Debug.LogError("[CombatWarning] Shader Hidden/Lit/CombatWarning introuvable.");
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(WarningMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "MAT_CombatWarning" };
            AssetDatabase.CreateAsset(material, WarningMaterialPath);
        }

        Transform volumeTransform = battleManager.Find("CombatWarningPass");
        if (volumeTransform == null)
        {
            GameObject volumeObject = new GameObject("CombatWarningPass");
            volumeTransform = volumeObject.transform;
            volumeTransform.SetParent(battleManager, false);
        }

        CustomPassVolume volume = volumeTransform.GetComponent<CustomPassVolume>();
        if (volume == null) volume = volumeTransform.gameObject.AddComponent<CustomPassVolume>();
        volume.isGlobal = true;
        volume.priority = 1001f;
        volume.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
        volume.customPasses.Clear();
        FullScreenCustomPass pass = new FullScreenCustomPass
        {
            name = "Combat Warning",
            enabled = false,
            fullscreenPassMaterial = material,
            fetchColorBuffer = true
        };
        volume.customPasses.Add(pass);

        CombatWarningPresentationController warning = battleManager.GetComponent<CombatWarningPresentationController>();
        if (warning == null) warning = battleManager.gameObject.AddComponent<CombatWarningPresentationController>();
        SerializedObject serialized = new SerializedObject(warning);
        serialized.FindProperty("combatManager").objectReferenceValue = battleManager.GetComponent<RealTimeCombatManager>();
        serialized.FindProperty("lockCamera").objectReferenceValue = battleManager.GetComponent<CombatLockOnCameraController>();
        serialized.FindProperty("customPassVolume").objectReferenceValue = volume;
        serialized.FindProperty("warningMaterial").objectReferenceValue = material;
        serialized.FindProperty("customPassName").stringValue = "Combat Warning";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(volume);
        EditorUtility.SetDirty(warning);
        EditorUtility.SetDirty(material);
    }

    private static AnimationClip LoadClip(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
        }
        return null;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root.name == childName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), childName);
            if (found != null) return found;
        }
        return null;
    }

    private static void ClearTracks(TimelineAsset timeline)
    {
        foreach (TrackAsset track in new List<TrackAsset>(timeline.GetRootTracks())) timeline.DeleteTrack(track);
    }
}
#endif
