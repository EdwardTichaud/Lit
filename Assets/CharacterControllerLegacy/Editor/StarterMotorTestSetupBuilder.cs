using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StarterMotorTestSetupBuilder
{
    private const string SourcePrefabPath = "Assets/Prefabs/Character/Player_Model_MechanicGirl.prefab";
    private const string TestPrefabPath = "Assets/Prefabs/Character/Player_Model_MechanicGirl_StarterMotorTest.prefab";
    private const string TestScenePath = "Assets/CharacterControllerLegacy/Scenes/StarterMotorTest.unity";
    private const string TestAnimatorControllerPath = "Assets/Animations/Player_Model_StarterMotorTest.controller";

    [MenuItem("Tools/Movement/Create Starter Motor Test Setup")]
    public static void BuildFromMenu()
    {
        BuildFromBatchmode();
    }

    public static void BuildFromBatchmode()
    {
        EnsureTestAnimatorController();
        CreateOrUpdateTestPrefab();
        CreateOrUpdateTestScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Starter motor test setup ready: prefab='{TestPrefabPath}', scene='{TestScenePath}'");
    }

    public static void ValidateGeneratedSetup()
    {
        EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);

        GameObject testCharacter = GameObject.Find("Player_Model_MechanicGirl_StarterMotorTest");
        if (testCharacter == null)
        {
            throw new InvalidOperationException("Starter motor validation failed: test character not found.");
        }

        StarterInspiredThirdPersonMotor motor = testCharacter.GetComponent<StarterInspiredThirdPersonMotor>();
        if (motor == null)
        {
            throw new InvalidOperationException("Starter motor validation failed: motor component not found.");
        }

        StarterMotorLocalInputBridge inputBridge = testCharacter.GetComponent<StarterMotorLocalInputBridge>();
        if (inputBridge != null)
        {
            inputBridge.enabled = false;
        }

        StarterMotorAnimatorDriver animatorDriver = testCharacter.GetComponent<StarterMotorAnimatorDriver>();
        if (animatorDriver == null)
        {
            throw new InvalidOperationException("Starter motor validation failed: animator driver component not found.");
        }

        Animator animator = testCharacter.GetComponent<Animator>();
        if (animator == null)
        {
            throw new InvalidOperationException("Starter motor validation failed: Animator component not found.");
        }

        RuntimeAnimatorController expectedController =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(TestAnimatorControllerPath);
        if (expectedController == null || animator.runtimeAnimatorController != expectedController)
        {
            throw new InvalidOperationException(
                "Starter motor validation failed: isolated animator controller is not assigned.");
        }

        MethodInfo tickMethod = typeof(StarterInspiredThirdPersonMotor).GetMethod(
            "Tick",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (tickMethod == null)
        {
            throw new InvalidOperationException("Starter motor validation failed: Tick method not found.");
        }

        MethodInfo animationTickMethod = typeof(StarterMotorAnimatorDriver).GetMethod(
            "Tick",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (animationTickMethod == null)
        {
            throw new InvalidOperationException("Starter motor validation failed: animation Tick method not found.");
        }

        ValidateForwardMovement(testCharacter, motor, tickMethod);
        ValidateBraking(testCharacter, motor, tickMethod);
        ValidateHardReversal(testCharacter, motor, tickMethod);
        ValidateWallCollision(testCharacter, motor, tickMethod);
        ValidateFlatGrounding(testCharacter, motor, tickMethod);
        ValidateSlopeGrounding(testCharacter, motor, tickMethod);
        ValidateSmallDropSnap(testCharacter, motor, tickMethod);
        ValidateLedgeFall(testCharacter, motor, tickMethod);
        ValidateIdleJump(testCharacter, motor, tickMethod);
        ValidateMovingJump(testCharacter, motor, tickMethod);
        ValidateTinyDropDoesNotTriggerLanding(testCharacter, motor, tickMethod);
        ValidateMediumFallLanding(testCharacter, motor, tickMethod);
        ValidateHighFallLanding(testCharacter, motor, tickMethod);
        ValidateFlightCruiseAndLandingRecovery(testCharacter, motor, tickMethod);
        ValidateFlightLandingDoesNotGlide(testCharacter, motor, tickMethod);
        ValidateAnimationDriving(testCharacter, motor, animatorDriver, animator, tickMethod, animationTickMethod);
        ValidateLadderMotorCompatibility(testCharacter, motor, tickMethod);

        Debug.Log("Starter motor validation passed: planar movement, braking, hard reversal, wall collision, grounding, slope grounding, small-drop stability, edge falling, jump, free fall, landing, flight cruise/landing recovery, flight landing glide control, minimal animation driving and ladder motor suspension behaved correctly.");
    }

    private static void EnsureTestAnimatorController()
    {
        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(TestAnimatorControllerPath);
        if (controller == null)
        {
            throw new InvalidOperationException(
                $"Starter motor test setup missing isolated animator controller at '{TestAnimatorControllerPath}'.");
        }
    }

    private static void CreateOrUpdateTestPrefab()
    {
        if (!AssetDatabase.CopyAsset(SourcePrefabPath, TestPrefabPath) &&
            AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabPath) == null)
        {
            Debug.LogError($"Could not create test prefab from '{SourcePrefabPath}'.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(TestPrefabPath);
        try
        {
            root.name = "Player_Model_MechanicGirl_StarterMotorTest";
            DisableOldRuntimeFlow(root);

            CharacterController controller = root.GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = root.AddComponent<CharacterController>();
            }

            ConfigureCharacterController(controller);

            StarterInspiredThirdPersonMotor motor = root.GetComponent<StarterInspiredThirdPersonMotor>();
            if (motor == null)
            {
                motor = root.AddComponent<StarterInspiredThirdPersonMotor>();
            }

            StarterMotorLocalInputBridge inputBridge = root.GetComponent<StarterMotorLocalInputBridge>();
            if (inputBridge == null)
            {
                inputBridge = root.AddComponent<StarterMotorLocalInputBridge>();
            }

            Animator animator = root.GetComponent<Animator>();
            StarterMotorAnimatorDriver animatorDriver = root.GetComponent<StarterMotorAnimatorDriver>();
            if (animatorDriver == null)
            {
                animatorDriver = root.AddComponent<StarterMotorAnimatorDriver>();
            }

            ConfigureMotor(motor, controller);
            ConfigureInputBridge(inputBridge, motor);
            ConfigureAnimator(animator);
            ConfigureAnimatorDriver(animatorDriver, motor, animator);

            PrefabUtility.SaveAsPrefabAsset(root, TestPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void DisableOldRuntimeFlow(GameObject root)
    {
        SquadCharacterController oldController = root.GetComponent<SquadCharacterController>();
        if (oldController != null)
        {
            oldController.enabled = false;
        }

        Rigidbody body = root.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.useGravity = false;
            body.isKinematic = true;
            body.detectCollisions = false;
            body.interpolation = RigidbodyInterpolation.None;
        }

        foreach (CapsuleCollider capsule in root.GetComponents<CapsuleCollider>())
        {
            capsule.enabled = false;
        }

        foreach (SphereCollider sphere in root.GetComponents<SphereCollider>())
        {
            sphere.enabled = false;
        }

        LocalVoiceLineController voiceLineController = root.GetComponent<LocalVoiceLineController>();
        if (voiceLineController != null)
        {
            voiceLineController.enabled = false;
        }

        AudioListener audioListener = root.GetComponent<AudioListener>();
        if (audioListener != null)
        {
            audioListener.enabled = false;
        }
    }

    private static void ConfigureCharacterController(CharacterController controller)
    {
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.height = 1.8f;
        controller.radius = 0.45f;
        controller.slopeLimit = 50f;
        controller.stepOffset = 0f;
        controller.skinWidth = 0.06f;
        controller.minMoveDistance = 0f;
    }

    private static void ConfigureMotor(StarterInspiredThirdPersonMotor motor, CharacterController controller)
    {
        SerializedObject serializedMotor = new SerializedObject(motor);
        serializedMotor.FindProperty("characterController").objectReferenceValue = controller;
        serializedMotor.FindProperty("cameraTransform").objectReferenceValue = null;
        serializedMotor.FindProperty("autoResolveMainCamera").boolValue = true;
        serializedMotor.FindProperty("inputDeadZone").floatValue = 0.12f;
        serializedMotor.FindProperty("walkMoveSpeed").floatValue = 5f;
        serializedMotor.FindProperty("maxMoveSpeed").floatValue = 6.5f;
        serializedMotor.FindProperty("acceleration").floatValue = 11f;
        serializedMotor.FindProperty("deceleration").floatValue = 14f;
        serializedMotor.FindProperty("hardReverseAngle").floatValue = 135f;
        serializedMotor.FindProperty("hardReverseBrakeMultiplier").floatValue = 1.4f;
        serializedMotor.FindProperty("lowSpeedTurnRate").floatValue = 620f;
        serializedMotor.FindProperty("moveTurnRate").floatValue = 500f;
        serializedMotor.FindProperty("sprintTurnRate").floatValue = 360f;
        serializedMotor.FindProperty("groundMask").intValue = ~0;
        serializedMotor.FindProperty("groundProbeRadiusScale").floatValue = 0.9f;
        serializedMotor.FindProperty("groundProbeDistance").floatValue = 0.18f;
        serializedMotor.FindProperty("groundProbeStartOffset").floatValue = 0.08f;
        serializedMotor.FindProperty("maxGroundAngle").floatValue = 50f;
        serializedMotor.FindProperty("groundedGraceTime").floatValue = 0.1f;
        serializedMotor.FindProperty("enableStepTraversal").boolValue = true;
        serializedMotor.FindProperty("maxStepRise").floatValue = 0.35f;
        serializedMotor.FindProperty("maxStepDrop").floatValue = 0.45f;
        serializedMotor.FindProperty("minStepRise").floatValue = 0.03f;
        serializedMotor.FindProperty("stepSearchDistance").floatValue = 0.9f;
        serializedMotor.FindProperty("stepSearchExtraDistance").floatValue = 0.22f;
        serializedMotor.FindProperty("stepSurfaceInset").floatValue = 0.08f;
        serializedMotor.FindProperty("stepContactOffset").floatValue = 0.03f;
        serializedMotor.FindProperty("gravity").floatValue = -24f;
        serializedMotor.FindProperty("maxFallSpeed").floatValue = 35f;
        serializedMotor.FindProperty("groundedStickVelocity").floatValue = 2f;
        serializedMotor.FindProperty("groundSnapDistance").floatValue = 0.35f;
        serializedMotor.FindProperty("jumpImpulse").floatValue = 7f;
        serializedMotor.FindProperty("jumpInputBufferTime").floatValue = 0.12f;
        serializedMotor.FindProperty("jumpGroundedGraceTime").floatValue = 0.08f;
        serializedMotor.FindProperty("jumpGroundIgnoreTime").floatValue = 0.12f;
        serializedMotor.FindProperty("enableFlight").boolValue = true;
        serializedMotor.FindProperty("flightTakeoffVerticalSpeed").floatValue = 6.5f;
        serializedMotor.FindProperty("flightTakeoffDuration").floatValue = 0.45f;
        serializedMotor.FindProperty("flightTakeoffDamping").floatValue = 16f;
        serializedMotor.FindProperty("flightCruiseSpeed").floatValue = 33f;
        serializedMotor.FindProperty("flightBoostSpeed").floatValue = 81f;
        serializedMotor.FindProperty("flightAcceleration").floatValue = 54f;
        serializedMotor.FindProperty("flightBoostAcceleration").floatValue = 126f;
        serializedMotor.FindProperty("flightDeceleration").floatValue = 36f;
        serializedMotor.FindProperty("flightStopDecelerationMultiplier").floatValue = 3f;
        serializedMotor.FindProperty("flightVerticalSpeed").floatValue = 24f;
        serializedMotor.FindProperty("flightVerticalAcceleration").floatValue = 66f;
        serializedMotor.FindProperty("flightVerticalDeceleration").floatValue = 54f;
        serializedMotor.FindProperty("flightVerticalDeadZone").floatValue = 0.05f;
        serializedMotor.FindProperty("flightIdleSpeedThreshold").floatValue = 0.08f;
        serializedMotor.FindProperty("flightTurnRate").floatValue = 760f;
        serializedMotor.FindProperty("flightBoostTurnRate").floatValue = 460f;
        serializedMotor.FindProperty("flightExitDownwardVelocity").floatValue = 1.5f;
        serializedMotor.FindProperty("flightBoostKickSpeed").floatValue = 4.5f;
        serializedMotor.FindProperty("flightGroundContactLandingMinSpeed").floatValue = 2.75f;
        serializedMotor.FindProperty("flightGroundContactLandingMinDownwardSpeed").floatValue = 0.2f;
        serializedMotor.FindProperty("flightLandingPlanarVelocityRetention").floatValue = 0.25f;
        serializedMotor.FindProperty("flightLandingDampingMultiplier").floatValue = 1f;
        serializedMotor.FindProperty("flightLandingControlGraceTime").floatValue = 0.08f;
        serializedMotor.FindProperty("freeFallMinAirborneTime").floatValue = 0.18f;
        serializedMotor.FindProperty("freeFallMinDownwardSpeed").floatValue = 1.5f;
        serializedMotor.FindProperty("landingMinAirborneTime").floatValue = 0.14f;
        serializedMotor.FindProperty("landingMinDownwardSpeed").floatValue = 2.5f;
        serializedMotor.FindProperty("mediumLandingDownwardSpeed").floatValue = 7f;
        serializedMotor.FindProperty("heavyLandingDownwardSpeed").floatValue = 10f;
        serializedMotor.FindProperty("landingDampingDuration").floatValue = 0.18f;
        serializedMotor.FindProperty("lightLandingDamping").floatValue = 14f;
        serializedMotor.FindProperty("mediumLandingDamping").floatValue = 26f;
        serializedMotor.FindProperty("heavyLandingDamping").floatValue = 38f;
        serializedMotor.FindProperty("enableAirborneWallSlide").boolValue = true;
        serializedMotor.FindProperty("wallSlideMaxNormalY").floatValue = 0.35f;
        serializedMotor.FindProperty("wallSlideContactMemoryTime").floatValue = 0.08f;
        serializedMotor.FindProperty("wallSlideMinDownwardSpeed").floatValue = 8f;
        serializedMotor.FindProperty("wallSlideGravityMultiplier").floatValue = 1.25f;
        serializedMotor.FindProperty("showDebugValues").boolValue = true;
        serializedMotor.FindProperty("showDebugGizmos").boolValue = true;
        serializedMotor.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureInputBridge(StarterMotorLocalInputBridge inputBridge, StarterInspiredThirdPersonMotor motor)
    {
        SerializedObject serializedBridge = new SerializedObject(inputBridge);
        serializedBridge.FindProperty("motor").objectReferenceValue = motor;
        serializedBridge.FindProperty("readKeyboard").boolValue = true;
        serializedBridge.FindProperty("readGamepad").boolValue = true;
        serializedBridge.FindProperty("readJump").boolValue = true;
        serializedBridge.FindProperty("readFlightControls").boolValue = true;
        serializedBridge.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureAnimator(Animator animator)
    {
        if (animator == null)
        {
            return;
        }

        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        RuntimeAnimatorController testController =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(TestAnimatorControllerPath);
        if (testController != null)
        {
            animator.runtimeAnimatorController = testController;
        }
    }

    private static void ConfigureAnimatorDriver(
        StarterMotorAnimatorDriver animatorDriver,
        StarterInspiredThirdPersonMotor motor,
        Animator animator)
    {
        SerializedObject serializedDriver = new SerializedObject(animatorDriver);
        serializedDriver.FindProperty("motor").objectReferenceValue = motor;
        serializedDriver.FindProperty("animator").objectReferenceValue = animator;
        serializedDriver.FindProperty("disableRootMotion").boolValue = true;
        serializedDriver.FindProperty("motorFullSpeed").floatValue = 6.5f;
        serializedDriver.FindProperty("locomotionBlendMax").floatValue = 3.25f;
        serializedDriver.FindProperty("speedDampTime").floatValue = 0.05f;
        serializedDriver.FindProperty("motionSpeedDampTime").floatValue = 0.05f;
        serializedDriver.FindProperty("movingEnterSpeedThreshold").floatValue = 0.32f;
        serializedDriver.FindProperty("movingExitSpeedThreshold").floatValue = 0.12f;
        serializedDriver.FindProperty("landingVisualHoldTime").floatValue = 0.34f;
        serializedDriver.FindProperty("flightLandingVisualHoldTime").floatValue = 0.16f;
        serializedDriver.FindProperty("crossFadeJumpStates").boolValue = true;
        serializedDriver.FindProperty("jumpCrossFadeDuration").floatValue = 0.08f;
        serializedDriver.FindProperty("fallCrossFadeDuration").floatValue = 0.1f;
        serializedDriver.FindProperty("landingCrossFadeDuration").floatValue = 0.08f;
        serializedDriver.FindProperty("animatorLayer").intValue = 0;
        serializedDriver.FindProperty("flightFullSpeed").floatValue = 81f;
        serializedDriver.FindProperty("flightMoveSpeedThreshold").floatValue = 0.7f;
        serializedDriver.FindProperty("flightMoveExitSpeedThreshold").floatValue = 0.35f;
        serializedDriver.FindProperty("flightCrossFadeDuration").floatValue = 0.08f;
        serializedDriver.FindProperty("flightIdleMotionSpeed").floatValue = 0.85f;
        serializedDriver.FindProperty("flightBoostMotionSpeed").floatValue = 1.45f;
        serializedDriver.FindProperty("flightTakeoffMotionSpeed").floatValue = 1f;
        serializedDriver.FindProperty("flightStopMinSpeed").floatValue = 1.2f;
        serializedDriver.FindProperty("flightStopExitSpeedThreshold").floatValue = 0.35f;
        serializedDriver.FindProperty("flightStopVisualHoldTime").floatValue = 0.18f;
        serializedDriver.FindProperty("flightStopCrossFadeDuration").floatValue = 0.05f;
        serializedDriver.FindProperty("flightBoostVisualHoldTime").floatValue = 0.22f;
        serializedDriver.FindProperty("flightDashCrossFadeDuration").floatValue = 0.04f;
        serializedDriver.FindProperty("flightDashExitNormalizedTime").floatValue = 0.98f;
        serializedDriver.FindProperty("flyingIdleStateName").stringValue = "Flying_Idle";
        serializedDriver.FindProperty("flyingMoveStateName").stringValue = "Flying_Loop";
        serializedDriver.FindProperty("flyingStopStateName").stringValue = "Flying_Stop";
        serializedDriver.FindProperty("flyingDashStateName").stringValue = "Flying_Dash";
        serializedDriver.FindProperty("showDebugValues").boolValue = true;
        serializedDriver.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateOrUpdateTestScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientLight = new Color(0.45f, 0.48f, 0.52f);

        Camera camera = CreateCamera();
        CreateDirectionalLight();
        CreateFloor();
        CreateWall("Wall_Front", new Vector3(0f, 1f, 7f), new Vector3(8f, 2f, 0.4f));
        CreateWall("Wall_Right", new Vector3(4f, 1f, 1.5f), new Vector3(0.4f, 2f, 5f));
        CreateWall("Wall_SmallObstacle", new Vector3(-2f, 0.35f, 2f), new Vector3(2f, 0.7f, 0.35f));
        CreateWall("SmallDropPlatform", new Vector3(2f, 0.1f, -3f), new Vector3(3f, 0.2f, 2f));
        CreateWall("DropPlatform", new Vector3(-8f, 1f, -1f), new Vector3(4f, 0.2f, 4f));
        CreateWall("DropLowerFloor", new Vector3(-10f, -0.05f, -1f), new Vector3(8f, 0.1f, 8f));
        CreateWall("HighDropPlatform", new Vector3(-16f, 3f, -1f), new Vector3(4f, 0.2f, 4f));
        CreateWall("HighDropLowerFloor", new Vector3(-18f, -0.05f, -1f), new Vector3(8f, 0.1f, 8f));
        CreateSlope("Slope_25", new Vector3(6.5f, 0.45f, -1f), new Vector3(4f, 0.25f, 5f), -25f);
        CreateWall("Uneven_Block_A", new Vector3(-1.5f, 0.035f, -3.5f), new Vector3(1.1f, 0.07f, 1f));
        CreateWall("Uneven_Block_B", new Vector3(-0.4f, 0.055f, -3.35f), new Vector3(1.1f, 0.11f, 1f));
        CreateWall("Uneven_Block_C", new Vector3(0.7f, 0.025f, -3.6f), new Vector3(1.1f, 0.05f, 1f));
        CreateLadderFixture();

        GameObject testPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabPath);
        GameObject testCharacter = PrefabUtility.InstantiatePrefab(testPrefab, scene) as GameObject;
        if (testCharacter != null)
        {
            testCharacter.transform.position = Vector3.zero;
            testCharacter.transform.rotation = Quaternion.identity;

            StarterInspiredThirdPersonMotor motor = testCharacter.GetComponent<StarterInspiredThirdPersonMotor>();
            if (motor != null)
            {
                SerializedObject serializedMotor = new SerializedObject(motor);
                serializedMotor.FindProperty("cameraTransform").objectReferenceValue = camera.transform;
                serializedMotor.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        EditorSceneManager.SaveScene(scene, TestScenePath);
    }

    private static Camera CreateCamera()
    {
        GameObject cameraObject = new GameObject("StarterMotorTestCamera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 4.5f, -7f);
        cameraObject.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -0.45f, 1f).normalized, Vector3.up);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 500f;
        camera.fieldOfView = 55f;
        cameraObject.AddComponent<AudioListener>();
        return camera;
    }

    private static void CreateDirectionalLight()
    {
        GameObject lightObject = new GameObject("Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
    }

    private static void CreateFloor()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "StarterMotorTestFloor";
        floor.transform.position = new Vector3(0f, -0.05f, 1.5f);
        floor.transform.localScale = new Vector3(12f, 0.1f, 14f);
    }

    private static void CreateWall(string name, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.position = position;
        wall.transform.localScale = scale;
    }

    private static void CreateSlope(string name, Vector3 position, Vector3 scale, float xRotation)
    {
        GameObject slope = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slope.name = name;
        slope.transform.position = position;
        slope.transform.rotation = Quaternion.Euler(xRotation, 0f, 0f);
        slope.transform.localScale = scale;
    }

    private static void CreateLadderFixture()
    {
        GameObject ladder = new GameObject("StarterMotorTestLadder");
        ladder.transform.position = new Vector3(8f, 0f, -4f);
        ladder.transform.rotation = Quaternion.identity;
        ladder.AddComponent<LadderController>();

        CreateLadderPoint(ladder.transform, "B_Trigger", new Vector3(0f, 0f, 0f), Quaternion.identity);
        CreateLadderPoint(ladder.transform, "H_Trigger", new Vector3(0f, 3f, 0f), Quaternion.identity);
        CreateLadderPoint(ladder.transform, "H_Exit", new Vector3(0f, 3f, 1.2f), Quaternion.identity);
        CreateLadderPoint(ladder.transform, "B_Exit", new Vector3(0f, 0f, -1.2f), Quaternion.identity);

        CreateLadderLandingPad(ladder.transform, "LowerLandingPad", new Vector3(0f, -0.05f, -0.6f), new Vector3(3f, 0.1f, 2.4f));
        CreateLadderLandingPad(ladder.transform, "UpperLandingPad", new Vector3(0f, 2.95f, 1.2f), new Vector3(3f, 0.1f, 2f));

        GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rail.name = "StarterMotorTestLadder_Rails";
        rail.transform.SetParent(ladder.transform, false);
        rail.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        rail.transform.localScale = new Vector3(0.8f, 3f, 0.08f);
    }

    private static void CreateLadderPoint(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
    {
        GameObject point = new GameObject(name);
        point.transform.SetParent(parent, false);
        point.transform.localPosition = localPosition;
        point.transform.localRotation = localRotation;
    }

    private static void CreateLadderLandingPad(Transform parent, string name, Vector3 localPosition, Vector3 localScale)
    {
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = name;
        pad.transform.SetParent(parent, false);
        pad.transform.localPosition = localPosition;
        pad.transform.localScale = localScale;
    }

    private static void ValidateForwardMovement(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        MethodInfo tickMethod)
    {
        ResetCharacter(character, motor, Vector3.zero);

        motor.SetMoveInput(Vector2.up);
        TickMotor(motor, tickMethod, 60);

        if (character.transform.position.z < 0.75f)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: forward movement too small, z={character.transform.position.z:0.###}.");
        }

        if (motor.ActualSpeed <= 0.5f)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: actual speed did not increase, speed={motor.ActualSpeed:0.###}.");
        }

        if (motor.CurrentState != StarterInspiredThirdPersonMotor.MovementState.Locomotion)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: expected Locomotion, got {motor.CurrentState}.");
        }
    }

    private static void ValidateBraking(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        MethodInfo tickMethod)
    {
        float speedBeforeBrake = motor.ActualSpeed;
        motor.Stop();
        TickMotor(motor, tickMethod, 45);

        if (motor.ActualSpeed >= speedBeforeBrake)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: braking did not reduce speed, before={speedBeforeBrake:0.###}, after={motor.ActualSpeed:0.###}.");
        }

        if (motor.CurrentState != StarterInspiredThirdPersonMotor.MovementState.Idle)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: expected Idle after braking, got {motor.CurrentState}.");
        }
    }

    private static void ValidateHardReversal(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        MethodInfo tickMethod)
    {
        ResetCharacter(character, motor, Vector3.zero);

        motor.SetMoveInput(Vector2.up);
        TickMotor(motor, tickMethod, 50);
        float speedBeforeReverse = motor.ActualSpeed;

        motor.SetMoveInput(Vector2.down);
        TickMotor(motor, tickMethod, 1);

        if (motor.CurrentState != StarterInspiredThirdPersonMotor.MovementState.Brake)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: expected Brake on hard reversal, got {motor.CurrentState}.");
        }

        if (motor.ActualSpeed >= speedBeforeReverse)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: hard reversal did not brake first, before={speedBeforeReverse:0.###}, after={motor.ActualSpeed:0.###}.");
        }

        TickMotor(motor, tickMethod, 90);

        if (motor.CurrentState != StarterInspiredThirdPersonMotor.MovementState.Locomotion)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: expected Locomotion after hard reversal recovery, got {motor.CurrentState}.");
        }

        if (motor.CurrentPlanarVelocity.z >= -0.5f)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: velocity did not reverse direction, velocity={motor.CurrentPlanarVelocity}.");
        }
    }

    private static void ValidateWallCollision(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        MethodInfo tickMethod)
    {
        ResetCharacter(character, motor, new Vector3(0f, 0f, 5.8f));

        motor.SetMoveInput(Vector2.up);
        TickMotor(motor, tickMethod, 140);

        if (character.transform.position.z > 6.45f)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: character crossed front wall, z={character.transform.position.z:0.###}.");
        }
    }

    private static void ValidateFlatGrounding(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        MethodInfo tickMethod)
    {
        ResetCharacter(character, motor, Vector3.zero);
        TickMotor(motor, tickMethod, 10);

        if (!motor.RawGrounded || !motor.StableGrounded)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: flat grounding unstable, raw={motor.RawGrounded}, stable={motor.StableGrounded}.");
        }

        if (motor.GroundAngle > 3f)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: flat ground angle too high, angle={motor.GroundAngle:0.###}.");
        }

        if (motor.VerticalVelocity > 0.01f)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: grounded vertical velocity is upward, verticalVelocity={motor.VerticalVelocity:0.###}.");
        }
    }

    private static void ValidateSlopeGrounding(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        MethodInfo tickMethod)
    {
        ResetCharacterOnGround(character, motor, new Vector3(6.5f, 6f, -1f));
        TickMotor(motor, tickMethod, 20);

        if (!motor.RawGrounded || !motor.StableGrounded)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: slope grounding unstable, raw={motor.RawGrounded}, stable={motor.StableGrounded}.");
        }

        if (motor.GroundAngle < 10f || motor.GroundAngle > 35f)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: unexpected slope angle, angle={motor.GroundAngle:0.###}.");
        }
    }

    private static void ValidateSmallDropSnap(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        MethodInfo tickMethod)
    {
        ResetCharacter(character, motor, new Vector3(2f, 0.22f, -3.8f));
        TickMotor(motor, tickMethod, 10);

        bool snapObserved = false;
        bool unstableObserved = false;
        motor.SetMoveInput(Vector2.up);

        for (int i = 0; i < 90; i++)
        {
            TickMotor(motor, tickMethod, 1);
            snapObserved |= motor.SnapActive;
            unstableObserved |= !motor.StableGrounded && motor.TimeSinceGrounded > 0.12f;
        }

        if (unstableObserved)
        {
            throw new InvalidOperationException("Starter motor validation failed: small drop caused a real airborne flicker.");
        }

        if (!motor.StableGrounded)
        {
            throw new InvalidOperationException("Starter motor validation failed: motor did not recover grounded state after small drop.");
        }

        if (!snapObserved)
        {
            Debug.Log("Starter motor validation note: small drop stayed grounded without requiring an explicit snap frame.");
        }
    }

    private static void ValidateLedgeFall(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        MethodInfo tickMethod)
    {
        ResetCharacter(character, motor, new Vector3(-8f, 1.12f, -1f));
        TickMotor(motor, tickMethod, 10);

        float startY = character.transform.position.y;
        bool airborneObserved = false;
        bool freeFallObserved = false;
        bool downwardVelocityObserved = false;
        bool jumpObserved = false;
        motor.SetMoveInput(Vector2.left);

        for (int i = 0; i < 120; i++)
        {
            TickMotor(motor, tickMethod, 1);
            airborneObserved |= !motor.StableGrounded;
            freeFallObserved |= motor.FreeFall;
            downwardVelocityObserved |= motor.VerticalVelocity < -3f;
            jumpObserved |= motor.JumpStarted;
        }

        if (!airborneObserved || !freeFallObserved || !downwardVelocityObserved || jumpObserved)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: ledge fall not observed, airborne={airborneObserved}, freeFall={freeFallObserved}, downwardVelocity={downwardVelocityObserved}, jumpObserved={jumpObserved}, y={character.transform.position.y:0.###}.");
        }

        if (character.transform.position.y > startY - 0.5f)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: ledge fall did not lower character enough, startY={startY:0.###}, y={character.transform.position.y:0.###}.");
        }
    }

    private static void ValidateIdleJump(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        MethodInfo tickMethod)
    {
        ResetCharacter(character, motor, Vector3.zero);
        TickMotor(motor, tickMethod, 10);

        motor.RequestJump();
        TickMotor(motor, tickMethod, 1);

        if (!motor.JumpStarted || motor.VerticalVelocity <= 0f || motor.StableGrounded)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: idle jump did not start correctly, jumpStarted={motor.JumpStarted}, stable={motor.StableGrounded}, verticalVelocity={motor.VerticalVelocity:0.###}.");
        }

        if (motor.FreeFall)
        {
            throw new InvalidOperationException("Starter motor validation failed: idle jump was classified as free fall during ascent.");
        }

        TickMotor(motor, tickMethod, 12);

        if (motor.VerticalVelocity <= 0f && motor.AirborneTime < 0.1f)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: jump ascent ended too early, verticalVelocity={motor.VerticalVelocity:0.###}, airborneTime={motor.AirborneTime:0.###}.");
        }
    }

    private static void ValidateMovingJump(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        MethodInfo tickMethod)
    {
        ResetCharacter(character, motor, Vector3.zero);

        motor.SetMoveInput(Vector2.up);
        TickMotor(motor, tickMethod, 35);
        float speedBeforeJump = motor.ActualSpeed;

        motor.RequestJump();
        TickMotor(motor, tickMethod, 1);

        if (!motor.JumpStarted || motor.VerticalVelocity <= 0f || motor.ActualSpeed < speedBeforeJump * 0.5f)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: moving jump did not preserve usable horizontal motion, jumpStarted={motor.JumpStarted}, verticalVelocity={motor.VerticalVelocity:0.###}, before={speedBeforeJump:0.###}, after={motor.ActualSpeed:0.###}.");
        }
    }

    private static void ValidateTinyDropDoesNotTriggerLanding(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        MethodInfo tickMethod)
    {
        ResetCharacter(character, motor, new Vector3(2f, 0.22f, -3.8f));
        TickMotor(motor, tickMethod, 10);

        bool landingObserved = false;
        bool freeFallObserved = false;
        motor.SetMoveInput(Vector2.up);

        for (int i = 0; i < 90; i++)
        {
            TickMotor(motor, tickMethod, 1);
            landingObserved |= motor.LandingTriggered;
            freeFallObserved |= motor.FreeFall;
        }

        if (landingObserved || freeFallObserved)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: tiny drop triggered fall/landing behavior, freeFall={freeFallObserved}, landing={landingObserved}, severity={motor.LastLandingSeverity}.");
        }
    }

    private static void ValidateMediumFallLanding(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        MethodInfo tickMethod)
    {
        ResetCharacter(character, motor, new Vector3(-8f, 1.12f, -1f));
        TickMotor(motor, tickMethod, 10);

        bool freeFallObserved = false;
        bool landingObserved = false;
        StarterInspiredThirdPersonMotor.LandingSeverity observedSeverity = StarterInspiredThirdPersonMotor.LandingSeverity.None;
        float speedBeforeLanding = 0f;
        float speedAfterLanding = 0f;
        motor.SetMoveInput(Vector2.left);

        for (int i = 0; i < 180; i++)
        {
            freeFallObserved |= motor.FreeFall;
            speedBeforeLanding = motor.ActualSpeed;
            TickMotor(motor, tickMethod, 1);

            if (motor.LandingTriggered)
            {
                landingObserved = true;
                observedSeverity = motor.LastLandingSeverity;
                speedAfterLanding = motor.ActualSpeed;
                break;
            }
        }

        if (!freeFallObserved || !landingObserved)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: medium fall landing not observed, freeFall={freeFallObserved}, landing={landingObserved}, stable={motor.StableGrounded}, y={character.transform.position.y:0.###}.");
        }

        if (observedSeverity == StarterInspiredThirdPersonMotor.LandingSeverity.None)
        {
            throw new InvalidOperationException("Starter motor validation failed: medium fall landing had no severity.");
        }

        TickMotor(motor, tickMethod, 8);

        if (motor.ActualSpeed >= speedBeforeLanding && speedAfterLanding > 0.1f)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: landing damping did not reduce horizontal speed, before={speedBeforeLanding:0.###}, after={motor.ActualSpeed:0.###}, severity={observedSeverity}.");
        }
    }

    private static void ValidateHighFallLanding(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        MethodInfo tickMethod)
    {
        ResetCharacter(character, motor, new Vector3(-16f, 3.12f, -1f));
        TickMotor(motor, tickMethod, 10);

        bool freeFallObserved = false;
        bool landingObserved = false;
        StarterInspiredThirdPersonMotor.LandingSeverity observedSeverity = StarterInspiredThirdPersonMotor.LandingSeverity.None;
        motor.SetMoveInput(Vector2.left);

        for (int i = 0; i < 240; i++)
        {
            TickMotor(motor, tickMethod, 1);
            freeFallObserved |= motor.FreeFall;

            if (motor.LandingTriggered)
            {
                landingObserved = true;
                observedSeverity = motor.LastLandingSeverity;
                break;
            }
        }

        if (!freeFallObserved || !landingObserved)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: high fall landing not observed, freeFall={freeFallObserved}, landing={landingObserved}, stable={motor.StableGrounded}, y={character.transform.position.y:0.###}.");
        }

        if (observedSeverity != StarterInspiredThirdPersonMotor.LandingSeverity.Heavy)
        {
            throw new InvalidOperationException(
                $"Starter motor validation failed: high fall expected Heavy severity, got {observedSeverity}.");
        }
    }

    private static void ValidateFlightCruiseAndLandingRecovery(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        MethodInfo tickMethod)
    {
        ResetCharacter(character, motor, Vector3.zero);

        motor.SetFlightMode(true);
        TickMotor(motor, tickMethod, 34);
        if (!motor.FlightActive)
        {
            throw new InvalidOperationException("Starter motor flight validation failed: flight did not remain active after takeoff.");
        }

        float xBeforeCruise = character.transform.position.x;
        motor.SetMoveInput(Vector2.left);
        TickMotor(motor, tickMethod, 20);
        if (!motor.FlightActive)
        {
            throw new InvalidOperationException("Starter motor flight validation failed: passive ground contact ended flight during cruise.");
        }

        if (xBeforeCruise - character.transform.position.x < 0.5f || motor.FlightSpeed <= 2f)
        {
            throw new InvalidOperationException(
                $"Starter motor flight validation failed: cruise movement too small, dx={xBeforeCruise - character.transform.position.x:0.###}, speed={motor.FlightSpeed:0.###}.");
        }

        ResetCharacter(character, motor, new Vector3(0f, 3f, 0f));
        motor.SetFlightMode(true);
        TickMotor(motor, tickMethod, 34);
        motor.SetMoveInput(Vector2.zero);
        motor.SetFlightVerticalInput(-1f);
        bool landedFromFlight = false;
        for (int i = 0; i < 150; i++)
        {
            TickMotor(motor, tickMethod, 1);
            landedFromFlight |= motor.LandingFromFlightTriggered;
            if (!motor.FlightActive && motor.StableGrounded)
            {
                break;
            }
        }

        if (motor.FlightActive || !motor.StableGrounded || !landedFromFlight)
        {
            throw new InvalidOperationException(
                $"Starter motor flight validation failed: descent did not recover grounded locomotion, flight={motor.FlightActive}, stable={motor.StableGrounded}, landedFromFlight={landedFromFlight}.");
        }

        motor.SetFlightVerticalInput(0f);
        motor.SetMoveInput(Vector2.left);
        float xBeforeGroundMove = character.transform.position.x;
        TickMotor(motor, tickMethod, 60);
        if (motor.ActualSpeed < 4.8f || character.transform.position.x >= xBeforeGroundMove - 2.5f)
        {
            throw new InvalidOperationException(
                $"Starter motor flight validation failed: grounded walk speed did not recover after flight, speed={motor.ActualSpeed:0.###}, dx={xBeforeGroundMove - character.transform.position.x:0.###}, state={motor.CurrentState}.");
        }

        motor.SetSprintInput(true);
        TickMotor(motor, tickMethod, 30);
        if (motor.ActualSpeed < 6f)
        {
            throw new InvalidOperationException(
                $"Starter motor flight validation failed: grounded sprint speed did not recover after flight, speed={motor.ActualSpeed:0.###}, state={motor.CurrentState}.");
        }
    }

    private static void ValidateFlightLandingDoesNotGlide(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        MethodInfo tickMethod)
    {
        ResetCharacter(character, motor, new Vector3(0f, 3f, 0f));

        motor.SetFlightMode(true);
        TickMotor(motor, tickMethod, 34);
        motor.SetMoveInput(Vector2.left);
        TickMotor(motor, tickMethod, 20);
        motor.SetFlightVerticalInput(-1f);

        bool landedFromFlight = false;
        for (int i = 0; i < 180; i++)
        {
            TickMotor(motor, tickMethod, 1);
            landedFromFlight |= motor.LandingFromFlightTriggered;
            if (!motor.FlightActive && motor.StableGrounded)
            {
                break;
            }
        }

        if (motor.FlightActive || !motor.StableGrounded || !landedFromFlight)
        {
            throw new InvalidOperationException(
                $"Starter motor flight glide validation failed: moving descent did not land cleanly, flight={motor.FlightActive}, stable={motor.StableGrounded}, landedFromFlight={landedFromFlight}.");
        }

        motor.SetMoveInput(Vector2.zero);
        motor.SetFlightVerticalInput(0f);
        Vector3 positionAfterLanding = character.transform.position;
        TickMotor(motor, tickMethod, 30);

        float planarDrift = Vector3.ProjectOnPlane(character.transform.position - positionAfterLanding, Vector3.up).magnitude;
        if (planarDrift > 0.35f || motor.ActualSpeed > 0.45f)
        {
            throw new InvalidOperationException(
                $"Starter motor flight glide validation failed: landing retained too much planar drift, drift={planarDrift:0.###}, speed={motor.ActualSpeed:0.###}, state={motor.CurrentState}.");
        }
    }

    private static void ValidateAnimationDriving(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        StarterMotorAnimatorDriver animatorDriver,
        Animator animator,
        MethodInfo tickMethod,
        MethodInfo animationTickMethod)
    {
        if (animator.applyRootMotion)
        {
            throw new InvalidOperationException("Starter motor animation validation failed: Root Motion is enabled.");
        }

        ResetCharacterAndAnimation(character, motor, animatorDriver, Vector3.zero);
        TickMotorAndAnimation(motor, animatorDriver, tickMethod, animationTickMethod, 5);

        if (animatorDriver.DebugAnimatorSpeed > 0.05f || animatorDriver.DebugIsMoving)
        {
            throw new InvalidOperationException(
                $"Starter motor animation validation failed: idle animation values are not idle, speed={animatorDriver.DebugAnimatorSpeed:0.###}, moving={animatorDriver.DebugIsMoving}.");
        }

        motor.SetMoveInput(Vector2.up);
        TickMotorAndAnimation(motor, animatorDriver, tickMethod, animationTickMethod, 45);

        if (!animatorDriver.DebugIsMoving || animatorDriver.DebugAnimatorSpeed <= 0.5f || animator.GetFloat("Speed") <= 0.1f)
        {
            throw new InvalidOperationException(
                $"Starter motor animation validation failed: locomotion animation did not receive speed, debugSpeed={animatorDriver.DebugAnimatorSpeed:0.###}, animatorSpeed={animator.GetFloat("Speed"):0.###}, moving={animatorDriver.DebugIsMoving}.");
        }

        motor.Stop();
        TickMotorAndAnimation(motor, animatorDriver, tickMethod, animationTickMethod, 70);

        if (animatorDriver.DebugIsMoving || animatorDriver.DebugAnimatorSpeed > 0.05f)
        {
            throw new InvalidOperationException(
                $"Starter motor animation validation failed: stop animation values did not settle, speed={animatorDriver.DebugAnimatorSpeed:0.###}, moving={animatorDriver.DebugIsMoving}.");
        }

        ResetCharacterAndAnimation(character, motor, animatorDriver, Vector3.zero);
        TickMotorAndAnimation(motor, animatorDriver, tickMethod, animationTickMethod, 10);
        motor.RequestJump();
        TickMotorAndAnimation(motor, animatorDriver, tickMethod, animationTickMethod, 1);

        if (!animatorDriver.DebugJumpTriggered ||
            animatorDriver.DebugJumpPhase != 1 ||
            animator.GetInteger("JumpPhase") != 1)
        {
            throw new InvalidOperationException(
                $"Starter motor animation validation failed: idle jump was not driven, driverPhase={animatorDriver.DebugJumpPhase}, animatorPhase={animator.GetInteger("JumpPhase")}, triggered={animatorDriver.DebugJumpTriggered}.");
        }

        ResetCharacterAndAnimation(character, motor, animatorDriver, Vector3.zero);
        motor.SetMoveInput(Vector2.up);
        TickMotorAndAnimation(motor, animatorDriver, tickMethod, animationTickMethod, 35);
        motor.RequestJump();
        TickMotorAndAnimation(motor, animatorDriver, tickMethod, animationTickMethod, 1);

        if (!animatorDriver.DebugJumpTriggered || !animator.GetBool("JumpFromMovement"))
        {
            throw new InvalidOperationException(
                $"Starter motor animation validation failed: moving jump context was not driven, triggered={animatorDriver.DebugJumpTriggered}, jumpFromMovement={animator.GetBool("JumpFromMovement")}.");
        }

        ResetCharacterAndAnimation(character, motor, animatorDriver, new Vector3(-8f, 1.12f, -1f));
        TickMotorAndAnimation(motor, animatorDriver, tickMethod, animationTickMethod, 10);

        bool freeFallAnimationObserved = false;
        motor.SetMoveInput(Vector2.left);
        for (int i = 0; i < 160; i++)
        {
            TickMotorAndAnimation(motor, animatorDriver, tickMethod, animationTickMethod, 1);
            freeFallAnimationObserved |= animatorDriver.DebugFreeFall &&
                                         animatorDriver.DebugJumpPhase == 2 &&
                                         animator.GetBool("IsAirborne");
        }

        if (!freeFallAnimationObserved)
        {
            throw new InvalidOperationException(
                $"Starter motor animation validation failed: ledge fall did not drive airborne/fall animation values, freeFall={animatorDriver.DebugFreeFall}, phase={animatorDriver.DebugJumpPhase}, isAirborne={animator.GetBool("IsAirborne")}.");
        }

        ResetCharacterAndAnimation(character, motor, animatorDriver, new Vector3(2f, 0.22f, -3.8f));
        TickMotorAndAnimation(motor, animatorDriver, tickMethod, animationTickMethod, 10);

        bool tinyDropLandingObserved = false;
        motor.SetMoveInput(Vector2.up);
        for (int i = 0; i < 90; i++)
        {
            TickMotorAndAnimation(motor, animatorDriver, tickMethod, animationTickMethod, 1);
            tinyDropLandingObserved |= animatorDriver.DebugLandingTriggered ||
                                       animatorDriver.DebugJumpPhase >= 3 ||
                                       animator.GetInteger("LandingType") != 0;
        }

        if (tinyDropLandingObserved)
        {
            throw new InvalidOperationException("Starter motor animation validation failed: tiny drop drove landing animation values.");
        }

        ValidateAnimationLanding(
            character,
            motor,
            animatorDriver,
            animator,
            tickMethod,
            animationTickMethod,
            new Vector3(-8f, 1.12f, -1f),
            StarterInspiredThirdPersonMotor.LandingSeverity.None);

        ValidateAnimationLanding(
            character,
            motor,
            animatorDriver,
            animator,
            tickMethod,
            animationTickMethod,
            new Vector3(-16f, 3.12f, -1f),
            StarterInspiredThirdPersonMotor.LandingSeverity.Heavy);
    }

    private static void ValidateAnimationLanding(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        StarterMotorAnimatorDriver animatorDriver,
        Animator animator,
        MethodInfo tickMethod,
        MethodInfo animationTickMethod,
        Vector3 startPosition,
        StarterInspiredThirdPersonMotor.LandingSeverity expectedSeverity)
    {
        ResetCharacterAndAnimation(character, motor, animatorDriver, startPosition);
        TickMotorAndAnimation(motor, animatorDriver, tickMethod, animationTickMethod, 10);

        bool landingAnimationObserved = false;
        StarterInspiredThirdPersonMotor.LandingSeverity observedSeverity = StarterInspiredThirdPersonMotor.LandingSeverity.None;
        motor.SetMoveInput(Vector2.left);

        for (int i = 0; i < 220; i++)
        {
            TickMotorAndAnimation(motor, animatorDriver, tickMethod, animationTickMethod, 1);
            if (animatorDriver.DebugLandingTriggered)
            {
                landingAnimationObserved = animatorDriver.DebugJumpPhase == 3 &&
                                           animator.GetInteger("JumpPhase") == 3 &&
                                           animator.GetInteger("LandingType") == 1;
                observedSeverity = animatorDriver.DebugLandingSeverity;
                break;
            }
        }

        if (!landingAnimationObserved)
        {
            throw new InvalidOperationException(
                $"Starter motor animation validation failed: landing animation values were not driven, phase={animatorDriver.DebugJumpPhase}, animatorPhase={animator.GetInteger("JumpPhase")}, landingType={animator.GetInteger("LandingType")}, severity={observedSeverity}.");
        }

        if (expectedSeverity != StarterInspiredThirdPersonMotor.LandingSeverity.None &&
            observedSeverity != expectedSeverity)
        {
            throw new InvalidOperationException(
                $"Starter motor animation validation failed: expected {expectedSeverity} landing severity, got {observedSeverity}.");
        }
    }

    private static void ValidateLadderMotorCompatibility(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        MethodInfo tickMethod)
    {
        ResetCharacter(character, motor, new Vector3(0f, 0f, 0f));
        TickMotor(motor, tickMethod, 10);

        motor.SetMoveInput(Vector2.up);
        TickMotor(motor, tickMethod, 25);

        if (motor.ActualSpeed <= 0.5f)
        {
            throw new InvalidOperationException(
                $"Starter motor ladder validation failed: motor did not move before ladder lock, speed={motor.ActualSpeed:0.###}.");
        }

        Vector3 ladderStartPosition = character.transform.position;
        Quaternion ladderStartRotation = character.transform.rotation;

        motor.BeginLadderTraversal();
        motor.SetMoveInput(Vector2.up);
        motor.RequestJump();
        TickMotor(motor, tickMethod, 30);

        if (!motor.IsLadderTraversalActive || motor.CurrentState != StarterInspiredThirdPersonMotor.MovementState.Ladder)
        {
            throw new InvalidOperationException(
                $"Starter motor ladder validation failed: ladder state not active, active={motor.IsLadderTraversalActive}, state={motor.CurrentState}.");
        }

        if (Vector3.Distance(character.transform.position, ladderStartPosition) > 0.01f ||
            motor.ActualSpeed > 0.01f ||
            Mathf.Abs(motor.VerticalVelocity) > 0.01f ||
            motor.JumpStarted ||
            motor.FreeFall)
        {
            throw new InvalidOperationException(
                $"Starter motor ladder validation failed: normal movement was not suspended, positionDelta={Vector3.Distance(character.transform.position, ladderStartPosition):0.###}, speed={motor.ActualSpeed:0.###}, vertical={motor.VerticalVelocity:0.###}, jump={motor.JumpStarted}, freeFall={motor.FreeFall}.");
        }

        Vector3 ladderMidPosition = ladderStartPosition + Vector3.up * 2f;
        Quaternion ladderMidRotation = Quaternion.Euler(0f, 90f, 0f);
        motor.ApplyLadderPose(ladderMidPosition, ladderMidRotation);
        TickMotor(motor, tickMethod, 10);

        if (Vector3.Distance(character.transform.position, ladderMidPosition) > 0.01f ||
            Quaternion.Angle(character.transform.rotation, ladderMidRotation) > 0.5f)
        {
            throw new InvalidOperationException(
                $"Starter motor ladder validation failed: scripted ladder pose was not preserved, position={character.transform.position}, rotationAngle={Quaternion.Angle(character.transform.rotation, ladderMidRotation):0.###}.");
        }

        Vector3 ladderExitPosition = new Vector3(ladderMidPosition.x, ladderStartPosition.y, ladderMidPosition.z + 1f);
        motor.ApplyLadderPose(ladderExitPosition, ladderStartRotation);
        motor.EndLadderTraversal();
        TickMotor(motor, tickMethod, 10);

        if (motor.IsLadderTraversalActive || motor.CurrentState == StarterInspiredThirdPersonMotor.MovementState.Ladder)
        {
            throw new InvalidOperationException(
                $"Starter motor ladder validation failed: ladder state did not release, active={motor.IsLadderTraversalActive}, state={motor.CurrentState}.");
        }

        motor.SetMoveInput(Vector2.up);
        TickMotor(motor, tickMethod, 35);

        if (motor.ActualSpeed <= 0.5f)
        {
            throw new InvalidOperationException(
                $"Starter motor ladder validation failed: locomotion did not resume after ladder, speed={motor.ActualSpeed:0.###}.");
        }
    }

    private static void ResetCharacter(GameObject character, StarterInspiredThirdPersonMotor motor, Vector3 position)
    {
        character.transform.position = position;
        character.transform.rotation = Quaternion.identity;
        Physics.SyncTransforms();
        motor.ResetMotionState();
        Physics.SyncTransforms();
    }

    private static void ResetCharacterAndAnimation(
        GameObject character,
        StarterInspiredThirdPersonMotor motor,
        StarterMotorAnimatorDriver animatorDriver,
        Vector3 position)
    {
        ResetCharacter(character, motor, position);
        animatorDriver.ResetAnimationState();
    }

    private static void ResetCharacterOnGround(GameObject character, StarterInspiredThirdPersonMotor motor, Vector3 rayOrigin)
    {
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            throw new InvalidOperationException($"Starter motor validation failed: no ground found below {rayOrigin}.");
        }

        ResetCharacter(character, motor, hit.point + Vector3.up * 0.03f);
    }

    private static void TickMotor(StarterInspiredThirdPersonMotor motor, MethodInfo tickMethod, int frames)
    {
        const float DeltaTime = 1f / 60f;
        object[] args = { DeltaTime };

        for (int i = 0; i < frames; i++)
        {
            tickMethod.Invoke(motor, args);
            Physics.SyncTransforms();
        }
    }

    private static void TickMotorAndAnimation(
        StarterInspiredThirdPersonMotor motor,
        StarterMotorAnimatorDriver animatorDriver,
        MethodInfo tickMethod,
        MethodInfo animationTickMethod,
        int frames)
    {
        const float DeltaTime = 1f / 60f;
        object[] args = { DeltaTime };

        for (int i = 0; i < frames; i++)
        {
            tickMethod.Invoke(motor, args);
            Physics.SyncTransforms();
            animationTickMethod.Invoke(animatorDriver, args);
            Physics.SyncTransforms();
        }
    }
}
