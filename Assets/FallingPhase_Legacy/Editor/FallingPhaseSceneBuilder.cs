using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class FallingPhaseSceneBuilder
{
    private const string RootPath = "Assets/FallingPhase_Legacy";
    private const string ScenePath = RootPath + "/FallingPhase.unity";
    private const string ControllerPath = RootPath + "/Animations/Player_Model_Falling.controller";
    private const string PlayerControllerPath = "Assets/Characters/4_Animations/Player_Model.controller";
    private const string LucianPrefabPath = "Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab";
    private const string InputActionsPath = "Assets/PlayerInputs.inputactions";
    private const string GrappleMaterialPath = "Assets/Environment/3_VFX/VFX_GlitteringStars/Material_Grapple.mat";

    [MenuItem("Lit/Legacy/Falling/Build FallingPhase")]
    public static void Build()
    {
        EnsureFolder(RootPath);
        EnsureFolder(RootPath + "/Animations");
        EnsureFolder(RootPath + "/Materials");
        EnsureFolder(RootPath + "/Prefabs");

        AnimatorController controller = CreateFallingController();
        Material staticMaterial = CreateMaterial("MAT_Falling_Static", new Color(0.25f, 0.72f, 0.9f));
        Material mobileMaterial = CreateMaterial("MAT_Falling_Mobile", new Color(1f, 0.37f, 0.16f));
        GameObject staticObstacle = CreateStaticObstaclePrefab(staticMaterial);
        GameObject mobileObstacle = CreateMobileObstaclePrefab(mobileMaterial);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateLight();
        FallingPlayerController player = CreatePlayer(controller);
        CreateCamera(player.transform, player);
        CreateSpawner(player, staticObstacle, mobileObstacle);
        CreateScore(player);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Legacy FallingPhase was created. Open Assets/FallingPhase_Legacy/FallingPhase.unity and press Play.");
    }

    [MenuItem("Lit/Legacy/Falling/Refresh Falling Animator")]
    public static void RefreshFallingAnimator()
    {
        CreateFallingController();
        AssetDatabase.SaveAssets();
        Debug.Log("Player_Model_Falling was refreshed.");
    }

    private static FallingPlayerController CreatePlayer(AnimatorController controller)
    {
        GameObject root = new GameObject("FallingLucian");
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;

        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 0.95f, 0f);
        collider.height = 1.9f;
        collider.radius = 0.38f;

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(LucianPrefabPath);
        if (source == null)
        {
            throw new FileNotFoundException("Lucian prefab was not found.", LucianPrefabPath);
        }

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source);
        model.name = "Lucian_Model";
        model.transform.SetParent(root.transform, false);
        DisableGameplayBehaviours(model);

        Animator animator = model.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            throw new MissingComponentException("Lucian prefab has no Animator.");
        }

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        FallingPlayerController player = root.AddComponent<FallingPlayerController>();
        root.AddComponent<FallingGrappleController>();
        SerializedObject playerData = new SerializedObject(player);
        playerData.FindProperty("inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        playerData.FindProperty("animator").objectReferenceValue = animator;
        playerData.ApplyModifiedPropertiesWithoutUndo();
        return player;
    }

    private static void DisableGameplayBehaviours(GameObject root)
    {
        Behaviour[] behaviours = root.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is Animator)
            {
                continue;
            }

            behaviours[i].enabled = false;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private static void CreateCamera(Transform player, FallingPlayerController playerController)
    {
        GameObject cameraObject = new GameObject("FallingCamera", typeof(Camera), typeof(AudioListener), typeof(FallingCameraRig));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 155f;
        camera.fieldOfView = 68f;
        camera.backgroundColor = new Color(0.008f, 0.018f, 0.04f);

        FallingCameraRig rig = cameraObject.GetComponent<FallingCameraRig>();
        SerializedObject data = new SerializedObject(rig);
        data.FindProperty("target").objectReferenceValue = player;
        data.FindProperty("player").objectReferenceValue = playerController;
        data.ApplyModifiedPropertiesWithoutUndo();
        cameraObject.transform.position = player.TransformPoint(new Vector3(0f, 2.4f, -6.8f));
        cameraObject.transform.LookAt(player.position + Vector3.up * 0.9f);
    }

    private static void CreateLight()
    {
        GameObject lightObject = new GameObject("Falling Key Light", typeof(Light));
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2.1f;
        light.color = new Color(0.55f, 0.72f, 1f);
        lightObject.transform.rotation = Quaternion.Euler(42f, -25f, 0f);
    }

    private static void CreateSpawner(FallingPlayerController player, GameObject staticObstacle, GameObject mobileObstacle)
    {
        GameObject spawnerObject = new GameObject("FallingObstacleSpawner");
        FallingObstacleSpawner spawner = spawnerObject.AddComponent<FallingObstacleSpawner>();
        SerializedObject data = new SerializedObject(spawner);
        data.FindProperty("player").objectReferenceValue = player;
        data.FindProperty("grappleMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(GrappleMaterialPath);
        SerializedProperty prefabs = data.FindProperty("obstaclePrefabs");
        prefabs.arraySize = 2;
        prefabs.GetArrayElementAtIndex(0).objectReferenceValue = staticObstacle;
        prefabs.GetArrayElementAtIndex(1).objectReferenceValue = mobileObstacle;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateScore(FallingPlayerController player)
    {
        GameObject scoreObject = new GameObject("FallingRunScore");
        FallingRunScore score = scoreObject.AddComponent<FallingRunScore>();
        SerializedObject data = new SerializedObject(score);
        data.FindProperty("player").objectReferenceValue = player;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateStaticObstaclePrefab(Material material)
    {
        string path = RootPath + "/Prefabs/FallingObstacle_Static.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.name = "FallingObstacle_Static";
        obstacle.transform.localScale = new Vector3(2.8f, 2.8f, 2.8f);
        obstacle.GetComponent<Renderer>().sharedMaterial = material;
        obstacle.AddComponent<FallingObstacle>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obstacle, path);
        Object.DestroyImmediate(obstacle);
        return prefab;
    }

    private static GameObject CreateMobileObstaclePrefab(Material material)
    {
        string path = RootPath + "/Prefabs/FallingObstacle_Mobile.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        GameObject obstacle = new GameObject("FallingObstacle_Mobile");
        FallingObstacle marker = obstacle.AddComponent<FallingObstacle>();
        FallingRotator rotator = obstacle.AddComponent<FallingRotator>();
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "OrbitingSphere";
        visual.transform.SetParent(obstacle.transform, false);
        visual.transform.localPosition = new Vector3(2.25f, 0f, 0f);
        visual.transform.localScale = Vector3.one * 2.3f;
        visual.GetComponent<Renderer>().sharedMaterial = material;
        SerializedObject data = new SerializedObject(rotator);
        data.FindProperty("orbitingVisual").objectReferenceValue = visual.transform;
        data.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obstacle, path);
        Object.DestroyImmediate(obstacle);
        return prefab;
    }

    private static Material CreateMaterial(string materialName, Color color)
    {
        string path = RootPath + "/Materials/" + materialName + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader) { name = materialName };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", color * 0.25f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static AnimatorController CreateFallingController()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null &&
            !AssetDatabase.CopyAsset(PlayerControllerPath, ControllerPath))
        {
            throw new IOException("Player_Model_Falling controller could not be created.");
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        EnsureParameter(controller, "FallingBoost", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "BoostCharge", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "FallingImpact", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "FallingGrapple", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState falling = FindOrCreateState(stateMachine, "Falling_Loop");
        AnimatorState charge = FindOrCreateState(stateMachine, "BoostCharge");
        AnimatorState boost = FindOrCreateState(stateMachine, "Falling_Boost");
        AnimatorState impact = FindOrCreateState(stateMachine, "Falling_Impact");
        AnimatorState grapple = FindOrCreateState(stateMachine, "Falling_Grapple");
        falling.motion = LoadClip("Assets/Characters/4_Animations/Mixamo_Flying.fbx");
        charge.motion = LoadClip("Assets/Raise Creation/Super_Fast_Fighting Pack/Animations/Style_One/Anim_SF_Moving_Backward.fbx");
        boost.motion = LoadClip("Assets/Raise Creation/Super_Fast_Fighting Pack/Animations/Style_One/Anim_SF_Strike_Fly.fbx");
        impact.motion = LoadClip("Assets/Raise Creation/Super_Fast_Fighting Pack/Animations/Style_One/Anim_SF_Get_Hit_Hard.fbx");
        grapple.motion = LoadClip("Assets/Raise Creation/Super_Fast_Fighting Pack/Animations/Style_One/Anim_SF_Strike_Fly.fbx");
        stateMachine.defaultState = falling;

        if (falling.transitions.Length == 0)
        {
            AnimatorStateTransition toBoost = falling.AddTransition(boost);
            toBoost.hasExitTime = false;
            toBoost.duration = 0.08f;
            toBoost.AddCondition(AnimatorConditionMode.If, 0f, "FallingBoost");

            AnimatorStateTransition toFalling = boost.AddTransition(falling);
            toFalling.hasExitTime = false;
            toFalling.duration = 0.12f;
            toFalling.AddCondition(AnimatorConditionMode.IfNot, 0f, "FallingBoost");

            AnimatorStateTransition impactToFalling = impact.AddTransition(falling);
            impactToFalling.hasExitTime = true;
            impactToFalling.exitTime = 0.9f;
            impactToFalling.duration = 0.08f;

            AnimatorStateTransition anyToImpact = stateMachine.AddAnyStateTransition(impact);
            anyToImpact.hasExitTime = false;
            anyToImpact.duration = 0.04f;
            anyToImpact.canTransitionToSelf = false;
            anyToImpact.AddCondition(AnimatorConditionMode.If, 0f, "FallingImpact");
        }

        EnsureBoolTransition(falling, charge, "BoostCharge", true, 0.06f);
        EnsureBoolTransition(charge, falling, "BoostCharge", false, 0.06f);
        EnsureTriggerTransition(stateMachine, grapple, "FallingGrapple", 0.04f);
        EnsureExitTransition(grapple, falling, 0.8f, 0.08f);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimatorState FindOrCreateState(AnimatorStateMachine stateMachine, string stateName)
    {
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state != null && states[i].state.name == stateName)
            {
                return states[i].state;
            }
        }

        return stateMachine.AddState(stateName, new Vector3(640f, 160f + states.Length * 80f, 0f));
    }

    private static void EnsureParameter(AnimatorController controller, string parameterName, AnimatorControllerParameterType type)
    {
        for (int i = 0; i < controller.parameters.Length; i++)
        {
            if (controller.parameters[i].name == parameterName)
            {
                return;
            }
        }

        controller.AddParameter(parameterName, type);
    }

    private static void EnsureBoolTransition(
        AnimatorState from,
        AnimatorState to,
        string parameterName,
        bool requiredValue,
        float duration)
    {
        AnimatorConditionMode condition = requiredValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;
        AnimatorStateTransition[] transitions = from.transitions;
        for (int i = 0; i < transitions.Length; i++)
        {
            AnimatorStateTransition transition = transitions[i];
            if (transition.destinationState != to || transition.conditions.Length != 1)
            {
                continue;
            }

            AnimatorCondition existing = transition.conditions[0];
            if (existing.parameter == parameterName && existing.mode == condition)
            {
                return;
            }
        }

        AnimatorStateTransition created = from.AddTransition(to);
        created.hasExitTime = false;
        created.duration = duration;
        created.AddCondition(condition, 0f, parameterName);
    }

    private static void EnsureTriggerTransition(
        AnimatorStateMachine stateMachine,
        AnimatorState destination,
        string parameterName,
        float duration)
    {
        AnimatorStateTransition[] transitions = stateMachine.anyStateTransitions;
        for (int i = 0; i < transitions.Length; i++)
        {
            AnimatorStateTransition transition = transitions[i];
            if (transition.destinationState == destination && transition.conditions.Length == 1 &&
                transition.conditions[0].parameter == parameterName)
            {
                return;
            }
        }

        AnimatorStateTransition created = stateMachine.AddAnyStateTransition(destination);
        created.hasExitTime = false;
        created.duration = duration;
        created.canTransitionToSelf = false;
        created.AddCondition(AnimatorConditionMode.If, 0f, parameterName);
    }

    private static void EnsureExitTransition(AnimatorState from, AnimatorState to, float exitTime, float duration)
    {
        AnimatorStateTransition[] transitions = from.transitions;
        for (int i = 0; i < transitions.Length; i++)
        {
            if (transitions[i].destinationState == to)
            {
                return;
            }
        }

        AnimatorStateTransition created = from.AddTransition(to);
        created.hasExitTime = true;
        created.exitTime = exitTime;
        created.duration = duration;
    }

    private static AnimationClip LoadClip(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }

        throw new FileNotFoundException("Animation clip was not found in asset.", path);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }
}
