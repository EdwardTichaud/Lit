using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;

/// <summary>
/// Authors the Mad Scientist as a complete, server-authoritative combat actor.
/// It deliberately creates dedicated assets rather than borrowing Juggernaut
/// data: the Nina encounter can be tuned independently afterwards.
/// </summary>
public static class MadScientistCombatSetup
{
    private const string ScenePath = "Assets/Scenes/District_1/District_1_Enigme_Ghost_Nina.unity";
    private const string PrefabPath = "Assets/Characters/9_Ghosts/Luc/Enemy_Model_MadScientist.prefab";
    private const string CharacterPath = "Assets/Narrative/NinaCycle/Data/Enemy_ScientifiqueFou.asset";
    private const string Folder = "Assets/Narrative/NinaCycle/Combat";
    private const string ProfilePath = Folder + "/MadScientist_CombatProfile.asset";
    private const string SkillPath = Folder + "/Skill_MadScientist_Slash.asset";
    private const string ClipPath = Folder + "/MadScientist_Slash.anim";
    private const string ControllerPath = Folder + "/MadScientist.controller";
    private const string SourceClipPath = "Assets/Characters/1_Squad/Lucian/Animation/Skill_3_Cicatrice.anim";
    private const string SourceControllerPath = "Assets/Characters/4_Animations/Player_Model.controller";
    private const string RequestPath = "Library/MadScientistCombatSetup.request";
    private static bool setupScheduled;

    [InitializeOnLoadMethod]
    private static void ConsumeRequestedSetup()
    {
        if (setupScheduled || !File.Exists(RequestPath)) return;
        setupScheduled = true;
        EditorApplication.delayCall += () =>
        {
            setupScheduled = false;
            if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                Configure();
                File.Delete(RequestPath);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        };
    }

    [MenuItem("Lit/Narrative/Configure Mad Scientist Combat")]
    public static void Configure()
    {
        EnsureFolder();
        SkillSO skill = ConfigureSkill();
        EnemyCombatProfileSO profile = ConfigureProfile(skill);
        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(CharacterPath);
        if (data == null) throw new System.InvalidOperationException("CharacterData Scientifique fou introuvable.");
        data.combatSkills.Clear();
        data.combatSkills.Add(skill);
        data.enemyCombatProfile = profile;
        data.hp = Mathf.Max(60, data.hp);
        EditorUtility.SetDirty(data);

        ConfigurePrefab(skill);
        ConfigureBakedSceneInstance(skill);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MadScientistCombat] Scientifique fou configure : perception, NavMesh, physique, IA et attaque Slash.");
    }

    // Usable by Unity batch mode as part of automated project validation.
    public static void ConfigureFromCommandLine() => Configure();

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Narrative/NinaCycle"))
            throw new System.InvalidOperationException("Le dossier NinaCycle est introuvable.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Narrative/NinaCycle", "Combat");
    }

    private static SkillSO ConfigureSkill()
    {
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceClipPath);
        if (source == null) throw new System.InvalidOperationException("Clip source Cicatrice introuvable.");
        AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (attackClip == null)
        {
            attackClip = Object.Instantiate(source);
            attackClip.name = "MadScientist_Slash";
            AssetDatabase.CreateAsset(attackClip, ClipPath);
        }

        SkillSO skill = AssetDatabase.LoadAssetAtPath<SkillSO>(SkillPath);
        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<SkillSO>();
            AssetDatabase.CreateAsset(skill, SkillPath);
        }
        skill.name = "Skill_MadScientist_Slash";
        skill.skillName = "MadScientist_Slash";
        skill.description = "Coup de laboratoire du Scientifique fou.";
        skill.animationClip = attackClip;
        skill.animatorState = "MadScientist_Slash";
        skill.damages = 12f;
        skill.minimumHitDistance = 0f;
        skill.maximumHitDistance = 2.35f;
        skill.requireValidRangeToStart = true;
        skill.enemyImpact.requireGroundedTarget = false;
        skill.enemyImpact.offset = new Vector3(0f, 1f, 1.15f);
        skill.enemyImpact.radius = 1.3f;
        skill.enemyImpact.arcDegrees = 105f;
        skill.enemyImpact.targetMask = ~0;
        skill.enemyActionMotion.enableAdvance = true;
        skill.enemyActionMotion.advanceDistance = .35f;
        skill.enemyActionMotion.advanceDuration = .18f;
        skill.enemyActionMotion.movementMode = EnemyActionMovementMode.Grounded;
        skill.reactionTelegraph.enabled = false;
        skill.combatWarning.enabled = false;
        EditorUtility.SetDirty(skill);

        float length = Mathf.Max(.2f, attackClip.length);
        AnimationUtility.SetAnimationEvents(attackClip, new[]
        {
            new AnimationEvent { functionName = "LockEnemyAttackDirection", time = Mathf.Min(.12f, length * .2f) },
            new AnimationEvent { functionName = "BeginEnemyAdvance", time = Mathf.Min(.16f, length * .27f) },
            new AnimationEvent { functionName = "EnemyAttack", objectReferenceParameter = skill, time = length * .56f },
            new AnimationEvent { functionName = "EndEnemyAdvance", time = Mathf.Min(length - .06f, length * .72f) },
            new AnimationEvent { functionName = "EndEnemyAttack", time = Mathf.Max(.05f, length - .025f) }
        });
        EditorUtility.SetDirty(attackClip);
        ConfigureController(attackClip);
        return skill;
    }

    private static void ConfigureController(AnimationClip attackClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            if (!AssetDatabase.CopyAsset(SourceControllerPath, ControllerPath))
                throw new System.InvalidOperationException("Copie du controller du Scientifique fou impossible.");
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        }
        if (controller == null) throw new System.InvalidOperationException("Controller du Scientifique fou introuvable.");
        AnimatorState state = FindState(controller.layers[0].stateMachine, "MadScientist_Slash") ??
                              controller.layers[0].stateMachine.AddState("MadScientist_Slash");
        state.motion = attackClip;
        state.writeDefaultValues = true;
        EditorUtility.SetDirty(controller);
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string stateName)
    {
        foreach (ChildAnimatorState child in machine.states)
            if (child.state != null && child.state.name == stateName) return child.state;
        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
        {
            AnimatorState found = FindState(child.stateMachine, stateName);
            if (found != null) return found;
        }
        return null;
    }

    private static EnemyCombatProfileSO ConfigureProfile(SkillSO skill)
    {
        EnemyCombatProfileSO profile = AssetDatabase.LoadAssetAtPath<EnemyCombatProfileSO>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<EnemyCombatProfileSO>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        profile.name = "MadScientist_CombatProfile";
        profile.patterns.Clear();
        profile.patterns.Add(new EnemyCombatPattern
        {
            name = "Slash",
            skills = new System.Collections.Generic.List<SkillSO> { skill },
            weight = 1,
            cooldownSeconds = 1.35f,
            recoverySeconds = .65f,
            maximumConsecutiveUses = 1,
            maximumStartAngle = 40f
        });
        profile.preferMeleeApproach = true;
        profile.preferredCombatDistance = 1.8f;
        profile.pursuitRadius = 18f;
        profile.disengagePauseSeconds = 1.25f;
        profile.returnReengageDistance = 5f;
        profile.observationSeconds = new Vector2(.25f, .45f);
        profile.guardChance = 0f;
        profile.trackingDegreesPerSecond = 360f;
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static void ConfigurePrefab(SkillSO skill)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ConfigureActor(root, skill);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ConfigureBakedSceneInstance(SkillSO skill)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasAlreadyLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasAlreadyLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        SceneMarker marker = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (SceneMarker candidate in root.GetComponentsInChildren<SceneMarker>(true))
            {
                if (candidate.CharacterData != null && candidate.CharacterData.characterName == "Scientifique fou")
                {
                    marker = candidate;
                    break;
                }
            }
            if (marker != null) break;
        }
        if (marker == null || marker.BakedCharacterInstance == null)
            throw new System.InvalidOperationException("Marker ou copie bakee du Scientifique fou introuvable.");
        ConfigureActor(marker.BakedCharacterInstance, skill);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!wasAlreadyLoaded) EditorSceneManager.CloseScene(scene, true);
    }

    private static void ConfigureActor(GameObject root, SkillSO skill)
    {
        Animator animator = root.GetComponent<Animator>();
        if (animator == null) throw new System.InvalidOperationException("Animator absent sur " + root.name);
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Transform lockPoint = root.transform.Find("EnemyLockPoint");
        if (lockPoint == null)
        {
            var point = new GameObject("EnemyLockPoint");
            lockPoint = point.transform;
            lockPoint.SetParent(root.transform, false);
            lockPoint.localPosition = new Vector3(0f, 1.2f, 0f);
        }

        CombatHealth health = Ensure<CombatHealth>(root);
        CharacterInfo info = Ensure<CharacterInfo>(root);
        info.SetCharacterData(AssetDatabase.LoadAssetAtPath<CharacterData>(CharacterPath));
        VisionField vision = Ensure<VisionField>(root);
        RealTimeCombatEnemy enemy = Ensure<RealTimeCombatEnemy>(root);
        CombatActorAnimationRoot animationRoot = Ensure<CombatActorAnimationRoot>(root);
        Ensure<CombatActorRootMotionRelay>(root);
        EnemySkills skills = Ensure<EnemySkills>(root);
        Rigidbody body = Ensure<Rigidbody>(root);
        CapsuleCollider capsule = Ensure<CapsuleCollider>(root);
        NavMeshAgent agent = Ensure<NavMeshAgent>(root);
        CombatEnemyPhysicsMotor motor = Ensure<CombatEnemyPhysicsMotor>(root);
        Ensure<CombatEnemyLocomotionController>(root);
        Ensure<EnemyAttackRecoverySafety>(root);
        Ensure<EnemyNavigationController>(root);
        Ensure<EnemyCinematicState>(root);
        Ensure<CombatEnemyRuntimeContract>(root);
        Ensure<EnemyCombatBrain>(root);
        Ensure<RealTimeCombatAnimationEvents>(root);
        Ensure<NetworkObject>(root);
        Ensure<NetworkTransform>(root);

        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        capsule.isTrigger = false;
        capsule.enabled = true;
        capsule.radius = Mathf.Max(.3f, capsule.radius);
        capsule.height = Mathf.Max(capsule.radius * 2f, capsule.height);
        agent.enabled = true;
        agent.updateRotation = false;
        agent.speed = 2.8f;
        agent.acceleration = 14f;
        agent.angularSpeed = 360f;
        agent.stoppingDistance = 1.65f;
        agent.radius = Mathf.Max(.3f, agent.radius);
        agent.height = Mathf.Max(1.6f, agent.height);

        animationRoot.Configure(root.transform, animator, lockPoint);
        SetReference(enemy, "health", health);
        SetReference(enemy, "animationContract", animationRoot);
        SetReference(enemy, "animator", animator);
        SetReference(enemy, "visionField", vision);
        SetReference(enemy, "enemySkills", skills);
        SetReference(enemy, "physicsMotor", motor);
        SetReference(enemy, "enemyLockPoint", lockPoint);
        SetString(enemy, "idleAnimatorState", "CombatIdle");
        SetString(enemy, "hitAnimatorState", "Twinblades_Defense_Hit_Root");
        SetString(enemy, "deathAnimatorState", "Death");
        SetReference(skills, "enemy", enemy);
        SetReference(skills, "animationContract", animationRoot);
        SetReference(skills, "animator", animator);
        SetReference(motor, "enemy", enemy);
        SetReference(motor, "navigationAgent", agent);
        SetReference(motor, "body", body);
        SetReference(motor, "bodyCollider", capsule);
        SetEnum(motor, "animationMovementMode", 1); // ScriptedOnly: NavMesh owns normal movement.
        SetReference(vision, "origin", lockPoint);
        SetFloat(vision, "maximumDistance", 18f);
        SetFloat(vision, "fieldOfViewDegrees", 120f);
        SetFloat(vision, "eyeHeight", 0f);
        SetFloat(vision, "targetHeight", 1f);

        health.SetHealth(Mathf.Max(60, info.CharacterData.ResolveMaxHp()), Mathf.Max(60, info.CharacterData.ResolveMaxHp()));
        EditorUtility.SetDirty(root);
    }

    private static T Ensure<T>(GameObject root) where T : Component => root.GetComponent<T>() ?? root.AddComponent<T>();
    private static void SetReference(Object target, string name, Object value)
    {
        var property = new SerializedObject(target).FindProperty(name);
        if (property != null) { property.objectReferenceValue = value; property.serializedObject.ApplyModifiedPropertiesWithoutUndo(); }
    }
    private static void SetString(Object target, string name, string value)
    {
        var property = new SerializedObject(target).FindProperty(name);
        if (property != null) { property.stringValue = value; property.serializedObject.ApplyModifiedPropertiesWithoutUndo(); }
    }
    private static void SetFloat(Object target, string name, float value)
    {
        var property = new SerializedObject(target).FindProperty(name);
        if (property != null) { property.floatValue = value; property.serializedObject.ApplyModifiedPropertiesWithoutUndo(); }
    }
    private static void SetEnum(Object target, string name, int value)
    {
        var property = new SerializedObject(target).FindProperty(name);
        if (property != null) { property.enumValueIndex = value; property.serializedObject.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
