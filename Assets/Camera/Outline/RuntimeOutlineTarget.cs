using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class RuntimeOutlineTarget : MonoBehaviour
{
    [SerializeField] private bool outlined;

    private readonly Dictionary<GameObject, int> originalLayers = new Dictionary<GameObject, int>();
    private bool capturedOriginalLayers;
    private const string OutlineLayerName = "RuntimeOutline";

    private void Awake()
    {
        CaptureOriginalLayers();
        Apply();
    }

    private void OnEnable()
    {
        if (!capturedOriginalLayers)
        {
            CaptureOriginalLayers();
        }

        Apply();
    }

    public void SetOutlined(bool value)
    {
        outlined = value;
        Apply();
    }

    public bool IsOutlined()
    {
        return outlined;
    }

    private void Apply()
    {
        if (!capturedOriginalLayers)
        {
            CaptureOriginalLayers();
        }

        int outlineLayer = LayerMask.NameToLayer(OutlineLayerName);

        if (outlineLayer < 0)
        {
            Debug.LogError("Layer RuntimeOutline introuvable.");
            return;
        }

        if (outlined)
        {
            SetLayerRecursively(gameObject, outlineLayer);
            return;
        }

        RestoreLayersRecursively(gameObject);
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void RestoreLayersRecursively(GameObject obj)
    {
        if (obj == null)
        {
            return;
        }

        if (originalLayers.TryGetValue(obj, out int layer))
        {
            obj.layer = layer;
        }

        foreach (Transform child in obj.transform)
        {
            RestoreLayersRecursively(child.gameObject);
        }
    }

    private void CaptureOriginalLayers()
    {
        originalLayers.Clear();
        CaptureOriginalLayersRecursively(gameObject);
        capturedOriginalLayers = true;
    }

    private void CaptureOriginalLayersRecursively(GameObject obj)
    {
        if (obj == null)
        {
            return;
        }

        originalLayers[obj] = obj.layer;

        foreach (Transform child in obj.transform)
        {
            CaptureOriginalLayersRecursively(child.gameObject);
        }
    }
}
