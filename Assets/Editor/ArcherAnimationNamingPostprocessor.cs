using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;

[InitializeOnLoad]
public sealed class ArcherAnimationNamingPostprocessor : AssetPostprocessor
{
    private const string ArcherAnimationsFolder = "Assets/Characters/4_Animations/Archer/";
    private const string Prefix = "Mixamo_Archer_";
    private static readonly HashSet<string> PendingAssetPaths = new HashSet<string>();
    private static bool processingScheduled;

    static ArcherAnimationNamingPostprocessor()
    {
        QueueFolder();
    }

    [MenuItem("Tools/Lit/Rename Archer Animation Clips")]
    private static void RenameAllArcherAnimationClips()
    {
        QueueFolder();
    }

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        QueueAnimationAssets(importedAssets);
        QueueAnimationAssets(movedAssets);
    }

    private static void QueueFolder()
    {
        QueueAnimationAssets(AssetDatabase.FindAssets("t:Model", new[] { ArcherAnimationsFolder }));
    }

    private static void QueueAnimationAssets(IEnumerable<string> assets)
    {
        foreach (string asset in assets)
        {
            string path = asset.StartsWith("guid:") ? AssetDatabase.GUIDToAssetPath(asset) : asset;
            if (!IsArcherAnimation(path))
            {
                continue;
            }

            PendingAssetPaths.Add(path);
        }

        if (PendingAssetPaths.Count == 0 || processingScheduled)
        {
            return;
        }

        processingScheduled = true;
        EditorApplication.delayCall += RenamePendingClips;
    }

    private static void RenamePendingClips()
    {
        processingScheduled = false;
        string[] paths = new string[PendingAssetPaths.Count];
        PendingAssetPaths.CopyTo(paths);
        PendingAssetPaths.Clear();

        foreach (string path in paths)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                continue;
            }

            string fileName = Path.GetFileNameWithoutExtension(path);
            if (fileName.StartsWith(Prefix))
            {
                fileName = fileName.Substring(Prefix.Length);
            }

            string desiredName = Prefix + ToAnimationName(fileName);
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }
            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].name == desiredName)
                {
                    continue;
                }

                clips[i].name = desiredName;
                changed = true;
            }

            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }
    }

    private static bool IsArcherAnimation(string path)
    {
        return !string.IsNullOrEmpty(path) &&
            path.StartsWith(ArcherAnimationsFolder) &&
            path.EndsWith(".fbx") &&
            Path.GetFileNameWithoutExtension(path) != "Y Bot";
    }

    private static string ToAnimationName(string fileName)
    {
        string[] words = fileName.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            words[i] = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words[i]);
        }

        return string.Join("_", words);
    }
}
