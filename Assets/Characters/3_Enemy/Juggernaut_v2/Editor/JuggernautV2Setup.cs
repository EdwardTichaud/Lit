using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Ultrabolt.BrainsAI;

[InitializeOnLoad]
public static class JuggernautV2Setup
{
    public const string Folder = "Assets/Characters/3_Enemy/Juggernaut_v2";
    public const string PrefabPath = Folder + "/Juggernaut_v2.prefab";
    public const string ControllerPath = Folder + "/Juggernaut_v2.controller";
    public const string ScenePath = Folder + "/Juggernaut_v2_Test.unity";
    public const string OriginalPrefab = "Assets/Characters/3_Enemy/Juggernaut/Juggernaut_Combat.prefab";
    private const string OriginalFolder = "Assets/Characters/3_Enemy/Juggernaut/";
    static JuggernautV2Setup() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        const string request = "Library/JuggernautV2.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { Create(); File.WriteAllText("Library/JuggernautV2.result", "SUCCESS"); }
        catch (Exception exception) { File.WriteAllText("Library/JuggernautV2.result", exception.ToString()); Debug.LogException(exception); }
    }

    [MenuItem("Lit/Brains AI/Create Juggernaut V2")]
    public static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) { Validate(); return; }
        Directory.CreateDirectory(Folder + "/Animations");
        AssetDatabase.Refresh();
        AnimationClip Copy(string clip, string name, bool loop = false) =>
            IluviliraeSetup.CopyClip(ActorAnimationAudit.EnemySource(name) ?? OriginalFolder + clip + ".anim",
                name, loop, Folder, "Juggernaut_v2");
        var idle = Copy("InPlace_Twinblades_Idle_Inplace", "Idle", true);
        var walk = Copy("InPlace_Twinblades_Strafe_Walk_F_Inplace", "Walk", true);
        var run = Copy("InPlace_Twinblades_Strafe_Run_F_Inplace", "Run", true);
        var strike = Copy("Juggernaut_Strike", "Strike");
        var sweep = Copy("Juggernaut_Sweep", "Sweep");
        var followup = Copy("Juggernaut_Followup", "Followup");
        var hurt = Copy("InPlace_TwinSword_Hit_L_Inplace", "Hurt");
        var death = Copy("InPlace_Death_v2", "Death");
        var controller = IluviliraeSetup.CreateController(idle, walk, run, strike, hurt, death, ControllerPath);
        AddAttackVariants(controller, sweep, followup);
        var originalData = AssetDatabase.LoadAssetAtPath<CharacterData>(OriginalFolder + "Juggernaut.asset");
        var data = ScriptableObject.CreateInstance<CharacterData>();
        data.characterId = "juggernaut_v2_brainsai_test";
        data.characterName = "Juggernaut v2";
        data.isEnemy = true;
        data.hp = originalData != null ? originalData.hp : 300;
        if (originalData != null)
            data.vision = JsonUtility.FromJson<CharacterVisionSettings>(JsonUtility.ToJson(originalData.vision));
        // Match Iluvilirae: the actor itself must not obstruct its own view.
        data.vision.obstructionMask = LayerMask.GetMask("Default", "Ground", "Obstacle", "CameraObstruction");
        AssetDatabase.CreateAsset(data, Folder + "/Juggernaut_v2.asset");
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPrefab);
        if (original == null) throw new InvalidOperationException("Juggernaut_Combat missing.");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(original);
        try
        {
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "Juggernaut_v2";
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            // Remove legacy dependants before their required components.
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true).Reverse())
                if (component != null) UnityEngine.Object.DestroyImmediate(component);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                t.gameObject.layer = LayerMask.NameToLayer("Enemy");
            }
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                if (collider.isTrigger) UnityEngine.Object.DestroyImmediate(collider);
            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var agent = root.GetComponent<NavMeshAgent>();
            agent.enabled = false;
            agent.autoRepath = true;
            var fov = root.AddComponent<FOV>();
            fov.useExternalDetection = true;
            fov.canInvestigate = true;
            fov.investigateDuration = 3;
            fov.drawGizmos = false;
            fov.viewRadius = data.vision.maximumDistance;
            fov.viewAngle = data.vision.fieldOfViewDegrees;
            fov.fovOffset = Vector3.up * data.vision.eyeHeight;
            fov.onDetectTarget = new UnityEngine.Events.UnityEvent();
            fov.onLostTarget = new UnityEngine.Events.UnityEvent();
            fov.onEnterNone = new UnityEngine.Events.UnityEvent();
            fov.onEnterAlerted = new UnityEngine.Events.UnityEvent();
            fov.onEnterInvestigate = new UnityEngine.Events.UnityEvent();
            var brain = root.AddComponent<JuggernautV2Brain>();
            brain.characterData = data;
            brain.fov = fov;
            brain.brainType = Brain.BrainType.Engager;
            brain.canAttack = true;
            brain.attackCallType = Brain.AttackCallType.External;
            brain.maxAttacks = 3;
            brain.attackTime = new Vector2(2.8f, 4);
            brain.moveRange = new Vector2(1.8f, 3.2f);
            brain.runSpeed = 3.6f;
            brain.walkSpeed = 1.6f;
            brain.canPatrol = false;
            brain.jumpHeight = 1;
            brain.durationMultiplier = 0.4f;
            brain.jumpCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 1), new Keyframe(1, 0));
            brain.groupName = "JuggernautV2Test";
            brain.alarmGroups = Array.Empty<string>();
            brain.alarmRadius = 0;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            data.worldPrefab = prefab;
            EditorUtility.SetDirty(data);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        CreateScene();
        AssetDatabase.SaveAssets();
        LucianBrainsCombatSetup.UpgradeEnemy();
        Validate();
        Debug.Log("[Juggernaut v2] Independent Brains AI prefab and test scene created. Original unchanged.");
    }

    private static void AddAttackVariants(AnimatorController controller, AnimationClip sweep, AnimationClip followup)
    {
        var machine = controller.layers[0].stateMachine;
        var locomotion = machine.states.First(s => s.state.name == "Locomotion").state;
        var strike = machine.states.First(s => s.state.name == "Attack").state;
        strike.tag = "Attack";
        machine.anyStateTransitions.First(t => t.destinationState == strike).AddCondition(AnimatorConditionMode.Equals, 1, "Attack ID");
        var clips = new[] { sweep, followup };
        for (int i = 0; i < clips.Length; i++)
        {
            var state = machine.AddState(i == 0 ? "Attack_Sweep" : "Attack_Followup", new Vector3(740, 50 + i * 100));
            state.motion = clips[i]; state.tag = "Attack"; state.writeDefaultValues = false;
            var entry = machine.AddAnyStateTransition(state);
            entry.hasExitTime = false; entry.duration = 0.08f; entry.canTransitionToSelf = false;
            entry.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            entry.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            entry.AddCondition(AnimatorConditionMode.Equals, i + 2, "Attack ID");
            var exit = state.AddTransition(locomotion);
            exit.hasExitTime = true; exit.exitTime = 0.95f; exit.duration = 0.12f;
            exit.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
        }
        EditorUtility.SetDirty(controller);
    }

    private static void CreateScene()
    {
        if (!AssetDatabase.CopyAsset(IluviliraeSetup.ScenePath, ScenePath)) throw new InvalidOperationException("Unable to copy test arena.");
        var active = SceneManager.GetActiveScene();
        var mode = Application.isBatchMode && string.IsNullOrEmpty(active.path) ? OpenSceneMode.Single : OpenSceneMode.Additive;
        var scene = EditorSceneManager.OpenScene(ScenePath, mode);
        try
        {
            var arena = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<IluviliraeTestArena>(true)).Single();
            UnityEngine.Object.DestroyImmediate(arena.enemy.gameObject);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.position = new Vector3(0, 0, -4);
            var brain = instance.GetComponent<JuggernautV2Brain>();
            brain.detectionTargetOverride = arena.target;
            arena.enemy = brain;
            arena.enemyPrefab = prefab;
            arena.gameObject.name = "Juggernaut_v2_TestControls";
            // The larger actor needs its own NavMesh clearance.
            var settings = NavMesh.GetSettingsByID(brain.GetComponent<NavMeshAgent>().agentTypeID);
            settings.agentRadius = brain.GetComponent<NavMeshAgent>().radius;
            settings.agentHeight = brain.GetComponent<NavMeshAgent>().height;
            var sources = scene.GetRootGameObjects().Where(go => go.name == "TestFloor" || go.name == "VisionAndNavigationObstacle")
                .Select(go => new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(go.transform.position, go.transform.rotation, Vector3.one), size = go.transform.localScale }).ToList();
            var navigation = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(Vector3.zero, new Vector3(32, 12, 32)), Vector3.zero, Quaternion.identity);
            if (navigation == null) throw new InvalidOperationException("Juggernaut test NavMesh bake failed.");
            AssetDatabase.CreateAsset(navigation, Folder + "/Juggernaut_v2_Test_NavMesh.asset");
            arena.navigation = navigation;
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        }
    }

    [MenuItem("Lit/Brains AI/Validate Juggernaut V2")]
    public static void Validate()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Juggernaut v2 missing.");
        var brain = prefab.GetComponent<JuggernautV2Brain>();
        if (brain == null || brain.fov == null || !brain.fov.useExternalDetection || brain.characterData == null || brain.characterData.worldPrefab != prefab)
            throw new InvalidOperationException("Juggernaut v2 perception/data bindings invalid.");
        if (prefab.GetComponents<Brain>().Length != 1 || prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c == null || !(c is Brain) && !(c is FOV) && !(c is AnimationEvents)))
            throw new InvalidOperationException("Unexpected or missing legacy script on Juggernaut v2.");
        if (prefab.GetComponent<Rigidbody>() != null || prefab.GetComponent<SphereCollider>() != null)
            throw new InvalidOperationException("Legacy movement/detection components remain.");
        var animator = prefab.GetComponent<Animator>();
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPrefab);
        if (!animator.isHuman || animator.avatar != original.GetComponent<Animator>().avatar || animator.applyRootMotion)
            throw new InvalidOperationException("Juggernaut avatar/root motion invalid.");
        var controller = animator.runtimeAnimatorController as AnimatorController;
        var knockoutClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(CounterKnockoutSignalSetup.AnimationPath);
        // animationClips counts bindings, including the same clip in multiple states.
        int expectedClipBindings = 22 + (controller == null ? 0 : controller.layers[0].stateMachine.states.Count(s =>
            s.state.name == "Knocked Out" || knockoutClip != null && s.state.motion == knockoutClip));
        if (controller == null || AssetDatabase.GetAssetPath(controller) != ControllerPath || controller.animationClips.Length != expectedClipBindings)
            throw new InvalidOperationException("Juggernaut owned controller/animations invalid.");
        var knockoutMotion = controller.layers[0].stateMachine.states.FirstOrDefault(s => s.state.name == "Knocked Out").state?.motion;
        foreach (var clip in controller.animationClips)
            if (!clip.isHumanMotion || AnimationUtility.GetAnimationEvents(clip).Any(e => e.functionName != "ResolveBrainsAttackImpact" && e.functionName != "OpenBrainsReactionOpportunity" && e.functionName != "BeginBrainsStrike" && e.functionName != "BeginBrainsRecovery") ||
                !(AssetDatabase.GetAssetPath(clip).StartsWith(Folder + "/Animations/") || clip == knockoutMotion && AssetDatabase.GetAssetPath(clip) == CounterKnockoutSignalSetup.AnimationPath))
                throw new InvalidOperationException("Unexpected source clip/event on Juggernaut v2.");
        var before = original.GetComponentsInChildren<Renderer>(true);
        var after = prefab.GetComponentsInChildren<Renderer>(true);
        if (before.Length != after.Length || before.Where((renderer, i) => !renderer.sharedMaterials.SequenceEqual(after[i].sharedMaterials)).Any())
            throw new InvalidOperationException("Juggernaut visual materials changed.");
    }

    public static void RunSmoke()
    {
        Validate();
        IluviliraePlayModeValidation.RunScene(ScenePath, "JuggernautV2Smoke.result");
    }
}
