using UnityEngine;

[DisallowMultipleComponent]
public class WaterMotion : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField, Min(0)] private int materialIndex;
    [SerializeField] private bool useSharedMaterial;
    [SerializeField] private string texturePropertyName = "_BaseColorMap";

    [Header("Offset")]
    [SerializeField] private bool useMaterialOffsetAtStart = true;
    [SerializeField] private Vector2 offsetStart;
    [SerializeField] private Vector2 offsetSpeed = new Vector2(0.02f, 0.012f);

    [Header("Tiling")]
    [SerializeField] private bool useMaterialTilingAtStart = true;
    [SerializeField] private Vector2 baseTiling = Vector2.one;
    [SerializeField] private Vector2 tilingPulseAmplitude = new Vector2(0.015f, 0.01f);
    [SerializeField] private Vector2 tilingPulseSpeed = new Vector2(0.04f, 0.03f);
    [SerializeField] private Vector2 tilingPulsePhase = new Vector2(0f, 0.25f);

    private const float MinTiling = 0.0001f;
    private static readonly string[] FallbackTextureProperties =
    {
        "_BaseColorMap",
        "_BaseMap",
        "_MainTex",
        "_AlbedoTex1",
        "_NormalMap1"
    };

    private Material targetMaterial;
    private string resolvedTexturePropertyName;
    private Vector2 initialOffset;
    private Vector2 initialTiling;
    private float startTime;
    private bool warnedMissingTarget;
    private bool warnedMissingTextureProperty;

    private void Reset()
    {
        targetRenderer = GetComponent<Renderer>();
    }

    private void Awake()
    {
        InitializeMaterial();
    }

    private void OnEnable()
    {
        if (targetMaterial == null)
        {
            InitializeMaterial();
        }

        startTime = Time.time;
    }

    private void Update()
    {
        if (targetMaterial == null && !InitializeMaterial())
        {
            return;
        }

        float elapsedTime = Time.time - startTime;
        Vector2 offset = initialOffset + offsetSpeed * elapsedTime;
        Vector2 tiling = new Vector2(
            initialTiling.x * (1f + Mathf.Sin((elapsedTime * tilingPulseSpeed.x + tilingPulsePhase.x) * Mathf.PI * 2f) * tilingPulseAmplitude.x),
            initialTiling.y * (1f + Mathf.Sin((elapsedTime * tilingPulseSpeed.y + tilingPulsePhase.y) * Mathf.PI * 2f) * tilingPulseAmplitude.y));

        tiling.x = Mathf.Max(MinTiling, tiling.x);
        tiling.y = Mathf.Max(MinTiling, tiling.y);

        targetMaterial.SetTextureOffset(resolvedTexturePropertyName, offset);
        targetMaterial.SetTextureScale(resolvedTexturePropertyName, tiling);
    }

    private bool InitializeMaterial()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        if (targetRenderer == null)
        {
            WarnMissingTarget("aucun Renderer trouve.");
            return false;
        }

        Material[] materials = useSharedMaterial ? targetRenderer.sharedMaterials : targetRenderer.materials;
        if (materials == null || materials.Length == 0)
        {
            WarnMissingTarget("le Renderer n'a pas de material.");
            return false;
        }

        if (materialIndex >= materials.Length)
        {
            WarnMissingTarget($"l'index de material {materialIndex} est invalide pour {materials.Length} slot(s).");
            return false;
        }

        targetMaterial = materials[materialIndex];
        if (targetMaterial == null)
        {
            WarnMissingTarget("le material cible est nul.");
            return false;
        }

        resolvedTexturePropertyName = ResolveTexturePropertyName(targetMaterial);
        if (string.IsNullOrEmpty(resolvedTexturePropertyName))
        {
            WarnMissingTextureProperty();
            targetMaterial = null;
            return false;
        }

        initialOffset = useMaterialOffsetAtStart ? targetMaterial.GetTextureOffset(resolvedTexturePropertyName) : offsetStart;
        initialTiling = useMaterialTilingAtStart ? targetMaterial.GetTextureScale(resolvedTexturePropertyName) : baseTiling;
        initialTiling.x = Mathf.Max(MinTiling, initialTiling.x);
        initialTiling.y = Mathf.Max(MinTiling, initialTiling.y);
        startTime = Time.time;

        return true;
    }

    private string ResolveTexturePropertyName(Material material)
    {
        if (!string.IsNullOrWhiteSpace(texturePropertyName) && material.HasProperty(texturePropertyName))
        {
            return texturePropertyName;
        }

        for (int i = 0; i < FallbackTextureProperties.Length; i++)
        {
            string fallbackPropertyName = FallbackTextureProperties[i];
            if (material.HasProperty(fallbackPropertyName))
            {
                return fallbackPropertyName;
            }
        }

        return null;
    }

    private void WarnMissingTarget(string reason)
    {
        if (warnedMissingTarget)
        {
            return;
        }

        warnedMissingTarget = true;
        Debug.LogWarning($"{nameof(WaterMotion)} on {name}: {reason}", this);
    }

    private void WarnMissingTextureProperty()
    {
        if (warnedMissingTextureProperty)
        {
            return;
        }

        warnedMissingTextureProperty = true;
        Debug.LogWarning($"{nameof(WaterMotion)} on {name}: aucune propriete de texture compatible trouvee sur {targetMaterial.name}.", this);
    }

    private void OnValidate()
    {
        materialIndex = Mathf.Max(0, materialIndex);
        tilingPulseAmplitude.x = Mathf.Max(0f, tilingPulseAmplitude.x);
        tilingPulseAmplitude.y = Mathf.Max(0f, tilingPulseAmplitude.y);
        tilingPulseSpeed.x = Mathf.Max(0f, tilingPulseSpeed.x);
        tilingPulseSpeed.y = Mathf.Max(0f, tilingPulseSpeed.y);
        baseTiling.x = Mathf.Max(MinTiling, baseTiling.x);
        baseTiling.y = Mathf.Max(MinTiling, baseTiling.y);
    }
}
