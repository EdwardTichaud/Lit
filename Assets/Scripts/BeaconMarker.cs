using UnityEngine;

[DisallowMultipleComponent]
public class BeaconMarker : MonoBehaviour
{
    [Header("Color")]
    [SerializeField] private Color markerColor = new Color(0.98f, 0.48f, 0.14f, 1f);

    [Header("Shape")]
    [SerializeField] private float discRadius = 0.1f;
    [SerializeField] private float discThickness = 0.025f;
    [SerializeField] private float stemRadius = 0.018f;
    [SerializeField] private float stemLength = 0.12f;
    [SerializeField] private float gemRadius = 0.032f;
    [SerializeField] private float surfaceOffset = 0.015f;

    [Header("Runtime")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Renderer discRenderer;
    [SerializeField] private Renderer stemRenderer;
    [SerializeField] private Renderer gemRenderer;
    [SerializeField] private Light[] pointLights;

    private MaterialPropertyBlock propertyBlock;

    public Color MarkerColor => markerColor;
    public float SurfaceOffset => Mathf.Max(0f, surfaceOffset);

    private void Awake()
    {
        EnsureVisuals();
        EnsurePropertyBlock();
        ApplyColor();
    }

    private void OnEnable()
    {
        EnsureVisuals();
        EnsurePropertyBlock();
        ApplyColor();
    }

    public void SetColor(Color color)
    {
        markerColor = color;
        EnsureVisuals();
        EnsurePropertyBlock();
        ApplyColor();
    }

    public static bool TrySetColor(GameObject target, Color color)
    {
        if (!TryFind(target, out BeaconMarker beacon))
        {
            return false;
        }

        beacon.SetColor(color);
        return true;
    }

    public static bool TryFind(GameObject target, out BeaconMarker beacon)
    {
        beacon = null;
        if (target == null)
        {
            return false;
        }

        beacon = target.GetComponent<BeaconMarker>();
        if (beacon == null)
        {
            beacon = target.GetComponentInChildren<BeaconMarker>(true);
        }

        return beacon != null;
    }

    private void EnsureVisuals()
    {
        if (visualRoot == null)
        {
            Transform existing = transform.Find("BeaconVisual");
            if (existing != null)
            {
                visualRoot = existing;
            }
        }

        if (visualRoot == null)
        {
            GameObject root = new GameObject("BeaconVisual");
            root.transform.SetParent(transform, false);
            visualRoot = root.transform;
        }

        if (discRenderer == null)
        {
            discRenderer = EnsurePrimitiveRenderer("Disc", PrimitiveType.Cylinder);
        }

        if (stemRenderer == null)
        {
            stemRenderer = EnsurePrimitiveRenderer("Stem", PrimitiveType.Cylinder);
        }

        if (gemRenderer == null)
        {
            gemRenderer = EnsurePrimitiveRenderer("Gem", PrimitiveType.Sphere);
        }

        LayoutVisuals();
        EnsureLights();
    }

    private Renderer EnsurePrimitiveRenderer(string name, PrimitiveType primitiveType)
    {
        if (visualRoot == null)
        {
            return null;
        }

        Transform existing = visualRoot.Find(name);
        GameObject target;
        if (existing != null)
        {
            target = existing.gameObject;
        }
        else
        {
            target = GameObject.CreatePrimitive(primitiveType);
            target.name = name;
            target.transform.SetParent(visualRoot, false);
        }

        Collider col = target.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
            Destroy(col);
        }

        return target.GetComponent<Renderer>();
    }

    private void LayoutVisuals()
    {
        if (visualRoot == null)
        {
            return;
        }

        float baseOffset = SurfaceOffset;
        float discHeight = Mathf.Max(0.002f, discThickness);
        float stemHeight = Mathf.Max(0.01f, stemLength);
        float discScaleXZ = Mathf.Max(0.01f, discRadius) * 2f;
        float discScaleY = discHeight * 0.5f;
        float stemScaleXZ = Mathf.Max(0.005f, stemRadius) * 2f;
        float stemScaleY = stemHeight * 0.5f;
        float gemScale = Mathf.Max(0.01f, gemRadius) * 2f;

        if (discRenderer != null)
        {
            Transform disc = discRenderer.transform;
            disc.localPosition = Vector3.up * (baseOffset + discHeight * 0.5f);
            disc.localRotation = Quaternion.identity;
            disc.localScale = new Vector3(discScaleXZ, discScaleY, discScaleXZ);
        }

        if (stemRenderer != null)
        {
            Transform stem = stemRenderer.transform;
            float stemCenter = baseOffset + discHeight + stemHeight * 0.5f;
            stem.localPosition = Vector3.up * stemCenter;
            stem.localRotation = Quaternion.identity;
            stem.localScale = new Vector3(stemScaleXZ, stemScaleY, stemScaleXZ);
        }

        if (gemRenderer != null)
        {
            Transform gem = gemRenderer.transform;
            float gemCenter = baseOffset + discHeight + stemHeight + Mathf.Max(0.01f, gemRadius);
            gem.localPosition = Vector3.up * gemCenter;
            gem.localRotation = Quaternion.identity;
            gem.localScale = Vector3.one * gemScale;
        }
    }

    private void ApplyColor()
    {
        EnsurePropertyBlock();
        ApplyColorToRenderer(discRenderer, markerColor * 0.85f);
        ApplyColorToRenderer(stemRenderer, markerColor * 0.7f);
        ApplyColorToRenderer(gemRenderer, markerColor);
        ApplyColorToLights(markerColor);
    }

    private void ApplyColorToRenderer(Renderer renderer, Color color)
    {
        if (renderer == null || renderer.sharedMaterial == null)
        {
            return;
        }

        EnsurePropertyBlock();
        if (propertyBlock == null)
        {
            return;
        }

        propertyBlock.Clear();
        if (renderer.sharedMaterial.HasProperty("_BaseColor"))
        {
            propertyBlock.SetColor("_BaseColor", color);
        }
        else if (renderer.sharedMaterial.HasProperty("_Color"))
        {
            propertyBlock.SetColor("_Color", color);
        }
        else
        {
            return;
        }

        renderer.SetPropertyBlock(propertyBlock);
    }

    private void EnsurePropertyBlock()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private void EnsureLights()
    {
        if (pointLights != null && pointLights.Length > 0)
        {
            return;
        }

        pointLights = GetComponentsInChildren<Light>(true);
    }

    private void ApplyColorToLights(Color color)
    {
        EnsureLights();
        if (pointLights == null || pointLights.Length == 0)
        {
            return;
        }

        for (int i = 0; i < pointLights.Length; i++)
        {
            Light target = pointLights[i];
            if (target == null || target.type != LightType.Point)
            {
                continue;
            }

            target.color = color;
        }
    }
}
