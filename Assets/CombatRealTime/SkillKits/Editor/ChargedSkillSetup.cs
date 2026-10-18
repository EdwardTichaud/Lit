using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ChargedSkillSetup
{
    public const string SkillPath = "Assets/CombatRealTime/Skills/Skill_AscensionLumineuse.asset";
    public const string Folder = "Assets/Characters/3_Enemy/Iluvilirae";
    public const string EnemyPath = Folder + "/ChargedEnemy_Lab.prefab";
    public const string EnemyControllerPath = Folder + "/ChargedEnemy_Lab.controller";
    [MenuItem("Lit/Combat Lab/Configure Ascension lumineuse")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring.");
        var skill = AssetDatabase.LoadAssetAtPath<ChargedSkillSO>(SkillPath);
        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<ChargedSkillSO>();
            skill.skillName = "Ascension lumineuse";
            skill.description = "Maintenir X : projette la cible et la rejoint. Flottement permettant des BasicSkills aeriennes manuelles.";
            skill.damages = 10; skill.cooldownSeconds = 8;
            skill.useLaunchRange = true; skill.minimumLaunchDistance = 0; skill.maximumLaunchDistance = 3;
            skill.minimumHitDistance = 0; skill.maximumHitDistance = 3;
            skill.animatorState = "Base Layer.ChargedAscension";
            skill.presentation.movementPolicy = PlayerActionMovementPolicy.VisualOnly;
            skill.presentation.lockMovementDuringAnimation = true;
            skill.presentation.allowMobilityCancel = false;
            skill.presentation.recoveryNormalizedTime = 1;
            skill.presentation.chainNormalizedTime = 1;
            skill.impactFeedback.enabled = false;
            AssetDatabase.CreateAsset(skill, SkillPath);
        }
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(GroundImpactSkillSetup.ControllerPath);
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(GroundImpactSkillSetup.DataPath);
        if (controller == null || data == null) throw new InvalidOperationException("Lucian laboratory assets missing.");
        if (skill.preparationClip == null || AssetDatabase.GetAssetPath(skill.preparationClip) == Folder + "/ChargedPreparation.anim" ||
            AssetDatabase.GetAssetPath(skill.preparationClip) == Folder + "/ChargedWindup.anim")
        {
            skill.preparationClip = CreatePreparation();
        }
        if (skill.animationClip == null || AssetDatabase.GetAssetPath(skill.animationClip) == Folder + "/ChargedAscension.anim")
        {
            var ground = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(Folder + "/Clash/ClashBasic_2.asset").AnimationClip;
            skill.animationClip = CreateLaunch(ground);
        }
        if (skill.playerFloatClip == null || AssetDatabase.GetAssetPath(skill.playerFloatClip) == Folder + "/ChargedPlayerFloat.anim")
            skill.playerFloatClip = Copy("Assets/Characters/1_Squad/Lucian/Animation/jump_falling_loop_inplace.fbx", "ChargedPlayerFloatUpright", true);
        if (skill.enemyFloatClip == null) skill.enemyFloatClip = CreateFloatHit();
        skill.description = "Maintenir X : un coup projette la cible, Lucian la rejoint puis flotte. Les attaques aeriennes suivantes sont manuelles.";
        State(controller, "ChargedPreparation", skill.preparationClip);
        State(controller, "ChargedAscension", skill.animationClip);
        State(controller, "ChargedFloat", skill.playerFloatClip);
        data.chargedSkill = skill;
        SkillTargetingAuthoring.ConfigureIfMissing(skill);
        EditorUtility.SetDirty(skill); EditorUtility.SetDirty(data); EditorUtility.SetDirty(controller);
        var previous = EditorSceneManager.GetSceneManagerSetup();
        var scene = EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
        if (arena == null || arena.enemyPrefab == null) throw new InvalidOperationException("Authored laboratory enemy missing.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath) == null)
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(arena.enemyPrefab), EnemyPath);
        var enemy = PrefabUtility.LoadPrefabContents(EnemyPath);
        try
        {
            var animator = enemy.GetComponent<Animator>();
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath) == null)
            {
                var source = animator.runtimeAnimatorController;
                if (source is AnimatorOverrideController overrides) source = overrides.runtimeAnimatorController;
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), EnemyControllerPath);
            }
            var enemyController = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath);
            State(enemyController, "ChargedFloatHit", skill.enemyFloatClip);
            EditorUtility.SetDirty(enemyController);
            animator.runtimeAnimatorController = enemyController;
            var body = enemy.GetComponent<Rigidbody>();
            if (body == null) body = enemy.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            PrefabUtility.SaveAsPrefabAsset(enemy, EnemyPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(enemy); }
        arena.enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath);
        EditorUtility.SetDirty(arena); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        if (previous.Length > 0 && previous.All(item => !string.IsNullOrEmpty(item.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
        AssetDatabase.SaveAssets();
        Debug.Log("[ChargedSkill] AUTHORING PASS | Lucian laboratory only; one contact and independent airborne poses.");
    }

    private static AnimationClip Copy(string path, string name, bool loop)
    {
        string output = Folder + "/" + name + ".anim";
        var result = AssetDatabase.LoadAssetAtPath<AnimationClip>(output);
        if (result != null) return result;
        var source = path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ? ActorAnimationAudit.Source(path) : AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        result = UnityEngine.Object.Instantiate(source);
        result.name = name;
        AnimationUtility.SetAnimationEvents(result, Array.Empty<AnimationEvent>());
        Normalize(result, loop);
        AssetDatabase.CreateAsset(result, output);
        return result;
    }

    private static AnimationClip CreatePreparation()
    {
        string output = Folder + "/ChargedWindup.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(output);
        var source = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(Folder + "/Clash/ClashBasic_0.asset").AnimationClip;
        bool create = clip == null;
        if (create) clip = UnityEngine.Object.Instantiate(source);
        clip.name = "ChargedWindup";
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            AnimationUtility.SetEditorCurve(clip, binding, null);
        // Hold BasicSkill_1's exact final pose without freezing the Animator or replaying its events.
        foreach (var binding in AnimationUtility.GetCurveBindings(source))
        {
            var curve = AnimationUtility.GetEditorCurve(source, binding);
            float value = curve.Evaluate(source.length);
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 1f, value));
        }
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
        AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
        Normalize(clip, true);
        if (create) AssetDatabase.CreateAsset(clip, output);
        else EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void Normalize(AnimationClip clip, bool loop)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        var serialized = new SerializedObject(clip);
        foreach (string field in new[] { "m_LoopBlendOrientation", "m_LoopBlendPositionXZ", "m_LoopBlendPositionY" })
            serialized.FindProperty("m_AnimationClipSettings." + field).boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static AnimationClip CreateLaunch(AnimationClip ground)
    {
        string output = Folder + "/ChargedLaunch.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(output);
        if (clip != null) return clip;
        clip = UnityEngine.Object.Instantiate(ground); clip.name = "ChargedLaunch";
        // One grounded strike, followed by a short visual tail until physical ascent completes.
        var bindings = AnimationUtility.GetCurveBindings(ground);
        foreach (var binding in bindings)
        {
            var first = AnimationUtility.GetEditorCurve(ground, binding);
            var keys = new Keyframe[82];
            for (int i = 0; i < keys.Length; i++)
            {
                float t = i / 60f;
                keys[i] = new Keyframe(t, first.Evaluate(Mathf.Clamp01(t / .7f) * ground.length));
            }
            AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
        }
        AnimationUtility.SetAnimationEvents(clip, new[] {
            new AnimationEvent { time = .02f, functionName = "ShowSword" },
            new AnimationEvent { time = .35f, functionName = "ChargedSkillContact", intParameter = 0 }
        });
        Normalize(clip, false); AssetDatabase.CreateAsset(clip, output);
        return clip;
    }

    private static AnimationClip CreateFloatHit()
    {
        string output = Folder + "/ChargedEnemyFloatHit.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(output);
        if (clip != null) return clip;
        var source = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters/3_Enemy/Juggernaut_v2/Animations/Juggernaut_v2_Hurt.anim");
        clip = UnityEngine.Object.Instantiate(source); clip.name = "ChargedEnemyFloatHit";
        foreach (var binding in AnimationUtility.GetCurveBindings(source))
        {
            var curve = AnimationUtility.GetEditorCurve(source, binding);
            var keys = new Keyframe[121];
            for (int i = 0; i < keys.Length; i++)
            {
                float t = i / 60f;
                float phase = .25f + .035f * Mathf.Sin(t * Mathf.PI);
                keys[i] = new Keyframe(t, curve.Evaluate(source.length * phase));
            }
            AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
        }
        AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
        Normalize(clip, true); AssetDatabase.CreateAsset(clip, output);
        return clip;
    }

    private static void State(AnimatorController controller, string name, AnimationClip clip)
    {
        var machine = controller.layers[0].stateMachine;
        var state = machine.states.FirstOrDefault(item => item.state.name == name).state ?? machine.AddState(name);
        state.motion = clip; state.tag = "RealTimeCombatInPlace"; state.writeDefaultValues = false;
        EditorUtility.SetDirty(state);
    }

    public static void RunBatch()
    {
        try { Configure(); Configure(); EditorApplication.Exit(0); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
}
