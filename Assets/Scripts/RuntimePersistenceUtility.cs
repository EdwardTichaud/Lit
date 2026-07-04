using UnityEngine;
using Unity.Netcode;

public static class RuntimePersistenceUtility
{
    public static void DontDestroyOnLoadRoot(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (target.GetComponentInParent<NetworkObject>() != null)
        {
            return;
        }

        Transform transform = target.transform;
        if (transform.parent != null)
        {
            transform.SetParent(null, true);
        }

        Object.DontDestroyOnLoad(target);
    }
}
