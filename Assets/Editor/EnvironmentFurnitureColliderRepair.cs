using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures solid environment furniture has a physical collider.  The repair is
/// deliberately restricted to furniture roots so VFX and gameplay triggers are
/// never turned into level geometry.
/// </summary>
[InitializeOnLoad]
public static class EnvironmentFurnitureColliderRepair
{
    private const string IceTablePrefabPath = "Assets/Environment/Prefabs_Ice/SM_Table/SM_Table_Ice.prefab";
    private const string RequestPath = "Library/EnvironmentFurnitureColliderRepair.request";
    private const string ResultPath = "Library/EnvironmentFurnitureColliderRepair.result";

    static EnvironmentFurnitureColliderRepair()
    {
        EditorApplication.delayCall += RunRequestedRepair;
        EditorApplication.update += RunRequestedRepair;
    }

    [MenuItem("Lit/Environment/Repair Missing Furniture Colliders")]
    public static void RepairMissingFurnitureColliders()
    {
        int repaired = RepairIceTablePrefab();
        foreach (string sceneGuid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes/District_1" }))
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
            repaired += RepairScene(scenePath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Lit Environment] Repaired {repaired} missing furniture collider(s).");
    }

    private static void RunRequestedRepair()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        try
        {
            File.Delete(RequestPath);
            RepairMissingFurnitureColliders();
            File.WriteAllText(ResultPath, "success " + DateTime.UtcNow.ToString("O"));
        }
        catch (Exception exception)
        {
            File.WriteAllText(ResultPath, "failure\n" + exception);
            Debug.LogException(exception);
        }
    }

    private static int RepairIceTablePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(IceTablePrefabPath);
        try
        {
            if (HasRootSolidCollider(root))
                return 0;

            MeshFilter meshFilter = root.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                throw new InvalidOperationException("SM_Table_Ice has no mesh from which to build a collider.");

            MeshCollider collider = root.AddComponent<MeshCollider>();
            collider.sharedMesh = meshFilter.sharedMesh;
            collider.convex = false;
            collider.isTrigger = false;
            PrefabUtility.SaveAsPrefabAsset(root, IceTablePrefabPath);
            return 1;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static int RepairScene(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        int repaired = 0;
        try
        {
            foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (gameObject.scene != scene || !IsSolidFurnitureRoot(gameObject) || HasRootSolidCollider(gameObject))
                    continue;

                if (!TryAddBoundsCollider(gameObject))
                    continue;

                repaired++;
                EditorSceneManager.MarkSceneDirty(scene);
            }

            if (scene.isDirty)
                EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        return repaired;
    }

    private static bool IsSolidFurnitureRoot(GameObject gameObject)
    {
        string name = gameObject.name;
        return name == "Table" || name == "CrudeShelf" ||
               name.StartsWith("SM_Table", StringComparison.Ordinal) ||
               name.StartsWith("SM_Shelf", StringComparison.Ordinal) ||
               name.StartsWith("CrudeShelf", StringComparison.Ordinal);
    }

    private static bool HasRootSolidCollider(GameObject gameObject)
    {
        foreach (Collider collider in gameObject.GetComponents<Collider>())
            if (collider != null && !collider.isTrigger)
                return true;
        return false;
    }

    private static bool TryAddBoundsCollider(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return false;

        bool hasBounds = false;
        Bounds localBounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (renderer is ParticleSystemRenderer || !renderer.enabled)
                continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            for (int x = 0; x <= 1; x++)
            for (int y = 0; y <= 1; y++)
            for (int z = 0; z <= 1; z++)
            {
                Vector3 point = root.transform.InverseTransformPoint(new Vector3(
                    x == 0 ? min.x : max.x,
                    y == 0 ? min.y : max.y,
                    z == 0 ? min.z : max.z));
                if (hasBounds) localBounds.Encapsulate(point);
                else { localBounds = new Bounds(point, Vector3.zero); hasBounds = true; }
            }
        }

        if (!hasBounds || localBounds.size.sqrMagnitude < 0.0001f)
            return false;

        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = localBounds.center;
        collider.size = localBounds.size;
        collider.isTrigger = false;
        return true;
    }
}
