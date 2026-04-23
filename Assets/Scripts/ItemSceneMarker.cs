using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[AddComponentMenu("Lit/Item/Scene Marker")]
public class ItemSceneMarker : MonoBehaviour
{
    public enum MarkerAssetType
    {
        Item = 0,
        Enemy = 1
    }

    [Tooltip("Type de ScriptableObject place par ce marker.")]
    public MarkerAssetType assetType = MarkerAssetType.Item;
    [Tooltip("Item utilise pour la preview et le bake en scene.")]
    public Item item;
    [Tooltip("CharacterData ennemi utilise pour la preview et le bake en scene.")]
    public CharacterData enemy;

    public Item Item => item;
    public CharacterData Enemy => enemy;
    public bool UsesEnemy => assetType == MarkerAssetType.Enemy;
    public bool UsesItem => assetType == MarkerAssetType.Item;

    public void SetAssetType(MarkerAssetType type)
    {
        assetType = type;
    }

    public GameObject ResolvePreviewPrefab()
    {
        if (UsesEnemy)
        {
            return enemy != null ? enemy.ResolveWorldPrefab() : null;
        }

        return item != null ? item.ResolveWorldPrefab() : null;
    }

#if UNITY_EDITOR
    private static readonly Color PreviewColor = new Color(0.15f, 0.85f, 1f, 0.95f);
    private static readonly Color SelectedPreviewColor = new Color(1f, 0.65f, 0.15f, 1f);

    private void OnDrawGizmos()
    {
        DrawPreviewGizmo(Selection.Contains(gameObject));
    }

    private void DrawPreviewGizmo(bool isSelected)
    {
        GameObject prefab = ResolvePreviewPrefab();
        if (prefab == null)
        {
            return;
        }

        Color previousColor = Gizmos.color;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.color = isSelected ? SelectedPreviewColor : PreviewColor;

        bool drewMesh = DrawMeshPreview(prefab.transform);
        if (!drewMesh)
        {
            DrawBoundsPreview(prefab.transform);
        }

        Gizmos.color = previousColor;
        Gizmos.matrix = previousMatrix;
    }

    private bool DrawMeshPreview(Transform prefabRoot)
    {
        bool drewMesh = false;
        Matrix4x4 previewMatrix = GetPreviewRootMatrix(prefabRoot);
        MeshFilter[] meshFilters = prefabRoot.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            Gizmos.matrix = previewMatrix * GetRelativeMatrix(prefabRoot, meshFilter.transform);
            Gizmos.DrawWireMesh(meshFilter.sharedMesh);
            drewMesh = true;
        }

        SkinnedMeshRenderer[] skinnedRenderers = prefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinnedRenderers.Length; i++)
        {
            SkinnedMeshRenderer skinnedRenderer = skinnedRenderers[i];
            if (skinnedRenderer == null || skinnedRenderer.sharedMesh == null)
            {
                continue;
            }

            Gizmos.matrix = previewMatrix * GetRelativeMatrix(prefabRoot, skinnedRenderer.transform);
            Gizmos.DrawWireMesh(skinnedRenderer.sharedMesh);
            drewMesh = true;
        }

        return drewMesh;
    }

    private void DrawBoundsPreview(Transform prefabRoot)
    {
        if (!TryCalculateLocalPreviewBounds(prefabRoot.gameObject, out Bounds localBounds))
        {
            return;
        }

        Gizmos.matrix = GetPreviewRootMatrix(prefabRoot);
        Gizmos.DrawWireCube(localBounds.center, localBounds.size);
    }

    private Matrix4x4 GetPreviewRootMatrix(Transform prefabRoot)
    {
        Vector3 rootScale = prefabRoot != null ? prefabRoot.localScale : Vector3.one;
        Quaternion rootRotation = prefabRoot != null ? prefabRoot.localRotation : Quaternion.identity;
        return transform.localToWorldMatrix
            * Matrix4x4.Rotate(rootRotation)
            * Matrix4x4.Scale(rootScale);
    }

    private static Matrix4x4 GetRelativeMatrix(Transform root, Transform current)
    {
        if (root == null || current == null)
        {
            return Matrix4x4.identity;
        }

        return root.worldToLocalMatrix * current.localToWorldMatrix;
    }

    private static bool TryCalculateLocalPreviewBounds(GameObject prefabRootObject, out Bounds localBounds)
    {
        localBounds = new Bounds(Vector3.zero, Vector3.zero);
        if (prefabRootObject == null)
        {
            return false;
        }

        Transform prefabRoot = prefabRootObject.transform;
        bool hasBounds = false;

        MeshFilter[] meshFilters = prefabRoot.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            EncapsulateBounds(prefabRoot, meshFilter.transform, meshFilter.sharedMesh.bounds, ref hasBounds, ref localBounds);
        }

        SkinnedMeshRenderer[] skinnedRenderers = prefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinnedRenderers.Length; i++)
        {
            SkinnedMeshRenderer skinnedRenderer = skinnedRenderers[i];
            if (skinnedRenderer == null)
            {
                continue;
            }

            Bounds sourceBounds = skinnedRenderer.localBounds;
            if (sourceBounds.size == Vector3.zero && skinnedRenderer.sharedMesh != null)
            {
                sourceBounds = skinnedRenderer.sharedMesh.bounds;
            }

            if (sourceBounds.size == Vector3.zero)
            {
                continue;
            }

            EncapsulateBounds(prefabRoot, skinnedRenderer.transform, sourceBounds, ref hasBounds, ref localBounds);
        }

        if (!hasBounds)
        {
            Collider[] colliders = prefabRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (!TryGetColliderBounds(collider, out Bounds colliderBounds))
                {
                    continue;
                }

                EncapsulateBounds(prefabRoot, collider.transform, colliderBounds, ref hasBounds, ref localBounds);
            }
        }

        if (hasBounds && localBounds.size == Vector3.zero)
        {
            localBounds.size = Vector3.one * 0.1f;
        }

        return hasBounds;
    }

    private static bool TryGetColliderBounds(Collider collider, out Bounds localBounds)
    {
        localBounds = new Bounds(Vector3.zero, Vector3.zero);
        if (collider == null)
        {
            return false;
        }

        if (collider is BoxCollider boxCollider)
        {
            localBounds = new Bounds(boxCollider.center, boxCollider.size);
            return true;
        }

        if (collider is SphereCollider sphereCollider)
        {
            float diameter = sphereCollider.radius * 2f;
            localBounds = new Bounds(sphereCollider.center, Vector3.one * diameter);
            return true;
        }

        if (collider is CapsuleCollider capsuleCollider)
        {
            float diameter = capsuleCollider.radius * 2f;
            Vector3 size = Vector3.one * diameter;
            switch (capsuleCollider.direction)
            {
                case 0:
                    size.x = Mathf.Max(diameter, capsuleCollider.height);
                    break;
                case 2:
                    size.z = Mathf.Max(diameter, capsuleCollider.height);
                    break;
                default:
                    size.y = Mathf.Max(diameter, capsuleCollider.height);
                    break;
            }

            localBounds = new Bounds(capsuleCollider.center, size);
            return true;
        }

        if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
        {
            localBounds = meshCollider.sharedMesh.bounds;
            return true;
        }

        return false;
    }

    private static void EncapsulateBounds(
        Transform prefabRoot,
        Transform boundsTransform,
        Bounds sourceBounds,
        ref bool hasBounds,
        ref Bounds combinedBounds)
    {
        Matrix4x4 relativeMatrix = GetRelativeMatrix(prefabRoot, boundsTransform);
        Vector3 center = sourceBounds.center;
        Vector3 extents = sourceBounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 localCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 relativeCorner = relativeMatrix.MultiplyPoint3x4(localCorner);
                    if (!hasBounds)
                    {
                        combinedBounds = new Bounds(relativeCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(relativeCorner);
                    }
                }
            }
        }
    }
#endif
}
