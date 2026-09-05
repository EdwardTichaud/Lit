using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Explicit, repeatable asset migration. The Library request is for local editor automation.</summary>
[InitializeOnLoad]
public static class JuggernautPatternSetup
{
    private const string Folder = "Assets/Characters/3_Enemy/Juggernaut/";
    private const string Request = "Library/JuggernautPatternSetup.request";
    static JuggernautPatternSetup() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        File.Delete(Request);
        try { Configure(); File.WriteAllText("Library/JuggernautPatternSetup.result", "PASS"); }
        catch (Exception e) { File.WriteAllText("Library/JuggernautPatternSetup.result", e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Lit/Combat/Install Juggernaut Patterns")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Quitter Play Mode avant migration.");
        var source = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Characters/4_Animations/Player_Model.controller");
        var old = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "Juggernaut.controller");
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(Folder + "Juggernaut.asset");
        var assomoir = AssetDatabase.LoadAssetAtPath<SkillSO>(Folder + "Skill_Juggernaut_Assomoir.asset");
        if (source == null || old == null || data == null || assomoir == null) throw new InvalidOperationException("Sources Juggernaut absentes.");
        string[] sourceNames = { "TwinSword_attack03_Inplace", "TwinSword_attack04_Inplace", "TwinSword_Attack15_Inplace" };
        AnimationClip[] clips = sourceNames.Select(n => (Find(source.layers[0].stateMachine, n)?.motion ??
            Find(source.layers[0].stateMachine, n.Replace("_Inplace", "_Root"))?.motion) as AnimationClip).ToArray();
        if (clips.Any(c => c == null || !c.humanMotion)) throw new InvalidOperationException("Clips Humanoid requis : " +
            string.Join(", ", sourceNames.Select((n, i) => n + "=" + (clips[i] == null ? "ABSENT" : clips[i].name + ":human=" + clips[i].humanMotion))));
        string controllerPath = Folder + "Juggernaut_Model.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) == null &&
            !AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), controllerPath)) throw new InvalidOperationException("Copie controller refusee.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        var machine = controller.layers[0].stateMachine;
        controller.layers = new[] { controller.layers[0] };
        string[] names = { "Juggernaut_Strike", "Juggernaut_Followup", "Juggernaut_Sweep" };
        var skills = new List<SkillSO>();
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + names[i] + ".anim");
            if (clip == null)
            {
                clip = UnityEngine.Object.Instantiate(clips[i]);
                clip.name = names[i];
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                float length = clip.length;
                AnimationUtility.SetAnimationEvents(clip, new[] {
                    Event("CombatWarningOn", length * .15f), Event("OpenReactionWindow", length * .3f, .3f),
                    Event("LockEnemyAttackDirection", length * .42f), Event("OpenEnemyAttackHitbox", length * .48f),
                    Event("CloseEnemyAttackHitbox", length * .66f), Event("CombatWarningOff", length * .67f),
                    Event("EndEnemyAttack", length * .94f) });
                AssetDatabase.CreateAsset(clip, Folder + names[i] + ".anim");
            }
            string skillPath = Folder + "Skill_" + names[i] + ".asset";
            SkillSO skill = AssetDatabase.LoadAssetAtPath<SkillSO>(skillPath);
            if (skill == null)
            {
                skill = UnityEngine.Object.Instantiate(assomoir);
                skill.name = "Skill_" + names[i];
                AssetDatabase.CreateAsset(skill, skillPath);
            }
            var serialized = new SerializedObject(skill);
            serialized.FindProperty("skillName").stringValue = names[i];
            serialized.FindProperty("animationClip").objectReferenceValue = clip;
            serialized.FindProperty("animatorState").stringValue = names[i];
            serialized.FindProperty("minimumHitDistance").floatValue = 0f;
            serialized.FindProperty("maximumHitDistance").floatValue = i == 2 ? 3.6f : 3.1f;
            serialized.FindProperty("enemyActionMotion.movementMode").enumValueIndex = 0;
            serialized.FindProperty("enemyActionMotion.enableHomingRush").boolValue = false;
            serialized.FindProperty("enemyDamageMultiplier").floatValue = i == 1 ? .8f : 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            skill.enemyImpact.radius = i == 2 ? 2.5f : 1.6f;
            skill.enemyImpact.arcDegrees = i == 2 ? 240f : 100f;
            MakeInPlace(clip);
            var events = AnimationUtility.GetAnimationEvents(clip).Where(e => e.functionName != "BeginEnemyAdvance" && e.functionName != "EndEnemyAdvance").ToList();
            if (i < 2)
            {
                float begin = events.First(e => e.functionName == "LockEnemyAttackDirection").time;
                events.Add(Event("BeginEnemyAdvance", begin + .001f));
                events.Add(Event("EndEnemyAdvance", begin + (i == 0 ? .18f : .2f) + .001f));
            }
            AnimationUtility.SetAnimationEvents(clip, events.OrderBy(e => e.time).ToArray());
            skill.EnemyActionMotion.enableAdvance = i < 2;
            skill.EnemyActionMotion.advanceDistance = i == 0 ? .4f : i == 1 ? .5f : 0f;
            skill.EnemyActionMotion.advanceDuration = i == 0 ? .18f : .2f;
            EnsureState(machine, names[i], clip);
            EditorUtility.SetDirty(skill);
            skills.Add(skill);
        }
        skills.Add(assomoir);
        var assomoirData = new SerializedObject(assomoir);
        assomoirData.FindProperty("animatorState").stringValue = "Juggernaut_Assomoir";
        assomoirData.FindProperty("minimumHitDistance").floatValue = 0f;
        assomoirData.FindProperty("maximumHitDistance").floatValue = 6f;
        // Apex at warning (0.837s), with takeoff at 0.1s.
        assomoirData.FindProperty("enemyActionMotion.initialUpwardSpeed").floatValue = 10.5f;
        assomoirData.FindProperty("enemyActionMotion.gravity").floatValue = 10.5f / .737f;
        assomoirData.FindProperty("enemyActionMotion.lockRushDestination").boolValue = true;
        assomoirData.ApplyModifiedPropertiesWithoutUndo();
        AnimationEvent[] jumpEvents = AnimationUtility.GetAnimationEvents(assomoir.AnimationClip);
        foreach (var entry in jumpEvents)
        {
            if (entry.functionName == "RequestEnemyLanding") entry.time = 1.25f;
            if (entry.functionName == "ResolveEnemyAttackImpact") entry.time = 1.7f;
            if (entry.functionName == "CombatWarningOff") entry.time = 1.71f;
            if (entry.functionName == "EndEnemyRush") entry.time = 1.72f;
        }
        AnimationUtility.SetAnimationEvents(assomoir.AnimationClip, jumpEvents.OrderBy(e => e.time).ToArray());
        EditorUtility.SetDirty(assomoir.AnimationClip);
        EnsureState(machine, "Juggernaut_Assomoir", assomoir.AnimationClip);
        foreach (string name in new[] { "Guard", "Hit", "Countered", "GetUp", "Death" })
        {
            Motion motion = Find(old.layers[0].stateMachine, name)?.motion;
            if (motion == null) throw new InvalidOperationException("Clip requis absent : " + name);
            EnsureState(machine, name, motion);
        }
        var kept = new HashSet<string>(names.Concat(new[] { "CombatIdle", "CombatLocomotion", "Juggernaut_Assomoir", "Guard", "Hit", "Countered", "GetUp", "Death" }));
        foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
        foreach (var transition in machine.entryTransitions) machine.RemoveEntryTransition(transition);
        foreach (var child in machine.stateMachines) machine.RemoveStateMachine(child.stateMachine);
        foreach (var child in machine.states)
        {
            foreach (var transition in child.state.transitions) child.state.RemoveTransition(transition);
            if (!kept.Contains(child.state.name)) machine.RemoveState(child.state);
        }
        machine.defaultState = Find(machine, "CombatIdle");
        var profile = AssetDatabase.LoadAssetAtPath<EnemyCombatProfileSO>(Folder + "Juggernaut_CombatProfile.asset");
        if (profile == null) throw new InvalidOperationException("Profil absent.");
        profile.patterns = new List<EnemyCombatPattern> {
            Pattern("Frappe", new[] {skills[0]}, 40, 1.5f, .7f),
            Pattern("Combo", new[] {skills[0], skills[1]}, 30, 3f, .7f),
            Pattern("Balayage", new[] {skills[2]}, 20, 3f, .9f),
            Pattern("Assomoir", new[] {assomoir}, 25, 5f, 1.2f) };
        profile.patterns[3].minimumStartDistance = 2.5f;
        profile.preferMeleeApproach = true;
        profile.airborneAlternativeChance = .25f;
        foreach (var child in machine.states) child.state.motion = ConvertMotion(child.state.motion);
        MakeInPlace(assomoir.AnimationClip);
        data.combatSkills = skills;
        data.enemyCombatProfile = profile;
        var root = PrefabUtility.LoadPrefabContents(Folder + "Juggernaut_Combat.prefab");
        try
        {
            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            var motorData = new SerializedObject(root.GetComponent<CombatEnemyPhysicsMotor>());
            motorData.FindProperty("animationMovementMode").enumValueIndex = 1;
            motorData.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<CharacterInfo>().SetCharacterData(data);
            if (root.GetComponent<EnemyCinematicState>() == null) root.AddComponent<EnemyCinematicState>();
            var legacy = root.GetComponent<RealTimeCombatEnemyBehaviour>();
            if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
            var tactical = root.GetComponent<EnemyTacticalResponseController>();
            if (tactical != null) UnityEngine.Object.DestroyImmediate(tactical);
            var enemyData = new SerializedObject(root.GetComponent<RealTimeCombatEnemy>());
            enemyData.FindProperty("idleAnimatorState").stringValue = "CombatIdle";
            enemyData.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "Juggernaut_Combat.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        EditorUtility.SetDirty(controller); EditorUtility.SetDirty(profile); EditorUtility.SetDirty(data); EditorUtility.SetDirty(assomoir);
        AssetDatabase.SaveAssets();
        NetcodePrefabRegistry.InvalidateSceneMarkerCharacterCache();
        Debug.Log("[JuggernautPatterns] Migration terminee : 4 skills, 4 patterns, controller dedie.");
    }
    private static EnemyCombatPattern Pattern(string name, SkillSO[] skills, int weight, float cooldown, float recovery) =>
        new EnemyCombatPattern { name = name, skills = skills.ToList(), weight = weight, cooldownSeconds = cooldown, recoverySeconds = recovery };

    private static Motion ConvertMotion(Motion motion)
    {
        if (motion is BlendTree tree)
        {
            var children = tree.children;
            for (int i = 0; i < children.Length; i++) children[i].motion = ConvertMotion(children[i].motion);
            tree.children = children;
            EditorUtility.SetDirty(tree);
            return tree;
        }
        if (!(motion is AnimationClip clip)) return motion;
        string sourcePath = AssetDatabase.GetAssetPath(clip);
        if (!sourcePath.StartsWith(Folder, StringComparison.Ordinal) || !sourcePath.EndsWith(".anim", StringComparison.Ordinal))
        {
            string path = Folder + "InPlace_" + clip.name.Replace("/", "_") + ".anim";
            var copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (copy == null)
            {
                copy = UnityEngine.Object.Instantiate(clip);
                copy.name = "InPlace_" + clip.name;
                AssetDatabase.CreateAsset(copy, path);
            }
            clip = copy;
        }
        MakeInPlace(clip);
        return clip;
    }

    private static void MakeInPlace(AnimationClip clip)
    {
        var serializedClip = new SerializedObject(clip);
        foreach (string property in new[] { "m_LoopBlendOrientation", "m_LoopBlendPositionY", "m_LoopBlendPositionXZ" })
        {
            var value = serializedClip.FindProperty("m_AnimationClipSettings." + property);
            if (value == null) throw new InvalidOperationException("Reglage InPlace absent : " + property);
            value.boolValue = true;
        }
        serializedClip.ApplyModifiedPropertiesWithoutUndo();
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            bool rootPose = binding.path == "" && (binding.propertyName.StartsWith("RootT.") ||
                binding.propertyName.StartsWith("RootQ.") || binding.type == typeof(Transform) &&
                (binding.propertyName.StartsWith("m_LocalPosition.") || binding.propertyName.StartsWith("m_LocalRotation.")));
            if (!rootPose) continue;
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            float value = curve.Evaluate(0f);
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, clip.length, value));
        }
        EditorUtility.SetDirty(clip);
    }
    private static AnimationEvent Event(string name, float time, float parameter = 0f) =>
        new AnimationEvent { functionName = name, time = time, floatParameter = parameter };
    private static AnimatorState Find(AnimatorStateMachine machine, string name)
    {
        var found = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
        if (found != null) return found;
        foreach (var child in machine.stateMachines)
        {
            found = Find(child.stateMachine, name);
            if (found != null) return found;
        }
        return null;
    }
    private static void EnsureState(AnimatorStateMachine machine, string name, Motion clip)
    {
        var state = Find(machine, name) ?? machine.AddState(name);
        state.motion = clip;
        state.speed = 1f;
        state.speedParameterActive = false;
    }
}
