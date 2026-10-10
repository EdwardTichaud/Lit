using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Ultrabolt.BrainsAI;

/// <summary>Explicit, reproducible authoring of the isolated Brains AI enemy.</summary>
[InitializeOnLoad]
public static class IluviliraeSetup
{
    public const string Folder = "Assets/Characters/3_Enemy/Iluvilirae";
    public const string PrefabPath = Folder + "/Iluvilirae.prefab";
    public const string ScenePath = Folder + "/Iluvilirae_Test.unity";
    public const string ControllerPath = Folder + "/Iluvilirae.controller";
    private const string Request = "Library/Iluvilirae.request";
    private const string Result = "Library/Iluvilirae.result";
    static IluviliraeSetup() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        try
        {
            if (command == "validate") Validate();
            else Create();
            File.WriteAllText(Result, "SUCCESS " + command + " " + DateTime.UtcNow.ToString("O"));
        }
        catch (Exception e) { File.WriteAllText(Result, e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Lit/Brains AI/Create Iluvilirae Test Enemy")]
    public static void Create()
    {
        Directory.CreateDirectory(Folder + "/Animations");
        AssetDatabase.Refresh();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                CreateScene(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                AssetDatabase.SaveAssets();
            }
            Validate(); // Never overwrite subsequent Inspector authoring.
            return;
        }
        var idle = CopyClip("Assets/BrainsAI/Animations/Animators/Locomation-Idle.anim", "Idle", true);
        var walk = CopyClip("Assets/BrainsAI/Animations/Animators/Locomation-Walk.anim", "Walk", true);
        var run = CopyClip("Assets/BrainsAI/Animations/Animators/Locomation-Run-Forward.anim", "Run", true);
        var attack = CopyClip("Assets/BrainsAI/Animations/Animators/Attack.anim", "Attack", false);
        var hurt = CopyClip("Assets/Characters/3_Enemy/Juggernaut/InPlace_TwinSword_Hit_L_Inplace.anim", "Hurt", false);
        var death = CopyClip("Assets/BrainsAI/Animations/Animators/Dead.anim", "Death", false);
        var controller = CreateController(idle, walk, run, attack, hurt, death);
        var data = ScriptableObject.CreateInstance<CharacterData>();
        data.characterId = "iluvilirae_brainsai_test";
        data.characterName = "Iluvilirae";
        data.isEnemy = true;
        data.hp = 100;
        data.vision.maximumDistance = 14;
        data.vision.fieldOfViewDegrees = 140;
        data.vision.obstructionMask = LayerMask.GetMask("Default", "Ground", "Obstacle", "CameraObstruction");
        AssetDatabase.CreateAsset(data, Folder + "/Iluvilirae.asset");

        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BrainsAI/Prefabs/Ultrabot - Attacker AI.prefab");
        if (source == null) throw new InvalidOperationException("Brains AI attacker model is missing.");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "Iluvilirae";
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(component is FOV)) UnityEngine.Object.DestroyImmediate(component);
            int layer = LayerMask.NameToLayer("Enemy");
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            var fov = root.GetComponentInChildren<FOV>();
            fov.useExternalDetection = true;
            fov.canInvestigate = true;
            fov.investigateDuration = 3;
            fov.onDetectTarget = new UnityEngine.Events.UnityEvent();
            fov.onLostTarget = new UnityEngine.Events.UnityEvent();
            fov.onEnterNone = new UnityEngine.Events.UnityEvent();
            fov.onEnterAlerted = new UnityEngine.Events.UnityEvent();
            fov.onEnterInvestigate = new UnityEngine.Events.UnityEvent();
            fov.viewRadius = data.vision.maximumDistance;
            fov.viewAngle = data.vision.fieldOfViewDegrees;
            fov.fovOffset = Vector3.up * data.vision.eyeHeight;
            fov.drawGizmos = false; // Lit's perception is the source of truth.
            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var agent = root.GetComponent<NavMeshAgent>();
            agent.enabled = false;
            agent.radius = 0.35f;
            agent.height = 2;
            agent.acceleration = 12;
            agent.angularSpeed = 360;
            agent.autoRepath = true;
            var brain = root.AddComponent<IluviliraeBrain>();
            brain.characterData = data;
            brain.fov = fov;
            brain.brainType = Brain.BrainType.Engager;
            brain.canPatrol = false;
            brain.canAttack = true;
            brain.attackCallType = Brain.AttackCallType.External;
            brain.maxAttacks = 1;
            brain.attackTime = new Vector2(2.8f, 4);
            brain.moveRange = new Vector2(1.2f, 2.2f);
            brain.runSpeed = 3.5f;
            brain.walkSpeed = 1.5f;
            brain.jumpHeight = 1;
            brain.durationMultiplier = 0.4f;
            brain.jumpCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 1), new Keyframe(1, 0));
            brain.groupName = "IluviliraeTest";
            brain.alarmGroups = Array.Empty<string>();
            brain.alarmRadius = 0;
            var bodyMaterial = CreateMaterial("Iluvilirae_Body", new Color(0.14f, 0.09f, 0.23f), 0.45f);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => bodyMaterial).ToArray();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            data.worldPrefab = prefab;
            EditorUtility.SetDirty(data);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        CreateScene(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log("[Iluvilirae] Prefab, owned animations/controller and isolated test scene created.");
    }

    public static AnimationClip CopyClip(string source, string name, bool loop, string folder = Folder, string prefix = "Iluvilirae")
    {
        var original = source.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
            ? ActorAnimationAudit.Source(source) : AssetDatabase.LoadAssetAtPath<AnimationClip>(source);
        if (original == null || !original.isHumanMotion) throw new InvalidOperationException("Missing humanoid animation: " + source);
        var clip = UnityEngine.Object.Instantiate(original);
        clip.name = prefix + "_" + name;
        AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, folder + "/Animations/" + clip.name + ".anim");
        return clip;
    }

    public static AnimatorController CreateController(AnimationClip idle, AnimationClip walk, AnimationClip run, AnimationClip attack, AnimationClip hurt, AnimationClip death, string controllerPath = ControllerPath)
    {
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        foreach (string parameter in new[] { "Speed", "MoveX", "MoveY" }) controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack ID", AnimatorControllerParameterType.Int);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        var machine = controller.layers[0].stateMachine;
        var locomotion = machine.AddState("Locomotion", new Vector3(260, 80));
        locomotion.tag = "Moveable";
        locomotion.writeDefaultValues = false;
        var tree = new BlendTree { name = controller.name + " Locomotion", blendParameter = "Speed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(idle, 0); tree.AddChild(walk, 0.5f); tree.AddChild(run, 1);
        locomotion.motion = tree;
        machine.defaultState = locomotion;
        var attackState = AddAction(machine, "Attack", attack, new Vector3(500, 40));
        var hurtState = AddAction(machine, "Hurt", hurt, new Vector3(500, 140));
        var deathState = AddAction(machine, "Death", death, new Vector3(500, 240));
        // Highest priority first: death must interrupt hurt and attack.
        AddEntry(machine, deathState, "Dead", AnimatorConditionMode.If);
        AddEntry(machine, hurtState, "Hurt", AnimatorConditionMode.If);
        AddEntry(machine, attackState, "Attack", AnimatorConditionMode.If);
        foreach (var state in new[] { attackState, hurtState })
        {
            var transition = state.AddTransition(locomotion);
            transition.hasExitTime = true;
            transition.exitTime = 0.95f;
            transition.duration = 0.12f;
            transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
        }
        return controller;
    }

    private static AnimatorState AddAction(AnimatorStateMachine machine, string name, AnimationClip clip, Vector3 position)
    {
        var state = machine.AddState(name, position);
        state.motion = clip;
        state.tag = "Action";
        state.writeDefaultValues = false;
        return state;
    }
    private static void AddEntry(AnimatorStateMachine machine, AnimatorState state, string condition, AnimatorConditionMode mode)
    {
        var transition = machine.AddAnyStateTransition(state);
        transition.hasExitTime = false;
        transition.duration = 0.08f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(mode, 0, condition);
        if (condition != "Dead") transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
    }
    private static Material CreateMaterial(string name, Color color, float smoothness)
    {
        var material = new Material(Shader.Find("HDRP/Lit")) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(material, Folder + "/" + name + ".mat");
        return material;
    }

    private static void CreateScene(GameObject prefab)
    {
        var active = SceneManager.GetActiveScene();
        var mode = Application.isBatchMode && string.IsNullOrEmpty(active.path) ? NewSceneMode.Single : NewSceneMode.Additive;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
        SceneManager.SetActiveScene(scene);
        try
        {
            var floorMaterial = CreateMaterial("Iluvilirae_Arena", new Color(0.22f, 0.25f, 0.3f), 0.15f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TestFloor";
            floor.transform.position = new Vector3(0, -0.25f, 0);
            floor.transform.localScale = new Vector3(30, 0.5f, 30);
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "VisionAndNavigationObstacle";
            obstacle.transform.position = new Vector3(4, 1.5f, 0);
            obstacle.transform.localScale = new Vector3(2, 3, 5);
            obstacle.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            var sources = new[] { floor, obstacle }.Select(go => new NavMeshBuildSource {
                shape = NavMeshBuildSourceShape.Box, transform = Matrix4x4.TRS(go.transform.position, go.transform.rotation, Vector3.one),
                size = go.transform.localScale, area = 0 }).ToList();
            var navigation = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(Vector3.zero, new Vector3(32, 12, 32)), Vector3.zero, Quaternion.identity);
            if (navigation == null) throw new InvalidOperationException("Test NavMesh bake failed.");
            AssetDatabase.CreateAsset(navigation, Folder + "/Iluvilirae_Test_NavMesh.asset");
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            target.name = "TestPlayerTarget_ArrowKeys";
            target.transform.position = new Vector3(0, 0, 5);
            // Keep feet at the target origin for the shared vision settings.
            var targetRoot = new GameObject("TestPlayerTarget");
            targetRoot.transform.position = target.transform.position;
            target.transform.SetParent(targetRoot.transform);
            target.transform.localPosition = Vector3.up;
            target.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Iluvilirae_TestTarget", new Color(0.12f, 0.6f, 0.55f), 0.25f);
            target.layer = LayerMask.NameToLayer("Player");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.position = new Vector3(0, 0, -4);
            var brain = instance.GetComponent<IluviliraeBrain>();
            brain.detectionTargetOverride = targetRoot.transform;
            var arena = new GameObject("Iluvilirae_TestControls").AddComponent<IluviliraeTestArena>();
            arena.navigation = navigation;
            arena.target = targetRoot.transform;
            arena.enemy = brain;
            arena.enemyPrefab = prefab;
            var camera = new GameObject("TestCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 17, -15);
            camera.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.05f);
            var light = new GameObject("TestKeyLight", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            light.intensity = 12000;
            light.shadows = LightShadows.Soft;
            light.gameObject.AddComponent<HDAdditionalLightData>();
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, Folder + "/Iluvilirae_Test_Volume.asset");
            var exposure = profile.Add<Exposure>(true);
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(9);
            AssetDatabase.AddObjectToAsset(exposure, profile);
            var volume = new GameObject("TestExposure").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        }
    }

    [MenuItem("Lit/Brains AI/Validate Iluvilirae")]
    public static void Validate()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Iluvilirae prefab missing.");
        var brain = prefab.GetComponent<IluviliraeBrain>();
        if (brain == null || brain.fov == null || !brain.fov.useExternalDetection || brain.characterData == null)
            throw new InvalidOperationException("Iluvilirae perception bindings missing.");
        if (prefab.GetComponentInChildren<EnemyController>(true) != null || prefab.GetComponents<Brain>().Length != 1)
            throw new InvalidOperationException("Iluvilirae must have exactly one Brains AI brain and no legacy EnemyController.");
        var animator = prefab.GetComponent<Animator>();
        if (!animator.isHuman || animator.runtimeAnimatorController == null || animator.applyRootMotion)
            throw new InvalidOperationException("Iluvilirae humanoid/Animator invalid.");
        var controller = (AnimatorController)animator.runtimeAnimatorController;
        foreach (var state in controller.layers[0].stateMachine.states)
            if (state.state.motion == null) throw new InvalidOperationException("Empty animation state: " + state.state.name);
        foreach (var clip in controller.animationClips)
            if (!clip.isHumanMotion || AnimationUtility.GetAnimationEvents(clip).Length != 0 || !AssetDatabase.GetAssetPath(clip).StartsWith(Folder))
                throw new InvalidOperationException("Invalid owned animation: " + clip.name);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null || AssetDatabase.LoadAssetAtPath<NavMeshData>(Folder + "/Iluvilirae_Test_NavMesh.asset") == null)
            throw new InvalidOperationException("Iluvilirae test scene/NavMesh missing.");
    }
}
