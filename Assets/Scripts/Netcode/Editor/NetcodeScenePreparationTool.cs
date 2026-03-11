#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NetcodeScenePreparationTool
{
    [MenuItem("Tools/Lit/Netcode/Scan Scene Network Objects")]
    private static void ScanSceneNetworkObjects()
    {
        ScenePreparationReport report = BuildReport(prepareMissingNetworkObjects: false);
        Debug.Log(report.ToLogString("scan"));
    }

    [MenuItem("Tools/Lit/Netcode/Prepare Scene Network Objects")]
    private static void PrepareSceneNetworkObjects()
    {
        ScenePreparationReport report = BuildReport(prepareMissingNetworkObjects: true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(report.ToLogString("prepare"));
    }

    private static ScenePreparationReport BuildReport(bool prepareMissingNetworkObjects)
    {
        ScenePreparationReport report = new ScenePreparationReport();
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            report.failedScenes.Add("Operation annulee: scenes ouvertes non sauvegardees.");
            return report;
        }

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                ProcessScene(scene, scenePath, report, prepareMissingNetworkObjects);
            }
        }
        finally
        {
            if (previousSetup != null && previousSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
        }

        return report;
    }

    private static void ProcessScene(Scene scene, string scenePath, ScenePreparationReport report, bool prepareMissingNetworkObjects)
    {
        if (!scene.IsValid())
        {
            report.failedScenes.Add($"{scenePath} (scene invalide)");
            return;
        }

        bool sceneModified = false;
        HashSet<NetworkObject> readyObjects = new HashSet<NetworkObject>();
        List<string> missingEntries = new List<string>();

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            NetworkBehaviour[] behaviours = roots[i].GetComponentsInChildren<NetworkBehaviour>(true);
            for (int j = 0; j < behaviours.Length; j++)
            {
                NetworkBehaviour behaviour = behaviours[j];
                if (behaviour == null || ShouldSkipScenePreparation(behaviour))
                {
                    continue;
                }

                GameObject host = behaviour.gameObject;
                if (host == null)
                {
                    continue;
                }

                NetworkObject networkObject = host.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    networkObject = host.GetComponentInParent<NetworkObject>();
                }

                if (networkObject == null)
                {
                    string entry = $"{host.name} ({behaviour.GetType().Name})";
                    if (!prepareMissingNetworkObjects)
                    {
                        missingEntries.Add(entry);
                        continue;
                    }

                    networkObject = host.AddComponent<NetworkObject>();
                    EditorUtility.SetDirty(host);
                    EditorUtility.SetDirty(networkObject);
                    report.preparedEntries.Add($"{scenePath} :: {entry}");
                    sceneModified = true;
                }

                readyObjects.Add(networkObject);
            }
        }

        if (missingEntries.Count > 0)
        {
            for (int i = 0; i < missingEntries.Count; i++)
            {
                report.legacyFallbackEntries.Add($"{scenePath} :: {missingEntries[i]}");
            }
            return;
        }

        if (sceneModified)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.preparedScenes.Add($"{scenePath} ({readyObjects.Count} objets reseau)");
        }
        else
        {
            report.readyScenes.Add($"{scenePath} ({readyObjects.Count} objets reseau)");
        }
    }

    private static bool ShouldSkipScenePreparation(NetworkBehaviour behaviour)
    {
        return behaviour is WorldInteractionService
            || behaviour is NetcodeCharacterIdentity
            || behaviour is NetcodeLocalPlayer
            || behaviour is NetworkCharacterInput
            || behaviour is NetworkInventory;
    }

    private sealed class ScenePreparationReport
    {
        public readonly List<string> readyScenes = new List<string>();
        public readonly List<string> preparedScenes = new List<string>();
        public readonly List<string> preparedEntries = new List<string>();
        public readonly List<string> legacyFallbackEntries = new List<string>();
        public readonly List<string> failedScenes = new List<string>();

        public string ToLogString(string mode)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("NetcodeScenePreparationTool: mode=").Append(mode);
            builder.Append(" ready=").Append(readyScenes.Count);
            builder.Append(" preparedScenes=").Append(preparedScenes.Count);
            builder.Append(" preparedEntries=").Append(preparedEntries.Count);
            builder.Append(" legacyFallback=").Append(legacyFallbackEntries.Count);
            builder.Append(" failed=").Append(failedScenes.Count);

            AppendSection(builder, "readyScenes", readyScenes);
            AppendSection(builder, "preparedScenes", preparedScenes);
            AppendSection(builder, "preparedEntries", preparedEntries);
            AppendSection(builder, "legacyFallback", legacyFallbackEntries);
            AppendSection(builder, "failed", failedScenes);
            return builder.ToString();
        }

        private static void AppendSection(StringBuilder builder, string label, List<string> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            builder.AppendLine();
            builder.Append(label).Append(':');
            for (int i = 0; i < entries.Count; i++)
            {
                builder.AppendLine();
                builder.Append(" - ").Append(entries[i]);
            }
        }
    }
}
#endif
