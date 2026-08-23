using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneHierarchyOrganizer
{
    private const string MaisonScenePath = "Assets/Scenes/Maison.unity";
    private const string OrganizerRootName = "00_SCENE_ORGANIZATION";
    private const string ZonesRootName = "01_Zones_Rooms";
    private const string GlobalRootName = "02_Global";
    private const string UnsortedZoneName = "99_Unassigned";

    private static readonly Type[] InteractableComponentTypes =
    {
        typeof(InteractableItem),
        typeof(BuildingInfoInteractable),
        typeof(Door),
        typeof(LadderInteractable),
        typeof(Flame),
        typeof(StabReading),
        typeof(DestructibleObject),
        typeof(ReturnHomeTrigger),
        typeof(LabyrinthStartTrigger),
        typeof(HubCompanionSwapTrigger),
        typeof(MovementLabInteractable),
        typeof(MovementLabDoor)
    };

    private static readonly HashSet<string> RootOnlyComponentNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "AudioManager",
        "KnowledgeManager",
        "SquadManager",
        "ConfirmationManager",
        "InfoBoxUI",
        "LoadingScreenService",
        "SaveSessionManager",
        "ItemPassiveEffectSystem",
        "NetcodeBootstrap",
        "NetcodeLobbyUI",
        "LocalPlayerInput"
    };

    private sealed class RoomBucket
    {
        public readonly string Name;
        public readonly Transform Root;
        public readonly Transform Geometry;
        public readonly Transform Interactables;
        public readonly Transform Lights;
        public readonly Transform Ghosts;
        public readonly Transform Markers;
        public Bounds Bounds;
        public bool HasBounds;

        public RoomBucket(string name, Transform parent)
        {
            Name = name;
            Root = EnsureFolder(parent, name);
            Markers = EnsureFolder(Root, "00_Markers");
            Geometry = EnsureFolder(Root, "01_Geometry_Decor");
            Interactables = EnsureFolder(Root, "02_Interactables");
            Lights = EnsureFolder(Root, "03_Lights");
            Ghosts = EnsureFolder(Root, "04_Ghosts");
        }

        public Vector3 Center => HasBounds ? Bounds.center : Root.position;
    }

    [MenuItem("Lit/Scenes/Organize Maison Hierarchy")]
    public static void OrganizeMaisonHierarchyMenu()
    {
        OrganizeMaisonHierarchy();
    }

    [MenuItem("Lit/Scenes/Audit Maison Hierarchy")]
    public static void AuditMaisonHierarchyMenu()
    {
        AuditMaisonHierarchy();
    }

    [MenuItem("Lit/Scenes/Validate Maison Hierarchy")]
    public static void ValidateMaisonHierarchyMenu()
    {
        ValidateMaisonHierarchy();
    }

    [MenuItem("Lit/Scenes/Repair Maison Root Objects")]
    public static void RepairMaisonRootObjectsMenu()
    {
        RepairMaisonRootObjects();
    }

    public static void OrganizeMaisonHierarchy()
    {
        Scene scene = OpenMaisonScene();
        OrganizationStats stats = OrganizeScene(scene, dryRun: false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(stats.Format("Maison hierarchy organized"));
    }

    public static void AuditMaisonHierarchy()
    {
        Scene scene = OpenMaisonScene();
        OrganizationStats stats = OrganizeScene(scene, dryRun: true);
        Debug.Log(stats.Format("Maison hierarchy audit"));
    }

    public static void ValidateMaisonHierarchy()
    {
        Scene scene = OpenMaisonScene();
        HierarchyValidation validation = ValidateOrganizedScene(scene);
        Debug.Log(validation.Format());
    }

    public static void RepairMaisonRootObjects()
    {
        Scene scene = OpenMaisonScene();
        int repairedRootOnlyObjects = RepairRootOnlySceneObjects(scene);
        if (repairedRootOnlyObjects > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log($"Maison hierarchy root object repair: repairedRootOnlyObjects={repairedRootOnlyObjects}");
    }

    private static Scene OpenMaisonScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.path != MaisonScenePath)
        {
            return EditorSceneManager.OpenScene(MaisonScenePath, OpenSceneMode.Single);
        }

        return activeScene;
    }

    private static OrganizationStats OrganizeScene(Scene scene, bool dryRun)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            throw new InvalidOperationException("Scene not loaded.");
        }

        int repairedRootOnlyObjects = dryRun ? CountNestedRootOnlySceneObjects(scene) : RepairRootOnlySceneObjects(scene);

        List<GameObject> initialRoots = scene.GetRootGameObjects()
            .Where(root => root != null && root.name != OrganizerRootName && !ShouldRemainSceneRoot(root))
            .ToList();

        Transform organizerRoot = dryRun ? null : EnsureRoot(scene, OrganizerRootName);
        Transform zonesRoot = dryRun ? null : EnsureFolder(organizerRoot, ZonesRootName);
        Transform globalRoot = dryRun ? null : EnsureFolder(organizerRoot, GlobalRootName);
        RoomBucket globalBucket = dryRun ? null : new RoomBucket("00_Global", globalRoot);
        RoomBucket unassignedBucket = dryRun ? null : new RoomBucket(UnsortedZoneName, zonesRoot);

        List<RoomMarker> markers = CollectRoomMarkers(initialRoots);
        HashSet<Transform> virtualRoomRoots = new HashSet<Transform>(
            markers.Where(marker => !marker.MoveTransform && marker.Transform != null).Select(marker => marker.Transform));
        List<RoomBucket> buckets = dryRun
            ? new List<RoomBucket>()
            : CreateRoomBuckets(markers, zonesRoot, unassignedBucket);

        OrganizationStats stats = new OrganizationStats
        {
            RootCount = initialRoots.Count,
            RoomMarkerCount = markers.Count,
            RoomCount = dryRun ? CountDistinctRoomNames(markers) : buckets.Count,
            RepairedRootOnlyObjects = repairedRootOnlyObjects
        };

        if (dryRun)
        {
            CountDryRun(initialRoots, ref stats);
            stats.RoomNames = string.Join(
                ", ",
                markers.Select(marker => marker.RoomName).Distinct(StringComparer.Ordinal).OrderBy(name => name));
            stats.RootBreakdown = string.Join(
                "\n",
                initialRoots
                    .OrderBy(root => root.name, StringComparer.OrdinalIgnoreCase)
                    .Select(root => $"{ClassifyObject(root),-12} | {root.name} | pos={FormatVector(root.transform.position)}"));
            return stats;
        }

        HashSet<Transform> protectedTransforms = new HashSet<Transform>();
        protectedTransforms.Add(organizerRoot);
        foreach (RoomBucket bucket in buckets)
        {
            protectedTransforms.Add(bucket.Root);
            protectedTransforms.Add(bucket.Markers);
            protectedTransforms.Add(bucket.Geometry);
            protectedTransforms.Add(bucket.Interactables);
            protectedTransforms.Add(bucket.Lights);
            protectedTransforms.Add(bucket.Ghosts);
        }

        for (int i = 0; i < markers.Count; i++)
        {
            RoomMarker marker = markers[i];
            if (!marker.MoveTransform)
            {
                continue;
            }

            RoomBucket bucket = FindBucketByName(buckets, marker.RoomName) ?? unassignedBucket;
            SafeSetParent(marker.Transform, bucket.Markers);
            stats.MarkersMoved++;
        }

        RecalculateBucketBounds(buckets, markers);

        Dictionary<Transform, ObjectKind> movableCategoryObjects = CollectMovableCategoryObjects(scene, protectedTransforms);
        foreach (KeyValuePair<Transform, ObjectKind> entry in movableCategoryObjects.OrderByDescending(pair => GetDepth(pair.Key)))
        {
            Transform target = entry.Key;
            if (target == null || target.parent == null && target.name == OrganizerRootName)
            {
                continue;
            }

            RoomBucket bucket = IsGlobalObject(target.gameObject, entry.Value)
                ? globalBucket
                : ResolveRoomBucket(target.gameObject, buckets, unassignedBucket);
            Transform destination = ResolveDestination(bucket, entry.Value);
            SafeSetParent(target, destination);
            stats.Add(entry.Value);
            protectedTransforms.Add(target);
        }

        List<GameObject> rootsToOrganize = scene.GetRootGameObjects()
            .Where(root => root != null &&
                           root.name != OrganizerRootName &&
                           !ShouldRemainSceneRoot(root) &&
                           !protectedTransforms.Contains(root.transform))
            .ToList();

        for (int i = 0; i < rootsToOrganize.Count; i++)
        {
            GameObject root = rootsToOrganize[i];
            if (root == null || root.transform.parent != null)
            {
                continue;
            }

            ObjectKind kind = virtualRoomRoots.Contains(root.transform) ? ObjectKind.Other : ClassifyObject(root);
            RoomBucket bucket = IsGlobalObject(root, kind)
                ? globalBucket
                : ResolveRoomBucket(root, buckets, unassignedBucket);
            Transform destination = ResolveDestination(bucket, kind);
            SafeSetParent(root.transform, destination);
            stats.Add(kind);
        }

        SortOrganizerFolders(organizerRoot, zonesRoot, globalRoot, buckets, globalBucket);
        return stats;
    }

    private static List<RoomBucket> CreateRoomBuckets(List<RoomMarker> markers, Transform zonesRoot, RoomBucket unassignedBucket)
    {
        List<RoomBucket> buckets = new List<RoomBucket>();
        foreach (IGrouping<string, RoomMarker> group in markers.GroupBy(marker => marker.RoomName).OrderBy(group => group.Key))
        {
            RoomBucket bucket = new RoomBucket(group.Key, zonesRoot);
            buckets.Add(bucket);
        }

        buckets.Add(unassignedBucket);
        return buckets;
    }

    private static int CountDistinctRoomNames(List<RoomMarker> markers)
    {
        return markers.Select(marker => marker.RoomName).Distinct(StringComparer.Ordinal).Count();
    }

    private static void CountDryRun(List<GameObject> roots, ref OrganizationStats stats)
    {
        for (int i = 0; i < roots.Count; i++)
        {
            GameObject root = roots[i];
            if (root == null)
            {
                continue;
            }

            stats.Add(ClassifyObject(root));
        }
    }

    private static int CountNestedRootOnlySceneObjects(Scene scene)
    {
        return FindNestedRootOnlySceneObjects(scene).Count;
    }

    private static int RepairRootOnlySceneObjects(Scene scene)
    {
        List<Transform> rootOnlyObjects = FindNestedRootOnlySceneObjects(scene);
        for (int i = 0; i < rootOnlyObjects.Count; i++)
        {
            Transform rootOnlyObject = rootOnlyObjects[i];
            if (rootOnlyObject == null || rootOnlyObject.parent == null)
            {
                continue;
            }

            Undo.SetTransformParent(rootOnlyObject, null, "Repair root-only scene object");
            rootOnlyObject.SetParent(null, worldPositionStays: true);
            SceneManager.MoveGameObjectToScene(rootOnlyObject.gameObject, scene);
        }

        return rootOnlyObjects.Count;
    }

    private static List<Transform> FindNestedRootOnlySceneObjects(Scene scene)
    {
        HashSet<Transform> matches = new HashSet<Transform>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            GameObject root = roots[rootIndex];
            if (root == null)
            {
                continue;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                Transform current = transforms[transformIndex];
                if (current == null || current.parent == null)
                {
                    continue;
                }

                if (HasRootOnlyComponent(current.gameObject))
                {
                    matches.Add(current);
                }
            }
        }

        return matches
            .Where(transform => transform != null)
            .OrderBy(transform => GetDepth(transform))
            .ToList();
    }

    private static bool ShouldRemainSceneRoot(GameObject root)
    {
        return root != null && HasRootOnlyComponent(root);
    }

    private static bool HasRootOnlyComponent(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return false;
        }

        MonoBehaviour[] behaviours = gameObject.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (IsRootOnlyComponent(behaviours[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRootOnlyComponent(MonoBehaviour behaviour)
    {
        if (behaviour == null)
        {
            return false;
        }

        Type type = behaviour.GetType();
        return RootOnlyComponentNames.Contains(type.Name) ||
               (!string.IsNullOrEmpty(type.FullName) && RootOnlyComponentNames.Contains(type.FullName));
    }

    private static List<RoomMarker> CollectRoomMarkers(List<GameObject> roots)
    {
        List<RoomMarker> markers = new List<RoomMarker>();
        for (int i = 0; i < roots.Count; i++)
        {
            GameObject root = roots[i];
            if (root == null)
            {
                continue;
            }

            AddMarkers(root.GetComponentsInChildren<Zone>(true), markers);
            AddMarkers(root.GetComponentsInChildren<EnvironmentZone>(true), markers);
            AddMarkers(root.GetComponentsInChildren<TemporalZone>(true), markers);
        }

        AddNamedRoomMarkers(roots, markers);
        AddRootRoomMarkers(roots, markers);
        return markers
            .GroupBy(marker => marker.Transform)
            .Select(group => group.OrderByDescending(marker => marker.Score).First())
            .Where(marker => marker.Transform != null)
            .ToList();
    }

    private static void AddMarkers(Component[] components, List<RoomMarker> markers)
    {
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
            {
                continue;
            }

            Transform markerTransform = component.transform;
            string roomName = SanitizeRoomName(markerTransform.name);
            markers.Add(new RoomMarker(markerTransform, roomName, score: 100, moveTransform: true));
        }
    }

    private static void AddNamedRoomMarkers(List<GameObject> roots, List<RoomMarker> markers)
    {
        string[] roomTokens =
        {
            "Room", "Salle", "Salon", "Cuisine", "Chambre", "Couloir", "Hall",
            "Cave", "Grenier", "HiddenRoom", "Labyrinth", "Library", "Bibliotheque"
        };

        for (int i = 0; i < roots.Count; i++)
        {
            GameObject root = roots[i];
            if (root == null)
            {
                continue;
            }

            if (!roomTokens.Any(token => root.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                continue;
            }

            markers.Add(new RoomMarker(root.transform, SanitizeRoomName(root.name), score: 25, moveTransform: false));
        }
    }

    private static void AddRootRoomMarkers(List<GameObject> roots, List<RoomMarker> markers)
    {
        for (int i = 0; i < roots.Count; i++)
        {
            GameObject root = roots[i];
            if (root == null || IsGlobalObject(root, ClassifyObject(root)) || !TryCalculateBounds(root, out Bounds bounds))
            {
                continue;
            }

            string lower = root.name.ToLowerInvariant();
            bool explicitAreaName = LooksLikeStructuralAreaRoot(lower);
            bool largeArea = root.transform.childCount >= 8 && (bounds.size.x >= 8f || bounds.size.z >= 8f);
            if (!explicitAreaName && !largeArea)
            {
                continue;
            }

            markers.Add(new RoomMarker(root.transform, SanitizeRoomName(root.name), score: 15, moveTransform: false));
        }
    }

    private static bool LooksLikeStructuralAreaRoot(string lowerName)
    {
        if (string.IsNullOrWhiteSpace(lowerName) ||
            lowerName.StartsWith("item_", StringComparison.Ordinal) ||
            lowerName.StartsWith("lit_", StringComparison.Ordinal))
        {
            return false;
        }

        return lowerName.Contains("maison") ||
               lowerName.Contains("castle") ||
               lowerName.Contains("arena") ||
               lowerName.Contains("acte") ||
               lowerName.Contains("labyrinth") ||
               lowerName.Contains("hidden");
    }

    private static void RecalculateBucketBounds(List<RoomBucket> buckets, List<RoomMarker> markers)
    {
        for (int i = 0; i < buckets.Count; i++)
        {
            RoomBucket bucket = buckets[i];
            bool hasBounds = false;
            Bounds mergedBounds = default;
            for (int markerIndex = 0; markerIndex < markers.Count; markerIndex++)
            {
                RoomMarker marker = markers[markerIndex];
                if (!string.Equals(marker.RoomName, bucket.Name, StringComparison.Ordinal) ||
                    marker.Transform == null ||
                    !TryCalculateBounds(marker.Transform.gameObject, out Bounds markerBounds))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    mergedBounds = markerBounds;
                    hasBounds = true;
                }
                else
                {
                    mergedBounds.Encapsulate(markerBounds);
                }
            }

            bucket.HasBounds = hasBounds;
            bucket.Bounds = mergedBounds;
        }
    }

    private static Dictionary<Transform, ObjectKind> CollectMovableCategoryObjects(Scene scene, HashSet<Transform> protectedTransforms)
    {
        Dictionary<Transform, ObjectKind> candidates = new Dictionary<Transform, ObjectKind>();

        AddComponentCandidates(FindSceneComponents<GhostController>(scene), ObjectKind.Ghost, candidates, protectedTransforms);
        AddComponentCandidates(FindSceneComponents<Flame>(scene), ObjectKind.Light, candidates, protectedTransforms);
        AddComponentCandidates(FindSceneComponents<Flame>(scene), ObjectKind.Light, candidates, protectedTransforms);
        AddComponentCandidates(FindSceneComponents<Light>(scene), ObjectKind.Light, candidates, protectedTransforms);
        AddTaggedLightCandidates(scene, candidates, protectedTransforms);

        for (int i = 0; i < InteractableComponentTypes.Length; i++)
        {
            AddComponentCandidates(FindSceneComponents(scene, InteractableComponentTypes[i]), ObjectKind.Interactable, candidates, protectedTransforms);
        }

        RemoveNestedCandidates(candidates);
        return candidates;
    }

    private static List<Component> FindSceneComponents(Scene scene, Type type)
    {
        List<Component> components = new List<Component>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root.name == OrganizerRootName)
            {
                continue;
            }

            components.AddRange(root.GetComponentsInChildren(type, true).Cast<Component>());
        }

        return components;
    }

    private static List<T> FindSceneComponents<T>(Scene scene) where T : Component
    {
        List<T> components = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root.name == OrganizerRootName)
            {
                continue;
            }

            components.AddRange(root.GetComponentsInChildren<T>(true));
        }

        return components;
    }

    private static void AddComponentCandidates<T>(
        List<T> components,
        ObjectKind kind,
        Dictionary<Transform, ObjectKind> candidates,
        HashSet<Transform> protectedTransforms) where T : Component
    {
        for (int i = 0; i < components.Count; i++)
        {
            AddCandidate(components[i] != null ? components[i].transform : null, kind, candidates, protectedTransforms);
        }
    }

    private static void AddComponentCandidates(
        List<Component> components,
        ObjectKind kind,
        Dictionary<Transform, ObjectKind> candidates,
        HashSet<Transform> protectedTransforms)
    {
        for (int i = 0; i < components.Count; i++)
        {
            AddCandidate(components[i] != null ? components[i].transform : null, kind, candidates, protectedTransforms);
        }
    }

    private static void AddTaggedLightCandidates(
        Scene scene,
        Dictionary<Transform, ObjectKind> candidates,
        HashSet<Transform> protectedTransforms)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root.name == OrganizerRootName)
            {
                continue;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                Transform current = transforms[transformIndex];
                if (current == null || !HasTag(current.gameObject, "Light"))
                {
                    continue;
                }

                AddCandidate(current, ObjectKind.Light, candidates, protectedTransforms);
            }
        }
    }

    private static void AddCandidate(
        Transform source,
        ObjectKind kind,
        Dictionary<Transform, ObjectKind> candidates,
        HashSet<Transform> protectedTransforms)
    {
        if (source == null || IsUnderOrganizer(source))
        {
            return;
        }

        Transform target = ResolveMovableRoot(source, kind);
        if (target == null || protectedTransforms.Contains(target))
        {
            return;
        }

        if (target.parent != null)
        {
            return;
        }

        if (IsUnderOrganizer(target))
        {
            return;
        }

        if (candidates.TryGetValue(target, out ObjectKind existingKind))
        {
            candidates[target] = HigherPriority(existingKind, kind);
            return;
        }

        candidates.Add(target, kind);
    }

    private static Transform ResolveMovableRoot(Transform source, ObjectKind kind)
    {
        if (source == null)
        {
            return null;
        }

        if (kind != ObjectKind.Light)
        {
            return source;
        }

        Transform current = source;
        Transform best = null;
        while (current != null)
        {
            if (LooksLikeLight(current.gameObject) ||
                current.GetComponent<Flame>() != null ||
                current.GetComponent<Flame>() != null)
            {
                best = current;
            }

            if (current.parent == null)
            {
                break;
            }

            current = current.parent;
        }

        if (best != null && !IsSceneWideContainer(best))
        {
            return best;
        }

        return source;
    }

    private static bool IsSceneWideContainer(Transform transform)
    {
        if (transform == null)
        {
            return false;
        }

        string lower = transform.name.ToLowerInvariant();
        return lower.Contains("world") ||
               lower.Contains("legacy") ||
               lower.Contains("scene") ||
               lower.Contains("zone") ||
               lower.Contains("rooms");
    }

    private static bool IsUnderOrganizer(Transform transform)
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name == OrganizerRootName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static int GetDepth(Transform transform)
    {
        int depth = 0;
        Transform current = transform;
        while (current != null)
        {
            depth++;
            current = current.parent;
        }

        return depth;
    }

    private static void RemoveNestedCandidates(Dictionary<Transform, ObjectKind> candidates)
    {
        List<Transform> keys = candidates.Keys.ToList();
        HashSet<Transform> toRemove = new HashSet<Transform>();
        for (int i = 0; i < keys.Count; i++)
        {
            Transform candidate = keys[i];
            if (candidate == null)
            {
                toRemove.Add(candidate);
                continue;
            }

            Transform parent = candidate.parent;
            while (parent != null)
            {
                if (candidates.ContainsKey(parent))
                {
                    candidates[parent] = HigherPriority(candidates[parent], candidates[candidate]);
                    toRemove.Add(candidate);
                    break;
                }

                parent = parent.parent;
            }
        }

        foreach (Transform remove in toRemove)
        {
            candidates.Remove(remove);
        }
    }

    private static ObjectKind HigherPriority(ObjectKind a, ObjectKind b)
    {
        return Priority(b) > Priority(a) ? b : a;
    }

    private static int Priority(ObjectKind kind)
    {
        switch (kind)
        {
            case ObjectKind.Ghost:
                return 3;
            case ObjectKind.Light:
                return 2;
            case ObjectKind.Interactable:
                return 1;
            default:
                return 0;
        }
    }

    private static RoomBucket ResolveRoomBucket(GameObject root, List<RoomBucket> buckets, RoomBucket fallback)
    {
        Vector3 center = ResolveObjectCenter(root);
        RoomBucket containing = null;
        float containingVolume = float.PositiveInfinity;
        for (int i = 0; i < buckets.Count; i++)
        {
            RoomBucket bucket = buckets[i];
            if (bucket == fallback || !bucket.HasBounds || !bucket.Bounds.Contains(center))
            {
                continue;
            }

            float volume = Mathf.Max(0.001f, bucket.Bounds.size.x * bucket.Bounds.size.y * bucket.Bounds.size.z);
            if (volume < containingVolume)
            {
                containing = bucket;
                containingVolume = volume;
            }
        }

        if (containing != null)
        {
            return containing;
        }

        RoomBucket nearest = null;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < buckets.Count; i++)
        {
            RoomBucket bucket = buckets[i];
            if (bucket == fallback || !bucket.HasBounds)
            {
                continue;
            }

            float distance = (bucket.Center - center).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = bucket;
                nearestDistance = distance;
            }
        }

        return nearest ?? fallback;
    }

    private static Transform ResolveDestination(RoomBucket bucket, ObjectKind kind)
    {
        switch (kind)
        {
            case ObjectKind.Ghost:
                return bucket.Ghosts;
            case ObjectKind.Light:
                return bucket.Lights;
            case ObjectKind.Interactable:
                return bucket.Interactables;
            default:
                return bucket.Geometry;
        }
    }

    private static bool IsGlobalObject(GameObject root, ObjectKind kind)
    {
        if (root == null)
        {
            return true;
        }

        string lower = root.name.ToLowerInvariant();
        if (lower.Contains("manager") ||
            lower.Contains("camera") ||
            lower.Contains("canvas") ||
            lower.Contains("ui") ||
            lower.Contains("eventsystem") ||
            lower.Contains("post") ||
            lower.Contains("volume") ||
            lower.Contains("navmesh") ||
            lower.Contains("audio") ||
            lower.Contains("lighting") ||
            lower.Contains("sky") ||
            lower.Contains("directional light"))
        {
            return true;
        }

        return kind == ObjectKind.Other && root.GetComponentInChildren<Transform>(true).childCount == 0 && !TryCalculateBounds(root, out _);
    }

    private static ObjectKind ClassifyObject(GameObject root)
    {
        if (root == null)
        {
            return ObjectKind.Other;
        }

        if (root.GetComponentInChildren<GhostController>(true) != null)
        {
            return ObjectKind.Ghost;
        }

        if (HasLightContent(root))
        {
            return ObjectKind.Light;
        }

        if (HasInteractableContent(root))
        {
            return ObjectKind.Interactable;
        }

        return ObjectKind.Other;
    }

    private static bool HasLightContent(GameObject root)
    {
        return root.GetComponentInChildren<Light>(true) != null ||
               root.GetComponentsInChildren<ParticleSystem>(true).Any(system => system != null && LooksLikeLight(system.gameObject)) ||
               root.GetComponentsInChildren<Transform>(true).Any(transform => transform != null && LooksLikeLight(transform.gameObject));
    }

    private static bool LooksLikeLight(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return false;
        }

        if (HasTag(gameObject, "Light"))
        {
            return true;
        }

        string lower = gameObject.name.ToLowerInvariant();
        return lower.Contains("light") ||
               lower.Contains("flame") ||
               lower.Contains("flame") ||
               lower.Contains("flame") ||
               lower.Contains("fire") ||
               lower.Contains("flame") ||
               lower.Contains("candle") ||
               lower.Contains("chandelier");
    }

    private static bool HasTag(GameObject gameObject, string tagName)
    {
        if (gameObject == null || string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        try
        {
            return gameObject.CompareTag(tagName);
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private static bool HasInteractableContent(GameObject root)
    {
        for (int i = 0; i < InteractableComponentTypes.Length; i++)
        {
            Type type = InteractableComponentTypes[i];
            if (root.GetComponentInChildren(type, true) != null)
            {
                return true;
            }
        }

        return root.GetComponentsInChildren<MonoBehaviour>(true)
            .Any(component => component is ICharacterDetectedInteractable);
    }

    private static Vector3 ResolveObjectCenter(GameObject root)
    {
        return TryCalculateBounds(root, out Bounds bounds) ? bounds.center : root.transform.position;
    }

    private static bool TryCalculateBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
        {
            return false;
        }

        bool hasBounds = false;
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

        return hasBounds;
    }

    private static Transform EnsureRoot(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root != null && root.name == name)
            {
                return root.transform;
            }
        }

        GameObject created = new GameObject(name);
        SceneManager.MoveGameObjectToScene(created, scene);
        Undo.RegisterCreatedObjectUndo(created, "Create scene organization root");
        return created.transform;
    }

    private static Transform EnsureFolder(Transform parent, string name)
    {
        Transform existing = parent != null ? parent.Find(name) : null;
        if (existing != null)
        {
            return existing;
        }

        GameObject created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, "Create hierarchy folder");
        if (parent != null)
        {
            created.transform.SetParent(parent, worldPositionStays: false);
        }

        return created.transform;
    }

    private static void SafeSetParent(Transform child, Transform parent)
    {
        if (child == null || parent == null || child == parent || child.IsChildOf(parent))
        {
            return;
        }

        Undo.SetTransformParent(child, parent, "Organize scene hierarchy");
        child.SetParent(parent, worldPositionStays: true);
    }

    private static RoomBucket FindBucketByName(List<RoomBucket> buckets, string roomName)
    {
        for (int i = 0; i < buckets.Count; i++)
        {
            if (string.Equals(buckets[i].Name, roomName, StringComparison.Ordinal))
            {
                return buckets[i];
            }
        }

        return null;
    }

    private static void SortOrganizerFolders(
        Transform organizerRoot,
        Transform zonesRoot,
        Transform globalRoot,
        List<RoomBucket> buckets,
        RoomBucket globalBucket)
    {
        SortDirectChildren(organizerRoot);
        SortDirectChildren(zonesRoot);
        SortDirectChildren(globalRoot);

        for (int i = 0; i < buckets.Count; i++)
        {
            SortBucket(buckets[i]);
        }

        SortBucket(globalBucket);
    }

    private static void SortBucket(RoomBucket bucket)
    {
        if (bucket == null)
        {
            return;
        }

        SortDirectChildren(bucket.Root);
        SortDirectChildren(bucket.Markers);
        SortDirectChildren(bucket.Geometry);
        SortDirectChildren(bucket.Interactables);
        SortDirectChildren(bucket.Lights);
        SortDirectChildren(bucket.Ghosts);
    }

    private static void SortDirectChildren(Transform root)
    {
        if (root == null)
        {
            return;
        }

        List<Transform> children = new List<Transform>();
        for (int i = 0; i < root.childCount; i++)
        {
            children.Add(root.GetChild(i));
        }

        children.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        for (int i = 0; i < children.Count; i++)
        {
            children[i].SetSiblingIndex(i);
        }
    }

    private static string SanitizeRoomName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return "Room";
        }

        string sanitized = rawName.Trim()
            .Replace('/', '_')
            .Replace('\\', '_')
            .Replace(':', '_');

        return sanitized.Length > 64 ? sanitized.Substring(0, 64) : sanitized;
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:0.##}, {value.y:0.##}, {value.z:0.##})";
    }

    private static HierarchyValidation ValidateOrganizedScene(Scene scene)
    {
        HierarchyValidation validation = new HierarchyValidation();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            validation.Errors.Add("Scene not loaded.");
            return validation;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        Transform organizerRoot = null;
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
            {
                continue;
            }

            if (root.name == OrganizerRootName)
            {
                organizerRoot = root.transform;
                validation.OrganizationRootCount++;
            }
            else
            {
                validation.LooseRootNames.Add(root.name);
            }
        }

        if (organizerRoot == null)
        {
            validation.Errors.Add($"{OrganizerRootName} is missing.");
            return validation;
        }

        Transform zonesRoot = organizerRoot.Find(ZonesRootName);
        Transform globalRoot = organizerRoot.Find(GlobalRootName);
        if (zonesRoot == null)
        {
            validation.Errors.Add($"{ZonesRootName} is missing.");
        }

        if (globalRoot == null)
        {
            validation.Errors.Add($"{GlobalRootName} is missing.");
        }

        if (zonesRoot != null)
        {
            for (int i = 0; i < zonesRoot.childCount; i++)
            {
                ValidateBucket(zonesRoot.GetChild(i), validation);
            }
        }

        if (globalRoot != null)
        {
            for (int i = 0; i < globalRoot.childCount; i++)
            {
                ValidateBucket(globalRoot.GetChild(i), validation);
            }
        }

        ValidateCategorizedContent(scene, validation);
        return validation;
    }

    private static void ValidateBucket(Transform bucketRoot, HierarchyValidation validation)
    {
        if (bucketRoot == null)
        {
            return;
        }

        validation.BucketCount++;
        validation.BucketNames.Add(bucketRoot.name);

        CountBucketFolder(bucketRoot, "00_Markers", ref validation.MarkerFolderObjectCount, validation);
        CountBucketFolder(bucketRoot, "01_Geometry_Decor", ref validation.GeometryFolderObjectCount, validation);
        CountBucketFolder(bucketRoot, "02_Interactables", ref validation.InteractableFolderObjectCount, validation);
        CountBucketFolder(bucketRoot, "03_Lights", ref validation.LightFolderObjectCount, validation);
        CountBucketFolder(bucketRoot, "04_Ghosts", ref validation.GhostFolderObjectCount, validation);
    }

    private static void CountBucketFolder(
        Transform bucketRoot,
        string folderName,
        ref int objectCount,
        HierarchyValidation validation)
    {
        Transform folder = bucketRoot.Find(folderName);
        if (folder == null)
        {
            validation.Errors.Add($"{bucketRoot.name}/{folderName} is missing.");
            return;
        }

        objectCount += folder.childCount;
    }

    private static void ValidateCategorizedContent(Scene scene, HierarchyValidation validation)
    {
        HashSet<Transform> validatedGhosts = new HashSet<Transform>();
        HashSet<Transform> validatedLights = new HashSet<Transform>();
        HashSet<Transform> validatedInteractables = new HashSet<Transform>();

        ValidateComponentPlacement(
            FindSceneComponentsIncludingOrganizer<GhostController>(scene),
            "04_Ghosts",
            "ghost",
            validation,
            validatedGhosts);

        ValidateComponentPlacement(
            FindSceneComponentsIncludingOrganizer<Flame>(scene),
            "03_Lights",
            "light",
            validation,
            validatedLights);

        ValidateComponentPlacement(
            FindSceneComponentsIncludingOrganizer<Flame>(scene),
            "03_Lights",
            "light",
            validation,
            validatedLights);

        ValidateComponentPlacement(
            FindSceneComponentsIncludingOrganizer<Light>(scene),
            "03_Lights",
            "light",
            validation,
            validatedLights);

        Transform[] transforms = FindSceneTransformsIncludingOrganizer(scene);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (transform != null && HasTag(transform.gameObject, "Light"))
            {
                ValidateTransformPlacement(transform, "03_Lights", "light tag", validation, validatedLights);
            }
        }

        for (int i = 0; i < InteractableComponentTypes.Length; i++)
        {
            Type type = InteractableComponentTypes[i];
            if (type == typeof(Flame) || type == typeof(Flame))
            {
                continue;
            }

            ValidateComponentPlacement(
                FindSceneComponentsIncludingOrganizer(scene, type),
                new[] { "02_Interactables", "03_Lights", "04_Ghosts" },
                "interactable",
                validation,
                validatedInteractables);
        }

        List<MonoBehaviour> behaviours = FindSceneComponentsIncludingOrganizer<MonoBehaviour>(scene);
        for (int i = 0; i < behaviours.Count; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour is ICharacterDetectedInteractable)
            {
                ValidateTransformPlacement(
                    behaviour.transform,
                    new[] { "02_Interactables", "03_Lights", "04_Ghosts" },
                    "interactable",
                    validation,
                    validatedInteractables);
            }
        }
    }

    private static void ValidateComponentPlacement<T>(
        List<T> components,
        string expectedFolder,
        string category,
        HierarchyValidation validation,
        HashSet<Transform> validatedTransforms) where T : Component
    {
        for (int i = 0; i < components.Count; i++)
        {
            T component = components[i];
            if (component != null)
            {
                ValidateTransformPlacement(component.transform, expectedFolder, category, validation, validatedTransforms);
            }
        }
    }

    private static void ValidateComponentPlacement(
        List<Component> components,
        string expectedFolder,
        string category,
        HierarchyValidation validation,
        HashSet<Transform> validatedTransforms)
    {
        for (int i = 0; i < components.Count; i++)
        {
            Component component = components[i];
            if (component != null)
            {
                ValidateTransformPlacement(component.transform, expectedFolder, category, validation, validatedTransforms);
            }
        }
    }

    private static void ValidateComponentPlacement(
        List<Component> components,
        string[] expectedFolders,
        string category,
        HierarchyValidation validation,
        HashSet<Transform> validatedTransforms)
    {
        for (int i = 0; i < components.Count; i++)
        {
            Component component = components[i];
            if (component != null)
            {
                ValidateTransformPlacement(component.transform, expectedFolders, category, validation, validatedTransforms);
            }
        }
    }

    private static void ValidateTransformPlacement(
        Transform transform,
        string expectedFolder,
        string category,
        HierarchyValidation validation,
        HashSet<Transform> validatedTransforms)
    {
        ValidateTransformPlacement(transform, new[] { expectedFolder }, category, validation, validatedTransforms);
    }

    private static void ValidateTransformPlacement(
        Transform transform,
        string[] expectedFolders,
        string category,
        HierarchyValidation validation,
        HashSet<Transform> validatedTransforms)
    {
        if (transform == null || !validatedTransforms.Add(transform))
        {
            return;
        }

        validation.PlacementChecks++;
        for (int i = 0; i < expectedFolders.Length; i++)
        {
            if (HasAncestorNamed(transform, expectedFolders[i]))
            {
                return;
            }
        }

        string expectedFolderList = string.Join(" or ", expectedFolders);
        if (validation.Errors.Count < 25)
        {
            validation.Errors.Add($"{category} '{GetHierarchyPath(transform)}' is not under {expectedFolderList}.");
        }

        validation.PlacementIssueCount++;
    }

    private static bool HasAncestorNamed(Transform transform, string name)
    {
        Transform current = transform;
        while (current != null)
        {
            if (string.Equals(current.name, name, StringComparison.Ordinal))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return "<null>";
        }

        List<string> names = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private static List<Component> FindSceneComponentsIncludingOrganizer(Scene scene, Type type)
    {
        List<Component> components = new List<Component>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root != null)
            {
                components.AddRange(root.GetComponentsInChildren(type, true).Cast<Component>());
            }
        }

        return components;
    }

    private static List<T> FindSceneComponentsIncludingOrganizer<T>(Scene scene) where T : Component
    {
        List<T> components = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root != null)
            {
                components.AddRange(root.GetComponentsInChildren<T>(true));
            }
        }

        return components;
    }

    private static Transform[] FindSceneTransformsIncludingOrganizer(Scene scene)
    {
        List<Transform> transforms = new List<Transform>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root != null)
            {
                transforms.AddRange(root.GetComponentsInChildren<Transform>(true));
            }
        }

        return transforms.ToArray();
    }

    private enum ObjectKind
    {
        Other,
        Interactable,
        Light,
        Ghost
    }

    private readonly struct RoomMarker
    {
        public readonly Transform Transform;
        public readonly string RoomName;
        public readonly int Score;
        public readonly bool MoveTransform;

        public RoomMarker(Transform transform, string roomName, int score, bool moveTransform)
        {
            Transform = transform;
            RoomName = roomName;
            Score = score;
            MoveTransform = moveTransform;
        }
    }

    private struct OrganizationStats
    {
        public int RootCount;
        public int RoomMarkerCount;
        public int RoomCount;
        public int GeometryCount;
        public int InteractableCount;
        public int LightCount;
        public int GhostCount;
        public int MarkersMoved;
        public int RepairedRootOnlyObjects;
        public string RoomNames;
        public string RootBreakdown;

        public void Add(ObjectKind kind)
        {
            switch (kind)
            {
                case ObjectKind.Ghost:
                    GhostCount++;
                    break;
                case ObjectKind.Light:
                    LightCount++;
                    break;
                case ObjectKind.Interactable:
                    InteractableCount++;
                    break;
                default:
                    GeometryCount++;
                    break;
            }
        }

        public string Format(string title)
        {
            string summary = $"{title}: roots={RootCount}, roomMarkers={RoomMarkerCount}, rooms={RoomCount}, " +
                             $"geometry={GeometryCount}, interactables={InteractableCount}, lights={LightCount}, " +
                             $"ghosts={GhostCount}, markersMoved={MarkersMoved}, " +
                             $"repairedRootOnlyObjects={RepairedRootOnlyObjects}";

            if (!string.IsNullOrWhiteSpace(RoomNames))
            {
                summary += $"\nRooms: {RoomNames}";
            }

            if (!string.IsNullOrWhiteSpace(RootBreakdown))
            {
                summary += $"\nRoot breakdown:\n{RootBreakdown}";
            }

            return summary;
        }
    }

    private sealed class HierarchyValidation
    {
        public int OrganizationRootCount;
        public int BucketCount;
        public int MarkerFolderObjectCount;
        public int GeometryFolderObjectCount;
        public int InteractableFolderObjectCount;
        public int LightFolderObjectCount;
        public int GhostFolderObjectCount;
        public int PlacementChecks;
        public int PlacementIssueCount;
        public readonly List<string> BucketNames = new List<string>();
        public readonly List<string> LooseRootNames = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public string Format()
        {
            string summary = "Maison hierarchy validation: " +
                             $"organizationRoots={OrganizationRootCount}, buckets={BucketCount}, " +
                             $"markers={MarkerFolderObjectCount}, geometry={GeometryFolderObjectCount}, " +
                             $"interactables={InteractableFolderObjectCount}, lights={LightFolderObjectCount}, " +
                             $"ghosts={GhostFolderObjectCount}, placementChecks={PlacementChecks}, " +
                             $"placementIssues={PlacementIssueCount}, looseRoots={LooseRootNames.Count}, errors={Errors.Count}";

            if (BucketNames.Count > 0)
            {
                summary += "\nBuckets: " + string.Join(", ", BucketNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            }

            if (LooseRootNames.Count > 0)
            {
                summary += "\nLoose roots: " + string.Join(", ", LooseRootNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            }

            if (Errors.Count > 0)
            {
                summary += "\nErrors:\n" + string.Join("\n", Errors);
            }

            return summary;
        }
    }
}
