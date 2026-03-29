using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AllFacesTriplanarImportHook
{
    private const string ShaderGraphPath = "Assets/Material/AllFaces_CoherentTriplanarHDRP.shadergraph";
    private const string MaterialPath = "Assets/Material/AllFaces_CoherentTriplanarHDRP.mat";
    private const string StatusPath = "Temp/AllFacesTriplanarImportStatus.txt";

    [InitializeOnLoadMethod]
    private static void TryImport()
    {
        EditorApplication.delayCall += Run;
    }

    private static void Run()
    {
        try
        {
            AssetDatabase.ImportAsset(ShaderGraphPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(MaterialPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderGraphPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            if (shader == null)
            {
                throw new InvalidOperationException("ShaderGraph import returned a null Shader.");
            }

            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "AllFaces_CoherentTriplanarHDRP"
                };

                AssetDatabase.CreateAsset(material, MaterialPath);
                AssetDatabase.ImportAsset(MaterialPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            AssetDatabase.SaveAssets();
            WriteStatus("OK");
            Debug.Log("[AllFacesTriplanarImportHook] ShaderGraph imported successfully.");
        }
        catch (Exception exception)
        {
            WriteStatus(exception.ToString());
            Debug.LogException(exception);
        }
    }

    private static void WriteStatus(string content)
    {
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), StatusPath), content);
    }
}
