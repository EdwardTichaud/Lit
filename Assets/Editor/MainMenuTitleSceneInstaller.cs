using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainMenuTitleSceneInstaller
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string MaterialFolder = "Assets/Materials/MainMenu";
    private const string GeneratedUiFolder = "Assets/UI/MainMenu/Generated";
    private const string CursorSpritePath = GeneratedUiFolder + "/MainMenuPointerCursor.png";

    [MenuItem("Lit/MainMenu/Install Title Decor", priority = 30)]
    public static void Install()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"MainMenuTitleSceneInstaller: scene introuvable {ScenePath}");
            return;
        }

        Directory.CreateDirectory(MaterialFolder);
        Directory.CreateDirectory(GeneratedUiFolder);

        Material stone = GetOrCreateMaterial("M_MainMenu_Decor_Stone", new Color(0.08f, 0.09f, 0.1f, 1f), 0.35f);
        Material darkStone = GetOrCreateMaterial("M_MainMenu_Decor_DarkStone", new Color(0.025f, 0.028f, 0.034f, 1f), 0.2f);
        Material bronze = GetOrCreateMaterial("M_MainMenu_Decor_Bronze", new Color(0.45f, 0.28f, 0.12f, 1f), 0.55f);
        Material paper = GetOrCreateMaterial("M_MainMenu_Decor_Paper", new Color(0.62f, 0.54f, 0.42f, 1f), 0.25f);
        Material ember = GetOrCreateMaterial("M_MainMenu_Decor_Ember", new Color(1f, 0.45f, 0.12f, 1f), 0.15f, true);
        Material clue = GetOrCreateMaterial("M_MainMenu_Decor_Clue", new Color(0.18f, 0.2f, 0.24f, 1f), 0.3f);

        DestroyNamedRoot("MainMenu_TitleDecor");
        GameObject decorRoot = new GameObject("MainMenu_TitleDecor");
        decorRoot.transform.position = Vector3.zero;

        GameObject baseRoot = CreateChild(decorRoot.transform, "Base");
        GameObject noSaveRoot = CreateChild(decorRoot.transform, "MainMenu_Decor_NoSave");
        GameObject earlyRoot = CreateChild(decorRoot.transform, "MainMenu_Decor_EarlyProgress");
        GameObject midRoot = CreateChild(decorRoot.transform, "MainMenu_Decor_MidProgress");
        GameObject lateRoot = CreateChild(decorRoot.transform, "MainMenu_Decor_LateProgress");
        GameObject clueRoot = CreateChild(decorRoot.transform, "CursorIntercation_Clues");

        BuildBaseDecor(baseRoot.transform, stone, darkStone, bronze);
        BuildStateDecor(noSaveRoot.transform, earlyRoot.transform, midRoot.transform, lateRoot.transform, paper, bronze, ember);
        BuildCursorClues(clueRoot.transform, clue);

        Light ambientLight = CreatePointLight("MainMenu_Decor_Ambient", decorRoot.transform, new Vector3(0f, 2.3f, 6f), 9f, 0.35f, new Color(0.38f, 0.42f, 0.5f, 1f));
        Light accentLight = CreatePointLight("MainMenu_Decor_ProgressLight", decorRoot.transform, new Vector3(0f, 1.2f, 7.2f), 5f, 1.2f, new Color(1f, 0.75f, 0.42f, 1f));
        Light torchLight = CreatePointLight("MainMenu_CursorTorch", decorRoot.transform, Vector3.zero, 6.5f, 25f, new Color(1f, 0.78f, 0.45f, 1f));

        MainMenuTitleDecorController decorController = decorRoot.AddComponent<MainMenuTitleDecorController>();
        AssignObject(decorController, "noSaveRoot", noSaveRoot);
        AssignObject(decorController, "earlyProgressRoot", earlyRoot);
        AssignObject(decorController, "midProgressRoot", midRoot);
        AssignObject(decorController, "lateProgressRoot", lateRoot);
        AssignObject(decorController, "ambientLight", ambientLight);
        AssignObject(decorController, "accentLight", accentLight);
        AssignRendererArray(decorController, "progressTintRenderers", new[]
        {
            FindRenderer(baseRoot.transform, "Chronicle_Seal"),
            FindRenderer(midRoot.transform, "Mid_Brazier"),
            FindRenderer(lateRoot.transform, "Late_Relic")
        });
        decorController.RefreshDecor();

        Camera mainCamera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        ConfigureCamera(mainCamera);
        Canvas mainCanvas = Object.FindAnyObjectByType<Canvas>();
        GameObject pointerCursor = InstallPointerCursor(mainCanvas, mainCamera, torchLight);
        pointerCursor.transform.SetAsLastSibling();

        RemoveLegacyForcedCursors();
        ClearLegacyCursorReferences();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("MainMenuTitleSceneInstaller: decor 3D, pointeur souris/manette et torche installes dans MainMenu.");
    }

    private static void BuildBaseDecor(Transform parent, Material stone, Material darkStone, Material bronze)
    {
        CreatePrimitive(parent, "Floor", PrimitiveType.Cube, new Vector3(0f, -1.05f, 7f), new Vector3(12f, 0.24f, 15f), stone, false);
        CreatePrimitive(parent, "BackWall", PrimitiveType.Cube, new Vector3(0f, 1.6f, 13.2f), new Vector3(12f, 5.4f, 0.35f), darkStone, false);
        CreatePrimitive(parent, "LeftWall", PrimitiveType.Cube, new Vector3(-6.1f, 1.2f, 7f), new Vector3(0.35f, 4.8f, 15f), darkStone, false);
        CreatePrimitive(parent, "RightWall", PrimitiveType.Cube, new Vector3(6.1f, 1.2f, 7f), new Vector3(0.35f, 4.8f, 15f), darkStone, false);
        CreatePrimitive(parent, "ArchiveTable", PrimitiveType.Cube, new Vector3(0f, -0.35f, 5.9f), new Vector3(3.2f, 0.35f, 1.5f), bronze, false);
        CreatePrimitive(parent, "Chronicle_Seal", PrimitiveType.Cylinder, new Vector3(0f, -0.1f, 5.15f), new Vector3(0.7f, 0.08f, 0.7f), bronze, false);

        for (int i = 0; i < 4; i++)
        {
            float x = i < 2 ? -4.4f : 4.4f;
            float z = i % 2 == 0 ? 3.2f : 10.8f;
            CreatePrimitive(parent, $"Pillar_{i + 1}", PrimitiveType.Cylinder, new Vector3(x, 0.75f, z), new Vector3(0.55f, 1.85f, 0.55f), stone, false);
        }
    }

    private static void BuildStateDecor(Transform noSaveRoot, Transform earlyRoot, Transform midRoot, Transform lateRoot, Material paper, Material bronze, Material ember)
    {
        CreatePrimitive(noSaveRoot, "Empty_Ledger", PrimitiveType.Cube, new Vector3(0f, -0.07f, 5.72f), new Vector3(1.25f, 0.08f, 0.8f), paper, false);

        CreatePrimitive(earlyRoot, "Early_Ledger", PrimitiveType.Cube, new Vector3(-0.7f, -0.05f, 5.65f), new Vector3(1.4f, 0.08f, 0.9f), paper, false);
        CreatePrimitive(earlyRoot, "Early_Candle", PrimitiveType.Cylinder, new Vector3(0.9f, 0.06f, 5.65f), new Vector3(0.12f, 0.25f, 0.12f), bronze, false);
        CreatePrimitive(earlyRoot, "Early_Flame", PrimitiveType.Sphere, new Vector3(0.9f, 0.38f, 5.65f), new Vector3(0.18f, 0.28f, 0.18f), ember, false);

        CreatePrimitive(midRoot, "Mid_MapStack", PrimitiveType.Cube, new Vector3(-1.1f, 0f, 5.45f), new Vector3(1.3f, 0.14f, 0.9f), paper, false);
        CreatePrimitive(midRoot, "Mid_Brazier", PrimitiveType.Cylinder, new Vector3(1.25f, 0.0f, 5.75f), new Vector3(0.45f, 0.28f, 0.45f), bronze, false);
        CreatePrimitive(midRoot, "Mid_BrazierFlame", PrimitiveType.Sphere, new Vector3(1.25f, 0.42f, 5.75f), new Vector3(0.45f, 0.55f, 0.45f), ember, false);

        CreatePrimitive(lateRoot, "Late_Relic", PrimitiveType.Sphere, new Vector3(0f, 0.55f, 5.65f), new Vector3(0.7f, 0.7f, 0.7f), ember, false);
        CreatePrimitive(lateRoot, "Late_Fragment_Left", PrimitiveType.Cube, new Vector3(-1.2f, 0.35f, 5.9f), new Vector3(0.2f, 0.7f, 0.08f), bronze, false);
        CreatePrimitive(lateRoot, "Late_Fragment_Right", PrimitiveType.Cube, new Vector3(1.2f, 0.35f, 5.9f), new Vector3(0.2f, 0.7f, 0.08f), bronze, false);
    }

    private static void BuildCursorClues(Transform parent, Material clueMaterial)
    {
        GameObject ledger = CreatePrimitive(parent, "Clue_SaveLedger", PrimitiveType.Cube, new Vector3(-2.1f, -0.02f, 5.55f), new Vector3(0.75f, 0.12f, 0.48f), clueMaterial, true);
        AddCursorIntercation(ledger);

        GameObject wallMark = CreatePrimitive(parent, "Clue_WallMark", PrimitiveType.Cube, new Vector3(2.6f, 1.45f, 13.0f), new Vector3(1.0f, 0.55f, 0.08f), clueMaterial, true);
        AddCursorIntercation(wallMark);

        GameObject mask = CreatePrimitive(parent, "Clue_BrokenMask", PrimitiveType.Sphere, new Vector3(3.4f, -0.55f, 7.4f), new Vector3(0.38f, 0.48f, 0.18f), clueMaterial, true);
        AddCursorIntercation(mask);
    }

    private static void AddCursorIntercation(GameObject target)
    {
        CursorIntercation interaction = target.GetComponent<CursorIntercation>();
        if (interaction == null)
        {
            interaction = target.AddComponent<CursorIntercation>();
        }

        RuntimeOutlineUtility.EnsureOutlineTargets(target);

        SerializedObject serialized = new SerializedObject(interaction);
        serialized.FindProperty("createOutlineIfMissing").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject InstallPointerCursor(Canvas canvas, Camera camera, Light torchLight)
    {
        if (canvas == null)
        {
            Debug.LogWarning("MainMenuTitleSceneInstaller: Canvas MainMenu introuvable, creation du pointeur ignoree.");
            return new GameObject("MainMenu_PointerCursor_MissingCanvas");
        }

        Transform oldCursor = FindInHierarchy(canvas.transform, "MainMenu_PointerCursor");
        if (oldCursor != null)
        {
            Object.DestroyImmediate(oldCursor.gameObject);
        }

        Sprite cursorSprite = LoadOrCreateCursorSprite();
        GameObject cursor = new GameObject("MainMenu_PointerCursor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(MainMenuPointerCursor));
        cursor.layer = canvas.gameObject.layer;
        cursor.transform.SetParent(canvas.transform, false);

        RectTransform rect = cursor.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.12f, 0.86f);
        rect.sizeDelta = new Vector2(42f, 52f);

        Image image = cursor.GetComponent<Image>();
        image.sprite = cursorSprite;
        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = true;

        MainMenuPointerCursor pointer = cursor.GetComponent<MainMenuPointerCursor>();
        AssignObject(pointer, "canvas", canvas);
        AssignObject(pointer, "cursorVisual", rect);
        AssignObject(pointer, "decorCamera", camera);
        AssignObject(pointer, "torchLight", torchLight);
        AssignObject(pointer, "torchBoundsRoot", torchLight != null ? torchLight.transform.parent : null);
        AssignFloat(pointer, "gamepadSpeed", 1150f);
        AssignFloat(pointer, "worldRayDistance", 80f);
        return cursor;
    }

    private static Sprite LoadOrCreateCursorSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(CursorSpritePath);
        if (existing != null)
        {
            return existing;
        }

        Texture2D texture = new Texture2D(32, 40, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color border = new Color(0f, 0f, 0f, 1f);
        Color fill = new Color(1f, 0.92f, 0.72f, 1f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                bool insideHead = x <= 16 && y >= 22 - x && y <= 39 - x / 2;
                bool insideStem = x >= 8 && x <= 14 && y >= 4 && y <= 24;
                bool inside = insideHead || insideStem;
                bool edge = inside && (x <= 1 || y <= 1 || x >= texture.width - 2 || y >= texture.height - 2 ||
                    !IsCursorPixelInside(x - 1, y) || !IsCursorPixelInside(x + 1, y) ||
                    !IsCursorPixelInside(x, y - 1) || !IsCursorPixelInside(x, y + 1));
                texture.SetPixel(x, y, inside ? edge ? border : fill : clear);
            }
        }

        texture.Apply();
        File.WriteAllBytes(CursorSpritePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(CursorSpritePath);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(CursorSpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 32f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(CursorSpritePath);
    }

    private static bool IsCursorPixelInside(int x, int y)
    {
        bool insideHead = x <= 16 && y >= 22 - x && y <= 39 - x / 2;
        bool insideStem = x >= 8 && x <= 14 && y >= 4 && y <= 24;
        return insideHead || insideStem;
    }

    private static void RemoveLegacyForcedCursors()
    {
        DestroyNamedRoot("MainMenu_Cursor");
        DestroyObjectByName("MainMenu_Cursor");
        DestroyObjectByName("VirtualKeyboardCursor");
        DestroyObjectByName("MainMenu_VirtualKeyboardCursor");

        MenuCursorLink[] links = Object.FindObjectsByType<MenuCursorLink>(FindObjectsInactive.Include);
        for (int i = 0; i < links.Length; i++)
        {
            Object.DestroyImmediate(links[i]);
        }

        CursorController[] cursors = Object.FindObjectsByType<CursorController>(FindObjectsInactive.Include);
        for (int i = 0; i < cursors.Length; i++)
        {
            if (cursors[i] != null && IsInMainMenuScene(cursors[i].gameObject))
            {
                Object.DestroyImmediate(cursors[i].gameObject);
            }
        }

        MenuCursorNavigator[] navigators = Object.FindObjectsByType<MenuCursorNavigator>(FindObjectsInactive.Include);
        for (int i = 0; i < navigators.Length; i++)
        {
            if (navigators[i] != null && IsInMainMenuScene(navigators[i].gameObject))
            {
                Object.DestroyImmediate(navigators[i]);
            }
        }
    }

    private static void ClearLegacyCursorReferences()
    {
        MainMenuController controller = Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            return;
        }

        AssignObject(controller, "sharedCursor", null);
        AssignObject(controller, "gameOptionsCursorRoot", null);
        AssignObject(controller, "soloOptionsCursorRoot", null);
        AssignObject(controller, "multiOptionsCursorRoot", null);
        AssignObject(controller, "optionsCursorRoot", null);
        AssignObject(controller, "loadMenuCursorRoot", null);
        AssignObject(controller, "loadConfirmCursorRoot", null);
        AssignObject(controller, "newGameCursorRoot", null);
        AssignObject(controller, "joinCursorRoot", null);
        AssignObject(controller, "virtualKeyboardCursor", null);
    }

    private static void ConfigureCamera(Camera camera)
    {
        if (camera == null)
        {
            return;
        }

        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.005f, 0.007f, 0.01f, 1f);
        camera.fieldOfView = 55f;
        camera.transform.position = new Vector3(0f, 1.05f, -10f);
        camera.transform.rotation = Quaternion.identity;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.025f, 0.028f, 0.034f, 1f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.012f, 0.014f, 0.018f, 1f);
        RenderSettings.fogDensity = 0.025f;
    }

    private static Material GetOrCreateMaterial(string name, Color color, float smoothness, bool emission = false)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        SetMaterialColor(material, color, emission);
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", smoothness);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetMaterialColor(Material material, Color color, bool emission)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
        if (emission && material.HasProperty("_EmissiveColor"))
        {
            material.SetColor("_EmissiveColor", color * 2.5f);
        }
        if (emission && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2.5f);
        }
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static GameObject CreatePrimitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool keepCollider)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.position = position;
        obj.transform.localScale = scale;
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        if (!keepCollider)
        {
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        return obj;
    }

    private static Light CreatePointLight(string name, Transform parent, Vector3 position, float range, float intensity, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.position = position;
        Light light = obj.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = range;
        light.intensity = intensity;
        light.color = color;
        return light;
    }

    private static Renderer FindRenderer(Transform root, string name)
    {
        Transform found = FindInHierarchy(root, name);
        return found != null ? found.GetComponent<Renderer>() : null;
    }

    private static Transform FindInHierarchy(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindInHierarchy(root.GetChild(i), name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static void DestroyNamedRoot(string name)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].name == name)
            {
                Object.DestroyImmediate(roots[i]);
            }
        }
    }

    private static void DestroyObjectByName(string name)
    {
        GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == name && IsInMainMenuScene(all[i]))
            {
                Object.DestroyImmediate(all[i]);
            }
        }
    }

    private static bool IsInMainMenuScene(GameObject obj)
    {
        return obj != null && obj.scene == SceneManager.GetActiveScene();
    }

    private static void AssignObject(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void AssignFloat(Object target, string propertyName, float value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void AssignRendererArray(Object target, string propertyName, Renderer[] renderers)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        property.arraySize = renderers.Length;
        for (int i = 0; i < renderers.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
