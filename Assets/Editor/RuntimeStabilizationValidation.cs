using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit repair of the legacy terrain tree's unsupported billboard path.</summary>
public static class RuntimeStabilizationValidation
{
    [MenuItem("Lit/Validation/Repair Legacy Terrain Tree LOD")]
    public static void RepairTerrainTreeLod()
    {
        const string path = "Assets/North Ember Studios/Terrain/Model/Träd.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Renderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Terrain tree has no mesh renderers.");
            // HDRP materials cannot use the legacy Nature/Soft Occlusion billboard.
            // A mesh LOD retains their lighting and silhouette up to normal culling.
            var group = root.GetComponent<LODGroup>();
            if (group == null)
            {
                group = root.AddComponent<LODGroup>();
                group.SetLODs(new[] { new LOD(0.01f, renderers) });
                group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            Directory.CreateDirectory("Library/Stabilization");
            File.WriteAllText("Library/Stabilization/tree-repair.txt", "Tree uses mesh LOD; original materials preserved. Visual distance/performance validation remains required.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
