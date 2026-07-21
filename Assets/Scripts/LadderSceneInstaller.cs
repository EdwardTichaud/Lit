using UnityEngine;
using UnityEngine.SceneManagement;
using Lit.Performance;

public static class LadderSceneInstaller
{
    private const float StackedLadderMaxHorizontalSize = 2.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        InstallSceneLadders(SceneManager.GetActiveScene());
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneTransitionProfiler.Mark($"Initialisation echelles debut ({scene.name})");
        InstallSceneLadders(scene);
        SceneTransitionProfiler.Mark($"Initialisation echelles fin ({scene.name})");
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
            return IsStackedLadderContainer(candidate);
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

    private static bool IsStackedLadderContainer(Transform candidate)
    {
        if (candidate == null || IsBroadLadderCollectionName(candidate.name))
        {
            return false;
        }

        Transform[] children = candidate.GetComponentsInChildren<Transform>(true);
        int segmentCount = 0;
        Bounds combined = default;
        bool hasBounds = false;
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == candidate || !IsLadderName(child.name.ToLowerInvariant()))
            {
                continue;
            }

            if (!TryCalculateBounds(child, out Bounds childBounds))
            {
                continue;
            }

            if (!hasBounds)
            {
                combined = childBounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(childBounds);
            }

            segmentCount++;
        }

        if (!hasBounds || segmentCount < 2)
        {
            return false;
        }

        float horizontalSize = Mathf.Max(combined.size.x, combined.size.z);
        float allowedHorizontalSize = Mathf.Max(StackedLadderMaxHorizontalSize, combined.size.y * 0.35f);
        return combined.size.y > horizontalSize && horizontalSize <= allowedHorizontalSize;
    }

    private static bool IsBroadLadderCollectionName(string objectName)
    {
        string normalizedName = NormalizeName(objectName);
        return normalizedName == "ladders" ||
               normalizedName == "echelles" ||
               normalizedName == "laddergroup" ||
               normalizedName == "laddercollection";
    }

    private static bool HasLadderInteractableInParent(Transform candidate)
    {
        Transform parent = candidate.parent;
        while (parent != null)
        {
            if (parent.GetComponent<LadderInteractable>() != null)
            {
                return true;
            }

            parent = parent.parent;
        }

        return false;
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(".", string.Empty)
            .ToLowerInvariant();
    }

    private static void ConfigureLadderInteraction(LadderInteractable ladder)
    {
        if (ladder == null)
        {
            return;
        }

        if (ladder.ladderController == null)
        {
            ladder.ladderController = ResolveOrCreateLadderController(ladder);
        }

        if (ladder.interactionCollider == null)
        {
            ladder.interactionCollider = FindLocalInteractionCollider(ladder);
        }

        if (ladder.interactionCollider == null && TryCalculateBounds(ladder.transform, out Bounds colliderBounds))
        {
            BoxCollider box = ladder.gameObject.AddComponent<BoxCollider>();
            FitBoxColliderToWorldBounds(box, colliderBounds);
            ladder.interactionCollider = box;
        }

        if (ladder.interactionCollider == null)
        {
            ladder.interactionCollider = CharacterInteractionDetection.ResolveInteractionCollider(ladder, null);
        }
    }

    private static Collider FindLocalInteractionCollider(LadderInteractable ladder)
    {
        if (ladder == null)
        {
            return null;
        }

        Collider[] colliders = ladder.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null && collider.enabled && !collider.isTrigger)
            {
                return collider;
            }
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null && collider.enabled)
            {
                return collider;
            }
        }

        return null;
    }

    private static LadderController ResolveOrCreateLadderController(LadderInteractable ladder)
    {
        if (ladder == null)
        {
            return null;
        }

        LadderController controller = ladder.GetComponent<LadderController>();
        if (controller != null)
        {
            return controller;
        }

        controller = ladder.GetComponentInParent<LadderController>();
        if (controller != null)
        {
            return controller;
        }

        controller = ladder.GetComponentInChildren<LadderController>(true);
        if (controller != null)
        {
            return controller;
        }

        return ladder.gameObject.AddComponent<LadderController>();
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
