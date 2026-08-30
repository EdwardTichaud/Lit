using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Builds a deterministic additive course for UCC exploration tuning.</summary>
public static class UccExplorationTestCourseBuilder
{
    private const string ScenePath = "Assets/Scenes/Tests/UccExplorationCourse.unity";

    [MenuItem("Lit/UCC/Create Exploration Test Course")]
    public static void CreateCourse()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject root = new GameObject("UCC Exploration Test Course");

        CreateCube(root.transform, "Ground_Line", new Vector3(0f, -0.25f, 0f), new Vector3(8f, 0.5f, 50f));
        CreateMarker(root.transform, "Spawn_Exploration", new Vector3(0f, 0.1f, -20f));
        CreateMarker(root.transform, "Start_Run_Stop", new Vector3(0f, 0.1f, -12f));
        CreateMarker(root.transform, "Start_Reverse", new Vector3(0f, 0.1f, -2f));

        CreateCube(root.transform, "Low_Step", new Vector3(0f, 0.2f, 5f), new Vector3(3f, 0.4f, 1f));
        CreateCube(root.transform, "Slope", new Vector3(0f, 1f, 12f), new Vector3(4f, 0.5f, 10f), new Vector3(-11f, 0f, 0f));
        CreateCube(root.transform, "Landing_Platform", new Vector3(0f, 2.05f, 17.5f), new Vector3(4f, 0.5f, 4f));
        CreateCube(root.transform, "Jump_Platform", new Vector3(0f, 3.5f, 24f), new Vector3(4f, 0.5f, 4f));
        CreateCube(root.transform, "Jump_Drop", new Vector3(0f, 0.4f, 31f), new Vector3(8f, 0.8f, 8f));

        CreateCube(root.transform, "Camera_Corridor_Left", new Vector3(-2.4f, 1.5f, 39f), new Vector3(0.4f, 3f, 12f));
        CreateCube(root.transform, "Camera_Corridor_Right", new Vector3(2.4f, 1.5f, 39f), new Vector3(0.4f, 3f, 12f));
        CreateCube(root.transform, "Camera_Corridor_Ground", new Vector3(0f, -0.25f, 39f), new Vector3(5f, 0.5f, 12f));
        CreateMarker(root.transform, "Camera_Corridor_Start", new Vector3(0f, 0.1f, 34f));

        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Scenes/Tests");
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        Debug.Log("[UCC] Exploration test course created: " + ScenePath);
    }

    private static void CreateCube(Transform parent, string name, Vector3 position, Vector3 scale, Vector3? eulerAngles = null)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.SetPositionAndRotation(position, Quaternion.Euler(eulerAngles ?? Vector3.zero));
        cube.transform.localScale = scale;
        cube.isStatic = true;
    }

    private static void CreateMarker(Transform parent, string name, Vector3 position)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = position;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name)) AssetDatabase.CreateFolder(parent, name);
    }
}
