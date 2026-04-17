using UnityEngine;
using UnityEngine.SceneManagement;

public static class LadderSceneInstaller
{
    private const string BottomAnchorName = "Base_Basse";
    private const string TopAnchorName = "Base_Haute";
    private const float DefaultAnchorStandOff = 0.45f;
    private const float DefaultAnchorVerticalPadding = 0.05f;

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

            ConfigureLadder(ladder);
        }
    }

    private static bool IsLadderCandidate(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        string lowerName = candidate.name.ToLowerInvariant();
        if (!lowerName.Contains("ladder") &&
            !lowerName.Contains("echelle") &&
            !lowerName.Contains("échelle"))
        {
            return false;
        }

        return candidate.GetComponentInChildren<Collider>(true) != null ||
               candidate.GetComponentInChildren<Renderer>(true) != null;
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

    private static void ConfigureLadder(LadderInteractable ladder)
    {
        if (ladder == null)
        {
            return;
        }

        Transform root = ladder.transform;
        if (ladder.interactionCollider == null)
        {
            ladder.interactionCollider = CharacterInteractionDetection.ResolveInteractionCollider(ladder, null);
        }

        if (ladder.interactionCollider == null && TryCalculateBounds(root, out Bounds colliderBounds))
        {
            BoxCollider box = root.gameObject.AddComponent<BoxCollider>();
            FitBoxColliderToWorldBounds(box, colliderBounds);
            ladder.interactionCollider = box;
        }

        bool createdAnchor = false;
        if (ladder.bottomBase == null)
        {
            ladder.bottomBase = FindOrCreateAnchor(root, BottomAnchorName, out bool createdBottom);
            createdAnchor |= createdBottom;
        }

        if (ladder.topBase == null)
        {
            ladder.topBase = FindOrCreateAnchor(root, TopAnchorName, out bool createdTop);
            createdAnchor |= createdTop;
        }

        if (ladder.bottomBase == null || ladder.topBase == null)
        {
            return;
        }

        if (!createdAnchor)
        {
            return;
        }

        if (!TryCalculateBounds(root, out Bounds bounds))
        {
            return;
        }

        Vector3 forward = root.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        else
        {
            forward.Normalize();
        }

        Vector3 offset = -forward * DefaultAnchorStandOff;
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
        Vector3 center = bounds.center + offset;
        float bottomY = bounds.min.y + DefaultAnchorVerticalPadding;
        float topY = bounds.max.y - DefaultAnchorVerticalPadding;
        if (topY < bottomY)
        {
            bottomY = bounds.center.y;
            topY = bounds.center.y;
        }

        ladder.bottomBase.SetPositionAndRotation(new Vector3(center.x, bottomY, center.z), rotation);
        ladder.topBase.SetPositionAndRotation(new Vector3(center.x, topY, center.z), rotation);
    }

    private static Transform FindOrCreateAnchor(Transform root, string anchorName, out bool created)
    {
        created = false;
        if (root == null)
        {
            return null;
        }

        Transform existing = root.Find(anchorName);
        if (existing != null)
        {
            return existing;
        }

        GameObject anchorObject = new GameObject(anchorName);
        Transform anchor = anchorObject.transform;
        anchor.SetParent(root, false);
        created = true;
        return anchor;
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
            if (collider == null || collider.GetComponent<LadderInteractable>() != null)
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
