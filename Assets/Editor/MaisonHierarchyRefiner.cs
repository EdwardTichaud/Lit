using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class MaisonHierarchyRefiner
{
    private const string MaisonScenePath = "Assets/Scenes/Maison.unity";
    private const string OrganizerRootName = "00_SCENE_ORGANIZATION";
    private const string ZonesRootName = "01_Zones_Rooms";
    private const string GlobalRootName = "02_Global";

    private static readonly string[] BucketFolders =
    {
        "00_Markers",
        "01_Geometry_Decor",
        "02_Interactables",
        "03_Lights",
        "04_Ghosts"
    };

    [MenuItem("Lit/Scenes/Refine Maison Hierarchy")]
    public static void RefineMaisonHierarchy()
    {
        Scene scene = OpenMaisonScene();
        Transform organizerRoot = FindSceneRoot(scene, OrganizerRootName);
        if (organizerRoot == null)
        {
            SceneHierarchyOrganizer.OrganizeMaisonHierarchy();
            scene = OpenMaisonScene();
            organizerRoot = FindSceneRoot(scene, OrganizerRootName);
        }

        if (organizerRoot == null)
        {
            throw new InvalidOperationException("Maison hierarchy organizer root is missing.");
        }

        Transform zonesRoot = EnsureFolder(organizerRoot, ZonesRootName);
        EnsureFolder(organizerRoot, GlobalRootName);

        RefineStats stats = new RefineStats();
        List<RoomMarkerInfo> markers = CollectRoomMarkers(zonesRoot, stats);
        Dictionary<string, Transform> buckets = EnsureBuckets(zonesRoot, markers);
        MoveMarkersToNamedBuckets(zonesRoot, markers, buckets, stats);
        RecalculateMarkerBounds(markers);
        RebucketZoneContent(zonesRoot, markers, buckets, stats);
        stats.RemovedEmptyBuckets = RemoveEmptyGenericBuckets(zonesRoot);
        SortHierarchy(organizerRoot);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(stats.Format());
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

    private static Transform FindSceneRoot(Scene scene, string rootName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].name == rootName)
            {
                return roots[i].transform;
            }
        }

        return null;
    }

    private static List<RoomMarkerInfo> CollectRoomMarkers(Transform zonesRoot, RefineStats stats)
    {
        Dictionary<Transform, RoomMarkerInfo> markersByTransform = new Dictionary<Transform, RoomMarkerInfo>();
        List<Transform> buckets = GetDirectChildren(zonesRoot);
        for (int i = 0; i < buckets.Count; i++)
        {
            Transform bucket = buckets[i];
            if (bucket == null)
            {
                continue;
            }

            Transform markerFolder = bucket.Find("00_Markers");
            bool hasExplicitMarkers = markerFolder != null && markerFolder.childCount > 0;
            if (!hasExplicitMarkers && !IsGenericZoneName(bucket.name) && HasBucketContent(bucket))
            {
                AddOrUpdateMarker(markersByTransform, bucket, bucket.name, moveMarker: false);
            }

            if (markerFolder == null)
            {
                continue;
            }

            List<Transform> markerTransforms = GetDirectChildren(markerFolder);
            for (int markerIndex = 0; markerIndex < markerTransforms.Count; markerIndex++)
            {
                Transform marker = markerTransforms[markerIndex];
                string resolvedName = ResolveMarkerName(marker, bucket.name);
                if (!string.Equals(marker.name, resolvedName, StringComparison.Ordinal))
                {
                    marker.name = resolvedName;
                    stats.RenamedMarkers++;
                }

                AddOrUpdateMarker(markersByTransform, marker, resolvedName, moveMarker: true);
            }
        }

        return markersByTransform.Values
            .Where(marker => !string.IsNullOrWhiteSpace(marker.RoomName))
            .OrderBy(marker => marker.RoomName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddOrUpdateMarker(
        Dictionary<Transform, RoomMarkerInfo> markersByTransform,
        Transform transform,
        string roomName,
        bool moveMarker)
    {
        if (transform == null)
        {
            return;
        }

        string sanitizedName = SanitizeRoomName(roomName);
        if (markersByTransform.TryGetValue(transform, out RoomMarkerInfo existing))
        {
            if (moveMarker)
            {
                existing.MoveMarker = true;
            }

            if (IsGenericZoneName(existing.RoomName) && !IsGenericZoneName(sanitizedName))
            {
                existing.RoomName = sanitizedName;
            }

            return;
        }

        markersByTransform.Add(transform, new RoomMarkerInfo(transform, sanitizedName, moveMarker));
    }

    private static string ResolveMarkerName(Transform marker, string bucketName)
    {
        if (marker == null)
        {
            return "Zone";
        }

        if (!IsGenericZoneName(marker.name))
        {
            return SanitizeRoomName(marker.name);
        }

        Zone gameplayZone = marker.GetComponent<Zone>();
        if (gameplayZone != null)
        {
            if (gameplayZone.isMaison)
            {
                return "Maison_Zone";
            }

            if (gameplayZone.zoneAudioProfile != null)
            {
                return SanitizeRoomName(CleanAssetLabel(gameplayZone.zoneAudioProfile.name, "ZoneAudioProfile_", "Audio Zone"));
            }

            if (gameplayZone.zoneMusic != null)
            {
                return SanitizeRoomName(CleanAssetLabel(gameplayZone.zoneMusic.name, "Music_", null));
            }
        }

        EnvironmentZone environmentZone = marker.GetComponent<EnvironmentZone>();
        if (environmentZone != null && environmentZone.Profile != null)
        {
            return SanitizeRoomName(CleanAssetLabel(environmentZone.Profile.name, "BiomeZone_", null));
        }

        Volume volume = marker.GetComponent<Volume>();
        if (volume != null && volume.sharedProfile != null)
        {
            return SanitizeRoomName(CleanAssetLabel(volume.sharedProfile.name, "BiomeZone_", "Volume"));
        }

        if (!IsGenericZoneName(bucketName))
        {
            return SanitizeRoomName(bucketName);
        }

        Vector3 position = marker.position;
        return SanitizeRoomName($"Zone_X{Mathf.RoundToInt(position.x)}_Z{Mathf.RoundToInt(position.z)}");
    }

    private static string CleanAssetLabel(string assetName, string prefix, string suffix)
    {
        string cleaned = string.IsNullOrWhiteSpace(assetName) ? "Zone" : assetName.Trim();
        if (!string.IsNullOrEmpty(prefix) && cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned.Substring(prefix.Length);
        }

        cleaned = cleaned.Replace('_', ' ');
        cleaned = StripDiacritics(cleaned);
        if (!string.IsNullOrWhiteSpace(suffix) &&
            cleaned.IndexOf(suffix, StringComparison.OrdinalIgnoreCase) < 0)
        {
            cleaned = $"{cleaned} {suffix}";
        }

        return cleaned;
    }

    private static string StripDiacritics(string value)
    {
        string normalized = value.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(normalized.Length);
        for (int i = 0; i < normalized.Length; i++)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(normalized[i]);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(normalized[i]);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static Dictionary<string, Transform> EnsureBuckets(Transform zonesRoot, List<RoomMarkerInfo> markers)
    {
        Dictionary<string, Transform> buckets = new Dictionary<string, Transform>(StringComparer.Ordinal);
        for (int i = 0; i < markers.Count; i++)
        {
            RoomMarkerInfo marker = markers[i];
            if (string.IsNullOrWhiteSpace(marker.RoomName))
            {
                continue;
            }

            Transform bucket = EnsureBucket(zonesRoot, marker.RoomName);
            buckets[marker.RoomName] = bucket;
        }

        return buckets;
    }

    private static Transform EnsureBucket(Transform zonesRoot, string bucketName)
    {
        Transform bucket = EnsureFolder(zonesRoot, bucketName);
        for (int i = 0; i < BucketFolders.Length; i++)
        {
            EnsureFolder(bucket, BucketFolders[i]);
        }

        return bucket;
    }

    private static void MoveMarkersToNamedBuckets(
        Transform zonesRoot,
        List<RoomMarkerInfo> markers,
        Dictionary<string, Transform> buckets,
        RefineStats stats)
    {
        for (int i = 0; i < markers.Count; i++)
        {
            RoomMarkerInfo marker = markers[i];
            if (!marker.MoveMarker || marker.Transform == null)
            {
                continue;
            }

            Transform bucket = ResolveBucket(zonesRoot, marker.RoomName, buckets);
            Transform markerFolder = EnsureFolder(bucket, "00_Markers");
            if (marker.Transform.parent != markerFolder)
            {
                marker.Transform.SetParent(markerFolder, worldPositionStays: true);
                stats.MovedMarkers++;
            }
        }
    }

    private static void RecalculateMarkerBounds(List<RoomMarkerInfo> markers)
    {
        for (int i = 0; i < markers.Count; i++)
        {
            RoomMarkerInfo marker = markers[i];
            marker.HasBounds = TryCalculateBounds(marker.Transform.gameObject, out Bounds bounds);
            marker.Bounds = bounds;
        }
    }

    private static void RebucketZoneContent(
        Transform zonesRoot,
        List<RoomMarkerInfo> markers,
        Dictionary<string, Transform> buckets,
        RefineStats stats)
    {
        if (markers.Count == 0)
        {
            return;
        }

        List<Transform> bucketRoots = GetDirectChildren(zonesRoot);
        for (int bucketIndex = 0; bucketIndex < bucketRoots.Count; bucketIndex++)
        {
            Transform bucketRoot = bucketRoots[bucketIndex];
            if (bucketRoot == null)
            {
                continue;
            }

            for (int folderIndex = 1; folderIndex < BucketFolders.Length; folderIndex++)
            {
                string folderName = BucketFolders[folderIndex];
                Transform folder = bucketRoot.Find(folderName);
                if (folder == null)
                {
                    continue;
                }

                List<Transform> children = GetDirectChildren(folder);
                for (int childIndex = 0; childIndex < children.Count; childIndex++)
                {
                    Transform child = children[childIndex];
                    if (child == null)
                    {
                        continue;
                    }

                    RoomMarkerInfo targetMarker = ResolveBestMarker(child.gameObject, markers);
                    if (targetMarker == null)
                    {
                        continue;
                    }

                    Transform targetBucket = ResolveBucket(zonesRoot, targetMarker.RoomName, buckets);
                    Transform targetFolder = EnsureFolder(targetBucket, folderName);
                    if (child.parent == targetFolder)
                    {
                        continue;
                    }

                    child.SetParent(targetFolder, worldPositionStays: true);
                    stats.MovedContentRoots++;
                }
            }
        }
    }

    private static RoomMarkerInfo ResolveBestMarker(GameObject root, List<RoomMarkerInfo> markers)
    {
        Vector3 center = TryCalculateBounds(root, out Bounds bounds) ? bounds.center : root.transform.position;
        RoomMarkerInfo containing = null;
        float containingVolume = float.PositiveInfinity;
        for (int i = 0; i < markers.Count; i++)
        {
            RoomMarkerInfo marker = markers[i];
            if (!marker.HasBounds || !marker.Bounds.Contains(center))
            {
                continue;
            }

            float volume = Mathf.Max(0.001f, marker.Bounds.size.x * marker.Bounds.size.y * marker.Bounds.size.z);
            if (volume < containingVolume)
            {
                containing = marker;
                containingVolume = volume;
            }
        }

        if (containing != null)
        {
            return containing;
        }

        RoomMarkerInfo nearest = null;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < markers.Count; i++)
        {
            RoomMarkerInfo marker = markers[i];
            if (!marker.HasBounds)
            {
                continue;
            }

            float distance = (marker.Bounds.center - center).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = marker;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private static Transform ResolveBucket(
        Transform zonesRoot,
        string bucketName,
        Dictionary<string, Transform> buckets)
    {
        if (buckets.TryGetValue(bucketName, out Transform bucket) && bucket != null)
        {
            return bucket;
        }

        bucket = EnsureBucket(zonesRoot, bucketName);
        buckets[bucketName] = bucket;
        return bucket;
    }

    private static int RemoveEmptyGenericBuckets(Transform zonesRoot)
    {
        int removed = 0;
        List<Transform> buckets = GetDirectChildren(zonesRoot);
        for (int i = 0; i < buckets.Count; i++)
        {
            Transform bucket = buckets[i];
            if (bucket == null || !IsGenericZoneName(bucket.name))
            {
                continue;
            }

            if (!HasBucketContent(bucket) && IsMarkerFolderEmpty(bucket))
            {
                UnityEngine.Object.DestroyImmediate(bucket.gameObject);
                removed++;
            }
        }

        return removed;
    }

    private static bool HasBucketContent(Transform bucket)
    {
        for (int i = 1; i < BucketFolders.Length; i++)
        {
            Transform folder = bucket.Find(BucketFolders[i]);
            if (folder != null && folder.childCount > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMarkerFolderEmpty(Transform bucket)
    {
        Transform markerFolder = bucket.Find("00_Markers");
        return markerFolder == null || markerFolder.childCount == 0;
    }

    private static bool IsGenericZoneName(string name)
    {
        return string.Equals(name, "Zone", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Room", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "EnvironmentZone", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "TemporalZone", StringComparison.OrdinalIgnoreCase);
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
            if (collider == null || !TryCalculateColliderBounds(collider, out Bounds colliderBounds))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = colliderBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(colliderBounds);
            }
        }

        return hasBounds;
    }

    private static bool TryCalculateColliderBounds(Collider collider, out Bounds bounds)
    {
        bounds = default;
        if (collider == null)
        {
            return false;
        }

        if (collider is BoxCollider boxCollider)
        {
            Vector3 halfSize = boxCollider.size * 0.5f;
            Vector3[] corners =
            {
                boxCollider.center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
                boxCollider.center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z),
                boxCollider.center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z),
                boxCollider.center + new Vector3(-halfSize.x, halfSize.y, halfSize.z),
                boxCollider.center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z),
                boxCollider.center + new Vector3(halfSize.x, -halfSize.y, halfSize.z),
                boxCollider.center + new Vector3(halfSize.x, halfSize.y, -halfSize.z),
                boxCollider.center + new Vector3(halfSize.x, halfSize.y, halfSize.z)
            };

            bounds = new Bounds(boxCollider.transform.TransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++)
            {
                bounds.Encapsulate(boxCollider.transform.TransformPoint(corners[i]));
            }

            return true;
        }

        if (collider is SphereCollider sphereCollider)
        {
            Vector3 scale = sphereCollider.transform.lossyScale;
            float radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            float radius = sphereCollider.radius * Mathf.Max(0.0001f, radiusScale);
            bounds = new Bounds(sphereCollider.transform.TransformPoint(sphereCollider.center), Vector3.one * radius * 2f);
            return true;
        }

        if (!collider.enabled)
        {
            return false;
        }

        bounds = collider.bounds;
        return true;
    }

    private static Transform EnsureFolder(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing;
        }

        GameObject created = new GameObject(name);
        created.transform.SetParent(parent, worldPositionStays: false);
        return created.transform;
    }

    private static List<Transform> GetDirectChildren(Transform parent)
    {
        List<Transform> children = new List<Transform>();
        if (parent == null)
        {
            return children;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            children.Add(parent.GetChild(i));
        }

        return children;
    }

    private static void SortHierarchy(Transform organizerRoot)
    {
        SortDirectChildrenRecursive(organizerRoot);
    }

    private static void SortDirectChildrenRecursive(Transform root)
    {
        if (root == null)
        {
            return;
        }

        List<Transform> children = GetDirectChildren(root);
        children.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
        for (int i = 0; i < children.Count; i++)
        {
            children[i].SetSiblingIndex(i);
            SortDirectChildrenRecursive(children[i]);
        }
    }

    private static string SanitizeRoomName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return "Zone";
        }

        string sanitized = rawName.Trim()
            .Replace('/', '_')
            .Replace('\\', '_')
            .Replace(':', '_');

        while (sanitized.Contains("  ", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("  ", " ");
        }

        return sanitized.Length > 64 ? sanitized.Substring(0, 64) : sanitized;
    }

    private sealed class RoomMarkerInfo
    {
        public readonly Transform Transform;
        public string RoomName;
        public bool MoveMarker;
        public Bounds Bounds;
        public bool HasBounds;

        public RoomMarkerInfo(Transform transform, string roomName, bool moveMarker)
        {
            Transform = transform;
            RoomName = roomName;
            MoveMarker = moveMarker;
        }
    }

    private struct RefineStats
    {
        public int RenamedMarkers;
        public int MovedMarkers;
        public int MovedContentRoots;
        public int RemovedEmptyBuckets;

        public string Format()
        {
            return "Maison hierarchy refined: " +
                   $"renamedMarkers={RenamedMarkers}, movedMarkers={MovedMarkers}, " +
                   $"movedContentRoots={MovedContentRoots}, removedEmptyBuckets={RemovedEmptyBuckets}";
        }
    }
}
