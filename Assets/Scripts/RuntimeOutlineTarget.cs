using UnityEngine;

[DisallowMultipleComponent]
public class RuntimeOutlineTarget : MonoBehaviour
{
    [SerializeField] private bool outlined;

    private int originalLayer;
    private const string OutlineLayerName = "RuntimeOutline";

    private void Awake()
    {
        originalLayer = gameObject.layer;
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
        int outlineLayer = LayerMask.NameToLayer(OutlineLayerName);

        if (outlineLayer < 0)
        {
            Debug.LogError("Layer RuntimeOutline introuvable.");
            return;
        }

        SetLayerRecursively(gameObject, outlined ? outlineLayer : originalLayer);
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}