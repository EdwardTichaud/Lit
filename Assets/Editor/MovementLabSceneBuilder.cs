using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MovementLabSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/MovementLab.unity";
    private const string MaterialFolder = "Assets/Materials/MovementLab";

    [MenuItem("Tools/Movement Lab/Rebuild Scene")]
    public static void RebuildSceneMenu()
    {
        RebuildScene();
    }

    public static void RebuildScene()
    {
        EnsureFolders();

        Material floor = GetOrCreateMaterial("ML_Floor", new Color(0.28f, 0.29f, 0.31f, 1f));
        Material wall = GetOrCreateMaterial("ML_Wall", new Color(0.45f, 0.46f, 0.48f, 1f));
        Material stair = GetOrCreateMaterial("ML_Stair", new Color(0.52f, 0.42f, 0.32f, 1f));
        Material ramp = GetOrCreateMaterial("ML_Ramp", new Color(0.18f, 0.6f, 0.75f, 1f));
        Material slope = GetOrCreateMaterial("ML_Slope", new Color(0.36f, 0.58f, 0.36f, 1f));
        Material door = GetOrCreateMaterial("ML_Door", new Color(0.5f, 0.26f, 0.12f, 1f));
        Material interactable = GetOrCreateMaterial("ML_Interactable", new Color(0.25f, 0.55f, 0.85f, 1f));
        Material platform = GetOrCreateMaterial("ML_Platform", new Color(0.7f, 0.45f, 0.18f, 1f));
        Material marker = GetOrCreateMaterial("ML_Marker", new Color(0.95f, 0.82f, 0.25f, 1f));
        Material hazard = GetOrCreateMaterial("ML_Hazard", new Color(0.75f, 0.2f, 0.18f, 1f));

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "MovementLab";

        GameObject root = new GameObject("MovementLab");
        GameObject geometryRoot = new GameObject("Geometry");
        geometryRoot.transform.SetParent(root.transform);
        GameObject interactionRoot = new GameObject("Interactables");
        interactionRoot.transform.SetParent(root.transform);
        GameObject platformRoot = new GameObject("MovingPlatforms");
        platformRoot.transform.SetParent(root.transform);

        CreateSceneLighting(root.transform);
        CreateFloor(geometryRoot.transform, floor, wall);
        CreateSpawn(marker, root.transform);
        CreateRawStairs(geometryRoot.transform, stair, hazard);
        CreateRampColliderStairs(geometryRoot.transform, stair, ramp);
        CreateSlopeBank(geometryRoot.transform, slope);
        CreateWallCourse(geometryRoot.transform, wall, hazard);
        CreateDoorCourse(interactionRoot.transform, wall, door);
        CreateInteractableCourse(interactionRoot.transform, interactable);
        CreatePlatformCourse(platformRoot.transform, platform);
        CreateReferenceObstacles(geometryRoot.transform, wall, hazard);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Movement lab scene rebuilt at {ScenePath}");
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/Materials", "MovementLab");
        }
    }

    private static Material GetOrCreateMaterial(string name, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(ResolveLitShader());
            AssetDatabase.CreateAsset(material, path);
        }

        material.name = name;
        SetMaterialColor(material, color);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Shader ResolveLitShader()
    {
        return Shader.Find("HDRP/Lit")
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard")
            ?? Shader.Find("Diffuse");
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void CreateSceneLighting(Transform parent)
    {
        GameObject cameraObject = new GameObject("MovementLab_Camera");
        cameraObject.transform.SetParent(parent);
        cameraObject.transform.position = new Vector3(4f, 22f, -36f);
        cameraObject.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.fieldOfView = 45f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 150f;

        GameObject lightObject = new GameObject("MovementLab_Sun");
        lightObject.transform.SetParent(parent);
        lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2.2f;
        light.color = new Color(1f, 0.95f, 0.86f, 1f);
    }

    private static void CreateFloor(Transform parent, Material floor, Material wall)
    {
        CreateCube("Main test floor", new Vector3(0f, -0.06f, 8f), new Vector3(72f, 0.12f, 68f), floor, parent);
        CreateCube("North boundary", new Vector3(0f, 1f, 42f), new Vector3(74f, 2f, 0.35f), wall, parent);
        CreateCube("South boundary", new Vector3(0f, 1f, -27f), new Vector3(74f, 2f, 0.35f), wall, parent);
        CreateCube("West boundary", new Vector3(-37f, 1f, 8f), new Vector3(0.35f, 2f, 69f), wall, parent);
        CreateCube("East boundary", new Vector3(37f, 1f, 8f), new Vector3(0.35f, 2f, 69f), wall, parent);
    }

    private static void CreateSpawn(Material marker, Transform parent)
    {
        GameObject spawn = new GameObject("SpawnPoint");
        spawn.tag = "SpawnPoint";
        spawn.transform.SetParent(parent);
        spawn.transform.position = new Vector3(0f, 0f, -22f);
        spawn.transform.rotation = Quaternion.identity;

        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = "Player spawn marker";
        capsule.transform.SetParent(spawn.transform);
        capsule.transform.localPosition = new Vector3(0f, 1f, 0f);
        capsule.transform.localScale = new Vector3(0.55f, 1f, 0.55f);
        SetMaterial(capsule, marker);
        Object.DestroyImmediate(capsule.GetComponent<Collider>());

        CreateLabel("SPAWN / drop current player here", new Vector3(0f, 2.4f, -22f), parent);
    }

    private static void CreateRawStairs(Transform parent, Material stair, Material hazard)
    {
        Transform zone = CreateZone("01_Raw_Box_Stairs", parent, new Vector3(-24f, 0f, -12f));
        CreateLabel("01 Raw stairs: individual box colliders", zone.position + new Vector3(0f, 2.4f, -2.4f), zone);

        float tread = 0.75f;
        float stepHeight = 0.18f;
        for (int i = 0; i < 8; i++)
        {
            float height = (i + 1) * stepHeight;
            CreateCube(
                $"Raw step {i + 1}",
                zone.position + new Vector3(0f, height * 0.5f, i * tread),
                new Vector3(5f, height, tread),
                stair,
                zone);
        }

        for (int i = 0; i < 5; i++)
        {
            float height = 0.08f + i * 0.06f;
            CreateCube(
                $"Step height probe {height:0.00}m",
                zone.position + new Vector3(-3.5f + i * 1.75f, height * 0.5f, 8.3f),
                new Vector3(1.2f, height, 1.2f),
                i >= 3 ? hazard : stair,
                zone);
        }
    }

    private static void CreateRampColliderStairs(Transform parent, Material stair, Material ramp)
    {
        Transform zone = CreateZone("02_Ramp_Collider_Stairs", parent, new Vector3(-13f, 0f, -12f));
        CreateLabel("02 Visual stairs + ramp collider", zone.position + new Vector3(0f, 2.4f, -2.4f), zone);

        float tread = 0.75f;
        float stepHeight = 0.18f;
        for (int i = 0; i < 8; i++)
        {
            float height = (i + 1) * stepHeight;
            GameObject visualStep = CreateCube(
                $"Visual step {i + 1}",
                zone.position + new Vector3(0f, height * 0.5f, i * tread),
                new Vector3(5f, height, tread),
                stair,
                zone);
            Object.DestroyImmediate(visualStep.GetComponent<Collider>());
        }

        float totalHeight = 8f * stepHeight;
        float totalDepth = 8f * tread;
        float angle = Mathf.Rad2Deg * Mathf.Atan2(totalHeight, totalDepth);
        float length = Mathf.Sqrt(totalDepth * totalDepth + totalHeight * totalHeight);
        GameObject rampCollider = CreateCube(
            "Ramp collider surface",
            zone.position + new Vector3(0f, totalHeight * 0.5f + 0.04f, totalDepth * 0.5f - tread * 0.5f),
            new Vector3(5.2f, 0.12f, length),
            ramp,
            zone);
        rampCollider.transform.rotation = Quaternion.Euler(-angle, 0f, 0f);
    }

    private static void CreateSlopeBank(Transform parent, Material slope)
    {
        Transform zone = CreateZone("03_Slope_Bank", parent, new Vector3(0f, 0f, -12f));
        CreateLabel("03 Slopes: 12 / 24 / 36 / 45 deg", zone.position + new Vector3(0f, 2.4f, -2.4f), zone);
        CreateSlope("Slope 12 deg", zone.position + new Vector3(-6f, 0f, 3f), 12f, 8f, 3.2f, slope, zone);
        CreateSlope("Slope 24 deg", zone.position + new Vector3(-2f, 0f, 3f), 24f, 8f, 3.2f, slope, zone);
        CreateSlope("Slope 36 deg", zone.position + new Vector3(2f, 0f, 3f), 36f, 8f, 3.2f, slope, zone);
        CreateSlope("Slope 45 deg", zone.position + new Vector3(6f, 0f, 3f), 45f, 8f, 3.2f, slope, zone);
    }

    private static void CreateWallCourse(Transform parent, Material wall, Material hazard)
    {
        Transform zone = CreateZone("04_Walls_Corners_Narrow_Gaps", parent, new Vector3(15f, 0f, -13f));
        CreateLabel("04 Walls: narrow gaps, corners, low blockers", zone.position + new Vector3(0f, 2.4f, -2.4f), zone);

        CreateCube("Left corridor wall", zone.position + new Vector3(-2f, 1.1f, 3f), new Vector3(0.35f, 2.2f, 9f), wall, zone);
        CreateCube("Right corridor wall", zone.position + new Vector3(2f, 1.1f, 3f), new Vector3(0.35f, 2.2f, 9f), wall, zone);
        CreateCube("Narrow gap left", zone.position + new Vector3(-0.9f, 1.1f, 8.5f), new Vector3(0.35f, 2.2f, 3f), wall, zone);
        CreateCube("Narrow gap right", zone.position + new Vector3(0.9f, 1.1f, 8.5f), new Vector3(0.35f, 2.2f, 3f), wall, zone);
        CreateCube("Corner block A", zone.position + new Vector3(4.5f, 1.1f, 4f), new Vector3(5f, 2.2f, 0.35f), wall, zone);
        CreateCube("Corner block B", zone.position + new Vector3(7f, 1.1f, 6.3f), new Vector3(0.35f, 2.2f, 4.8f), wall, zone);
        CreateCube("Low toe-catcher 10cm", zone.position + new Vector3(-4.5f, 0.05f, 7.2f), new Vector3(2.4f, 0.1f, 0.35f), hazard, zone);
        CreateCube("Low toe-catcher 25cm", zone.position + new Vector3(-4.5f, 0.125f, 8.6f), new Vector3(2.4f, 0.25f, 0.35f), hazard, zone);
    }

    private static void CreateDoorCourse(Transform parent, Material wall, Material door)
    {
        Transform zone = CreateZone("05_Doors", parent, new Vector3(27f, 0f, -11f));
        CreateLabel("05 Doors: hinge and sliding interactables", zone.position + new Vector3(0f, 2.4f, -2.4f), zone);

        CreateDoorFrame(zone.position + new Vector3(-2.5f, 0f, 3f), wall, zone);
        CreateHingeDoor("Hinge test door", zone.position + new Vector3(-3.25f, 0f, 3f), door, zone);

        CreateDoorFrame(zone.position + new Vector3(3f, 0f, 3f), wall, zone);
        CreateSlidingDoor("Sliding test door", zone.position + new Vector3(3f, 1f, 3f), door, zone);
    }

    private static void CreateInteractableCourse(Transform parent, Material material)
    {
        Transform zone = CreateZone("06_Interactables", parent, new Vector3(27f, 0f, 8f));
        CreateLabel("06 Interactables: select + trigger animation", zone.position + new Vector3(0f, 2.4f, -2.4f), zone);

        CreateInteractable("Toggle color cube", zone.position + new Vector3(-4f, 0.5f, 3f), Vector3.one, MovementLabInteractable.ResponseMode.ToggleColor, material, zone);
        CreateInteractable("Lift block", zone.position + new Vector3(0f, 0.4f, 3f), new Vector3(1.3f, 0.8f, 1.3f), MovementLabInteractable.ResponseMode.Lift, material, zone);
        CreateInteractable("Pulse scale pillar", zone.position + new Vector3(4f, 0.75f, 3f), new Vector3(1.1f, 1.5f, 1.1f), MovementLabInteractable.ResponseMode.PulseScale, material, zone);
    }

    private static void CreatePlatformCourse(Transform parent, Material platform)
    {
        Transform zone = CreateZone("07_Moving_Platforms", parent, new Vector3(-11f, 0f, 24f));
        CreateLabel("07 Moving platforms: horizontal, elevator, rotating", zone.position + new Vector3(6f, 2.6f, -3f), zone);

        CreateMovingPlatform("Horizontal platform", zone.position + new Vector3(0f, 0.18f, 0f), new Vector3(4f, 0.35f, 3f), new Vector3(7f, 0f, 0f), 4f, 0f, Vector3.zero, platform, zone);
        CreateMovingPlatform("Elevator platform", zone.position + new Vector3(10f, 0.18f, 0f), new Vector3(4f, 0.35f, 3f), new Vector3(0f, 3f, 0f), 5f, 0.5f, Vector3.zero, platform, zone);
        CreateMovingPlatform("Rotating platform", zone.position + new Vector3(20f, 0.18f, 0f), new Vector3(4f, 0.35f, 4f), new Vector3(0f, 0f, 0f), 6f, 1f, new Vector3(0f, 180f, 0f), platform, zone);
    }

    private static void CreateReferenceObstacles(Transform parent, Material wall, Material hazard)
    {
        Transform zone = CreateZone("08_Reference_Colliders", parent, new Vector3(11f, 0f, 24f));
        CreateLabel("08 Collider references: capsule traps and ledges", zone.position + new Vector3(5f, 2.6f, -3f), zone);
        CreateCube("Thin wall 5cm", zone.position + new Vector3(0f, 1f, 0f), new Vector3(0.05f, 2f, 4f), hazard, zone);
        CreateCube("Tall ledge 50cm", zone.position + new Vector3(4f, 0.25f, 0f), new Vector3(3f, 0.5f, 3f), wall, zone);
        CreateCube("Walkable ledge 20cm", zone.position + new Vector3(8f, 0.1f, 0f), new Vector3(3f, 0.2f, 3f), wall, zone);
    }

    private static Transform CreateZone(string name, Transform parent, Vector3 position)
    {
        GameObject zone = new GameObject(name);
        zone.transform.SetParent(parent);
        zone.transform.position = position;
        return zone.transform;
    }

    private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent);
        cube.transform.position = position;
        cube.transform.localScale = scale;
        SetMaterial(cube, material);
        return cube;
    }

    private static void CreateSlope(string name, Vector3 baseCenter, float angle, float depth, float width, Material material, Transform parent)
    {
        float radians = angle * Mathf.Deg2Rad;
        float height = Mathf.Sin(radians) * depth;
        float length = Mathf.Sqrt(depth * depth + height * height);
        GameObject slope = CreateCube(
            name,
            baseCenter + new Vector3(0f, height * 0.5f + 0.05f, depth * 0.5f),
            new Vector3(width, 0.16f, length),
            material,
            parent);
        slope.transform.rotation = Quaternion.Euler(-angle, 0f, 0f);
    }

    private static void CreateDoorFrame(Vector3 center, Material material, Transform parent)
    {
        CreateCube("Door frame left", center + new Vector3(-1.25f, 1.25f, 0f), new Vector3(0.25f, 2.5f, 0.35f), material, parent);
        CreateCube("Door frame right", center + new Vector3(1.25f, 1.25f, 0f), new Vector3(0.25f, 2.5f, 0.35f), material, parent);
        CreateCube("Door frame top", center + new Vector3(0f, 2.45f, 0f), new Vector3(2.75f, 0.25f, 0.35f), material, parent);
    }

    private static void CreateHingeDoor(string name, Vector3 hingePosition, Material material, Transform parent)
    {
        GameObject pivot = new GameObject(name);
        pivot.transform.SetParent(parent);
        pivot.transform.position = hingePosition + new Vector3(-0.95f, 1.05f, 0f);

        GameObject leaf = CreateCube("Door leaf", pivot.transform.position + new Vector3(0.95f, 0f, 0f), new Vector3(1.9f, 2.1f, 0.18f), material, pivot.transform);
        leaf.transform.localPosition = new Vector3(0.95f, 0f, 0f);
        leaf.transform.localRotation = Quaternion.identity;
        BoxCollider leafCollider = leaf.GetComponent<BoxCollider>();

        MovementLabDoor labDoor = pivot.AddComponent<MovementLabDoor>();
        labDoor.Configure(MovementLabDoor.DoorMode.Hinge, leaf.transform, leafCollider, leaf.transform, 95f, Vector3.zero, 2.6f);
    }

    private static void CreateSlidingDoor(string name, Vector3 center, Material material, Transform parent)
    {
        GameObject leaf = CreateCube(name, center, new Vector3(1.9f, 2.1f, 0.18f), material, parent);
        BoxCollider leafCollider = leaf.GetComponent<BoxCollider>();
        MovementLabDoor labDoor = leaf.AddComponent<MovementLabDoor>();
        labDoor.Configure(MovementLabDoor.DoorMode.Slide, leaf.transform, leafCollider, leaf.transform, 0f, new Vector3(0f, 2.4f, 0f), 2.6f);
    }

    private static void CreateInteractable(
        string name,
        Vector3 position,
        Vector3 scale,
        MovementLabInteractable.ResponseMode mode,
        Material material,
        Transform parent)
    {
        GameObject cube = CreateCube(name, position, scale, material, parent);
        BoxCollider collider = cube.GetComponent<BoxCollider>();
        MovementLabInteractable labInteractable = cube.AddComponent<MovementLabInteractable>();
        labInteractable.Configure(mode, cube.transform, collider, cube.transform, 2.5f, 10);
    }

    private static void CreateMovingPlatform(
        string name,
        Vector3 position,
        Vector3 scale,
        Vector3 travel,
        float period,
        float delay,
        Vector3 rotation,
        Material material,
        Transform parent)
    {
        GameObject platform = CreateCube(name, position, scale, material, parent);
        Rigidbody body = platform.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        MovementLabMovingPlatform mover = platform.AddComponent<MovementLabMovingPlatform>();
        mover.Configure(travel, period, delay, rotation);
    }

    private static void CreateLabel(string text, Vector3 position, Transform parent)
    {
        GameObject label = new GameObject($"Label - {text}");
        label.transform.SetParent(parent);
        label.transform.position = position;
        label.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        TextMesh textMesh = label.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = 0.13f;
        textMesh.fontSize = 64;
        textMesh.color = Color.white;
    }

    private static void SetMaterial(GameObject target, Material material)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }
}
