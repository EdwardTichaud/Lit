using UnityEngine;
using UnityEngine.SceneManagement;

public static class LadderSceneInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        InstallSceneLadders(SceneManager.GetActiveScene());
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallSceneLadders(scene);
    }

    private static void InstallSceneLadders(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            InstallLaddersInRoot(roots[i]);
        }
    }

    private static void InstallLaddersInRoot(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (!IsLadderCandidate(candidate))
            {
                continue;
            }

            if (HasLadderInteractableInParent(candidate))
            {
                continue;
            }

            LadderInteractable ladder = candidate.GetComponent<LadderInteractable>();
            if (ladder == null)
            {
                ladder = candidate.gameObject.AddComponent<LadderInteractable>();
            }

            ConfigureLadderInteraction(ladder);
        }
    }

    private static bool IsLadderCandidate(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        string lowerName = candidate.name.ToLowerInvariant();
        if (!IsLadderName(lowerName))
        {
            return false;
        }

        if (HasLocalLadderGeometry(candidate))
        {
            return true;
        }

        if (HasDescendantLadderCandidate(candidate))
        {
            return false;
        }

        return candidate.GetComponentInChildren<Collider>(true) != null ||
               candidate.GetComponentInChildren<Renderer>(true) != null;
    }

    private static bool IsLadderName(string lowerName)
    {
        return lowerName.Contains("ladder") ||
               lowerName.Contains("echelle") ||
               lowerName.Contains("échelle");
    }

    private static bool HasLocalLadderGeometry(Transform candidate)
    {
        return candidate != null &&
               (candidate.GetComponent<Collider>() != null ||
                candidate.GetComponent<Renderer>() != null ||
                candidate.GetComponent<MeshFilter>() != null);
    }

    private static bool HasDescendantLadderCandidate(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        Transform[] children = candidate.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == candidate)
            {
                continue;
            }

            if (IsLadderName(child.name.ToLowerInvariant()) &&
                (HasLocalLadderGeometry(child) ||
                 child.GetComponentInChildren<Collider>(true) != null ||
                 child.GetComponentInChildren<Renderer>(true) != null))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLadderContainer(Transform candidate)
    {
        return candidate != null &&
               IsLadderName(candidate.name.ToLowerInvariant()) &&
               !HasLocalLadderGeometry(candidate) &&
               HasDescendantLadderCandidate(candidate);
    }

    private static bool HasLadderInteractableInParent(Transform candidate)
    {
        Transform parent = candidate.parent;
        while (parent != null)
        {
            if (parent.GetComponent<LadderInteractable>() != null && !IsLadderContainer(parent))
            {
                return true;
            }

            parent = parent.parent;
        }

        return false;
    }

    private static void ConfigureLadderInteraction(LadderInteractable ladder)
    {
        if (ladder == null)
        {
            return;
        }

        if (ladder.interactionCollider == null)
        {
            ladder.interactionCollider = CharacterInteractionDetection.ResolveInteractionCollider(ladder, null);
        }

        if (ladder.interactionCollider == null && TryCalculateBounds(ladder.transform, out Bounds colliderBounds))
        {
            BoxCollider box = ladder.gameObject.AddComponent<BoxCollider>();
            FitBoxColliderToWorldBounds(box, colliderBounds);
            ladder.interactionCollider = box;
        }
    }

    private static bool TryCalculateBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
        {
            return false;
        }

        bool hasBounds = false;
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static void FitBoxColliderToWorldBounds(BoxCollider box, Bounds worldBounds)
    {
        if (box == null)
        {
            return;
        }

        Transform target = box.transform;
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
        };

        Bounds localBounds = new Bounds(target.InverseTransformPoint(corners[0]), Vector3.zero);
        for (int i = 1; i < corners.Length; i++)
        {
            localBounds.Encapsulate(target.InverseTransformPoint(corners[i]));
        }

        box.isTrigger = false;
        box.center = localBounds.center;
        box.size = localBounds.size;
    }
}
