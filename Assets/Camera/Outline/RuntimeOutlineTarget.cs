using UnityEngine;

[DisallowMultipleComponent]
public class RuntimeOutlineTarget : MonoBehaviour
{
    [SerializeField] private bool outlined;

    private int originalLayer;
    private bool capturedOriginalLayer;
    private const string OutlineLayerName = "RuntimeOutline";

    private void Awake()
    {
        CaptureOriginalLayer();
        Apply();
    }

    private void OnEnable()
    {
        if (!capturedOriginalLayer)
        {
            CaptureOriginalLayer();
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
        if (!capturedOriginalLayer)
        {
            CaptureOriginalLayer();
        }

        int outlineLayer = LayerMask.NameToLayer(OutlineLayerName);

        if (outlineLayer < 0)
        {
            Debug.LogError("Layer RuntimeOutline introuvable.");
            return;
        }

        // Un RuntimeOutlineTarget ne possede que le Renderer pose sur son
        // propre GameObject. Il ne doit jamais modifier les layers de ses
        // enfants (cheveux, accessoires, VFX, etc.).
        gameObject.layer = outlined ? outlineLayer : originalLayer;
    }

    private void CaptureOriginalLayer()
    {
        originalLayer = gameObject.layer;
        capturedOriginalLayer = true;
    }
}
