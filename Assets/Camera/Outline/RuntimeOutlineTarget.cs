using UnityEngine;

[DisallowMultipleComponent]
public class RuntimeOutlineTarget : MonoBehaviour
{
    public enum OpacityChannel { Automatic, Alpha, Red, Opaque }
    [SerializeField, Tooltip("Automatique : respecte la transparence du materiau. Rouge pour les textures dont le masque est en niveaux de gris.")]
    private OpacityChannel opacityChannel;
    [SerializeField, Tooltip("Nom de la propriete texture pour un shader personnalise. Vide = detection automatique.")]
    private string opacityTextureProperty;
    [SerializeField] private bool outlined;
    private Renderer targetRenderer;
    private MaterialPropertyBlock opacityBlock;
    private readonly System.Collections.Generic.List<Material> materials = new System.Collections.Generic.List<Material>();

    private void LateUpdate()
    {
        if (outlined) RefreshOpacity();
    }

    private void RefreshOpacity()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        if (targetRenderer == null) return;
        if (opacityBlock == null) opacityBlock = new MaterialPropertyBlock();
        targetRenderer.GetSharedMaterials(materials);
        for (int i = 0; i < materials.Count; i++)
        {
            Material source = materials[i];
            if (source == null) continue;
            // Merge into the existing block, preserving unrelated runtime overrides.
            targetRenderer.GetPropertyBlock(opacityBlock, i);
            bool useRendererBlock = materials.Count == 1 && opacityBlock.isEmpty;
            if (opacityBlock.isEmpty) targetRenderer.GetPropertyBlock(opacityBlock);
            bool particle = targetRenderer is ParticleSystemRenderer;
            bool transparent = particle || source.renderQueue >= 2450 ||
                (source.HasProperty("_SurfaceType") && source.GetFloat("_SurfaceType") > 0f) ||
                source.IsKeywordEnabled("_ALPHATEST_ON");
            bool useAlpha = opacityChannel != OpacityChannel.Opaque &&
                (opacityChannel != OpacityChannel.Automatic || transparent);
            // This project's GlitteringStars graph connects SampleTexture.RGBA to
            // scalar Alpha, which reads R. Its HDR glow color is not opacity.
            bool red = opacityChannel == OpacityChannel.Red ||
                (opacityChannel == OpacityChannel.Automatic && source.shader.name.Contains("GlitteringStars"));
            string property = opacityTextureProperty;
            if (string.IsNullOrEmpty(property))
                property = source.HasProperty("_BaseColorMap") ? "_BaseColorMap" :
                    source.HasProperty("_BaseMap") ? "_BaseMap" :
                    source.HasProperty("_UnlitColorMap") ? "_UnlitColorMap" : "_MainTex";
            Texture texture = Texture2D.whiteTexture;
            Vector2 scale = Vector2.one, offset = Vector2.zero;
            if (source.HasProperty(property))
            {
                texture = opacityBlock.GetTexture(property) ?? source.GetTexture(property) ?? Texture2D.whiteTexture;
                scale = source.GetTextureScale(property);
                offset = source.GetTextureOffset(property);
            }
            Vector4 st = new Vector4(scale.x, scale.y, offset.x, offset.y);
            if (opacityBlock.HasVector(property + "_ST")) st = opacityBlock.GetVector(property + "_ST");
            string colorProperty = source.HasProperty("_BaseColor") ? "_BaseColor" :
                source.HasProperty("_UnlitColor") ? "_UnlitColor" : "_Color";
            float alpha = source.HasProperty(colorProperty) ? source.GetColor(colorProperty).a : 1f;
            if (opacityBlock.HasColor(colorProperty)) alpha = opacityBlock.GetColor(colorProperty).a;
            opacityBlock.SetTexture("_RuntimeOutlineTexture", texture);
            opacityBlock.SetVector("_RuntimeOutlineTexture_ST", st);
            opacityBlock.SetFloat("_RuntimeOutlineOpacity", alpha);
            opacityBlock.SetFloat("_RuntimeOutlineAlphaEnabled", useAlpha ? 1f : 0f);
            opacityBlock.SetFloat("_RuntimeOutlineRedChannel", red ? 1f : 0f);
            opacityBlock.SetFloat("_RuntimeOutlineVertexAlpha", particle ? 1f : 0f);
            if (useRendererBlock) targetRenderer.SetPropertyBlock(opacityBlock);
            else targetRenderer.SetPropertyBlock(opacityBlock, i);
        }
    }

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
        if (outlined) RefreshOpacity();
    }

    private void CaptureOriginalLayer()
    {
        originalLayer = gameObject.layer;
        capturedOriginalLayer = true;
    }
}
