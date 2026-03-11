using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Valide les objets de scene deja reseautes sans muter la scene au runtime.
public static class NetcodeSceneObjectInstaller
{
    private static readonly HashSet<GameObject> processedRoots = new HashSet<GameObject>();
    private static readonly HashSet<string> reportedMissingObjects = new HashSet<string>();

    public static void PrepareActiveScene()
    {
        PrepareScene(SceneManager.GetActiveScene());
    }

    public static void PrepareScene(Scene scene)
    {
        if (!scene.IsValid())
        {
            return;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        if (roots == null)
        {
            return;
        }

        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || processedRoots.Contains(root))
            {
                continue;
            }

            processedRoots.Add(root);
            ValidateSceneNetworkObjects(root, scene.path);
        }
    }

    private static void ValidateSceneNetworkObjects(GameObject root, string scenePath)
    {
        NetworkBehaviour[] behaviours = root.GetComponentsInChildren<NetworkBehaviour>(true);
        if (behaviours == null)
        {
            return;
        }

        for (int i = 0; i < behaviours.Length; i++)
        {
            NetworkBehaviour behaviour = behaviours[i];
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
                ReportMissingNetworkObject(scenePath, behaviour, host);
                continue;
            }

            uint hash = NetcodeSceneIdUtility.GetStableId(networkObject.transform);
            NetcodeRuntimeUtilities.EnsureSceneObjectHash(
                networkObject,
                hash,
                $"{behaviour.GetType().Name}:{host.name}");
        }
    }

    private static void ReportMissingNetworkObject(string scenePath, NetworkBehaviour behaviour, GameObject host)
    {
        string path = string.IsNullOrWhiteSpace(scenePath) ? "<scene inconnue>" : scenePath;
        string key = $"{path}:{behaviour.GetType().Name}:{host.GetInstanceID()}";
        if (!reportedMissingObjects.Add(key))
        {
            return;
        }

        Debug.LogError(
            $"NetcodeSceneObjectInstaller: {host.name} ({behaviour.GetType().Name}) dans {path} n'a aucun NetworkObject serialize. " +
            "Execute Tools > Lit > Netcode > Prepare Scene Network Objects pour corriger la scene.");
    }

    private static bool ShouldSkipScenePreparation(NetworkBehaviour behaviour)
    {
        return behaviour is WorldInteractionService
            || behaviour is NetcodeCharacterIdentity
            || behaviour is NetcodeLocalPlayer
            || behaviour is NetworkCharacterInput
            || behaviour is NetworkInventory;
    }
}
