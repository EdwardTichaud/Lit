using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
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

        PrepareSquadCharacters(roots);
        PersistentWorldSceneInstaller.PrepareScene(scene);
    }

    private static void PrepareSquadCharacters(GameObject[] roots)
    {
        if (roots == null)
        {
            return;
        }

        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
            {
                continue;
            }

            SquadCharacterController[] controllers = root.GetComponentsInChildren<SquadCharacterController>(true);
            if (controllers == null)
            {
                continue;
            }

            for (int j = 0; j < controllers.Length; j++)
            {
                SquadCharacterController controller = controllers[j];
                if (controller == null)
                {
                    continue;
                }

                GameObject host = controller.gameObject;
                NetworkObject networkObject = NetcodeRuntimeUtilities.GetOrAdd<NetworkObject>(host);
                NetcodeRuntimeUtilities.GetOrAdd<NetworkTransform>(host);
                NetcodeRuntimeUtilities.GetOrAdd<NetcodeCharacterIdentity>(host);
                NetcodeRuntimeUtilities.GetOrAdd<NetcodeLocalPlayer>(host);
                NetcodeRuntimeUtilities.GetOrAdd<NetworkCharacterInput>(host);
                NetcodeRuntimeUtilities.GetOrAdd<NetworkInventory>(host);

                uint hash = NetcodeSceneIdUtility.GetStableId(networkObject.transform);
                NetcodeRuntimeUtilities.EnsureSceneObjectHash(networkObject, hash);
            }
        }
    }
}
