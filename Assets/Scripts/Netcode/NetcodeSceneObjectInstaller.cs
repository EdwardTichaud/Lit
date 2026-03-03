using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Ajoute un NetworkObject aux objets de scene qui ont des NetworkBehaviour.
public static class NetcodeSceneObjectInstaller
{
    private static readonly HashSet<GameObject> processedRoots = new HashSet<GameObject>();

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
            NetworkBehaviour[] behaviours = root.GetComponentsInChildren<NetworkBehaviour>(true);
            if (behaviours == null)
            {
                continue;
            }

            for (int j = 0; j < behaviours.Length; j++)
            {
                NetworkBehaviour behaviour = behaviours[j];
                if (behaviour == null)
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
                    if (networkObject != null)
                    {
                        uint parentHash = NetcodeSceneIdUtility.GetStableId(networkObject.transform);
                        NetcodeRuntimeUtilities.EnsureSceneObjectHash(networkObject, parentHash);
                        continue;
                    }

                    networkObject = host.AddComponent<NetworkObject>();
                }

                uint hash = NetcodeSceneIdUtility.GetStableId(networkObject.transform);
                NetcodeRuntimeUtilities.EnsureSceneObjectHash(networkObject, hash);
            }
        }
    }
}
