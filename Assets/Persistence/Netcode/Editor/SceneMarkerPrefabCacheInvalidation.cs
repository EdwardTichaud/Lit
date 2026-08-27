using System;
using UnityEditor;

/// <summary>
/// SceneMarker handlers must never retain a prefab object from before an
/// authoring change. Clearing their lightweight cache is safe both before play
/// and while iterating in the editor.
/// </summary>
public sealed class SceneMarkerPrefabCacheInvalidation : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (TouchesCharacterOrPrefab(importedAssets) ||
            TouchesCharacterOrPrefab(deletedAssets) ||
            TouchesCharacterOrPrefab(movedAssets) ||
            TouchesCharacterOrPrefab(movedFromAssetPaths))
        {
            NetcodePrefabRegistry.InvalidateSceneMarkerCharacterCache();
        }
    }

    private static bool TouchesCharacterOrPrefab(string[] paths)
    {
        if (paths == null)
        {
            return false;
        }

        for (int index = 0; index < paths.Length; index++)
        {
            string path = paths[index];
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) &&
                path.IndexOf("Characters", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
